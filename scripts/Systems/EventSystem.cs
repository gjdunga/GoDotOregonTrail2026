using System;
using System.Collections.Generic;
using System.Linq;
using OregonTrail2026.Models;

namespace OregonTrail2026.Systems;

/// <summary>
/// Random trail events: breakdowns, theft, good weather, encounters.
/// Converted from RenPy: OT2026_encounters.rpy, OT2026_travel.rpy event logic.
/// </summary>
public static class EventSystem
{
    /// <summary>Roll for a random event and apply it to state.</summary>
    public static void TryRandomEvent(GameState st)
    {
        float eventChance = st.Miles switch
        {
            < 500 => GameConstants.EventChanceEarly,
            > 1500 => GameConstants.EventChanceLate,
            _ => GameConstants.EventChanceMid,
        };

        // Scout bonus reduces event chance
        if (st.ScoutBonusUntil > st.Day)
            eventChance *= (1f - GameConstants.RoleScoutHazardWarning);

        if (GameManager.RandFloat() >= eventChance) return;

        // Weighted event selection
        float roll = GameManager.RandFloat();
        string terrain = TravelSystem.TerrainByMiles(st.Miles);

        if (roll < GameConstants.HazardIllnessBase)
        {
            HealthSystem.TryIllness(st);
            return;
        }

        if (roll < GameConstants.HazardIllnessBase + GameConstants.HazardTheftBase)
        {
            ApplyTheft(st);
            return;
        }

        if (roll < 0.30f)
        {
            ApplyBadWater(st);
            return;
        }

        if (roll < 0.45f)
        {
            ApplyLostTrail(st);
            return;
        }

        // Mountain hazards
        if (terrain == "mountains")
        {
            if (GameManager.RandFloat() < GameConstants.HazardRockslideChance)
            {
                ApplyRockslide(st);
                return;
            }
            if (GameManager.RandFloat() < GameConstants.HazardEarlySnowChance)
            {
                st.Weather = "snow";
                st.LastCard = "res://assets/images/events/evt_early_snow.webp";
                st.LastEvent = new() { { "type", "early_snow" }, { "text", "EARLY SNOW." } };
                return;
            }
        }

        // Good events
        if (roll > 0.75f)
        {
            ApplyGoodEvent(st);
            return;
        }

        // Encounter
        if (roll > 0.60f)
        {
            GenerateEncounter(st);
        }
    }

    // ---- Specific event handlers ----

    private static void ApplyTheft(GameState st)
    {
        int foodStolen = GameManager.RandInt(10, 50);
        int bulletsStolen = GameManager.RandInt(5, 20);
        st.Supplies["food"] = Math.Max(0, st.Supplies.GetValueOrDefault("food", 0) - foodStolen);
        st.Supplies["bullets"] = Math.Max(0, st.Supplies.GetValueOrDefault("bullets", 0) - bulletsStolen);
        st.LastCard = "res://assets/images/events/evt_thief_night.webp";
        st.LastEvent = new() { { "type", "thief" }, { "text", "THIEVES IN THE NIGHT." } };
    }

    private static void ApplyBadWater(GameState st)
    {
        // Bad water increases illness chance
        foreach (var p in st.Living())
        {
            if (GameManager.RandFloat() < 0.25f && string.IsNullOrEmpty(p.Illness))
            {
                p.Illness = "food_poisoning";
                p.IllnessSeverity = 0.35f;
                p.IllnessDays = GameManager.RandInt(2, 5);
                break;
            }
        }
        st.LastCard = "res://assets/images/events/evt_bad_water.webp";
        st.LastEvent = new() { { "type", "bad_water" }, { "text", "BAD WATER." } };
    }

    private static void ApplyLostTrail(GameState st)
    {
        int lostMiles = GameManager.RandInt(5, 15);
        st.Miles = Math.Max(0, st.Miles - lostMiles);
        st.LastCard = "res://assets/images/events/evt_lost_trail.webp";
        st.LastEvent = new() { { "type", "lost_trail" }, { "text", $"LOST THE TRAIL. LOST {lostMiles} MILES." } };
    }

