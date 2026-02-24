#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using OregonTrail2026.Models;

namespace OregonTrail2026.Systems;

/// <summary>
/// Random trail events: breakdowns, theft, weather hazards, wildlife, encounters.
/// Converted and significantly expanded from RenPy: OT2026_encounters.rpy, OT2026_travel.rpy.
///
/// Event dispatch model:
///   TryRandomEvent is called once per travel day by GameManager.TravelOneDay.
///   It rolls against eventChance, then selects an event from a weighted table
///   segmented by terrain and season. Each event writes LastCard (image path)
///   and LastEvent (type + text) into GameState, which MainScene then displays.
///
/// Additions vs original RenPy:
///   - Dust storm, thunderstorm, heavy rain, heat wave (weather hazards)
///   - Snakebite, skunk spray, ox injured, ox died, forced rest (wildlife/camp)
///   - Bandits (heavier theft with narrative)
///   - Wagon stuck in mud, mountain pass descent (terrain-specific)
///   - Fire in camp (supply destruction)
///   - Frozen river edges (late season river warning)
///   - Rations low notification (proactive warning at food threshold)
///   - TerrainWarningUntil now reduces hazard selection weight
///   - GuidanceUntil now also reduces overall event chance (already used in rivers)
///   - Illness card image now set when TryIllness infects a party member
/// </summary>
public static class EventSystem
{
    // =========================================================================
    // MAIN ENTRY POINT
    // =========================================================================

    /// <summary>Roll for a random event and apply it to state.</summary>
    public static void TryRandomEvent(GameState st)
    {
        float eventChance = st.Miles switch
        {
            < 500  => GameConstants.EventChanceEarly,
            > 1500 => GameConstants.EventChanceLate,
            _      => GameConstants.EventChanceMid,
        };

        // Scout reduces raw event chance (hazard warning / route pre-scouting)
        if (st.ScoutBonusUntil > st.Day)
            eventChance *= (1f - GameConstants.RoleScoutHazardWarning);

        // Guidance encounter: local knowledge reduces event probability
        if (st.GuidanceUntil > st.Day)
            eventChance *= 0.75f;

        if (GameManager.RandFloat() >= eventChance) return;

        string terrain  = TravelSystem.TerrainByMiles(st.Miles);
        string weather  = st.Weather;
        var (month, _)  = DateCalc.DayToDate(st.Day);
        bool winter     = month is "NOV" or "DEC" or "JAN" or "FEB";
        bool summer     = month is "JUN" or "JUL" or "AUG";

        // TerrainWarning active: suppress hazard events by 50% (player was warned)
        bool terrainWarned = st.TerrainWarningUntil > st.Day;

        // Build weighted event table for current context
        var events = BuildEventTable(terrain, weather, winter, summer, terrainWarned, st);

        // Low food warning fires at threshold regardless of event roll
        int food = st.Supplies.GetValueOrDefault("food", 0);
        int living = st.Living().Count;
        int daysOfFood = living > 0 ? food / Math.Max(1, (int)(3 * living)) : 999;
        if (daysOfFood <= 3 && food > 0 && GameManager.RandFloat() < 0.35f)
        {
            FireRationsLow(st);
            return;
        }

        if (events.Count == 0) return;

        float total = events.Sum(e => e.weight);
        float roll  = GameManager.RandFloat() * total;
        float cum   = 0f;
        foreach (var (weight, action) in events)
        {
            cum += weight;
            if (roll <= cum)
            {
                action(st);
                return;
            }
        }

        // Fallthrough: fire last event
        events[^1].action(st);
    }

    // =========================================================================
    // EVENT TABLE BUILDER
    // =========================================================================

