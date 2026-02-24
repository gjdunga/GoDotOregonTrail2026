#nullable enable
using System;
using System.Collections.Generic;
using OregonTrail2026.Models;

namespace OregonTrail2026.Systems;

/// <summary>
/// Store pricing, region/season modifiers, buying mechanics.
/// Converted from RenPy: OT2026_data.rpy pricing functions + OT2026_store.rpy.
/// </summary>
public static class EconomySystem
{
    /// <summary>Progress factor: 0.0 at Independence, 1.0 near Oregon City.</summary>
    public static float ProgressFactor(GameState st)
        => Math.Clamp(st.Miles / (float)GameConstants.TargetMiles, 0f, 1f);

    /// <summary>Region-based price multiplier for an item type.</summary>
    public static float RegionPriceMult(GameState st, string itemType)
    {
        string terrain = TravelSystem.TerrainByMiles(st.Miles);
        float baseMult = terrain switch
        {
            "prairie" => 0.98f,
            "plains" => 1.00f,
            "high_plains" => 1.06f,
            "mountains" => 1.18f,
            "river_valley" => 1.10f,
            "coastal" => 1.12f,
            _ => 1.00f,
        };

        // Category-specific terrain tilts
        if (itemType == "parts" && terrain is "mountains" or "river_valley")
            baseMult *= 1.10f;
        if (itemType == "livestock" && terrain is "high_plains" or "mountains" or "river_valley" or "coastal")
            baseMult *= 1.08f;
        if (itemType == "ammo" && terrain is "mountains" or "coastal")
            baseMult *= 1.06f;
        if (itemType == "food" && terrain is "mountains" or "coastal")
            baseMult *= 1.05f;

        return baseMult;
    }

    /// <summary>Season-based price multiplier.</summary>
    public static float SeasonPriceMult(GameState st, string itemType)
    {
        var (month, _) = DateCalc.DayToDate(st.Day);
        bool winter = month is "NOV" or "DEC" or "JAN" or "FEB";
        bool fall = month is "SEP" or "OCT";

        float mult = 1.0f;
        if (fall)
        {
            if (itemType is "food" or "ammo") mult *= 1.05f;
            if (itemType == "parts") mult *= 1.06f;
        }
        if (winter)
        {
            if (itemType is "food" or "ammo") mult *= 1.12f;
            if (itemType == "clothes") mult *= 1.20f;
            if (itemType == "parts") mult *= 1.10f;
        }
        return mult;
    }

    /// <summary>Progression-based price multiplier.</summary>
    public static float ProgressionPriceMult(GameState st, string itemType)
    {
        float p = ProgressFactor(st);
        return itemType switch
        {
            "food" => 1.0f + 0.10f * p,
            "ammo" => 1.0f + 0.12f * p,
            "parts" => 1.0f + 0.18f * p,
            "livestock" => 1.0f + 0.15f * p,
            "clothes" => 1.0f + 0.08f * p,
            _ => 1.0f + 0.10f * p,
        };
    }

    /// <summary>Calculate final price for an item at a specific store.</summary>
    public static float CalculatePrice(GameState st, string storeKey, string itemKey)
    {
        if (!GameData.Prices.TryGetValue(storeKey, out var storePrices))
            return 0f;

        float basePrice = itemKey switch
        {
            "yoke_oxen" => storePrices.YokeOxen,
            "food_lb" => storePrices.FoodLb,
            "clothes_set" => storePrices.ClothesSet,
            "bullets_box" => storePrices.BulletsBox,
            "spare_wheel" => storePrices.SpareWheel,
            "spare_axle" => storePrices.SpareAxle,
            "spare_tongue" => storePrices.SpareTongue,
            _ => 0f,
        };

        if (basePrice <= 0) return 0f;

        string itemType = GameData.ItemTypes.GetValueOrDefault(itemKey, "food");

        // Apply all multipliers
        float storeProfileMult = 1.0f;
        if (GameData.StoreProfiles.TryGetValue(storeKey, out var profile))
            storeProfileMult = profile.PriceMult.GetValueOrDefault(itemType, 1.0f);

        float finalPrice = basePrice
            * storeProfileMult
            * RegionPriceMult(st, itemType)
            * SeasonPriceMult(st, itemType)
            * ProgressionPriceMult(st, itemType);

        // Occupation-specific service pricing
        if (st.Occupation == "carpenter")
            finalPrice *= GameConstants.ServiceCarpenterDiscount;
        else if (st.Occupation == "banker")
            finalPrice *= GameConstants.ServiceBankerGouge;

        return (float)Math.Round(finalPrice, 2);
    }

