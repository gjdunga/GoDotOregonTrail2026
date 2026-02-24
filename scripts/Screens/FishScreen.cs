#nullable enable
using System;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Systems;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Fishing screen. No ammo cost. Player casts repeatedly, each cast has a
/// random chance of catching a fish based on terrain/season/weather.
///
/// Mechanics:
///   - IsNearRiver: true if within 60 miles of any river in GameData.Rivers.
///     Applies FishRiverProximityBonus multiplier to base yield.
///   - Each CAST roll: 60% base hit chance. Winter reduces by FishWinterMult.
///   - Catch yields FishYieldMin-FishYieldMax lbs per successful cast.
///   - Player may cast up to 10 times per outing.
///   - RETURN commits yield via CargoSystem.AddFoodWithCapacity.
///   - One day advanced for the outing.
///
/// Signals:
///   FishComplete - emitted on return.
/// </summary>
public partial class FishScreen : Control
{
    [Signal] public delegate void FishCompleteEventHandler();

    private GameState _state = null!;

    private int _castsUsed;
    private const int MaxCasts = 10;
    private int _fishTotal;

    private Label _castsLabel = null!;
    private Label _fishLabel = null!;
    private VBoxContainer _logBox = null!;
    private Button _castBtn = null!;

    public void Initialize(GameState state)
    {
        _state = state;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var overlay = UIKit.MakeDarkOverlay(0.70f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = UIKit.MakePanel();
        panel.CustomMinimumSize = new Vector2(480, 0);
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

        var title = UIKit.MakeDisplayLabel(Tr(TK.FishTitle), 26);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        vbox.AddChild(UIKit.MakeDivider());

        // Context
        bool nearRiver = IsNearRiver();
        var (month, _) = DateCalc.DayToDate(_state.Day);
        bool winter = month is "NOV" or "DEC" or "JAN" or "FEB";

        string ctx = nearRiver ? Tr(TK.FishNearRiver) : Tr(TK.FishNoRiver);
        if (winter) ctx += " WATER IS ICY.";
        else if (_state.Weather is "rain" or "fog") ctx += " OVERCAST BUT FISH ARE BITING.";

        var ctxLabel = UIKit.MakeBodyLabel(ctx, 13, UIKit.ColGray);
        ctxLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(ctxLabel);

        vbox.AddChild(UIKit.MakeSpacer(4));

        // Stats row
        var stats = new HBoxContainer();
        stats.Alignment = BoxContainer.AlignmentMode.Center;
        stats.AddThemeConstantOverride("separation", 32);

        _castsLabel = UIKit.MakeDisplayLabel(string.Format(Tr(TK.FishCasts), MaxCasts), 17, UIKit.ColAmber);
        _fishLabel  = UIKit.MakeDisplayLabel(string.Format(Tr(TK.FishCaught), 0), 17, UIKit.ColGreen);

        stats.AddChild(_castsLabel);
        stats.AddChild(_fishLabel);
        vbox.AddChild(stats);

        vbox.AddChild(UIKit.MakeDivider());

        // Log
        var logScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 160),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _logBox = new VBoxContainer();
        _logBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _logBox.AddThemeConstantOverride("separation", 4);
        logScroll.AddChild(_logBox);
        vbox.AddChild(logScroll);

        AppendLog(Tr(TK.FishIntro), UIKit.ColParchment);

        // Buttons
        var btnRow = new HBoxContainer();
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        btnRow.AddThemeConstantOverride("separation", 16);

        _castBtn = UIKit.MakePrimaryButton(Tr(TK.FishCast), 18);
        _castBtn.CustomMinimumSize = new Vector2(140, 48);
        _castBtn.Pressed += OnCast;
        btnRow.AddChild(_castBtn);

        var returnBtn = UIKit.MakeSecondaryButton(Tr(TK.CommonReturnCamp), 16);
        returnBtn.CustomMinimumSize = new Vector2(200, 48);
        returnBtn.Pressed += OnReturn;
        btnRow.AddChild(returnBtn);

        vbox.AddChild(btnRow);
    }

    // =========================================================================
    // FISH LOGIC
    // =========================================================================

    private void OnCast()
    {
        if (_castsUsed >= MaxCasts) return;
        _castsUsed++;

        var (month, _) = DateCalc.DayToDate(_state.Day);
        bool winter = month is "NOV" or "DEC" or "JAN" or "FEB";

        float hitChance = 0.60f;
        if (winter) hitChance *= GameConstants.FishWinterMult;
        if (_state.Weather is "rain" or "fog") hitChance *= 1.05f; // slight bonus - overcast

        bool nearRiver = IsNearRiver();

        bool caught = GameManager.RandFloat() < hitChance;

        if (caught)
        {
            int raw = GameManager.RandInt(GameConstants.FishYieldMin, GameConstants.FishYieldMax);
            if (nearRiver) raw = (int)Math.Round(raw * GameConstants.FishRiverProximityBonus);
            _fishTotal += raw;
            AppendLog(string.Format(Tr(TK.FishHit), raw), UIKit.ColGreen);
        }
        else
        {
            AppendLog(Tr(TK.FishMiss), UIKit.ColGray);
        }

        int remaining = MaxCasts - _castsUsed;
        _castsLabel.Text = string.Format(Tr(TK.FishCasts), remaining);
        _castsLabel.AddThemeColorOverride("font_color",
            remaining <= 2 ? UIKit.ColRed : UIKit.ColAmber);
        _fishLabel.Text = string.Format(Tr(TK.FishCaught), _fishTotal);

        _castBtn.Disabled = _castsUsed >= MaxCasts;
        if (_castsUsed >= MaxCasts)
            AppendLog(Tr(TK.FishNoCasts), UIKit.ColAmberDim);

        CallDeferred(MethodName.ScrollLogToBottom);
    }

    private void OnReturn()
    {
        int added = CargoSystem.AddFoodWithCapacity(_state, _fishTotal);

        // One day consumed
        _state.Day++;
        TravelSystem.ApplyWeather(_state);
        HealthSystem.ApplyDailyConsumption(_state);

        _state.StopFlags["fish_added"] = added;
        EmitSignal(SignalName.FishComplete);
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private bool IsNearRiver()
    {
        foreach (var river in GameData.Rivers)
        {
            if (Math.Abs(_state.Miles - river.Miles) <= 60) return true;
        }
        return false;
    }

    private void AppendLog(string text, Color color)
    {
        var lbl = UIKit.MakeBodyLabel(text, 13, color);
        lbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _logBox.AddChild(lbl);
    }

    private void ScrollLogToBottom()
    {
        if (_logBox.GetParent() is ScrollContainer sc)
            sc.ScrollVertical = (int)sc.GetVScrollBar().MaxValue;
    }
}
