public native func SC_DoseCount() -> Int32;
public native func SC_DoseValue(index: Int32, field: String) -> String;
public native func SC_Consume(id: String, key: String) -> Bool;
public native func SC_DrugVisual(saturation: Float, vignette: Float) -> Void;

public class StreetChemDrugTweak extends ScriptableTweak {
  protected cb func OnApply() -> Void {
    let i: Int32 = 0;
    StreetChemDrugs.Profiles();
    while i < SC_DoseCount() { StreetChemDrugs.Define(i); i += 1; }
  }
}

public abstract class StreetChemDrugs {
  public static func ID(index: Int32) -> TweakDBID { return TDBID.Create("Items.StreetChemDose_" + SC_DoseValue(index, "key")); }
  public static func LegacyID(index: Int32) -> TweakDBID { return TDBID.Create("Items.StreetChem_" + SC_DoseValue(index, "key")); }
  public static func IsDrug(id: TweakDBID) -> Bool { return TweakDBInterface.GetBool(id + t".streetChemDrug", false); }
  public static func Family(family: String) -> String {
    if StrContains(family, "Meth") { return "Meth"; }
    if StrContains(family, "Cocaine") { return "Cocaine"; }
    if StrContains(family, "Mushroom") || StrContains(family, "Shroom") { return "Shrooms"; }
    return "Weed";
  }
  public static func SellPrice(record: TweakDBID) -> Int32 {
    let i: Int32 = 0;
    while i < SC_DoseCount() { if StreetChemDrugs.ID(i) == record || StreetChemDrugs.LegacyID(i) == record { return Max(0, StringToInt(SC_DoseValue(i, "sellPrice"))); } i += 1; }
    return 0;
  }
  public static func Description(family: String) -> String {
    if Equals(family, "Meth") { return "Meth high (120s): movement +25%, firearm rate +25%, vivid colors; recoil +20%. Crash (45s): movement -15%, firearm rate -10%."; }
    if Equals(family, "Cocaine") { return "Cocaine rush (90s): movement +15%, firearm rate +15%; recoil +15%. Crash (35s): movement -10%, firearm rate -10%."; }
    if Equals(family, "Shrooms") { return "Shroom trip (150s): jump height +20%, vivid colors; movement -10%. Aftereffect (30s): movement -5%."; }
    return "Cannabis high (180s): stamina regeneration +20%; movement -10%, firearm rate -5%. Aftereffect (30s): movement -5%.";
  }
  public static func Duration(family: String) -> Float {
    if Equals(family, "Meth") { return 120.0; }
    if Equals(family, "Cocaine") { return 90.0; }
    if Equals(family, "Shrooms") { return 150.0; }
    return 180.0;
  }
  public static func CrashDuration(family: String) -> Float { return Equals(family, "Meth") ? 45.0 : Equals(family, "Cocaine") ? 35.0 : 30.0; }
  public static func Define(index: Int32) -> Void {
    let record: TweakDBID = StreetChemDrugs.ID(index);
    let family: String = StreetChemDrugs.Family(SC_DoseValue(index, "family"));
    let actions: array<TweakDBID>;
    let empty: array<TweakDBID>;
    let tags: array<CName>;
    let itemStats: array<TweakDBID>;
    let legacy: TweakDBID = StreetChemDrugs.LegacyID(index);
    let icon: String = "StreetChem_" + SC_DoseValue(index, "key");
    let iconRecord: TweakDBID = TDBID.Create("UIIcon." + icon);
    if !IsDefined(TweakDBInterface.GetItemRecord(record)) { TweakDBManager.CloneRecord(record, t"Items.Drug"); }
    // Define old records before saved inventories load; the sync then removes only our old mirrors.
    if !IsDefined(TweakDBInterface.GetItemRecord(legacy)) { TweakDBManager.CloneRecord(legacy, t"Items.HealthBooster"); }
    ArrayPush(actions, t"ItemAction.Consume");
    ArrayPush(tags, n"Consumable"); ArrayPush(tags, n"Drug"); ArrayPush(tags, n"Quest");
    ArrayPush(tags, n"SkipActivityLog"); ArrayPush(tags, n"SkipActivityLogOnLoot"); ArrayPush(tags, n"SkipActivityLogOnRemove");
    TweakDBManager.SetFlat(record + t".streetChemDrug", true);
    TweakDBManager.SetFlat(record + t".streetChemKey", SC_DoseValue(index, "key"));
    TweakDBManager.SetFlat(record + t".streetChemFamily", family);
    TweakDBManager.SetFlat(record + t".streetChemName", SC_DoseValue(index, "name") + " — " + SC_DoseValue(index, "quality"));
    TweakDBManager.SetFlat(record + t".streetChemDescription", StreetChemDrugs.Description(family) + " Consumes one native Schedule I package (" + SC_DoseValue(index, "amount") + " product unit(s)); stock is shared with sales and runners.");
    TweakDBManager.SetFlat(record + t".tags", tags);
    TweakDBManager.SetFlat(record + t".objectActions", actions);
    TweakDBManager.SetFlat(record + t".itemSecondaryAction", t"ItemAction.Consume");
    TweakDBManager.SetFlat(record + t".itemType", t"ItemType.Con_LongLasting");
    TweakDBManager.SetFlat(record + t".entityName", n"base_junk_item");
    TweakDBManager.SetFlat(record + t".appearanceName", n"base_junk_item_medicine_bottle");
    TweakDBManager.SetFlat(record + t".OnEquip", empty);
    TweakDBManager.SetFlat(record + t".statModifierGroups", empty);
    ArrayPush(itemStats, t"StreetChemInventory.StackCapacity");
    ArrayPush(itemStats, t"StreetChemInventory.ZeroWeight");
    TweakDBManager.SetFlat(record + t".statModifiers", itemStats);
    TweakDBManager.CreateRecord(iconRecord, n"UIIcon");
    TweakDBManager.SetFlat(iconRecord + t".atlasResourcePath", ResRef.FromString("streetchem/icons/" + SC_DoseValue(index, "key") + ".inkatlas"));
    TweakDBManager.SetFlat(iconRecord + t".atlasPartName", n"icon");
    TweakDBManager.UpdateRecord(iconRecord);
    TweakDBManager.SetFlat(record + t".iconPath", icon);
    TweakDBManager.SetFlat(legacy + t".streetChemDrug", true);
    TweakDBManager.SetFlat(legacy + t".streetChemName", SC_DoseValue(index, "name"));
    TweakDBManager.SetFlat(legacy + t".tags", tags);
    TweakDBManager.UpdateRecord(legacy);
    TweakDBManager.UpdateRecord(record);
  }
  private static func Stat(path: String, type: String, value: Float) -> TweakDBID {
    let record: TweakDBID = TDBID.Create(path);
    TweakDBManager.CreateRecord(record, n"ConstantStatModifier");
    TweakDBManager.SetFlat(record + t".statType", TDBID.Create("BaseStats." + type));
    TweakDBManager.SetFlat(record + t".modifierType", n"AdditiveMultiplier");
    TweakDBManager.SetFlat(record + t".value", value);
    TweakDBManager.UpdateRecord(record);
    return record;
  }
  private static func Profile(family: String, crash: Bool, speed: Float, secondary: String, value: Float) -> Void {
    let name: String = "BaseStatusEffect.StreetChem" + family + (crash ? "Crash" : "High");
    let record: TweakDBID = TDBID.Create(name);
    let duration: TweakDBID = TDBID.Create(name + "Duration");
    let modifier: TweakDBID = TDBID.Create(name + "Seconds");
    let package: TweakDBID = TDBID.Create(name + "Stats");
    let modifiers: array<TweakDBID>;
    let stats: array<TweakDBID>;
    let packages: array<TweakDBID>;
    TweakDBManager.CloneRecord(record, t"BaseStatusEffect.Drugged");
    TweakDBManager.CreateRecord(modifier, n"ConstantStatModifier");
    TweakDBManager.SetFlat(modifier + t".statType", t"BaseStats.MaxDuration");
    TweakDBManager.SetFlat(modifier + t".modifierType", n"Additive");
    TweakDBManager.SetFlat(modifier + t".value", crash ? StreetChemDrugs.CrashDuration(family) : StreetChemDrugs.Duration(family));
    TweakDBManager.UpdateRecord(modifier);
    ArrayPush(modifiers, modifier);
    TweakDBManager.CreateRecord(duration, n"StatModifierGroup");
    TweakDBManager.SetFlat(duration + t".statModifiers", modifiers);
    TweakDBManager.UpdateRecord(duration);
    ArrayPush(stats, StreetChemDrugs.Stat(name + "Speed", "MaxSpeed", speed));
    if NotEquals(secondary, "") { ArrayPush(stats, StreetChemDrugs.Stat(name + "Secondary", secondary, value)); }
    TweakDBManager.CreateRecord(package, n"GameplayLogicPackage");
    TweakDBManager.SetFlat(package + t".stats", stats);
    TweakDBManager.UpdateRecord(package);
    ArrayPush(packages, package);
    TweakDBManager.SetFlat(record + t".duration", duration);
    TweakDBManager.SetFlat(record + t".packages", packages);
    TweakDBManager.UpdateRecord(record);
  }
  public static func Profiles() -> Void {
    TweakDBManager.CreateRecord(t"StreetChemInventory.StackCapacity", n"ConstantStatModifier");
    TweakDBManager.SetFlat(t"StreetChemInventory.StackCapacity.statType", t"BaseStats.Quantity");
    TweakDBManager.SetFlat(t"StreetChemInventory.StackCapacity.modifierType", n"Additive");
    TweakDBManager.SetFlat(t"StreetChemInventory.StackCapacity.value", 99999999.0);
    TweakDBManager.UpdateRecord(t"StreetChemInventory.StackCapacity");
    TweakDBManager.CreateRecord(t"StreetChemInventory.ZeroWeight", n"ConstantStatModifier");
    TweakDBManager.SetFlat(t"StreetChemInventory.ZeroWeight.statType", t"BaseStats.Weight");
    TweakDBManager.SetFlat(t"StreetChemInventory.ZeroWeight.modifierType", n"Multiplier");
    TweakDBManager.SetFlat(t"StreetChemInventory.ZeroWeight.value", 0.0);
    TweakDBManager.UpdateRecord(t"StreetChemInventory.ZeroWeight");
    StreetChemDrugs.Profile("Meth", false, 0.25, "StaminaRegenRate", 0.15);
    StreetChemDrugs.Profile("Cocaine", false, 0.15, "StaminaRegenRate", 0.10);
    StreetChemDrugs.Profile("Weed", false, -0.10, "StaminaRegenRate", 0.20);
    StreetChemDrugs.Profile("Shrooms", false, -0.10, "JumpHeight", 0.20);
    StreetChemDrugs.Profile("Meth", true, -0.15, "StaminaRegenRate", -0.15);
    StreetChemDrugs.Profile("Cocaine", true, -0.10, "StaminaRegenRate", -0.10);
    StreetChemDrugs.Profile("Weed", true, -0.05, "", 0.0);
    StreetChemDrugs.Profile("Shrooms", true, -0.05, "", 0.0);
  }
}

