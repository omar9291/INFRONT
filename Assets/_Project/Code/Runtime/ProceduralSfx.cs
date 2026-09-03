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

                case SoundId.AtemEin:           return Breath("sfx_atem_ein",       0.55f, 620f, 0.30f, true);
                case SoundId.AtemAus:           return Breath("sfx_atem_aus",       0.75f, 430f, 0.26f, false);
                case SoundId.AtemKeuchen:       return Breath("sfx_atem_keuchen",   0.42f, 900f, 0.42f, true);
                case SoundId.AtemSchnappen:     return Breath("sfx_atem_schnappen", 0.35f, 1150f, 0.5f, true);

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

                case SoundId.Wind:               return Wind("sfx_wind");
                case SoundId.FernesFeuergefecht: return DistantFirefight("sfx_fernes_feuergefecht");
                case SoundId.Artillerie:         return Artillery("sfx_artillerie");
                case SoundId.Hubschrauber:       return Helicopter("sfx_hubschrauber");
                case SoundId.MetallKnarzen:      return MetalCreak("sfx_metall_knarzen");
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
        /// <summary>
        /// Atemzug: gefiltertes Rauschen mit einer weichen Huellkurve.
        /// <paramref name="rising"/> = Einatmen (Huellkurve steigt langsam an
        /// und bricht ab), sonst Ausatmen (schneller Anstieg, langes Abklingen).
        /// Klingt nicht wie eine echte Aufnahme - aber Rhythmus und Lautstaerke
        /// stimmen, und darum geht es hier.
        /// </summary>
        static AudioClip Breath(string name, float seconds, float cutoff, float vol, bool rising)
        {
            int n = Len(seconds);
            var data = new float[n];
            float lp = 0f, hp = 0f, prev = 0f;
            float a = Mathf.Clamp01(cutoff / SampleRate * 6f);
            System.Random rng = new(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                // Huellkurve: Einatmen zieht an und bricht ab, Ausatmen
                // beginnt kraeftig und laeuft aus.
                float env = rising
                    ? Mathf.Pow(k, 1.6f) * Mathf.Exp(-Mathf.Pow(k, 6f) * 5f)
                    : Mathf.Exp(-k * 2.6f) * Mathf.Min(1f, k * 14f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * a;                 // Tiefpass: dumpfer
                hp = 0.94f * (hp + lp - prev);          // Hochpass: kein Dröhnen
                prev = lp;
                data[i] = hp * env * vol;
            }
            return FromData(name, data);
        }

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

        // ---- Umgebung / Krieg drumherum -----------------------------------

        /// <summary>Windbett: tiefpassgefiltertes Rauschen, dessen Lautstaerke
        /// langsam von zwei Schwingungen (Boeen) moduliert wird. 4 s, laeuft in
        /// Schleife (Anfang und Ende blenden ineinander).</summary>
        static AudioClip Wind(string name)
        {
            int n = Len(4f);
            var data = new float[n];
            float lp = 0f, lp2 = 0f;
            System.Random rng = new(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.04f;      // erste Filterstufe
                lp2 += (lp - lp2) * 0.20f;       // zweite -> weiches Rauschen
                float gust = 0.5f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.13f * t)
                                  + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.37f * t + 1.3f);
                // Nahtlose Schleife: Rand mit einer halben Kosinuswelle andicken.
                float k = i / (float)n;
                float edge = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * k);
                data[i] = Mathf.Clamp(lp2 * gust * (0.6f + 0.4f * edge) * 0.9f, -1f, 1f);
            }
            return FromData(name, data);
        }

        /// <summary>Fernes Dauerfeuer: unregelmaessige, gedaempfte Knack-Bursts
        /// (kurze tiefpassgefilterte Rausch-Pops) ueber 2,6 s.</summary>
        static AudioClip DistantFirefight(string name)
        {
            int n = Len(2.6f);
            var data = new float[n];
            System.Random rng = new(name.GetHashCode());
            int i = 0;
            while (i < n)
            {
                i += Len((float)(0.03 + rng.NextDouble() * 0.22));   // Pause bis zum naechsten Schuss
                int pop = Len((float)(0.02 + rng.NextDouble() * 0.03));
                float lp = 0f;
                float vol = (float)(0.2 + rng.NextDouble() * 0.5);
                for (int j = 0; j < pop && i + j < n; j++)
                {
                    float k = j / (float)pop;
                    float env = Mathf.Exp(-k * 16f);
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (noise - lp) * 0.06f;   // dunkel = weit weg
                    data[i + j] += Mathf.Clamp(lp * env * vol, -1f, 1f);
                }
                i += pop;
            }
            return FromData(name, data);
        }

        /// <summary>Artillerie: kurzer fallender Pfeif-Anflug, dann ein tiefer,
        /// dumpfer Einschlag mit langem Abrollen. 1,6 s.</summary>
        static AudioClip Artillery(string name)
        {
            int n = Len(1.6f);
            var data = new float[n];
            int whistleN = Len(0.55f);
            float phase = 0f;
            for (int i = 0; i < whistleN; i++)
            {
                float k = i / (float)whistleN;
                float freq = Mathf.Lerp(1600f, 380f, k * k);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float env = Mathf.Sin(Mathf.PI * k) * 0.18f;
                data[i] += Mathf.Sin(phase) * env;
            }
            float lp = 0f;
            System.Random rng = new(name.GetHashCode());
            for (int i = whistleN; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = (i - whistleN) / (float)(n - whistleN);
                float env = Mathf.Exp(-k * 3.2f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.03f;
                float rumble = Mathf.Sin(2f * Mathf.PI * 42f * t) * 0.5f
                             + Mathf.Sin(2f * Mathf.PI * 27f * t) * 0.5f;
                data[i] += Mathf.Clamp((lp * 1.4f + rumble * 0.5f) * env, -1f, 1f);
            }
            return FromData(name, data);
        }

        /// <summary>Hubschrauber: rhythmischer Rotor-Schlag (Pulsrate ~11 Hz) plus
        /// ein Turbinen-Sinus. 3,5 s, gleichmaessige Huellkurve.</summary>
        static AudioClip Helicopter(string name)
        {
            int n = Len(3.5f);
            var data = new float[n];
            float lp = 0f;
            System.Random rng = new(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)n;
                float edge = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * k);   // rein/raus
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.12f;
                // Rotorschlag: geschaerfte Pulswelle
                float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * 11f * t)), 6f);
                float turbine = Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.12f;
                data[i] = Mathf.Clamp((lp * beat * 0.9f + turbine) * edge * 0.8f, -1f, 1f);
            }
            return FromData(name, data);
        }

        /// <summary>Metall-Knarzen: tiefer Sinus, dessen Tonhoehe zittert (FM),
        /// mit ein wenig Rausch-Reibung. 0,8 s.</summary>
        static AudioClip MetalCreak(string name)
        {
            int n = Len(0.8f);
            var data = new float[n];
            float phase = 0f, lp = 0f;
            System.Random rng = new(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float k = i / (float)n;
                float env = Mathf.Sin(Mathf.PI * k) * Mathf.Exp(-k * 1.5f);
                float wobble = Mathf.Sin(2f * Mathf.PI * 7f * t) * 14f
                             + Mathf.Sin(2f * Mathf.PI * 23f * t) * 5f;
                phase += 2f * Mathf.PI * (85f + wobble) / SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * 0.02f;
                data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.7f + lp * 0.4f) * env * 0.6f, -1f, 1f);
            }
            return FromData(name, data);
        }
    }
}
