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
/// Fort/town store screen. Presented when the player chooses VISIT STORE
/// from the choice menu while at a town.
///
/// Reads storeKey from GameState.AtTownStoreKey to determine pricing,
/// stock availability, and soldout state via EconomySystem.
///
/// Sections rendered based on GameData.StoreStock flags for the store:
///   Supplies: food, bullets boxes, clothes sets (if Food/Bullets/Clothes)
///   Livestock: yoke oxen (if Oxen)
///   Wagon Parts: wheel, axle, tongue (if Parts)
///   Medical: cures per illness if party is afflicted (if Cures)
///
/// Purchases are immediate: each BUY button calls EconomySystem.BuyItem
/// or EconomySystem.BuyCure, then refreshes the UI.
///
/// Signals:
///   StoreExited - emitted when player clicks LEAVE. MainScene returns to
///     the choice menu and calls EconomySystem.ClearStoreSoldout on depart.
/// </summary>
public partial class FortStoreScreen : Control
{
    [Signal] public delegate void StoreExitedEventHandler();

    private GameState _state = null!;
    private string _storeKey = "";

    // Live-refresh labels
    private Label _cashLabel = null!;
    private Label _weightLabel = null!;
    private VBoxContainer _contentRoot = null!;

    // Item definitions: (itemKey, nameKey, unitKey, stepQty, category)
    // nameKey and unitKey are TK constants resolved via TranslationServer.Translate() at UI build time.
    private static readonly (string key, string nameKey, string unitKey, int step, string cat)[] Items =
    {
        ("food_lb",      TK.StoreItemFood,    TK.StoreUnitPerLb,   50, "food"),
        ("bullets_box",  TK.StoreItemAmmo,    TK.StoreUnitPerBox,   1, "ammo"),
        ("clothes_set",  TK.StoreItemClothes, TK.StoreUnitPerSet,   1, "clothes"),
        ("yoke_oxen",    TK.StoreItemOxen,    TK.StoreUnitPerYoke,  1, "livestock"),
        ("spare_wheel",  TK.StoreItemWheel,   TK.StoreUnitEach,     1, "parts"),
        ("spare_axle",   TK.StoreItemAxle,    TK.StoreUnitEach,     1, "parts"),
        ("spare_tongue", TK.StoreItemTongue,  TK.StoreUnitEach,     1, "parts"),
    };

    public void Initialize(GameState state)
    {
        _state = state;
        _storeKey = state.AtTownStoreKey;
    }

