namespace Infront
{
    /// <summary>
    /// Alle Faehigkeiten des Spiels. Ueber das Netz geht nur diese Zahl.
    /// Reihenfolge nicht aendern (koennte spaeter in Speicherdaten stehen).
    /// </summary>
    public enum AbilityKind
    {
        Keine = 0,
        Rauchwand = 1,      // blockiert Sicht (auch die der Bots)
        Blendgranate = 2,   // weisser Bildschirm / Bots zielen daneben
        Splittergranate = 3,
        ScanPuls = 4,
        Brandwand = 5,
        Stolperdraht = 6,
    }

    /// <summary>Auf welche Taste die Faehigkeit gelegt wird: Q, F oder G.</summary>
    public enum AbilitySlot
    {
        Q = 0,
        F = 1,
        G = 2,
    }
}
