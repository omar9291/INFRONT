# MASTERPLAN — Wie INFRONT in die große Liga kommt

Erstellt: 2026-08-31
Gilt zusammen mit SCOPE.md (was ist V1) und PROGRESS.md (was ist gebaut).

Dieses Dokument beantwortet eine einzige Frage:
**Was machen Valorant, Counter-Strike, Fortnite und Fall Guys wirklich gut —
und welchen Teil davon kann ein einzelner Entwickler ohne Budget bauen?**

---

## 1. Die Entscheidungen (2026-08-31 getroffen)

| Frage | Entscheidung |
|---|---|
| Richtung | **Valorant-Weg**: Fähigkeiten/Gadgets als Herzstück |
| Zielgruppe | Portfolio-Stück + wirklich mit Freunden spielen + eigener Spaß |
| Schwachstellen (eigene Einschätzung) | Bots berechenbar, komplett stumm, alles grau, jede Runde gleich |
| Umfang | Groß, in klaren Etappen |
| Multiplayer | Später — erst muss das Spiel gut sein |
| Optik | Stil aus Code **+** kostenlose Mixamo-Figuren |
| Sound | System zuerst mit Platzhalter-Tönen, echte Dateien später eintauschen |
| Fähigkeiten | Erst Gadgets im bestehenden Kaufmenü, später feste Charaktere |

---

## 2. Die Diagnose: Was wirklich fehlt

Du hast vier Schwachstellen genannt. Das sieht aus wie vier Probleme.
Es ist aber **ein einziges**:

> Das Spiel antwortet dir nicht.

- Du schießt — es kommt kein Knall.
- Du triffst — es passiert nichts Sichtbares.
- Du läufst durch die Karte — sie sagt dir nicht, wo du bist.
- Du gewinnst eine Runde — niemand feiert.
- Der Gegner tut jedes Mal dasselbe — es gibt nichts zu erzählen.

Das Fachwort dafür ist **Feedback-Dichte**: wie oft pro Sekunde das Spiel
auf das antwortet, was du tust. In Counter-Strike passiert bei *jedem*
Schuss: Knall, Mündungsfeuer, Rückstoß, Hülse fliegt raus, Einschlagfunke,
Loch in der Wand, Fadenkreuz geht auf. Sieben Antworten auf einen Klick.
Bei INFRONT sind es aktuell zwei (Rückstoß und Fadenkreuz).

**Das ist der eigentliche Unterschied zwischen "Prototyp" und "Spiel" —
nicht die Grafikqualität.** Ein Spiel aus Würfeln mit satter Rückmeldung
fühlt sich besser an als eines mit teuren Modellen und Stille.

### Die Leitidee für alles Weitere

> **Jede Runde muss eine Geschichte ergeben, die du danach jemandem
> erzählen willst.**

"Ich war allein gegen drei, hab die Bombe gelegt, mich im Rauch versteckt
und in der letzten Sekunde noch zwei erwischt" — DAS ist es, was Valorant
und CS erzeugen. Dafür braucht es genau drei Zutaten:

1. **Wucht** — es fühlt sich körperlich an (Sound, Licht, Erschütterung)
2. **Werkzeuge** — du hast echte Entscheidungen (Fähigkeiten)
3. **Gegner mit Kopf** — sie überraschen dich

Der **Look** ist der Rahmen drumherum: Er entscheidet, ob jemand überhaupt
lange genug hinschaut, um die drei Zutaten zu merken.

---

## 3. Die Etappen

Regel für alle: **Nach jeder Etappe ist das Spiel spielbar und besser als
vorher.** Nichts Bestehendes wird gelöscht, es wird nur ergänzt. Müsste eine
Etappe abgebrochen werden, steht trotzdem ein gutes Spiel da.

---

### Etappe A — "Wucht" (Sound + Trefferrückmeldung + Waffe in der Hand)

**Stand 2026-08-31:** Paket 1 (Sound-System) und Paket 2
(Trefferrückmeldung) gebaut - 71/71 Tests grün, Mac-Build neu. Paket 3
(sichtbare Waffe in der Hand / ViewModel) offen. Details in PROGRESS.md.

