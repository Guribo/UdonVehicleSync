using TLP.UdonUtils;
using TLP.UdonUtils.Extensions;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace TLP.UdonVehicleSync.Runtime.Testing
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class SyncRateTester : TlpBaseBehaviour
    {
        [UdonSynced]
        public int FrameCount;

        public void Update()
        {
            if (Networking.IsOwner(gameObject))
            {
                FrameCount = Time.frameCount;
                MarkNetworkDirty();
                RequestSerialization();
            }
        }

        public override void OnDeserialization(DeserializationResult deserializationResult)
        {
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