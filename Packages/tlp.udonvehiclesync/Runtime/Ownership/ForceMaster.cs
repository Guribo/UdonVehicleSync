using TLP.UdonUtils;
using TLP.UdonUtils.Extensions;
using UdonSharp;
using VRC.SDKBase;

namespace TLP.UdonVehicleSync.Runtime.Ownership
{
    /// <summary>
    /// Script that continuously ensures that the current master has ownership.
    /// Note: Runs in Update().
    ///       Only the current master player transfers ownership.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ForceMaster : TlpBaseBehaviour
    {
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