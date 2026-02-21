#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Independence, Missouri starting screen. Handles the full town experience:
///   1. Welcome/story intro panel
///   2. Departure month picker
///   3. Matt's General Store (supply purchasing)
///   4. Depart button
///
/// Signals:
///   DepartureReady(monthOffset) - player clicked DEPART with supplies purchased.
///     monthOffset: 0=March, 1=April, 2=May, 3=June, 4=July
/// </summary>
public partial class IndependenceScreen : Control
{
    [Signal] public delegate void DepartureReadyEventHandler(int monthOffset);

    private GameState _state = null!;
    private Label _cashLabel = null!;
    private VBoxContainer _storePanel = null!;
    private int _selectedMonth = 1; // default April (index 1)

    // Store item quantities the player is buying
    private readonly Dictionary<string, int> _cart = new()
    {
        { "oxen", 0 }, { "food", 0 }, { "clothes", 0 },
        { "bullets", 0 }, { "wheel", 0 }, { "axle", 0 }, { "tongue", 0 },
    };

    // Prices at Matt's Outfitter (Independence)
    private static readonly Dictionary<string, float> BasePrices = new()
    {
        { "oxen", 40f },     // per yoke (2 oxen)
        { "food", 0.20f },   // per pound
        { "clothes", 10f },  // per set
        { "bullets", 2f },   // per box (20 rounds)
        { "wheel", 10f },    // spare wheel
        { "axle", 10f },     // spare axle
        { "tongue", 10f },   // spare tongue
    };

    // Step increments for each item type
    private static readonly Dictionary<string, int> StepSizes = new()
    {
        { "oxen", 1 }, { "food", 50 }, { "clothes", 1 },
        { "bullets", 1 }, { "wheel", 1 }, { "axle", 1 }, { "tongue", 1 },
    };

    // Recommended amounts for guidance text
    private static readonly Dictionary<string, string> Recommendations = new()
    {
        { "oxen", "You need at least 1 yoke. 3 is ideal." },
        { "food", "200 lbs per person for the journey. 1000 lbs total recommended." },
        { "clothes", "At least 2 sets per person. Cold mountains ahead." },
        { "bullets", "5+ boxes for hunting. More if you rely on game." },
        { "wheel", "1 spare minimum. Trails are rough." },
        { "axle", "1 spare recommended." },
        { "tongue", "1 spare recommended." },
    };

    private static readonly string[] MonthNames = { "MARCH", "APRIL", "MAY", "JUNE", "JULY" };
    private static readonly string[] MonthDescriptions =
    {
        "Early start. Cold and wet, but more time on the trail.",
        "Good balance of weather and timing. Most popular choice.",
        "Warm start, but you must keep pace to beat the snow.",
        "Late start. Risk of early snow in the mountains.",
        "Very late. Snow in the Blue Mountains is almost certain.",
    };

