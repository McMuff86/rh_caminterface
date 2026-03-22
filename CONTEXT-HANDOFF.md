# CONTEXT-HANDOFF — RH_caminterface / RhinoCNCExporter

Dieses Dokument dient dem schnellen Einstieg bei Sitzungswechsel oder Übergabe an einen neuen Agenten/Entwickler.

---

## Was ist das Projekt?

Ein **Rhino 8 C#-Plugin** (Yak Package), das aus 2D-Geometrien + Layer-Konventionen CNC-Fräsprogramme generiert für:
- **SCM** (Maestro/CAD+T) → `.xcs` (Xilog-Format)
- **Biesse** (bSolid/BiesseWorks) → `.cix` (BEGIN/END Blöcke) oder `.bpp` (INI-Style)
- **Homag** (woodWOP) → `.mpr` (ASCII-Sektionen) oder `.mprx` (XML)

Einsatzgebiet: Holzbearbeitung / Möbelindustrie — Platten fräsen, bohren, Nuten schneiden.

## Aktueller Stand (zuletzt aktualisiert: 2026-03-22)

### Was existiert und funktioniert
- **Python-Referenz** (`RH_caminterface_v007.py`): Vollständig funktional, kann .xcs-Dateien erzeugen
- **C#-Skeleton**: Projekt-Struktur steht, baut erfolgreich
  - LayerParser (Regex + DTOs): implementiert
  - NameService: implementiert mit Tests
  - XilogEmitter: Header/Footer funktional, Operationen sind **Stubs**
  - UI: Settings-Panel + Export-Dialog als Grundgerüst
- **Yak-Vorbereitung**: manifest.yml erstellt, .csproj für Rhino 8 netcore konfiguriert

### Was fehlt / nächste Schritte
1. **Emitter-Implementierung** — Die Emit*.cs-Dateien geben nur Kommentare zurück, keine echte .xcs-Ausgabe
2. **GeometryUtils** — Polyline-Sampling, Offset-Berechnung, Groove-Konstruktion fehlen
3. **IEmitter-Interface** — Für Multi-Maschinen-Support brauchen wir eine Abstraktion
4. **Biesse-Emitter** (.cix) — Noch nicht begonnen
5. **Homag-Emitter** (.mpr) — Noch nicht begonnen
6. **End-to-End-Test** — C#-Ausgabe gegen Python-Referenz validieren

## Schlüsseldateien

| Datei | Zweck |
|-------|-------|
| `RH_caminterface_v007.py` | Quelle der Wahrheit — alle Regeln, Mappings, Emitter-Logik |
| `maestro_editor_text.txt` | Durchsuchbarer Maestro-Handbuch-Text (Page-Marker) |
| `manifest.yml` | Yak Package Manifest für Rhino Package Manager |
| `RhinoCNCExporter/RhinoCNCExporter.csproj` | Plugin-Projekt (net7.0-windows, Rhino 8) |
| `RhinoCNCExporter/Core/LayerParser/LayerRegex.cs` | Alle Regex-Patterns + Parsing |
| `RhinoCNCExporter/Core/LayerParser/Specs.cs` | DTOs (CutSpec, PocketSpec, DrillSpec, ...) |
| `RhinoCNCExporter/Core/Emitters/XilogEmitter.cs` | SCM-Ausgabe (Header/Footer) |
| `RhinoCNCExporter/Core/Emitters/Emit*.cs` | Operations-Emitter (aktuell Stubs!) |
| `RhinoCNCExporter/Core/Profiles/MachineProfile.cs` | Maschinenprofil-Basisklasse |
| `RhinoCNCExporter/Services/ExportService.cs` | Orchestrierung des Exports |
| `tests/test_01.xcs`, `test_02.xcs` | Referenz-Ausgaben der Python-Implementierung |

## Architektur-Entscheidungen

### Warum C# und nicht Python?
- Rhino-Plugins müssen als .rhp (kompilierte DLL) vorliegen für produktiven Einsatz
- RhinoPython-Scripts haben keinen Zugang zu Eto.Forms UI, Plugin-Settings, Yak-Packaging
- Performance und Typsicherheit für produktive Nutzung

### Warum Yak Package?
- Offizieller Rhino Package Manager — einfache Installation für Endbenutzer
- Automatische Updates via `_PackageManager` in Rhino
- Build: `yak build --platform win` → `rhinocncexporter-0.1.0-rh8_0-win.yak`
- Publish: `yak push <package>.yak`

### Multi-Maschinen-Strategie
```
Rhino-Geometrie → LayerParser → Specs (maschinenunabhängig)
                                  ↓
                    ExportService + MachineProfile
                                  ↓
                    ┌─────────────┼─────────────┐
                    ↓             ↓             ↓
              XilogEmitter  BiesseEmitter  HomagEmitter
                (.xcs)     (.cix/.bpp)    (.mpr/.mprx)
```

## Maschinenformat-Übersicht

### SCM — Xilog/Maestro (.xcs)
- Text-Format, Zeilenbasiert
- Maestro-Handbuch als Referenz: `maestro_editor_text.txt`
- Python-Referenz implementiert vollständig

