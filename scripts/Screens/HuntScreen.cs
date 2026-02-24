#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Systems;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Hunting screen. Text-based target selection: player fires at animals
/// appearing in a list and tracks ammo vs yield in real time.
///
/// Mechanics (all from GameConstants):
///   - Player has up to HuntMaxAmmo shots or the bullets they own, whichever is less.
///   - Each FIRE round selects a random animal from the terrain pool.
///   - Hit chance varies by animal. Miss consumes ammo, hit adds meat and consumes ammo.
///   - Predator threat check after each shot: random chance of predator encounter
///     dealing health damage to the party leader.
///   - Final yield is capped by terrain (HuntYieldCap*), modified by weather
///     (HuntRainFogMult, HuntWinterMult, HuntFallMult) and hunter role bonus.
///   - RETURN TO CAMP commits yield to cargo and deducts ammo from supplies.
///
/// Signals:
///   HuntComplete - emitted when player returns to camp.
///     MainScene reads the hunt result from GameState.StopFlags.
/// </summary>
public partial class HuntScreen : Control
{
    [Signal] public delegate void HuntCompleteEventHandler();

    private GameState _state = null!;

    private int _ammoUsed;
    private int _ammoAvailable;
    private int _meatTotal;
    private int _cap;

    private Label _ammoLabel = null!;
    private Label _meatLabel = null!;
    private Label _capLabel = null!;
    private VBoxContainer _logBox = null!;
    private Button _fireBtn = null!;
    private Button _returnBtn = null!;

    // Terrain-appropriate animals: (name, hitChance, meatYield)
    private static readonly Dictionary<string, (string name, float hit, int meatMin, int meatMax)[]> TerrainAnimals = new()
    {
        { "prairie",     new[] {
            ("BISON",   0.55f, 40, 90), ("RABBIT",  0.85f, 5,  15),
            ("ANTELOPE", 0.65f, 20, 40), ("TURKEY",  0.75f, 8,  18) } },
        { "plains",      new[] {
            ("BISON",   0.55f, 40, 90), ("DEER",    0.70f, 25, 50),
            ("ANTELOPE", 0.65f, 20, 40), ("RABBIT",  0.85f, 5,  15) } },
        { "high_plains", new[] {
            ("DEER",    0.70f, 25, 50), ("ANTELOPE", 0.65f, 20, 40),
            ("RABBIT",  0.85f, 5,  15), ("ELK",     0.50f, 50, 110) } },
        { "mountains",   new[] {
            ("ELK",     0.50f, 50, 110), ("DEER",   0.70f, 25, 50),
            ("BEAR",    0.35f, 60, 130), ("RABBIT",  0.85f, 5,  15) } },
        { "river_valley", new[] {
            ("DEER",    0.70f, 25, 50), ("TURKEY",  0.75f, 8,  18),
            ("RABBIT",  0.85f, 5,  15), ("ELK",     0.50f, 50, 110) } },
        { "coastal",     new[] {
            ("DEER",    0.70f, 25, 50), ("TURKEY",  0.75f, 8,  18),
            ("RABBIT",  0.85f, 5,  15), ("BEAR",    0.35f, 60, 130) } },
    };

    public void Initialize(GameState state)
    {
        _state = state;
    }

