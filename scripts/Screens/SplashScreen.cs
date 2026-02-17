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

        // Bar fill (warm goldenrod, rendered UNDER the frame so borders overlay it)
        _barFill = new ColorRect { Color = new Color("B8860B") };
        _barFill.SetAnchor(Side.Left, 0.2f);
        _barFill.SetAnchor(Side.Right, 0.8f);
        _barFill.SetAnchor(Side.Top, 0.74f);
        _barFill.SetAnchor(Side.Bottom, 0.74f);
        _barFill.SetOffset(Side.Left, 6);
        _barFill.SetOffset(Side.Top, 6);
        _barFill.SetOffset(Side.Right, -6);
        _barFill.SetOffset(Side.Bottom, 40);
        AddChild(_barFill);

        // Loading bar frame (themed panel 9-slice, on top of the fill)
        _barFrame = UIKit.MakePanel();
        _barFrame.SetAnchor(Side.Left, 0.2f);
        _barFrame.SetAnchor(Side.Right, 0.8f);
        _barFrame.SetAnchor(Side.Top, 0.74f);
        _barFrame.SetAnchor(Side.Bottom, 0.74f);
        _barFrame.SetOffset(Side.Left, 0);
        _barFrame.SetOffset(Side.Right, 0);
        _barFrame.SetOffset(Side.Top, 0);
        _barFrame.SetOffset(Side.Bottom, 46);
        AddChild(_barFrame);

        // Status text (below bar, off-white, display font for western consistency)
        _statusLabel = UIKit.MakeDisplayLabel(Tr(_loadingSteps[0].key), 20, UIKit.ColWhite);
        _statusLabel.SetAnchor(Side.Left, 0.15f);
        _statusLabel.SetAnchor(Side.Right, 0.85f);
        _statusLabel.SetAnchor(Side.Top, 0.74f);
        _statusLabel.SetAnchor(Side.Bottom, 0.74f);
        _statusLabel.SetOffset(Side.Left, 0);
        _statusLabel.SetOffset(Side.Right, 0);
        _statusLabel.SetOffset(Side.Top, 72);
        _statusLabel.SetOffset(Side.Bottom, 98);
        AddChild(_statusLabel);

        // "PRESS ANY KEY" (hidden until loading completes)
        _pressKeyLabel = UIKit.MakeDisplayLabel(Tr(TK.SplashPressKey), 22, UIKit.ColAmber);
        _pressKeyLabel.SetAnchor(Side.Left, 0.15f);
        _pressKeyLabel.SetAnchor(Side.Right, 0.85f);
        _pressKeyLabel.SetAnchor(Side.Top, 0.86f);
        _pressKeyLabel.SetAnchor(Side.Bottom, 0.86f);
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

            // Progress bar fill (fill is a sibling with same anchors as frame)
            float progress = Math.Clamp(_elapsed / LoadDuration, 0f, 1f);
            float frameWidth = _barFrame.Size.X;
            // At 0%: right offset collapses to left edge. At 100%: right offset = -6 (full width)
            float rightOffset = -frameWidth + 6 + progress * (frameWidth - 12);
            _barFill.SetOffset(Side.Right, rightOffset);

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
                _barFill.SetOffset(Side.Right, -6);
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

        // Handle keyboard
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Escape)
            {
                GetTree().Quit();
                GetViewport().SetInputAsHandled();
                return;
            }
            HandleAdvance();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Handle mouse click
        if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
        {
            HandleAdvance();
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleAdvance()
    {
        if (_phase == Phase.Loading)
        {
            _elapsed = LoadDuration;
        }
        else
        {
            EmitSignal(SignalName.SplashFinished);
        }
    }
}
