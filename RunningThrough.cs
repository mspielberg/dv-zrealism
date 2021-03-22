using DV.JObjectExtstensions;
using DV.ServicePenalty;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class RunningThrough
    {
        private const DebtType BrokenJunction = (DebtType)100;

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
            if (!_junctionKeys.TryGetValue(junction, out var key))
                _junctionKeys[junction] = key = System.Array.IndexOf(JunctionsSaveManager.OrderedJunctions, junction);
            return key;
        }

        private static bool JunctionIsBroken(Junction junction)
        {
            var key = JunctionKey(junction);
            return DebtController.currentNonZeroPricedDebts.OfType<BrokenJunctionDebt>().Any(debt => debt.key == key);
        }

        private static void DamageJunction(TrainCar car, Junction junction)
        {
            if (Main.settings.playJunctionDamageSound)
                car.GetComponentInChildren<TrainDerailAudio>().PlayDerailAudio(car);
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
                    if (isBroken && Random.value < Main.settings.damagedJunctionDerailPercent / 100f)
                        __instance.Derail("Passing over broken junction");
                    return false;
                }

                if (branchIndex == junction.selectedBranch)
                {
                    // trailing-point movement on correct branch
                    return false;
                }

                // trailing-point movement on incorrect branch
                if (!isBroken && Random.value < Main.settings.runningThroughDamagePercent / 100f)
                    DamageJunction(__instance.Car, junction);
                if (Main.settings.forceSwitchOnRunningThrough)
                    junction.Switch(Junction.SwitchMode.FORCED);

                return false;
            }
        }

        [HarmonyPatch(typeof(Junction), nameof(Junction.Switch))]
        public static class SwitchPatch
        {
            public static bool Prefix(Junction __instance)
            {
                return !JunctionIsBroken(__instance);
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
                foreach (var junctionKey in savedData[SaveKey])
                    DebtController.RegisterDebt(new BrokenJunctionDebt((int)junctionKey));
            }
        }
    }
}