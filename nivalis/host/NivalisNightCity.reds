public native func NC_Connected() -> Bool;
public native func NC_Clock() -> Float;
public native func NC_ReplyState(id: String) -> String;
public native func NC_DeveloperManagerTravel() -> Int32;
public native func NC_NewID() -> String;
public native func NC_Lease(pair: String) -> Bool;
public native func NC_Count() -> Int32;
public native func NC_VenueValue(index: Int32, field: String) -> String;
public native func NC_Day() -> Int32;
public native func NC_LastRestockDay() -> Int32;
public native func NC_AutoRestock() -> Bool;
public native func NC_ShoppingCost() -> Int32;
public native func NC_BeginRestock(pair: String, id: String, budget: Int32, before: Int32, after: Int32) -> Bool;
public native func NC_RestockState(id: String) -> String;
public native func NC_RestockValue(id: String, field: String) -> Int32;
public native func NC_SettleRestock(id: String, balance: Int32) -> Bool;
public native func NC_Hotkey(key: Int32, modifiers: Int32) -> Bool;
public native func NC_ToggleRestock() -> Bool;
public native func NC_Request(op: String, id: String, args: String) -> Bool;
public native func NC_MenuValue(index: Int32, dish: Int32, field: String) -> String;
public native func NC_CashNet() -> Int32;
public native func NC_LastCashDay() -> Int32;
public native func NC_BeginCash(pair: String, id: String, budget: Int32, before: Int32, after: Int32) -> Bool;
public native func NC_Report(state: String) -> Void;
public native func NC_RefundReady(id: String, pair: String, budget: Int32) -> Bool;
public native func NC_RefundAmount(id: String, pair: String, budget: Int32) -> Int32;
public native func NC_FinishRefund(id: String, balance: Int32) -> Bool;

public class NightCityBusinessTick extends Event {}

public class NightCityBusinessSystem extends ScriptableSystem {
  public final func RequestRestock() -> Void { this.manualRestock = true; this.restockRetryTicks = 0; }
  public final func RequestCash() -> Void { this.manualCash = true; this.cashAttemptedDay = -1; }
  private persistent let pair: CName;
  private persistent let restockId: CName;
  private persistent let restockStage: CName;
  private persistent let restockBudget: Int32;
  private persistent let attemptedDay: Int32;
  private let warnedFundsDay: Int32;
  private persistent let cashId: CName;
  private persistent let cashStage: CName;
  private persistent let cashBudget: Int32;
  private persistent let cashAttemptedDay: Int32;
  private let manualRestock: Bool;
  private let manualCash: Bool;
  private let restockRetryTicks: Int32;
  private let announceRestock: Bool;

