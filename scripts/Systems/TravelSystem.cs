using OregonTrail2026.Models;

namespace OregonTrail2026.Systems;

/// <summary>
/// Terrain classification, weather generation, daily distance calculation, wagon/oxen wear.
/// Converted from RenPy: OT2026_travel.rpy terrain_by_miles, apply_weather, travel_one_day.
/// </summary>
public static class TravelSystem
{
    // ========================================================================
    // TERRAIN
    // ========================================================================

    /// <summary>Determine terrain type based on mile marker.</summary>
    public static string TerrainByMiles(int miles)
    {
        if (miles < GameConstants.TerrainPrairieMax) return "prairie";
        if (miles < GameConstants.TerrainPlainsMax) return "plains";
        if (miles < GameConstants.TerrainHighPlainsMax) return "high_plains";
        if (miles < GameConstants.TerrainMountainsMax) return "mountains";
        if (miles < GameConstants.TerrainRiverValleyMax) return "river_valley";
        return "coastal";
    }

    /// <summary>Get travel speed multiplier for terrain + weather combo.</summary>
    public static float GetWeatherTravelMult(string terrain, string weather)
    {
        if (weather == "clear") return 1.0f;

        var key = (terrain, weather);
        if (GameConstants.WeatherTravelMults.TryGetValue(key, out float mult))
            return mult;

        var defaultKey = ("default", weather);
        return GameConstants.WeatherTravelMults.GetValueOrDefault(defaultKey, 1.0f);
    }

    // ========================================================================
    // WEATHER GENERATION
    // ========================================================================

    /// <summary>Apply weather for the current day. Converted from apply_weather().</summary>
    public static void ApplyWeather(GameState st)
    {
        string terrain = TerrainByMiles(st.Miles);
        var (monthName, _) = DateCalc.DayToDate(st.Day);
        bool winter = monthName is "NOV" or "DEC" or "JAN" or "FEB";
        bool fall = monthName is "SEP" or "OCT";

        float roll = GameManager.RandFloat();

        // Snow chance increases in winter and at high elevations
        float snowChance = terrain switch
        {
            "mountains" => winter ? 0.40f : fall ? 0.15f : 0.05f,
            "high_plains" => winter ? 0.30f : fall ? 0.10f : 0.02f,
            _ => winter ? 0.15f : 0.0f,
        };

        float rainChance = terrain switch
        {
            "prairie" or "plains" => 0.20f,
            "river_valley" or "coastal" => 0.25f,
            _ => 0.15f,
        };

        float dustChance = terrain switch
        {
            "prairie" or "plains" or "high_plains" => winter ? 0.02f : 0.10f,
            _ => 0.03f,
        };

        float fogChance = terrain switch
        {
            "river_valley" or "coastal" => 0.12f,
            _ => 0.05f,
        };

        if (roll < snowChance)
            st.Weather = "snow";
        else if (roll < snowChance + rainChance)
            st.Weather = "rain";
        else if (roll < snowChance + rainChance + dustChance)
            st.Weather = "dust";
        else if (roll < snowChance + rainChance + dustChance + fogChance)
            st.Weather = "fog";
        else
            st.Weather = "clear";
    }

    // ========================================================================
    // DAILY DISTANCE
    // ========================================================================

    /// <summary>Calculate daily travel distance based on pace, weather, terrain, cargo.</summary>
    public static int CalculateDailyDistance(GameState st)
    {
        if (st.Pace == "rest") return 0;

        int baseMin, baseMax;
        if (st.Pace == "grueling")
        {
            baseMin = GameConstants.PaceGruelingMilesMin;
            baseMax = GameConstants.PaceGruelingMilesMax;
        }
        else // steady
        {
            baseMin = GameConstants.PaceSteadyMilesMin;
            baseMax = GameConstants.PaceSteadyMilesMax;
        }

        int baseDist = GameManager.RandInt(baseMin, baseMax);
        float mult = GetWeatherTravelMult(TerrainByMiles(st.Miles), st.Weather);

        // Overload penalty
        int weight = CargoSystem.CargoWeight(st);
        int capacity = CargoSystem.CargoCapacity(st);
        if (weight > capacity)
            mult *= GameConstants.OverloadTravelSpeedMult;

        // Oxen condition penalty
        float oxenPct = st.OxenCondition / (float)GameConstants.ConditionMaximum;
        if (oxenPct < 0.3f)
            mult *= 0.5f;
        else if (oxenPct < 0.6f)
            mult *= 0.75f;

        // Barlow Road penalty
        if (st.RouteChoice == "barlow" && st.Miles >= GameConstants.BarlowRoadMiles)
        {
            mult *= GameConstants.BarlowBaseSpeedMult;
            if (st.Weather == "rain")
                mult *= GameConstants.BarlowRainSlowdownMult;
        }

        return Math.Max(1, (int)Math.Round(baseDist * mult));
    }

