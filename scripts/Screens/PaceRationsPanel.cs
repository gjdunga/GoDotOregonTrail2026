#nullable enable
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Modal panel for setting pace and rations. Opens from the PACE / RATIONS
/// choice button. No travel day consumed.
///
/// PACE options (writes to GameState.Pace):
///   REST     - Miles = 0. Party recovers health. Wagon does not wear.
///              Stranded check only fires when oxen/wagon are the cause,
///              not when resting intentionally.
///   STEADY   - 8-17 miles/day. Normal wear.
///   GRUELING - 12-22 miles/day. 2.5x wagon wear, 2x oxen wear.
///              Higher breakdown chance.
///
/// RATIONS options (writes to GameState.Rations):
///   BARE BONES - 2 lbs/person/day. Health recovery is halved.
///   MEAGER     - 3 lbs/person/day. Recovery reduced 30%.
///   FILLING    - 5 lbs/person/day. Full health recovery.
///
/// Current selection is highlighted in each group. Selecting a button
/// writes the value immediately. CONFIRM closes the panel.
///
/// Signals:
///   Confirmed - player closed the panel.
/// </summary>
public partial class PaceRationsPanel : Control
{
    [Signal] public delegate void ConfirmedEventHandler();

    private GameState _state = null!;

    // Pace button group
    private Button _btnRest    = null!;
    private Button _btnSteady  = null!;
    private Button _btnGrueling = null!;

    // Rations button group
    private Button _btnBare    = null!;
    private Button _btnMeager  = null!;
    private Button _btnFilling = null!;

    public void Initialize(GameState state) => _state = state;

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
        panel.CustomMinimumSize = new Vector2(540, 0);
        center.AddChild(panel);

        var pad = new MarginContainer();
        pad.AddThemeConstantOverride("margin_left",   36);
        pad.AddThemeConstantOverride("margin_right",  36);
        pad.AddThemeConstantOverride("margin_top",    28);
        pad.AddThemeConstantOverride("margin_bottom", 28);
        panel.AddChild(pad);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        pad.AddChild(vbox);

        var title = UIKit.MakeDisplayLabel("TRAVEL SETTINGS", 24);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        // Daily food consumption preview
        int living = _state.Living().Count;
        var foodRow = new HBoxContainer();
        foodRow.Alignment = BoxContainer.AlignmentMode.Center;
        foodRow.AddThemeConstantOverride("separation", 24);
        AddFoodStat(foodRow, "FOOD REMAINING", $"{_state.Supplies.GetValueOrDefault("food", 0)} LBS");
        AddFoodStat(foodRow, "PARTY SIZE",     $"{living} ALIVE");
        vbox.AddChild(foodRow);

        vbox.AddChild(UIKit.MakeDivider());

        // ---- PACE section ----
        AddCentered(vbox, UIKit.MakeDisplayLabel("PACE", 19, UIKit.ColAmber));

        var paceDesc = UIKit.MakeBodyLabel(
            "REST recovers health but covers no ground. GRUELING moves faster but damages the wagon and oxen.",
            13, UIKit.ColGray);
        paceDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        paceDesc.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(paceDesc);

        var paceRow = new HBoxContainer();
        paceRow.Alignment = BoxContainer.AlignmentMode.Center;
        paceRow.AddThemeConstantOverride("separation", 10);

        _btnRest     = MakePaceBtn("REST",     "0 miles/day\nFull health recovery\nNo wagon wear");
        _btnSteady   = MakePaceBtn("STEADY",   "8-17 miles/day\nNormal health\nNormal wagon wear");
        _btnGrueling = MakePaceBtn("GRUELING", "12-22 miles/day\nReduced recovery\n2.5x wagon wear");

        _btnRest.Pressed     += () => SetPace("rest");
        _btnSteady.Pressed   += () => SetPace("steady");
        _btnGrueling.Pressed += () => SetPace("grueling");

        paceRow.AddChild(_btnRest);
        paceRow.AddChild(_btnSteady);
        paceRow.AddChild(_btnGrueling);
        vbox.AddChild(paceRow);

        vbox.AddChild(UIKit.MakeDivider());

        // ---- RATIONS section ----
        AddCentered(vbox, UIKit.MakeDisplayLabel("RATIONS", 19, UIKit.ColAmber));

        // Daily consumption preview per rations level
        string consPreview = $"BARE: {2 * living} LBS/DAY    MEAGER: {3 * living} LBS/DAY    FILLING: {5 * living} LBS/DAY";
        var consLabel = UIKit.MakeBodyLabel(consPreview, 12, UIKit.ColGray);
        consLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(consLabel);

        var rationsRow = new HBoxContainer();
        rationsRow.Alignment = BoxContainer.AlignmentMode.Center;
        rationsRow.AddThemeConstantOverride("separation", 10);

        _btnBare    = MakePaceBtn("BARE BONES", $"{2 * living} lbs/day\nHalf health recovery\nUse when food low");
        _btnMeager  = MakePaceBtn("MEAGER",     $"{3 * living} lbs/day\n-30% health recovery\nConserves food");
        _btnFilling = MakePaceBtn("FILLING",    $"{5 * living} lbs/day\nFull health recovery\nBest for weak party");

        _btnBare.Pressed    += () => SetRations("bare");
        _btnMeager.Pressed  += () => SetRations("meager");
        _btnFilling.Pressed += () => SetRations("filling");

