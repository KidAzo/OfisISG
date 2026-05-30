using System;
using UnityEngine.Events;

namespace Woi.OfficeFire
{
    [Serializable]
    public class ServerRoomStateChangedEvent : UnityEvent<ServerRoomState> { }
}
