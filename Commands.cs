using CommandTerminal;
using HarmonyLib;
using System;

namespace DvMod.ZRealism
{
    public static class Commands
    {
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.Start))]
        public static class RegisterCommandsPatch
        {
            public static void Postfix()
            {
                Register();
            }
        }

        private static void Register(string name, Action<CommandArg[]> proc)
        {
            name = Main.mod!.Info.Id + "." + name;
            if (Terminal.Shell == null)
                return;
            if (Terminal.Shell.Commands.Remove(name.ToUpper()))
                Main.DebugLog(() => $"replacing existing command {name}");
            else
                Terminal.Autocomplete.Register(name);
            Terminal.Shell.AddCommand(name, proc);
        }

        public static void Register()
        {
        }
    }
}