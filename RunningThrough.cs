using DV.JObjectExtstensions;
using DV.ServicePenalty;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class RunningThrough
    {
        private const DebtType BrokenJunction = (DebtType)100;
        private const string BrokenJunctionMessage = "{0} has broken a misaligned junction!" +
            " To fix the junction pay the J-{1} fee at the Career Manager." +
            " See the ZRealism settings to adjust this feature.";

        public class BrokenJunctionDebt : DisplayableDebt
        {
            public override string ID => $"J-{key}";
            public override bool IsStaged => true;

            public override CarDebtData[] GetCarDebts() => new CarDebtData[0];
            public override DebtType GetDebtType() => BrokenJunction;
            public override float GetTotalPrice() => 1000;

            public readonly int key;

            public BrokenJunctionDebt(int key)
            {
                this.key = key;
            }

            public override void Pay()
            {
                base.Pay();
                SingletonBehaviour<CareerManagerDebtController>.Instance.UnregisterDebt(this);
            }
        }

        private static CareerManagerDebtController DebtController
        {
            get => SingletonBehaviour<CareerManagerDebtController>.Instance;
        }

        private static readonly Dictionary<Junction, int> _junctionKeys = new Dictionary<Junction, int>();
        private static int JunctionKey(Junction junction)
        {
            if (_junctionKeys.Count == 0)
            {
                foreach (var (j, i) in JunctionsSaveManager.OrderedJunctions.Select((x, i) => (x, i)))
                    _junctionKeys[j] = i;
            }
            return _junctionKeys[junction];
        }

        private static bool JunctionIsBroken(Junction junction)
        {
            var key = JunctionKey(junction);
            return DebtController.currentNonZeroPricedDebts.OfType<BrokenJunctionDebt>().Any(debt => debt.key == key);
        }

        private static void DamageJunction(TrainCar car, Junction junction)
        {
            if (Main.settings.playJunctionDamageSound)
            {
                var clip = car.GetComponentInChildren<CarCollisionSounds>().impactClips.Last();
                clip.Play(junction.transform.position, 1f, UnityEngine.Random.Range(0.95f, 1.05f), 0f, 1f, 500f, default(AudioSourceCurves), AudioManager.e ? AudioManager.e.collisionGroup : null);
                DV.CommsRadioController.PlayAudioFromCar(clip, car);
            }
            if (Main.settings.showBrokenJunctionMessage)
            {
                MessageBox.ShowMessage(
                    String.Format(BrokenJunctionMessage, car.ID, JunctionKey(junction)),
                    pauseGame: true,
                    delay: 3);
            }
            DebtController.RegisterDebt(new BrokenJunctionDebt(JunctionKey(junction)));
        }

        [HarmonyPatch(typeof(Bogie), nameof(Bogie.SwitchJunctionIfNeeded))]
        public static class SwitchJunctionIfNeededPatch
        {
            public static bool Prefix(Bogie __instance, RailTrack track, bool first)
            {
                var junction = first ? track.inJunction : track.outJunction;
                if (junction == null)
                    return false;

                var isBroken = JunctionIsBroken(junction);
                var branchIndex = junction.outBranches.FindIndex(b => b.track == track);
                // Main.DebugLog(() => $"branchIndex={branchIndex}, selectedBranch={junction.selectedBranch}");
                if (branchIndex < 0)
                {
                    // facing-point movement
                    if (isBroken && UnityEngine.Random.value < Main.settings.damagedJunctionFlipPercent / 100f)
                        junction.Switch(Junction.SwitchMode.NO_SOUND);
                    return false;
                }

                if (branchIndex == junction.selectedBranch)
                {
                    // trailing-point movement on correct branch
                    return false;
                }

                // trailing-point movement on incorrect branch
                if (!isBroken && UnityEngine.Random.value < Main.settings.runningThroughDamagePercent / 100f)
                    DamageJunction(__instance.Car, junction);
                if (Main.settings.forceSwitchOnRunningThrough)
                    junction.Switch(Junction.SwitchMode.FORCED);

                return false;
            }
        }

        [HarmonyPatch(typeof(Junction), nameof(Junction.Switch))]
        public static class SwitchPatch
        {
            public static bool Prefix(Junction __instance, Junction.SwitchMode mode)
            {
                return mode == Junction.SwitchMode.NO_SOUND || !JunctionIsBroken(__instance);
            }
        }

        private const string SaveKey = "DvMod.ZRealism.broken";

        [HarmonyPatch(typeof(JunctionsSaveManager), nameof(JunctionsSaveManager.GetJunctionsSaveData))]
        static public class GetJunctionSaveDataPatch
        {
            public static void Postfix(JObject __result)
            {
                var brokenJunctions = SingletonBehaviour<CareerManagerDebtController>.Instance
                    .currentNonZeroPricedDebts
                    .OfType<BrokenJunctionDebt>()
                    .Select(debt => debt.key)
                    .ToArray();
                __result.SetIntArray(SaveKey, brokenJunctions);
            }
        }

        [HarmonyPatch(typeof(JunctionsSaveManager), nameof(JunctionsSaveManager.Load))]
        static public class LoadPatch
        {
            public static void Postfix(JObject savedData)
            {
                if (savedData.TryGetValue(SaveKey, out var keys))
                {
                    foreach (var junctionKey in keys)
                        DebtController.RegisterDebt(new BrokenJunctionDebt((int)junctionKey));
                }
            }
        }
    }
}
