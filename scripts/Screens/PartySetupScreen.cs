#nullable enable
using System.Linq;
using Godot;
using OregonTrail2026.Models;

namespace OregonTrail2026.Screens;

/// <summary>
/// Two-step party setup wizard.
///   Step 1: Choose occupation (shows cash, repair skill, score multiplier).
///   Step 2: Name all 5 party members (all fields visible, tab between them).
///
/// Signals:
///   SetupComplete(string occupation, string[] names) - emitted when player hits BEGIN.
///
/// Aesthetic: 1985 Oregon Trail. Dark framed panels, amber/gold text, uppercase.
/// Background: bg_independence_store.webp. Music: Main_Menu_Score_V1b.mp3.
/// </summary>
public partial class PartySetupScreen : Control
{
    [Signal]
    public delegate void SetupCompleteEventHandler(string occupation, string[] names);

    // ---- Color palette (1985 Oregon Trail CRT feel) ----
    private static readonly Color ColAmber      = new("FFD700");
    private static readonly Color ColAmberDim   = new("B8960F");
    private static readonly Color ColGreen      = new("5BFF5B");
    private static readonly Color ColWhite      = new("F0E6D2");    // parchment white
    private static readonly Color ColGray       = new("888877");
    private static readonly Color ColPanelBg    = new("1A1408E6");  // near-black, slight transparency
    private static readonly Color ColPanelBorder = new("8B7332");   // dark gold border
    private static readonly Color ColInputBg    = new("0D0A04F0");
    private static readonly Color ColSelected   = new("3A2A0AFF");
    private static readonly Color ColHover      = new("2A1C08FF");

    // ---- State ----
    private int _step = 0;  // 0 = occupation, 1 = names
    private string _selectedOccupation = "";
    private readonly string[] _partyNames = { "Gabriel", "Dude", "Andrea", "Tank", "Mellow" };
    private readonly LineEdit[] _nameEdits = new LineEdit[5];

    // ---- Node refs built in code ----
    private TextureRect _background = null!;
    private Control _step0Panel = null!;
    private Control _step1Panel = null!;
    private Button[] _occButtons = new Button[3];
    private Label _occDetailLabel = null!;
    private Label _partyPreviewLabel = null!;

    // ---- Occupation descriptions (visible consequences) ----
    private static readonly (string key, string title, string cash, string skill, string score, string desc)[] OccInfo =
    {
        ("banker",    "BANKER FROM BOSTON",     "$1,600", "POOR (12%)",  "x1",
         "You start with the most money but\nyour hands have never held a wrench.\nField repairs will be difficult."),
        ("carpenter", "CARPENTER FROM OHIO",   "$800",   "EXPERT (78%)", "x2",
         "Moderate funds, but you know wood\nand iron. Repairs come naturally.\nScore multiplier rewards the skill."),
        ("farmer",    "FARMER FROM ILLINOIS",  "$400",   "FAIR (48%)",  "x3",
         "The least money, but you know the\nland and hard work. The highest\nscore multiplier for those who survive."),
    };

    public override void _Ready()
    {
        // Fill the entire parent
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        BuildBackground();
        BuildStep0();
        BuildStep1();

        ShowStep(0);
    }

    // ========================================================================
    // BACKGROUND
    // ========================================================================

