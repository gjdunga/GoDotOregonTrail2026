#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Systems;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Save slot picker screen, used for both Load Game and Delete Game flows.
///
/// Layout:
///   Parchment background + dark overlay
///   Title ("LOAD GAME" or "DELETE GAME")
///   Scrollable list of 11 slot rows (auto + slots 0-9)
///   Each row shows: slot name, leader, day, miles, party count
///   Empty slots are shown but disabled
///   BACK button returns to main menu
///
/// Signals:
///   SlotSelected(slotId) - user picked a populated slot
///   BackRequested        - user wants to return to menu
/// </summary>
public partial class SaveSlotScreen : Control
{
    public enum Mode { Load, Delete }

    [Signal] public delegate void SlotSelectedEventHandler(string slotId);
    [Signal] public delegate void BackRequestedEventHandler();

    private readonly Mode _mode;
    private VBoxContainer _slotList = null!;
    private Control? _confirmDialog;

    public SaveSlotScreen(Mode mode)
    {
        _mode = mode;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Background
        var bg = UIKit.MakeParchmentBackground();
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var overlay = UIKit.MakeDarkOverlay(0.35f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // Center column
        var center = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        center.SetAnchor(Side.Left, 0.15f);
        center.SetAnchor(Side.Right, 0.85f);
        center.SetAnchor(Side.Top, 0.05f);
        center.SetAnchor(Side.Bottom, 0.95f);
        center.SetOffset(Side.Left, 0);
        center.SetOffset(Side.Right, 0);
        center.SetOffset(Side.Top, 0);
        center.SetOffset(Side.Bottom, 0);
        AddChild(center);

        // Title
        string titleKey = _mode == Mode.Load ? TK.MenuLoadGame : TK.MenuDeleteGame;
        var title = UIKit.MakeDisplayLabel(Tr(titleKey), 28);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        center.AddChild(title);

        center.AddChild(UIKit.MakeSpacer(12));

        // Scrollable slot list
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        center.AddChild(scroll);

        _slotList = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _slotList.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_slotList);

        BuildSlotRows();

        center.AddChild(UIKit.MakeSpacer(8));

        // Back button
        var btnBack = UIKit.MakeSecondaryButton(Tr(TK.SettingsBack), 18);
        btnBack.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        btnBack.Pressed += () => EmitSignal(SignalName.BackRequested);
        center.AddChild(btnBack);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        if (key.Keycode == Key.Escape)
        {
            if (_confirmDialog != null)
                CloseConfirmDialog();
            else
                EmitSignal(SignalName.BackRequested);
            GetViewport().SetInputAsHandled();
        }
    }

    // ========================================================================
    // SLOT LIST
    // ========================================================================

    private void BuildSlotRows()
    {
        _slotList.QueueFreeChildren();

        var allSlots = SaveFileSystem.ListAllSlots();

        // Auto-save first
        AddSlotRow(SaveFileSystem.AutoSlotId, allSlots.GetValueOrDefault(SaveFileSystem.AutoSlotId));

        // Manual slots 0-9
        for (int i = 0; i < SaveFileSystem.ManualSlotCount; i++)
        {
            string id = i.ToString();
            AddSlotRow(id, allSlots.GetValueOrDefault(id));
        }
    }

    private void AddSlotRow(string slotId, SaveSlotMeta? meta)
    {
        bool isEmpty = meta == null;
        bool isAuto = slotId == SaveFileSystem.AutoSlotId;

        // Row panel with dark background
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = isEmpty
                ? new Color(UIKit.ColDarkBrown, 0.5f)
                : new Color(UIKit.ColDarkBrown, 0.8f),
            BorderColor = UIKit.ColAmberDim,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 16, ContentMarginRight = 16,
            ContentMarginTop = 8, ContentMarginBottom = 8,
        };
        panel.AddThemeStyleboxOverride("panel", style);

