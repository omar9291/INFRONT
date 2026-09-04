using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Infront
{
    /// <summary>
    /// Der erste Start. Laeuft genau einmal, danach nie wieder - gemerkt im
    /// oertlichen Profil (<see cref="PlayerProfile"/>), nicht in einem Konto.
    ///
    /// Absicht: kein Textblock, den niemand liest. Drei kurze Karten, jede mit
    /// genau einer Aussage, jederzeit ueberspringbar. Wer schon weiss, was er
    /// tut, ist in vier Sekunden durch.
    ///
    /// Warum ueberhaupt: INFRONT ist seit dieser Woche deutlich schwerer
    /// geworden - langsameres Gehen, Atmung, Rueckstoss mit Streuung,
    /// Trefferzonen, Blutungen. Ohne ein Wort dazu wirkt das nicht realistisch,
    /// sondern kaputt. Genau das ist der Unterschied zwischen "anspruchsvoll"
    /// und "schlecht gemacht".
    ///
    /// NICHT pruefbar: ob es hilft. Pruefbar: laeuft nur beim ersten Mal, laesst
    /// sich ueberspringen, merkt sich das, und blockiert nichts.
    /// </summary>
    public sealed class FirstRunFlow : MonoBehaviour
    {
        public struct Karte
        {
            public string Titel;
            public string Text;
        }

        /// <summary>
        /// Bewusst nur drei. Jede sagt eine Sache, die das Spiel sonst nicht
        /// erklaert und die man sonst fuer einen Fehler halten koennte.
        /// </summary>
        public static readonly Karte[] Karten =
        {
            new Karte {
                Titel = "DU BIST SCHWER",
                Text  = "Du gehst und drehst dich langsamer als in den meisten Shootern. "
                      + "Das ist Absicht. Anlaufen, Abbremsen und Landen kosten Zeit - "
                      + "plane deine Wege, statt zu zappeln.",
            },
            new Karte {
                Titel = "DEIN ATEM ZAEHLT",
                Text  = "Nach dem Sprinten geht dein Atem schwer und das Bild wandert. "
                      + "Beim Zielen kannst du mit Umschalt die Luft anhalten - "
                      + "aber nur ein paar Sekunden, danach wird es schlimmer als vorher.",
            },
            new Karte {
                Titel = "TREFFER SIND UNTERSCHIEDLICH",
                Text  = "Kopf, Rumpf, Arme und Beine zaehlen getrennt. Beintreffer bremsen, "
                      + "Armtreffer machen dein Zielen unruhig, und Wunden bluten weiter, "
                      + "bis du ein Verbandspaket benutzt.",
            },
        };

        int _index;
        VisualElement _root;
        VisualElement _karte;
        Action _fertig;

        public int IndexForTests => _index;
        public bool IstFertigForTests { get; private set; }

        /// <summary>
        /// Zeigt den Ablauf, wenn es der erste Start ist. Sonst wird sofort
        /// <paramref name="fertig"/> aufgerufen - der Erstlauf darf niemals
        /// zwischen dem Spieler und dem Spiel stehen.
        /// </summary>
        public static bool ZeigeWennNoetig(VisualElement root, Action fertig)
        {
            if (!PlayerProfile.IsFirstRun)
            {
                fertig?.Invoke();
                return false;
            }

            var go = new GameObject("FirstRunFlow");
            var flow = go.AddComponent<FirstRunFlow>();
            flow.Starte(root, fertig);
            return true;
        }

        void Starte(VisualElement root, Action fertig)
        {
            _root = root;
            _fertig = fertig;
            _index = 0;
            Baue();
        }

        void Baue()
        {
            _karte?.RemoveFromHierarchy();

            var k = Karten[Mathf.Clamp(_index, 0, Karten.Length - 1)];

            _karte = new VisualElement();
            _karte.name = "firstrun";
            _karte.style.position = Position.Absolute;
            _karte.style.left = 0f; _karte.style.top = 0f;
            _karte.style.right = 0f; _karte.style.bottom = 0f;
            _karte.style.alignItems = Align.Center;
            _karte.style.justifyContent = Justify.Center;
            _karte.style.backgroundColor = new Color(0.02f, 0.025f, 0.035f, 0.94f);

            var box = new VisualElement();
            box.style.width = 520f;
            box.style.maxWidth = Length.Percent(88f);
            box.style.paddingTop = 26f; box.style.paddingBottom = 26f;
            box.style.paddingLeft = 28f; box.style.paddingRight = 28f;
            box.style.backgroundColor = UiTheme.Glass;
            UiTheme.Square(box);
            UiTheme.Border(box, 1f, UiTheme.Edge);

            var zaehler = new Label($"{_index + 1} / {Karten.Length}");
            zaehler.style.color = UiTheme.TextDim;
            zaehler.style.fontSize = 11f;
            zaehler.style.letterSpacing = 2f;
            zaehler.style.marginBottom = 10f;
            box.Add(zaehler);

            var titel = new Label(k.Titel);
            titel.name = "firstrun-title";
            titel.style.color = UiTheme.Text;
            titel.style.fontSize = 20f;
            titel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titel.style.letterSpacing = 3f;
            titel.style.marginBottom = 10f;
            titel.style.whiteSpace = WhiteSpace.Normal;
            box.Add(titel);

            var text = new Label(k.Text);
            text.style.color = UiTheme.TextDim;
            text.style.fontSize = 13f;
            text.style.whiteSpace = WhiteSpace.Normal;
            box.Add(text);

            var reihe = new VisualElement();
            reihe.style.flexDirection = FlexDirection.Row;
            reihe.style.justifyContent = Justify.SpaceBetween;
            reihe.style.marginTop = 22f;

            var ueber = new Button(Ueberspringen) { text = "UEBERSPRINGEN" };
            ueber.name = "firstrun-skip";
            Schmuecke(ueber, leise: true);

            var weiter = new Button(Weiter)
            {
                text = _index >= Karten.Length - 1 ? "LOS GEHT'S" : "WEITER",
            };
            weiter.name = "firstrun-next";
            Schmuecke(weiter, leise: false);

            reihe.Add(ueber);
            reihe.Add(weiter);
            box.Add(reihe);

            _karte.Add(box);
            _root.Add(_karte);
        }

        static void Schmuecke(Button b, bool leise)
        {
            b.style.height = 36f;
            b.style.paddingLeft = 20f; b.style.paddingRight = 20f;
            b.style.fontSize = 12f;
            b.style.letterSpacing = 2f;
            b.style.color = leise ? UiTheme.TextDim : UiTheme.Text;
            b.style.backgroundColor = leise ? UiTheme.GlassDeep : UiTheme.GlassHi;
            UiTheme.Square(b);
            UiTheme.Border(b, 1f, UiTheme.Edge);
        }

        void Weiter()
        {
            _index++;
            if (_index >= Karten.Length) Beende();
            else Baue();
        }

        void Ueberspringen() => Beende();

        void Beende()
        {
            IstFertigForTests = true;
            PlayerProfile.MarkOnboardingDone();
            _karte?.RemoveFromHierarchy();
            _fertig?.Invoke();
            if (gameObject != null) Destroy(gameObject);
        }

        /// <summary>Nur fuer Tests: einen Schritt weiter, ohne Klick.</summary>
        public void WeiterForTests() => Weiter();

        /// <summary>Nur fuer Tests: abbrechen, ohne Klick.</summary>
        public void UeberspringenForTests() => Ueberspringen();
    }
}
