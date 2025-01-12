using JetBrains.Annotations;
using TLP.UdonUtils.Runtime;
using TLP.UdonUtils.Runtime.Extensions;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace TLP.UdonVehicleSync.Runtime.Testing
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DefaultExecutionOrder(ExecutionOrder)]
    [TlpDefaultExecutionOrder(typeof(SyncRateTester), ExecutionOrder)]
    public class SyncRateTester : TlpBaseBehaviour
    {
        #region ExecutionOrder
        public override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = TlpExecutionOrder.TestingStart + 200;
        #endregion

        [UdonSynced]
        public int FrameCount;

        public void Update() {
            if (Networking.IsOwner(gameObject)) {
                FrameCount = Time.frameCount;
                MarkNetworkDirty();
                RequestSerialization();
            }
        }

        public override void OnDeserialization(DeserializationResult deserializationResult) {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog(
                    $"{nameof(OnDeserialization)} received frame {FrameCount}, latency = {deserializationResult.Latency()} seconds"
            );
#endif
            #endregion
        }
    }
}