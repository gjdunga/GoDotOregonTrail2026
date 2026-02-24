#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using OregonTrail2026.Models;
using OregonTrail2026.Utils;

namespace OregonTrail2026.Screens;

/// <summary>
/// Trail trade offer screen. Shown when a trade encounter fires and the
/// player has seen the initial encounter card.
///
/// A trade offer consists of:
///   offer_item / offer_qty: what the trader gives
///   want_item  / want_qty:  what the trader wants (cash = dollar amount)
///
/// Player choices:
///   ACCEPT TRADE - validates player has the required goods/cash, executes swap.
///   DECLINE       - closes with no effect.
///
/// Both paths emit TradeResolved(bool accepted, string resultMessage).
/// MainScene displays the result message and continues normal flow.
///
/// Signals:
///   TradeResolved(bool accepted, string resultMessage)
/// </summary>
public partial class TradeScreen : Control
{
    [Signal] public delegate void TradeResolvedEventHandler(bool accepted, string resultMessage);

    private GameState _state = null!;
    private Dictionary<string, object> _offer = null!;

    public void Initialize(GameState state, Dictionary<string, object> offer)
    {
        _state = state;
        _offer = offer;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Semi-transparent overlay with trade image behind
        var overlay = UIKit.MakeDarkOverlay(0.70f);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = UIKit.MakePanel();
        panel.CustomMinimumSize = new Vector2(560, 0);
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

        // Title
        var title = UIKit.MakeDisplayLabel(Tr(TK.TradeTitle), 26, UIKit.ColAmber);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var subtitle = UIKit.MakeBodyLabel(
            "A trader pulls up alongside your wagon.", 14, UIKit.ColGray);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(subtitle);

        vbox.AddChild(UIKit.MakeDivider());

        // Offer display
        string offerItem = _offer.GetValueOrDefault("offer_item", "food") as string ?? "food";
        int    offerQty  = _offer.GetValueOrDefault("offer_qty",  0) as int? ?? 0;
        string wantItem  = _offer.GetValueOrDefault("want_item",  "cash") as string ?? "cash";
        int    wantQty   = _offer.GetValueOrDefault("want_qty",   0) as int? ?? 0;

        string offerDesc = FormatItem(offerItem, offerQty, giving: true);
        string wantDesc  = FormatItem(wantItem,  wantQty,  giving: false);

        // Two column layout: trader gives / trader wants
        var tradeRow = new HBoxContainer();
        tradeRow.AddThemeConstantOverride("separation", 20);
        tradeRow.Alignment = BoxContainer.AlignmentMode.Center;

        tradeRow.AddChild(BuildTradeColumn(Tr(TK.TradeGives), offerDesc, UIKit.ColGreen));
        tradeRow.AddChild(BuildDividerVertical());
        tradeRow.AddChild(BuildTradeColumn(Tr(TK.TradeWants), wantDesc, UIKit.ColAmber));
        vbox.AddChild(tradeRow);

        vbox.AddChild(UIKit.MakeDivider());

        // Player's current inventory snapshot for the relevant items
        vbox.AddChild(BuildInventoryHint(offerItem, wantItem, wantQty));

        vbox.AddChild(UIKit.MakeSpacer(4));

        // Can the player afford/provide the trade?
        bool canAfford = CanAfford(wantItem, wantQty);

        // Buttons
        var btnRow = new HBoxContainer();
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        btnRow.AddThemeConstantOverride("separation", 16);

        var acceptBtn = UIKit.MakePrimaryButton(Tr(TK.TradeAccept), 18);
        acceptBtn.CustomMinimumSize = new Vector2(200, 52);
        acceptBtn.Disabled = !canAfford;
        if (!canAfford)
        {
            var notice = UIKit.MakeBodyLabel(
                $"YOU DON'T HAVE ENOUGH {wantItem.ToUpper()} TO TRADE.", 13, UIKit.ColRed);
            notice.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(notice);
        }
        acceptBtn.Pressed += () => OnAccept(offerItem, offerQty, wantItem, wantQty);

        var declineBtn = UIKit.MakeSecondaryButton(Tr(TK.TradeDecline), 18);
        declineBtn.CustomMinimumSize = new Vector2(160, 52);
        declineBtn.Pressed += OnDecline;

        btnRow.AddChild(acceptBtn);
        btnRow.AddChild(declineBtn);
        vbox.AddChild(btnRow);
    }

    // =========================================================================
    // ACTION HANDLERS
    // =========================================================================