**Warum zuerst:** größter spürbarer Sprung pro Arbeitsstunde im ganzen Plan.
Und Sound ist bei einem Shooter **kein Schmuck, sondern Spielmechanik** — in
CS gewinnt man Runden, weil man Schritte hört. Außerdem brauchen alle
späteren Etappen (Fähigkeiten, Explosionen, Bots die hören) dieses Fundament.

**Was gebaut wird**

*Sound-System (neu: `AudioService`, `SoundBank`, `SoundEmitter`)*
- 3D-Ortung: man hört, aus welcher Richtung und Entfernung etwas kommt
- Schuss (pro Waffe unterschiedlich), Nachladen, Waffenwechsel
- **Schritte** mit Lautstärke nach Bewegungszustand:
  schleichen = fast lautlos, gehen = hörbar, sprinten = weithin hörbar
  → macht Sprinten zu einer echten Entscheidung statt "immer an"
- Treffer-Bestätigung (der kurze "Tock", wenn man selbst trifft),
  eigener Ton für Kopfschuss, eigener für Abschuss
- Rundenstart, Rundensieg, Rundenniederlage, Ende der Kaufzeit
- Bomben-Piepen (schneller werdend), Legen, Entschärfen, Explosion
- Der Server sagt "Schuss an Position X" — die Töne entstehen bei allen
  Clients, nicht nur beim Schützen (server-autoritativ wie alles andere)

*Platzhalter-Töne*
- Ich erzeuge alle Töne per Code (`AudioClip.Create`) — du wirst nie
  blockiert, alles funktioniert sofort
- **Jeder Ton hat einen Datei-Platz**: legst du später
  `Assets/_Project/Audio/schuss_ak.wav` ab, wird die Datei automatisch statt
  des Platzhalters benutzt. Du kannst einzeln tauschen, ohne dass ich
  nochmal ran muss.

*Trefferrückmeldung (erweitert `DamageFeedback`, `TracerEffect`)*
- Mündungsfeuer (kurzes Licht + Blitz-Sprite)
- Einschlag an der Wand: Funken + Einschussloch, das bleibt
- Treffer-Effekt am Gegner (nicht blutig — ein kurzer Farbstoß)
- Kill-Bestätigung: Fadenkreuz-Haken + Ton + kurzer Zoom
- Kamera-Erschütterung: beim Schießen leicht, beim Getroffenwerden stark
- Hülsen, die aus der Waffe fliegen

*Waffe in der Hand (neu: `ViewModel`)*
- Aktuell siehst du **gar keine Waffe** — ein riesiger Teil des fehlenden
  "Spiel"-Gefühls
- Aus Code gebautes, stilisiertes Gewehr im Blickfeld
- Bewegung: Wippen beim Laufen (Bob), Nachschwingen beim Umsehen (Sway),
  Rückstoß-, Nachlade- und Wechsel-Animation

**Wie geprüft:** neue Tests (`AudioTests`, `ViewModelTests`) prüfen, dass bei
Schuss/Treffer/Tod/Bombe der richtige Ton **angefordert** wird, dass die
Schritt-Lautstärke am Bewegungszustand hängt, und dass eine vorhandene
Sounddatei den Platzhalter ersetzt. Wie es **klingt**, kann ich nicht
prüfen — das musst du hören.

**Größe:** mittel bis groß. 2–3 Arbeitspakete.

---

### Etappe B — "Der Look" (Beleuchtung, Post-Processing, echte Figuren)

**Warum an zweiter Stelle:** sehr viel Wirkung für wenig Arbeit, weil bei dir
gerade buchstäblich alles ausgeschaltet ist. Und alles, was danach kommt
(Rauch, Feuer, Blitze), sieht sofort besser aus, wenn der Rahmen stimmt.

**Was gebaut wird**

*Bild-Aufwertung (Editor-Code, wie `GraphicsTune`)*
- HDR wieder an + **Tonemapping** (ACES) — satte, filmische Farben statt
  ausgewaschener
- **Bloom** — helle Dinge strahlen. Damit leuchten Mündungsfeuer,
  Bombenlicht und die orangen Akzente wirklich
