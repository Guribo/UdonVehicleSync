using JetBrains.Annotations;
using TLP.UdonUtils.Runtime.DesignPatterns.MVC;
using TLP.UdonUtils.Runtime.Events;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace TLP.UdonVehicleSync.Runtime.Testing
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Any)]
    [DefaultExecutionOrder(ExecutionOrder)]
    [TlpDefaultExecutionOrder(typeof(SyncTweaker), ExecutionOrder)]
    public class SyncTweaker : View
    {
        #region ExecutionOrder
        public override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = View.ExecutionOrder + 20;
        #endregion
        [FormerlySerializedAs("UiChanged")]
        [SerializeField]
        private UiEvent UiChangedEvent;

        #region Ui Elements
        [SerializeField]
        private Slider SendRateSlider;

        [SerializeField]
        private TextMeshProUGUI SendRateText;

        [FormerlySerializedAs("VelocitySmoothingSlider")]
        [SerializeField]
        private Slider ErrorCorrectionDurationSlider;

        [FormerlySerializedAs("VelocitySmoothingText")]
        [SerializeField]
        private TextMeshProUGUI ErrorCorrectionDurationText;

        [FormerlySerializedAs("PositionSmoothingSlider")]
        [SerializeField]
        private Slider ErrorCorrectionSoftnessSlider;

        [FormerlySerializedAs("PositionSmoothingText")]
        [SerializeField]
        private TextMeshProUGUI ErrorCorrectionSoftnessText;

        [SerializeField]
        private Slider PredictionReductionSlider;

        [SerializeField]
        private TextMeshProUGUI PredictionReductionText;

        [SerializeField]
        private Toggle DynamicRateToggle;

        [SerializeField]
        private Toggle DebugTrailToggle;
        #endregion

        #region MVC Components
        private SyncTweakerController _controller;
        public SyncTweakerModel SyncTweakerModel;
        #endregion

        #region State
        private bool _skipUiEventHandling;
        #endregion

        #region Unity Event Callbacks
        /// <summary>
        /// called by UI sliders when they change
        /// </summary>
        [PublicAPI]
        public void OnUiChanged() {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog(nameof(OnUiChanged));
#endif
            #endregion

            if (_skipUiEventHandling) {
                DebugLog("UiEvents are suppressed for the duration of the update");
                return;
            }

            if (!HasStartedOk) {
                Error($"{nameof(OnUiChanged)}: Not initialized");
                return;
            }

            _controller.UpdateSendRate(Mathf.RoundToInt(SendRateSlider.value));
            _controller.UpdateVelocitySmoothing(ErrorCorrectionDurationSlider.value);
            _controller.UpdatePositionSmoothing(ErrorCorrectionSoftnessSlider.value);
            _controller.UpdatePredictionReduction(PredictionReductionSlider.value);
            _controller.UpdateDynamicSendRateEnabled(DynamicRateToggle.isOn);
            _controller.UpdateDebugTrailsEnabled(DebugTrailToggle.isOn);
        }
        #endregion

        #region Hook Implementations
        #region TLP Base Behaviour
        protected override bool SetupAndValidate() {
            if (!base.SetupAndValidate()) {
                return false;
            }

            if (!InitializeMvc(
                        SyncTweakerModel,
                        gameObject.GetComponent<View>(),
                        gameObject.GetComponent<Controller>(),
                        gameObject.GetComponent<UdonEvent>()
                )) {
                Error("Model-View-Controller initialization failed");
                return false;
            }

            Model.Dirty = true;
            Model.NotifyIfDirty(1);
            return true;
        }

        public override void OnEvent(string eventName) {
            switch (eventName) {
                case nameof(OnUiChanged):

                    #region TLP_DEBUG
#if TLP_DEBUG
                    DebugLog($"{nameof(OnEvent)} {eventName}");
#endif
                    #endregion

                    OnUiChanged();
                    break;
                default:
                    base.OnEvent(eventName);
                    break;
            }
        }
        #endregion

        #region Model View Controller
        protected override bool InitializeInternal() {
            if (!base.InitializeInternal()) {
                return false;
            }

            if (!UiChangedEvent) {
                Error($"{nameof(UiChangedEvent)} not set");
                return false;
            }

            if (!UiChangedEvent.AddListenerVerified(this, nameof(OnUiChanged))) {
                return false;
            }

            _controller = (SyncTweakerController)Controller;
            if (!_controller) {
                Error($"Controller is not a {nameof(SyncTweakerController)}");
                return false;
            }


            if (!SendRateSlider) {
                Error($"{nameof(SendRateSlider)} not set");
                return false;
            }

            if (!SendRateText) {
                Error($"{nameof(SendRateText)} not set");
                return false;
            }

            SendRateSlider.minValue = 1;
            SendRateSlider.maxValue = 20;
            SendRateSlider.wholeNumbers = true;
            SendRateText.text = "0";

            if (!ErrorCorrectionDurationSlider) {
                Error($"{nameof(ErrorCorrectionDurationSlider)} not set");
                return false;
            }

            if (!ErrorCorrectionDurationText) {
                Error($"{nameof(ErrorCorrectionDurationText)} not set");
                return false;
            }

            ErrorCorrectionDurationSlider.minValue = 0;
            ErrorCorrectionDurationSlider.maxValue = 1f;
            ErrorCorrectionDurationSlider.wholeNumbers = false;
            ErrorCorrectionDurationText.text = "0.0";


            if (!ErrorCorrectionSoftnessSlider) {
                Error($"{nameof(ErrorCorrectionSoftnessSlider)} not set");
                return false;
            }

            if (!ErrorCorrectionSoftnessText) {
                Error($"{nameof(ErrorCorrectionSoftnessText)} not set");
                return false;
            }

            ErrorCorrectionSoftnessSlider.minValue = 0;
            ErrorCorrectionSoftnessSlider.maxValue = 1f;
            ErrorCorrectionSoftnessSlider.wholeNumbers = false;
            ErrorCorrectionSoftnessText.text = "0.0";

            if (!PredictionReductionSlider) {
                Error($"{nameof(PredictionReductionSlider)} not set");
                return false;
            }

            if (!PredictionReductionText) {
                Error($"{nameof(PredictionReductionText)} not set");
                return false;
            }

            PredictionReductionSlider.minValue = 0;
            PredictionReductionSlider.maxValue = 1;
            PredictionReductionSlider.wholeNumbers = false;
            PredictionReductionText.text = "0.0";

            if (!DynamicRateToggle) {
                Error($"{nameof(DynamicRateToggle)} not set");
                return false;
            }

            if (!DebugTrailToggle) {
                Error($"{nameof(DebugTrailToggle)} not set");
                return false;
            }


            return true;
        }

        protected override bool DeInitializeInternal() {
            if (!UiChangedEvent) {
                Error($"{nameof(UiChangedEvent)} not set");
                base.DeInitializeInternal();
                return false;
            }

            UiChangedEvent.RemoveListener(this);

            return base.DeInitializeInternal();
        }

        public override void OnModelChanged() {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog(nameof(OnModelChanged));
#endif
            #endregion

            _skipUiEventHandling = true;

            SendRateText.text = $"{SyncTweakerModel.SendRate}";
            SendRateSlider.value = SyncTweakerModel.SendRate;

            ErrorCorrectionDurationText.text = $"{SyncTweakerModel.ErrorCorrectionDuration:F3}";
            ErrorCorrectionDurationSlider.value = SyncTweakerModel.ErrorCorrectionDuration;

            ErrorCorrectionSoftnessText.text = $"{SyncTweakerModel.ErrorCorrectionSoftness:F3}";
            ErrorCorrectionSoftnessSlider.value = SyncTweakerModel.ErrorCorrectionSoftness;

            PredictionReductionText.text = $"{SyncTweakerModel.PredictionReduction:F3}";
            PredictionReductionSlider.value = SyncTweakerModel.PredictionReduction;

            DynamicRateToggle.isOn = SyncTweakerModel.DynamicSendRateEnabled;
            DebugTrailToggle.isOn = SyncTweakerModel.DebugTrailsEnabled;
            _skipUiEventHandling = false;
        }
        #endregion
        #endregion
    }
}