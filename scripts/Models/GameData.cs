#nullable enable
using System;
using System.Collections.Generic;

namespace OregonTrail2026.Models;

/// <summary>
/// Static game data tables: illnesses, landmarks, rivers, occupations, pricing, events.
/// Converted from RenPy: OT2026_data.rpy data definitions.
/// </summary>
public static class GameData
{
    // ========================================================================
    // ILLNESSES
    // ========================================================================
    public record IllnessInfo(string Key, string Name, float Severity, float BaseChance, string Cure, string CardImage);

    public static readonly IllnessInfo[] Illnesses =
    {
        new("cholera",        "Cholera",                   0.95f, 0.030f, "Rehydration salts",   "res://assets/images/illness/ill_cholera.webp"),
        new("smallpox",       "Smallpox",                  0.90f, 0.012f, "Isolation kit",        "res://assets/images/illness/ill_smallpox.webp"),
        new("pneumonia",      "Pneumonia",                 0.80f, 0.020f, "Tonic + rest",         "res://assets/images/illness/ill_pneumonia.webp"),
        new("typhoid",        "Typhoid / Mountain Fever",  0.75f, 0.018f, "Boiled-water kit",     "res://assets/images/illness/ill_typhoid_mountain_fever.webp"),
        new("dysentery",      "Dysentery",                 0.70f, 0.030f, "Astringent mixture",   "res://assets/images/illness/ill_dysentery.webp"),
        new("diphtheria",     "Diphtheria",                0.65f, 0.014f, "Throat remedy",        "res://assets/images/illness/ill_diphtheria.webp"),
        new("measles",        "Measles",                   0.55f, 0.016f, "Fever reducer",        "res://assets/images/illness/ill_measles.webp"),
        new("influenza",      "Influenza",                 0.45f, 0.035f, "Warmth + broth",       "res://assets/images/illness/ill_influenza.webp"),
        new("scurvy",         "Scurvy",                    0.40f, 0.010f, "Dried fruit pack",     "res://assets/images/illness/ill_scurvy.webp"),
        new("food_poisoning", "Food Poisoning",            0.35f, 0.028f, "Charcoal tabs",        "res://assets/images/illness/ill_food_poisoning.webp"),
    };

    public static IllnessInfo? GetIllness(string key) =>
        Array.Find(Illnesses, i => i.Key == key);

    public static string IllnessDisplayName(string key) =>
        GetIllness(key)?.Name ?? key;

    // ========================================================================
    // LANDMARKS
    // ========================================================================
    public record LandmarkInfo(
        string Name, int Miles, string Pin, (int x, int y) MapPos,
        bool IsTown, string? StoreKey, string BgImage, string? Key = null,
        string? ArrivalText = null, string? NextStopHint = null);

