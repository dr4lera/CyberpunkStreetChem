public native func SC_Clock() -> Float;
public native func SC_ShopCount() -> Int32;
public native func SC_ShopValue(index: Int32, field: String) -> String;

public class StreetChemDealerTweak extends ScriptableTweak {
  protected cb func OnApply() -> Void {
    // Reuse an owned vanilla civilian model, without modifying its original quest record.
    let empty: array<TweakDBID>;
    TweakDBManager.CloneRecord(t"Character.StreetChemDealer", t"Character.q000_hym_ma");
    TweakDBManager.CreateRecord(t"Vendors.StreetChem", n"Vendor");
    TweakDBManager.SetFlat(t"Vendors.StreetChem.vendorType", t"VendorType.Medical");
    TweakDBManager.SetFlat(t"Vendors.StreetChem.itemStock", empty);
    TweakDBManager.SetFlat(t"Vendors.StreetChem.itemQueries", empty);
    TweakDBManager.SetFlat(t"Vendors.StreetChem.accessPrereqs", empty);
    TweakDBManager.SetFlat(t"Vendors.StreetChem.mapVisibilityPrereqs", empty);
    TweakDBManager.SetFlat(t"Vendors.StreetChem.localizedName", "Street Chem");
    TweakDBManager.SetFlat(t"Vendors.StreetChem.localizedDescription", "Unlimited supply of native Schedule I products");
    TweakDBManager.UpdateRecord(t"Vendors.StreetChem");
    TweakDBManager.SetFlat(t"Character.StreetChemDealer.vendorID", t"Vendors.StreetChem");
    TweakDBManager.UpdateRecord(t"Character.StreetChemDealer");
  }
}

