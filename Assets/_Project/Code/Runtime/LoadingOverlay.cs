using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace Infront
{
    /// <summary>
    /// Ladebildschirm im Stil "Dark Tactical". Haengt am GameFlow-Objekt und
    /// ueberlebt den Szenenwechsel, damit er den Uebergang Menue -> Arena
    /// (und zurueck) ueberbrueckt.
    ///
    /// Reines UI Toolkit, per Code gebaut. Ohne PanelSettings
    /// (Resources/InfrontPanel) schaltet er sich still ab - das Spiel laeuft
    /// dann ohne Ladebildschirm weiter.
    /// </summary>
    public sealed class LoadingOverlay : MonoBehaviour
    {
        public static LoadingOverlay Instance { get; private set; }

        static readonly string[] Tips =
        {
            "Hold E to plant or defuse the bomb.",
            "Headshots do double damage.",
            "If you die, you stay dead - there is no respawn mid-round.",
            "Survivors carry their weapon and armor into the next round.",
            "Sides are swapped after 15 rounds.",
            "When dead, left and right click switch which teammate you watch.",
            "Press B at the start of a round to open the buy menu.",
            "Body armor absorbs half of all body damage.",
        };

        UIDocument _doc;
        VisualElement _screen;      // Vollbild-Hintergrund
        VisualElement _barFill;
        VisualElement _scan;        // wandernde Linie
        VisualElement _pattern;     // driftendes Streifenmuster im Hintergrund
        VisualElement _glow;        // pulsierendes Leuchten hinter der Wortmarke
        Label _percent;
        Label _tip;
        Label _mode;
        Label _dots;                // "." ".." "..." beim Laden
        Label _phaseLabel;          // was gerade passiert, im Klartext
        string _phase = "";

        float _patternT;
        float _pulseT;
        float _tipTimer;
        int _tipIndex;

        bool _ready;
        bool _visible;
        float _shownProgress;
        float _targetProgress;
        float _opacity;
        float _opacityTarget;
        float _scanT;

        bool _pendingBegin;
        string _pendingMode = "";
        string _pendingContext = "ARENA";

        // ---- Test-Schnittstelle ----
        public bool ReadyForTests => _ready;
        public bool IsVisibleForTests => _visible;
        public float ShownProgressForTests => _shownProgress;
        public void SnapProgressForTests() { _shownProgress = _targetProgress; UpdateBar(); }
        public void ForceHideForTests()
        {
            _opacityTarget = 0f; _opacity = 0f; _visible = false;
            if (_screen != null) { _screen.style.opacity = 0f; _screen.style.display = DisplayStyle.None; }
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            var panel = Resources.Load<PanelSettings>("InfrontPanel");
            if (panel == null)
            {
                Debug.LogWarning("[Infront] Kein InfrontPanel in Resources - Ladebildschirm aus.");
                enabled = false;
                return;
            }

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.sortingOrder = 100f;   // ueber dem Menue
            StartCoroutine(BuildWhenReady());
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        IEnumerator BuildWhenReady()
        {
            int guard = 0;
            while ((_doc == null || _doc.rootVisualElement == null) && guard++ < 240)
                yield return null;

            if (_doc == null || _doc.rootVisualElement == null)
            {
                Debug.LogWarning("[Infront] Ladebildschirm: UIDocument wurde nicht bereit.");
                enabled = false;
                yield break;
            }

            Build(_doc.rootVisualElement);
            _ready = true;
            _opacity = 0f;
            _opacityTarget = 0f;
            _visible = false;
            _screen.style.opacity = 0f;
            _screen.style.display = DisplayStyle.None;

            if (_pendingBegin) { _pendingBegin = false; Begin(_pendingMode, _pendingContext); }
        }

        // ------------------------------------------------------------------
        //  Oeffentlich (von GameFlow benutzt)
        // ------------------------------------------------------------------

        /// <summary>Ladebildschirm einblenden. modeLabel steht unten rechts.</summary>
        public void Begin(string modeLabel) => Begin(modeLabel, "ARENA");

        /// <summary>
        /// Wie <see cref="Begin(string)"/>, aber mit eigenem Kontext links vom
        /// Punkt. Der Startbildschirm meldet sich als "START", der
        /// Szenenwechsel als "ARENA" - so sieht man sofort, wofuer gewartet
        /// wird.
        /// </summary>
        public void Begin(string modeLabel, string context)
        {
            if (!_ready) { _pendingBegin = true; _pendingMode = modeLabel; _pendingContext = context; return; }

            _shownProgress = 0f;
            _targetProgress = 0.02f;
            _scanT = 0f;
            _phase = "PREPARING";
            if (_phaseLabel != null) _phaseLabel.text = _phase;
            _tipTimer = 0f;
            _tipIndex = Random.Range(0, Tips.Length);
            if (_tip != null) _tip.text = Tips[_tipIndex];
            if (_mode != null)
                _mode.text = (string.IsNullOrEmpty(context) ? "ARENA" : context)
                             + "   ·   " + (string.IsNullOrEmpty(modeLabel) ? "-" : modeLabel);

            _visible = true;
            _opacityTarget = 1f;
            if (Application.isBatchMode) { _opacity = 1f; }
            _screen.style.display = DisplayStyle.Flex;
            _screen.style.opacity = _opacity;
            UpdateBar();
        }

        /// <summary>Fortschritt 0..1 setzen. Der Balken zieht weich nach.</summary>
        /// <summary>
        /// Fortschritt MIT Phasennamen. Ein Balken ohne Beschriftung ist eine
        /// Behauptung - man sieht nicht, ob das Spiel arbeitet oder haengt.
        /// Mit Phasennamen sieht man beides.
        /// </summary>
        public void SetProgress(float p01, string phase)
        {
            if (!string.IsNullOrEmpty(phase))
            {
                _phase = phase;
                if (_phaseLabel != null) _phaseLabel.text = phase;
            }
            SetProgress(p01);
        }

        /// <summary>Die aktuelle Phase im Klartext. Nur fuer Tests.</summary>
        public string PhaseForTests => _phase;

        public void SetProgress(float p01)
        {
            _targetProgress = Mathf.Clamp01(p01);
            if (Application.isBatchMode) { _shownProgress = _targetProgress; UpdateBar(); }
        }

        /// <summary>Ausblenden. Wartet, bis die Deckkraft auf 0 ist.</summary>
        public IEnumerator PlayOutAndHide()
        {
            if (!_ready) yield break;
            _opacityTarget = 0f;
            if (Application.isBatchMode) { ForceHideForTests(); yield break; }

            float guard = 0f;
            while (_opacity > 0.01f && guard < 3f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }
            _visible = false;
            _screen.style.display = DisplayStyle.None;
        }

        // ------------------------------------------------------------------
        //  Laufzeit
        // ------------------------------------------------------------------

        void Update()
        {
            if (!_ready) return;

            _shownProgress = Mathf.MoveTowards(_shownProgress, _targetProgress,
                Time.unscaledDeltaTime * 1.4f);
            UpdateBar();

            float step = Application.isBatchMode ? 10f : Time.unscaledDeltaTime * 6f;
            _opacity = Mathf.MoveTowards(_opacity, _opacityTarget, step);
            if (_screen != null)
            {
                _screen.style.opacity = _opacity;
                bool show = _opacity > 0.001f || _opacityTarget > 0f;
                _screen.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                _visible = show;
            }

            if (!_visible) return;

            float dt = Time.unscaledDeltaTime;

            if (_scan != null)
            {
                _scanT = (_scanT + dt * 0.4f) % 1f;
                _scan.style.top = Length.Percent(8f + _scanT * 84f);
            }

            // Hintergrundmuster driftet langsam.
            if (_pattern != null)
            {
                _patternT = (_patternT + dt * 12f) % 64f;
                _pattern.style.left = -64f + _patternT;
                _pattern.style.top = -64f + _patternT * 0.5f;
            }

            // Leuchten hinter der Wortmarke pulsiert.
            if (_glow != null)
            {
                _pulseT += dt;
                float k = 0.35f + 0.25f * Mathf.Sin(_pulseT * 2.2f);
                _glow.style.opacity = k;
            }

            // Lade-Punkte.
            if (_dots != null)
            {
                int n = 1 + Mathf.FloorToInt(_pulseT * 2f) % 3;
                _dots.text = new string('.', n);
            }

            // Tipp wechselt alle paar Sekunden.
            _tipTimer += dt;
            if (_tipTimer > 4.5f && _tip != null)
            {
                _tipTimer = 0f;
                _tipIndex = (_tipIndex + 1) % Tips.Length;
                _tip.text = Tips[_tipIndex];
            }
        }

        void UpdateBar()
        {
            if (_barFill != null) _barFill.style.width = Length.Percent(_shownProgress * 100f);
            if (_percent != null) _percent.text = Mathf.RoundToInt(_shownProgress * 100f) + " %";
        }

        // ------------------------------------------------------------------
        //  Aufbau
        // ------------------------------------------------------------------

        void Build(VisualElement root)
        {
            root.Clear();

            _screen = new VisualElement();
            _screen.name = "loading-screen";
            _screen.style.position = Position.Absolute;
            _screen.style.left = 0f; _screen.style.top = 0f;
            _screen.style.right = 0f; _screen.style.bottom = 0f;
            _screen.style.backgroundColor = UiTheme.Bg;
            _screen.style.alignItems = Align.Center;
            _screen.style.justifyContent = Justify.Center;
            _screen.pickingMode = PickingMode.Ignore;
            _screen.style.overflow = Overflow.Hidden;
            root.Add(_screen);

            // driftendes Streifenmuster (Deko, sehr dezent)
            _pattern = new VisualElement();
            _pattern.style.position = Position.Absolute;
            _pattern.style.width = Length.Percent(140f);
            _pattern.style.height = Length.Percent(140f);
            _pattern.style.flexDirection = FlexDirection.Row;
            _pattern.style.flexWrap = Wrap.Wrap;
            _pattern.pickingMode = PickingMode.Ignore;
            var stripeCol = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.035f);
            for (int i = 0; i < 220; i++)
            {
                var bar = new VisualElement();
                bar.style.width = 22f; bar.style.height = 3f;
                bar.style.marginRight = 42f; bar.style.marginBottom = 61f;
                bar.style.rotate = new Rotate(new Angle(-24f, AngleUnit.Degree));
                bar.style.backgroundColor = stripeCol;
                _pattern.Add(bar);
            }
            _screen.Add(_pattern);

            // Ecken-Klammern wie ein HUD-Rahmen
            AddCorner(true, true); AddCorner(false, true);
            AddCorner(true, false); AddCorner(false, false);

            // wandernde Scan-Linie (Deko)
            _scan = new VisualElement();
            _scan.style.position = Position.Absolute;
            _scan.style.left = 0f; _scan.style.right = 0f;
            _scan.style.height = 1f;
            _scan.style.backgroundColor = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.16f);
            _screen.Add(_scan);

            // Mitte: Wortmarke + Balken
            var center = new VisualElement();
            center.style.width = Length.Percent(46f);
            center.style.minWidth = 420f;
            center.style.alignItems = Align.FlexStart;

            var brandRow = new VisualElement();
            brandRow.style.flexDirection = FlexDirection.Row;
            brandRow.style.alignItems = Align.Center;
            brandRow.style.marginBottom = 6f;

            var tick = new VisualElement();
            tick.style.width = 6f; tick.style.height = 40f;
            tick.style.backgroundColor = UiTheme.Accent;
            tick.style.marginRight = 16f;

            // Leuchten hinter der Wortmarke
            _glow = new VisualElement();
            _glow.style.position = Position.Absolute;
            _glow.style.left = -30f; _glow.style.width = 260f; _glow.style.height = 60f;
            _glow.style.backgroundColor = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.5f);
            _glow.style.opacity = 0.3f;
            foreach (var e in new[] { _glow })
            {
                e.style.borderTopLeftRadius = 30f; e.style.borderTopRightRadius = 30f;
                e.style.borderBottomLeftRadius = 30f; e.style.borderBottomRightRadius = 30f;
            }
            brandRow.Add(_glow);

            var title = new Label("INFRONT");
            title.style.color = UiTheme.Text;
            title.style.fontSize = 42f;
            title.style.letterSpacing = 10f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            brandRow.Add(tick);
            brandRow.Add(title);
            center.Add(brandRow);

            var subtitle = new Label("TACTICAL TEAM SHOOTER");
            subtitle.style.color = UiTheme.TextDim;
            subtitle.style.fontSize = 11f;
            subtitle.style.letterSpacing = 5f;
            subtitle.style.marginBottom = 24f;
            subtitle.style.marginLeft = 22f;
            center.Add(subtitle);

            // Balken-Spur
            var track = new VisualElement();
            track.style.width = Length.Percent(100f);
            track.style.height = 4f;
            track.style.backgroundColor = UiTheme.Line;

            _barFill = new VisualElement();
            _barFill.style.height = 4f;
            _barFill.style.width = Length.Percent(0f);
            _barFill.style.backgroundColor = UiTheme.Accent;
            track.Add(_barFill);
            center.Add(track);

            // Prozent + Status
            var infoRow = new VisualElement();
            infoRow.style.flexDirection = FlexDirection.Row;
            infoRow.style.justifyContent = Justify.SpaceBetween;
            infoRow.style.width = Length.Percent(100f);
            infoRow.style.marginTop = 10f;

            var loadingWrap = new VisualElement();
            loadingWrap.style.flexDirection = FlexDirection.Row;

            var loadingLabel = new Label("LOADING");
            loadingLabel.style.color = UiTheme.TextDim;
            loadingLabel.style.fontSize = 12f;
            loadingLabel.style.letterSpacing = 3f;
            loadingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

            _dots = new Label(".");
            _dots.style.color = UiTheme.Accent;
            _dots.style.fontSize = 12f;
            _dots.style.unityFontStyleAndWeight = FontStyle.Bold;
            _dots.style.width = 20f;
            loadingWrap.Add(loadingLabel);
            loadingWrap.Add(_dots);

            _percent = new Label("0 %");
            _percent.style.color = UiTheme.Accent;
            _percent.style.fontSize = 12f;
            _percent.style.unityFontStyleAndWeight = FontStyle.Bold;

            infoRow.Add(loadingWrap);
            infoRow.Add(_percent);
            center.Add(infoRow);

            // Phasenzeile: was gerade wirklich passiert. Ohne sie ist der
            // Balken nur eine Behauptung - man sieht nicht, ob das Spiel
            // arbeitet oder haengt.
            _phaseLabel = new Label(_phase);
            _phaseLabel.name = "loading-phase";
            _phaseLabel.style.color = UiTheme.Ice;
            _phaseLabel.style.fontSize = 11f;
            _phaseLabel.style.letterSpacing = 3f;
            _phaseLabel.style.marginTop = 8f;
            _phaseLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            center.Add(_phaseLabel);

            // Tipp
            _tip = new Label(Tips[0]);
            _tip.style.color = UiTheme.TextDim;
            _tip.style.fontSize = 13f;
            _tip.style.marginTop = 28f;
            _tip.style.whiteSpace = WhiteSpace.Normal;
            center.Add(_tip);

            _screen.Add(center);

            // Fusszeilen
            var footL = new Label("DRIFTLAB");
            footL.style.position = Position.Absolute;
            footL.style.left = 48f; footL.style.bottom = 30f;
            footL.style.color = UiTheme.TextDim;
            footL.style.fontSize = 11f;
            footL.style.letterSpacing = 3f;
            footL.style.unityFontStyleAndWeight = FontStyle.Bold;
            _screen.Add(footL);

            _mode = new Label("ARENA   ·   -");
            _mode.style.position = Position.Absolute;
            _mode.style.right = 48f; _mode.style.bottom = 30f;
            _mode.style.color = UiTheme.TextDim;
            _mode.style.fontSize = 11f;
            _mode.style.letterSpacing = 3f;
            _mode.style.unityFontStyleAndWeight = FontStyle.Bold;
            _screen.Add(_mode);
        }

        /// <summary>Eine kleine Winkel-Klammer in einer Bildschirmecke (HUD-Rahmen).</summary>
        void AddCorner(bool left, bool top)
        {
            var c = new VisualElement();
            c.style.position = Position.Absolute;
            c.style.width = 26f; c.style.height = 26f;
            if (left) c.style.left = 34f; else c.style.right = 34f;
            if (top) c.style.top = 34f; else c.style.bottom = 34f;
            float w = 2f;
            c.style.borderLeftWidth = left ? w : 0f;
            c.style.borderRightWidth = left ? 0f : w;
            c.style.borderTopWidth = top ? w : 0f;
            c.style.borderBottomWidth = top ? 0f : w;
            var col = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.6f);
            c.style.borderLeftColor = col; c.style.borderRightColor = col;
            c.style.borderTopColor = col; c.style.borderBottomColor = col;
            c.pickingMode = PickingMode.Ignore;
            _screen.Add(c);
        }
    }
}