    public static readonly LandmarkInfo[] Landmarks =
    {
        new("Independence",      0,    "res://assets/images/map/pin_independence.webp",       (220, 820),  true,  "matts",       "res://assets/images/bg/bg_independence_street.webp",
            ArrivalText: "INDEPENDENCE, MISSOURI. THE START OF EVERYTHING. LOAD YOUR WAGON WITH CARE. THE TRAIL AHEAD DOES NOT FORGIVE.",
            NextStopHint: "NEXT STOP: FORT KEARNY, 304 MILES."),
        new("Fort Kearny",       304,  "res://assets/images/map/pin_fort_kearny.webp",        (430, 740),  true,  "fort_kearny", "res://assets/images/bg/stops/bg_stop_fort_kearny.webp",
            ArrivalText: "FORT KEARNY. THE FIRST MILITARY POST ON THE TRAIL. SUPPLIES AND A BLACKSMITH ARE AVAILABLE. THE PLATTE RIVER FOLLOWS THE TRAIL WEST FROM HERE.",
            NextStopHint: "NEXT STOP: CHIMNEY ROCK, 250 MILES."),
        new("Chimney Rock",      554,  "res://assets/images/map/pin_chimney_rock.webp",       (610, 670),  false, null,          "res://assets/images/bg/bg_chimney_rock.webp",
            ArrivalText: "CHIMNEY ROCK. A GREAT SPIRE OF CLAY AND SANDSTONE RISING 300 FEET FROM THE PLAIN. EVERY EMIGRANT CARVES THEIR NAME. YOUR PARTY STOPS TO REST.",
            NextStopHint: "NEXT STOP: FORT LARAMIE, 86 MILES."),
        new("Fort Laramie",      640,  "res://assets/images/map/pin_fort_laramie.webp",       (710, 610),  true,  "fort_laramie","res://assets/images/bg/bg_fort_laramie.webp",
            ArrivalText: "FORT LARAMIE. THE LAST MAJOR RESUPPLY BEFORE THE MOUNTAINS. A BLACKSMITH OPERATES HERE. EXAMINE YOUR WAGON AND OXEN CAREFULLY BEFORE MOVING ON.",
            NextStopHint: "NEXT STOP: INDEPENDENCE ROCK, 190 MILES."),
        new("Independence Rock", 830,  "res://assets/images/map/pin_independence_rock.webp",  (820, 560),  false, null,          "res://assets/images/bg/stops/bg_stop_independence_rock.webp",
            ArrivalText: "INDEPENDENCE ROCK. CALLED THE REGISTER OF THE DESERT. EMIGRANTS AIM TO REACH IT BY THE 4TH OF JULY TO STAY AHEAD OF WINTER. YOUR PARTY CAMPS IN ITS SHADOW.",
            NextStopHint: "NEXT STOP: SOUTH PASS, 170 MILES."),
        new("South Pass",        1000, "res://assets/images/map/pin_south_pass.webp",         (940, 500),  false, null,          "res://assets/images/bg/bg_south_pass.webp",
            ArrivalText: "SOUTH PASS. THE CONTINENTAL DIVIDE. WATER ON ONE SIDE FLOWS TO THE ATLANTIC; ON THE OTHER, TO THE PACIFIC. THE WORST CLIMBING IS BEHIND YOU.",
            NextStopHint: "NEXT STOP: FORT BRIDGER, 160 MILES."),
        new("Fort Bridger",      1160, "res://assets/images/map/pin_fort_bridger.webp",       (1045, 460), true,  "fort_bridger","res://assets/images/bg/stops/bg_stop_fort_bridger.webp",
            ArrivalText: "FORT BRIDGER. JIM BRIDGER'S TRADING POST. SUPPLIES ARE THINNER AND PRICES HIGHER. BLACKSMITH REPAIRS AVAILABLE. THE SNAKE RIVER COUNTRY LIES AHEAD.",
            NextStopHint: "NEXT STOP: SODA SPRINGS, 150 MILES."),
        new("Soda Springs",      1310, "res://assets/images/map/pin_soda_springs.webp",       (1125, 440), true,  "soda_springs","res://assets/images/bg/stops/bg_stop_soda_springs.webp",
            ArrivalText: "SODA SPRINGS. NATURAL CARBONATED SPRINGS BUBBLE FROM THE GROUND HERE. THE WATER IS STRANGE BUT THE PLACE IS KNOWN. FORT HALL IS CLOSE.",
            NextStopHint: "NEXT STOP: FORT HALL, 100 MILES."),
        new("Fort Hall",         1410, "res://assets/images/map/pin_fort_hall.webp",           (1200, 430), true,  "fort_hall",   "res://assets/images/bg/stops/bg_stop_fort_hall.webp",
            ArrivalText: "FORT HALL. THE LAST HUDSON'S BAY COMPANY POST ON THE TRAIL. A BLACKSMITH CAN SERVICE YOUR WAGON. THE SNAKE RIVER MUST BE CROSSED AHEAD.",
            NextStopHint: "NEXT STOP: FORT BOISE, 340 MILES."),
        new("Fort Boise",        1750, "res://assets/images/map/pin_fort_boise.webp",         (1330, 400), true,  "fort_boise",  "res://assets/images/bg/stops/bg_stop_fort_boise.webp",
            ArrivalText: "FORT BOISE. A SMALL POST ON THE BOISE RIVER. SUPPLIES ARE SCARCE AND EXPENSIVE. THE BLUE MOUNTAINS WAIT TO THE NORTHWEST.",
            NextStopHint: "NEXT STOP: BLUE MOUNTAINS, 110 MILES."),
        new("Blue Mountains",    1860, "res://assets/images/map/pin_blue_mountains.webp",     (1415, 380), false, null,          "res://assets/images/bg/bg_blue_mountains.webp",
            ArrivalText: "THE BLUE MOUNTAINS. THE LAST GREAT BARRIER. THE ASCENT IS STEEP AND THE DESCENT STEEPER. WAGONS HAVE BEEN LOST ON THESE SLOPES. GO CAREFULLY.",
            NextStopHint: "NEXT STOP: THE DALLES, 40 MILES."),
        new("The Dalles",        1900, "res://assets/images/map/pin_the_dalles.webp",         (1460, 360), true,  "the_dalles",  "res://assets/images/bg/bg_the_dalles.webp",
            ArrivalText: "THE DALLES. THE END OF THE OVERLAND TRAIL. THE COLUMBIA RIVER FILLS THE GORGE BELOW. OREGON CITY IS 270 MILES AHEAD. YOU MUST CHOOSE HOW TO REACH IT.",
            NextStopHint: "ROUTE CHOICE REQUIRED: BARLOW ROAD OR THE COLUMBIA RIVER."),
        new("Barlow Road",       2050, "res://assets/images/map/pin_barlow_road.webp",        (1478, 332), false, null,          "res://assets/images/bg/bg_barlow_road_day.webp", Key: "barlow_road",
            ArrivalText: "BARLOW ROAD TOLL GATE. SAM BARLOW BUILT THIS ROAD TO AVOID THE RIVER. THE ROAD IS ROUGH BUT IT IS LAND. OREGON CITY LIES JUST AHEAD.",
            NextStopHint: "NEXT STOP: OREGON CITY, 120 MILES."),
        new("Oregon City",       2170, "res://assets/images/map/pin_oregon_city.webp",        (1500, 300), true,  null,          "res://assets/images/bg/bg_oregon_city_arrival.webp",
            ArrivalText: "OREGON CITY. THE WILLAMETTE VALLEY. YOU HAVE ARRIVED.",
            NextStopHint: null),
    };

