#nullable enable
using System;
using Godot;
using OregonTrail2026.Models;

namespace OregonTrail2026.Screens;

/// <summary>
/// Splash/loading screen shown at game launch.
///
/// Phases:
///   1. LOADING  - Logo fades in, progress bar fills over ~10 seconds.
///                 Escape quits the game immediately.
///   2. WAITING  - "PRESS ANY KEY TO CONTINUE" pulses.
///                 Any key except Escape advances to party setup.
///                 Escape quits the game.
///
/// Music: OregonTrail2026_Title_Score_V1a.mp3 (starts immediately, continues
///        until the key press, then MainScene switches to menu music).
///
/// Visual: Black background, centered logo (fade in), amber loading bar,
///         version text bottom-right.
/// </summary>
public partial class SplashScreen : Control
{
    [Signal]
    public delegate void SplashFinishedEventHandler();

    // ---- Palette (matching PartySetupScreen 1985 aesthetic) ----
    private static readonly Color ColAmber     = new("FFD700");
    private static readonly Color ColAmberDim  = new("8B7332");
    private static readonly Color ColBarBg     = new("1A1408");
    private static readonly Color ColBarFill   = new("FFD700");
    private static readonly Color ColBlack     = new("0A0804");
    private static readonly Color ColGray      = new("666655");
    private static readonly Color ColWhite     = new("F0E6D2");

    // ---- Timing ----
    private const float LogoFadeDuration = 1.5f;
    private const float LoadDuration = 10.0f;
    private const float PulsePeriod = 1.8f;

    // ---- State ----
    private enum Phase { Loading, Waiting }
    private Phase _phase = Phase.Loading;
    private float _elapsed = 0f;
    private float _pulseTimer = 0f;

    // ---- Fake loading steps (shown as status text under the bar) ----
    private static readonly (float pct, string text)[] _loadingSteps =
    {
        (0.00f, "HITCHING THE OXEN..."),
        (0.08f, "LOADING SUPPLIES..."),
        (0.18f, "CHECKING WAGON WHEELS..."),
        (0.30f, "MAPPING THE TRAIL..."),
        (0.42f, "STOCKING PROVISIONS..."),
        (0.55f, "READING ALMANAC..."),
        (0.65f, "SCOUTING THE ROUTE..."),
        (0.78f, "PACKING THE WAGON..."),
        (0.88f, "SAYING GOODBYE TO KIN..."),
        (0.96f, "READY TO DEPART!"),
    };

