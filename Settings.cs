using UnityModManagerNet;

namespace DvMod.RealismFixes
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        [Draw("Force switches when running through junction")] public bool forceSwitchOnRunningThrough = false;

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