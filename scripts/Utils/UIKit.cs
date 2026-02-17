#nullable enable
using System;
using Godot;

namespace OregonTrail2026.Utils;

/// <summary>
/// Centralized UI factory. Loads all themed assets once, provides factory
/// methods for panels, buttons, labels, and progress bars so every screen
/// shares a consistent visual language using the assets in assets/images/ui/.
///
/// Usage:
///   var panel = UIKit.MakePanel();
///   var btn   = UIKit.MakePrimaryButton("NEW GAME");
///   var label = UIKit.MakeDisplayLabel("OREGON TRAIL", 28);
///   var body  = UIKit.MakeBodyLabel("Choose your occupation.");
/// </summary>
public static class UIKit
{
    // ========================================================================
    // PALETTE (1850s parchment aesthetic)
    // ========================================================================
    public static readonly Color ColAmber       = new("FFD700");
    public static readonly Color ColAmberDim    = new("8B7332");
    public static readonly Color ColParchment   = new("F0E6D2");
    public static readonly Color ColDarkBrown   = new("1A1408");
    public static readonly Color ColBlack       = new("0A0804");
    public static readonly Color ColGray        = new("666655");
    public static readonly Color ColRed         = new("CC3333");
    public static readonly Color ColGreen       = new("5A8A3C");
    public static readonly Color ColWhite       = new("F5F0E8");

    // ========================================================================
    // FONT PATHS
    // ========================================================================
    private const string FontPathBody    = "res://assets/fonts/NotoSans-Variable.ttf";
    private const string FontPathDisplay = "res://assets/fonts/Federant-Regular.ttf";

    // ========================================================================
    // TEXTURE PATHS
    // ========================================================================
    private const string TexPanelFrame       = "res://assets/images/ui/frame_panel_9slice.webp";
    private const string TexEventFrame       = "res://assets/images/ui/frame_event_9slice.webp";
    private const string TexTooltipFrame     = "res://assets/images/ui/frame_tooltip_9slice.webp";
    private const string TexPortraitFrame    = "res://assets/images/ui/frame_portrait_9slice.webp";
    private const string TexParchmentBg      = "res://assets/images/ui/ui_parchment_bg.webp";
    private const string TexDivider          = "res://assets/images/ui/divider_rule.webp";
    private const string TexBtnPriIdle       = "res://assets/images/ui/btn_primary_idle.png";
    private const string TexBtnPriHover      = "res://assets/images/ui/btn_primary_hover.png";
    private const string TexBtnPriPressed    = "res://assets/images/ui/btn_primary_pressed.png";
    private const string TexBtnPriDisabled   = "res://assets/images/ui/btn_primary_disabled.png";
    private const string TexBtnSecIdle       = "res://assets/images/ui/btn_secondary_idle.png";
    private const string TexBtnSecHover      = "res://assets/images/ui/btn_secondary_hover.png";
    private const string TexBtnSecPressed    = "res://assets/images/ui/btn_secondary_pressed.png";
    private const string TexBtnSecDisabled   = "res://assets/images/ui/btn_secondary_disabled.png";
    private const string TexBarHealthEmpty   = "res://assets/images/ui/bar_health_empty.webp";
    private const string TexBarHealthFill    = "res://assets/images/ui/bar_health_fill.webp";
    private const string TexBarWagonEmpty    = "res://assets/images/ui/bar_wagon_empty.webp";
    private const string TexBarWagonFill     = "res://assets/images/ui/bar_wagon_fill.webp";
    private const string TexBadgeDay         = "res://assets/images/ui/badge_day.webp";
    private const string TexBadgeMiles       = "res://assets/images/ui/badge_miles.webp";
    private const string TexBadgeNote        = "res://assets/images/ui/badge_note.webp";

    // ========================================================================
    // CACHED RESOURCES (loaded on first access)
    // ========================================================================
    private static FontFile? _bodyFont;
    private static FontFile? _displayFont;

    public static FontFile BodyFont =>
        _bodyFont ??= GD.Load<FontFile>(FontPathBody);

    public static FontFile DisplayFont =>
        _displayFont ??= GD.Load<FontFile>(FontPathDisplay);

    // ========================================================================
    // LABELS
    // ========================================================================