public class StreetChemDoseSystem extends ScriptableSystem {
  private persistent let savedID: CName;
  private persistent let savedKey: CName;
  private persistent let savedSession: CName;
  private persistent let savedFamily: CName;
  private let pendingID: String;
  private let pendingKey: String;
  private persistent let pendingRecord: TweakDBID;
  private let pendingSession: String;
  private let activeFamily: String;
  private let restored: Bool;
  private persistent let phase: Int32;
  private persistent let remaining: Float;
  private let lastClock: Float;
  private let nextSync: Float;
  private let nextPoll: Float;
  private let weapon: wref<WeaponObject>;
  private let cycle: ref<gameStatModifierData>;
  private let recoil: ref<gameStatModifierData>;
  private let weaponPhase: Int32;
  private let weaponFamily: String;
  public func Diagnostic(player: ref<PlayerPuppet>) -> String {
    let gun: ref<WeaponObject> = GameObject.GetActiveWeapon(player);
    return "{\"catalog\":" + ToString(SC_DoseCount()) + ",\"pending\":\"" + this.pendingID + "\",\"family\":\"" + this.activeFamily + "\",\"phase\":" + ToString(this.phase) + ",\"remaining\":" + ToString(this.remaining) + ",\"maxSpeed\":" + ToString(GameInstance.GetStatsSystem(player.GetGame()).GetStatValue(Cast<StatsObjectID>(player.GetEntityID()), gamedataStatType.MaxSpeed)) + ",\"cycleTime\":" + ToString(IsDefined(gun) ? GameInstance.GetStatsSystem(player.GetGame()).GetStatValue(Cast<StatsObjectID>(gun.GetEntityID()), gamedataStatType.CycleTimeBase) : 0.0) + "}";
  }
  private func OnAttach() -> Void { this.restored = false; }
  private func OnRestored(saveVersion: Int32, gameVersion: Int32) -> Void { this.restored = false; }
  private func RestoreRuntime() -> Void {
    if this.restored { return; }
    this.pendingID = Equals(this.savedID, n"") ? "" : NameToString(this.savedID);
    this.pendingKey = Equals(this.savedKey, n"") ? "" : NameToString(this.savedKey);
    this.pendingSession = Equals(this.savedSession, n"") ? "" : NameToString(this.savedSession);
    this.activeFamily = Equals(this.savedFamily, n"") ? "" : NameToString(this.savedFamily);
    this.nextSync = 0.0; this.nextPoll = 0.0; this.lastClock = 0.0; this.restored = true;
  }
  private func Persist() -> Void {
    this.savedID = StringToName(this.pendingID); this.savedKey = StringToName(this.pendingKey);
    this.savedSession = StringToName(this.pendingSession); this.savedFamily = StringToName(this.activeFamily);
  }
  public func Purchased(player: ref<PlayerPuppet>, record: TweakDBID) -> Void {
    GameInstance.GetTransactionSystem(player.GetGame()).GiveItem(player, ItemID.FromTDBID(record), 1);
    this.nextSync = EngineTime.ToFloat(GameInstance.GetSimTime(player.GetGame())) + 1.0;
  }
  public func Busy() -> Bool { return NotEquals(this.pendingID, ""); }
  public func Begin(player: ref<PlayerPuppet>, record: TweakDBID) -> Void {
    this.RestoreRuntime();
    let city: ref<StreetChemCitySystem> = GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"StreetChemCitySystem") as StreetChemCitySystem;
    if !SC_Connected() || !city.ConsumablesReady() { city.Notify(player, "Load your paired solo saves to consume this product."); return; }
    if NotEquals(this.pendingID, "") || city.SaleBusy() { city.Notify(player, "Finish the current stock transaction first."); return; }
    this.pendingID = SC_NewID(); this.pendingRecord = record;
    this.pendingKey = TweakDBInterface.GetString(record + t".streetChemKey", "");
    this.pendingSession = SC_Session(); this.nextPoll = 0.0;
    if !SC_Consume(this.pendingID, this.pendingKey) { this.pendingID = ""; city.Notify(player, "Stock bridge busy. Try again."); }
    this.Persist();
  }
  public func StopWeapon(player: ref<PlayerPuppet>) -> Void {
    if IsDefined(this.weapon) {
      if IsDefined(this.cycle) { GameInstance.GetStatsSystem(player.GetGame()).RemoveModifier(Cast<StatsObjectID>(this.weapon.GetEntityID()), this.cycle); }
      if IsDefined(this.recoil) { GameInstance.GetStatsSystem(player.GetGame()).RemoveModifier(Cast<StatsObjectID>(this.weapon.GetEntityID()), this.recoil); }
    }
    this.weapon = null; this.cycle = null; this.recoil = null;
    SC_DrugVisual(1.0, 0.0);
  }
  private func Effect() -> TweakDBID { return TDBID.Create("BaseStatusEffect.StreetChem" + this.activeFamily + (this.phase == 2 ? "Crash" : "High")); }
  private func StartHigh(player: ref<PlayerPuppet>, family: String, now: Float) -> Void {
    if this.phase > 0 { StatusEffectHelper.RemoveStatusEffect(player, this.Effect()); }
    this.activeFamily = family; this.phase = 1; this.remaining = StreetChemDrugs.Duration(family);
    StatusEffectHelper.ApplyStatusEffect(player, this.Effect());
  }
  public func Tick(player: ref<PlayerPuppet>) -> Void {
    let now: Float = EngineTime.ToFloat(GameInstance.GetSimTime(player.GetGame()));
    let position: Vector4 = player.GetWorldPosition();
    let index: Int32; let wanted: Int32; let have: Int32; let record: TweakDBID; let status: String;
    let tx: ref<TransactionSystem> = GameInstance.GetTransactionSystem(player.GetGame());
    let quests: ref<QuestsSystem> = GameInstance.GetQuestsSystem(player.GetGame());
    let city: ref<StreetChemCitySystem> = GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"StreetChemCitySystem") as StreetChemCitySystem;
    let gun: ref<WeaponObject>;
    let rate: Float = 1.0; let drift: Float = 0.0; let saturation: Float = 1.0; let vignette: Float = 0.0;
    this.RestoreRuntime();
    if this.phase > 0 && this.lastClock > 0.0 { this.remaining -= MaxF(0.0, now - this.lastClock); }
    this.lastClock = now;
    if NotEquals(this.pendingID, "") && now >= this.nextPoll && SC_Connected() {
      this.nextPoll = now + 0.5;
      if NotEquals(this.pendingSession, SC_Session()) { city.Notify(player, "Return to the paired Schedule I save to finish consumption."); }
      else {
        status = SC_Status(this.pendingID);
        if Equals(status, "committed") {
          if quests.GetFactStr("streetchem_dose_" + this.pendingID) == 0 {
            this.StartHigh(player, TweakDBInterface.GetString(this.pendingRecord + t".streetChemFamily", "Weed"), now);
            tx.RemoveItem(player, ItemID.FromTDBID(this.pendingRecord), 1);
            quests.SetFactStr("streetchem_dose_" + this.pendingID, 1);
            city.Notify(player, "Consumed " + TweakDBInterface.GetString(this.pendingRecord + t".streetChemName", "product") + ". " + StreetChemDrugs.Description(this.activeFamily));
            GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
          }
          this.pendingID = ""; this.nextSync = now + 1.0;
        } else {
          if Equals(status, "error") { city.Notify(player, "Consumption failed: " + SC_Value(this.pendingID, "detail")); this.pendingID = ""; }
          else { SC_Consume(this.pendingID, this.pendingKey); }
        }
      }
    }
    if SC_Connected() && city.ConsumablesReady() && !city.SaleBusy() && Equals(this.pendingID, "") && now >= this.nextSync {
      this.nextSync = now + 1.0; index = 0;
      while index < SC_DoseCount() {
        StreetChemDrugs.Define(index); record = StreetChemDrugs.ID(index);
        have = tx.GetItemQuantity(player, ItemID.FromTDBID(StreetChemDrugs.LegacyID(index)));
        if have > 0 { tx.RemoveItem(player, ItemID.FromTDBID(StreetChemDrugs.LegacyID(index)), have); }
        wanted = Min(StringToInt(SC_DoseValue(index, "quantity")), 99999999); have = tx.GetItemQuantity(player, ItemID.FromTDBID(record));
        if have < wanted { tx.GiveItem(player, ItemID.FromTDBID(record), wanted - have); }
        else { if have > wanted { tx.RemoveItem(player, ItemID.FromTDBID(record), have - wanted); } }
        index += 1;
      }
    }
    if this.phase > 0 && this.remaining <= 0.0 {
      StatusEffectHelper.RemoveStatusEffect(player, this.Effect());
      if this.phase == 1 { this.phase = 2; this.remaining = StreetChemDrugs.CrashDuration(this.activeFamily); StatusEffectHelper.ApplyStatusEffect(player, this.Effect()); city.Notify(player, "Street Chem: " + this.activeFamily + " high ended. Temporary crash / aftereffect."); }
      else { this.phase = 0; }
    }
    if this.phase == 1 {
      if Equals(this.activeFamily, "Meth") { rate = 1.25; drift = 0.20; saturation = 1.40; }
      else { if Equals(this.activeFamily, "Cocaine") { rate = 1.15; drift = 0.15; saturation = 1.15; }
      else { if Equals(this.activeFamily, "Shrooms") { saturation = 1.55; vignette = 0.10; } else { rate = 0.95; saturation = 0.95; vignette = 0.06; } } }
    } else { if this.phase == 2 { saturation = 0.88; if Equals(this.activeFamily, "Meth") || Equals(this.activeFamily, "Cocaine") { rate = 0.90; } } }
    gun = GameObject.GetActiveWeapon(player);
    if gun != this.weapon || this.weaponPhase != this.phase || NotEquals(this.weaponFamily, this.activeFamily) {
      this.StopWeapon(player); this.weapon = gun; this.weaponPhase = this.phase; this.weaponFamily = this.activeFamily;
      if IsDefined(gun) && this.phase > 0 {
        this.cycle = RPGManager.CreateStatModifier(gamedataStatType.CycleTimeBase, gameStatModifierType.Multiplier, 1.0 / rate);
        GameInstance.GetStatsSystem(player.GetGame()).AddModifier(Cast<StatsObjectID>(gun.GetEntityID()), this.cycle);
        this.recoil = RPGManager.CreateStatModifier(gamedataStatType.RecoilKickMax, gameStatModifierType.AdditiveMultiplier, drift);
        GameInstance.GetStatsSystem(player.GetGame()).AddModifier(Cast<StatsObjectID>(gun.GetEntityID()), this.recoil);
      }
    }
    SC_DrugVisual(saturation, vignette);
    this.Persist();
    SC_Report("{\"doses\":" + this.Diagnostic(player) + ",\"dealer\":" + (GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"StreetChemDealerSystem") as StreetChemDealerSystem).Diagnostic() + ",\"position\":{\"x\":" + ToString(position.X) + ",\"y\":" + ToString(position.Y) + ",\"z\":" + ToString(position.Z) + "}}");
  }
}

