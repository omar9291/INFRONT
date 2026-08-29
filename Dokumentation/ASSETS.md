# ASSETS.md — Verwendete Asset-Pakete und Lizenzen

Letzte Aktualisierung: 2026-08-29

Jedes importierte Asset-Paket wird hier eingetragen, BEVOR es benutzt wird:
Name, Quelle, Lizenztyp, ob kommerzielle Nutzung erlaubt ist.

## Regel

Vor Nutzung pruefen: Erlaubt die Lizenz kommerzielle Nutzung? Das Spiel
koennte spaeter unter der Marke "Driftlab" auf itch.io veroeffentlicht
werden. Wenn ein Asset das nicht abdeckt: hier vermerken UND den Nutzer
warnen, bevor es eingebaut wird.

Die meisten Unity-Asset-Store-Pakete laufen unter der "Unity Extension
Asset EULA" oder "Standard Unity Asset Store EULA" — beide erlauben die
Nutzung im fertigen Spiel, auch kommerziell. Was NICHT geht: das Asset
selbst weiterverkaufen oder einzeln weitergeben.

## Version 1 — verwendete Pakete

Noch keine. V1 nutzt ausschliesslich Unity-Bordmittel und selbst per Code
erzeugte Platzhalter-Geometrie (graue Boxen).

| Paket | Version | Quelle | Lizenz | Kommerziell? | Wofuer | Eingetragen am |
|-------|---------|--------|--------|--------------|--------|----------------|
| (keins) | | | | | | |

## Unity-Pakete (Package Manager, keine Store-Assets)

| Paket | Version | Zweck | Lizenz |
|-------|---------|-------|--------|
| com.unity.render-pipelines.universal | 17.6.0 | Render-Pipeline (URP) | Unity Companion License |
| com.unity.netcode.gameobjects | 2.13.2 | Netzwerk (server-autoritativ) | Unity Companion License |
| com.unity.inputsystem | 1.20.0 | Eingabe (Tastatur/Maus/Gamepad) | Unity Companion License |
| com.unity.ai.navigation | 2.0.14 | NavMesh fuer Bots (ab Phase 3) | Unity Companion License |
| com.unity.test-framework | 1.7.0 | PlayMode-Tests | Unity Companion License |

Alle unter Unity Companion License: im fertigen Spiel frei nutzbar, auch
kommerziell. Stand: installiert in Phase 1 (2026-08-29).

## Spaeter geplante Asset-Kategorien

Wenn es an Grafik geht (Spaeter-Stufe 4), werden hier Pakete fuer
Charaktermodelle, Waffen, Umgebung und Animationen eingetragen. Jedes
einzeln mit Lizenzpruefung.
