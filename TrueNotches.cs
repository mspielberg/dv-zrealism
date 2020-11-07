using HarmonyLib;
using UnityEngine;

namespace DvMod.RealismFixes
{
    public static class ShunterThrottleNotchFix
    {
        private const int NumNotches = 7;
        private static float ActualThrottle(float impreciseThrottle) =>
            Mathf.Pow(Mathf.RoundToInt(impreciseThrottle * NumNotches) / (float)NumNotches, Main.settings.throttleGamma);

        [HarmonyPatch(typeof(LocoKeyboardInputShunter), nameof(LocoKeyboardInputShunter.TryApplyThrottleInput))]
        public static class TryApplyThrottleInputPatch
        {
            public static void Postfix(LocoKeyboardInputShunter __instance)
            {
                if (__instance.currentInputSpeedThrottle == 0.0f && __instance.throttleVelo == 0.0f)
                    __instance.setThrottleDelegate(ActualThrottle(__instance.control.targetThrottle));
            }
        }

        [HarmonyPatch(typeof(LocoControllerShunter), nameof(LocoControllerShunter.UpdateSimThrottle))]
        public static class UpdateSimThrottlePatch
        {
            public static bool Prefix(LocoControllerShunter __instance)
            {
                // var loco = TrainCar.Resolve(__instance.gameObject);
                var sim = __instance.sim;
                //     var lever = loco.loadedInterior?.GetComponent<CabInputShunter>()?.throttleControl;
                //     Main.DebugLog(loco, $"{loco.ID}: leverPosition={lever?.Value}, throttle={__instance.throttle * NumNotches}, targetThrottle={__instance.targetThrottle}, rounded={ActualThrottle(__instance.throttle) * NumNotches}");
                sim.throttle.SetValue(sim.engineOn ? ActualThrottle(__instance.targetThrottle) : 0.0f);
                return false;
            }
        }

        [HarmonyPatch(typeof(ShunterLocoSimulation), nameof(ShunterLocoSimulation.SimulateEngineRPM))]
        public static class SimulateEngineRPMPatch
        {
            public static bool Prefix(ShunterLocoSimulation __instance)
            {
                // var loco = TrainCar.Resolve(__instance.gameObject);
                var throttleVelo = __instance.throttleToTargetDiff.value;
                var nextRPM = Mathf.SmoothDamp(
                    current: __instance.engineRPM.value,
                    target: __instance.throttle.value,
                    currentVelocity: ref throttleVelo,
                    smoothTime: 1f / Main.settings.shunterThrottleResponse,
                    maxSpeed: 1f);
                __instance.throttleToTargetDiff.SetNextValue(throttleVelo);
                __instance.engineRPM.SetNextValue(nextRPM);
                //     Main.DebugLog(loco, $"{loco.ID}: throttle={__instance.throttle.value}, RPM={__instance.engineRPM.value}, velo={throttleVelo}");
                return false;
            }
        }
    }
}