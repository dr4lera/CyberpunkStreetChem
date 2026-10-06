// Street Chem solo v0.2.0 camera loop. Ordinary-key controls are in StreetChemCity.reds.
public native func SC_Key(key: Int32) -> Bool;
public native func SC_Camera(position: Vector4, forward: Vector4, up: Vector4, fov: Float, aspect: Float) -> Void;
public native func SC_Visual(equipment: String, position: Vector4, yaw: Float) -> Bool;

public class StreetChemVisualTick extends Event {}

@addField(PlayerPuppet)
private let scVisualActive: Bool;

@addField(PlayerPuppet)
private let scVisualDelay: DelayID;

@wrapMethod(PlayerPuppet)
protected cb func OnGameAttached() -> Bool {
  wrappedMethod();
  this.scVisualActive = true;
  this.scVisualDelay = GameInstance.GetDelaySystem(this.GetGame()).DelayEvent(this, new StreetChemVisualTick(), 0.016, false);
}

@wrapMethod(PlayerPuppet)
protected cb func OnDetach() -> Bool {
  this.scVisualActive = false;
  (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"StreetChemDealerSystem") as StreetChemDealerSystem).Stop(this);
  (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"StreetChemDoseSystem") as StreetChemDoseSystem).StopWeapon(this);
  GameInstance.GetDelaySystem(this.GetGame()).CancelDelay(this.scVisualDelay);
  wrappedMethod();
}

@addMethod(PlayerPuppet)
protected cb func OnStreetChemVisualTick(evt: ref<StreetChemVisualTick>) -> Bool {
  let pose: Transform;
  let position: Vector4;
  let camera: ref<CameraSystem>;
  let system: ref<StreetChemCitySystem>;
  if !this.scVisualActive { return false; }
  (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"StreetChemDoseSystem") as StreetChemDoseSystem).Tick(this);
  (GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"StreetChemDealerSystem") as StreetChemDealerSystem).TickWorld(this);
  camera = GameInstance.GetCameraSystem(this.GetGame());
  if !GameInstance.GetBlackboardSystem(this.GetGame()).Get(GetAllBlackboardDefs().UI_System).GetBool(GetAllBlackboardDefs().UI_System.IsInMenu) && camera.GetActiveCameraWorldTransform(pose) {
    position = Transform.GetPosition(pose);
    SC_Camera(position, camera.GetActiveCameraForward(), camera.GetActiveCameraUp(), camera.GetActiveCameraFOV(), camera.GetAspectRatio());
    system = GameInstance.GetScriptableSystemsContainer(this.GetGame()).Get(n"StreetChemCitySystem") as StreetChemCitySystem;
    if IsDefined(system) { system.Tick(this, position, camera.GetActiveCameraForward()); }
  }
  this.scVisualDelay = GameInstance.GetDelaySystem(this.GetGame()).DelayEvent(this, new StreetChemVisualTick(), 0.016, false);
}
