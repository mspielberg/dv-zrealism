using UnityEngine;

namespace DvMod.ZRealism
{
    public class CouplerBreaker : MonoBehaviour
    {
        public ConfigurableJoint? joint;
        public float jointStress;
        private float Alpha => 1f - Main.settings.couplerStressSmoothing;
        private static readonly Vector3 StressScaler = new Vector3(0.1f, 0.1f, 1.0f);

        public void FixedUpdate()
        {
            if (joint == null)
            {
                Object.Destroy(this);
                return;
            }
            var scaledForce = Vector3.Scale(joint.currentForce, StressScaler);
            jointStress = ((1f - Alpha) * jointStress) + (Alpha * scaledForce.magnitude);
            if (jointStress > Main.settings.couplerStrength * 1e6)
            {
                Main.DebugLog(() => $"Breaking coupler: currentForce={joint.currentForce.magnitude},jointStress={jointStress}");
                joint!.gameObject.SendMessage("OnJointBreak", jointStress);
                Object.Destroy(joint);
                Object.Destroy(this);
            }
        }
    }
}