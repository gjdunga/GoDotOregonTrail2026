#nullable enable
using System;
using System.Linq;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Systems;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Trail map overlay. Displays map_main.webp (1536x1024 native) with:
///   - Visited landmark pins at their GameData.MapPos anchor positions
///   - Unvisited landmarks shown as dimmed pins
///   - Wagon icon at the player's current miles, interpolated between
///     the nearest pair of landmarks on the route
///   - Status strip: date, miles, terrain, weather
///   - CLOSE button returns to choice menu
///
/// Pin/wagon positions are computed as fractions of the 1536x1024 map
/// image and applied as anchors on the AspectRatioContainer that holds
/// the map, so they track the image regardless of viewport size.
///
/// Signals:
///   MapClosed - emitted when player closes the map.
/// </summary>
public partial class MapScreen : Control
{
    [Signal] public delegate void MapClosedEventHandler();

    private const float MapNativeW = 1536f;
    private const float MapNativeH = 1024f;

    private GameState _state = null!;

    public void Initialize(GameState state)
    {
        _state = state;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Semi-transparent dark backdrop
        var backdrop = UIKit.MakeDarkOverlay(0.80f);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        // Outer VBox: title bar, map area, status strip, close btn
        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 0);
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.SetOffset(Side.Left,   24);
        outer.SetOffset(Side.Right, -24);
        outer.SetOffset(Side.Top,    16);
        outer.SetOffset(Side.Bottom,-16);
        AddChild(outer);

        // Title
        var title = UIKit.MakeDisplayLabel("THE OREGON TRAIL", 22);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        outer.AddChild(title);

        outer.AddChild(UIKit.MakeSpacer(6));

        // Map container: AspectRatioContainer preserves 3:2 ratio
        var arc = new AspectRatioContainer
        {
            Ratio = MapNativeW / MapNativeH,
            StretchMode = AspectRatioContainer.StretchModeEnum.HeightControlsWidth,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical   = SizeFlags.ExpandFill,
        };
        outer.AddChild(arc);