  private final func Notify(player: ref<PlayerPuppet>, text: String) -> Void {
    let message: SimpleScreenMessage;
    message.isShown = true; message.duration = 8.0; message.message = text;
    GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UI_Notifications).SetVariant(GetAllBlackboardDefs().UI_Notifications.WarningMessage, ToVariant(message), true);
  }

  public final func Tick(player: ref<PlayerPuppet>) -> Void {
    let refund: Int32;
    if this.restockRetryTicks > 0 { this.restockRetryTicks -= 1; }
    if Equals(this.restockStage, n"refunding") && Equals(NC_RestockState(NameToString(this.restockId)), "cancelled_refunded") {
      this.restockStage = n""; this.attemptedDay = -1;
      GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
    }
    if (Equals(this.restockStage, n"recovery") || Equals(this.restockStage, n"")) && NC_RefundReady(NameToString(this.restockId), NameToString(this.pair), this.restockBudget) {
      // Only a verified guest checkpoint restore authorizes this; never infer a refund from a timeout.
      refund = NC_RefundAmount(NameToString(this.restockId), NameToString(this.pair), this.restockBudget);
      if refund <= 0 { return; }
      this.restockStage = n"refunding";
      GameInstance.GetTransactionSystem(player.GetGame()).GiveItem(player, MarketSystem.Money(), refund);
      if NC_FinishRefund(NameToString(this.restockId), GameInstance.GetTransactionSystem(player.GetGame()).GetItemQuantity(player, MarketSystem.Money())) {
        this.Notify(player, "Supply test restored; â‚¬$" + ToString(refund) + " refunded."); this.restockStage = n"";
      }
      GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
    }
    NC_Report("{\"money\":" + ToString(GameInstance.GetTransactionSystem(player.GetGame()).GetItemQuantity(player, MarketSystem.Money())) + ",\"pair\":\"" + NameToString(this.pair) + "\",\"restockId\":\"" + NameToString(this.restockId) + "\",\"restockStage\":\"" + NameToString(this.restockStage) + "\",\"cashId\":\"" + NameToString(this.cashId) + "\",\"cashStage\":\"" + NameToString(this.cashStage) + "\",\"managers\":" + (GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"NightCityBusinessManagers") as NightCityBusinessManagers).Diagnostic() + "}");
    if NC_Hotkey(85, 5) { NC_Request("nc-maintenance", NC_NewID(), "{\"enabled\":\"false\"}"); this.Notify(player, "Starting businesses. Keep Nivalis running with your paired save."); }
    if !NC_Connected() { return; }
    if Equals(this.pair, n"") { this.pair = StringToName(NC_NewID()); }
    NC_Lease(NameToString(this.pair));
    if NC_Hotkey(82, 5) { this.manualRestock = true; this.attemptedDay = -1; }
    if NC_Hotkey(67, 5) { this.manualCash = true; this.cashAttemptedDay = -1; }
    if NC_Hotkey(65, 5) { this.Notify(player, "Continuous ingredient restocking: " + (NC_ToggleRestock() ? "ON" : "OFF")); }
    if Equals(this.cashStage, n"") { this.Restock(player); }
    if Equals(this.restockStage, n"") { this.Cash(player); }
    (GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"NightCityBusinessSites") as NightCityBusinessSites).Tick(player, this);
  }

  private final func Cash(player: ref<PlayerPuppet>) -> Void {
    let ts: ref<TransactionSystem> = GameInstance.GetTransactionSystem(player.GetGame());
    let state: String;
    let give: Int32;
    let balance: Int32;
    let after: Int32;
    let net: Int32;
    let day: Int32 = NC_Day();
    if Equals(this.cashStage, n"waiting") {
      state = NC_RestockState(NameToString(this.cashId));
      if Equals(state, "committed") {
        give = NC_RestockValue(NameToString(this.cashId), "GiveEddies");
        if give < 0 || give > 40000000 { this.cashStage = n"recovery"; return; }
        this.cashStage = n"settling";
        if give > 0 { ts.GiveItem(player, MarketSystem.Money(), give); }
        if !NC_SettleRestock(NameToString(this.cashId), ts.GetItemQuantity(player, MarketSystem.Money())) { this.cashStage = n"recovery"; return; }
        this.Notify(player, "Business income after wages and rent: â‚¬$" + ToString(NC_RestockValue(NameToString(this.cashId), "NetEddies")) + ".");
        this.cashStage = n"";
        GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
      } else { if Equals(state, "needs_recovery") { this.cashStage = n"recovery"; this.Notify(player, "Business cash transfer needs recovery. Automatic transfers paused."); } }
      return;
    }
    if NotEquals(this.cashStage, n"") || day < 0 || this.cashAttemptedDay == day || (!this.manualCash && NC_LastCashDay() == day) { return; }
    this.manualCash = false;
    net = NC_CashNet();
    if net == 0 { return; }
    this.cashBudget = net < 0 ? -net : 0;
    balance = ts.GetItemQuantity(player, MarketSystem.Money());
    if balance < this.cashBudget { this.Notify(player, "Business wages and rent need â‚¬$" + ToString(this.cashBudget) + ". Add funds before settling."); this.cashAttemptedDay = day; return; }
    this.cashId = StringToName(NC_NewID()); this.cashStage = n"reserving";
    if this.cashBudget > 0 && !ts.RemoveItem(player, MarketSystem.Money(), this.cashBudget) { this.cashStage = n""; return; }
    after = ts.GetItemQuantity(player, MarketSystem.Money());
    if balance - after != this.cashBudget { this.cashStage = n"recovery"; return; }
    if !NC_BeginCash(NameToString(this.pair), NameToString(this.cashId), this.cashBudget, balance, after) {
      if this.cashBudget > 0 { ts.GiveItem(player, MarketSystem.Money(), this.cashBudget); }
      this.cashStage = n""; return;
    }
    this.cashAttemptedDay = day; this.cashStage = n"waiting";
    GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
  }

  private final func Restock(player: ref<PlayerPuppet>) -> Void {
    let quotedBudget: Int32;
    let ts: ref<TransactionSystem> = GameInstance.GetTransactionSystem(player.GetGame());
    let state: String;
    let refund: Int32;
    let balance: Int32;
    let after: Int32;
    let day: Int32;
    if Equals(this.restockStage, n"waiting") {
      state = NC_RestockState(NameToString(this.restockId));
      if Equals(state, "committed") {
        refund = NC_RestockValue(NameToString(this.restockId), "RefundEddies");
        if refund < 0 || refund > this.restockBudget { this.restockStage = n"recovery"; return; }
        // Mark the refund before applying it; a replay must never refund twice.
        this.restockStage = n"settling";
        if refund > 0 { ts.GiveItem(player, MarketSystem.Money(), refund); }
        balance = ts.GetItemQuantity(player, MarketSystem.Money());
        if !NC_SettleRestock(NameToString(this.restockId), balance) { this.restockStage = n"recovery"; return; }
        if this.announceRestock {
          this.Notify(player, "Businesses restocked: " + ToString(NC_RestockValue(NameToString(this.restockId), "Bought")) + " items, â‚¬$" + ToString(NC_RestockValue(NameToString(this.restockId), "ChargeEddies")) + ".");
        }
        this.restockStage = n"";
        GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
      } else {
        if Equals(state, "needs_recovery") {
          this.restockStage = n"recovery";
          this.Notify(player, "Business supply transaction needs recovery. Funds remain reserved; automatic purchases paused.");
        }
      }
      return;
    }
    if NotEquals(this.restockStage, n"") || (!NC_AutoRestock() && !this.manualRestock) { return; }
    day = NC_Day();
    if day < 0 || (!this.manualRestock && this.restockRetryTicks > 0) { return; }
    quotedBudget = NC_ShoppingCost();
    if quotedBudget <= 0 { return; }
    this.restockRetryTicks = 5;
    balance = ts.GetItemQuantity(player, MarketSystem.Money());
    if balance < quotedBudget {
      if this.warnedFundsDay != day { this.Notify(player, "Business shopping list needs â‚¬$" + ToString(quotedBudget) + ". Add funds to restock."); this.warnedFundsDay = day; }
      return;
    }
    this.restockBudget = quotedBudget;
    this.restockId = StringToName(NC_NewID());
    this.restockStage = n"reserving";
    if !ts.RemoveItem(player, MarketSystem.Money(), this.restockBudget) { this.restockStage = n""; return; }
    after = ts.GetItemQuantity(player, MarketSystem.Money());
    if balance - after != this.restockBudget { this.restockStage = n"recovery"; return; }
    if !NC_BeginRestock(NameToString(this.pair), NameToString(this.restockId), this.restockBudget, balance, after) {
      ts.GiveItem(player, MarketSystem.Money(), this.restockBudget);
      this.restockStage = n"";
      return;
    }
    this.attemptedDay = day;
    this.restockStage = n"waiting";
    this.announceRestock = this.manualRestock;
    this.manualRestock = false;
    GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
  }
}

