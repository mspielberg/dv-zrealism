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

                Main.DebugLog(() => $"Creating coupler joints between {__instance.train.ID} and {__instance.coupledTo.train.ID}");
                CreateTensionJoint(__instance);
                CreateCompressionJoint(__instance);
                var breaker = __instance.gameObject.AddComponent<CouplerBreaker>();
                breaker.joint = __instance.springyCJ;
                // Main.DebugLog(() => $"before: {__instance.Uncoupled.GetInvocationList().Length}");
                __instance.Uncoupled += OnUncoupled;
                // Main.DebugLog(() => $"after: {__instance.Uncoupled.GetInvocationList().Length}");
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

                cj.xMotion = ConfigurableJointMotion.Limited;
                cj.yMotion = ConfigurableJointMotion.Limited;
                cj.zMotion = ConfigurableJointMotion.Limited;
                cj.angularYMotion = ConfigurableJointMotion.Limited;

                cj.angularYLimit = new SoftJointLimit { limit = 30f };

                var distance = JointDelta(cj).z;
                cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(CouplerSlop, Mathf.Abs(distance)) };
                cj.linearLimitSpring = new SoftJointLimitSpring { spring = ChainSpring };
                cj.enableCollision = false;
                cj.breakForce = float.PositiveInfinity;
                cj.breakTorque = 1e3f;

                coupler.springyCJ = cj;
                coupler.jointCoroSpringy = coupler.StartCoroutine(AdaptLimitCoro(cj));
            }

            private static void CreateCompressionJoint(Coupler coupler)
            {
                DestroyPrecoupleJoint(coupler);
                DestroyPrecoupleJoint(coupler.coupledTo);

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
                cj.targetPosition = Vector3.zero;
                cj.breakForce = float.PositiveInfinity;
                cj.breakTorque = float.PositiveInfinity;

                coupler.rigidCJ = cj;
                coupler.jointCoroRigid = coupler.StartCoroutine(AdaptLimitCoro(cj));
                ApplySettings(coupler);
            }

            private static IEnumerator AdaptLimitCoro(ConfigurableJoint cj)
            {
                while (cj.linearLimit.limit > CouplerSlop)
                {
                    yield return WaitFor.FixedUpdate;
                    cj.linearLimit = new SoftJointLimit { limit = Mathf.Max(CouplerSlop, cj.linearLimit.limit - 0.001f) };
                }
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
                        CreatePrecoupleJoint(coupler, otherCoupler);
                    }
                    else
                    {
                        DestroyPrecoupleJoint(coupler);
                    }
                };
            }
        }

        private static void OnUncoupled(object coupler, UncoupleEventArgs args)
        {
            Main.DebugLog(() => $"Running OnUncoupled on {args.thisCoupler.rigidCJ} ajg {args.otherCoupler.rigidCJ}");
            args.thisCoupler.Uncoupled -= OnUncoupled;
            CreatePrecoupleJoint(args.thisCoupler, args.otherCoupler);
        }

        private static void CreatePrecoupleJoint(Coupler a, Coupler b)
        {
            if (a.IsCoupled() || b.IsCoupled())
                return;
            if ((a.rigidCJ && !a.springyCJ) || (b.rigidCJ && !b.springyCJ)) // already connected with precouple joint
                return;
            Main.DebugLog(() => $"Creating precouple joint between {TrainCar.Resolve(a.gameObject)?.ID} and {TrainCar.Resolve(b.gameObject)?.ID}");

            var cj = a.train.gameObject.AddComponent<ConfigurableJoint>();
            cj.autoConfigureConnectedAnchor = false;
            cj.anchor = a.transform.localPosition;
            cj.connectedBody = b.train.gameObject.GetComponent<Rigidbody>();
            cj.connectedAnchor = b.transform.localPosition;
            cj.zMotion = ConfigurableJointMotion.Limited;

            var distance = JointDelta(cj).z;
            cj.linearLimit = new SoftJointLimit { limit = float.PositiveInfinity };
            cj.linearLimitSpring = new SoftJointLimitSpring { spring = ChainSpring };
            cj.enableCollision = false;
            cj.targetPosition = Vector3.zero;
            cj.breakForce = float.PositiveInfinity;
            cj.breakTorque = float.PositiveInfinity;

            a.rigidCJ = cj;
            a.jointCoroRigid = a.StartCoroutine(AdaptPrecoupleJointCoro(cj));
        }

        private static void DestroyPrecoupleJoint(Coupler coupler)
        {
            if (coupler.rigidCJ == null)
                return;
            Main.DebugLog(() => $"Destroying precouple joint between {TrainCar.Resolve(coupler.gameObject)?.ID} and {TrainCar.Resolve(coupler.rigidCJ.connectedBody.gameObject)?.ID}");
            if (coupler.jointCoroRigid != null)
            {
                coupler.StopCoroutine(coupler.jointCoroRigid);
                coupler.jointCoroRigid = null;
            }
            Component.Destroy(coupler.rigidCJ);
        }

        private static IEnumerator AdaptPrecoupleJointCoro(ConfigurableJoint cj)
        {
            while (true)
            {
                yield return WaitFor.FixedUpdate;
                var distance = JointDelta(cj).z;
                if (distance > 0.01)
                {
                    cj.zDrive = new JointDrive {
                        positionSpring = Main.settings.bufferSpringRate * 1e6f,
                        positionDamper = Main.settings.bufferDamperRate * 1e6f,
                        maximumForce = float.PositiveInfinity,
                    };
                    cj.linearLimit = new SoftJointLimit { limit = CouplerSlop };
                }
                else
                {
                    cj.zDrive = default;
                    cj.linearLimit = new SoftJointLimit { limit = float.PositiveInfinity };
                }
            }
        }

        private static Vector3 JointDelta(Joint joint)
        {
            return joint.transform.InverseTransformPoint(joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)) - joint.anchor;
        }

        public static void ApplySettings(Coupler coupler)
        {
                var joint = coupler.rigidCJ;
                if (joint == null)
                    return;
                joint.zDrive = new JointDrive {
                    positionSpring = Main.settings.bufferSpringRate * 1e6f,
                    positionDamper = Main.settings.bufferDamperRate * 1e6f,
                    maximumForce = float.PositiveInfinity,
                };
        }

        public static void ApplySettings()
        {
            foreach (var coupler in Component.FindObjectsOfType<Coupler>())
                ApplySettings(coupler);
        }
    }
}