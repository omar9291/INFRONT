using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Setzt beim Start harte Grafik-Leitplanken: VSync an (gegen zerrissenes
    /// Bild), Zielbildrate 60 und den Anzeige-Modus (Vollbild oder Fenster) aus
    /// den <see cref="GameSettings"/>. Laeuft vor der ersten Szene.
    /// </summary>
    public static class GraphicsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;
            ApplyDisplayMode();
        }

        /// <summary>
        /// Schaltet das Spielfenster auf Vollbild (randloses Fenster in
        /// Bildschirmgroesse) oder zurueck auf ein 1280x720-Fenster. Unity merkt
        /// sich sonst die zuletzt genutzte Fenstergroesse - deshalb setzen wir
        /// sie hier bei jedem Start neu. Der Menue-Schalter ruft das ebenfalls auf.
        /// Im Editor bewusst wirkungslos, damit Tests das Game-View nicht umbauen.
        /// </summary>
        public static void ApplyDisplayMode()
        {
#if !UNITY_EDITOR
            if (GameSettings.DisplayMode == GameSettings.Anzeige.Vollbild)
            {
                int w = Display.main.systemWidth;
                int h = Display.main.systemHeight;
                Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            }
#endif
        }
    }
}
