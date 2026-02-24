#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Systems;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// River crossing choice screen. Presented whenever the wagon reaches a river.
///
/// Layout (code-built, no .tscn):
///   Full-rect control laid over the river background image.
///   Dark overlay dims the background.
///   Centered NinePatchRect panel contains:
///     - River name heading
///     - Depth and weather context line
///     - One button per available crossing method (2-4)
///     - WAIT button (passes one day, updates depth)
///     - Party cash display updated after paid crossings
///
/// Signals:
///   CrossingComplete - emitted after a crossing attempt (success or failure).
///     MainScene reads GameState for consequences and continues travel loop.
///   WaitDayRequested - emitted when the player waits. MainScene calls
///     GameManager.Rest(1) then re-initializes this screen.
/// </summary>
public partial class RiverCrossingScreen : Control
{
    [Signal] public delegate void CrossingCompleteEventHandler();
    [Signal] public delegate void WaitDayRequestedEventHandler();

    private GameData.RiverInfo _river = null!;
    private GameState _state = null!;

    // Rebuilt when depth changes after a wait day
    private VBoxContainer _methodButtons = null!;
    private Label _depthLabel = null!;
    private Label _cashLabel = null!;

    public void Initialize(GameState state, GameData.RiverInfo river)
    {
        _state = state;
        _river = river;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Dark overlay over the background image set by MainScene
        var overlay = UIKit.MakeDarkOverlay(0.55f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // Centered content panel
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = UIKit.MakePanel();
        panel.CustomMinimumSize = new Vector2(540, 0);
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        var padding = new MarginContainer();
        padding.AddThemeConstantOverride("margin_left",   36);
        padding.AddThemeConstantOverride("margin_right",  36);
        padding.AddThemeConstantOverride("margin_top",    30);
        padding.AddThemeConstantOverride("margin_bottom", 30);
        padding.AddChild(vbox);
        panel.AddChild(padding);

        // Title
        var title = UIKit.MakeDisplayLabel($"THE {_river.Name.ToUpper()}", 28);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        vbox.AddChild(UIKit.MakeDivider());

        // Context line (depth + weather)
        _depthLabel = BuildDepthLabel();
        vbox.AddChild(_depthLabel);

        vbox.AddChild(UIKit.MakeSpacer(4));

        // Method buttons container
        _methodButtons = new VBoxContainer();
        _methodButtons.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(_methodButtons);
        BuildMethodButtons();

        vbox.AddChild(UIKit.MakeSpacer(4));
        vbox.AddChild(UIKit.MakeDivider());

        // WAIT button row
        var waitRow = new HBoxContainer();
        waitRow.Alignment = BoxContainer.AlignmentMode.Center;
        var waitBtn = UIKit.MakeSecondaryButton(Tr(TK.RiverWait), 15);
        waitBtn.TooltipText = "Passes one day. River depth may change.";
        waitBtn.Pressed += OnWaitPressed;
        waitRow.AddChild(waitBtn);
        vbox.AddChild(waitRow);

        // Cash display
        _cashLabel = UIKit.MakeBodyLabel(string.Format(Tr(TK.RiverCash), _state.Cash), 14, UIKit.ColAmberDim);
        _cashLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_cashLabel);
    }

    // =========================================================================
    // UI CONSTRUCTION HELPERS
    // =========================================================================

    private Label BuildDepthLabel()
    {
        string depthRange = $"{_river.DepthFt.min}-{_river.DepthFt.max} FT DEEP";
        string weather = _state.Weather == "clear" ? "" : $"  |  WEATHER: {_state.Weather.ToUpper()}";

        var label = UIKit.MakeBodyLabel(
            string.Format(Tr(TK.RiverDepth), $"{depthRange}{weather}"), 15, UIKit.ColParchment);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    private void BuildMethodButtons()
    {
        // Clear existing buttons (called again after a wait day)
        foreach (Node child in _methodButtons.GetChildren())
            child.QueueFree();

        var available = RiverSystem.GetAvailableMethods(_state, _river);
        foreach (var method in available)
        {
            var row = new HBoxContainer();
            row.Alignment = BoxContainer.AlignmentMode.Center;
            row.AddThemeConstantOverride("separation", 0);

            string name     = RiverSystem.MethodName(method);
            string risk     = RiverSystem.MethodRiskLabel(method);
            string cost     = RiverSystem.MethodCostLabel(_state, _river, method);
            string label    = string.IsNullOrEmpty(cost)
                ? $"{name}  [{risk}]"
                : $"{name}  [{risk}]  {cost}";

            var btn = UIKit.MakeSecondaryButton(label, 15);
            btn.CustomMinimumSize = new Vector2(460, 48);

            // Disable paid methods the player can't afford
            bool cantAffordFerry  = method == RiverSystem.CrossingMethod.Ferry
                                    && _state.FreeFerryUses <= 0
                                    && _state.Cash < _river.FerryCost.min;
            bool cantAffordGuide  = method == RiverSystem.CrossingMethod.Guide
                                    && _state.Cash < GameConstants.RiverGuideCostMin;
            if (cantAffordFerry || cantAffordGuide)
            {
                btn.Disabled = true;
                btn.TooltipText = Tr(TK.RiverNoCash);
            }

            var captured = method;
            btn.Pressed += () => OnCrossingMethodChosen(captured);
            row.AddChild(btn);
            _methodButtons.AddChild(row);
        }
    }

    private void RefreshAfterWait()
    {
        // Re-draw depth label and method buttons with updated state
        _depthLabel.Text = BuildDepthLabel().Text;
        _cashLabel.Text  = string.Format(Tr(TK.RiverCash), _state.Cash);
        BuildMethodButtons();
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    private void OnCrossingMethodChosen(RiverSystem.CrossingMethod method)
    {
        var (success, msg) = RiverSystem.AttemptCrossing(_state, _river, method);

        // Store result on state so MainScene can display it after screen teardown
        _state.StopFlags["river_crossing_success"] = success;
        _state.StopFlags["river_crossing_msg"]     = msg;

        EmitSignal(SignalName.CrossingComplete);
    }

    private void OnWaitPressed()
    {
        EmitSignal(SignalName.WaitDayRequested);
    }

    // Called by MainScene after processing the wait day (GameManager.Rest(1))
    // so the screen reflects the new date and weather.
    public void OnWaitDayProcessed()
    {
        RefreshAfterWait();
    }
}
