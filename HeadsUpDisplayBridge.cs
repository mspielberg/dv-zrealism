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
                if (hudMod?.Active != true)
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
            typeof(IComparable),
            typeof(bool),
        };

        private HeadsUpDisplayBridge(UnityModManager.ModEntry hudMod)
        {
            void RegisterPull(string label, Provider provider, Formatter formatter, IComparable? order = null, bool hidden = false)
            {
                hudMod.Invoke(
                    "DvMod.HeadsUpDisplay.Registry.RegisterPull",
                    out var _,
                    new object?[] { label, provider, formatter, order, hidden },
                    RegisterPullArgumentTypes);
            }

            RegisterPull(
                "Fuel consumption",
                car =>
                car.carType switch
                {
                    TrainCarType.LocoDiesel =>
                        DieselPower.DieselFuelUsage(DieselPower.RawPowerInWatts(car.GetComponentInChildren<DieselLocoSimulation>())),
                    TrainCarType.LocoShunter =>
                        ShunterFuelPatch.DieselFuelUsage(ShunterPower.RawPowerInWatts(car.GetComponentInChildren<ShunterLocoSimulation>())),
                    _ =>
                         null,
                },
                v => $"{v * 3600f:F0} L/h"
            );
        }
    }
}
