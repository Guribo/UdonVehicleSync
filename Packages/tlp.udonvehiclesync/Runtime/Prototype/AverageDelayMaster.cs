using TLP.UdonUtils.Runtime;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using VRC.Udon.Common.Enums;

namespace TLP.UdonVehicleSync.TLP.UdonVehicleSync.Runtime.Prototype
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class AverageDelayMaster : TlpBaseBehaviour
    {
        public float syncInterVal = 0.33f;

        [UdonSynced]
        public int syncedOwnerServerTimeMillis;

        [UdonSynced]
        public int syncedSendDelayMillisPrevious;

        [UdonSynced]
        public int syncedDeltaToPreviousOwner;

        private bool m_WasOwner;

        private int m_PreviousSendDelayMillis;
        public int myServerTimeError;
        public string serverTimeErrorName = "serverTimeError";
        public UdonBehaviour toUpdate;

        private int m_MyDeltaToOwner;

        private int m_NextPlayer;

        public Text deltaToPlayer;

        private bool m_UpdateQueued;
        private bool m_PlayerSwitchQueued;

        public override void Start() {
            base.Start();
            EnqueueUpdateOwnerServerTime();
        }

        public void UpdateOwnerServerTime() {
            if (!m_UpdateQueued || m_PlayerSwitchQueued) {
                return;
            }

            m_UpdateQueued = false;

            if (!(Utilities.IsValid(gameObject)
                  && Networking.IsOwner(gameObject))) {
                return;
            }

            RequestSerialization();
        }

        public override void OnPreSerialization() {
            // send the own server time and average send delay to all players
            syncedOwnerServerTimeMillis = Networking.GetServerTimeInMilliseconds() + myServerTimeError;
            syncedSendDelayMillisPrevious = m_PreviousSendDelayMillis;
            syncedDeltaToPreviousOwner = m_MyDeltaToOwner;

            if (Utilities.IsValid(deltaToPlayer)) {
                deltaToPlayer.text = "---";
            }
        }

        public override void OnPostSerialization(SerializationResult result) {
            if (!result.success) {
                Warn("Serialization failed, trying again");
                RequestSerialization();
                return;
            }

            DebugLog($"Serialized {result.byteCount} bytes");

            // calculate a smoothed send delay 
            m_PreviousSendDelayMillis = Mathf.RoundToInt(
                    Mathf.Lerp(
                            m_PreviousSendDelayMillis,
                            Networking.GetServerTimeInMilliseconds() - syncedOwnerServerTimeMillis,
                            0.05f));

            SendCustomEventDelayedSeconds("GoToNextPlayer", syncInterVal, EventTiming.LateUpdate);
            m_PlayerSwitchQueued = true;
            m_WasOwner = true;
        }

        public override void OnDeserialization(DeserializationResult deserializationResult) {
            var localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(localPlayer)
                || Networking.IsOwner(gameObject)) {
                return;
            }

            if (Utilities.IsValid(deltaToPlayer)) {
                var owner = Networking.GetOwner(gameObject);
                if (Utilities.IsValid(owner)) {
                    if (m_WasOwner) {
                        m_MyDeltaToOwner = Networking.GetServerTimeInMilliseconds() -
                                           (syncedOwnerServerTimeMillis + syncedSendDelayMillisPrevious);
                        int deltaChange = syncedDeltaToPreviousOwner - m_MyDeltaToOwner;
                        int avgDeltaToMe = Mathf.RoundToInt(0.5f * (m_MyDeltaToOwner + syncedDeltaToPreviousOwner));
                        myServerTimeError = Mathf.RoundToInt(Mathf.Lerp(myServerTimeError, deltaChange, 0.05f));
                        if (Utilities.IsValid(toUpdate)) {
                            toUpdate.SetProgramVariable(serverTimeErrorName, myServerTimeError);
                        }

                        DebugLog(
                                $"My serverTime error to {owner.playerId}: " + myServerTimeError.ToString("D5") + "ms");
                        deltaToPlayer.text = $"{owner.playerId}: avg. " + avgDeltaToMe.ToString("D5");
                    } else {
                        m_MyDeltaToOwner = Networking.GetServerTimeInMilliseconds() + myServerTimeError -
                                           (syncedOwnerServerTimeMillis + syncedSendDelayMillisPrevious);
                        deltaToPlayer.text = $"{owner.playerId}: " + m_MyDeltaToOwner.ToString("D5");
                    }
                }
            }

            m_WasOwner = false;
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player) {
            var localPlayer = Networking.LocalPlayer;
            if (!(Utilities.IsValid(localPlayer)
                  && Utilities.IsValid(player)
                  && player.playerId == localPlayer.playerId)) {
                return;
            }

            SendCustomEventDelayedSeconds("UpdateOwnerServerTime", syncInterVal, EventTiming.LateUpdate);
            m_UpdateQueued = true;
        }

        public void GoToNextPlayer() {
            if (!m_PlayerSwitchQueued) {
                return;
            }

            m_PlayerSwitchQueued = false;

            if (!(Utilities.IsValid(gameObject)
                  && Networking.IsOwner(gameObject))) {
                return;
            }

            var playerCount = VRCPlayerApi.GetPlayerCount();
            var allPlayers = new VRCPlayerApi[playerCount];
            VRCPlayerApi.GetPlayers(allPlayers);

            if (playerCount < 2) {
                return;
            }

            m_NextPlayer = (m_NextPlayer + 1) % playerCount;
            var nextPlayerApi = allPlayers[m_NextPlayer];
            if (!Utilities.IsValid(nextPlayerApi)) {
                // try again next frame
                SendCustomEventDelayedFrames("GoToNextPlayer", 1, EventTiming.LateUpdate);
                m_PlayerSwitchQueued = true;
                return;
            }

            Networking.SetOwner(nextPlayerApi, gameObject);
            EnqueueUpdateOwnerServerTime();
        }

        public override void OnPlayerJoined(VRCPlayerApi player) {
            if (!(Utilities.IsValid(gameObject)
                  && Networking.IsOwner(gameObject))) {
                return;
            }

            EnqueueUpdateOwnerServerTime();
        }

        public override void OnPlayerLeft(VRCPlayerApi player) {
            if (!(Utilities.IsValid(gameObject)
                  && Networking.IsOwner(gameObject))) {
                return;
            }

            EnqueueUpdateOwnerServerTime();
        }

        private void EnqueueUpdateOwnerServerTime() {
            SendCustomEventDelayedSeconds("UpdateOwnerServerTime", syncInterVal, EventTiming.LateUpdate);
            m_UpdateQueued = true;
        }
    }
}