        // Inner Control: map image + pins positioned by anchor
        var mapRoot = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical   = SizeFlags.ExpandFill,
        };
        arc.AddChild(mapRoot);

        // Map image
        var mapTex = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/images/map/map_main.webp"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        mapTex.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        mapRoot.AddChild(mapTex);

        // Visited set
        var visited  = new System.Collections.Generic.HashSet<string>(_state.VisitedLandmarks);
        var crossed  = new System.Collections.Generic.HashSet<string>(_state.CrossedRivers);

        // Landmark pins
        foreach (var lm in GameData.Landmarks)
        {
            bool seen = visited.Contains(lm.Name);
            AddPin(mapRoot, lm.Pin, lm.MapPos.x, lm.MapPos.y, seen, lm.Name, lm.Miles);
        }

        // Wagon marker at interpolated position
        (float wagonX, float wagonY) = WagonMapPos(_state.Miles);
        AddWagon(mapRoot, wagonX, wagonY);

        // Status strip
        outer.AddChild(UIKit.MakeSpacer(6));
        outer.AddChild(BuildStatusStrip());
        outer.AddChild(UIKit.MakeSpacer(8));

        // Close button
        var closeRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        var closeBtn = UIKit.MakePrimaryButton("CLOSE MAP", 18);
        closeBtn.CustomMinimumSize = new Vector2(200, 48);
        closeBtn.Pressed += () => EmitSignal(SignalName.MapClosed);
        closeRow.AddChild(closeBtn);
        outer.AddChild(closeRow);
    }

    // =========================================================================
    // PIN PLACEMENT
    // =========================================================================

    private static void AddPin(
        Control mapRoot, string texPath, int px, int py, bool visited,
        string name, int miles)
    {
        float ax = px / MapNativeW;
        float ay = py / MapNativeH;

        var pin = new TextureRect
        {
            Texture = GD.Load<Texture2D>(texPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(28, 28),
            // Dim unvisited pins
            Modulate = visited ? Colors.White : new Color(1, 1, 1, 0.45f),
        };

        // Center pin on the anchor point
        pin.SetAnchor(Side.Left,   ax);
        pin.SetAnchor(Side.Right,  ax);
        pin.SetAnchor(Side.Top,    ay);
        pin.SetAnchor(Side.Bottom, ay);
        pin.SetOffset(Side.Left,   -14);
        pin.SetOffset(Side.Right,   14);
        pin.SetOffset(Side.Top,    -28); // pin tip points down
        pin.SetOffset(Side.Bottom,   0);
        mapRoot.AddChild(pin);

        // Name tooltip label (shown below pin)
        var lbl = new Label
        {
            Text = visited ? $"{name}\n{miles}mi" : "???",
            AutowrapMode = TextServer.AutowrapMode.Disabled,
        };
        lbl.AddThemeFontOverride("font", UIKit.BodyFont);
        lbl.AddThemeFontSizeOverride("font_size", 9);
        lbl.AddThemeColorOverride("font_color",
            visited ? new Color("FFD700") : new Color("888888"));
        lbl.SetAnchor(Side.Left,   ax);
        lbl.SetAnchor(Side.Right,  ax);
        lbl.SetAnchor(Side.Top,    ay);
        lbl.SetAnchor(Side.Bottom, ay);
        lbl.SetOffset(Side.Left,   -30);
        lbl.SetOffset(Side.Right,   30);
        lbl.SetOffset(Side.Top,      2);
        lbl.SetOffset(Side.Bottom,  28);
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        mapRoot.AddChild(lbl);
    }

    private static void AddWagon(Control mapRoot, float ax, float ay)
    {
        var wagon = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/images/map/wagon_icon.webp"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(32, 32),
        };
        wagon.SetAnchor(Side.Left,   ax);
        wagon.SetAnchor(Side.Right,  ax);
        wagon.SetAnchor(Side.Top,    ay);
        wagon.SetAnchor(Side.Bottom, ay);
        wagon.SetOffset(Side.Left,   -16);
        wagon.SetOffset(Side.Right,   16);
        wagon.SetOffset(Side.Top,    -16);
        wagon.SetOffset(Side.Bottom,  16);
        mapRoot.AddChild(wagon);
    }

    // =========================================================================
    // WAGON POSITION INTERPOLATION
    // =========================================================================

    /// <summary>
    /// Linearly interpolates wagon MapPos between the two landmarks
    /// nearest to currentMiles (one before, one after).
    /// Returns (anchorX, anchorY) as fractions of map image dimensions.
    /// </summary>
    private static (float ax, float ay) WagonMapPos(int currentMiles)
    {
        var lms = GameData.Landmarks.OrderBy(l => l.Miles).ToArray();

        // Before first landmark
        if (currentMiles <= lms[0].Miles)
            return (lms[0].MapPos.x / MapNativeW, lms[0].MapPos.y / MapNativeH);

        // After last landmark
        if (currentMiles >= lms[^1].Miles)
            return (lms[^1].MapPos.x / MapNativeW, lms[^1].MapPos.y / MapNativeH);

        // Find bracketing pair
        for (int i = 0; i < lms.Length - 1; i++)
        {
            if (currentMiles >= lms[i].Miles && currentMiles < lms[i + 1].Miles)
            {
                float t = (float)(currentMiles - lms[i].Miles)
                        / (lms[i + 1].Miles - lms[i].Miles);

                float x = Mathf.Lerp(lms[i].MapPos.x, lms[i + 1].MapPos.x, t);
                float y = Mathf.Lerp(lms[i].MapPos.y, lms[i + 1].MapPos.y, t);
                return (x / MapNativeW, y / MapNativeH);
            }
        }

        return (lms[^1].MapPos.x / MapNativeW, lms[^1].MapPos.y / MapNativeH);
    }

    // =========================================================================
    // STATUS STRIP
    // =========================================================================

    private Control BuildStatusStrip()
    {
        var row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 28);

        string dateStr = DateCalc.DateStr(_state.Day);
        string terrain = TravelSystem.TerrainByMiles(_state.Miles)
                             .Replace("_", " ").ToUpper();
        int pct = (int)((float)_state.Miles / GameConstants.TargetMiles * 100f);

        AddStat(row, "DATE",    dateStr);
        AddStat(row, "MILES",   $"{_state.Miles} / {GameConstants.TargetMiles}  ({pct}%)");
        AddStat(row, "TERRAIN", terrain);
        AddStat(row, "WEATHER", _state.Weather.ToUpper());
        AddStat(row, "CASH",    $"${_state.Cash:F0}");

        return row;
    }

    private static void AddStat(HBoxContainer row, string labelText, string valueText)
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);

        var lbl = UIKit.MakeBodyLabel(labelText, 11, UIKit.ColAmberDim);
        lbl.HorizontalAlignment = HorizontalAlignment.Center;

        var val = UIKit.MakeBodyLabel(valueText, 13, UIKit.ColParchment);
        val.HorizontalAlignment = HorizontalAlignment.Center;

        col.AddChild(lbl);
        col.AddChild(val);
        row.AddChild(col);
    }
}