    public void Initialize(GameState state)
    {
        _state = state;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Background
        var bg = UIKit.MakeParchmentBackground();
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var overlay = UIKit.MakeDarkOverlay(0.25f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // Main scroll container
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        scroll.SetOffset(Side.Left, 40);
        scroll.SetOffset(Side.Right, -40);
        scroll.SetOffset(Side.Top, 20);
        scroll.SetOffset(Side.Bottom, -20);
        AddChild(scroll);

        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(root);

        // ---- TITLE ----
        var titleLabel = UIKit.MakeDisplayLabel("INDEPENDENCE, MISSOURI", 28);
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(titleLabel);

        var yearLabel = UIKit.MakeDisplayLabel("SPRING, 1850", 20);
        yearLabel.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(yearLabel);

        root.AddChild(UIKit.MakeSpacer(4));

        // ---- STORY INTRO ----
        string occName = GameData.GetOccupation(_state.Occupation)?.Name ?? _state.Occupation;
        string leaderName = _state.Party.Count > 0 ? _state.Party[0].Name : "You";

        string introText =
            $"{leaderName}, a {occName.ToLower()}, has gathered a party of five " +
            "for the 2,170 mile journey west to Oregon's Willamette Valley. " +
            "The route follows the Platte River across the Great Plains, through " +
            "South Pass in the Rocky Mountains, and down the Columbia River gorge " +
            "to Oregon City.\n\n" +
            "Before departing, you must outfit your wagon at Matt's General Store. " +
            "Choose your supplies wisely. Once you leave Independence, resupply " +
            "opportunities are scarce and prices only go up.";

        var introLabel = UIKit.MakeBodyLabel(introText, 15, UIKit.ColDarkBrown);
        introLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(introLabel);

        // ---- CASH DISPLAY ----
        _cashLabel = UIKit.MakeDisplayLabel($"TREASURY: ${_state.Cash:F2}", 22);
        _cashLabel.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(_cashLabel);

        root.AddChild(MakeSectionDivider());

        // ---- DEPARTURE MONTH ----
        var monthTitle = UIKit.MakeDisplayLabel("CHOOSE DEPARTURE MONTH", 20);
        monthTitle.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(monthTitle);

        var monthPanel = BuildMonthSelector();
        root.AddChild(monthPanel);

        root.AddChild(MakeSectionDivider());

        // ---- STORE ----
        var storeTitle = UIKit.MakeDisplayLabel("MATT'S GENERAL STORE", 22);
        storeTitle.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(storeTitle);

        var storeHint = UIKit.MakeBodyLabel(
            "Use + and \u2212 to adjust quantities. Your budget updates in real time.",
            13, UIKit.ColGray);
        storeHint.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(storeHint);

        _storePanel = new VBoxContainer();
        _storePanel.AddThemeConstantOverride("separation", 6);
        root.AddChild(_storePanel);

        BuildStoreRows();

        root.AddChild(MakeSectionDivider());

        // ---- DEPART BUTTON ----
        root.AddChild(UIKit.MakeSpacer(8));

        var departBtn = UIKit.MakeSecondaryButton("HEAD WEST", 22);
        departBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        departBtn.CustomMinimumSize = new Vector2(300, 60);
        departBtn.Pressed += OnDepartPressed;
        root.AddChild(departBtn);

        root.AddChild(UIKit.MakeSpacer(20));
    }

    // ========================================================================
    // MONTH SELECTOR
    // ========================================================================

    private Control BuildMonthSelector()
    {
        var container = new VBoxContainer();
        container.AddThemeConstantOverride("separation", 8);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;

        var descLabel = UIKit.MakeBodyLabel(MonthDescriptions[_selectedMonth], 14, UIKit.ColDarkBrown);
        descLabel.HorizontalAlignment = HorizontalAlignment.Center;
        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        for (int i = 0; i < MonthNames.Length; i++)
        {
            int monthIdx = i; // capture for closure
            var btn = UIKit.MakeSecondaryButton(MonthNames[i], 14);
            btn.CustomMinimumSize = new Vector2(100, 40);
            btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

            btn.Pressed += () =>
            {
                _selectedMonth = monthIdx;
                descLabel.Text = MonthDescriptions[monthIdx];

                // Update button visuals: highlight selected
                int childIdx = 0;
                foreach (var child in btnRow.GetChildren())
                {
                    if (child is Button b)
                    {
                        var col = childIdx == monthIdx ? UIKit.ColAmber : UIKit.ColDarkBrown;
                        b.AddThemeColorOverride("font_color", col);
                        childIdx++;
                    }
                }
            };

            // Highlight default selection
            if (i == _selectedMonth)
                btn.AddThemeColorOverride("font_color", UIKit.ColAmber);

            btnRow.AddChild(btn);
        }

        container.AddChild(btnRow);
        container.AddChild(descLabel);
        return container;
    }

    // ========================================================================
    // STORE
    // ========================================================================

    private void BuildStoreRows()
    {
        _storePanel.QueueFreeChildren();

        AddStoreRow("oxen",    "YOKE OF OXEN (2 oxen)");
        AddStoreRow("food",    "FOOD (pounds)");
        AddStoreRow("clothes", "SETS OF CLOTHING");
        AddStoreRow("bullets", "BOXES OF BULLETS (20 ea)");
        AddStoreRow("wheel",   "SPARE WHEELS");
        AddStoreRow("axle",    "SPARE AXLES");
        AddStoreRow("tongue",  "SPARE TONGUES");
    }

    private void AddStoreRow(string itemKey, string displayName)
    {
        float unitPrice = BasePrices[itemKey];
        int step = StepSizes[itemKey];
        string rec = Recommendations[itemKey];

        // Row container
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(UIKit.ColDarkBrown, 0.12f),
            ContentMarginLeft = 12, ContentMarginRight = 12,
            ContentMarginTop = 6, ContentMarginBottom = 6,
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 2);

        // Top row: name, price, quantity controls, line total
        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 12);

