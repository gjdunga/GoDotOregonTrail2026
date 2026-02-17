#nullable enable
using System;
using Godot;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Main menu screen. Parchment background with themed buttons.
///
/// Options:
///   NEW GAME      -> emits NewGameRequested
///   LOAD GAME     -> emits LoadGameRequested
///   DELETE GAME   -> emits DeleteGameRequested
///   GRAPHICS      -> inline sub-panel (fullscreen toggle)
///   SOUND         -> inline sub-panel (volume sliders)
///   QUIT          -> exits game
///
/// F12 (in-game only, not here) opens the dev console. No visible hint.
/// </summary>
public partial class MainMenuScreen : Control
{
    [Signal] public delegate void NewGameRequestedEventHandler();
    [Signal] public delegate void LoadGameRequestedEventHandler();
    [Signal] public delegate void DeleteGameRequestedEventHandler();

    // Sub-panels
    private Control? _settingsPanel;
    private VBoxContainer _menuButtons = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Parchment tiled background
        var bg = UIKit.MakeParchmentBackground();
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // Dark overlay to mute the parchment
        var overlay = UIKit.MakeDarkOverlay(0.35f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // Center column container
        var center = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        center.SetAnchor(Side.Left, 0.3f);
        center.SetAnchor(Side.Right, 0.7f);
        center.SetAnchor(Side.Top, 0.05f);
        center.SetAnchor(Side.Bottom, 0.95f);
        center.SetOffset(Side.Left, 0);
        center.SetOffset(Side.Right, 0);
        center.SetOffset(Side.Top, 0);
        center.SetOffset(Side.Bottom, 0);
        AddChild(center);

        // Logo
        var logo = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/images/ui/logo_oregon_trail_2026.webp"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(0, 220),
        };
        center.AddChild(logo);

        center.AddChild(UIKit.MakeSpacer(12));

        // Menu buttons (all same style for visual consistency)
        _menuButtons = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        _menuButtons.AddThemeConstantOverride("separation", 6);

        var btnNew    = UIKit.MakeSecondaryButton(Tr(TK.MenuNewGame), 20);
        var btnLoad   = UIKit.MakeSecondaryButton(Tr(TK.MenuLoadGame), 20);
        var btnDelete = UIKit.MakeSecondaryButton(Tr(TK.MenuDeleteGame));
        var btnGfx    = UIKit.MakeSecondaryButton(Tr(TK.MenuGraphics));
        var btnSound  = UIKit.MakeSecondaryButton(Tr(TK.MenuSound));
        var btnQuit   = UIKit.MakeSecondaryButton(Tr(TK.MenuQuit));

        // Center buttons horizontally
        foreach (var btn in new Button[] { btnNew, btnLoad, btnDelete, btnGfx, btnSound, btnQuit })
        {
            btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            _menuButtons.AddChild(btn);
        }

        center.AddChild(_menuButtons);

