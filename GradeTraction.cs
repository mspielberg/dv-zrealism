using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class GradeTraction
    {
        [HarmonyPatch(typeof(CarTypes), nameof(CarTypes.GetCarPrefab))]
        public static class GetCarPrefabPatch
        {
            public static void Postfix(ref GameObject __result)
            {
                if (__result?.GetComponent<DrivingForce>() is DrivingForce df && df != null)
                {
                    df.wheelslipToFrictionModifierCurve = AnimationCurve.EaseInOut(0, 0.25f, 1, 0);
                }
            }
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

/*
        public static class Foo
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var iter = instructions.GetEnumerator();
                Main.DebugLog(() => "Searching for Stloc.2");
                iter.MoveNext();
                while (iter.Current.opcode != OpCodes.Stloc_2)
                {
                    Main.DebugLog(() => iter.Current.ToString());
                    yield return iter.Current;
                    iter.MoveNext();
                }
                Main.DebugLog(() => "Found Stloc.2");
                var labels = iter.Current.labels;
                // iter now at: TOP = 1 - slopCoeficientMultiplier * <head of stack> / 90
                // skip to: num = TOP
                Main.DebugLog(() => "Searching for Stloc.3");
                while (iter.Current.opcode != OpCodes.Stloc_3)
                    iter.MoveNext();
                Main.DebugLog(() => "Found Stloc.3");

                // TOP = Cos(TOP * Deg2Rad)
                yield return new CodeInstruction(
                    OpCodes.Ldc_R4,
                    UnityEngine.Mathf.Deg2Rad)
                    {
                        labels = labels,
                    };
                yield return new CodeInstruction(OpCodes.Mul);
                yield return new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(UnityEngine.Mathf), nameof(UnityEngine.Mathf.Cos)));

                // emit: num = TOP
                yield return iter.Current;

                while (iter.MoveNext())
                    yield return iter.Current;
            }
        }
        */
    }
}