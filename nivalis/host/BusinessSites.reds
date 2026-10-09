public class NightCityBusinessSite extends IScriptable {
  public persistent let venue: CName;
  public persistent let position: Vector4;
  public let pin: NewMappinID;
}

public class NightCityBusinessSites extends ScriptableSystem {
  private persistent let sites: array<ref<NightCityBusinessSite>>;
  private let selected: Int32;
  private let dish: Int32;
  private let listener: Bool;
  private let current: Int32;
  private let showing: Bool;

  private final func Notify(player: ref<PlayerPuppet>, text: String) -> Void {
    let message: SimpleScreenMessage;
    message.isShown = true; message.duration = 12.0; message.message = text;
    GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UI_Notifications).SetVariant(GetAllBlackboardDefs().UI_Notifications.WarningMessage, ToVariant(message), true);
  }
  private final func Index(id: CName) -> Int32 {
    let i: Int32 = 0;
    while i < NC_Count() { if Equals(NC_VenueValue(i, "id"), NameToString(id)) { return i; } i += 1; }
    return -1;
  }
  private final func Show(player: ref<PlayerPuppet>, index: Int32) -> Void {
    this.Notify(player, NC_VenueValue(index, "name") + " | ready: " + NC_VenueValue(index, "ready") + " | menu: " + NC_VenueValue(index, "menuCount") + " | staff: " + NC_VenueValue(index, "staffCount") + " | meals served: " + NC_VenueValue(index, "mealsServed") + ". Ctrl+Alt+N: next meal. Ctrl+Alt +/-: price. Ctrl+Alt+R: restock every business. Ctrl+Alt+C: collect net proceeds.");
  }
  public final func Tick(player: ref<PlayerPuppet>, business: ref<NightCityBusinessSystem>) -> Void {
    let i: Int32;
    let nearest: Int32 = -1;
    let best: Float = 4.0;
    let d: Float;
    let site: ref<NightCityBusinessSite>;
    let data: MappinData;
    let count: Int32;
    let id: CName;
    if !this.listener { player.RegisterInputListener(this, n"Choice1"); player.RegisterInputListener(this, n"ChoiceApply"); this.listener = true; }
    if NC_Count() <= 0 { return; }
    if this.selected >= NC_Count() { this.selected = 0; }
    if NC_Hotkey(77, 5) {
      this.selected = (this.selected + 1) % NC_Count(); this.dish = 0;
      this.Notify(player, "Selected " + NC_VenueValue(this.selected, "name") + ". Stand at its Night City counter and press Ctrl+Alt+B to link it.");
    }
    if NC_Hotkey(66, 5) {
      if !Equals(NC_VenueValue(this.selected, "owned"), "true") || !Equals(NC_VenueValue(this.selected, "ready"), "true") {
        this.Notify(player, "This venue needs setup in Nivalis before linking a counter.");
      } else {
        id = StringToName(NC_VenueValue(this.selected, "id"));
        while i < ArraySize(this.sites) {
          if Equals(this.sites[i].venue, id) { site = this.sites[i]; }
          else { if Vector4.Distance(player.GetWorldPosition(), this.sites[i].position) < 8.0 { this.Notify(player, "Another business already uses this counter. Choose a separate shop."); return; } }
          i += 1;
        }
        if !IsDefined(site) { site = new NightCityBusinessSite(); site.venue = id; ArrayPush(this.sites, site); }
        if site.pin.value != 0ul { GameInstance.GetMappinSystem(player.GetGame()).UnregisterMappin(site.pin); site.pin.value = 0ul; }
        site.position = player.GetWorldPosition(); site.position.Z += 0.8;
        this.Notify(player, NC_VenueValue(this.selected, "name") + " linked to this counter. Its Nivalis menu, staff and stock run here. Save Cyberpunk to keep the location.");
        GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
      }
    }
    i = 0;
    while i < ArraySize(this.sites) {
      site = this.sites[i];
      if site.pin.value == 0ul {
        data.mappinType = t"Mappins.DefaultStaticMappin";
        data.variant = gamedataMappinVariant.ServicePointFoodVariant; data.active = true;
        site.pin = GameInstance.GetMappinSystem(player.GetGame()).RegisterMappin(data, site.position);
      }
      d = Vector4.Distance(player.GetWorldPosition(), site.position);
      if d < best { best = d; nearest = this.Index(site.venue); }
      i += 1;
    }
    if NC_Hotkey(73, 5) { this.Show(player, nearest >= 0 ? nearest : this.selected); }
    if NC_Hotkey(78, 5) {
      count = StringToInt(NC_VenueValue(this.selected, "menuCount"));
      if count > 0 { this.dish = (this.dish + 1) % count; this.Notify(player, NC_MenuValue(this.selected, this.dish, "dish") + " | price €$" + ToString(StringToInt(NC_MenuValue(this.selected, this.dish, "priceHundredths")) / 100)); }
    }
    if NC_Hotkey(187, 5) || NC_Hotkey(189, 5) {
      // The minus key's held state is not read; separate events below handle direction.
      this.Notify(player, "Use Ctrl+Alt+PageUp / PageDown to change the selected meal price.");
    }
    if NC_Hotkey(33, 5) { this.Price(player, 100); }
    if NC_Hotkey(34, 5) { this.Price(player, -100); }
    this.Prompt(player, nearest);
  }
  private final func Price(player: ref<PlayerPuppet>, change: Int32) -> Void {
    let dishID: String = NC_MenuValue(this.selected, this.dish, "id");
    let price: Int32 = StringToInt(NC_MenuValue(this.selected, this.dish, "priceHundredths")) + change;
    if Equals(dishID, "") || price < 100 || price > 1000000 { return; }
    if NC_Request("nc-menu", NC_NewID(), "{\"venue\":\"" + NC_VenueValue(this.selected, "id") + "\",\"dish\":\"" + dishID + "\",\"price\":" + ToString(price) + "}") {
      this.Notify(player, "Setting " + NC_MenuValue(this.selected, this.dish, "dish") + " to €$" + ToString(price / 100) + ".");
    }
  }
  private final func Prompt(player: ref<PlayerPuppet>, index: Int32) -> Void {
    let bb: ref<IBlackboard> = GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UIInteractions);
    let hub: InteractionChoiceHubData = FromVariant<InteractionChoiceHubData>(bb.GetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub));
    let choice: InteractionChoiceData;
    this.current = index;
    if index < 0 || player.IsInCombat() { this.Clear(player); return; }
    if hub.active && hub.id != 670303 { this.showing = false; this.current = -1; return; }
    hub.id = 670303; hub.active = true; hub.title = NC_VenueValue(index, "name"); hub.flags = EVisualizerDefinitionFlags.None;
    ArrayClear(hub.choices);
    choice.inputAction = n"Choice1"; choice.localizedName = "Manage business"; choice.isHoldAction = false; ArrayPush(hub.choices, choice);
    bb.SetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub, ToVariant(hub), true); this.showing = true;
  }
  protected cb func OnAction(action: ListenerAction, consumer: ListenerActionConsumer) -> Bool {
    let player: ref<PlayerPuppet> = GetPlayer(this.GetGameInstance());
    if this.showing && this.current >= 0 && (Equals(ListenerAction.GetName(action), n"Choice1") || Equals(ListenerAction.GetName(action), n"ChoiceApply")) && ListenerAction.IsButtonJustReleased(action) {
      this.selected = this.current; this.Show(player, this.current); return true;
    }
    return false;
  }
  private final func Clear(player: ref<PlayerPuppet>) -> Void {
    let bb: ref<IBlackboard> = GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UIInteractions);
    let hub: InteractionChoiceHubData = FromVariant<InteractionChoiceHubData>(bb.GetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub));
    if hub.id == 670303 { hub.active = false; ArrayClear(hub.choices); bb.SetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub, ToVariant(hub), true); }
    this.showing = false;
  }
  public final func Stop(player: ref<PlayerPuppet>) -> Void {
    let i: Int32 = 0;
    if this.listener { player.UnregisterInputListener(this); this.listener = false; }
    this.Clear(player);
    while i < ArraySize(this.sites) {
      if this.sites[i].pin.value != 0ul { GameInstance.GetMappinSystem(player.GetGame()).UnregisterMappin(this.sites[i].pin); this.sites[i].pin.value = 0ul; }
      i += 1;
    }
  }
}