        // Connect signals
        btnNew.Pressed    += () => EmitSignal(SignalName.NewGameRequested);
        btnLoad.Pressed   += () => EmitSignal(SignalName.LoadGameRequested);
        btnDelete.Pressed += () => EmitSignal(SignalName.DeleteGameRequested);
        btnGfx.Pressed    += OnGraphicsPressed;
        btnSound.Pressed  += OnSoundPressed;
        btnQuit.Pressed   += () => GetTree().Quit();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        // Escape closes settings sub-panel, or quits from main menu
        if (key.Keycode == Key.Escape)
        {
            if (_settingsPanel != null)
            {
                CloseSettingsPanel();
            }
            else
            {
                GetTree().Quit();
            }
            GetViewport().SetInputAsHandled();
        }
    }

    // ========================================================================
    // GRAPHICS SUB-PANEL
    // ========================================================================

    private void OnGraphicsPressed()
    {
        GD.Print("[MainMenu] Graphics pressed");
        try
        {
            if (_settingsPanel != null) CloseSettingsPanel();

            _settingsPanel = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = new Color(UIKit.ColDarkBrown, 0.92f),
                BorderColor = UIKit.ColAmberDim,
                BorderWidthLeft = 2, BorderWidthRight = 2,
                BorderWidthTop = 2, BorderWidthBottom = 2,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                ContentMarginLeft = 24, ContentMarginRight = 24,
                ContentMarginTop = 16, ContentMarginBottom = 16,
            };
            ((PanelContainer)_settingsPanel).AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);

            // Title
            var title = UIKit.MakeDisplayLabel(Tr(TK.MenuGraphics), 22);
            vbox.AddChild(title);

            // Fullscreen toggle (staged, not instant)
            var fsRow = new HBoxContainer();
            var fsLabel = UIKit.MakeBodyLabel(Tr(TK.SettingsFullscreen), 16, UIKit.ColParchment);
            fsLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            fsRow.AddChild(fsLabel);

            var sm = OregonTrail2026.Systems.SettingsManager.Instance;
            var fsToggle = new CheckButton();
            fsToggle.ButtonPressed = sm.Fullscreen;
            fsRow.AddChild(fsToggle);
            vbox.AddChild(fsRow);

            // Button row
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 12);
            btnRow.Alignment = BoxContainer.AlignmentMode.Center;

            // Apply button
            var btnApply = UIKit.MakeSecondaryButton(Tr(TK.CommonApply), 16);
            btnApply.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            btnApply.Pressed += () =>
            {
                sm.Fullscreen = fsToggle.ButtonPressed;
                GD.Print($"[MainMenu] Applied fullscreen={fsToggle.ButtonPressed}");
            };
            btnRow.AddChild(btnApply);

            // Back button
            var btnBack = UIKit.MakeSecondaryButton(Tr(TK.SettingsBack), 16);
            btnBack.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            btnBack.Pressed += CloseSettingsPanel;
            btnRow.AddChild(btnBack);

            vbox.AddChild(btnRow);

            ((PanelContainer)_settingsPanel).AddChild(vbox);

            _settingsPanel.SetAnchor(Side.Left, 0.25f);
            _settingsPanel.SetAnchor(Side.Right, 0.75f);
            _settingsPanel.SetAnchor(Side.Top, 0.3f);
            _settingsPanel.SetAnchor(Side.Bottom, 0.7f);

            AddChild(_settingsPanel);
            _menuButtons.Visible = false;
            GD.Print("[MainMenu] Graphics panel shown");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MainMenu] Graphics error: {e}");
        }
    }

    // ========================================================================
    // SOUND SUB-PANEL
    // ========================================================================

    private void OnSoundPressed()
    {
        GD.Print("[MainMenu] Sound pressed");
        try
        {
            if (_settingsPanel != null) CloseSettingsPanel();

            _settingsPanel = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = new Color(UIKit.ColDarkBrown, 0.92f),
                BorderColor = UIKit.ColAmberDim,
                BorderWidthLeft = 2, BorderWidthRight = 2,
                BorderWidthTop = 2, BorderWidthBottom = 2,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                ContentMarginLeft = 24, ContentMarginRight = 24,
                ContentMarginTop = 16, ContentMarginBottom = 16,
            };
            ((PanelContainer)_settingsPanel).AddThemeStyleboxOverride("panel", style);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 12);

            var title = UIKit.MakeDisplayLabel(Tr(TK.MenuSound), 22);
            vbox.AddChild(title);

            var sm = OregonTrail2026.Systems.SettingsManager.Instance;

            vbox.AddChild(MakeVolumeRow(Tr(TK.SettingsMasterVol), sm.MasterVolume,
                v => sm.MasterVolume = v));
            vbox.AddChild(MakeVolumeRow(Tr(TK.SettingsMusicVol), sm.MusicVolume,
                v => sm.MusicVolume = v));
            vbox.AddChild(MakeVolumeRow(Tr(TK.SettingsSfxVol), sm.SfxVolume,
                v => sm.SfxVolume = v));

            var btnBack = UIKit.MakeSecondaryButton(Tr(TK.SettingsBack), 16);
            btnBack.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            btnBack.Pressed += CloseSettingsPanel;
            vbox.AddChild(btnBack);

            ((PanelContainer)_settingsPanel).AddChild(vbox);

            _settingsPanel.SetAnchor(Side.Left, 0.2f);
            _settingsPanel.SetAnchor(Side.Right, 0.8f);
            _settingsPanel.SetAnchor(Side.Top, 0.2f);
            _settingsPanel.SetAnchor(Side.Bottom, 0.8f);

            AddChild(_settingsPanel);
            _menuButtons.Visible = false;
            GD.Print("[MainMenu] Sound panel shown");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[MainMenu] Sound error: {e}");
        }
    }

    private static HBoxContainer MakeVolumeRow(string label, float initialValue, Action<float> onChange)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var lbl = UIKit.MakeBodyLabel(label, 15, UIKit.ColParchment);
        lbl.CustomMinimumSize = new Vector2(160, 0);
        row.AddChild(lbl);

        var slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.05f,
            Value = initialValue,
            CustomMinimumSize = new Vector2(200, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        var pctLabel = UIKit.MakeBodyLabel($"{(int)(initialValue * 100)}%", 14, UIKit.ColAmber);
        pctLabel.CustomMinimumSize = new Vector2(48, 0);
        pctLabel.HorizontalAlignment = HorizontalAlignment.Right;

        slider.ValueChanged += (v) =>
        {
            onChange((float)v);
            pctLabel.Text = $"{(int)(v * 100)}%";
        };

        row.AddChild(slider);
        row.AddChild(pctLabel);
        return row;
    }

    // ========================================================================
    // SUB-PANEL MANAGEMENT
    // ========================================================================

    private void CloseSettingsPanel()
    {
        if (_settingsPanel != null)
        {
            _settingsPanel.QueueFree();
            _settingsPanel = null;
        }
        _menuButtons.Visible = true;
    }
}