    public override void _Ready()
    {
        // Seed soldout state once per visit (no-op if already seeded)
        EconomySystem.SeedStoreSoldout(_state, _storeKey);

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var overlay = UIKit.MakeDarkOverlay(0.60f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // Outer scroll so the whole panel scrolls if content exceeds viewport
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical   = SizeFlags.ExpandFill,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        scroll.SetOffset(Side.Left,   60);
        scroll.SetOffset(Side.Right, -60);
        scroll.SetOffset(Side.Top,    24);
        scroll.SetOffset(Side.Bottom,-24);
        AddChild(scroll);

        _contentRoot = new VBoxContainer();
        _contentRoot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _contentRoot.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(_contentRoot);

        BuildHeader();
        BuildSections();
        BuildFooter();
    }

    // =========================================================================
    // HEADER
    // =========================================================================

    private void BuildHeader()
    {
        string storeName = GameData.StoreProfiles.TryGetValue(_storeKey, out var prof)
            ? prof.Label
            : _storeKey.Replace("_", " ").ToUpper();

        var title = UIKit.MakeDisplayLabel(storeName, 26);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _contentRoot.AddChild(title);

        _contentRoot.AddChild(UIKit.MakeDivider());

        // Cash + weight row
        var statRow = new HBoxContainer();
        statRow.Alignment = BoxContainer.AlignmentMode.Center;
        statRow.AddThemeConstantOverride("separation", 40);

        _cashLabel = UIKit.MakeDisplayLabel(string.Format(Tr(TK.StoreCash), _state.Cash), 20, UIKit.ColAmber);
        statRow.AddChild(_cashLabel);

        int cargo = CargoSystem.CargoWeight(_state);
        int cap   = CargoSystem.CargoCapacity(_state);
        _weightLabel = UIKit.MakeBodyLabel(string.Format(Tr(TK.StoreCargo), cargo, cap), 14, UIKit.ColGray);
        _weightLabel.VerticalAlignment = VerticalAlignment.Center;
        statRow.AddChild(_weightLabel);

        _contentRoot.AddChild(statRow);
        _contentRoot.AddChild(UIKit.MakeSpacer(4));
    }

    // =========================================================================
    // SECTIONS
    // =========================================================================

    private void BuildSections()
    {
        var stock = GameData.StoreStock.GetValueOrDefault(_storeKey,
            GameData.StoreStock["default"]);

        // Supplies: food, ammo, clothes
        var supplyItems = Items.Where(i =>
            (i.cat == "food"    && stock.Food)    ||
            (i.cat == "ammo"    && stock.Bullets)  ||
            (i.cat == "clothes" && stock.Clothes)).ToList();

        if (supplyItems.Count > 0)
            BuildItemSection(Tr(TK.StoreSecSupplies),  supplyItems);

        // Livestock
        var oxenItems = Items.Where(i => i.cat == "livestock" && stock.Oxen).ToList();
        if (oxenItems.Count > 0)
            BuildItemSection(Tr(TK.StoreSecLivestock), oxenItems);

        // Wagon parts
        var partItems = Items.Where(i => i.cat == "parts" && stock.Parts).ToList();
        if (partItems.Count > 0)
            BuildItemSection(Tr(TK.StoreSecParts), partItems);

        // Cures
        if (stock.Cures)
            BuildCuresSection();

        // Blacksmith wagon services
        if (stock.Blacksmith)
            BuildBlacksmithSection();
    }

    private void BuildItemSection(
        string heading,
        List<(string key, string name, string unit, int step, string cat)> items)
    {
        var sectionLabel = UIKit.MakeDisplayLabel(heading, 18, UIKit.ColAmberDim);
        sectionLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _contentRoot.AddChild(sectionLabel);

        foreach (var item in items)
        {
            bool soldout = EconomySystem.IsSoldout(_state, _storeKey, item.cat);
            float price  = EconomySystem.CalculatePrice(_state, _storeKey, item.key);

            if (price <= 0 && !soldout) continue; // item not stocked at this location

            _contentRoot.AddChild(BuildItemRow(item.key, item.name, item.unit, item.step, price, soldout));
        }

        _contentRoot.AddChild(UIKit.MakeDivider());
    }

    // =========================================================================
    // ITEM ROW: [Name / Owned]  [price]  [−] [qty] [+]  [BUY]
    // =========================================================================

    private Control BuildItemRow(
        string itemKey, string displayName, string unit, int step, float unitPrice, bool soldout)
    {
        int owned = OwnedCount(itemKey);
        int qty   = step; // default purchase qty

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        // Name + owned
        var nameCol = new VBoxContainer();
        nameCol.CustomMinimumSize = new Vector2(180, 0);
        var nameLabel = UIKit.MakeBodyLabel(displayName, 15, UIKit.ColParchment);
        var ownedLabel = UIKit.MakeBodyLabel(string.Format(Tr(TK.StoreOwned), owned), 12, UIKit.ColGray);
        nameCol.AddChild(nameLabel);
        nameCol.AddChild(ownedLabel);
        row.AddChild(nameCol);

        // Price
        var priceLabel = UIKit.MakeBodyLabel(
            soldout ? "SOLD OUT" : $"${unitPrice:F2} {unit}",
            13,
            soldout ? UIKit.ColRed : UIKit.ColGray);
        priceLabel.CustomMinimumSize = new Vector2(130, 0);
        priceLabel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(priceLabel);

        if (soldout)
        {
            // Filler to preserve row height
            row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            return row;
        }

        // Spacer
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        // Qty controls
        var qtyLabel = UIKit.MakeDisplayLabel(qty.ToString(), 16);
        qtyLabel.CustomMinimumSize = new Vector2(48, 0);
        qtyLabel.HorizontalAlignment = HorizontalAlignment.Center;

        var btnMinus = MakeQtyButton("\u2212");
        btnMinus.Pressed += () =>
        {
            if (qty > step) { qty -= step; qtyLabel.Text = qty.ToString(); }
        };

        var btnPlus = MakeQtyButton("+");
        btnPlus.Pressed += () =>
        {
            qty += step;
            qtyLabel.Text = qty.ToString();
        };

        row.AddChild(btnMinus);
        row.AddChild(qtyLabel);
        row.AddChild(btnPlus);

        // BUY button
        var buyBtn = UIKit.MakeSecondaryButton(Tr(TK.StoreBuy), 14);
        buyBtn.CustomMinimumSize = new Vector2(72, 40);
        buyBtn.Pressed += () =>
        {
            var (ok, msg) = EconomySystem.BuyItem(_state, _storeKey, itemKey, qty);
            if (ok)
            {
                qty = step; // reset qty to step size after purchase
                qtyLabel.Text = qty.ToString();
                RefreshStatus();
                ownedLabel.Text = $"OWNED: {OwnedCount(itemKey)}";
            }
            ShowFlash(msg, ok);
        };
        row.AddChild(buyBtn);

        return row;
    }

    // =========================================================================
    // CURES SECTION
    // =========================================================================

    private void BuildCuresSection()
    {
        // Only show illnesses that at least one living party member has
        var activeIllnesses = _state.Living()
            .Where(p => !string.IsNullOrEmpty(p.Illness))
            .Select(p => p.Illness)
            .Distinct()
            .ToList();

        if (activeIllnesses.Count == 0) return;

        bool soldout = EconomySystem.IsSoldout(_state, _storeKey, "cures");

        var heading = UIKit.MakeDisplayLabel(Tr(TK.StoreSecMedical), 18, UIKit.ColAmberDim);
        heading.HorizontalAlignment = HorizontalAlignment.Left;
        _contentRoot.AddChild(heading);

        if (soldout)
        {
            _contentRoot.AddChild(
                UIKit.MakeBodyLabel(Tr(TK.StoreMedSoldOut), 14, UIKit.ColRed));
        }
        else
        {
            foreach (string illKey in activeIllnesses)
            {
                if (!_state.CurePrices.TryGetValue(illKey, out int price)) continue;

                string illName = GameData.IllnessDisplayName(illKey).ToUpper();
                var patients = _state.Living()
                    .Where(p => p.Illness == illKey)
                    .Select(p => p.Name).ToList();
                string whoStr = string.Join(", ", patients);

                _contentRoot.AddChild(BuildCureRow(illKey, illName, whoStr, price));
            }
        }

        _contentRoot.AddChild(UIKit.MakeDivider());
    }

    private Control BuildCureRow(string illKey, string illName, string who, int price)
    {
        var row = new HBoxContainer();
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddThemeConstantOverride("separation", 8);

        var nameCol = new VBoxContainer();
        nameCol.CustomMinimumSize = new Vector2(180, 0);
        nameCol.AddChild(UIKit.MakeBodyLabel(illName, 15, UIKit.ColParchment));
        nameCol.AddChild(UIKit.MakeBodyLabel(string.Format(Tr(TK.StoreAfflicted), who), 12, UIKit.ColGray));
        row.AddChild(nameCol);

        row.AddChild(UIKit.MakeBodyLabel($"${price}", 14, UIKit.ColGray));
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var buyBtn = UIKit.MakeSecondaryButton(Tr(TK.StoreCure), 14);
        buyBtn.CustomMinimumSize = new Vector2(72, 40);
        buyBtn.Pressed += () =>
        {
            var (ok, msg) = EconomySystem.BuyCure(_state, illKey);
            ShowFlash(msg, ok);
            if (ok)
            {
                RefreshStatus();
                // Rebuild cures section so cured illnesses disappear
                RebuildContent();
            }
        };
        row.AddChild(buyBtn);

        return row;
    }

    // =========================================================================
    // BLACKSMITH SECTION
    // =========================================================================

    private void BuildBlacksmithSection()
    {
        var heading = UIKit.MakeDisplayLabel(Tr(TK.StoreSecBlacksmith), 18, UIKit.ColAmberDim);
        heading.HorizontalAlignment = HorizontalAlignment.Left;
        _contentRoot.AddChild(heading);

        float locationMult = 1.0f;
        if (GameData.StoreProfiles.TryGetValue(_storeKey, out var prof)
            && prof.PriceMult.TryGetValue("repair", out float rm))
            locationMult = rm;

        float inspectCost = (float)Math.Round(GameConstants.BlacksmithInspectBaseCost * locationMult, 2);
        float repairCost  = (float)Math.Round(GameConstants.BlacksmithRepairBaseCost  * locationMult, 2);
        float tuneupCost  = (float)Math.Round(GameConstants.BlacksmithTuneupBaseCost  * locationMult, 2);

        if (_state.Occupation == "carpenter") { inspectCost *= GameConstants.ServiceCarpenterDiscount; repairCost *= GameConstants.ServiceCarpenterDiscount; tuneupCost *= GameConstants.ServiceCarpenterDiscount; }
        else if (_state.Occupation == "banker") { inspectCost *= GameConstants.ServiceBankerGouge; repairCost *= GameConstants.ServiceBankerGouge; tuneupCost *= GameConstants.ServiceBankerGouge; }

        inspectCost = (float)Math.Round(inspectCost, 2);
        repairCost  = (float)Math.Round(repairCost,  2);
        tuneupCost  = (float)Math.Round(tuneupCost,  2);

        int wagonPct = (int)(_state.Wagon / (float)GameConstants.ConditionMaximum * 100f);
        bool tuneupActive = _state.TuneupUntilMiles > _state.Miles;

        // Inspection
        _contentRoot.AddChild(BuildServiceRow(
            "WAGON INSPECTION",
            $"See exact wagon condition. Current: {wagonPct}%",
            $"${inspectCost:F2}",
            canAfford: _state.Cash >= inspectCost,
            disabled: false,
            onBuy: () =>
            {
                if (_state.Cash < inspectCost) { ShowFlash("NOT ENOUGH CASH.", false); return; }
                _state.Cash -= inspectCost;
                int wPct = (int)(_state.Wagon / (float)GameConstants.ConditionMaximum * 100f);
                int oPct = (int)(_state.OxenCondition / (float)GameConstants.ConditionMaximum * 100f);
                string pending = _state.PendingRepair != null
                    ? $" BROKEN {(_state.PendingRepair["part"] as string ?? "PART").ToUpper()} FOUND."
                    : "";
                ShowFlash($"WAGON: {wPct}%  OXEN: {oPct}%{pending}", true);
                RefreshStatus();
            }));

        // Full repair
        bool repairNeeded = _state.Wagon < 800 || _state.PendingRepair != null;
        _contentRoot.AddChild(BuildServiceRow(
            "FIELD REPAIR",
            repairNeeded
                ? $"Guaranteed quality repair. Wagon at {wagonPct}%."
                : "Wagon is in good condition. Repair not needed.",
            $"${repairCost:F2}",
            canAfford: _state.Cash >= repairCost,
            disabled: !repairNeeded,
            onBuy: () =>
            {
                if (_state.Cash < repairCost) { ShowFlash("NOT ENOUGH CASH.", false); return; }
                _state.Cash -= repairCost;
                string result = RepairSystem.BlacksmithRepair(_state);
                ShowFlash(result, true);
                RefreshStatus();
                RebuildContent();
            }));

        // Tuneup
        _contentRoot.AddChild(BuildServiceRow(
            "FULL WAGON TUNEUP",
            tuneupActive
                ? $"Tuneup active until mile {_state.TuneupUntilMiles}. Cannot stack."
                : $"Reduces breakdown chance for {GameConstants.TuneupDurationMiles} miles.",
            $"${tuneupCost:F2}",
            canAfford: _state.Cash >= tuneupCost,
            disabled: tuneupActive,
            onBuy: () =>
            {
                if (_state.Cash < tuneupCost) { ShowFlash("NOT ENOUGH CASH.", false); return; }
                _state.Cash -= tuneupCost;
                string result = RepairSystem.BlacksmithTuneup(_state);
                ShowFlash(result, true);
                RefreshStatus();
            }));

        _contentRoot.AddChild(UIKit.MakeDivider());
    }

    private Control BuildServiceRow(
        string name, string description, string price,
        bool canAfford, bool disabled, System.Action onBuy)
    {
        var row = new HBoxContainer();
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddThemeConstantOverride("separation", 8);

        var nameCol = new VBoxContainer();
        nameCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameCol.AddChild(UIKit.MakeBodyLabel(name, 15,
            disabled ? UIKit.ColGray : UIKit.ColParchment));

        var descLbl = UIKit.MakeBodyLabel(description, 12, UIKit.ColGray);
        descLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        nameCol.AddChild(descLbl);
        row.AddChild(nameCol);

        var priceLbl = UIKit.MakeBodyLabel(price, 14,
            canAfford ? UIKit.ColAmber : UIKit.ColRed);
        priceLbl.CustomMinimumSize = new Vector2(60, 0);
        priceLbl.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(priceLbl);

        var btn = UIKit.MakeSecondaryButton(Tr(TK.StoreBuy), 14);
        btn.CustomMinimumSize = new Vector2(60, 40);
        btn.Disabled = disabled || !canAfford;
        btn.Pressed += () => onBuy();
        row.AddChild(btn);

        return row;
    }

    // =========================================================================
    // FOOTER
    // =========================================================================

    private void BuildFooter()
    {
        _contentRoot.AddChild(UIKit.MakeSpacer(8));

        var leaveRow = new HBoxContainer();
        leaveRow.Alignment = BoxContainer.AlignmentMode.Center;

        var leaveBtn = UIKit.MakePrimaryButton(Tr(TK.StoreLeave), 20);
        leaveBtn.CustomMinimumSize = new Vector2(260, 56);
        leaveBtn.Pressed += () => EmitSignal(SignalName.StoreExited);
        leaveRow.AddChild(leaveBtn);

        _contentRoot.AddChild(leaveRow);
        _contentRoot.AddChild(UIKit.MakeSpacer(16));
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private int OwnedCount(string itemKey) => itemKey switch
    {
        "food_lb"      => _state.Supplies.GetValueOrDefault("food",    0),
        "bullets_box"  => _state.Supplies.GetValueOrDefault("bullets", 0),
        "clothes_set"  => _state.Supplies.GetValueOrDefault("clothes", 0),
        "yoke_oxen"    => _state.Supplies.GetValueOrDefault("oxen",    0),
        "spare_wheel"  => _state.Supplies.GetValueOrDefault("wheel",   0),
        "spare_axle"   => _state.Supplies.GetValueOrDefault("axle",    0),
        "spare_tongue" => _state.Supplies.GetValueOrDefault("tongue",  0),
        _              => 0,
    };

    private void RefreshStatus()
    {
        _cashLabel.Text = $"CASH: ${_state.Cash:F2}";
        _cashLabel.AddThemeColorOverride("font_color",
            _state.Cash < 20 ? UIKit.ColRed : UIKit.ColAmber);

        int cargo = CargoSystem.CargoWeight(_state);
        int cap   = CargoSystem.CargoCapacity(_state);
        _weightLabel.Text = $"CARGO: {cargo}/{cap} LBS";
        _weightLabel.AddThemeColorOverride("font_color",
            cargo > cap ? UIKit.ColRed : UIKit.ColGray);
    }

    // Full rebuild after a cure purchase removes an illness from the list
    private void RebuildContent()
    {
        // Clear and rebuild everything below the header (first 4 nodes: title, divider, statRow, spacer)
        var children = _contentRoot.GetChildren();
        // Remove everything after index 3 (0=title, 1=divider, 2=statRow, 3=spacer)
        for (int i = children.Count - 1; i > 3; i--)
            children[i].QueueFree();

        BuildSections();
        BuildFooter();
    }

    // Transient flash message at the top of the panel
    private void ShowFlash(string msg, bool success)
    {
        var flash = UIKit.MakeBodyLabel(msg, 14,
            success ? UIKit.ColGreen : UIKit.ColRed);
        flash.HorizontalAlignment = HorizontalAlignment.Center;

        // Insert just below the header
        _contentRoot.AddChild(flash);
        _contentRoot.MoveChild(flash, 4); // after title, divider, statRow, spacer

        // Auto-remove after 2.5 seconds using a SceneTreeTimer
        GetTree().CreateTimer(2.5f).Timeout += () =>
        {
            if (IsInstanceValid(flash)) flash.QueueFree();
        };
    }

    private static Button MakeQtyButton(string label)
    {
        var btn = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(32, 32),
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(UIKit.ColDarkBrown, 0.7f),
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft   = 3, CornerRadiusTopRight   = 3,
            ContentMarginLeft = 4, ContentMarginRight = 4,
            ContentMarginTop  = 2, ContentMarginBottom = 2,
        };
        var hover = new StyleBoxFlat
        {
            BgColor = new Color(UIKit.ColAmberDim, 0.8f),
            CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft   = 3, CornerRadiusTopRight   = 3,
            ContentMarginLeft = 4, ContentMarginRight = 4,
            ContentMarginTop  = 2, ContentMarginBottom = 2,
        };
        btn.AddThemeStyleboxOverride("normal",  style);
        btn.AddThemeStyleboxOverride("hover",   hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus",   style);
        btn.AddThemeColorOverride("font_color", UIKit.ColParchment);
        btn.AddThemeFontSizeOverride("font_size", 18);
        return btn;
    }
}
