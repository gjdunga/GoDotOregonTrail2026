#nullable enable

namespace OregonTrail2026.Utils;

/// <summary>
/// Translation Key constants. Every user-facing string in the game
/// is referenced by a key defined here. Usage: Tr(TK.MenuNewGame)
///
/// Keys follow the pattern: CATEGORY_DESCRIPTION
/// New keys MUST be added to translations.csv with at least an English value.
/// </summary>
public static class TK
{
    // ========================================================================
    // SPLASH SCREEN
    // ========================================================================
    public const string SplashLoading1  = "SPLASH_LOADING_1";
    public const string SplashLoading2  = "SPLASH_LOADING_2";
    public const string SplashLoading3  = "SPLASH_LOADING_3";
    public const string SplashLoading4  = "SPLASH_LOADING_4";
    public const string SplashLoading5  = "SPLASH_LOADING_5";
    public const string SplashLoading6  = "SPLASH_LOADING_6";
    public const string SplashLoading7  = "SPLASH_LOADING_7";
    public const string SplashLoading8  = "SPLASH_LOADING_8";
    public const string SplashLoading9  = "SPLASH_LOADING_9";
    public const string SplashLoading10 = "SPLASH_LOADING_10";
    public const string SplashPressKey  = "SPLASH_PRESS_KEY";
    public const string SplashEscQuit   = "SPLASH_ESC_QUIT";

    // ========================================================================
    // MAIN MENU
    // ========================================================================
    public const string MenuNewGame     = "MENU_NEW_GAME";
    public const string MenuLoadGame    = "MENU_LOAD_GAME";
    public const string MenuDeleteGame  = "MENU_DELETE_GAME";
    public const string MenuGraphics    = "MENU_GRAPHICS";
    public const string MenuSound       = "MENU_SOUND";
    public const string MenuQuit        = "MENU_QUIT";

    // ========================================================================
    // SETTINGS
    // ========================================================================
    public const string SettingsFullscreen   = "SETTINGS_FULLSCREEN";
    public const string SettingsWindowed     = "SETTINGS_WINDOWED";
    public const string SettingsMasterVol    = "SETTINGS_MASTER_VOL";
    public const string SettingsMusicVol     = "SETTINGS_MUSIC_VOL";
    public const string SettingsSfxVol       = "SETTINGS_SFX_VOL";
    public const string SettingsLanguage     = "SETTINGS_LANGUAGE";
    public const string SettingsBack         = "SETTINGS_BACK";
    public const string SettingsOn           = "SETTINGS_ON";
    public const string SettingsOff          = "SETTINGS_OFF";

    // ========================================================================
    // PARTY SETUP
    // ========================================================================
    public const string SetupChooseOcc      = "SETUP_CHOOSE_OCC";
    public const string SetupBanker         = "SETUP_BANKER";
    public const string SetupCarpenter      = "SETUP_CARPENTER";
    public const string SetupFarmer         = "SETUP_FARMER";
    public const string SetupStartCash      = "SETUP_START_CASH";
    public const string SetupRepairSkill    = "SETUP_REPAIR_SKILL";
    public const string SetupScoreMult      = "SETUP_SCORE_MULT";
    public const string SetupNameParty      = "SETUP_NAME_PARTY";
    public const string SetupWagonLeader    = "SETUP_WAGON_LEADER";
    public const string SetupPartyMember    = "SETUP_PARTY_MEMBER";
    public const string SetupYourParty      = "SETUP_YOUR_PARTY";
    public const string SetupHitTrail       = "SETUP_HIT_TRAIL";
    public const string SetupBack           = "SETUP_BACK";

    // ========================================================================
    // SAVE / LOAD
    // ========================================================================
    public const string SaveSlotEmpty       = "SAVE_SLOT_EMPTY";
    public const string SaveSlotAuto        = "SAVE_SLOT_AUTO";
    public const string SaveDay             = "SAVE_DAY";
    public const string SaveMiles           = "SAVE_MILES";
    public const string SavePartyAlive      = "SAVE_PARTY_ALIVE";
    public const string SaveCorruptRecov    = "SAVE_CORRUPT_RECOV";
    public const string SaveCorruptFail     = "SAVE_CORRUPT_FAIL";
    public const string SaveConfirmDelete   = "SAVE_CONFIRM_DELETE";
    public const string SaveDeleted         = "SAVE_DELETED";

    // ========================================================================
    // COMMON
    // ========================================================================
    public const string CommonYes           = "COMMON_YES";
    public const string CommonNo            = "COMMON_NO";
    public const string CommonOk            = "COMMON_OK";
    public const string CommonCancel        = "COMMON_CANCEL";
    public const string CommonConfirm       = "COMMON_CONFIRM";
}
