using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Infront
{
    /// <summary>
    /// Das Hauptmenue mit Unity UI Toolkit im "Kino-Look".
    ///
    /// Die 3D-Kulisse dahinter (SceneBuilder + <see cref="MenuCameraRig"/>) wird
    /// vom <see cref="PostFxController"/> weich verschwommen (Tiefenunschaerfe).
    /// Davor steht die Oberflaeche klar und ruhig - wenige, gezielte Bewegungen
    /// statt vieler kleiner Wackler:
    ///  - der ganze Inhalt kippt leicht mit der Maus (Parallaxe, zweite Ebene
    ///    zur Kamerafahrt), das Hintergrund-Raster kippt gegenlaeufig;
    ///  - ein Licht-Wisch ueber dem Startknopf;
    ///  - Seitenwechsel gleiten von rechts herein;
    ///  - jeder Klick gibt einen kurzen Ton (ueber den <see cref="AudioService"/>).
    ///
    /// Aufbau der Navigation:
    ///  - SPIELEN        alles, was du vor einer Runde entscheidest (Einsatzart,
    ///                   Teamgroesse, Bot-Staerke), rechts die Aufstellung, unten
    ///                   der breite Startbalken.
    ///  - EINSTELLUNGEN  einmal einstellen: Anzeige, Bild, Maus, Ton.
    ///  - STEUERUNG      reine Tastenreferenz.
    ///  - Beenden        abgesetzt unten, kein gleichwertiger Reiter.
    ///
    /// Farben: Orange ist die Aktions- und Startfarbe. Eisblau (<see cref="UiTheme.Ice"/>)
    /// ist der kuehle Gegenpol fuer Zahlen, Messwerte und das eigene Team. Die
    /// Flaechen sind dunkle Eck-Rahmen (<see cref="SoftPanel"/>) statt voller Kaesten.
    ///
    /// Das alte IMGUI-Menue (<see cref="MainMenu"/>) bleibt als Rueckfallebene im
    /// Objektbaum: schlaegt der Aufbau hier fehl, oder drueckst du F10, erscheint
    /// wieder das alte Menue.
    ///
    /// NICHT pruefbar: wie es aussieht (Farben, Abstaende, Hover, Animation).
    /// Pruefbar ist nur, dass der Baum steht und die Schalter in GameSettings landen.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuUi : MonoBehaviour
    {
        enum Page { Spielen, Einstellungen, Zugaenglichkeit, Steuerung, Quellen, Beenden }

        // Kurze Hinweise, die in der Navigation langsam durchwechseln.
        static readonly string[] Tips =
        {
            "Kopftreffer machen doppelten Schaden.",
            "Halte E, um die Bombe zu legen oder zu entschaerfen.",
            "Wer stirbt, bleibt die Runde tot - kein Respawn.",
            "Ueberlebende behalten Waffe und Weste.",
            "Mit B oeffnest du am Rundenanfang das Kaufmenue.",
            "Die Weste schluckt den halben Koerperschaden.",
            "Nach 15 Runden werden die Seiten getauscht.",
        };

        // Rufnamen fuer die Aufstellung. Platz 0 bist immer du selbst.
        static readonly string[] TeamNames =
            { "DU", "FALKE", "WOLF", "LUCHS", "RABE", "BÄR", "OTTER", "HECHT", "GEIER", "DACHS" };
        static readonly string[] FoeNames =
            { "KOBRA", "NATTER", "SKORPION", "HORNISSE", "WIDDER", "PIRANHA", "WARAN", "BUSSARD", "MARDER", "ILTIS" };

        UIDocument _doc;
        VisualElement _parallax;         // ganze Oberflaeche, kippt leicht mit der Maus
        VisualElement _grid;             // feines Raster, kippt gegenlaeufig -> Tiefe
        VisualElement _pageHost;         // Inhalt rechts, wird pro Seite geleert
        VisualElement _pageEdge;         // Akzent-Linie, faehrt bei jedem Seitenwechsel ueber die Oberkante
        VisualElement _lineup;           // Aufstellung im Briefing (dein Team gegen Gegner)
        Label _briefLine;                // Modus-/Bot-Zeile im Briefing
        Slider _sensSlider;
        Label _sensValue;
        Slider _volSlider;
        Label _volValue;
        Label _summary;                  // Zeile ueber dem Startbalken
        bool _built;

        Vector2 _look;                   // geglaettete Mausablage, Mitte = 0, Rand = ±1

        Page _page = Page.Spielen;
        readonly Dictionary<Page, Button> _navButtons = new();
        readonly Dictionary<Page, VisualElement> _navBars = new();   // Akzent-Balken je Nav-Knopf

        // Test-Haken: Name eines Elements -> was ein Klick darauf tut.
        readonly Dictionary<string, Action> _actions = new();

        // ---- Test-Schnittstelle ----
        public bool IsBuiltForTests => _built;
        public VisualElement RootForTests => _doc != null ? _doc.rootVisualElement : null;
        public bool ClickForTests(string elementName)
            => _actions.TryGetValue(elementName, out var a) && Run(a);
        public void SetSensitivityForTests(float v)
        {
            if (_sensSlider != null) _sensSlider.value = v;
            else { GameSettings.MouseSensitivity = v; GameSettings.Save(); }
        }
        public void SetVolumeForTests(float v)
        {
            if (_volSlider != null) _volSlider.value = v;
            else { GameSettings.SfxVolume = v; GameSettings.Save(); }
        }
        static bool Run(Action a) { a(); return true; }

        void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc.panelSettings == null)
            {
                Debug.LogWarning("[Infront] MenuUI ohne PanelSettings - altes Menue bleibt aktiv.");
                enabled = false;
                return;
            }
            MainMenu.Suppressed = true;   // altes IMGUI-Menue zeichnet nichts mehr
        }

        void OnEnable()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            TryBuild();
        }

        void Update()
        {
            if (!_built && enabled) { TryBuild(); return; }

            var kb = Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame)
                ToggleFallback();

            if (!_built) return;

            // Maus-Parallaxe: der Inhalt kippt leicht entgegen der Maus, das
            // Raster staerker und gegenlaeufig - zwei Ebenen, spuerbare Tiefe.
            var m = Mouse.current;
            if (m != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 target = new Vector2(
                    Mathf.Clamp(m.position.x.ReadValue() / Screen.width * 2f - 1f, -1f, 1f),
                    Mathf.Clamp(m.position.y.ReadValue() / Screen.height * 2f - 1f, -1f, 1f));
                _look = Vector2.Lerp(_look, target, Time.unscaledDeltaTime * 4f);

                if (_parallax != null)
                    _parallax.style.translate = new Translate(-_look.x * 7f, -_look.y * 5f, 0f);
                if (_grid != null)
                    _grid.style.translate = new Translate(_look.x * 16f, _look.y * 11f, 0f);
            }
        }

        void ToggleFallback()
        {
            MainMenu.Suppressed = !MainMenu.Suppressed;
            if (_doc != null && _doc.rootVisualElement != null)
                _doc.rootVisualElement.style.display =
                    MainMenu.Suppressed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void TryBuild()
        {
            if (_built || _doc == null) return;
            var root = _doc.rootVisualElement;
            if (root == null) return;   // UIDocument noch nicht bereit - naechster Frame

            try
            {
                Build(root);
                _built = true;

                // Erster Start ueberhaupt? Dann die drei Karten zeigen. Laeuft
                // genau einmal und liegt ueber dem fertigen Menue - wer
                // ueberspringt, ist sofort drin. Im Testlauf uebersprungen,
                // sonst haengen alle Menue-Tests am Erstlauf fest.
                // Erst nach dem Startbildschirm - sonst liegen die Karten
                // unsichtbar hinter dem Ladebildschirm und werden weggeklickt,
                // bevor sie jemand sieht.
                if (!Application.isBatchMode)
                    BootFlow.WhenDone(() => FirstRunFlow.ZeigeWennNoetig(root, null));
            }
            catch (Exception e)
            {
                Debug.LogError("[Infront] Aufbau des neuen Menues fehlgeschlagen - "
                               + "zurueck zum alten Menue.\n" + e);
                MainMenu.Suppressed = false;
                try { _doc.rootVisualElement?.Clear(); } catch { /* egal */ }
                enabled = false;
            }
        }

        // ------------------------------------------------------------------
        //  Aufbau
        // ------------------------------------------------------------------

        void Build(VisualElement root)
        {
            root.Clear();
            root.style.flexGrow = 1f;
            // Durchsichtig lassen - dahinter laeuft die (verschwommene) 3D-Kulisse.
            root.style.backgroundColor = Color.clear;
            root.style.display = DisplayStyle.Flex;

            // Feines Raster ueber der ganzen Kulisse - kippt in Update() mit der Maus.
            root.Add(BuildGrid());

            // Leichter Abdunkler. Die Tiefenunschaerfe nimmt der Kulisse schon
            // viel Unruhe; hier kommt nur noch etwas Kontrast dazu.
            var scrim = new VisualElement { name = "scrim" };
            scrim.style.position = Position.Absolute;
            scrim.style.left = 0f; scrim.style.top = 0f; scrim.style.right = 0f; scrim.style.bottom = 0f;
            var sc = UiTheme.Bg; sc.a = 0.30f;
            scrim.style.backgroundColor = sc;
            scrim.pickingMode = PickingMode.Ignore;
            root.Add(scrim);

            // Weiche dunkle Baender oben und unten (zusaetzlich zur PostFx-Vignette),
            // damit Kopf- und Fusszeile immer satt lesbar bleiben.
            var bandTop = Band(fromTop: true);
            bandTop.name = "band-top";
            bandTop.style.top = 0f; bandTop.style.height = 190f;
            root.Add(bandTop);

            var bandBottom = Band(fromTop: false);
            bandBottom.name = "band-bottom";
            bandBottom.style.bottom = 0f; bandBottom.style.height = 92f;
            root.Add(bandBottom);

            // Die ganze Oberflaeche in einer Ebene, die mit der Maus kippt.
            _parallax = new VisualElement();
            _parallax.style.flexGrow = 1f;
            _parallax.style.flexDirection = FlexDirection.Column;
            root.Add(_parallax);

            var header = BuildHeader();
            var hline = HLine();
            var body = BuildBody();
            var footer = BuildFooter();
            _parallax.Add(header);
            _parallax.Add(hline);
            _parallax.Add(body);
            _parallax.Add(footer);

            ShowPage(_page);

            // Auftritt: Kopf, Linie, Inhalt, Fuss nacheinander von unten einblenden.
            FadeUp(header, 40);
            FadeUp(hline, 110);
            FadeUp(body, 170);
            FadeUp(footer, 250);
        }

        /// <summary>Weiches dunkles Band aus vier Streifen mit abnehmender
        /// Deckkraft - faket einen Verlauf, damit die Kante nicht hart abbricht.</summary>
        static VisualElement Band(bool fromTop)
        {
            var wrap = new VisualElement();
            wrap.style.position = Position.Absolute;
            wrap.style.left = 0f; wrap.style.right = 0f;
            wrap.style.flexDirection = FlexDirection.Column;
            wrap.pickingMode = PickingMode.Ignore;

            float[] a = fromTop
                ? new[] { 0.70f, 0.50f, 0.28f, 0.10f }
                : new[] { 0.10f, 0.28f, 0.50f, 0.70f };
            for (int i = 0; i < a.Length; i++)
            {
                var s = new VisualElement();
                s.style.flexGrow = 1f;
                var c = UiTheme.Bg; c.a = a[i];
                s.style.backgroundColor = c;
                wrap.Add(s);
            }
            return wrap;
        }

        /// <summary>Feines Linienraster als Hintergrund-Deko. Bewegt wird es in Update().</summary>
        VisualElement BuildGrid()
        {
            _grid = new VisualElement { name = "grid" };
            _grid.style.position = Position.Absolute;
            _grid.style.left = Length.Percent(-25f);
            _grid.style.top = Length.Percent(-25f);
            _grid.style.width = Length.Percent(150f);
            _grid.style.height = Length.Percent(150f);
            _grid.pickingMode = PickingMode.Ignore;

            var col = UiTheme.Ice; col.a = 0.02f;
            const int step = 72;
            for (int x = 0; x < 42; x++)
            {
                var v = new VisualElement();
                v.style.position = Position.Absolute;
                v.style.left = x * step; v.style.top = 0f;
                v.style.width = 1f; v.style.height = Length.Percent(100f);
                v.style.backgroundColor = col;
                _grid.Add(v);
            }
            for (int y = 0; y < 28; y++)
            {
                var h = new VisualElement();
                h.style.position = Position.Absolute;
                h.style.top = y * step; h.style.left = 0f;
                h.style.height = 1f; h.style.width = Length.Percent(100f);
                h.style.backgroundColor = col;
                _grid.Add(h);
            }
            return _grid;
        }

        // ------------------------------------------------------------------
        //  Kleine Helfer
        // ------------------------------------------------------------------

        /// <summary>Kurzer Klick-Ton ueber den AudioService. Still, wenn es den
        /// Dienst nicht gibt (z.B. im Test) - dann passiert einfach nichts.</summary>
        static void Click(SoundId id = SoundId.WaffeWechsel, float volume = 0.4f)
        {
            var a = AudioService.Instance;
            if (a != null) a.Play2D(id, volume);
        }

        /// <summary>Dunkle, fast durchsichtige Flaeche mit zwei Eck-Winkeln statt
        /// vollem Kasten - der Kino-Look fuer die Flaechen im Menue.</summary>
        static void SoftPanel(VisualElement el, float fillAlpha, bool brackets = true)
        {
            UiTheme.Square(el);
            el.style.backgroundColor = new Color(0.02f, 0.025f, 0.035f, fillAlpha);
            if (brackets)
            {
                UiTheme.Border(el, 0f, UiTheme.Edge);
                AddBracket(el, left: true, top: true);
                AddBracket(el, left: false, top: false);
            }
            else
            {
                UiTheme.Border(el, 1f, UiTheme.Edge);
            }
        }

        /// <summary>Ein L-foermiger Eck-Winkel in eine Flaeche.</summary>
        static void AddBracket(VisualElement host, bool left, bool top, float len = 16f)
        {
            var c = UiTheme.Edge;

            var h = new VisualElement();
            h.style.position = Position.Absolute;
            h.style.width = len; h.style.height = 2f;
            h.style.backgroundColor = c;
            h.pickingMode = PickingMode.Ignore;
            if (left) h.style.left = 0f; else h.style.right = 0f;
            if (top) h.style.top = 0f; else h.style.bottom = 0f;
            host.Add(h);

            var v = new VisualElement();
            v.style.position = Position.Absolute;
            v.style.width = 2f; v.style.height = len;
            v.style.backgroundColor = c;
            v.pickingMode = PickingMode.Ignore;
            if (left) v.style.left = 0f; else v.style.right = 0f;
            if (top) v.style.top = 0f; else v.style.bottom = 0f;
            host.Add(v);
        }

        /// <summary>Blendet ein Element sanft von unten ein.</summary>
        static void FadeUp(VisualElement el, int delayMs, float fromY = 16f)
        {
            el.style.opacity = 0f;
            el.style.translate = new Translate(0f, fromY, 0f);
            el.schedule.Execute(() =>
            {
                el.style.transitionProperty = new List<StylePropertyName> { "opacity", "translate" };
                el.style.transitionDuration = new List<TimeValue> { new TimeValue(260, TimeUnit.Millisecond) };
                el.style.transitionTimingFunction =
                    new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                el.style.opacity = 1f;
                el.style.translate = new Translate(0f, 0f, 0f);
            }).StartingIn(delayMs);
        }

        /// <summary>Zaehlt eine Zahl von 0 auf den Zielwert hoch.</summary>
        static void CountUp(Label label, int target, int durationMs = 650)
        {
            if (target <= 0) { label.text = "0"; return; }
            int steps = Mathf.Clamp(target, 1, 24);
            int stepMs = Mathf.Max(16, durationMs / steps);
            int i = 0;
            label.text = "0";
            label.schedule.Execute(() =>
            {
                i++;
                label.text = Mathf.RoundToInt(Mathf.Lerp(0f, target, i / (float)steps)).ToString();
            }).Every(stepMs).ForDuration(stepMs * steps + 40);
        }

        /// <summary>Laesst einen duennen Balken einmal von 0 auf volle Breite wachsen.</summary>
        static void GrowBar(VisualElement bar, int delayMs, int durationMs = 500)
        {
            bar.style.width = Length.Percent(0f);
            bar.schedule.Execute(() =>
            {
                bar.style.transitionProperty = new List<StylePropertyName> { "width" };
                bar.style.transitionDuration = new List<TimeValue> { new TimeValue(durationMs, TimeUnit.Millisecond) };
                bar.style.transitionTimingFunction =
                    new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                bar.style.width = Length.Percent(100f);
            }).StartingIn(delayMs);
        }

        // ------------------------------------------------------------------
        //  Kopfzeile
        // ------------------------------------------------------------------

        VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.FlexEnd;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 72f; header.style.paddingRight = 72f;
            header.style.paddingTop = 32f; header.style.paddingBottom = 12f;

            var brand = new VisualElement();
            brand.style.flexDirection = FlexDirection.Row;
            brand.style.alignItems = Align.FlexEnd;

            var tick = new VisualElement();
            tick.style.width = 8f; tick.style.height = 60f;
            tick.style.backgroundColor = UiTheme.Accent;
            tick.style.marginRight = 18f;
            tick.style.marginBottom = 8f;

            var titleCol = new VisualElement();

            var title = new Label("INFRONT");
            title.style.color = UiTheme.Text;
            title.style.fontSize = 60f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            // Buchstaben laufen beim Auftritt von weit auf den Sollabstand zusammen.
            title.style.letterSpacing = 34f;
            title.schedule.Execute(() =>
            {
                title.style.transitionProperty = new List<StylePropertyName> { "letter-spacing" };
                title.style.transitionDuration = new List<TimeValue> { new TimeValue(650, TimeUnit.Millisecond) };
                title.style.transitionTimingFunction =
                    new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                title.style.letterSpacing = 14f;
            }).StartingIn(120);

            var tagline = new Label("TAKTIK-SHOOTER   ·   RUNDENBASIERT   ·   HOST-MODUS");
            tagline.style.color = UiTheme.TextDim;
            tagline.style.fontSize = 11f;
            tagline.style.letterSpacing = 5f;
            tagline.style.unityFontStyleAndWeight = FontStyle.Bold;
            tagline.style.marginTop = 4f;
            tagline.style.marginLeft = 3f;

            titleCol.Add(title);
            titleCol.Add(tagline);
            brand.Add(tick);
            brand.Add(titleCol);

            var version = new Label("DRIFTLAB   ·   " + VersionText());
            version.style.color = UiTheme.TextDim;
            version.style.fontSize = 12f;
            version.style.letterSpacing = 2f;
            version.style.unityFontStyleAndWeight = FontStyle.Bold;
            version.style.marginBottom = 6f;

            header.Add(brand);
            header.Add(version);
            return header;
        }

        /// <summary>Version aus den Projekteinstellungen, damit die Anzeige nicht
        /// irgendwann luegt. Faellt auf "V0.9" zurueck, wenn nichts gesetzt ist.</summary>
        static string VersionText()
        {
            var v = Application.version;
            if (string.IsNullOrWhiteSpace(v)) return "V0.9";
            return v.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? v.ToUpperInvariant() : "V" + v;
        }

        VisualElement BuildCareer()
        {
            var box = new VisualElement();
            box.style.marginTop = 26f;
            box.style.paddingLeft = 14f; box.style.paddingRight = 14f;
            box.style.paddingTop = 12f; box.style.paddingBottom = 12f;
            SoftPanel(box, 0.30f);

            box.Add(UiTheme.Section("LAUFBAHN"));

            // Neuer Spieler: vier Nullen sehen aus wie ein Fehler. Der
            // Leer-Zustand aus UiStates sagt stattdessen, was hier stehen wird
            // und was zu tun ist - und sieht ueberall im Spiel gleich aus.
            if (CareerStats.Matches <= 0)
            {
                box.Add(UiStates.KeineLaufbahn(() => ShowPage(Page.Spielen)));
                return box;
            }

            int rowIdx = 0;
            void Row(string label, int value)
            {
                var r = new VisualElement();
                r.style.flexDirection = FlexDirection.Row;
                r.style.justifyContent = Justify.SpaceBetween;
                r.style.alignItems = Align.Center;
                r.style.marginTop = 6f;
                var a = new Label(label); a.style.color = UiTheme.TextDim; a.style.fontSize = 12f;

                var right = new VisualElement();
                right.style.alignItems = Align.FlexEnd;
                var b = new Label("0");
                b.style.color = UiTheme.Ice; b.style.fontSize = 13f;
                b.style.unityFontStyleAndWeight = FontStyle.Bold;
                var bar = new VisualElement();
                bar.style.height = 2f;
                bar.style.width = Length.Percent(0f);
                bar.style.minWidth = 34f;
                bar.style.marginTop = 2f;
                var barCol = UiTheme.Ice; barCol.a = 0.5f;
                bar.style.backgroundColor = barCol;
                right.Add(b); right.Add(bar);

                r.Add(a); r.Add(right);
                box.Add(r);
                CountUp(b, value);
                GrowBar(bar, 200 + rowIdx * 70);
                rowIdx++;
            }

            Row("Matches", CareerStats.Matches);
            Row("Siege", CareerStats.Wins);
            Row("Aces", CareerStats.Aces);
            Row("Längste Serie", CareerStats.BestStreak);
            return box;
        }

        /// <summary>Duenne Trennlinie unter der Kopfzeile mit einem festen
        /// orangefarbenen Anschnitt links - kein wanderndes Licht mehr.</summary>
        VisualElement HLine()
        {
            var wrap = new VisualElement();
            wrap.style.height = 1f;
            wrap.style.backgroundColor = UiTheme.Line;
            wrap.style.flexShrink = 0f;
            wrap.style.marginLeft = 72f; wrap.style.marginRight = 72f;

            var seg = new VisualElement();
            seg.style.position = Position.Absolute;
            seg.style.left = 0f; seg.style.top = 0f;
            seg.style.height = 1f; seg.style.width = 72f;
            seg.style.backgroundColor = UiTheme.Accent;
            seg.pickingMode = PickingMode.Ignore;
            wrap.Add(seg);
            return wrap;
        }

        VisualElement BuildBody()
        {
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;
            body.style.alignItems = Align.Stretch;
            body.style.paddingLeft = 72f; body.style.paddingRight = 72f;
            body.style.paddingTop = 30f; body.style.paddingBottom = 22f;

            // ---- Navigation links ----
            var nav = new VisualElement();
            nav.style.width = 264f;
            nav.style.flexShrink = 0f;
            nav.style.marginRight = 44f;
            nav.Add(NavButton("SPIELEN", Page.Spielen));
            nav.Add(NavButton("EINSTELLUNGEN", Page.Einstellungen));
            nav.Add(NavButton("ZUGAENGLICHKEIT", Page.Zugaenglichkeit));
            nav.Add(NavButton("STEUERUNG", Page.Steuerung));
            nav.Add(NavButton("QUELLEN", Page.Quellen));

            var sep = new VisualElement();
            sep.style.height = 1f;
            sep.style.backgroundColor = UiTheme.Line;
            sep.style.marginTop = 6f; sep.style.marginBottom = 10f;
            nav.Add(sep);

            nav.Add(NavButton("Beenden", Page.Beenden, minor: true));
            nav.Add(BuildCareer());

            var navSpacer = new VisualElement();
            navSpacer.style.flexGrow = 1f;
            nav.Add(navSpacer);

            nav.Add(BuildTipBox());
            nav.Add(BuildStatusLine());
            body.Add(nav);

            // ---- Inhalt rechts (Hauptflaeche) ----
            var panel = new VisualElement();
            panel.style.flexGrow = 1f;
            panel.style.overflow = Overflow.Hidden;   // fuer Kantenlinie und Auftritt
            panel.style.paddingLeft = 36f; panel.style.paddingRight = 36f;
            panel.style.paddingTop = 32f; panel.style.paddingBottom = 30f;
            // Hauptflaeche: etwas deckender + ein Hauch Rand, damit die Schrift
            // auch ueber hellen Stellen der Kulisse sicher lesbar bleibt.
            SoftPanel(panel, 0.62f, brackets: false);

            // Grosse animierte L-Ecke oben links - ein gezielter Auftritt-Effekt.
            var cornerH = new VisualElement();
            cornerH.style.position = Position.Absolute;
            cornerH.style.left = -1f; cornerH.style.top = -1f;
            cornerH.style.width = 0f; cornerH.style.height = 3f;
            cornerH.style.backgroundColor = UiTheme.Accent;
            cornerH.pickingMode = PickingMode.Ignore;
            panel.Add(cornerH);

            var cornerV = new VisualElement();
            cornerV.style.position = Position.Absolute;
            cornerV.style.left = -1f; cornerV.style.top = -1f;
            cornerV.style.width = 3f; cornerV.style.height = 0f;
            cornerV.style.backgroundColor = UiTheme.Accent;
            cornerV.pickingMode = PickingMode.Ignore;
            panel.Add(cornerV);

            cornerH.schedule.Execute(() =>
            {
                cornerH.style.transitionProperty = new List<StylePropertyName> { "width" };
                cornerH.style.transitionDuration = new List<TimeValue> { new TimeValue(430, TimeUnit.Millisecond) };
                cornerH.style.transitionTimingFunction =
                    new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                cornerH.style.width = 60f;
                cornerV.style.transitionProperty = new List<StylePropertyName> { "height" };
                cornerV.style.transitionDuration = new List<TimeValue> { new TimeValue(430, TimeUnit.Millisecond) };
                cornerV.style.transitionTimingFunction =
                    new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                cornerV.style.height = 60f;
            }).StartingIn(340);

            // Akzent-Linie, die bei jedem Seitenwechsel ueber die Oberkante faehrt.
            _pageEdge = new VisualElement();
            _pageEdge.style.position = Position.Absolute;
            _pageEdge.style.left = 0f; _pageEdge.style.top = -1f;
            _pageEdge.style.height = 2f;
            _pageEdge.style.width = Length.Percent(0f);
            _pageEdge.style.backgroundColor = UiTheme.Ice;
            _pageEdge.pickingMode = PickingMode.Ignore;
            panel.Add(_pageEdge);

            _pageHost = new VisualElement();
            _pageHost.style.flexGrow = 1f;
            panel.Add(_pageHost);

            body.Add(panel);
            return body;
        }

        /// <summary>Kleiner Kasten unten in der Navigation: Hinweise wechseln durch.</summary>
        VisualElement BuildTipBox()
        {
            var box = new VisualElement();
            box.style.marginTop = 12f;
            box.style.backgroundColor = new Color(0.02f, 0.025f, 0.035f, 0.34f);
            UiTheme.Square(box);
            UiTheme.Border(box, 0f, UiTheme.Edge);
            box.style.borderLeftWidth = 3f;
            box.style.borderLeftColor = UiTheme.Ice;
            UiTheme.Pad(box, 12f);

            var head = new Label("TIPP");
            head.style.color = UiTheme.Ice;
            head.style.fontSize = 10f;
            head.style.letterSpacing = 3f;
            head.style.unityFontStyleAndWeight = FontStyle.Bold;
            head.style.marginBottom = 4f;
            box.Add(head);

            var text = new Label(Tips[UnityEngine.Random.Range(0, Tips.Length)]);
            text.style.color = UiTheme.TextDim;
            text.style.fontSize = 11f;
            text.style.whiteSpace = WhiteSpace.Normal;
            box.Add(text);

            int idx = 0;
            text.style.transitionProperty = new List<StylePropertyName> { "opacity" };
            text.style.transitionDuration = new List<TimeValue> { new TimeValue(280, TimeUnit.Millisecond) };
            box.schedule.Execute(() =>
            {
                text.style.opacity = 0f;
                box.schedule.Execute(() =>
                {
                    idx = (idx + 1) % Tips.Length;
                    text.text = Tips[idx];
                    text.style.opacity = 1f;
                }).StartingIn(300);
            }).Every(9000).StartingIn(9000);

            return box;
        }

        /// <summary>Status-Zeile ganz unten in der Navigation, mit ruhig blinkendem Punkt.</summary>
        VisualElement BuildStatusLine()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 10f;

            var dot = new VisualElement();
            dot.style.width = 8f; dot.style.height = 8f;
            dot.style.backgroundColor = UiTheme.Ice;
            dot.style.marginRight = 8f;
            dot.style.transitionProperty = new List<StylePropertyName> { "opacity" };
            dot.style.transitionDuration = new List<TimeValue> { new TimeValue(900, TimeUnit.Millisecond) };
            bool on = true;
            dot.schedule.Execute(() => { on = !on; dot.style.opacity = on ? 1f : 0.3f; }).Every(1400);

            var label = new Label("SYSTEM BEREIT   ·   HOST");
            label.style.color = UiTheme.TextDim;
            label.style.fontSize = 10f;
            label.style.letterSpacing = 2f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;

            row.Add(dot);
            row.Add(label);
            return row;
        }

        VisualElement BuildFooter()
        {
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.paddingLeft = 72f; footer.style.paddingRight = 72f;
            footer.style.paddingBottom = 18f;

            var left = new Label("F10  –  ALTES MENUE");
            left.style.color = UiTheme.TextDim;
            left.style.fontSize = 11f;
            left.style.letterSpacing = 2f;

            var right = new Label("HOST-MODUS  ·  EINZELSPIELER GEGEN BOTS");
            right.style.color = UiTheme.TextDim;
            right.style.fontSize = 11f;
            right.style.letterSpacing = 2f;

            footer.Add(left);
            footer.Add(right);
            return footer;
        }

        // ------------------------------------------------------------------
        //  Navigation
        // ------------------------------------------------------------------

        Button NavButton(string text, Page page, bool minor = false)
        {
            var b = new Button(() => { Click(); ShowPage(page); }) { text = text };
            b.name = "nav-" + page.ToString().ToLowerInvariant();
            b.style.height = minor ? 38f : 46f;
            b.style.marginTop = 0f; b.style.marginBottom = 8f;
            b.style.marginLeft = 0f; b.style.marginRight = 0f;
            b.style.paddingLeft = 18f;
            b.style.fontSize = minor ? 12f : 14f;
            b.style.letterSpacing = 3f;
            b.style.color = minor ? UiTheme.TextDim : UiTheme.Text;
            b.style.backgroundColor = UiTheme.Glass;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.overflow = Overflow.Hidden;
            UiTheme.Square(b);
            UiTheme.Border(b, 1f, UiTheme.Edge);
            b.style.transitionProperty = new List<StylePropertyName> { "background-color", "translate" };
            b.style.transitionDuration = new List<TimeValue> { new TimeValue(120, TimeUnit.Millisecond) };

            // Akzent-Balken links, waechst beim Drueberfahren von oben herein.
            var growBar = new VisualElement();
            growBar.style.position = Position.Absolute;
            growBar.style.left = 0f; growBar.style.top = 0f;
            growBar.style.width = 3f;
            growBar.style.height = Length.Percent(0f);
            growBar.style.backgroundColor = UiTheme.Accent;
            growBar.pickingMode = PickingMode.Ignore;
            growBar.style.transitionProperty = new List<StylePropertyName> { "height" };
            growBar.style.transitionDuration = new List<TimeValue> { new TimeValue(160, TimeUnit.Millisecond) };
            growBar.style.transitionTimingFunction =
                new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
            b.Add(growBar);
            _navBars[page] = growBar;

            b.RegisterCallback<MouseEnterEvent>(_ =>
            {
                growBar.style.height = Length.Percent(100f);
                if (_page != page)
                {
                    b.style.backgroundColor = UiTheme.GlassHi;
                    b.style.translate = new Translate(6f, 0f, 0f);
                }
            });
            b.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (_page != page)
                {
                    growBar.style.height = Length.Percent(0f);
                    b.style.backgroundColor = UiTheme.Glass;
                    b.style.translate = new Translate(0f, 0f, 0f);
                }
            });

            _navButtons[page] = b;
            _actions[b.name] = () => ShowPage(page);
            return b;
        }

        void ShowPage(Page page)
        {
            _page = page;
            foreach (var kv in _navButtons)
            {
                bool sel = kv.Key == page;
                kv.Value.style.backgroundColor = sel ? UiTheme.GlassHi : UiTheme.Glass;
                kv.Value.style.translate = new Translate(sel ? 4f : 0f, 0f, 0f);
                kv.Value.style.color = sel
                    ? Color.white
                    : (kv.Key == Page.Beenden ? UiTheme.TextDim : UiTheme.Text);
            }
            foreach (var kv in _navBars)
                kv.Value.style.height = Length.Percent(kv.Key == page ? 100f : 0f);

            // Regler-Referenzen gehoeren zur Einstellungen-Seite; beim Verlassen ungueltig.
            if (page != Page.Einstellungen)
            {
                _sensSlider = null; _sensValue = null;
                _volSlider = null; _volValue = null;
            }
            _summary = null;
            _lineup = null;
            _briefLine = null;

            // Andere Seiten nicht endlos breit ziehen; SPIELEN darf die volle Breite.
            if (page == Page.Spielen) _pageHost.style.maxWidth = StyleKeyword.None;
            else _pageHost.style.maxWidth = 720f;

            // Akzent-Linie einmal ueber die Panel-Oberkante ziehen.
            if (_pageEdge != null)
            {
                _pageEdge.style.transitionProperty = new List<StylePropertyName>();
                _pageEdge.style.width = Length.Percent(0f);
                _pageEdge.schedule.Execute(() =>
                {
                    _pageEdge.style.transitionProperty = new List<StylePropertyName> { "width", "opacity" };
                    _pageEdge.style.transitionDuration = new List<TimeValue>
                        { new TimeValue(420, TimeUnit.Millisecond), new TimeValue(500, TimeUnit.Millisecond) };
                    _pageEdge.style.transitionTimingFunction =
                        new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                    _pageEdge.style.opacity = 1f;
                    _pageEdge.style.width = Length.Percent(100f);
                    _pageEdge.schedule.Execute(() => _pageEdge.style.opacity = 0f).StartingIn(500);
                }).StartingIn(20);
            }

            if (_pageHost == null) return;
            _pageHost.Clear();
            switch (page)
            {
                case Page.Spielen: BuildSpielen(_pageHost); break;
                case Page.Einstellungen: BuildEinstellungen(_pageHost); break;
                case Page.Zugaenglichkeit: BuildZugaenglichkeit(_pageHost); break;
                case Page.Steuerung: BuildSteuerung(_pageHost); break;
                case Page.Quellen: BuildQuellen(_pageHost); break;
                case Page.Beenden: BuildBeenden(_pageHost); break;
            }
            StaggerIn(_pageHost);
        }

        /// <summary>Blendet die Kinder eines Elements nacheinander von rechts ein.</summary>
        static void StaggerIn(VisualElement host)
        {
            int i = 0;
            foreach (var child in host.Children())
            {
                child.style.opacity = 0f;
                child.style.translate = new Translate(40f, 6f, 0f);
                int delay = 30 + i * 45;
                var c = child;
                host.schedule.Execute(() =>
                {
                    c.style.transitionProperty = new List<StylePropertyName> { "opacity", "translate" };
                    c.style.transitionDuration = new List<TimeValue> { new TimeValue(220, TimeUnit.Millisecond) };
                    c.style.transitionTimingFunction = new List<EasingFunction>
                        { new EasingFunction(EasingMode.EaseOutCubic) };
                    c.style.opacity = 1f;
                    c.style.translate = new Translate(0f, 0f, 0f);
                }).StartingIn(delay);
                i++;
            }
        }

        // ------------------------------------------------------------------
        //  Seite: SPIELEN  (Entscheidungen links, Aufstellung rechts, Startbalken unten)
        // ------------------------------------------------------------------

        void BuildSpielen(VisualElement host)
        {
            host.style.flexGrow = 1f;
            host.style.flexDirection = FlexDirection.Column;

            // Obere Zeile: links die Entscheidungen, rechts die Aufstellung.
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.flexGrow = 1f;

            var left = new VisualElement();
            left.style.flexGrow = 1f;
            left.style.flexBasis = 0f;
            left.style.marginRight = 30f;

            // --- Einsatzart: zwei grosse Karten ---
            left.Add(UiTheme.Section("EINSATZART"));

            var modeRow = new VisualElement();
            modeRow.style.flexDirection = FlexDirection.Row;
            modeRow.style.marginTop = 8f;

            var modeCards = new List<Button>();
            int modeCurrent = (int)GameSettings.GameMode;

            void PaintModes()
            {
                for (int k = 0; k < modeCards.Count; k++)
                {
                    bool s = k == modeCurrent;
                    var c = modeCards[k];
                    c.style.backgroundColor = s ? UiTheme.GlassHi : UiTheme.Glass;
                    Color bc = s ? UiTheme.Accent : UiTheme.Edge;
                    UiTheme.Border(c, 1f, bc);
                    c.style.borderLeftWidth = 3f;
                    c.style.borderLeftColor = s ? UiTheme.Accent : UiTheme.Edge;
                }
            }

            void PickMode(int i)
            {
                modeCurrent = Mathf.Clamp(i, 0, modeCards.Count - 1);
                GameSettings.GameMode = (GameSettings.Mode)modeCurrent;
                GameSettings.Save();
                PaintModes();
                RefreshSummary();
                RefreshBriefing();
            }

            Button MakeModeCard(int idx, string title, string desc)
            {
                var card = new Button(() => { Click(); PickMode(idx); });
                card.name = "seg-modus-" + idx;
                card.style.flexGrow = 1f;
                card.style.flexBasis = 0f;
                card.style.minHeight = 116f;
                card.style.flexDirection = FlexDirection.Column;
                card.style.alignItems = Align.FlexStart;
                card.style.justifyContent = Justify.FlexStart;
                card.style.unityTextAlign = TextAnchor.UpperLeft;
                card.style.paddingLeft = 18f; card.style.paddingRight = 18f;
                card.style.paddingTop = 16f; card.style.paddingBottom = 16f;
                UiTheme.Square(card);
                UiTheme.Margin(card, 0f);
                if (idx > 0) card.style.marginLeft = 14f;

                var t = new Label(title);
                t.style.color = UiTheme.Text;
                t.style.fontSize = 17f;
                t.style.letterSpacing = 3f;
                t.style.unityFontStyleAndWeight = FontStyle.Bold;

                var d = new Label(desc);
                d.style.color = UiTheme.TextDim;
                d.style.fontSize = 12f;
                d.style.marginTop = 8f;
                d.style.whiteSpace = WhiteSpace.Normal;
                d.style.width = Length.Percent(100f);
                d.style.unityTextAlign = TextAnchor.UpperLeft;

                card.Add(t);
                card.Add(d);
                card.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    if (idx != modeCurrent) card.style.backgroundColor = UiTheme.GlassHi;
                });
                card.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    if (idx != modeCurrent) card.style.backgroundColor = UiTheme.Glass;
                });
                _actions[card.name] = () => PickMode(idx);
                modeCards.Add(card);
                return card;
            }

            modeRow.Add(MakeModeCard(0, "AUSSCHEIDEN",
                "Schalte das gegnerische Team komplett aus, dann ist die Runde gewonnen."));
            modeRow.Add(MakeModeCard(1, "BOMBE",
                "Lege die Bombe und halte sie - oder verhindere das Legen und entschaerfe."));
            left.Add(modeRow);
            PaintModes();

            // --- Teamgroesse + Bot-Staerke nebeneinander ---
            left.Add(UiTheme.Gap(24f));

            var twoCol = new VisualElement();
            twoCol.style.flexDirection = FlexDirection.Row;

            var colA = new VisualElement();
            colA.style.flexGrow = 1f; colA.style.flexBasis = 0f;
            colA.style.marginRight = 20f;
            colA.Add(UiTheme.Section("TEAMGROESSE"));
            colA.Add(Segmented("seg-team", new[] { "2", "3", "4", "5" },
                Mathf.Clamp(GameSettings.TeamSize - 2, 0, 3), i =>
                {
                    GameSettings.TeamSize = i + 2;
                    GameSettings.Save();
                    RefreshSummary();
                    RefreshBriefing();
                }));

            var colB = new VisualElement();
            colB.style.flexGrow = 1f; colB.style.flexBasis = 0f;
            colB.Add(UiTheme.Section("BOT-STAERKE"));
            colB.Add(Segmented("seg-diff", new[] { "LEICHT", "NORMAL", "SCHWER" },
                (int)GameSettings.Difficulty, i =>
                {
                    GameSettings.Difficulty = (GameSettings.Level)i;
                    GameSettings.Save();
                    RefreshSummary();
                    RefreshBriefing();
                }));

            twoCol.Add(colA);
            twoCol.Add(colB);
            left.Add(twoCol);

            top.Add(left);
            top.Add(BuildBriefing());
            host.Add(top);

            // --- Trennlinie + Zusammenfassung ---
            host.Add(UiTheme.Gap(18f));

            var line = new VisualElement();
            line.style.height = 1f;
            line.style.backgroundColor = UiTheme.Line;
            host.Add(line);

            _summary = new Label();
            _summary.style.color = UiTheme.TextDim;
            _summary.style.fontSize = 12f;
            _summary.style.letterSpacing = 2f;
            _summary.style.marginTop = 12f;
            _summary.style.marginBottom = 4f;
            _summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            host.Add(_summary);
            RefreshSummary();

            // --- Breiter Startbalken unten: die einzige orange Flaeche im Menue ---
            var start = new Button(() => { Click(SoundId.RundeStart, 0.6f); StartRound(); })
                { text = "▶   RUNDE STARTEN" };
            start.name = "btn-start";
            start.style.height = 62f;
            start.style.marginTop = 14f;
            start.style.fontSize = 18f;
            start.style.letterSpacing = 6f;
            start.style.color = Color.black;
            start.style.backgroundColor = UiTheme.Accent;
            start.style.unityFontStyleAndWeight = FontStyle.Bold;
            start.style.overflow = Overflow.Hidden;
            UiTheme.Square(start);
            UiTheme.Border(start, 0f, UiTheme.Accent);
            UiTheme.Margin(start, 0f);
            start.style.marginTop = 14f;
            start.RegisterCallback<MouseEnterEvent>(_ => start.style.backgroundColor = UiTheme.AccentBright);
            start.RegisterCallback<MouseLeaveEvent>(_ => start.style.backgroundColor = UiTheme.Accent);
            _actions["btn-start"] = StartRound;

            // Der eine Hero-Effekt: ein Licht-Wisch schraeg ueber den Knopf.
            var shine = new VisualElement();
            shine.style.position = Position.Absolute;
            shine.style.top = -20f; shine.style.width = 70f; shine.style.height = 150f;
            shine.style.backgroundColor = new Color(1f, 1f, 1f, 0.20f);
            shine.style.rotate = new Rotate(new Angle(18f, AngleUnit.Degree));
            shine.style.left = Length.Percent(-20f);
            shine.pickingMode = PickingMode.Ignore;
            start.Add(shine);
            bool shineRight = false;
            shine.schedule.Execute(() =>
            {
                shineRight = !shineRight;
                if (shineRight)
                {
                    shine.style.transitionProperty = new List<StylePropertyName> { "left" };
                    shine.style.transitionDuration = new List<TimeValue> { new TimeValue(720, TimeUnit.Millisecond) };
                    shine.style.transitionTimingFunction =
                        new List<EasingFunction> { new EasingFunction(EasingMode.EaseInOutSine) };
                    shine.style.left = Length.Percent(120f);
                }
                else
                {
                    shine.style.transitionProperty = new List<StylePropertyName>();
                    shine.style.left = Length.Percent(-20f);
                }
            }).Every(2600).StartingIn(2600);

            host.Add(start);
        }

        // ------------------------------------------------------------------
        //  Briefing rechts auf der SPIELEN-Seite
        // ------------------------------------------------------------------

        VisualElement BuildBriefing()
        {
            var card = new VisualElement();
            card.style.width = 300f;
            card.style.flexShrink = 0f;
            card.style.flexDirection = FlexDirection.Column;
            card.style.paddingLeft = 18f; card.style.paddingRight = 18f;
            card.style.paddingTop = 16f; card.style.paddingBottom = 16f;
            SoftPanel(card, 0.34f);

            card.Add(UiTheme.Section("AUFSTELLUNG"));

            _lineup = new VisualElement();
            _lineup.style.flexDirection = FlexDirection.Column;
            _lineup.style.marginTop = 8f;
            card.Add(_lineup);

            card.Add(UiTheme.Gap(12f));
            _briefLine = new Label();
            _briefLine.style.color = UiTheme.Ice;
            _briefLine.style.fontSize = 12f;
            _briefLine.style.letterSpacing = 1f;
            _briefLine.style.unityFontStyleAndWeight = FontStyle.Bold;
            _briefLine.style.whiteSpace = WhiteSpace.Normal;
            card.Add(_briefLine);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            card.Add(spacer);

            // kleine "Readout"-Zeilen ganz unten - reine Deko, fuellt die Hoehe
            card.Add(ReadoutRow("NETCODE", "HOST-AUTHORITATIV"));
            card.Add(ReadoutRow("TICKRATE", "64"));
            card.Add(ReadoutRow("REGION", "LOKAL"));

            RefreshBriefing();
            return card;
        }

        static VisualElement ReadoutRow(string key, string val)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.justifyContent = Justify.SpaceBetween;
            r.style.marginTop = 4f;
            var a = new Label(key);
            a.style.color = UiTheme.TextDim; a.style.fontSize = 10f; a.style.letterSpacing = 2f;
            var b = new Label(val);
            b.style.color = UiTheme.TextDim; b.style.fontSize = 10f; b.style.letterSpacing = 1f;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            r.Add(a); r.Add(b);
            return r;
        }

        static VisualElement RosterHead(string text, Color c)
        {
            var l = new Label(text);
            l.style.color = c;
            l.style.fontSize = 10f;
            l.style.letterSpacing = 3f;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 6f;
            l.style.marginBottom = 3f;
            l.style.opacity = 0.85f;
            return l;
        }

        static VisualElement RosterRow(string name, Color accent, int order)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.alignItems = Align.Center;
            r.style.marginTop = 3f;
            r.style.paddingLeft = 8f;
            r.style.paddingTop = 3f; r.style.paddingBottom = 3f;
            r.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f);
            r.style.borderLeftWidth = 2f;
            r.style.borderLeftColor = accent;

            var tag = new VisualElement();
            tag.style.width = 5f; tag.style.height = 5f;
            tag.style.backgroundColor = accent;
            tag.style.marginRight = 8f;

            var l = new Label(name);
            l.style.color = UiTheme.Text;
            l.style.fontSize = 12f;
            l.style.letterSpacing = 1f;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;

            r.Add(tag);
            r.Add(l);

            // gestaffelt einblenden
            r.style.opacity = 0f;
            r.style.translate = new Translate(10f, 0f, 0f);
            r.style.transitionProperty = new List<StylePropertyName> { "opacity", "translate" };
            r.style.transitionDuration = new List<TimeValue> { new TimeValue(200, TimeUnit.Millisecond) };
            r.style.transitionTimingFunction =
                new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
            r.schedule.Execute(() =>
            {
                r.style.opacity = 1f;
                r.style.translate = new Translate(0f, 0f, 0f);
            }).StartingIn(30 + order * 30);

            return r;
        }

        void RefreshBriefing()
        {
            if (_lineup != null)
            {
                _lineup.Clear();
                int n = Mathf.Clamp(GameSettings.TeamSize, 1, 10);

                _lineup.Add(RosterHead("DEIN TEAM", UiTheme.Ice));
                for (int i = 0; i < n; i++)
                    _lineup.Add(RosterRow(TeamNames[i % TeamNames.Length], UiTheme.Ice, i));

                _lineup.Add(RosterHead("GEGNER", UiTheme.Foe));
                for (int i = 0; i < n; i++)
                    _lineup.Add(RosterRow(FoeNames[i % FoeNames.Length], UiTheme.Foe, n + i));
            }

            if (_briefLine != null)
            {
                string mode = GameSettings.GameMode == GameSettings.Mode.Bombe ? "BOMBE" : "AUSSCHEIDEN";
                string diff = GameSettings.Difficulty switch
                {
                    GameSettings.Level.Leicht => "LEICHT",
                    GameSettings.Level.Schwer => "SCHWER",
                    _ => "NORMAL"
                };
                _briefLine.text = mode + "   ·   BOTS: " + diff;
            }
        }

        void RefreshSummary()
        {
            if (_summary == null) return;
            string mode = GameSettings.GameMode == GameSettings.Mode.Bombe ? "BOMBE" : "AUSSCHEIDEN";
            string diff = GameSettings.Difficulty switch
            {
                GameSettings.Level.Leicht => "LEICHT",
                GameSettings.Level.Schwer => "SCHWER",
                _ => "NORMAL"
            };
            int n = GameSettings.TeamSize;
            _summary.text = $"{mode}   ·   {n} GEGEN {n}   ·   BOTS {diff}";
        }

        void StartRound()
        {
            GameSettings.Save();
            if (GameFlow.Instance != null) GameFlow.Instance.ToArena();
        }

        // ------------------------------------------------------------------
        //  Seite: EINSTELLUNGEN  (einmal einstellen, nie wieder anfassen)
        // ------------------------------------------------------------------

        // ------------------------------------------------------------------
        //  Seite: ZUGAENGLICHKEIT
        // ------------------------------------------------------------------

        /// <summary>
        /// Eine eigene Seite, nicht ein Kasten unten in den Einstellungen.
        /// Wer diese Sachen braucht, soll sie finden, ohne zu suchen.
        /// </summary>
        void BuildZugaenglichkeit(VisualElement host)
        {
            Hinweis(host,
                "Diese Einstellungen aendern nichts am Schwierigkeitsgrad. "
                + "Sie aendern nur, wie das Spiel aussieht und wie es bedient wird.");

            // --- Schriftgroesse ---------------------------------------------
            host.Add(UiTheme.Gap(18f));
            host.Add(UiTheme.Section("GROESSE DER ANZEIGE"));
            host.Add(Regler("slider-uiscale", 0.8f, 1.6f, GameSettings.UiScale, "0.00", v =>
            {
                GameSettings.UiScale = v;
                GameSettings.Save();
                Zugaenglichkeit.UiGroesseAnwenden();
            }));
            Hinweis(host, "Vergroessert Menue und Anzeige im Spiel gemeinsam.");

            // --- Fadenkreuz --------------------------------------------------
            host.Add(UiTheme.Gap(18f));
            host.Add(UiTheme.Section("FADENKREUZ"));
            host.Add(Regler("slider-crosshair", 0.6f, 2f, GameSettings.CrosshairScale, "0.00", v =>
            {
                GameSettings.CrosshairScale = v;
                GameSettings.Save();
            }));
            Hinweis(host, "Groesser und dicker. Ein Fadenkreuz, das man nicht sieht, "
                          + "macht das ganze Spiel unspielbar.");

            // --- Farben ------------------------------------------------------
            host.Add(UiTheme.Gap(24f));
            host.Add(UiTheme.Section("FARBEN"));
            host.Add(Segmented("seg-farbe",
                new[] { "STANDARD", "ROT-GRUEN", "BLAU-GELB", "KONTRAST" },
                (int)GameSettings.ColorMode, i =>
                {
                    GameSettings.ColorMode = (GameSettings.Farbmodus)i;
                    GameSettings.Save();
                }));
            Hinweis(host,
                "Betrifft vor allem die Lebensanzeige. Gruen-gelb-rot laeuft bei "
                + "Rot-Gruen-Schwaeche ineinander; dann wird daraus blau-gelb-magenta.");

            // --- Bewegung ----------------------------------------------------
            host.Add(UiTheme.Gap(24f));
            host.Add(UiTheme.Section("WENIGER BEWEGUNG"));
            host.Add(Segmented("seg-motion", new[] { "AUS", "AN" },
                GameSettings.ReduceMotion ? 1 : 0, i =>
                {
                    GameSettings.ReduceMotion = i == 1;
                    GameSettings.Save();
                }));
            Hinweis(host,
                "Daempft Atem-Schwenk und Waffenwippen stark. Wenn dir vom Bild "
                + "schlecht wird, liegt das nicht an dir - schalte das hier an.");

            // --- Halten oder Umschalten --------------------------------------
            host.Add(UiTheme.Gap(24f));
            host.Add(UiTheme.Section("HALTEN ODER UMSCHALTEN"));

            host.Add(Zeile("ZIELEN", "seg-toggleaim", GameSettings.ToggleAim, v =>
            {
                GameSettings.ToggleAim = v; GameSettings.Save();
            }));
            host.Add(Zeile("DUCKEN", "seg-togglecrouch", GameSettings.ToggleCrouch, v =>
            {
                GameSettings.ToggleCrouch = v; GameSettings.Save();
            }));
            host.Add(Zeile("SPRINTEN", "seg-togglesprint", GameSettings.ToggleSprint, v =>
            {
                GameSettings.ToggleSprint = v; GameSettings.Save();
            }));
            Hinweis(host,
                "Dauerhaft gedrueckt halten tut auf Dauer weh, und mit einer Hand "
                + "geht es gar nicht. Die Tasten bleiben dieselben.");
        }

        /// <summary>Beschriftete Zeile mit HALTEN/UMSCHALTEN.</summary>
        VisualElement Zeile(string titel, string name, bool an, Action<bool> setzen)
        {
            var reihe = new VisualElement();
            reihe.style.flexDirection = FlexDirection.Row;
            reihe.style.alignItems = Align.Center;
            reihe.style.marginTop = 8f;

            var l = new Label(titel);
            l.style.color = UiTheme.TextDim;
            l.style.fontSize = 12f;
            l.style.letterSpacing = 2f;
            l.style.width = 130f;
            reihe.Add(l);

            var seg = Segmented(name, new[] { "HALTEN", "UMSCHALTEN" }, an ? 1 : 0,
                                i => setzen(i == 1));
            seg.style.flexGrow = 1f;
            reihe.Add(seg);
            return reihe;
        }

        /// <summary>Kleiner grauer Erklaertext unter einer Einstellung.</summary>
        static void Hinweis(VisualElement host, string text)
        {
            var l = new Label(text);
            l.style.color = UiTheme.TextDim;
            l.style.fontSize = 11f;
            l.style.marginTop = 6f;
            l.style.whiteSpace = WhiteSpace.Normal;
            host.Add(l);
        }

        /// <summary>Regler mit Zahl daneben. Dieselbe Optik wie Maus und Lautstaerke.</summary>
        static VisualElement Regler(string name, float min, float max, float wert,
                                    string format, Action<float> setzen)
        {
            var reihe = new VisualElement();
            reihe.style.flexDirection = FlexDirection.Row;
            reihe.style.alignItems = Align.Center;
            reihe.style.marginTop = 6f;

            var slider = new Slider(min, max) { value = wert, name = name };
            slider.style.flexGrow = 1f;

            var zahl = new Label(wert.ToString(format));
            zahl.style.color = UiTheme.Ice;
            zahl.style.width = 64f;
            zahl.style.unityTextAlign = TextAnchor.MiddleRight;
            zahl.style.unityFontStyleAndWeight = FontStyle.Bold;

            slider.RegisterValueChangedCallback(ev =>
            {
                setzen(ev.newValue);
                zahl.text = ev.newValue.ToString(format);
            });

            reihe.Add(slider);
            reihe.Add(zahl);
            return reihe;
        }

        void BuildEinstellungen(VisualElement host)
        {
            host.Add(UiTheme.Section("ANZEIGE"));
            host.Add(Segmented("seg-anzeige", new[] { "VOLLBILD", "FENSTER" },
                (int)GameSettings.DisplayMode, i =>
                {
                    var next = (GameSettings.Anzeige)i;
                    bool changed = next != GameSettings.DisplayMode;
                    GameSettings.DisplayMode = next;
                    GameSettings.Save();
                    // Nur bei echter Aenderung umschalten - nicht beim ersten Zeichnen.
                    if (changed) GraphicsBootstrap.ApplyDisplayMode();
                }));

            var anzeigeHint = new Label(
                "Vollbild: randloses Fenster in Bildschirmgröße. Fenster: 1280×720, "
                + "falls du schnell auf den Schreibtisch wechseln willst.");
            anzeigeHint.style.color = UiTheme.TextDim;
            anzeigeHint.style.fontSize = 11f;
            anzeigeHint.style.marginTop = 6f;
            anzeigeHint.style.whiteSpace = WhiteSpace.Normal;
            host.Add(anzeigeHint);

            host.Add(UiTheme.Gap(24f));
            host.Add(UiTheme.Section("BILD"));
            host.Add(Segmented("seg-grafik", new[] { "VOLL", "SCHLICHT" },
                (int)GameSettings.GraphicsQuality, i =>
                {
                    GameSettings.GraphicsQuality = (GameSettings.Graphics)i;
                    GameSettings.Save();
                }));

            var bildHint = new Label(
                "Voll: mit Tiefenunschärfe, Bloom, Vignette und Nebel. "
                + "Schlicht: alles aus, falls es ruckelt oder streift.");
            bildHint.style.color = UiTheme.TextDim;
            bildHint.style.fontSize = 11f;
            bildHint.style.marginTop = 6f;
            bildHint.style.whiteSpace = WhiteSpace.Normal;
            host.Add(bildHint);

            host.Add(UiTheme.Gap(24f));
            host.Add(UiTheme.Section("MAUS-EMPFINDLICHKEIT"));

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 6f;
            row.style.marginBottom = 8f;

            _sensSlider = new Slider(0.02f, 0.30f) { value = GameSettings.MouseSensitivity };
            _sensSlider.name = "slider-sens";
            _sensSlider.style.flexGrow = 1f;

            _sensValue = new Label(GameSettings.MouseSensitivity.ToString("0.00"));
            _sensValue.style.color = UiTheme.Ice;
            _sensValue.style.width = 64f;
            _sensValue.style.unityTextAlign = TextAnchor.MiddleRight;
            _sensValue.style.unityFontStyleAndWeight = FontStyle.Bold;

            _sensSlider.RegisterValueChangedCallback(ev =>
            {
                GameSettings.MouseSensitivity = ev.newValue;
                GameSettings.Save();
                if (_sensValue != null) _sensValue.text = ev.newValue.ToString("0.00");
            });

            row.Add(_sensSlider);
            row.Add(_sensValue);
            host.Add(row);

            host.Add(UiTheme.Gap(18f));
            host.Add(UiTheme.Section("LAUTSTAERKE"));

            var volRow = new VisualElement();
            volRow.style.flexDirection = FlexDirection.Row;
            volRow.style.alignItems = Align.Center;
            volRow.style.marginTop = 6f;
            volRow.style.marginBottom = 8f;

            _volSlider = new Slider(0f, 1f) { value = GameSettings.SfxVolume };
            _volSlider.name = "slider-volume";
            _volSlider.style.flexGrow = 1f;

            _volValue = new Label(Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + " %");
            _volValue.style.color = UiTheme.Ice;
            _volValue.style.width = 64f;
            _volValue.style.unityTextAlign = TextAnchor.MiddleRight;
            _volValue.style.unityFontStyleAndWeight = FontStyle.Bold;

            _volSlider.RegisterValueChangedCallback(ev =>
            {
                GameSettings.SfxVolume = ev.newValue;
                GameSettings.Save();
                if (_volValue != null) _volValue.text = Mathf.RoundToInt(ev.newValue * 100f) + " %";
            });

            volRow.Add(_volSlider);
            volRow.Add(_volValue);
            host.Add(volRow);
        }

        // ------------------------------------------------------------------
        //  Seite: STEUERUNG  (reine Tastenreferenz)
        // ------------------------------------------------------------------

        void BuildSteuerung(VisualElement host)
        {
            host.Add(UiTheme.Section("TASTENBELEGUNG"));

            var list = new VisualElement();
            list.style.marginTop = 4f;
            AddKey(list, "Bewegen", "W", "A", "S", "D");
            AddKey(list, "Umsehen", "Maus");
            AddKey(list, "Schiessen", "Linke Maustaste");
            AddKey(list, "Zielen (halten)", "Rechte Maustaste");
            AddKey(list, "Ducken (halten)", "Strg");
            AddKey(list, "Schleichen (halten)", "Alt");
            AddKey(list, "Nachladen", "R");
            AddKey(list, "Springen", "Leertaste");
            AddKey(list, "Sprinten (halten)", "Umschalt");
            AddKey(list, "Luft anhalten (im Zielfernrohr)", "Umschalt");
            AddKey(list, "Waffe wechseln", "1", "2");
            AddKey(list, "Bombe legen / entschärfen (halten)", "E");
            AddKey(list, "Kaufmenü", "B");
            AddKey(list, "Punktetabelle (halten)", "Tab");
            AddKey(list, "Pause", "Esc");
            AddKey(list, "Zuschauen wechseln (tot)", "Linksklick", "Rechtsklick");
            host.Add(list);
        }

        /// <summary>
        /// Quellen und Lizenzen. Rechtlich noetig ist das bei CC0 nicht - bei
        /// Mixamo schon, und ausserdem gehoert es sich: hinter jedem Ton und
        /// jedem Modell steckt jemand, der Arbeit hineingesteckt hat.
        /// </summary>
        void BuildQuellen(VisualElement host)
        {
            host.Add(UiTheme.Section("SPIEL"));
            var intro = new VisualElement();
            AddQuelle(intro, "INFRONT", "Driftlab", "Code und Spielaufbau");
            host.Add(intro);

            host.Add(UiTheme.Section("FIGUREN UND ANIMATIONEN"));
            var figuren = new VisualElement();
            AddQuelle(figuren, "Figur, Idle, Gehen, Rennen, Sterben",
                      "Mixamo (Adobe)",
                      "Nutzung im Spiel erlaubt. Die Dateien selbst duerfen "
                      + "NICHT einzeln weitergegeben werden.");
            host.Add(figuren);

            host.Add(UiTheme.Section("GERAEUSCHE"));
            var ton = new VisualElement();
            AddQuelle(ton, "Schussaufnahmen (AK-47, Carl Gustav M45, Mosin Nagant, 1911)",
                      "The Free Firearm Sound Library - opengameart.org",
                      "CC0. Aufgenommen von Ben Jaszczak, Brian Nelson, "
                      + "Kevin Heras und Matthew Nanney.");
            AddQuelle(ton, "Alle uebrigen Geraeusche", "Driftlab",
                      "Im Spiel selbst erzeugt.");
            host.Add(ton);

            host.Add(UiTheme.Section("MODELLE UND TEXTUREN"));
            var art = new VisualElement();
            AddQuelle(art, "Deckungen, Faesser, Kisten, Lampen, Rolltore, Hallenkran, Himmel",
                      "Poly Haven - polyhaven.com", "CC0, keine Namensnennung noetig.");
            AddQuelle(art, "Wand-, Boden- und Deckungstexturen",
                      "ambientCG - ambientcg.com", "CC0, keine Namensnennung noetig.");
            host.Add(art);

            host.Add(UiTheme.Section("WERKZEUGE"));
            var tools = new VisualElement();
            AddQuelle(tools, "Unity 6", "Unity Technologies", "");
            AddQuelle(tools, "Netcode for GameObjects", "Unity Technologies", "");
            host.Add(tools);

            var schluss = new Label(
                "CC0 heisst: frei nutzbar, auch geschaeftlich, ohne dass jemand "
                + "genannt werden muss. Genannt wird hier trotzdem.");
            schluss.style.color = UiTheme.TextDim;
            schluss.style.fontSize = 11f;
            schluss.style.whiteSpace = WhiteSpace.Normal;
            schluss.style.marginTop = 14f;
            host.Add(schluss);
        }

        void AddQuelle(VisualElement list, string was, string wer, string hinweis)
        {
            var r = new VisualElement();
            r.style.paddingTop = 6f;
            r.style.paddingBottom = 6f;
            r.style.borderBottomWidth = 1f;
            r.style.borderBottomColor = UiTheme.Line;

            var a = new Label(was);
            a.style.color = UiTheme.Text;
            a.style.fontSize = 13f;
            a.style.whiteSpace = WhiteSpace.Normal;
            r.Add(a);

            var b = new Label(wer);
            b.style.color = UiTheme.Ice;
            b.style.fontSize = 12f;
            b.style.whiteSpace = WhiteSpace.Normal;
            r.Add(b);

            if (!string.IsNullOrEmpty(hinweis))
            {
                var c = new Label(hinweis);
                c.style.color = UiTheme.TextDim;
                c.style.fontSize = 11f;
                c.style.whiteSpace = WhiteSpace.Normal;
                r.Add(c);
            }

            list.Add(r);
        }

        void AddKey(VisualElement list, string action, params string[] keys)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.justifyContent = Justify.SpaceBetween;
            r.style.alignItems = Align.Center;
            r.style.paddingTop = 6f; r.style.paddingBottom = 6f;
            r.style.borderBottomWidth = 1f;
            r.style.borderBottomColor = UiTheme.Line;

            var a = new Label(action);
            a.style.color = UiTheme.TextDim;
            a.style.fontSize = 13f;
            a.style.whiteSpace = WhiteSpace.Normal;
            a.style.flexShrink = 1f;

            var caps = new VisualElement();
            caps.style.flexDirection = FlexDirection.Row;
            caps.style.flexShrink = 0f;
            caps.style.marginLeft = 12f;
            foreach (var key in keys) caps.Add(KeyCap(key));

            r.Add(a);
            r.Add(caps);
            list.Add(r);
        }

        /// <summary>Eine kleine umrandete Tastenkappe wie auf einer echten Tastatur.</summary>
        static VisualElement KeyCap(string key)
        {
            var cap = new Label(key);
            cap.style.color = UiTheme.Text;
            cap.style.fontSize = 12f;
            cap.style.unityFontStyleAndWeight = FontStyle.Bold;
            cap.style.unityTextAlign = TextAnchor.MiddleCenter;
            cap.style.minWidth = 26f;
            cap.style.paddingLeft = 7f; cap.style.paddingRight = 7f;
            cap.style.paddingTop = 3f; cap.style.paddingBottom = 3f;
            cap.style.marginLeft = 6f;
            cap.style.backgroundColor = UiTheme.GlassHi;
            UiTheme.Border(cap, 1f, UiTheme.IceDim);
            UiTheme.Square(cap);
            return cap;
        }

        // ------------------------------------------------------------------
        //  Seite: BEENDEN
        // ------------------------------------------------------------------

        void BuildBeenden(VisualElement host)
        {
            var q = new Label("Spiel wirklich beenden?");
            q.style.color = UiTheme.Text;
            q.style.fontSize = 18f;
            q.style.unityFontStyleAndWeight = FontStyle.Bold;
            q.style.marginBottom = 22f;
            host.Add(q);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var yes = new Button(Quit) { text = "JA, BEENDEN" };
            yes.name = "btn-quit";
            StyleChoice(yes, danger: true);
            _actions["btn-quit"] = Quit;

            var no = new Button(() => ShowPage(Page.Spielen)) { text = "ZURUECK" };
            no.name = "btn-quit-cancel";
            StyleChoice(no, danger: false);
            no.style.marginLeft = 12f;
            _actions["btn-quit-cancel"] = () => ShowPage(Page.Spielen);

            row.Add(yes);
            row.Add(no);
            host.Add(row);
        }

        void StyleChoice(Button b, bool danger)
        {
            b.style.height = 48f;
            b.style.minWidth = 170f;
            b.style.fontSize = 14f;
            b.style.letterSpacing = 3f;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            UiTheme.Square(b);
            UiTheme.Margin(b, 0f);
            // Rot fuer die zerstoererische Wahl - Orange bleibt allein dem Startknopf.
            Color fill = danger ? UiTheme.Bad : UiTheme.Glass;
            Color fillHi = danger ? new Color32(0xF0, 0x50, 0x42, 0xFF) : UiTheme.GlassHi;
            b.style.backgroundColor = fill;
            b.style.color = danger ? Color.white : UiTheme.Text;
            UiTheme.Border(b, 1f, danger ? UiTheme.Bad : UiTheme.Edge);
            b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = fillHi);
            b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = fill);
        }

        void Quit()
        {
            GameSettings.Save();
            Application.Quit();
        }

        // ------------------------------------------------------------------
        //  Segment-Schalter (eine Reihe Knoepfe, einer aktiv)
        // ------------------------------------------------------------------

        VisualElement Segmented(string name, string[] labels, int selected, Action<int> onPick)
        {
            var rowEl = new VisualElement();
            rowEl.name = name;
            rowEl.style.flexDirection = FlexDirection.Row;
            rowEl.style.marginTop = 6f;

            var buttons = new List<Button>();
            int current = -1;

            void Paint()
            {
                for (int k = 0; k < buttons.Count; k++)
                {
                    bool s = k == current;
                    // Ausgewaehlt: helleres Glas + weisse Schrift + Akzent-Rand.
                    // KEIN oranger Fuellblock - Orange bleibt dem Startknopf.
                    buttons[k].style.backgroundColor = s ? UiTheme.GlassHi : UiTheme.Glass;
                    buttons[k].style.color = s ? Color.white : UiTheme.Text;
                    Color bc = s ? UiTheme.Accent : UiTheme.Edge;
                    buttons[k].style.borderTopColor = bc; buttons[k].style.borderBottomColor = bc;
                    buttons[k].style.borderLeftColor = bc; buttons[k].style.borderRightColor = bc;
                }
            }

            void Pick(int i)
            {
                current = Mathf.Clamp(i, 0, buttons.Count - 1);
                Paint();
                onPick(current);
            }

            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var b = new Button(() => { Click(); Pick(idx); }) { text = labels[i] };
                b.name = name + "-" + i;
                b.style.height = 42f;
                b.style.flexGrow = 1f;
                b.style.fontSize = 13f;
                b.style.letterSpacing = 2f;
                b.style.unityFontStyleAndWeight = FontStyle.Bold;
                UiTheme.Square(b);
                UiTheme.Border(b, 1f, UiTheme.Edge);
                UiTheme.Margin(b, 0f);
                b.style.marginTop = 0f;
                if (i > 0) b.style.marginLeft = -1f;   // Raender teilen sich eine Linie

                b.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    if (idx != current) b.style.backgroundColor = UiTheme.GlassHi;
                });
                b.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    if (idx != current) b.style.backgroundColor = UiTheme.Glass;
                });

                buttons.Add(b);
                rowEl.Add(b);
                _actions[b.name] = () => Pick(idx);
            }

            Pick(selected);
            return rowEl;
        }
    }
}
