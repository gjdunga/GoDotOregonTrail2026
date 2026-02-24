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
    private Label _messageLabel = null!;
    private Control _choicePanel = null!;
    private Label _dateLabel = null!;
    private Label _weatherLabel = null!;
    private Label _milesLabel = null!;
    private Label _cashLabel = null!;
    private Label _foodLabel = null!;
    private Label _healthLabel = null!;

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

        // ---- MESSAGE PANEL (bottom center) ----
        _messagePanel = new PanelContainer();
        _messagePanel.Visible = false;
        _messagePanel.SetAnchor(Side.Left, 0.15f);
        _messagePanel.SetAnchor(Side.Right, 0.85f);
        _messagePanel.SetAnchor(Side.Top, 1.0f);
        _messagePanel.SetAnchor(Side.Bottom, 1.0f);
        _messagePanel.SetOffset(Side.Top, -140);
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
        _messagePanel.AddChild(_messageLabel);

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
        row1.AddChild(MakeChoiceButton("REST", 4));

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
            // Show the crossing result first, then the game-over message on next dismiss.
            // Set flow state before ShowMessage so OnMessageDismissed routes correctly.
            _flowState = fail == "chapter_complete" ? FlowState.Victory : FlowState.GameOver;
            SetBackground("res://assets/images/bg/bg_oregon_city_arrival.webp");

            string gameOverMsg = fail switch
            {
                "game_over_dead"        => "EVERYONE IS DEAD.",
                "game_over_unconscious" => "THE PARTY FELL UNCONSCIOUS AND NEVER RECOVERED.",
                "game_over_starved"     => "YOU RAN OUT OF FOOD FOR TOO LONG.\nTHE PARTY STARVED ON THE TRAIL.",
                "game_over_stranded"    => "YOU CANNOT MOVE ON.\nWITHOUT OXEN OR A WORKING WAGON, THE JOURNEY ENDS HERE.",
                "game_over_time"        => "WINTER CAME. YOU RAN OUT OF TIME.",
                "chapter_complete"      => "WILLAMETTE VALLEY. YOU MADE IT!\nSURVIVORS CONTINUE TO CHAPTER 2.",
                _                       => $"GAME OVER: {fail}",
            };

            // Crossing result -> game over msg -> main menu (two dismissals)
            ShowMessage(msg);
            _messageQueue.Enqueue(gameOverMsg);
            _messageQueue.Enqueue($"MILES TRAVELED: {st.Miles}");
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

        string bg = TravelSystem.TravelBgForState(gm.State);
        SetBackground(bg);

        var info = gm.TravelOneDay();

        if (!string.IsNullOrEmpty(gm.State.LastCard))
        {
            string? eventText = gm.State.LastEvent.GetValueOrDefault("text", null) as string;
            if (eventText != null)
                ShowMessage(eventText);
        }

        if (gm.State.PendingRepair != null)
        {
            string part = gm.State.PendingRepair["part"] as string ?? "wheel";
            var (success, msg) = RepairSystem.AttemptFieldRepair(gm.State, part);
            ShowMessage(msg);
            gm.State.PendingRepair = null;
        }

        // Trade encounter: EventSystem sets PendingEncounter but nothing consumed it.
        // Show a simple inline trade result for now and clear the field.
        // Proper trade UI (offer/counter/decline) can replace this block later.
        if (gm.State.PendingEncounter != null)
        {
            string encounterType = gm.State.PendingEncounter.GetValueOrDefault("type") as string ?? "";
            if (encounterType == "trade")
            {
                // Simulate a basic trade: swap food for bullets if player has surplus food
                int playerFood = gm.State.Supplies.GetValueOrDefault("food", 0);
                if (playerFood >= 40)
                {
                    gm.State.Supplies["food"] = playerFood - 30;
                    gm.State.Supplies["bullets"] = gm.State.Supplies.GetValueOrDefault("bullets", 0) + 20;
                    _messageQueue.Enqueue("THE TRADER EXCHANGED 30 LBS OF FOOD FOR 20 BULLETS.");
                }
                else
                {
                    _messageQueue.Enqueue("THE TRADER HAD NOTHING YOU NEEDED. HE MOVED ON.");
                }
            }
            gm.State.PendingEncounter = null;
        }

        if (gm.State.PendingStopType == "town")
        {
            string townName = gm.State.PendingStopKey ?? "";
            gm.EnterTown(townName);
            ShowMessage($"YOU HAVE REACHED {townName.ToUpper()}.");
            PlayMusic("res://assets/audio/OregonTrail2026_General_Fort_Score_V1a.mp3");
            gm.State.PendingStopType = null;
            gm.State.PendingStopKey = null;
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
        var gm = GameManager.Instance;

        // Victory and defeat are distinct flow states. chapter_complete reaches Oregon;
        // all other reasons are failures. Both currently return to main menu on dismiss,
        // but Victory can expand to its own screen without touching routing logic.
        _flowState = reason == "chapter_complete" ? FlowState.Victory : FlowState.GameOver;

        string message = reason switch
        {
            "game_over_dead"        => "EVERYONE IS DEAD.",
            "game_over_unconscious" => "THE PARTY FELL UNCONSCIOUS AND NEVER RECOVERED.",
            "game_over_starved"     => "YOU RAN OUT OF FOOD FOR TOO LONG.\nTHE PARTY STARVED ON THE TRAIL.",
            "game_over_stranded"    => "YOU CANNOT MOVE ON.\nWITHOUT OXEN OR A WORKING WAGON, THE JOURNEY ENDS HERE.",
            "game_over_time"        => "WINTER CAME. YOU RAN OUT OF TIME.",
            "chapter_complete"      => "WILLAMETTE VALLEY. YOU MADE IT!\nSURVIVORS CONTINUE TO CHAPTER 2.",
            _                       => $"GAME OVER: {reason}",
        };

        SetBackground("res://assets/images/bg/bg_oregon_city_arrival.webp");
        ShowMessage(message);
        _messageQueue.Enqueue($"MILES TRAVELED: {gm.State.Miles}");
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
                    ShowMessage(_messageQueue.Dequeue());
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
                ShowMessage($"MILES: {gm.State.Miles} / {GameConstants.TargetMiles}\n" +
                           $"TERRAIN: {TravelSystem.TerrainByMiles(gm.State.Miles).ToUpper()}");
                break;
            case 3:
                if (!string.IsNullOrEmpty(gm.State.AtTownStoreKey))
                    ShowMessage("STORE SCREEN: COMING SOON");
                else
                    ShowMessage("NO STORE HERE.");
                break;
            case 4:
                gm.Rest(1);
                ShowMessage("YOU REST FOR 1 DAY.");
                break;
            case 5:
                if (gm.State.Supplies.GetValueOrDefault("bullets", 0) <= 0)
                    ShowMessage("NO AMMO.");
                else
                {
                    int meat = GameManager.RandInt(20, 120);
                    gm.State.Supplies["bullets"] = Math.Max(0, gm.State.Supplies["bullets"] - GameManager.RandInt(5, 15));
                    CargoSystem.AddFoodWithCapacity(gm.State, meat);
                    ShowMessage($"YOU BROUGHT BACK {meat} LBS OF FOOD.");
                }
                break;
            case 6:
                int fish = GameManager.RandInt(GameConstants.FishYieldMin, GameConstants.FishYieldMax);
                CargoSystem.AddFoodWithCapacity(gm.State, fish);
                ShowMessage($"YOU CAUGHT {fish} LBS OF FISH.");
                break;
            case 7:
                gm.QuickSave();
                ShowMessage("GAME SAVED.");
                break;
            case 8:
                ShowMessage("ROLES: DRIVER, HUNTER, MEDIC, SCOUT");
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
            // Return to main menu after defeat
            ShowMainMenu();
        }
        else if (_flowState == FlowState.Victory)
        {
            // Return to main menu after victory (placeholder until Victory screen exists)
            ShowMainMenu();
        }
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

    private void ShowMessage(string text)
    {
        _messageLabel.Text = text;
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
    }
}
