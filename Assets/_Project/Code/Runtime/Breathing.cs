using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Realismus-Etappe Schritt 3: die Atmung des eigenen Spielers.
    ///
    /// Liefert einen kleinen, ruhigen Versatz fuer Kamera und Waffe, wird bei
    /// Anstrengung und wenig Leben schwerer, laesst sich beim Zielen kurz
    /// anhalten und ist hoerbar.
    ///
    /// Bewusst rein oertlich (kein Netzwerk): Atmung betrifft nur die eigene
    /// Ansicht und das eigene Ohr. Gegner hoeren sie nicht - das waere ein
    /// eigenes Thema (Ortung durch Geraeusche) und gehoert nicht hierher.
    ///
    /// NICHT pruefbar: ob die Staerke angenehm ist oder ob es nach Atmen
    /// klingt. Pruefbar: Rhythmus, Anstrengungsaufbau, Anhaltegrenze, und
    /// dass die Waffe der Kamera verzoegert folgt.
    /// </summary>
    public sealed class Breathing : MonoBehaviour
    {
        [Header("Rhythmus (Atemzuege pro Minute)")]
        [SerializeField] float _rateRuhe = 14f;
        [SerializeField] float _rateErschoepft = 34f;

        [Header("Staerke (Grad Blickversatz)")]
        // Bewusst klein. Alles darueber wird schnell unangenehm.
        [SerializeField] float _amplitudeRuhe = 0.16f;
        [SerializeField] float _amplitudeErschoepft = 0.85f;
        // Beim Zielen wird ruhiger geatmet, aber nicht gar nicht.
        [SerializeField, Range(0f, 1f)] float _amplitudeBeimZielen = 0.55f;

        [Header("Anstrengung")]
        // Sekunden Sprint bis zur vollen Erschoepfung, und Sekunden Ruhe zurueck.
        [SerializeField] float _erschoepfungAufbau = 9f;
        [SerializeField] float _erschoepfungAbbau = 14f;
        // Wenig Leben macht den Atem schwerer, auch ohne Anstrengung.
        [SerializeField, Range(0f, 1f)] float _schwellenLeben = 0.5f;

        [Header("Luft anhalten")]
        // Begrenzt, sonst waere Anhalten reiner Vorteil statt Realismus.
        [SerializeField] float _haltenMax = 4.5f;
        [SerializeField] float _haltenErholung = 3.5f;
        // Nach dem Anhalten geht der Atem staerker als vorher.
        [SerializeField] float _rueckschlag = 0.45f;

        [Header("Waffe")]
        // Die Waffe folgt der Kamera verzoegert - dadurch wirkt sie schwer.
        [SerializeField] float _waffeNachlauf = 0.18f;

        float _phase;
        float _exertion;        // 0 = ausgeruht, 1 = am Ende
        float _holdLeft;        // Restluft zum Anhalten, in Sekunden
        bool _holding;
        float _lastPhase;
        Vector2 _offset;
        Vector2 _weaponOffset;

        // --- Eingaben von aussen (setzt NetworkPlayerController jeden Frame) --
        public bool Sprinting { get; set; }
        public bool Aiming { get; set; }
        public bool WantHold { get; set; }
        public float Health01 { get; set; } = 1f;
        public bool Suspended { get; set; }

        // --- Ergebnisse -------------------------------------------------------
        /// <summary>Blickversatz in Grad (x = seitlich, y = hoch/runter).</summary>
        public Vector2 Offset => _offset;

        /// <summary>Versatz der Waffe - folgt dem Blick verzoegert.</summary>
        public Vector2 WeaponOffset => _weaponOffset;

        public float Exertion01ForTests => _exertion;
        public float HoldLeftForTests => _holdLeft;
        public bool IsHoldingForTests => _holding;
        public float PhaseForTests => _phase;
        public float RateForTests => Mathf.Lerp(_rateRuhe, _rateErschoepft, _exertion);
        public float AmplitudeForTests => AktuelleAmplitude();
        public float HaltenMaxForTests => _haltenMax;

        void Awake() => _holdLeft = _haltenMax;

        float AktuelleAmplitude()
        {
            // Wenig Leben zaehlt wie Anstrengung: bei halbem Leben oder weniger
            // steigt die Staerke bis zum vollen Wert.
            float ausLeben = _schwellenLeben > 0f
                ? Mathf.Clamp01(1f - Health01 / _schwellenLeben)
                : 0f;
            float schwere = Mathf.Max(_exertion, ausLeben);
            float amp = Mathf.Lerp(_amplitudeRuhe, _amplitudeErschoepft, schwere);
            if (_holding) amp *= 0.12f;          // angehalten: fast ruhig
            else if (Aiming) amp *= _amplitudeBeimZielen;

            // "Weniger Bewegung": der Atem bleibt spuerbar, aber der Blick
            // wandert kaum noch. Wer von der Kamerabewegung Uebelkeit bekommt,
            // kann sonst gar nicht spielen.
            amp *= GameSettings.BewegungsFaktor;
            return amp;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (Suspended)
            {
                _offset = Vector2.zero;
                _weaponOffset = Vector2.zero;
                return;
            }

            // --- Anstrengung ---------------------------------------------------
            float ziel = Sprinting ? 1f : 0f;
            float rate = Sprinting
                ? (_erschoepfungAufbau > 0f ? dt / _erschoepfungAufbau : 1f)
                : (_erschoepfungAbbau > 0f ? dt / _erschoepfungAbbau : 1f);
            _exertion = Mathf.MoveTowards(_exertion, ziel, rate);

            // --- Luft anhalten -------------------------------------------------
            bool darfHalten = WantHold && Aiming && _holdLeft > 0.01f && !Sprinting;
            if (darfHalten)
            {
                _holding = true;
                _holdLeft = Mathf.Max(0f, _holdLeft - dt);
                if (_holdLeft <= 0f)
                {
                    // Luft ist raus: der Atem schlaegt zurueck, staerker als vorher.
                    _holding = false;
                    _exertion = Mathf.Clamp01(_exertion + _rueckschlag);
                    AudioService.Instance?.Play2D(SoundId.AtemSchnappen, 0.7f);
                }
            }
            else
            {
                if (_holding)
                {
                    // Freiwillig losgelassen - kleiner Rueckschlag, kein Schnappen.
                    _exertion = Mathf.Clamp01(_exertion + _rueckschlag * 0.4f);
                }
                _holding = false;
                _holdLeft = Mathf.Min(_haltenMax,
                    _holdLeft + dt * (_haltenErholung > 0f ? _haltenMax / _haltenErholung : 0f));
            }

            // --- Rhythmus ------------------------------------------------------
            float atemzuegeProSekunde = Mathf.Lerp(_rateRuhe, _rateErschoepft, _exertion) / 60f;
            if (_holding) atemzuegeProSekunde *= 0.15f;   // angehalten: fast stehend
            _lastPhase = _phase;
            _phase = Mathf.Repeat(_phase + atemzuegeProSekunde * dt, 1f);

            // --- Versatz -------------------------------------------------------
            float amp = AktuelleAmplitude();
            // Auf und ab folgt dem Atemzug, seitlich mit halber Frequenz und
            // weniger Ausschlag - sonst sieht es aus wie ein Pendel.
            float y = Mathf.Sin(_phase * Mathf.PI * 2f) * amp;
            float x = Mathf.Sin(_phase * Mathf.PI + 0.7f) * amp * 0.35f;
            _offset = new Vector2(x, y);

            // Die Waffe zieht nach: je groesser _waffeNachlauf, desto traeger.
            float k = _waffeNachlauf > 0f
                ? 1f - Mathf.Exp(-dt / _waffeNachlauf)
                : 1f;
            _weaponOffset = Vector2.Lerp(_weaponOffset, _offset, k);

            // --- Ton -----------------------------------------------------------
            if (!_holding) SpieleAtemTon();
        }

        void SpieleAtemTon()
        {
            var audio = AudioService.Instance;
            if (audio == null) return;

            // Einatmen am Anfang des Zyklus, Ausatmen in der Mitte.
            bool ein = _lastPhase > _phase;                       // Umbruch bei 0
            bool aus = _lastPhase < 0.5f && _phase >= 0.5f;
            if (!ein && !aus) return;

            // Bei Ruhe ist der Atem still - man hoert sich erst, wenn es anstrengend wird.
            if (_exertion < 0.18f) return;

            var id = _exertion > 0.7f
                ? SoundId.AtemKeuchen
                : (ein ? SoundId.AtemEin : SoundId.AtemAus);
            audio.Play2D(id, Mathf.Lerp(0.15f, 0.75f, _exertion));
        }

        /// <summary>Nur fuer Tests: Anstrengung direkt setzen.</summary>
        public void SetExertionForTests(float v) => _exertion = Mathf.Clamp01(v);

        /// <summary>Nur fuer Tests: Restluft direkt setzen.</summary>
        public void SetHoldLeftForTests(float v) => _holdLeft = Mathf.Clamp(v, 0f, _haltenMax);
    }
}
