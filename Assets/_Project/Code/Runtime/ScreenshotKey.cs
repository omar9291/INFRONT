using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Infront
{
    /// <summary>
    /// F9 speichert einen Screenshot direkt aus dem Spiel heraus - ohne
    /// System-Screenshot-Berechtigung. Bilder landen in
    /// &lt;Projekt&gt;/Screenshots/. Nur ein Hilfsmittel fuer die Entwicklung.
    /// </summary>
    public sealed class ScreenshotKey : MonoBehaviour
    {
        const string Dir = "/Users/user/UnityProjects/INFRONT/Screenshots";
        int _count;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.f9Key.wasPressedThisFrame)
                return;

            Directory.CreateDirectory(Dir);
            string path = Path.Combine(Dir, $"shot_{System.DateTime.Now:HHmmss}_{_count++}.png");
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"[Infront] Screenshot: {path}");
        }
    }
}
