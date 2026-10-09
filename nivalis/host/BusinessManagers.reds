public class NightCityManagerTweak extends ScriptableTweak {
  protected cb func OnApply() -> Void {
    TweakDBManager.CloneRecord(t"Character.NivalisBusinessManager", t"Character.q000_hym_ma");
    TweakDBManager.SetFlat(t"Character.NivalisBusinessManager.vendorID", t"");
    TweakDBManager.UpdateRecord(t"Character.NivalisBusinessManager");
  }
}

public class NightCityManagedBusiness extends IScriptable {
  public persistent let venue: CName;
  public persistent let cityName: CName;
  public persistent let position: Vector4;
  public persistent let placed: Bool;
  public persistent let businessName: CName;
  public let body: wref<NPCPuppet>;
  public let hold: ref<AIHoldPositionCommand>;
}

public class NightCityBusinessManagers extends ScriptableSystem {
  private persistent let sites: array<ref<NightCityManagedBusiness>>;
  private let pending: String;
  private let nextAction: Float;
  private let nextWorld: Float;
  private let listener: Bool;
  private let promptBody: wref<NPCPuppet>;
  private let promptPage: Int32;
  private let talking: Bool;
  private let selection: Int32;
  private let promptActions: array<String>;
  public static func Options(page: Int32, out captions: array<String>, out actions: array<String>) -> Void {
    if page == 1 {
      captions = ["Next menu dish / current price", "Raise price 1 eddy", "Lower price 1 eddy", "Choose eight local favourites", "Back"];
      actions = ["NCNextMeal", "NCPriceUp", "NCPriceDown", "NCFocusMenu", "NCBack"];
    } else { if page == 2 {
      captions = ["Open 08:00-20:00", "Open 12:00-20:00", "Open 12:00-24:00", "Back"];
      actions = ["NCEarly", "NCLunch", "NCLate", "NCBack"];
    } else {
      captions = ["How is the business doing?", "Menu and prices", "Opening hours", "Hire and train an efficient crew", "Restock all businesses", "Toggle automatic restocking", "Settle income and operating costs", "Start business operations", "Pause business operations", "See you later."];
      actions = ["NCStatus", "NCMenu", "NCHours", "NCCrew", "NCRestock", "NCAuto", "NCCash", "NCStart", "NCPause", "NCBye"];
    } }
  }
  private final func ClearPrompt(player: ref<PlayerPuppet>) -> Void {
    let bb: ref<IBlackboard> = GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UIInteractions);
    let data: DialogChoiceHubs = FromVariant<DialogChoiceHubs>(bb.GetVariant(GetAllBlackboardDefs().UIInteractions.DialogChoiceHubs));
    let i: Int32 = ArraySize(data.choiceHubs) - 1;
    while i >= 0 { if data.choiceHubs[i].id == 670304 { ArrayErase(data.choiceHubs, i); } i -= 1; }
    bb.SetVariant(GetAllBlackboardDefs().UIInteractions.DialogChoiceHubs, ToVariant(data), true);
    let interaction: InteractionChoiceHubData = FromVariant<InteractionChoiceHubData>(bb.GetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub));
    if interaction.id == 670304 { interaction.active = false; ArrayClear(interaction.choices); bb.SetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub, ToVariant(interaction), true); }
    this.talking = false; this.promptBody = null; ArrayClear(this.promptActions);
  }
  public final func Stop(player: ref<PlayerPuppet>) -> Void {
    if this.listener { player.UnregisterInputListener(this); this.listener = false; }
    this.ClearPrompt(player);
  }
  private final func Prompt(player: ref<PlayerPuppet>) -> Void {
    let camera: ref<CameraSystem> = GameInstance.GetCameraSystem(player.GetGame());
    let pose: Transform;
    let body: wref<NPCPuppet>;
    let point: Vector4;
    let captions: array<String>;
    let actions: array<String>;
    let hub: ListChoiceHubData;
    let choice: ListChoiceData;
    let data: DialogChoiceHubs;
    let interaction: InteractionChoiceHubData;
    let talkChoice: InteractionChoiceData;
    let bb: ref<IBlackboard> = GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UIInteractions);
    let i: Int32 = 0;
    if !player.IsInCombat() && !GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UI_System).GetBool(GetAllBlackboardDefs().UI_System.IsInMenu) && camera.GetActiveCameraWorldTransform(pose) {
      while i < ArraySize(this.sites) {
        if IsDefined(this.sites[i].body) && Vector4.Distance(player.GetWorldPosition(), this.sites[i].body.GetWorldPosition()) < 4.0 {
          point = this.sites[i].body.GetWorldPosition(); point.Z += 1.2;
          if Vector4.Dot(Vector4.Normalize(point - Transform.GetPosition(pose)), camera.GetActiveCameraForward()) > 0.88 { body = this.sites[i].body; break; }
        }
        i += 1;
      }
    }
    if this.talking && IsDefined(this.promptBody) && Vector4.Distance(player.GetWorldPosition(), this.promptBody.GetWorldPosition()) < 4.0 && !player.IsInCombat() { body = this.promptBody; }
    if !IsDefined(body) { if IsDefined(this.promptBody) { this.ClearPrompt(player); } return; }
    if this.promptBody != body { this.ClearPrompt(player); this.promptBody = body; body.ncManagerPage = 0; }
    interaction = FromVariant<InteractionChoiceHubData>(bb.GetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub));
    if !this.talking {
      if interaction.active && interaction.id == 670304 { return; }
      interaction.id = 670304; interaction.active = true; interaction.title = NC_VenueValue(this.Index(body.ncManagerVenue), "name") + " manager";
      ArrayClear(interaction.choices); talkChoice.inputAction = n"Choice1"; talkChoice.localizedName = "Talk"; talkChoice.isHoldAction = false; ArrayPush(interaction.choices, talkChoice);
      bb.SetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub, ToVariant(interaction), true); return;
    }
    if interaction.id == 670304 && interaction.active { interaction.active = false; ArrayClear(interaction.choices); bb.SetVariant(GetAllBlackboardDefs().UIInteractions.InteractionChoiceHub, ToVariant(interaction), true); }
    data = FromVariant<DialogChoiceHubs>(bb.GetVariant(GetAllBlackboardDefs().UIInteractions.DialogChoiceHubs));
    if ArraySize(data.choiceHubs) > 0 && data.choiceHubs[0].id != 670304 { if IsDefined(this.promptBody) { this.ClearPrompt(player); } return; }
    if this.promptBody == body && this.promptPage == body.ncManagerPage && ArraySize(data.choiceHubs) > 0 { return; }
    NightCityBusinessManagers.Options(body.ncManagerPage, captions, actions);
    if this.promptPage != body.ncManagerPage { this.selection = 0; }
    this.promptBody = body; this.promptPage = body.ncManagerPage; this.promptActions = actions;
    hub.id = 670304; hub.activityState = EVisualizerActivityState.Active; hub.title = NC_VenueValue(this.Index(body.ncManagerVenue), "name") + " manager"; hub.flags = EVisualizerDefinitionFlags.None;
    i = 0;
    while i < ArraySize(captions) {
      choice.inputActionName = i == this.selection ? n"DialogConfirm" : n""; choice.localizedName = captions[i];
      ArrayPush(hub.choices, choice); i += 1;
    }
    ArrayClear(data.choiceHubs); ArrayPush(data.choiceHubs, hub);
    bb.SetInt(GetAllBlackboardDefs().UIInteractions.ActiveChoiceHubID, 670304, true);
    bb.SetInt(GetAllBlackboardDefs().UIInteractions.SelectedIndex, this.selection, true);
    bb.SetVariant(GetAllBlackboardDefs().UIInteractions.DialogChoiceHubs, ToVariant(data), true);
  }
  public final func HasPrompt() -> Bool { return IsDefined(this.promptBody); }
  public final func Talking() -> Bool { return this.talking && this.HasPrompt(); }
  public final func Selection() -> Int32 { return this.selection; }
  public final func TalkData() -> InteractionChoiceHubData {
    let hub: InteractionChoiceHubData;
    let choice: InteractionChoiceData;
    hub.id = 670304; hub.active = this.HasPrompt() && !this.talking;
    if this.HasPrompt() { hub.title = NameToString(this.promptBody.ncManagerLabel); }
    choice.inputAction = n"Choice1"; choice.localizedName = "Talk"; ArrayPush(hub.choices, choice); return hub;
  }
  public final func DialogueData() -> DialogChoiceHubs {
    let data: DialogChoiceHubs;
    let hub: ListChoiceHubData;
    let choice: ListChoiceData;
    let captions: array<String>;
    let actions: array<String>;
    let i: Int32;
    if !this.Talking() { return data; }
    NightCityBusinessManagers.Options(this.promptBody.ncManagerPage, captions, actions);
    hub.id = 670304; hub.activityState = EVisualizerActivityState.Active; hub.title = NameToString(this.promptBody.ncManagerLabel);
    while i < ArraySize(captions) { choice.inputActionName = i == this.selection ? n"DialogConfirm" : n""; choice.localizedName = captions[i]; ArrayPush(hub.choices, choice); i += 1; }
    ArrayPush(data.choiceHubs, hub); return data;
  }
  private final func Input(player: ref<PlayerPuppet>) -> Void {
    let confirm: Bool = NC_Hotkey(70, 0);
    let up: Bool = NC_Hotkey(38, 0) || NC_Hotkey(81, 0);
    let down: Bool = NC_Hotkey(40, 0) || NC_Hotkey(69, 0);
    if !this.HasPrompt() { return; }
    if !this.talking {
      if confirm && NC_Clock() >= this.nextAction { this.talking = true; this.nextAction = NC_Clock() + 0.3; this.selection = 0; this.promptPage = -1; this.Prompt(player); }
      return;
    }
    if up || down {
      this.selection = (this.selection + ArraySize(this.promptActions) + (up ? -1 : 1)) % ArraySize(this.promptActions);
      GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UIInteractions).SetInt(GetAllBlackboardDefs().UIInteractions.SelectedIndex, this.selection, true);
      GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UIInteractions).SetVariant(GetAllBlackboardDefs().UIInteractions.DialogChoiceHubs, ToVariant(this.DialogueData()), true);
    }
    if confirm && this.selection >= 0 && this.selection < ArraySize(this.promptActions) { this.Action(player, this.promptBody, this.promptActions[this.selection]); this.Prompt(player); }
  }
  public final func Diagnostic() -> String {
    let result: String = "[";
    let i: Int32 = 0;
    while i < ArraySize(this.sites) {
      if i > 0 { result += ","; }
      result += "{\"venue\":\"" + NameToString(this.sites[i].venue) + "\",\"x\":" + ToString(this.sites[i].position.X) + ",\"y\":" + ToString(this.sites[i].position.Y) + ",\"z\":" + ToString(this.sites[i].position.Z) + ",\"present\":" + ToString(IsDefined(this.sites[i].body)) + ",\"talking\":" + ToString(this.Talking()) + ",\"prompt\":" + ToString(this.HasPrompt()) + ",\"selected\":" + ToString(this.selection) + "}";
      i += 1;
    }
    return result + "]";
  }
  public static func IsManager(body: ref<ScriptedPuppet>) -> Bool {
    return IsDefined(body) && body.GetRecordID() == t"Character.NivalisBusinessManager";
  }
  private final func Say(player: ref<PlayerPuppet>, line: String) -> Void {
    let message: SimpleScreenMessage;
    message.isShown = true; message.duration = 12.0; message.message = "Manager: " + line;
    GameInstance.GetBlackboardSystem(player.GetGame()).Get(GetAllBlackboardDefs().UI_Notifications).SetVariant(GetAllBlackboardDefs().UI_Notifications.WarningMessage, ToVariant(message), true);
  }
  private final func Index(venue: CName) -> Int32 {
    let i: Int32 = 0;
    while i < NC_Count() { if Equals(NC_VenueValue(i, "id"), NameToString(venue)) { return i; } i += 1; }
    return -1;
  }
  private final func Seed(player: ref<PlayerPuppet>) -> Void {
    let maps: array<ref<IMappin>> = GameInstance.GetMappinSystem(player.GetGame()).GetAllMappins();
    let site: ref<NightCityManagedBusiness>;
    let i: Int32 = ArraySize(this.sites);
    let j: Int32;
    let k: Int32;
    let close: Bool;
    let point: Vector4;
    if i >= NC_Count() { return; }
    while j < ArraySize(maps) && i < NC_Count() {
      if Equals(maps[j].GetVariant(), gamedataMappinVariant.ServicePointFoodVariant) || Equals(maps[j].GetVariant(), gamedataMappinVariant.ServicePointBarVariant) {
        point = maps[j].GetWorldPosition(); close = false; k = 0;
        while k < ArraySize(this.sites) { if Vector4.Distance(point, this.sites[k].position) < 12.0 { close = true; } k += 1; }
        if !close {
          site = new NightCityManagedBusiness(); site.venue = StringToName(NC_VenueValue(i, "id"));
          site.cityName = StringToName(maps[j].GetDisplayName()); site.position = point;
          ArrayPush(this.sites, site); i += 1;
        }
      }
      j += 1;
    }
    if ArraySize(this.sites) >= NC_Count() { GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint(); }
  }
  public final func Tick(player: ref<PlayerPuppet>, business: ref<NightCityBusinessSystem>) -> Void {
    let now: Float = NC_Clock();
    let entities: ref<DynamicEntitySystem> = GameInstance.GetDynamicEntitySystem();
    let site: ref<NightCityManagedBusiness>;
    let spec: ref<DynamicEntitySpec>;
    let tag: CName;
    let id: EntityID;
    let point: Vector4;
    let probe: NavigationFindPointResult;
    let floor: TraceResult;
    let rotation: EulerAngles;
    let index: Int32;
    let travel: Int32;
    let i: Int32 = 0;
    if NotEquals(this.pending, "") && NotEquals(NC_ReplyState(this.pending), "pending") {
      this.Say(player, Equals(NC_ReplyState(this.pending), "done") ? "Done, boss. The change is saved." : "That needs checking. I've kept operations paused so nothing gets messed up."); this.pending = "";
    }
    this.Prompt(player); this.Input(player);
    if now < this.nextWorld || !IsDefined(entities) || !entities.IsReady() || !entities.IsRestored() { return; }
    this.nextWorld = now + 2.0;
    this.Seed(player);
    if ArraySize(this.sites) > 0 {
      travel = NC_DeveloperManagerTravel();
      if travel >= 0 && travel < ArraySize(this.sites) {
        point = this.sites[travel].position; point.X += 4.0;
        GameInstance.GetTeleportationFacility(player.GetGame()).Teleport(player, point, rotation);
      }
    }
    while i < ArraySize(this.sites) {
      site = this.sites[i]; index = this.Index(site.venue);
      if NotEquals(site.venue, n"") {
        entities.DeleteTagged(StringToName("NivalisManager_" + NameToString(site.venue)));
        entities.DeleteTagged(StringToName("NivalisManagerV2_" + NameToString(site.venue)));
        tag = StringToName("NivalisManagerV3_" + NameToString(site.venue)); id = entities.GetTaggedID(tag);
        if !entities.IsManaged(id) && Vector4.Distance(player.GetWorldPosition(), site.position) < 80.0 {
          point = site.position;
          if i == 0 { point = new Vector4(-481.090881, 582.255493, 31.734262, 1.0); }
          else { point.X += 2.5; }
          probe = GameInstance.GetAINavigationSystem(player.GetGame()).FindPointInSphereForCharacter(point, 1.5, player);
          if Equals(probe.status, worldNavigationRequestStatus.OK) {
            point = probe.point;
            if GameInstance.GetSpatialQueriesSystem(player.GetGame()).SyncRaycastByCollisionGroup(point + new Vector4(0.0, 0.0, 0.15, 0.0), point + new Vector4(0.0, 0.0, -1.5, 0.0), n"Static", floor, true, false) && floor.normal.Z >= 0.8 {
            point.Z = floor.position.Z + 0.02;
            site.position = point; site.placed = true;
            spec = new DynamicEntitySpec(); spec.recordID = t"Character.NivalisBusinessManager";
            spec.position = site.position; spec.orientation = EulerAngles.ToQuat(rotation);
            spec.persistSpawn = true; spec.persistState = false; spec.alwaysSpawned = false; spec.spawnInView = true; spec.active = true;
            ArrayPush(spec.tags, tag); id = entities.CreateEntity(spec);
            }
          }
        }
        site.body = entities.GetEntity(id) as NPCPuppet;
        if IsDefined(site.body) {
          site.body.ncManagerVenue = site.venue;
          if index >= 0 { site.businessName = StringToName(NC_VenueValue(index, "name") + " - Manager"); }
          site.body.ncManagerLabel = NotEquals(site.businessName, n"") ? site.businessName : n"Business manager (link loading)";
          if Vector4.Distance(player.GetWorldPosition(), site.body.GetWorldPosition()) < 4.0 {
            point = player.GetWorldPosition() - site.body.GetWorldPosition(); point.Z = 0.0;
            rotation = Vector4.ToRotation(point); rotation.Pitch = 0.0; rotation.Roll = 0.0;
            GameInstance.GetTeleportationFacility(player.GetGame()).Teleport(site.body, site.body.GetWorldPosition(), rotation);
          }
          if !IsDefined(site.hold) && IsDefined(site.body.GetAIControllerComponent()) {
            site.hold = new AIHoldPositionCommand(); site.hold.duration = -1.0; site.body.GetAIControllerComponent().SendCommand(site.hold);
          }
          site.body.GetAttitudeAgent().SetAttitudeTowards(player.GetAttitudeAgent(), EAIAttitude.AIA_Friendly);
        } else { site.hold = null;
        }
      }
      i += 1;
    }
  }
  private final func Change(player: ref<PlayerPuppet>, venue: CName, action: String, extra: String) -> Void {
    if NotEquals(this.pending, "") { this.Say(player, "One thing at a time. I'm still on the last job."); return; }
    let id: String = NC_NewID();
    if NC_Request("nc-manager-action", id, "{\"venue\":\"" + NameToString(venue) + "\",\"action\":\"" + action + "\"" + extra + "}") {
      this.pending = id; this.Say(player, "On it, boss.");
    } else { this.Say(player, "We're busy with supplies or a check right now. Give me a moment."); }
  }
  public final func Action(player: ref<PlayerPuppet>, body: ref<NPCPuppet>, choice: String) -> Void {
    let index: Int32 = this.Index(body.ncManagerVenue);
    let business: ref<NightCityBusinessSystem> = GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"NightCityBusinessSystem") as NightCityBusinessSystem;
    let count: Int32;
    let price: Int32;
    if NC_Clock() < this.nextAction { return; }
    if Equals(choice, "NCBye") { this.nextAction = NC_Clock() + 0.3; this.ClearPrompt(player); return; }
    if index < 0 || !NC_Connected() { this.Say(player, "The business link is loading. Bring the paired Nivalis save online, then we can make changes."); return; }
    this.nextAction = NC_Clock() + 0.3;
    switch choice {
      case "NCBye": this.Say(player, "Catch you later, boss."); this.ClearPrompt(player); return;
      case "NCStart":
      case "NCPause":
        if NotEquals(this.pending, "") { this.Say(player, "Let me finish the current job first."); return; }
        this.pending = NC_NewID();
        if !NC_Request("nc-maintenance", this.pending, Equals(choice, "NCStart") ? "{\"enabled\":false}" : "{\"enabled\":true}") { this.pending = ""; this.Say(player, "The link is busy. Try again in a moment."); } break;
      case "NCStatus":
        this.Say(player, NC_VenueValue(index, "name") + ": " + NC_VenueValue(index, "menuCount") + " dishes, " + NC_VenueValue(index, "staffCount") + " staff, " + NC_VenueValue(index, "seats") + " seats. Hours " + NC_VenueValue(index, "openStart") + "-" + NC_VenueValue(index, "openEnd") + ". Net cash for all businesses: eddies " + ToString(NC_CashNet()) + "."); break;
      case "NCMenu": body.ncManagerPage = 1; break;
      case "NCHours": body.ncManagerPage = 2; break;
      case "NCBack": body.ncManagerPage = 0; break;
      case "NCNextMeal":
        count = StringToInt(NC_VenueValue(index, "menuCount"));
        if count > 0 { body.ncManagerDish = (body.ncManagerDish + 1) % count; }
        this.Say(player, NC_MenuValue(index, body.ncManagerDish, "dish") + " is eddies " + ToString(StringToInt(NC_MenuValue(index, body.ncManagerDish, "priceHundredths")) / 100) + "."); break;
      case "NCPriceUp":
      case "NCPriceDown":
        price = StringToInt(NC_MenuValue(index, body.ncManagerDish, "priceHundredths")) + (Equals(choice, "NCPriceUp") ? 100 : -100);
        if price < 100 { this.Say(player, "That's already our minimum price."); return; }
        this.Change(player, body.ncManagerVenue, "price", ",\"dish\":\"" + NC_MenuValue(index, body.ncManagerDish, "id") + "\",\"price\":" + ToString(price)); break;
      case "NCFocusMenu": this.Change(player, body.ncManagerVenue, "menu", ",\"count\":8"); break;
      case "NCEarly": this.Change(player, body.ncManagerVenue, "hours", ",\"start\":8,\"end\":20"); break;
      case "NCLunch": this.Change(player, body.ncManagerVenue, "hours", ",\"start\":12,\"end\":20"); break;
      case "NCLate": this.Change(player, body.ncManagerVenue, "hours", ",\"start\":12,\"end\":24"); break;
      case "NCCrew": this.Change(player, body.ncManagerVenue, "staff", ""); break;
      case "NCRestock": business.RequestRestock(); this.Say(player, "I'll order missing supplies for every business. The actual bill comes from your eddies."); break;
      case "NCAuto": this.Say(player, "Automatic restocking is " + (NC_ToggleRestock() ? "on." : "off.")); break;
      case "NCCash": business.RequestCash(); this.Say(player, "I'll settle our net income and operating costs into your eddies."); break;
    }
    body.NivalisManagerChoices();
  }
}

