using OregonTrail2026.Models;

namespace OregonTrail2026.Systems;

/// <summary>
/// Wagon breakdown probability, field repair, and blacksmith repairs.
/// Converted from RenPy: OT2026_repair.rpy.
/// </summary>
public static class RepairSystem
{
    /// <summary>Roll for a random breakdown event.</summary>
    public static void TryBreakdown(GameState st)
    {
        if (st.Pace == "rest") return;

        float baseChance = GameConstants.HazardBreakdownBase;
        float wagonPct = st.Wagon / (float)GameConstants.ConditionMaximum;

        // Worse wagon = higher breakdown chance
        if (wagonPct < 0.3f) baseChance *= 2.5f;
        else if (wagonPct < 0.6f) baseChance *= 1.5f;

        // Grueling pace increases risk
        if (st.Pace == "grueling") baseChance *= GameConstants.PaceGruelingWearMult;

        // Driver role reduces risk
        string driverName = st.Roles.GetValueOrDefault("driver", "");
        if (!string.IsNullOrEmpty(driverName) &&
            st.Living().Any(p => p.Name == driverName && p.IsConscious))
        {
            baseChance *= (1f - GameConstants.RoleDriverBreakdownReduction);
        }

        // Terrain difficulty
        string terrain = TravelSystem.TerrainByMiles(st.Miles);
        if (terrain is "mountains" or "high_plains") baseChance *= 1.3f;

        // Overload risk
        if (CargoSystem.CargoWeight(st) > CargoSystem.CargoCapacity(st))
            baseChance *= GameConstants.OverloadBreakdownRiskMult;

        if (GameManager.RandFloat() >= baseChance) return;

        // Select which part breaks
        string[] parts = { "wheel", "axle", "tongue" };
        float[] weights = { 0.45f, 0.30f, 0.25f };
        string broken = SelectWeighted(parts, weights);

        st.PendingRepair = new()
        {
            { "part", broken },
            { "severity", GameManager.RandFloat() < 0.4f ? "minor" : "major" }
        };

        string eventKey = $"{broken}_broken";
        var card = Array.Find(GameData.EventCards, c => c.Key == eventKey);
        st.LastCard = card?.Image ?? "";
        st.LastEvent = new()
        {
            { "type", eventKey },
            { "text", card?.Text ?? $"WAGON {broken.ToUpper()} BROKE." }
        };
    }

    /// <summary>Attempt a field repair using spare parts or jury-rigging.</summary>
    public static (bool success, string message) AttemptFieldRepair(GameState st, string part)
    {
        int spareParts = st.Supplies.GetValueOrDefault(part, 0);
        float skill = GetRepairSkill(st.Occupation);

        if (spareParts > 0)
        {
            // Use spare part
            st.Supplies[part] = spareParts - 1;

            if (GameManager.RandFloat() < skill)
            {
                // Good repair
                float quality = GameManager.RandRange(0.7f, 1.0f);
                st.RepairQuality[part] = quality;
                st.Wagon = Math.Min(GameConstants.ConditionMaximum,
                    st.Wagon + (int)(200 * quality));
                return (true, $"REPAIRED {part.ToUpper()} SUCCESSFULLY.");
            }
            else
            {
                // Poor repair
                float quality = GameConstants.RepairQualityPoor;
                st.RepairQuality[part] = quality;
                st.Wagon = Math.Min(GameConstants.ConditionMaximum,
                    st.Wagon + (int)(200 * quality));
                return (true, $"REPAIR WAS ROUGH BUT HOLDS FOR NOW.");
            }
        }
        else
        {
            // Jury rig attempt
            if (GameManager.RandFloat() < skill * 0.6f)
            {
                float quality = GameConstants.RepairQualityJuryRig;
                st.RepairQuality[part] = quality;
                st.Wagon = Math.Min(GameConstants.ConditionMaximum,
                    st.Wagon + (int)(100 * quality));
                return (true, $"JURY-RIGGED THE {part.ToUpper()}. WON'T LAST FOREVER.");
            }
            else
            {
                return (false, $"COULD NOT REPAIR THE {part.ToUpper()}. NO SPARE PARTS.");
            }
        }
    }

    /// <summary>Get repair skill based on occupation.</summary>
    public static float GetRepairSkill(string occupation)
    {
        return occupation switch
        {
            "carpenter" => GameConstants.RepairSkillCarpenter,
            "farmer" => GameConstants.RepairSkillFarmer,
            _ => GameConstants.RepairSkillBanker,
        };
    }

    private static string SelectWeighted(string[] items, float[] weights)
    {
        float total = weights.Sum();
        float roll = GameManager.RandFloat() * total;
        float cumulative = 0f;
        for (int i = 0; i < items.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative) return items[i];
        }
        return items[^1];
    }
}