@addField(PlayerPuppet)
private let ncBusinessActive: Bool;

@addField(PlayerPuppet)
private let ncBusinessDelay: DelayID;

@wrapMethod(PlayerPuppet)
protected cb func OnGameAttached() -> Bool {
  wrappedMethod();
  this.ncBusinessActive = true;
  this.ncManagerDelay = GameInstance.GetDelaySystem(this.GetGame()).DelayEvent(this, new NightCityManagerTick(), 0.10, false);
  this.ncBusinessDelay = GameInstance.GetDelaySystem(this.GetGame()).DelayEvent(this, new NightCityBusinessTick(), 1.0, false);
}

@wrapMethod(PlayerPuppet)
protected cb func OnDetach() -> Bool {
  this.ncBusinessActive = false;
  GameInstance.GetDelaySystem(this.GetGame()).CancelDelay(this.ncBusinessDelay);
  GameInstance.GetDelaySystem(this.GetGame()).CancelDelay(this.ncManagerDelay);
  (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"NightCityBusinessSites") as NightCityBusinessSites).Stop(this);
  (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"NightCityBusinessManagers") as NightCityBusinessManagers).Stop(this);
  wrappedMethod();
}

@addMethod(PlayerPuppet)
protected cb func OnNightCityBusinessTick(evt: ref<NightCityBusinessTick>) -> Bool {
  if !this.ncBusinessActive { return false; }
  if !GameInstance.GetBlackboardSystem(this.GetGame()).Get(GetAllBlackboardDefs().UI_System).GetBool(GetAllBlackboardDefs().UI_System.IsInMenu) {
    (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"NightCityBusinessSystem") as NightCityBusinessSystem).Tick(this);
  }
  this.ncBusinessDelay = GameInstance.GetDelaySystem(this.GetGame()).DelayEvent(this, new NightCityBusinessTick(), 1.0, false);
}

public class NightCityManagerTick extends Event {}
@addField(PlayerPuppet)
private let ncManagerDelay: DelayID;
@addMethod(PlayerPuppet)
protected cb func OnNightCityManagerTick(evt: ref<NightCityManagerTick>) -> Bool {
  if !this.ncBusinessActive { return false; }
  if !GameInstance.GetBlackboardSystem(this.GetGame()).Get(GetAllBlackboardDefs().UI_System).GetBool(GetAllBlackboardDefs().UI_System.IsInMenu) {
    (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"NightCityBusinessManagers") as NightCityBusinessManagers).Tick(this, GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"NightCityBusinessSystem") as NightCityBusinessSystem);
  }
  this.ncManagerDelay = GameInstance.GetDelaySystem(this.GetGame()).DelayEvent(this, new NightCityManagerTick(), 0.10, false);
}