@addField(NPCPuppet)
public let ncManagerVenue: CName;
@addField(NPCPuppet)
public let ncManagerPage: Int32;
@addField(NPCPuppet)
public let ncManagerDish: Int32;

@addMethod(ScriptedPuppet)
public final func NivalisManagerChoices() -> Void {
  let choices: array<InteractionChoice>;
  let choice: InteractionChoice;
  let captions: array<String>;
  let actions: array<String>;
  let i: Int32 = 0;
  let npc: ref<NPCPuppet> = this as NPCPuppet;
  if !NightCityBusinessManagers.IsManager(this) || !IsDefined(this.m_interactionComponent) { return; }
  NightCityBusinessManagers.Options(npc.ncManagerPage, captions, actions);
  while i < ArraySize(captions) {
    choice.caption = captions[i]; choice.choiceMetaData.tweakDBName = actions[i];
    ArrayPush(choices, choice); i += 1;
  }
  this.m_interactionComponent.SetChoices(choices, n"GenericTalk");
}

@wrapMethod(ScriptedPuppet)
protected cb func OnInteractionActivated(evt: ref<InteractionActivationEvent>) -> Bool {
  if NightCityBusinessManagers.IsManager(this) && Equals(evt.layerData.tag, n"GenericTalk") {
    if Equals(evt.eventType, gameinteractionsEInteractionEventType.EIET_activate) { this.NivalisManagerChoices(); }
    else { this.m_interactionComponent.ResetChoices(n"GenericTalk"); }
    return true;
  }
  return wrappedMethod(evt);
}
@wrapMethod(ScriptedPuppet)
protected cb func OnInteractionUsed(evt: ref<InteractionChoiceEvent>) -> Bool {
  if NightCityBusinessManagers.IsManager(this) {
    (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"NightCityBusinessManagers") as NightCityBusinessManagers).Action(evt.activator as PlayerPuppet, this as NPCPuppet, evt.choice.choiceMetaData.tweakDBName); return true;
  }
  return wrappedMethod(evt);
}
@wrapMethod(ScriptedPuppet)
protected cb func OnInteraction(evt: ref<InteractionChoiceEvent>) -> Bool {
  if NightCityBusinessManagers.IsManager(this) {
    (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"NightCityBusinessManagers") as NightCityBusinessManagers).Action(evt.activator as PlayerPuppet, this as NPCPuppet, evt.choice.choiceMetaData.tweakDBName); return true;
  }
  return wrappedMethod(evt);
}

