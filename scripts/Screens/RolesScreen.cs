#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Roles assignment screen. Each role slot has a dropdown showing all living
/// conscious party members plus "(UNASSIGNED)". A person can hold at most
/// one role; selecting them in a second slot clears the first.
///
/// Roles and their active effects (applied elsewhere in systems):
///   DRIVER  - RepairSystem: -30% breakdown chance when conscious.
///   HUNTER  - HuntScreen: +30% meat yield multiplier.
///   MEDIC   - HealthSystem: illness damage * 0.80 when conscious.
///   SCOUT   - EventSystem: -25% random event chance while ScoutBonusUntil active.
///             (Scout bonus duration is handled by encounter cards, not the role itself.)
///
/// Signals:
///   RolesConfirmed - player clicked CONFIRM. MainScene returns to choice menu.
/// </summary>
public partial class RolesScreen : Control
{
    [Signal] public delegate void RolesConfirmedEventHandler();

    private GameState _state = null!;

    private static readonly string[] RoleKeys  = { "driver", "hunter", "medic", "scout" };
    private static readonly string[] RoleNames = { "DRIVER", "HUNTER", "MEDIC", "SCOUT" };

    private static readonly Dictionary<string, string> RoleDescriptions = new()
    {
        { "driver", "Reduces breakdown chance by 30%. Needs to be conscious to help." },
        { "hunter", "Increases meat yield by 30% on hunting trips." },
        { "medic",  "Slows illness damage by 20% for the whole party." },
        { "scout",  "Reduces hazard event chance by 25% per terrain warning." },
    };

    // One OptionButton per role slot
    private readonly Dictionary<string, OptionButton> _dropdowns = new();

