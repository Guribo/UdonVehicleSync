using TLP.UdonUtils.Runtime;
using TLP.UdonUtils.Runtime.Extensions;
using UdonSharp;
using VRC.SDKBase;

namespace TLP.UdonVehicleSync.Runtime.Ownership
{
    /// <summary>
    /// Script that continuously ensures that the first found non-master player takes ownership.
    /// Note: Runs in Update() and uses the list of players to find the new owner when needed.
    ///       Only the current master player transfers ownership.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ForceNotMaster : TlpBaseBehaviour
    {
        public void Update() {
            MasterGiveAwayOwnership();
        }

        #region Internal
        /// <summary>
        /// If currently master the local player will find the first non-master player and transfers the ownership.
        /// No-op in single player-mode.
        /// </summary>
        private void MasterGiveAwayOwnership() {
            var owner = Networking.GetOwner(gameObject);
            if (!owner.IsMasterSafe()) {
                return;
            }

            if (!Networking.LocalPlayer.IsMasterSafe() || VRCPlayerApi.GetPlayerCount() <= 1) {
                return;
            }

            var players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            players = VRCPlayerApi.GetPlayers(players);

            foreach (var player in players) {
                if (TryTransferToNonMaster(player)) {
                    return;
                }
            }
        }

        private bool TryTransferToNonMaster(VRCPlayerApi player) {
            if (!Utilities.IsValid(player)) return false;
            if (player.isMaster) return false;

            Networking.SetOwner(player, gameObject);
            return true;
        }
        #endregion
    }
}