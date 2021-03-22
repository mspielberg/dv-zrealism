using DV.CabControls;
using DV.CabControls.Spec;
using HarmonyLib;
using UnityEngine;

namespace DvMod.ZRealism
{
    internal static class ThrottleNotching
    {
        public const int NumNotches = 8;
        public static float GetNotch(float impreciseThrottle) =>
            Mathf.RoundToInt(impreciseThrottle * NumNotches);
        public static float Notched(float impreciseThrottle) =>
            GetNotch(impreciseThrottle) / NumNotches;
    }

    [HarmonyPatch(typeof(ControlsInstantiator), nameof(ControlsInstantiator.Spawn))]
    public static class ThrottleLeverNotchCountPatch
    {
        public static void Prefix(ControlSpec spec)
        {
            if (spec.name == "C throttle" && spec is Lever leverSpec)
            {
                leverSpec.invertDirection = true;
                leverSpec.notches = ThrottleNotching.NumNotches + 1; // +1 for notch "0" (idle position)
            }
        }
    }

    [HarmonyPatch(typeof(LocoControllerDiesel), nameof(LocoControllerDiesel.GetEngineRPMGauge))]
    public static class DieselRPMGaugeRangePatch
    {
        public static bool Prefix(LocoControllerDiesel __instance, ref float __result)
        {
            if (!__instance.sim.engineOn)
                return true;
            __result = Mathf.Lerp(275f / 10f, 835f / 10f, __instance.sim.engineRPM.value);
            return false;
            // Main.DebugLog(() => $"gauge: {TrainCar.Resolve(__instance.gameObject).carType} {__instance.name}: unclamped={__instance.unclamped}, min={__instance.minValue}, max={__instance.maxValue}, minAngle={__instance.minAngle}, maxAngle={__instance.maxAngle}");
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
                var sim = __instance.sim;
                sim.throttle.SetValue(sim.engineOn ? ThrottleNotching.Notched(__instance.targetThrottle) : 0.0f);
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
                var sim = __instance.sim;
                sim.throttle.SetValue(sim.engineOn ? ThrottleNotching.Notched(__instance.targetThrottle) : 0.0f);
                return false;
            }
        }
    }
}