    // ========================================================================
    // RIVERS
    // ========================================================================
    public record RiverInfo(
        string Key, string Name, int Miles, (int min, int max) DepthFt,
        bool HasFerry, (int min, int max) FerryCost, bool HasGuide, string BgImage);

    public static readonly RiverInfo[] Rivers =
    {
        new("kansas",      "Kansas River",     102,  (2, 5), true,  (3, 12),  true,  "res://assets/images/bg/bg_river_crossing_generic.webp"),
        new("big_blue",    "Big Blue River",   250,  (2, 6), true,  (5, 18),  false, "res://assets/images/bg/bg_river_crossing_generic.webp"),
        new("platte",      "Platte River",     310,  (2, 5), false, (0, 0),   true,  "res://assets/images/bg/bg_river_crossing_generic.webp"),
        new("north_platte","North Platte",     465,  (2, 6), false, (0, 0),   true,  "res://assets/images/bg/bg_river_crossing_generic.webp"),
        new("sweetwater",  "Sweetwater River", 900,  (1, 4), false, (0, 0),   false, "res://assets/images/bg/bg_river_crossing_generic.webp"),
        new("green",       "Green River",      1110, (2, 7), true,  (8, 25),  true,  "res://assets/images/bg/bg_ferry_landing.webp"),
        new("bear",        "Bear River",       1240, (2, 6), false, (0, 0),   true,  "res://assets/images/bg/bg_river_crossing_generic.webp"),
        new("snake",       "Snake River",      1600, (3, 8), false, (0, 0),   true,  "res://assets/images/bg/bg_snake_river.webp"),
        new("boise",       "Boise River",      1730, (2, 6), false, (0, 0),   false, "res://assets/images/bg/bg_river_crossing_generic.webp"),
        new("columbia",    "Columbia River",    2050, (3, 8), true,  (10, 35), false, "res://assets/images/bg/bg_columbia_gorge.webp"),
    };

    // ========================================================================
    // OCCUPATIONS
    // ========================================================================
    public record OccupationInfo(string Key, string Name, float Cash, int ScoreMult);

    public static readonly OccupationInfo[] Occupations =
    {
        new("banker",    "Banker (Boston)",     1600f, 1),
        new("carpenter", "Carpenter (Ohio)",    800f,  2),
        new("farmer",    "Farmer (Illinois)",   400f,  3),
    };

    public static OccupationInfo? GetOccupation(string key) =>
        Array.Find(Occupations, o => o.Key == key);

    // ========================================================================
    // STORE PRICES (base prices by location)
    // ========================================================================
    public record StorePrices(
        float YokeOxen, float FoodLb, float ClothesSet, float BulletsBox,
        float SpareWheel, float SpareAxle, float SpareTongue);

    public static readonly Dictionary<string, StorePrices> Prices = new()
    {
        { "matts",        new(40f, 0.20f, 10f,   2f,    10f,   10f,   10f) },
        { "fort_kearny",  new(50f, 0.25f, 12.5f, 2.5f,  12.5f, 12.5f, 12.5f) },
        { "fort_laramie", new(60f, 0.30f, 15f,   3f,    13f,   13f,   13f) },
        { "the_dalles",   new(70f, 0.40f, 18f,   3.5f,  16f,   16f,   16f) },
        { "fort_bridger", new(55f, 0.30f, 13f,   3.5f,  18f,   16f,   14f) },
        { "soda_springs", new(0f,  0.28f, 12f,   3.25f, 0f,    0f,    0f) },
        { "fort_hall",    new(60f, 0.32f, 14f,   3.75f, 20f,   18f,   16f) },
        { "fort_boise",   new(0f,  0.33f, 14.5f, 4f,    22f,   20f,   18f) },
    };

