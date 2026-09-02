using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Erzeugt einfache Platzhalter-Töne per Code - ganz ohne Audiodateien.
    /// Klingt nach "Prototyp", aber alles funktioniert sofort: Ortung,
    /// Lautstärke, Timing. Sobald du echte Dateien in
    /// <c>Assets/_Project/Audio/Resources/</c> legst, wird dieser Code für den
    /// jeweiligen Ton nicht mehr benutzt (siehe <see cref="AudioService"/>).
    ///
    /// Bewusst schlicht gehalten: Sinus-Töne, gefiltertes Rauschen, kurze
    /// Hüllkurven. Nichts hier ist Spiel-Logik - reine Optik fürs Ohr.
    /// </summary>
    public static class ProceduralSfx
    {
        const int SampleRate = 44100;

        /// <summary>Baut den Platzhalter-Clip für eine Ton-Art.</summary>
        public static AudioClip Build(SoundId id)
        {
            switch (id)
            {
                case SoundId.SchussGewehr:     return Shot("sfx_schuss_gewehr",  0.16f, 220f, 0.9f, 1.0f);
                case SoundId.SchussMp:          return Shot("sfx_schuss_mp",      0.11f, 260f, 0.7f, 1.15f);
                case SoundId.SchussSniper:      return Shot("sfx_schuss_sniper",  0.34f, 140f, 1.0f, 0.8f);
                case SoundId.SchussPistole:     return Shot("sfx_schuss_pistole", 0.13f, 300f, 0.65f, 1.1f);
                case SoundId.SchussFern:        return DistantBoom("sfx_schuss_fern");
                case SoundId.Zischen:           return Whiz("sfx_zischen");

                case SoundId.Nachladen:         return Clicks("sfx_nachladen", 2, 0.09f, 1300f);
                case SoundId.WaffeWechsel:      return Clicks("sfx_wechsel", 1, 0f, 900f);

                case SoundId.TrefferMarke:      return Blip("sfx_treffer", 1650f, 0.05f, 0.5f);
                case SoundId.TrefferKopf:       return TwoTone("sfx_kopf", 1800f, 2500f, 0.09f, 0.55f);
                case SoundId.Abschuss:          return TwoTone("sfx_abschuss", 880f, 520f, 0.22f, 0.6f);
                case SoundId.EigenerTod:        return Sweep("sfx_tod", 400f, 90f, 0.7f, 0.7f);
                case SoundId.OhrenPfeifen:      return Ringing("sfx_ohren_pfeifen");

                case SoundId.EinschlagWand:     return Noise("sfx_einschlag_wand", 0.07f, 3000f, 0.5f);
                case SoundId.EinschlagKoerper:  return Noise("sfx_einschlag_koerper", 0.09f, 700f, 0.55f);

                case SoundId.SchrittLeise:      return Noise("sfx_schritt_leise", 0.05f, 500f, 0.16f);
                case SoundId.SchrittNormal:     return Noise("sfx_schritt_normal", 0.06f, 800f, 0.32f);
                case SoundId.SchrittLaut:       return Noise("sfx_schritt_laut", 0.07f, 1100f, 0.5f);

                case SoundId.RundeStart:        return Arp("sfx_runde_start", new[] { 392f, 523f }, 0.12f, 0.45f);
                case SoundId.RundeSieg:         return Arp("sfx_runde_sieg", new[] { 523f, 659f, 784f, 1047f }, 0.11f, 0.5f);
                case SoundId.RundeNiederlage:   return Arp("sfx_runde_niederlage", new[] { 440f, 349f, 262f }, 0.16f, 0.5f);
                case SoundId.KaufzeitVorbei:    return Blip("sfx_kaufzeit_vorbei", 300f, 0.16f, 0.45f);

                case SoundId.BombePiep:         return Blip("sfx_bombe_piep", 880f, 0.06f, 0.5f);
                case SoundId.BombeGelegt:       return Clicks("sfx_bombe_gelegt", 3, 0.07f, 500f);
                case SoundId.BombeEntschaerft:  return Sweep("sfx_bombe_entschaerft", 900f, 300f, 0.4f, 0.5f);
                case SoundId.BombeExplosion:    return Explosion("sfx_bombe_explosion");
            }
            return Blip("sfx_unbekannt", 500f, 0.1f, 0.4f);
        }

        // ---- Bausteine ------------------------------------------------------

        static AudioClip FromData(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static int Len(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));

        /// <summary>Schuss: Rausch-Knall + tiefer Sinus-"Bauch", schnelle Abklingzeit.</summary>
        static AudioClip Shot(string name, float seconds, float body, float punch, float bright)
        {
            int n = Len(seconds);
            var data = new float[n];
            float lp = 0f;
            System.Random rng = new(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)n;                       // 0..1
                float env = Mathf.Exp(-k * 14f);              // sehr schneller Abfall
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * Mathf.Clamp01(0.05f * bright + 0.02f);
                float thump = Mathf.Sin(2f * Mathf.PI * body * t) * Mathf.Exp(-k * 22f);
                data[i] = Mathf.Clamp((lp * 0.8f + thump * 0.6f) * env * punch, -1f, 1f);
            }
            return FromData(name, data);
        }

        /// <summary>Ein oder mehrere kurze Klick-Geräusche hintereinander.</summary>
        static AudioClip Clicks(string name, int count, float gap, float freq)
        {
            float clickLen = 0.02f;
            int total = Len(clickLen * count + gap * Mathf.Max(0, count - 1) + 0.02f);
            var data = new float[total];
            for (int c = 0; c < count; c++)
            {
                int start = Len(c * (clickLen + gap));
                int cn = Len(clickLen);
                for (int i = 0; i < cn && start + i < total; i++)
                {
                    float k = i / (float)cn;
                    float env = Mathf.Exp(-k * 30f);
                    data[start + i] += Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate)) * env * 0.5f;
                }
            }
            return FromData(name, data);
        }

        /// <summary>Kurzer Sinus-Piep mit weicher Hüllkurve.</summary>
        static AudioClip Blip(string name, float freq, float seconds, float vol)
        {
            int n = Len(seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float env = Mathf.Sin(Mathf.PI * k);          // rein und raus
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate)) * env * vol;
            }
            return FromData(name, data);
        }

        /// <summary>Zwei Sinus-Töne direkt nacheinander.</summary>
        static AudioClip TwoTone(string name, float a, float b, float each, float vol)
        {
            int half = Len(each);
            var data = new float[half * 2];
            for (int i = 0; i < half * 2; i++)
            {
                float freq = i < half ? a : b;
                float k = (i % half) / (float)half;
                float env = Mathf.Sin(Mathf.PI * k);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate)) * env * vol;
            }
            return FromData(name, data);
        }

        /// <summary>Sinus, dessen Tonhöhe von start nach ende gleitet.</summary>
        static AudioClip Sweep(string name, float startHz, float endHz, float seconds, float vol)
        {
            int n = Len(seconds);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float freq = Mathf.Lerp(startHz, endHz, k);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Mathf.Sin(Mathf.PI * k);
                data[i] = Mathf.Sin(phase) * env * vol;
            }
            return FromData(name, data);
        }

        /// <summary>Gefiltertes Rauschen mit schnellem Abfall (Einschlag, Schritt).</summary>
        static AudioClip Noise(string name, float seconds, float cutoff, float vol)
        {
            int n = Len(seconds);
            var data = new float[n];
            float lp = 0f;
            float a = Mathf.Clamp01(cutoff / SampleRate * 6f);
            System.Random rng = new(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float env = Mathf.Exp(-k * 12f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * a;
                data[i] = lp * env * vol;
            }
            return FromData(name, data);
        }

        /// <summary>Kurze Tonfolge (Akkord-Arpeggio) für Rundenmeldungen.</summary>
        static AudioClip Arp(string name, float[] notes, float each, float vol)
        {
            int step = Len(each);
            var data = new float[step * notes.Length];
            for (int s = 0; s < notes.Length; s++)
            {
                for (int i = 0; i < step; i++)
                {
                    float k = i / (float)step;
                    float env = Mathf.Sin(Mathf.PI * k) * Mathf.Exp(-k * 2f);
                    data[s * step + i] = Mathf.Sin(2f * Mathf.PI * notes[s] * (i / (float)SampleRate)) * env * vol;
                }
            }
            return FromData(name, data);
        }

        /// <summary>Ferner Schuss: kein scharfer Knall mehr, sondern ein tiefes,
        /// langsam anschwellendes und wieder abrollendes Grollen - so klingt ein
        /// Schuss, der weit weg faellt und dessen Hall von den Waenden kommt.</summary>
        static AudioClip DistantBoom(string name)
        {
            int n = Len(0.7f);
            var data = new float[n];
            float lp = 0f;
            System.Random rng = new(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)n;
                // weicher Ein- und Ausklang (kein Attack-Transient)
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)) * Mathf.Exp(-k * 1.6f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.012f;                 // sehr dunkles Rauschen
                float rumble = Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.5f
                             + Mathf.Sin(2f * Mathf.PI * 38f * t) * 0.5f;
                data[i] = Mathf.Clamp((lp * 1.6f + rumble * 0.5f) * env * 0.7f, -1f, 1f);
            }
            return FromData(name, data);
        }

        /// <summary>Kugel-Zischen: ein sehr kurzer, hoher, nach unten gleitender
        /// Rausch-"Fffft" - die Druckwelle einer dicht vorbeifliegenden Kugel.</summary>
        static AudioClip Whiz(string name)
        {
            int n = Len(0.09f);
            var data = new float[n];
            float hp = 0f, prev = 0f;
            System.Random rng = new(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float env = Mathf.Sin(Mathf.PI * k);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                // Hochpass -> zischt; Doppler-artiges Absenken ueber die Zeit
                float a = Mathf.Lerp(0.9f, 0.45f, k);
                hp = a * (hp + noise - prev);
                prev = noise;
                data[i] = Mathf.Clamp(hp * env * 0.5f, -1f, 1f);
            }
            return FromData(name, data);
        }

        /// <summary>Ohren-Klingeln nach einer nahen Explosion: ein hoher Sinus, der
        /// langsam ausklingt. Zusammen mit dem Tiefpassfilter auf dem Ohr klingt
        /// das nach "kurz taub".</summary>
        static AudioClip Ringing(string name)
        {
            int n = Len(3.2f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)n;
                float env = Mathf.Exp(-k * 2.2f) * Mathf.Min(1f, k * 40f);  // schneller Anschlag, langer Ausklang
                float tone = Mathf.Sin(2f * Mathf.PI * 4300f * t) * 0.6f
                           + Mathf.Sin(2f * Mathf.PI * 6400f * t) * 0.25f;
                data[i] = tone * env * 0.5f;
            }
            return FromData(name, data);
        }

        /// <summary>Explosion: breiter Rausch-Ausbruch mit langer Abklingzeit + tiefes Grollen.</summary>
        static AudioClip Explosion(string name)
        {
            int n = Len(1.1f);
            var data = new float[n];
            float lp = 0f;
            System.Random rng = new(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)n;
                float env = Mathf.Exp(-k * 4.5f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.08f;
                float rumble = Mathf.Sin(2f * Mathf.PI * 45f * t) * Mathf.Exp(-k * 3f);
                data[i] = Mathf.Clamp((lp * 0.9f + rumble * 0.7f) * env, -1f, 1f);
            }
            return FromData(name, data);
        }
    }
}
