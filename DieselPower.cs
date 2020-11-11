using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace DvMod.RealismFixes
{
    public static class DieselPower
    {
        public const int NumNotches = 8;
        public const float MinPower = 1f / NumNotches;
        public static float Power(float engineRPM) =>
            Mathf.Pow(((engineRPM * 7f) + 1) / 8f, Main.settings.throttleGamma);
        // Notches 0 and 1 are idle, so 7 distinct RPM settings possible
        public static float TargetRPM(float targetThrottle) =>
            Mathf.Max(0f, (ThrottleNotching.GetNotch(targetThrottle) - 1) / (NumNotches - 1));

        [HarmonyPatch(typeof(DieselLocoSimulation), nameof(DieselLocoSimulation.SimulateEngineRPM))]
        public static class SimulateEngineRPMPatch
        {
            public static bool Prefix(DieselLocoSimulation __instance)
            {
                // var loco = TrainCar.Resolve(__instance.gameObject);
                var throttleVelo = __instance.throttleToTargetDiff.value;
                var nextRPM = Mathf.SmoothDamp(
                    current: __instance.engineRPM.value,
                    target: TargetRPM(__instance.throttle.value),
                    currentVelocity: ref throttleVelo,
                    smoothTime: 1f / Main.settings.shunterThrottleResponse,
                    maxSpeed: 1f);
                __instance.throttleToTargetDiff.SetNextValue(throttleVelo);
                __instance.engineRPM.SetNextValue(nextRPM);
                //     Main.DebugLog(loco, $"{loco.ID}: throttle={__instance.throttle.value}, RPM={__instance.engineRPM.value}, velo={throttleVelo}");
                return false;
            }
        }

        [HarmonyPatch(typeof(LocoControllerDiesel), nameof(LocoControllerDiesel.GetTractionForce))]
        public static class DieselPowerPatch
        {
            public const float MaxPower = 1300 * 0.85f * 1000; // 1300 kW prime mover, 85% transmission efficiency

            private static readonly Dictionary<LocoControllerDiesel, (float, float)> tractionVelo =
                new Dictionary<LocoControllerDiesel, (float, float)>();

            public static bool Prefix(LocoControllerDiesel __instance, ref float __result)
            {
                var (effort, velo) = tractionVelo.GetValueOrDefault(__instance);
                var target = __instance.sim.throttle.value == 0f
                    ? 0f
                    : MaxPower * Power(__instance.sim.engineRPM.value) / Mathf.Max(1f, Mathf.Abs(__instance.GetForwardSpeed()));
                effort = Mathf.SmoothDamp(
                    effort,
                    target,
                    ref velo,
                    0.5f);
                tractionVelo[__instance] = (effort, velo);
                __result = effort;
                return false;
            }
        }

        [HarmonyPatch(typeof(IndicatorsDiesel), nameof(IndicatorsDiesel.Update))]
        public static class AmmeterPatch
        {
            public static void Postfix(IndicatorsDiesel __instance)
            {
                var controller = __instance.ctrl;
                var tractiveEffort = controller.reverser == 0 ? 0f : controller.GetTractionForce();
                var amps = 0.00387571f * tractiveEffort;
                __instance.rpm.transform.parent.Find("I voltage_meter").GetComponent<Indicator>().value = amps;
            }
        }
    }
}