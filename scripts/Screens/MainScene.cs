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
    // Node references
    private TextureRect _background = null!;
    private AudioStreamPlayer _audioPlayer = null!;
    private PanelContainer _messagePanel = null!;
    private Label _messageLabel = null!;
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
    private DevConsole? _devConsole;

    public override void _Ready()
    {
        // Get node references
        _background = GetNode<TextureRect>("Background");
        _audioPlayer = GetNode<AudioStreamPlayer>("AudioPlayer");
        _messagePanel = GetNode<PanelContainer>("UILayer/MessagePanel");
        _messageLabel = GetNode<Label>("UILayer/MessagePanel/MessageLabel");
        _dateLabel = GetNode<Label>("UILayer/HUD/TopBar/DateLabel");
        _weatherLabel = GetNode<Label>("UILayer/HUD/TopBar/WeatherLabel");
        _milesLabel = GetNode<Label>("UILayer/HUD/TopBar/MilesLabel");
        _cashLabel = GetNode<Label>("UILayer/HUD/TopBar/CashLabel");
        _foodLabel = GetNode<Label>("UILayer/HUD/TopBar/FoodLabel");
        _healthLabel = GetNode<Label>("UILayer/HUD/TopBar/HealthLabel");

        // Connect to GameManager signals
        GameManager.Instance.StateChanged += OnStateChanged;

        // Create dev console (always exists, just hidden)
        _devConsole = new DevConsole();
        AddChild(_devConsole);

        // Start with splash screen
        ShowSplashScreen();
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
            ShowChoiceMenuHint();
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

        ShowHUD();

        var nameList = new System.Collections.Generic.List<string>(names);
        GameManager.Instance.StartNewGame(occupation, nameList);
        GameManager.Instance.EnterTown("Independence");

        SetBackground("res://assets/images/bg/bg_independence_street.webp");
        ShowMessage("INDEPENDENCE, MISSOURI. 1850.");
        _messageQueue.Enqueue("YOU MUST BUY SUPPLIES BEFORE YOU LEAVE.");
        _messageQueue.Enqueue("[Press SPACE to continue]");

        PlayMusic("res://assets/audio/OregonTrail2026_Main_Menu_Score_V1b.mp3");
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
                ShowMessage($"YOU MUST CROSS THE {river.Name.ToUpper()}.");
                var (success, msg) = RiverSystem.AttemptCrossing(gm.State, river, RiverSystem.CrossingMethod.Ford);
                _messageQueue.Enqueue(msg);
            }
            gm.State.PendingStopType = null;
            gm.State.PendingStopKey = null;
        }

        fail = gm.CheckFailStates();
        if (fail != null)
        {
            HandleGameEnd(fail);
            return;
        }

        _flowState = FlowState.AwaitChoice;
        UpdateHUD();
    }

    private void HandleGameEnd(string reason)
    {
        var gm = GameManager.Instance;
        _flowState = FlowState.GameOver;

        string message = reason switch
        {
            "game_over_dead" => "EVERYONE IS DEAD.",
            "game_over_unconscious" => "THE PARTY FELL UNCONSCIOUS AND NEVER RECOVERED.",
            "game_over_starved" => "YOU RAN OUT OF FOOD FOR TOO LONG.\nTHE PARTY STARVED ON THE TRAIL.",
            "game_over_stranded" => "YOU CANNOT MOVE ON.\nWITHOUT OXEN OR A WORKING WAGON, THE JOURNEY ENDS HERE.",
            "game_over_time" => "WINTER CAME. YOU RAN OUT OF TIME.",
            "chapter_complete" => "WILLAMETTE VALLEY. YOU MADE IT!\nSURVIVORS CONTINUE TO CHAPTER 2.",
            _ => $"GAME OVER: {reason}",
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

            if (_flowState == FlowState.AwaitChoice)
            {
                HandleChoiceInput(keyEvent);
                GetViewport().SetInputAsHandled();
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

    private void HandleChoiceInput(InputEventKey key)
    {
        var gm = GameManager.Instance;

        int choice = key.Keycode switch
        {
            Key.Key1 => 1,
            Key.Key2 => 2,
            Key.Key3 => 3,
            Key.Key4 => 4,
            Key.Key5 => 5,
            Key.Key6 => 6,
            Key.Key7 => 7,
            Key.Key8 => 8,
            Key.Key9 => 9,
            _ => 0,
        };

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
                    ShowMessage("STORE SCREEN - USE S KEY TO BUY SUPPLIES");
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
            ShowChoiceMenuHint();
        }
        else if (_flowState == FlowState.GameOver)
        {
            // Return to main menu after game over
            ShowMainMenu();
        }
    }

    private void ShowChoiceMenuHint()
    {
        var gm = GameManager.Instance;
        string location = !string.IsNullOrEmpty(gm.State.AtTownName) ? $" [{gm.State.AtTownName}]" : "";
        ShowMessage(
            $"WHAT IS YOUR CHOICE?{location}\n" +
            "1.CONTINUE  2.MAP  3.STORE  4.REST\n" +
            "5.HUNT  6.FISH  7.SAVE  8.ROLES");
    }

    // ========================================================================
    // UI HELPERS
    // ========================================================================

    private void HideHUD()
    {
        var hud = GetNodeOrNull("UILayer/HUD");
        if (hud != null) hud.Set("visible", false);
        _messagePanel.Visible = false;
    }

    private void ShowHUD()
    {
        var hud = GetNodeOrNull("UILayer/HUD");
        if (hud != null) hud.Set("visible", true);
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
        }
    }
}
