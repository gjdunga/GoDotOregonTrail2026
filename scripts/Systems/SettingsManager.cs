#nullable enable
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using OregonTrail2026.Utils;
using GodotFileAccess = Godot.FileAccess;

namespace OregonTrail2026.Systems;

/// <summary>
/// Persistent settings manager (autoload singleton).
///
/// Persists to user://settings.json. Controls:
///   - Audio: master/music/sfx volume via AudioServer buses
///   - Video: fullscreen toggle, window scale
///   - Language: TranslationServer locale
///   - Internal: debug authorization state (not exposed in menus)
///
/// Register as autoload in project.godot:
///   [autoload]
///   SettingsManager="*res://scripts/Systems/SettingsManager.cs"
/// </summary>
public partial class SettingsManager : Node
{
    public static SettingsManager Instance { get; private set; } = null!;

    private const string SettingsPath = "user://settings.json";

    // ========================================================================
    // SETTINGS DATA
    // ========================================================================

    private SettingsData _data = new();

    public float MasterVolume
    {
        get => _data.MasterVolume;
        set { _data.MasterVolume = Math.Clamp(value, 0f, 1f); ApplyAudio(); Save(); }
    }

    public float MusicVolume
    {
        get => _data.MusicVolume;
        set { _data.MusicVolume = Math.Clamp(value, 0f, 1f); ApplyAudio(); Save(); }
    }

    public float SfxVolume
    {
        get => _data.SfxVolume;
        set { _data.SfxVolume = Math.Clamp(value, 0f, 1f); ApplyAudio(); Save(); }
    }

    public bool Fullscreen
    {
        get => _data.Fullscreen;
        set { _data.Fullscreen = value; ApplyWindowMode(); Save(); }
    }

    public string Language
    {
        get => _data.Language;
        set { _data.Language = value; ApplyLanguage(); Save(); }
    }

    /// <summary>
    /// Debug authorization. Set internally by DevConsole password check.
    /// Not exposed in any user-facing menu.
    /// </summary>
    public bool DebugUnlocked
    {
        get => _data.DebugUnlocked;
        set { _data.DebugUnlocked = value; Save(); }
    }

    // ========================================================================
    // LIFECYCLE
    // ========================================================================

    public override void _Ready()
    {
        Instance = this;
        TranslationLoader.LoadCsv();
        Load();
        ApplyAll();
        GD.Print("[SettingsManager] Initialized.");
    }

    // ========================================================================
    // APPLY SETTINGS
    // ========================================================================

    private void ApplyAll()
    {
        ApplyAudio();
        ApplyWindowMode();
        ApplyLanguage();
    }

    private void ApplyAudio()
    {
        // Godot default buses: Master (0), Music (1), SFX (2)
        // Ensure buses exist before setting. If they don't, we just use Master.
        SetBusVolume(0, _data.MasterVolume); // Master

        int musicIdx = AudioServer.GetBusIndex("Music");
        if (musicIdx >= 0) SetBusVolume(musicIdx, _data.MusicVolume);

        int sfxIdx = AudioServer.GetBusIndex("SFX");
        if (sfxIdx >= 0) SetBusVolume(sfxIdx, _data.SfxVolume);
    }

    private static void SetBusVolume(int busIdx, float linear)
    {
        if (linear <= 0.001f)
        {
            AudioServer.SetBusMute(busIdx, true);
        }
        else
        {
            AudioServer.SetBusMute(busIdx, false);
            // Convert linear 0-1 to dB (Godot uses dB for bus volume)
            float db = Mathf.LinearToDb(linear);
            AudioServer.SetBusVolumeDb(busIdx, db);
        }
    }

    private void ApplyWindowMode()
    {
        if (_data.Fullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        }
        else
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            // Center the window after leaving fullscreen
            var screenSize = DisplayServer.ScreenGetSize();
            var winSize = DisplayServer.WindowGetSize();
            var pos = (screenSize - winSize) / 2;
            DisplayServer.WindowSetPosition(pos);
        }
    }

    private void ApplyLanguage()
    {
        if (!string.IsNullOrEmpty(_data.Language))
        {
            TranslationServer.SetLocale(_data.Language);
        }
    }

    // ========================================================================
    // PERSISTENCE
    // ========================================================================

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, _jsonOptions);
            using var file = GodotFileAccess.Open(SettingsPath, GodotFileAccess.ModeFlags.Write);
            file?.StoreString(json);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SettingsManager] Save failed: {ex.Message}");
        }
    }

    private void Load()
    {
        if (!GodotFileAccess.FileExists(SettingsPath))
        {
            GD.Print("[SettingsManager] No settings file, using defaults.");
            return;
        }

        try
        {
            using var file = GodotFileAccess.Open(SettingsPath, GodotFileAccess.ModeFlags.Read);
            string json = file?.GetAsText() ?? "";
            if (!string.IsNullOrEmpty(json))
            {
                _data = JsonSerializer.Deserialize<SettingsData>(json, _jsonOptions) ?? new SettingsData();
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SettingsManager] Load failed, using defaults: {ex.Message}");
            _data = new SettingsData();
        }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    // ========================================================================
    // SETTINGS DATA MODEL
    // ========================================================================

    private class SettingsData
    {
        [JsonPropertyName("master_volume")] public float MasterVolume { get; set; } = 0.8f;
        [JsonPropertyName("music_volume")]  public float MusicVolume { get; set; } = 0.7f;
        [JsonPropertyName("sfx_volume")]    public float SfxVolume { get; set; } = 0.8f;
        [JsonPropertyName("fullscreen")]    public bool Fullscreen { get; set; } = false;
        [JsonPropertyName("language")]      public string Language { get; set; } = "en";
        [JsonPropertyName("debug_unlocked")] public bool DebugUnlocked { get; set; } = false;
    }
}
