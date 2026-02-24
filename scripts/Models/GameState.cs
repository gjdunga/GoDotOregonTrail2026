#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OregonTrail2026.Models;

/// <summary>
/// Central game state container tracking all progress, resources, and party status.
/// Converted from RenPy: OT2026_data.rpy class GameState.
///
/// State Lifecycle:
///   1. Created via NewGameWithParty() at game start
///   2. Updated every day via travel loop
///   3. Serialized via ToJson() for saving
///   4. Restored via FromJson() when loading
///
/// Invariants:
///   - Party size is always 5
///   - Health in range [0, 1000]
///   - Miles in range [0, TargetMiles]
///   - Supplies dict always has required keys
/// </summary>
public class GameState
{
    // ---- Progress ----
    [JsonPropertyName("occupation")] public string Occupation { get; set; } = "banker";
    [JsonPropertyName("cash")] public float Cash { get; set; } = 0f;
    [JsonPropertyName("day")] public int Day { get; set; } = 0;
    [JsonPropertyName("miles")] public int Miles { get; set; } = 0;

    // ---- Travel ----
    [JsonPropertyName("pace")] public string Pace { get; set; } = "steady";
    [JsonPropertyName("rations")] public string Rations { get; set; } = "filling";
    [JsonPropertyName("weather")] public string Weather { get; set; } = "clear";

    // ---- Wagon ----
    [JsonPropertyName("wagon")] public int Wagon { get; set; } = 900;
    [JsonPropertyName("oxen_condition")] public int OxenCondition { get; set; } = 900;

    // ---- Party ----
    [JsonPropertyName("party")] public List<Person> Party { get; set; } = new();

    // ---- Supplies ----
    [JsonPropertyName("supplies")] public Dictionary<string, int> Supplies { get; set; } = new()
    {
        { "food", 0 }, { "bullets", 0 }, { "clothes", 0 },
        { "oxen", 0 }, { "wheel", 0 }, { "axle", 0 }, { "tongue", 0 },
    };

    // ---- Economy ----
    [JsonPropertyName("ledger")] public List<Dictionary<string, object?>> Ledger { get; set; } = new();
    [JsonPropertyName("cure_prices")] public Dictionary<string, int> CurePrices { get; set; } = new();
    [JsonPropertyName("remedies")] public Dictionary<string, int> Remedies { get; set; } = new();

    // ---- Roles ----
    [JsonPropertyName("roles")] public Dictionary<string, string> Roles { get; set; } = new()
    {
        { "driver", "" }, { "hunter", "" }, { "medic", "" }, { "scout", "" },
    };

    // ---- Route ----
    [JsonPropertyName("route_choice")] public string RouteChoice { get; set; } = "";

    // ---- Events / State ----
    [JsonPropertyName("last_event")] public Dictionary<string, object?> LastEvent { get; set; } = new();
    [JsonPropertyName("last_card")] public string LastCard { get; set; } = "";
    [JsonPropertyName("stop_flags")] public Dictionary<string, object?> StopFlags { get; set; } = new();

    // ---- Fail state counters ----
    [JsonPropertyName("all_unconscious_days")] public int AllUnconsciousDays { get; set; } = 0;
    [JsonPropertyName("starve_days")] public int StarveDays { get; set; } = 0;
    [JsonPropertyName("stranded_days")] public int StrandedDays { get; set; } = 0;

    // ---- Encounter / bonus timers ----
    [JsonPropertyName("pending_encounter")] public Dictionary<string, object?>? PendingEncounter { get; set; }
    [JsonPropertyName("free_ferry_uses")] public int FreeFerryUses { get; set; } = 0;
    [JsonPropertyName("guidance_until")] public int GuidanceUntil { get; set; } = 0;
    [JsonPropertyName("terrain_warning_until")] public int TerrainWarningUntil { get; set; } = 0;
    [JsonPropertyName("scout_bonus_until")] public int ScoutBonusUntil { get; set; } = 0;
    [JsonPropertyName("river_notes_until")] public int RiverNotesUntil { get; set; } = 0;
    [JsonPropertyName("river_notes_bad_until")] public int RiverNotesBadUntil { get; set; } = 0;
    [JsonPropertyName("river_notes_target")] public string RiverNotesTarget { get; set; } = "";
    [JsonPropertyName("prevent_event_once")] public Dictionary<string, object?>? PreventEventOnce { get; set; }
    [JsonPropertyName("bad_intel_until")] public int BadIntelUntil { get; set; } = 0;
    [JsonPropertyName("bad_intel_kind")] public string BadIntelKind { get; set; } = "";
    [JsonPropertyName("bad_intel_source")] public string BadIntelSource { get; set; } = "";
    [JsonPropertyName("talk_paid_day")] public Dictionary<string, int> TalkPaidDay { get; set; } = new();
    [JsonPropertyName("illness_resist_until")] public int IllnessResistUntil { get; set; } = 0;
    [JsonPropertyName("blacksmith_voucher_until")] public int BlacksmithVoucherUntil { get; set; } = 0;
    [JsonPropertyName("snow_spell_until")] public int SnowSpellUntil { get; set; } = 0;
    [JsonPropertyName("sprayed_until")] public int SprayedUntil { get; set; } = 0;

