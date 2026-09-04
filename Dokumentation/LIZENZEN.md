# Lizenzen — Stand 2026-09-04 (beide offenen Punkte entschieden)

Diese Datei beantwortet eine Frage: **darf INFRONT so, wie es jetzt ist,
veröffentlicht werden?** Kurz: fast. Ein Punkt ist offen, und den kann nur
der Nutzer entscheiden.

## Der Code

Der gesamte Code in `Assets/_Project/Code/` stammt aus diesem Projekt
(Driftlab). Es gibt bisher **keine Lizenzdatei im Repo**. Das heisst
rechtlich: alle Rechte vorbehalten, niemand darf den Code benutzen. Für ein
öffentliches Repo ist das ungewöhnlich, aber nicht falsch — es ist einfach der
Standard, wenn nichts dabeisteht.

**Entschieden am 2026-09-04:** bewusst **„alle Rechte vorbehalten"**. Seitdem
liegt eine `LICENSE`-Datei im Wurzelverzeichnis, die genau das sagt — lesen und
daraus lernen ja, übernehmen nein.

Der Grund für diese Richtung: eine einmal vergebene freie Lizenz lässt sich
nicht zurückziehen. Von „alle Rechte vorbehalten" kann man später jederzeit
lockern, umgekehrt nicht. Für ein Portfolio-Stück, das angesehen und nicht
kopiert werden soll, ist das der passende Stand.

## Die Assets

| Quelle | Lizenz | Namensnennung | Weitergabe der Dateien |
|---|---|---|---|
| Poly Haven | CC0 | nicht nötig | erlaubt |
| ambientCG | CC0 | nicht nötig | erlaubt |
| The Free Firearm Sound Library | CC0 | nicht nötig | erlaubt |
| **Mixamo (Adobe)** | **Mixamo-Bedingungen** | — | **NICHT erlaubt** |

Alle bis auf Mixamo sind unproblematisch. Genannt werden sie trotzdem, im
Hauptmenü unter **CREDITS**.

## Der offene Punkt: Mixamo im öffentlichen Repo

**Das Problem.** Mixamo erlaubt, Figuren und Animationen in einem Spiel zu
benutzen — auch geschäftlich, ohne Gebühr. Was nicht erlaubt ist: die Dateien
**als Dateien** weiterzugeben. Sie dürfen nur *eingebaut in ein Produkt*
verbreitet werden.

Im Repo `github.com/omar9291/INFRONT` liegen aber genau diese fünf Dateien
offen herum:

```
Assets/_Project/Art/Figures/basis.fbx
Assets/_Project/Art/Figures/idle.fbx
Assets/_Project/Art/Figures/walk.fbx
Assets/_Project/Art/Figures/run.fbx
Assets/_Project/Art/Figures/death.fbx
```

Jeder kann sie einzeln herunterladen, ohne das Spiel zu benutzen. Das ist
näher an „Weitergabe der Dateien" als an „eingebaut in ein Produkt".

**Wie schlimm ist das wirklich?** Es ist eine Grauzone, kein eindeutiger
Verstoss. Adobe geht erfahrungsgemäss nicht gegen kleine Hobbyprojekte vor, und
in unzähligen öffentlichen Repos liegen Mixamo-Dateien. Trotzdem: die fertig
gebaute App weiterzugeben ist eindeutig erlaubt, das Repo mit den Rohdateien
nicht eindeutig. Wenn man es sauber haben will, gehören sie da nicht hin.

**Drei Möglichkeiten:**

1. **So lassen.** Risiko sehr klein, Aufwand null. Ehrlich gesagt das, was die
   meisten machen.
2. **Ab jetzt nicht mehr mitschicken.** Die Dateien bleiben auf der Platte und
   das Spiel funktioniert weiter, aber Git verfolgt sie nicht mehr
   (`git rm --cached` plus `.gitignore`). Sie bleiben allerdings in der alten
   Versionsgeschichte auffindbar. Aufwand: klein. Rückgängig machbar.
