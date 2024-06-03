using System;
using JetBrains.Annotations;
using TLP.UdonUtils.Runtime;
using TLP.UdonUtils.Runtime.Common;
using TLP.UdonUtils.Runtime.Events;
using TLP.UdonUtils.Runtime.Sync;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace TLP.UdonVehicleSync.TLP.UdonVehicleSync.Runtime.Prototype
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DefaultExecutionOrder(ExecutionOrder)]
    public class PredictingSync : TlpAccurateSyncBehaviourUpdate
    {
        #region ExecutionOrder
        protected override int ExecutionOrderReadOnly => ExecutionOrder;

        // make sure this script updates after the sender
        [PublicAPI]
        public new const int ExecutionOrder = TlpExecutionOrder.VehicleMotionStart - 1;
        #endregion

        #region Dependencies
        public Transform Target;

        [SerializeField]
        internal PositionSendController PositionSendController;

        public UdonEvent OnRespawnEvent;
        #endregion

        #region Constants
        private const float PredictionLimit = 10f;
        private const float MaxDePenetrationVelocity = 1f;
        #endregion

        #region Settings
        [Tooltip(
                "Turnrate in degrees/second considered circular movement. " +
                "Lower values increase the chance for motion to be considered circular.")]
        [Range(5, 90)]
        public float CircleThreshold = 15f;

        public float ErrorCorrectionSoftness = 0.5f;
        public float ErrorCorrectionDuration = 0.375f;
        public float TeleportationDistance = 100f;
        public float RespawnHeight = -100f;
        #endregion

        #region Network State
        [UdonSynced]
        [NonSerialized]
        public Vector3 SyncedPosition;

        [UdonSynced]
        [NonSerialized]
        public Vector3 SyncedVelocity;

        [UdonSynced]
        [NonSerialized]
        public Vector3 SyncedAcceleration;

        [UdonSynced]
        [NonSerialized]
        public Quaternion SyncedRotation;

        [UdonSynced]
        [NonSerialized]
        public Vector3 SyncedAngularVelocityRadians;

        [UdonSynced]
        [NonSerialized]
        public bool SyncedTeleportingFlipFlop;

        [UdonSynced]
        [NonSerialized]
        public float SyncedCircleAngularVelocityDegrees;

        #region Working Copy
        [NonSerialized]
        public double PreviousWorkingSendTime;

        [NonSerialized]
        public Vector3 WorkingPosition;

        [NonSerialized]
        public Vector3 PreviousWorkingPosition;

        [NonSerialized]
        public Vector3 WorkingVelocity;

        [NonSerialized]
        public Vector3 PreviousWorkingVelocity;

        [NonSerialized]
        public Vector3 WorkingAcceleration;

        [NonSerialized]
        public Vector3 PreviousWorkingAcceleration;

        [NonSerialized]
        public Quaternion WorkingRotation;

        [NonSerialized]
        public Quaternion PreviousWorkingRotation;

        [NonSerialized]
        public Vector3 WorkingAngularVelocityRadians;

        [NonSerialized]
        public Vector3 PreviousWorkingAngularVelocityRadians;

        [NonSerialized]
        public bool WorkingTeleportingFlipFlop;

        [NonSerialized]
        public float WorkingCircleAngularVelocityDegrees;

        [NonSerialized]
        public float PreviousWorkingCircleAngularVelocityDegrees;

        private bool _workingOldTeleportingFlipFlopState;
        #endregion
        #endregion

        #region State
        public Vector3 SpawnPosition;
        public Quaternion SpawnRotation;
        private Rigidbody _targetRigidBody;
        #endregion

        #region Public
        /// <summary>
        /// Teleport to a location
        /// </summary>
        /// <param name="spawnPosition"></param>
        /// <param name="spawnRotation"></param>
        /// <returns>false if caller is not owner of GameObject, true otherwise</returns>
        public bool TeleportTo(Vector3 spawnPosition, Quaternion spawnRotation) {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog($"{nameof(TeleportTo)} P={spawnPosition} R={spawnRotation.normalized.eulerAngles}");
#endif
            #endregion

            if (!Networking.IsOwner(gameObject)) {
                Warn($"Only the owner is allowed to teleport");
                return false;
            }

            WorkingAcceleration = Vector3.zero;
            WorkingVelocity = Vector3.zero;
            WorkingAngularVelocityRadians = Vector3.zero;
            WorkingPosition = spawnPosition;
            WorkingRotation = spawnRotation;
            SetTeleportationTrigger();

            LocallyTeleportToLastKnownPosition();
            return true;
        }

        /// <summary>
        /// Respawns the <see cref="Target"/> to its initial world position and rotation
        /// </summary>
        /// <param name="raiseRespawnEvent">if true <see cref="OnRespawnEvent"/> is raised after
        /// moving to the spawn location</param>
        /// <returns>true on success, false if not owner or other errors occurred</returns>
        public bool Respawn(bool raiseRespawnEvent = true) {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog(nameof(Respawn));
#endif
            #endregion

            _targetRigidBody.velocity = Vector3.zero;
            _targetRigidBody.angularVelocity = Vector3.zero;
            if (!TeleportTo(SpawnPosition + Vector3.up * 0.05f, SpawnRotation)) {
                return false;
            }

            if (raiseRespawnEvent && !OnRespawnEvent.Raise(this)) {
                Error($"Failed to raise {nameof(OnRespawnEvent)}");
                return false;
            }

            return true;
        }
        #endregion

        #region Overrides
        #region TLP Base Behaviour
        protected override bool SetupAndValidate() {
            if (!base.SetupAndValidate()) {
                return false;
            }

            if (!Utilities.IsValid(OnRespawnEvent)) {
                Error($"{nameof(OnRespawnEvent)} is not set");
                return false;
            }

            if (OnRespawnEvent.ListenerMethod != "OnRespawn") {
                Error($"{nameof(OnRespawnEvent)}.{nameof(OnRespawnEvent.ListenerMethod)} is not set to 'OnRespawn'");
                return false;
            }

            if (!Utilities.IsValid(NetworkTime)) {
                Error($"{nameof(NetworkTime)} is not set");
                return false;
            }

            if (!Utilities.IsValid(Target)) {
                Error($"{nameof(Target)} is not set");
                return false;
            }

            _targetRigidBody = Target.gameObject.GetComponent<Rigidbody>();
            if (!Utilities.IsValid(_targetRigidBody)) {
                Error($"{nameof(Target)} has no {nameof(Rigidbody)} component");
                return false;
            }

            if (!Utilities.IsValid(DebugTrailReceived)) {
                Error($"{nameof(DebugTrailReceived)} not set");
                return false;
            }

            if (!Utilities.IsValid(DebugTrailSmoothPrediction)) {
                Error($"{nameof(DebugTrailSmoothPrediction)} not set");
                return false;
            }

            if (!Utilities.IsValid(DebugTrailRawPrediction)) {
                Error($"{nameof(DebugTrailRawPrediction)} not set");
                return false;
            }

            Target.GetPositionAndRotation(out SpawnPosition, out SpawnRotation);

            DebugTrailReceived.SetActive(false);
            DebugTrailReceived.transform.parent = null;

            DebugTrailRawPrediction.SetActive(false);
            DebugTrailRawPrediction.transform.parent = null;

            DebugTrailSmoothPrediction.SetActive(true);
            DebugTrailSmoothPrediction.transform.parent = Target;
            DebugTrailSmoothPrediction.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (_targetRigidBody.isKinematic) {
                Warn($"{_targetRigidBody.GetComponentPathInScene()} was kinematic and is now changed to non-kinematic");
                _targetRigidBody.isKinematic = false;
            }

            if (_targetRigidBody.interpolation != RigidbodyInterpolation.Interpolate) {
                Warn(
                        $"{_targetRigidBody.GetComponentPathInScene()} was set to " +
                        $"{_targetRigidBody.interpolation} and is now changed to Interpolate");
                _targetRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // one-time setup upon first activation
            DebugLog(
                    $"{_targetRigidBody.interpolation}.{nameof(Rigidbody.maxDepenetrationVelocity)} " +
                    $"set to {MaxDePenetrationVelocity} m/s");
            _targetRigidBody.maxDepenetrationVelocity = MaxDePenetrationVelocity;

            return true;
        }

        public void LateUpdate() {
            if (Networking.IsOwner(gameObject) && Target.position.y < RespawnHeight) {
                Respawn();
            }
        }
        #endregion

        #region TlpAccurateSyncBehaviour
        protected override void CreateNetworkStateFromWorkingState() {
            PositionSendController.UpdateSerializableData();
            base.CreateNetworkStateFromWorkingState();
            SyncedPosition = WorkingPosition;
            SyncedVelocity = WorkingVelocity;
            SyncedAcceleration = WorkingAcceleration;
            SyncedRotation = WorkingRotation;
            SyncedAngularVelocityRadians = WorkingAngularVelocityRadians;
            SyncedTeleportingFlipFlop = WorkingTeleportingFlipFlop;
            SyncedCircleAngularVelocityDegrees = WorkingCircleAngularVelocityDegrees;
        }

        protected override void RecordSnapshot(TimeSnapshot timeSnapshot, double mostRecentServerTime) {
            base.RecordSnapshot(timeSnapshot, mostRecentServerTime);

            var transformSnapshot = (TransformSnapshot)timeSnapshot;
            transformSnapshot.Position = WorkingPosition;
            transformSnapshot.Rotation = WorkingRotation;
        }

        protected override void CreateWorkingCopyOfNetworkState() {
            MakeInterpolatedStateOldState();

            WorkingPosition = SyncedPosition;
            WorkingVelocity = SyncedVelocity;
            WorkingAcceleration = SyncedAcceleration;
            WorkingRotation = SyncedRotation;
            WorkingAngularVelocityRadians = SyncedAngularVelocityRadians;
            WorkingTeleportingFlipFlop = SyncedTeleportingFlipFlop;
            WorkingCircleAngularVelocityDegrees = SyncedCircleAngularVelocityDegrees;

            base.CreateWorkingCopyOfNetworkState();
        }


        public override void OnPreSerialization() {
            _targetRigidBody.isKinematic = false;
            base.OnPreSerialization();
            DebugTrailReceived.SetActive(false);
            DebugTrailRawPrediction.SetActive(false);
            DebugTrailSmoothPrediction.SetActive(ShowDebugTrails);
        }

        public override void OnDeserialization(DeserializationResult deserializationResult) {
            _targetRigidBody.isKinematic = true;
            DebugTrailReceived.transform.SetPositionAndRotation(WorkingPosition, WorkingRotation);
            DebugTrailReceived.SetActive(ShowDebugTrails);
            DebugTrailRawPrediction.SetActive(ShowDebugTrails);
            DebugTrailSmoothPrediction.SetActive(ShowDebugTrails);

            base.OnDeserialization(deserializationResult);
        }


        protected override void PredictMovement(double elapsedSinceSent, float deltaTime) {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog(
                    $"{nameof(PredictMovement)}: {nameof(elapsedSinceSent)} = {elapsedSinceSent:F6}s;" +
                    $" {nameof(deltaTime)} = {deltaTime:F6}s");
#endif
            #endregion

            if (IsTeleportationTriggered()) {
                #region TLP_DEBUG
#if TLP_DEBUG
                DebugLog("Teleportation triggered");
#endif
                #endregion

                ClearTeleportationFlag();
                LocallyTeleportToLastKnownPosition();
                return;
            }

            if (TryInterpolateMovement()) {
                #region TLP_DEBUG
#if TLP_DEBUG
                DebugLog("Interpolating triggered");
#endif
                #endregion

                return;
            }

            if (elapsedSinceSent > PredictionLimit || elapsedSinceSent < 0) {
                LocallyTeleportToLastKnownPosition();

                #region TLP_DEBUG
#if TLP_DEBUG
                DebugLog("Last known Position");
#endif
                #endregion

                return;
            }

            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog("Predicting");
#endif
            #endregion

            // predict from latest received data
            PredictState(
                    WorkingPosition,
                    WorkingVelocity,
                    WorkingAcceleration,
                    WorkingAngularVelocityRadians,
                    WorkingRotation,
                    WorkingCircleAngularVelocityDegrees,
                    CircleThreshold,
                    elapsedSinceSent,
                    out var newPosition,
                    out var newVelocity,
                    out var newRotation);

            // predict from previous received data
            double elapsedSincePreviousSent =
                    NetworkTime.TimeAsDouble() - PreviousWorkingSendTime - PredictionReduction;
            PredictState(
                    PreviousWorkingPosition,
                    PreviousWorkingVelocity,
                    PreviousWorkingAcceleration,
                    PreviousWorkingAngularVelocityRadians,
                    PreviousWorkingRotation,
                    PreviousWorkingCircleAngularVelocityDegrees,
                    CircleThreshold,
                    elapsedSincePreviousSent,
                    out var oldPosition,
                    out var oldVelocity,
                    out var oldRotation);


            double rawBlend = Mathf.Clamp(
                    (float)((NetworkTime.TimeAsDouble() - ReceiveTime) / ErrorCorrectionDuration),
                    0,
                    1);
            float blend = ErrorCorrectionSoftness == 0f
                    ? (float)rawBlend
                    : CubicInterpolationFactor(
                            (float)Math.Pow(rawBlend, Mathf.Clamp01(ErrorCorrectionSoftness)));


            if (Vector3.Distance(oldPosition, newPosition) > TeleportationDistance) {
                PreviousWorkingAcceleration = WorkingAcceleration;
                PreviousWorkingPosition = WorkingPosition;
                PreviousWorkingRotation = WorkingRotation;
                PreviousWorkingVelocity = WorkingVelocity;
                PreviousWorkingAngularVelocityRadians = WorkingAngularVelocityRadians;
                PreviousWorkingCircleAngularVelocityDegrees = WorkingCircleAngularVelocityDegrees;
                PreviousWorkingSendTime = WorkingSendTime;
                Target.SetPositionAndRotation(newPosition, newRotation);
                return;
            }

            Target.SetPositionAndRotation(
                    Vector3.Lerp(oldPosition, newPosition, blend),
                    Quaternion.Slerp(oldRotation, newRotation, blend));

            if (DebugTrailRawPrediction) {
                DebugTrailRawPrediction.transform.SetPositionAndRotation(newPosition, newRotation);
            }
        }
        #endregion
        #endregion

        #region Internal
        /// <param name="t">will be clamped to 0-1 range</param>
        internal static float CubicInterpolationFactor(float t) {
            float x = Mathf.Clamp01(t);
            float xSquared = x * x;
            return -2f * xSquared * x + 3f * xSquared;
        }

        /// <summary>
        /// Flips the teleporting flags and updates the network variable but without triggering a synchronization.
        /// </summary>
        internal void SetTeleportationTrigger() {
            WorkingTeleportingFlipFlop = !WorkingTeleportingFlipFlop;
            _workingOldTeleportingFlipFlopState = WorkingTeleportingFlipFlop;
        }

        /// <summary>
        /// Checks the current teleporting flag against its previous state to see if the latest
        /// deserialization requests teleportation.
        /// </summary>
        /// <returns>true if the teleporting flag has been flipped and teleportation is needed</returns>
        internal bool IsTeleportationTriggered() {
            return WorkingTeleportingFlipFlop != _workingOldTeleportingFlipFlopState;
        }

        /// <summary>
        /// Resets the teleportation flags in a way to detect the next teleportation request.
        /// </summary>
        internal void ClearTeleportationFlag() {
            _workingOldTeleportingFlipFlopState = WorkingTeleportingFlipFlop;
        }

        internal void ShareMovement(
                double time,
                Vector3 position,
                Vector3 velocity,
                Vector3 acceleration,
                Quaternion rotation,
                Vector3 angularVelocity,
                Vector3 angularAcceleration,
                Transform relativeTo,
                float circleAngularVelocityDegrees
        ) {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog(nameof(ShareMovement));
#endif
            #endregion

            WorkingSendTime = time;
            WorkingPosition = position;
            WorkingVelocity = velocity;
            WorkingAcceleration = acceleration;
            WorkingRotation = rotation;
            WorkingAngularVelocityRadians = angularVelocity;
            WorkingCircleAngularVelocityDegrees = circleAngularVelocityDegrees;
        }

        internal bool TryInterpolateMovement() {
            DebugLog(nameof(TryInterpolateMovement));
            if (!Backlog.Interpolatable(NetworkTime.Time() - PredictionReduction)) {
                return false;
            }

            _targetRigidBody.velocity = Vector3.zero;
            _targetRigidBody.angularVelocity = Vector3.zero;

            if (((TransformBacklog)Backlog).Interpolate(
                        NetworkTime.Time() - PredictionReduction,
                        out var position,
                        out var rotation)) {
                _targetRigidBody.MovePosition(position);
                _targetRigidBody.MoveRotation(rotation);
            }

            return true;
        }

        internal void LocallyTeleportToLastKnownPosition() {
            DebugLog(nameof(LocallyTeleportToLastKnownPosition));
            Target.SetPositionAndRotation(WorkingPosition, WorkingRotation);

            PreviousWorkingAcceleration = WorkingAcceleration;
            PreviousWorkingPosition = WorkingPosition;
            PreviousWorkingRotation = WorkingRotation;
            PreviousWorkingVelocity = WorkingVelocity;
            PreviousWorkingAngularVelocityRadians = WorkingAngularVelocityRadians;
            PreviousWorkingCircleAngularVelocityDegrees = WorkingCircleAngularVelocityDegrees;
            PreviousWorkingSendTime = WorkingSendTime;
        }

        internal void MakeInterpolatedStateOldState() {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog(nameof(MakeInterpolatedStateOldState));
#endif
            #endregion

            double elapsedSincePreviousSent = NetworkTime.TimeAsDouble() - WorkingSendTime;
            if (elapsedSincePreviousSent > PredictionLimit) {
                PreviousWorkingSendTime = SyncedSendTime;
                PreviousWorkingPosition = SyncedPosition;
                PreviousWorkingVelocity = SyncedVelocity;
                PreviousWorkingAcceleration = SyncedAcceleration;
                PreviousWorkingRotation = SyncedRotation;
                PreviousWorkingAngularVelocityRadians = SyncedAngularVelocityRadians;
                PreviousWorkingCircleAngularVelocityDegrees = SyncedCircleAngularVelocityDegrees;

                // predict from previous received data
                double elapsedSinceSent = NetworkTime.TimeAsDouble() - PreviousWorkingSendTime;
                PredictState(
                        PreviousWorkingPosition,
                        PreviousWorkingVelocity,
                        PreviousWorkingAcceleration,
                        PreviousWorkingAngularVelocityRadians,
                        PreviousWorkingRotation,
                        PreviousWorkingCircleAngularVelocityDegrees,
                        CircleThreshold,
                        elapsedSinceSent,
                        out PreviousWorkingPosition,
                        out PreviousWorkingVelocity,
                        out PreviousWorkingRotation);
                PreviousWorkingSendTime = NetworkTime.TimeAsDouble();
                return;
            }

            // predict from previously latest received data
            PredictState(
                    WorkingPosition,
                    WorkingVelocity,
                    WorkingAcceleration,
                    WorkingAngularVelocityRadians,
                    WorkingRotation,
                    WorkingCircleAngularVelocityDegrees,
                    CircleThreshold,
                    NetworkTime.TimeAsDouble() - WorkingSendTime,
                    out var newPosition,
                    out var newVelocity,
                    out var newRotation);

            // predict from previous received data
            PredictState(
                    PreviousWorkingPosition,
                    PreviousWorkingVelocity,
                    PreviousWorkingAcceleration,
                    PreviousWorkingAngularVelocityRadians,
                    PreviousWorkingRotation,
                    PreviousWorkingCircleAngularVelocityDegrees,
                    CircleThreshold,
                    NetworkTime.TimeAsDouble() - PreviousWorkingSendTime,
                    out var oldPosition,
                    out var oldVelocity,
                    out var oldRotation);

            double rawBlend = 1.0;
            if (ErrorCorrectionDuration > 0f) {
                rawBlend = Mathf.Clamp(
                        (float)((NetworkTime.TimeAsDouble() - ReceiveTime) / ErrorCorrectionDuration),
                        0,
                        1);
            }

            float blend = (float)rawBlend;
            if (ErrorCorrectionSoftness > 0f) {
                blend = CubicInterpolationFactor(
                        (float)Math.Pow(rawBlend, Mathf.Min(1.0f, ErrorCorrectionSoftness)));
            }

            PreviousWorkingVelocity = Vector3.Lerp(oldVelocity, newVelocity, blend);
            PreviousWorkingAngularVelocityRadians = Vector3.Lerp(
                    PreviousWorkingAngularVelocityRadians,
                    WorkingAngularVelocityRadians,
                    blend);
            PreviousWorkingPosition = Vector3.Lerp(oldPosition, newPosition, blend);
            PreviousWorkingAcceleration = Vector3.Lerp(PreviousWorkingAcceleration, WorkingAcceleration, blend);
            PreviousWorkingRotation = Quaternion.Slerp(oldRotation, newRotation, blend);
            PreviousWorkingSendTime = NetworkTime.TimeAsDouble();
            PreviousWorkingCircleAngularVelocityDegrees = Mathf.Lerp(
                    PreviousWorkingCircleAngularVelocityDegrees,
                    WorkingCircleAngularVelocityDegrees,
                    blend);
        }
        #endregion
    }
}