public class StreetChemDealerSystem extends ScriptableSystem {
  private persistent let purchaseID: CName;
  private persistent let purchaseKey: CName;
  private persistent let purchaseSession: CName;
  private persistent let purchaseStage: CName;
  private persistent let purchasePrice: Int32;
  private let nextPoll: Float;
  private let nextWorld: Float;
  private persistent let uiRequest: Int32;
  private persistent let purchaseRecord: TweakDBID;
  private let nextOpen: Float;
  private let listenerAttached: Bool;
  private let promptShown: Bool;
  private let nextHint: Float;
  private let body: wref<NPCPuppet>;
  private let hold: ref<AIHoldPositionCommand>;
  public static func Rotation(yaw: Float) -> EulerAngles { let rotation: EulerAngles; rotation.Yaw = yaw; return rotation; }
  public static func Position() -> Vector4 { return new Vector4(-1391.0, 1262.7, 123.0824, 1.0); }
  public static func IsDealer(puppet: ref<ScriptedPuppet>) -> Bool { return IsDefined(puppet) && puppet.GetRecordID() == t"Character.StreetChemDealer"; }
  public func Busy() -> Bool { return NotEquals(this.purchaseID, n""); }
  public func Diagnostic() -> String { return "{\"present\":" + ToString(IsDefined(this.body)) + ",\"prompt\":" + ToString(this.promptShown) + ",\"pending\":\"" + NameToString(this.purchaseID) + "\",\"stage\":\"" + NameToString(this.purchaseStage) + "\",\"price\":" + ToString(this.purchasePrice) + "}"; }
  private func OnAttach() -> Void { this.nextWorld = 0.0; this.nextPoll = 0.0; this.listenerAttached = false; this.promptShown = false; }
  private func OnRestored(saveVersion: Int32, gameVersion: Int32) -> Void { this.nextWorld = 0.0; this.nextPoll = 0.0; this.listenerAttached = false; this.promptShown = false; }
  public func TickWorld(player: ref<PlayerPuppet>) -> Void {
    let now: Float = SC_Clock();
    let entities: ref<DynamicEntitySystem>;
    let spec: ref<DynamicEntitySpec>;
    let id: EntityID;
    this.Poll(player, GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"StreetChemCitySystem") as StreetChemCitySystem, now);
    if !this.listenerAttached { player.RegisterInputListener(this, n"Choice1"); player.RegisterInputListener(this, n"ChoiceApply"); this.listenerAttached = true; }
    this.Prompt(player);
    if now < this.nextWorld { return; }
    this.nextWorld = now + 1.0;
    entities = GameInstance.GetDynamicEntitySystem();
    if !IsDefined(entities) || !entities.IsReady() || !entities.IsRestored() { return; }
    entities.DeleteTagged(n"StreetChemApartmentDealer");
    id = entities.GetTaggedID(n"StreetChemHallwayDealerV2");
    if !entities.IsManaged(id) {
      spec = new DynamicEntitySpec(); spec.recordID = t"Character.StreetChemDealer";
      spec.position = StreetChemDealerSystem.Position(); spec.orientation = EulerAngles.ToQuat(StreetChemDealerSystem.Rotation(0.0));
      spec.persistSpawn = true; spec.persistState = false; spec.alwaysSpawned = false; spec.spawnInView = true; spec.active = true;
      ArrayPush(spec.tags, n"StreetChemHallwayDealerV2"); id = entities.CreateEntity(spec);
    }
    this.body = entities.GetEntity(id) as NPCPuppet;
    if IsDefined(this.body) {
      if Vector4.Distance(this.body.GetWorldPosition(), StreetChemDealerSystem.Position()) > 0.6 {
        GameInstance.GetTeleportationFacility(player.GetGame()).Teleport(this.body, StreetChemDealerSystem.Position(), StreetChemDealerSystem.Rotation(0.0));
      }
      if !IsDefined(this.hold) { this.hold = new AIHoldPositionCommand(); this.hold.duration = -1.0; this.body.GetAIControllerComponent().SendCommand(this.hold); }
      this.body.EnableInteraction(n"GenericTalk", true);
      this.body.StreetChemShopChoice();
      this.body.GetAttitudeAgent().SetAttitudeTowards(player.GetAttitudeAgent(), EAIAttitude.AIA_Friendly);
    } else { this.hold = null; }
  }
  public func Stop(player: ref<PlayerPuppet>) -> Void { player.UnregisterInputListener(this); this.ClearPrompt(player); this.listenerAttached = false; }
  private func ClearPrompt(player: ref<PlayerPuppet>) -> Void {
    let bb: ref<IBlackboard> = GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UIInteractions);
    let hub: InteractionChoiceHubData = FromVariant<InteractionChoiceHubData>(bb.GetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub));
    if hub.id == 670202 { hub.active = false; ArrayClear(hub.choices); bb.SetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub, ToVariant(hub), true); }
    this.promptShown = false;
  }
  private func Prompt(player: ref<PlayerPuppet>) -> Void {
    let hub: InteractionChoiceHubData;
    let choice: InteractionChoiceData;
    let camera: ref<CameraSystem> = GameInstance.GetCameraSystem(player.GetGame());
    let pose: Transform;
    let direction: Vector4;
    let point: Vector4;
    let looking: Bool = false;
    if IsDefined(this.body) && !player.IsInCombat() && !GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UI_System).GetBool(GetAllBlackboardDefs().UI_System.IsInMenu) && Vector4.Distance(player.GetWorldPosition(), this.body.GetWorldPosition()) < 4.0 && camera.GetActiveCameraWorldTransform(pose) {
      point = this.body.GetWorldPosition(); point.Z += 1.2; direction = point - Transform.GetPosition(pose);
      looking = Vector4.Dot(Vector4.Normalize(direction), camera.GetActiveCameraForward()) > 0.85;
    }
    if !looking { if this.promptShown { this.ClearPrompt(player); } return; }
    hub.id = 670202; hub.active = true; hub.title = "Street Chem dealer"; hub.flags = EVisualizerDefinitionFlags.None;
    choice.inputAction = n"Choice1"; choice.localizedName = "Browse drugs"; choice.isHoldAction = false;
    ArrayPush(hub.choices, choice);
    GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UIInteractions).SetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub, ToVariant(hub), true);
    this.promptShown = true;
  }
  protected cb func OnAction(action: ListenerAction, consumer: ListenerActionConsumer) -> Bool {
    let player: ref<PlayerPuppet> = GetPlayer(this.GetGameInstance());
    if this.promptShown && (Equals(ListenerAction.GetName(action), n"Choice1") || Equals(ListenerAction.GetName(action), n"ChoiceApply")) && ListenerAction.IsButtonJustReleased(action) {
      this.ClearPrompt(player); this.Open(player, this.body); return true;
    }
    return false;
  }
  private func Payload() -> String { return "{\"key\":\"" + NameToString(this.purchaseKey) + "\"}"; }
  public func Open(player: ref<PlayerPuppet>, owner: ref<NPCPuppet>) -> Void {
    let city: ref<StreetChemCitySystem> = GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"StreetChemCitySystem") as StreetChemCitySystem;
    let data: ref<VendorPanelData>;
    if SC_Clock() < this.nextOpen || player.IsInCombat() || Vector4.Distance(player.GetWorldPosition(), owner.GetWorldPosition()) > 4.0 { return; }
    this.nextOpen = SC_Clock() + 1.0;
    if !SC_Connected() || !city.ConsumablesReady() { city.Notify(player, "Load the paired Schedule I solo save to browse native stock."); return; }
    this.body = owner;
    MarketSystem.GetInstance(player.GetGame()).StreetChemAttach(owner);
    data = new VendorPanelData(); data.data.vendorId = "Vendors.StreetChem"; data.data.entityID = owner.GetEntityID(); data.data.isActive = true;
    GameInstance.GetUISystem(player.GetGame()).RequestVendorMenu(data);
  }
  public func Stock(owner: ref<GameObject>) -> array<SItemStack> {
    let stock: array<SItemStack>;
    let stack: SItemStack;
    let item: ItemID;
    let i: Int32 = 0;
    let j: Int32;
    let tx: ref<TransactionSystem> = GameInstance.GetTransactionSystem(owner.GetGame());
    while i < SC_ShopCount() {
      j = 0;
      while j < SC_DoseCount() {
        if Equals(SC_DoseValue(j, "key"), SC_ShopValue(i, "key")) {
          StreetChemDrugs.Define(j); item = ItemID.FromTDBID(StreetChemDrugs.ID(j));
          if tx.GetItemQuantity(owner, item) < 1 { tx.GiveItem(owner, item, 1); }
          stack.itemID = item; stack.quantity = 1; stack.isAvailable = true;
          stack.dynamicTags = TweakDBInterface.GetItemRecord(StreetChemDrugs.ID(j)).Tags();
          ArrayPush(stock, stack); break;
        }
        j += 1;
      }
      i += 1;
    }
    return stock;
  }
  public func CompleteUI(player: ref<PlayerPuppet>, success: Bool) -> Void {
    let event: ref<UIVendorItemsBoughtEvent> = new UIVendorItemsBoughtEvent();
    event.requestID = this.uiRequest;
    if success { ArrayPush(event.itemsID, ItemID.FromTDBID(this.purchaseRecord)); ArrayPush(event.quantity, 1); }
    GameInstance.GetUISystem(player.GetGame()).QueueEvent(event);
  }
  public func Buy(player: ref<PlayerPuppet>, item: ItemID, quantity: Int32, request: Int32) -> Void {
    let city: ref<StreetChemCitySystem> = GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"StreetChemCitySystem") as StreetChemCitySystem;
    if this.Busy() || city.SaleBusy() || (GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"StreetChemDoseSystem") as StreetChemDoseSystem).Busy() {
      let event: ref<UIVendorItemsBoughtEvent> = new UIVendorItemsBoughtEvent(); event.requestID = request; GameInstance.GetUISystem(player.GetGame()).QueueEvent(event);
      city.Notify(player, "Finish the current stock transaction first."); return;
    }
    this.uiRequest = request; this.purchaseRecord = ItemID.GetTDBID(item);
    if quantity != 1 || !StreetChemDrugs.IsDrug(this.purchaseRecord) || !SC_Connected() || !city.ConsumablesReady() { this.CompleteUI(player, false); return; }
    this.purchaseID = StringToName(SC_NewID()); this.purchaseKey = StringToName(TweakDBInterface.GetString(this.purchaseRecord + t".streetChemKey", ""));
    this.purchaseSession = StringToName(SC_Session()); this.purchaseStage = n"quote"; this.purchasePrice = 0; this.nextPoll = 0.0;
    if !SC_Request("buy_reserve", NameToString(this.purchaseID), this.Payload()) { this.purchaseID = n""; this.CompleteUI(player, false); city.Notify(player, "Stock bridge busy. Try again."); }
  }
  private func Poll(player: ref<PlayerPuppet>, city: ref<StreetChemCitySystem>, now: Float) -> Void {
    let id: String = NameToString(this.purchaseID);
    let state: String;
    let price: Int32;
    let tx: ref<TransactionSystem> = GameInstance.GetTransactionSystem(player.GetGame());
    let quests: ref<QuestsSystem> = GameInstance.GetQuestsSystem(player.GetGame());
    if !this.Busy() || !SC_Connected() || now < this.nextPoll { return; }
    this.nextPoll = now + 0.5;
    if NotEquals(this.purchaseSession, StringToName(SC_Session())) { return; }
    state = SC_Status(id);
    if Equals(state, "committed") {
      (GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"StreetChemDoseSystem") as StreetChemDoseSystem).Purchased(player, this.purchaseRecord);
      this.CompleteUI(player, true);
      city.Notify(player, "Purchased one package for " + ToString(this.purchasePrice) + " eddies. Stock is in your linked inventory. Save Cyberpunk.");
      this.purchaseID = n""; this.purchaseStage = n"";
      GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
    } else {
      if Equals(this.purchaseStage, n"quote") && Equals(state, "reserved") {
        price = StringToInt(SC_Value(id, "price"));
        if price < 1 || price > 1000000 || tx.GetItemQuantity(player, MarketSystem.Money()) < price {
          SC_Request("buy_abort", id, this.Payload()); this.purchaseID = n""; this.CompleteUI(player, false); city.Notify(player, "Not enough eddies for this package."); return;
        }
        this.purchasePrice = price;
        if quests.GetFactStr("streetchem_purchase_" + id) == 0 {
          if !tx.RemoveItem(player, MarketSystem.Money(), price) { SC_Request("buy_abort", id, this.Payload()); this.purchaseID = n""; this.CompleteUI(player, false); city.Notify(player, "Eddy payment failed; no stock was purchased."); return; }
          quests.SetFactStr("streetchem_purchase_" + id, 1);
        }
        this.purchaseStage = n"commit";
        GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
        SC_Request("buy_commit", id, this.Payload());
      } else {
        if Equals(state, "error") && Equals(this.purchaseStage, n"quote") {
          city.Notify(player, "Purchase unavailable: " + SC_Value(id, "detail")); this.purchaseID = n""; this.CompleteUI(player, false);
        } else {
          if Equals(state, "error") && Equals(this.purchaseStage, n"commit") && now >= this.nextHint {
            this.nextHint = now + 15.0; city.Notify(player, "Purchase needs recovery: " + SC_Value(id, "detail") + ". Keep the paired saves; payment will not repeat.");
          }
          SC_Request(Equals(this.purchaseStage, n"quote") ? "buy_reserve" : "buy_commit", id, this.Payload());
        }
      }
    }
  }
  public func Interact(player: ref<PlayerPuppet>, city: ref<StreetChemCitySystem>, camera: Vector4, forward: Vector4, pressed: Bool) -> Bool {
    let target: ref<ScriptedPuppet> = GameInstance.GetTargetingSystem(player.GetGame()).GetLookAtObject(player, true, true) as ScriptedPuppet;
    if SC_Hotkey(89, 3) && !player.IsInCombat() { GameInstance.GetTeleportationFacility(player.GetGame()).Teleport(player, new Vector4(-1391.0, 1265.2, 123.0824, 1.0), StreetChemDealerSystem.Rotation(180.0)); city.Notify(player, "V's H10 apartment. The drug shop is outside in the hallway."); }
    if pressed && StreetChemDealerSystem.IsDealer(target) { city.Notify(player, "Use the normal Browse drugs interaction to open this shop."); return true; }
    return false;
  }
}

