#nullable enable
using System;
using System.Linq;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Systems;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Victory screen shown when the party reaches Oregon City (chapter_complete).
///
/// Displays:
///   - Arrival header and survivor list
///   - Journey stats: miles, days on trail, date arrived, cash remaining
///   - Scoring breakdown: survival bonus + pace bonus + cash bonus
///   - Two buttons: PLAY AGAIN (new game) and MAIN MENU
///
/// Scoring (all informal, final number displayed for flavor):
///   Base:          1000 pts
///   Per survivor:  +200 pts each
///   Days bonus:    1000 - (days * 4), floored at 0  (faster = more pts)
///   Cash bonus:    (int)(cash remaining / 5)
///
/// Signals:
///   PlayAgainRequested - user wants a new game.
///   MainMenuRequested  - user wants main menu.
/// </summary>
public partial class VictoryScreen : Control
{
    [Signal] public delegate void PlayAgainRequestedEventHandler();
    [Signal] public delegate void MainMenuRequestedEventHandler();

    private GameState _state = null!;

    public void Initialize(GameState state)
    {
        _state = state;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Oregon City arrival background
        var bg = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/images/bg/bg_oregon_city_arrival.webp"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var dimOverlay = UIKit.MakeDarkOverlay(0.55f);
        dimOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dimOverlay);

        // Centered scroll panel
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scroll.SetAnchor(Side.Left,   0.10f);
        scroll.SetAnchor(Side.Right,  0.90f);
        scroll.SetAnchor(Side.Top,    0.04f);
        scroll.SetAnchor(Side.Bottom, 0.96f);
        AddChild(scroll);

        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(vbox);

        // ---- HEADER ----
        AddCentered(vbox, UIKit.MakeDisplayLabel(Tr(TK.WinLocation), 32, UIKit.ColAmber));

        string dateStr = DateCalc.DateStr(_state.Day);
        AddCentered(vbox, UIKit.MakeBodyLabel($"YOU ARRIVED ON {dateStr}, 1850.", 16, UIKit.ColParchment));
        vbox.AddChild(UIKit.MakeDivider());

        // ---- SURVIVORS ----
        AddCentered(vbox, UIKit.MakeDisplayLabel(Tr(TK.WinSurvivors), 22));

        var living = _state.Living();
        if (living.Count == 0)
        {
            AddCentered(vbox, UIKit.MakeBodyLabel(Tr(TK.WinNoSurvivors), 15, UIKit.ColRed));
        }
        else
        {
            var partyRow = new HBoxContainer();
            partyRow.Alignment = BoxContainer.AlignmentMode.Center;
            partyRow.AddThemeConstantOverride("separation", 20);

            foreach (var p in living)
            {
                var col = new VBoxContainer();
                col.AddThemeConstantOverride("separation", 2);

                var nameLbl = UIKit.MakeBodyLabel(p.Name.ToUpper(), 15, UIKit.ColParchment);
                nameLbl.HorizontalAlignment = HorizontalAlignment.Center;

                int hpPct = (int)(p.Health / 10f);
                Color hpColor = hpPct >= 70 ? UIKit.ColGreen
                              : hpPct >= 40 ? UIKit.ColAmber
                              : UIKit.ColRed;
                var hpLbl = UIKit.MakeBodyLabel($"HEALTH {hpPct}%", 12, hpColor);
                hpLbl.HorizontalAlignment = HorizontalAlignment.Center;

                if (!string.IsNullOrEmpty(p.Illness))
                {
                    var illLbl = UIKit.MakeBodyLabel(
                        GameData.IllnessDisplayName(p.Illness).ToUpper(), 11, UIKit.ColRed);
                    illLbl.HorizontalAlignment = HorizontalAlignment.Center;
                    col.AddChild(illLbl);
                }

                col.AddChild(nameLbl);
                col.AddChild(hpLbl);
                partyRow.AddChild(col);
            }
            vbox.AddChild(partyRow);
        }

        int dead = _state.Party.Count(p => !p.Alive);
        if (dead > 0)
        {
            // WIN_LOST: "{0} MEMBER{1} LOST ON THE TRAIL."
            AddCentered(vbox,
                UIKit.MakeBodyLabel(
                    string.Format(Tr(TK.WinLost), dead, dead > 1 ? "S" : ""),
                    13, UIKit.ColGray));
        }

        vbox.AddChild(UIKit.MakeDivider());

        // ---- JOURNEY STATS ----
        AddCentered(vbox, UIKit.MakeDisplayLabel(Tr(TK.WinStats), 20));

        var stats = new VBoxContainer();
        stats.AddThemeConstantOverride("separation", 6);
        stats.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        AddStatRow(stats, Tr(TK.WinMiles),       $"{_state.Miles}");
        AddStatRow(stats, Tr(TK.WinDays),         $"{_state.Day}");
        AddStatRow(stats, Tr(TK.WinDateArrived),  dateStr);
        AddStatRow(stats, Tr(TK.WinCash),         $"${_state.Cash:F0}");
        AddStatRow(stats, Tr(TK.WinFood),         $"{_state.Supplies.GetValueOrDefault("food", 0)} LBS");

        vbox.AddChild(stats);
        vbox.AddChild(UIKit.MakeDivider());

        // ---- SCORE ----
        int score = CalculateScore();
        AddCentered(vbox, UIKit.MakeDisplayLabel(Tr(TK.WinScore), 20));
        AddCentered(vbox, UIKit.MakeDisplayLabel($"{score}", 36, UIKit.ColAmber));
        AddCentered(vbox, UIKit.MakeBodyLabel(ScoreRank(score), 14, UIKit.ColGray));

        vbox.AddChild(UIKit.MakeSpacer(8));

        // ---- BUTTONS ----
        var btnRow = new HBoxContainer();
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        btnRow.AddThemeConstantOverride("separation", 20);

        var playAgain = UIKit.MakePrimaryButton(Tr(TK.CommonPlayAgain), 18);
        playAgain.CustomMinimumSize = new Vector2(200, 52);
        playAgain.Pressed += () => EmitSignal(SignalName.PlayAgainRequested);
        btnRow.AddChild(playAgain);

        var mainMenu = UIKit.MakeSecondaryButton(Tr(TK.CommonMainMenu), 18);
        mainMenu.CustomMinimumSize = new Vector2(200, 52);
        mainMenu.Pressed += () => EmitSignal(SignalName.MainMenuRequested);
        btnRow.AddChild(mainMenu);

        vbox.AddChild(btnRow);
        vbox.AddChild(UIKit.MakeSpacer(16));
    }

    // =========================================================================
    // SCORE
    // =========================================================================

    private int CalculateScore()
    {
        int scoreMult = GameData.GetOccupation(_state.Occupation)?.ScoreMult ?? 1;
        int base_  = 1000;
        int surv   = _state.Living().Count * 200;
        int days   = Math.Max(0, 1000 - _state.Day * 4);
        int cash   = (int)(_state.Cash / 5f);
        return (base_ + surv + days + cash) * scoreMult;
    }

    private string ScoreRank(int score) => score switch
    {
        >= 2500 => Tr(TK.WinRankLegendary),
        >= 2000 => Tr(TK.WinRankSeasoned),
        >= 1500 => Tr(TK.WinRankCapable),
        >= 1000 => Tr(TK.WinRankDetermined),
        _       => Tr(TK.WinRankLucky),
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
        v.CustomMinimumSize = new Vector2(120, 0);

        row.AddChild(l);
        row.AddChild(v);
        parent.AddChild(row);
    }
}
