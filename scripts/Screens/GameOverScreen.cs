#nullable enable
using System;
using System.Linq;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Systems;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Game over screen for all defeat conditions:
///   game_over_dead        - entire party died
///   game_over_unconscious - party unconscious too long
///   game_over_starved     - food out for too long
///   game_over_stranded    - no oxen or working wagon for too long
///   game_over_time        - winter caught the party
///   (fallthrough)         - any unexpected reason key
///
/// Displays:
///   - Cause of failure with flavor text
///   - Party fate list (survived / when and how they died)
///   - Journey stats: miles reached, days, date
///   - PLAY AGAIN and MAIN MENU buttons
///
/// Signals:
///   PlayAgainRequested
///   MainMenuRequested
/// </summary>
public partial class GameOverScreen : Control
{
    [Signal] public delegate void PlayAgainRequestedEventHandler();
    [Signal] public delegate void MainMenuRequestedEventHandler();

    private GameState _state = null!;
    private string _reason = "";

    public void Initialize(GameState state, string reason)
    {
        _state  = state;
        _reason = reason;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Background: use a dark plains image for melancholy
        var bg = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/images/bg/bg_trail_plains_night.webp"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var dimOverlay = UIKit.MakeDarkOverlay(0.70f);
        dimOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dimOverlay);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scroll.SetAnchor(Side.Left,   0.12f);
        scroll.SetAnchor(Side.Right,  0.88f);
        scroll.SetAnchor(Side.Top,    0.05f);
        scroll.SetAnchor(Side.Bottom, 0.95f);
        AddChild(scroll);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(vbox);

        // ---- CAUSE HEADER ----
        AddCentered(vbox, UIKit.MakeDisplayLabel("GAME OVER", 34, UIKit.ColRed));
        vbox.AddChild(UIKit.MakeSpacer(4));

        var (headline, flavor) = ReasonText(_reason);
        AddCentered(vbox, UIKit.MakeDisplayLabel(headline, 22, UIKit.ColParchment));

        var flavorLbl = UIKit.MakeBodyLabel(flavor, 14, UIKit.ColGray);
        flavorLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        flavorLbl.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(flavorLbl);

        vbox.AddChild(UIKit.MakeDivider());

        // ---- PARTY FATE ----
        AddCentered(vbox, UIKit.MakeDisplayLabel("THE PARTY", 20));

        foreach (var p in _state.Party)
        {
            vbox.AddChild(BuildPersonRow(p));
        }

        vbox.AddChild(UIKit.MakeDivider());

        // ---- JOURNEY STATS ----
        AddCentered(vbox, UIKit.MakeDisplayLabel("HOW FAR YOU GOT", 20));

        var stats = new VBoxContainer();
        stats.AddThemeConstantOverride("separation", 6);
        stats.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        int pct = (int)((float)_state.Miles / GameConstants.TargetMiles * 100f);
        string distLabel = pct switch
        {
            >= 95 => "SO CLOSE.",
            >= 75 => "PAST THE BLUE MOUNTAINS.",
            >= 60 => "THROUGH SOUTH PASS.",
            >= 40 => "HALFWAY ACROSS THE PLAINS.",
            >= 20 => "STILL ON THE PRAIRIE.",
            _     => "BARELY OUT OF INDEPENDENCE.",
        };

        AddStatRow(stats, "MILES REACHED",  $"{_state.Miles} / {GameConstants.TargetMiles}");
        AddStatRow(stats, "PROGRESS",       $"{pct}%   {distLabel}");
        AddStatRow(stats, "DAYS ON TRAIL",  $"{_state.Day}");
        AddStatRow(stats, "DATE",           DateCalc.DateStr(_state.Day));
        AddStatRow(stats, "CASH LEFT",      $"${_state.Cash:F0}");

        vbox.AddChild(stats);
        vbox.AddChild(UIKit.MakeSpacer(8));

        // ---- BUTTONS ----
        var btnRow = new HBoxContainer();
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        btnRow.AddThemeConstantOverride("separation", 20);

