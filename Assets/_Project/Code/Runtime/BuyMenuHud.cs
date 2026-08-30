using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Infront
{
    /// <summary>
    /// Kaufmenue fuer den lokalen Spieler. Oeffnet sich am Rundenanfang
    /// automatisch und laesst sich mit B ein- und ausblenden.
    ///
    ///  - Zeigt Geld, die kaufbaren Waffen und die Weste mit Preisen.
    ///  - Ziffern 1..4 oder Klick kaufen.
    ///  - "Bereit" beendet die Kaufzeit sofort.
    ///
    /// Platzhalter-IMGUI wie das restliche HUD. Laeuft nur beim Besitzer.
    /// </summary>
    public sealed class BuyMenuHud : NetworkBehaviour
    {
        PurchaseAgent _agent;
        NetworkWeapon _weapon;
        Health _health;
        Wallet _wallet;
        WeaponCatalog _catalog;

        bool _open;
        bool _wasBuyTime;
        GUIStyle _title;
        GUIStyle _row;
        GUIStyle _hint;

        /// <summary>true, solange das Kaufmenue sichtbar ist (Maus frei, Sicht steht still).</summary>
        public static bool IsOpen { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }

            _agent = GetComponent<PurchaseAgent>();
            _weapon = GetComponent<NetworkWeapon>();
            _health = GetComponent<Health>();
            _wallet = GetComponent<Wallet>();
            _catalog = _agent != null ? _agent.Catalog : null;
        }

        public override void OnNetworkDespawn() => IsOpen = false;
        void OnDisable() => IsOpen = false;

        bool BuyTimeNow()
        {
            var mm = MatchManager.Instance;
            return mm != null && !mm.SuspendedForTests && mm.IsBuyTime
                   && _health != null && _health.IsAlive;
        }

        void Update()
        {
            bool buyTime = BuyTimeNow();

            // Kaufzeit beginnt -> Menue automatisch auf
            if (buyTime && !_wasBuyTime) _open = true;
            // Kaufzeit vorbei -> zu
            if (!buyTime && _wasBuyTime) _open = false;
            _wasBuyTime = buyTime;

            IsOpen = buyTime && _open;

            if (!buyTime) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.bKey.wasPressedThisFrame) _open = !_open;
            if (!_open) return;

            if (kb.digit1Key.wasPressedThisFrame) Buy(0);
            if (kb.digit2Key.wasPressedThisFrame) Buy(1);
            if (kb.digit3Key.wasPressedThisFrame) Buy(2);
            if (kb.digit4Key.wasPressedThisFrame) BuyArmor();
        }

        void Buy(int entryIndex)
        {
            if (_agent != null) _agent.RequestBuyWeaponRpc(entryIndex);
        }

        void BuyArmor()
        {
            if (_agent != null) _agent.RequestBuyArmorRpc();
        }

        void Ready()
        {
            if (MatchManager.Instance != null)
                MatchManager.Instance.RequestEndBuyTimeRpc();
            _open = false;
        }

        void OnGUI()
        {
            var mm = MatchManager.Instance;
            bool buyTime = BuyTimeNow();
            if (!buyTime || _catalog == null) return;

            EnsureStyles();

            int money = _wallet != null ? _wallet.Money : 0;
            int secs = Mathf.CeilToInt((float)mm.BuySecondsLeft);

            if (!_open)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.75f);
                GUI.Label(new Rect(0, 60f, Screen.width, 24f),
                    $"Kaufzeit {secs}s   -   B fuer Kaufmenue", Center(_hint));
                GUI.color = Color.white;
                return;
            }

            float w = 420f, h = 300f;
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(box.x + 16, box.y + 10, w - 32, 30), $"Kaufmenue      $ {money}      {secs}s", _title);

            float y = box.y + 52;
            for (int i = 0; i < _catalog.BuyEntries.Length && i < 3; i++)
            {
                var e = _catalog.BuyEntries[i];
                bool owned = _weapon != null && _weapon.HasPrimary && _weapon.PrimaryIndex == e.PlayerWeaponIndex;
                bool afford = money >= e.Price && !owned;
                string label = $"[{i + 1}]  {e.DisplayName}"
                    + (owned ? "   (hast du)" : $"   $ {e.Price}");
                DrawRow(new Rect(box.x + 16, y, w - 32, 34), label, afford, () => Buy(i));
                y += 40;
            }

            // Weste = Eintrag 4
            bool hasArmor = _health != null && _health.Armor >= _health.MaxArmor;
            bool affordArmor = _agent != null && money >= _agent.ArmorPrice && !hasArmor;
            string armorLabel = "[4]  Schutzweste"
                + (hasArmor ? "   (hast du)" : $"   $ {(_agent != null ? _agent.ArmorPrice : 0)}");
            DrawRow(new Rect(box.x + 16, y, w - 32, 34), armorLabel, affordArmor, BuyArmor);
            y += 48;

            if (GUI.Button(new Rect(box.x + 16, box.y + h - 44, w - 32, 32), "Bereit  (Kaufzeit beenden)"))
                Ready();
        }

        void DrawRow(Rect r, string label, bool enabled, System.Action onClick)
        {
            GUI.enabled = enabled;
            if (GUI.Button(r, label, _row) && enabled)
                onClick();
            GUI.enabled = true;
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _row = new GUIStyle(GUI.skin.button) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperCenter };
        }

        static GUIStyle Center(GUIStyle s)
        {
            s.alignment = TextAnchor.UpperCenter;
            return s;
        }
    }
}
