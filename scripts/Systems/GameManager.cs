using System;
using System.Collections.Generic;
using Godot;
using OregonTrail2026.Models;

namespace OregonTrail2026.Systems;

/// <summary>
/// Global game manager autoload. Orchestrates game state, scene transitions,
/// and cross-system coordination.
/// Converted from RenPy: label start + label travel_loop in OT2026_main.rpy.
/// </summary>
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; } = null!;

    public GameState State { get; set; } = null!;

    // Signals for UI updates
    [Signal] public delegate void StateChangedEventHandler();
    [Signal] public delegate void MessageDisplayedEventHandler(string message);
    [Signal] public delegate void GameOverEventHandler(string reason);
    [Signal] public delegate void ScreenRequestedEventHandler(string screenName);

    // Debug mode
    public bool DebugMode { get; set; } = false;
    private static readonly Random _rng = new();

    public override void _Ready()
    {
        Instance = this;
        GD.Print("[GameManager] Initialized.");
    }

    // ========================================================================
    // GAME INITIALIZATION
    // ========================================================================

    /// <summary>Start a new game with the given occupation and party names.</summary>
    public void StartNewGame(string occupation, List<string> partyNames)
    {
        State = GameState.NewGameWithParty(occupation, partyNames);
        EnterTown("Independence");
        EmitSignal(SignalName.StateChanged);
        GD.Print($"[GameManager] New game started. Occupation: {occupation}, Cash: ${State.Cash}");
    }

    // ========================================================================
    // TRAVEL LOOP (converted from label travel_loop)
    // ========================================================================

    /// <summary>
    /// Execute one day of travel. Returns info dict about what happened.
    /// This is the core game loop tick, called from the travel scene.
    /// </summary>
    public Dictionary<string, object> TravelOneDay()
    {
        var info = new Dictionary<string, object>();

        if (State.Pace == "rest")
        {
            State.Day++;
            TravelSystem.ApplyWeather(State);
            HealthSystem.ApplyDailyConsumption(State);
            HealthSystem.IllnessTick(State, resting: true);
            HealthSystem.DailyRecovery(State, resting: true, inTown: false);
            info["resting"] = true;
            info["miles_traveled"] = 0;
        }
        else
        {
            int oldMiles = State.Miles;
            int distance = TravelSystem.CalculateDailyDistance(State);
            int newMiles = Math.Min(State.Miles + distance, GameConstants.TargetMiles);

            // Check for stops (towns, rivers) between old and new position
            var (stopType, stopData) = TravelSystem.NextStopBetween(State, oldMiles, newMiles);
            if (stopType != null)
            {
                // Snap to the stop location
                newMiles = stopType == "town"
                    ? ((GameData.LandmarkInfo)stopData!).Miles
                    : ((GameData.RiverInfo)stopData!).Miles;

                State.PendingStopType = stopType;
                if (stopType == "town")
                    State.PendingStopKey = ((GameData.LandmarkInfo)stopData!).Name;
                else
                    State.PendingStopKey = ((GameData.RiverInfo)stopData!).Key;

                info["stop_type"] = stopType;
                info["stop_data"] = stopData!;
            }

            State.Miles = newMiles;
            State.Day++;

            TravelSystem.ApplyWeather(State);
            TravelSystem.ApplyWagonWear(State);
            TravelSystem.ApplyOxenWear(State);
            HealthSystem.ApplyDailyConsumption(State);
            HealthSystem.IllnessTick(State, resting: false);
            HealthSystem.DailyRecovery(State, resting: false, inTown: false);

            // Random events
            EventSystem.TryRandomEvent(State);

            // Breakdown check
            RepairSystem.TryBreakdown(State);

            info["miles_traveled"] = newMiles - oldMiles;
        }

        // Update unconscious flags
        HealthSystem.UpdateUnconsciousFlags(State);

        // Check all-unconscious counter
        bool allOut = State.Living().All(p => p.Unconscious || !p.Alive);
        if (allOut && State.AnyAlive())
        {
            State.AllUnconsciousDays++;
        }
        else
        {
            State.AllUnconsciousDays = 0;
        }

        EmitSignal(SignalName.StateChanged);
        return info;
    }

    // ========================================================================
    // FAIL STATE CHECKS (converted from check_fail_states)
    // ========================================================================

    /// <summary>Check for game-ending conditions. Returns label name or null.</summary>
    public string? CheckFailStates()
    {
        if (!State.AnyAlive())
            return "game_over_dead";

        if (State.AllUnconsciousDays >= GameConstants.GameOverAllUnconsciousDays)
            return "game_over_unconscious";

        if (State.StarveDays >= GameConstants.GameOverStarvationDays)
            return "game_over_starved";

        if (State.StrandedDays >= GameConstants.GameOverStrandedDays)
            return "game_over_stranded";

        if (State.Miles >= GameConstants.TargetMiles)
            return "chapter_complete";

        if (State.Day >= 200)
            return "game_over_time";

        return null;
    }

    // ========================================================================
    // TOWN ENTER/LEAVE
    // ========================================================================

    public void EnterTown(string townName)
    {
        State.AtTownName = townName;
        var landmark = Array.Find(GameData.Landmarks, l => l.Name == townName);
        if (landmark?.StoreKey != null)
        {
            State.AtTownStoreKey = landmark.StoreKey;
        }
        if (!State.VisitedLandmarks.Contains(townName))
        {
            State.VisitedLandmarks.Add(townName);
        }
    }

    public void LeaveTown()
    {
        State.AtTownName = "";
        State.AtTownStoreKey = "";
    }

    // ========================================================================
    // REST
    // ========================================================================

    /// <summary>Rest for the given number of days.</summary>
    public void Rest(int days)
    {
        for (int i = 0; i < days; i++)
        {
            State.Day++;
            TravelSystem.ApplyWeather(State);
            HealthSystem.ApplyDailyConsumption(State);
            HealthSystem.IllnessTick(State, resting: true);
            HealthSystem.DailyRecovery(State, resting: true, inTown: false);
        }
        EmitSignal(SignalName.StateChanged);
    }

    // ========================================================================
    // SAVE / LOAD
    // ========================================================================

    public void SaveGame(string path)
    {
        string json = State.ToJson();
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file?.StoreString(json);
        GD.Print($"[GameManager] Game saved to {path}");
    }

    public bool LoadGame(string path)
    {
        if (!FileAccess.FileExists(path)) return false;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        string json = file?.GetAsText() ?? "";
        if (string.IsNullOrEmpty(json)) return false;

        State = GameState.FromJson(json);
        EmitSignal(SignalName.StateChanged);
        GD.Print($"[GameManager] Game loaded from {path}");
        return true;
    }

    // ========================================================================
    // UTILITY
    // ========================================================================

    public static int RandInt(int min, int max) => _rng.Next(min, max + 1);
    public static float RandFloat() => (float)_rng.NextDouble();
    public static float RandRange(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
    public static T RandChoice<T>(T[] items) => items[_rng.Next(items.Length)];
}
