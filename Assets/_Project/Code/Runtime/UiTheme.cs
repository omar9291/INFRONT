using UnityEngine;
using UnityEngine.UIElements;

namespace Infront
{
    /// <summary>
    /// Farben und kleine Bausteine fuer die neue Oberflaeche mit Unity UI Toolkit.
    /// Stil "Dark Tactical": fast schwarz, gedeckte Graustufen, ein kraeftiger
    /// Orange-Akzent. Menue (<see cref="MainMenuUi"/>) und Ladebildschirm
    /// (<see cref="LoadingOverlay"/>) teilen sich diese Werte, damit alles
    /// gleich aussieht.
    ///
    /// Willst du auf Giftgruen wechseln: nur <see cref="Accent"/> und
    /// <see cref="AccentBright"/> aendern.
    ///
    /// Warm gegen kalt: Orange (<see cref="Accent"/>) ist die Marken- und
    /// Aktionsfarbe (Startknopf, "das hast du gewaehlt"). Eisblau
    /// (<see cref="Ice"/>) ist der kuehle Gegenpol fuer Zahlen, Messwerte,
    /// Scan-Linien und das eigene Team. Die Menue-Flaechen sind jetzt
    /// halbdurchsichtiges Glas (<see cref="Glass"/>), damit die 3D-Kulisse
    /// dahinter durchschimmert.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Bg          = new Color32(0x0B, 0x0D, 0x0F, 0xFF);   // Hintergrund
        public static readonly Color Panel       = new Color32(0x16, 0x19, 0x1D, 0xFF);   // Kasten
        public static readonly Color PanelHi     = new Color32(0x20, 0x25, 0x2B, 0xFF);   // Kasten (Maus drueber)
        public static readonly Color Line        = new Color32(0x2C, 0x31, 0x38, 0xFF);   // Raender
        public static readonly Color Text        = new Color32(0xD6, 0xDA, 0xDE, 0xFF);   // normale Schrift
        public static readonly Color TextDim     = new Color32(0x76, 0x7E, 0x86, 0xFF);   // Nebeninfos
        public static readonly Color Accent      = new Color32(0xFF, 0x6A, 0x1A, 0xFF);   // Akzent
        public static readonly Color AccentBright = new Color32(0xFF, 0x88, 0x3A, 0xFF);  // Akzent hell

        // --- Menue: kuehler Gegen-Akzent + Glas ----------------------------
        public static readonly Color Ice     = new Color32(0x69, 0xA6, 0xC2, 0xFF);   // Zahlen, Messwerte, eigenes Team (P6: gedeckter, ernster)
        public static readonly Color IceDim  = new Color32(0x33, 0x5C, 0x6E, 0xFF);   // Eisblau gedaempft
        public static readonly Color Foe     = new Color32(0xFF, 0x6B, 0x5E, 0xFF);   // Gegner im Briefing
        // Halbdurchsichtige Flaechen fuers Menue - die Kulisse schimmert durch.
        public static readonly Color Glass   = new Color(0.055f, 0.066f, 0.086f, 0.76f);
        public static readonly Color GlassHi = new Color(0.11f,  0.13f,  0.16f,  0.85f);
        public static readonly Color GlassDeep = new Color(0.03f, 0.037f, 0.05f, 0.82f);  // vertiefte Kaesten (Mini-Karte)
        public static readonly Color Edge    = new Color32(0x3C, 0x46, 0x54, 0xFF);   // hellerer Glasrand fuers Menue
        public static readonly Color Sheen   = new Color(1f, 1f, 1f, 0.02f);          // Glanz oben auf dem Glas (P6: dezenter)

        // --- HUD im Spiel ---------------------------------------------------
        // Halbdurchsichtiger Kasten, damit das Spielbild durchscheint.
        public static readonly Color HudPanelBg  = new Color(0.04f, 0.05f, 0.06f, 0.72f);
        public static readonly Color HudLine     = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color Good        = new Color32(0x4C, 0xD9, 0x64, 0xFF);   // Leben ok / gekauft
        public static readonly Color Warn        = new Color32(0xFF, 0xC4, 0x3A, 0xFF);   // Leben mittel
        public static readonly Color Bad         = new Color32(0xE0, 0x3B, 0x2E, 0xFF);   // Leben kritisch / Schaden
        public static readonly Color Money       = new Color32(0x8B, 0xE6, 0x9B, 0xFF);   // Geldbetrag
        public static readonly Color Armor       = new Color32(0x4F, 0x9B, 0xFF, 0xFF);   // Schutzweste
        public static readonly Color TeamMine    = new Color32(0x5B, 0xA9, 0xFF, 0xFF);   // eigenes Team

        // ------------------------------------------------------------------
        //  Farben, die sich nach dem Farbmodus richten
        //
        //  Die festen Werte oben bleiben, damit nichts kaputtgeht. Wer eine
        //  Farbe waehlt, die BEDEUTUNG traegt - Leben, Freund, Feind - nimmt
        //  ab jetzt diese Eigenschaften. Sie liefern im Standardfall genau die
        //  alten Werte zurueck.
        // ------------------------------------------------------------------

