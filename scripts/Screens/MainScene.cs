#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Systems;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Main scene controller. Manages game flow, screen transitions, HUD updates.
///
/// Flow:
///   Splash -> MainMenu -> (NewGame -> PartySetup -> Travel)
///                       -> (LoadGame -> Travel)
///                       -> (DeleteGame -> back to menu)
///                       -> (Settings sub-panels -> back to menu)
///
/// F12 activates DevConsole during active gameplay only.
/// </summary>
public partial class MainScene : Control
{
    // Node references (from .tscn)
    private TextureRect _background = null!;
    private AudioStreamPlayer _audioPlayer = null!;

    // Code-built UI
    private CanvasLayer _uiLayer = null!;
    private Control _hud = null!;
    private PanelContainer _messagePanel = null!;
    private TextureRect _cardImage = null!;
    private Label _messageLabel = null!;
    private Control _choicePanel = null!;
    private Label _dateLabel = null!;
    private Label _weatherLabel = null!;
    private Label _milesLabel = null!;
    private Label _cashLabel = null!;
    private Label _foodLabel = null!;
    private Label _healthLabel = null!;
    private Label _paceLabel   = null!;
    private Label _rationsLabel = null!;

    // Game flow state
    private enum FlowState { Splash, MainMenu, Setup, Travel, AwaitChoice, Store, Rest, Hunt, Fish, River, GameOver, Victory }
    private FlowState _flowState = FlowState.Splash;

    // Pending message queue
    private readonly Queue<string> _messageQueue = new();
    private bool _awaitingClick = false;

    // Screen references
    private SplashScreen? _splashScreen;
    private MainMenuScreen? _mainMenuScreen;
    private PartySetupScreen? _partySetupScreen;
    private SaveSlotScreen? _saveSlotScreen;
    private IndependenceScreen? _independenceScreen;
    private RiverCrossingScreen? _riverCrossingScreen;
    private FortStoreScreen? _fortStoreScreen;
    private HuntScreen? _huntScreen;
    private FishScreen? _fishScreen;
    private RolesScreen? _rolesScreen;
    private PaceRationsPanel? _pacePanel;
    private RouteChoiceScreen? _routeChoiceScreen;
    private TradeScreen? _tradeScreen;
    private MapScreen? _mapScreen;
    private VictoryScreen? _victoryScreen;
    private GameOverScreen? _gameOverScreen;
    private DevConsole? _devConsole;

    public override void _Ready()
    {
        // Get .tscn node references
        _background = GetNode<TextureRect>("Background");
        _audioPlayer = GetNode<AudioStreamPlayer>("AudioPlayer");

        // Build UI layer in code (replaces old .tscn UILayer)
        BuildUILayer();

        // Connect to GameManager signals
        GameManager.Instance.StateChanged += OnStateChanged;

        // Create dev console (always exists, just hidden)
        _devConsole = new DevConsole();
        AddChild(_devConsole);

        // Start with splash screen
        ShowSplashScreen();
    }

    // ========================================================================
    // UI CONSTRUCTION
    // ========================================================================

