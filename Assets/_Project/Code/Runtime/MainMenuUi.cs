using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Infront
{
    /// <summary>
    /// Das neue Hauptmenue mit Unity UI Toolkit, Stil "Dark Tactical".
    /// Baut den kompletten Baum per Code (kein UXML), damit nichts still
    /// beim Import kaputtgehen kann.
    ///
    /// Aufbau der Navigation:
    ///  - SPIELEN        nur Sachen, die du vor jeder Runde entscheidest
    ///                   (Modus, Teamgroesse, Bot-Staerke) und der Startknopf.
    ///  - EINSTELLUNGEN  Sachen, die du einmal einstellst: Bild, Maus, Lautstaerke.
    ///  - STEUERUNG      reine Tastenreferenz zum Nachschlagen.
    ///  - Beenden        abgesetzt unten, kein gleichwertiger Reiter.
    ///
    /// Das alte IMGUI-Menue (<see cref="MainMenu"/>) bleibt als Rueckfallebene
    /// im Objektbaum: schlaegt der Aufbau hier fehl, oder drueckst du F10,
    /// erscheint wieder das alte Menue.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuUi : MonoBehaviour
    {
        enum Page { Spielen, Einstellungen, Steuerung, Beenden }

        UIDocument _doc;
        VisualElement _pageHost;         // Inhalt rechts, wird pro Seite geleert
        Slider _sensSlider;
        Label _sensValue;
        Slider _volSlider;
        Label _volValue;
        Label _summary;                  // Zeile ueber dem Startknopf
        bool _built;

        Page _page = Page.Spielen;
        readonly Dictionary<Page, Button> _navButtons = new();

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
            if (!_built && enabled) TryBuild();

            var kb = Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame)
                ToggleFallback();
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

            // Abdunkler über der Kulisse: hält Schrift lesbar, lässt die
            // Bewegung aber durchscheinen.
            var scrim = new VisualElement { name = "scrim" };
            scrim.style.position = Position.Absolute;
            scrim.style.left = 0f; scrim.style.top = 0f; scrim.style.right = 0f; scrim.style.bottom = 0f;
            var sc = UiTheme.Bg; sc.a = 0.58f;
            scrim.style.backgroundColor = sc;
            root.Add(scrim);

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

            var title = new Label("INFRONT");
            title.style.color = UiTheme.Text;
            title.style.fontSize = 34f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
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
            brand.Add(title);

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
            box.style.marginTop = 40f;
            UiTheme.Border(box, 1f, UiTheme.Line);
            UiTheme.Pad(box, 14f);
            box.style.backgroundColor = UiTheme.Panel;

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

            void Row(string label, int value)
            {
                var r = new VisualElement();
                r.style.flexDirection = FlexDirection.Row;
                r.style.justifyContent = Justify.SpaceBetween;
                r.style.marginTop = 4f;
                var a = new Label(label); a.style.color = UiTheme.TextDim; a.style.fontSize = 12f;
                var b = new Label("0");
                b.style.color = UiTheme.Text; b.style.fontSize = 12f;
                b.style.unityFontStyleAndWeight = FontStyle.Bold;
                r.Add(a); r.Add(b);
                box.Add(r);
                CountUp(b, value);
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

            // Leuchtender Streifen, der langsam hin und her wandert.
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
            // Kinder oben ausrichten, nicht auf volle Hoehe strecken - sonst wird
            // das Inhalts-Panel zu einem riesigen leeren Kasten.
            body.style.alignItems = Align.FlexStart;
            body.style.paddingLeft = 48f; body.style.paddingRight = 48f;
            body.style.paddingTop = 34f; body.style.paddingBottom = 22f;

            // Navigation links
            var nav = new VisualElement();
            nav.style.width = 240f;
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
            body.Add(nav);

            // Inhalt rechts (Kasten mit Akzent-Ecke)
            var panel = new VisualElement();
            panel.style.flexGrow = 1f;
            panel.style.maxWidth = 780f;
            panel.style.backgroundColor = UiTheme.Panel;
            UiTheme.Border(panel, 1f, UiTheme.Line);
            UiTheme.Pad(panel, 30f);

            // Echte L-Ecke oben links statt eines kaum sichtbaren Strichs.
            var cornerH = new VisualElement();
            cornerH.style.position = Position.Absolute;
            cornerH.style.left = -1f; cornerH.style.top = -1f;
            cornerH.style.width = 46f; cornerH.style.height = 3f;
            cornerH.style.backgroundColor = UiTheme.Accent;
            panel.Add(cornerH);

            var cornerV = new VisualElement();
            cornerV.style.position = Position.Absolute;
            cornerV.style.left = -1f; cornerV.style.top = -1f;
            cornerV.style.width = 3f; cornerV.style.height = 46f;
            cornerV.style.backgroundColor = UiTheme.Accent;
            panel.Add(cornerV);

            _pageHost = new VisualElement();
            _pageHost.style.flexGrow = 1f;
            _pageHost.style.maxWidth = 640f;   // Inhalt nicht ueber den halben Monitor ziehen
            panel.Add(_pageHost);

            body.Add(panel);
            return body;
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
            b.style.backgroundColor = UiTheme.Panel;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            UiTheme.Square(b);
            UiTheme.Border(b, 1f, UiTheme.Line);
            b.style.borderLeftWidth = 3f;
            b.style.borderLeftColor = UiTheme.Line;
            b.style.transitionProperty = new List<StylePropertyName> { "background-color", "translate" };
            b.style.transitionDuration = new List<TimeValue> { new TimeValue(120, TimeUnit.Millisecond) };

            b.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (_page != page)
                {
                    b.style.backgroundColor = UiTheme.PanelHi;
                    b.style.translate = new Translate(6f, 0f, 0f);
                }
            });
            b.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (_page != page)
                {
                    b.style.backgroundColor = UiTheme.Panel;
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
                kv.Value.style.backgroundColor = sel ? UiTheme.PanelHi : UiTheme.Panel;
                kv.Value.style.borderLeftColor = sel ? UiTheme.Accent : UiTheme.Line;
                kv.Value.style.translate = new Translate(sel ? 4f : 0f, 0f, 0f);
                kv.Value.style.color = sel
                    ? Color.white
                    : (kv.Key == Page.Beenden ? UiTheme.TextDim : UiTheme.Text);
            }

            // Regler-Referenzen gehoeren zur Einstellungen-Seite; beim Verlassen ungueltig.
            if (page != Page.Einstellungen)
            {
                _sensSlider = null; _sensValue = null;
                _volSlider = null; _volValue = null;
            }
            _summary = null;

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

        /// <summary>Blendet die Kinder eines Elements nacheinander von unten ein -
        /// gibt dem Menue einen weicheren Auftritt statt hartem Umschalten.</summary>
        static void StaggerIn(VisualElement host)
        {
            int i = 0;
            foreach (var child in host.Children())
            {
                child.style.opacity = 0f;
                child.style.translate = new Translate(22f, 8f, 0f);
                int delay = 30 + i * 45;
                var c = child;
                host.schedule.Execute(() =>
                {
                    c.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName>
                        { "opacity", "translate" };
                    c.style.transitionDuration = new System.Collections.Generic.List<TimeValue>
                        { new TimeValue(200, TimeUnit.Millisecond) };
                    c.style.transitionTimingFunction = new System.Collections.Generic.List<EasingFunction>
                        { new EasingFunction(EasingMode.EaseOutCubic) };
                    c.style.opacity = 1f;
                    c.style.translate = new Translate(0f, 0f, 0f);
                }).StartingIn(delay);
                i++;
            }
        }

        // ------------------------------------------------------------------
        //  Seite: SPIELEN  (nur die Runde)
        // ------------------------------------------------------------------

        void BuildSpielen(VisualElement host)
        {
            // --- Spielmodus: zwei Karten mit erklaerender Zeile ---
            host.Add(UiTheme.Section("SPIELMODUS"));

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
                    c.style.backgroundColor = s ? UiTheme.PanelHi : UiTheme.Panel;
                    Color bc = s ? UiTheme.Accent : UiTheme.Line;
                    UiTheme.Border(c, 1f, bc);
                    c.style.borderLeftWidth = 3f;
                    c.style.borderLeftColor = s ? UiTheme.Accent : UiTheme.Line;
                }
            }

            void PickMode(int i)
            {
                modeCurrent = Mathf.Clamp(i, 0, modeCards.Count - 1);
                GameSettings.GameMode = (GameSettings.Mode)modeCurrent;
                GameSettings.Save();
                PaintModes();
                RefreshSummary();
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
                // Buttons stehen per Default auf zentriertem Text und vererben das an
                // die Kind-Labels. Hier soll alles linksbuendig sein.
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
                    if (idx != modeCurrent) card.style.backgroundColor = UiTheme.PanelHi;
                });
                card.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    if (idx != modeCurrent) card.style.backgroundColor = UiTheme.Panel;
                });
                _actions[card.name] = () => PickMode(idx);
                modeCards.Add(card);
                return card;
            }

            modeRow.Add(MakeModeCard(0, "AUSSCHEIDEN",
                "Schaltet das gegnerische Team komplett aus, dann ist die Runde gewonnen."));
            modeRow.Add(MakeModeCard(1, "BOMBE",
                "Ein Team legt die Bombe, das andere entschärft sie oder verhindert das Legen."));
            host.Add(modeRow);
            PaintModes();

            // --- Teamgroesse + Bot-Staerke nebeneinander (kleinere Entscheidungen) ---
            host.Add(UiTheme.Gap(22f));

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
                }));

            twoCol.Add(colA);
            twoCol.Add(colB);
            host.Add(twoCol);

            // --- Trennlinie + Zusammenfassung: hier hoeren die Einstellungen auf ---
            host.Add(UiTheme.Gap(28f));

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
            UiTheme.Square(start);
            UiTheme.Border(start, 0f, UiTheme.Accent);
            UiTheme.Margin(start, 0f);
            start.RegisterCallback<MouseEnterEvent>(_ => start.style.backgroundColor = UiTheme.AccentBright);
            start.RegisterCallback<MouseLeaveEvent>(_ => start.style.backgroundColor = UiTheme.Accent);
            _actions["btn-start"] = StartRound;
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
            _sensValue.style.color = UiTheme.Accent;
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
            _volValue.style.color = UiTheme.Accent;
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
            cap.style.backgroundColor = UiTheme.PanelHi;
            UiTheme.Border(cap, 1f, UiTheme.Line);
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
            Color fill = danger ? UiTheme.Bad : UiTheme.Panel;
            Color fillHi = danger ? new Color32(0xF0, 0x50, 0x42, 0xFF) : UiTheme.PanelHi;
            b.style.backgroundColor = fill;
            b.style.color = danger ? Color.white : UiTheme.Text;
            UiTheme.Border(b, 1f, danger ? UiTheme.Bad : UiTheme.Line);
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
                    // Ausgewaehlt: heller Kasten + weisse Schrift + Akzent-Rand.
                    // KEIN oranger Fuellblock mehr - Orange bleibt dem Startknopf.
                    buttons[k].style.backgroundColor = s ? UiTheme.PanelHi : UiTheme.Panel;
                    buttons[k].style.color = s ? Color.white : UiTheme.Text;
                    Color bc = s ? UiTheme.Accent : UiTheme.Line;
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
                UiTheme.Border(b, 1f, UiTheme.Line);
                UiTheme.Margin(b, 0f);
                b.style.marginTop = 0f;
                if (i > 0) b.style.marginLeft = -1f;   // Raender teilen sich eine Linie

                b.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    if (idx != current) b.style.backgroundColor = UiTheme.PanelHi;
                });
                b.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    if (idx != current) b.style.backgroundColor = UiTheme.Panel;
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
