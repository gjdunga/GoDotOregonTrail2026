#nullable enable
using System.Text.Json.Serialization;

namespace OregonTrail2026.Models;

/// <summary>
/// Unencrypted metadata stored in each save archive for load-screen display.
/// Contains NO gameplay-affecting data. Editing this changes what the menu shows,
/// not what the game loads.
/// </summary>
public class SaveSlotMeta
{
    [JsonPropertyName("slot_name")]
    public string SlotName { get; set; } = "";

    [JsonPropertyName("saved_at")]
    public string SavedAt { get; set; } = "";

    [JsonPropertyName("leader_name")]
    public string LeaderName { get; set; } = "";

    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("miles")]
    public int Miles { get; set; }

    [JsonPropertyName("party_alive")]
    public int PartyAlive { get; set; }

    [JsonPropertyName("occupation")]
    public string Occupation { get; set; } = "";

    /// <summary>Build display meta from a live GameState.</summary>
    public static SaveSlotMeta FromGameState(GameState state, string slotName)
    {
        return new SaveSlotMeta
        {
            SlotName = slotName,
            SavedAt = System.DateTime.UtcNow.ToString("o"),
            LeaderName = state.Party.Count > 0 ? state.Party[0].Name : "???",
            Day = state.Day,
            Miles = state.Miles,
            PartyAlive = state.Living().Count,
            Occupation = state.Occupation,
        };
    }
}
