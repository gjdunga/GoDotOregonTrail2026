#nullable enable
using System;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// One-time route choice screen at The Dalles (mile 1900).
/// Player must choose between the Barlow Road and the Columbia River.
/// No dismiss without a choice. No back button.
///
/// BARLOW ROAD (toll road):
///   Cost: $5 toll deducted immediately.
///   Effect: RouteChoice = "barlow". TravelSystem applies 0.85x speed
///   and 1.5x wagon wear past mile 2050. Avoids the Columbia crossing.
///   Best when: low cash is not the constraint and wagon/oxen are marginal.
///
/// COLUMBIA RIVER:
///   Cost: none upfront.
///   Effect: RouteChoice = "river". Columbia River crossing triggers at
///   mile 2050 via RiverSystem. Full ford/ferry/caulk logic applies.
///   Best when: strong party, enough cash for ferry, supplies to spare.
///
/// Sets RouteChoiceMade = true on either choice so the trigger cannot fire again.
///
/// Signals:
///   RouteChosen(string choice) - "barlow" or "river".
/// </summary>
public partial class RouteChoiceScreen : Control
{
    [Signal] public delegate void RouteChosenEventHandler(string choice);

    private GameState _state = null!;

    public void Initialize(GameState state) => _state = state;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // The Dalles / Columbia Gorge background
        var bg = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/images/bg/bg_the_dalles.webp"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var dim = UIKit.MakeDarkOverlay(0.65f);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = UIKit.MakePanel();
        panel.CustomMinimumSize = new Vector2(680, 0);
        center.AddChild(panel);

        var pad = new MarginContainer();
        pad.AddThemeConstantOverride("margin_left",   40);
        pad.AddThemeConstantOverride("margin_right",  40);
        pad.AddThemeConstantOverride("margin_top",    30);
        pad.AddThemeConstantOverride("margin_bottom", 30);
        panel.AddChild(pad);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        pad.AddChild(vbox);

        // Title
        var title = UIKit.MakeDisplayLabel(Tr(TK.RouteTitle), 28, UIKit.ColAmber);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var subtitle = UIKit.MakeBodyLabel(Tr(TK.RouteSubtitle), 15, UIKit.ColParchment);
        subtitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(subtitle);

        // Party status strip
        vbox.AddChild(BuildStatusStrip());
        vbox.AddChild(UIKit.MakeDivider());

        // Route cards side by side
        var routeRow = new HBoxContainer();
        routeRow.AddThemeConstantOverride("separation", 20);
        routeRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        routeRow.AddChild(BuildRouteCard(
            Tr(TK.RouteBarlowName),
            Tr(TK.RouteBarlowCost),
            Tr(TK.RouteBarlowSub),
            new[]
            {
                "Sam Barlow's toll road skirts Mt. Hood.",
                "Slower travel (85% speed) past mile 2050.",
                "Hard on the wagon - 50% more wear.",
                "Avoids the Columbia entirely.",
                "Best if your wagon and oxen are worn.",
            },
            RecommendBarlow() ? Tr(TK.RouteRecommended) : null,
            "barlow",
            UIKit.ColAmber));

        routeRow.AddChild(BuildRouteCard(
            Tr(TK.RouteColumbiaName),
            Tr(TK.RouteColumbiaSub1),
            Tr(TK.RouteColumbiaSub2),
            new[]
            {
                "Raft or ferry down the Columbia River.",
                "Full river crossing at mile 2050.",
                "Fast but the Columbia is dangerous.",
                "Strong current, potential loss of goods.",
                "Best if your party and supplies are strong.",
            },
            !RecommendBarlow() ? Tr(TK.RouteRecommended) : null,
            "river",
            UIKit.ColGreen));

        vbox.AddChild(routeRow);

        vbox.AddChild(UIKit.MakeSpacer(4));