    private void OnAccept(string offerItem, int offerQty, string wantItem, int wantQty)
    {
        if (!CanAfford(wantItem, wantQty))
        {
            EmitSignal(SignalName.TradeResolved, false,
                $"NOT ENOUGH {wantItem.ToUpper()} TO COMPLETE THE TRADE.");
            return;
        }

        // Deduct what player gives
        if (wantItem == "cash")
        {
            _state.Cash = Math.Max(0, _state.Cash - wantQty);
        }
        else
        {
            string supplyKey = ItemToSupplyKey(wantItem);
            _state.Supplies[supplyKey] = Math.Max(0,
                _state.Supplies.GetValueOrDefault(supplyKey, 0) - wantQty);
        }

        // Add what player receives
        string receiveKey = ItemToSupplyKey(offerItem);
        if (offerItem == "food")
        {
            CargoSystem.AddFoodWithCapacity(_state, offerQty);
        }
        else
        {
            _state.Supplies[receiveKey] = _state.Supplies.GetValueOrDefault(receiveKey, 0) + offerQty;
        }

        string result = $"TRADE ACCEPTED. RECEIVED {FormatItem(offerItem, offerQty, giving: true)}" +
                        $" FOR {FormatItem(wantItem, wantQty, giving: false)}.";
        EmitSignal(SignalName.TradeResolved, true, result);
    }

    private void OnDecline()
    {
        EmitSignal(SignalName.TradeResolved, false, Tr(TK.TradeDeclined));
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private bool CanAfford(string wantItem, int wantQty)
    {
        if (wantItem == "cash")
            return _state.Cash >= wantQty;

        string key = ItemToSupplyKey(wantItem);
        return _state.Supplies.GetValueOrDefault(key, 0) >= wantQty;
    }

    private static string ItemToSupplyKey(string item) => item switch
    {
        "food"    => "food",
        "bullets" => "bullets",
        "clothes" => "clothes",
        "wheel"   => "wheel",
        "axle"    => "axle",
        "tongue"  => "tongue",
        _         => item,
    };

    private static string FormatItem(string item, int qty, bool giving)
    {
        return item switch
        {
            "food"    => $"{qty} LBS FOOD",
            "bullets" => $"{qty} BULLETS",
            "clothes" => $"{qty} SET{(qty > 1 ? "S" : "")} CLOTHES",
            "wheel"   => $"{qty} SPARE WHEEL{(qty > 1 ? "S" : "")}",
            "axle"    => $"{qty} SPARE AXLE{(qty > 1 ? "S" : "")}",
            "tongue"  => $"{qty} WAGON TONGUE{(qty > 1 ? "S" : "")}",
            "cash"    => $"${qty}",
            _         => $"{qty} {item.ToUpper()}",
        };
    }

    private Control BuildTradeColumn(string heading, string content, Color accent)
    {
        var col = new VBoxContainer();
        col.CustomMinimumSize = new Vector2(200, 0);
        col.AddThemeConstantOverride("separation", 8);

        var h = UIKit.MakeBodyLabel(heading, 13, UIKit.ColAmberDim);
        h.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(h);

        var c = UIKit.MakeDisplayLabel(content, 18, accent);
        c.HorizontalAlignment = HorizontalAlignment.Center;
        c.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(c);

        return col;
    }

    private static Control BuildDividerVertical()
    {
        var sep = new VSeparator();
        sep.CustomMinimumSize = new Vector2(2, 60);
        return sep;
    }

    private Control BuildInventoryHint(string offerItem, string wantItem, int wantQty)
    {
        var row = new HBoxContainer();
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddThemeConstantOverride("separation", 24);

        // Show current supply of what they'll receive
        string receiveKey = ItemToSupplyKey(offerItem);
        int currentReceive = offerItem == "cash"
            ? (int)_state.Cash
            : _state.Supplies.GetValueOrDefault(receiveKey, 0);
        AddInventoryCell(row, $"YOUR {offerItem.ToUpper()}", currentReceive);

        // Show current supply of what they'll give
        if (wantItem == "cash")
        {
            AddInventoryCell(row, Tr(TK.TradeYourCash), (int)_state.Cash,
                _state.Cash < wantQty ? UIKit.ColRed : UIKit.ColGreen);
        }
        else
        {
            string giveKey = ItemToSupplyKey(wantItem);
            int currentGive = _state.Supplies.GetValueOrDefault(giveKey, 0);
            AddInventoryCell(row, $"YOUR {wantItem.ToUpper()}", currentGive,
                currentGive < wantQty ? UIKit.ColRed : UIKit.ColGreen);
        }

        return row;
    }

    private static void AddInventoryCell(HBoxContainer row, string label, int value,
        Color? valueColor = null)
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);
        var l = UIKit.MakeBodyLabel(label, 11, UIKit.ColAmberDim);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        var v = UIKit.MakeBodyLabel(value.ToString(), 15, valueColor ?? UIKit.ColParchment);
        v.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(l);
        col.AddChild(v);
        row.AddChild(col);
    }
}
