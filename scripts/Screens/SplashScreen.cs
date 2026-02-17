#nullable enable
using System;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Splash/loading screen shown at game launch.
///
/// Phases:
///   1. LOADING  - Logo scales to fit window, progress bar fills over ~10s.
///                 Any key skips to end. Escape quits.
///   2. WAITING  - Pulsing "PRESS ANY KEY TO CONTINUE".
///                 Any key advances. Escape quits.
///
/// All strings use Tr() for i18n. All UI uses UIKit for themed consistency.
/// </summary>
public partial class SplashScreen : Control
{
    [Signal]
    public delegate void SplashFinishedEventHandler();

    // ---- Timing ----
    private const float LogoFadeDuration = 1.5f;
    private const float LoadDuration = 10.0f;
    private const float PulsePeriod = 1.8f;

    // ---- State ----
    private enum Phase { Loading, Waiting }
    private Phase _phase = Phase.Loading;
    private float _elapsed = 0f;
    private float _pulseTimer = 0f;

    // ---- Loading step keys (progress threshold, translation key) ----
    private static readonly (float pct, string key)[] _loadingSteps =
    {
        (0.00f, TK.SplashLoading1),
        (0.08f, TK.SplashLoading2),
        (0.18f, TK.SplashLoading3),
        (0.30f, TK.SplashLoading4),
        (0.42f, TK.SplashLoading5),
        (0.55f, TK.SplashLoading6),
        (0.65f, TK.SplashLoading7),
        (0.78f, TK.SplashLoading8),
        (0.88f, TK.SplashLoading9),
        (0.96f, TK.SplashLoading10),
    };

