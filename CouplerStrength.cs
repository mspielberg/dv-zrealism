using HarmonyLib;

namespace DvMod.ZRealism
{
    [HarmonyPatch(typeof(Coupler), nameof(Coupler.CreateJoints))]
    public static class CouplerStrength
    {
        public static class CreateJointsPatch
        {
            public static void Postfix(Coupler __instance)
            {
                __instance.springyCJ.breakForce = Main.settings.couplerStrength * 1e6f;
                __instance.rigidCJ.breakForce = Main.settings.couplerStrength * 1e6f;
            }
        }
    }
}