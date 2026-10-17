using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI;
using UnityEngine;
using System.Reflection;

namespace StreetChem;
internal static class GuestClock
{
    static readonly SceneTransport pose=new();
    static float next;
    static readonly Type? sleepType=typeof(TimeManager).Assembly.GetType("Il2CppScheduleOne.GameTime.SleepController");
    static readonly PropertyInfo? oldSleeping=typeof(TimeManager).GetProperty("IsSleepInProgress");
    static readonly MethodInfo? skip=typeof(TimeManager).GetMethod("SkipForwardToTime",new[]{typeof(int)})
        ?? typeof(TimeManager).GetMethod("SkipToTimeAndSync",new[]{typeof(int)});
    static object? SleepInstance()=>sleepType?.GetProperty("Instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy)?.GetValue(null);
    static bool Sleeping(TimeManager clock) {
        if(oldSleeping!=null)return (bool)(oldSleeping.GetValue(clock)??true);
        var sleep=SleepInstance();
        return sleep==null || (bool)(sleepType?.GetProperty("IsSleepInProgress")?.GetValue(sleep)??true);
    }
    static int WakeTime() {
        var sleep=SleepInstance();
        if(sleep!=null && sleepType?.GetMethod("GetWakeTime",Type.EmptyTypes) is MethodInfo getWake)
            return (int)getWake.Invoke(sleep,null)!;
        var property=(sleepType??typeof(TimeManager)).GetProperty("WakeTime",BindingFlags.Public|BindingFlags.Static);
        if(property!=null)return (int)property.GetValue(null)!;
        var field=(sleepType??typeof(TimeManager)).GetField("WakeTime",BindingFlags.Public|BindingFlags.Static);
        if(field!=null)return (int)field.GetValue(null)!;
        throw new InvalidOperationException("native_wake_time_api_unavailable");
    }
    public static object Status() {
        var clock=TimeManager.Instance;
        return new {ok=true,status="clock",time=clock?.CurrentTime,day=clock?.ElapsedDays,endOfDay=clock?.IsEndOfDay,sleeping=clock==null?(bool?)null:Sleeping(clock),paused=PauseMenu.Instance?.IsPaused};
    }
    public static object Wake() {
        GuestWorld.RequireSession();var clock=TimeManager.Instance;
        if(!clock.IsEndOfDay || Sleeping(clock))return Status();
        if(skip==null)throw new InvalidOperationException("native_time_skip_api_unavailable");
        skip.Invoke(clock,new object[]{WakeTime()});
        GuestSave.Changed();return Status();
    }
    public static void Tick() {
        if(Time.unscaledTime<next)return;next=Time.unscaledTime+1;
        if(LoadManager.Instance?.IsGameLoaded!=true || PauseMenu.Instance?.IsPaused==true)return;
        if(!pose.Camera(out _,out _,out _,out _,out _) || pose.WorldKey<=0)return;
        var clock=TimeManager.Instance;
        if(clock.IsEndOfDay && !Sleeping(clock)) {
            try {Wake();MelonLoader.MelonLogger.Msg("Night City passthrough: advanced the native 4 AM cutoff to morning.");}
            catch(Exception e){MelonLoader.MelonLogger.Warning("Native clock rollover failed: "+e.Message);next=Time.unscaledTime+30;}
        }
    }
}
