#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using GodotFileAccess = Godot.FileAccess;

namespace OregonTrail2026.Utils;

/// <summary>
/// Loads translation CSV files at runtime and registers them with
/// Godot's TranslationServer. This avoids requiring the editor's
/// import system to process the CSV first.
///
/// CSV format:
///   keys,en,es,fr,de,ja
///   KEY_NAME,English text,Spanish text,...
///
/// Call LoadCsv() once at startup (from SettingsManager._Ready).
/// </summary>
public static class TranslationLoader
{
    private const string TranslationCsvPath = "res://assets/translations/translations.csv";

    /// <summary>
    /// Parse the CSV and register all language columns with TranslationServer.
    /// </summary>
    public static void LoadCsv()
    {
        if (!GodotFileAccess.FileExists(TranslationCsvPath))
        {
            GD.PrintErr("[TranslationLoader] CSV not found: " + TranslationCsvPath);
            return;
        }

        using var file = GodotFileAccess.Open(TranslationCsvPath, GodotFileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr("[TranslationLoader] Failed to open: " + TranslationCsvPath);
            return;
        }

        // Read header row to get language codes
        string headerLine = file.GetLine().StripEdges();
        if (string.IsNullOrEmpty(headerLine)) return;

        string[] headers = SplitCsvLine(headerLine);
        if (headers.Length < 2) return; // need at least "keys" + one language

        // Create a Translation resource per language column
        var translations = new Dictionary<string, Translation>();
        for (int col = 1; col < headers.Length; col++)
        {
            string locale = headers[col].Trim();
            if (string.IsNullOrEmpty(locale)) continue;
            var t = new Translation();
            t.Locale = locale;
            translations[locale] = t;
        }

        // Read data rows
        int rowCount = 0;
        while (!file.EofReached())
        {
            string line = file.GetLine().StripEdges();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cells = SplitCsvLine(line);
            if (cells.Length < 2) continue;

            string key = cells[0].Trim();
            if (string.IsNullOrEmpty(key)) continue;

            for (int col = 1; col < headers.Length && col < cells.Length; col++)
            {
                string locale = headers[col].Trim();
                if (translations.TryGetValue(locale, out var t))
                {
                    string value = cells[col].Trim();
                    if (!string.IsNullOrEmpty(value))
                        t.AddMessage(key, value);
                }
            }
            rowCount++;
        }

        // Register all translations with Godot
        foreach (var kvp in translations)
        {
            TranslationServer.AddTranslation(kvp.Value);
        }

        GD.Print($"[TranslationLoader] Loaded {rowCount} keys across {translations.Count} language(s): {string.Join(", ", translations.Keys)}");
    }

    /// <summary>
    /// Simple CSV line splitter that handles quoted fields containing commas.
    /// </summary>
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        int start = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == ',' && !inQuotes)
            {
                fields.Add(Unquote(line.Substring(start, i - start)));
                start = i + 1;
            }
        }
        // Last field
        fields.Add(Unquote(line.Substring(start)));

        return fields.ToArray();
    }

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
            s = s[1..^1].Replace("\"\"", "\"");
        return s;
    }
}