- **Vignette** (dunkle Bildränder) und **Farbgraduierung**: kühle blaugraue
  Schatten, warme orange Lichter — genau der Dark-Tactical-Look aus deinem
  neuen Menü, jetzt auch im Spiel
- **Umgebungsverdeckung** (SSAO): Ecken und Kanten bekommen Schatten,
  Objekte "stehen" auf dem Boden statt zu schweben
- **Nebel** in der Ferne: gibt Tiefe und macht die Karte lesbar
- Filmkorn und leichte Schärfe

*Karte lesbar machen (erweitert `SceneBuilder`)*
- Drei Material-Familien statt Zufallsfarben: Boden, Wand, Deckung
- **Leuchtende orange Akzentstreifen** an Kanten und Durchgängen — führen
  das Auge und sehen bewusst gestaltet aus
- Farbcodierte Bombenplätze A und B mit großen Bodenmarkierungen
- Punktlichter an Engstellen: Gegner zeichnen sich als Silhouette ab
- Team-Farben deutlich: eigenes Team blau, Gegner rot

*Echte Figuren (Mixamo — DEIN Teil)*
- Du lädst bei mixamo.com (gratis, Adobe-Konto nötig) herunter:
  1 Figur + Animationen Idle, Laufen vorwärts/rückwärts/seitwärts, Sprinten,
  Springen, Sterben, Nachladen
- Ich baue Animator, Zustandsübergänge und die Netzwerk-Anbindung
- **Wichtig:** die Kapseln bleiben als Rückfallebene erhalten. Fehlt die
  Figur, läuft das Spiel wie bisher weiter.
- Selbst EINE Figur für beide Teams (nur eingefärbt) ist ein riesiger Sprung
  gegenüber Kapseln

**Wie geprüft:** `LookTests` prüfen, dass das Post-Processing-Volume mit den
erwarteten Effekten existiert, dass HDR an ist, und dass die Figur-Anbindung
sauber auf Kapseln zurückfällt, wenn kein Modell da ist. Ob es **schön**
aussieht, kann ich nicht prüfen.

**Größe:** mittel. 2 Arbeitspakete (eins ohne dich, eins mit Mixamo).

---

### Etappe C — "Werkzeuge" (die Fähigkeiten-Maschine)

**Das Herzstück des Valorant-Wegs.** Ab hier spielt sich jede Runde anders.

**Was gebaut wird**

*Die Maschine (neu: `AbilityStats`, `AbilityCatalog`, `AbilityHolder`, `AbilityEffect`)*
- Genauso aufgebaut wie dein Waffen-System: Fähigkeiten sind Assets, kein
  Code — Balance ändern ohne Programmieren
- Server-autoritativ: der Client fragt, der Server entscheidet und verteilt
- Ladungen pro Runde, Wirkzeit, Abklingzeit
- Gekauft im **bestehenden Kaufmenü** mit dem **bestehenden Geld-System** —
  nichts wird neu erfunden
- Tasten: **Q** = Fähigkeit 1, **F** = Fähigkeit 2, **G** = Granate

*Die ersten sechs Werkzeuge*

| Werkzeug | Wirkung | Warum es Runden verändert |
|---|---|---|
| **Rauchwand** | blockiert Sicht 15 s | erzeugt sichere Wege. Blockiert auch die Sicht der Bots. |
| **Blendgranate** | weißer Bildschirm 2 s | der klassische Angriffs-Öffner. Bots werden echt geblendet. |
| **Splittergranate** | Flächenschaden | räumt Ecken, in denen jemand campt |
| **Scan-Puls** | zeigt Gegner 3 s durch Wände | Aufklärung. Verwandelt Raten in Wissen. |
| **Brandwand** | Feuer sperrt einen Weg 8 s | Verteidiger sperren einen Zugang, Angreifer drängen ab |
| **Stolperdraht** | Alarm + kurze Blendung | sichert den Rücken, während man vorn kämpft |

*Und das Entscheidende:* **Die Bots verstehen sie.**
- Rauch blockiert ihre Sichtprüfung wirklich (hängt sich in
  `BotBrain._sightBlockers`)
