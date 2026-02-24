#nullable enable
using System.Text.Json.Serialization;

namespace OregonTrail2026.Models;

/// <summary>
/// A single journal entry recording one game event.
///
/// Category values (open string, not an enum so future categories
/// can be added without schema changes):
///   "landmark"  - arrived at a named landmark
///   "river"     - river crossing attempt and outcome
///   "event"     - random trail event or encounter
///   "death"     - party member died
///   "illness"   - party member fell ill or recovered
///   "store"     - significant store transaction
///   "weather"   - severe weather onset
///   "hunt"      - hunt result summary
///   "fish"      - fish result summary
///   "route"     - route choice made at The Dalles
///   "system"    - save, departure, arrival
///
/// Seq is monotonically increasing within a save file (never reused).
/// Day and Miles snapshot the state at time of writing, enabling
/// timeline reconstruction without decoding the full GameState.
/// </summary>
public class JournalEntry
{
    [JsonPropertyName("seq")]      public int    Seq      { get; set; }
    [JsonPropertyName("day")]      public int    Day      { get; set; }
    [JsonPropertyName("miles")]    public int    Miles    { get; set; }
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("text")]     public string Text     { get; set; } = "";

    // Parameterless ctor required by System.Text.Json
    public JournalEntry() { }

    public JournalEntry(int seq, int day, int miles, string category, string text)
    {
        Seq      = seq;
        Day      = day;
        Miles    = miles;
        Category = category;
        Text     = text;
    }
}
