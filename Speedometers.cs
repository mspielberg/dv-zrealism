using HarmonyLib;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class Speedometers
    {
        private static float GetWheelSpeedKmH(LocoControllerBase loco)
        {
            if (loco.GetComponent<WheelRotationViaAnimation>() is var wrva && wrva != null)
            {
                return Mathf.Abs(wrva.rotationSpeed * wrva.wheelCircumference * 3.6f);
            }
            else if (loco.GetComponent<LocoWheelRotationViaCode>() is var lwrvc && lwrvc != null)
            {
                var drivingForce = loco.drivingForce;
                var revSpeed = drivingForce.wheelslip > 0f ? Mathf.Lerp(lwrvc.curRevSpeed, lwrvc.WHEELSLIP_SPEED_MAX, drivingForce.wheelslip) : lwrvc.curRevSpeed;
                return Mathf.Abs(revSpeed * lwrvc.wheelCircumference * 3.6f);
            }
            return default;
        }

        private static float GetSpeedometerSpeed(LocoControllerBase loco)
        {
            return Mathf.Max(Mathf.Abs(loco.GetSpeedKmH()), GetWheelSpeedKmH(loco));
        }

        [HarmonyPatch(typeof(IndicatorsDiesel), nameof(IndicatorsDiesel.Update))]
        public static class IndicatorsDieselUpdatePatch
        {
            public static void Postfix(IndicatorsDiesel __instance)
            {
                __instance.speed.value = GetSpeedometerSpeed(__instance.ctrl);
            }
        }

        [HarmonyPatch(typeof(IndicatorsShunter), nameof(IndicatorsShunter.Update))]
        public static class IndicatorsShunterUpdatePatch
        {
            public static void Postfix(IndicatorsShunter __instance)
            {
                __instance.speed.value = GetSpeedometerSpeed(__instance.ctrl);
            }
        }

        [HarmonyPatch(typeof(IndicatorsSteam), nameof(IndicatorsSteam.Update))]
        public static class IndicatorsSteamUpdatePatch
        {
            public static void Postfix(IndicatorsSteam __instance)
            {
                __instance.speed.value = GetSpeedometerSpeed(__instance.ctrl);
            }
        }
    }
}