- Geblendete Bots schießen daneben und suchen Deckung
- Bots laufen nicht ins Feuer
- Bots **kaufen und benutzen** Fähigkeiten selbst (erweitert `BotBuyer`,
  `BotObjective`): Angreifer rauchen eine Engstelle ein und blenden vor dem
  Sturm, Verteidiger sperren mit Feuer den Zugang zum Platz

*HUD*
- Fähigkeiten-Leiste unten mit Ladungen und Abklingzeit (im Dark-Tactical-Stil)

**Wie geprüft:** `AbilityTests` (Kauf, Ladungen, Abklingzeit, Server lehnt
unerlaubte Nutzung ab), `AbilitySightTests` (Rauch blockiert Bot-Sicht
messbar), `BotAbilityTests` (Bots kaufen und zünden sinnvoll).

**Größe:** groß. 3–4 Arbeitspakete — pro Paket zwei Werkzeuge, damit du
zwischendrin spielen kannst.

---

### Etappe D — "Gegner mit Kopf" (Bot-Überarbeitung)

**Warum hier:** erst jetzt gibt es genug Systeme (Sound zum Hören,
Fähigkeiten zum Nutzen), damit schlaue Bots überhaupt schlau wirken können.
Vorher wären es nur besser zielende Bots.

**Was gebaut wird**
- **Bots hören**: Schüsse und Sprint-Schritte in Reichweite erzeugen einen
  Verdachtspunkt. Sie drehen sich um. Sie kommen nachschauen.
- **Bots nutzen Deckung**: Deckungspunkte auf der Karte, Bot geht in Deckung,
  lugt heraus (Peek), zieht sich zurück, kommt an anderer Stelle wieder
  raus — statt frontal anzurennen
- **Bots halten Winkel**: Verteidiger stellen sich auf eine Tür ein und
  warten, statt herumzulaufen
- **Rollen im Team**: Vorstoß / Unterstützung / Flankierer / Scharfschütze.
  Der Flankierer nimmt bewusst den langen Weg → **du wirst überrascht**
- **Menschliches Zielen**: Reaktionszeit, Zielfehler beim Verfolgen,
  gelegentliches Danebenschießen, Überkorrektur. Ein Bot, der nie
  danebenschießt, ist unfair; einer, der stur zielt, ist langweilig.
- **Ansagen**: Bots melden im Kill-Feed "Feind Mitte!", "Ich gehe A!",
  "Brauche Hilfe B!" — extrem billig zu bauen, macht das Team lebendig
- **Schwierigkeitsgrade neu**: Leicht/Normal/Schwer stellen jetzt Reaktion,
  Zielgüte, Aggressivität, Fähigkeits-Nutzung und Teamwork ein — nicht nur
  das Tempo

**Wie geprüft:** `BotSenseTests` (Bot reagiert auf Schuss außerhalb der
Sicht), `BotCoverTests` (Bot sucht Deckung statt Direktweg), `BotRoleTests`
(Flankierer nimmt nachweislich nicht den kürzesten Weg), und alle
bestehenden Bot-Tests müssen grün bleiben.

**Größe:** groß. 2–3 Arbeitspakete.

---

### Etappe E — "Momente" (Highlights, Statistik, Wiederkommen)

**Warum:** das ist der Schritt von "gutes Spiel" zu "ich spiel noch eine".

**Was gebaut wird**
- **Erkannte Momente** mit Banner + Ton: Doppelkill, Dreifachkill, **Ace**
  (alle Gegner allein erledigt), Kopfschuss-Kill, Rache-Kill, **Clutch**
  ("1 gegen 3 gewonnen"), Entschärfung in letzter Sekunde
- **Bester der Runde** wird am Rundenende genannt
- **Endbildschirm im Dark-Tactical-Stil** statt IMGUI-Platzhalter:
  Abschüsse/Tode, Kopfschuss-Quote, Schaden, bester Moment des Matches
- **Laufbahn-Statistik** in PlayerPrefs: Matches, Siege, Aces, längste
  Siegesserie — beim Menüstart sichtbar
