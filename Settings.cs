using UnityModManagerNet;

namespace DvMod.RealismFixes
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Force switches when running through junction")] public bool forceSwitchOnRunningThrough = false;
        [Draw("Damage when running through junction")] public float runningThroughDamage = 1f;

        [Draw("Diesel cooling")] public float dieselCoolingMultiplier = 1.0f;
        [Draw("Shunter cooling")] public float shunterTemperatureMultiplier = 0.5f;

        [Draw("Fuel consumption multiplier")] public float fuelConsumptionMultiplier = 4f;
        [Draw("Oil consumption multiplier")] public float oilConsumptionMultiplier = 0.1f;

        [Draw("Enable logging")] public bool enableLogging = false;
        public readonly string? version = Main.mod?.Info.Version;

        override public void Save(UnityModManager.ModEntry entry)
        {
            Save<Settings>(this, entry);
        }

        public void OnChange()
        {
        }
    }
}