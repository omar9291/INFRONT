using System.Collections.Generic;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Erzeugt die sichtbaren/wirksamen Effekte einer Faehigkeit. Server-Seite:
    /// der Server ruft <see cref="ServerSpawn"/>, das Effekt-Objekt lebt in der
    /// Host-Welt (im aktuellen Host-Modus ist der Host der einzige Client). Die
    /// Replikation an weitere Clients kommt mit Etappe F (Online) dazu.
    ///
    /// Alles per Code gebaut - keine Prefabs, keine Registrierung im
    /// NetworkManager noetig.
    /// </summary>
    public static class AbilitySpawner
    {
        public static GameObject ServerSpawn(AbilityStats stats, GameObject user,
                                             Vector3 origin, Vector3 direction, int team)
        {
            if (stats == null) return null;

            Vector3 point = ResolvePoint(origin, direction, stats.ThrowRange);
            Vector3 flatDir = direction; flatDir.y = 0f;
            if (flatDir.sqrMagnitude < 0.0001f) flatDir = Vector3.forward;
            flatDir.Normalize();

            switch (stats.Kind)
            {
                case AbilityKind.Rauchwand:
                {
                    var go = new GameObject("Rauchwand");
                    go.transform.position = point;
                    go.AddComponent<SmokeVolume>().Init(stats.Radius, stats.Duration);
                    return go;
                }
                case AbilityKind.Blendgranate:
                {
                    var go = new GameObject("Blendgranate");
                    go.transform.position = point;
                    go.AddComponent<FlashBurst>().Init(stats.Radius, stats.Duration);
                    return go;
                }
                case AbilityKind.Splittergranate:
                {
                    var go = new GameObject("Splittergranate");
                    go.transform.position = point;
                    go.AddComponent<FragGrenade>().Init(stats.Radius, user, team);
                    return go;
                }
                case AbilityKind.ScanPuls:
                {
                    var go = new GameObject("ScanPuls");
                    go.transform.position = origin;
                    go.AddComponent<ScanPulse>().Init(Mathf.Max(stats.Radius, 14f), stats.Duration, team);
                    return go;
                }
                case AbilityKind.Brandwand:
                {
                    var go = new GameObject("Brandwand");
                    go.transform.position = point;
                    go.transform.rotation = Quaternion.LookRotation(flatDir, Vector3.up);
                    go.AddComponent<FireWall>().Init(stats.Radius, stats.Duration, user, team);
                    return go;
                }
                case AbilityKind.Stolperdraht:
                {
                    var go = new GameObject("Stolperdraht");
                    go.transform.position = point;
                    go.transform.rotation = Quaternion.LookRotation(flatDir, Vector3.up);
                    go.AddComponent<Tripwire>().Init(stats.Radius, stats.Duration, team);
                    return go;
                }
                default:
                    return null;
            }
        }

        /// <summary>Wurfziel: ThrowRange weit nach vorn, aber vor einer Wand stoppen.</summary>
        static Vector3 ResolvePoint(Vector3 origin, Vector3 dir, float range)
        {
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, range, 1 << 0, QueryTriggerInteraction.Ignore))
                return hit.point - dir * 0.6f;
            Vector3 p = origin + dir * range;

            // auf den Boden fallen lassen
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit floor, 12f, 1 << 0))
                return floor.point + Vector3.up * 1.2f;
            return p;
        }
    }

    // ------------------------------------------------------------------

    /// <summary>Eine Rauchwolke: waechst auf, haelt, loest sich auf. Blockiert
    /// ueber <see cref="SmokeRegistry"/> die Bot-Sicht.</summary>
    public sealed class SmokeVolume : MonoBehaviour
    {
        float _radius = 4f;
        float _duration = 15f;
        float _age;
        Transform _sphere;
        Material _mat;
        bool _registered;

        public void Init(float radius, float duration)
        {
            _radius = Mathf.Max(0.5f, radius);
            _duration = Mathf.Max(1f, duration);
        }

        void Start()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Wolke";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _sphere = go.transform;
            _sphere.SetParent(transform, false);
            _sphere.localScale = Vector3.one * 0.2f;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            _mat = new Material(shader) { name = "SmokeMat" };
            var c = new Color(0.72f, 0.73f, 0.75f, 1f);
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
            _mat.color = c;
            if (_mat.HasProperty("_Smoothness")) _mat.SetFloat("_Smoothness", 0f);
            go.GetComponent<Renderer>().sharedMaterial = _mat;
        }

        void Update()
        {
            _age += Time.deltaTime;

            float grow = Mathf.SmoothStep(0f, 1f, _age / 0.6f);
            float fade = _age > _duration - 1.2f
                ? Mathf.InverseLerp(_duration, _duration - 1.2f, _age)
                : 1f;
            float r = _radius * grow * Mathf.Lerp(0.4f, 1f, fade);
            if (_sphere != null) _sphere.localScale = Vector3.one * r * 2f;

            // Nur registrieren, solange die Wolke wirklich dicht ist.
            bool dense = grow > 0.6f && fade > 0.4f;
            if (dense && !_registered)
            {
                SmokeRegistry.Register(transform, _radius * 0.9f);
                _registered = true;
            }
            else if (!dense && _registered)
            {
                SmokeRegistry.Unregister(transform);
                _registered = false;
            }

            if (_age >= _duration) Destroy(gameObject);
        }

        void OnDestroy()
        {
            SmokeRegistry.Unregister(transform);
            if (_mat != null) Destroy(_mat);
        }
    }

    // ------------------------------------------------------------------

    /// <summary>Eine Blendgranate: ein kurzer greller Blitz. Wer in Reichweite,
    /// in Sichtlinie und ungefaehr hinschaut, wird geblendet - Spieler bekommen
    /// einen weissen Bildschirm, Bots zielen daneben und suchen Deckung.</summary>
    public sealed class FlashBurst : MonoBehaviour
    {
        float _radius = 9f;
        float _blindTime = 2f;
        Light _light;
        float _age;

        public void Init(float radius, float blindTime)
        {
            _radius = Mathf.Max(3f, radius);
            _blindTime = Mathf.Max(0.5f, blindTime);
        }

        void Start()
        {
            var lightGo = new GameObject("Blitz");
            lightGo.transform.SetParent(transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = Color.white;
            _light.range = 30f;
            _light.intensity = 18f;
            _light.shadows = LightShadows.None;

            ApplyBlind();
            Destroy(gameObject, 0.8f);
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_light != null) _light.intensity = Mathf.Lerp(18f, 0f, _age / 0.6f);
        }

        void ApplyBlind()
        {
            Vector3 pos = transform.position;

            foreach (var m in Combatants.Everyone)
            {
                if (m == null || m.Health == null || !m.Health.IsAlive) continue;

                var aim = m.GetComponent<IAimSource>();
                if (aim == null) continue;

                Vector3 eye = aim.AimOrigin;
                float dist = Vector3.Distance(eye, pos);
                if (dist > _radius) continue;

                // Wand dazwischen? -> kein Blenden
                if (Physics.Linecast(eye, pos, 1 << 0, QueryTriggerInteraction.Ignore))
                    continue;

                // Blickwinkel: voll geblendet beim Hinschauen, sonst nur kurz.
                Vector3 toFlash = (pos - eye).normalized;
                float facing = Vector3.Dot(aim.AimDirection.normalized, toFlash); // 1 = direkt drauf
                float strength = facing > 0.2f ? 1f : (facing > -0.4f ? 0.5f : 0.2f);
                float seconds = _blindTime * strength * Mathf.Lerp(1f, 0.4f, dist / _radius);
                if (seconds < 0.25f) continue;

                Blind.Apply(m, seconds);
            }
        }
    }

    // ------------------------------------------------------------------

    /// <summary>Kleiner Helfer: jemanden blenden - Bot oder Spieler.</summary>
    static class Blind
    {
        public static void Apply(TeamMember m, float seconds)
        {
            if (m == null || seconds <= 0f) return;
            var bot = m.GetComponent<BotBrain>();
            if (bot != null) { bot.ServerBlind(seconds); return; }
            m.GetComponent<AbilityHolder>()?.ServerBlindOwner(seconds);
        }
    }

    // ------------------------------------------------------------------

    /// <summary>Splittergranate: kurz nach dem Wurf scharf, dann Flaechenschaden
    /// mit Abfall nach Entfernung (nur bei freier Sicht).</summary>
    public sealed class FragGrenade : MonoBehaviour
    {
        float _radius = 5f;
        GameObject _user;
        int _team;
        float _fuse = 1.4f;
        Transform _ball;
        Material _mat;

        public void Init(float radius, GameObject user, int team)
        {
            _radius = Mathf.Max(2f, radius);
            _user = user;
            _team = team;
        }

        void Start()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Granate";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.28f;
            _ball = go.transform;

            _mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FragMat" };
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", new Color(0.2f, 0.22f, 0.24f));
            go.GetComponent<Renderer>().sharedMaterial = _mat;
        }

        void Update()
        {
            _fuse -= Time.deltaTime;
            if (_ball != null)
            {
                float blink = Mathf.PingPong(Time.time * 8f, 1f);
                if (_mat != null && _mat.HasProperty("_EmissionColor"))
                {
                    _mat.EnableKeyword("_EMISSION");
                    _mat.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.1f) * blink * 2f);
                }
            }
            if (_fuse <= 0f) Detonate();
        }

        void Detonate()
        {
            Vector3 pos = transform.position;

            foreach (var m in Combatants.Everyone)
            {
                if (m == null || m.Health == null || !m.Health.IsAlive) continue;
                Vector3 c = m.transform.position + Vector3.up * 1f;
                float dist = Vector3.Distance(pos, c);
                if (dist > _radius) continue;
                if (Physics.Linecast(pos, c, 1 << 0, QueryTriggerInteraction.Ignore)) continue;

                float k = 1f - dist / _radius;          // 1 im Zentrum, 0 am Rand
                int dmg = Mathf.RoundToInt(Mathf.Lerp(15f, 90f, k));
                m.Health.ApplyDamage(dmg, _user != null ? _user : gameObject);
            }

            var fx = new GameObject("Splitter_FX");
            fx.transform.position = pos;
            fx.AddComponent<BlastFlash>();
            if (AudioService.Instance != null)
                AudioService.Instance.PlayAt(SoundId.BombeExplosion, pos, 1f);

            Destroy(gameObject);
        }

        void OnDestroy() { if (_mat != null) Destroy(_mat); }
    }

    /// <summary>Ein kurzer Explosions-Lichtblitz (fuer Splitter / spaeter mehr).</summary>
    public sealed class BlastFlash : MonoBehaviour
    {
        Light _light;
        float _age;

        void Start()
        {
            _light = gameObject.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.6f, 0.25f);
            _light.range = 14f;
            _light.intensity = 12f;
            _light.shadows = LightShadows.None;
            Destroy(gameObject, 0.5f);
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_light != null) _light.intensity = Mathf.Lerp(12f, 0f, _age / 0.4f);
        }
    }

    // ------------------------------------------------------------------

    /// <summary>Scan-Puls: zeigt Gegner eine Weile lang an (auch durch Waende).
    /// Fuer den Spieler Umrisse im HUD, fuer Bots "sie wissen, wo du bist".</summary>
    public sealed class ScanPulse : MonoBehaviour
    {
        void Start() { Destroy(gameObject, 0.4f); }

        public void Init(float radius, float duration, int scanningTeam)
        {
            Vector3 pos = transform.position;
            foreach (var m in Combatants.Everyone)
            {
                if (m == null || m.Health == null || !m.Health.IsAlive) continue;
                if (m.TeamId == scanningTeam) continue;                 // nur Gegner
                if (Vector3.Distance(pos, m.transform.position) > radius) continue;
                ScanRegistry.Reveal(m, scanningTeam, duration);
            }
        }
    }

    /// <summary>Wer ist gerade fuer welches Team "aufgeklaert"?</summary>
    public static class ScanRegistry
    {
        static readonly Dictionary<TeamMember, (int team, float until)> _revealed = new();

        public static void Reveal(TeamMember m, int forTeam, float seconds)
        {
            if (m != null) _revealed[m] = (forTeam, Time.time + seconds);
        }

        public static bool IsRevealedTo(TeamMember m, int viewerTeam)
        {
            if (m == null) return false;
            if (_revealed.TryGetValue(m, out var e))
            {
                if (Time.time <= e.until && e.team == viewerTeam) return true;
                if (Time.time > e.until) _revealed.Remove(m);
            }
            return false;
        }

        public static void Reset() => _revealed.Clear();
    }

    // ------------------------------------------------------------------

    /// <summary>Brandwand: eine Reihe Feuer quer zur Blickrichtung. Wer drin
    /// steht, nimmt Schaden pro Sekunde.</summary>
    public sealed class FireWall : MonoBehaviour
    {
        float _halfLength = 4f;
        float _duration = 8f;
        GameObject _user;
        int _team;
        float _age;
        float _tick;

        public void Init(float radius, float duration, GameObject user, int team)
        {
            _halfLength = Mathf.Max(2f, radius);
            _duration = Mathf.Max(2f, duration);
            _user = user;
            _team = team;
        }

        void Start()
        {
            int seg = Mathf.Clamp(Mathf.RoundToInt(_halfLength), 3, 8);
            for (int i = 0; i < seg; i++)
            {
                float f = seg == 1 ? 0f : (i / (float)(seg - 1) - 0.5f) * 2f;
                var fire = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fire.name = "Flamme";
                var col = fire.GetComponent<Collider>();
                if (col != null) Destroy(col);
                fire.transform.SetParent(transform, false);
                fire.transform.localPosition = new Vector3(f * _halfLength, 0.4f, 0f);
                fire.transform.localScale = new Vector3(1.1f, 0.9f, 0.5f);
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FireMat" };
                var c = new Color(1f, 0.45f, 0.12f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", c * 3f);
                fire.GetComponent<Renderer>().sharedMaterial = mat;
            }

            var l = gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.5f, 0.2f);
            l.range = _halfLength * 3f;
            l.intensity = 4f;
            l.shadows = LightShadows.None;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _duration) { Destroy(gameObject); return; }

            _tick -= Time.deltaTime;
            if (_tick > 0f) return;
            _tick = 0.5f;

            Vector3 axis = transform.right;
            Vector3 center = transform.position;

            foreach (var m in Combatants.Everyone)
            {
                if (m == null || m.Health == null || !m.Health.IsAlive) continue;
                Vector3 p = m.transform.position;
                float along = Mathf.Clamp(Vector3.Dot(p - center, axis), -_halfLength, _halfLength);
                Vector3 onLine = center + axis * along;
                float flat = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(onLine.x, 0f, onLine.z));
                if (flat > 1.4f || Mathf.Abs(p.y - center.y) > 2.5f) continue;
                m.Health.ApplyDamage(6, _user != null ? _user : gameObject);
            }
        }
    }

    // ------------------------------------------------------------------

    /// <summary>Stolperdraht: eine unsichtbare Linie. Laeuft ein Gegner hindurch,
    /// wird er kurz geblendet (Alarm). Sichert den Ruecken.</summary>
    public sealed class Tripwire : MonoBehaviour
    {
        float _halfLength = 3f;
        float _duration = 25f;
        int _team;
        float _age;
        readonly HashSet<TeamMember> _tripped = new();

        public void Init(float length, float duration, int team)
        {
            _halfLength = Mathf.Max(1.5f, length);
            _duration = Mathf.Max(3f, duration);
            _team = team;
        }

        void Start()
        {
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "Draht";
            var col = line.GetComponent<Collider>();
            if (col != null) Destroy(col);
            line.transform.SetParent(transform, false);
            line.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            line.transform.localScale = new Vector3(_halfLength * 2f, 0.03f, 0.03f);
            line.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "WireMat" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 0.2f, 0.2f));
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(1f, 0.1f, 0.1f) * 2f);
            line.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _duration) { Destroy(gameObject); return; }

            Vector3 center = transform.position;
            Vector3 axis = transform.forward;

            foreach (var m in Combatants.Everyone)
            {
                if (m == null || m.Health == null || !m.Health.IsAlive) continue;
                if (m.TeamId == _team) continue;
                if (_tripped.Contains(m)) continue;

                Vector3 p = m.transform.position;
                float along = Vector3.Dot(p - center, axis);
                if (Mathf.Abs(along) > _halfLength) continue;
                Vector3 onLine = center + axis * along;
                if (Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(onLine.x, 0, onLine.z)) > 0.8f) continue;

                _tripped.Add(m);
                Blind.Apply(m, 0.8f);
                if (AudioService.Instance != null)
                    AudioService.Instance.PlayAt(SoundId.RundeStart, transform.position, 0.5f);
            }
        }
    }
}
