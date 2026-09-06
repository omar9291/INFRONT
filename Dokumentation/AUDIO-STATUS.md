# Audio-Stand – 2026-09-06

61 neue Audiodateien; 42 stabile Klangkennungen, davon 24 mit Audiodateien und
18 bewusst oder vorläufig synthetisch. Die Zählung unterscheidet Aufnahmen,
Foley-Samples und elektronische Musik; importierte Dateien sind nicht automatisch
Feldaufnahmen. Vier bestehende Schussaufnahmen bleiben unverändert.

Neue Inhalte: Beton-/Metall-/Schuttschritte mit Varianten, Materialeinschläge,
Airsoft-Nachladen und trockene Mechanikaufnahmen, menschliche Atmung, ein aufgenommenes
Lüftungsgeräusch für Innenräume, Menümusik sowie Rundenauftakt und Spannung.
Musik hat einen getrennten Lautstärkeregler. Klangkennungen wurden ausschließlich
angehängt; bestehende gespeicherte oder übertragene Werte bleiben erhalten.

`Werkzeuge/prepare_audio.py DOWNLOAD_ORDNER` erzeugt die Bearbeitungen reproduzierbar
mit numpy und soundfile. Archive werden nicht unter Assets eingebaut. Die
Archiv-Lizenzen für Schritte und Kenney wurden gelesen und mitgenommen;
bei Einzeldateien und beim Raumtonarchiv fehlen eingebettete Lizenzdateien,
weshalb die jeweiligen Anbieterbelege ausdrücklich so bezeichnet sind.
CC-BY-Schritte behalten Originaltitel, Urheber, Bearbeiter, Lizenzlink und
Bearbeitungsvermerk, sichtbar im Spiel und in CREDITS.md.

Technisch geprüft: 11/11 EditMode-Tests für Importe, Pegel und Quellen. Erster
Gesamtlauf: 294/295 PlayMode; nach Korrektur der alten Credits-Prüfung 30/30
betroffene Spielprüfungen bestanden. Mac-Testbuild erfolgreich; Einstellungen und
HUD bei 1600 × 786 angesehen. Nach einem im Benchmark entdeckten Explosionsfilter-
Fehler an der Rundgangkamera bestehen 296/296 im vollständigen Schlusslauf;
der korrigierte Mac-Build ist erfolgreich.
Hör-Rückmeldung zur ersten Fassung: „Soll rauer und realistischer klingen“.
Daraufhin wurden die künstliche Tonhöhenstreuung der Schritte reduziert, die
Musik zurückgenommen sowie Bombenexplosion und Strukturknarzen durch bearbeitete
Aufnahmen ersetzt. Die zweite Vorschau ist erstellt, aber noch nicht abgenommen.
Einzelne Außengeräusche bleiben zur Verbesserung offen. Der gesamte Ton-Abschnitt
ist damit noch nicht abgeschlossen.

Laufzeit geprüft: Alle Varianten werden beim Start vorgeladen. Im reproduzierbaren
M1-Lauf mit Full, klarem Wetter und 5 gegen 5 erreicht der fertige Build 59,94 FPS
im Mittel und 51,44 FPS beim 1-%-Tiefpunkt. 7/7 betroffene Hörfiltertests bestehen;
der Mac-Build ist erfolgreich (302,7 MB).

| Klangkennung | Herkunft | Credit bzw. Grund |
|---|---|---|
| SchussGewehr | recording | free-firearm-sound-library |
| SchussMp | recording | free-firearm-sound-library |
| SchussSniper | recording | free-firearm-sound-library |
| SchussPistole | recording | free-firearm-sound-library |
| SchussFern | synthetic | Designed low-frequency tail for distance propagation; layered with the actual recorded shot rather than replacing it. |
| Zischen | synthetic | Designed near-miss cue with controlled duration and stereo position, kept distinct from environmental recordings. |
| Nachladen | recording | springyspringo-mechanics |
| WaffeWechsel | recording | lfa-equipment-clicks |
| TrefferMarke | synthetic | Short non-diegetic hit confirmation; deliberately electronic for clear feedback. |
| TrefferKopf | synthetic | Electronic head-hit confirmation, distinct from the recorded impact foley. |
| Abschuss | synthetic | Non-diegetic elimination confirmation, kept electronic for consistent feedback. |
| EigenerTod | synthetic | Non-diegetic defeat cue; no human death recording is needed. |
| OhrenPfeifen | synthetic | Tinnitus effect requires a controlled synthetic tone and envelope. |
| EinschlagWand | sampled-foley | kenney-impact-sounds |
| EinschlagKoerper | sampled-foley | kenney-impact-sounds |
| AtemEin | recording | mikeask-breathing |
| AtemAus | recording | mikeask-breathing |
| AtemKeuchen | recording | mikeask-breathing |
| AtemSchnappen | recording | mikeask-breathing |
| SchrittLeise | recording | footsteps-boots |
| SchrittNormal | recording | footsteps-boots |
| SchrittLaut | recording | footsteps-boots |
| RundeStart | synthetic | Short interface confirmation; the separate musical round-start sting uses the credited composition. |
| RundeSieg | synthetic | Electronic round-win confirmation, deliberately distinct from combat audio. |
| RundeNiederlage | synthetic | Electronic round-loss confirmation, deliberately distinct from combat audio. |
| KaufzeitVorbei | synthetic | Electronic buy-time confirmation supports precise event timing. |
| BombePiep | synthetic | A bomb timer is an electronic beep, so synthesis is intentional. |
| BombeGelegt | synthetic | Electronic arming confirmation, not a simulation of a mechanical recording. |
| BombeEntschaerft | synthetic | Electronic defuse confirmation, kept distinct from the timer beep. |
| BombeExplosion | recording | rubberduck-bangs |
| Wind | synthetic | Seamless synthesized outdoor weather bed retained; actual recorded ventilation now supplies the indoor room tone. Recorded outdoor wind remains open. |
| FernesFeuergefecht | synthetic | Designed distant battle layer retained pending a suitable field recording; nearby weapon shots use recordings. |
| Artillerie | synthetic | Designed distant whistle and blast retained pending a suitable credited composite or recording. |
| Hubschrauber | synthetic | Designed distant rotor bed retained; downloaded alternative still requires provenance and listening checks. |
| MetallKnarzen | recording | bart-workshop |
| SchrittMetall | recording | footsteps-metal |
| SchrittSchutt | recording | footsteps-gravel |
| EinschlagMetall | sampled-foley | kenney-impact-sounds |
| Raumton | recording | legit-audio-room |
| MusikMenue | music | yd-factory-music |
| MusikSpannung | music | yd-factory-music |
| MusikRundenstart | music | yd-factory-music |
