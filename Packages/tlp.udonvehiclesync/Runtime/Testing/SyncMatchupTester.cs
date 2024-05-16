using JetBrains.Annotations;
using TLP.UdonUtils;
using TLP.UdonUtils.Sources;
using TLP.UdonVehicleSync.TLP.UdonVehicleSync.Runtime.Prototype;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;
using VRC.Udon.Common.Enums;

namespace TLP.UdonVehicleSync.Runtime.Testing
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DefaultExecutionOrder(ExecutionOrder)]
    public class SyncMatchupTester : TlpBaseBehaviour
    {
        protected override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = PredictingSync.ExecutionOrder + 1;

        public Rigidbody Target;
        public float[] Velocities = new float[] { 4, 8, 16, 32, 64, 128, 256, 512, 1024 };
        private int _index = 5;

        public KeyCode Code = KeyCode.F;

        public TimeSource NetworkTime;

        [UdonSynced]
        public double StartTime;

        public void LateUpdate()
        {
            if (Input.GetKeyDown(Code))
            {
                Trigger();
            }

            if (Input.GetKeyDown(KeyCode.U))
            {
                _index = Mathf.Min(Velocities.Length - 1, _index + 1);
                DebugLog($"New Speed = {Velocities[_index]} m/s");
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                _index = Mathf.Max(0, _index - 1);
                DebugLog($"New Speed = {Velocities[_index]} m/s");
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                StartTime = NetworkTime.TimeAsDouble() + 3f;
                RequestSerialization();
                SendCustomEventDelayedSeconds(
                    nameof(Trigger),
                    (float)(StartTime - NetworkTime.TimeAsDouble()),
                    EventTiming.LateUpdate
                );
            }
        }

        public override void OnDeserialization(DeserializationResult deserializationResult)
        {
            base.OnDeserialization(deserializationResult);
            SendCustomEventDelayedSeconds(
                nameof(Trigger),
                (float)(StartTime - NetworkTime.TimeAsDouble()),
                EventTiming.LateUpdate
            );
        }

        public void Trigger()
        {
            float timeAfterTrigger =
                    (float)(NetworkTime.TimeAsDouble() - StartTime) + (Time.fixedTime - Time.time) +
                Time.fixedDeltaTime;
            var ownTransform = transform;
            var transformForward = ownTransform.forward;
            Target.transform.position = ownTransform.position +
                                        timeAfterTrigger *
                                        Velocities[_index] * transformForward;
            Target.velocity = transformForward * Velocities[_index];
        }
    }
}