    // ---- Nodes ----
    private TextureRect _logo = null!;
    private NinePatchRect _barFrame = null!;
    private ColorRect _barFill = null!;
    private Label _statusLabel = null!;
    private Label _pressKeyLabel = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Black background
        var bg = new ColorRect { Color = UIKit.ColBlack };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // Logo (centered, fits window, fades in)
        _logo = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/images/ui/logo_oregon_trail_2026.webp"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = new Color(1, 1, 1, 0),
        };
        _logo.SetAnchor(Side.Left, 0.1f);
        _logo.SetAnchor(Side.Right, 0.9f);
        _logo.SetAnchor(Side.Top, 0.03f);
        _logo.SetAnchor(Side.Bottom, 0.65f);
        _logo.SetOffset(Side.Left, 0);
        _logo.SetOffset(Side.Right, 0);
        _logo.SetOffset(Side.Top, 0);
        _logo.SetOffset(Side.Bottom, 0);
        AddChild(_logo);

        // Loading bar frame (themed panel 9-slice)
        _barFrame = UIKit.MakePanel();
        _barFrame.SetAnchor(Side.Left, 0.15f);
        _barFrame.SetAnchor(Side.Right, 0.85f);
        _barFrame.SetAnchor(Side.Top, 0.70f);
        _barFrame.SetOffset(Side.Left, 0);
        _barFrame.SetOffset(Side.Right, 0);
        _barFrame.SetOffset(Side.Top, 0);
        _barFrame.SetOffset(Side.Bottom, 36);
        AddChild(_barFrame);

        // Bar fill (amber, inside the frame, padded)
        _barFill = new ColorRect { Color = UIKit.ColAmber };
        _barFill.SetAnchor(Side.Left, 0);
        _barFill.SetAnchor(Side.Top, 0);
        _barFill.SetAnchor(Side.Bottom, 1.0f);
        _barFill.SetOffset(Side.Left, 42);
        _barFill.SetOffset(Side.Top, 8);
        _barFill.SetOffset(Side.Right, 42);
        _barFill.SetOffset(Side.Bottom, -8);
        _barFrame.AddChild(_barFill);

        // Status text (below bar)
        _statusLabel = UIKit.MakeBodyLabel(Tr(_loadingSteps[0].key), 14, UIKit.ColAmberDim);
        _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _statusLabel.SetAnchor(Side.Left, 0.15f);
        _statusLabel.SetAnchor(Side.Right, 0.85f);
        _statusLabel.SetAnchor(Side.Top, 0.70f);
        _statusLabel.SetOffset(Side.Left, 0);
        _statusLabel.SetOffset(Side.Right, 0);
        _statusLabel.SetOffset(Side.Top, 42);
        _statusLabel.SetOffset(Side.Bottom, 62);
        AddChild(_statusLabel);

        // "PRESS ANY KEY" (hidden until loading completes)
        _pressKeyLabel = UIKit.MakeDisplayLabel(Tr(TK.SplashPressKey), 22, UIKit.ColAmber);
        _pressKeyLabel.SetAnchor(Side.Left, 0.15f);
        _pressKeyLabel.SetAnchor(Side.Right, 0.85f);
        _pressKeyLabel.SetAnchor(Side.Top, 0.83f);
        _pressKeyLabel.SetOffset(Side.Left, 0);
        _pressKeyLabel.SetOffset(Side.Right, 0);
        _pressKeyLabel.SetOffset(Side.Top, 0);
        _pressKeyLabel.SetOffset(Side.Bottom, 30);
        _pressKeyLabel.Visible = false;
        AddChild(_pressKeyLabel);

        // Version label (bottom-right)
        var versionLabel = UIKit.MakeBodyLabel($"v{GameConstants.GameVersion}", 13, UIKit.ColGray);
        versionLabel.HorizontalAlignment = HorizontalAlignment.Right;
        versionLabel.SetAnchor(Side.Left, 0.7f);
        versionLabel.SetAnchor(Side.Right, 1.0f);
        versionLabel.SetAnchor(Side.Top, 1.0f);
        versionLabel.SetAnchor(Side.Bottom, 1.0f);
        versionLabel.SetOffset(Side.Left, 0);
        versionLabel.SetOffset(Side.Right, -12);
        versionLabel.SetOffset(Side.Top, -28);
        versionLabel.SetOffset(Side.Bottom, -6);
        AddChild(versionLabel);

        // ESC hint (bottom-left)
        var escLabel = UIKit.MakeBodyLabel(Tr(TK.SplashEscQuit), 11, UIKit.ColGray);
        escLabel.SetAnchor(Side.Left, 0.0f);
        escLabel.SetAnchor(Side.Right, 0.3f);
        escLabel.SetAnchor(Side.Top, 1.0f);
        escLabel.SetAnchor(Side.Bottom, 1.0f);
        escLabel.SetOffset(Side.Left, 12);
        escLabel.SetOffset(Side.Right, 0);
        escLabel.SetOffset(Side.Top, -28);
        escLabel.SetOffset(Side.Bottom, -6);
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

            // Progress bar fill
            float progress = Math.Clamp(_elapsed / LoadDuration, 0f, 1f);
            float frameWidth = _barFrame.Size.X;
            float maxFillWidth = frameWidth - 84; // 42px padding each side
            _barFill.SetOffset(Side.Right, 42 + maxFillWidth * progress);

            // Status text
            string statusKey = _loadingSteps[0].key;
            for (int i = _loadingSteps.Length - 1; i >= 0; i--)
            {
                if (progress >= _loadingSteps[i].pct)
                {
                    statusKey = _loadingSteps[i].key;
                    break;
                }
            }
            _statusLabel.Text = Tr(statusKey);

            // Complete
            if (_elapsed >= LoadDuration)
            {
                _phase = Phase.Waiting;
                _barFill.SetOffset(Side.Right, 42 + maxFillWidth);
                _statusLabel.Visible = false;
                _pressKeyLabel.Visible = true;
                _pulseTimer = 0f;
            }
        }
        else
        {
            _pulseTimer += dt;
            float alpha = 0.4f + 0.6f * (float)Math.Abs(Math.Sin(_pulseTimer * Math.PI / PulsePeriod));
            _pressKeyLabel.AddThemeColorOverride("font_color",
                new Color(UIKit.ColAmber.R, UIKit.ColAmber.G, UIKit.ColAmber.B, alpha));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        if (key.Keycode == Key.Escape)
        {
            GetTree().Quit();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_phase == Phase.Loading)
        {
            _elapsed = LoadDuration;
            GetViewport().SetInputAsHandled();
        }
        else
        {
            GetViewport().SetInputAsHandled();
            EmitSignal(SignalName.SplashFinished);
        }
    }
}
