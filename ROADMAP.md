# ROADMAP — RH_caminterface / RhinoCNCExporter

## Vision

Ein Rhino 8 Plugin (Yak Package), das aus 2D-Geometrien und Layer-Konventionen CNC-Programme für **SCM**, **Biesse** und **Homag** Maschinen erzeugt — konsistent, validierbar, erweiterbar.

---

## ✅ Phase 1: SCM/Maestro — Core fertigstellen

**Status**: KOMPLETT ✅

- [x] LayerParser vollständig implementiert + Tests ✅
- [x] GeometryUtils (Polyline, Offsets, Groove-Berechnungen) + Tests ✅
- [x] Emitter komplett implementiert (CUT, POCKET, DRILL, ROW, GrooveCH, GrooveRNT) ✅
- [x] NameService finalisiert + Tests ✅
- [x] ExportService Orchestrierung (Geometrie sammeln → parsen → emittieren → Datei schreiben) ✅
- [x] Maschinenprofile (MaestroCadTProfile als Default) ✅
- [x] .xcs-Ausgabe gegen Python-Referenz validiert ✅
- [ ] Settings-Panel & Export-Dialog (Eto.Forms) — UI noch zu finalisieren
- [ ] Validierung & Warnings (Layer-Mismatches, Geometrie-Probleme) — planned

## ✅ Phase 2: Emitter-Abstraktion — Multi-Maschinen-Architektur

**Status**: KOMPLETT ✅

- [x] `IEmitter`-Interface definiert (gemeinsame Schnittstelle für alle Maschinenformate) ✅
- [x] `IMachineProfile`-Interface (Defaults, Technologie-Mapping, Werkzeuge) ✅
- [x] XilogEmitter auf IEmitter refactored ✅
- [x] MachineProfile auf IMachineProfile refactored ✅
- [x] BiesseProfile implementiert ✅
- [x] BiesseEmitter Grundstruktur (Header, Drill, Cut) ✅
- [x] ExportService maschinenunabhängig (beide Formate unterstützt) ✅
- [x] E2E Tests erweitert für Interface-Validierung ✅
- [ ] Emitter-Registry / Factory-Pattern für dynamische Emitter-Auswahl — geplant

## Phase 3: Biesse-Support (.cix / .bpp)

**Status**: Grundstruktur implementiert, Erweiterung geplant

### Format-Details (aus Research)
- **CIX** (bSolid, aktuell): Text-Format mit `BEGIN/END` Blöcken (kein XML!)
  - `BEGIN MAINDATA` → LPX/LPY/LPZ (Werkstück)
  - `BEGIN MACRO NAME=BG` → Bohren (X, Y, Dp, Dia)
  - `BEGIN MACRO NAME=ROUTG` → Fräsen (GID, Dia, Dp, RSP, WSP, CRC)
  - `BEGIN MACRO NAME=POCK` → Tasche (GID, Dia, Dp, TYP=ptZIG)
  - `BEGIN MACRO NAME=CUT_G` → Nut/Schnitt (X, Y, Xe, Ye, Dp, Ang)
  - Geometrie via `GEO` + `START_POINT` / `LINE_EP` / `ARC_EPCE` / `ENDPATH`
