# Oregon Trail 2026 (Godot + C#)

A Godot 4.3 + .NET 8.0 / C# conversion of the original RenPy Oregon Trail 2026 project.

## Source

Converted from: [OregonTrail2026 RenPy v7.2.2b](https://github.com/gjdunga/OregonTrail2026/tree/V7_2_2b)

## Requirements

- Godot 4.3+ with .NET support
- .NET 8.0 SDK
- Visual Studio 2022+ or VS Code with C# extension

## Project Structure

```
GoDotOregonTrail2026/
  project.godot              Godot project configuration
  OregonTrail2026.csproj     .NET 8.0 project file
  OregonTrail2026.sln        Visual Studio solution

  assets/
    audio/                   4 MP3 music tracks
    images/                  208 WebP images (bg, events, hunt, fish, icons, illness, map, ui)

  scripts/
    Models/
      Person.cs              Party member data model
      GameState.cs           Central game state (save/load via JSON)
      GameConstants.cs       All balance constants
      GameData.cs            Static tables (illnesses, landmarks, rivers, prices, events)

    Systems/
      GameManager.cs         Autoload singleton, game loop orchestrator
      TravelSystem.cs        Terrain, weather, daily distance, wagon wear
      HealthSystem.cs        Illness, recovery, food consumption
      CargoSystem.cs         Weight/capacity + date utilities
      EconomySystem.cs       Store pricing, buying mechanics
      EventSystem.cs         Random trail events, encounters
      RepairSystem.cs        Wagon breakdown and repair
      RiverSystem.cs         River crossing mechanics

    Screens/
      MainScene.cs           Main UI controller and game flow

  scenes/
    Main.tscn                Entry point scene
```

## Conversion Map (RenPy to C#)

| RenPy Source               | C# Equivalent                     |
|---------------------------|-----------------------------------|
| OT2026_main.rpy           | MainScene.cs + GameManager.cs     |
| OT2026_data.rpy           | GameState.cs + GameData.cs        |
| OT2026_constants.rpy      | GameConstants.cs                  |
| OT2026_travel.rpy         | TravelSystem.cs + HealthSystem.cs |
| OT2026_store.rpy          | EconomySystem.cs                  |
| OT2026_repair.rpy         | RepairSystem.cs                   |
| OT2026_rivers.rpy         | RiverSystem.cs                    |
| OT2026_encounters.rpy     | EventSystem.cs                    |
| OT2026_hunt.rpy           | (Planned) HuntingMinigame.cs      |
| OT2026_fish.rpy           | (Planned) FishingMinigame.cs      |

## What's Converted

- Complete data model (Person, GameState, all constants and lookup tables)
- Core game loop (daily travel, weather, distance, town/river stops)
- Health system (illness, recovery, food consumption, starvation)
- Economy system (dynamic pricing with region/season/progression modifiers)
- Event system (random events, encounters, good/bad events)
- Repair system (breakdown probability, field repair, jury-rig)
- River crossing system (ford, caulk, ferry, guide with probability math)
- Cargo/weight system with capacity limits
- Save/load via JSON serialization (Newtonsoft.Json)
- HUD, keyboard input, music playback
- All 208 images and 4 audio tracks ported

## Expansion Needed

- Party name setup screen (currently defaults)
- Full store buy/sell UI
- Map screen with landmark pins
- Hunting minigame (click-to-shoot with moving targets)
- Fishing minigame
- River crossing choice dialog
- Fort interaction screens (blacksmith, doctor, trader)
- Role assignment screen
- Journal/notes system
- Visual polish (weather overlays, transitions, animations)

## Running

1. Open project.godot in Godot 4.3+ (.NET edition)
2. Build C# solution (Build > Build Solution, or `dotnet build`)
3. Press F5 to run

## Smart Branch Merge Workflow

If you have two branches and want a safer merge pass, use the helper script:

```bash
./tools_smart_merge_review.sh <branch-a> <branch-b>
```

The script prints:
- commits unique to each branch,
- files touched by both branches (highest conflict risk),
- files unique to each branch.

Suggested flow:
1. Run the script and review the "changed in both branches" list.
2. Merge with `git merge --no-ff <other-branch>`.
3. Resolve overlapping files first, then run your normal build/tests.