    private static void ApplyRockslide(GameState st)
    {
        int wagonDmg = GameManager.RandInt(50, 150);
        st.Wagon = Math.Max(0, st.Wagon - wagonDmg);
        foreach (var p in st.Living())
        {
            if (GameManager.RandFloat() < 0.3f)
                p.Health = Math.Max(0, p.Health - GameManager.RandInt(30, 100));
        }
        st.LastCard = "res://assets/images/events/evt_rockslide.webp";
        st.LastEvent = new() { { "type", "rockslide" }, { "text", "ROCKSLIDE!" } };
    }

    private static void ApplyGoodEvent(GameState st)
    {
        float roll = GameManager.RandFloat();
        if (roll < 0.4f)
        {
            // Found berries
            int food = GameManager.RandInt(15, 40);
            CargoSystem.AddFoodWithCapacity(st, food);
            st.LastCard = "res://assets/images/events/evt_found_berries.webp";
            st.LastEvent = new() { { "type", "find_berries" }, { "text", $"FOUND BERRIES. +{food} LBS FOOD." } };
        }
        else if (roll < 0.7f)
        {
            // Good weather
            st.Weather = "clear";
            st.LastCard = "res://assets/images/events/evt_good_weather.webp";
            st.LastEvent = new() { { "type", "good_weather" }, { "text", "GOOD WEATHER." } };
        }
        else
        {
            // Found wagon parts
            string[] parts = { "wheel", "axle", "tongue" };
            string part = parts[GameManager.RandInt(0, parts.Length - 1)];
            st.Supplies[part] = st.Supplies.GetValueOrDefault(part, 0) + 1;
            st.LastCard = "res://assets/images/events/evt_found_wagon_parts.webp";
            st.LastEvent = new() { { "type", "find_wagon_parts" }, { "text", $"FOUND A SPARE {part.ToUpper()}." } };
        }
    }

    private static void GenerateEncounter(GameState st)
    {
        float roll = GameManager.RandFloat();
        if (roll < 0.3f)
        {
            // Guidance encounter
            st.GuidanceUntil = st.Day + GameConstants.EncounterGuidanceDuration;
            st.LastCard = "res://assets/images/events/evt_enc_guidance.webp";
            st.LastEvent = new() { { "type", "enc_guidance" }, { "text", "A TRAVELER SHARES LOCAL KNOWLEDGE." } };
        }
        else if (roll < 0.5f)
        {
            // Medical help
            var sick = st.Living().Where(p => !string.IsNullOrEmpty(p.Illness)).ToList();
            if (sick.Count > 0)
            {
                var patient = sick[GameManager.RandInt(0, sick.Count - 1)];
                patient.Health = HealthSystem.Clamp(patient.Health + GameManager.RandInt(80, 200));
                patient.IllnessDays = Math.Max(0, patient.IllnessDays - GameManager.RandInt(1, 3));
            }
            st.LastCard = "res://assets/images/events/evt_enc_medical_help.webp";
            st.LastEvent = new() { { "type", "enc_medical_help" }, { "text", "A TRAVELING MEDIC HELPS." } };
        }
        else if (roll < 0.7f)
        {
            // Terrain warning
            st.TerrainWarningUntil = st.Day + GameConstants.EncounterTerrainWarningDuration;
            st.LastCard = "res://assets/images/events/evt_enc_terrain_warning.webp";
            st.LastEvent = new() { { "type", "enc_terrain_warning" }, { "text", "WARNED ABOUT TERRAIN AHEAD." } };
        }
        else
        {
            // Trade encounter
            st.PendingEncounter = new() { { "type", "trade" } };
            st.LastCard = "res://assets/images/events/evt_enc_trade.webp";
            st.LastEvent = new() { { "type", "enc_trade" }, { "text", "A TRADER OFFERS TO TRADE." } };
        }
    }
}