    private void BuildUILayer()
    {
        _uiLayer = new CanvasLayer { Layer = 10 };
        AddChild(_uiLayer);

        // ---- HUD (top bar) ----
        _hud = new Control();
        _hud.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _hud.SetOffset(Side.Bottom, 52);
        _hud.Visible = false;
        _uiLayer.AddChild(_hud);

        // Dark bar background
        var hudBg = new PanelContainer();
        hudBg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var hudStyle = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.7f),
            ContentMarginLeft = 16, ContentMarginRight = 16,
            ContentMarginTop = 6, ContentMarginBottom = 6,
            BorderColor = new Color(UIKit.ColAmberDim, 0.5f),
            BorderWidthBottom = 1,
        };
        hudBg.AddThemeStyleboxOverride("panel", hudStyle);
        _hud.AddChild(hudBg);

        var hudRow = new HBoxContainer();
        hudRow.AddThemeConstantOverride("separation", 24);
        hudRow.Alignment = BoxContainer.AlignmentMode.Center;
        hudBg.AddChild(hudRow);

        // Font for HUD
        var bodyFont = GD.Load<Font>("res://assets/fonts/NotoSans-Variable.ttf");
        var displayFont = GD.Load<Font>("res://assets/fonts/Federant-Regular.ttf");

        // Date (display font, amber)
        _dateLabel = MakeHudLabel("APR 1", 18, displayFont, UIKit.ColAmber);
        hudRow.AddChild(_dateLabel);

        // Separator
        hudRow.AddChild(MakeHudSep());

        // Weather
        _weatherLabel = MakeHudLabel("CLEAR", 14, bodyFont, UIKit.ColParchment);
        hudRow.AddChild(_weatherLabel);

        hudRow.AddChild(MakeHudSep());

        // Miles
        _milesLabel = MakeHudLabel("MILES: 0/2170", 14, bodyFont, UIKit.ColParchment);
        hudRow.AddChild(_milesLabel);

        hudRow.AddChild(MakeHudSep());

        // Cash
        _cashLabel = MakeHudLabel("$0.00", 14, bodyFont, UIKit.ColAmber);
        hudRow.AddChild(_cashLabel);

        hudRow.AddChild(MakeHudSep());

        // Food
        _foodLabel = MakeHudLabel("FOOD: 0", 14, bodyFont, UIKit.ColParchment);
        hudRow.AddChild(_foodLabel);

        hudRow.AddChild(MakeHudSep());

        // Health
        _healthLabel = MakeHudLabel("HEALTH: GOOD", 14, bodyFont, UIKit.ColGreen);
        hudRow.AddChild(_healthLabel);

        hudRow.AddChild(MakeHudSep());

        // Pace
        _paceLabel = MakeHudLabel("PACE: STEADY", 14, bodyFont, UIKit.ColParchment);
        hudRow.AddChild(_paceLabel);

        hudRow.AddChild(MakeHudSep());

        // Rations
        _rationsLabel = MakeHudLabel("RATIONS: FILLING", 14, bodyFont, UIKit.ColParchment);
        hudRow.AddChild(_rationsLabel);

        // ---- MESSAGE PANEL (bottom center) ----
        _messagePanel = new PanelContainer();
        _messagePanel.Visible = false;
        _messagePanel.SetAnchor(Side.Left, 0.15f);
        _messagePanel.SetAnchor(Side.Right, 0.85f);
        _messagePanel.SetAnchor(Side.Top, 1.0f);
        _messagePanel.SetAnchor(Side.Bottom, 1.0f);
        _messagePanel.SetOffset(Side.Top, -280);   // expanded to fit card image + text
        _messagePanel.SetOffset(Side.Bottom, -16);
        _messagePanel.SetOffset(Side.Left, 0);
        _messagePanel.SetOffset(Side.Right, 0);

        var msgStyle = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.8f),
            BorderColor = new Color(UIKit.ColAmberDim, 0.6f),
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
            ContentMarginLeft = 20, ContentMarginRight = 20,
            ContentMarginTop = 12, ContentMarginBottom = 12,
        };
        _messagePanel.AddThemeStyleboxOverride("panel", msgStyle);

        _messageLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _messageLabel.AddThemeFontOverride("font", bodyFont);
        _messageLabel.AddThemeFontSizeOverride("font_size", 16);
        _messageLabel.AddThemeColorOverride("font_color", UIKit.ColParchment);
        // Event card image (hidden by default)
        _cardImage = new TextureRect
        {
            ExpandMode  = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            Visible = false,
        };

        // The message panel now uses a VBoxContainer to stack image + text
        var msgVbox = new VBoxContainer();
        msgVbox.AddThemeConstantOverride("separation", 8);
        msgVbox.AddChild(_cardImage);
        msgVbox.AddChild(_messageLabel);
        _messagePanel.AddChild(msgVbox);

        _uiLayer.AddChild(_messagePanel);

        // ---- CHOICE PANEL (bottom center, button grid) ----
        BuildChoicePanel();
    }

    private void BuildChoicePanel()
    {
        _choicePanel = new PanelContainer();
        _choicePanel.Visible = false;
        _choicePanel.SetAnchor(Side.Left, 0.1f);
        _choicePanel.SetAnchor(Side.Right, 0.9f);
        _choicePanel.SetAnchor(Side.Top, 1.0f);
        _choicePanel.SetAnchor(Side.Bottom, 1.0f);
        _choicePanel.SetOffset(Side.Top, -170);
        _choicePanel.SetOffset(Side.Bottom, -12);
        _choicePanel.SetOffset(Side.Left, 0);
        _choicePanel.SetOffset(Side.Right, 0);

        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.85f),
            BorderColor = new Color(UIKit.ColAmberDim, 0.6f),
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            ContentMarginLeft = 16, ContentMarginRight = 16,
            ContentMarginTop = 12, ContentMarginBottom = 12,
        };
        ((PanelContainer)_choicePanel).AddThemeStyleboxOverride("panel", panelStyle);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);

        // Title row
        var titleLabel = UIKit.MakeDisplayLabel("WHAT IS YOUR CHOICE?", 18);
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(titleLabel);

        // Button grid: 2 rows x 4 columns
        var row1 = new HBoxContainer();
        row1.AddThemeConstantOverride("separation", 8);
        row1.Alignment = BoxContainer.AlignmentMode.Center;

        var row2 = new HBoxContainer();
        row2.AddThemeConstantOverride("separation", 8);
        row2.Alignment = BoxContainer.AlignmentMode.Center;

        // Row 1
        row1.AddChild(MakeChoiceButton("CONTINUE", 1));
        row1.AddChild(MakeChoiceButton("CHECK MAP", 2));
        row1.AddChild(MakeChoiceButton("VISIT STORE", 3));
        row1.AddChild(MakeChoiceButton("PACE/RATIONS", 4));

        // Row 2
        row2.AddChild(MakeChoiceButton("GO HUNTING", 5));
        row2.AddChild(MakeChoiceButton("GO FISHING", 6));
        row2.AddChild(MakeChoiceButton("SAVE GAME", 7));
        row2.AddChild(MakeChoiceButton("ROLES", 8));

        vbox.AddChild(row1);
        vbox.AddChild(row2);

        ((PanelContainer)_choicePanel).AddChild(vbox);
        _uiLayer.AddChild(_choicePanel);
    }

    private Button MakeChoiceButton(string text, int choiceId)
    {
        var btn = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(160, 40),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(UIKit.ColDarkBrown, 0.9f),
            BorderColor = UIKit.ColAmberDim,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            ContentMarginLeft = 8, ContentMarginRight = 8,
            ContentMarginTop = 4, ContentMarginBottom = 4,
        };
        var hoverStyle = new StyleBoxFlat
        {
            BgColor = new Color(UIKit.ColAmberDim, 0.6f),
            BorderColor = UIKit.ColAmber,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            ContentMarginLeft = 8, ContentMarginRight = 8,
            ContentMarginTop = 4, ContentMarginBottom = 4,
        };
        var pressedStyle = new StyleBoxFlat
        {
            BgColor = new Color(UIKit.ColAmber, 0.3f),
            BorderColor = UIKit.ColAmber,
            BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            ContentMarginLeft = 8, ContentMarginRight = 8,
            ContentMarginTop = 4, ContentMarginBottom = 4,
        };

        btn.AddThemeStyleboxOverride("normal", normalStyle);
        btn.AddThemeStyleboxOverride("hover", hoverStyle);
        btn.AddThemeStyleboxOverride("pressed", pressedStyle);
        btn.AddThemeStyleboxOverride("focus", normalStyle);

        var font = GD.Load<Font>("res://assets/fonts/NotoSans-Variable.ttf");
        btn.AddThemeFontOverride("font", font);
        btn.AddThemeFontSizeOverride("font_size", 14);
        btn.AddThemeColorOverride("font_color", UIKit.ColParchment);
        btn.AddThemeColorOverride("font_hover_color", UIKit.ColWhite);
        btn.AddThemeColorOverride("font_pressed_color", UIKit.ColAmber);

        int captured = choiceId;
        btn.Pressed += () => HandleChoice(captured);

        return btn;
    }

    private void ShowChoiceMenu()
    {
        _messagePanel.Visible = false;
        _awaitingClick = false;
        _choicePanel.Visible = true;
    }

    private void HideChoiceMenu()
    {
        _choicePanel.Visible = false;
    }

    private static Label MakeHudLabel(string text, int size, Font font, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static VSeparator MakeHudSep()
    {
        var sep = new VSeparator();
        sep.AddThemeConstantOverride("separation", 2);
        sep.Modulate = new Color(UIKit.ColAmberDim, 0.5f);
        return sep;
    }

    // ========================================================================
    // SPLASH -> MAIN MENU -> GAME
    // ========================================================================

    private void ShowSplashScreen()
    {
        _flowState = FlowState.Splash;
        HideHUD();

        PlayMusic("res://assets/audio/OregonTrail2026_Title_Score_V1a.mp3");

        _splashScreen = new SplashScreen();
        AddChild(_splashScreen);
        _splashScreen.SplashFinished += OnSplashFinished;
    }

    private void OnSplashFinished()
    {
        if (_splashScreen != null)
        {
            _splashScreen.SplashFinished -= OnSplashFinished;
            _splashScreen.QueueFree();
            _splashScreen = null;
        }

        // Apply user's saved window preference now (after splash)
        // SettingsManager already applied during _Ready, but this ensures
        // the transition from splash windowed size to user's preference.

        ShowMainMenu();
    }

    private void ShowMainMenu()
    {
        _flowState = FlowState.MainMenu;
        HideHUD();

        PlayMusic("res://assets/audio/OregonTrail2026_Main_Menu_Score_V1b.mp3");

        _mainMenuScreen = new MainMenuScreen();
        AddChild(_mainMenuScreen);
        _mainMenuScreen.NewGameRequested    += OnNewGameRequested;
        _mainMenuScreen.LoadGameRequested   += OnLoadGameRequested;
        _mainMenuScreen.DeleteGameRequested += OnDeleteGameRequested;
    }

    private void RemoveMainMenu()
    {
        if (_mainMenuScreen != null)
        {
            _mainMenuScreen.NewGameRequested    -= OnNewGameRequested;
            _mainMenuScreen.LoadGameRequested   -= OnLoadGameRequested;
            _mainMenuScreen.DeleteGameRequested -= OnDeleteGameRequested;
            _mainMenuScreen.QueueFree();
            _mainMenuScreen = null;
        }
    }

    private void OnNewGameRequested()
    {
        RemoveMainMenu();
        ShowSetupScreen();
    }

    private void OnLoadGameRequested()
    {
        RemoveMainMenu();
        ShowSaveSlotScreen(SaveSlotScreen.Mode.Load);
    }

    private void OnDeleteGameRequested()
    {
        RemoveMainMenu();
        ShowSaveSlotScreen(SaveSlotScreen.Mode.Delete);
    }

    // ========================================================================
    // SAVE SLOT SCREEN
    // ========================================================================

    private void ShowSaveSlotScreen(SaveSlotScreen.Mode mode)
    {
        _saveSlotScreen = new SaveSlotScreen(mode);
        AddChild(_saveSlotScreen);
        _saveSlotScreen.SlotSelected += OnSaveSlotSelected;
        _saveSlotScreen.BackRequested += OnSaveSlotBack;
    }

    private void RemoveSaveSlotScreen()
    {
        if (_saveSlotScreen != null)
        {
            _saveSlotScreen.SlotSelected -= OnSaveSlotSelected;
            _saveSlotScreen.BackRequested -= OnSaveSlotBack;
            _saveSlotScreen.QueueFree();
            _saveSlotScreen = null;
        }
    }

    private void OnSaveSlotSelected(string slotId)
    {
        GD.Print($"[MainScene] Loading slot '{slotId}'");
        var (state, msg) = SaveFileSystem.Load(slotId);
        if (state != null)
        {
            RemoveSaveSlotScreen();
            GameManager.Instance.LoadFromState(state);
            ShowHUD();
            _flowState = FlowState.AwaitChoice;
            UpdateHUD();
            ShowChoiceMenu();
            PlayMusic("res://assets/audio/OregonTrail2026_Travel_Score_V1a.mp3");

            if (msg != "OK")
            {
                // Recovered from backup
                ShowMessage(Tr(TK.SaveCorruptRecov));
            }
        }
        else
        {
            // Stay on slot screen, log the error
            GD.PrintErr($"[MainScene] Load failed for slot '{slotId}': {msg}");
        }
    }

    private void OnSaveSlotBack()
    {
        RemoveSaveSlotScreen();
        ShowMainMenu();
    }

    // ========================================================================
    // PARTY SETUP
    // ========================================================================

    private void ShowSetupScreen()
    {
        _flowState = FlowState.Setup;
        HideHUD();

        _partySetupScreen = new PartySetupScreen();
        AddChild(_partySetupScreen);
        _partySetupScreen.SetupComplete += OnPartySetupComplete;
    }

    private void OnPartySetupComplete(string occupation, string[] names)
    {
        if (_partySetupScreen != null)
        {
            _partySetupScreen.SetupComplete -= OnPartySetupComplete;
            _partySetupScreen.QueueFree();
            _partySetupScreen = null;
        }

        var nameList = new System.Collections.Generic.List<string>(names);
        GameManager.Instance.StartNewGame(occupation, nameList);

        ShowIndependenceScreen();
    }

    // ========================================================================
    // INDEPENDENCE TOWN SCREEN
    // ========================================================================

    private void ShowIndependenceScreen()
    {
        _flowState = FlowState.Setup; // still in setup phase
        HideHUD();

        SetBackground("res://assets/images/bg/bg_independence_street.webp");

        _independenceScreen = new IndependenceScreen();
        _independenceScreen.Initialize(GameManager.Instance.State);
        AddChild(_independenceScreen);
        _independenceScreen.DepartureReady += OnDepartureReady;
    }

    private void OnDepartureReady(int monthOffset)
    {
        if (_independenceScreen != null)
        {
            _independenceScreen.DepartureReady -= OnDepartureReady;
            _independenceScreen.QueueFree();
            _independenceScreen = null;
        }

        // Already in Independence from StartNewGame. Leave town to start travel.
        GameManager.Instance.LeaveTown();

        ShowHUD();
        _flowState = FlowState.AwaitChoice;
        UpdateHUD();

        ShowMessage("YOUR WAGON IS LOADED. THE TRAIL AWAITS.\n[Press SPACE to continue]");

        PlayMusic("res://assets/audio/OregonTrail2026_Travel_Score_V1a.mp3");
    }

    // ========================================================================
    // RIVER CROSSING SCREEN
    // ========================================================================

    private void ShowRiverCrossingScreen(GameData.RiverInfo river)
    {
        _flowState = FlowState.River;
        HideChoiceMenu();

        // Set the river-specific background before the screen overlays it
        SetBackground(river.BgImage);

        _riverCrossingScreen = new RiverCrossingScreen();
        _riverCrossingScreen.Initialize(GameManager.Instance.State, river);
        AddChild(_riverCrossingScreen);
        _riverCrossingScreen.CrossingComplete  += OnRiverCrossingComplete;
        _riverCrossingScreen.WaitDayRequested  += OnRiverWaitDayRequested;
    }

    private void RemoveRiverCrossingScreen()
    {
        if (_riverCrossingScreen != null)
        {
            _riverCrossingScreen.CrossingComplete -= OnRiverCrossingComplete;
            _riverCrossingScreen.WaitDayRequested -= OnRiverWaitDayRequested;
            _riverCrossingScreen.QueueFree();
            _riverCrossingScreen = null;
        }
    }

    private void OnRiverCrossingComplete()
    {
        var st = GameManager.Instance.State;

        // Retrieve result flags written by RiverCrossingScreen before it signalled
        bool success = st.StopFlags.GetValueOrDefault("river_crossing_success") as bool? ?? false;
        string msg   = st.StopFlags.GetValueOrDefault("river_crossing_msg") as string
                       ?? "THE CROSSING IS DONE.";
        st.StopFlags.Remove("river_crossing_success");
        st.StopFlags.Remove("river_crossing_msg");

        RemoveRiverCrossingScreen();

        // Restore travel background now that the river screen is gone
        SetBackground(TravelSystem.TravelBgForState(st));

        // Check for deaths from crossing failure before continuing
        string? fail = GameManager.Instance.CheckFailStates();
        if (fail != null)
        {
            // Show crossing result message first, then transition to the
            // win/loss screen after the player dismisses it.
            _messageQueue.Enqueue("__game_end__:" + fail);
            ShowMessage(msg);
            return;
        }

        _flowState = FlowState.AwaitChoice;
        UpdateHUD();
        ShowMessage(msg);
        // OnMessageDismissed -> ShowChoiceMenu continues normally
    }

    private void OnRiverWaitDayRequested()
    {
        // Advance one rest day so depth context updates for the player
        GameManager.Instance.Rest(1);
        UpdateHUD();

        // Tell the screen to refresh its UI with the new date/weather
        _riverCrossingScreen?.OnWaitDayProcessed();
    }

    // ========================================================================
    // FORT STORE SCREEN
    // ========================================================================

    private void ShowFortStoreScreen()
    {
        _flowState = FlowState.Store;
        HideChoiceMenu();

        _fortStoreScreen = new FortStoreScreen();
        _fortStoreScreen.Initialize(GameManager.Instance.State);
        AddChild(_fortStoreScreen);
        _fortStoreScreen.StoreExited += OnStoreExited;
    }

    private void RemoveFortStoreScreen()
    {
        if (_fortStoreScreen != null)
        {
            _fortStoreScreen.StoreExited -= OnStoreExited;
            _fortStoreScreen.QueueFree();
            _fortStoreScreen = null;
        }
    }

    private void OnStoreExited()
    {
        // Clear soldout flags so next visit to this store re-rolls stock
        EconomySystem.ClearStoreSoldout(
            GameManager.Instance.State,
            GameManager.Instance.State.AtTownStoreKey);

        RemoveFortStoreScreen();

        _flowState = FlowState.AwaitChoice;
        UpdateHUD();
        ShowChoiceMenu();
    }

    // ========================================================================
    // TRAVEL LOOP
    // ========================================================================

    private void StartTravelLoop()
    {
        _flowState = FlowState.Travel;
        PlayMusic("res://assets/audio/OregonTrail2026_Travel_Score_V1a.mp3");
        ExecuteTravelDay();
    }

    private void ExecuteTravelDay()
    {
        var gm = GameManager.Instance;

        string? fail = gm.CheckFailStates();
        if (fail != null)
        {
            HandleGameEnd(fail);
            return;
        }

        // Set background before travel. If pace is rest, show camp art.
        string bg = gm.State.Pace == "rest"
            ? TravelSystem.CampBgForState(gm.State)
            : TravelSystem.TravelBgForState(gm.State);
        SetBackground(bg);

        var info = gm.TravelOneDay();

        if (!string.IsNullOrEmpty(gm.State.LastCard))
        {
            string? eventText = gm.State.LastEvent.GetValueOrDefault("text", null) as string;
            if (eventText != null)
                ShowMessage(eventText, gm.State.LastCard);
        }

        if (gm.State.PendingRepair != null)
        {
            string part = gm.State.PendingRepair["part"] as string ?? "wheel";
            var (success, msg) = RepairSystem.AttemptFieldRepair(gm.State, part);
            ShowMessage(msg);
            gm.State.PendingRepair = null;
        }

        // Trade encounter: show TradeScreen after the encounter card message is dismissed.
        // The encounter card has already been shown via ShowMessage above (LastCard set).
        // We queue the trade screen as a sentinel so it fires after the card dismiss.
        if (gm.State.PendingEncounter != null)
        {
            string encounterType = gm.State.PendingEncounter.GetValueOrDefault("type") as string ?? "";
            if (encounterType == "trade")
            {
                // Stash offer in StopFlags so the sentinel handler can retrieve it
                gm.State.StopFlags["pending_trade_offer"] = gm.State.PendingEncounter;
                _messageQueue.Enqueue("__trade__");
            }
            gm.State.PendingEncounter = null;
        }

        if (gm.State.PendingStopType == "town")
        {
            string townName = gm.State.PendingStopKey ?? "";
            gm.EnterTown(townName);
            PlayMusic("res://assets/audio/OregonTrail2026_General_Fort_Score_V1a.mp3");
            gm.State.PendingStopType = null;
            gm.State.PendingStopKey  = null;

            // Show arrival message with landmark background image
            var lm = Array.Find(GameData.Landmarks, l => l.Name == townName);
            string arrivalText = lm?.ArrivalText ?? $"YOU HAVE REACHED {townName.ToUpper()}.";
            string? bgImg = lm?.BgImage;
            ShowMessage(arrivalText, bgImg);

            // Queue next-stop hint as follow-up message
            if (lm?.NextStopHint != null)
                _messageQueue.Enqueue("__text__:" + lm.NextStopHint);

            // The Dalles: queue route choice trigger after hint is dismissed
            if (townName == "The Dalles" && !gm.State.RouteChoiceMade)
                _messageQueue.Enqueue("__route_choice__");
        }
        else if (gm.State.PendingStopType == "landmark")
        {
            // Scenic/pass/toll landmarks: show arrival text with landmark bg image.
            // No store, no EnterTown, no music change.
            // Mark visited so the map renders the pin at full opacity.
            string landmarkName = gm.State.PendingStopKey ?? "";
            gm.State.PendingStopType = null;
            gm.State.PendingStopKey  = null;

            if (!gm.State.VisitedLandmarks.Contains(landmarkName))
                gm.State.VisitedLandmarks.Add(landmarkName);

            var lm = Array.Find(GameData.Landmarks, l => l.Name == landmarkName);
            string arrivalText = lm?.ArrivalText ?? $"YOU HAVE REACHED {landmarkName.ToUpper()}.";
            string? bgImg = lm?.BgImage;
            ShowMessage(arrivalText, bgImg);

            // Queue next-stop hint if available
            if (lm?.NextStopHint != null)
                _messageQueue.Enqueue("__text__:" + lm.NextStopHint);
        }
        else if (gm.State.PendingStopType == "river")
        {
            string riverKey = gm.State.PendingStopKey ?? "";
            var river = Array.Find(GameData.Rivers, r => r.Key == riverKey);
            if (river != null)
            {
                gm.State.PendingStopType = null;
                gm.State.PendingStopKey  = null;
                ShowRiverCrossingScreen(river);
                return; // Flow resumes via OnRiverCrossingComplete
            }
            gm.State.PendingStopType = null;
            gm.State.PendingStopKey  = null;
        }

        fail = gm.CheckFailStates();
        if (fail != null)
        {
            HandleGameEnd(fail);
            return;
        }

        _flowState = FlowState.AwaitChoice;
        UpdateHUD();

        // If no message was triggered this day, go straight to choice menu
        if (!_awaitingClick)
            ShowChoiceMenu();
    }

    private void HandleGameEnd(string reason)
    {
        HideChoiceMenu();

        if (reason == "chapter_complete")
        {
            _flowState = FlowState.Victory;
            ShowVictoryScreen();
        }
        else
        {
            _flowState = FlowState.GameOver;
            ShowGameOverScreen(reason);
        }
    }

    // ========================================================================
    // INPUT HANDLING
    // ========================================================================

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            // F12: DevConsole (only during active gameplay, not splash/menu/setup)
            if (keyEvent.Keycode == Key.F12 && IsInGameplay())
            {
                _devConsole?.Activate();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_awaitingClick)
            {
                if (_messageQueue.Count > 0)
                {
                    string next = _messageQueue.Dequeue();
                    if (next.StartsWith("__game_end__:"))
                    {
                        _messagePanel.Visible = false;
                        _awaitingClick = false;
                        HandleGameEnd(next["__game_end__:".Length..]);
                    }
                    else if (next == "__route_choice__")
                    {
                        _messagePanel.Visible = false;
                        _awaitingClick = false;
                        ShowRouteChoiceScreen();
                    }
                    else if (next == "__trade__")
                    {
                        _messagePanel.Visible = false;
                        _awaitingClick = false;
                        ShowTradeScreen();
                    }
                    else if (next.StartsWith("__text__:"))
                    {
                        ShowMessage(next["__text__:".Length..]);
                    }
                    else
                    {
                        ShowMessage(next);
                    }
                }
                else
                {
                    _messagePanel.Visible = false;
                    _awaitingClick = false;
                    OnMessageDismissed();
                }
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    /// <summary>
    /// Returns true if the game is in an active gameplay state where
    /// the dev console should be accessible.
    /// </summary>
    private bool IsInGameplay()
    {
        return _flowState is FlowState.Travel
            or FlowState.AwaitChoice
            or FlowState.Store
            or FlowState.Rest
            or FlowState.Hunt
            or FlowState.Fish
            or FlowState.River
            or FlowState.GameOver
            or FlowState.Victory;
    }

    private void HandleChoice(int choice)
    {
        HideChoiceMenu();
        var gm = GameManager.Instance;

        switch (choice)
        {
            case 1:
                gm.LeaveTown();
                gm.State.LastCard = "";
                gm.State.LastEvent.Clear();
                ExecuteTravelDay();
                break;
            case 2:
                ShowMapScreen();
                break;
            case 3:
                if (!string.IsNullOrEmpty(gm.State.AtTownStoreKey))
                    ShowFortStoreScreen();
                else
                    ShowMessage("NO STORE HERE.");
                break;
            case 4:
                ShowPaceRationsPanel();
                break;
            case 5:
                if (gm.State.Supplies.GetValueOrDefault("bullets", 0) <= 0)
                    ShowMessage("NO AMMO. LOAD UP BEFORE HUNTING.");
                else
                    ShowHuntScreen();
                break;
            case 6:
                ShowFishScreen();
                break;
            case 7:
                gm.QuickSave();
                ShowMessage("GAME SAVED.");
                break;
            case 8:
                ShowRolesScreen();
                break;
        }

        UpdateHUD();
    }

    private void OnMessageDismissed()
    {
        if (_flowState == FlowState.Setup)
        {
            StartTravelLoop();
        }
        else if (_flowState == FlowState.AwaitChoice)
        {
            ShowChoiceMenu();
        }
        else if (_flowState == FlowState.GameOver)
        {
            ShowMainMenu();
        }
        else if (_flowState == FlowState.Victory)
        {
            ShowMainMenu();
        }
    }

    // ========================================================================
    // HUNT SCREEN
    // ========================================================================

    private void ShowHuntScreen()
    {
        _flowState = FlowState.Hunt;
        HideChoiceMenu();

        _huntScreen = new HuntScreen();
        _huntScreen.Initialize(GameManager.Instance.State);
        AddChild(_huntScreen);
        _huntScreen.HuntComplete += OnHuntComplete;
    }

    private void RemoveHuntScreen()
    {
        if (_huntScreen != null)
        {
            _huntScreen.HuntComplete -= OnHuntComplete;
            _huntScreen.QueueFree();
            _huntScreen = null;
        }
    }

    private void OnHuntComplete()
    {
        var st = GameManager.Instance.State;
        int meat = st.StopFlags.GetValueOrDefault("hunt_meat_added") as int? ?? 0;
        int ammo = st.StopFlags.GetValueOrDefault("hunt_ammo_used")  as int? ?? 0;
        st.StopFlags.Remove("hunt_meat_added");
        st.StopFlags.Remove("hunt_ammo_used");

        RemoveHuntScreen();

        string msg = meat > 0
            ? $"YOU RETURNED WITH {meat} LBS OF MEAT. USED {ammo} BULLETS."
            : $"EMPTY HANDED. USED {ammo} BULLETS.";

        string? fail = GameManager.Instance.CheckFailStates();
        if (fail != null) { HandleGameEnd(fail); return; }

        UpdateHUD();
        _flowState = FlowState.AwaitChoice;
        ShowMessage(msg);
    }

    // ========================================================================
    // FISH SCREEN
    // ========================================================================

    private void ShowFishScreen()
    {
        _flowState = FlowState.Fish;
        HideChoiceMenu();

        _fishScreen = new FishScreen();
        _fishScreen.Initialize(GameManager.Instance.State);
        AddChild(_fishScreen);
        _fishScreen.FishComplete += OnFishComplete;
    }

    private void RemoveFishScreen()
    {
        if (_fishScreen != null)
        {
            _fishScreen.FishComplete -= OnFishComplete;
            _fishScreen.QueueFree();
            _fishScreen = null;
        }
    }

    private void OnFishComplete()
    {
        var st = GameManager.Instance.State;
        int added = st.StopFlags.GetValueOrDefault("fish_added") as int? ?? 0;
        st.StopFlags.Remove("fish_added");

        RemoveFishScreen();

        string msg = added > 0
            ? $"YOU CAUGHT {added} LBS OF FISH."
            : "THE FISH WEREN'T BITING TODAY.";

        string? fail = GameManager.Instance.CheckFailStates();
        if (fail != null) { HandleGameEnd(fail); return; }

        UpdateHUD();
        _flowState = FlowState.AwaitChoice;
        ShowMessage(msg);
    }

    // ========================================================================
    // MAP SCREEN
    // ========================================================================

    private void ShowMapScreen()
    {
        HideChoiceMenu();

        _mapScreen = new MapScreen();
        _mapScreen.Initialize(GameManager.Instance.State);
        AddChild(_mapScreen);
        _mapScreen.MapClosed += OnMapClosed;
    }

    private void RemoveMapScreen()
    {
        if (_mapScreen != null)
        {
            _mapScreen.MapClosed -= OnMapClosed;
            _mapScreen.QueueFree();
            _mapScreen = null;
        }
    }

    private void OnMapClosed()
    {
        RemoveMapScreen();
        ShowChoiceMenu();
    }

    // ========================================================================
    // VICTORY SCREEN
    // ========================================================================

    private void ShowVictoryScreen()
    {
        SetBackground("res://assets/images/bg/bg_oregon_city_arrival.webp");
        HideHUD();

        _victoryScreen = new VictoryScreen();
        _victoryScreen.Initialize(GameManager.Instance.State);
        AddChild(_victoryScreen);
        _victoryScreen.PlayAgainRequested += OnEndScreenPlayAgain;
        _victoryScreen.MainMenuRequested  += OnEndScreenMainMenu;
    }

    private void RemoveVictoryScreen()
    {
        if (_victoryScreen != null)
        {
            _victoryScreen.PlayAgainRequested -= OnEndScreenPlayAgain;
            _victoryScreen.MainMenuRequested  -= OnEndScreenMainMenu;
            _victoryScreen.QueueFree();
            _victoryScreen = null;
        }
    }

    // ========================================================================
    // GAME OVER SCREEN
    // ========================================================================

    private void ShowGameOverScreen(string reason)
    {
        HideHUD();

        _gameOverScreen = new GameOverScreen();
        _gameOverScreen.Initialize(GameManager.Instance.State, reason);
        AddChild(_gameOverScreen);
        _gameOverScreen.PlayAgainRequested += OnEndScreenPlayAgain;
        _gameOverScreen.MainMenuRequested  += OnEndScreenMainMenu;
    }

    private void RemoveGameOverScreen()
    {
        if (_gameOverScreen != null)
        {
            _gameOverScreen.PlayAgainRequested -= OnEndScreenPlayAgain;
            _gameOverScreen.MainMenuRequested  -= OnEndScreenMainMenu;
            _gameOverScreen.QueueFree();
            _gameOverScreen = null;
        }
    }

    // Shared end-screen handlers
    private void OnEndScreenPlayAgain()
    {
        RemoveVictoryScreen();
        RemoveGameOverScreen();
        ShowSetupScreen(); // re-enter party setup for a fresh run
    }

    private void OnEndScreenMainMenu()
    {
        RemoveVictoryScreen();
        RemoveGameOverScreen();
        ShowMainMenu();
    }

    // ========================================================================
    // TRADE SCREEN
    // ========================================================================

    private void ShowTradeScreen()
    {
        var gm    = GameManager.Instance;
        var offer = gm.State.StopFlags.GetValueOrDefault("pending_trade_offer")
                    as System.Collections.Generic.Dictionary<string, object>;
        gm.State.StopFlags.Remove("pending_trade_offer");

        if (offer == null)
        {
            // No offer data; fall through to choice menu
            _flowState = FlowState.AwaitChoice;
            ShowChoiceMenu();
            return;
        }

        HideChoiceMenu();
        _tradeScreen = new TradeScreen();
        _tradeScreen.Initialize(gm.State, offer);
        AddChild(_tradeScreen);
        _tradeScreen.TradeResolved += OnTradeResolved;
    }

    private void RemoveTradeScreen()
    {
        if (_tradeScreen != null)
        {
            _tradeScreen.TradeResolved -= OnTradeResolved;
            _tradeScreen.QueueFree();
            _tradeScreen = null;
        }
    }

    private void OnTradeResolved(bool accepted, string resultMessage)
    {
        RemoveTradeScreen();
        UpdateHUD();
        ShowMessage(resultMessage,
            accepted
                ? "res://assets/images/events/evt_enc_trade.webp"
                : null);
        // OnMessageDismissed -> ShowChoiceMenu (FlowState.AwaitChoice already set)
    }

    // ========================================================================
    // PACE / RATIONS PANEL
    // ========================================================================

    private void ShowPaceRationsPanel()
    {
        HideChoiceMenu();

        _pacePanel = new PaceRationsPanel();
        _pacePanel.Initialize(GameManager.Instance.State);
        AddChild(_pacePanel);
        _pacePanel.Confirmed += OnPaceRationsConfirmed;
    }

    private void RemovePaceRationsPanel()
    {
        if (_pacePanel != null)
        {
            _pacePanel.Confirmed -= OnPaceRationsConfirmed;
            _pacePanel.QueueFree();
            _pacePanel = null;
        }
    }

    private void OnPaceRationsConfirmed()
    {
        RemovePaceRationsPanel();
        UpdateHUD();
        ShowChoiceMenu();
    }

    // ========================================================================
    // ROUTE CHOICE SCREEN
    // ========================================================================

    private void ShowRouteChoiceScreen()
    {
        _flowState = FlowState.AwaitChoice;
        HideChoiceMenu();

        _routeChoiceScreen = new RouteChoiceScreen();
        _routeChoiceScreen.Initialize(GameManager.Instance.State);
        AddChild(_routeChoiceScreen);
        _routeChoiceScreen.RouteChosen += OnRouteChosen;
    }

    private void RemoveRouteChoiceScreen()
    {
        if (_routeChoiceScreen != null)
        {
            _routeChoiceScreen.RouteChosen -= OnRouteChosen;
            _routeChoiceScreen.QueueFree();
            _routeChoiceScreen = null;
        }
    }

    private void OnRouteChosen(string choice)
    {
        RemoveRouteChoiceScreen();
        UpdateHUD();

        string msg = choice == "barlow"
            ? $"BARLOW ROAD CHOSEN. $5 TOLL PAID. THE ROAD IS ROUGH BUT DRY."
            : "COLUMBIA RIVER ROUTE CHOSEN. PREPARE FOR THE CROSSING AHEAD.";
        ShowMessage(msg);
        // After message dismiss, OnMessageDismissed -> ShowChoiceMenu (FlowState.AwaitChoice)
    }

    // ========================================================================
    // ROLES SCREEN
    // ========================================================================

    private void ShowRolesScreen()
    {
        HideChoiceMenu();

        _rolesScreen = new RolesScreen();
        _rolesScreen.Initialize(GameManager.Instance.State);
        AddChild(_rolesScreen);
        _rolesScreen.RolesConfirmed += OnRolesConfirmed;
    }

    private void RemoveRolesScreen()
    {
        if (_rolesScreen != null)
        {
            _rolesScreen.RolesConfirmed -= OnRolesConfirmed;
            _rolesScreen.QueueFree();
            _rolesScreen = null;
        }
    }

    private void OnRolesConfirmed()
    {
        RemoveRolesScreen();
        UpdateHUD();
        ShowChoiceMenu();
    }

    // ========================================================================
    // UI HELPERS
    // ========================================================================

    private void HideHUD()
    {
        _hud.Visible = false;
        _messagePanel.Visible = false;
        _choicePanel.Visible = false;
    }

    private void ShowHUD()
    {
        _hud.Visible = true;
    }

    private void SetBackground(string path)
    {
        if (ResourceLoader.Exists(path))
            _background.Texture = GD.Load<Texture2D>(path);
    }

    private void ShowMessage(string text, string? cardImagePath = null)
    {
        _messageLabel.Text = text;

        bool hasImage = !string.IsNullOrEmpty(cardImagePath)
                        && ResourceLoader.Exists(cardImagePath);
        _cardImage.Visible = hasImage;
        if (hasImage)
        {
            _cardImage.Texture = GD.Load<Texture2D>(cardImagePath!);
            // Expand panel when image is shown, shrink when not
            _messagePanel.SetOffset(Side.Top, -280);
        }
        else
        {
            _messagePanel.SetOffset(Side.Top, -140);
        }

        _messagePanel.Visible = true;
        _awaitingClick = true;
        HideChoiceMenu();
    }

    private void PlayMusic(string path)
    {
        if (ResourceLoader.Exists(path))
        {
            _audioPlayer.Bus = "Music";
            _audioPlayer.Stream = GD.Load<AudioStream>(path);
            _audioPlayer.Play();
        }
    }

    private void OnStateChanged()
    {
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        var st = GameManager.Instance.State;
        if (st == null) return;

        _dateLabel.Text = DateCalc.DateStr(st.Day);
        _weatherLabel.Text = st.Weather.ToUpper();
        _milesLabel.Text = $"MILES: {st.Miles}/{GameConstants.TargetMiles}";
        _cashLabel.Text = $"${st.Cash:F2}";
        _foodLabel.Text = $"FOOD: {st.Supplies.GetValueOrDefault("food", 0)}";

        var living = st.Living();
        if (living.Count > 0)
        {
            int avgHealth = (int)living.Average(p => p.Health);
            string healthStr = avgHealth switch
            {
                > 800 => "GOOD",
                > 500 => "FAIR",
                > 200 => "POOR",
                > 0 => "VERY POOR",
                _ => "CRITICAL",
            };
            _healthLabel.Text = $"HEALTH: {healthStr}";

            // Color code health
            Color healthColor = avgHealth switch
            {
                > 800 => UIKit.ColGreen,
                > 500 => UIKit.ColAmber,
                > 200 => new Color("CC7733"),
                _ => UIKit.ColRed,
            };
            _healthLabel.AddThemeColorOverride("font_color", healthColor);
        }

        // Pace
        string paceStr = st.Pace.ToUpper() switch
        {
            "REST"     => "REST",
            "GRUELING" => "GRUELING",
            _          => "STEADY",
        };
        _paceLabel.Text = $"PACE: {paceStr}";
        _paceLabel.AddThemeColorOverride("font_color", paceStr switch
        {
            "REST"     => UIKit.ColGreen,
            "GRUELING" => UIKit.ColRed,
            _          => UIKit.ColParchment,
        });

        // Rations
        string ratStr = st.Rations.ToLower() switch
        {
            "bare" or "bare bones" or "barebones" => "BARE",
            "meager" or "meagre"                  => "MEAGER",
            _                                     => "FILLING",
        };
        _rationsLabel.Text = $"RATIONS: {ratStr}";
        _rationsLabel.AddThemeColorOverride("font_color", ratStr switch
        {
            "BARE"   => UIKit.ColRed,
            "MEAGER" => UIKit.ColAmber,
            _        => UIKit.ColGreen,
        });
    }
}
