using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Infront
{
    /// <summary>
    /// Das gesamte Spiel-HUD in Unity UI Toolkit, in einem einzigen UIDocument.
    /// Loest die alte Sammlung einzelner IMGUI-Zeichner ab (MatchHud,
    /// Teile von AbilityHud/BombHud/KillFeedHud/HighlightBanner ...), damit
    /// alles denselben Stil wie das Menue hat und sich nichts mehr gegenseitig
    /// ueberdeckt.
    ///
    /// Aufbau: ein festes Zonen-Raster. Jede Anzeige hat ihre eigene Zone:
    ///  - oben Mitte:  Punktestand, Uhr, Rolle, Lebende, Statuszeile
    ///  - unten links: Leben / Weste / Geld
    ///  - unten rechts: Munition / Waffenslots
    ///  - unten Mitte: Faehigkeiten Q/F/G
    ///  - oben rechts: Kill-Feed
    ///  - Mitte:      Ereignis-Banner, Bomben-Hinweise, Rundenende
    ///
    /// Alles ausser den echten Knoepfen (Rundenende, Pause, Kaufmenue) hat
    /// PickingMode.Ignore - sonst wuerde das HUD die Mausklicks abfangen und
    /// man koennte nicht mehr schiessen.
    ///
    /// NICHT pruefbar: wie es aussieht. Die Tests pruefen nur, dass die
    /// Elemente existieren, die richtigen Werte tragen und auf die richtigen
    /// Ereignisse reagieren.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        public static HudController Instance { get; private set; }

        UIDocument _doc;
        bool _built;

        // --- Zonen ---
        VisualElement _root;
        VisualElement _zoneTop, _zoneBottomLeft, _zoneBottomRight, _zoneBottomCenter,
                      _zoneTopRight, _zoneCenter;

        // --- oben: Punktestand ---
        Label _badgeAlpha, _badgeBravo, _scoreAlpha, _scoreBravo, _clock, _roundInfo;
        VisualElement _aliveAlpha, _aliveBravo;
        Label _roleLine, _statusLine;
        float _statusUntil;

        // --- unten links: Leben / Weste / Geld ---
        VisualElement _statusBox;
        VisualElement _hpFill, _hpGhost, _armorFill;
        Label _hpText, _moneyText;
        VisualElement _armorRow;
        float _hpShown = 1f, _hpGhostShown = 1f;
        int _moneyShown = -1;
        float _hpFlash;
        float _hpShakeT;
        int _hpLast = -1, _ammoLast = -1;
        float _hudFadeIn;

        // --- unten rechts: Waffe ---
        VisualElement _weaponBox;
        Label _ammoText, _ammoMag, _weaponName;
        VisualElement _slot1, _slot2;
        float _ammoPulse;

        // --- unten Mitte: Faehigkeiten ---
        VisualElement _abilityBar;
        readonly AbilityCell[] _abilityCells = new AbilityCell[3];

        // --- oben rechts: Kill-Feed ---
        VisualElement _killFeed;

        // --- Mitte: Banner / Bombe / Rundenende ---
        Label _banner;
        float _bannerUntil = -99f;
        float _bannerSlide;
        VisualElement _bombPrompt;
        Label _bombPromptText;
        VisualElement _bombBarBg, _bombBarFill;
        VisualElement _roundOverPanel;
        Label _roundOverTitle, _roundOverSub;
        Button _roundOverNext, _roundOverMenu;
        bool _roundOverShown;

        // --- Pause ---
        VisualElement _pausePanel;

        // --- Punktetabelle (Tab) ---
        VisualElement _scoreboard;
        Label _scoreboardTitle;
        VisualElement _sbAlpha, _sbBravo;
        readonly List<TeamMember> _sbBuf = new();

        // --- Kaufmenue ---
        VisualElement _buyDim;
        VisualElement _buyPanel;
        Label _buyTitle;
        VisualElement _buyWeapons, _buyGear;
        Button _buyReady;
        Label _buyHint;
        int _buyRowsSig;   // grobe Signatur, um nur bei Aenderung neu zu bauen

        // echte Knoepfe: nach dem PickingMode.Ignore-Durchlauf wieder anklickbar
        readonly List<VisualElement> _interactive = new();

        struct AbilityCell
        {
            public VisualElement Root;
            public Label Key;
            public Label Name;
            public Label Value;
            public VisualElement CooldownVeil;
            public VisualElement Dots;
            public float FlashT;
            public AbilityKind LastKind;
            public int LastCharges;
        }

        // ---------------- Test-Schnittstelle ----------------
        public bool IsBuiltForTests => _built;
        public VisualElement RootForTests => _root;
        public string StatusLineForTests => _statusLine != null ? _statusLine.text : null;
        public string ClockForTests => _clock != null ? _clock.text : null;
        public int AliveDotsForTests(int team)
        {
            var row = team == Team.Alpha ? _aliveAlpha : _aliveBravo;
            if (row == null) return -1;
            int lit = 0;
            foreach (var d in row.Children())
                if (d.style.opacity.value > 0.6f) lit++;
            return lit;
        }
        public bool RoundOverShownForTests => _roundOverShown;
        public string HealthTextForTests => _hpText != null ? _hpText.text : null;
        public string AmmoTextForTests => _ammoText != null ? _ammoText.text : null;
        public string BannerForTests => _banner != null && _banner.style.display == DisplayStyle.Flex
            ? _banner.text : null;

        // ---------------------------------------------------------------

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            var panel = Resources.Load<PanelSettings>("InfrontPanel");
            if (panel == null)
            {
                Debug.LogWarning("[Infront] Kein InfrontPanel in Resources - HUD aus.");
                enabled = false;
                return;
            }

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.sortingOrder = 10f;   // ueber der Welt, unter dem Ladebildschirm (100)
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
                Debug.LogWarning("[Infront] HUD: UIDocument wurde nicht bereit.");
                enabled = false;
                yield break;
            }

            try { Build(_doc.rootVisualElement); _built = true; }
            catch (Exception e)
            {
                Debug.LogError("[Infront] HUD-Aufbau fehlgeschlagen.\n" + e);
                enabled = false;
            }
        }

        // ================================================================
        //  Aufbau
        // ================================================================

        void Build(VisualElement root)
        {
            root.Clear();
            root.style.flexGrow = 1f;
            root.pickingMode = PickingMode.Ignore;
            _root = root;

            BuildTop(root);
            BuildStatusBox(root);
            BuildWeaponBox(root);
            BuildAbilityBar(root);
            BuildKillFeed(root);
            BuildCenter(root);
            BuildScoreboard(root);
            BuildBuyMenu(root);
            BuildPause(root);

            UiTheme.IgnorePickingTree(root);
            // ... ausser den echten Knoepfen (Rundenende, Pause). PickingMode.Ignore
            // auf einem Eltern-Element haelt Kinder NICHT vom Angeklicktwerden ab -
            // die Vollbild-Zonen bleiben also "durchlaessig", nur die Knoepfe selbst
            // fangen die Maus. Sichtbar sind sie ohnehin nur bei Rundenende / Pause.
            foreach (var b in _interactive)
                b.pickingMode = PickingMode.Position;
        }

        static VisualElement AbsoluteZone()
        {
            var z = new VisualElement();
            z.style.position = Position.Absolute;
            z.pickingMode = PickingMode.Ignore;
            return z;
        }

        void BuildTop(VisualElement root)
        {
            _zoneTop = AbsoluteZone();
            _zoneTop.style.top = 0f;
            _zoneTop.style.left = 0f;
            _zoneTop.style.right = 0f;
            _zoneTop.style.alignItems = Align.Center;
            root.Add(_zoneTop);

            // --- Punktestand-Leiste ---
            var bar = UiTheme.HudBox();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.marginTop = 10f;
            bar.style.paddingLeft = 4f; bar.style.paddingRight = 4f;
            bar.style.borderTopWidth = 0f;
            bar.style.borderTopLeftRadius = 0f; bar.style.borderTopRightRadius = 0f;

            _badgeAlpha = TeamBadge("ALPHA", UiTheme.TeamMine);
            _badgeBravo = TeamBadge("BRAVO", UiTheme.TeamFoe);
            _scoreAlpha = BigNum("0");
            _scoreBravo = BigNum("0");

            _clock = new Label("0:00");
            _clock.style.color = UiTheme.Text;
            _clock.style.fontSize = UiTheme.FontL;
            _clock.style.unityFontStyleAndWeight = FontStyle.Bold;
            _clock.style.minWidth = 92f;
            _clock.style.unityTextAlign = TextAnchor.MiddleCenter;
            _clock.style.letterSpacing = 1f;

            bar.Add(_badgeAlpha);
            bar.Add(_scoreAlpha);
            bar.Add(_clock);
            bar.Add(_scoreBravo);
            bar.Add(_badgeBravo);
            _zoneTop.Add(bar);

            _roundInfo = new Label("");
            _roundInfo.style.color = UiTheme.TextDim;
            _roundInfo.style.fontSize = UiTheme.FontXS;
            _roundInfo.style.letterSpacing = 2f;
            _roundInfo.style.marginTop = 3f;
            _zoneTop.Add(_roundInfo);

            // --- Lebende (Rauten pro Team) ---
            var aliveWrap = new VisualElement();
            aliveWrap.style.flexDirection = FlexDirection.Row;
            aliveWrap.style.alignItems = Align.Center;
            aliveWrap.style.marginTop = 6f;

            _aliveAlpha = DotRow(true);
            _aliveBravo = DotRow(false);
            var vs = new Label("VS");
            vs.style.color = UiTheme.TextDim;
            vs.style.fontSize = UiTheme.FontXS;
            vs.style.marginLeft = 10f; vs.style.marginRight = 10f;
            vs.style.unityFontStyleAndWeight = FontStyle.Bold;

            aliveWrap.Add(_aliveAlpha);
            aliveWrap.Add(vs);
            aliveWrap.Add(_aliveBravo);
            _zoneTop.Add(aliveWrap);

            // --- Rolle (Angriff / Verteidigung) ---
            _roleLine = new Label("");
            _roleLine.style.fontSize = UiTheme.FontS;
            _roleLine.style.unityFontStyleAndWeight = FontStyle.Bold;
            _roleLine.style.letterSpacing = 3f;
            _roleLine.style.marginTop = 6f;
            _roleLine.style.display = DisplayStyle.None;
            _zoneTop.Add(_roleLine);

            // --- eine Statuszeile ---
            _statusLine = new Label("");
            _statusLine.style.color = UiTheme.Accent;
            _statusLine.style.fontSize = UiTheme.FontM;
            _statusLine.style.unityFontStyleAndWeight = FontStyle.Bold;
            _statusLine.style.marginTop = 8f;
            _statusLine.style.display = DisplayStyle.None;
            _zoneTop.Add(_statusLine);
        }

        Label TeamBadge(string text, Color c)
        {
            var l = new Label(text);
            l.style.color = Color.black;
            l.style.backgroundColor = c;
            l.style.fontSize = UiTheme.FontS;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.letterSpacing = 2f;
            l.style.paddingLeft = 12f; l.style.paddingRight = 12f;
            l.style.paddingTop = 6f; l.style.paddingBottom = 6f;
            l.style.marginLeft = 4f; l.style.marginRight = 4f;
            return l;
        }

        Label BigNum(string text)
        {
            var l = new Label(text);
            l.style.color = UiTheme.Text;
            l.style.fontSize = UiTheme.FontL;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.minWidth = 40f;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            return l;
        }

        VisualElement DotRow(bool alpha)
        {
            var row = new VisualElement();
            row.style.flexDirection = alpha ? FlexDirection.RowReverse : FlexDirection.Row;
            row.pickingMode = PickingMode.Ignore;
            return row;
        }

        void BuildStatusBox(VisualElement root)
        {
            _zoneBottomLeft = AbsoluteZone();
            _zoneBottomLeft.style.left = 24f;
            _zoneBottomLeft.style.bottom = 24f;
            root.Add(_zoneBottomLeft);

            _statusBox = UiTheme.HudBox();
            UiTheme.Pad(_statusBox, 12f);
            _statusBox.style.minWidth = 260f;
            _zoneBottomLeft.Add(_statusBox);

            // Geld
            _moneyText = new Label("$ 0");
            _moneyText.style.color = UiTheme.Money;
            _moneyText.style.fontSize = UiTheme.FontM;
            _moneyText.style.unityFontStyleAndWeight = FontStyle.Bold;
            _moneyText.style.marginBottom = 8f;
            _statusBox.Add(_moneyText);

            // Weste (schmaler Balken, nur wenn vorhanden)
            _armorRow = new VisualElement();
            _armorRow.style.height = 6f;
            _armorRow.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            _armorRow.style.marginBottom = 5f;
            _armorRow.style.display = DisplayStyle.None;
            _armorFill = new VisualElement();
            _armorFill.style.height = 6f;
            _armorFill.style.width = Length.Percent(100f);
            _armorFill.style.backgroundColor = UiTheme.Armor;
            _armorRow.Add(_armorFill);
            _statusBox.Add(_armorRow);

            // Leben: Hintergrund, Geisterbalken, Fuellbalken, Zahl
            var hpBg = new VisualElement();
            hpBg.style.height = 26f;
            hpBg.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            hpBg.style.justifyContent = Justify.Center;

            _hpGhost = new VisualElement();
            _hpGhost.style.position = Position.Absolute;
            _hpGhost.style.left = 0f; _hpGhost.style.top = 0f; _hpGhost.style.bottom = 0f;
            _hpGhost.style.width = Length.Percent(100f);
            _hpGhost.style.backgroundColor = new Color(1f, 1f, 1f, 0.22f);

            _hpFill = new VisualElement();
            _hpFill.style.position = Position.Absolute;
            _hpFill.style.left = 0f; _hpFill.style.top = 0f; _hpFill.style.bottom = 0f;
            _hpFill.style.width = Length.Percent(100f);
            _hpFill.style.backgroundColor = UiTheme.Gut;

            _hpText = new Label("100");
            _hpText.style.color = Color.white;
            _hpText.style.fontSize = UiTheme.FontS;
            _hpText.style.unityFontStyleAndWeight = FontStyle.Bold;
            _hpText.style.marginLeft = 10f;
            _hpText.style.unityTextAlign = TextAnchor.MiddleLeft;

            hpBg.Add(_hpGhost);
            hpBg.Add(_hpFill);
            hpBg.Add(_hpText);
            _statusBox.Add(hpBg);
        }

        void BuildWeaponBox(VisualElement root)
        {
            _zoneBottomRight = AbsoluteZone();
            _zoneBottomRight.style.right = 24f;
            _zoneBottomRight.style.bottom = 24f;
            _zoneBottomRight.style.alignItems = Align.FlexEnd;
            root.Add(_zoneBottomRight);

            _weaponBox = UiTheme.HudBox();
            UiTheme.Pad(_weaponBox, 12f);
            _weaponBox.style.alignItems = Align.FlexEnd;
            _zoneBottomRight.Add(_weaponBox);

            _weaponName = new Label("-");
            _weaponName.style.color = UiTheme.TextDim;
            _weaponName.style.fontSize = UiTheme.FontXS;
            _weaponName.style.letterSpacing = 2f;
            _weaponName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _weaponBox.Add(_weaponName);

            var ammoRow = new VisualElement();
            ammoRow.style.flexDirection = FlexDirection.Row;
            ammoRow.style.alignItems = Align.FlexEnd;

            _ammoText = new Label("0");
            _ammoText.style.color = UiTheme.Text;
            _ammoText.style.fontSize = UiTheme.FontL;
            _ammoText.style.unityFontStyleAndWeight = FontStyle.Bold;

            _ammoMag = new Label("/ 0");
            _ammoMag.style.color = UiTheme.TextDim;
            _ammoMag.style.fontSize = UiTheme.FontS;
            _ammoMag.style.marginLeft = 4f;
            _ammoMag.style.marginBottom = 4f;

            ammoRow.Add(_ammoText);
            ammoRow.Add(_ammoMag);
            _weaponBox.Add(ammoRow);

            var slots = new VisualElement();
            slots.style.flexDirection = FlexDirection.Row;
            slots.style.marginTop = 6f;
            _slot1 = SlotChip("1");
            _slot2 = SlotChip("2");
            slots.Add(_slot1);
            slots.Add(_slot2);
            _weaponBox.Add(slots);
        }

        VisualElement SlotChip(string key)
        {
            var c = new VisualElement();
            c.style.width = 22f; c.style.height = 20f;
            c.style.marginLeft = 4f;
            c.style.justifyContent = Justify.Center;
            c.style.alignItems = Align.Center;
            UiTheme.Border(c, 1f, UiTheme.HudLine);
            var l = new Label(key);
            l.style.color = UiTheme.TextDim;
            l.style.fontSize = UiTheme.FontXS;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            c.Add(l);
            return c;
        }

        void BuildAbilityBar(VisualElement root)
        {
            _zoneBottomCenter = AbsoluteZone();
            _zoneBottomCenter.style.bottom = 100f;
            _zoneBottomCenter.style.left = 0f;
            _zoneBottomCenter.style.right = 0f;
            _zoneBottomCenter.style.alignItems = Align.Center;
            root.Add(_zoneBottomCenter);

            _abilityBar = new VisualElement();
            _abilityBar.style.flexDirection = FlexDirection.Row;
            _abilityBar.pickingMode = PickingMode.Ignore;
            _abilityBar.style.display = DisplayStyle.None;
            _zoneBottomCenter.Add(_abilityBar);

            string[] keys = { "Q", "F", "G" };
            for (int i = 0; i < 3; i++)
            {
                var cell = new AbilityCell { LastKind = AbilityKind.Keine, LastCharges = -1 };
                cell.Root = UiTheme.HudBox();
                cell.Root.style.width = 100f;
                cell.Root.style.height = 54f;
                cell.Root.style.marginLeft = 6f; cell.Root.style.marginRight = 6f;
                cell.Root.style.justifyContent = Justify.Center;
                cell.Root.style.alignItems = Align.Center;
                cell.Root.style.overflow = Overflow.Hidden;

                cell.CooldownVeil = new VisualElement();
                cell.CooldownVeil.style.position = Position.Absolute;
                cell.CooldownVeil.style.left = 0f; cell.CooldownVeil.style.right = 0f;
                cell.CooldownVeil.style.bottom = 0f;
                cell.CooldownVeil.style.height = Length.Percent(0f);
                cell.CooldownVeil.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
                cell.Root.Add(cell.CooldownVeil);

                cell.Key = new Label(keys[i]);
                cell.Key.style.position = Position.Absolute;
                cell.Key.style.top = 3f; cell.Key.style.left = 6f;
                cell.Key.style.color = UiTheme.Accent;
                cell.Key.style.fontSize = UiTheme.FontXS;
                cell.Key.style.unityFontStyleAndWeight = FontStyle.Bold;
                cell.Root.Add(cell.Key);

                cell.Name = new Label("–");
                cell.Name.style.color = UiTheme.Text;
                cell.Name.style.fontSize = UiTheme.FontXS;
                cell.Name.style.unityFontStyleAndWeight = FontStyle.Bold;
                cell.Root.Add(cell.Name);

                cell.Value = new Label("");
                cell.Value.style.color = UiTheme.TextDim;
                cell.Value.style.fontSize = UiTheme.FontS;
                cell.Value.style.unityFontStyleAndWeight = FontStyle.Bold;
                cell.Root.Add(cell.Value);

                cell.Dots = new VisualElement();
                cell.Dots.style.flexDirection = FlexDirection.Row;
                cell.Dots.style.position = Position.Absolute;
                cell.Dots.style.bottom = 4f;
                cell.Root.Add(cell.Dots);

                _abilityCells[i] = cell;
                _abilityBar.Add(cell.Root);
            }
        }

        void BuildKillFeed(VisualElement root)
        {
            _zoneTopRight = AbsoluteZone();
            _zoneTopRight.style.top = 92f;
            _zoneTopRight.style.right = 16f;
            _zoneTopRight.style.alignItems = Align.FlexEnd;
            root.Add(_zoneTopRight);

            _killFeed = new VisualElement();
            _killFeed.style.alignItems = Align.FlexEnd;
            _killFeed.pickingMode = PickingMode.Ignore;
            _zoneTopRight.Add(_killFeed);
        }

        void BuildCenter(VisualElement root)
        {
            _zoneCenter = AbsoluteZone();
            _zoneCenter.style.left = 0f; _zoneCenter.style.right = 0f;
            _zoneCenter.style.top = 0f; _zoneCenter.style.bottom = 0f;
            _zoneCenter.style.alignItems = Align.Center;
            _zoneCenter.style.justifyContent = Justify.Center;
            root.Add(_zoneCenter);

            // Ereignis-Banner (Doppelkill / Ace / Clutch)
            _banner = new Label("");
            _banner.style.position = Position.Absolute;
            _banner.style.top = Length.Percent(24f);
            _banner.style.color = new Color(1f, 0.85f, 0.3f);
            _banner.style.fontSize = UiTheme.FontXL;
            _banner.style.unityFontStyleAndWeight = FontStyle.Bold;
            _banner.style.letterSpacing = 4f;
            _banner.style.unityTextAlign = TextAnchor.MiddleCenter;
            _banner.style.display = DisplayStyle.None;
            _zoneCenter.Add(_banner);

            // Bomben-Hinweis + Balken (unteres Drittel)
            _bombPrompt = new VisualElement();
            _bombPrompt.style.position = Position.Absolute;
            _bombPrompt.style.top = Length.Percent(62f);
            _bombPrompt.style.alignItems = Align.Center;
            _bombPrompt.style.display = DisplayStyle.None;
            _zoneCenter.Add(_bombPrompt);

            _bombPromptText = new Label("");
            _bombPromptText.style.color = UiTheme.Text;
            _bombPromptText.style.fontSize = UiTheme.FontM;
            _bombPromptText.style.unityFontStyleAndWeight = FontStyle.Bold;
            _bombPrompt.Add(_bombPromptText);

            _bombBarBg = new VisualElement();
            _bombBarBg.style.width = 240f;
            _bombBarBg.style.height = 12f;
            _bombBarBg.style.marginTop = 8f;
            _bombBarBg.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _bombBarFill = new VisualElement();
            _bombBarFill.style.height = 12f;
            _bombBarFill.style.width = Length.Percent(0f);
            _bombBarFill.style.backgroundColor = UiTheme.Accent;
            _bombBarBg.Add(_bombBarFill);
            _bombPrompt.Add(_bombBarBg);

            // Rundenende-Kasten (mit echten Knoepfen)
            _roundOverPanel = UiTheme.HudBox();
            _roundOverPanel.style.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.92f);
            UiTheme.Pad(_roundOverPanel, 26f);
            _roundOverPanel.style.alignItems = Align.Center;
            _roundOverPanel.style.minWidth = 320f;
            _roundOverPanel.style.display = DisplayStyle.None;
            _zoneCenter.Add(_roundOverPanel);

            var corner = new VisualElement();
            corner.style.position = Position.Absolute;
            corner.style.left = -1f; corner.style.top = -1f;
            corner.style.width = 46f; corner.style.height = 3f;
            corner.style.backgroundColor = UiTheme.Accent;
            _roundOverPanel.Add(corner);

            _roundOverTitle = new Label("");
            _roundOverTitle.style.color = UiTheme.Text;
            _roundOverTitle.style.fontSize = UiTheme.FontL;
            _roundOverTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _roundOverTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            _roundOverPanel.Add(_roundOverTitle);

            _roundOverSub = new Label("");
            _roundOverSub.style.color = UiTheme.Warn;
            _roundOverSub.style.fontSize = UiTheme.FontXS;
            _roundOverSub.style.letterSpacing = 2f;
            _roundOverSub.style.marginTop = 6f;
            _roundOverSub.style.marginBottom = 14f;
            _roundOverSub.style.display = DisplayStyle.None;
            _roundOverPanel.Add(_roundOverSub);

            _roundOverNext = MenuButton("SOFORT WEITER", () =>
            {
                var mm = MatchManager.Instance;
                if (mm != null && mm.IsServer) mm.ServerStartNextRoundNow();
            });
            _roundOverMenu = MenuButton("ZURUECK ZUM MENUE", () =>
            {
                if (GameFlow.Instance != null) GameFlow.Instance.ToMenu();
            });
            _roundOverPanel.Add(_roundOverNext);
            _roundOverPanel.Add(_roundOverMenu);
        }

        Button MenuButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.height = 44f;
            b.style.minWidth = 280f;
            b.style.marginTop = 8f;
            b.style.fontSize = UiTheme.FontS;
            b.style.letterSpacing = 3f;
            b.style.color = UiTheme.Text;
            b.style.backgroundColor = UiTheme.Panel;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            UiTheme.Square(b);
            UiTheme.Border(b, 1f, UiTheme.Line);
            b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = UiTheme.PanelHi);
            b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = UiTheme.Panel);
            _interactive.Add(b);
            return b;
        }

        void BuildScoreboard(VisualElement root)
        {
            _scoreboard = AbsoluteZone();
            _scoreboard.style.left = 0f; _scoreboard.style.right = 0f;
            _scoreboard.style.top = 0f; _scoreboard.style.bottom = 0f;
            _scoreboard.style.alignItems = Align.Center;
            _scoreboard.style.justifyContent = Justify.Center;
            _scoreboard.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            _scoreboard.style.display = DisplayStyle.None;
            root.Add(_scoreboard);

            var box = UiTheme.HudBox();
            box.style.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.95f);
            UiTheme.Pad(box, 24f);
            box.style.minWidth = 680f;
            _scoreboard.Add(box);

            _scoreboardTitle = new Label("");
            _scoreboardTitle.style.color = UiTheme.Text;
            _scoreboardTitle.style.fontSize = UiTheme.FontL;
            _scoreboardTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _scoreboardTitle.style.marginBottom = 16f;
            box.Add(_scoreboardTitle);

            var cols = new VisualElement();
            cols.style.flexDirection = FlexDirection.Row;
            _sbAlpha = SbColumn("ALPHA", UiTheme.TeamMine);
            _sbBravo = SbColumn("BRAVO", UiTheme.TeamFoe);
            _sbAlpha.style.marginRight = 24f;
            cols.Add(_sbAlpha);
            cols.Add(_sbBravo);
            box.Add(cols);
        }

        VisualElement SbColumn(string name, Color c)
        {
            var col = new VisualElement();
            col.style.flexGrow = 1f;
            col.style.flexBasis = 0f;

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.justifyContent = Justify.SpaceBetween;
            head.style.borderBottomWidth = 2f;
            head.style.borderBottomColor = c;
            head.style.paddingBottom = 4f;
            head.style.marginBottom = 6f;

            var hn = new Label(name);
            hn.style.color = c;
            hn.style.fontSize = UiTheme.FontM;
            hn.style.unityFontStyleAndWeight = FontStyle.Bold;
            hn.style.letterSpacing = 3f;
            var hk = new Label("K  /  T");
            hk.style.color = UiTheme.TextDim;
            hk.style.fontSize = UiTheme.FontXS;
            hk.style.unityFontStyleAndWeight = FontStyle.Bold;
            head.Add(hn); head.Add(hk);
            col.Add(head);
            return col;
        }

        void BuildBuyMenu(VisualElement root)
        {
            // Hinweiszeile, wenn Kaufzeit laeuft aber Menue zu ist
            _buyHint = new Label("");
            _buyHint.style.position = Position.Absolute;
            _buyHint.style.top = 64f;
            _buyHint.style.left = 0f; _buyHint.style.right = 0f;
            _buyHint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _buyHint.style.color = UiTheme.TextDim;
            _buyHint.style.fontSize = UiTheme.FontS;
            _buyHint.style.unityFontStyleAndWeight = FontStyle.Bold;
            _buyHint.style.display = DisplayStyle.None;
            _buyHint.pickingMode = PickingMode.Ignore;
            root.Add(_buyHint);

            _buyDim = AbsoluteZone();
            _buyDim.style.left = 0f; _buyDim.style.right = 0f;
            _buyDim.style.top = 0f; _buyDim.style.bottom = 0f;
            _buyDim.style.alignItems = Align.Center;
            _buyDim.style.justifyContent = Justify.Center;
            _buyDim.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            _buyDim.style.display = DisplayStyle.None;
            root.Add(_buyDim);

            _buyPanel = UiTheme.HudBox();
            _buyPanel.style.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.97f);
            UiTheme.Pad(_buyPanel, 24f);
            _buyPanel.style.minWidth = 620f;
            _buyDim.Add(_buyPanel);

            var corner = new VisualElement();
            corner.style.position = Position.Absolute;
            corner.style.left = -1f; corner.style.top = -1f;
            corner.style.width = 46f; corner.style.height = 3f;
            corner.style.backgroundColor = UiTheme.Accent;
            _buyPanel.Add(corner);

            _buyTitle = new Label("KAUFMENUE");
            _buyTitle.style.color = UiTheme.Text;
            _buyTitle.style.fontSize = UiTheme.FontL;
            _buyTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _buyTitle.style.letterSpacing = 3f;
            _buyTitle.style.marginBottom = 16f;
            _buyPanel.Add(_buyTitle);

            var cols = new VisualElement();
            cols.style.flexDirection = FlexDirection.Row;

            _buyWeapons = new VisualElement();
            _buyWeapons.style.flexGrow = 1f; _buyWeapons.style.flexBasis = 0f;
            _buyWeapons.style.marginRight = 20f;
            _buyWeapons.Add(UiTheme.Section("WAFFEN"));

            _buyGear = new VisualElement();
            _buyGear.style.flexGrow = 1f; _buyGear.style.flexBasis = 0f;
            _buyGear.Add(UiTheme.Section("AUSRUESTUNG & FAEHIGKEITEN"));

            cols.Add(_buyWeapons);
            cols.Add(_buyGear);
            _buyPanel.Add(cols);

            _buyReady = MenuButton("BEREIT  ·  KAUFZEIT BEENDEN", () => BuyMenuHud.Local?.Ready());
            _buyReady.style.marginTop = 18f;
            _buyReady.style.minWidth = 0f;
            _buyPanel.Add(_buyReady);
        }

        void UpdateBuyMenu()
        {
            var bm = BuyMenuHud.Local;
            if (bm == null || bm.Catalog == null)
            {
                _buyDim.style.display = DisplayStyle.None;
                _buyHint.style.display = DisplayStyle.None;
                return;
            }

            var mm = MatchManager.Instance;
            int secs = mm != null ? Mathf.CeilToInt((float)mm.BuySecondsLeft) : 0;

            if (bm.ShouldShowHint)
            {
                _buyHint.style.display = DisplayStyle.Flex;
                _buyHint.text = $"KAUFZEIT {secs}s   ·   [B] FUER KAUFMENUE";
            }
            else _buyHint.style.display = DisplayStyle.None;

            if (!bm.ShouldShowMenu) { _buyDim.style.display = DisplayStyle.None; return; }
            _buyDim.style.display = DisplayStyle.Flex;

            int money = bm.Money;
            _buyTitle.text = $"KAUFMENUE      $ {money}      {secs}s";

            // Signatur: was koennte die Zeilen aendern? Geld-Stufe, Besitz, Kit-Angebot
            int sig = money / 50;
            var cat = bm.Catalog;
            int wCount = Mathf.Min(cat.BuyEntries.Length, 3);
            for (int i = 0; i < wCount; i++) sig = sig * 31 + (bm.OwnsWeapon(i) ? 1 : 0);
            sig = sig * 31 + (bm.OwnsArmor ? 1 : 0);
            sig = sig * 31 + (bm.KitOffered ? (bm.OwnsKit ? 2 : 1) : 0);
            var acat = bm.AbilityCatalog;
            int aCount = acat != null ? Mathf.Min(acat.Abilities.Length, 8) : 0;
            for (int i = 0; i < aCount; i++)
            {
                var a = acat.Abilities[i];
                sig = sig * 31 + (a != null && bm.OwnsAbility(a.Kind) ? 1 : 0);
            }
            if (sig == _buyRowsSig) return;
            _buyRowsSig = sig;

            RebuildBuyRows(bm, money, wCount, acat, aCount);
        }

        void RebuildBuyRows(BuyMenuHud bm, int money, int wCount, AbilityCatalog acat, int aCount)
        {
            // Kind 0 ist jeweils die Sektion-Ueberschrift -> behalten
            while (_buyWeapons.childCount > 1) _buyWeapons.RemoveAt(1);
            while (_buyGear.childCount > 1) _buyGear.RemoveAt(1);

            var cat = bm.Catalog;
            for (int i = 0; i < wCount; i++)
            {
                var e = cat.BuyEntries[i];
                bool owned = bm.OwnsWeapon(i);
                bool afford = money >= e.Price && !owned;
                int idx = i;
                _buyWeapons.Add(BuyRow($"{i + 1}", e.DisplayName,
                    owned ? "gekauft" : $"$ {e.Price}", owned, afford,
                    () => bm.BuyWeapon(idx)));
            }

            _buyGear.Add(BuyRow("4", "Schutzweste",
                bm.OwnsArmor ? "gekauft" : $"$ {bm.Agent.ArmorPrice}",
                bm.OwnsArmor, money >= bm.Agent.ArmorPrice && !bm.OwnsArmor,
                () => bm.BuyArmor()));

            if (bm.KitOffered)
                _buyGear.Add(BuyRow("5", "Entschaerfungs-Kit",
                    bm.OwnsKit ? "gekauft" : $"$ {bm.Agent.KitPrice}",
                    bm.OwnsKit, money >= bm.Agent.KitPrice && !bm.OwnsKit,
                    () => bm.BuyKit()));

            for (int i = 0; i < aCount; i++)
            {
                var a = acat.Abilities[i];
                if (a == null) continue;
                // Schritt 6: nicht angebotene Ausruestung taucht im Kaufmenue
                // nicht auf. Sie bleibt aber im Katalog, damit sich die
                // Netz-Indizes dahinter nicht verschieben.
                if (!a.Angeboten) continue;
                bool have = bm.OwnsAbility(a.Kind);
                string slot = a.Slot == AbilitySlot.Q ? "Q" : a.Slot == AbilitySlot.F ? "F" : "G";
                string key = i < 5 ? ((i + 6) % 10).ToString() : "";
                int idx = i;
                _buyGear.Add(BuyRow(key, $"{a.DisplayName}  ({slot})",
                    have ? "gekauft" : $"$ {a.Price}", have,
                    money >= a.Price && !have, () => bm.BuyAbility(idx)));
            }
        }

        VisualElement BuyRow(string key, string name, string price, bool owned, bool afford, Action onClick)
        {
            var row = new Button(afford ? onClick : (Action)(() => { }));
            row.text = string.Empty;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 34f;
            row.style.marginTop = 4f;
            UiTheme.Square(row);
            UiTheme.Margin(row, 0f); row.style.marginTop = 4f;
            UiTheme.Border(row, 1f, UiTheme.Line);
            row.style.backgroundColor = UiTheme.Panel;
            row.style.paddingLeft = 8f; row.style.paddingRight = 8f;
            if (afford)
            {
                row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = UiTheme.PanelHi);
                row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = UiTheme.Panel);
            }

            var kk = new VisualElement();
            kk.style.width = 20f; kk.style.height = 20f;
            kk.style.marginRight = 10f;
            kk.style.justifyContent = Justify.Center;
            kk.style.alignItems = Align.Center;
            UiTheme.Border(kk, 1f, UiTheme.Line);
            if (!string.IsNullOrEmpty(key))
            {
                var kl = new Label(key);
                kl.style.fontSize = UiTheme.FontXS;
                kl.style.color = UiTheme.TextDim;
                kl.style.unityFontStyleAndWeight = FontStyle.Bold;
                kk.Add(kl);
            }
            row.Add(kk);

            var nl = new Label(name);
            nl.style.flexGrow = 1f;
            nl.style.fontSize = UiTheme.FontS;
            nl.style.color = owned ? UiTheme.Good : afford ? UiTheme.Text : UiTheme.TextDim;
            nl.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(nl);

            var pl = new Label(price);
            pl.style.fontSize = UiTheme.FontXS;
            pl.style.color = owned ? UiTheme.Good : afford ? UiTheme.Money : UiTheme.TextDim;
            pl.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(pl);

            row.style.opacity = owned || afford ? 1f : 0.55f;
            return row;
        }

        void BuildPause(VisualElement root)
        {
            _pausePanel = AbsoluteZone();
            _pausePanel.style.left = 0f; _pausePanel.style.right = 0f;
            _pausePanel.style.top = 0f; _pausePanel.style.bottom = 0f;
            _pausePanel.style.alignItems = Align.Center;
            _pausePanel.style.justifyContent = Justify.Center;
            _pausePanel.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            _pausePanel.style.display = DisplayStyle.None;
            root.Add(_pausePanel);

            var box = UiTheme.HudBox();
            box.style.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.95f);
            UiTheme.Pad(box, 26f);
            box.style.alignItems = Align.Center;
            _pausePanel.Add(box);

            var title = new Label("PAUSE");
            title.style.color = UiTheme.Text;
            title.style.fontSize = UiTheme.FontL;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 6f;
            title.style.marginBottom = 18f;
            box.Add(title);

            var resume = MenuButton("WEITER", () => PauseMenu.SetPausedExternally(false));
            var quit = MenuButton("SPIEL BEENDEN", () =>
            {
                PauseMenu.ForceResume();
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.Shutdown();
                Application.Quit();
            });
            box.Add(resume);
            box.Add(quit);
        }

        // ================================================================
        //  Aktualisierung pro Frame
        // ================================================================

        void Update()
        {
            if (!_built) return;

            float dt = Time.unscaledDeltaTime;

            // HUD blendet beim Aufbau kurz ein
            if (_hudFadeIn < 1f)
            {
                _hudFadeIn = Mathf.Min(1f, _hudFadeIn + dt * 2.5f);
                float o = _hudFadeIn;
                _zoneTop.style.opacity = o;
                _zoneBottomLeft.style.opacity = o;
                _zoneBottomRight.style.opacity = o;
                _zoneBottomCenter.style.opacity = o;
                _zoneTopRight.style.opacity = o;
                _zoneBottomLeft.style.translate = new Translate(0f, (1f - o) * 20f, 0f);
                _zoneBottomRight.style.translate = new Translate(0f, (1f - o) * 20f, 0f);
            }

            UpdateMatch(dt);
            UpdateLocalPlayer(dt);
            UpdateAbilities(dt);
            UpdateKillFeed();
            UpdateBanner(dt);
            UpdateScoreboard();
            UpdateBuyMenu();
            UpdatePause();
        }

        void UpdateScoreboard()
        {
            var kb = Keyboard.current;
            bool show = kb != null && kb.tabKey.isPressed;
            _scoreboard.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            var mm = MatchManager.Instance;
            _scoreboardTitle.text = mm != null
                ? $"ALPHA  {mm.GetScore(Team.Alpha)}   :   {mm.GetScore(Team.Bravo)}  BRAVO"
                : "PUNKTETABELLE";

            FillSbColumn(_sbAlpha, Team.Alpha);
            FillSbColumn(_sbBravo, Team.Bravo);
        }

        void FillSbColumn(VisualElement col, int team)
        {
            _sbBuf.Clear();
            foreach (var m in Combatants.Everyone)
                if (m != null && m.TeamId == team) _sbBuf.Add(m);
            _sbBuf.Sort((a, b) => b.Kills.CompareTo(a.Kills));

            // Kopfzeile (Kind 0) behalten, Rest neu aufbauen
            while (col.childCount - 1 > _sbBuf.Count) col.RemoveAt(col.childCount - 1);
            while (col.childCount - 1 < _sbBuf.Count)
            {
                var r = new VisualElement();
                r.style.flexDirection = FlexDirection.Row;
                r.style.justifyContent = Justify.SpaceBetween;
                r.style.paddingTop = 3f; r.style.paddingBottom = 3f;
                var nm = new Label(); nm.name = "n";
                nm.style.fontSize = UiTheme.FontS;
                var kd = new Label(); kd.name = "kd";
                kd.style.fontSize = UiTheme.FontS;
                kd.style.unityFontStyleAndWeight = FontStyle.Bold;
                r.Add(nm); r.Add(kd);
                col.Add(r);
            }

            for (int i = 0; i < _sbBuf.Count; i++)
            {
                var m = _sbBuf[i];
                var r = col[i + 1];
                bool alive = m.Health != null && m.Health.IsAlive;
                var nm = r.Q<Label>("n");
                var kd = r.Q<Label>("kd");
                nm.text = m.DisplayName + (alive ? "" : "  (tot)");
                nm.style.color = alive ? UiTheme.Text : UiTheme.TextDim;
                kd.text = $"{m.Kills} / {m.Deaths}";
                kd.style.color = alive ? UiTheme.Text : UiTheme.TextDim;
            }
        }

        // ---- oben: Punktestand, Uhr, Rolle, Lebende, Statuszeile ----

        void UpdateMatch(float dt)
        {
            var match = MatchManager.Instance;
            int myTeam = LocalTeam();

            if (match == null)
            {
                _zoneTop.style.display = DisplayStyle.None;
                _roundOverPanel.style.display = DisplayStyle.None;
                _roundOverShown = false;
                return;
            }
            _zoneTop.style.display = DisplayStyle.Flex;

            int a = match.GetScore(Team.Alpha);
            int b = match.GetScore(Team.Bravo);
            _scoreAlpha.text = a.ToString();
            _scoreBravo.text = b.ToString();

            int secs = Mathf.Max(0, Mathf.CeilToInt((float)match.SecondsRemaining));
            _clock.text = $"{secs / 60}:{secs % 60:00}";
            _clock.style.color = secs <= 10 && !match.IsFrozen ? UiTheme.Schlecht : UiTheme.Text;

            _roundInfo.text = $"RUNDE BIS {match.RoundsToWin}";

            // eigenes Team hervorheben
            _badgeAlpha.style.opacity = myTeam == Team.Alpha || myTeam == Team.None ? 1f : 0.45f;
            _badgeBravo.style.opacity = myTeam == Team.Bravo || myTeam == Team.None ? 1f : 0.45f;

            UpdateAliveDots();

            // Rolle
            if (match.IsBombMode && myTeam != Team.None)
            {
                bool attacker = myTeam == match.AttackingTeam;
                _roleLine.style.display = DisplayStyle.Flex;
                _roleLine.text = attacker ? "ANGRIFF" : "VERTEIDIGUNG";
                _roleLine.style.color = attacker ? UiTheme.Accent : UiTheme.Armor;
            }
            else _roleLine.style.display = DisplayStyle.None;

            // eine Statuszeile nach Prioritaet
            string status = null;
            Color statusColor = UiTheme.Accent;
            var bomb = Bomb.Instance;
            if (match.IsBombMode && bomb != null && bomb.IsPlanted)
            {
                int t = Mathf.CeilToInt(bomb.FuseSecondsLeft);
                status = $"BOMBE GELEGT   {t}";
                statusColor = UiTheme.Schlecht;
            }
            else if (match.IsFrozen || match.IsBuyTime)
            {
                int fz = Mathf.Max(0, Mathf.CeilToInt((float)match.FreezeSecondsLeft));
                status = match.IsBombMode && myTeam != Team.None
                    ? (myTeam == match.AttackingTeam ? $"KAUFZEIT {fz} — DU GREIFST AN" : $"KAUFZEIT {fz} — DU VERTEIDIGST")
                    : $"KAUFZEIT {fz}";
            }
            if (status != null)
            {
                _statusLine.style.display = DisplayStyle.Flex;
                _statusLine.text = status;
                _statusLine.style.color = statusColor;
            }
            else if (Time.unscaledTime < _statusUntil && _statusLine.text.Length > 0)
            {
                _statusLine.style.display = DisplayStyle.Flex;
            }
            else _statusLine.style.display = DisplayStyle.None;

            // Rundenende
            bool over = match.CurrentPhase == MatchManager.Phase.RoundOver;
            _roundOverShown = over;
            _roundOverPanel.style.display = over ? DisplayStyle.Flex : DisplayStyle.None;
            if (over)
            {
                string text;
                if (match.MatchWinner != Team.None)
                    text = Team.Name(match.MatchWinner) + " GEWINNT DAS MATCH";
                else if (match.RoundWinner == Team.None)
                    text = "RUNDE UNENTSCHIEDEN";
                else
                    text = Team.Name(match.RoundWinner) + " GEWINNT DIE RUNDE";
                _roundOverTitle.text = text;

                bool halftime = match.IsBombMode && match.MatchWinner == Team.None
                                && match.RoundsPlayed == match.RoundsPerHalf;
                _roundOverSub.style.display = halftime ? DisplayStyle.Flex : DisplayStyle.None;
                if (halftime) _roundOverSub.text = "HALBZEIT — SEITEN GEWECHSELT, GELD ZURUECKGESETZT";

                bool matchOver = match.MatchWinner != Team.None;
                _roundOverNext.style.display = matchOver ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        void UpdateAliveDots()
        {
            RebuildDots(_aliveAlpha, Team.Alpha, LocalTeam());
            RebuildDots(_aliveBravo, Team.Bravo, LocalTeam());
        }

        void RebuildDots(VisualElement row, int team, int myTeam)
        {
            // Mitglieder dieses Teams einsammeln (stabil nach Slot sortiert)
            _dotBuf.Clear();
            foreach (var m in Combatants.Everyone)
                if (m != null && m.TeamId == team) _dotBuf.Add(m);
            _dotBuf.Sort((x, y) => x.Slot.CompareTo(y.Slot));

            while (row.childCount < _dotBuf.Count)
            {
                var d = new VisualElement();
                d.style.width = 12f; d.style.height = 12f;
                d.style.marginLeft = 3f; d.style.marginRight = 3f;
                d.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
                row.Add(d);
            }
            while (row.childCount > _dotBuf.Count)
                row.RemoveAt(row.childCount - 1);

            Color live = team == myTeam ? UiTheme.TeamMine : UiTheme.TeamFoe;
            for (int i = 0; i < _dotBuf.Count; i++)
            {
                var d = row[i];
                bool alive = _dotBuf[i].Health != null && _dotBuf[i].Health.IsAlive;
                d.style.backgroundColor = alive ? live : new Color(0.3f, 0.3f, 0.3f, 1f);
                d.style.opacity = alive ? 1f : 0.35f;
            }
        }
        readonly List<TeamMember> _dotBuf = new();

        /// <summary>Eine kurze Meldung fuer die Statuszeile (z. B. von aussen).</summary>
        public void FlashStatus(string text, float seconds = 2f)
        {
            if (_statusLine == null) return;
            _statusLine.text = text;
            _statusUntil = Time.unscaledTime + seconds;
        }

        // ---- unten: Leben / Weste / Geld / Munition ----

        void UpdateLocalPlayer(float dt)
        {
            var local = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
                ? NetworkManager.Singleton.LocalClient.PlayerObject
                : null;

            if (local == null)
            {
                _statusBox.style.display = DisplayStyle.None;
                _weaponBox.style.display = DisplayStyle.None;
                return;
            }
            _statusBox.style.display = DisplayStyle.Flex;
            _weaponBox.style.display = DisplayStyle.Flex;

            var health = local.GetComponent<Health>();
            var weapon = local.GetComponent<NetworkWeapon>();
            var wallet = local.GetComponent<Wallet>();

            if (health != null)
            {
                if (_hpLast >= 0 && health.Current < _hpLast) { _hpFlash = 1f; _hpShakeT = 1f; }
                _hpLast = health.Current;

                // Kasten ruckelt + Rand blitzt rot bei Schaden
                if (_hpShakeT > 0f)
                {
                    _hpShakeT = Mathf.Max(0f, _hpShakeT - dt * 4f);
                    float s = _hpShakeT * 6f;
                    _statusBox.style.translate = new Translate(
                        Mathf.Sin(Time.unscaledTime * 90f) * s, Mathf.Cos(Time.unscaledTime * 70f) * s * 0.5f, 0f);
                    UiTheme.Border(_statusBox, 1f, Color.Lerp(UiTheme.HudLine, UiTheme.Schlecht, _hpShakeT));
                }
                else
                {
                    _statusBox.style.translate = new Translate(0f, 0f, 0f);
                }

                float f = health.Max > 0 ? (float)health.Current / health.Max : 0f;
                _hpShown = Mathf.MoveTowards(_hpShown, f, dt * 3.5f);
                _hpGhostShown = _hpGhostShown > f
                    ? Mathf.MoveTowards(_hpGhostShown, f, dt * 0.9f)
                    : f;

                _hpFill.style.width = Length.Percent(Mathf.Clamp01(_hpShown) * 100f);
                _hpGhost.style.width = Length.Percent(Mathf.Clamp01(_hpGhostShown) * 100f);
                _hpText.text = health.Current.ToString();

                Color hpc = f > 0.5f
                    ? Color.Lerp(UiTheme.Mittel, UiTheme.Gut, (f - 0.5f) * 2f)
                    : Color.Lerp(UiTheme.Schlecht, UiTheme.Mittel, f * 2f);
                if (_hpFlash > 0f)
                {
                    _hpFlash = Mathf.Max(0f, _hpFlash - dt * 3f);
                    hpc = Color.Lerp(hpc, Color.white, _hpFlash);
                }
                _hpFill.style.backgroundColor = hpc;

                bool hasArmor = health.MaxArmor > 0 && health.Armor > 0;
                _armorRow.style.display = hasArmor ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasArmor)
                    _armorFill.style.width = Length.Percent((float)health.Armor / health.MaxArmor * 100f);
            }

            if (wallet != null)
            {
                if (_moneyShown < 0) _moneyShown = wallet.Money;
                if (_moneyShown != wallet.Money)
                {
                    int step = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(wallet.Money - _moneyShown) * dt * 4f));
                    _moneyShown = _moneyShown < wallet.Money
                        ? Mathf.Min(wallet.Money, _moneyShown + step)
                        : Mathf.Max(wallet.Money, _moneyShown - step);
                }
                _moneyText.text = $"$ {_moneyShown}";
            }

            if (weapon != null)
            {
                if (_ammoLast >= 0 && weapon.Ammo < _ammoLast) _ammoPulse = 1f;
                _ammoLast = weapon.Ammo;

                _weaponName.text = weapon.WeaponName.ToUpperInvariant();
                _ammoText.text = weapon.Ammo.ToString();
                _ammoMag.text = "/ " + weapon.MagazineSize;
                bool low = weapon.MagazineSize > 0 && weapon.Ammo <= weapon.MagazineSize * 0.25f;
                _ammoText.style.color = weapon.IsReloading ? UiTheme.Accent
                    : low ? UiTheme.Schlecht : UiTheme.Text;

                if (_ammoPulse > 0f)
                {
                    _ammoPulse = Mathf.Max(0f, _ammoPulse - dt * 4f);
                    _ammoText.style.scale = new Scale(Vector3.one * (1f + _ammoPulse * 0.25f));
                }

                _slot1.style.borderTopColor = _slot1.style.borderBottomColor =
                    _slot1.style.borderLeftColor = _slot1.style.borderRightColor =
                    weapon.ActiveSlot == 0 ? UiTheme.Accent : UiTheme.HudLine;
                _slot2.style.borderTopColor = _slot2.style.borderBottomColor =
                    _slot2.style.borderLeftColor = _slot2.style.borderRightColor =
                    weapon.ActiveSlot == 1 ? UiTheme.Accent : UiTheme.HudLine;
                (_slot1[0] as Label).style.color = weapon.ActiveSlot == 0 ? UiTheme.Accent : UiTheme.TextDim;
                (_slot2[0] as Label).style.color = weapon.ActiveSlot == 1 ? UiTheme.Accent : UiTheme.TextDim;
            }
        }

        // ---- Faehigkeiten ----

        void UpdateAbilities(float dt)
        {
            var local = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
                ? NetworkManager.Singleton.LocalClient.PlayerObject
                : null;
            var holder = local != null ? local.GetComponent<AbilityHolder>() : null;
            var match = MatchManager.Instance;
            bool show = holder != null && match != null
                        && match.CurrentPhase == MatchManager.Phase.Playing
                        && HasAnyAbility(holder);

            _abilityBar.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            for (int i = 0; i < 3; i++)
            {
                var cell = _abilityCells[i];
                var kind = holder.KindInSlot(i);

                if (kind == AbilityKind.Keine)
                {
                    cell.Name.text = "–";
                    cell.Value.text = "";
                    cell.Dots.Clear();
                    cell.CooldownVeil.style.height = Length.Percent(0f);
                    cell.Root.style.opacity = 0.4f;
                    _abilityCells[i] = cell;
                    continue;
                }
                cell.Root.style.opacity = 1f;
                cell.Name.text = AbilityShortName(kind);

                int charges = holder.ChargesInSlot(i);
                float cd = holder.CooldownLeft(i);
                // Ladung gesunken -> Kachel blitzt (Faehigkeit eingesetzt)
                if (cell.LastCharges >= 0 && charges < cell.LastCharges) cell.FlashT = 1f;
                cell.LastCharges = charges;
                cell.Value.text = cd > 0.1f ? $"{cd:0.0}s" : "";
                cell.CooldownVeil.style.height = Length.Percent(
                    cd > 0.1f ? Mathf.Clamp01(cd / 8f) * 100f : 0f);

                // Ladungspunkte
                while (cell.Dots.childCount < charges)
                {
                    var d = new VisualElement();
                    d.style.width = 6f; d.style.height = 6f;
                    d.style.marginLeft = 2f; d.style.marginRight = 2f;
                    d.style.backgroundColor = UiTheme.Accent;
                    cell.Dots.Add(d);
                }
                while (cell.Dots.childCount > Mathf.Max(0, charges))
                    cell.Dots.RemoveAt(cell.Dots.childCount - 1);

                // Aufblitzen beim Einsatz (Ladung gesunken)
                cell.FlashT = Mathf.Max(0f, cell.FlashT - dt * 3f);
                if (cell.FlashT > 0f)
                {
                    cell.Root.style.scale = new Scale(Vector3.one * (1f - cell.FlashT * 0.12f));
                    UiTheme.Border(cell.Root, 1f, Color.Lerp(UiTheme.HudLine, UiTheme.Accent, cell.FlashT));
                }
                else
                {
                    cell.Root.style.scale = new Scale(Vector3.one);
                    UiTheme.Border(cell.Root, 1f, UiTheme.HudLine);
                }

                _abilityCells[i] = cell;
            }
        }

        static bool HasAnyAbility(AbilityHolder h)
        {
            for (int i = 0; i < 3; i++)
                if (h.KindInSlot(i) != AbilityKind.Keine) return true;
            return false;
        }

        static string AbilityShortName(AbilityKind k) => k switch
        {
            AbilityKind.Rauchwand => "RAUCH",
            AbilityKind.Blendgranate => "BLEND",
            AbilityKind.Splittergranate => "SPLITTER",
            AbilityKind.ScanPuls => "SCAN",
            AbilityKind.Brandwand => "BRAND",
            AbilityKind.Stolperdraht => "DRAHT",
            _ => "-",
        };

        // ---- Kill-Feed ----

        void UpdateKillFeed()
        {
            var feed = KillFeedHud.Instance;
            if (feed == null) { _killFeed.Clear(); return; }

            var entries = feed.EntriesForHud;
            // einfache Neuzeichnung: passt sich der Liste an
            while (_killFeed.childCount > entries.Count)
                _killFeed.RemoveAt(0);
            while (_killFeed.childCount < entries.Count)
            {
                var l = new Label();
                l.style.fontSize = UiTheme.FontS;
                l.style.unityFontStyleAndWeight = FontStyle.Bold;
                l.style.marginBottom = 3f;
                l.style.paddingLeft = 8f; l.style.paddingRight = 8f;
                l.style.paddingTop = 2f; l.style.paddingBottom = 2f;
                l.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
                l.pickingMode = PickingMode.Ignore;
                _killFeed.Add(l);
            }

            int myTeam = LocalTeam();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var l = _killFeed[i] as Label;
                float age = Time.time - e.Time;
                float alpha = age > 4f ? Mathf.Clamp01(5f - age) : 1f;

                if (e.Note != null)
                {
                    l.text = e.Note;
                    var c = e.NoteColor; c.a = alpha;
                    l.style.color = c;
                }
                else
                {
                    l.text = e.Killer != null ? $"{e.Killer}  ›  {e.Victim}" : $"✖  {e.Victim}";
                    Color kc = UiTheme.TeamColor(e.KillerTeam, myTeam);
                    Color vc = UiTheme.TeamColor(e.VictimTeam, myTeam);
                    Color col = e.Killer != null ? Color.Lerp(kc, vc, 0.5f) : vc;
                    col.a = alpha;
                    l.style.color = col;
                }

                // von rechts reinrutschen (erste ~0,18 s)
                float slide = Mathf.Clamp01(age / 0.18f);
                l.style.translate = new Translate((1f - slide) * 60f, 0f, 0f);
                l.style.opacity = alpha * Mathf.Max(0.15f, slide);
            }
        }

        // ---- Ereignis-Banner ----

        void UpdateBanner(float dt)
        {
            float since = Time.time - _bannerUntil;
            if (since > 2.6f || _banner.text.Length == 0)
            {
                _banner.style.display = DisplayStyle.None;
                return;
            }
            _banner.style.display = DisplayStyle.Flex;

            // reinrutschen (erste 0.25 s), dann halten, dann ausblenden
            _bannerSlide = Mathf.MoveTowards(_bannerSlide, 1f, dt * 5f);
            float a = since > 2.0f ? Mathf.Clamp01(2.6f - since) / 0.6f : 1f;
            _banner.style.opacity = a * _bannerSlide;
            _banner.style.translate = new Translate(0f, (1f - _bannerSlide) * -30f, 0f);
        }

        /// <summary>Von HighlightBanner: einen Moment gross einblenden.</summary>
        public void ShowBanner(string text)
        {
            _banner.text = text;
            _bannerUntil = Time.time;
            _bannerSlide = 0f;
        }

        // ---- Bomben-Hinweis (von BombHud gefuettert) ----

        public void SetBombPrompt(string text, float progress01, Color barColor)
        {
            if (_bombPrompt == null) return;
            if (string.IsNullOrEmpty(text))
            {
                _bombPrompt.style.display = DisplayStyle.None;
                return;
            }
            _bombPrompt.style.display = DisplayStyle.Flex;
            _bombPromptText.text = text;
            bool bar = progress01 > 0f;
            _bombBarBg.style.display = bar ? DisplayStyle.Flex : DisplayStyle.None;
            if (bar)
            {
                _bombBarFill.style.width = Length.Percent(Mathf.Clamp01(progress01) * 100f);
                _bombBarFill.style.backgroundColor = barColor;
            }
        }

        // ---- Pause ----

        void UpdatePause()
        {
            _pausePanel.style.display = PauseMenu.IsPaused ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ---------------------------------------------------------------

        static int LocalTeam()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return Team.None;
            var tm = nm.LocalClient.PlayerObject.GetComponent<TeamMember>();
            return tm != null ? tm.TeamId : Team.None;
        }
    }
}