@wrapMethod(ConsumeAction)
public func CompleteAction(gameInstance: GameInstance) -> Void {
  let data: wref<gameItemData> = this.GetItemData();
  let player: ref<PlayerPuppet> = this.GetExecutor() as PlayerPuppet;
  if IsDefined(player) && IsDefined(data) && StreetChemDrugs.IsDrug(ItemID.GetTDBID(data.GetID())) {
    (GameInstance.GetScriptableSystemsContainer(gameInstance).Get(n"StreetChemDoseSystem") as StreetChemDoseSystem).Begin(player, ItemID.GetTDBID(data.GetID()));
    return;
  }
  wrappedMethod(gameInstance);
}

@wrapMethod(UIItemsHelper)
public final static func GetItemName(itemRecord: ref<Item_Record>, itemData: wref<gameItemData>) -> String {
  if IsDefined(itemRecord) && StreetChemDrugs.IsDrug(itemRecord.GetID()) { return TweakDBInterface.GetString(itemRecord.GetID() + t".streetChemName", "Schedule I product"); }
  return wrappedMethod(itemRecord, itemData);
}
@wrapMethod(InventoryItemData)
public final static func GetName(const self: script_ref<InventoryItemData>) -> String {
  let id: TweakDBID = ItemID.GetTDBID(InventoryItemData.GetID(self));
  if StreetChemDrugs.IsDrug(id) { return TweakDBInterface.GetString(id + t".streetChemName", "Schedule I product"); }
  return wrappedMethod(self);
}
@wrapMethod(InventoryItemData)
public final static func GetDescription(const self: script_ref<InventoryItemData>) -> String {
  let id: TweakDBID = ItemID.GetTDBID(InventoryItemData.GetID(self));
  if StreetChemDrugs.IsDrug(id) { return TweakDBInterface.GetString(id + t".streetChemDescription", ""); }
  return wrappedMethod(self);
}

