public native func SC_Hotkey(key: Int32, modifiers: Int32) -> Bool;
public native func SC_Request(op: String, id: String, payload: String) -> Bool;
public native func SC_Value(id: String, field: String) -> String;
public native func SC_Session() -> String;
public native func SC_NewID() -> String;
public native func SC_Connected() -> Bool;
public native func SC_Count() -> Int32;
public native func SC_Name(index: Int32) -> String;
public native func SC_Quantity(index: Int32) -> Int32;
public native func SC_Price(index: Int32) -> Int32;
public native func SC_Sale(op: String, id: String, index: Int32, qty: Int32) -> Bool;
public native func SC_Status(id: String) -> String;
public native func SC_WorldNew() -> Int32;
public native func SC_WorldLoad(world: Int32, session: String) -> Int32;
public native func SC_WorldCount() -> Int32;
public native func SC_WorldGUID(index: Int32) -> String;
public native func SC_WorldPosition(index: Int32) -> Vector4;
public native func SC_WorldYaw(index: Int32) -> Float;
public native func SC_WorldPut(guid: String, position: Vector4, yaw: Float) -> Bool;
public native func SC_WorldRemove(guid: String) -> Bool;
public native func SC_Report(report: String) -> Void;

public class StreetChemPlacement extends IScriptable {
  public persistent let id: CName;
  public persistent let guid: CName;
  public persistent let position: Vector4;
  public persistent let yaw: Float;
}
public class StreetChemCitySystem extends ScriptableSystem {
  private persistent let placements: array<ref<StreetChemPlacement>>;
  private persistent let guestSession: CName;
  private persistent let pending: CName;
  private persistent let pendingPlacement: ref<StreetChemPlacement>;
  private persistent let pendingPrice: Int32;
  private let yaw: Float;
  private let nextTick: Float;
  private let nextPendingPoll: Float;
  private let restoreIndex: Int32;
  private let restored: Bool;
  private let action: String;
  private let actionKind: String;
  private let actionGuid: String;
  private let storageReady: Bool;
  private let worldKey: Int32;
  private let productIndex: Int32;
  private persistent let saleID: CName;
  private persistent let salePrice: Int32;
  private persistent let saleStage: CName;
  private let saleNextPoll: Float;
  private let customer: wref<ScriptedPuppet>;
  private let lastCustomer: EntityID;
  private let customerCooldown: Float;
  private let actionPrice: Int32;
  private let helpPage: Int32;
  private persistent let buyerIDs: array<EntityID>;
  private persistent let buyerUntil: array<Float>;
  private persistent let saleProductIndex: Int32;
  private persistent let saleKind: CName;
  private persistent let saleDealer: CName;
  private persistent let collectLimit: Int32;
  private persistent let runnerState: ref<StreetChemRunnerSystem>;
  private func ResetRuntime() -> Void { this.storageReady = false; this.restored = false; this.restoreIndex = 0; this.nextTick = 0.0; this.action = ""; }
  private func OnAttach() -> Void { this.ResetRuntime(); }
  private func OnRestored(saveVersion: Int32, gameVersion: Int32) -> Void { this.ResetRuntime(); }
  public final func Notify(player: ref<PlayerPuppet>, text: String, opt duration: Float) -> Void {
    let message: SimpleScreenMessage;
    message.isShown = true; message.duration = duration > 0.0 ? duration : 8.0; message.message = text;
    GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UI_Notifications).SetVariant(GetAllBlackboardDefs().UI_Notifications.WarningMessage, ToVariant(message), true);
  }
  private final func Payload(point: Vector4, yaw: Float) -> String {
    return "\"position\":{\"x\":" + ToString(point.X) + ",\"y\":" + ToString(point.Y) + ",\"z\":" + ToString(point.Z) + "},\"yaw\":" + ToString(yaw);
  }
  private final func Bind(placement: ref<StreetChemPlacement>) -> Void {
    SC_Request("bind", SC_NewID(), "{\"world\":" + ToString(this.worldKey) + ",\"guid\":\"" + NameToString(placement.guid) + "\"," + this.Payload(placement.position, placement.yaw) + "}");
  }
  private final func Closest(player: ref<PlayerPuppet>, cameraPosition: Vector4, forward: Vector4) -> ref<StreetChemPlacement> {
    let best: ref<StreetChemPlacement>;
    let distance: Float = 5.0;
    let bestAlignment: Float = 0.50;
    let alignment: Float;
    let direction: Vector4;
    let i: Int32 = 0;
    let d: Float;
    while i < ArraySize(this.placements) {
      d = Vector4.Distance(player.GetWorldPosition(), this.placements[i].position);
      direction = this.placements[i].position - cameraPosition;
      direction.Z += 0.8;
      alignment = Vector4.Dot(Vector4.Normalize(direction), forward);
      if d < distance && alignment > bestAlignment { best = this.placements[i]; bestAlignment = alignment; }
      i += 1;
    }
    return best;
  }
  public final func Tick(player: ref<PlayerPuppet>, cameraPosition: Vector4, forward: Vector4) -> Void {
    let trace: TraceResult;
    let placement: ref<StreetChemPlacement>;
    let state: String;
    let inspect: Bool;
    let adopt: Bool;
    let loadResult: Int32;
    let i: Int32;
    let npc: ref<ScriptedPuppet>;
    let runnerSystem: ref<StreetChemRunnerSystem>;
    let sellPressed: Bool;
    let runnerHandled: Bool;
    let money: ItemID = MarketSystem.Money();
    let transaction: ref<TransactionSystem> = GameInstance.GetTransactionSystem(player.GetGame());
    let now: Float = EngineTime.ToFloat(GameInstance.GetSimTime(player.GetGame()));
    if now < this.nextTick { return; }
    this.nextTick = now + 0.10;
    if SC_Hotkey(72, 1) {
      this.helpPage = (this.helpPage + 1) % 3;
      if this.helpPage == 1 { this.Notify(player, "GROW: Ctrl+B place (1200). Ctrl+R rotate next. Aim at tent: Ctrl+E plant / water / harvest. Ctrl+G inspect. Ctrl+U refill empty tent (300)."); }
      else { if this.helpPage == 2 { this.Notify(player, "SELL: Harvest bags automatically. Ctrl+P bags loose stock. Ctrl+N cycles product and price. Aim at civilian within 4m: Ctrl+S sells one bag. 60s cooldown per customer."); }
      else { this.Notify(player, "RECOVERY: Run both games with paired saves. Ctrl+Shift+B reconnects an unlinked tent for free. Manual-save Cyberpunk after placements and sales. Ctrl+H: next help page."); } }
    }
    npc = GameInstance.GetTargetingSystem(player.GetGame()).GetLookAtObject(player, true, true) as ScriptedPuppet;
    if !IsDefined(this.runnerState) { this.runnerState = new StreetChemRunnerSystem(); }
    runnerSystem = this.runnerState;
    SC_Report("{\"money\":" + ToString(transaction.GetItemQuantity(player, money)) + ",\"product\":\"" + SC_Name(this.productIndex) + "\",\"quantity\":" + ToString(SC_Quantity(this.productIndex)) + ",\"price\":" + ToString(SC_Price(this.productIndex)) + ",\"sale\":\"" + NameToString(this.saleID) + "\",\"stage\":\"" + NameToString(this.saleStage) + "\",\"target\":\"" + (IsDefined(npc) ? EntityID.ToDebugString(npc.GetEntityID()) : "") + "\",\"civilian\":" + ToString(IsDefined(npc) && (npc.IsCrowd() || npc.IsCivilian())) + ",\"distance\":" + ToString(IsDefined(npc) ? Vector4.Distance(player.GetWorldPosition(), npc.GetWorldPosition()) : -1.0) + ",\"runners\":" + runnerSystem.Diagnostic() + "}");
    if !SC_Connected() { this.restored = false; this.restoreIndex = 0; return; }
    if !this.storageReady {
      this.worldKey = GameInstance.GetQuestsSystem(player.GetGame()).GetFactStr("streetchem_world_anchor_key");
      if this.worldKey == 0 {
        this.worldKey = SC_WorldNew();
        GameInstance.GetQuestsSystem(player.GetGame()).SetFactStr("streetchem_world_anchor_key", this.worldKey);
        GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
      }
      loadResult = SC_WorldLoad(this.worldKey, SC_Session());
      if loadResult < 0 { this.Notify(player, "Anchor file unavailable or paired with another Schedule I save"); return; }
      if loadResult == 0 {
        i = 0;
        while i < ArraySize(this.placements) { SC_WorldPut(NameToString(this.placements[i].guid), this.placements[i].position, this.placements[i].yaw); i += 1; }
      }
      ArrayClear(this.placements);
      i = 0;
      while i < SC_WorldCount() {
        placement = new StreetChemPlacement(); placement.guid = StringToName(SC_WorldGUID(i));
        placement.id = placement.guid; placement.position = SC_WorldPosition(i); placement.yaw = SC_WorldYaw(i);
        ArrayPush(this.placements, placement); i += 1;
      }
      this.guestSession = StringToName(SC_Session()); this.storageReady = true;
    }
    if NotEquals(this.guestSession, n"") && NotEquals(this.guestSession, StringToName(SC_Session())) { return; }
    runnerSystem = this.runnerState;
    this.TickSale(player, now);
    runnerSystem.Tick(player, this, now);
    sellPressed = SC_Hotkey(83, 1);
    runnerHandled = runnerSystem.Interact(player, this, cameraPosition, forward, sellPressed);
    if !runnerSystem.MenuOpen() && SC_Hotkey(78, 1) {
      if SC_Count() == 0 { this.Notify(player, "No product stock. Harvest a mature plant first."); }
      else { this.productIndex = (this.productIndex + 1) % SC_Count(); this.ShowStock(player); }
    }
    if sellPressed && !runnerHandled && Equals(this.saleID, n"") {
      npc = GameInstance.GetTargetingSystem(player.GetGame()).GetLookAtObject(player, true, true) as ScriptedPuppet;
      if !IsDefined(npc) || !(npc.IsCrowd() || npc.IsCivilian()) || npc.IsCharacterPolice() || !npc.IsActive() || npc.IsAggressive() || Vector4.Distance(player.GetWorldPosition(), npc.GetWorldPosition()) > 4.0 {
        this.Notify(player, "Look at a living civilian within 4 metres to sell.");
      } else {
        if this.OnCooldown(npc.GetEntityID(), now) { this.Notify(player, "This customer just bought. Try someone else."); }
        else {
          if this.productIndex >= SC_Count() { this.productIndex = 0; }
          if SC_Count() == 0 || SC_Quantity(this.productIndex) < 1 || SC_Price(this.productIndex) < 1 { this.Notify(player, "No sellable product stock. Harvest and bag first."); }
          else {
            this.salePrice = SC_Price(this.productIndex);
            this.saleProductIndex = this.productIndex;
            this.saleKind = n"direct"; this.saleDealer = n"";
            this.saleID = StringToName(SC_NewID()); this.saleStage = n"reserve"; this.customer = npc;
            if !SC_Sale("reserve", NameToString(this.saleID), this.productIndex, 1) { this.saleID = n""; }
            else { this.Notify(player, "Offering one " + SC_Name(this.productIndex) + " for " + ToString(this.salePrice) + " eddies..."); }
          }
        }
      }
    }
    if SC_Hotkey(80, 1) && Equals(this.action, "") {
      this.action = SC_NewID(); this.actionKind = "pack"; this.actionPrice = 0; SC_Request("pack", this.action, "{}");
    }
    if !this.restored {
      if this.restoreIndex < ArraySize(this.placements) { this.Bind(this.placements[this.restoreIndex]); this.restoreIndex += 1; }
      else { this.restored = true; }
    }
    if NotEquals(this.pending, n"") {
      state = SC_Value(NameToString(this.pending), "status");
      if (Equals(state, "preparing") || Equals(state, "")) && now >= this.nextPendingPoll {
        this.nextPendingPoll = now + 0.5;
        SC_Request("placement_status", NameToString(this.pending), "{}");
      }
      if Equals(state, "placed") {
        this.pendingPlacement.guid = StringToName(SC_Value(NameToString(this.pending), "guid")); ArrayPush(this.placements, this.pendingPlacement);
        if !SC_WorldPut(NameToString(this.pendingPlacement.guid), this.pendingPlacement.position, this.pendingPlacement.yaw) { this.Notify(player, "Anchor write failed. Keep this session open."); }
        this.guestSession = StringToName(SC_Session()); this.Bind(this.pendingPlacement);
        this.Notify(player, "Grow tent installed. Ctrl+E: plant / water / harvest. Ctrl+G: inspect.");
        this.pending = n""; this.pendingPlacement = null;
        GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
      } else { if Equals(state, "error") {
        transaction.GiveItem(player, money, this.pendingPrice); this.Notify(player, "Placement failed: " + SC_Value(NameToString(this.pending), "detail"));
        this.pending = n""; this.pendingPlacement = null;
      } }
    }
    if NotEquals(this.action, "") {
      state = SC_Value(this.action, "status");
      if Equals(state, "pot") {
        if Equals(this.actionKind, "inspect") {
          this.Notify(player, "Growth: " + SC_Value(this.action, "growth") + " | water: " + SC_Value(this.action, "water") + " | light: " + SC_Value(this.action, "light")); this.action = "";
        } else {
          if Equals(SC_Value(this.action, "ready"), "true") { this.actionKind = "harvest"; }
          else { if Equals(SC_Value(this.action, "growing"), "false") { this.actionKind = "plant"; } else { this.actionKind = "water"; } }
          this.action = SC_NewID(); SC_Request("grow", this.action, "{\"guid\":\"" + this.actionGuid + "\",\"action\":\"" + this.actionKind + "\"}");
        }
      } else { if Equals(state, "plant") || Equals(state, "water") || Equals(state, "harvested") || Equals(state, "already_planted") || Equals(state, "supplied") || Equals(state, "packed") || Equals(state, "stocked") {
        this.Notify(player, Equals(state, "harvested") ? "Harvest collected and automatically bagged. Ctrl+N: stock. Ctrl+S: sell to aimed civilian." : "Grow tent: " + state); this.action = ""; this.actionPrice = 0;
        GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
      } else { if Equals(state, "error") {
        if this.actionPrice > 0 { transaction.GiveItem(player, money, this.actionPrice); this.actionPrice = 0; }
        if Equals(SC_Value(this.action, "detail"), "pot_missing") {
          this.ForgetMissing(this.actionGuid);
          this.Notify(player, "Removed an old missing placement. Look at your tent and press Ctrl+E again.");
        } else { this.Notify(player, SC_Value(this.action, "detail")); }
        this.action = "";
      } } }
    }
    if SC_Hotkey(82, 1) { this.yaw += 90.0; this.Notify(player, "Next tent rotation: " + ToString(this.yaw)); }
    adopt = SC_Hotkey(66, 3);
    if (adopt || SC_Hotkey(66, 1)) && Equals(this.pending, n"") {
      if ArraySize(this.placements) >= 64 { this.Notify(player, "Placement limit reached"); return; }
      if !adopt && transaction.GetItemQuantity(player, money) < 1200 { this.Notify(player, "Grow tent starter kit: 1200 eddies"); return; }
      if !GameInstance.GetSpatialQueriesSystem(player.GetGame()).SyncRaycastByCollisionGroup(cameraPosition, cameraPosition + forward * 8.0, n"Static", trace, true, false) || trace.normal.Z < 0.65 { this.Notify(player, "Aim at a flat floor within 8 metres"); return; }
      placement = new StreetChemPlacement(); placement.id = StringToName(SC_NewID()); placement.position = Cast<Vector4>(trace.position); placement.yaw = this.yaw;
      if !SC_Request(adopt ? "adopt" : "place", NameToString(placement.id), "{\"equipment\":\"growtent\",\"starter\":true}") { return; }
      if !adopt { transaction.RemoveItem(player, money, 1200); }
      this.pending = placement.id; this.pendingPlacement = placement; this.pendingPrice = adopt ? 0 : 1200;
      this.Notify(player, "Installing grow tent...");
    }
    inspect = SC_Hotkey(71, 1);
    let supply: Bool = SC_Hotkey(85, 1);
    if (inspect || supply || SC_Hotkey(69, 1)) && Equals(this.action, "") {
      placement = this.Closest(player, cameraPosition, forward);
      if !IsDefined(placement) { this.Notify(player, "Look at your grow tent within 5 metres"); return; }
      this.actionGuid = NameToString(placement.guid); this.action = SC_NewID(); this.actionKind = inspect ? "inspect" : "operate";
      if supply {
        if transaction.GetItemQuantity(player, money) < 300 { this.Notify(player, "Seed and soil refill: 300 eddies"); this.action = ""; return; }
        if SC_Request("grow", this.action, "{\"guid\":\"" + this.actionGuid + "\",\"action\":\"supply\"}") { transaction.RemoveItem(player, money, 300); this.actionPrice = 300; }
        else { this.action = ""; }
        return;
      }
      SC_Request("pot_status", this.action, "{\"guid\":\"" + NameToString(placement.guid) + "\"}");
    }
  }
  private final func ShowStock(player: ref<PlayerPuppet>) -> Void {
    this.Notify(player, SC_Name(this.productIndex) + " | stock " + ToString(SC_Quantity(this.productIndex)) + " | " + ToString(SC_Price(this.productIndex)) + " eddies each. Ctrl+S: sell.");
  }
  private final func TickSale(player: ref<PlayerPuppet>, now: Float) -> Void {
    if Equals(this.saleID, n"") { return; }
    let id: String = NameToString(this.saleID);
    let state: String = SC_Status(id);
    if Equals(state, "reserved") && Equals(this.saleStage, n"reserve") {
      if !IsDefined(this.customer) || !this.customer.IsActive() || (NotEquals(this.saleKind, n"runner") && Vector4.Distance(player.GetWorldPosition(), this.customer.GetWorldPosition()) > 5.0) {
        SC_Sale("abort", id, this.productIndex, 1); this.saleID = n""; this.Notify(player, "Customer walked away. Stock retained."); return;
      }
      if StringToInt(SC_Value(id, "price")) > 0 { this.salePrice = StringToInt(SC_Value(id, "price")); }
      this.saleStage = n"commit"; SC_Sale("commit", id, this.productIndex, 1); this.saleNextPoll = now + 0.5;
    } else {
      if Equals(state, "committed") {
        if StringToInt(SC_Value(id, "price")) > 0 { this.salePrice = StringToInt(SC_Value(id, "price")); }
        let paidFact: String = "streetchem_paid_" + id;
        if this.salePrice <= 0 || (NotEquals(this.saleKind, n"runner") && GameInstance.GetQuestsSystem(player.GetGame()).GetFactStr(paidFact) == 0 && GameInstance.GetTransactionSystem(player.GetGame()).GetItemQuantity(player, MarketSystem.Money()) > 2147483647 - this.salePrice) {
          if now >= this.saleNextPoll { this.Notify(player, "Payout held: eddy balance has no room. Spend some eddies, then retry."); this.saleNextPoll = now + 8.0; } return;
        }
        if NotEquals(this.saleKind, n"runner") && GameInstance.GetQuestsSystem(player.GetGame()).GetFactStr(paidFact) == 0 {
          GameInstance.GetTransactionSystem(player.GetGame()).GiveItem(player, MarketSystem.Money(), this.salePrice);
          GameInstance.GetQuestsSystem(player.GetGame()).SetFactStr(paidFact, 1);
        }
        if IsDefined(this.customer) { this.MarkBuyer(this.customer.GetEntityID(), now + 60.0); }
        this.customerCooldown = now + 60.0;
        this.Notify(player, Equals(this.saleKind, n"runner") ? "Runner sold a unit. Earnings are ready to collect with Ctrl+S." : Equals(this.saleKind, n"collect") ? "Collected " + ToString(this.salePrice) + " eddies from your runner. Save Cyberpunk." : "Sold one unit for " + ToString(this.salePrice) + " eddies. Save Cyberpunk to retain payment.");
        this.saleID = n""; this.saleStage = n"";
        GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
      } else {
        if Equals(state, "error") || Equals(state, "aborted") {
          this.Notify(player, Equals(SC_Value(id, "detail"), "runner_has_no_cash") ? "This runner has no earnings to collect yet." : "Trade interrupted. Check your stock, then try again."); this.saleID = n""; this.saleStage = n"";
        } else {
          if now >= this.saleNextPoll {
            if Equals(this.saleStage, n"collect") { SC_Request("collect", id, "{\"dealer\":\"" + NameToString(this.saleDealer) + "\",\"limit\":" + ToString(this.collectLimit) + "}"); }
            else { if Equals(this.saleStage, n"commit") { SC_Sale("commit", id, this.saleProductIndex, 1); }
            else { this.ReserveSale(); } }
            this.saleNextPoll = now + 0.5;
          }
        }
      }
    }
  }
  public final func World() -> Int32 { return this.worldKey; }
  public final func SaleBusy() -> Bool { return NotEquals(this.saleID, n""); }
  public final func BuyerCooling(id: EntityID, now: Float) -> Bool { return this.OnCooldown(id, now); }
  private final func ReserveSale() -> Void {
    if Equals(this.saleKind, n"runner") { SC_RunnerReserve(NameToString(this.saleID), NameToString(this.saleDealer)); }
    else { SC_Sale("reserve", NameToString(this.saleID), this.saleProductIndex, 1); }
  }
  public final func BeginRunnerSale(player: ref<PlayerPuppet>, target: ref<ScriptedPuppet>, dealer: String) -> Void {
    if this.SaleBusy() { return; }
    this.customer = target; this.saleID = StringToName(SC_NewID()); this.saleKind = n"runner"; this.saleDealer = StringToName(dealer); this.saleStage = n"reserve";
    this.ReserveSale();
  }
  public final func CollectRunner(player: ref<PlayerPuppet>, dealer: String) -> Void {
    if this.SaleBusy() { this.Notify(player, "Finish the current trade first."); return; }
    this.collectLimit = Min(1000000, 2147483647 - GameInstance.GetTransactionSystem(player.GetGame()).GetItemQuantity(player, MarketSystem.Money()));
    if this.collectLimit <= 0 { this.Notify(player, "Your eddy balance is full. Spend some before collecting."); return; }
    this.salePrice = 0;
    this.customer = null; this.saleID = StringToName(SC_NewID()); this.saleKind = n"collect"; this.saleDealer = StringToName(dealer); this.saleStage = n"collect";
    SC_Request("collect", NameToString(this.saleID), "{\"dealer\":\"" + dealer + "\",\"limit\":" + ToString(this.collectLimit) + "}");
  }
  public final func StockRunner(player: ref<PlayerPuppet>, dealer: String) -> Void {
    if NotEquals(this.action, "") || this.SaleBusy() { this.Notify(player, "Finish the current interaction first."); return; }
    let quantity: Int32 = SC_Quantity(this.productIndex);
    if quantity > 5 { quantity = 5; }
    if quantity < 1 { this.Notify(player, "No selected product stock. Ctrl+N selects product."); return; }
    this.action = SC_NewID(); this.actionKind = "stock"; this.actionPrice = 0;
    SC_Request("runner_stock", this.action, "{\"dealer\":\"" + dealer + "\",\"slot\":" + ToString(SC_ProductSlot(this.productIndex)) + ",\"quantity\":" + ToString(quantity) + "}");
  }
  private final func OnCooldown(id: EntityID, now: Float) -> Bool {
    let i: Int32 = 0;
    while i < ArraySize(this.buyerIDs) { if this.buyerIDs[i] == id && now < this.buyerUntil[i] { return true; } i += 1; }
    return false;
  }
  private final func MarkBuyer(id: EntityID, until: Float) -> Void {
    let i: Int32 = 0;
    while i < ArraySize(this.buyerIDs) { if this.buyerIDs[i] == id { this.buyerUntil[i] = until; return; } i += 1; }
    if ArraySize(this.buyerIDs) >= 128 { ArrayErase(this.buyerIDs, 0); ArrayErase(this.buyerUntil, 0); }
    ArrayPush(this.buyerIDs, id); ArrayPush(this.buyerUntil, until);
  }
  private final func ForgetMissing(guid: String) -> Void {
    SC_WorldRemove(guid);
    let i: Int32 = ArraySize(this.placements) - 1;
    while i >= 0 {
      if Equals(NameToString(this.placements[i].guid), guid) { ArrayErase(this.placements, i); }
      i -= 1;
    }
  }
}
