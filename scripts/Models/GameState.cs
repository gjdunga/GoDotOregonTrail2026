using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

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
    [JsonProperty("occupation")] public string Occupation { get; set; } = "banker";
    [JsonProperty("cash")] public float Cash { get; set; } = 0f;
    [JsonProperty("day")] public int Day { get; set; } = 0;
    [JsonProperty("miles")] public int Miles { get; set; } = 0;

    // ---- Travel ----
    [JsonProperty("pace")] public string Pace { get; set; } = "steady";
    [JsonProperty("rations")] public string Rations { get; set; } = "filling";
    [JsonProperty("weather")] public string Weather { get; set; } = "clear";

    // ---- Wagon ----
    [JsonProperty("wagon")] public int Wagon { get; set; } = 900;
    [JsonProperty("oxen_condition")] public int OxenCondition { get; set; } = 900;

    // ---- Party ----
    [JsonProperty("party")] public List<Person> Party { get; set; } = new();

    // ---- Supplies ----
    [JsonProperty("supplies")] public Dictionary<string, int> Supplies { get; set; } = new()
    {
        { "food", 0 }, { "bullets", 0 }, { "clothes", 0 },
        { "oxen", 0 }, { "wheel", 0 }, { "axle", 0 }, { "tongue", 0 },
    };

    // ---- Economy ----
    [JsonProperty("ledger")] public List<Dictionary<string, object>> Ledger { get; set; } = new();
    [JsonProperty("cure_prices")] public Dictionary<string, int> CurePrices { get; set; } = new();
    [JsonProperty("remedies")] public Dictionary<string, int> Remedies { get; set; } = new();

    // ---- Roles ----
    [JsonProperty("roles")] public Dictionary<string, string> Roles { get; set; } = new()
    {
        { "driver", "" }, { "hunter", "" }, { "medic", "" }, { "scout", "" },
    };

    // ---- Route ----
    [JsonProperty("route_choice")] public string RouteChoice { get; set; } = "";

    // ---- Events / State ----
    [JsonProperty("last_event")] public Dictionary<string, object> LastEvent { get; set; } = new();
    [JsonProperty("last_card")] public string LastCard { get; set; } = "";
    [JsonProperty("stop_flags")] public Dictionary<string, object> StopFlags { get; set; } = new();

    // ---- Fail state counters ----
    [JsonProperty("all_unconscious_days")] public int AllUnconsciousDays { get; set; } = 0;
    [JsonProperty("starve_days")] public int StarveDays { get; set; } = 0;
    [JsonProperty("stranded_days")] public int StrandedDays { get; set; } = 0;

    // ---- Encounter / bonus timers ----
    [JsonProperty("pending_encounter")] public Dictionary<string, object>? PendingEncounter { get; set; }
    [JsonProperty("free_ferry_uses")] public int FreeFerryUses { get; set; } = 0;
    [JsonProperty("guidance_until")] public int GuidanceUntil { get; set; } = 0;
    [JsonProperty("terrain_warning_until")] public int TerrainWarningUntil { get; set; } = 0;
    [JsonProperty("scout_bonus_until")] public int ScoutBonusUntil { get; set; } = 0;
    [JsonProperty("river_notes_until")] public int RiverNotesUntil { get; set; } = 0;
    [JsonProperty("river_notes_bad_until")] public int RiverNotesBadUntil { get; set; } = 0;
    [JsonProperty("river_notes_target")] public string RiverNotesTarget { get; set; } = "";
    [JsonProperty("prevent_event_once")] public Dictionary<string, object>? PreventEventOnce { get; set; }
    [JsonProperty("bad_intel_until")] public int BadIntelUntil { get; set; } = 0;
    [JsonProperty("bad_intel_kind")] public string BadIntelKind { get; set; } = "";
    [JsonProperty("bad_intel_source")] public string BadIntelSource { get; set; } = "";
    [JsonProperty("talk_paid_day")] public Dictionary<string, int> TalkPaidDay { get; set; } = new();
    [JsonProperty("illness_resist_until")] public int IllnessResistUntil { get; set; } = 0;
    [JsonProperty("blacksmith_voucher_until")] public int BlacksmithVoucherUntil { get; set; } = 0;
    [JsonProperty("snow_spell_until")] public int SnowSpellUntil { get; set; } = 0;
    [JsonProperty("sprayed_until")] public int SprayedUntil { get; set; } = 0;

    // ---- Pending actions ----
    [JsonProperty("pending_stop_type")] public string? PendingStopType { get; set; }
    [JsonProperty("pending_stop_key")] public string? PendingStopKey { get; set; }
    [JsonProperty("pending_repair")] public Dictionary<string, object>? PendingRepair { get; set; }
    [JsonProperty("forced_rest_days")] public int ForcedRestDays { get; set; } = 0;
    [JsonProperty("pending_weather")] public Dictionary<string, object>? PendingWeather { get; set; }

    // ---- Location tracking ----
    [JsonProperty("visited_landmarks")] public List<string> VisitedLandmarks { get; set; } = new();
    [JsonProperty("crossed_rivers")] public List<string> CrossedRivers { get; set; } = new();
    [JsonProperty("river_depth_ft")] public float RiverDepthFt { get; set; } = 0f;

    // ---- Repair system ----
    [JsonProperty("repair_quality")] public Dictionary<string, float> RepairQuality { get; set; } = new()
    {
        { "wheel", 1.0f }, { "axle", 1.0f }, { "tongue", 1.0f },
    };
    [JsonProperty("tuneup_until_miles")] public int TuneupUntilMiles { get; set; } = 0;
    [JsonProperty("jury_rig_until")] public int JuryRigUntil { get; set; } = 0;
    [JsonProperty("blacksmith_vouchers")] public int BlacksmithVouchers { get; set; } = 0;

    // ---- Town/Store state ----
    [JsonProperty("fort_trade_offers")] public List<Dictionary<string, object>> FortTradeOffers { get; set; } = new();
    public string AtTownName { get; set; } = "";
    public string AtTownStoreKey { get; set; } = "";
    public Dictionary<string, float> StorePriceMult { get; set; } = new();
    public Dictionary<string, bool> StoreSoldout { get; set; } = new();
    public Dictionary<string, float> StorePriceCache { get; set; } = new();

    // ---- Journal ----
    [JsonProperty("journal")] public List<Dictionary<string, object>> Journal { get; set; } = new();
    [JsonProperty("journal_seq")] public int JournalSeq { get; set; } = 0;
    public int LastNoteNudgeDay { get; set; } = 0;
    public Dictionary<string, float> TempStorePriceMultByStore { get; set; } = new();
    public int ServiceDiscountRemaining { get; set; } = 0;
    public int ServiceDiscountUntilDay { get; set; } = 0;

    // ========================================================================
    // METHODS
    // ========================================================================

    /// <summary>Returns all living party members.</summary>
    public List<Person> Living() => Party.Where(p => p.Alive).ToList();

    /// <summary>Returns true if any party member is alive.</summary>
    public bool AnyAlive() => Party.Any(p => p.Alive);

    /// <summary>Serialize game state to JSON for save files.</summary>
    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

    /// <summary>Deserialize game state from JSON save file.</summary>
    public static GameState FromJson(string json) => JsonConvert.DeserializeObject<GameState>(json)!;

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
