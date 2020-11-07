using UnityModManagerNet;

namespace DvMod.RealismFixes
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Force switches when running through junction")] public bool forceSwitchOnRunningThrough = false;
        [Draw("Damage when running through junction")] public float runningThroughDamage = 1f;

        [Draw("Shunter cooling")] public float shunterTemperatureMultiplier = 0.5f;
        [Draw("Shunter fuel consumption")] public float shunterFuelConsumptionMultiplier = 0.1f;

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