    /// <summary>Attempt to buy an item. Returns (success, message).</summary>
    public static (bool success, string message) BuyItem(GameState st, string storeKey, string itemKey, int quantity)
    {
        float unitPrice = CalculatePrice(st, storeKey, itemKey);
        float totalCost = unitPrice * quantity;

        if (st.Cash < totalCost)
            return (false, "NOT ENOUGH MONEY.");

        // Apply purchase
        st.Cash -= totalCost;

        switch (itemKey)
        {
            case "food_lb":
                int added = CargoSystem.AddFoodWithCapacity(st, quantity);
                if (added < quantity)
                {
                    float refund = (quantity - added) * unitPrice;
                    st.Cash += refund;
                    return (true, $"BOUGHT {added} LBS FOOD (WAGON FULL). ${totalCost - refund:F2}");
                }
                break;
            case "bullets_box":
                st.Supplies["bullets"] = st.Supplies.GetValueOrDefault("bullets", 0) + quantity * 20;
                break;
            case "clothes_set":
                st.Supplies["clothes"] = st.Supplies.GetValueOrDefault("clothes", 0) + quantity;
                break;
            case "yoke_oxen":
                st.Supplies["oxen"] = st.Supplies.GetValueOrDefault("oxen", 0) + quantity;
                st.OxenCondition = Math.Min(GameConstants.ConditionMaximum, st.OxenCondition + 200 * quantity);
                break;
            case "spare_wheel":
                st.Supplies["wheel"] = st.Supplies.GetValueOrDefault("wheel", 0) + quantity;
                break;
            case "spare_axle":
                st.Supplies["axle"] = st.Supplies.GetValueOrDefault("axle", 0) + quantity;
                break;
            case "spare_tongue":
                st.Supplies["tongue"] = st.Supplies.GetValueOrDefault("tongue", 0) + quantity;
                break;
        }

        st.Ledger.Add(new()
        {
            { "day", st.Day },
            { "action", "buy" },
            { "item", itemKey },
            { "qty", quantity },
            { "cost", totalCost },
            { "store", storeKey },
        });

        return (true, $"BOUGHT {quantity}x {itemKey.Replace("_", " ").ToUpper()}. ${totalCost:F2}");
    }


    // ========================================================================
    // CURE PURCHASING
    // ========================================================================

    /// <summary>
    /// Buy a cure for one party member afflicted with illnessKey.
    /// Cost comes from State.CurePrices (seeded by RegenCurePrices on EnterTown).
    /// Clears illness fields on the first living member found with that illness.
    /// </summary>
    public static (bool success, string message) BuyCure(GameState st, string illnessKey)
    {
        if (!st.CurePrices.TryGetValue(illnessKey, out int price))
            return (false, "CURE NOT AVAILABLE HERE.");

        if (st.Cash < price)
            return (false, $"NOT ENOUGH MONEY. NEED ${price}.");

        var patient = st.Living().Find(p => p.Illness == illnessKey);
        if (patient == null)
            return (false, "NO ONE IN THE PARTY HAS THIS ILLNESS.");

        st.Cash -= price;
        patient.Illness = "";
        patient.IllnessSeverity = 0f;
        patient.IllnessDays = 0;

        st.Ledger.Add(new()
        {
            { "day", st.Day },
            { "action", "buy_cure" },
            { "item", illnessKey },
            { "qty", 1 },
            { "cost", (float)price },
            { "store", st.AtTownStoreKey },
        });

        string illName = GameData.IllnessDisplayName(illnessKey).ToUpper();
        return (true, $"CURED {patient.Name.ToUpper()} OF {illName}.");
    }

    // ========================================================================
    // SOLDOUT SEEDING
    // ========================================================================

    /// <summary>
    /// Roll soldout state for each supply category at the current store.
    /// Called once per store visit from FortStoreScreen._Ready so the state
    /// is stable for the duration of the visit.
    /// Keys written: "{storeKey}_{category}" e.g. "fort_kearny_food".
    /// Categories: food, ammo, clothes, livestock, parts, cures.
    /// </summary>
    public static void SeedStoreSoldout(GameState st, string storeKey)
    {
        if (!GameData.StoreProfiles.TryGetValue(storeKey, out var profile)) return;

        string[] categories = { "food", "ammo", "clothes", "livestock", "parts", "cures" };
        foreach (string cat in categories)
        {
            string key = $"{storeKey}_{cat}";
            // Only roll if not already seeded this visit
            if (!st.StoreSoldout.ContainsKey(key))
            {
                float baseChance = profile.SoldoutBase.GetValueOrDefault(cat, 0f);
                st.StoreSoldout[key] = GameManager.RandFloat() < baseChance;
            }
        }
    }

    /// <summary>Returns true if the given category is soldout at the given store.</summary>
    public static bool IsSoldout(GameState st, string storeKey, string category) =>
        st.StoreSoldout.GetValueOrDefault($"{storeKey}_{category}", false);

    /// <summary>Clear soldout flags for a store on departure so the next visit re-rolls.</summary>
    public static void ClearStoreSoldout(GameState st, string storeKey)
    {
        string[] categories = { "food", "ammo", "clothes", "livestock", "parts", "cures" };
        foreach (string cat in categories)
            st.StoreSoldout.Remove($"{storeKey}_{cat}");
    }

    /// <summary>Regenerate cure prices for current store.</summary>
    public static void RegenCurePrices(GameState st, string? storeKey = null)
    {
        st.CurePrices.Clear();
        float p = ProgressFactor(st);
        var (month, _) = DateCalc.DayToDate(st.Day);
        bool winter = month is "NOV" or "DEC" or "JAN" or "FEB";
        bool fall = month is "SEP" or "OCT";

        foreach (var ill in GameData.Illnesses)
        {
            int basePrice = GameConstants.CurePriceTable[
                GameManager.RandInt(0, GameConstants.CurePriceTable.Length - 1)];
            float mult = 1.0f + 0.18f * p;

            if (storeKey != null && GameData.StoreProfiles.TryGetValue(storeKey, out var profile))
                mult *= profile.PriceMult.GetValueOrDefault("cures", 1.0f);

            if (fall) mult *= 1.05f;
            if (winter) mult *= 1.12f;

            st.CurePrices[ill.Key] = Math.Min(GameConstants.CurePriceMax, (int)Math.Round(basePrice * mult));
        }
    }
}
