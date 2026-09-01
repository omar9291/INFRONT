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
    /// Das alte IMGUI-Menue (<see cref="MainMenu"/>) bleibt als Rueckfallebene
    /// im Objektbaum: schlaegt der Aufbau hier fehl, oder drueckst du F10,
    /// erscheint wieder das alte Menue.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuUi : MonoBehaviour
    {
        enum Page { Spielen, Steuerung, Beenden }

        UIDocument _doc;
        VisualElement _pageHost;         // Inhalt rechts, wird pro Seite geleert
        Slider _sensSlider;
        Label _sensValue;
        Slider _volSlider;
        Label _volValue;
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
            root.style.backgroundColor = UiTheme.Bg;
            root.style.display = DisplayStyle.Flex;

            root.Add(BuildHeader());
            root.Add(HLine());
            root.Add(BuildBody());
            root.Add(BuildFooter());

            ShowPage(_page);
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
            title.style.letterSpacing = 8f;

            brand.Add(tick);
            brand.Add(title);

            var version = new Label("DRIFTLAB   ·   V0.9");
            version.style.color = UiTheme.TextDim;
            version.style.fontSize = 12f;
            version.style.letterSpacing = 2f;
            version.style.unityFontStyleAndWeight = FontStyle.Bold;

            header.Add(brand);
            header.Add(version);
            return header;
        }

        VisualElement BuildCareer()
        {
            var box = new VisualElement();
            box.style.marginTop = 40f;
            UiTheme.Border(box, 1f, UiTheme.Line);
            UiTheme.Pad(box, 14f);
            box.style.backgroundColor = UiTheme.Panel;

            box.Add(UiTheme.Section("LAUFBAHN"));

            void Row(string label, int value)
            {
                var r = new VisualElement();
                r.style.flexDirection = FlexDirection.Row;
                r.style.justifyContent = Justify.SpaceBetween;
                r.style.marginTop = 4f;
                var a = new Label(label); a.style.color = UiTheme.TextDim; a.style.fontSize = 12f;
                var b = new Label(value.ToString());
                b.style.color = UiTheme.Text; b.style.fontSize = 12f;
                b.style.unityFontStyleAndWeight = FontStyle.Bold;
                r.Add(a); r.Add(b);
                box.Add(r);
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
            return l;
        }

        VisualElement BuildBody()
        {
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1f;
            body.style.paddingLeft = 48f; body.style.paddingRight = 48f;
            body.style.paddingTop = 34f; body.style.paddingBottom = 22f;

            // Navigation links
            var nav = new VisualElement();
            nav.style.width = 240f;
            nav.style.flexShrink = 0f;
            nav.style.marginRight = 40f;
            nav.Add(NavButton("SPIELEN", Page.Spielen));
            nav.Add(NavButton("STEUERUNG", Page.Steuerung));
            nav.Add(NavButton("BEENDEN", Page.Beenden));
            nav.Add(BuildCareer());
            body.Add(nav);

            // Inhalt rechts (Kasten mit Akzent-Ecke)
            var panel = new VisualElement();
            panel.style.flexGrow = 1f;
            panel.style.backgroundColor = UiTheme.Panel;
            UiTheme.Border(panel, 1f, UiTheme.Line);
            UiTheme.Pad(panel, 30f);

            var corner = new VisualElement();
            corner.style.position = Position.Absolute;
            corner.style.left = -1f; corner.style.top = -1f;
            corner.style.width = 46f; corner.style.height = 3f;
            corner.style.backgroundColor = UiTheme.Accent;
            panel.Add(corner);

            _pageHost = new VisualElement();
            _pageHost.style.flexGrow = 1f;
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

        Button NavButton(string text, Page page)
        {
            var b = new Button(() => ShowPage(page)) { text = text };
            b.name = "nav-" + page.ToString().ToLowerInvariant();
            b.style.height = 46f;
            b.style.marginTop = 0f; b.style.marginBottom = 8f;
            b.style.marginLeft = 0f; b.style.marginRight = 0f;
            b.style.paddingLeft = 18f;
            b.style.fontSize = 14f;
            b.style.letterSpacing = 3f;
            b.style.color = UiTheme.Text;
            b.style.backgroundColor = UiTheme.Panel;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            UiTheme.Square(b);
            UiTheme.Border(b, 1f, UiTheme.Line);
            b.style.borderLeftWidth = 3f;
            b.style.borderLeftColor = UiTheme.Line;

            b.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (_page != page) b.style.backgroundColor = UiTheme.PanelHi;
            });
            b.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (_page != page) b.style.backgroundColor = UiTheme.Panel;
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
                kv.Value.style.color = sel ? Color.white : UiTheme.Text;
            }

            if (_pageHost == null) return;
            _pageHost.Clear();
            switch (page)
            {
                case Page.Spielen: BuildSpielen(_pageHost); break;
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
                child.style.translate = new Translate(0f, 14f, 0f);
                int delay = 30 + i * 45;
                var c = child;
                host.schedule.Execute(() =>
                {
                    c.style.transitionProperty = new System.Collections.Generic.List<StylePropertyName>
                        { "opacity", "translate" };
                    c.style.transitionDuration = new System.Collections.Generic.List<TimeValue>
                        { new TimeValue(180, TimeUnit.Millisecond) };
                    c.style.opacity = 1f;
                    c.style.translate = new Translate(0f, 0f, 0f);
                }).StartingIn(delay);
                i++;
            }
        }

        // ------------------------------------------------------------------
        //  Seite: SPIELEN
        // ------------------------------------------------------------------

        void BuildSpielen(VisualElement host)
        {
            host.Add(UiTheme.Section("SPIELMODUS"));
            host.Add(Segmented("seg-modus", new[] { "AUSSCHEIDEN", "BOMBE" },
                (int)GameSettings.GameMode, i =>
                {
                    GameSettings.GameMode = (GameSettings.Mode)i;
                    GameSettings.Save();
                }));

            host.Add(UiTheme.Gap(22f));
            host.Add(UiTheme.Section("TEAMGROESSE"));
            host.Add(Segmented("seg-team", new[] { "2", "3", "4", "5" },
                Mathf.Clamp(GameSettings.TeamSize - 2, 0, 3), i =>
                {
                    GameSettings.TeamSize = i + 2;
                    GameSettings.Save();
                }));

            host.Add(UiTheme.Gap(22f));
            host.Add(UiTheme.Section("BOT-SCHWIERIGKEIT"));
            host.Add(Segmented("seg-diff", new[] { "LEICHT", "NORMAL", "SCHWER" },
                (int)GameSettings.Difficulty, i =>
                {
                    GameSettings.Difficulty = (GameSettings.Level)i;
                    GameSettings.Save();
                }));

            host.Add(UiTheme.Gap(22f));
            host.Add(UiTheme.Section("BILD"));
            host.Add(Segmented("seg-grafik", new[] { "VOLL", "SCHLICHT" },
                (int)GameSettings.GraphicsQuality, i =>
                {
                    GameSettings.GraphicsQuality = (GameSettings.Graphics)i;
                    GameSettings.Save();
                }));

            host.Add(UiTheme.Gap(40f));

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
        }

        void StartRound()
        {
            GameSettings.Save();
            if (GameFlow.Instance != null) GameFlow.Instance.ToArena();
        }

        // ------------------------------------------------------------------
        //  Seite: STEUERUNG
        // ------------------------------------------------------------------

        void BuildSteuerung(VisualElement host)
        {
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

            host.Add(UiTheme.Gap(24f));
            host.Add(UiTheme.Section("TASTENBELEGUNG"));

            var list = new VisualElement();
            list.style.marginTop = 4f;
            AddKey(list, "Bewegen", "W  A  S  D");
            AddKey(list, "Umsehen / Zielen", "Maus");
            AddKey(list, "Schiessen", "Linke Maustaste");
            AddKey(list, "Nachladen", "R");
            AddKey(list, "Springen", "Leertaste");
            AddKey(list, "Sprinten", "Umschalt (halten)");
            AddKey(list, "Waffe wechseln", "1  /  2");
            AddKey(list, "Bombe legen / entschaerfen", "E (halten)");
            AddKey(list, "Kaufmenue", "B");
            AddKey(list, "Punktetabelle", "Tab (halten)");
            AddKey(list, "Pause", "Esc");
            AddKey(list, "Zuschauen wechseln (tot)", "Links- / Rechtsklick");
            host.Add(list);
        }

        void AddKey(VisualElement list, string action, string key)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row;
            r.style.justifyContent = Justify.SpaceBetween;
            r.style.paddingTop = 5f; r.style.paddingBottom = 5f;
            r.style.borderBottomWidth = 1f;
            r.style.borderBottomColor = UiTheme.Line;

            var a = new Label(action);
            a.style.color = UiTheme.TextDim;
            a.style.fontSize = 13f;

            var k = new Label(key);
            k.style.color = UiTheme.Text;
            k.style.fontSize = 13f;
            k.style.unityFontStyleAndWeight = FontStyle.Bold;

            r.Add(a);
            r.Add(k);
            list.Add(r);
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
            Color fill = danger ? UiTheme.Accent : UiTheme.Panel;
            Color fillHi = danger ? UiTheme.AccentBright : UiTheme.PanelHi;
            b.style.backgroundColor = fill;
            b.style.color = danger ? Color.black : UiTheme.Text;
            UiTheme.Border(b, 1f, danger ? UiTheme.Accent : UiTheme.Line);
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
                    buttons[k].style.backgroundColor = s ? UiTheme.Accent : UiTheme.Panel;
                    buttons[k].style.color = s ? Color.black : UiTheme.Text;
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