    private static List<(float weight, Action<GameState> action)> BuildEventTable(
        string terrain, string weather, bool winter, bool summer,
        bool terrainWarned, GameState st)
    {
        float hazardScale = terrainWarned ? 0.5f : 1.0f;

        var table = new List<(float, Action<GameState>)>();

        // --- Always-present events ---
        table.Add((10f, ApplyTheft));
        table.Add((8f,  ApplyBadWater));
        table.Add((7f,  ApplyLostTrail));
        table.Add((5f,  ApplySnakebite));
        table.Add((4f,  ApplySkunkSprayed));
        table.Add((8f,  ApplyGoodEvent));
        table.Add((6f,  GenerateEncounter));
        table.Add((5f,  s => HealthSystem.TryIllness(s)));

        // --- Weather-triggered events ---
        if (weather is "rain" or "snow")
            table.Add((12f * hazardScale, ApplyWagonStuckMud));

        if (weather == "dust")
            table.Add((14f, ApplyDustStorm));

        if (weather == "rain" && !winter)
            table.Add((10f, ApplyThunderstorm));

        if (weather == "fog")
            table.Add((8f,  ApplyHeavyFog));

        // --- Seasonal events ---
        if (summer && terrain is "prairie" or "plains" or "high_plains")
            table.Add((10f, ApplyHeatWave));

        if (winter || (terrain is "mountains" or "high_plains" && GameManager.RandFloat() < 0.3f))
            table.Add((9f,  ApplyFrozenTrail));

        // --- Terrain events ---
        if (terrain == "mountains")
        {
            table.Add((13f * hazardScale, ApplyRockslide));
            table.Add((8f,               ApplyMountainPassDescent));
            if (!winter) table.Add((6f,  ApplyEarlySnow));
        }

        if (terrain is "plains" or "high_plains" or "mountains")
        {
            table.Add((6f * hazardScale, ApplyBandits));
        }

        // --- Camp events ---
        table.Add((5f, ApplyFireInCamp));

        // --- Ox events ---
        int oxenCount = st.Supplies.GetValueOrDefault("oxen", 0);
        if (oxenCount > 0)
        {
            table.Add((7f * hazardScale, ApplyOxInjured));
            if (oxenCount > 1)
                table.Add((3f * hazardScale, ApplyOxDied));
        }

        // --- Forced rest ---
        bool anyWeak = st.Living().Any(p => p.Health < 300);
        if (anyWeak) table.Add((8f, ApplyForcedRest));

        return table;
    }

    // =========================================================================
    // NEGATIVE EVENTS
    // =========================================================================

    private static void ApplyTheft(GameState st)
    {
        int foodStolen    = GameManager.RandInt(10, 50);
        int bulletsStolen = GameManager.RandInt(5, 20);
        st.Supplies["food"]    = Math.Max(0, st.Supplies.GetValueOrDefault("food",    0) - foodStolen);
        st.Supplies["bullets"] = Math.Max(0, st.Supplies.GetValueOrDefault("bullets", 0) - bulletsStolen);
        st.LastCard  = "res://assets/images/events/evt_thief_night.webp";
        st.LastEvent = new() { { "type", "thief" },
            { "text", $"THIEVES IN THE NIGHT. LOST {foodStolen} LBS FOOD AND {bulletsStolen} BULLETS." } };
    }

    private static void ApplyBandits(GameState st)
    {
        // Bandits steal more and are harder to deter
        int cashLost   = GameManager.RandInt(5, 30);
        int foodLost   = GameManager.RandInt(20, 60);
        st.Cash        = Math.Max(0, st.Cash - cashLost);
        st.Supplies["food"] = Math.Max(0, st.Supplies.GetValueOrDefault("food", 0) - foodLost);
        st.LastCard  = "res://assets/images/events/evt_bandits_threat.webp";
        st.LastEvent = new() { { "type", "bandits" },
            { "text", $"BANDITS RANSACKED THE CAMP. LOST ${cashLost} AND {foodLost} LBS OF FOOD." } };
    }

    private static void ApplyBadWater(GameState st)
    {
        foreach (var p in st.Living())
        {
            if (GameManager.RandFloat() < 0.25f && string.IsNullOrEmpty(p.Illness))
            {
                p.Illness         = "food_poisoning";
                p.IllnessSeverity = 0.35f;
                p.IllnessDays     = GameManager.RandInt(2, 5);
                break;
            }
        }
        st.LastCard  = "res://assets/images/events/evt_bad_water.webp";
        st.LastEvent = new() { { "type", "bad_water" },
            { "text", "THE WATER TASTED FOUL. SOMEONE MAY FALL ILL." } };
    }

    private static void ApplyLostTrail(GameState st)
    {
        int lostMiles = GameManager.RandInt(5, 15);
        st.Miles      = Math.Max(0, st.Miles - lostMiles);
        st.LastCard   = "res://assets/images/events/evt_lost_trail.webp";
        st.LastEvent  = new() { { "type", "lost_trail" },
            { "text", $"LOST THE TRAIL. BACKTRACKED {lostMiles} MILES BEFORE FINDING IT AGAIN." } };
    }

