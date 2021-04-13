using HarmonyLib;
using System.Collections;
using UnityEngine;

namespace DvMod.ZRealism
{
    public static class Couplers
    {
        private const float ChainSpring = 2e7f; // ~1,200,000 lb/in
        private const float ChainSlop = 0.5f;
        private const float BufferTravel = 0.25f;

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

                Main.DebugLog(() => $"Creating tension joint between {__instance.train.ID} and {__instance.coupledTo.train.ID}");
                CreateTensionJoint(__instance);
                var breaker = __instance.gameObject.AddComponent<CouplerBreaker>();
                breaker.joint = __instance.springyCJ;
                if (__instance.rigidCJ == null && __instance.coupledTo.rigidCJ == null)
                    CreateCompressionJoint(__instance, __instance.coupledTo);
                return false;
            }
        }

        [HarmonyPatch(typeof(Coupler), nameof(Coupler.Uncouple))]
        public static class UncouplePatch
        {
            private static ConfigurableJoint? compressionJoint;
            private static Coroutine? coro;

            public static void Prefix(Coupler __instance)
            {
                compressionJoint = __instance.rigidCJ;
                // Prevent Uncouple from destroying compression joint
                __instance.rigidCJ = null;
                coro = __instance.jointCoroRigid;
                __instance.jointCoroRigid = null;
            }

            public static void Postfix(Coupler __instance)
            {
                __instance.rigidCJ = compressionJoint;
                compressionJoint = null;
                __instance.jointCoroRigid = coro;
                coro = null;
            }
        }

        private static void CreateTensionJoint(Coupler coupler)
        {
            var anchorOffset =  Vector3.forward * ChainSlop * (coupler.isFrontCoupler ? -1f : 1f);

            var cj = coupler.train.gameObject.AddComponent<ConfigurableJoint>();
            cj.autoConfigureConnectedAnchor = false;
            cj.anchor = coupler.transform.localPosition + anchorOffset;
            cj.connectedBody = coupler.coupledTo.train.gameObject.GetComponent<Rigidbody>();
            cj.connectedAnchor = coupler.coupledTo.transform.localPosition;

            cj.xMotion = ConfigurableJointMotion.Limited;
            cj.yMotion = ConfigurableJointMotion.Limited;
            cj.zMotion = ConfigurableJointMotion.Limited;
            cj.angularYMotion = ConfigurableJointMotion.Limited;

            cj.angularYLimit = new SoftJointLimit { limit = 30f };

            var distance = JointDelta(cj, coupler.isFrontCoupler).z;
            cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(ChainSlop, Mathf.Abs(distance)) };
            cj.linearLimitSpring = new SoftJointLimitSpring { spring = ChainSpring };
            cj.enableCollision = false;
            cj.breakForce = float.PositiveInfinity;
            cj.breakTorque = 1e3f;

            coupler.springyCJ = cj;
            coupler.jointCoroSpringy = coupler.StartCoroutine(AdaptTensionJointCoro(cj));
        }

        private static IEnumerator AdaptTensionJointCoro(ConfigurableJoint cj)
        {
            while (cj.linearLimit.limit > ChainSlop)
            {
                yield return WaitFor.FixedUpdate;
                cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(ChainSlop, cj.linearLimit.limit - 0.001f) };
            }
        }

        // Ensure CouplingScanners always stay active
        // TODO: disable when coupled, enable when uncoupled
        [HarmonyPatch(typeof(ChainCouplerVisibilityOptimizer), nameof(ChainCouplerVisibilityOptimizer.Disable))]
        public static class ChainCouplerVisibilityOptimizerDisablePatch
        {
            public static bool Prefix(ChainCouplerVisibilityOptimizer __instance)
            {
                if (!__instance.enabled)
                    return false;
                __instance.enabled = false;
                __instance.chain.SetActive(false);
                return false;
            }
        }

        [HarmonyPatch(typeof(CouplingScanner), nameof(CouplingScanner.Start))]
        public static class CouplingScannerStartPatch
        {
            public static void Postfix(CouplingScanner __instance)
            {
                var scanner = __instance;
                __instance.ScanStateChanged += (CouplingScanner otherScanner) =>
                {
                    if (scanner == null)
                        return;
                    var car = TrainCar.Resolve(scanner.gameObject);
                    if (car == null)
                        return;
                    var coupler = scanner.transform.localPosition.z > 0 ? car.frontCoupler : car.rearCoupler;
                    if (coupler == null)
                        return;

                    if (otherScanner != null)
                    {
                        var otherCar = TrainCar.Resolve(otherScanner.gameObject);
                        var otherCoupler = otherScanner.transform.localPosition.z > 0 ? otherCar.frontCoupler : otherCar.rearCoupler;
                        if (coupler.rigidCJ == null && otherCoupler.rigidCJ == null)
                            CreateCompressionJoint(coupler, otherCoupler);
                    }
                    else
                    {
                        DestroyCompressionJoint(coupler);
                    }
                };
            }
        }

        [HarmonyPatch(typeof(CouplingScanner), nameof(CouplingScanner.MasterCoro))]
        public static class CouplerScannerMasterCoroPatch
        {
            public static bool Prefix(CouplingScanner __instance, ref IEnumerator __result)
            {
                __result = ReplacementCoro(__instance);
                return false;
            }

            private static IEnumerator ReplacementCoro(CouplingScanner __instance)
            {
                var wait = WaitFor.Seconds(0.1f);
                while (true)
                {
                    yield return wait;
                    var offset = __instance.transform.InverseTransformPoint(__instance.nearbyScanner.transform.position);
                    if (Mathf.Abs(offset.x) > 1.6f || Mathf.Abs(offset.z) > 2f)
                        break;
                }
                __instance.Unpair(true);
            }
        }

        private static void CreateCompressionJoint(Coupler a, Coupler b)
        {
            Main.DebugLog(() => $"Creating compression joint between {TrainCar.Resolve(a.gameObject)?.ID} and {TrainCar.Resolve(b.gameObject)?.ID}");

            var cj = a.train.gameObject.AddComponent<ConfigurableJoint>();
            cj.autoConfigureConnectedAnchor = false;
            cj.anchor = a.transform.localPosition;
            cj.connectedBody = b.train.gameObject.GetComponent<Rigidbody>();
            cj.connectedAnchor = b.transform.localPosition;
            cj.zMotion = ConfigurableJointMotion.Limited;

            var distance = JointDelta(cj, a.isFrontCoupler).z;
            cj.linearLimit = new SoftJointLimit { limit = float.PositiveInfinity };
            cj.linearLimitSpring = new SoftJointLimitSpring { spring = ChainSpring };
            cj.enableCollision = false;
            cj.targetPosition = Vector3.zero;
            cj.breakForce = float.PositiveInfinity;
            cj.breakTorque = float.PositiveInfinity;

            a.rigidCJ = cj;
            a.jointCoroRigid = a.StartCoroutine(AdaptCompressionJointCoro(cj, a.isFrontCoupler));
        }

        private static void DestroyCompressionJoint(Coupler coupler)
        {
            if (coupler.rigidCJ == null)
                return;
            Main.DebugLog(() => $"Destroying compression joint between {TrainCar.Resolve(coupler.gameObject)?.ID} and {TrainCar.Resolve(coupler.rigidCJ.connectedBody.gameObject)?.ID}");
            if (coupler.jointCoroRigid != null)
            {
                coupler.StopCoroutine(coupler.jointCoroRigid);
                coupler.jointCoroRigid = null;
            }
            Component.Destroy(coupler.rigidCJ);
        }

        private static IEnumerator AdaptCompressionJointCoro(ConfigurableJoint cj, bool isFrontCoupler)
        {
            while (true)
            {
                yield return WaitFor.FixedUpdate;
                var distance = JointDelta(cj, isFrontCoupler).z;
                if (distance < -0.01)
                {
                    cj.zDrive = new JointDrive {
                        positionSpring = Main.settings.bufferSpringRate * 1e6f,
                        positionDamper = Main.settings.bufferDamperRate * 1e6f,
                        maximumForce = float.PositiveInfinity,
                    };
                    cj.linearLimit = new SoftJointLimit { limit = BufferTravel };
                }
                else
                {
                    cj.zDrive = default;
                    cj.linearLimit = new SoftJointLimit { limit = float.PositiveInfinity };
                }
            }
        }

        private static Vector3 JointDelta(Joint joint, bool isFrontCoupler)
        {
            var delta = joint.transform.InverseTransformPoint(joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)) - joint.anchor;
            return isFrontCoupler ? delta : -delta;
        }
    }
}