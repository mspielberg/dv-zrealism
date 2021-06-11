using System;
using System.Linq;
using UnityModManagerNet;
using Formatter = System.Func<float, string>;
using Provider = System.Func<TrainCar, float?>;
using Pusher = System.Action<TrainCar, float>;

namespace DvMod.ZRealism
{
    internal sealed class HeadsUpDisplayBridge
    {
        public static HeadsUpDisplayBridge? instance;

        public static void Init()
        {
            // just here to force the static initializer to run
        }

        static HeadsUpDisplayBridge()
        {
            try
            {
                var hudMod = UnityModManager.FindMod("HeadsUpDisplay");
                if (hudMod?.Loaded != true)
                    return;
                instance = new HeadsUpDisplayBridge(hudMod);
            }
            catch (System.IO.FileNotFoundException)
            {
            }
        }

        private static readonly Type[] RegisterPullArgumentTypes = new Type[]
        {
            typeof(string),
            typeof(Provider),
            typeof(Formatter),
            typeof(IComparable)
        };

        private HeadsUpDisplayBridge(UnityModManager.ModEntry hudMod)
        {
            void RegisterPull(string label, Provider provider, Formatter formatter, IComparable? order = null)
            {
                hudMod.Invoke(
                    "DvMod.HeadsUpDisplay.Registry.RegisterPull",
                    out var _,
                    new object?[] { label, provider, formatter, order },
                    RegisterPullArgumentTypes);
            }

            RegisterPull(
                "Front coupler",
                car => car.frontCoupler.GetComponent<CouplerBreaker>()?.jointStress,
                v => $"{v / Main.settings.couplerStrength / 1e6:P0}");

            RegisterPull(
                "Rear coupler",
                car => car.rearCoupler.GetComponent<CouplerBreaker>()?.jointStress,
                v => $"{v / Main.settings.couplerStrength / 1e6:P0}");
        }
    }
}