    private static void ApplyRockslide(GameState st)
    {
        int wagonDmg = GameManager.RandInt(50, 150);
        st.Wagon     = Math.Max(0, st.Wagon - wagonDmg);
        foreach (var p in st.Living())
        {
            if (GameManager.RandFloat() < 0.3f)
                p.Health = Math.Max(0, p.Health - GameManager.RandInt(30, 100));
        }
        st.LastCard  = "res://assets/images/events/evt_rockslide.webp";
        st.LastEvent = new() { { "type", "rockslide" },
            { "text", "ROCKSLIDE. BOULDERS HIT THE WAGON AND SCATTERED THE PARTY." } };
    }

    private static void ApplySnakebite(GameState st)
    {
        var living = st.Living();
        if (living.Count == 0) return;
        var victim  = living[GameManager.RandInt(0, living.Count - 1)];
        int dmg     = GameManager.RandInt(80, 220);
        victim.Health = Math.Max(0, victim.Health - dmg);
        if (string.IsNullOrEmpty(victim.Illness))
        {
            victim.Illness         = "snakebite";
            victim.IllnessSeverity = 0.55f;
            victim.IllnessDays     = GameManager.RandInt(3, 6);
        }
        st.LastCard  = "res://assets/images/events/evt_snakebite.webp";
        st.LastEvent = new() { { "type", "snakebite" },
            { "text", $"{victim.Name.ToUpper()} WAS BITTEN BY A RATTLESNAKE." } };
    }

    private static void ApplySkunkSprayed(GameState st)
    {
        var living = st.Living();
        if (living.Count == 0) return;
        var victim  = living[GameManager.RandInt(0, living.Count - 1)];
        // SprayedUntil increases illness risk via HealthSystem.TryIllness
        st.SprayedUntil = st.Day + GameConstants.SkunkSprayDurationDays;
        st.LastCard  = "res://assets/images/events/evt_skunk_sprayed.webp";
        st.LastEvent = new() { { "type", "skunk_sprayed" },
            { "text", $"{victim.Name.ToUpper()} WAS SPRAYED BY A SKUNK. THE SMELL LINGERS." } };
    }

    private static void ApplyOxInjured(GameState st)
    {
        int dmg = GameManager.RandInt(80, 200);
        st.OxenCondition = Math.Max(0, st.OxenCondition - dmg);
        st.LastCard  = "res://assets/images/events/evt_ox_injured.webp";
        st.LastEvent = new() { { "type", "ox_injured" },
            { "text", "ONE OF THE OXEN WAS INJURED. TRAVEL WILL BE SLOWER UNTIL IT HEALS." } };
    }

    private static void ApplyOxDied(GameState st)
    {
        int oxen = st.Supplies.GetValueOrDefault("oxen", 0);
        if (oxen <= 0) return;
        st.Supplies["oxen"] = oxen - 1;
        int dmg = GameManager.RandInt(100, 250);
        st.OxenCondition = Math.Max(0, st.OxenCondition - dmg);
        st.LastCard  = "res://assets/images/events/evt_ox_dead.webp";
        st.LastEvent = new() { { "type", "ox_died" },
            { "text", "ONE OF THE OXEN COLLAPSED AND DIED. YOU HAVE FEWER OXEN NOW." } };
    }

    private static void ApplyFireInCamp(GameState st)
    {
        int foodBurned    = GameManager.RandInt(20, 80);
        int clothesBurned = GameManager.RandFloat() < 0.4f ? 1 : 0;
        st.Supplies["food"]    = Math.Max(0, st.Supplies.GetValueOrDefault("food",    0) - foodBurned);
        st.Supplies["clothes"] = Math.Max(0, st.Supplies.GetValueOrDefault("clothes", 0) - clothesBurned);
        // Light wagon damage
        st.Wagon = Math.Max(0, st.Wagon - GameManager.RandInt(20, 60));
        st.LastCard  = "res://assets/images/events/evt_fire_in_camp.webp";
        string clothesLine = clothesBurned > 0 ? " CLOTHING AND SUPPLIES BURNED." : "";
        st.LastEvent = new() { { "type", "fire_in_camp" },
            { "text", $"FIRE IN CAMP. LOST {foodBurned} LBS OF FOOD.{clothesLine}" } };
    }

