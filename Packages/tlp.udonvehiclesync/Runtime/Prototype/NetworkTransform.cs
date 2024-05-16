// using UdonSharp;
// using UnityEngine;
//
// public class NetworkTransform : UdonSharpBehaviour
// {
//     private bool _enableTransformation = true;
//
//     public double InterpolationDelay = 0.2;
//
//     // We store twenty states with "playback" information
//     private readonly State[] m_BufferedState = new State[20];
//
//     // Keep track of what slots are used
//     private int m_TimestampCount;
//     private Rigidbody rigid;
//
//
//     public void SetUpdateTransformToActive(bool active)
//     {
//         _enableTransformation = active;
//     }
//
//     private void Update()
//     {
//         // if (photonView.isMine || !PhotonNetwork.inRoom)
//         // return; // if this object is under our control, we don't need to apply received position-updates 
//
//         var currentTime = Time.timeSinceLevelLoad; // PhotonNetwork.time;
//         var interpolationTime = currentTime - InterpolationDelay;
//
//         // We have a window of InterpolationDelay where we basically play back old updates.
//         // By having InterpolationDelay the average ping, you will usually use interpolation.
//         // And only if no more data arrives we will use the latest known position.
//
//         // Use interpolation, if the interpolated time is still "behind" the update timestamp time:
//         if (m_BufferedState[0].timestamp > interpolationTime)
//         {
//             for (var i = 0; i < m_TimestampCount; i++)
//                 // Find the state which matches the interpolation time (time+0.1) or use last state
//                 if (m_BufferedState[i].timestamp <= interpolationTime || i == m_TimestampCount - 1)
//                 {
//                     // The state one slot newer (<100ms) than the best playback state
//                     var rhs = m_BufferedState[Mathf.Max(i - 1, 0)];
//                     // The best playback state (closest to 100 ms old (default time))
//                     var lhs = m_BufferedState[i];
//
//                     // Use the time between the two slots to determine if interpolation is necessary
//                     var diffBetweenUpdates = rhs.timestamp - lhs.timestamp;
//                     var t = 0.0F;
//                     // As the time difference gets closer to 100 ms t gets closer to 1 in 
//                     // which case rhs is only used
//                     if (diffBetweenUpdates > 0.0001)
//                         t = (float) ((interpolationTime - lhs.timestamp) / diffBetweenUpdates);
//
//                     if (_enableTransformation)
//                     {
//                         // if t=0 => lhs is used directly
//                         transform.localPosition = Vector3.Lerp(lhs.pos, rhs.pos, t);
//                         transform.localRotation = Quaternion.Slerp(lhs.rot, rhs.rot, t);
//
//
//                         if (rigid != null)
//                         {
//                             rigid.velocity = Vector3.Lerp(lhs.vel, rhs.vel, t);
//                             rigid.angularVelocity = Vector3.Lerp(lhs.angularVel, rhs.angularVel, t);
//                         }
//                     }
//
//                     return;
//                 }
//         }
//         else
//         {
//             // If our interpolation time catched up with the time of the latest update:
//             // Simply move to the latest known position.
//
//             //Debug.Log("Lerping!");
//             var latest = m_BufferedState[0];
//
//             if (_enableTransformation)
//             {
//                 transform.localPosition = Vector3.Lerp(transform.localPosition, latest.pos, Time.deltaTime * 20);
//                 transform.localRotation = latest.rot;
//
//                 if (rigid != null)
//                 {
//                     rigid.velocity = Vector3.Lerp(rigid.velocity, latest.vel, Time.deltaTime * 20);
//                     rigid.angularVelocity =
//                         Vector3.Lerp(rigid.angularVelocity, latest.angularVel, Time.deltaTime * 20);
//                 }
//             }
//         }
//     }
//
//     public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
//     {
//         if (_enableTransformation)
//         {
//             // Always send transform (depending on reliability of the network view)
//             if (stream.isWriting)
//             {
//                 stream.SendNext(transform.localPosition);
//                 stream.SendNext(transform.localRotation);
//
//                 if (rigid)
//                 {
//                     stream.SendNext(rigid.velocity);
//                     stream.SendNext(rigid.angularVelocity);
//                 }
//
//
//                 //stream.SendNext(Environment.TickCount);
//                 return;
//             }
//
//             // When receiving, buffer the information
//             // Receive latest state information
//             // receive local position
//             Vector3 pos = (Vector3) stream.ReceiveNext();
//
//             // receive local rotation
//             object localRotObj = stream.ReceiveNext();
//             Quaternion rot = Quaternion.identity;
//
//             if (localRotObj != null)
//             {
//                 if (localRotObj is Quaternion)
//                 {
//                     rot = (Quaternion) localRotObj;
//                 }
//                 else
//                 {
//                     Debug.LogWarning("RotObj is not Quaternion! It is of type: " + localRotObj.GetType());
//                 }
//             }
//             else
//             {
//                 Debug.LogWarning("received localRotObj is null!!!");
//             }
//
//             Vector3 vel = Vector3.zero;
//             Vector3 angularVel = Vector3.zero;
//
//
//             if (rigid)
//             {
//                 vel = (Vector3) stream.ReceiveNext();
//                 angularVel = (Vector3) stream.ReceiveNext();
//             }
//
//             //int localTimeOfSend = (int)stream.ReceiveNext();
//             //Debug.Log("timeDiff" + (Environment.TickCount - localTimeOfSend) + " update age: " + (PhotonNetwork.time - info.timestamp));
//
//             // Shift buffer contents, oldest data erased, 18 becomes 19, ... , 0 becomes 1
//             for (var i = m_BufferedState.Length - 1; i >= 1; i--)
//             {
//                 m_BufferedState[i] = m_BufferedState[i - 1];
//             }
//
//
//             // Save currect received state as 0 in the buffer, safe to overwrite after shifting
//             State state;
//             state.timestamp = info.timestamp;
//             state.pos = pos;
//             state.rot = rot;
//             state.vel = vel;
//             state.angularVel = angularVel;
//             m_BufferedState[0] = state;
//
//             // Increment state count but never exceed buffer size
//             m_TimestampCount = Mathf.Min(m_TimestampCount + 1, m_BufferedState.Length);
//
//             // Check integrity, lowest numbered state in the buffer is newest and so on
//             for (var i = 0; i < m_TimestampCount - 1; i++)
//                 if (m_BufferedState[i].timestamp < m_BufferedState[i + 1].timestamp)
//                     Debug.Log("State inconsistent");
//         }
//     }
//
//     internal struct State
//     {
//         internal double timestamp;
//         internal Vector3 pos;
//         internal Quaternion rot;
//         internal Vector3 vel;
//         internal Vector3 angularVel;
//     }
// }