        /// <summary>Leben in Ordnung.</summary>
        public static Color Gut => GameSettings.ColorMode switch
        {
            // Rot-Gruen-Schwaeche: gruen faellt weg, also blau als "alles gut".
            GameSettings.Farbmodus.RotGruen => new Color32(0x4D, 0xB2, 0xFF, 0xFF),
            // Blau-Gelb-Schwaeche: gruen bleibt gut unterscheidbar.
            GameSettings.Farbmodus.BlauGelb => new Color32(0x4C, 0xD9, 0x64, 0xFF),
            GameSettings.Farbmodus.HoherKontrast => new Color32(0xF2, 0xF2, 0xF2, 0xFF),
            _ => Good,
        };

        /// <summary>Leben mittel.</summary>
        public static Color Mittel => GameSettings.ColorMode switch
        {
            GameSettings.Farbmodus.RotGruen => new Color32(0xFF, 0xD5, 0x4A, 0xFF),
            GameSettings.Farbmodus.BlauGelb => new Color32(0xE8, 0xE8, 0xE8, 0xFF),
            GameSettings.Farbmodus.HoherKontrast => new Color32(0x9A, 0x9A, 0x9A, 0xFF),
            _ => Warn,
        };

        /// <summary>Leben kritisch.</summary>
        public static Color Schlecht => GameSettings.ColorMode switch
        {
            // Magenta statt Rot: gegen Blau und Gelb klar unterscheidbar, auch
            // wenn Rot und Gruen ineinanderlaufen.
            GameSettings.Farbmodus.RotGruen => new Color32(0xFF, 0x3E, 0xC8, 0xFF),
            GameSettings.Farbmodus.BlauGelb => new Color32(0xE0, 0x3B, 0x2E, 0xFF),
            GameSettings.Farbmodus.HoherKontrast => new Color32(0x33, 0x33, 0x33, 0xFF),
            _ => Bad,
        };

        /// <summary>Eigenes Team.</summary>
        public static Color Freund => GameSettings.ColorMode == GameSettings.Farbmodus.HoherKontrast
            ? (Color)new Color32(0xFF, 0xFF, 0xFF, 0xFF)
            : TeamMine;

        /// <summary>Gegner.</summary>
        public static Color Gegner => GameSettings.ColorMode == GameSettings.Farbmodus.HoherKontrast
            ? (Color)new Color32(0x22, 0x22, 0x22, 0xFF)
            : Foe;
        public static readonly Color TeamFoe     = new Color32(0xFF, 0x6B, 0x5E, 0xFF);   // Gegner

        /// <summary>Feste Schriftstufen fuers HUD - nirgends sonst eine Groesse
        /// erfinden. XS Nebeninfo, S normal, M betont, L Zahl, XL Ereignis.</summary>
        public const float FontXS = 12f;
        public const float FontS  = 15f;
        public const float FontM  = 19f;
        public const float FontL  = 28f;
        public const float FontXL = 46f;

        public static Color TeamColor(int team, int myTeam)
        {
            if (team == Team.None) return TextDim;
            return team == myTeam ? TeamMine : TeamFoe;
        }

        /// <summary>Ein HUD-Kasten: halbdurchsichtig, feiner Rand, eckig.</summary>
        public static VisualElement HudBox()
        {
            var e = new VisualElement();
            e.style.backgroundColor = HudPanelBg;
            Border(e, 1f, HudLine);
            Square(e);
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        /// <summary>Beschriftung im HUD-Stil (gesperrt, halbtransparent).</summary>
        public static Label HudLabel(string text, float size, Color color)
        {
            var l = new Label(text);
            l.style.color = color;
            l.style.fontSize = size;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.pickingMode = PickingMode.Ignore;
            return l;
        }

        /// <summary>Nimmt einem Element und allen Kindern die Mausannahme -
        /// sonst frisst das HUD die Klicks und man kann nicht mehr schiessen.</summary>
        public static void IgnorePickingTree(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            foreach (var child in root.Children())
                IgnorePickingTree(child);
        }

        /// <summary>Rand an allen vier Seiten gleich setzen.</summary>
        public static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
        }

        /// <summary>Alle vier Ecken eckig machen (Standard-Knoepfe sind rund).</summary>
        public static void Square(VisualElement e)
        {
            e.style.borderTopLeftRadius = 0f; e.style.borderTopRightRadius = 0f;
            e.style.borderBottomLeftRadius = 0f; e.style.borderBottomRightRadius = 0f;
        }

        /// <summary>Innenabstand an allen vier Seiten gleich.</summary>
        public static void Pad(VisualElement e, float v)
        {
            e.style.paddingLeft = v; e.style.paddingRight = v;
            e.style.paddingTop = v; e.style.paddingBottom = v;
        }

        /// <summary>Aussenabstand an allen vier Seiten gleich.</summary>
        public static void Margin(VisualElement e, float v)
        {
            e.style.marginLeft = v; e.style.marginRight = v;
            e.style.marginTop = v; e.style.marginBottom = v;
        }

        /// <summary>Leerraum fester Hoehe zum Trennen von Abschnitten.</summary>
        public static VisualElement Gap(float h)
        {
            var v = new VisualElement();
            v.style.height = h;
            v.style.flexShrink = 0f;
            return v;
        }

        /// <summary>Kleine, gesperrte Ueberschrift ueber einer Einstellung.</summary>
        public static Label Section(string text)
        {
            var l = new Label(text);
            l.style.color = TextDim;
            l.style.fontSize = 12f;
            l.style.letterSpacing = 3f;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginBottom = 4f;
            return l;
        }
    }
}
