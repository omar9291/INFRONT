# INFRONT veröffentlichen

Ziel: nach jeder Arbeitseinheit die neuste spielbare Version auf **itch.io**, öffentlich
und über die Suche auffindbar, damit Freunde und andere sie testen können.

Der Haupt-Kanal ist itch.io (dort suchen Leute nach Spielen). Zusätzlich liegen die
gleichen ZIPs als GitHub-Release unter `github.com/omar9291/INFRONT/releases` — für
Leute, die über den Code kommen.

Hinweis Alter: Auf itch.io veröffentlichen setzt Zustimmung der Eltern voraus (wie schon
bei Snake und Platformer — der Driftlab-Account ist mit ihrem Einverständnis angelegt).

## Zielplattform

**Windows-PC** ist die Hauptplattform. Begründung:

- INFRONT ist ein Ego-Shooter mit Maus-Zielen — das funktioniert nur mit Maus + Tastatur.
- Auf itch.io laden die meisten Leute die Windows-Version herunter.
- **Mac** fällt gratis ab, weil hier auf dem Mac entwickelt wird (der iMac-Freund kann sofort testen).
- **Tablet / Handy** geht nicht: kein Download möglich, keine Touch-Steuerung im Spiel.
  Wer nur ein Tablet hat, kann diese Runde leider nicht mittesten.

## Einmal einrichten

### 1. Windows-Modul im Unity Hub nachinstallieren

Ohne dieses Modul kann nur die Mac-Version gebaut werden.

1. Unity Hub öffnen → **Installs**
2. Bei Version **6000.5.8f1** auf das Zahnrad → **Add modules**
3. **Windows Build Support (Mono)** ankreuzen → Install (ca. 2 GB, einmalig)

### 2. itch.io-Seite anlegen

1. Auf https://itch.io einloggen (Driftlab-Account)
2. Oben rechts → **Upload new project**
3. Ausfüllen:
   - **Title:** `INFRONT`
   - **Project URL:** `infront` → wird zu `https://driftlab.itch.io/infront`
   - **Short description:** `Rundenbasierter First-Person-Team-Shooter. Ein Spieler gegen Bots.`
   - **Classification:** Game
   - **Kind of project:** Downloadable
   - **Release status:** `In development`
   - **Pricing:** `No payments` (kostenlos)
   - **Uploads:** die `INFRONT-mac-v1.0.zip` hochladen, Häkchen **macOS**
     (Windows-ZIP kommt dazu, sobald das Modul installiert ist)
   - **Genre:** `Shooter`; **Tags:** `fps`, `shooter`, `multiplayer`, `unity`, `singleplayer`
   - **Screenshots:** 2–3 Bilder vom Menü und aus dem Spiel (macht die Seite in der Suche
     klickbarer — itch.io zeigt sie in den Ergebnissen)
4. Ganz unten **Visibility & access:**
   - Zum **Selber-Prüfen zuerst:** auf `Draft` lassen, oben steht ein geheimer Link zum Anschauen.
   - Wenn alles passt: auf **`Public`** stellen → **Save**. Ab dann steht die Seite in der
     itch.io-Suche und wird mit der Zeit auch von Google indexiert.
5. **Save & view page**

**Der Link für alle:** `https://driftlab.itch.io/infront`

## Nach jeder Arbeitseinheit — neue Version hochladen

### Schritt 1: bauen (macht Claude oder du selbst)

Unity-Editor **schließen** (Cmd+Q), dann im Projektordner:

```bash
Werkzeuge/veroeffentlichen.sh
```

Das baut Mac + Windows und legt fertige ZIPs in `Builds/dist/`:

- `INFRONT-mac-v1.0.zip`
- `INFRONT-windows-v1.0.zip`

(Die Versionsnummer kommt aus den Projekt-Einstellungen. Zum Hochzählen:
Unity → *Edit → Project Settings → Player → Version*.)

### Schritt 2: hochladen (musst du machen)

1. https://driftlab.itch.io/dashboard → **INFRONT** → **Edit**
2. Runter zu **Uploads**
3. Alte ZIPs löschen (Mülleimer-Symbol), neue hochziehen
4. Bei jedem Upload die Plattform ankreuzen:
   - `INFRONT-mac-...zip` → **macOS** ✔
   - `INFRONT-windows-...zip` → **Windows** ✔
5. **Save**

Der Link `https://driftlab.itch.io/infront` bleibt bei jeder neuen Version gleich.

## Was die Freunde wissen müssen

**Windows:** ZIP herunterladen, entpacken, `INFRONT.exe` starten.
Beim ersten Start meldet Windows „Der Computer wurde geschützt" → *Weitere Informationen*
→ *Trotzdem ausführen*. (Normal bei nicht-signierten Spielen.)

**Mac:** ZIP herunterladen, entpacken, dann **Rechtsklick auf `INFRONT.app` → Öffnen**
(nicht doppelklicken!). Einmal „Öffnen" bestätigen. Danach startet sie normal.
Falls macOS ganz blockt, hilft im Terminal:

```bash
xattr -dr com.apple.quarantine ~/Downloads/INFRONT.app
```

## Später: Hochladen automatisieren (butler)

itch.io hat ein Kommandozeilen-Werkzeug `butler`, mit dem der Upload ein einziger Befehl
wird. Das braucht einen API-Key aus dem itch.io-Account. Wenn das eingerichtet ist, kann
Claude auch das Hochladen übernehmen. Bis dahin: von Hand über die Website (dauert ~1 Minute).
