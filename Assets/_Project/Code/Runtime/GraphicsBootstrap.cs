using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Setzt beim Start harte Grafik-Leitplanken: VSync an (gegen zerrissenes
    /// Bild) und Zielbildrate 60. Laeuft vor der ersten Szene.
    /// </summary>
    public static class GraphicsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
        }
    }
}
