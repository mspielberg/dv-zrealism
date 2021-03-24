using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class Couplers
    {
        private const float ChainSpring = 1e12f;
        private const float BufferSpring = 5e4f;
        private const float BufferDamper = 1e5f;
        private const float BreakStrength = 5e6f;
        private const float CouplerSlop = 0.5f;

        [HarmonyPatch(typeof(Coupler), nameof(Coupler.CreateJoints))]
        public static class CreateJointsPatch
        {
            public static bool Prefix(Coupler __instance)
            {
                // ignore tender joint
                if (CarTypes.IsSteamLocomotive(__instance.train.carType) && !__instance.isFrontCoupler)
                    return true;

                var coupler = __instance;
                var coupledToOffset = coupler.coupledTo.train.transform.InverseTransformPoint(coupler.coupledTo.transform.position);
                coupledToOffset.z -= Mathf.Sign(coupledToOffset.z) * CouplerSlop / 2f;

                var cj = coupler.train.gameObject.AddComponent<ConfigurableJoint>();
                cj.autoConfigureConnectedAnchor = false;
                cj.anchor = coupler.train.transform.InverseTransformPoint(coupler.transform.position);
                cj.connectedBody = coupler.coupledTo.train.gameObject.GetComponent<Rigidbody>();
                cj.connectedAnchor = coupledToOffset;
                cj.xMotion = ConfigurableJointMotion.Free;
                cj.yMotion = ConfigurableJointMotion.Free;
                cj.zMotion = ConfigurableJointMotion.Limited;
                cj.angularXMotion = ConfigurableJointMotion.Free;
                cj.angularYMotion = ConfigurableJointMotion.Free;
                cj.angularZMotion = ConfigurableJointMotion.Free;

                var distance = coupler.transform.InverseTransformPoint(coupler.coupledTo.train.transform.TransformPoint(cj.connectedAnchor)).z;

                cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(CouplerSlop / 2, Mathf.Abs(distance)) };
                cj.linearLimitSpring = new SoftJointLimitSpring { spring = ChainSpring };

                // Main.DebugLog(() => $"distance={distance}, limit={cj.linearLimit.limit}");

                cj.zDrive = new JointDrive {
                    positionSpring = BufferSpring,
                    positionDamper = BufferDamper,
                    maximumForce = BreakStrength * Main.settings.couplerStrength,
                };
                cj.targetPosition = new Vector3(0f, 0f, Mathf.Sign(__instance.transform.localPosition.z) * CouplerSlop / 2f);

                cj.breakForce = BreakStrength;
                cj.enableCollision = true;

                coupler.springyCJ = cj;
                coupler.jointCoroSpringy = coupler.StartCoroutine(TensionChainCoro(cj));

                return false;
            }
        }

        private static IEnumerator TensionChainCoro(ConfigurableJoint cj)
        {
            while (cj.linearLimit.limit > CouplerSlop / 2)
            {
                yield return WaitFor.FixedUpdate;
                cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(CouplerSlop / 2, cj.linearLimit.limit - 0.001f) };
                // Main.DebugLog(() => $"newLimit={cj.linearLimit.limit}");
            }
        }
    }
}