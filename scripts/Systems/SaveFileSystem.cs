#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OregonTrail2026.Models;
using GodotFileAccess = Godot.FileAccess;
using SysFileAccess = System.IO.FileAccess;

namespace OregonTrail2026.Systems;

/// <summary>
/// Encrypted save file system using ZIP archives.
///
/// Each save slot is a single .ot26 file (ZIP archive) containing:
///   meta.json    - unencrypted display info for the load screen
///   current.enc  - GZip(JSON) encrypted with AES-256-CBC, HMAC-signed
///   backup.enc   - previous current.enc (for corruption recovery)
///
/// .enc binary format:
///   [16 bytes]  IV (random per write)
///   [32 bytes]  HMAC-SHA256 of the compressed plaintext
///   [N  bytes]  AES-256-CBC ciphertext of GZip-compressed JSON
///
/// Slot layout:
///   10 manual slots (0-9) + 1 auto-save ("auto")
///   Manual slots can be renamed and deleted.
///   Auto-save overwrites silently at forts, hunt, and fish locations.
///
/// Threat model:
///   Defends against casual file editing (Notepad, hex editor).
///   Does NOT defend against memory scanners or IL decompilation.
///   Debug menu (Patreon perk) writes through the same path.
///
/// Dependencies: System.Security.Cryptography, System.IO.Compression,
///   System.Text.Json. All built into .NET 8. Zero NuGet packages.
/// </summary>
public static class SaveFileSystem
{
    // ========================================================================
    // CONSTANTS
    // ========================================================================

    public const int ManualSlotCount = 10;
    public const string AutoSlotId = "auto";
    private const string SaveDir = "user://saves";
    private const string FileExtension = ".ot26";
    private const string MetaEntry = "meta.json";
    private const string CurrentEntry = "current.enc";
    private const string BackupEntry = "backup.enc";

    // ---- Key derivation components (split across the file to resist grep) ----
    // These are NOT secret in any cryptographic sense. They raise the bar
    // above "open file in Notepad." A determined attacker who decompiles the
    // IL will find them. That's accepted in our threat model.
    private static readonly byte[] _saltA = Encoding.UTF8.GetBytes("OregonTrail2026::Willamette");
    private static readonly byte[] _saltB = Encoding.UTF8.GetBytes("IndependenceMO::1850::Dispatch");
    // Assembled at runtime, never as a single literal in the binary.
    private static byte[] BuildSaltBlock() =>
        _saltA.Concat(new byte[] { 0x4F, 0x54, 0x32, 0x36 }).Concat(_saltB).ToArray();

    // ========================================================================
    // PUBLIC API
    // ========================================================================

    /// <summary>
    /// Whitelist-validate a slot identifier before it touches the filesystem.
    /// Valid values: single digit "0"-"9" for manual slots, "auto" for auto-save.
    /// Anything else is rejected to prevent path traversal or key confusion.
    /// </summary>
    private static void ValidateSlotId(string slotId)
    {
        bool valid = slotId == AutoSlotId ||
                     (slotId.Length == 1 && slotId[0] >= '0' && slotId[0] <= '9');
        if (!valid)
            throw new ArgumentException($"Invalid slot ID '{slotId}'. Must be 0-9 or 'auto'.");
    }

