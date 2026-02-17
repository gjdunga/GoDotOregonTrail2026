#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using Godot;
using OregonTrail2026.Systems;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Hidden developer console. No visible menu entry, no asset file
/// references, no UI hint anywhere in the game.
///
/// Activation: F12 during active gameplay (not from main menu or splash).
/// If already authenticated, opens the debug panel directly.
/// If not, shows a small password input. Correct password sets
/// SettingsManager.DebugUnlocked = true (persisted). Wrong password
/// dismisses silently with no feedback.
///
/// Password is verified via SHA256 hash comparison. The plaintext
/// password never appears in source.
/// </summary>
public partial class DevConsole : Control
{
    // SHA256 hash of the debug password. Change the hash to change the password.
    // To generate: echo -n "YourPassword" | sha256sum
    private const string PasswordHash = "e632b7095b0bf32c260fa4c539e9fd7b852d0de454e9be26f24d0d6f91d069a3";

    private LineEdit? _passwordInput;
    private bool _isAuthMode = false;

    [Signal]
    public delegate void ConsoleDismissedEventHandler();

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
    }

    /// <summary>
    /// Call from MainScene when F12 is pressed during gameplay.
    /// </summary>
    public void Activate()
    {
        if (SettingsManager.Instance.DebugUnlocked)
        {
            ShowDebugPanel();
        }
        else
        {
            ShowPasswordPrompt();
        }
        Visible = true;
    }

    private void ShowPasswordPrompt()
    {
        _isAuthMode = true;
        ClearChildren();

        // Dim overlay
        var overlay = UIKit.MakeDarkOverlay(0.5f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // Small input panel (no title, no hint about what this is)
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.05f, 0.95f),
            BorderColor = UIKit.ColGray,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 16, ContentMarginRight = 16,
            ContentMarginTop = 12, ContentMarginBottom = 12,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        panel.SetAnchor(Side.Left, 0.3f);
        panel.SetAnchor(Side.Right, 0.7f);
        panel.SetAnchor(Side.Top, 0.42f);
        panel.SetOffset(Side.Left, 0);
        panel.SetOffset(Side.Right, 0);
        panel.SetOffset(Side.Top, 0);
        panel.SetOffset(Side.Bottom, 48);
        AddChild(panel);

        _passwordInput = new LineEdit
        {
            PlaceholderText = ">",
            Secret = true,
            SecretCharacter = "*",
            CustomMinimumSize = new Vector2(200, 32),
        };
        _passwordInput.AddThemeFontOverride("font", UIKit.BodyFont);
        _passwordInput.AddThemeFontSizeOverride("font_size", 14);
        _passwordInput.AddThemeColorOverride("font_color", UIKit.ColGreen);
        _passwordInput.TextSubmitted += OnPasswordSubmitted;
        panel.AddChild(_passwordInput);

        // Focus the input
        CallDeferred(MethodName.GrabPasswordFocus);
    }

    private void GrabPasswordFocus()
    {
        _passwordInput?.GrabFocus();
    }

    private void OnPasswordSubmitted(string text)
    {
        string hash = ComputeSha256(text);
        if (hash == PasswordHash)
        {
            SettingsManager.Instance.DebugUnlocked = true;
            GD.Print("[DevConsole] Debug mode authorized.");
            ShowDebugPanel();
        }
        else
        {
            // Wrong password: dismiss silently
            Dismiss();
        }
    }

    private void ShowDebugPanel()
    {
        _isAuthMode = false;
        ClearChildren();

        // Dim overlay
        var overlay = UIKit.MakeDarkOverlay(0.5f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // Debug panel
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.05f, 0.95f),
            BorderColor = UIKit.ColGreen,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 20, ContentMarginRight = 20,
            ContentMarginTop = 16, ContentMarginBottom = 16,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        panel.SetAnchor(Side.Left, 0.15f);
        panel.SetAnchor(Side.Right, 0.85f);
        panel.SetAnchor(Side.Top, 0.1f);
        panel.SetAnchor(Side.Bottom, 0.9f);
        panel.SetOffset(Side.Left, 0);
        panel.SetOffset(Side.Right, 0);
        panel.SetOffset(Side.Top, 0);
        panel.SetOffset(Side.Bottom, 0);
        AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);

        var title = UIKit.MakeBodyLabel("DEVELOPER CONSOLE", 16, UIKit.ColGreen);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var gm = GameManager.Instance;
        if (gm?.State != null)
        {
            // Quick action buttons
            AddDebugButton(vbox, "Add $1000", () => { gm.State.Cash += 1000; });
            AddDebugButton(vbox, "Add 100 Food", () => { gm.State.Supplies["food"] += 100; });
            AddDebugButton(vbox, "Add 50 Bullets", () => { gm.State.Supplies["bullets"] += 50; });
            AddDebugButton(vbox, "Full Health All", () =>
            {
                foreach (var p in gm.State.Party)
                {
                    p.Health = 100;
                    p.Illness = "";
                    p.IllnessDays = 0;
                    p.Injury = "";
                    p.Unconscious = false;
                    p.Alive = true;
                }
            });
            AddDebugButton(vbox, "Skip 50 Miles", () => { gm.State.Miles += 50; });
            AddDebugButton(vbox, "Fix Wagon 100%", () => { gm.State.Wagon = 900; });
            AddDebugButton(vbox, "Add All Supplies", () =>
            {
                gm.State.Cash += 5000;
                gm.State.Supplies["food"] += 500;
                gm.State.Supplies["bullets"] += 200;
                gm.State.Supplies["clothes"] += 10;
                gm.State.Supplies["oxen"] += 4;
                gm.State.Supplies["wheel"] += 3;
                gm.State.Supplies["axle"] += 3;
                gm.State.Supplies["tongue"] += 3;
            });
        }
        else
        {
            var noGame = UIKit.MakeBodyLabel("No active game state.", 14, UIKit.ColGray);
            noGame.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(noGame);
        }

        // Close hint
        var closeHint = UIKit.MakeBodyLabel("F12 or ESC to close", 12, UIKit.ColGray);
        closeHint.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(UIKit.MakeSpacer(8));
        vbox.AddChild(closeHint);

        panel.AddChild(vbox);
    }

    private static void AddDebugButton(VBoxContainer parent, string text, Action action)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(200, 32),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
        };
        btn.AddThemeFontOverride("font", UIKit.BodyFont);
        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.Pressed += () =>
        {
            action();
            GD.Print($"[DevConsole] Executed: {text}");
        };
        parent.AddChild(btn);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

        if (key.Keycode == Key.Escape || key.Keycode == Key.F12)
        {
            Dismiss();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Dismiss()
    {
        ClearChildren();
        Visible = false;
        _isAuthMode = false;
        EmitSignal(SignalName.ConsoleDismissed);
    }

    private void ClearChildren()
    {
        foreach (var child in GetChildren())
            child.QueueFree();
        _passwordInput = null;
    }

    private static string ComputeSha256(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(64);
        foreach (byte b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
