using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Ein Eingabe-Kommando, das der besitzende Client an den Server schickt.
    /// Der Server allein rechnet daraus Bewegung aus (server-autoritativ).
    /// </summary>
    public struct PlayerInputCommand : INetworkSerializable
    {
        public Vector2 Move;
        public float Yaw;
        public bool Sprint;
        public bool Jump;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Sprint);
            serializer.SerializeValue(ref Jump);
        }
    }
}
