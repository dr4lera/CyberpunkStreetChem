using Il2CppScheduleOne.Persistence;
using UnityEngine;
namespace StreetChem;
internal static class GuestSave
{
    static bool dirty;
    static float after;
    public static void Changed(){dirty=true;after=Time.unscaledTime+0.5f;}
    public static void Tick(){
        if(!dirty||Time.unscaledTime<after||LoadManager.Instance?.IsGameLoaded!=true||SaveManager.Instance.IsSaving)return;
        SaveManager.Instance.Save();dirty=false;
    }
}
