using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Text.Json;
using System.IO.MemoryMappedFiles;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
namespace StreetChem;

internal sealed class RemoteInput
{
    Keyboard? keyboard;
    Mouse? mouse;
    long lastPacket;
    long nextConnect;
    MemoryMappedFile? shared;
    MemoryMappedViewAccessor? view;
    public bool Active {get;private set;}
    InputDevice[] originalDevices=Array.Empty<InputDevice>();
    public void SetActive(bool active)
    {
        if(active && keyboard==null) {
            keyboard=InputSystem.AddDevice<Keyboard>("StreetChemKeyboard");
            mouse=InputSystem.AddDevice<Mouse>("StreetChemMouse");
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground=true;
        }
        Active=active;
        lastPacket=Environment.TickCount64;
        if(active && GameInput.Instance.PlayerInput!=null) {
            originalDevices=GameInput.Instance.PlayerInput.devices.ToArray();
            GameInput.Instance.PlayerInput.SwitchCurrentControlScheme(new InputDevice[] {keyboard!,mouse!});
            GameInput.Instance.PlayerInput.ActivateInput();
            MelonLogger.Msg("Lab controls active: virtual input devices paired to actual Schedule I PlayerInput.");
        }
        if(!active && originalDevices.Length>0 && GameInput.Instance.PlayerInput!=null) {
            GameInput.Instance.PlayerInput.SwitchCurrentControlScheme(originalDevices);
            originalDevices=Array.Empty<InputDevice>();
        }
        if(!active) Release();
    }
    public void Apply(JsonElement packet)
    {
        if(!Active || keyboard==null || mouse==null) throw new InvalidOperationException("lab_controls_inactive");
        var state=new KeyboardState();
        if(packet.TryGetProperty("keys",out var keys)) {
            foreach(var key in keys.EnumerateArray()) {
                if(Enum.TryParse<Key>(key.GetString(),out var code) && code!=Key.None) state.Set(code,true);
            }
        }
        var ms=new MouseState {
            position=new Vector2(packet.GetProperty("x").GetSingle()*Screen.width,packet.GetProperty("y").GetSingle()*Screen.height),
            delta=new Vector2(packet.GetProperty("dx").GetSingle(),packet.GetProperty("dy").GetSingle()),
            scroll=new Vector2(0,packet.GetProperty("wheel").GetSingle()),
            buttons=(ushort)packet.GetProperty("buttons").GetInt32()
        };
        InputSystem.QueueStateEvent(keyboard,state,-1);
        InputSystem.QueueStateEvent(mouse,ms,-1);
        lastPacket=Environment.TickCount64;
    }
    public void Tick() { if(Active && Environment.TickCount64-lastPacket>1000) SetActive(false); }
    public void PollSharedMemory()
    {
        if(view==null) {
            if(Environment.TickCount64<nextConnect) return;
            nextConnect=Environment.TickCount64+1000;
            try {shared=MemoryMappedFile.OpenExisting(InputConfig.Mapping,MemoryMappedFileRights.Read);view=shared.CreateViewAccessor(0,64,MemoryMappedFileAccess.Read);} catch(FileNotFoundException) {return;}
        }
        if(view.ReadUInt32(0)!=InputConfig.Magic || view.ReadUInt32(4)!=InputConfig.Version) return;
        var sequence=view.ReadUInt64(8);
        if((sequence&1)!=0) return;
        var timestamp=view.ReadInt64(16);
        var keys=view.ReadUInt64(24);
        var ms=new MouseState {position=new Vector2(view.ReadSingle(32)*Screen.width,view.ReadSingle(36)*Screen.height),delta=new Vector2(view.ReadSingle(40),view.ReadSingle(44)),scroll=new Vector2(0,view.ReadSingle(48)),buttons=(ushort)view.ReadUInt32(52)};
        var active=view.ReadUInt32(56)!=0 && Environment.TickCount64-timestamp<InputConfig.StaleMs && Environment.TickCount64>=timestamp;
        if(view.ReadUInt64(8)!=sequence) return;
        if(!active) {if(Active) SetActive(false);return;}
        if(!Active) {
            SetActive(true);
            if(PauseMenu.Instance!=null && PauseMenu.Instance.IsPaused) PauseMenu.Instance.Resume();
        }
        if(timestamp==lastPacket) return;
        var state=new KeyboardState();
        for(int i=0;i<InputConfig.Keys.Length;i++) if((keys&(1UL<<i))!=0) state.Set(InputConfig.Keys[i],true);
        InputSystem.QueueStateEvent(keyboard,state,-1);
        InputSystem.QueueStateEvent(mouse,ms,-1);
        lastPacket=timestamp;
    }
    public void Release()
    {
        if(keyboard!=null) InputSystem.QueueStateEvent(keyboard,new KeyboardState(),-1);
        if(mouse!=null) InputSystem.QueueStateEvent(mouse,new MouseState(),-1);
    }
}
