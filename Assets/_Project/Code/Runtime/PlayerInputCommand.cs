using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Ein Eingabe-Kommando, das der besitzende Client an den Server schickt.
    /// Der Server allein rechnet daraus Bewegung und Blickrichtung aus
    /// (server-autoritativ).
    /// </summary>
    public struct PlayerInputCommand : INetworkSerializable
    {
        public Vector2 Move;
        public float Yaw;
        public float Pitch;
        public bool Sprint;
        public bool Jump;
        public bool Aim;      // rechte Maustaste: ueber Kimme/Korn zielen
        public bool Crouch;   // Strg: ducken
        public bool Walk;     // Alt: schleichen

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Move);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Pitch);
            serializer.SerializeValue(ref Sprint);
            serializer.SerializeValue(ref Jump);
            serializer.SerializeValue(ref Aim);
            serializer.SerializeValue(ref Crouch);
            serializer.SerializeValue(ref Walk);
        }
    }
}