@addField(NPCPuppet)
public let ncManagerLabel: CName;


@wrapMethod(InteractionUIBase)
protected cb func OnInteractionData(value: Variant) -> Bool {
  let player: ref<PlayerPuppet> = this.GetPlayerControlledObject() as PlayerPuppet;
  let manager: ref<NightCityBusinessManagers>;
  if IsDefined(player) {
    manager = GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"NightCityBusinessManagers") as NightCityBusinessManagers;
    if IsDefined(manager) && manager.HasPrompt() { if manager.Talking() { let empty: InteractionChoiceHubData; return wrappedMethod(ToVariant(empty)); } return wrappedMethod(ToVariant(manager.TalkData())); }
  }
  return wrappedMethod(value);
}
@wrapMethod(InteractionUIBase)
protected cb func OnDialogsData(value: Variant) -> Bool {
  let player: ref<PlayerPuppet> = this.GetPlayerControlledObject() as PlayerPuppet;
  let manager: ref<NightCityBusinessManagers>;
  if IsDefined(player) {
    manager = GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"NightCityBusinessManagers") as NightCityBusinessManagers;
    if IsDefined(manager) && manager.Talking() { return wrappedMethod(ToVariant(manager.DialogueData())); }
  }
  return wrappedMethod(value);
}
@wrapMethod(dialogWidgetGameController)
protected cb func OnDialogsActivateHub(activeHubId: Int32) -> Bool {
  let player: ref<PlayerPuppet> = this.GetPlayerControlledObject() as PlayerPuppet;
  let manager: ref<NightCityBusinessManagers>;
  if IsDefined(player) {
    manager = GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"NightCityBusinessManagers") as NightCityBusinessManagers;
    if IsDefined(manager) && manager.Talking() { return wrappedMethod(670304); }
  }
  return wrappedMethod(activeHubId);
}
@wrapMethod(dialogWidgetGameController)
protected cb func OnDialogsSelectIndex(index: Int32) -> Bool {
  let player: ref<PlayerPuppet> = this.GetPlayerControlledObject() as PlayerPuppet;
  let manager: ref<NightCityBusinessManagers>;
  if IsDefined(player) {
    manager = GameInstance.GetScriptableSystemsContainer(player.GetGame()).Get(n"NightCityBusinessManagers") as NightCityBusinessManagers;
    if IsDefined(manager) && manager.Talking() { return wrappedMethod(manager.Selection()); }
  }
  return wrappedMethod(index);
}
@wrapMethod(NameplateVisualsLogicController)
public final func SetVisualData(puppet: ref<GameObject>, const incomingData: script_ref<NPCNextToTheCrosshair>, opt isNewNpc: Bool) -> Void {
  let manager: ref<NPCPuppet> = puppet as NPCPuppet;
  let changed: NPCNextToTheCrosshair;
  if NightCityBusinessManagers.IsManager(manager) && NotEquals(manager.ncManagerLabel, n"") {
    changed = Deref(incomingData); changed.name = NameToString(manager.ncManagerLabel); wrappedMethod(puppet, changed, isNewNpc); return;
  }
  wrappedMethod(puppet, incomingData, isNewNpc);
}
@wrapMethod(NPCPuppet)
public const func CompileScannerChunks() -> Bool {
  let result: Bool = wrappedMethod();
  let name: ref<ScannerName>;
  if NightCityBusinessManagers.IsManager(this) && NotEquals(this.ncManagerLabel, n"") {
    name = new ScannerName(); name.Set(NameToString(this.ncManagerLabel));
    GameInstance.GetBlackboardSystem(this.GetGame()).Get(GetAllBlackboardDefs().UI_ScannerModules).SetVariant(GetAllBlackboardDefs().UI_ScannerModules.ScannerName, ToVariant(name), true);
  }
  return result;
}