@addMethod(ScriptedPuppet)
public final func StreetChemShopChoice() -> Void {
  let choice: InteractionChoice;
  if !StreetChemDealerSystem.IsDealer(this) || !IsDefined(this.m_interactionComponent) { return; }
  choice.caption = "Browse drugs"; choice.choiceMetaData.tweakDBName = "StreetChemShop";
  this.m_interactionComponent.SetSingleChoice(choice, n"GenericTalk");
}

@wrapMethod(ScriptedPuppet)
protected cb func OnInteractionActivated(evt: ref<InteractionActivationEvent>) -> Bool {
  if StreetChemDealerSystem.IsDealer(this) && Equals(evt.layerData.tag, n"GenericTalk") {
    if Equals(evt.eventType, gameinteractionsEInteractionEventType.EIET_activate) { this.StreetChemShopChoice(); }
    else { this.m_interactionComponent.ResetChoices(n"GenericTalk"); }
    return true;
  }
  return wrappedMethod(evt);
}

@wrapMethod(ScriptedPuppet)
protected cb func OnInteractionUsed(evt: ref<InteractionChoiceEvent>) -> Bool {
  if StreetChemDealerSystem.IsDealer(this) && Equals(evt.choice.choiceMetaData.tweakDBName, "StreetChemShop") {
    (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"StreetChemDealerSystem") as StreetChemDealerSystem).Open(evt.activator as PlayerPuppet, this as NPCPuppet); return true;
  }
  return wrappedMethod(evt);
}

