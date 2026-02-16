using OregonTrail2026.Models;

namespace OregonTrail2026.Systems;

/// <summary>
/// Health, illness, food consumption, recovery, and death processing.
/// Converted from RenPy: OT2026_travel.rpy illness_tick, daily_recovery, apply_daily_consumption.
/// </summary>
public static class HealthSystem
{
    // ========================================================================
    // FOOD CONSUMPTION
    // ========================================================================

    /// <summary>Consume daily food rations. Converted from apply_daily_consumption.</summary>
    public static void ApplyDailyConsumption(GameState st)
    {
        int livingCount = st.Living().Count;
        if (livingCount == 0) return;

        float perPerson = st.Rations.ToLower() switch
        {
            "bare" or "bare bones" or "barebones" => GameConstants.FoodRationsBare,
            "meager" or "meagre" => GameConstants.FoodRationsMeager,
            _ => GameConstants.FoodRationsFilling,
        };

        int totalFood = (int)Math.Ceiling(perPerson * livingCount);
        int currentFood = st.Supplies.GetValueOrDefault("food", 0);

        if (currentFood >= totalFood)
        {
            st.Supplies["food"] = currentFood - totalFood;
            st.StarveDays = 0;
        }
        else
        {
            st.Supplies["food"] = 0;
            st.StarveDays++;

            // Starvation damage to all living members
            foreach (var p in st.Living())
            {
                int damage = GameManager.RandInt(20, 60);
                p.Health = Math.Max(GameConstants.HealthDead, p.Health - damage);
                if (p.Health <= 0)
                {
                    p.Alive = false;
                    p.Unconscious = false;
                }
            }
        }
    }

    // ========================================================================
    // ILLNESS
    // ========================================================================

    /// <summary>Apply daily illness damage to sick party members. Converted from illness_tick.</summary>
    public static void IllnessTick(GameState st, bool resting)
    {
        foreach (var p in st.Living())
        {
            if (string.IsNullOrEmpty(p.Illness)) continue;

            if (p.IllnessDays <= 0)
            {
                // Illness ended naturally
                p.Illness = "";
                p.IllnessSeverity = 0.0f;
                p.IllnessDays = 0;
                // Recovery bonus
                p.Health = Clamp(p.Health + GameManager.RandInt(
                    GameConstants.IllnessRecoveryMin, GameConstants.IllnessRecoveryMax));
                continue;
            }

            float sev = p.IllnessSeverity;
            float baseDmg = GameManager.RandRange(GameConstants.IllnessDamageMin, GameConstants.IllnessDamageMax);
            float dmg = baseDmg * sev;

            if (resting)
                dmg *= GameConstants.IllnessDamageRestingMult;

            // Weather modifiers
            if (st.Weather == "snow") dmg *= GameConstants.IllnessSnowMult;
            if (st.Weather == "dust") dmg *= GameConstants.IllnessDustMult;

            // Medic role reduces illness progression
            if (HasRoleActive(st, "medic"))
                dmg *= GameConstants.RoleMedicIllnessSlowdown;

            p.Health = Math.Max(GameConstants.HealthDead, p.Health - (int)Math.Round(dmg));
            p.IllnessDays--;

            if (p.Health <= 0)
            {
                p.Alive = false;
                p.Unconscious = false;
            }
        }
    }

    /// <summary>Try to infect a party member with a random illness.</summary>
    public static void TryIllness(GameState st)
    {
        var healthy = st.Living().Where(p => string.IsNullOrEmpty(p.Illness)).ToList();
        if (healthy.Count == 0) return;

        // Illness resist bonus
        if (st.IllnessResistUntil > st.Day) return;

        // Skunk spray increases illness risk
        float riskMult = st.SprayedUntil > st.Day
            ? GameConstants.SkunkSprayIllnessRiskMult
            : 1.0f;

        foreach (var ill in GameData.Illnesses)
        {
            if (GameManager.RandFloat() < ill.BaseChance * riskMult)
            {
                var victim = healthy[GameManager.RandInt(0, healthy.Count - 1)];
                victim.Illness = ill.Key;
                victim.IllnessSeverity = ill.Severity;
                victim.IllnessDays = GameManager.RandInt(3, 12);
                break; // Only one illness per tick
            }
        }
    }

    // ========================================================================
    // RECOVERY
    // ========================================================================

    /// <summary>Apply daily health recovery. Converted from daily_recovery.</summary>
    public static void DailyRecovery(GameState st, bool resting, bool inTown)
    {
        float rationsMult = GetRationsRecoveryMult(st.Rations);

        foreach (var p in st.Living())
        {
            if (!string.IsNullOrEmpty(p.Illness)) continue; // Sick people don't recover normally

            float baseMult = resting
                ? GameConstants.RecoveryRestingMult
                : GameConstants.RecoveryTravelingMult;

            if (inTown) baseMult *= GameConstants.RecoveryInTownBonus;

            int recovery = (int)Math.Round(GameManager.RandInt(5, 25) * baseMult * rationsMult);
            p.Health = Clamp(p.Health + recovery);
        }
    }

    // ========================================================================
    // UNCONSCIOUS FLAGS
    // ========================================================================

    /// <summary>Update unconscious flags for all party members.</summary>
    public static void UpdateUnconsciousFlags(GameState st)
    {
        foreach (var p in st.Party)
        {
            if (!p.Alive)
            {
                p.Unconscious = false;
                continue;
            }
            if (p.Health <= GameConstants.UnconsciousThreshold)
                p.Unconscious = true;
            else if (p.Health >= GameConstants.WakeThreshold)
                p.Unconscious = false;
        }
    }

    // ========================================================================
    // MEDICINE / TREATMENT
    // ========================================================================

    /// <summary>Treat a sick person with a remedy. Returns true on success.</summary>
    public static bool TreatPerson(GameState st, Person person)
    {
        if (string.IsNullOrEmpty(person.Illness)) return false;

        string illKey = person.Illness;
        int have = st.Remedies.GetValueOrDefault(illKey, 0);
        if (have <= 0) return false;

        st.Remedies[illKey] = have - 1;

        // Treatment effect: shorten + reduce severity + heal
        person.IllnessDays = Math.Max(0, person.IllnessDays - GameManager.RandInt(2, 5));
        person.IllnessSeverity = Math.Max(0.12f, person.IllnessSeverity - 0.18f);
        person.Health = Clamp(person.Health + GameManager.RandInt(120, 260));

        if (person.IllnessDays <= 0)
        {
            person.Illness = "";
            person.IllnessSeverity = 0f;
            person.IllnessDays = 0;
        }

        UpdateUnconsciousFlags(st);
        return true;
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    public static int Clamp(int v, int lo = GameConstants.HealthDead, int hi = GameConstants.HealthMaximum)
        => Math.Max(lo, Math.Min(hi, v));

    public static float GetRationsRecoveryMult(string rations)
    {
        return rations.ToLower() switch
        {
            "bare" or "bare bones" or "barebones" => GameConstants.RationsBareRecoveryMult,
            "meager" or "meagre" => GameConstants.RationsMeagerRecoveryMult,
            _ => GameConstants.RationsFillingRecoveryMult,
        };
    }

    private static bool HasRoleActive(GameState st, string role)
    {
        if (!st.Roles.TryGetValue(role, out string? name) || string.IsNullOrEmpty(name))
            return false;
        return st.Living().Any(p => p.Name == name && p.IsConscious);
    }
}
