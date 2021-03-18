using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class ShunterFanAudio
    {
        [HarmonyPatch(typeof(LocoAudioShunter), nameof(LocoAudioShunter.SetupForCar))]
        public static class SetupForCarPatch
        {
            private static LayeredAudio GetLayeredAudio(LocoAudioShunter locoAudio)
            {
                var engineTransform = locoAudio.transform.Find("Engine");
                var coolingFanGO = engineTransform.Find("CoolingFan")?.gameObject;
                if (coolingFanGO == null)
                {
                    coolingFanGO = new GameObject("CoolingFan");
                    coolingFanGO.transform.parent = engineTransform;
                    coolingFanGO.transform.localPosition = Vector3.zero;
                    coolingFanGO.transform.localRotation = Quaternion.identity;

                    var source = coolingFanGO.AddComponent<AudioSource>();
                    source.loop = true;
                    source.clip = FileAudio.Load("Ventilation Fan drone 2.mp3");
                    source.spatialBlend = 1f;
                    source.pitch = 1f;

                    var layer = new LayeredAudio.Layer()
                    {
                        name = "CoolingFan",
                        volumeCurve = AnimationCurve.Linear(0, 0, 1, 2),
                        inertia = 2f,
                        inertialPitch = true,
                        source = source,
                    };

                    var layered = coolingFanGO.AddComponent<LayeredAudio>();
                    layered.layers = new LayeredAudio.Layer[] { layer };
                    layered.linearPitchLerp = true;
                    layered.minPitch = 0f;
                }
                return coolingFanGO.GetComponent<LayeredAudio>();
            }

            private static IEnumerator CoolingFanCoro(LocoAudioShunter locoAudio)
            {
                var layered = GetLayeredAudio(locoAudio);
                var controller = (LocoControllerShunter)locoAudio.locoController;
                while (true)
                {
                    yield return null;
                    layered.Set(controller.GetFan() ? 1f : 0f);
                }
            }

            public static void Postfix(LocoAudioShunter __instance)
            {
                __instance.StartCoroutine(CoolingFanCoro(__instance));
            }
        }
    }
}