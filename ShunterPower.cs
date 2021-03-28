using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class ShunterPower
    {
        private const float ThrottleGamma = 1.4f;
        public const float EngineMaxPower = 392_000; // 392 kW prime mover
        public const float TransmissionEfficiency = 0.85f;

        private class ExtraState
        {
            private const float FanOnThreshold = 87f;
            private const float FanOffThreshold = 82f;

            private readonly LocoControllerShunter controller;
            public bool fanRunning = false;

            public ExtraState(LocoControllerShunter controller)
            {
                this.controller = controller;
            }

            public bool UpdateFan()
            {
                var temperature = controller.sim.engineTemp.value;
                if (controller.GetFan())
                    fanRunning = true;
                else if (temperature > FanOnThreshold)
                    fanRunning = true;
                else if (temperature < FanOffThreshold)
                    fanRunning = false;
                return fanRunning;
            }

            private static readonly Dictionary<ShunterLocoSimulation, ExtraState> states =
                new Dictionary<ShunterLocoSimulation, ExtraState>();

            public static ExtraState Instance(ShunterLocoSimulation sim)
            {
                if (!states.TryGetValue(sim, out var state))
                    states[sim] = state = new ExtraState(sim.GetComponent<LocoControllerShunter>());
                return state;
            }
        }

        public static bool IsFanRunning(ShunterLocoSimulation sim)
        {
            return ExtraState.Instance(sim).fanRunning;
        }

        public static float CrankshaftPower(ShunterLocoSimulation sim)
        {
            var atIdle = sim.GetComponent<LocoControllerShunter>().reverser == 0f || sim.throttle.value == 0;
            var power = atIdle ? 0f : EngineMaxPower * Mathf.Pow(sim.engineRPM.value, ThrottleGamma);
            return power;
        }
        public static float TargetRPM(float targetThrottle) => targetThrottle;

        public static float RawPowerInWatts(ShunterLocoSimulation sim)
        {
            // 30 kW to run accessories
            var accessoryPower = (0.1f + sim.engineRPM.value) * 30e3f;
            return CrankshaftPower(sim) + accessoryPower;
        }

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
                    smoothTime: 1f,
                    maxSpeed: 1f);
                __instance.throttleToTargetDiff.SetNextValue(throttleVelo);
                __instance.engineRPM.SetNextValue(nextRPM);
                return false;
            }
        }

        [HarmonyPatch(typeof(LocoControllerShunter), nameof(LocoControllerShunter.GetTractionForce))]
        public static class GetTractionForcePatch
        {
            public static bool Prefix(LocoControllerShunter __instance, ref float __result)
            {
                __result = TransmissionEfficiency * CrankshaftPower(__instance.sim) / Mathf.Max(1f, Mathf.Abs(__instance.GetForwardSpeed()));
                return false;
            }
        }

        [HarmonyPatch(typeof(ShunterLocoSimulation), nameof(ShunterLocoSimulation.SimulateEngineTemp))]
        public static class ShunterTemperaturePatch
        {
            public const float ThermostatTemp = 80f;
            public const float AmbientTemperature = 20f;
            public const float CoolingFanSpeedEquivalent = 50f;
            public const float NoRadiatorSpeedEquivalent = 5f;
            public static bool Prefix(ShunterLocoSimulation __instance, float delta)
            {
                var heating = Mathf.Lerp(0.025f, 1f, __instance.engineRPM.value) * 12f;
                if (__instance.engineOn)
                    __instance.engineTemp.AddNextValue(heating * delta);

                var fanRunning = ExtraState.Instance(__instance).UpdateFan();
                var thermostatOpen = __instance.engineTemp.value >= ThermostatTemp;
                var airflow =
                    !thermostatOpen ? NoRadiatorSpeedEquivalent :
                    fanRunning && __instance.speed.value < CoolingFanSpeedEquivalent ? CoolingFanSpeedEquivalent :
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
            var fuelUsage = DieselFuelUsage(ShunterPower.RawPowerInWatts(__instance)) * Main.settings.shunterFuelConsumptionMultiplier *
                (delta / __instance.timeMult);
            __instance.TotalFuelConsumed += fuelUsage;
            __instance.fuel.AddNextValue(-fuelUsage);
            // Main.DebugLog(TrainCar.Resolve(__instance.gameObject), () => $"fuel={__instance.fuel.value} / {__instance.fuel.max}, fuelConsumption={fuelUsage / (delta / __instance.timeMult) * 3600} Lph, timeToExhaust={__instance.fuel.value/(fuelUsage/(delta/__instance.timeMult))} s");
            return false;
        }
    }

    [HarmonyPatch(typeof(ShunterLocoSimulation), nameof(ShunterLocoSimulation.SimulateOil))]
    public static class ShunterOilPatch
    {
        public const float OilConsumption = 1e-10f;
        public static bool Prefix(ShunterLocoSimulation __instance, float delta)
        {
            if (__instance.engineRPM.value <= 0.0 || __instance.oil.value <= 0.0)
                return false;
            var oilUsage = ShunterPower.RawPowerInWatts(__instance) * OilConsumption * Main.settings.shunterOilConsumptionMultiplier * delta / __instance.timeMult;
            __instance.oil.AddNextValue(-oilUsage);
            // Main.DebugLog(TrainCar.Resolve(__instance.gameObject), () => $"oil={__instance.oil.value} / {__instance.oil.max}, oilConsumption={oilUsage / (delta / __instance.timeMult) * 3600} Lph, timeToExhaust={__instance.oil.value/(oilUsage/(delta/__instance.timeMult))} s");

            return false;
        }
    }

    public static class ShunterSanding
    {
        public const float SandingRate = 0.02f * 4f; // in .001 m^3/s; https://www.knorr-bremse.com/remote/media/documents/railvehicles/en/en_neu_2010/Sanding_systems.pdf

        [HarmonyPatch(typeof(ShunterLocoSimulation), nameof(ShunterLocoSimulation.SimulateSand))]
        public static class SimulateSandPatch
        {
            public static bool Prefix(DieselLocoSimulation __instance, float delta)
            {
                var sand = __instance.sand;
                var sandFlow = __instance.sandFlow;
                if ((__instance.sandOn && sand.value > 0.0 && sandFlow.value < sandFlow.max) ||
                ((!__instance.sandOn || sand.value == 0.0) && sandFlow.value > sandFlow.min))
                {
                    sandFlow.AddNextValue((!__instance.sandOn || sand.value <= 0.0 ? -1f : 1f) * 10f * delta);
                }

                if (sandFlow.value <= 0.0 || sand.value <= 0.0)
                    return false;
                sand.AddNextValue(-sandFlow.value * SandingRate * delta / __instance.timeMult);
                return false;
            }
        }
    }
}