    /// <summary>
    /// Save the current GameState to the specified slot.
    /// Rotates current -> backup before writing.
    /// </summary>
    /// <param name="slotId">Slot identifier: "0"-"9" for manual, "auto" for auto-save.</param>
    /// <param name="state">The GameState to persist.</param>
    /// <param name="slotName">Display name for this slot (manual saves only).</param>
    /// <returns>True if the save succeeded.</returns>
    public static bool Save(string slotId, GameState state, string slotName = "")
    {
        try
        {
            ValidateSlotId(slotId);
            string path = GetSlotPath(slotId);
            EnsureSaveDir();

            // Serialize and compress
            string json = state.ToJson();
            byte[] compressed = GZipCompress(Encoding.UTF8.GetBytes(json));

            // Derive key for this slot
            byte[] key = DeriveKey(slotId);

            // Encrypt
            byte[] encPayload = Encrypt(compressed, key);

            // Build metadata
            if (string.IsNullOrWhiteSpace(slotName))
                slotName = slotId == AutoSlotId ? "AUTO-SAVE" : $"Save Slot {slotId}";
            var meta = SaveSlotMeta.FromGameState(state, slotName);
            string metaJson = JsonSerializer.Serialize(meta, _metaJsonOptions);

            // Write ZIP archive to a temp file, then move (atomic-ish on Windows)
            string tempPath = path + ".tmp";
            string globalTemp = ProjectSettings.GlobalizePath(tempPath);
            string globalFinal = ProjectSettings.GlobalizePath(path);

            using (var fs = new FileStream(globalTemp, FileMode.Create, SysFileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                // If an existing archive has current.enc, copy it to backup first
                if (GodotFileAccess.FileExists(path))
                {
                    byte[]? existingCurrent = ReadEntryFromExistingArchive(globalFinal, CurrentEntry);
                    if (existingCurrent != null)
                    {
                        var backupE = zip.CreateEntry(BackupEntry, CompressionLevel.NoCompression);
                        using var bw = backupE.Open();
                        bw.Write(existingCurrent);
                    }
                }

                // Write current.enc (no ZIP compression; data is already encrypted/random)
                var currentE = zip.CreateEntry(CurrentEntry, CompressionLevel.NoCompression);
                using (var cw = currentE.Open())
                    cw.Write(encPayload);

                // Write meta.json (allow ZIP compression; it's small readable text)
                var metaE = zip.CreateEntry(MetaEntry, CompressionLevel.Fastest);
                using (var mw = new StreamWriter(metaE.Open(), Encoding.UTF8))
                    mw.Write(metaJson);
            }

            // Atomic replace: delete old, rename temp
            if (File.Exists(globalFinal))
                File.Delete(globalFinal);
            File.Move(globalTemp, globalFinal);

            GD.Print($"[SaveSystem] Saved slot '{slotId}' ({compressed.Length} bytes compressed, {encPayload.Length} bytes encrypted)");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SaveSystem] Save failed for slot '{slotId}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load a GameState from the specified slot.
    /// Attempts current.enc first, falls back to backup.enc on integrity failure.
    /// </summary>
    /// <returns>Tuple of (GameState?, errorMessage). State is null on failure.</returns>
    public static (GameState? State, string Message) Load(string slotId)
    {
        ValidateSlotId(slotId);
        string path = GetSlotPath(slotId);
        if (!GodotFileAccess.FileExists(path))
            return (null, "Save file not found.");

        string globalPath = ProjectSettings.GlobalizePath(path);
        byte[] key = DeriveKey(slotId);

        // Try current.enc
        var (state, currentErr) = TryLoadEntry(globalPath, CurrentEntry, key);
        if (state != null)
        {
            GD.Print($"[SaveSystem] Loaded slot '{slotId}' from current save.");
            return (state, "OK");
        }

        // Current failed. Try backup.enc
        GD.PrintErr($"[SaveSystem] Current save corrupted for slot '{slotId}': {currentErr}");
        var (backupState, backupErr) = TryLoadEntry(globalPath, BackupEntry, key);
        if (backupState != null)
        {
            GD.Print($"[SaveSystem] Recovered slot '{slotId}' from backup.");
            return (backupState, "RECOVERED FROM BACKUP. Your most recent progress may be lost.");
        }

        GD.PrintErr($"[SaveSystem] Backup also failed for slot '{slotId}': {backupErr}");
        return (null, $"Save corrupted beyond recovery.\nCurrent: {currentErr}\nBackup: {backupErr}");
    }

    /// <summary>Read metadata for a slot without decrypting the game state.</summary>
    public static SaveSlotMeta? ReadMeta(string slotId)
    {
        ValidateSlotId(slotId);
        string path = GetSlotPath(slotId);
        if (!GodotFileAccess.FileExists(path))
            return null;

        try
        {
            string globalPath = ProjectSettings.GlobalizePath(path);
            using var fs = new FileStream(globalPath, FileMode.Open, SysFileAccess.Read);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = zip.GetEntry(MetaEntry);
            if (entry == null) return null;

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            string json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<SaveSlotMeta>(json, _metaJsonOptions);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SaveSystem] Failed to read meta for slot '{slotId}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Get metadata for all slots (manual + auto). Null entries = empty slot.</summary>
    public static Dictionary<string, SaveSlotMeta?> ListAllSlots()
    {
        var result = new Dictionary<string, SaveSlotMeta?>();
        for (int i = 0; i < ManualSlotCount; i++)
            result[i.ToString()] = ReadMeta(i.ToString());
        result[AutoSlotId] = ReadMeta(AutoSlotId);
        return result;
    }

    /// <summary>Rename a manual save slot.</summary>
    public static bool RenameSlot(string slotId, string newName)
    {
        ValidateSlotId(slotId);
        if (slotId == AutoSlotId) return false; // can't rename auto-save

        string path = GetSlotPath(slotId);
        if (!GodotFileAccess.FileExists(path)) return false;

        try
        {
            string globalPath = ProjectSettings.GlobalizePath(path);

            // Read existing meta
            var meta = ReadMeta(slotId);
            if (meta == null) return false;
            meta.SlotName = newName;
            string metaJson = JsonSerializer.Serialize(meta, _metaJsonOptions);

            // Rewrite meta.json inside the ZIP
            // ZipArchiveMode.Update allows modifying entries in place
            using var fs = new FileStream(globalPath, FileMode.Open, SysFileAccess.ReadWrite);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Update);

            // Delete old meta entry and write new one
            var oldEntry = zip.GetEntry(MetaEntry);
            oldEntry?.Delete();

            var newEntry = zip.CreateEntry(MetaEntry, CompressionLevel.Fastest);
            using (var writer = new StreamWriter(newEntry.Open(), Encoding.UTF8))
                writer.Write(metaJson);

            GD.Print($"[SaveSystem] Renamed slot '{slotId}' to '{newName}'");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SaveSystem] Rename failed for slot '{slotId}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Delete a save slot entirely.</summary>
    public static bool DeleteSlot(string slotId)
    {
        ValidateSlotId(slotId);
        string path = GetSlotPath(slotId);
        if (!GodotFileAccess.FileExists(path)) return true; // already gone

        try
        {
            string globalPath = ProjectSettings.GlobalizePath(path);
            File.Delete(globalPath);
            GD.Print($"[SaveSystem] Deleted slot '{slotId}'");
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SaveSystem] Delete failed for slot '{slotId}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Check if a slot has a save file.</summary>
    public static bool SlotExists(string slotId)
    {
        ValidateSlotId(slotId);
        return GodotFileAccess.FileExists(GetSlotPath(slotId));
    }

    // ========================================================================
    // AUTO-SAVE TRIGGERS (called by GameManager)
    // ========================================================================

    /// <summary>
    /// Auto-save the current game state. Called at fort arrival,
    /// hunt start, and fish start. Uses the dedicated auto slot.
    /// </summary>
    public static void AutoSave(GameState state)
    {
        Save(AutoSlotId, state, "AUTO-SAVE");
    }

    // ========================================================================
    // ENCRYPTION / DECRYPTION
    // ========================================================================

    /// <summary>
    /// Encrypt compressed bytes: generates random IV, computes HMAC of plaintext,
    /// produces [IV(16) | HMAC(32) | Ciphertext(N)].
    /// </summary>
    private static byte[] Encrypt(byte[] compressed, byte[] key)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.GenerateIV();

        byte[] iv = aes.IV;
        byte[] hmac = ComputeHmac(compressed, key);

        using var ms = new MemoryStream();
        ms.Write(iv, 0, 16);
        ms.Write(hmac, 0, 32);

        using (var encryptor = aes.CreateEncryptor())
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(compressed, 0, compressed.Length);
            cs.FlushFinalBlock();
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Decrypt and verify: reads [IV(16) | HMAC(32) | Ciphertext(N)],
    /// decrypts, verifies HMAC against compressed bytes, then decompresses.
    /// HMAC is checked before decompression so a tampered payload cannot
    /// trigger a decompression bomb.
    /// Throws on tamper detection or corruption.
    /// </summary>
    private static byte[] DecryptAndVerify(byte[] payload, byte[] key)
    {
        if (payload.Length < 49) // 16 IV + 32 HMAC + at least 1 byte ciphertext
            throw new InvalidDataException("Payload too short to contain a valid save.");

        // Extract IV
        byte[] iv = new byte[16];
        Array.Copy(payload, 0, iv, 0, 16);

        // Extract stored HMAC
        byte[] storedHmac = new byte[32];
        Array.Copy(payload, 16, storedHmac, 0, 32);

        // Extract ciphertext
        int cipherLen = payload.Length - 48;
        byte[] ciphertext = new byte[cipherLen];
        Array.Copy(payload, 48, ciphertext, 0, cipherLen);

        // Decrypt
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;

        byte[] compressed;
        using (var ms = new MemoryStream(ciphertext))
        using (var decryptor = aes.CreateDecryptor())
        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
        using (var output = new MemoryStream())
        {
            cs.CopyTo(output);
            compressed = output.ToArray();
        }

        // Verify HMAC (constant-time comparison) BEFORE decompressing.
        // This prevents a crafted payload from causing a decompression bomb
        // against a file that hasn't passed integrity verification.
        byte[] computedHmac = ComputeHmac(compressed, key);
        if (!CryptographicOperations.FixedTimeEquals(storedHmac, computedHmac))
            throw new InvalidDataException("HMAC mismatch: save data has been tampered with or is corrupted.");

        return compressed;
    }

    // ========================================================================
    // KEY DERIVATION
    // ========================================================================

    /// <summary>
    /// Derive a 256-bit AES key from the slot identifier.
    /// Uses PBKDF2: slot-specific password, fixed salt, 100k iterations, SHA256.
    /// Each slot gets a unique key, preventing cross-slot copy attacks.
    /// </summary>
    private static byte[] DeriveKey(string slotId)
    {
        // password: slot-specific token (the secret differentiator per slot)
        // salt: fixed application constant (not secret, raises bar vs generic attacks)
        byte[] password = Encoding.UTF8.GetBytes($"slot::{slotId}::key");
        byte[] salt = BuildSaltBlock();

        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            iterations: 100_000,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(32);
    }

    /// <summary>HMAC-SHA256 using the same key as encryption.</summary>
    private static byte[] ComputeHmac(byte[] data, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    // ========================================================================
    // COMPRESSION
    // ========================================================================

    private static byte[] GZipCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(data, 0, data.Length);
        return output.ToArray();
    }

    /// <summary>
    /// Decompress GZip data with a hard cap on output size.
    /// A 50 MB limit is generous for any real save file (typical saves are
    /// under 100 KB compressed) and prevents decompression bomb DoS.
    /// Called only after HMAC verification passes, so this is a second
    /// layer of defense against corrupted-but-not-tampered data.
    /// </summary>
    private static byte[] GZipDecompress(byte[] data, int maxBytes = 50 * 1024 * 1024)
    {
        using var input = new MemoryStream(data);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = gz.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + bytesRead > maxBytes)
                throw new InvalidDataException(
                    $"Decompressed save exceeds {maxBytes / (1024 * 1024)} MB safety limit.");
            output.Write(buffer, 0, bytesRead);
        }

        return output.ToArray();
    }

    // ========================================================================
    // ZIP HELPERS
    // ========================================================================

    /// <summary>Try to load and decrypt a specific entry from a save archive.</summary>
    private static (GameState? State, string Error) TryLoadEntry(
        string archivePath, string entryName, byte[] key)
    {
        try
        {
            using var fs = new FileStream(archivePath, FileMode.Open, SysFileAccess.Read);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = zip.GetEntry(entryName);
            if (entry == null)
                return (null, $"Entry '{entryName}' not found in archive.");

            byte[] payload;
            using (var es = entry.Open())
            using (var ms = new MemoryStream())
            {
                es.CopyTo(ms);
                payload = ms.ToArray();
            }

            byte[] compressed = DecryptAndVerify(payload, key);
            byte[] jsonBytes = GZipDecompress(compressed);
            string json = Encoding.UTF8.GetString(jsonBytes);
            var state = GameState.FromJson(json);
            return (state, "OK");
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>Read a single entry's raw bytes from an existing archive on disk.</summary>
    private static byte[]? ReadEntryFromExistingArchive(string archivePath, string entryName)
    {
        try
        {
            using var fs = new FileStream(archivePath, FileMode.Open, SysFileAccess.Read);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = zip.GetEntry(entryName);
            if (entry == null) return null;

            using var es = entry.Open();
            using var ms = new MemoryStream();
            es.CopyTo(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    // ========================================================================
    // PATH HELPERS
    // ========================================================================

    private static string GetSlotPath(string slotId) =>
        $"{SaveDir}/slot_{slotId}{FileExtension}";

    private static void EnsureSaveDir()
    {
        // Godot's DirAccess handles user:// paths correctly
        using var dir = DirAccess.Open("user://");
        if (dir != null && !dir.DirExists("saves"))
            dir.MakeDir("saves");
    }

    // ========================================================================
    // JSON OPTIONS (for meta.json only; GameState uses its own)
    // ========================================================================

    private static readonly JsonSerializerOptions _metaJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };
}