    public void Initialize(GameState state)
    {
        _state = state;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var overlay = UIKit.MakeDarkOverlay(0.65f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = UIKit.MakePanel();
        panel.CustomMinimumSize = new Vector2(560, 0);
        center.AddChild(panel);

        var pad = new MarginContainer();
        pad.AddThemeConstantOverride("margin_left",   36);
        pad.AddThemeConstantOverride("margin_right",  36);
        pad.AddThemeConstantOverride("margin_top",    28);
        pad.AddThemeConstantOverride("margin_bottom", 28);
        panel.AddChild(pad);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        pad.AddChild(vbox);

        var title = UIKit.MakeDisplayLabel("ASSIGN ROLES", 26);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var hint = UIKit.MakeBodyLabel(
            "Each role gives one party member a special bonus. A person can hold only one role.",
            14, UIKit.ColGray);
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(hint);

        vbox.AddChild(UIKit.MakeDivider());

        // Party status strip
        vbox.AddChild(BuildPartyStrip());
        vbox.AddChild(UIKit.MakeSpacer(4));

        // Role rows
        for (int i = 0; i < RoleKeys.Length; i++)
        {
            vbox.AddChild(BuildRoleRow(RoleKeys[i], RoleNames[i]));
            vbox.AddChild(UIKit.MakeSpacer(2));
        }

        vbox.AddChild(UIKit.MakeDivider());

        // Confirm button
        var confirmRow = new HBoxContainer();
        confirmRow.Alignment = BoxContainer.AlignmentMode.Center;
        var confirmBtn = UIKit.MakePrimaryButton("CONFIRM ROLES", 20);
        confirmBtn.CustomMinimumSize = new Vector2(240, 52);
        confirmBtn.Pressed += OnConfirm;
        confirmRow.AddChild(confirmBtn);
        vbox.AddChild(confirmRow);
    }

    // =========================================================================
    // PARTY STATUS STRIP
    // =========================================================================

    private Control BuildPartyStrip()
    {
        var hbox = new HBoxContainer();
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.AddThemeConstantOverride("separation", 12);

        foreach (var person in _state.Party)
        {
            var card = new VBoxContainer();
            card.AddThemeConstantOverride("separation", 2);

            Color nameColor = !person.Alive       ? UIKit.ColGray
                            : person.Unconscious   ? UIKit.ColRed
                            : UIKit.ColParchment;

            string status = !person.Alive       ? "(DEAD)"
                          : person.Unconscious   ? "(OUT)"
                          : $"{(int)(person.Health / 10f)}%";

            var nameLbl = UIKit.MakeBodyLabel(person.Name, 13, nameColor);
            var statLbl = UIKit.MakeBodyLabel(status, 11, UIKit.ColGray);
            nameLbl.HorizontalAlignment = HorizontalAlignment.Center;
            statLbl.HorizontalAlignment = HorizontalAlignment.Center;
            card.AddChild(nameLbl);
            card.AddChild(statLbl);
            hbox.AddChild(card);
        }

        return hbox;
    }

    // =========================================================================
    // ROLE ROW: [Role Name / Desc] [OptionButton]
    // =========================================================================

    private Control BuildRoleRow(string roleKey, string roleName)
    {
        var row = new HBoxContainer();
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddThemeConstantOverride("separation", 10);

        // Left: name + description
        var left = new VBoxContainer();
        left.CustomMinimumSize = new Vector2(280, 0);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var nameLabel = UIKit.MakeDisplayLabel(roleName, 18, UIKit.ColAmber);
        nameLabel.HorizontalAlignment = HorizontalAlignment.Left;
        left.AddChild(nameLabel);

        var descLabel = UIKit.MakeBodyLabel(RoleDescriptions[roleKey], 12, UIKit.ColGray);
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        left.AddChild(descLabel);

        row.AddChild(left);

        // Right: OptionButton
        var dropdown = new OptionButton();
        dropdown.CustomMinimumSize = new Vector2(180, 40);
        StyleDropdown(dropdown);

        // Populate options
        dropdown.AddItem("(UNASSIGNED)", 0);
        int selectedIdx = 0;
        string currentAssignee = _state.Roles.GetValueOrDefault(roleKey, "");

        int idx = 1;
        foreach (var person in _state.Party)
        {
            if (!person.Alive) continue;
            dropdown.AddItem(person.Name, idx);

            // Disable unconscious members (can still clear by re-selecting)
            if (person.Unconscious)
                dropdown.SetItemDisabled(dropdown.ItemCount - 1, true);

            if (person.Name == currentAssignee)
                selectedIdx = idx;

            idx++;
        }

        dropdown.Selected = selectedIdx;

        // Enforce uniqueness: selecting a person in this slot clears any other
        // slot that had them
        dropdown.ItemSelected += (long chosen) =>
        {
            string chosenName = chosen == 0 ? "" : dropdown.GetItemText((int)chosen);
            if (!string.IsNullOrEmpty(chosenName))
            {
                // Clear chosenName from any other role
                foreach (string otherKey in RoleKeys)
                {
                    if (otherKey == roleKey) continue;
                    if (_state.Roles.GetValueOrDefault(otherKey, "") == chosenName)
                    {
                        _state.Roles[otherKey] = "";
                        // Reset the other dropdown to (UNASSIGNED)
                        if (_dropdowns.TryGetValue(otherKey, out var otherDd))
                            otherDd.Selected = 0;
                    }
                }
            }
            _state.Roles[roleKey] = chosenName;
        };

        _dropdowns[roleKey] = dropdown;
        row.AddChild(dropdown);

        return row;
    }

    // =========================================================================
    // CONFIRM
    // =========================================================================

    private void OnConfirm()
    {
        // Final sync (dropdowns already write to _state.Roles on change;
        // this is a safety pass in case the user never interacted)
        for (int i = 0; i < RoleKeys.Length; i++)
        {
            if (!_dropdowns.TryGetValue(RoleKeys[i], out var dd)) continue;
            string chosen = dd.Selected == 0 ? "" : dd.GetItemText(dd.Selected);
            _state.Roles[RoleKeys[i]] = chosen;
        }

        EmitSignal(SignalName.RolesConfirmed);
    }

    // =========================================================================
    // STYLE
    // =========================================================================

    private static void StyleDropdown(OptionButton btn)
    {
        var style = new StyleBoxFlat
        {
            BgColor     = new Color(UIKit.ColDarkBrown, 0.85f),
            BorderColor = new Color(UIKit.ColAmberDim,  0.60f),
            BorderWidthLeft   = 1, BorderWidthRight  = 1,
            BorderWidthTop    = 1, BorderWidthBottom = 1,
            ContentMarginLeft   = 10, ContentMarginRight  = 10,
            ContentMarginTop    =  6, ContentMarginBottom =  6,
        };
        btn.AddThemeStyleboxOverride("normal",  style);
        btn.AddThemeStyleboxOverride("hover",   style);
        btn.AddThemeStyleboxOverride("pressed", style);
        btn.AddThemeStyleboxOverride("focus",   style);
        btn.AddThemeFontOverride("font", UIKit.BodyFont);
        btn.AddThemeFontSizeOverride("font_size", 15);
        btn.AddThemeColorOverride("font_color",          UIKit.ColParchment);
        btn.AddThemeColorOverride("font_hover_color",    UIKit.ColWhite);
        btn.AddThemeColorOverride("font_disabled_color", UIKit.ColGray);
    }
}
