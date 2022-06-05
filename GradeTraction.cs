using HarmonyLib;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class GradeTraction
    {
        private static void ModifyFrictionCurve(TrainCar car)
        {
            if (car.GetComponent<DrivingForce>() is DrivingForce df && df != null)
                df.wheelslipToFrictionModifierCurve = AnimationCurve.EaseInOut(0, 0.25f, 1, 0);
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
            public static bool Prefix(DrivingForce __instance, float inputForce, Bogie bogie, float maxTractionForcePossible)
            {
                if (__instance.preventWheelslip || bogie.HasDerailed || !bogie.enabled)
                {
                    __instance.wheelslip = 0f;
                    bogie.wheelslip = __instance.wheelslip;
                    return false;
                }
                TrainCar car = bogie.Car;
                bool flag = Mathf.Sign(car.GetForwardSpeed()) != Mathf.Sign(inputForce);
                __instance.frictionCoeficient = Mathf.Clamp01(__instance.wheelslipToFrictionModifierCurve.Evaluate(Mathf.Clamp01(__instance.wheelslip)) * ((flag && Mathf.Abs(car.GetForwardSpeed()) > 1f) ? 1f : __instance.sandCoef));
                float num = car.transform.localEulerAngles.x;
                float num2 = Mathf.Cos(Mathf.Deg2Rad * num);
                // assume total bogie mass is 1/2 of adhesive weight
                __instance.factorOfAdhesion = bogie.rb.mass * num2 * __instance.frictionCoeficient * 2;
                __instance.tractionForceWheelslipLimit = __instance.factorOfAdhesion * 9.8f;
                // Main.DebugLog(car, () => $"frictionCoeff={__instance.frictionCoeficient},angle={num},normalRatio={num2},wheelslipLimit={__instance.tractionForceWheelslipLimit}");
                float num3 = Mathf.Abs(inputForce) - bogie.brakingForce;
                __instance.wheelslip = Mathf.Clamp01((num3 - __instance.tractionForceWheelslipLimit) / Mathf.Abs(maxTractionForcePossible - __instance.tractionForceWheelslipLimit));
                bogie.wheelslip = __instance.wheelslip;

                return false;
            }
        }
    }
}