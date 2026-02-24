#nullable enable

namespace OregonTrail2026.Utils;

/// <summary>
/// Translation Key constants. Every user-facing string is referenced by a
/// key defined here. Usage: Tr(TK.MenuNewGame) inside any Godot Node.
/// For static contexts use: TranslationServer.Translate(TK.Key)
///
/// Keys follow the pattern: CATEGORY_DESCRIPTION
/// Format-string keys use {0}, {1}, ... placeholders compatible with
/// string.Format(Tr(TK.Key), arg0, arg1).
///
/// All keys MUST have at least an English value in translations.csv.
/// Multi-line values in the CSV use \n (which TranslationLoader unescapes).
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
    public const string SetupTrailTitle     = "SETUP_TRAIL_TITLE";
    public const string SetupTrailIntro     = "SETUP_TRAIL_INTRO";
    public const string SetupChooseHint     = "SETUP_CHOOSE_HINT";
    public const string SetupNameQuestion1  = "SETUP_NAME_QUESTION_1";
    public const string SetupNameQuestion2  = "SETUP_NAME_QUESTION_2";
    public const string SetupTabHint        = "SETUP_TAB_HINT";
    public const string SetupEnterName      = "SETUP_ENTER_NAME";
    public const string SetupBankerTitle    = "SETUP_BANKER_TITLE";
    public const string SetupCarpenterTitle = "SETUP_CARPENTER_TITLE";
    public const string SetupFarmerTitle    = "SETUP_FARMER_TITLE";
    public const string SetupBankerDesc     = "SETUP_BANKER_DESC";
    public const string SetupCarpenterDesc  = "SETUP_CARPENTER_DESC";
    public const string SetupFarmerDesc     = "SETUP_FARMER_DESC";

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
    public const string CommonApply         = "COMMON_APPLY";
    public const string CommonConfirm       = "COMMON_CONFIRM";
    public const string CommonMainMenu      = "COMMON_MAIN_MENU";
    public const string CommonPlayAgain     = "COMMON_PLAY_AGAIN";
    public const string CommonTryAgain      = "COMMON_TRY_AGAIN";
    public const string CommonReturnCamp    = "COMMON_RETURN_CAMP";

    // ========================================================================
    // TRAVEL / HUD (MainScene)
    // ========================================================================
    public const string TravelChoiceTitle   = "TRAVEL_CHOICE_TITLE";
    public const string TravelContinue      = "TRAVEL_CONTINUE";
    public const string TravelCheckMap      = "TRAVEL_CHECK_MAP";
    public const string TravelVisitStore    = "TRAVEL_VISIT_STORE";
    public const string TravelPaceRations   = "TRAVEL_PACE_RATIONS";
    public const string TravelGoHunting     = "TRAVEL_GO_HUNTING";
    public const string TravelGoFishing     = "TRAVEL_GO_FISHING";
    public const string TravelSaveGame      = "TRAVEL_SAVE_GAME";
    public const string TravelRoles         = "TRAVEL_ROLES";
    public const string TravelGameSaved     = "TRAVEL_GAME_SAVED";
    public const string TravelNoStore       = "TRAVEL_NO_STORE";
    public const string TravelNoAmmo        = "TRAVEL_NO_AMMO";
    public const string WagonGood           = "WAGON_GOOD";
    public const string WagonFair           = "WAGON_FAIR";
    public const string WagonPoor           = "WAGON_POOR";
    public const string WagonVeryPoor       = "WAGON_VERY_POOR";
    public const string WagonCritical       = "WAGON_CRITICAL";
    public const string HudCash             = "HUD_CASH";
    public const string HudMiles            = "HUD_MILES";
    public const string HudFood             = "HUD_FOOD";
    public const string HudHealth           = "HUD_HEALTH";
    public const string HudPace             = "HUD_PACE";
    public const string HudRations          = "HUD_RATIONS";

    // ========================================================================
    // PACE AND RATIONS (PaceRationsPanel)
    // ========================================================================
    public const string PaceTitle           = "PACE_TITLE";
    public const string PaceFoodRemaining   = "PACE_FOOD_REMAINING";
    public const string PacePartySize       = "PACE_PARTY_SIZE";
    public const string PacePaceHeader      = "PACE_PACE_HEADER";
    public const string PaceRationsHeader   = "PACE_RATIONS_HEADER";
    public const string PaceRest            = "PACE_REST";
    public const string PaceSteady          = "PACE_STEADY";
    public const string PaceGrueling        = "PACE_GRUELING";
    public const string PaceRestDesc        = "PACE_REST_DESC";
    public const string PaceSteadyDesc      = "PACE_STEADY_DESC";
    public const string PaceGruelingDesc    = "PACE_GRUELING_DESC";
    public const string RationsBare         = "RATIONS_BARE";
    public const string RationsMeager       = "RATIONS_MEAGER";
    public const string RationsFilling      = "RATIONS_FILLING";
    public const string RationsBareLabel    = "RATIONS_BARE_LABEL";
    public const string RationsBareDesc     = "RATIONS_BARE_DESC";
    public const string RationsMeagerDesc   = "RATIONS_MEAGER_DESC";
    public const string RationsFillingDesc  = "RATIONS_FILLING_DESC";
    public const string SuffixLbs           = "SUFFIX_LBS";
    public const string SuffixAlive         = "SUFFIX_ALIVE";

    // ========================================================================
    // INDEPENDENCE SCREEN
    // ========================================================================
    public const string IndTitle            = "IND_TITLE";
    public const string IndYear             = "IND_YEAR";
    public const string IndIntro            = "IND_INTRO";
    public const string IndTreasury         = "IND_TREASURY";
    public const string IndChooseMonth      = "IND_CHOOSE_MONTH";
    public const string IndStoreName        = "IND_STORE_NAME";
    public const string IndStoreHint        = "IND_STORE_HINT";
    public const string IndDepart           = "IND_DEPART";
    public const string IndNeedOxen         = "IND_NEED_OXEN";
    public const string IndMonthMarch       = "IND_MONTH_MARCH";
    public const string IndMonthApril       = "IND_MONTH_APRIL";
    public const string IndMonthMay         = "IND_MONTH_MAY";
    public const string IndMonthJune        = "IND_MONTH_JUNE";
    public const string IndMonthJuly        = "IND_MONTH_JULY";
    public const string IndMonthDescMarch   = "IND_MONTH_DESC_MARCH";
    public const string IndMonthDescApril   = "IND_MONTH_DESC_APRIL";
    public const string IndMonthDescMay     = "IND_MONTH_DESC_MAY";
    public const string IndMonthDescJune    = "IND_MONTH_DESC_JUNE";
    public const string IndMonthDescJuly    = "IND_MONTH_DESC_JULY";
    public const string IndItemOxen         = "IND_ITEM_OXEN";
    public const string IndItemFood         = "IND_ITEM_FOOD";
    public const string IndItemClothes      = "IND_ITEM_CLOTHES";
    public const string IndItemBullets      = "IND_ITEM_BULLETS";
    public const string IndItemWheel        = "IND_ITEM_WHEEL";
    public const string IndItemAxle         = "IND_ITEM_AXLE";
    public const string IndItemTongue       = "IND_ITEM_TONGUE";
    public const string IndRecOxen          = "IND_REC_OXEN";
    public const string IndRecFood          = "IND_REC_FOOD";
    public const string IndRecClothes       = "IND_REC_CLOTHES";
    public const string IndRecBullets       = "IND_REC_BULLETS";
    public const string IndRecWheel         = "IND_REC_WHEEL";
    public const string IndRecAxle          = "IND_REC_AXLE";
    public const string IndRecTongue        = "IND_REC_TONGUE";
    public const string IndPricePerLb       = "IND_PRICE_PER_LB";
    public const string IndPriceEach        = "IND_PRICE_EACH";

    // ========================================================================
    // FORT STORE SCREEN
    // ========================================================================
    public const string StoreCash           = "STORE_CASH";
    public const string StoreCargo          = "STORE_CARGO";
    public const string StoreOwned          = "STORE_OWNED";
    public const string StoreAfflicted      = "STORE_AFFLICTED";
    public const string StoreSecSupplies    = "STORE_SEC_SUPPLIES";
    public const string StoreSecLivestock   = "STORE_SEC_LIVESTOCK";
    public const string StoreSecParts       = "STORE_SEC_PARTS";
    public const string StoreSecMedical     = "STORE_SEC_MEDICAL";
    public const string StoreSecBlacksmith  = "STORE_SEC_BLACKSMITH";
    public const string StoreSoldOut        = "STORE_SOLD_OUT";
    public const string StoreMedSoldOut     = "STORE_MED_SOLD_OUT";
    public const string StoreBuy            = "STORE_BUY";
    public const string StoreCure           = "STORE_CURE";
    public const string StoreLeave          = "STORE_LEAVE";
    public const string StoreNoCash         = "STORE_NO_CASH";
    public const string StoreItemFood       = "STORE_ITEM_FOOD";
    public const string StoreItemAmmo       = "STORE_ITEM_AMMO";
    public const string StoreItemClothes    = "STORE_ITEM_CLOTHES";
    public const string StoreItemOxen       = "STORE_ITEM_OXEN";
    public const string StoreItemWheel      = "STORE_ITEM_WHEEL";
    public const string StoreItemAxle       = "STORE_ITEM_AXLE";
    public const string StoreItemTongue     = "STORE_ITEM_TONGUE";
    public const string StoreUnitPerLb      = "STORE_UNIT_PER_LB";
    public const string StoreUnitPerBox     = "STORE_UNIT_PER_BOX";
    public const string StoreUnitPerSet     = "STORE_UNIT_PER_SET";
    public const string StoreUnitPerYoke    = "STORE_UNIT_PER_YOKE";
    public const string StoreUnitEach       = "STORE_UNIT_EACH";
    public const string SmithInspect        = "SMITH_INSPECT";
    public const string SmithInspectDesc    = "SMITH_INSPECT_DESC";
    public const string SmithRepair         = "SMITH_REPAIR";
    public const string SmithRepairNeeded   = "SMITH_REPAIR_NEEDED";
    public const string SmithRepairOk       = "SMITH_REPAIR_OK";
    public const string SmithTuneup         = "SMITH_TUNEUP";
    public const string SmithTuneupActive   = "SMITH_TUNEUP_ACTIVE";
    public const string SmithTuneupDesc     = "SMITH_TUNEUP_DESC";
    public const string SmithResultWagon    = "SMITH_RESULT_WAGON";
    public const string SmithResultBroken   = "SMITH_RESULT_BROKEN";

    // ========================================================================
    // HUNTING SCREEN
    // ========================================================================
    public const string HuntTitle           = "HUNT_TITLE";
    public const string HuntFire            = "HUNT_FIRE";
    public const string HuntIntro           = "HUNT_INTRO";
    public const string HuntAmmo            = "HUNT_AMMO";
    public const string HuntMeat            = "HUNT_MEAT";
    public const string HuntCap             = "HUNT_CAP";
    public const string HuntHit             = "HUNT_HIT";
    public const string HuntMiss            = "HUNT_MISS";
    public const string HuntPredator        = "HUNT_PREDATOR";
    public const string HuntNoAmmo          = "HUNT_NO_AMMO";
    public const string HuntWagonFull       = "HUNT_WAGON_FULL";

    // ========================================================================
    // FISHING SCREEN
    // ========================================================================
    public const string FishTitle           = "FISH_TITLE";
    public const string FishCast            = "FISH_CAST";
    public const string FishIntro           = "FISH_INTRO";
    public const string FishCasts           = "FISH_CASTS";
    public const string FishCaught          = "FISH_CAUGHT";
    public const string FishHit             = "FISH_HIT";
    public const string FishMiss            = "FISH_MISS";
    public const string FishNoCasts         = "FISH_NO_CASTS";
    public const string FishNearRiver       = "FISH_NEAR_RIVER";
    public const string FishNoRiver         = "FISH_NO_RIVER";

    // ========================================================================
    // MAP SCREEN
    // ========================================================================
    public const string MapTitle            = "MAP_TITLE";
    public const string MapClose            = "MAP_CLOSE";
    public const string MapStatDate         = "MAP_STAT_DATE";
    public const string MapStatMiles        = "MAP_STAT_MILES";
    public const string MapStatTerrain      = "MAP_STAT_TERRAIN";
    public const string MapStatWeather      = "MAP_STAT_WEATHER";
    public const string MapStatCash         = "MAP_STAT_CASH";

    // ========================================================================
    // ROUTE CHOICE SCREEN
    // ========================================================================
    public const string RouteTitle          = "ROUTE_TITLE";
    public const string RouteBarlowName     = "ROUTE_BARLOW_NAME";
    public const string RouteBarlowSub      = "ROUTE_BARLOW_SUB";
    public const string RouteBarlowCost     = "ROUTE_BARLOW_COST";
    public const string RouteSubtitle       = "ROUTE_SUBTITLE";
    public const string RouteColumbiaName   = "ROUTE_COLUMBIA_NAME";
    public const string RouteColumbiaSub1   = "ROUTE_COLUMBIA_SUB1";
    public const string RouteColumbiaSub2   = "ROUTE_COLUMBIA_SUB2";
    public const string RouteRecommended    = "ROUTE_RECOMMENDED";
    public const string RouteIrreversible   = "ROUTE_IRREVERSIBLE";
    public const string RouteStatCash       = "ROUTE_STAT_CASH";
    public const string RouteStatWagon      = "ROUTE_STAT_WAGON";
    public const string RouteStatOxen       = "ROUTE_STAT_OXEN";
    public const string RouteStatOxenCond   = "ROUTE_STAT_OXEN_COND";
    public const string RouteStatFood       = "ROUTE_STAT_FOOD";
    public const string RouteStatSurv       = "ROUTE_STAT_SURV";
    public const string RouteYokes          = "ROUTE_YOKES";
    public const string RouteLbs            = "ROUTE_LBS";

    // ========================================================================
    // RIVER CROSSING SCREEN
    // ========================================================================
    public const string RiverWait           = "RIVER_WAIT";
    public const string RiverCash           = "RIVER_CASH";
    public const string RiverDepth          = "RIVER_DEPTH";
    public const string RiverNoCash         = "RIVER_NO_CASH";

    // ========================================================================
    // ROLES SCREEN
    // ========================================================================
    public const string RolesTitle          = "ROLES_TITLE";
    public const string RolesHint           = "ROLES_HINT";
    public const string RolesConfirm        = "ROLES_CONFIRM";
    public const string RolesDriver         = "ROLES_DRIVER";
    public const string RolesHunter         = "ROLES_HUNTER";
    public const string RolesMedic          = "ROLES_MEDIC";
    public const string RolesScout          = "ROLES_SCOUT";
    public const string RolesDriverDesc     = "ROLES_DRIVER_DESC";
    public const string RolesHunterDesc     = "ROLES_HUNTER_DESC";
    public const string RolesMedicDesc      = "ROLES_MEDIC_DESC";
    public const string RolesScoutDesc      = "ROLES_SCOUT_DESC";

    // ========================================================================
    // TRADE SCREEN
    // ========================================================================
    public const string TradeTitle          = "TRADE_TITLE";
    public const string TradeGives          = "TRADE_GIVES";
    public const string TradeWants          = "TRADE_WANTS";
    public const string TradeAccept         = "TRADE_ACCEPT";
    public const string TradeDecline        = "TRADE_DECLINE";
    public const string TradeDeclined       = "TRADE_DECLINED";
    public const string TradeYourCash       = "TRADE_YOUR_CASH";

    // ========================================================================
    // GAME OVER SCREEN
    // ========================================================================
    public const string GameOverTitle       = "GAME_OVER_TITLE";
    public const string GameOverParty       = "GAME_OVER_PARTY";
    public const string GameOverDistance    = "GAME_OVER_DISTANCE";
    public const string GameOverSurvived    = "GAME_OVER_SURVIVED";
    public const string GameOverDied        = "GAME_OVER_DIED";
    public const string GameOverSoClose     = "GAME_OVER_SO_CLOSE";
    public const string GameOverBlueMtn     = "GAME_OVER_BLUE_MTN";
    public const string GameOverSouthPass   = "GAME_OVER_SOUTH_PASS";
    public const string GameOverHalfway     = "GAME_OVER_HALFWAY";
    public const string GameOverPrairie     = "GAME_OVER_PRAIRIE";
    public const string GameOverEarly       = "GAME_OVER_EARLY";
    public const string GameOverMiles       = "GAME_OVER_MILES";
    public const string GameOverProgress    = "GAME_OVER_PROGRESS";
    public const string GameOverDays        = "GAME_OVER_DAYS";
    public const string GameOverDate        = "GAME_OVER_DATE";
    public const string GameOverCash        = "GAME_OVER_CASH";
    public const string GameOverCauseAll    = "GAME_OVER_CAUSE_ALL";
    public const string GameOverCauseUncon  = "GAME_OVER_CAUSE_UNCON";
    public const string GameOverCauseStarve = "GAME_OVER_CAUSE_STARVE";
    public const string GameOverCauseStr    = "GAME_OVER_CAUSE_STRAND";
    public const string GameOverCauseWinter = "GAME_OVER_CAUSE_WINTER";

    // ========================================================================
    // VICTORY SCREEN
    // ========================================================================
    public const string WinLocation        = "WIN_LOCATION";
    public const string WinSurvivors       = "WIN_SURVIVORS";
    public const string WinNoSurvivors     = "WIN_NO_SURVIVORS";
    public const string WinLost            = "WIN_LOST";
    public const string WinStats           = "WIN_STATS";
    public const string WinMiles           = "WIN_MILES";
    public const string WinDays            = "WIN_DAYS";
    public const string WinDateArrived     = "WIN_DATE_ARRIVED";
    public const string WinCash            = "WIN_CASH";
    public const string WinFood            = "WIN_FOOD";
    public const string WinScore           = "WIN_SCORE";
    public const string WinRankLegendary   = "WIN_RANK_LEGENDARY";
    public const string WinRankSeasoned    = "WIN_RANK_SEASONED";
    public const string WinRankCapable     = "WIN_RANK_CAPABLE";
    public const string WinRankDetermined  = "WIN_RANK_DETERMINED";
    public const string WinRankLucky       = "WIN_RANK_LUCKY";
    public const string WinArrivalDate     = "WIN_ARRIVAL_DATE";
    public const string WinMemberHealth    = "WIN_MEMBER_HEALTH";

    // ========================================================================
    // TRAVEL RESULT MESSAGES (MainScene, previously hardcoded)
    // ========================================================================
    public const string TravelWagonLoaded   = "TRAVEL_WAGON_LOADED";
    public const string TravelReachedPlace  = "TRAVEL_REACHED_PLACE";
    public const string RiverCrossDone      = "RIVER_CROSS_DONE";
    public const string HuntResultMeat      = "HUNT_RESULT_MEAT";
    public const string HuntResultEmpty     = "HUNT_RESULT_EMPTY";
    public const string FishResultCatch     = "FISH_RESULT_CATCH";
    public const string FishResultMiss      = "FISH_RESULT_MISS";
    public const string RouteBarlowChosen   = "ROUTE_BARLOW_CHOSEN";
    public const string RouteColumbiaChosen = "ROUTE_COLUMBIA_CHOSEN";
    public const string GameOverCauseFail   = "GAME_OVER_CAUSE_FAIL";
}