- **BPP** (BiesseWorks, legacy): INI-Style mit `[PROGRAM]` Sektion, `@`-prefixed Operationen
- **Referenz-Library**: [BppLib](https://github.com/viachpaliy/BppLib) (C#, liest/schreibt BPP+CIX)
- **Seiten**: SIDE=0 (top), 1 (bottom), 2 (left), 3 (right), 4 (front), 5 (back)

### Tasks
- [x] BiesseProfile erstellt (Technologie-Mapping, Werkzeug-IDs, Defaults) ✅
- [x] BiesseEmitter Grundstruktur implementiert (.cix-Generierung, BEGIN/END Blöcke) ✅
- [x] Basis-Operationen implementiert:
  - [x] Header → MAINDATA (LPX, LPY, LPZ) ✅
  - [x] DRILL → BG (Generic Boring) ✅
  - [x] CUT → ROUTG + GEO (Geometry + Routing) ✅
  - [x] RNT → Rectangular routing (no native RNT macro) ✅
- [x] Tests für Biesse-Emitter implementiert ✅
- [x] Beispiel-CIX Referenzdatei erstellt ✅
- [ ] Erweiterte Operationen:
  - [ ] POCKET → POCK + GEO
  - [ ] DRILLROW → BG mit Repeat (Nrp/Dx/Dy)
  - [ ] Komplexere Geometrien (Arcs, komplexe Konturen)
- [ ] BppLib NuGet Integration evaluieren
- [ ] Optional: BPP-Ausgabe für ältere BiesseWorks-Installationen
- [ ] Biesse-spezifische Validierung

## Phase 4: Homag-Support (.mpr / .mprx)

**Status**: Geplant

### Format-Details (aus Research)
- **MPR** (woodWOP, alle Versionen): ASCII-Text, 5 Blöcke in fester Reihenfolge
  - `[H` → Header (VERSION, MAT, INCH=0 für mm)
  - `[001` → Variablen (L=Länge, W=Breite, T=Dicke)
  - `[K` → Koordinatensysteme
  - `]n` → Konturen (KP=Punkt, KL=Linie, KA=Bogen, KR=Rundung)
  - `<ID \Name\` → Operationen mit Parametern
- **Operations-IDs**:
  - `<100 \WerkStck\` → Werkstück (LA, BR, DI)
  - `<102 \BohrVert\` → Vertikalbohren (XA, YA, DU, TI)
  - `<103 \BohrHoriz\` → Horizontalbohren
  - `<105 \Konturfraesen\` → Konturfräsen (Referenziert Kontur ]n)
  - `<109 \Nuten\` → Nut/Sägeschnitt (XA, YA, LA, TI, NB=Breite)
  - `<112 \Tasche\` → Tasche (XA, YA, LA, BR, TI)
  - `<119 \Polygonzug\` → Polygon-Fräsen
- **MPRX** (woodWOP 6+): XML-basiert, für CAD/CAM-Plugin-Daten
- **MPRXE** (woodWOP 8+, 2021): Neues Austauschformat, schneller
- **Offizielle Spec**: "woodWOP Formatbeschreibung" (~70 Seiten, Dok-Nr. 9-080-42-7190)
- **Referenz**: [prgToMPR](https://github.com/mustafayildizmuh/prgToMPR) (C#), Autodesk Post Processor

### Tasks
- [ ] MPR-Referenzdatei beschaffen (Export aus woodWOP)
- [ ] HomagProfile erstellen (Technologie-Mapping, Werkzeug-Nummern)
- [ ] HomagEmitter implementieren (.mpr-Generierung, Sektionen [H, [0, ], <)
- [ ] Operationen mappen:
  - CUT → `<105 \Konturfraesen\` + Kontur `]n`
  - POCKET → `<112 \Tasche\`
  - DRILL → `<102 \BohrVert\`
  - DRILLROW → `<102 \BohrVert\` mit AN (Anzahl) + AB (Abstand)
  - RBNUT → `<109 \Nuten\`
- [ ] Konturen als `]n` Blöcke (KP, KL, KA Elemente) generieren
- [ ] Optional: MPRX-Ausgabe für neuere woodWOP-Versionen
- [ ] Tests gegen bekannte .mpr-Referenzdateien
- [ ] Homag-spezifische Validierung

## Phase 5: UI & UX

- [ ] Maschinenauswahl im Export-Dialog (SCM / Biesse / Homag)
- [ ] Profil-Editor (Technologien, Werkzeuge, Defaults pro Maschine)
- [ ] 2D-Vorschau der Werkzeugbahnen im Panel
- [ ] Batch-Export (mehrere Formate gleichzeitig)
- [ ] Layer-Cheatsheet im Plugin (Schnellreferenz)

## Phase 6: Yak Package & Distribution

- [ ] Yak `manifest.yml` finalisieren (Name, Version, Keywords)
- [ ] Build-Pipeline: `dotnet build -c Release` → `yak build --platform win`
- [ ] Plugin-Icon (64x64 PNG)
- [ ] Test-Deployment auf `test.yak.rhino3d.com`
- [ ] CI/CD: GitHub Actions mit `yak.exe` + `YAK_TOKEN`
- [ ] Publish auf `yak.rhino3d.com`
- [ ] Beispieldateien (.3dm + erwartete Ausgaben pro Format)
- [ ] Benutzer-Dokumentation

---

## Erweiterungsideen (Backlog)

- Tabs/Mikrostege via Layer-Flags (`_TAB3x10`)
- Lead-In/Out als Layer-Schalter (`_LI3_R`, `_LO3_T`)
- Unterseite (Bottom Workplane), Flip & Rotation
- Material- & Werkzeug-DB (CSV/JSON)
- Weitere Postprozessoren (Heidenhain) als zusätzliche Emitter
- Grasshopper-Komponente für parametrische Workflows
- BPP-Ausgabe für ältere Biesse-Installationen
- MPRXE-Ausgabe für neueste Homag-Maschinen