3. **Ganz aus der Geschichte tilgen.** Wirklich sauber, aber es schreibt die
   öffentliche Versionsgeschichte um. Alle vorhandenen Kopien des Repos passen
   danach nicht mehr dazu. **Das sollte nur passieren, wenn der Nutzer es
   ausdrücklich will.**

Was sich **nicht** ändert: die veröffentlichte Spieldatei (`INFRONT.app`,
später der itch.io-Download) ist in jedem Fall in Ordnung. Dort sind die
Figuren eingebaut, genau so wie Mixamo es vorsieht.

## Veröffentlichen auf itch.io

Erlaubt, mit einer Einschränkung, die nichts mit Lizenzen zu tun hat: itch.io
verlangt für ein eigenes Konto **18 Jahre oder die Zustimmung der Eltern**.
Die liegt vor (das Konto „Driftlab" besteht bereits und hat Snake und
Platformer veröffentlicht).

## Regel für die Zukunft

Bei jeder neuen Quelle:

1. **Die Lizenzdatei IM Paket lesen**, nicht nur die Angabe auf der Webseite.
   Am 2026-09-04 stand auf einer opengameart-Seite „CC0", im ZIP aber
   `Copyright (c) 2009 Vincent Sevedge, CC-BY 3.0` — mit einem anderen Namen
   als dem des Hochladenden. Das Paket wurde deshalb verworfen.
2. Zeile in `Dokumentation/ASSETS.md` eintragen, mit der echten Lizenz.
3. Eintrag in `MainMenuUi.BuildQuellen` ergänzen, damit es im Spiel steht.
4. Bei irgendeinem Zweifel: hier vermerken statt hoffen.


## Die zwei offenen Punkte — beide am 2026-09-04 entschieden

### 1. Code-Lizenz → alle Rechte vorbehalten

`LICENSE` liegt im Wurzelverzeichnis. Sie betrifft **nur** den Code unter
`Assets/_Project/Code/`. Fremde Inhalte behalten ihre eigenen Lizenzen; die
Datei verweist dafür auf `ASSETS.md` und auf die Seite CREDITS im Hauptmenü.

### 2. Mixamo-Dateien → nicht mehr im Repository

Die fünf FBX unter `Assets/_Project/Art/Figures/` und die daraus erzeugte
`figur.prefab` stehen jetzt in `.gitignore` und wurden mit `git rm --cached`
aus der Nachverfolgung genommen.

- **Lokal ändert sich nichts.** Die Dateien liegen weiter auf der Platte, das
  Spiel läuft hier unverändert mit den echten Figuren.
- **Wer klont, bekommt sie nicht.** Dort greift die eingebaute Rückfallebene
  und die Figuren werden wie früher aus Grundkörpern gebaut.
- **`figur.prefab` fällt mit heraus.** Es verweist per GUID auf die FBX. Bliebe
  es drin, wäre es in einem Klon *kaputt* statt *abwesend* — und die
  Rückfallebene würde nicht greifen.

**Was damit ausdrücklich NICHT erledigt ist:** in den alten Commits liegen die
Dateien weiterhin und sind dort abrufbar. Sie wirklich zu entfernen hieße, die
öffentliche Versionsgeschichte umzuschreiben. Das wurde bewusst nicht gemacht —
jeder, der das Repository schon geklont hat, müsste sonst neu klonen. Vor einer
echten Veröffentlichung ist dieser Punkt noch einmal zu prüfen.

### Was sich durch die neue Download-Regel ändert

Der Nutzer hat am 2026-09-04 erlaubt: *„downloads are all allowed (as long as
it is with credits and doesnt cost anything)"*. Damit ist neben CC0 jetzt auch
**CC-BY** zulässig — Bedingung ist die Namensnennung in `ASSETS.md` **und** auf
der Seite CREDITS im Spiel. Weiterhin ausgeschlossen bleiben **CC-BY-SA** (die
Weitergabe-Klausel kann auf das ganze Spiel durchschlagen) und alles mit
**NC/nicht-kommerziell**. Die Regel steht ausführlich in
`.claude/agents/asset-sucher.md`.
