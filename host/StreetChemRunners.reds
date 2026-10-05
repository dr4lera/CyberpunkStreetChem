public native func SC_RunnerCount() -> Int32;
public native func SC_RunnerValue(index: Int32, field: String) -> String;
public native func SC_ProductSlot(index: Int32) -> Int32;
public native func SC_RunnerReserve(id: String, dealer: String) -> Bool;

public class StreetChemRunner extends IScriptable {
  public persistent let dealer: CName;
  public persistent let bodyID: EntityID;
  public persistent let deployed: Bool;
  public let body: wref<NPCPuppet>;
  public let target: wref<NPCPuppet>;
  public let command: ref<AIMoveToCommand>;
  public let nextSearch: Float;
  public let deadline: Float;
}
public class StreetChemRunnerSystem extends IScriptable {
  private persistent let runners: array<ref<StreetChemRunner>>;
  private let selected: Int32;
  private let menuDealer: String;
  private let menuChoice: Int32;
  public final func MenuOpen() -> Bool { return NotEquals(this.menuDealer, ""); }
  public final func Diagnostic() -> String {
    let i: Int32 = 0;
    let result: String = "[";
    while i < ArraySize(this.runners) {
      if i > 0 { result += ","; }
      result += "{\"dealer\":\"" + NameToString(this.runners[i].dealer) + "\",\"deployed\":" + ToString(this.runners[i].deployed) + ",\"body\":\"" + EntityID.ToDebugString(this.runners[i].bodyID) + "\",\"present\":" + ToString(IsDefined(this.runners[i].body)) + ",\"target\":\"" + (IsDefined(this.runners[i].target) ? EntityID.ToDebugString(this.runners[i].target.GetEntityID()) : "") + "}";
      i += 1;
    }
    return result + "]";
  }
  private final func Entry(id: String) -> ref<StreetChemRunner> {
    let i: Int32 = 0;
    while i < ArraySize(this.runners) { if Equals(NameToString(this.runners[i].dealer), id) { return this.runners[i]; } i += 1; }
    return null;
  }
  private final func Index(id: String) -> Int32 {
    let i: Int32 = 0;
    while i < SC_RunnerCount() { if Equals(SC_RunnerValue(i, "id"), id) { return i; } i += 1; }
    return -1;
  }
  public final func IsRunner(id: EntityID) -> Bool {
    let i: Int32 = 0;
    while i < ArraySize(this.runners) { if this.runners[i].deployed && this.runners[i].bodyID == id { return true; } i += 1; }
    return false;
  }
  private final func Stop(runner: ref<StreetChemRunner>) -> Void {
    if IsDefined(runner.body) && IsDefined(runner.command) {
      runner.body.GetAIControllerComponent().CancelCommand(runner.command);
      runner.body.GetAIControllerComponent().StopExecutingCommand(runner.command, true);
    }
    runner.command = null; runner.target = null;
  }
  public final func Tick(player: ref<PlayerPuppet>, city: ref<StreetChemCitySystem>, now: Float) -> Void {
    let id: String;
    let runner: ref<StreetChemRunner>;
    let body: ref<NPCPuppet>;
    let route: ref<NavigationPath>;
    let nearby: array<ref<NPCPuppet>>;
    let index: Int32;
    let i: Int32;
    let j: Int32;
    if SC_Hotkey(74, 1) {
      if SC_RunnerCount() == 0 { city.Notify(player, "Recruit a runner in Schedule I first."); }
      else { this.selected = (this.selected + 1) % SC_RunnerCount(); city.Notify(player, SC_RunnerValue(this.selected, "name") + " | stock " + SC_RunnerValue(this.selected, "quantity") + " | cash " + SC_RunnerValue(this.selected, "cash") + " eddies. Aim at a civilian: Ctrl+K assigns their Night City body.", 15.0); }
    }
    if SC_Hotkey(75, 1) {
      if SC_RunnerCount() == 0 { city.Notify(player, "Recruit a runner in Schedule I first."); return; }
      if this.selected >= SC_RunnerCount() { this.selected = 0; }
      id = SC_RunnerValue(this.selected, "id"); runner = this.Entry(id);
      if IsDefined(runner) && runner.deployed && IsDefined(runner.body) {
        this.Stop(runner); runner.deployed = false; city.Notify(player, "Runner released. Stock and earnings stay with your Schedule I dealer.");
      } else {
        body = GameInstance.GetTargetingSystem(player.GetGame()).GetLookAtObject(player, true, true) as NPCPuppet;
        if !IsDefined(body) || !(body.IsCrowd() || body.IsCivilian()) || body.IsQuest() || !body.IsActive() || body.IsCharacterPolice() || body.IsAggressive() || Vector4.Distance(player.GetWorldPosition(), body.GetWorldPosition()) > 5.0 || this.IsRunner(body.GetEntityID()) {
          city.Notify(player, "Aim at a living ambient civilian within 5 metres, then Ctrl+K. Story characters are excluded."); return;
        }
        if !IsDefined(body.GetAIControllerComponent()) { city.Notify(player, "This civilian cannot take walking commands. Try another."); return; }
        if !IsDefined(runner) { runner = new StreetChemRunner(); runner.dealer = StringToName(id); ArrayPush(this.runners, runner); }
        runner.body = body; runner.bodyID = body.GetEntityID(); runner.deployed = true; runner.nextSearch = now + 2.0;
        city.Notify(player, SC_RunnerValue(this.selected, "name") + " assigned to this Night City civilian. Aim at them: Ctrl+S for stock and cash.", 15.0);
      }
      GameInstance.GetAutoSaveSystem(player.GetGame()).RequestForcedCheckpoint();
    }
    while i < ArraySize(this.runners) {
      runner = this.runners[i];
      if runner.deployed {
        if !IsDefined(runner.body) { runner.body = GameInstance.FindEntityByID(player.GetGame(), runner.bodyID) as NPCPuppet; }
        if IsDefined(runner.body) && runner.body.IsActive() {
          index = this.Index(NameToString(runner.dealer));
          if index >= 0 && StringToInt(SC_RunnerValue(index, "quantity")) > 0 && !city.SaleBusy() {
            if IsDefined(runner.target) && (!runner.target.IsActive() || now > runner.deadline) { this.Stop(runner); runner.nextSearch = now + 3.0; }
            if !IsDefined(runner.target) && now >= runner.nextSearch {
              runner.nextSearch = now + 3.0; nearby = player.GetNPCsAroundObject(20.0); j = 0;
              while j < ArraySize(nearby) {
                if (nearby[j].IsCrowd() || nearby[j].IsCivilian()) && nearby[j].IsActive() && !nearby[j].IsCharacterPolice() && !nearby[j].IsAggressive() && !this.IsRunner(nearby[j].GetEntityID()) && !city.BuyerCooling(nearby[j].GetEntityID(), now) {
                  route = GameInstance.GetAINavigationSystem(player.GetGame()).CalculatePathForCharacter(runner.body.GetWorldPosition(), nearby[j].GetWorldPosition(), 0.5, runner.body);
                  if IsDefined(route) && ArraySize(route.path) > 0 {
                    runner.target = nearby[j]; runner.deadline = now + 25.0; runner.command = new AIMoveToCommand();
                    AIPositionSpec.SetEntity(runner.command.movementTarget, runner.target);
                    runner.command.movementType = moveMovementType.Walk; runner.command.ignoreNavigation = false;
                    runner.command.desiredDistanceFromTarget = 1.2; runner.command.finishWhenDestinationReached = true;
                    runner.body.GetAIControllerComponent().SendCommand(runner.command); break;
                  }
                }
                j += 1;
              }
            }
            if IsDefined(runner.target) && Vector4.Distance(runner.body.GetWorldPosition(), runner.target.GetWorldPosition()) < 1.8 {
              city.BeginRunnerSale(player, runner.target, NameToString(runner.dealer)); this.Stop(runner); runner.nextSearch = now + 8.0;
            }
          }
        }
      }
      i += 1;
    }
  }
  public final func Interact(player: ref<PlayerPuppet>, city: ref<StreetChemCitySystem>, camera: Vector4, forward: Vector4, pressed: Bool) -> Bool {
    let body: ref<NPCPuppet>;
    let runner: ref<StreetChemRunner>;
    let i: Int32;
    if this.MenuOpen() {
      if SC_Hotkey(78, 1) { this.menuChoice = (this.menuChoice + 1) % 3; this.ShowMenu(player, city); }
      if pressed {
        if this.menuChoice == 0 { city.CollectRunner(player, this.menuDealer); }
        else { if this.menuChoice == 1 { city.StockRunner(player, this.menuDealer); } }
        this.menuDealer = "";
      }
      return pressed;
    }
    if !pressed { return false; }
    body = GameInstance.GetTargetingSystem(player.GetGame()).GetLookAtObject(player, true, true) as NPCPuppet;
    if !IsDefined(body) { return false; }
    while i < ArraySize(this.runners) {
      runner = this.runners[i];
      if runner.deployed && runner.bodyID == body.GetEntityID() && Vector4.Distance(player.GetWorldPosition(), body.GetWorldPosition()) < 4.0 {
        this.menuDealer = NameToString(runner.dealer); this.menuChoice = 0; this.ShowMenu(player, city); return true;
      }
      i += 1;
    }
    return false;
  }
  private final func ShowMenu(player: ref<PlayerPuppet>, city: ref<StreetChemCitySystem>) -> Void {
    let index: Int32 = this.Index(this.menuDealer);
    let action: String = this.menuChoice == 0 ? "COLLECT EDDIES" : this.menuChoice == 1 ? "GIVE UP TO 5 SELECTED PRODUCTS" : "CLOSE";
    city.Notify(player, SC_RunnerValue(index, "name") + " | cash " + SC_RunnerValue(index, "cash") + " | stock " + SC_RunnerValue(index, "quantity") + " | " + action + " — Ctrl+N: next; Ctrl+S: confirm.", 20.0);
  }
}
// Street Chem solo v0.1.0 — original mod source, MIT licensed.