        rationsRow.AddChild(_btnBare);
        rationsRow.AddChild(_btnMeager);
        rationsRow.AddChild(_btnFilling);
        vbox.AddChild(rationsRow);

        vbox.AddChild(UIKit.MakeSpacer(6));

        // CONFIRM
        var confirmRow = new HBoxContainer();
        confirmRow.Alignment = BoxContainer.AlignmentMode.Center;
        var confirmBtn = UIKit.MakePrimaryButton("CONFIRM", 20);
        confirmBtn.CustomMinimumSize = new Vector2(220, 52);
        confirmBtn.Pressed += () => EmitSignal(SignalName.Confirmed);
        confirmRow.AddChild(confirmBtn);
        vbox.AddChild(confirmRow);

        RefreshHighlights();
    }

    // =========================================================================
    // STATE SETTERS
    // =========================================================================

    private void SetPace(string pace)
    {
        _state.Pace = pace;
        RefreshHighlights();
    }

    private void SetRations(string rations)
    {
        _state.Rations = rations;
        RefreshHighlights();
    }

    // =========================================================================
    // HIGHLIGHT CURRENT SELECTION
    // =========================================================================

    private void RefreshHighlights()
    {
        HighlightPace(_btnRest,     "rest");
        HighlightPace(_btnSteady,   "steady");
        HighlightPace(_btnGrueling, "grueling");

        HighlightRations(_btnBare,    "bare");
        HighlightRations(_btnMeager,  "meager");
        HighlightRations(_btnFilling, "filling");
    }

    private void HighlightPace(Button btn, string pace)
    {
        bool active = _state.Pace == pace ||
                      (_state.Pace == "bare bones" && pace == "bare") ||
                      (_state.Pace == "bare bones" && pace == "bare");
        ApplyHighlight(btn, active, pace switch
        {
            "rest"     => UIKit.ColGreen,
            "grueling" => UIKit.ColRed,
            _          => UIKit.ColAmber,
        });
    }

    private void HighlightRations(Button btn, string rations)
    {
        string cur = _state.Rations.ToLower();
        bool active = cur == rations ||
                      (cur is "bare bones" or "barebones" && rations == "bare");
        ApplyHighlight(btn, active, rations switch
        {
            "bare"    => UIKit.ColRed,
            "meager"  => UIKit.ColAmber,
            _         => UIKit.ColGreen,
        });
    }

    private static void ApplyHighlight(Button btn, bool active, Color color)
    {
        if (active)
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color(color, 0.20f),
                BorderColor = color,
                BorderWidthLeft   = 2, BorderWidthRight  = 2,
                BorderWidthTop    = 2, BorderWidthBottom = 2,
                ContentMarginLeft   = 12, ContentMarginRight  = 12,
                ContentMarginTop    = 10, ContentMarginBottom = 10,
                CornerRadiusTopLeft = 4,  CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            };
            btn.AddThemeStyleboxOverride("normal",  style);
            btn.AddThemeStyleboxOverride("hover",   style);
            btn.AddThemeStyleboxOverride("pressed", style);
            btn.AddThemeColorOverride("font_color", color);
        }
        else
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color(UIKit.ColDarkBrown, 0.70f),
                BorderColor = new Color(UIKit.ColAmberDim, 0.40f),
                BorderWidthLeft   = 1, BorderWidthRight  = 1,
                BorderWidthTop    = 1, BorderWidthBottom = 1,
                ContentMarginLeft   = 12, ContentMarginRight  = 12,
                ContentMarginTop    = 10, ContentMarginBottom = 10,
                CornerRadiusTopLeft = 4,  CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            };
            btn.AddThemeStyleboxOverride("normal",  style);
            btn.AddThemeStyleboxOverride("hover",   style);
            btn.AddThemeStyleboxOverride("pressed", style);
            btn.AddThemeColorOverride("font_color", UIKit.ColGray);
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private static Button MakePaceBtn(string label, string detail)
    {
        // Multi-line button: label on top, detail below in smaller text
        // Godot Button doesn't support multi-line natively; use a VBoxContainer
        // wrapped in a custom button approach: we embed a Panel + VBox inside
        // a MarginContainer and hook mouse input.
        //
        // Simpler approach that works: set text to the full string with \n
        // and let autowrap handle it. Godot Button does support \n in text.
        var btn = new Button
        {
            Text = $"{label}\n{detail}",
            CustomMinimumSize = new Vector2(150, 90),
            ClipText = false,
        };
        btn.AddThemeFontOverride("font", UIKit.BodyFont);
        btn.AddThemeFontSizeOverride("font_size", 13);
        return btn;
    }

    private static void AddCentered(VBoxContainer parent, Label label)
    {
        label.HorizontalAlignment = HorizontalAlignment.Center;
        parent.AddChild(label);
    }

    private static void AddFoodStat(HBoxContainer row, string label, string value)
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);

        var l = UIKit.MakeBodyLabel(label, 11, UIKit.ColAmberDim);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        var v = UIKit.MakeBodyLabel(value, 14, UIKit.ColParchment);
        v.HorizontalAlignment = HorizontalAlignment.Center;

        col.AddChild(l);
        col.AddChild(v);
        row.AddChild(col);
    }
}
