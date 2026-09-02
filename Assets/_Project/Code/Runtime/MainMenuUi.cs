using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Infront
{
    /// <summary>
    /// Das Hauptmenue mit Unity UI Toolkit, Stil "Dark Tactical".
    /// Baut den kompletten Baum per Code (kein UXML), damit nichts still
    /// beim Import kaputtgehen kann.
    ///
    /// Aufbau der Navigation:
    ///  - SPIELEN        nur Sachen, die du vor jeder Runde entscheidest
    ///                   (Modus, Teamgroesse, Bot-Staerke) und der Startknopf.
    ///                   Rechts daneben das BRIEFING mit Mini-Karte und Aufstellung.
    ///  - EINSTELLUNGEN  Sachen, die du einmal einstellst: Anzeige, Bild, Maus, Ton.
    ///  - STEUERUNG      reine Tastenreferenz zum Nachschlagen.
    ///  - Beenden        abgesetzt unten, kein gleichwertiger Reiter.
    ///
    /// Optik: Die Flaechen sind halbdurchsichtiges Glas (<see cref="UiTheme.Glass"/>),
    /// dahinter laeuft die 3D-Kulisse mit der Kamerafahrt weiter. Orange bleibt
    /// die Aktionsfarbe, Eisblau (<see cref="UiTheme.Ice"/>) ist der kuehle
    /// Gegenpol fuer Zahlen und das eigene Team.
    ///
    /// Das alte IMGUI-Menue (<see cref="MainMenu"/>) bleibt als Rueckfallebene
    /// im Objektbaum: schlaegt der Aufbau hier fehl, oder drueckst du F10,
    /// erscheint wieder das alte Menue.
    ///
    /// NICHT pruefbar: wie es aussieht (Farben, Abstaende, Hover, Animation).
    /// Pruefbar ist nur, dass der Baum steht und die Schalter in GameSettings landen.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuUi : MonoBehaviour
    {
        enum Page { Spielen, Einstellungen, Steuerung, Beenden }

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

        UIDocument _doc;
        VisualElement _pageHost;         // Inhalt rechts, wird pro Seite geleert
        VisualElement _pageEdge;         // Akzent-Linie, die bei jedem Seitenwechsel ueber die Panel-Oberkante faehrt
        VisualElement _grid;             // feines Raster im Hintergrund, driftet langsam
        VisualElement _radar;            // drehender Zeiger auf der Mini-Karte
        VisualElement _lineup;           // Team-Punkte im Briefing (blau gegen rot)
        Label _briefLine;                // Modus-/Bot-Zeile im Briefing
        Slider _sensSlider;
        Label _sensValue;
        Slider _volSlider;
        Label _volValue;
        Label _summary;                  // Zeile ueber dem Startknopf
        bool _built;

        float _gridT;
        float _radarT;

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

            // Rein optische Bewegung: Raster driftet, Radar-Zeiger dreht.
            float dt = Time.unscaledDeltaTime;
            if (_grid != null)
            {
                _gridT = (_gridT + dt * 5f) % 64f;
                _grid.style.translate = new Translate(_gridT - 64f, _gridT * 0.6f - 64f, 0f);
            }
            if (_radar != null)
            {
                _radarT = (_radarT + dt * 55f) % 360f;
                _radar.style.rotate = new Rotate(new Angle(_radarT, AngleUnit.Degree));
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
            // Durchsichtig lassen - dahinter läuft die 3D-Kulisse mit der Kamerafahrt.
            root.style.backgroundColor = Color.clear;
            root.style.display = DisplayStyle.Flex;

            // Feines Raster über der ganzen Kulisse - driftet in Update() langsam.
            root.Add(BuildGrid());

            // Leichter Abdunkler über der ganzen Kulisse: nimmt der 3D-Szene
            // etwas Kontrast, lässt die Bewegung aber klar durchscheinen.
            var scrim = new VisualElement { name = "scrim" };
            scrim.style.position = Position.Absolute;
            scrim.style.left = 0f; scrim.style.top = 0f; scrim.style.right = 0f; scrim.style.bottom = 0f;
            var sc = UiTheme.Bg; sc.a = 0.28f;
            scrim.style.backgroundColor = sc;
            scrim.pickingMode = PickingMode.Ignore;
            root.Add(scrim);

            // Kräftigere Bänder oben und unten, wo Kopf- und Fußzeile direkt auf
            // der Kulisse sitzen - dort muss die Schrift immer lesbar bleiben.
            var bandTop = new VisualElement { name = "band-top" };
            bandTop.style.position = Position.Absolute;
            bandTop.style.left = 0f; bandTop.style.right = 0f; bandTop.style.top = 0f;
            bandTop.style.height = 150f;
            var bt = UiTheme.Bg; bt.a = 0.62f;
            bandTop.style.backgroundColor = bt;
            bandTop.pickingMode = PickingMode.Ignore;
            root.Add(bandTop);

            var bandBottom = new VisualElement { name = "band-bottom" };
            bandBottom.style.position = Position.Absolute;
            bandBottom.style.left = 0f; bandBottom.style.right = 0f; bandBottom.style.bottom = 0f;
            bandBottom.style.height = 74f;
            bandBottom.style.backgroundColor = bt;
            bandBottom.pickingMode = PickingMode.Ignore;
            root.Add(bandBottom);

            var header = BuildHeader();
            var hline = HLine();
            var body = BuildBody();
            var footer = BuildFooter();
            root.Add(header);
            root.Add(hline);
            root.Add(body);
            root.Add(footer);

            ShowPage(_page);

            // Auftritt: Kopf, Linie, Inhalt, Fuß nacheinander von unten einblenden.
            FadeUp(header, 40);
            FadeUp(hline, 100);
            FadeUp(body, 150);
            FadeUp(footer, 230);
        }

        /// <summary>Feines Linienraster als Hintergrund-Deko. Bewegt wird es in Update().</summary>
        VisualElement BuildGrid()
        {
            _grid = new VisualElement { name = "grid" };
            _grid.style.position = Position.Absolute;
            _grid.style.left = 0f; _grid.style.top = 0f;
            _grid.style.width = Length.Percent(140f);
            _grid.style.height = Length.Percent(140f);
            _grid.pickingMode = PickingMode.Ignore;

            var vCol = UiTheme.Ice;  vCol.a = 0.022f;
            var hCol = UiTheme.Accent; hCol.a = 0.022f;
            const int step = 64;
            for (int x = 0; x < 46; x++)
            {
                var v = new VisualElement();
                v.style.position = Position.Absolute;
                v.style.left = x * step; v.style.top = 0f;
                v.style.width = 1f; v.style.height = Length.Percent(100f);
                v.style.backgroundColor = vCol;
                _grid.Add(v);
            }
            for (int y = 0; y < 34; y++)
            {
                var h = new VisualElement();
                h.style.position = Position.Absolute;
                h.style.top = y * step; h.style.left = 0f;
                h.style.height = 1f; h.style.width = Length.Percent(100f);
                h.style.backgroundColor = hCol;
                _grid.Add(h);
            }
            return _grid;
        }

        // ------------------------------------------------------------------
        //  Kleine Animations-Helfer (rein optisch)
        // ------------------------------------------------------------------

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

        /// <summary>Zählt eine Zahl von 0 auf den Zielwert hoch.</summary>
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

        /// <summary>Lässt einen dünnen Balken einmal von 0 auf volle Breite wachsen.</summary>
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

        VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 48f; header.style.paddingRight = 48f;
            header.style.paddingTop = 26f; header.style.paddingBottom = 16f;

            var brand = new VisualElement();
            brand.style.flexDirection = FlexDirection.Row;
            brand.style.alignItems = Align.Center;

            var tick = new VisualElement();
            tick.style.width = 6f; tick.style.height = 34f;
            tick.style.backgroundColor = UiTheme.Accent;
            tick.style.marginRight = 14f;

            // Wortmarke mit zwei farbigen Geister-Bildern für den Glitch.
            var titleWrap = new VisualElement();
            titleWrap.style.position = Position.Relative;

            Label MakeTitle(Color c)
            {
                var t = new Label("INFRONT");
                t.style.color = c;
                t.style.fontSize = 34f;
                t.style.unityFontStyleAndWeight = FontStyle.Bold;
                t.style.letterSpacing = 8f;
                return t;
            }

            var ghostIce = MakeTitle(UiTheme.Ice);
            ghostIce.style.position = Position.Absolute;
            ghostIce.style.left = 0f; ghostIce.style.top = 0f;
            ghostIce.style.opacity = 0f;
            var ghostAcc = MakeTitle(UiTheme.Accent);
            ghostAcc.style.position = Position.Absolute;
            ghostAcc.style.left = 0f; ghostAcc.style.top = 0f;
            ghostAcc.style.opacity = 0f;

            var title = MakeTitle(UiTheme.Text);
            // Buchstaben laufen beim Auftritt von weit auf den Sollabstand zusammen.
            title.style.letterSpacing = 22f;
            title.schedule.Execute(() =>
            {
                title.style.transitionProperty = new List<StylePropertyName> { "letter-spacing" };
                title.style.transitionDuration = new List<TimeValue> { new TimeValue(600, TimeUnit.Millisecond) };
                title.style.transitionTimingFunction =
                    new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                title.style.letterSpacing = 8f;
            }).StartingIn(140);

            titleWrap.Add(ghostIce);
            titleWrap.Add(ghostAcc);
            titleWrap.Add(title);

            // Kurzer Versatz mit farbigen Geister-Bildern, alle ~9 Sekunden.
            void RunGlitch()
            {
                ghostIce.style.opacity = 0.55f;
                ghostIce.style.translate = new Translate(-4f, -1f, 0f);
                ghostAcc.style.opacity = 0.5f;
                ghostAcc.style.translate = new Translate(4f, 1f, 0f);
                titleWrap.schedule.Execute(() =>
                {
                    ghostIce.style.opacity = 0f;
                    ghostIce.style.translate = new Translate(0f, 0f, 0f);
                    ghostAcc.style.opacity = 0f;
                    ghostAcc.style.translate = new Translate(0f, 0f, 0f);
                }).StartingIn(90);
            }
            titleWrap.schedule.Execute(RunGlitch).Every(9000).StartingIn(4200);

            // schmaler Puls am Marken-Balken
            bool tickBright = false;
            tick.style.transitionProperty = new List<StylePropertyName> { "background-color" };
            tick.style.transitionDuration = new List<TimeValue> { new TimeValue(1400, TimeUnit.Millisecond) };
            tick.schedule.Execute(() =>
            {
                tickBright = !tickBright;
                tick.style.backgroundColor = tickBright ? UiTheme.AccentBright : UiTheme.Accent;
            }).Every(1400);

            brand.Add(tick);
            brand.Add(titleWrap);

            var version = new Label("DRIFTLAB   ·   " + VersionText());
            version.style.color = UiTheme.TextDim;
            version.style.fontSize = 12f;
            version.style.letterSpacing = 2f;
            version.style.unityFontStyleAndWeight = FontStyle.Bold;

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
            box.style.marginTop = 30f;
            UiTheme.Border(box, 1f, UiTheme.Edge);
            UiTheme.Pad(box, 14f);
            box.style.backgroundColor = UiTheme.Glass;

            box.Add(UiTheme.Section("LAUFBAHN"));

            // Neuer Spieler: vier Nullen sehen aus wie ein Fehler - stattdessen ein Hinweis.
            if (CareerStats.Matches <= 0)
            {
                var hint = new Label("Noch keine Runde gespielt");
                hint.style.color = UiTheme.TextDim;
                hint.style.fontSize = 11f;
                hint.style.marginTop = 2f;
                hint.style.whiteSpace = WhiteSpace.Normal;
                box.Add(hint);
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
                // dünner Balken unter der Zahl, wächst beim Auftritt einmal auf
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

        VisualElement HLine()
        {
            var l = new VisualElement();
            l.style.height = 1f;
            l.style.backgroundColor = UiTheme.Line;
            l.style.flexShrink = 0f;
            l.style.overflow = Overflow.Hidden;

            // Leuchtender Streifen, der langsam hin und her wandert - Farbe wechselt mit.
            var blip = new VisualElement();
            blip.style.position = Position.Absolute;
            blip.style.top = 0f;
            blip.style.height = 1f;
            blip.style.width = 130f;
            blip.style.backgroundColor = UiTheme.Accent;
            blip.style.left = Length.Percent(-12f);
            l.Add(blip);

            bool toRight = false;
            l.schedule.Execute(() =>
            {
                toRight = !toRight;
                blip.style.backgroundColor = toRight ? UiTheme.Ice : UiTheme.Accent;
                blip.style.transitionProperty = new List<StylePropertyName> { "left" };
                blip.style.transitionDuration = new List<TimeValue> { new TimeValue(3000, TimeUnit.Millisecond) };
                blip.style.transitionTimingFunction =
                    new List<EasingFunction> { new EasingFunction(EasingMode.EaseInOutSine) };
                blip.style.left = Length.Percent(toRight ? 100f : -12f);
            }).Every(3000).StartingIn(400);

            return l;
        }

        VisualElement BuildBody()
        {
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;
            // Navigation und Panel auf die volle Höhe strecken, damit das Menü
            // den Bildschirm füllt statt oben links zu kleben.
            body.style.alignItems = Align.Stretch;
            body.style.paddingLeft = 72f; body.style.paddingRight = 72f;
            body.style.paddingTop = 34f; body.style.paddingBottom = 24f;

            // ---- Navigation links (füllt die ganze Spaltenhöhe) ----
            var nav = new VisualElement();
            nav.style.width = 260f;
            nav.style.flexShrink = 0f;
            nav.style.marginRight = 40f;
            nav.Add(NavButton("SPIELEN", Page.Spielen));
            nav.Add(NavButton("EINSTELLUNGEN", Page.Einstellungen));
            nav.Add(NavButton("STEUERUNG", Page.Steuerung));

            var sep = new VisualElement();
            sep.style.height = 1f;
            sep.style.backgroundColor = UiTheme.Line;
            sep.style.marginTop = 6f; sep.style.marginBottom = 10f;
            nav.Add(sep);

            nav.Add(NavButton("Beenden", Page.Beenden, minor: true));
            nav.Add(BuildCareer());

            // schiebt Tipp + Status an die Unterkante
            var navSpacer = new VisualElement();
            navSpacer.style.flexGrow = 1f;
            nav.Add(navSpacer);

            nav.Add(BuildTipBox());
            nav.Add(BuildStatusLine());
            body.Add(nav);

            // ---- Inhalt rechts (Glas-Kasten mit Akzent-Ecke) ----
            var panel = new VisualElement();
            panel.style.flexGrow = 1f;
            panel.style.overflow = Overflow.Hidden;   // für Scan-Streifen und Kantenlinie
            panel.style.backgroundColor = UiTheme.Glass;
            UiTheme.Border(panel, 1f, UiTheme.Edge);
            UiTheme.Pad(panel, 34f);

            // Glanz oben auf dem Glas (fake Verlauf: heller Streifen, der oben aufliegt).
            var sheen = new VisualElement();
            sheen.style.position = Position.Absolute;
            sheen.style.left = 0f; sheen.style.right = 0f; sheen.style.top = 0f;
            sheen.style.height = 140f;
            sheen.style.backgroundColor = UiTheme.Sheen;
            sheen.pickingMode = PickingMode.Ignore;
            panel.Add(sheen);

            // Echte L-Ecke oben links - fährt beim Auftritt kurz aus.
            var cornerH = new VisualElement();
            cornerH.style.position = Position.Absolute;
            cornerH.style.left = -1f; cornerH.style.top = -1f;
            cornerH.style.width = 0f; cornerH.style.height = 3f;
            cornerH.style.backgroundColor = UiTheme.Accent;
            panel.Add(cornerH);

            var cornerV = new VisualElement();
            cornerV.style.position = Position.Absolute;
            cornerV.style.left = -1f; cornerV.style.top = -1f;
            cornerV.style.width = 3f; cornerV.style.height = 0f;
            cornerV.style.backgroundColor = UiTheme.Accent;
            panel.Add(cornerV);

            cornerH.schedule.Execute(() =>
            {
                cornerH.style.transitionProperty = new List<StylePropertyName> { "width" };
                cornerH.style.transitionDuration = new List<TimeValue> { new TimeValue(420, TimeUnit.Millisecond) };
                cornerH.style.transitionTimingFunction =
                    new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                cornerH.style.width = 54f;
                cornerV.style.transitionProperty = new List<StylePropertyName> { "height" };
                cornerV.style.transitionDuration = new List<TimeValue> { new TimeValue(420, TimeUnit.Millisecond) };
                cornerV.style.transitionTimingFunction =
                    new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
                cornerV.style.height = 54f;
            }).StartingIn(320);

            // Akzent-Linie, die bei jedem Seitenwechsel über die Oberkante fährt.
            _pageEdge = new VisualElement();
            _pageEdge.style.position = Position.Absolute;
            _pageEdge.style.left = 0f; _pageEdge.style.top = -1f;
            _pageEdge.style.height = 2f;
            _pageEdge.style.width = Length.Percent(0f);
            _pageEdge.style.backgroundColor = UiTheme.Ice;
            _pageEdge.pickingMode = PickingMode.Ignore;
            panel.Add(_pageEdge);

            // Feiner Scan-Streifen, der langsam über das Panel nach unten wandert.
            var scan = new VisualElement();
            scan.style.position = Position.Absolute;
            scan.style.left = 0f; scan.style.right = 0f;
            scan.style.height = 2f;
            scan.style.top = Length.Percent(-4f);
            var scanCol = UiTheme.Ice; scanCol.a = 0.14f;
            scan.style.backgroundColor = scanCol;
            scan.pickingMode = PickingMode.Ignore;
            panel.Add(scan);
            bool scanDown = false;
            panel.schedule.Execute(() =>
            {
                scanDown = !scanDown;
                scan.style.transitionProperty = new List<StylePropertyName> { "top" };
                scan.style.transitionDuration = new List<TimeValue> { new TimeValue(4200, TimeUnit.Millisecond) };
                scan.style.transitionTimingFunction =
                    new List<EasingFunction> { new EasingFunction(EasingMode.EaseInOutSine) };
                scan.style.top = Length.Percent(scanDown ? 104f : -4f);
            }).Every(4200).StartingIn(600);

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
            box.style.backgroundColor = UiTheme.GlassDeep;
            UiTheme.Border(box, 1f, UiTheme.Edge);
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
            }).Every(6500).StartingIn(6500);

            return box;
        }

        /// <summary>Status-Zeile ganz unten in der Navigation, mit blinkendem Punkt.</summary>
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
            dot.style.transitionDuration = new List<TimeValue> { new TimeValue(700, TimeUnit.Millisecond) };
            bool on = true;
            dot.schedule.Execute(() => { on = !on; dot.style.opacity = on ? 1f : 0.15f; }).Every(900);

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
            footer.style.paddingLeft = 48f; footer.style.paddingRight = 48f;
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
            var b = new Button(() => ShowPage(page)) { text = text };
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

            // Akzent-Balken links, wächst beim Drüberfahren von oben nach unten rein.
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
                if (_page != page) growBar.style.height = Length.Percent(0f);
                if (_page != page)
                {
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
            _radar = null;

            // Andere Seiten nicht endlos breit ziehen; SPIELEN darf die volle Breite.
            if (page == Page.Spielen) _pageHost.style.maxWidth = StyleKeyword.None;
            else _pageHost.style.maxWidth = 720f;

            // Akzent-Linie einmal über die Panel-Oberkante ziehen.
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
                case Page.Steuerung: BuildSteuerung(_pageHost); break;
                case Page.Beenden: BuildBeenden(_pageHost); break;
            }
            StaggerIn(_pageHost);
        }

        /// <summary>Blendet die Kinder eines Elements nacheinander von rechts ein -
        /// gibt dem Menue einen weicheren Auftritt statt hartem Umschalten.</summary>
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
        //  Seite: SPIELEN  (Runde links, Briefing rechts)
        // ------------------------------------------------------------------

        void BuildSpielen(VisualElement host)
        {
            host.style.flexGrow = 1f;

            // Obere Zeile: links die Entscheidungen, rechts das Briefing.
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.flexGrow = 1f;

            var left = new VisualElement();
            left.style.flexGrow = 1f;
            left.style.flexBasis = 0f;
            left.style.marginRight = 34f;

            // --- Spielmodus: zwei Karten mit erklaerender Zeile ---
            left.Add(UiTheme.Section("SPIELMODUS"));

            var modeRow = new VisualElement();
            modeRow.style.flexDirection = FlexDirection.Row;
            modeRow.style.marginTop = 6f;

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
                var card = new Button(() => PickMode(idx));
                card.name = "seg-modus-" + idx;
                card.style.flexGrow = 1f;
                card.style.flexBasis = 0f;
                card.style.flexDirection = FlexDirection.Column;
                card.style.alignItems = Align.FlexStart;
                card.style.justifyContent = Justify.FlexStart;
                card.style.unityTextAlign = TextAnchor.UpperLeft;
                UiTheme.Pad(card, 16f);
                UiTheme.Square(card);
                UiTheme.Margin(card, 0f);
                if (idx > 0) card.style.marginLeft = 12f;

                var t = new Label(title);
                t.style.color = UiTheme.Text;
                t.style.fontSize = 15f;
                t.style.letterSpacing = 2f;
                t.style.unityFontStyleAndWeight = FontStyle.Bold;

                var d = new Label(desc);
                d.style.color = UiTheme.TextDim;
                d.style.fontSize = 12f;
                d.style.marginTop = 6f;
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
                "Schaltet das gegnerische Team komplett aus, dann ist die Runde gewonnen."));
            modeRow.Add(MakeModeCard(1, "BOMBE",
                "Ein Team legt die Bombe, das andere entschärft sie oder verhindert das Legen."));
            left.Add(modeRow);
            PaintModes();

            // --- Teamgroesse + Bot-Staerke nebeneinander ---
            left.Add(UiTheme.Gap(22f));

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

            // --- Trennlinie + Zusammenfassung: hier hoeren die Einstellungen auf ---
            host.Add(UiTheme.Gap(20f));

            var line = new VisualElement();
            line.style.height = 1f;
            line.style.backgroundColor = UiTheme.Line;
            host.Add(line);

            _summary = new Label();
            _summary.style.color = UiTheme.TextDim;
            _summary.style.fontSize = 12f;
            _summary.style.letterSpacing = 2f;
            _summary.style.marginTop = 12f;
            _summary.style.marginBottom = 14f;
            _summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            host.Add(_summary);
            RefreshSummary();

            // --- Startknopf: die einzige orange Flaeche im Menue ---
            var start = new Button(StartRound) { text = "▶   RUNDE STARTEN" };
            start.name = "btn-start";
            start.style.height = 54f;
            start.style.fontSize = 17f;
            start.style.letterSpacing = 4f;
            start.style.color = Color.black;
            start.style.backgroundColor = UiTheme.Accent;
            start.style.unityFontStyleAndWeight = FontStyle.Bold;
            start.style.overflow = Overflow.Hidden;
            UiTheme.Square(start);
            UiTheme.Border(start, 0f, UiTheme.Accent);
            UiTheme.Margin(start, 0f);
            start.RegisterCallback<MouseEnterEvent>(_ => start.style.backgroundColor = UiTheme.AccentBright);
            start.RegisterCallback<MouseLeaveEvent>(_ => start.style.backgroundColor = UiTheme.Accent);
            _actions["btn-start"] = StartRound;

            // Glanz-Streifen, der schräg über den Knopf wischt.
            var shine = new VisualElement();
            shine.style.position = Position.Absolute;
            shine.style.top = -20f; shine.style.width = 60f; shine.style.height = 120f;
            shine.style.backgroundColor = new Color(1f, 1f, 1f, 0.22f);
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
                    shine.style.transitionDuration = new List<TimeValue> { new TimeValue(700, TimeUnit.Millisecond) };
                    shine.style.transitionTimingFunction =
                        new List<EasingFunction> { new EasingFunction(EasingMode.EaseInOutSine) };
                    shine.style.left = Length.Percent(120f);
                }
                else
                {
                    shine.style.transitionProperty = new List<StylePropertyName>();
                    shine.style.left = Length.Percent(-20f);
                }
            }).Every(1600).StartingIn(1600);

            host.Add(start);

            // Der Startknopf „atmet" ruhig - zieht das Auge ohne zu blinken.
            start.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f), 0f);
            start.style.transitionProperty = new List<StylePropertyName> { "scale", "background-color" };
            start.style.transitionDuration = new List<TimeValue>
                { new TimeValue(1200, TimeUnit.Millisecond), new TimeValue(120, TimeUnit.Millisecond) };
            start.style.transitionTimingFunction = new List<EasingFunction>
                { new EasingFunction(EasingMode.EaseInOut), new EasingFunction(EasingMode.EaseOutCubic) };
            bool grew = false;
            start.schedule.Execute(() =>
            {
                grew = !grew;
                float s = grew ? 1.02f : 1f;
                start.style.scale = new Scale(new Vector3(s, s, 1f));
            }).Every(1200);
        }

        // ------------------------------------------------------------------
        //  Briefing rechts auf der SPIELEN-Seite
        // ------------------------------------------------------------------

        VisualElement BuildBriefing()
        {
            var card = new VisualElement();
            card.style.width = 320f;
            card.style.flexShrink = 0f;
            card.style.flexDirection = FlexDirection.Column;
            card.style.backgroundColor = UiTheme.GlassDeep;
            UiTheme.Border(card, 1f, UiTheme.Edge);
            UiTheme.Pad(card, 16f);

            card.Add(UiTheme.Section("BRIEFING"));

            // --- Mini-Karte ---
            var map = new VisualElement();
            map.style.height = 180f;
            map.style.marginTop = 6f;
            map.style.backgroundColor = new Color(0.02f, 0.03f, 0.04f, 0.9f);
            UiTheme.Border(map, 1f, UiTheme.Edge);
            map.style.overflow = Overflow.Hidden;

            // Spielfeld-Rahmen
            var field = new VisualElement();
            field.style.position = Position.Absolute;
            field.style.left = 18f; field.style.right = 18f; field.style.top = 14f; field.style.bottom = 14f;
            var fCol = UiTheme.Ice; fCol.a = 0.25f;
            UiTheme.Border(field, 1f, fCol);
            map.Add(field);

            // Mittellinie
            var mid = new VisualElement();
            mid.style.position = Position.Absolute;
            mid.style.left = 18f; mid.style.right = 18f;
            mid.style.top = Length.Percent(50f);
            mid.style.height = 1f;
            var mCol = UiTheme.Line; mCol.a = 0.8f;
            mid.style.backgroundColor = mCol;
            map.Add(mid);

            // zwei Deckungs-Blöcke
            AddMapBlock(map, Length.Percent(30f), Length.Percent(36f), 46f, 14f);
            AddMapBlock(map, Length.Percent(58f), Length.Percent(52f), 34f, 16f);

            // Bombenplätze A und B
            AddSite(map, "A", Length.Percent(24f), Length.Percent(22f));
            AddSite(map, "B", Length.Percent(66f), Length.Percent(24f));

            // Radar-Zeiger, dreht in Update()
            _radar = new VisualElement();
            _radar.style.position = Position.Absolute;
            _radar.style.left = Length.Percent(50f);
            _radar.style.top = Length.Percent(50f);
            _radar.style.width = 2f;
            _radar.style.height = 66f;
            _radar.style.transformOrigin = new TransformOrigin(Length.Percent(50f), 0f, 0f);
            var rCol = UiTheme.Ice; rCol.a = 0.55f;
            _radar.style.backgroundColor = rCol;
            _radar.pickingMode = PickingMode.Ignore;
            map.Add(_radar);

            var center = new VisualElement();
            center.style.position = Position.Absolute;
            center.style.left = Length.Percent(50f); center.style.top = Length.Percent(50f);
            center.style.width = 6f; center.style.height = 6f;
            center.style.marginLeft = -3f; center.style.marginTop = -3f;
            center.style.backgroundColor = UiTheme.Ice;
            map.Add(center);

            card.Add(map);

            // --- Aufstellung: blaue Punkte gegen rote ---
            card.Add(UiTheme.Gap(14f));
            var lineHead = new Label("AUFSTELLUNG");
            lineHead.style.color = UiTheme.TextDim;
            lineHead.style.fontSize = 11f;
            lineHead.style.letterSpacing = 3f;
            lineHead.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(lineHead);

            _lineup = new VisualElement();
            _lineup.style.flexDirection = FlexDirection.Row;
            _lineup.style.alignItems = Align.Center;
            _lineup.style.marginTop = 8f;
            card.Add(_lineup);

            // --- Modus-/Bot-Zeile ---
            card.Add(UiTheme.Gap(14f));
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

            // kleine "Readout"-Zeilen ganz unten - reine Deko, füllt die Höhe
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

        static void AddMapBlock(VisualElement map, Length left, Length top, float w, float h)
        {
            var block = new VisualElement();
            block.style.position = Position.Absolute;
            block.style.left = left; block.style.top = top;
            block.style.width = w; block.style.height = h;
            var c = UiTheme.Ice; c.a = 0.12f;
            block.style.backgroundColor = c;
            var e = UiTheme.Ice; e.a = 0.3f;
            UiTheme.Border(block, 1f, e);
            map.Add(block);
        }

        static void AddSite(VisualElement map, string name, Length left, Length top)
        {
            var s = new VisualElement();
            s.style.position = Position.Absolute;
            s.style.left = left; s.style.top = top;
            s.style.width = 18f; s.style.height = 18f;
            s.style.alignItems = Align.Center;
            s.style.justifyContent = Justify.Center;
            var fill = UiTheme.Accent; fill.a = 0.16f;
            s.style.backgroundColor = fill;
            UiTheme.Border(s, 1f, UiTheme.Accent);

            var l = new Label(name);
            l.style.color = UiTheme.Accent;
            l.style.fontSize = 10f;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            s.Add(l);

            // sanftes Pulsieren
            s.style.transitionProperty = new List<StylePropertyName> { "opacity" };
            s.style.transitionDuration = new List<TimeValue> { new TimeValue(1100, TimeUnit.Millisecond) };
            bool bright = false;
            s.schedule.Execute(() => { bright = !bright; s.style.opacity = bright ? 1f : 0.45f; }).Every(1100);
            map.Add(s);
        }

        void RefreshBriefing()
        {
            if (_lineup != null)
            {
                _lineup.Clear();
                int n = Mathf.Clamp(GameSettings.TeamSize, 1, 10);
                for (int i = 0; i < n; i++) _lineup.Add(TeamDot(UiTheme.Ice, i));

                var vs = new Label("VS");
                vs.style.color = UiTheme.TextDim;
                vs.style.fontSize = 11f;
                vs.style.letterSpacing = 2f;
                vs.style.unityFontStyleAndWeight = FontStyle.Bold;
                vs.style.marginLeft = 8f; vs.style.marginRight = 8f;
                _lineup.Add(vs);

                for (int i = 0; i < n; i++) _lineup.Add(TeamDot(UiTheme.Foe, n + i));
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
                _briefLine.text = mode + "\nBOTS: " + diff;
            }
        }

        static VisualElement TeamDot(Color color, int index)
        {
            var d = new VisualElement();
            d.style.width = 12f; d.style.height = 12f;
            d.style.marginRight = 5f;
            d.style.backgroundColor = color;
            // poppt einzeln rein
            d.style.scale = new Scale(new Vector3(0f, 0f, 1f));
            d.style.transitionProperty = new List<StylePropertyName> { "scale" };
            d.style.transitionDuration = new List<TimeValue> { new TimeValue(220, TimeUnit.Millisecond) };
            d.style.transitionTimingFunction =
                new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) };
            d.schedule.Execute(() => d.style.scale = new Scale(Vector3.one)).StartingIn(40 + index * 35);
            return d;
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
                    // Nur bei echter Änderung umschalten - nicht beim ersten Zeichnen.
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
                "Voll: mit Bloom, Vignette und Nebel. Schlicht: alles aus, falls es ruckelt oder streift.");
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
            AddKey(list, "Umsehen / Zielen", "Maus");
            AddKey(list, "Schiessen", "Linke Maustaste");
            AddKey(list, "Nachladen", "R");
            AddKey(list, "Springen", "Leertaste");
            AddKey(list, "Sprinten (halten)", "Umschalt");
            AddKey(list, "Waffe wechseln", "1", "2");
            AddKey(list, "Bombe legen / entschärfen (halten)", "E");
            AddKey(list, "Kaufmenü", "B");
            AddKey(list, "Punktetabelle (halten)", "Tab");
            AddKey(list, "Pause", "Esc");
            AddKey(list, "Zuschauen wechseln (tot)", "Linksklick", "Rechtsklick");
            host.Add(list);
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
                var b = new Button(() => Pick(idx)) { text = labels[i] };
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
