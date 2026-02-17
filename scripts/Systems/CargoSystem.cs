#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using OregonTrail2026.Models;

namespace OregonTrail2026.Systems;

/// <summary>
/// Cargo weight/capacity calculations.
/// Converted from RenPy: OT2026_data.rpy cargo_capacity, cargo_weight, etc.
/// </summary>
public static class CargoSystem
{
    public static int CargoCapacity(GameState st)
    {
        float w = st.Wagon / 1000.0f;
        float mult = 0.70f + 0.30f * Math.Clamp(w, 0f, 1f);
        return (int)Math.Round(GameConstants.WagonCapacityBase * mult);
    }

    public static int CargoWeight(GameState st)
    {
        float w = 0f;
        var s = st.Supplies;
        w += s.GetValueOrDefault("food", 0) * GameConstants.ItemWeights["food"];
        w += s.GetValueOrDefault("bullets", 0) * GameConstants.ItemWeights["bullets"];
        w += s.GetValueOrDefault("clothes", 0) * GameConstants.ItemWeights["clothes"];
        w += s.GetValueOrDefault("wheel", 0) * GameConstants.ItemWeights["wheel"];
        w += s.GetValueOrDefault("axle", 0) * GameConstants.ItemWeights["axle"];
        w += s.GetValueOrDefault("tongue", 0) * GameConstants.ItemWeights["tongue"];

        // Remedies
        foreach (var kv in st.Remedies)
            w += kv.Value * GameConstants.ItemWeights["remedy"];

        return (int)Math.Round(w);
    }

    public static bool CanAddWeight(GameState st, float addW)
        => CargoWeight(st) + (int)Math.Round(addW) <= CargoCapacity(st);

    /// <summary>Add food up to remaining capacity. Returns amount actually added.</summary>
    public static int AddFoodWithCapacity(GameState st, int pounds)
    {
        int cap = CargoCapacity(st);
        int cur = CargoWeight(st);
        int space = Math.Max(0, cap - cur);
        int add = Math.Min(pounds, space);
        if (add > 0)
            st.Supplies["food"] = st.Supplies.GetValueOrDefault("food", 0) + add;
        return add;
    }
}

/// <summary>
/// Date calculation utilities.
/// Converted from RenPy: OT2026_data.rpy day_to_date, date_str.
/// </summary>
public static class DateCalc
{
    /// <summary>Convert game day number to (month_name, day_of_month). Day 0 = April 1.</summary>
    public static (string month, int dayOfMonth) DayToDate(int day)
    {
        int monthIdx = (GameConstants.StartMonth - 1 + day / 30) % 12;
        int dom = GameConstants.StartDayOfMonth + (day % 30);
        return (GameConstants.MonthNames[monthIdx], dom);
    }

    public static string DateStr(int day)
    {
        var (m, dom) = DayToDate(day);
        return $"{m} {dom}";
    }

    public static bool IsWinter(int day)
    {
        var (m, _) = DayToDate(day);
        return m is "NOV" or "DEC" or "JAN" or "FEB";
    }

    public static bool IsFall(int day)
    {
        var (m, _) = DayToDate(day);
        return m is "SEP" or "OCT";
    }

    public static string SeasonName(int day)
    {
        var (m, _) = DayToDate(day);
        return m switch
        {
            "DEC" or "JAN" or "FEB" => "Winter",
            "MAR" or "APR" or "MAY" => "Spring",
            "JUN" or "JUL" or "AUG" => "Summer",
            _ => "Fall",
        };
    }
}