    public override void _Ready()
    {
        string terrain = TravelSystem.TerrainByMiles(_state.Miles);
        _cap = GetYieldCap(terrain);
        _ammoAvailable = Math.Min(
            _state.Supplies.GetValueOrDefault("bullets", 0),
            GameConstants.HuntMaxAmmo);

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var overlay = UIKit.MakeDarkOverlay(0.70f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = UIKit.MakePanel();
        panel.CustomMinimumSize = new Vector2(520, 0);
        center.AddChild(panel);

        var pad = new MarginContainer();
        pad.AddThemeConstantOverride("margin_left",   32);
        pad.AddThemeConstantOverride("margin_right",  32);
        pad.AddThemeConstantOverride("margin_top",    26);
        pad.AddThemeConstantOverride("margin_bottom", 26);
        panel.AddChild(pad);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        pad.AddChild(vbox);

        // Title
        var title = UIKit.MakeDisplayLabel("GO HUNTING", 26);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        vbox.AddChild(UIKit.MakeDivider());

        // Stats row
        var stats = new HBoxContainer();
        stats.Alignment = BoxContainer.AlignmentMode.Center;
        stats.AddThemeConstantOverride("separation", 32);

        _ammoLabel = UIKit.MakeDisplayLabel($"AMMO: {_ammoAvailable}", 17, UIKit.ColAmber);
        _meatLabel = UIKit.MakeDisplayLabel("MEAT: 0 LBS", 17, UIKit.ColGreen);
        _capLabel  = UIKit.MakeBodyLabel($"CAP: {_cap} LBS", 14, UIKit.ColGray);
        _capLabel.VerticalAlignment = VerticalAlignment.Center;

        stats.AddChild(_ammoLabel);
        stats.AddChild(_meatLabel);
        stats.AddChild(_capLabel);
        vbox.AddChild(stats);

        // Weather/context note
        string weather = _state.Weather;
        if (weather != "clear")
        {
            var ctx = UIKit.MakeBodyLabel(
                $"WEATHER: {weather.ToUpper()}. CONDITIONS ARE POOR.",
                13, UIKit.ColGray);
            ctx.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(ctx);
        }

        vbox.AddChild(UIKit.MakeDivider());

        // Hunt log (scrollable)
        var logScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 180),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _logBox = new VBoxContainer();
        _logBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _logBox.AddThemeConstantOverride("separation", 4);
        logScroll.AddChild(_logBox);
        vbox.AddChild(logScroll);

        AppendLog("YOU ENTER THE WILDERNESS. YOUR RIFLE IS READY.", UIKit.ColParchment);

        // Buttons
        var btnRow = new HBoxContainer();
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        btnRow.AddThemeConstantOverride("separation", 16);

        _fireBtn = UIKit.MakePrimaryButton("FIRE!", 18);
        _fireBtn.CustomMinimumSize = new Vector2(140, 48);
        _fireBtn.Pressed += OnFire;
        btnRow.AddChild(_fireBtn);

        _returnBtn = UIKit.MakeSecondaryButton("RETURN TO CAMP", 16);
        _returnBtn.CustomMinimumSize = new Vector2(200, 48);
        _returnBtn.Pressed += OnReturn;
        btnRow.AddChild(_returnBtn);

        vbox.AddChild(btnRow);

        RefreshButtons();
    }

    // =========================================================================
    // HUNT LOGIC
    // =========================================================================

    private void OnFire()
    {
        if (_ammoUsed >= _ammoAvailable || _meatTotal >= _cap) return;

        string terrain = TravelSystem.TerrainByMiles(_state.Miles);
        var animals = TerrainAnimals.GetValueOrDefault(terrain, TerrainAnimals["plains"]);
        var animal = animals[GameManager.RandInt(0, animals.Length - 1)];

        _ammoUsed++;

        bool hit = GameManager.RandFloat() < animal.hit;

        if (hit)
        {
            int raw = GameManager.RandInt(animal.meatMin, animal.meatMax);
            int gained = Math.Min(raw, _cap - _meatTotal);
            _meatTotal += gained;
            AppendLog($"HIT! {animal.name} - +{gained} LBS MEAT.", UIKit.ColGreen);
        }
        else
        {
            AppendLog($"MISSED THE {animal.name}.", UIKit.ColGray);
        }

        // Predator threat
        float predChance = GameConstants.HuntPredatorThreatMin / 10f;
        if (GameManager.RandFloat() < predChance)
        {
            int dmg = GameManager.RandInt(
                GameConstants.HuntPredatorDamageMin,
                GameConstants.HuntPredatorDamageMax);
            var leader = _state.Living().Count > 0 ? _state.Living()[0] : null;
            if (leader != null)
            {
                leader.Health = Math.Max(0, leader.Health - dmg);
                AppendLog($"PREDATOR ATTACK! {leader.Name.ToUpper()} TAKES {dmg} DAMAGE.", UIKit.ColRed);
                HealthSystem.UpdateUnconsciousFlags(_state);
            }
        }

        UpdateStats();
        RefreshButtons();
    }