    // ========================================================================
    // STORE STOCK AVAILABILITY
    // ========================================================================
    public record StoreStockFlags(bool Oxen, bool Food, bool Clothes, bool Bullets, bool Parts, bool Cures, bool Blacksmith = false);

    public static readonly Dictionary<string, StoreStockFlags> StoreStock = new()
    {
        { "default",      new(true,  true, true, true, true,  true,  false) },
        { "matts",        new(true,  true, true, true, true,  true,  false) },
        { "fort_kearny",  new(true,  true, true, true, true,  true,  Blacksmith: true) },
        { "fort_laramie", new(true,  true, true, true, true,  true,  Blacksmith: true) },
        { "fort_bridger", new(false, true, true, true, true,  true,  Blacksmith: true) },
        { "soda_springs", new(false, true, true, true, false, true,  false) },
        { "fort_hall",    new(true,  true, true, true, true,  true,  Blacksmith: true) },
        { "fort_boise",   new(false, true, true, true, true,  true,  false) },
        { "the_dalles",   new(false, true, true, true, true,  true,  false) },
    };

    // ========================================================================
    // STORE PROFILES (per-stop pricing multipliers and stock ranges)
    // ========================================================================
    public record StoreProfile(
        string Label,
        Dictionary<string, float> PriceMult,
        Dictionary<string, (int min, int max)> Stock,
        Dictionary<string, float> SoldoutBase);

    public static readonly Dictionary<string, StoreProfile> StoreProfiles = new()
    {
        { "matts", new("MATT'S OUTFITTER",
            new() { {"food",0.98f},{"ammo",0.98f},{"parts",1f},{"clothes",1f},{"livestock",1f},{"cures",1f} },
            new() { {"food",(650,950)},{"ammo_boxes",(8,14)},{"clothes",(4,10)},{"yokes",(3,6)},{"parts_each",(2,5)} },
            new() { {"food",0f},{"ammo",0f},{"clothes",0f},{"livestock",0f},{"parts",0f} }) },

        { "fort_kearny", new("FORT KEARNY POST",
            new() { {"food",1f},{"ammo",0.99f},{"parts",0.98f},{"clothes",1.02f},{"livestock",1f},{"cures",1.02f},{"repair",1.00f} },
            new() { {"food",(450,800)},{"ammo_boxes",(6,12)},{"clothes",(3,8)},{"yokes",(1,4)},{"parts_each",(2,5)} },
            new() { {"food",0.02f},{"ammo",0.04f},{"clothes",0.03f},{"livestock",0.08f},{"parts",0.05f} }) },

        { "fort_laramie", new("FORT LARAMIE TRADERS",
            new() { {"food",1.02f},{"ammo",1f},{"parts",0.95f},{"clothes",1.04f},{"livestock",1.02f},{"cures",1.03f},{"repair",1.05f} },
            new() { {"food",(420,780)},{"ammo_boxes",(6,12)},{"clothes",(3,8)},{"yokes",(1,3)},{"parts_each",(2,6)} },
            new() { {"food",0.03f},{"ammo",0.05f},{"clothes",0.04f},{"livestock",0.10f},{"parts",0.05f} }) },

        { "fort_bridger", new("FORT BRIDGER TRADING POST",
            new() { {"food",1.06f},{"ammo",1.05f},{"parts",0.90f},{"clothes",1.08f},{"livestock",1.05f},{"cures",1.06f},{"repair",1.15f} },
            new() { {"food",(360,700)},{"ammo_boxes",(5,10)},{"clothes",(2,6)},{"yokes",(0,2)},{"parts_each",(2,5)} },
            new() { {"food",0.05f},{"ammo",0.06f},{"clothes",0.05f},{"livestock",0.18f},{"parts",0.06f} }) },

        { "soda_springs", new("SODA SPRINGS CAMP",
            new() { {"food",1.05f},{"ammo",1.03f},{"parts",1.20f},{"clothes",1.08f},{"livestock",1.10f},{"cures",0.97f} },
            new() { {"food",(380,720)},{"ammo_boxes",(4,9)},{"clothes",(2,6)},{"yokes",(0,1)},{"parts_each",(0,1)} },
            new() { {"food",0.04f},{"ammo",0.06f},{"clothes",0.06f},{"livestock",0.22f},{"parts",0.30f} }) },

        { "fort_hall", new("FORT HALL POST",
            new() { {"food",1.07f},{"ammo",1.06f},{"parts",0.98f},{"clothes",1.10f},{"livestock",1.06f},{"cures",1.05f},{"repair",1.20f} },
            new() { {"food",(320,660)},{"ammo_boxes",(4,9)},{"clothes",(2,6)},{"yokes",(0,2)},{"parts_each",(1,4)} },
            new() { {"food",0.05f},{"ammo",0.07f},{"clothes",0.06f},{"livestock",0.18f},{"parts",0.10f} }) },

        { "fort_boise", new("FORT BOISE STORE",
            new() { {"food",1.10f},{"ammo",1.08f},{"parts",1.04f},{"clothes",1.10f},{"livestock",1.10f},{"cures",1.08f} },
            new() { {"food",(260,600)},{"ammo_boxes",(3,8)},{"clothes",(1,5)},{"yokes",(0,1)},{"parts_each",(1,4)} },
            new() { {"food",0.06f},{"ammo",0.08f},{"clothes",0.08f},{"livestock",0.25f},{"parts",0.12f} }) },

        { "the_dalles", new("THE DALLES MERCHANTS",
            new() { {"food",1.12f},{"ammo",1.10f},{"parts",1.10f},{"clothes",1.10f},{"livestock",1.15f},{"cures",1.10f} },
            new() { {"food",(240,580)},{"ammo_boxes",(3,8)},{"clothes",(1,5)},{"yokes",(0,1)},{"parts_each",(1,3)} },
            new() { {"food",0.06f},{"ammo",0.09f},{"clothes",0.08f},{"livestock",0.30f},{"parts",0.15f} }) },
    };

