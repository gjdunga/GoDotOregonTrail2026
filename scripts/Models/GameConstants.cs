#nullable enable
using System.Collections.Generic;

namespace OregonTrail2026.Models;

/// <summary>
/// All magic numbers, thresholds, and configuration values centralized here.
/// Converted from RenPy: OT2026_constants.rpy + static data in OT2026_data.rpy.
/// </summary>
public static class GameConstants
{
    // ========================================================================
    // GAME VERSION
    // ========================================================================
    public const string GameVersion = "1.0.0_godot";
    public const string RenPySourceVersion = "7_2_2b";

    // ========================================================================
    // GAME DISTANCES & MILESTONES
    // ========================================================================
    public const int TargetMiles = 2170;
    public const int TerrainPrairieMax = 250;
    public const int TerrainPlainsMax = 900;
    public const int TerrainHighPlainsMax = 1300;
    public const int TerrainMountainsMax = 1750;
    public const int TerrainRiverValleyMax = 2050;
    public const int RouteDecisionMiles = 1900;
    public const int BarlowRoadMiles = 2050;
    public const int ColumbiaRiverMiles = 2050;

    // ========================================================================
    // TIME & CALENDAR
    // ========================================================================
    public const int StartMonth = 4; // April (1-based)
    public const int StartDayOfMonth = 1;
    public const int DaysPerMonth = 30;
    public static readonly string[] MonthNames =
        { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };

    // ========================================================================
    // HEALTH & SURVIVAL
    // ========================================================================
    public const int HealthMaximum = 1000;
    public const int HealthDead = 0;
    public const int UnconsciousThreshold = 100;
    public const int WakeThreshold = 130;

    public const float FoodRationsFilling = 5.0f;
    public const float FoodRationsMeager = 3.0f;
    public const float FoodRationsBare = 2.0f;

    public const float RecoveryRestingMult = 2.5f;
    public const float RecoveryTravelingMult = 1.0f;
    public const float RecoveryInTownBonus = 1.5f;

    public const float RationsBareRecoveryMult = 0.45f;
    public const float RationsMeagerRecoveryMult = 0.70f;
    public const float RationsFillingRecoveryMult = 1.00f;

    public const float IllnessDamageMin = 14.0f;
    public const float IllnessDamageMax = 52.0f;
    public const float IllnessDamageRestingMult = 0.45f;

    public const float IllnessSnowMult = 1.15f;
    public const float IllnessDustMult = 1.05f;
    public const float IllnessBadWaterMult = 1.10f;

    public const int IllnessRecoveryMin = 40;
    public const int IllnessRecoveryMax = 120;

    public const int SkunkSprayDurationDays = 3;
    public const float SkunkSprayIllnessRiskMult = 1.5f;

    // ========================================================================
    // WAGON & CARGO
    // ========================================================================
    public const int WagonCapacityBase = 2000;
    public const float WagonCapacityMinMult = 0.70f;
    public const float WagonCapacityMaxMult = 1.00f;

    public const int ConditionMaximum = 1000;
    public const int ConditionDead = 0;
    public const int ConditionPoorThreshold = 300;
    public const int ConditionFairThreshold = 600;

    public const float OverloadBreakdownRiskMult = 2.0f;
    public const float OverloadTravelSpeedMult = 0.75f;

    public static readonly Dictionary<string, float> ItemWeights = new()
    {
        { "food", 1.0f },
        { "bullets", 0.05f },
        { "clothes", 5.0f },
        { "wheel", 25.0f },
        { "axle", 40.0f },
        { "tongue", 35.0f },
        { "remedy", 1.0f },
    };

    // ========================================================================
    // TRAVEL & PACE
    // ========================================================================
    public const int PaceRestMiles = 0;
    public const int PaceSteadyMilesMin = 8;
    public const int PaceSteadyMilesMax = 17;
    public const int PaceGruelingMilesMin = 12;
    public const int PaceGruelingMilesMax = 22;

