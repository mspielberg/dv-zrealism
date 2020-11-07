using HarmonyLib;
using UnityEngine;

namespace DvMod.RealismFixes
{
    [HarmonyPatch(typeof(LocoControllerShunter), nameof(LocoControllerShunter.GetTractionForce))]
    public static class ShunterPowerPatch
    {
        public const float MaxPower = 392000;
        public static bool Prefix(LocoControllerShunter __instance, ref float __result)
        {
            __result = MaxPower * __instance.sim.engineRPM.value / Mathf.Max(1f, Mathf.Abs(__instance.GetForwardSpeed()));
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
            var car = TrainCar.Resolve(__instance.gameObject);
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
            Main.DebugLog(car, () => $"{car.ID}: RPM={__instance.engineRPM.value}, temp={__instance.engineTemp.value}, nextTemp={__instance.engineTemp.nextValue}, heating={heating}, airflow={airflow}, cooling={cooling}");

            return false;
        }
    }

    [HarmonyPatch(typeof(ShunterLocoSimulation), nameof(ShunterLocoSimulation.SimulateFuel))]
    public static class ShunterFuelPatch
    {
        public static bool Prefix(ShunterLocoSimulation __instance, float delta)
        {
            if (!__instance.engineOn || __instance.fuel.value <= 0.0)
                return false;
            float num = Mathf.Lerp(0.025f, 1f, __instance.engineRPM.value) * 15f *
                Main.settings.shunterFuelConsumptionMultiplier * delta;
            __instance.TotalFuelConsumed += num;
            __instance.fuel.AddNextValue(-num);
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
            __instance.oil.AddNextValue(-__instance.engineRPM.value * 0.3f *
                Main.settings.shunterFuelConsumptionMultiplier * delta);

            return false;
        }
    }
}