    private static void ApplyForcedRest(GameState st)
    {
        // Party is too exhausted - lose a day (GameManager advances day before calling this,
        // so we just apply rest effects without advancing again. Mark via LastEvent.)
        foreach (var p in st.Living())
        {
            int recovery = GameManager.RandInt(50, 120);
            p.Health = HealthSystem.Clamp(p.Health + recovery);
        }
        st.LastCard  = "res://assets/images/events/evt_forced_rest.webp";
        st.LastEvent = new() { { "type", "forced_rest" },
            { "text", "THE PARTY WAS TOO EXHAUSTED TO TRAVEL FAR. AN UNPLANNED REST." } };
    }

    // =========================================================================
    // WEATHER HAZARD EVENTS
    // =========================================================================

    private static void ApplyDustStorm(GameState st)
    {
        int milesLost = GameManager.RandInt(3, 10);
        st.Miles      = Math.Max(0, st.Miles - milesLost);
        int wearDmg   = GameManager.RandInt(20, 50);
        st.Wagon      = Math.Max(0, st.Wagon - wearDmg);
        // Dust aggravates breathing - small illness risk
        foreach (var p in st.Living())
        {
            if (GameManager.RandFloat() < 0.12f && string.IsNullOrEmpty(p.Illness))
            {
                p.Illness = "respiratory"; p.IllnessSeverity = 0.25f; p.IllnessDays = GameManager.RandInt(2, 4);
                break;
            }
        }
        st.LastCard  = "res://assets/images/events/evt_dust_storm.webp";
        st.LastEvent = new() { { "type", "dust_storm" },
            { "text", $"DUST STORM. LOST {milesLost} MILES. THE GRIT WORKS INTO EVERYTHING." } };
    }

    private static void ApplyThunderstorm(GameState st)
    {
        int wagonDmg = GameManager.RandInt(15, 50);
        st.Wagon     = Math.Max(0, st.Wagon - wagonDmg);
        // Lightning chance: small health hit to random person
        if (GameManager.RandFloat() < 0.08f)
        {
            var living = st.Living();
            if (living.Count > 0)
            {
                var victim = living[GameManager.RandInt(0, living.Count - 1)];
                victim.Health = Math.Max(0, victim.Health - GameManager.RandInt(50, 150));
            }
        }
        st.LastCard  = "res://assets/images/events/evt_thunderstorm.webp";
        st.LastEvent = new() { { "type", "thunderstorm" },
            { "text", "THUNDERSTORM. LIGHTNING CRACKED NEARBY. THE WAGON TOOK SOME DAMAGE." } };
    }

    private static void ApplyHeavyFog(GameState st)
    {
        int milesLost = GameManager.RandInt(2, 7);
        st.Miles      = Math.Max(0, st.Miles - milesLost);
        st.LastCard   = "res://assets/images/events/evt_thick_fog.webp";
        st.LastEvent  = new() { { "type", "heavy_fog" },
            { "text", $"THICK FOG. PROGRESS SLOW. LOST {milesLost} MILES FINDING THE TRAIL." } };
    }

    private static void ApplyHeatWave(GameState st)
    {
        // Heat damages oxen condition and forces slow pace
        int oxenDmg = GameManager.RandInt(50, 130);
        st.OxenCondition = Math.Max(0, st.OxenCondition - oxenDmg);
        foreach (var p in st.Living())
        {
            if (GameManager.RandFloat() < 0.20f)
                p.Health = Math.Max(0, p.Health - GameManager.RandInt(30, 80));
        }
        st.LastCard  = "res://assets/images/events/evt_heat_wave.webp";
        st.LastEvent = new() { { "type", "heat_wave" },
            { "text", "SCORCHING HEAT WAVE. THE OXEN STRAIN. THE PARTY SUFFERS IN THE SUN." } };
    }

    private static void ApplyFrozenTrail(GameState st)
    {
        int wagonDmg = GameManager.RandInt(30, 80);
        st.Wagon     = Math.Max(0, st.Wagon - wagonDmg);
        st.LastCard  = "res://assets/images/events/evt_frozen_edges.webp";
        st.LastEvent = new() { { "type", "frozen_trail" },
            { "text", "FROZEN GROUND AND ICE ON THE TRAIL. THE WAGON WHEELS STRUGGLED." } };
    }

    // =========================================================================
    // TERRAIN EVENTS
    // =========================================================================

    private static void ApplyEarlySnow(GameState st)
    {
        st.Weather = "snow";
        st.LastCard  = "res://assets/images/events/evt_early_snow.webp";
        st.LastEvent = new() { { "type", "early_snow" },
            { "text", "EARLY SNOW FALLS IN THE MOUNTAINS. THE GROUND GROWS TREACHEROUS." } };
    }

