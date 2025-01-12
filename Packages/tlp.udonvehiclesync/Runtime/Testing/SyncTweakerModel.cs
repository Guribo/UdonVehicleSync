using JetBrains.Annotations;
using TLP.UdonUtils.Runtime.DesignPatterns.MVC;
using TLP.UdonUtils.Runtime.Sync;
using TLP.UdonVehicleSync.TLP.UdonVehicleSync.Runtime.Prototype;
using UdonSharp;
using UnityEngine;
using VRC.Udon.Common;

namespace TLP.UdonVehicleSync.Runtime.Testing
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DefaultExecutionOrder(ExecutionOrder)]
    [TlpDefaultExecutionOrder(typeof(SyncTweakerModel), ExecutionOrder)]
    public class SyncTweakerModel : Model
    {
        #region ExecutionOrder
        public override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = Model.ExecutionOrder + 20;
        #endregion

        #region Constants
        private const float MinimumSendRate = 1f;
        private const float MaximumSendRate = 20f;
        private const float MaximumSendInterval = 1f / MinimumSendRate;
        private const float MinimumSendInterval = 1f / MaximumSendRate;
        #endregion

        #region Configuration
        [SerializeField]
        private PositionSendController Sender;

        [SerializeField]
        private TlpAccurateSyncBehaviour Receiver;

        [UdonSynced]
        public int SendRate = 8;

        [UdonSynced]
        public float ErrorCorrectionSoftness = 0.5f;

        [UdonSynced]
        public float ErrorCorrectionDuration = 0.375f;

        [UdonSynced]
        public float PredictionReduction;

        [UdonSynced]
        public bool DynamicSendRateEnabled;

        [UdonSynced]
        public bool DebugTrailsEnabled;
        #endregion

        protected override bool InitializeInternal() {
            if (!base.InitializeInternal()) {
                return false;
            }

            if (!Sender) {
                Error($"{nameof(Sender)} is not set");
                return false;
            }

            if (!Receiver) {
                Error($"{nameof(Receiver)} is not set");
                return false;
            }

            if (!Sender.PlayerNetworkTransform) {
                Error($"{nameof(Sender)}.{nameof(Sender.PlayerNetworkTransform)} is not set");
                return false;
            }

            SetSendRate(SendRate);
            SetErrorCorrectionDuration(ErrorCorrectionDuration);
            SetErrorCorrectionSoftness(ErrorCorrectionSoftness);
            SetPredictionReduction(PredictionReduction);
            SetDynamicSendRate(DynamicSendRateEnabled);
            SetDebugTrailsEnabled(DebugTrailsEnabled);
            Dirty = true;
            NotifyIfDirty(1);

            return true;
        }

        public void SetSendRate(int sentRate) {
            float interval = sentRate <= 0f
                    ? MaximumSendInterval
                    : Mathf.Clamp(1f / sentRate, MinimumSendInterval, MaximumSendInterval);

            Sender.SendInterval = interval;
            SendRate = sentRate;
            Dirty = true;
            NotifyIfDirty(1);
        }

        public void SetErrorCorrectionDuration(float errorCorrectionDuration) {
            Sender.PlayerNetworkTransform.ErrorCorrectionDuration = errorCorrectionDuration;
            ErrorCorrectionDuration = errorCorrectionDuration;
            Dirty = true;
            NotifyIfDirty(1);
        }

        public void SetErrorCorrectionSoftness(float errorCorrectionSoftness) {
            Sender.PlayerNetworkTransform.ErrorCorrectionSoftness = errorCorrectionSoftness;
            ErrorCorrectionSoftness = errorCorrectionSoftness;
            Dirty = true;
            NotifyIfDirty(1);
        }

        public void SetPredictionReduction(float predictionReduction) {
            Receiver.PredictionReduction = predictionReduction;
            PredictionReduction = predictionReduction;
            Dirty = true;
            NotifyIfDirty(1);
        }

        public void SetDynamicSendRate(bool on) {
            Sender.DynamicSendRate = on;
            DynamicSendRateEnabled = on;
            Dirty = true;
            NotifyIfDirty(1);
        }

        public void SetDebugTrailsEnabled(bool on) {
            Receiver.ShowDebugTrails = on;
            DebugTrailsEnabled = on;
            Dirty = true;
            NotifyIfDirty(1);
        }


        public override void OnPreSerialization() {
            base.OnPreSerialization();
            ErrorCorrectionSoftness = Sender.PlayerNetworkTransform.ErrorCorrectionSoftness;
            ErrorCorrectionDuration = Sender.PlayerNetworkTransform.ErrorCorrectionDuration;
            SendRate = Sender.SendInterval > 0 ? Mathf.RoundToInt(1f / Sender.SendInterval) : 0;
            PredictionReduction = Receiver.PredictionReduction;
            DynamicSendRateEnabled = Sender.DynamicSendRate;
            DebugTrailsEnabled = Receiver.ShowDebugTrails;
        }

        public override void OnDeserialization(DeserializationResult deserializationResult) {
            base.OnDeserialization(deserializationResult);

            SetSendRate(SendRate);
            SetErrorCorrectionDuration(ErrorCorrectionDuration);
            SetErrorCorrectionSoftness(ErrorCorrectionSoftness);
            SetPredictionReduction(PredictionReduction);
            SetDynamicSendRate(DynamicSendRateEnabled);
            SetDebugTrailsEnabled(DebugTrailsEnabled);
            Dirty = true;
            NotifyIfDirty();
        }
    }
}