    // ---- Nodes ----
    private TextureRect _logo = null!;
    private ColorRect _barBg = null!;
    private ColorRect _barFill = null!;
    private Label _statusLabel = null!;
    private Label _pressKeyLabel = null!;
    private Label _versionLabel = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Black background
        var bg = new ColorRect { Color = ColBlack };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // Logo (centered, starts invisible for fade-in)
        _logo = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/images/ui/logo_oregon_trail_2026.webp"),
            ExpandMode = TextureRect.ExpandModeEnum.KeepSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = new Color(1, 1, 1, 0), // starts fully transparent
        };
        _logo.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Shift logo up a bit to make room for the loading bar
        _logo.SetAnchor(Side.Top, 0.05f);
        _logo.SetAnchor(Side.Bottom, 0.70f);
        AddChild(_logo);

        // Loading bar container (centered below logo)
        var barContainer = new Control();
        barContainer.SetAnchor(Side.Left, 0.5f);
        barContainer.SetAnchor(Side.Top, 0.74f);
        barContainer.SetOffset(Side.Left, -250);
        barContainer.SetOffset(Side.Right, 250);
        barContainer.SetOffset(Side.Top, 0);
        barContainer.SetOffset(Side.Bottom, 24);
        AddChild(barContainer);

        // Bar background
        _barBg = new ColorRect
        {
            Color = ColBarBg,
            CustomMinimumSize = new Vector2(500, 24),
        };
        _barBg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        barContainer.AddChild(_barBg);

        // Bar border
        var barBorder = new ReferenceRect
        {
            BorderColor = ColAmberDim,
            BorderWidth = 2.0f,
            EditorOnly = false,
        };
        barBorder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        barContainer.AddChild(barBorder);

        // Bar fill (starts at width 0)
        _barFill = new ColorRect
        {
            Color = ColBarFill,
            CustomMinimumSize = new Vector2(0, 20),
        };
        _barFill.SetAnchor(Side.Left, 0);
        _barFill.SetAnchor(Side.Top, 0);
        _barFill.SetOffset(Side.Left, 2);
        _barFill.SetOffset(Side.Top, 2);
        _barFill.SetOffset(Side.Right, 2);   // will be updated in _Process
        _barFill.SetOffset(Side.Bottom, -2);
        barContainer.AddChild(_barFill);

        // Status text (below bar)
        _statusLabel = new Label
        {
            Text = _loadingSteps[0].text,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _statusLabel.SetAnchor(Side.Left, 0.5f);
        _statusLabel.SetAnchor(Side.Top, 0.79f);
        _statusLabel.SetOffset(Side.Left, -250);
        _statusLabel.SetOffset(Side.Right, 250);
        _statusLabel.AddThemeColorOverride("font_color", ColAmberDim);
        _statusLabel.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_statusLabel);

        // "PRESS ANY KEY" (hidden until loading completes)
        _pressKeyLabel = new Label
        {
            Text = "PRESS ANY KEY TO CONTINUE",
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        _pressKeyLabel.SetAnchor(Side.Left, 0.5f);
        _pressKeyLabel.SetAnchor(Side.Top, 0.86f);
        _pressKeyLabel.SetOffset(Side.Left, -200);
        _pressKeyLabel.SetOffset(Side.Right, 200);
        _pressKeyLabel.AddThemeColorOverride("font_color", ColAmber);
        _pressKeyLabel.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_pressKeyLabel);

        // Version label (bottom-right)
        _versionLabel = new Label
        {
            Text = $"v{GameConstants.GameVersion}",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        _versionLabel.SetAnchor(Side.Right, 1.0f);
        _versionLabel.SetAnchor(Side.Bottom, 1.0f);
        _versionLabel.SetOffset(Side.Left, -200);
        _versionLabel.SetOffset(Side.Right, -16);
        _versionLabel.SetOffset(Side.Top, -32);
        _versionLabel.SetOffset(Side.Bottom, -8);
        _versionLabel.AddThemeColorOverride("font_color", ColGray);
        _versionLabel.AddThemeFontSizeOverride("font_size", 13);
        AddChild(_versionLabel);

        // Escape hint (bottom-left)
        var escLabel = new Label
        {
            Text = "ESC TO QUIT",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        escLabel.SetAnchor(Side.Left, 0.0f);
        escLabel.SetAnchor(Side.Bottom, 1.0f);
        escLabel.SetOffset(Side.Left, 16);
        escLabel.SetOffset(Side.Right, 200);
        escLabel.SetOffset(Side.Top, -32);
        escLabel.SetOffset(Side.Bottom, -8);
        escLabel.AddThemeColorOverride("font_color", ColGray);
        escLabel.AddThemeFontSizeOverride("font_size", 11);
        AddChild(escLabel);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (_phase == Phase.Loading)
        {
            _elapsed += dt;

            // Logo fade-in
            float logoAlpha = Math.Clamp(_elapsed / LogoFadeDuration, 0f, 1f);
            _logo.Modulate = new Color(1, 1, 1, logoAlpha);

            // Progress bar
            float progress = Math.Clamp(_elapsed / LoadDuration, 0f, 1f);
            float barMaxWidth = 496f; // 500 - 4px padding
            _barFill.SetOffset(Side.Right, 2 + barMaxWidth * progress);

            // Status text (step through messages based on progress)
            string statusText = _loadingSteps[0].text;
            for (int i = _loadingSteps.Length - 1; i >= 0; i--)
            {
                if (progress >= _loadingSteps[i].pct)
                {
                    statusText = _loadingSteps[i].text;
                    break;
                }
            }
            _statusLabel.Text = statusText;

            // Loading complete
            if (_elapsed >= LoadDuration)
            {
                _phase = Phase.Waiting;
                _barFill.SetOffset(Side.Right, 2 + barMaxWidth);
                _statusLabel.Visible = false;
                _pressKeyLabel.Visible = true;
                _pulseTimer = 0f;
            }
        }
        else // Phase.Waiting
        {
            // Pulse the "PRESS ANY KEY" label
            _pulseTimer += dt;
            float alpha = 0.4f + 0.6f * (float)Math.Abs(Math.Sin(_pulseTimer * Math.PI / PulsePeriod));
            _pressKeyLabel.AddThemeColorOverride("font_color",
                new Color(ColAmber.R, ColAmber.G, ColAmber.B, alpha));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;

        // Only respond to key presses (not releases, not mouse)
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        // Escape always quits
        if (key.Keycode == Key.Escape)
        {
            GetTree().Quit();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_phase == Phase.Loading)
        {
            // During loading, any non-Escape key skips to the end
            _elapsed = LoadDuration;
            GetViewport().SetInputAsHandled();
        }
        else // Phase.Waiting
        {
            // Any key advances
            GetViewport().SetInputAsHandled();
            EmitSignal(SignalName.SplashFinished);
        }
    }
}
