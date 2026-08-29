# ARCHITECTURE.md — Technische Grundentscheidungen

Letzte Aktualisierung: 2026-08-29

## Engine

Unity 6000.5.8f1. Festgelegt am 2026-08-29 im Diagnose-Schritt.
Grund: einzige installierte Version, identisch mit dem Metroidvania-Projekt,
laeuft auf der vorhandenen Hardware (MacBook Pro M1, 8 GB RAM).

Der urspruengliche Auftrag nannte Unreal Engine. Das wurde am 2026-08-29
verworfen: nur 5 GB freier Speicher (Unreal braucht ~150-200 GB), 8 GB
RAM (Unreal empfiehlt 32), keine Xcode-Vollinstallation. Unity wurde als
tragfaehige Alternative gewaehlt, das Spielkonzept bleibt gleich.

## Sprache: C#

Unity hat keine visuelle Skriptsprache wie Unreals Blueprints im Kern.
Alles laeuft in C#. Die Blueprints-Faustregel aus dem Auftrag ("Kernsysteme
in C++, Feinschliff visuell") wird so uebersetzt:

- Kernsysteme (Bewegung, Waffen, Schaden, Bots, Netzwerk, Spielregeln)
  als saubere, testbare C#-Klassen.
- Feinschliff und schnelle Iteration ueber im Inspector einstellbare Werte
  (ScriptableObjects fuer Waffen-Kennwerte, serialisierte Felder fuer
  Tuning-Parameter). So kann ohne Code-Aenderung balanciert werden.

## Render-Pipeline

OFFEN — Entscheidung siehe "Offene Entscheidungen" unten.

## Projektstruktur

    Assets/
      _Project/          Alles Selbstgebaute. Unterstrich sortiert es nach oben.
        Art/             Modelle, Texturen, Materialien (spaeter)
        Audio/           Sound, Musik (spaeter)
        Code/
          Runtime/       Spiel-Logik, die im Build landet
          Editor/        Editor-Werkzeuge, Szenen-/Prefab-Generierung
          Tests/         PlayMode-Tests (headless ausfuehrbar)
        Prefabs/         Zusammengesetzte Spielobjekte
        Scenes/          Szenen
        Settings/        Render-/Input-/Projekt-Einstellungsassets
      ThirdParty/        Importierte Asset-Store-Pakete. Nie mit _Project mischen.
    Dokumentation/       Alle .md-Dateien dieses Projekts
    CLAUDE.md            Projekt-spezifische Arbeitsanweisungen

Regel: eigener Code und importierte Assets bleiben strikt getrennt. So
laesst sich ein Store-Asset spaeter aktualisieren oder entfernen, ohne
eigene Arbeit zu treffen.

## Erzeugung per Editor-Code

Wie im Metroidvania: Szenen, Prefabs und Platzhalter-Geometrie werden per
Editor-Skript erzeugt (Assets/_Project/Code/Editor/), nicht von Hand in
der Unity-Oberflaeche zusammengeklickt. Grund: auf diesem Mac kann nicht
in Unity geklickt und nichts optisch geprueft werden. Alles muss
reproduzierbar aus Code entstehen und headless testbar sein.

## Assembly-Definitions

Jeder Code-Ordner unter _Project/Code bekommt eine eigene asmdef:
- Infront.Runtime
- Infront.Editor      (referenziert Runtime, nur im Editor)
- Infront.Tests.PlayMode  (referenziert Runtime)

Grund: schnellere Kompilierung, klare Abhaengigkeiten, Tests koennen
gezielt nur Runtime-Code laden.

## Testen

Headless im PlayMode:
    Unity -batchmode -runTests -testPlatform PlayMode -projectPath <PROJ>

Nach jedem groesseren Block: melden was getestet wurde, mit Ergebnis.
Was nicht automatisiert testbar ist (Aussehen, Spielgefuehl), wird
ehrlich als ungeprueft benannt.

## Offene Entscheidungen

- [ ] Render-Pipeline: Built-in vs URP (Universal Render Pipeline).
      URP ist der uebliche Weg fuer PC-Spiele mit Ziel 60 FPS auf
      Mittelklasse-Hardware und noetig fuer moderne Grafik-Effekte.
      Wechsel spaeter ist muehsam (alle Materialien brechen). Sollte
      vor Phase 1 entschieden werden.