    // ---- Pending actions ----
    [JsonPropertyName("pending_stop_type")] public string? PendingStopType { get; set; }
    [JsonPropertyName("pending_stop_key")] public string? PendingStopKey { get; set; }
    [JsonPropertyName("pending_repair")] public Dictionary<string, object?>? PendingRepair { get; set; }
    [JsonPropertyName("forced_rest_days")] public int ForcedRestDays { get; set; } = 0;
    [JsonPropertyName("pending_weather")] public Dictionary<string, object?>? PendingWeather { get; set; }

    // ---- Location tracking ----
    [JsonPropertyName("visited_landmarks")] public List<string> VisitedLandmarks { get; set; } = new();
    [JsonPropertyName("crossed_rivers")] public List<string> CrossedRivers { get; set; } = new();
    [JsonPropertyName("river_depth_ft")] public float RiverDepthFt { get; set; } = 0f;

    // ---- Repair system ----
    [JsonPropertyName("repair_quality")] public Dictionary<string, float> RepairQuality { get; set; } = new()
    {
        { "wheel", 1.0f }, { "axle", 1.0f }, { "tongue", 1.0f },
    };
    [JsonPropertyName("tuneup_until_miles")] public int TuneupUntilMiles { get; set; } = 0;
    [JsonPropertyName("jury_rig_until")] public int JuryRigUntil { get; set; } = 0;
    [JsonPropertyName("blacksmith_vouchers")] public int BlacksmithVouchers { get; set; } = 0;

    // ---- Town/Store state ----
    [JsonPropertyName("fort_trade_offers")] public List<Dictionary<string, object?>> FortTradeOffers { get; set; } = new();
    [JsonPropertyName("at_town_name")]      public string AtTownName { get; set; } = "";
    [JsonPropertyName("at_town_store_key")] public string AtTownStoreKey { get; set; } = "";
    [JsonPropertyName("store_price_mult")]  public Dictionary<string, float> StorePriceMult { get; set; } = new();
    [JsonPropertyName("store_soldout")]     public Dictionary<string, bool> StoreSoldout { get; set; } = new();
    [JsonPropertyName("store_price_cache")] public Dictionary<string, float> StorePriceCache { get; set; } = new();

    // ---- Journal ----
    [JsonPropertyName("journal")]      public List<Dictionary<string, object?>> Journal { get; set; } = new();
    [JsonPropertyName("journal_seq")]  public int JournalSeq { get; set; } = 0;
    [JsonPropertyName("last_note_nudge_day")]           public int LastNoteNudgeDay { get; set; } = 0;
    [JsonPropertyName("temp_store_price_mult_by_store")] public Dictionary<string, float> TempStorePriceMultByStore { get; set; } = new();
    [JsonPropertyName("service_discount_remaining")]    public int ServiceDiscountRemaining { get; set; } = 0;
    [JsonPropertyName("service_discount_until_day")]    public int ServiceDiscountUntilDay { get; set; } = 0;

    // ========================================================================
    // METHODS
    // ========================================================================

    /// <summary>Returns all living party members.</summary>
    public List<Person> Living() => Party.Where(p => p.Alive).ToList();

    /// <summary>Returns true if any party member is alive.</summary>
    public bool AnyAlive() => Party.Any(p => p.Alive);

    /// <summary>
    /// Returns true if the wagon cannot move: no oxen remain or wagon
    /// condition has reached zero. Used to gate StrandedDays increment.
    /// Failure condition: oxen count zero, OxenCondition zero, OR Wagon zero.
    /// All three are checked because a river crossing or rockslide can zero
    /// Wagon without zeroing OxenCondition, and visa versa.
    /// </summary>
    public bool IsStranded() =>
        Supplies.GetValueOrDefault("oxen", 0) <= 0 || OxenCondition <= 0 || Wagon <= 0;

    /// <summary>Serialize game state to JSON for save files.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, _jsonOptions);

    /// <summary>Deserialize game state from JSON save file.</summary>
    public static GameState FromJson(string json) => JsonSerializer.Deserialize<GameState>(json, _jsonOptions)!;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Create a new game with the given occupation and party names.</summary>
    public static GameState NewGameWithParty(string occupationKey, List<string> names)
    {
        var st = new GameState();
        st.Occupation = occupationKey;
        var occ = GameData.GetOccupation(occupationKey);
        st.Cash = occ?.Cash ?? 1600f;
        st.Party = names.Select(n => new Person(n)).ToList();
        return st;
    }
}
