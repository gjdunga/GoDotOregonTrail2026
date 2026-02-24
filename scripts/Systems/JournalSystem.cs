#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using OregonTrail2026.Models;

namespace OregonTrail2026.Systems;

/// <summary>
/// Journal system: write and query the per-save event log.
///
/// Design constraints:
///   - All writes go through Add() to enforce the Seq counter invariant.
///   - GameState.JournalEntries is the source of truth; this class is stateless.
///   - Entry count is capped at GameConstants.JournalMaxNotes (80) by oldest-first
///     eviction. Categories with ongoing relevance (death, illness) are exempt from
///     eviction: the cap removes generic "event" entries first.
///   - Queries return IEnumerable; callers decide how many to materialize.
///
/// Usage:
///   JournalSystem.Add(state, "landmark", "Arrived at Fort Kearney.");
///   foreach (var e in JournalSystem.Recent(state, 5)) ...
/// </summary>
public static class JournalSystem
{
    // =========================================================================
    // WRITE
    // =========================================================================

    /// <summary>
    /// Append one entry to the journal, enforcing the max-notes cap.
    ///
    /// Eviction order when at capacity: evict "event" and "weather" entries
    /// first (oldest first), then other categories except "death" and "illness".
    /// Death and illness entries are never evicted.
    /// </summary>
    public static void Add(GameState state, string category, string text)
    {
        // Sanitize: strip newlines so each entry is one display line
        text = text.Replace("\n", " ").Replace("\r", "").Trim();
        if (string.IsNullOrEmpty(text)) return;

        var entry = new JournalEntry(
            state.JournalSeq++,
            state.Day,
            state.Miles,
            category,
            text);

        state.JournalEntries.Add(entry);

        // Cap enforcement
        int max = GameConstants.JournalMaxNotes;
        while (state.JournalEntries.Count > max)
        {
            // First try to evict low-priority categories
            int evictIdx = -1;
            for (int i = 0; i < state.JournalEntries.Count; i++)
            {
                string cat = state.JournalEntries[i].Category;
                if (cat is "event" or "weather")
                {
                    evictIdx = i;
                    break;
                }
            }
            // If none, evict oldest non-protected entry
            if (evictIdx < 0)
            {
                for (int i = 0; i < state.JournalEntries.Count; i++)
                {
                    string cat = state.JournalEntries[i].Category;
                    if (cat is not "death" and not "illness")
                    {
                        evictIdx = i;
                        break;
                    }
                }
            }
            // If still none (all are death/illness), evict the oldest
            if (evictIdx < 0) evictIdx = 0;

            state.JournalEntries.RemoveAt(evictIdx);
        }
    }

    // =========================================================================
    // QUERY
    // =========================================================================

    /// <summary>Returns the N most recent entries (newest first).</summary>
    public static IEnumerable<JournalEntry> Recent(GameState state, int count = 10) =>
        state.JournalEntries
            .AsEnumerable()
            .Reverse()
            .Take(count);

    /// <summary>Returns entries matching a specific category (newest first).</summary>
    public static IEnumerable<JournalEntry> ByCategory(GameState state, string category) =>
        state.JournalEntries
            .AsEnumerable()
            .Reverse()
            .Where(e => e.Category == category);

    /// <summary>Returns all entries within a day range [dayMin, dayMax] (chronological).</summary>
    public static IEnumerable<JournalEntry> ByDayRange(GameState state, int dayMin, int dayMax) =>
        state.JournalEntries
            .Where(e => e.Day >= dayMin && e.Day <= dayMax);

    /// <summary>
    /// Returns the last N entries as plain strings for save-slot meta display.
    /// Format: "Day {Day}: {Text}" truncated to 60 chars.
    /// </summary>
    public static List<string> ForSlotPreview(GameState state, int count = 3) =>
        state.JournalEntries
            .AsEnumerable()
            .Reverse()
            .Take(count)
            .Select(e =>
            {
                string line = $"Day {e.Day}: {e.Text}";
                return line.Length > 60 ? line[..57] + "..." : line;
            })
            .ToList();

    /// <summary>Total number of entries in the journal.</summary>
    public static int Count(GameState state) => state.JournalEntries.Count;
}
