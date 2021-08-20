using CommandTerminal;
using DV.CabControls.Spec;
using DV.Logic.Job;
using DV.ServicePenalty;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
            Register("dumpcouplers", _ =>
            {
                var car = PlayerManager.Car;
                if (car == null)
                    return;

                if (Couplers.bufferJoints.TryGetValue(car.frontCoupler, out var frontBufferJoint))
                {
                    Terminal.Log("Front buffer:");
                    DumpJoint(true, frontBufferJoint);
                    Terminal.Log("Front rigid compression:");
                    DumpJoint(true, car.frontCoupler.rigidCJ);
                }
                if (car.frontCoupler.IsCoupled())
                {
                    Terminal.Log("Front tension:");
                    DumpJoint(true, car.frontCoupler.springyCJ);
                }

                if (Couplers.bufferJoints.TryGetValue(car.rearCoupler, out var rearBufferJoint))
                {
                    Terminal.Log("Rear buffer:");
                    DumpJoint(true, rearBufferJoint);
                    Terminal.Log("Rear rigid compression:");
                    DumpJoint(true, car.rearCoupler.rigidCJ);
                }
                if (car.rearCoupler.IsCoupled())
                {
                    Terminal.Log("Rear tension:");
                    DumpJoint(true, car.rearCoupler.springyCJ);
                }
            });
        }

        private static Vector3 JointDelta(Joint joint, bool isFrontCoupler)
        {
            var delta = joint.transform.InverseTransformPoint(joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)) - joint.anchor;
            return isFrontCoupler ? delta : -delta;
        }

        private static void DumpJoint(bool isFront, ConfigurableJoint j)
        {
            if (j == null)
                return;
            // Terminal.Log($"jointPosition={j.transform.localPosition},jointRotation={j.transform.localEulerAngles}");
            // Terminal.Log($"Motion(x,y,z)={j.xMotion},{j.yMotion},{j.zMotion}");
            // Terminal.Log($"anchor={j.anchor},connectedAnchor={j.connectedAnchor}");
            Terminal.Log($"limit={j.linearLimit.limit},limitSpring={j.linearLimitSpring.spring}");
            Terminal.Log($"linearDelta={JointDelta(j, isFront):F3}");
            // var angles = Quaternion.FromToRotation(j.transform.forward, j.connectedBody.transform.forward);
            // Terminal.Log($"angle={angles.eulerAngles}");
            Terminal.Log($"breakForce={j.breakForce}");
            Terminal.Log($"jointForce={j.currentForce},magnitude={j.currentForce.magnitude}");
            Terminal.Log($"targetPosition={j.targetPosition},positionSpring={j.zDrive.positionSpring},positionDamper={j.zDrive.positionDamper}");
        }
    }
}