    private static void ApplyMountainPassDescent(GameState st)
    {
        // Steep descent - wagon wear, possible injury
        int wagonDmg = GameManager.RandInt(40, 100);
        st.Wagon     = Math.Max(0, st.Wagon - wagonDmg);
        if (GameManager.RandFloat() < 0.25f)
        {
            var living = st.Living();
            if (living.Count > 0)
            {
                var victim = living[GameManager.RandInt(0, living.Count - 1)];
                victim.Health = Math.Max(0, victim.Health - GameManager.RandInt(40, 120));
                st.LastCard  = "res://assets/images/events/evt_injury_fall.webp";
                st.LastEvent = new() { { "type", "mountain_descent_injury" },
                    { "text", $"STEEP MOUNTAIN DESCENT. {victim.Name.ToUpper()} SLIPPED AND WAS HURT. WAGON STRAINED." } };
                return;
            }
        }
        st.LastCard  = "res://assets/images/events/evt_mountain_pass.webp";
        st.LastEvent = new() { { "type", "mountain_descent" },
            { "text", "STEEP MOUNTAIN DESCENT. THE WAGON GROANED ON THE GRADE. HANDLE WITH CARE." } };
    }

    private static void ApplyWagonStuckMud(GameState st)
    {
        int wagonDmg  = GameManager.RandInt(20, 60);
        int milesLost = GameManager.RandInt(2, 6);
        st.Wagon = Math.Max(0, st.Wagon - wagonDmg);
        st.Miles = Math.Max(0, st.Miles - milesLost);
        st.LastCard  = "res://assets/images/events/evt_wagon_stuck_mud.webp";
        st.LastEvent = new() { { "type", "wagon_stuck_mud" },
            { "text", $"WAGON MIRED IN MUD. IT TOOK HOURS TO FREE IT. LOST {milesLost} MILES." } };
    }

    // =========================================================================
    // POSITIVE EVENTS
    // =========================================================================

    private static void ApplyGoodEvent(GameState st)
    {
        float roll = GameManager.RandFloat();
        if (roll < 0.35f)
        {
            int food = GameManager.RandInt(15, 45);
            CargoSystem.AddFoodWithCapacity(st, food);
            st.LastCard  = "res://assets/images/events/evt_found_berries.webp";
            st.LastEvent = new() { { "type", "find_berries" },
                { "text", $"FOUND WILD BERRIES AND GAME TRAILS. +{food} LBS OF FOOD." } };
        }
        else if (roll < 0.60f)
        {
            st.Weather   = "clear";
            st.LastCard  = "res://assets/images/events/evt_good_weather.webp";
            st.LastEvent = new() { { "type", "good_weather" },
                { "text", "FINE WEATHER. CLEAR SKIES AND A GOOD ROAD. SPIRITS ARE HIGH." } };
        }
        else
        {
            string[] parts = { "wheel", "axle", "tongue" };
            string part    = parts[GameManager.RandInt(0, parts.Length - 1)];
            st.Supplies[part] = st.Supplies.GetValueOrDefault(part, 0) + 1;
            st.LastCard  = "res://assets/images/events/evt_found_wagon_parts.webp";
            st.LastEvent = new() { { "type", "find_wagon_parts" },
                { "text", $"FOUND AN ABANDONED WAGON. SALVAGED A SPARE {part.ToUpper()}." } };
        }
    }

    private static void FireRationsLow(GameState st)
    {
        st.LastCard  = "res://assets/images/events/evt_rations_low.webp";
        st.LastEvent = new() { { "type", "rations_low" },
            { "text", "RATIONS ARE DANGEROUSLY LOW. HUNT, FISH, OR REACH A FORT SOON." } };
    }

    // =========================================================================
    // ENCOUNTERS
    // =========================================================================

