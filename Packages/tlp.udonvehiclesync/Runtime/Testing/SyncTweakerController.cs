using TLP.UdonUtils.Runtime.DesignPatterns.MVC;
using UdonSharp;
using VRC.SDKBase;

namespace TLP.UdonVehicleSync.Runtime.Testing
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Any)]
    public class SyncTweakerController : Controller
    {
        private SyncTweaker _ui;
        private SyncTweakerModel _model;

        protected override bool InitializeInternal() {
            if (!base.InitializeInternal()) {
                return false;
            }

            _ui = (SyncTweaker)View;
            _model = (SyncTweakerModel)Model;

            return _ui && _model;
        }

        public void UpdateSendRate(int sendRate) {
            if (!Networking.IsOwner(_model.gameObject)) {
                Networking.SetOwner(Networking.LocalPlayer, _model.gameObject);
            }

            _model.SetSendRate(sendRate);
            _model.MarkNetworkDirty();
        }

        public void UpdatePositionSmoothing(float smoothTime) {
            if (!Networking.IsOwner(_model.gameObject)) {
                Networking.SetOwner(Networking.LocalPlayer, _model.gameObject);
            }

            _model.SetErrorCorrectionSoftness(smoothTime);
            _model.MarkNetworkDirty();
        }

        public void UpdateVelocitySmoothing(float smoothTime) {
            if (!Networking.IsOwner(_model.gameObject)) {
                Networking.SetOwner(Networking.LocalPlayer, _model.gameObject);
            }

            _model.SetErrorCorrectionDuration(smoothTime);
            _model.MarkNetworkDirty();
        }

        public void UpdatePredictionReduction(float predictionReduction) {
            if (!Networking.IsOwner(_model.gameObject)) {
                Networking.SetOwner(Networking.LocalPlayer, _model.gameObject);
            }

            _model.SetPredictionReduction(predictionReduction);
            _model.MarkNetworkDirty();
        }

        public void UpdateDynamicSendRateEnabled(bool on) {
            if (!Networking.IsOwner(_model.gameObject)) {
                Networking.SetOwner(Networking.LocalPlayer, _model.gameObject);
            }

            _model.SetDynamicSendRate(on);
            _model.MarkNetworkDirty();
        }

        public void UpdateDebugTrailsEnabled(bool on) {
            if (!Networking.IsOwner(_model.gameObject)) {
                Networking.SetOwner(Networking.LocalPlayer, _model.gameObject);
            }

            _model.SetDebugTrailsEnabled(on);
            _model.MarkNetworkDirty();
        }
    }
}