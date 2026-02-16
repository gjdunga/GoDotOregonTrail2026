using OregonTrail2026.Models;

namespace OregonTrail2026.Systems;

/// <summary>
/// River crossing: ford, caulk and float, ferry, hire guide.
/// Converted from RenPy: OT2026_rivers.rpy.
/// </summary>
public static class RiverSystem
{
    public enum CrossingMethod { Ford, CaulkFloat, Ferry, Guide }

    /// <summary>Calculate success probability for a crossing method.</summary>
    public static float CalculateSuccessChance(GameState st, GameData.RiverInfo river, CrossingMethod method)
    {
        float depth = GameManager.RandRange(river.DepthFt.min, river.DepthFt.max);
        st.RiverDepthFt = depth;

        float baseChance = method switch
        {
            CrossingMethod.Ford => GameConstants.RiverBaseFordSuccess,
            CrossingMethod.CaulkFloat => GameConstants.RiverBaseCaulkSuccess,
            CrossingMethod.Ferry => GameConstants.RiverBaseFerrySuccess,
            CrossingMethod.Guide => GameConstants.RiverBaseGuideSuccess,
            _ => 0.5f,
        };

        // Depth penalty
        float depthPenalty = (depth - 2f) * GameConstants.RiverDepthPenaltyPerFt;
        baseChance -= depthPenalty;

        // Weather penalties
        if (st.Weather == "rain") baseChance -= GameConstants.RiverWeatherRainPenalty;
        if (st.Weather == "snow") baseChance -= GameConstants.RiverWeatherSnowPenalty;
        if (st.Weather == "dust") baseChance -= GameConstants.RiverWeatherDustPenalty;

        // Frozen edges penalty
        var (month, _) = DateCalc.DayToDate(st.Day);
        if (month is "NOV" or "DEC" or "JAN" or "FEB" &&
            TravelSystem.TerrainByMiles(st.Miles) is "mountains" or "high_plains")
        {
            baseChance -= GameConstants.RiverFrozenEdgesPenalty;
        }

        // Columbia River is harder
        if (river.Key == "columbia" && st.RouteChoice == "river")
            baseChance -= GameConstants.RiverColumbiaRoutePenalty;

        // Guidance bonus
        if (st.GuidanceUntil > st.Day)
            baseChance += 0.04f;

        // River notes
        if (st.RiverNotesUntil > st.Day && st.RiverNotesTarget == river.Key)
            baseChance += GameConstants.RiverNotesGoodBonus;
        if (st.RiverNotesBadUntil > st.Day && st.RiverNotesTarget == river.Key)
            baseChance -= GameConstants.RiverNotesBadPenalty;

        return Math.Clamp(baseChance, 0.05f, 0.98f);
    }

    /// <summary>
    /// Attempt a river crossing. Returns (success, message, consequences).
    /// </summary>
    public static (bool success, string message) AttemptCrossing(
        GameState st, GameData.RiverInfo river, CrossingMethod method)
    {
        float chance = CalculateSuccessChance(st, river, method);

        // Ferry costs money
        if (method == CrossingMethod.Ferry)
        {
            if (st.FreeFerryUses > 0)
            {
                st.FreeFerryUses--;
            }
            else
            {
                int cost = GameManager.RandInt(river.FerryCost.min, river.FerryCost.max);
                if (st.Cash < cost)
                    return (false, $"NOT ENOUGH MONEY FOR FERRY. NEED ${cost}.");
                st.Cash -= cost;
            }
        }

        bool success = GameManager.RandFloat() < chance;

        if (success)
        {
            st.CrossedRivers.Add(river.Key);
            return (true, $"CROSSED {river.Name.ToUpper()} SAFELY.");
        }
        else
        {
            // Failure consequences
            ApplyCrossingFailure(st, river, method);
            st.CrossedRivers.Add(river.Key); // Still crossed, just with damage
            return (false, $"TROUBLE CROSSING {river.Name.ToUpper()}!");
        }
    }

    private static void ApplyCrossingFailure(GameState st, GameData.RiverInfo river, CrossingMethod method)
    {
        // Food/supply loss
        int foodLoss = GameManager.RandInt(20, 80);
        st.Supplies["food"] = Math.Max(0, st.Supplies.GetValueOrDefault("food", 0) - foodLoss);

        int bulletLoss = GameManager.RandInt(10, 30);
        st.Supplies["bullets"] = Math.Max(0, st.Supplies.GetValueOrDefault("bullets", 0) - bulletLoss);

        // Possible health damage
        foreach (var p in st.Living())
        {
            if (GameManager.RandFloat() < 0.4f)
            {
                int dmg = GameManager.RandInt(30, 120);
                p.Health = Math.Max(0, p.Health - dmg);
                if (p.Health <= 0)
                {
                    p.Alive = false;
                    p.Unconscious = false;
                }
            }
        }

        // Wagon damage
        if (method == CrossingMethod.Ford)
        {
            st.Wagon = Math.Max(0, st.Wagon - GameManager.RandInt(30, 100));
        }

        HealthSystem.UpdateUnconsciousFlags(st);
    }

    /// <summary>Get available crossing methods for a river.</summary>
    public static List<CrossingMethod> GetAvailableMethods(GameState st, GameData.RiverInfo river)
    {
        var methods = new List<CrossingMethod> { CrossingMethod.Ford, CrossingMethod.CaulkFloat };
        if (river.HasFerry) methods.Add(CrossingMethod.Ferry);
        if (river.HasGuide) methods.Add(CrossingMethod.Guide);
        return methods;
    }

    /// <summary>Get display name for crossing method.</summary>
    public static string MethodName(CrossingMethod method) => method switch
    {
        CrossingMethod.Ford => "FORD THE RIVER",
        CrossingMethod.CaulkFloat => "CAULK AND FLOAT",
        CrossingMethod.Ferry => "TAKE THE FERRY",
        CrossingMethod.Guide => "HIRE A GUIDE",
        _ => "UNKNOWN",
    };
}