### Biesse — CIX (.cix)
- Text-Format mit `BEGIN MACRO ... END MACRO` Blöcken
- Werkstück: `BEGIN MAINDATA` → LPX, LPY, LPZ
- Bohren: `NAME=BG` (X, Y, Dp, Dia, Thr)
- Fräsen: `NAME=ROUTG` + `NAME=GEO` (Geometrie separat definiert)
- Tasche: `NAME=POCK` (GID, Dia, Dp, TYP)
- Nut: `NAME=CUT_G` (X, Y, Xe, Ye, Dp)
- Geometrie: START_POINT, LINE_EP, ARC_EPCE, ENDPATH
- Seiten: 0=top, 1=bottom, 2=left, 3=right, 4=front, 5=back
- Open-Source Referenz: [BppLib](https://github.com/viachpaliy/BppLib) (C#)

### Homag — woodWOP MPR (.mpr)
- ASCII-Text, 5 feste Blöcke: `[H`, `[001`, `[K`, `]n`, `<ID \Name\`
- Werkstück: `<100 \WerkStck\` → LA (Länge), BR (Breite), DI (Dicke)
- Variablen: L, W, T mit Formeln (SIN, COS, IF/THEN/ELSE)
- Bohren: `<102 \BohrVert\` (XA, YA, DU, TI, AN)
- Kontur: `<105 \Konturfraesen\` + Kontur `]n` (KP, KL, KA)
- Nut: `<109 \Nuten\` (XA, YA, LA, TI, NB)
- Tasche: `<112 \Tasche\` (XA, YA, LA, BR, TI)
- Offizielle Spec: "woodWOP Formatbeschreibung" Dok-Nr. 9-080-42-7190
- Open-Source Referenz: [prgToMPR](https://github.com/mustafayildizmuh/prgToMPR) (C#)

## Build-Umgebung

```bash
# Build
dotnet build RhinoCNCExporter/RhinoCNCExporter.csproj -c Release

# Tests
dotnet test RhinoCNCExporter.Tests/RhinoCNCExporter.Tests.csproj

# Output
# RhinoCNCExporter/bin/Release/net7.0-windows/RhinoCNCExporter.rhp

# In Rhino laden
# _-PlugIn _Install "C:\Users\Adi.Muff\repos\RH_caminterface\RhinoCNCExporter\bin\Release\net7.0-windows\RhinoCNCExporter.rhp"
```

### Rhino 8 SDK DLL-Pfade (auf diesem Rechner verifiziert)
- `C:\Program Files\Rhino 8\System\netcore\RhinoCommon.dll` — Core API
- `C:\Program Files\Rhino 8\System\netcore\Rhino.UI.dll` — UI/Panels
- `C:\Program Files\Rhino 8\System\Eto.dll` — Eto.Forms (**nicht** in netcore/, sondern im Hauptverzeichnis!)

### Company / Branding
- Organization: **Solid-ai.ai** (in AssemblyInfo.cs)
- Plugin GUID: `2e8c8a7c-1bcb-4b0d-8a56-4b2b6f0d7f6e`

## Bekannte Fallen / Gotchas

- **Eto.dll Pfad**: Liegt in `System\`, NICHT in `System\netcore\` — häufige Build-Fehlerquelle
- **Maximale Namenslänge**: 31 Zeichen (Maestro-Limit) — NameService kürzt automatisch
- **RNT-Makro-Signatur**: Muss exakt dem Maestro-Format entsprechen (siehe Python-Referenz)
- **CIX ist kein XML**: Trotz "X" im Namen — es sind BEGIN/END Textblöcke
- **MPR Konturen**: Werden als separate `]n` Blöcke definiert und von Operationen referenziert
- **Biesse Seiten vs Homag**: Biesse nutzt SIDE=0-5, Homag nutzt Koordinatensysteme (KO)
- **System.Drawing.Common**: Wird als NuGet-Paket (v7.0.0) benötigt wegen `Icon`-Typ in `Panels.RegisterPanel`
- **Yak Package**: manifest.yml muss im Root des dist-Ordners liegen, `.rhp` daneben
- **Workplane**: Immer "Top", Eingaben immer in mm
- **.gitignore**: Ist vorhanden — `bin/`, `obj/`, `*.rhp`, `*.yak` werden ignoriert

## Wie weiterarbeiten

### Phase 1 — SCM/Maestro Emitter fertigstellen
1. **Lies die Python-Referenz** (`RH_caminterface_v007.py`) — sie definiert das Soll-Verhalten
2. Implementiere die Emit*.cs Stubs mit echtem Code (Port aus Python)
3. Implementiere GeometryUtils (Polyline-Sampling, Offsets, Groove-Konstruktion)
4. **Teste gegen Referenz-Ausgaben** (`tests/test_01.xcs`, `test_02.xcs`)
5. ExportService End-to-End zum Laufen bringen

### Phase 2 — Multi-Maschinen-Abstraktion
6. IEmitter-Interface extrahieren
7. XilogEmitter darauf refactoren
8. IMachineProfile-Interface

### Phase 3+ — Biesse/Homag
9. **BppLib** als Referenz für CIX-Format nutzen (https://github.com/viachpaliy/BppLib)
10. **woodWOP Formatbeschreibung** für MPR-Format konsultieren (Dok-Nr. 9-080-42-7190)
11. **Maestro-Handbuch** bei Detailfragen: `maestro_editor_text.txt`

### Rhino-Kommandos zum Testen
- `ExportXilog` — Export-Dialog öffnen
- `RhinoCNCExporterSettings` — Settings-Panel öffnen
