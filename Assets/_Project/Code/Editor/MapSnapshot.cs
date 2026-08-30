using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Infront.EditorTools
{
    /// <summary>
    /// Zeichnet die Arena als 2D-Grundriss (Draufsicht) direkt in ein Bild -
    /// keine Kamera, kein Licht, jeder Block sichtbar. Farbe nach Bauteil-Typ.
    /// Headless: Unity -batchmode -quit -executeMethod Infront.EditorTools.MapSnapshot.Capture
    /// </summary>
    public static class MapSnapshot
    {
        const string ScenePath = "Assets/_Project/Scenes/Arena.unity";
        const string OutPath = "/Users/user/UnityProjects/INFRONT/Screenshots/map_topdown.png";

        const int Px = 1024;
        const float WorldHalf = 34f; // -34..34 -> ganze Karte plus Rand

        [MenuItem("Infront/Karte/Grundriss speichern")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var tex = new Texture2D(Px, Px, TextureFormat.RGB24, false);
            Fill(tex, new Color(0.62f, 0.64f, 0.66f)); // Boden

            var map = GameObject.Find("Map");
            if (map != null)
                foreach (Transform child in map.transform)
                    DrawBox(tex, child, ColorForName(child.name));

            // Spawn-Punkte markieren
            var spawns = GameObject.Find("SpawnPoints");
            if (spawns != null)
                foreach (Transform sp in spawns.transform)
                {
                    var tm = sp.GetComponent<SpawnPoint>();
                    var c = tm != null && tm.TeamId == Team.Alpha
                        ? new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.35f, 0.3f);
                    Dot(tex, sp.position, 6, c);
                }

            File.WriteAllBytes(OutPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"MAP_SNAPSHOT_OK {OutPath}");
        }

        static Color ColorForName(string n)
        {
            if (n.Contains("Wall") || n.Contains("Screen")) return new Color(0.12f, 0.14f, 0.22f);
            if (n.Contains("Platform")) return new Color(0.2f, 0.7f, 0.7f);
            if (n.Contains("Ramp")) return new Color(0.55f, 0.75f, 0.8f);
            if (n.Contains("Crate") || n.Contains("Cover")) return new Color(0.85f, 0.45f, 0.15f);
            return new Color(0.4f, 0.4f, 0.45f);
        }

        static void DrawBox(Texture2D tex, Transform t, Color c)
        {
            // Weltbereich in X/Z aus Position und Skalierung
            float hx = t.lossyScale.x * 0.5f, hz = t.lossyScale.z * 0.5f;
            Vector3 p = t.position;
            int x0 = ToPx(p.x - hx), x1 = ToPx(p.x + hx);
            int z0 = ToPx(p.z - hz), z1 = ToPx(p.z + hz);
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
            for (int y = Mathf.Min(z0, z1); y <= Mathf.Max(z0, z1); y++)
                if (x >= 0 && x < Px && y >= 0 && y < Px)
                    tex.SetPixel(x, y, c);
        }

        static void Dot(Texture2D tex, Vector3 world, int r, Color c)
        {
            int cx = ToPx(world.x), cy = ToPx(world.z);
            for (int x = -r; x <= r; x++)
            for (int y = -r; y <= r; y++)
                if (x * x + y * y <= r * r)
                {
                    int px = cx + x, py = cy + y;
                    if (px >= 0 && px < Px && py >= 0 && py < Px) tex.SetPixel(px, py, c);
                }
        }

        static int ToPx(float world) => Mathf.RoundToInt((world + WorldHalf) / (WorldHalf * 2f) * Px);

        static void Fill(Texture2D tex, Color c)
        {
            var px = new Color[Px * Px];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels(px);
        }
    }
}
