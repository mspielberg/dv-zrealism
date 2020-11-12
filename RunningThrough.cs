using HarmonyLib;

namespace DvMod.RealismFixes
{
    [HarmonyPatch(typeof(Bogie), nameof(Bogie.SwitchJunctionIfNeeded))]
    static public class SwitchJunctionIfNeededPatch
    {
        private static void DamageCar(TrainCar car)
        {
            Main.DebugLog(() => $"Bogie on {car.ID} running through junction");
            if (car.IsLoco)
            {
                var damageController = car.GetComponent<DamageController>();
                // Main.DebugLog(() => $"Applying {Main.settings.runningThroughDamagePercent} damage to loco wheels");
                damageController.ApplyDamage(damageController.wheels, damageController.wheels.fullHitPoints * Main.settings.runningThroughDamagePercent / 100);
            }
            else
            {
                // Main.DebugLog(() => $"Applying {Main.settings.runningThroughDamagePercent} damage to car, healthBefore={car.CarDamage.currentHealth}");
                car.CarDamage.DamageCar(car.CarDamage.maxHealth * Main.settings.runningThroughDamagePercent / 100);
                // Main.DebugLog(() => $"healthAfter={car.CarDamage.currentHealth}");
            }
        }

        public static bool Prefix(Bogie __instance, RailTrack track, bool first)
        {
            var junction = first ? track.inJunction : track.outJunction;
            if (junction == null)
                return false;
            var branchIndex = junction.outBranches.FindIndex(b => b.track == track);
            // Main.DebugLog(() => $"branchIndex={branchIndex}, selectedBranch={junction.selectedBranch}");
            if (branchIndex < 0 || branchIndex == junction.selectedBranch)
                return false;

            if (Main.settings.runningThroughDamagePercent > 0)
                DamageCar(__instance.Car);
            if (Main.settings.forceSwitchOnRunningThrough)
                junction.Switch(Junction.SwitchMode.FORCED);

            return false;
        }
    }
}