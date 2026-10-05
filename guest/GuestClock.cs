using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI;
using UnityEngine;

namespace StreetChem;
internal static class GuestClock
{
    static readonly SceneTransport pose=new();
    static float next;
    public static object Status() {
        var clock=TimeManager.Instance;
        return new {ok=true,status="clock",time=clock?.CurrentTime,day=clock?.ElapsedDays,endOfDay=clock?.IsEndOfDay,sleeping=clock?.IsSleepInProgress,paused=PauseMenu.Instance?.IsPaused};
    }
    public static object Wake() {
        GuestWorld.RequireSession();var clock=TimeManager.Instance;
        if(!clock.IsEndOfDay || clock.IsSleepInProgress)return Status();
        clock.SkipForwardToTime(TimeManager.WakeTime);
        GuestSave.Changed();return Status();
    }
    public static void Tick() {
        if(Time.unscaledTime<next)return;next=Time.unscaledTime+1;
        if(LoadManager.Instance?.IsGameLoaded!=true || PauseMenu.Instance?.IsPaused==true)return;
        if(!pose.Camera(out _,out _,out _,out _,out _) || pose.WorldKey<=0)return;
        var clock=TimeManager.Instance;
        if(clock.IsEndOfDay && !clock.IsSleepInProgress) {
            try {Wake();MelonLoader.MelonLogger.Msg("Night City passthrough: advanced the native 4 AM cutoff to morning.");}
            catch(Exception e){MelonLoader.MelonLogger.Warning("Native clock rollover failed: "+e.Message);next=Time.unscaledTime+30;}
        }
    }
}