        // VBox contains: top stats row + optional recent log lines
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 16);

        // Slot label (left column)
        string slotLabel = isAuto ? Tr(TK.SaveSlotAuto) : $"SLOT {int.Parse(slotId) + 1}";
        var lblSlot = UIKit.MakeDisplayLabel(slotLabel, 16);
        lblSlot.CustomMinimumSize = new Vector2(120, 0);
        hbox.AddChild(lblSlot);

        if (isEmpty)
        {
            // Empty slot
            var lblEmpty = UIKit.MakeBodyLabel(Tr(TK.SaveSlotEmpty), 14, UIKit.ColGray);
            lblEmpty.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hbox.AddChild(lblEmpty);
        }
        else
        {
            // Leader name
            var lblLeader = UIKit.MakeBodyLabel(meta!.LeaderName, 15, UIKit.ColParchment);
            lblLeader.CustomMinimumSize = new Vector2(140, 0);
            hbox.AddChild(lblLeader);

            // Day
            var lblDay = UIKit.MakeBodyLabel($"{Tr(TK.SaveDay)} {meta.Day}", 13, UIKit.ColAmber);
            lblDay.CustomMinimumSize = new Vector2(80, 0);
            hbox.AddChild(lblDay);

            // Miles
            var lblMiles = UIKit.MakeBodyLabel($"{Tr(TK.SaveMiles)} {meta.Miles}", 13, UIKit.ColAmber);
            lblMiles.CustomMinimumSize = new Vector2(100, 0);
            hbox.AddChild(lblMiles);

            // Party alive
            var lblParty = UIKit.MakeBodyLabel(
                $"{Tr(TK.SavePartyAlive)} {meta.PartyAlive}/5", 13, UIKit.ColAmber);
            lblParty.CustomMinimumSize = new Vector2(80, 0);
            hbox.AddChild(lblParty);

            // Save date (formatted)
            string dateStr = FormatSaveDate(meta.SavedAt);
            var lblDate = UIKit.MakeBodyLabel(dateStr, 12, UIKit.ColGray);
            lblDate.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            lblDate.HorizontalAlignment = HorizontalAlignment.Right;
            hbox.AddChild(lblDate);
        }

        vbox.AddChild(hbox);

        // Recent journal log (only for occupied slots with log entries)
        if (!isEmpty && meta!.RecentLog.Count > 0)
        {
            foreach (string logLine in meta.RecentLog)
            {
                var lblLog = UIKit.MakeBodyLabel(logLine, 11, UIKit.ColGray);
                lblLog.HorizontalAlignment = HorizontalAlignment.Left;
                vbox.AddChild(lblLog);
            }
        }

        panel.AddChild(vbox);

        // Wrap in a button-like clickable area
        if (!isEmpty)
        {
            var btn = new Button
            {
                Flat = true,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            // Transparent style so the panel shows through
            var btnStyle = new StyleBoxEmpty();
            btn.AddThemeStyleboxOverride("normal", btnStyle);
            btn.AddThemeStyleboxOverride("hover", new StyleBoxFlat
            {
                BgColor = new Color(UIKit.ColAmber, 0.15f),
            });
            btn.AddThemeStyleboxOverride("pressed", new StyleBoxFlat
            {
                BgColor = new Color(UIKit.ColAmber, 0.25f),
            });
            btn.AddThemeStyleboxOverride("focus", btnStyle);

            // Stack button on top of panel
            var stack = new Control();
            stack.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            stack.CustomMinimumSize = new Vector2(0, 48);

            panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            btn.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            stack.AddChild(panel);
            stack.AddChild(btn);

            string capturedId = slotId;
            btn.Pressed += () => OnSlotClicked(capturedId, meta!.SlotName);

            _slotList.AddChild(stack);
        }
        else
        {
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.CustomMinimumSize = new Vector2(0, 48);
            _slotList.AddChild(panel);
        }
    }

    // ========================================================================
    // SLOT ACTIONS
    // ========================================================================

    private void OnSlotClicked(string slotId, string slotName)
    {
        if (_mode == Mode.Load)
        {
            GD.Print($"[SaveSlotScreen] Load slot '{slotId}'");
            EmitSignal(SignalName.SlotSelected, slotId);
        }
        else
        {
            // Delete mode: show confirmation
            ShowDeleteConfirmation(slotId, slotName);
        }
    }

    private void ShowDeleteConfirmation(string slotId, string slotName)
    {
        if (_confirmDialog != null) CloseConfirmDialog();

        _confirmDialog = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(UIKit.ColDarkBrown, 0.95f),
            BorderColor = new Color("CC3333"),
            BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            ContentMarginLeft = 24, ContentMarginRight = 24,
            ContentMarginTop = 16, ContentMarginBottom = 16,
        };
        ((PanelContainer)_confirmDialog).AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);

        // Warning text
        var lblConfirm = UIKit.MakeBodyLabel(
            Tr(TK.SaveConfirmDelete), 16, new Color("FF6666"));
        lblConfirm.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(lblConfirm);

        // Slot name being deleted
        var lblSlot = UIKit.MakeDisplayLabel(slotName, 18);
        lblSlot.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(lblSlot);

        // Button row
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 16);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;

        var btnYes = UIKit.MakeSecondaryButton(Tr(TK.CommonConfirm), 16);
        btnYes.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        string capturedId = slotId;
        btnYes.Pressed += () =>
        {
            bool ok = SaveFileSystem.DeleteSlot(capturedId);
            CloseConfirmDialog();
            if (ok)
            {
                GD.Print($"[SaveSlotScreen] Deleted slot '{capturedId}'");
                BuildSlotRows(); // refresh the list
            }
        };
        btnRow.AddChild(btnYes);

        var btnNo = UIKit.MakeSecondaryButton(Tr(TK.CommonCancel), 16);
        btnNo.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        btnNo.Pressed += CloseConfirmDialog;
        btnRow.AddChild(btnNo);

        vbox.AddChild(btnRow);

        ((PanelContainer)_confirmDialog).AddChild(vbox);

        _confirmDialog.SetAnchor(Side.Left, 0.25f);
        _confirmDialog.SetAnchor(Side.Right, 0.75f);
        _confirmDialog.SetAnchor(Side.Top, 0.35f);
        _confirmDialog.SetAnchor(Side.Bottom, 0.65f);

        AddChild(_confirmDialog);
    }

    private void CloseConfirmDialog()
    {
        if (_confirmDialog != null)
        {
            _confirmDialog.QueueFree();
            _confirmDialog = null;
        }
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    private static string FormatSaveDate(string isoDate)
    {
        if (DateTime.TryParse(isoDate, out var dt))
            return dt.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
        return isoDate;
    }
}

// Extension to avoid naming collision
internal static class VBoxContainerExt
{
    public static void QueueFreeChildren(this VBoxContainer container)
    {
        foreach (var child in container.GetChildren())
        {
            if (child is Node n) n.QueueFree();
        }
    }
}
