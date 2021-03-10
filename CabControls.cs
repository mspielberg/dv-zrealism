using DV.CabControls;
using DV.CabControls.Spec;
using HarmonyLib;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class AdjustCabControls
    {
        [HarmonyPatch(typeof(ControlsInstantiator), nameof(ControlsInstantiator.Spawn))]
        public static class SpawnPatch
        {
            public static void Prefix(ControlSpec spec)
            {
                if (spec.name == "C throttle" && spec is Lever leverSpec)
                {
                    leverSpec.invertDirection ^= true;
                }
            }
        }
    }
}