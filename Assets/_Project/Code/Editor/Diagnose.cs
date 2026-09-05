using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

namespace Infront.EditorTools
{
    /// <summary>
    /// Werkzeug zum Nachsehen, WAS an einer Stelle der Karte steht.
    ///
    /// Entstanden am 2026-09-05: auf den Rundgangsbildern lag im unteren
    /// Drittel der Aussengassen eine pechschwarze Flaeche, und drei Anlaeufe,
    /// sie aus der Szenendatei zu erraten, gingen daneben. Der Fehler dabei
    /// war jedes Mal derselbe: in der YAML-Datei steht m_LocalPosition, also
    /// die Position RELATIV zum Elternobjekt. Fuer alles, was unter "Deko",
    /// "Dach" oder "Map" haengt, ist das nicht die Weltposition.
    ///
    /// Diese Klasse fragt stattdessen Unity selbst nach den Weltkoordinaten.
    /// </summary>
    public static class Diagnose
    {
        [MenuItem("Infront/Diagnose/Was steht in der Westgasse")]
        public static void WasStehtDa()
        {
            // Der Bereich laesst sich ueber Umgebungsvariablen setzen, damit
            // dasselbe Werkzeug fuer jede Stelle der Karte taugt:
            //   DIAG_BOX="xmin,ymin,zmin,xmax,ymax,zmax"

            var pfad = "Assets/_Project/Scenes/Arena.unity";
            var szene = EditorSceneManager.OpenScene(pfad, OpenSceneMode.Single);
            if (!szene.IsValid()) { Debug.Log("DIAG_FEHLER Szene nicht geladen"); return; }

            // Der Bildausschnitt, um den es geht: Westgasse, vom Boden bis
            // knapp ueber Kopfhoehe, im Blickfeld der Kamera d4_rand_west
            // (steht bei x=-30, y=4.25, z=-8 und schaut nach Norden).
            var min = new Vector3(-46f, -0.6f, -12f);
            var max = new Vector3(-32f, 3.2f, 42f);
            var roh = System.Environment.GetEnvironmentVariable("DIAG_BOX");
            if (!string.IsNullOrEmpty(roh))
            {
                var t = roh.Split(',');
                if (t.Length == 6)
                {
                    min = new Vector3(float.Parse(t[0]), float.Parse(t[1]), float.Parse(t[2]));
                    max = new Vector3(float.Parse(t[3]), float.Parse(t[4]), float.Parse(t[5]));
                }
            }
            Debug.Log($"DIAG_BEREICH min({min.x},{min.y},{min.z}) max({max.x},{max.y},{max.z})");
            var kasten = new Bounds();
            kasten.SetMinMax(min, max);

            var treffer = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,
                                                             FindObjectsSortMode.None)
                .Where(r => r.bounds.Intersects(kasten))
                .OrderBy(r => r.bounds.center.x)
                .ToArray();

            Debug.Log($"DIAG_ANZAHL {treffer.Length}");
            foreach (var r in treffer)
            {
                var b = r.bounds;
                var m = r.sharedMaterial;
                var farbe = (m != null && m.HasProperty("_BaseColor"))
                    ? m.GetColor("_BaseColor") : (m != null ? m.color : Color.magenta);
                string pfadImBaum = r.name;
                for (var t = r.transform.parent; t != null; t = t.parent)
                    pfadImBaum = t.name + "/" + pfadImBaum;
                Debug.Log($"DIAG {pfadImBaum} | mitte({b.center.x:F2},{b.center.y:F2},{b.center.z:F2}) "
                          + $"groesse({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) "
                          + $"| mat={(m == null ? "KEINS" : m.name)} "
                          + $"farbe({farbe.r:F2},{farbe.g:F2},{farbe.b:F2}) "
                          + $"| statisch={GameObjectUtility.GetStaticEditorFlags(r.gameObject)}");
            }
            Debug.Log("DIAG_OK");
        }
    }
}
