using HarmonyLib;

namespace DvMod.RealismFixes
{
    [HarmonyPatch(typeof(Bogie), nameof(Bogie.SwitchJunctionIfNeeded))]
    static public class SwitchJunctionIfNeededPatch
    {
        public static bool Prefix(Bogie __instance)
        {
            return Main.settings.forceSwitchOnRunningThrough;
        }
    }
}