        var note = UIKit.MakeBodyLabel(Tr(TK.RouteIrreversible), 13, UIKit.ColRed);
        note.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(note);
    }

    // =========================================================================
    // ROUTE CARD
    // =========================================================================

    private Control BuildRouteCard(
        string name, string cost, string tagline,
        string[] bullets, string? recommend,
        string choiceKey, Color accentColor)
    {
        var card = UIKit.MakePanel();
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var pad = new MarginContainer();
        pad.AddThemeConstantOverride("margin_left",   18);
        pad.AddThemeConstantOverride("margin_right",  18);
        pad.AddThemeConstantOverride("margin_top",    16);
        pad.AddThemeConstantOverride("margin_bottom", 16);
        card.AddChild(pad);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        pad.AddChild(col);

        // Header
        var nameLbl = UIKit.MakeDisplayLabel(name, 20, accentColor);
        nameLbl.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(nameLbl);

        var costLbl = UIKit.MakeBodyLabel(cost, 14, UIKit.ColParchment);
        costLbl.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(costLbl);

        var tagLbl = UIKit.MakeBodyLabel(tagline, 13, accentColor);
        tagLbl.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(tagLbl);

        col.AddChild(UIKit.MakeDivider());

        // Bullet points
        foreach (string bullet in bullets)
        {
            var bl = UIKit.MakeBodyLabel($"  * {bullet}", 13, UIKit.ColParchment);
            bl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            col.AddChild(bl);
        }

        col.AddChild(UIKit.MakeSpacer(4));

        // Recommendation badge
        if (recommend != null)
        {
            var rec = UIKit.MakeBodyLabel(recommend, 12, accentColor);
            rec.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(rec);
        }
        else
        {
            col.AddChild(UIKit.MakeSpacer(18));
        }

        col.AddChild(UIKit.MakeSpacer(4));

        // CHOOSE button
        var btn = UIKit.MakePrimaryButton($"CHOOSE {name}", 16);
        btn.CustomMinimumSize = new Vector2(0, 48);
        btn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        btn.Pressed += () => OnChoose(choiceKey);

        // Tint the button to match accent
        var btnStyle = new StyleBoxFlat
        {
            BgColor           = new Color(accentColor, 0.18f),
            BorderColor       = new Color(accentColor, 0.70f),
            BorderWidthLeft   = 2, BorderWidthRight  = 2,
            BorderWidthTop    = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop  = 10, ContentMarginBottom = 10,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        };
        btn.AddThemeStyleboxOverride("normal",  btnStyle);
        btn.AddThemeStyleboxOverride("hover",   btnStyle);
        btn.AddThemeColorOverride("font_color", accentColor);

        col.AddChild(btn);

        return card;
    }

    // =========================================================================
    // CHOICE HANDLER
    // =========================================================================

    private void OnChoose(string choice)
    {
        _state.RouteChoice     = choice;
        _state.RouteChoiceMade = true;

        if (choice == "barlow")
        {
            float toll = GameConstants.BarlowToll;
            _state.Cash = Math.Max(0, _state.Cash - toll);
        }

        EmitSignal(SignalName.RouteChosen, choice);
    }

    // =========================================================================
    // STATUS STRIP
    // =========================================================================

    private Control BuildStatusStrip()
    {
        var row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 28);

        int wagonPct  = (int)(_state.Wagon / (float)GameConstants.ConditionMaximum * 100f);
        int oxenPct   = (int)(_state.OxenCondition / (float)GameConstants.ConditionMaximum * 100f);
        int oxenCount = _state.Supplies.GetValueOrDefault("oxen", 0);

        AddStat(row, Tr(TK.RouteStatCash),    $"${_state.Cash:F0}");
        AddStat(row, Tr(TK.RouteStatWagon),   $"{wagonPct}%", wagonPct < 40 ? UIKit.ColRed : UIKit.ColGreen);
        AddStat(row, Tr(TK.RouteStatOxen),    string.Format(Tr(TK.RouteYokes), oxenCount));
        AddStat(row, Tr(TK.RouteStatOxenCond),$"{oxenPct}%",  oxenPct  < 40 ? UIKit.ColRed : UIKit.ColGreen);
        AddStat(row, Tr(TK.RouteStatFood),    string.Format(Tr(TK.RouteLbs), _state.Supplies.GetValueOrDefault("food", 0)));
        AddStat(row, Tr(TK.RouteStatSurv),    $"{_state.Living().Count}");

        return row;
    }

    private static void AddStat(HBoxContainer row, string label, string value, Color? valueColor = null)
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);
        var l = UIKit.MakeBodyLabel(label, 11, UIKit.ColAmberDim);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        var v = UIKit.MakeBodyLabel(value, 14, valueColor ?? UIKit.ColParchment);
        v.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(l);
        col.AddChild(v);
        row.AddChild(col);
    }

    // =========================================================================
    // RECOMMENDATION LOGIC
    // =========================================================================

    /// <summary>
    /// Recommend Barlow if wagon is degraded or oxen are weak.
    /// Recommend river otherwise (faster, better if party is strong).
    /// </summary>
    private bool RecommendBarlow()
    {
        int wagonPct = (int)(_state.Wagon / (float)GameConstants.ConditionMaximum * 100f);
        int oxenPct  = (int)(_state.OxenCondition / (float)GameConstants.ConditionMaximum * 100f);
        int oxenCount = _state.Supplies.GetValueOrDefault("oxen", 0);

        return wagonPct < 50 || oxenPct < 50 || oxenCount < 2
               || _state.Cash < 15f; // can't afford ferry either
    }
}