    // ========================================================================
    // EVENT CARDS
    // ========================================================================
    public record EventCard(string Key, string Text, string Image);

    public static readonly EventCard[] EventCards =
    {
        new("wheel_broken",      "WAGON WHEEL BROKE.",      "res://assets/images/events/evt_wheel_broken.webp"),
        new("axle_broken",       "WAGON AXLE CRACKED.",     "res://assets/images/events/evt_axle_broken.webp"),
        new("tongue_broken",     "WAGON TONGUE BROKE.",     "res://assets/images/events/evt_tongue_broken.webp"),
        new("thief",             "THIEVES IN THE NIGHT.",   "res://assets/images/events/evt_thief_night.webp"),
        new("lost_trail",        "LOST THE TRAIL.",         "res://assets/images/events/evt_lost_trail.webp"),
        new("bad_water",         "BAD WATER.",              "res://assets/images/events/evt_bad_water.webp"),
        new("rations_low",       "RATIONS RUNNING LOW.",    "res://assets/images/events/evt_rations_low.webp"),
        new("good_weather",      "GOOD WEATHER.",           "res://assets/images/events/evt_good_weather.webp"),
        new("find_berries",      "FOUND BERRIES.",          "res://assets/images/events/evt_found_berries.webp"),
        new("find_wagon_parts",  "FOUND WAGON PARTS.",      "res://assets/images/events/evt_found_wagon_parts.webp"),
        new("enc_trade",         "TRAIL TRADE OFFER.",      "res://assets/images/events/evt_enc_trade.webp"),
        new("enc_guidance",      "LOCAL GUIDANCE.",          "res://assets/images/events/evt_enc_guidance.webp"),
        new("enc_ferry_help",    "FERRY HELP OFFERED.",     "res://assets/images/events/evt_enc_ferry_help.webp"),
        new("enc_medical_help",  "TRAVELING MEDIC.",        "res://assets/images/events/evt_enc_medical_help.webp"),
        new("enc_terrain_warning","TERRAIN WARNING.",       "res://assets/images/events/evt_enc_terrain_warning.webp"),
        new("rockslide",         "ROCKSLIDE!",             "res://assets/images/events/evt_rockslide.webp"),
        new("early_snow",        "EARLY SNOW.",             "res://assets/images/events/evt_early_snow.webp"),
        new("frozen_edges",      "FROZEN RIVER EDGES.",     "res://assets/images/events/evt_frozen_edges.webp"),
    };

    public static readonly Dictionary<string, string> ItemTypes = new()
    {
        { "yoke_oxen", "livestock" },
        { "food_lb", "food" },
        { "clothes_set", "clothes" },
        { "bullets_box", "ammo" },
        { "spare_wheel", "parts" },
        { "spare_axle", "parts" },
        { "spare_tongue", "parts" },
    };
}
