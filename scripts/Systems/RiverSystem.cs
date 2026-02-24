#nullable enable
using System;
using System.Collections.Generic;
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

        // Guide costs money
        if (method == CrossingMethod.Guide)
        {
            int guideCost = GameManager.RandInt(
                GameConstants.RiverGuideCostMin, GameConstants.RiverGuideCostMax);
            if (st.Cash < guideCost)
                return (false, $"NOT ENOUGH MONEY TO HIRE A GUIDE. NEED ${guideCost}.");
            st.Cash -= guideCost;
        }

        bool success = GameManager.RandFloat() < chance;

        // Guard against duplicate entries (e.g. save-load mid-crossing)
        if (!st.CrossedRivers.Contains(river.Key))
            st.CrossedRivers.Add(river.Key);

        if (success)
        {
            return (true, $"CROSSED {river.Name.ToUpper()} SAFELY.");
        }
        else
        {
            ApplyCrossingFailure(st, river, method);
            return (false, BuildFailureMessage(st, river, method));
        }
    }

    private static void ApplyCrossingFailure(GameState st, GameData.RiverInfo river, CrossingMethod method)
    {
        switch (method)
        {
            case CrossingMethod.Ford:
                // Wagon drags through current: wagon damage, heavy supply loss, health risk
                st.Wagon = Math.Max(0, st.Wagon - GameManager.RandInt(30, 100));
                st.Supplies["food"]    = Math.Max(0, st.Supplies.GetValueOrDefault("food",    0) - GameManager.RandInt(20, 80));
                st.Supplies["bullets"] = Math.Max(0, st.Supplies.GetValueOrDefault("bullets", 0) - GameManager.RandInt(10, 30));
                foreach (var p in st.Living())
                {
                    if (GameManager.RandFloat() < 0.40f)
                    {
                        int dmg = GameManager.RandInt(30, 120);
                        p.Health = Math.Max(0, p.Health - dmg);
                        if (p.Health <= 0) { p.Alive = false; p.Unconscious = false; }
                    }
                }
                break;

            case CrossingMethod.CaulkFloat:
                // Wagon capsizes or takes on water: cargo floats away, no wagon frame damage,
                // heavier random supply loss across all categories
                st.Supplies["food"]    = Math.Max(0, st.Supplies.GetValueOrDefault("food",    0) - GameManager.RandInt(40, 120));
                st.Supplies["bullets"] = Math.Max(0, st.Supplies.GetValueOrDefault("bullets", 0) - GameManager.RandInt(15, 45));
                st.Supplies["clothes"] = Math.Max(0, st.Supplies.GetValueOrDefault("clothes", 0) - GameManager.RandInt(0,  3));
                foreach (var p in st.Living())
                {
                    if (GameManager.RandFloat() < 0.30f)
                    {
                        int dmg = GameManager.RandInt(20, 100);
                        p.Health = Math.Max(0, p.Health - dmg);
                        if (p.Health <= 0) { p.Alive = false; p.Unconscious = false; }
                    }
                }
                break;

            case CrossingMethod.Ferry:
                // Structural failure mid-crossing: lighter losses, ferry operator may help
                st.Supplies["food"]    = Math.Max(0, st.Supplies.GetValueOrDefault("food",    0) - GameManager.RandInt(10, 40));
                st.Supplies["bullets"] = Math.Max(0, st.Supplies.GetValueOrDefault("bullets", 0) - GameManager.RandInt(5,  20));
                foreach (var p in st.Living())
                {
                    if (GameManager.RandFloat() < 0.20f)
                    {
                        int dmg = GameManager.RandInt(20, 80);
                        p.Health = Math.Max(0, p.Health - dmg);
                        if (p.Health <= 0) { p.Alive = false; p.Unconscious = false; }
                    }
                }
                break;

            case CrossingMethod.Guide:
                // Guide mitigates most damage: minimal losses, low health risk
                st.Supplies["food"]    = Math.Max(0, st.Supplies.GetValueOrDefault("food",    0) - GameManager.RandInt(5,  25));
                foreach (var p in st.Living())
                {
                    if (GameManager.RandFloat() < 0.20f)
                    {
                        int dmg = GameManager.RandInt(10, 60);
                        p.Health = Math.Max(0, p.Health - dmg);
                        if (p.Health <= 0) { p.Alive = false; p.Unconscious = false; }
                    }
                }
                break;
        }

        HealthSystem.UpdateUnconsciousFlags(st);
    }

    private static string BuildFailureMessage(GameState st, GameData.RiverInfo river, CrossingMethod method)
    {
        string riverName = river.Name.ToUpper();
        return method switch
        {
            CrossingMethod.Ford       => $"THE WAGON TIPPED CROSSING {riverName}! SUPPLIES AND EQUIPMENT LOST.",
            CrossingMethod.CaulkFloat => $"THE WAGON TOOK ON WATER IN {riverName}! CARGO WASHED AWAY.",
            CrossingMethod.Ferry      => $"THE FERRY HAD TROUBLE ON {riverName}. SOME SUPPLIES LOST.",
            CrossingMethod.Guide      => $"EVEN THE GUIDE STRUGGLED ON {riverName}. MINOR LOSSES.",
            _                         => $"TROUBLE CROSSING {riverName}!",
        };
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
        CrossingMethod.Ford      => "FORD THE RIVER",
        CrossingMethod.CaulkFloat => "CAULK AND FLOAT",
        CrossingMethod.Ferry     => "TAKE THE FERRY",
        CrossingMethod.Guide     => "HIRE A GUIDE",
        _ => "UNKNOWN",
    };

    /// <summary>
    /// Short risk description shown on the choice button.
    /// Does not expose numeric chance so the player cannot game the roll.
    /// </summary>
    public static string MethodRiskLabel(CrossingMethod method) => method switch
    {
        CrossingMethod.Ford       => "RISKY",
        CrossingMethod.CaulkFloat => "MODERATE",
        CrossingMethod.Ferry      => "SAFE",
        CrossingMethod.Guide      => "GUIDED",
        _ => "",
    };

    /// <summary>
    /// Cost annotation for methods that require payment.
    /// Returns empty string for free methods.
    /// </summary>
    public static string MethodCostLabel(GameState st, GameData.RiverInfo river, CrossingMethod method)
    {
        return method switch
        {
            CrossingMethod.Ferry when st.FreeFerryUses > 0
                => "(FREE PASS)",
            CrossingMethod.Ferry
                => $"(${river.FerryCost.min}-${river.FerryCost.max})",
            CrossingMethod.Guide
                => $"(${GameConstants.RiverGuideCostMin}-${GameConstants.RiverGuideCostMax})",
            _ => "",
        };
    }
}
