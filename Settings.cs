using UnityModManagerNet;

namespace DvMod.ZRealism
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Force switches when running through junction")] public bool forceSwitchOnRunningThrough = false;
        [Draw("Chance to damage junction when running through")] public float runningThroughDamagePercent = 10f;
        [Draw("Show message when junction damaged")] public bool showBrokenJunctionMessage = true;
        [Draw("Play sound when junction damaged")] public bool playJunctionDamageSound = true;
        [Draw("Chance for damaged junction to flip")] public float damagedJunctionFlipPercent = 10f;

        [Draw("Diesel cooling")] public float dieselCoolingMultiplier = 1.0f;
        [Draw("Shunter cooling")] public float shunterTemperatureMultiplier = 0.12f;

        [Draw("Diesel fuel consumption multiplier")] public float dieselFuelConsumptionMultiplier = 10f;
        [Draw("Diesel oil consumption multiplier")] public float dieselOilConsumptionMultiplier = 50f;

        [Draw("Shunter fuel consumption multiplier")] public float shunterFuelConsumptionMultiplier = 10f;
        [Draw("Shunter oil consumption multiplier")] public float shunterOilConsumptionMultiplier = 50f;

        [Draw("Speedometer volume", Min=0f, Max=1f)] public float speedometerVolume = 0.2f;
        [Draw("Speedometer update period")] public float speedometerUpdatePeriod = 0.9f;
        [Draw("Speedometer smoothing")] public float speedometerSmoothing = 0.05f;

        [Draw("Enable logging")] public bool enableLogging = false;
        public readonly string? version = Main.mod?.Info.Version;

        override public void Save(UnityModManager.ModEntry entry)
        {
            Save<Settings>(this, entry);
        }

        public void OnChange()
        {
            Speedometers.OnSettingsChanged();
        }
    }
}
