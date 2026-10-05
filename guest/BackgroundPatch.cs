using HarmonyLib;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StreetChem;
[HarmonyPatch(typeof(PauseMenu), "OnGameLoseFocus")]
internal static class BackgroundPatch
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    static bool Prefix()
    {
        if(LoadManager.Instance?.IsGameLoaded!=true)return true;
        GetWindowThreadProcessId(GetForegroundWindow(),out uint pid);
        try {return Process.GetProcessById((int)pid).ProcessName!="Cyberpunk2077";}
        catch(ArgumentException){return true;}
    }
}