    /// <summary>
    /// Display/heading label using Federant. For titles, menu headings, etc.
    /// Falls back to Noto Sans for characters Federant can't render.
    /// </summary>
    public static Label MakeDisplayLabel(string text, int fontSize = 24, Color? color = null)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontOverride("font", DisplayFont);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color ?? ColAmber);
        return label;
    }

    /// <summary>
    /// Body text label using Noto Sans. For descriptions, dialog, tooltips.
    /// Supports all scripts/languages.
    /// </summary>
    public static Label MakeBodyLabel(string text, int fontSize = 16, Color? color = null)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontOverride("font", BodyFont);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color ?? ColParchment);
        return label;
    }

    // ========================================================================
    // PANELS (NinePatchRect from 9-slice assets)
    // ========================================================================

    /// <summary>Standard panel frame (parchment border with dark interior).</summary>
    public static NinePatchRect MakePanel()
    {
        var tex = GD.Load<Texture2D>(TexPanelFrame);
        return new NinePatchRect
        {
            Texture = tex,
            // 9-slice margins (pixels from each edge that don't stretch)
            PatchMarginLeft   = 40,
            PatchMarginRight  = 40,
            PatchMarginTop    = 35,
            PatchMarginBottom = 35,
            AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Tile,
            AxisStretchVertical   = NinePatchRect.AxisStretchMode.Tile,
        };
    }

    /// <summary>Event popup frame (wider, ornate corners).</summary>
    public static NinePatchRect MakeEventPanel()
    {
        var tex = GD.Load<Texture2D>(TexEventFrame);
        return new NinePatchRect
        {
            Texture = tex,
            PatchMarginLeft   = 50,
            PatchMarginRight  = 50,
            PatchMarginTop    = 40,
            PatchMarginBottom = 50,
            AxisStretchHorizontal = NinePatchRect.AxisStretchMode.Tile,
            AxisStretchVertical   = NinePatchRect.AxisStretchMode.Tile,
        };
    }

    /// <summary>Tooltip frame (small, subtle border).</summary>
    public static NinePatchRect MakeTooltipPanel()
    {
        var tex = GD.Load<Texture2D>(TexTooltipFrame);
        return new NinePatchRect
        {
            Texture = tex,
            PatchMarginLeft   = 20,
            PatchMarginRight  = 20,
            PatchMarginTop    = 20,
            PatchMarginBottom = 20,
        };
    }

    /// <summary>Parchment background (tileable, for full-screen use).</summary>
    public static TextureRect MakeParchmentBackground()
    {
        var tex = GD.Load<Texture2D>(TexParchmentBg);
        return new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Tile,
        };
    }

    /// <summary>Ornamental divider rule.</summary>
    public static TextureRect MakeDivider()
    {
        var tex = GD.Load<Texture2D>(TexDivider);
        return new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
    }

    // ========================================================================
    // BUTTONS
    // ========================================================================

    /// <summary>
    /// Primary themed button using 9-slice textures for all 4 states.
    /// Text is rendered with Federant display font.
    /// </summary>
    public static Button MakePrimaryButton(string text, int fontSize = 20)
    {
        var btn = MakeThemedButton(
            text, fontSize,
            TexBtnPriIdle, TexBtnPriHover, TexBtnPriPressed, TexBtnPriDisabled);
        return btn;
    }

    /// <summary>Secondary themed button (subtler styling).</summary>
    public static Button MakeSecondaryButton(string text, int fontSize = 18)
    {
        return MakeThemedButton(
            text, fontSize,
            TexBtnSecIdle, TexBtnSecHover, TexBtnSecPressed, TexBtnSecDisabled);
    }

    private static Button MakeThemedButton(
        string text, int fontSize,
        string idlePath, string hoverPath, string pressedPath, string disabledPath)
    {
        var btn = new Button
        {
            Text = text,
            ClipText = true,
            CustomMinimumSize = new Vector2(280, 52),
        };

        // Load textures
        var idle     = GD.Load<Texture2D>(idlePath);
        var hover    = GD.Load<Texture2D>(hoverPath);
        var pressed  = GD.Load<Texture2D>(pressedPath);
        var disabled = GD.Load<Texture2D>(disabledPath);

        // Build StyleBoxTexture for each state
        btn.AddThemeStyleboxOverride("normal",  MakeButtonStyleBox(idle));
        btn.AddThemeStyleboxOverride("hover",   MakeButtonStyleBox(hover));
        btn.AddThemeStyleboxOverride("pressed", MakeButtonStyleBox(pressed));
        btn.AddThemeStyleboxOverride("disabled", MakeButtonStyleBox(disabled));
        btn.AddThemeStyleboxOverride("focus",   MakeButtonStyleBox(hover)); // focus = hover look

        // Font
        btn.AddThemeFontOverride("font", DisplayFont);
        btn.AddThemeFontSizeOverride("font_size", fontSize);
        btn.AddThemeColorOverride("font_color", ColDarkBrown);
        btn.AddThemeColorOverride("font_hover_color", ColBlack);
        btn.AddThemeColorOverride("font_pressed_color", ColAmberDim);
        btn.AddThemeColorOverride("font_disabled_color", ColGray);

        return btn;
    }

    private static StyleBoxTexture MakeButtonStyleBox(Texture2D texture)
    {
        return new StyleBoxTexture
        {
            Texture = texture,
            // 9-slice margins for button textures (624x120, cropped)
            TextureMarginLeft   = 18,
            TextureMarginRight  = 28,
            TextureMarginTop    = 28,
            TextureMarginBottom = 18,
            // Content margins (padding inside the button for text)
            ContentMarginLeft   = 20,
            ContentMarginRight  = 20,
            ContentMarginTop    = 8,
            ContentMarginBottom = 8,
        };
    }

    // ========================================================================
    // BADGES
    // ========================================================================

    public static TextureRect MakeBadge(string type)
    {
        string path = type switch
        {
            "day"   => TexBadgeDay,
            "miles" => TexBadgeMiles,
            "note"  => TexBadgeNote,
            _ => TexBadgeNote,
        };
        return new TextureRect
        {
            Texture = GD.Load<Texture2D>(path),
            ExpandMode = TextureRect.ExpandModeEnum.KeepSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
    }

    // ========================================================================
    // UTILITY
    // ========================================================================

    /// <summary>Dark semi-transparent overlay for dimming backgrounds.</summary>
    public static ColorRect MakeDarkOverlay(float opacity = 0.65f)
    {
        return new ColorRect
        {
            Color = new Color(0, 0, 0, opacity),
        };
    }

    /// <summary>Simple spacer for VBox/HBox layouts.</summary>
    public static Control MakeSpacer(float height = 16)
    {
        return new Control { CustomMinimumSize = new Vector2(0, height) };
    }

    /// <summary>Horizontal spacer.</summary>
    public static Control MakeHSpacer(float width = 16)
    {
        return new Control { CustomMinimumSize = new Vector2(width, 0) };
    }
}
