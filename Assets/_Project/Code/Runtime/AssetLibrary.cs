using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Die eine Stelle, die nach echten Asset-Dateien sucht - genau nach dem
    /// Muster von <see cref="AudioService"/>: liegt eine Datei da, wird sie
    /// benutzt; sonst baut der Aufrufer weiter seine Code-Geometrie.
    ///
    /// Ablage (alles unter einem Ordner namens "Resources", damit
    /// <see cref="Resources.Load"/> es findet):
    ///
    ///   Assets/_Project/Art/Resources/Models/&lt;key&gt;.prefab   -> <see cref="Model"/>
    ///   Assets/_Project/Art/Resources/Materials/&lt;key&gt;.mat    -> <see cref="Surface"/>
    ///
    /// Fehlt eine Datei, kommt null zurueck und der Aufrufer nutzt seinen
    /// bisherigen Weg. Nichts am alten Code wird geloescht - er ist die
    /// Rueckfallebene. Gefaellt ein Modell nicht: Datei loeschen, alter Stand
    /// ist zurueck (bei Deko: danach SceneBuilder.Build neu laufen).
    ///
    /// <see cref="RealCount"/> / <see cref="FallbackCount"/> zaehlen mit, wie
    /// viel echt und wie viel Rueckfall ist - fuer die Tests und den Bericht.
    /// </summary>
    public static class AssetLibrary
    {
        const string ModelsPrefix = "Models/";
        const string MaterialsPrefix = "Materials/";

        /// <summary>Wie oft eine echte Datei gefunden wurde (seit letztem Reset).</summary>
        public static int RealCount { get; private set; }
        /// <summary>Wie oft auf die Code-Geometrie zurueckgefallen wurde.</summary>
        public static int FallbackCount { get; private set; }

        /// <summary>Setzt die Zaehler zurueck (Tests, Bericht-Lauf).</summary>
        public static void ResetCounts()
        {
            RealCount = 0;
            FallbackCount = 0;
        }

        /// <summary>
        /// Prefab/Modell unter Resources/Models/&lt;key&gt;. null, wenn es die
        /// Datei nicht gibt. Zaehlt den Treffer bzw. Fehlschlag mit.
        /// </summary>
        public static GameObject Model(string key)
        {
            var go = Resources.Load<GameObject>(ModelsPrefix + key);
            if (go != null) RealCount++;
            else FallbackCount++;
            return go;
        }

        /// <summary>
        /// Material unter Resources/Materials/&lt;key&gt;. null, wenn es die
        /// Datei nicht gibt. Zaehlt den Treffer bzw. Fehlschlag mit.
        /// </summary>
        public static Material Surface(string key)
        {
            var m = Resources.Load<Material>(MaterialsPrefix + key);
            if (m != null) RealCount++;
            else FallbackCount++;
            return m;
        }

        /// <summary>
        /// Nur pruefen, ob es ein Modell gibt - ohne die Zaehler zu bewegen.
        /// Gedacht fuer SceneBuilder: erst fragen, dann entweder echtes Modell
        /// einsetzen oder die Wuerfel bauen.
        /// </summary>
        public static bool HasModel(string key)
            => Resources.Load<GameObject>(ModelsPrefix + key) != null;

        /// <summary>Wie <see cref="HasModel"/>, aber fuer Materialien.</summary>
        public static bool HasSurface(string key)
            => Resources.Load<Material>(MaterialsPrefix + key) != null;

        /// <summary>
        /// Instanziert ein Deko-Modell an Ort und Stelle. null, wenn es die
        /// Datei nicht gibt; dann baut der Aufrufer seine Geometrie.
        ///
        /// Die Prefabs unter Resources/Models/ sind vom Import-Werkzeug bereits
        /// von Collidern befreit - hier wird nichts mehr entfernt (das ginge im
        /// Editor-Baumodus auch gar nicht sauber).
        /// </summary>
        public static GameObject SpawnModel(string key, Transform parent,
                                            Vector3 position, Quaternion rotation, float scale = 1f)
        {
            var prefab = Model(key);
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab, position, rotation, parent);
            go.name = prefab.name;
            if (!Mathf.Approximately(scale, 1f))
                go.transform.localScale = prefab.transform.localScale * scale;

            return go;
        }
    }
}