        var again = UIKit.MakePrimaryButton("TRY AGAIN", 18);
        again.CustomMinimumSize = new Vector2(200, 52);
        again.Pressed += () => EmitSignal(SignalName.PlayAgainRequested);
        btnRow.AddChild(again);

        var menu = UIKit.MakeSecondaryButton("MAIN MENU", 18);
        menu.CustomMinimumSize = new Vector2(200, 52);
        menu.Pressed += () => EmitSignal(SignalName.MainMenuRequested);
        btnRow.AddChild(menu);

        vbox.AddChild(btnRow);
        vbox.AddChild(UIKit.MakeSpacer(16));
    }

    // =========================================================================
    // PERSON ROW
    // =========================================================================

    private static Control BuildPersonRow(Person p)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        Color nameColor = p.Alive ? UIKit.ColParchment : UIKit.ColGray;
        string icon = p.Alive ? "SURVIVED" : "DIED";
        Color iconColor = p.Alive ? UIKit.ColGreen : UIKit.ColRed;

        var nameLbl = UIKit.MakeBodyLabel(p.Name.ToUpper(), 15, nameColor);
        nameLbl.CustomMinimumSize = new Vector2(160, 0);

        var iconLbl = UIKit.MakeBodyLabel(icon, 14, iconColor);
        iconLbl.CustomMinimumSize = new Vector2(80, 0);

        row.AddChild(nameLbl);
        row.AddChild(iconLbl);

        if (p.Alive && p.Health > 0)
        {
            int hpPct = (int)(p.Health / 10f);
            Color hpColor = hpPct >= 70 ? UIKit.ColGreen
                          : hpPct >= 40 ? UIKit.ColAmber
                          : UIKit.ColRed;
            var hpLbl = UIKit.MakeBodyLabel($"{hpPct}% HEALTH", 13, hpColor);
            row.AddChild(hpLbl);
        }

        if (!string.IsNullOrEmpty(p.Illness))
        {
            var illLbl = UIKit.MakeBodyLabel(
                GameData.IllnessDisplayName(p.Illness).ToUpper(), 12, UIKit.ColRed);
            row.AddChild(illLbl);
        }

        return row;
    }

    // =========================================================================
    // REASON TEXT
    // =========================================================================

    private static (string headline, string flavor) ReasonText(string reason) => reason switch
    {
        "game_over_dead" => (
            "EVERYONE IS DEAD.",
            "The wagon sat still on the trail. Weeks later, another party found it empty."),
        "game_over_unconscious" => (
            "THE PARTY FELL UNCONSCIOUS.",
            "No one was able to help the others. Days passed. No one came."),
        "game_over_starved" => (
            "YOU STARVED ON THE TRAIL.",
            "The last of the food ran out somewhere past the Platte. " +
            "The mountains were still ahead."),
        "game_over_stranded" => (
            "YOU WERE STRANDED.",
            "Without oxen or a working wagon, there was no going forward. " +
            "The wilderness closed in."),
        "game_over_time" => (
            "WINTER CAUGHT YOU.",
            "The Blue Mountains filled with snow. The trail became impassable. " +
            "You waited for a spring that never came in time."),
        _ => (
            $"FAILED: {reason.Replace("_", " ").ToUpper()}",
            "The Oregon Trail claimed another wagon."),
    };

    // =========================================================================
    // HELPERS
    // =========================================================================

    private static void AddCentered(VBoxContainer parent, Control child)
    {
        if (child is Label lbl)
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
        parent.AddChild(child);
    }

    private static void AddStatRow(VBoxContainer parent, string label, string value)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        var l = UIKit.MakeBodyLabel($"{label}:", 14, UIKit.ColAmberDim);
        l.CustomMinimumSize = new Vector2(200, 0);
        l.HorizontalAlignment = HorizontalAlignment.Right;

        var v = UIKit.MakeBodyLabel(value, 14, UIKit.ColParchment);
        v.CustomMinimumSize = new Vector2(220, 0);

        row.AddChild(l);
        row.AddChild(v);
        parent.AddChild(row);
    }
}