// Cyberpunk 2.x's backpack caches its own name/description instead of the older data helpers.
@wrapMethod(UIInventoryItem)
public final func GetName() -> String {
  let record: TweakDBID = ItemID.GetTDBID(this.ID);
  if StreetChemDrugs.IsDrug(record) { return TweakDBInterface.GetString(record + t".streetChemName", "Schedule I product"); }
  return wrappedMethod();
}
@wrapMethod(UIInventoryItem)
public final func GetDescription() -> String {
  let record: TweakDBID = ItemID.GetTDBID(this.ID);
  if StreetChemDrugs.IsDrug(record) { return TweakDBInterface.GetString(record + t".streetChemDescription", ""); }
  return wrappedMethod();
}
@wrapMethod(UIInventoryItem)
public final func GetIconPath() -> String {
  let record: TweakDBID = ItemID.GetTDBID(this.ID);
  if StreetChemDrugs.IsDrug(record) { return "UIIcon." + TweakDBInterface.GetString(record + t".iconPath", "drugs_endotrisine"); }
  return wrappedMethod();
}

@wrapMethod(PlayerPuppet)
protected cb func OnItemAddedToInventory(evt: ref<ItemAddedEvent>) -> Bool {
  if StreetChemDrugs.IsDrug(ItemID.GetTDBID(evt.itemID)) { evt.flaggedAsSilent = true; }
  return wrappedMethod(evt);
}

@wrapMethod(UIInventoryItem)
public final func GetSellPrice() -> Float {
  let record: TweakDBID = ItemID.GetTDBID(this.ID);
  if StreetChemDrugs.IsDrug(record) { return Cast<Float>(StreetChemDrugs.SellPrice(record)); }
  return wrappedMethod();
}

@wrapMethod(InventoryItemData)
public final static func GetPrice(const self: script_ref<InventoryItemData>) -> Float {
  let record: TweakDBID = ItemID.GetTDBID(InventoryItemData.GetID(self));
  if StreetChemDrugs.IsDrug(record) { return Cast<Float>(StreetChemDrugs.SellPrice(record)); }
  return wrappedMethod(self);
}