    private void BuildBackground()
    {
        // Background image
        _background = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/images/bg/bg_independence_store.webp"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        // Dark overlay to make text readable
        var overlay = new ColorRect { Color = new Color(0.04f, 0.03f, 0.01f, 0.65f) };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);
    }

    // ========================================================================
    // STEP 0: OCCUPATION SELECTION
    // ========================================================================

    private void BuildStep0()
    {
        _step0Panel = new Control();
        _step0Panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_step0Panel);

        var vbox = new VBoxContainer { };
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        vbox.GrowHorizontal = GrowDirection.Both;
        vbox.GrowVertical = GrowDirection.Both;
        vbox.CustomMinimumSize = new Vector2(700, 560);
        vbox.SetAnchor(Side.Left, 0.5f);
        vbox.SetAnchor(Side.Top, 0.5f);
        vbox.SetOffset(Side.Left, -350);
        vbox.SetOffset(Side.Top, -280);
        vbox.SetOffset(Side.Right, 350);
        vbox.SetOffset(Side.Bottom, 280);
        _step0Panel.AddChild(vbox);

        // Title area
        var titlePanel = MakePanel();
        var titleVbox = new VBoxContainer();
        titleVbox.AddThemeConstantOverride("separation", 4);
        titlePanel.AddChild(titleVbox);
        vbox.AddChild(titlePanel);

        titleVbox.AddChild(MakeLabel(
            "THE OREGON TRAIL", 28, ColAmber, HorizontalAlignment.Center));
        titleVbox.AddChild(MakeLabel(
            "MANY KINDS OF PEOPLE MADE THE TRIP TO OREGON.", 14, ColWhite, HorizontalAlignment.Center));
        titleVbox.AddChild(MakeSpacer(6));
        titleVbox.AddChild(MakeLabel(
            "YOU MAY:", 16, ColAmber, HorizontalAlignment.Center));
        titleVbox.AddChild(MakeSpacer(4));

        // Occupation buttons
        var btnContainer = new VBoxContainer();
        btnContainer.AddThemeConstantOverride("separation", 6);
        for (int i = 0; i < 3; i++)
        {
            int idx = i; // capture for lambda
            var info = OccInfo[i];

            var btn = new Button
            {
                Text = $"  {i + 1}. BE A {info.title}  (STARTING CASH: {info.cash})",
                CustomMinimumSize = new Vector2(660, 42),
                FocusMode = FocusModeEnum.All,
            };
            StyleOccupationButton(btn, false);
            btn.Pressed += () => OnOccupationClicked(idx);
            btn.MouseEntered += () => OnOccupationHover(idx);
            _occButtons[i] = btn;
            btnContainer.AddChild(btn);
        }
        titleVbox.AddChild(btnContainer);

        titleVbox.AddChild(MakeSpacer(8));

        // Detail panel (shows info about hovered/selected occupation)
        var detailPanel = MakePanel();
        _occDetailLabel = MakeLabel(
            "CHOOSE YOUR OCCUPATION TO SEE DETAILS.", 13, ColGray, HorizontalAlignment.Left);
        _occDetailLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _occDetailLabel.CustomMinimumSize = new Vector2(620, 80);
        detailPanel.AddChild(_occDetailLabel);
        titleVbox.AddChild(detailPanel);

        // Keyboard hint
        titleVbox.AddChild(MakeSpacer(6));
        titleVbox.AddChild(MakeLabel(
            "PRESS 1, 2, OR 3 TO CHOOSE", 12, ColGray, HorizontalAlignment.Center));
    }

    private void OnOccupationHover(int idx)
    {
        var info = OccInfo[idx];
        _occDetailLabel.Text =
            $"{info.title}\n" +
            $"STARTING CASH: {info.cash}    REPAIR SKILL: {info.skill}    SCORE: {info.score}\n\n" +
            info.desc;
        _occDetailLabel.AddThemeColorOverride("font_color", ColWhite);
    }

    private void OnOccupationClicked(int idx)
    {
        _selectedOccupation = OccInfo[idx].key;

        // Highlight selected, dim others
        for (int i = 0; i < 3; i++)
            StyleOccupationButton(_occButtons[i], i == idx);

        // Show detail for selected
        OnOccupationHover(idx);

        // Brief delay then advance to step 1
        GetTree().CreateTimer(0.35).Timeout += () => ShowStep(1);
    }

    // ========================================================================
    // STEP 1: NAME ENTRY
    // ========================================================================

    private void BuildStep1()
    {
        _step1Panel = new Control();
        _step1Panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _step1Panel.Visible = false;
        AddChild(_step1Panel);

        var vbox = new VBoxContainer();
        vbox.SetAnchor(Side.Left, 0.5f);
        vbox.SetAnchor(Side.Top, 0.5f);
        vbox.SetOffset(Side.Left, -350);
        vbox.SetOffset(Side.Top, -280);
        vbox.SetOffset(Side.Right, 350);
        vbox.SetOffset(Side.Bottom, 280);
        _step1Panel.AddChild(vbox);

        // Title
        var titlePanel = MakePanel();
        var titleInner = new VBoxContainer();
        titleInner.AddThemeConstantOverride("separation", 4);
        titlePanel.AddChild(titleInner);
        vbox.AddChild(titlePanel);

        titleInner.AddChild(MakeLabel(
            "WHAT ARE THE NAMES OF THE", 16, ColWhite, HorizontalAlignment.Center));
        titleInner.AddChild(MakeLabel(
            "FIVE MEMBERS IN YOUR PARTY?", 16, ColWhite, HorizontalAlignment.Center));

        titleInner.AddChild(MakeSpacer(12));

        // Name input fields
        string[] labels = {
            "1. WAGON LEADER:",
            "2. PARTY MEMBER:",
            "3. PARTY MEMBER:",
            "4. PARTY MEMBER:",
            "5. PARTY MEMBER:",
        };

        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);

            var lbl = MakeLabel(labels[i], 16, ColAmberDim, HorizontalAlignment.Left);
            lbl.CustomMinimumSize = new Vector2(200, 0);
            row.AddChild(lbl);

            var edit = new LineEdit
            {
                Text = _partyNames[i],
                MaxLength = GameConstants.PartyNameMaxLength,
                CustomMinimumSize = new Vector2(380, 36),
                PlaceholderText = "ENTER NAME...",
                SelectAllOnFocus = true,
            };
            StyleLineEdit(edit);
            edit.TextChanged += (text) => OnNameChanged(idx, text);
            _nameEdits[i] = edit;
            row.AddChild(edit);

            titleInner.AddChild(row);
        }

        titleInner.AddChild(MakeSpacer(10));

        // Party preview
        _partyPreviewLabel = MakeLabel("", 13, ColGray, HorizontalAlignment.Center);
        titleInner.AddChild(_partyPreviewLabel);
        UpdatePartyPreview();

        titleInner.AddChild(MakeSpacer(8));

        // Bottom buttons
        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 20);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;

        var backBtn = MakeStyledButton("< BACK");
        backBtn.Pressed += () => ShowStep(0);
        btnRow.AddChild(backBtn);

        var beginBtn = MakeStyledButton("HIT THE TRAIL >");
        beginBtn.CustomMinimumSize = new Vector2(220, 44);
        beginBtn.Pressed += OnBeginGame;
        btnRow.AddChild(beginBtn);

        titleInner.AddChild(btnRow);

        titleInner.AddChild(MakeSpacer(4));
        titleInner.AddChild(MakeLabel(
            "TAB TO MOVE BETWEEN FIELDS", 11, ColGray, HorizontalAlignment.Center));
    }

    private void OnNameChanged(int idx, string text)
    {
        _partyNames[idx] = text.Trim();
        UpdatePartyPreview();
    }

    private void UpdatePartyPreview()
    {
        string names = string.Join(", ", _partyNames.Select(
            n => string.IsNullOrWhiteSpace(n) ? "???" : n.ToUpper()));
        _partyPreviewLabel.Text = $"YOUR PARTY: {names}";
    }

    private void OnBeginGame()
    {
        // Validate: at least the leader has a name
        if (string.IsNullOrWhiteSpace(_partyNames[0]))
        {
            _partyNames[0] = "Gabriel";
            _nameEdits[0].Text = "Gabriel";
        }

        // Fill empty names with defaults
        string[] defaults = { "Gabriel", "Dude", "Andrea", "Tank", "Mellow" };
        for (int i = 0; i < 5; i++)
        {
            if (string.IsNullOrWhiteSpace(_partyNames[i]))
            {
                _partyNames[i] = defaults[i];
                _nameEdits[i].Text = defaults[i];
            }
        }

        EmitSignal(SignalName.SetupComplete, _selectedOccupation, _partyNames);
    }

    // ========================================================================
    // STEP TRANSITIONS
    // ========================================================================

    private void ShowStep(int step)
    {
        _step = step;
        _step0Panel.Visible = step == 0;
        _step1Panel.Visible = step == 1;

        if (step == 1 && _nameEdits[0] != null)
        {
            // Focus the first name field after a frame so Godot processes visibility
            CallDeferred(MethodName.FocusFirstName);
        }
    }

    private void FocusFirstName()
    {
        _nameEdits[0]?.GrabFocus();
    }

    // ========================================================================
    // KEYBOARD SHORTCUTS
    // ========================================================================

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed) return;
        if (!Visible) return;

        if (_step == 0)
        {
            // Number keys to pick occupation
            int choice = key.Keycode switch
            {
                Key.Key1 => 0,
                Key.Key2 => 1,
                Key.Key3 => 2,
                _ => -1,
            };
            if (choice >= 0)
            {
                OnOccupationClicked(choice);
                GetViewport().SetInputAsHandled();
            }
        }
        else if (_step == 1)
        {
            // Enter on last field or anywhere triggers begin
            if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
            {
                // Only if no LineEdit has focus or the last one does
                var focused = GetViewport().GuiGetFocusOwner();
                if (focused == _nameEdits[4] || focused is not LineEdit)
                {
                    OnBeginGame();
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (key.Keycode == Key.Escape)
            {
                ShowStep(0);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    // ========================================================================
    // STYLING HELPERS (1985 Oregon Trail aesthetic)
    // ========================================================================

    private static PanelContainer MakePanel()
    {
        var panel = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = ColPanelBg,
            BorderColor = ColPanelBorder,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 16,
            ContentMarginBottom = 16,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static Label MakeLabel(string text, int size, Color color, HorizontalAlignment align)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = align,
        };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private static Control MakeSpacer(int height)
    {
        return new Control { CustomMinimumSize = new Vector2(0, height) };
    }

    private void StyleOccupationButton(Button btn, bool selected)
    {
        // Normal state
        var normal = new StyleBoxFlat
        {
            BgColor = selected ? ColSelected : new Color(0.08f, 0.06f, 0.02f, 0.9f),
            BorderColor = selected ? ColAmber : ColPanelBorder,
            BorderWidthBottom = selected ? 2 : 1,
            BorderWidthTop = selected ? 2 : 1,
            BorderWidthLeft = selected ? 2 : 1,
            BorderWidthRight = selected ? 2 : 1,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };

        var hover = normal.Duplicate() as StyleBoxFlat;
        if (hover != null)
        {
            hover.BgColor = ColHover;
            hover.BorderColor = ColAmber;
            hover.BorderWidthBottom = 2;
            hover.BorderWidthTop = 2;
            hover.BorderWidthLeft = 2;
            hover.BorderWidthRight = 2;
        }

        var pressed = normal.Duplicate() as StyleBoxFlat;
        if (pressed != null)
        {
            pressed.BgColor = ColSelected;
            pressed.BorderColor = ColAmber;
        }

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover ?? normal);
        btn.AddThemeStyleboxOverride("pressed", pressed ?? normal);
        btn.AddThemeStyleboxOverride("focus", hover ?? normal);
        btn.AddThemeColorOverride("font_color", selected ? ColAmber : ColWhite);
        btn.AddThemeColorOverride("font_hover_color", ColAmber);
        btn.AddThemeColorOverride("font_pressed_color", ColAmber);
        btn.AddThemeFontSizeOverride("font_size", 16);
        btn.Alignment = HorizontalAlignment.Left;
    }

    private static void StyleLineEdit(LineEdit edit)
    {
        var style = new StyleBoxFlat
        {
            BgColor = ColInputBg,
            BorderColor = ColPanelBorder,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusBottomLeft = 1,
            CornerRadiusBottomRight = 1,
            CornerRadiusTopLeft = 1,
            CornerRadiusTopRight = 1,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };

        var focused = style.Duplicate() as StyleBoxFlat;
        if (focused != null)
        {
            focused.BorderColor = ColAmber;
            focused.BorderWidthBottom = 2;
            focused.BorderWidthTop = 2;
            focused.BorderWidthLeft = 2;
            focused.BorderWidthRight = 2;
        }

        edit.AddThemeStyleboxOverride("normal", style);
        edit.AddThemeStyleboxOverride("focus", focused ?? style);
        edit.AddThemeColorOverride("font_color", ColAmber);
        edit.AddThemeColorOverride("font_placeholder_color", ColGray);
        edit.AddThemeColorOverride("caret_color", ColAmber);
        edit.AddThemeColorOverride("selection_color", new Color(0.4f, 0.3f, 0.05f, 0.5f));
        edit.AddThemeFontSizeOverride("font_size", 18);
    }

    private static Button MakeStyledButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(160, 44),
        };

        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.08f, 0.02f, 0.95f),
            BorderColor = ColPanelBorder,
            BorderWidthBottom = 2,
            BorderWidthTop = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };

        var hover = normal.Duplicate() as StyleBoxFlat;
        if (hover != null)
        {
            hover.BgColor = ColHover;
            hover.BorderColor = ColAmber;
        }

        var pressed = normal.Duplicate() as StyleBoxFlat;
        if (pressed != null)
        {
            pressed.BgColor = ColSelected;
            pressed.BorderColor = ColAmber;
        }

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover ?? normal);
        btn.AddThemeStyleboxOverride("pressed", pressed ?? normal);
        btn.AddThemeStyleboxOverride("focus", hover ?? normal);
        btn.AddThemeColorOverride("font_color", ColWhite);
        btn.AddThemeColorOverride("font_hover_color", ColAmber);
        btn.AddThemeColorOverride("font_pressed_color", ColAmber);
        btn.AddThemeFontSizeOverride("font_size", 16);

        return btn;
    }
}
