namespace Infront
{
    /// <summary>
    /// Jeder eigenständige Ton im Spiel. Der Name eines Eintrags ist zugleich
    /// der Dateiname zum Austauschen: legst du
    /// <c>Assets/_Project/Audio/Resources/&lt;name&gt;.wav</c> ab, benutzt der
    /// <see cref="AudioService"/> automatisch deine Datei statt des
    /// Platzhalter-Tons. Der Dateiname ist die Kleinschreibung des Eintrags,
    /// zum Beispiel <c>schuss_gewehr.wav</c> für <see cref="SchussGewehr"/>.
    ///
    /// Reihenfolge egal - hier steht kein Netz-Index drin.
    /// </summary>
    public enum SoundId
    {
        // --- Waffen ---
        SchussGewehr,
        SchussMp,
        SchussSniper,
        SchussPistole,
        SchussFern,        // tiefer, rollender Nachhall eines weit entfernten Schusses
        Zischen,           // eine Kugel fliegt dicht am Kopf vorbei
        Nachladen,
        WaffeWechsel,

        // --- Treffer / Kampf (nur beim lokalen Spieler) ---
        TrefferMarke,      // kurzer "Tock", wenn ein eigener Schuss sitzt
        TrefferKopf,       // heller Ton bei Kopftreffer
        Abschuss,          // eigener Gegner ausgeschaltet
        EigenerTod,        // man selbst wurde ausgeschaltet
        OhrenPfeifen,      // hoher Ton nach einer nahen Explosion (Ohren klingeln)

        // --- Einschläge (3D am Auftreffpunkt, für alle hörbar) ---
        EinschlagWand,
        EinschlagKoerper,

        // --- Schritte (3D an der Figur, Lautstärke nach Tempo) ---
        SchrittLeise,
        SchrittNormal,
        SchrittLaut,

        // --- Rundenablauf (2D, für den lokalen Spieler) ---
        RundeStart,
        RundeSieg,
        RundeNiederlage,
        KaufzeitVorbei,

        // --- Bombe ---
        BombePiep,
        BombeGelegt,
        BombeEntschaerft,
        BombeExplosion,
    }
}