        // Item name
        var lblName = UIKit.MakeBodyLabel(displayName, 14, UIKit.ColDarkBrown);
        lblName.CustomMinimumSize = new Vector2(220, 0);
        topRow.AddChild(lblName);

        // Unit price
        string priceStr = itemKey == "food"
            ? $"${unitPrice:F2}/lb"
            : $"${unitPrice:F2} each";
        var lblPrice = UIKit.MakeBodyLabel(priceStr, 13, UIKit.ColGray);
        lblPrice.CustomMinimumSize = new Vector2(100, 0);
        topRow.AddChild(lblPrice);

        // Quantity display
        var lblQty = UIKit.MakeDisplayLabel(_cart[itemKey].ToString(), 16);
        lblQty.CustomMinimumSize = new Vector2(60, 0);
        lblQty.HorizontalAlignment = HorizontalAlignment.Center;

        // Line total
        float lineTotal = _cart[itemKey] * unitPrice;
        var lblTotal = UIKit.MakeBodyLabel($"${lineTotal:F2}", 14, UIKit.ColAmber);
        lblTotal.CustomMinimumSize = new Vector2(80, 0);
        lblTotal.HorizontalAlignment = HorizontalAlignment.Right;

        // Minus button
        var btnMinus = new Button { Text = "\u2212", CustomMinimumSize = new Vector2(36, 36) };
        StyleQuantityButton(btnMinus);
        btnMinus.Pressed += () =>
        {
            if (_cart[itemKey] >= step)
            {
                _cart[itemKey] -= step;
                lblQty.Text = _cart[itemKey].ToString();
                lblTotal.Text = $"${_cart[itemKey] * unitPrice:F2}";
                UpdateCashDisplay();
            }
        };

        // Plus button
        var btnPlus = new Button { Text = "+", CustomMinimumSize = new Vector2(36, 36) };
        StyleQuantityButton(btnPlus);
        btnPlus.Pressed += () =>
        {
            float cost = step * unitPrice;
            if (GetRemainingCash() >= cost)
            {
                _cart[itemKey] += step;
                lblQty.Text = _cart[itemKey].ToString();
                lblTotal.Text = $"${_cart[itemKey] * unitPrice:F2}";
                UpdateCashDisplay();
            }
        };

        topRow.AddChild(btnMinus);
        topRow.AddChild(lblQty);
        topRow.AddChild(btnPlus);
        topRow.AddChild(lblTotal);
        vbox.AddChild(topRow);

        // Recommendation hint
        var lblRec = UIKit.MakeBodyLabel(rec, 11, UIKit.ColGray);
        vbox.AddChild(lblRec);

