using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Die Hitbox-Ebene (6) kollidiert mit nichts physisch - sie ist nur fuer
    /// Raycasts da. So blockieren Trefferflaechen weder Bewegung noch andere
    /// Figuren.
    /// </summary>
    public static class PhysicsBootstrap
    {
        public const int HitboxLayer = 6;
        public const int CharacterLayer = 7;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(HitboxLayer, i, true);
        }
    }
}
