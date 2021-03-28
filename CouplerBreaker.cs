using UnityEngine;

namespace DvMod.ZRealism
{
    public class CouplerBreaker : MonoBehaviour
    {
        public ConfigurableJoint? joint;
        public float jointStress;
        private float Alpha => 1f - Main.settings.couplerStressSmoothing;

        public void FixedUpdate()
        {
            if (joint == null)
            {
                Object.Destroy(this);
                return;
            }
            jointStress = ((1f - Alpha) * jointStress) + (Alpha * joint.currentForce.magnitude);
            // Main.DebugLog(TrainCar.Resolve(joint.gameObject), () => $"currentForce={joint.currentForce.magnitude},jointStress={jointStress}");
            if (jointStress > Main.settings.couplerStrength * 1e6)
            {
                joint!.gameObject.SendMessage("OnJointBreak", jointStress);
                Object.Destroy(joint);
                Object.Destroy(this);
            }
        }
    }
}