        panel.AddChild(vbox);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _storePanel.AddChild(panel);
    }

    private static void StyleQuantityButton(Button btn)
    {
        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(UIKit.ColDarkBrown, 0.7f),
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            ContentMarginLeft = 4, ContentMarginRight = 4,
            ContentMarginTop = 2, ContentMarginBottom = 2,
        };
        var hoverStyle = new StyleBoxFlat
        {
            BgColor = new Color(UIKit.ColAmberDim, 0.8f),
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            ContentMarginLeft = 4, ContentMarginRight = 4,
            ContentMarginTop = 2, ContentMarginBottom = 2,
        };
        btn.AddThemeStyleboxOverride("normal", normalStyle);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        btn.AddThemeStyleboxOverride("pressed", hoverStyle);
        btn.AddThemeStyleboxOverride("focus", normalStyle);
        btn.AddThemeColorOverride("font_color", UIKit.ColParchment);
        btn.AddThemeColorOverride("font_hover_color", UIKit.ColWhite);
        btn.AddThemeFontSizeOverride("font_size", 18);
    }

    // ========================================================================
    // CASH TRACKING
    // ========================================================================

    private float GetCartTotal()
    {
        float total = 0;
        foreach (var (key, qty) in _cart)
            total += qty * BasePrices[key];
        return total;
    }

    private float GetRemainingCash() => _state.Cash - GetCartTotal();

    private void UpdateCashDisplay()
    {
        float remaining = GetRemainingCash();
        _cashLabel.Text = $"TREASURY: ${remaining:F2}";

        // Red if low
        if (remaining < 50)
            _cashLabel.AddThemeColorOverride("font_color", new Color("FF4444"));
        else
            _cashLabel.AddThemeColorOverride("font_color", UIKit.ColAmber);
    }

    // ========================================================================
    // DEPART
    // ========================================================================

    private void OnDepartPressed()
    {
        // Validate: must have at least 1 yoke of oxen
        if (_cart["oxen"] < 1)
        {
            ShowWarning("You need at least one yoke of oxen to pull the wagon!");
            return;
        }

        // Apply purchases to game state
        float spent = GetCartTotal();
        _state.Cash -= spent;

        _state.Supplies["oxen"] = _cart["oxen"];
        _state.Supplies["food"] = _cart["food"];
        _state.Supplies["clothes"] = _cart["clothes"];
        _state.Supplies["bullets"] = _cart["bullets"] * 20; // boxes to individual rounds
        _state.Supplies["wheel"] = _cart["wheel"];
        _state.Supplies["axle"] = _cart["axle"];
        _state.Supplies["tongue"] = _cart["tongue"];

        // Set departure day based on month
        // March=0, April=30, May=61, June=91, July=122
        int[] dayOffsets = { 0, 30, 61, 91, 122 };
        _state.Day = dayOffsets[_selectedMonth];

        GD.Print($"[Independence] Departing month={MonthNames[_selectedMonth]}, " +
                 $"day={_state.Day}, spent=${spent:F2}, remaining=${_state.Cash:F2}");
        GD.Print($"[Independence] Supplies: oxen={_state.Supplies["oxen"]}, " +
                 $"food={_state.Supplies["food"]}, clothes={_state.Supplies["clothes"]}, " +
                 $"bullets={_state.Supplies["bullets"]}, wheel={_state.Supplies["wheel"]}, " +
                 $"axle={_state.Supplies["axle"]}, tongue={_state.Supplies["tongue"]}");

        EmitSignal(SignalName.DepartureReady, _selectedMonth);
    }

    private void ShowWarning(string text)
    {
        // Simple overlay warning
        var popup = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = new Color(UIKit.ColDarkBrown, 0.95f),
            BorderColor = new Color("CC3333"),
            BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderWidthTop = 2, BorderWidthBottom = 2,
            ContentMarginLeft = 24, ContentMarginRight = 24,
            ContentMarginTop = 16, ContentMarginBottom = 16,
        };
        popup.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);

        var lbl = UIKit.MakeBodyLabel(text, 16, new Color("FF6666"));
        lbl.HorizontalAlignment = HorizontalAlignment.Center;
        lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(lbl);

        var btnOk = UIKit.MakeSecondaryButton("OK", 16);
        btnOk.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        btnOk.Pressed += () => popup.QueueFree();
        vbox.AddChild(btnOk);

        popup.AddChild(vbox);
        popup.SetAnchor(Side.Left, 0.25f);
        popup.SetAnchor(Side.Right, 0.75f);
        popup.SetAnchor(Side.Top, 0.35f);
        popup.SetAnchor(Side.Bottom, 0.65f);
        AddChild(popup);
    }

    // ========================================================================
    // HELPERS
    // ========================================================================

    private static ColorRect MakeSectionDivider()
    {
        var div = new ColorRect
        {
            Color = new Color(UIKit.ColAmberDim, 0.4f),
            CustomMinimumSize = new Vector2(0, 2),
        };
        return div;
    }
}