    // ========================================================================
    // WAGON & OXEN WEAR
    // ========================================================================

    public static void ApplyWagonWear(GameState st)
    {
        if (st.Pace == "rest") return;

        float wearMult = st.Pace == "grueling"
            ? GameConstants.PaceGruelingWearMult
            : GameConstants.PaceSteadyWearMult;

        string terrain = TerrainByMiles(st.Miles);
        float terrainMult = terrain switch
        {
            "mountains" => 1.5f,
            "high_plains" => 1.2f,
            "river_valley" => 1.1f,
            _ => 1.0f,
        };

        if (st.RouteChoice == "barlow" && st.Miles >= GameConstants.BarlowRoadMiles)
            terrainMult *= GameConstants.BarlowWearMult;

        int wear = (int)Math.Round(GameManager.RandInt(2, 8) * wearMult * terrainMult);
        st.Wagon = Math.Max(0, st.Wagon - wear);
    }

    public static void ApplyOxenWear(GameState st)
    {
        if (st.Pace == "rest") return;

        float mult = st.Pace == "grueling"
            ? GameConstants.PaceGruelingOxenMult
            : GameConstants.PaceSteadyOxenMult;

        int wear = (int)Math.Round(GameManager.RandInt(1, 5) * mult);
        st.OxenCondition = Math.Max(0, st.OxenCondition - wear);
    }

    // ========================================================================
    // STOP DETECTION
    // ========================================================================

    /// <summary>Find the next unvisited town or uncrossed river between miles a and b.</summary>
    public static (string? type, object? data) NextStopBetween(GameState st, int a, int b)
    {
        var town = NextUnvisitedTownBetween(st, a, b);
        var river = NextUncrossedRiverBetween(st, a, b);

        if (town != null && river != null)
            return town.Value.Miles <= river.Value.Miles ? ("town", town) : ("river", river);
        if (town != null) return ("town", town);
        if (river != null) return ("river", river);
        return (null, null);
    }

    private static GameData.LandmarkInfo? NextUnvisitedTownBetween(GameState st, int a, int b)
    {
        var visited = new HashSet<string>(st.VisitedLandmarks);
        return GameData.Landmarks
            .Where(lm => lm.IsTown && lm.StoreKey != null && lm.Miles > a && lm.Miles <= b && !visited.Contains(lm.Name))
            .OrderBy(lm => lm.Miles)
            .FirstOrDefault();
    }

    private static GameData.RiverInfo? NextUncrossedRiverBetween(GameState st, int a, int b)
    {
        var crossed = new HashSet<string>(st.CrossedRivers);
        return GameData.Rivers
            .Where(rv => rv.Miles > a && rv.Miles <= b && !crossed.Contains(rv.Key))
            .OrderBy(rv => rv.Miles)
            .FirstOrDefault();
    }

    // ========================================================================
    // BACKGROUND SELECTION
    // ========================================================================

    /// <summary>Get the background image path for the current travel state.</summary>
    public static string TravelBgForState(GameState st)
    {
        string terrain = TerrainByMiles(st.Miles);
        bool night = st.Day % 2 == 1; // Simplified day/night cycle

        // Check if near a landmark
        foreach (var lm in GameData.Landmarks)
        {
            if (Math.Abs(st.Miles - lm.Miles) <= GameConstants.MapLandmarkProximity)
                return lm.BgImage;
        }

        return terrain switch
        {
            "prairie" or "plains" => night
                ? "res://assets/images/bg/bg_trail_plains_night.webp"
                : "res://assets/images/bg/bg_trail_plains_day.webp",
            "mountains" => "res://assets/images/bg/bg_blue_mountains.webp",
            "river_valley" => "res://assets/images/bg/bg_snake_river.webp",
            "coastal" => "res://assets/images/bg/bg_columbia_gorge.webp",
            _ => "res://assets/images/bg/bg_trail_plains_day.webp",
        };
    }

    /// <summary>Get camp background for resting.</summary>
    public static string CampBgForState(GameState st)
    {
        bool night = st.Day % 2 == 1;
        return night
            ? "res://assets/images/bg/bg_camp_night.webp"
            : "res://assets/images/bg/bg_camp_day.webp";
    }
}
