using DV;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace DvMod.RealismFixes
{
    public static class DieselPower
    {
        private const float ThrottleGamma = 1.4f;
        public const int NumNotches = 8;
        public const float EngineMaxPower = 1_300_000; // 1.3 kW prime mover
        public const float TransmissionEfficiency = 0.85f;
        public const float MinPower = 1f / NumNotches;
        public static float OutputPower(float engineRPM) =>
            Mathf.Pow(((engineRPM * 7f) + 1) / 8f, ThrottleGamma);
        // Notches 0 and 1 are idle, so 7 distinct RPM settings possible
        public static float TargetRPM(float targetThrottle) =>
            Mathf.Max(0f, (ThrottleNotching.GetNotch(targetThrottle) - 1) / (NumNotches - 1));

        public static float RawPowerInWatts(DieselLocoSimulation sim)
        {
            var atIdle = sim.GetComponent<LocoControllerDiesel>().reverser == 0f || sim.throttle.value == 0;
            var motivePower = atIdle ? 0f : EngineMaxPower * OutputPower(sim.engineRPM.value);
            // 100 kW to run accessories
            var accessoryPower = (0.1f + sim.engineRPM.value) * 100e3f;
            return motivePower + accessoryPower;
        }

        private enum TransitionState
        {
            ToLow,
            Low,
            ToHigh,
            High,
        }

        private const float TransitionUpSpeed = 40f;
        private const float TransitionDownSpeed = 30f;
        private const float TransitionDuration = 2f;

        private class ExtraState
        {
            private readonly DieselLocoSimulation sim;
            public float tractiveEffort;
            public float tractiveEffortVelo;
            public TransitionState transitionState;
            public float transitionStartTime;

            public int runningFans = 0;

            public ExtraState(DieselLocoSimulation sim)
            {
                this.sim = sim;
            }

            public bool InTransition() => transitionState == TransitionState.ToHigh || transitionState == TransitionState.ToLow;

            public void CheckTransition()
            {
                var speedKph = sim.speed.value;
                var car = TrainCar.Resolve(sim.gameObject);
                switch (transitionState)
                {
                    case TransitionState.ToLow:
                        if (Time.time > transitionStartTime + TransitionDuration)
                        {
                            // Main.DebugLog(car, () => "Completing transition to low");
                            transitionState = TransitionState.Low;
                        }
                        break;
                    case TransitionState.Low:
                        if (speedKph > TransitionUpSpeed && Random.value < 0.01f)
                        {
                            // Main.DebugLog(car, () => "Starting transition to high");
                            transitionState = TransitionState.ToHigh;
                            transitionStartTime = Time.time;
                        }
                        break;
                    case TransitionState.ToHigh:
                        if (Time.time > transitionStartTime + TransitionDuration)
                        {
                            // Main.DebugLog(car, () => "Completing transition to high");
                            transitionState = TransitionState.High;
                        }
                        break;
                    case TransitionState.High:
                        if (speedKph < TransitionDownSpeed && Random.value < 0.01f)
                        {
                            // Main.DebugLog(car, () => "Starting transition to low");
                            transitionState = TransitionState.ToLow;
                            transitionStartTime = Time.time;
                        }
                        break;
                }
            }

            private static readonly Dictionary<DieselLocoSimulation, ExtraState> states =
                new Dictionary<DieselLocoSimulation, ExtraState>();

            public static ExtraState Instance(DieselLocoSimulation sim)
            {
                if (!states.TryGetValue(sim, out var state))
                    states[sim] = state = new ExtraState(sim);
                return state;
            }
        }

        [HarmonyPatch(typeof(DieselLocoSimulation), nameof(DieselLocoSimulation.SimulateEngineRPM))]
        public static class SimulateEngineRPMPatch
        {
            public static bool Prefix(DieselLocoSimulation __instance)
            {
                // var loco = TrainCar.Resolve(__instance.gameObject);
                if (PausePhysicsHandler.PhysicsHandlingInProcess)
                    return false;

                var throttleVelo = __instance.throttleToTargetDiff.value;
                var nextRPM = Mathf.SmoothDamp(
                    current: __instance.engineRPM.value,
                    target: TargetRPM(__instance.throttle.value),
                    currentVelocity: ref throttleVelo,
                    smoothTime: 1f,
                    maxSpeed: 1f);
                __instance.throttleToTargetDiff.SetNextValue(throttleVelo);
                __instance.engineRPM.SetNextValue(nextRPM);
                // Main.DebugLog(loco, $"{loco.ID}: throttle={__instance.throttle.value}, RPM={__instance.engineRPM.value}, velo={throttleVelo}");

                var state = ExtraState.Instance(__instance);
                state.CheckTransition();
                var speedMetersPerSecond = __instance.speed.value / 3.6f;
                var powerOutput = (state.InTransition() || __instance.throttle.value == 0f) ? 0f :
                    EngineMaxPower * TransmissionEfficiency * OutputPower(__instance.engineRPM.value);
                var target = powerOutput / Mathf.Max(1f, speedMetersPerSecond);
                state.tractiveEffort = Mathf.SmoothDamp(
                    state.tractiveEffort,
                    target,
                    ref state.tractiveEffortVelo,
                    0.5f);

                // Main.DebugLog(loco, () => $"engineRPM={__instance.engineRPM.value}, speed={speedMetersPerSecond}, target={target}, TE={state.tractiveEffort}");

                return false;
            }
        }

        [HarmonyPatch(typeof(LocoControllerDiesel), nameof(LocoControllerDiesel.GetTractionForce))]
        public static class DieselPowerPatch
        {
            public static bool Prefix(LocoControllerDiesel __instance, ref float __result)
            {
                var state = ExtraState.Instance(__instance.sim);
                __result = state.tractiveEffort;
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

        [HarmonyPatch(typeof(DieselLocoSimulation), nameof(DieselLocoSimulation.SimulateEngineTemp))]
        public static class SimulateEngineTempPatch
        {
            private const float CoolantCapacity = 977f; // in L; http://www.rr-fallenflags.org/manual/sd18-1.pdf p1
            private const float WaterHeatCapacity = 3.9f; // in kJ/kg @ ~75 C; https://www.engineeringtoolbox.com/specific-heat-capacity-water-d_660.html
            private const float CoolingAirflow = 0.005f;
            private const float AmbientTemperature = 20f;

            // http://www.rr-fallenflags.org/manual/sd18-4.pdf p45
            private const float RadiatorFan1OnThreshold = 77f;
            private const float RadiatorFan1OffThreshold = 68f;
            private const float RadiatorFan2OnThreshold = 82;
            private const float RadiatorFan2OffThreshold = 74;

            private static void SimulateFanThermostats(float engineTemp, ExtraState state)
            {
                switch (state.runningFans)
                {
                case 0:
                    if (engineTemp > RadiatorFan1OnThreshold)
                        state.runningFans = 1;
                    break;
                case 1:
                    if (engineTemp < RadiatorFan1OffThreshold)
                        state.runningFans = 0;
                    else if (engineTemp > RadiatorFan2OnThreshold)
                        state.runningFans = 2;
                    break;
                case 2:
                    if (engineTemp < RadiatorFan2OffThreshold)
                        state.runningFans = 1;
                    break;
                }
            }

            public static bool Prefix(DieselLocoSimulation __instance, float delta)
            {
                var engineTemp = __instance.engineTemp.value;
                var state = ExtraState.Instance(__instance);
                SimulateFanThermostats(engineTemp, state);

                var energyInKJ = RawPowerInWatts(__instance) * delta / __instance.timeMult / 1000;
                var heating = energyInKJ / WaterHeatCapacity / CoolantCapacity;
                if (__instance.engineOn)
                    __instance.engineTemp.AddNextValue(heating);

                var airflowFraction = state.runningFans == 1 ? 0.75f : state.runningFans == 2 ? 1f : 0.01f;
                var airflow = airflowFraction * CoolingAirflow;
                var temperatureDelta = __instance.engineTemp.value - AmbientTemperature;
                var cooling = airflow * temperatureDelta * Main.settings.dieselCoolingMultiplier * delta / __instance.timeMult;
                __instance.engineTemp.AddNextValue(-cooling);

                // Main.DebugLog(TrainCar.Resolve(__instance.gameObject), () => $"energy={energyInKJ}, heating={heating}, cooling={cooling}, engineTemp={engineTemp}, nextTemp={__instance.engineTemp.nextValue}, runningFans={state.runningFans}");

                return false;
            }
        }

        private const float DieselEnergyContent = 36e6f; /* J/L */
        private const float ThermalEfficiency = 0.30f;
        /// <summary>Returns fuel usage in L/s.</summary>
        public static float DieselFuelUsage(float energyJ) => energyJ / DieselEnergyContent / ThermalEfficiency;

        [HarmonyPatch(typeof(DieselLocoSimulation), nameof(DieselLocoSimulation.SimulateFuel))]
        public static class SimulateFuelPatch
        {
            public static bool Prefix(DieselLocoSimulation __instance, float delta)
            {
                if (!__instance.engineOn)
                    return false;

                var fuelUsage = DieselFuelUsage(RawPowerInWatts(__instance)) * Main.settings.fuelConsumptionMultiplier *
                    delta / __instance.timeMult;
                __instance.TotalFuelConsumed += fuelUsage;
                __instance.fuel.AddNextValue(-fuelUsage);
                // Main.DebugLog(TrainCar.Resolve(__instance.gameObject), () => $"fuel={__instance.fuel.value} / {__instance.fuel.max}, fuelConsumption={fuelUsage / (delta / __instance.timeMult) * 3600} Lph, timeToExhaust={__instance.fuel.value/(fuelUsage/(delta/__instance.timeMult))} s");
                return false;
            }
        }

        [HarmonyPatch(typeof(DieselLocoSimulation), nameof(DieselLocoSimulation.SimulateOil))]
        public static class DieselOilPatch
        {
            public const float OilConsumption = 1e-9f;
            public static bool Prefix(DieselLocoSimulation __instance, float delta)
            {
                if (__instance.oil.value <= 0)
                    __instance.oil.value = __instance.oil.nextValue = __instance.oil.max * (__instance.fuel.value / __instance.fuel.max);
                if (__instance.engineRPM.value <= 0.0 || __instance.oil.value <= 0.0)
                    return false;
                var oilUsage = RawPowerInWatts(__instance) * OilConsumption * Main.settings.oilConsumptionMultiplier * delta / __instance.timeMult;
                __instance.oil.AddNextValue(-oilUsage);
                // Main.DebugLog(TrainCar.Resolve(__instance.gameObject), () => $"oil={__instance.oil.value} / {__instance.oil.max}, oilConsumption={oilUsage / (delta / __instance.timeMult) * 3600} Lph, timeToExhaust={__instance.oil.value/(oilUsage/(delta/__instance.timeMult))} s");

                return false;
            }
        }

        public static class DieselSanding
        {
            public const float SandCapacity = 2000f; // in kg; http://www.rr-fallenflags.org/manual/sd18-1.pdf p1
            public const float SandingRate = 1f / 30f; // in kg/s; https://www.knorr-bremse.com/remote/media/documents/railvehicles/en/en_neu_2010/Sanding_systems.pdf

            [HarmonyPatch(typeof(DieselLocoSimulation), nameof(DieselLocoSimulation.InitComponents))]
            public static class InitComponentsPatch
            {
                public static void Postfix(DieselLocoSimulation __instance)
                {
                    __instance.sand.max = __instance.sand.value = SandCapacity;
                }
            }

            [HarmonyPatch(typeof(DieselLocoSimulation), nameof(DieselLocoSimulation.SimulateSand))]
            public static class SimulateSandPatch
            {
                public static bool Prefix(DieselLocoSimulation __instance, float delta)
                {
                    var sand = __instance.sand;
                    var sandFlow = __instance.sandFlow;
                    if (__instance.sandOn && sand.value > 0.0 && sandFlow.value < sandFlow.max ||
                    (!__instance.sandOn || sand.value == 0.0) && sandFlow.value > sandFlow.min)
                        sandFlow.AddNextValue((!__instance.sandOn || sand.value <= 0.0 ? -1f : 1f) * 10f * delta);
                    if (sandFlow.value <= 0.0 || sand.value <= 0.0)
                        return false;
                    sand.AddNextValue(-sandFlow.value * SandingRate * delta / __instance.timeMult);
                    return false;
                }
            }

            [HarmonyPatch(typeof(IndicatorsDiesel), nameof(IndicatorsDiesel.Start))]
            public static class StartPatch
            {
                public static void Postfix(IndicatorsDiesel __instance)
                {
                    __instance.sand.maxValue = SandCapacity;
                }
            }
        }
    }
}