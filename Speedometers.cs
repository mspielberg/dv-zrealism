using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class Speedometers
    {
        private static float GetWheelSpeedKmH(LocoControllerBase loco)
        {
            if (loco.GetComponent<WheelRotationViaAnimation>() is var wrva && wrva != null)
            {
                return Mathf.Abs(wrva.rotationSpeed * wrva.wheelCircumference * 3.6f);
            }
            else if (loco.GetComponent<LocoWheelRotationViaCode>() is var lwrvc && lwrvc != null)
            {
                var drivingForce = loco.drivingForce;
                var revSpeed = drivingForce.wheelslip > 0f ? Mathf.Lerp(lwrvc.curRevSpeed, lwrvc.WHEELSLIP_SPEED_MAX, drivingForce.wheelslip) : lwrvc.curRevSpeed;
                return Mathf.Abs(revSpeed * lwrvc.wheelCircumference * 3.6f);
            }
            return default;
        }

        private static float GetSpeedometerSpeed(LocoControllerBase loco)
        {
            return Mathf.Max(Mathf.Abs(loco.GetSpeedKmH()), GetWheelSpeedKmH(loco));
        }

        [HarmonyPatch]
        public static class IndicatorsRemoveSpeedometerUpdatePatch
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(IndicatorsDiesel), nameof(IndicatorsDiesel.Update));
                yield return AccessTools.Method(typeof(IndicatorsShunter), nameof(IndicatorsShunter.Update));
                yield return AccessTools.Method(typeof(IndicatorsSteam), nameof(IndicatorsSteam.Update));
            }

            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> insts = new List<CodeInstruction>(instructions);
                var index = insts.FindIndex(inst => inst.opcode == OpCodes.Ldfld && inst.operand is FieldInfo field && field.Name == "speed");
                if (index >= 1)
                    insts.RemoveRange(index - 1, 6);
                return insts;
            }
        }

        private static IEnumerator UpdateSpeedometerCoro(Indicator speedometer, LocoControllerBase controller)
        {
            float lastUpdate = 0f;
            float needlePosition = 0f;
            float needleTarget = 0f;
            float velo = 0f;
            while (true)
            {
                yield return null;
                if (Time.time - lastUpdate >= Main.settings.speedometerUpdatePeriod)
                {
                    lastUpdate = Time.time;
                    needleTarget = GetSpeedometerSpeed(controller);
                }
                speedometer.value = needlePosition = Mathf.SmoothDamp(needlePosition, needleTarget, ref velo, Main.settings.speedometerSmoothing);
            }
        }

        private static void SetupSpeedometerTicking(Indicator speedometer)
        {
            if (Main.settings.speedometerVolume == 0)
            {
                Component.Destroy(speedometer.GetComponent<AudioSource>());
            }

            var source = speedometer.GetComponent<AudioSource>();
            if (source == null)
            {
                source = speedometer.gameObject.AddComponent<AudioSource>();
                source.loop = true;
                source.clip = FileAudio.Load("clock-ticking-5.ogg");
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 0.1f;
                source.maxDistance = 3f;
                source.Play();
            }

            source.volume = Main.settings.speedometerVolume;
        }

        [HarmonyPatch(typeof(IndicatorsDiesel), nameof(IndicatorsDiesel.Start))]
        public static class IndicatorsDieselStartPatch
        {
            public static void Postfix(IndicatorsDiesel __instance)
            {
                SetupSpeedometerTicking(__instance.speed);
                __instance.StartCoroutine(UpdateSpeedometerCoro(__instance.speed, __instance.ctrl));
            }
        }

        [HarmonyPatch(typeof(IndicatorsShunter), nameof(IndicatorsShunter.Start))]
        public static class IndicatorsShunterStartPatch
        {
            public static void Postfix(IndicatorsShunter __instance)
            {
                SetupSpeedometerTicking(__instance.speed);
                __instance.StartCoroutine(UpdateSpeedometerCoro(__instance.speed, __instance.ctrl));
            }
        }

        [HarmonyPatch(typeof(IndicatorsSteam), nameof(IndicatorsSteam.Start))]
        public static class IndicatorsSteamStartPatch
        {
            public static void Postfix(IndicatorsSteam __instance)
            {
                SetupSpeedometerTicking(__instance.speed);
                __instance.StartCoroutine(UpdateSpeedometerCoro(__instance.speed, __instance.ctrl));
            }
        }

        public static void OnSettingsChanged()
        {
            foreach (var indicators in Component.FindObjectsOfType<IndicatorsDiesel>())
                SetupSpeedometerTicking(indicators.speed);
            foreach (var indicators in Component.FindObjectsOfType<IndicatorsShunter>())
                SetupSpeedometerTicking(indicators.speed);
            foreach (var indicators in Component.FindObjectsOfType<IndicatorsSteam>())
                SetupSpeedometerTicking(indicators.speed);
        }
    }
}