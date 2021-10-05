using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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

        [HarmonyPatch]
        public static class IndicatorsRemoveSpeedometerUpdatePatch
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(IndicatorsDiesel), nameof(IndicatorsDiesel.Update));
                yield return AccessTools.Method(typeof(IndicatorsShunter), nameof(IndicatorsShunter.Update));
                yield return AccessTools.Method(typeof(IndicatorsSteam), nameof(IndicatorsSteam.Update));
            }

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> insts = new List<CodeInstruction>(instructions);
                var index = insts.FindIndex(inst => inst.opcode == OpCodes.Ldfld && inst.operand is FieldInfo field && field.Name == "speed");
                if (index >= 0)
                    insts.RemoveRange(index, 6);
                return insts;
            }
        }

        private static IEnumerator UpdateSpeedometerCoro(Indicator speedometer, LocoControllerBase controller)
        {
            while (true)
            {
                yield return WaitFor.Seconds(1f);
                speedometer.value = GetSpeedometerSpeed(controller);
            }
        }

        [HarmonyPatch(typeof(IndicatorsDiesel), nameof(IndicatorsDiesel.Start))]
        public static class IndicatorsDieselStartPatch
        {
            public static void Postfix(IndicatorsDiesel __instance)
            {
                __instance.StartCoroutine(UpdateSpeedometerCoro(__instance.speed, __instance.ctrl));
            }
        }

        [HarmonyPatch(typeof(IndicatorsShunter), nameof(IndicatorsShunter.Start))]
        public static class IndicatorsShunterStartPatch
        {
            public static void Postfix(IndicatorsShunter __instance)
            {
                __instance.StartCoroutine(UpdateSpeedometerCoro(__instance.speed, __instance.ctrl));
            }
        }

        [HarmonyPatch(typeof(IndicatorsSteam), nameof(IndicatorsSteam.Start))]
        public static class IndicatorsSteamStartPatch
        {
            public static void Postfix(IndicatorsSteam __instance)
            {
                __instance.StartCoroutine(UpdateSpeedometerCoro(__instance.speed, __instance.ctrl));
            }
        }
    }
}