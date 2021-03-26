using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class Couplers
    {
        private const float ChainSpring = 2e7f; // ~1,200,000 lb/in
        private const float CouplerSlop = 0.25f;

        [HarmonyPatch(typeof(Coupler), nameof(Coupler.CreateJoints))]
        public static class CreateJointsPatch
        {
            public static bool Prefix(Coupler __instance)
            {
                // ignore tender joint
                if (!Main.settings.enableCustomCouplers ||
                    (CarTypes.IsSteamLocomotive(__instance.train.carType) && !__instance.isFrontCoupler))
                {
                    return true;
                }

                CreateTensionJoint(__instance);
                CreateCompressionJoint(__instance);
                return false;
            }

            private static void CreateTensionJoint(Coupler coupler)
            {
                var anchorOffset =  Vector3.forward * CouplerSlop * (coupler.isFrontCoupler ? -1f : 1f);

                var cj = coupler.train.gameObject.AddComponent<ConfigurableJoint>();
                cj.autoConfigureConnectedAnchor = false;
                cj.anchor = coupler.transform.localPosition + anchorOffset;
                cj.connectedBody = coupler.coupledTo.train.gameObject.GetComponent<Rigidbody>();
                cj.connectedAnchor = coupler.coupledTo.transform.localPosition;
                cj.zMotion = ConfigurableJointMotion.Limited;

                var distance = JointDelta(cj).z;
                cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(CouplerSlop, Mathf.Abs(distance)) };
                cj.linearLimitSpring = new SoftJointLimitSpring { spring = ChainSpring };
                cj.enableCollision = false;
                cj.targetPosition = -anchorOffset;

                coupler.springyCJ = cj;
                coupler.jointCoroSpringy = coupler.StartCoroutine(AdaptLimitCoro(cj));
                ApplySettings(coupler);
            }

            private static void CreateCompressionJoint(Coupler coupler)
            {
                var cj = coupler.train.gameObject.AddComponent<ConfigurableJoint>();
                cj.autoConfigureConnectedAnchor = false;
                cj.anchor = coupler.transform.localPosition;
                cj.connectedBody = coupler.coupledTo.train.gameObject.GetComponent<Rigidbody>();
                cj.connectedAnchor = coupler.coupledTo.transform.localPosition;
                cj.zMotion = ConfigurableJointMotion.Limited;

                var distance = JointDelta(cj).z;
                cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(CouplerSlop, Mathf.Abs(distance)) };
                cj.linearLimitSpring = new SoftJointLimitSpring { spring = ChainSpring };
                cj.enableCollision = false;
                cj.breakForce = float.PositiveInfinity;

                coupler.rigidCJ = cj;
                coupler.jointCoroRigid = coupler.StartCoroutine(AdaptLimitCoro(cj));
            }

            private static Vector3 JointDelta(Joint joint)
            {
                return joint.transform.InverseTransformPoint(joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)) - joint.anchor;
            }
        }

        private static IEnumerator AdaptLimitCoro(ConfigurableJoint cj)
        {
            while (cj.linearLimit.limit > CouplerSlop)
            {
                yield return WaitFor.FixedUpdate;
                cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(CouplerSlop, cj.linearLimit.limit - 0.001f) };
            }
        }

        public static void ApplySettings(Coupler coupler)
        {
                if (coupler.springyCJ == null)
                    return;
                var joint = coupler.springyCJ;
                joint.zDrive = new JointDrive {
                    positionSpring = Main.settings.bufferSpringRate * 1e4f,
                    positionDamper = Main.settings.bufferDamperRate * 1e4f,
                    maximumForce = float.PositiveInfinity,
                };
                joint.breakForce = 1e6f * Main.settings.couplerStrength;
        }

        public static void ApplySettings()
        {
            foreach (var coupler in Component.FindObjectsOfType<Coupler>())
                ApplySettings(coupler);
        }
    }
}