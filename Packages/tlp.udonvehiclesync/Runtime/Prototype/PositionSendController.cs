using JetBrains.Annotations;
using TLP.UdonUtils;
using TLP.UdonUtils.Extensions;
using TLP.UdonUtils.Physics;
using TLP.UdonUtils.Sources;
using TLP.UdonUtils.Sources.Time;
using TLP.UdonUtils.Sync;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace TLP.UdonVehicleSync.TLP.UdonVehicleSync.Runtime.Prototype
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DefaultExecutionOrder(ExecutionOrder)]
    public class PositionSendController : TlpBaseBehaviour
    {
        #region ExecutionOrder
        protected override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = VelocityProvider.ExecutionOrder + 1;
        #endregion

        #region Dependencies
        public VelocityProvider VelocityProvider;
        public PredictingSync PlayerNetworkTransform;
        public TlpNetworkTime NetworkTime;
        public TimeSource GameTime;
        #endregion

        #region Settings
        public bool DynamicSendRate;

        [Tooltip("Shortest time between new updates, if dynamic send rate is used this is the lower limit")]
        [Range(0.05f, 1f)]
        public float SendInterval = 0.125f;

        [Header(("Dynamic send rate"))]
        [Tooltip(
                "The longest time between updates, delta time between sends can increase up to this value when " +
                "the no large enough error in prediction is detected")]
        [Range(0.05f, 60f)]
        public float MaxSendInterval = 4f;

        [Tooltip("Degrees")]
        public float RotationThreshold = 5f;

        [Tooltip("Degrees")]
        public float VelocityDirectionThreshold = 3f;

        [Tooltip("m/s")]
        public float VelocityMagnitudeThreshold = 1f;

        [Tooltip("m")]
        public float PositionThreshold = 1f;
        #endregion

        #region State
        private float _nextSerializationTime = float.MinValue;

        #region LastShared
        private double _timeSinceLevelLoad;
        private Vector3 _position;
        private Vector3 _velocity;
        private Vector3 _acceleration;
        private Quaternion _rotation;
        private Vector3 _angularVelocity;
        private Vector3 _angularAcceleration;
        private Transform _relativeTo;
        private float _circleAngularVelocityDegrees;
        #endregion
        #endregion

        #region Lifecycle
        public override void PostLateUpdate() {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog(nameof(PostLateUpdate));
#endif
            #endregion

            float time = GameTime.Time();
            if (CanSkipUpdate(time)) {
                return;
            }

            _nextSerializationTime = ScheduleNextUpdate(_nextSerializationTime, time, SendInterval);
            PlayerNetworkTransform.RequestSerialization();
        }
        #endregion

        #region Public
        /// <summary>
        /// Grabs the latest snapshot from the <see cref="VelocityProvider"/> and
        /// copies it into the <see cref="PlayerNetworkTransform"/> for serialization.
        /// </summary>
        public virtual void UpdateSerializableData() {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog(nameof(UpdateSerializableData));
#endif
            #endregion

            GetLatestSnapShot(
                    out double timeSinceLevelLoad,
                    out var position,
                    out var velocity,
                    out var acceleration,
                    out var rotation,
                    out var angularVelocity,
                    out var angularAcceleration,
                    out var relativeTo,
                    out float circleAngularVelocityDegrees);

            _timeSinceLevelLoad = timeSinceLevelLoad;
            _position = position;
            _velocity = velocity;
            _acceleration = acceleration;
            _rotation = rotation;
            _angularVelocity = angularVelocity;
            _angularAcceleration = angularAcceleration;
            _relativeTo = relativeTo;
            _circleAngularVelocityDegrees = circleAngularVelocityDegrees;

            PlayerNetworkTransform.ShareMovement(
                    _timeSinceLevelLoad + NetworkTime.GetGameTimeOffset(),
                    _position,
                    _velocity,
                    _acceleration,
                    _rotation,
                    _angularVelocity,
                    _angularAcceleration,
                    _relativeTo,
                    _circleAngularVelocityDegrees
            );
        }
        #endregion

        #region Overrides
        protected override bool SetupAndValidate() {
            if (!base.SetupAndValidate()) {
                return false;
            }

            if (!Utilities.IsValid(NetworkTime)) {
                Error($"{nameof(NetworkTime)} is not set");
                return false;
            }

            if (!Utilities.IsValid(GameTime)) {
                Error($"{nameof(GameTime)} is not set");
                return false;
            }

            if (!Utilities.IsValid(PlayerNetworkTransform)) {
                Error($"{nameof(PlayerNetworkTransform)} is not set");
                return false;
            }

            if (!Utilities.IsValid(VelocityProvider)) {
                Error($"{nameof(VelocityProvider)} is not set");
                return false;
            }

            _nextSerializationTime = GameTime.Time();
            return true;
        }
        #endregion

        #region Internal
        private void GetLatestSnapShot(
                out double timeSinceLevelLoad,
                out Vector3 position,
                out Vector3 velocity,
                out Vector3 acceleration,
                out Quaternion rotation,
                out Vector3 angularVelocity,
                out Vector3 angularAcceleration,
                out Transform relativeTo,
                out float circleAngularVelocityDegrees
        ) {
            acceleration = VelocityProvider.AccelerationAvg3;
            timeSinceLevelLoad = VelocityProvider.GetLatestSnapShot(
                    out position,
                    out velocity,
                    out var unused,
                    out rotation,
                    out angularVelocity,
                    out angularAcceleration,
                    out relativeTo,
                    out circleAngularVelocityDegrees
            );
        }

        private bool IsUpdateRequired() {
            GetLatestSnapShot(
                    out double timeSinceLevelLoad,
                    out var position,
                    out var velocity,
                    out var acceleration,
                    out var rotation,
                    out var angularVelocity,
                    out var angularAcceleration,
                    out var relativeTo,
                    out float circleAngularVelocityDegrees);


            double toPredict =
                    timeSinceLevelLoad -
                    _timeSinceLevelLoad; // + SendInterval + AssumedAverageLatencyBetweenPlayers;
            if (toPredict <= 0) return true;
            TlpAccurateSyncBehaviour.PredictState(
                    _position,
                    _velocity,
                    _acceleration,
                    _angularVelocity,
                    _rotation,
                    _circleAngularVelocityDegrees,
                    15f,
                    (float)toPredict,
                    out var newPosition,
                    out var newVelocity,
                    out var newRotation);


            if (Vector3.Distance(newPosition, position) < PositionThreshold
                && rotation.GetDeltaToB(newRotation).eulerAngles.magnitude < RotationThreshold
                && Vector3.Angle(newVelocity, velocity) < VelocityDirectionThreshold
                && Vector3.Distance(newVelocity, velocity) < VelocityMagnitudeThreshold) {
                // Prediction is still good enough, no need to send another update
                return false;
            }

            return true;
        }

        private bool CanSkipUpdate(float time) {
            if (time < _nextSerializationTime) return true;
            float timeSinceSkippedUpdate = time - _nextSerializationTime;
            return DynamicSendRate
                   && timeSinceSkippedUpdate < MaxSendInterval
                   && !IsUpdateRequired();
        }


        /// <param name="time"></param>
        /// <param name="sendInterval">expected to be > 0</param>
        /// <param name="lastSerializationTime"></param>
        /// <returns></returns>
        internal float ScheduleNextUpdate(float lastSerializationTime, float time, float sendInterval) {
#if TLP_DEBUG
            if (sendInterval <= 0) {
                Warn($"{nameof(sendInterval)} should be > 0, was {sendInterval}");
            }
#endif
            float nextSerializationTime = lastSerializationTime + sendInterval;
            if (nextSerializationTime < time + 0.5f * sendInterval) {
                nextSerializationTime = time + sendInterval;
            }

            return nextSerializationTime;
        }
        #endregion
    }
}