- Kill-Feed und HUD komplett auf UI Toolkit umgestellt (Dark Tactical)

**Wie geprüft:** `HighlightTests` (Ace/Clutch/Doppelkill werden korrekt
erkannt und nicht fälschlich), `CareerStatsTests` (Werte überleben einen
Neustart).

**Größe:** mittel.

---

### Etappe F — "Mit Freunden" (Online-Multiplayer)

- Beitritts-Code-Lobby über **Unity Relay** (kostenlos bis zu einer Grenze,
  braucht ein Unity-Konto — das musst du anlegen)
- Alternativ vorher der schnelle Weg: Direktverbindung über IP im gleichen WLAN
- Verbindungsabbrüche, Bots füllen leere Plätze auf, Namen, Team-Wahl

**Größe:** groß. Kommt bewusst spät — dein Netzwerk-Fundament ist von Anfang
an server-autoritativ, deshalb ist das ein Aufsatz und kein Umbau.

---

### Etappe G — "Charaktere" (aus Gadgets werden Agenten)

Wenn sich die Fähigkeiten gut anfühlen, werden sie zu festen Figuren
gebündelt — mit Namen, Farbe, Silhouette und Auswahlbildschirm.
Beispiel: **Nebel** (Rauchwand + Blendgranate), **Auge** (Scan-Puls +
Stolperdraht), **Anker** (Brandwand + Splittergranate), **Sani** (Heilfeld +
Wiederbeleben).

Das ist bewusst ganz hinten: die Bündelung ist wenig Arbeit, wenn die
Maschine steht — und sie ist die falsche Reihenfolge, wenn sie nicht steht.

---

## 4. Was wir bewusst NICHT bauen

Fokus ist der Grund, warum ein Spiel gut wird. Diese Dinge kommen NICHT in
diesen Plan:

- **Battle Pass, Skins, Shop** — Kosmetik ohne Spieler ist sinnlos
- **Große offene Karte, Fahrzeuge** — ein anderes Spiel, nicht dieses
- **Fortnite-Bauen** — passt nicht zum taktischen Rundenmodus
- **Realistische gekaufte Grafik** — kostet Geld, und beim Portfolio zählt,
  was DU gebaut hast
- **Fall-Guys-Spaßmodi** — nett, aber sie verdünnen die Identität.
  Vielleicht ganz am Ende als Zugabe.
- **Ranglisten / Matchmaking** — braucht viele Spieler, die es nicht gibt

---

## 5. Regeln für den ganzen Plan

1. **Nichts löschen, nur ergänzen.** Jede neue Sache bekommt eine
   Rückfallebene: fehlt eine Sounddatei → Platzhalterton; fehlt ein Modell →
   Kapsel; stürzt ein neues System ab → das alte läuft weiter.
2. **Nach jeder Etappe ist das Spiel spielbar.** Kein Zustand, in dem eine
   Baustelle offen liegt.
3. **Alles wird per Code erzeugt** (`SceneBuilder`), nichts von Hand in der
   Unity-Oberfläche — so wie bisher.
4. **Alles wird headless getestet.** Die bestehenden 60 Tests müssen nach
   jeder Etappe grün bleiben.
5. **Optik und Klang kann ich nicht prüfen.** Das ist keine Bequemlichkeit,
   sondern eine harte Grenze dieses Rechners. Nach jeder Etappe bekommst du
   einen konkreten Spieltest-Auftrag: worauf achten, was melden.
6. **Reihenfolge einhalten.** Nicht die spaßigen Teile vorziehen.

---

## 6. Spieltest-Auftrag (Vorlage nach jeder Etappe)

Nach jeder Etappe baue ich neu, starte das Spiel und gebe dir eine Liste in
dieser Form:

- Was ist neu?
- Welche Taste / welche Situation löst es aus?
- Woran erkennst du, dass es richtig funktioniert?
- Was könnte kaputt sein und soll gemeldet werden?

Deine Rückmeldung ist der einzige Weg, wie Optik und Gefühl in dieses Projekt
kommen. Du bist das Auge und das Ohr dieses Plans.