    private static void GenerateEncounter(GameState st)
    {
        float roll = GameManager.RandFloat();
        if (roll < 0.25f)
        {
            st.GuidanceUntil = st.Day + GameConstants.EncounterGuidanceDuration;
            st.LastCard  = "res://assets/images/events/evt_enc_guidance.webp";
            st.LastEvent = new() { { "type", "enc_guidance" },
                { "text", "A TRAPPER SHARES LOCAL KNOWLEDGE. RIVER CROSSINGS AND TRAIL CONDITIONS AHEAD." } };
        }
        else if (roll < 0.45f)
        {
            var sick = st.Living().Where(p => !string.IsNullOrEmpty(p.Illness)).ToList();
            if (sick.Count > 0)
            {
                var patient = sick[GameManager.RandInt(0, sick.Count - 1)];
                patient.Health = HealthSystem.Clamp(patient.Health + GameManager.RandInt(80, 200));
                patient.IllnessDays = Math.Max(0, patient.IllnessDays - GameManager.RandInt(1, 3));
            }
            st.LastCard  = "res://assets/images/events/evt_enc_medical_help.webp";
            st.LastEvent = new() { { "type", "enc_medical_help" },
                { "text", sick.Count > 0
                    ? "A TRAVELING DOCTOR HELPED YOUR SICK. TREATMENTS APPLIED."
                    : "A TRAVELING DOCTOR CHECKED YOUR PARTY. ALL IN REASONABLE HEALTH." } };
        }
        else if (roll < 0.65f)
        {
            st.TerrainWarningUntil = st.Day + GameConstants.EncounterTerrainWarningDuration;
            st.LastCard  = "res://assets/images/events/evt_enc_terrain_warning.webp";
            st.LastEvent = new() { { "type", "enc_terrain_warning" },
                { "text", "WARNED BY RETURNING EMIGRANTS ABOUT ROUGH TERRAIN AHEAD. PROCEED CAREFULLY." } };
        }
        else
        {
            // Trade offer - sets PendingEncounter for MainScene to handle
            st.PendingEncounter = BuildTradeOffer(st);
            st.LastCard  = "res://assets/images/events/evt_enc_trade.webp";
            st.LastEvent = new() { { "type", "enc_trade" },
                { "text", "A TRADER'S WAGON PULLS UP ALONGSIDE. HE HAS GOODS TO OFFER." } };
        }
    }

    // =========================================================================
    // TRADE OFFER BUILDER
    // =========================================================================

    /// <summary>
    /// Build a randomized trade offer. The offer is stored in PendingEncounter
    /// so MainScene can display a proper TradeScreen instead of an inline swap.
    ///
    /// Structure:
    ///   offer_item:  what the trader gives  (food / bullets / wheel / axle / tongue)
    ///   offer_qty:   how much
    ///   want_item:   what the trader wants  (cash / food / bullets / clothes)
    ///   want_qty:    how much (or dollar amount for cash)
    /// </summary>
    public static Dictionary<string, object> BuildTradeOffer(GameState st)
    {
        // What the trader has to offer
        string[] offerItems = { "food", "bullets", "wheel", "axle", "tongue", "clothes" };
        string   offerItem  = offerItems[GameManager.RandInt(0, offerItems.Length - 1)];
        int offerQty = offerItem switch
        {
            "food"    => GameManager.RandInt(30, 100),
            "bullets" => GameManager.RandInt(20, 60),
            "clothes" => GameManager.RandInt(1, 3),
            _         => 1, // parts
        };

        // What the trader wants in return
        string[] wantItems = { "cash", "food", "bullets", "clothes" };
        string   wantItem  = wantItems[GameManager.RandInt(0, wantItems.Length - 1)];
        // Avoid offering and wanting the same thing
        if (wantItem == offerItem) wantItem = "cash";

        int wantQty = (offerItem, wantItem) switch
        {
            ("food",    "cash")    => GameManager.RandInt(3, 12),
            ("bullets", "cash")    => GameManager.RandInt(4, 15),
            ("wheel",   "cash")    => GameManager.RandInt(8, 20),
            ("axle",    "cash")    => GameManager.RandInt(10, 25),
            ("tongue",  "cash")    => GameManager.RandInt(8, 18),
            ("clothes", "cash")    => GameManager.RandInt(5, 14),
            ("food",    "bullets") => GameManager.RandInt(15, 40),
            ("bullets", "food")    => GameManager.RandInt(20, 60),
            ("wheel",   "food")    => GameManager.RandInt(40, 100),
            ("axle",    "food")    => GameManager.RandInt(50, 120),
            ("tongue",  "food")    => GameManager.RandInt(40, 90),
            ("clothes", "food")    => GameManager.RandInt(20, 50),
            _                      => GameManager.RandInt(10, 40),
        };

        return new()
        {
            { "type",      "trade" },
            { "offer_item", offerItem },
            { "offer_qty",  offerQty },
            { "want_item",  wantItem },
            { "want_qty",   wantQty },
        };
    }
}
