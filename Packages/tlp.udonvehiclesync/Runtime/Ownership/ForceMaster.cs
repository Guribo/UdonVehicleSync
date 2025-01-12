using JetBrains.Annotations;
using TLP.UdonUtils.Runtime;
using TLP.UdonUtils.Runtime.Extensions;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace TLP.UdonVehicleSync.Runtime.Ownership
{
    /// <summary>
    /// Script that continuously ensures that the current master has ownership.
    /// Note: Runs in Update().
    ///       Only the current master player transfers ownership.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DefaultExecutionOrder(ExecutionOrder)]
    [TlpDefaultExecutionOrder(typeof(ForceMaster), ExecutionOrder)]
    public class ForceMaster : TlpBaseBehaviour
    {
        #region ExecutionOrder
        public override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = ForceNotMaster.ExecutionOrder + 1;
#endregion
        public void Update() {
            MasterTakeOwnership();
        }

        #region Internal

        /// <summary>
        /// If currently master the local player will take ownership unless it already owns this GameObject.
        /// </summary>
        private void MasterTakeOwnership() {
            var owner = Networking.GetOwner(gameObject);
            if (owner.IsMasterSafe()) {
                return;
            }

            if (!Networking.LocalPlayer.IsMasterSafe()) {
                return;
            }

            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        #endregion
    }
}