@wrapMethod(ScriptedPuppet)
protected cb func OnInteraction(evt: ref<InteractionChoiceEvent>) -> Bool {
  if StreetChemDealerSystem.IsDealer(this) && Equals(evt.choice.choiceMetaData.tweakDBName, "StreetChemShop") {
    (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"StreetChemDealerSystem") as StreetChemDealerSystem).Open(evt.activator as PlayerPuppet, this as NPCPuppet); return true;
  }
  return wrappedMethod(evt);
}

@addMethod(MarketSystem)
public func StreetChemAttach(owner: ref<GameObject>) -> Void { this.GetOrAddVendor(owner).OnAttach(owner); }

@wrapMethod(Vendor)
public final func GetItemsForSale(checkPlayerCanBuy: Bool) -> array<SItemStack> {
  if StreetChemDealerSystem.IsDealer(this.m_vendorObject as ScriptedPuppet) {
    return (GameInstance.GetScriptableSystemsContainer(this.m_gameInstance).Get(n"StreetChemDealerSystem") as StreetChemDealerSystem).Stock(this.m_vendorObject);
  }
  return wrappedMethod(checkPlayerCanBuy);
}

@wrapMethod(MarketSystem)
public final static func GetBuyPrice(vendorObject: wref<GameObject>, itemID: ItemID) -> Int32 {
  let i: Int32 = 0;
  let key: String;
  if StreetChemDealerSystem.IsDealer(vendorObject as ScriptedPuppet) {
    key = TweakDBInterface.GetString(ItemID.GetTDBID(itemID) + t".streetChemKey", "");
    while i < SC_ShopCount() { if Equals(key, SC_ShopValue(i, "key")) { return StringToInt(SC_ShopValue(i, "price")); } i += 1; }
  }
  return wrappedMethod(vendorObject, itemID);
}

@wrapMethod(Vendor)
public final func BuyItemsFromVendor(const itemsStack: script_ref<array<SItemStack>>, requestId: Int32) -> Void {
  if StreetChemDealerSystem.IsDealer(this.m_vendorObject as ScriptedPuppet) {
    if ArraySize(Deref(itemsStack)) == 1 {
      (GameInstance.GetScriptableSystemsContainer(this.m_gameInstance).Get(n"StreetChemDealerSystem") as StreetChemDealerSystem).Buy(GetPlayer(this.m_gameInstance), Deref(itemsStack)[0].itemID, Deref(itemsStack)[0].quantity, requestId);
    } else {
      let event: ref<UIVendorItemsBoughtEvent> = new UIVendorItemsBoughtEvent(); event.requestID = requestId;
      GameInstance.GetUISystem(this.m_gameInstance).QueueEvent(event);
      (GameInstance.GetScriptableSystemsContainer(this.m_gameInstance).Get(n"StreetChemCitySystem") as StreetChemCitySystem).Notify(GetPlayer(this.m_gameInstance), "Buy one package at a time; the shop replenishes after each purchase.");
    }
    return;
  }
  wrappedMethod(itemsStack, requestId);
}