    public const float PaceRestWearMult = 0.0f;
    public const float PaceSteadyWearMult = 1.0f;
    public const float PaceGruelingWearMult = 2.5f;

    public const float PaceSteadyOxenMult = 1.0f;
    public const float PaceGruelingOxenMult = 2.0f;

    // ========================================================================
    // WEATHER IMPACT
    // ========================================================================
    public static readonly Dictionary<(string terrain, string weather), float> WeatherTravelMults = new()
    {
        { ("prairie", "rain"), 0.75f },
        { ("plains", "rain"), 0.75f },
        { ("river_valley", "rain"), 0.75f },
        { ("default", "rain"), 0.90f },
        { ("mountains", "snow"), 0.55f },
        { ("high_plains", "snow"), 0.70f },
        { ("default", "snow"), 0.85f },
        { ("river_valley", "fog"), 0.85f },
        { ("coastal", "fog"), 0.85f },
        { ("default", "fog"), 0.95f },
        { ("prairie", "dust"), 0.88f },
        { ("plains", "dust"), 0.88f },
        { ("high_plains", "dust"), 0.88f },
        { ("default", "dust"), 0.95f },
    };

    public const float BarlowRainSlowdownMult = 0.55f;
    public const float BarlowBaseSpeedMult = 0.85f;
    public const float BarlowWearMult = 1.5f;

    // ========================================================================
    // WAGON REPAIR & BREAKDOWN
    // ========================================================================
    public const float RepairQualityPerfect = 1.0f;
    public const float RepairQualityPoor = 0.3f;
    public const float RepairQualityJuryRig = 0.5f;

    public const float RepairSkillBanker = 0.12f;
    public const float RepairSkillFarmer = 0.48f;
    public const float RepairSkillCarpenter = 0.78f;

    public const float BlacksmithRepairQualityBonus = 0.2f;
    public const float BlacksmithTuneupBreakdownReduction = 0.40f;   // 40% breakdown reduction while tuneup active
    public const float BarlowToll = 5.0f;
    public const float BlacksmithInspectBaseCost  = 10.0f;
    public const float BlacksmithRepairBaseCost   = 30.0f;
    public const float BlacksmithTuneupBaseCost   = 50.0f;
    public const int BlacksmithVoucherDurationDays = 10;
    public const float BlacksmithVoucherBreakdownReduction = 0.5f;

    public const int TuneupDurationMiles = 300;
    public const float TuneupQualityImprovement = 0.1f;

    // ========================================================================
    // PRICING & ECONOMY
    // ========================================================================
    public static readonly int[] CurePriceTable = { 40, 55, 70, 85, 100, 120 };
    public const int CurePriceMax = 250;

    public const float ServiceCarpenterDiscount = 0.75f;
    public const float ServiceBankerGouge = 1.50f;

    // ========================================================================
    // RIVER CROSSING
    // ========================================================================
    public const float RiverBaseFordSuccess = 0.60f;
    public const float RiverBaseCaulkSuccess = 0.75f;
    public const float RiverBaseFerrySuccess = 0.92f;
    public const float RiverBaseGuideSuccess = 0.88f;

    public const float RiverDepthPenaltyPerFt = 0.04f;
    public const float RiverWeatherRainPenalty = 0.04f;
    public const float RiverWeatherSnowPenalty = 0.03f;
    public const float RiverWeatherDustPenalty = 0.01f;

    public const float RiverFrozenEdgesPenalty = 0.15f;
    public const float RiverColumbiaRoutePenalty = 0.20f;
    public const float CaulkingSuccessBonus = 0.10f;
    public const int CaulkingUses = 2;
    public const float RiverNotesGoodBonus = 0.15f;
    public const float RiverNotesBadPenalty = 0.10f;

    // Guide cost range in dollars (paid upfront, reduces failure risk significantly)
    public const int RiverGuideCostMin = 10;
    public const int RiverGuideCostMax = 30;