    private void OnReturn()
    {
        // Apply yield multipliers
        float mult = 1.0f;
        var (month, _) = DateCalc.DayToDate(_state.Day);
        if (_state.Weather is "rain" or "fog") mult *= GameConstants.HuntRainFogMult;
        if (month is "NOV" or "DEC" or "JAN" or "FEB") mult *= GameConstants.HuntWinterMult;
        if (month is "SEP" or "OCT") mult *= GameConstants.HuntFallMult;

        // Hunter role bonus
        bool hunterActive = _state.Roles.TryGetValue("hunter", out string? hunterName)
            && !string.IsNullOrEmpty(hunterName)
            && _state.Living().Exists(p => p.Name == hunterName && p.IsConscious);
        if (hunterActive) mult *= GameConstants.HuntRoleBonusMult;

        int finalMeat = (int)Math.Round(_meatTotal * mult);
        int added = CargoSystem.AddFoodWithCapacity(_state, finalMeat);

        // Deduct ammo
        _state.Supplies["bullets"] = Math.Max(0,
            _state.Supplies.GetValueOrDefault("bullets", 0) - _ammoUsed);

        // Advance one day for the hunt
        _state.Day++;
        TravelSystem.ApplyWeather(_state);
        HealthSystem.ApplyDailyConsumption(_state);

        // Store result for MainScene
        _state.StopFlags["hunt_meat_added"] = added;
        _state.StopFlags["hunt_ammo_used"]  = _ammoUsed;

        EmitSignal(SignalName.HuntComplete);
    }

    // =========================================================================
    // UI HELPERS
    // =========================================================================

    private void AppendLog(string text, Color color)
    {
        var lbl = UIKit.MakeBodyLabel(text, 13, color);
        lbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _logBox.AddChild(lbl);

        // Scroll to bottom
        CallDeferred(MethodName.ScrollLogToBottom);
    }

    private void ScrollLogToBottom()
    {
        if (_logBox.GetParent() is ScrollContainer sc)
            sc.ScrollVertical = (int)sc.GetVScrollBar().MaxValue;
    }

    private void UpdateStats()
    {
        int remaining = _ammoAvailable - _ammoUsed;
        _ammoLabel.Text = $"AMMO: {remaining}";
        _ammoLabel.AddThemeColorOverride("font_color",
            remaining <= 3 ? UIKit.ColRed : UIKit.ColAmber);

        _meatLabel.Text = $"MEAT: {_meatTotal} LBS";
    }

    private void RefreshButtons()
    {
        int remaining = _ammoAvailable - _ammoUsed;
        _fireBtn.Disabled = remaining <= 0 || _meatTotal >= _cap;

        if (remaining <= 0)
            AppendLog("OUT OF AMMO.", UIKit.ColRed);
        else if (_meatTotal >= _cap)
            AppendLog("WAGON IS FULL. RETURN TO CAMP.", UIKit.ColAmberDim);
    }

    private static int GetYieldCap(string terrain) => terrain switch
    {
        "prairie"     => GameConstants.HuntYieldCapPrairie,
        "plains"      => GameConstants.HuntYieldCapPlains,
        "high_plains" => GameConstants.HuntYieldCapHighPlains,
        "mountains"   => GameConstants.HuntYieldCapMountains,
        "river_valley" => GameConstants.HuntYieldCapRiverValley,
        _             => GameConstants.HuntYieldCapCoastal,
    };
}
