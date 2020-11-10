using DV.CabControls;
using DV.CabControls.Spec;
using HarmonyLib;
using UnityEngine;

namespace DvMod.RealismFixes
{
    internal static class ThrottleNotching
    {
        public const int NumNotches = 8;
        public static float GetNotch(float impreciseThrottle) =>
            Mathf.RoundToInt(impreciseThrottle * NumNotches);
        public static float Notched(float impreciseThrottle) =>
            GetNotch(impreciseThrottle) / (float)NumNotches;
        public static float Power(float throttle) =>
            Mathf.Pow(Notched(throttle), Main.settings.throttleGamma);
        // Notches 0 and 1 are idle, so 7 distinct RPM settings possible
        public static float TargetRPM(float targetThrottle) =>
            Mathf.Max(0f, (GetNotch(targetThrottle) - 1) / (NumNotches - 1));
    }

    [HarmonyPatch(typeof(ControlsInstantiator), nameof(ControlsInstantiator.Spawn))]
    public static class ThrottleLeverNotchCountPatch
    {
        public static void Prefix(ControlSpec spec)
        {
            if (spec.name == "C throttle" && spec is Lever leverSpec)
                leverSpec.notches = ThrottleNotching.NumNotches + 1;
        }
    }

    public static class DieselThrottleNotchFix
    {
        [HarmonyPatch(typeof(LocoKeyboardInputDiesel), nameof(LocoKeyboardInputDiesel.TryApplyThrottleInput))]
        public static class TryApplyThrottleInputPatch
        {
            public static void Postfix(LocoKeyboardInputDiesel __instance)
            {
                if (__instance.currentInputSpeedThrottle == 0.0f && __instance.throttleVelo == 0.0f)
                    __instance.setThrottleDelegate(ThrottleNotching.Notched(__instance.control.targetThrottle));
            }
        }

        [HarmonyPatch(typeof(LocoControllerDiesel), nameof(LocoControllerDiesel.UpdateSimThrottle))]
        public static class UpdateSimThrottlePatch
        {
            public static bool Prefix(LocoControllerDiesel __instance)
            {
                var loco = TrainCar.Resolve(__instance.gameObject);
                var sim = __instance.sim;
                var lever = loco.loadedInterior?.GetComponent<CabInputDiesel>()?.throttleControl;
                // Main.DebugLog(loco, () =>
                //     $"{loco.ID}: leverPosition={lever?.Value}, throttle={__instance.throttle * NumNotches}, targetThrottle={__instance.targetThrottle}, rounded={ActualThrottle(__instance.throttle) * NumNotches}");
                sim.throttle.SetValue(sim.engineOn ? ThrottleNotching.Power(__instance.targetThrottle) : 0.0f);
                return false;
            }
        }

        [HarmonyPatch(typeof(DieselLocoSimulation), nameof(DieselLocoSimulation.SimulateEngineRPM))]
        public static class SimulateEngineRPMPatch
        {
            public static bool Prefix(DieselLocoSimulation __instance)
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

    public static class ShunterThrottleNotchFix
    {
        [HarmonyPatch(typeof(LocoKeyboardInputShunter), nameof(LocoKeyboardInputShunter.TryApplyThrottleInput))]
        public static class TryApplyThrottleInputPatch
        {
            public static void Postfix(LocoKeyboardInputShunter __instance)
            {
                if (__instance.currentInputSpeedThrottle == 0.0f && __instance.throttleVelo == 0.0f)
                    __instance.setThrottleDelegate(ThrottleNotching.Notched(__instance.control.targetThrottle));
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
                sim.throttle.SetValue(sim.engineOn ? ThrottleNotching.Power(__instance.targetThrottle) : 0.0f);
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