    // ========================================================================
    // RANDOM EVENTS & ENCOUNTERS
    // ========================================================================
    public const float EventChanceEarly = 0.20f;
    public const float EventChanceMid = 0.30f;
    public const float EventChanceLate = 0.45f;

    public const float HazardBreakdownBase = 0.15f;
    public const float HazardIllnessBase = 0.10f;
    public const float HazardTheftBase = 0.05f;
    public const float HazardLostTrailBase = 0.08f;

    public const float HazardRockslideChance = 0.12f;
    public const float HazardEarlySnowChance = 0.10f;
    public const float HazardFrozenEdgesChance = 0.15f;

    public const int EncounterGuidanceDuration = 5;
    public const int EncounterTerrainWarningDuration = 7;
    public const int EncounterIllnessResistDuration = 4;
    public const int EncounterScoutBonusDuration = 5;

    public const float IntelGoodChance = 0.70f;
    public const int IntelBadDetourMiles = 15;
    public const int IntelPreventHazardUses = 1;

    // ========================================================================
    // JOURNAL & NOTES
    // ========================================================================
    public const int JournalMaxNotes = 80;
    public const int NoteDefaultExpiration = 30;
    public const int NoteServiceDiscountDuration = 10;
    public const float CouponPartsDiscount = 0.90f;
    public const float CouponFoodDiscount = 0.92f;
    public const float ServiceDiscountMult = 0.80f;

    // ========================================================================
    // ROLE SYSTEM
    // ========================================================================
    public const float RoleDriverBreakdownReduction = 0.30f;
    public const float RoleHunterYieldBonus = 1.3f;
    public const float RoleMedicIllnessSlowdown = 0.80f;
    public const float RoleScoutHazardWarning = 0.25f;

    // ========================================================================
    // GAME OVER THRESHOLDS
    // ========================================================================
    public const int GameOverStarvationDays = 5;
    public const int GameOverStrandedDays = 10;
    public const int GameOverAllUnconsciousDays = 7;

    // ========================================================================
    // HUNTING & FISHING
    // ========================================================================
    public const int HuntTimeLimit = 30;
    public const int HuntMaxAmmo = 30;
    public const int HuntMinTargets = 4;
    public const int HuntTargetYMin = 240;
    public const int HuntTargetYMax = 900;
    public const float HuntTargetSpawnDurationMin = 2.8f;
    public const float HuntTargetSpawnDurationMax = 5.5f;
    public const float HuntPredatorThreatMin = 1.3f;
    public const float HuntPredatorThreatMax = 2.4f;
    public const int HuntPredatorDamageMin = 15;
    public const int HuntPredatorDamageMax = 120;

    public const int HuntYieldCapPrairie = 220;
    public const int HuntYieldCapPlains = 220;
    public const int HuntYieldCapHighPlains = 195;
    public const int HuntYieldCapMountains = 165;
    public const int HuntYieldCapRiverValley = 185;
    public const int HuntYieldCapCoastal = 150;

    public const float HuntRainFogMult = 0.90f;
    public const float HuntWinterMult = 0.75f;
    public const float HuntFallMult = 0.92f;
    public const float HuntEfficiencyPoorMult = 0.60f;
    public const float HuntRoleBonusMult = 1.30f;

    public const int FishYieldMin = 8;
    public const int FishYieldMax = 35;
    public const float FishFrozenSpawnMult = 0.2f;
    public const float FishWinterMult = 0.70f;
    public const float FishRiverProximityBonus = 1.5f;

    // ========================================================================
    // UI
    // ========================================================================
    public const int MapLandmarkProximity = 60;
    public const int PartyNameMaxLength = 20;
    public const int PartySize = 5;
    public const int BulkBuyFoodAmount = 100;
    public const int BulkBuyBulletsAmount = 100;

    // ========================================================================
    // DEBUG
    // ========================================================================
    public const string DebugDefaultPassword = "A123456";
}
