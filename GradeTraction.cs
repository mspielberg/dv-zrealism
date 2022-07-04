using System;

using System.Reflection;

using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace DvMod.ZRealism
{
    public static class GradeTraction
    {
        private static void ModifyFrictionCurve(TrainCar car)
        {
            if (car.GetComponent<DrivingForce>() is DrivingForce df && df != null)
                df.wheelslipToFrictionModifierCurve = AnimationCurve.EaseInOut(0.01f, 1f, 1f, 0f);
        }

        public static void AddCallbacks()
        {
            CarSpawner.CarSpawned += ModifyFrictionCurve;
        }

        public static void RemoveCallbacks()
        {
            CarSpawner.CarSpawned -= ModifyFrictionCurve;
        }

        [HarmonyPatch(typeof(DrivingForce), nameof(DrivingForce.UpdateWheelslip))]
        public static class UpdateWheelslipPatch
        {
            private static readonly bool proceduralSkyLoaded = UnityModManager.FindMod("ProceduralSkyMod")?.Active ?? false;
            private static MethodInfo? rainStrengthBlendGetter;

            private static float WeatherRelatedTractionModifier
            {
                get
                {
                    if (!proceduralSkyLoaded)
                        return Main.settings.dryFrictionCoefficient;
                    if (rainStrengthBlendGetter == null)
                    {
                        var weatherSourceType = AccessTools.TypeByName("ProceduralSkyMod.WeatherSource");
                        rainStrengthBlendGetter = AccessTools.PropertyGetter(weatherSourceType, "RainStrengthBlend");
                    }
                    try
                    {
                        return Mathf.Lerp(
                            Main.settings.dryFrictionCoefficient,
                            Main.settings.wetFrictionCoefficient,
                            (float)rainStrengthBlendGetter.Invoke(null, new object[0]));
                    }
                    catch (Exception)
                    {
                        return Main.settings.dryFrictionCoefficient;
                    }
                }
            }

            private const float PositionalFrequencyScale = 1000f;

            private static float PositionalRandomFactor(Bogie bogie)
            {
                static float CoordAdjust(float coord) =>
                    coord * Main.settings.positionalTractionFrequency / PositionalFrequencyScale;
                Vector3 position = (Vector3)bogie.point1.position;
                float modifier = (Mathf.PerlinNoise(CoordAdjust(position.x), CoordAdjust(position.z)) * 2f) - 1f;
                return 1.0f + (modifier * Main.settings.positionalTractionAmplitude);
            }

            public static bool Prefix(DrivingForce __instance, float inputForce, Bogie bogie, float maxTractionForcePossible)
            {
                if (__instance.preventWheelslip || bogie.HasDerailed || !bogie.enabled)
                {
                    __instance.wheelslip = 0f;
                    bogie.wheelslip = __instance.wheelslip;
                    return false;
                }
                TrainCar car = bogie.Car;
                float positionModifier = PositionalRandomFactor(bogie);
                float wheelslipModifier = Mathf.Clamp01(
                    __instance.wheelslipToFrictionModifierCurve.Evaluate(Mathf.Clamp01(__instance.wheelslip)));
                float weatherModifier = WeatherRelatedTractionModifier;
                __instance.frictionCoeficient = positionModifier * Mathf.Lerp(
                    wheelslipModifier * weatherModifier,
                    Main.settings.sandFrictionCoefficient,
                    __instance.sandCoef);
                float num = car.transform.localEulerAngles.x;
                float num2 = Mathf.Cos(Mathf.Deg2Rad * num);
                // assume total bogie mass is 1/2 of adhesive weight
                __instance.factorOfAdhesion = bogie.rb.mass * num2 * __instance.frictionCoeficient * 2;
                __instance.tractionForceWheelslipLimit = __instance.factorOfAdhesion * 9.8f;
                Main.DebugLog(car, () => $"positionMod={positionModifier},slipMod={wheelslipModifier:F2},weatherMod={weatherModifier:F2},sand={__instance.sandCoef:F2},frictionCoeff={__instance.frictionCoeficient:F2},angle={num:F2},normalRatio={num2:F2},wheelslipLimit={__instance.tractionForceWheelslipLimit}");
                float num3 = Mathf.Abs(inputForce) - bogie.brakingForce;
                __instance.wheelslip = Mathf.Clamp01((num3 - __instance.tractionForceWheelslipLimit) / Mathf.Abs(maxTractionForcePossible - __instance.tractionForceWheelslipLimit));
                bogie.wheelslip = __instance.wheelslip;

                return false;
            }
        }

        [HarmonyPatch(typeof(DrivingForce), nameof(DrivingForce.ApplySand))]
        public static class ApplySandPatch
        {
            public static bool Prefix(DrivingForce __instance, float sandFlow)
            {
                __instance.sandCoef = sandFlow;
                return false;
            }
        }
    }
}
