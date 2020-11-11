using HarmonyLib;
using UnityEngine;

namespace DvMod.RealismFixes
{
    public static class ShunterPower
    {
        public static float Power(float engineRPM) => Mathf.Pow(engineRPM, Main.settings.throttleGamma);
        public static float TargetRPM(float targetThrottle) => targetThrottle;

        [HarmonyPatch(typeof(ShunterLocoSimulation), nameof(ShunterLocoSimulation.SimulateEngineRPM))]
        public static class SimulateEngineRPMPatch
        {
            public static bool Prefix(ShunterLocoSimulation __instance)
            {
                var throttleVelo = __instance.throttleToTargetDiff.value;
                var nextRPM = Mathf.SmoothDamp(
                    current: __instance.engineRPM.value,
                    target: TargetRPM(__instance.throttle.value),
                    currentVelocity: ref throttleVelo,
                    smoothTime: 1f / Main.settings.shunterThrottleResponse,
                    maxSpeed: 1f);
                __instance.throttleToTargetDiff.SetNextValue(throttleVelo);
                __instance.engineRPM.SetNextValue(nextRPM);
                return false;
            }
        }

        [HarmonyPatch(typeof(LocoControllerShunter), nameof(LocoControllerShunter.GetTractionForce))]
        public static class GetTractionForcePatch
        {
            public const float MaxPower = 392 * 0.85f * 1000; // 392 kW prime mover, 85% transmission efficiency
            public static bool Prefix(LocoControllerShunter __instance, ref float __result)
            {
                __result = MaxPower * Power(__instance.sim.engineRPM.value) / Mathf.Max(1f, Mathf.Abs(__instance.GetForwardSpeed()));
                return false;
            }
        }

        [HarmonyPatch(typeof(ShunterLocoSimulation), nameof(ShunterLocoSimulation.SimulateEngineTemp))]
        public static class ShunterTemperaturePatch
        {
            public const float ThermostatTemp = 80f;
            public const float AmbientTemperature = 20f;
            public const float CoolingFanSpeedEquivalent = 50f;
            public const float NoRadiatorSpeedEquivalent = 1f;
            public static bool Prefix(ShunterLocoSimulation __instance, float delta)
            {
                var heating = Mathf.Lerp(0.025f, 1f, __instance.engineRPM.value) * 12f;
                if (__instance.engineOn)
                    __instance.engineTemp.AddNextValue(heating * delta);

                var thermostatOpen = __instance.engineTemp.value >= ThermostatTemp;
                var airflow =
                    !thermostatOpen ? NoRadiatorSpeedEquivalent :
                    __instance.GetComponent<LocoControllerShunter>().GetFan() && __instance.speed.value < CoolingFanSpeedEquivalent ? CoolingFanSpeedEquivalent :
                    __instance.speed.value;
                var temperatureDelta = __instance.engineTemp.value - AmbientTemperature;
                var cooling = airflow / CoolingFanSpeedEquivalent * temperatureDelta * Main.settings.shunterTemperatureMultiplier;
                __instance.engineTemp.AddNextValue(-cooling * delta);
                //Main.DebugLog(car, () => $"{car.ID}: RPM={__instance.engineRPM.value}, temp={__instance.engineTemp.value}, nextTemp={__instance.engineTemp.nextValue}, heating={heating}, airflow={airflow}, cooling={cooling}");

                return false;
            }
        }
    }

    [HarmonyPatch(typeof(ShunterLocoSimulation), nameof(ShunterLocoSimulation.SimulateFuel))]
    public static class ShunterFuelPatch
    {
        private const float DieselEnergyContent = 36e6f; /* J/L */
        private const float ThermalEfficiency = 0.30f;
        public static float DieselFuelUsage(float energyJ) => energyJ / DieselEnergyContent / ThermalEfficiency;

        public static bool Prefix(ShunterLocoSimulation __instance, float delta)
        {
            if (!__instance.engineOn)
                return false;
            var motivePower = __instance.GetComponent<LocoControllerShunter>().reverser == 0f
                ? 0f
                : ShunterPower.Power(__instance.engineRPM.value);
            // 30 kW to run accessories
            var accessoryPower = (0.1f + __instance.engineRPM.value) * 30e3f;
            var fuelUsage = DieselFuelUsage(motivePower + accessoryPower) * Main.settings.fuelConsumptionMultiplier * delta;
            __instance.TotalFuelConsumed += fuelUsage;
            __instance.fuel.AddNextValue(-fuelUsage);
            return false;
        }
    }

    [HarmonyPatch(typeof(ShunterLocoSimulation), nameof(ShunterLocoSimulation.SimulateOil))]
    public static class ShunterOilPatch
    {
        public static bool Prefix(ShunterLocoSimulation __instance, float delta)
        {
            if (__instance.engineRPM.value <= 0.0 || __instance.oil.value <= 0.0)
                return false;
            var oilUsage = __instance.engineRPM.value * Main.settings.oilConsumptionMultiplier * delta;
            __instance.oil.AddNextValue(-oilUsage);

            return false;
        }
    }
}