## RH_caminterface — Rhino CNC-Exporter (XCS/CIX/MPR)

**Rhino 8 C# Plugin** zur Erzeugung professioneller CNC-Fräsprogramme aus 2D+3D-Geometrien:

- **SCM** (Maestro/CAD+T) → `.xcs` (Xilog-Format) ✅ **KOMPLETT**
- **Biesse** (bSolid/BiesseWorks) → `.cix` (BEGIN/END Blöcke) ✅ **GRUNDFUNKTIONEN**  
- **Homag** (woodWOP) → `.mpr` (ASCII-Sektionen) 🔄 **GEPLANT**

**Status März 2026:** Production-Ready für SCM/XCS. 55 Produktions-XCS-Dateien analysiert. CLAMEX-3D-Pipeline konzipiert.

**Einsatzgebiet:** Holzbearbeitung/Möbelindustrie — Platten fräsen, bohren, Nuten schneiden, Verbinder setzen.

### 🆕 Interaktives CAM-System (März 2026)

Neben dem bewährten Layer-basierten Export bietet das Plugin jetzt ein **interaktives CAM-System** — CNC-Bearbeitungen direkt auf Geometrie definieren, visualisieren und exportieren:

- **4 Operationstypen:** Kontur 🔴, Tasche 🔵, Bohrung 🟡, Nut 🟢
- **Brep-Kanten-Unterstützung:** Direkte Auswahl von Kanten an 3D-Körpern
- **Dockbares CNC Operations Panel:** Operationsliste, Eigenschafts-Editor, Schnellzugriff
- **Werkzeugbahn-Vorschau:** 2D + 3D Visualisierung mit Tiefendarstellung
- **Werkzeugbahn-Simulation:** Animierte Werkzeugbewegung entlang der Bahnen
- **Werkzeugbibliothek:** Pro Maschinenprofil, mit CRUD-Verwaltung
- **Pre-Export-Validierung:** 12 automatische Prüfungen (Werkzeug, Tiefe, Geometrie, …)
- **Workflow-Exportpanel:** gruppierte Platte-zu-Feature-Sicht mit globaler `offen`-/`bereit`-Summary, direktem Workflow-Fokus, kontextbezogenem CTA für den nächsten offenen Punkt und stabilen Automation-IDs für spätere Smoke-Tests
- **Export-Vorschau:** CNC-Code mit Syntax-Highlighting, Zeilennummern, Kopieren, Editor-Export
- **Maschinenprofile:** SCM (Xilog), Biesse (CIX), MaestroCadT — profilspezifische Standards
- **Undo-Support:** Alle Operationen in Rhinos Undo-System integriert

**Befehle:** `CNCAddContour`, `CNCAddPocket`, `CNCAddDrill`, `CNCAddGroove`, `CNCRemoveOperation`, `CNCPanel`, `CNCListOperations`

📖 Ausführliche Dokumentation: [`docs/CAM-INTERACTIVE-GUIDE.md`](docs/CAM-INTERACTIVE-GUIDE.md)

### Status auf einen Blick
- **Referenz-Codebasis (Python)**: `RH_caminterface_v007.py` (Quelle der Wahrheit für Regeln/Mappings)
- **Maestro-Handbuch (lokal analysierbar)**:
  - `Maestro Editor.pdf` (Original)
  - `maestro_editor_text.txt` (vollständiger Textauszug mit `=== Page N ===`-Markern)
  - `maestro_editor_outline.json` (Outline/Gliederung für schnelle Navigation)
- **Hilfsskript**: `extract_maestro_pdf.py` (erstellt Text und Outline aus dem PDF)

Hinweis: Die spätere C#-Umsetzung soll das Python-Verhalten 1:1 spiegeln; Abweichungen werden dokumentiert.

## Problem, Vision, Nutzen
- **Problem**: Manuelle CAM-Prozesse sind zersplittert, fehleranfällig und schwer zu versionieren.
- **Vision**: Rhino-Layerregeln → direkt maschinenfähige `.xcs`, validierbar, mit UI/Profilen steuerbar.
- **Nutzen**: Konsistenz, Nachvollziehbarkeit, weniger Fehler, einfache Schulung, schnelle Anpassung via Profile/Settings.

## Projektstruktur (aktuell)
```text
RH_caminterface/
  Maestro Editor.pdf
  maestro_editor_text.txt
  maestro_editor_outline.json
  extract_maestro_pdf.py
  RH_caminterface_v007.py
  tests/
    test_01.pgmx
    test_01.xcs
    test_02.pgmx
    test_02.xcs
```

## Quickstart
- **PDF-Analyse (optional)**:
  1) `pip install pypdf`
  2) `python extract_maestro_pdf.py` → erzeugt `maestro_editor_text.txt` und `maestro_editor_outline.json`

- **Python-Referenzcode**: `RH_caminterface_v007.py`
  - Dient als funktionale Spezifikation (Regeln, Geometrie, Emitter, Namensgebung)
  - Kann unmittelbar als Codebasis genutzt werden; Umbenennung/Modularisierung ist später möglich

## Aktuelle Features (März 2026)

### ✅ SCM/XCS-Export (Production-Ready)
- **Alle Standardoperationen:** CUT, POCKET, DRILL, DRILLROW, RBNUT
- **Pattern-Bohrungen:** `DRILLPAT_D5_X3_Y4_P32` → CreatePattern() 
- **Horizontal-Bohrungen:** `HDRILL_D8_Z30` → CreateWorkplane()
- **Bogen-Konturen:** AddArc2PointCenterToPolyline() für Rundungen
- **Production Header:** Kommentierte Header wie CAD+T-Ausgabe
- **Setup-Offsets:** Zugabe X/Y konfigurierbar

### 🔄 CLAMEX-System (In Entwicklung)
- **3D-Block-Workflow:** CLAMEX-Blöcke in 3D platzieren statt 2D-Layer
- **SawCut_Lamello-Makros:** ~48-Parameter Lamello-Verbinder  
- **3D-Pipeline Vision:** Aus 3D-Korpus pro Platte CNC-Programme ableiten

### 📊 Analysierte Produktionsdaten
- **55 echte XCS-Dateien** aus Schreinerei-Produktion analysiert
- **Neue MSL-Befehle entdeckt:** CreateBladeCut, CreateSectioningMillingStrategy, CreateHelicMillingStrategy
- **Feature-Gap geschlossen:** Production-Quality Headers, Pattern-Support, Bogen-Support

## Layerkonventionen (2D-Workflow)
Alle Angaben in Millimeter. Workplane „Top“. Empfohlenes DXF: ASCII 2004.

### Werkstück
- **Layer**: `WK_PIECE` → geschlossene Kurve → `CreateFinishedWorkpieceBox(DX, DY, DZ)`
- **Default**: `DZ = 19` mm (profilabhängig konfigurierbar)

### Konturen (CUT)
- **Muster**: `CUT_E<nnn>[_Z<tiefe>][_S<stepdown>][_D<werkzeugØ>]`
- **Beispiel**: `CUT_E010_Z16_S4_D9.5`

### Taschen (POCKET)
- **Muster**: `POCKET_E<nnn>[_Z<t>][_S<s>][_D<Ø>][_O<offsetstep>]`
- **Default**: `_O = Ø × 0.7` (konzentrische Offsets nach innen)

### Bohrungen (DRILL)
- **Muster**: `DRILL_D<Ø>[_Z<t>][_C P|L]`
- **Geometrie**: Punkt / Kreis / `ArcCurve.IsCircle()`
- **Seite**: `P` = Top/Positiv, `L` = Bottom/Negativ

### Pattern-Bohrungen (DRILLPAT) — NEU!
- **Muster**: `DRILLPAT_D<Ø>_X<xCount>_Y<yCount>_P<pitch>[_Z<t>]`
- **Beispiel**: `DRILLPAT_D5_X3_Y4_P32_Z13` → 3×4 Matrix, 32mm Abstand
- **Geometrie**: Startpunkt des Patterns

### Horizontal-Bohrungen (HDRILL) — NEU!  
- **Muster**: `HDRILL_D<Ø>[_Z<tiefe>][_SIDE L|R]`
- **Geometrie**: Punkt auf Plattenoberfläche → Bohrung von der Seite

### Lochreihen (DRILLROW)
- **Muster**: `DRILLROW_D<Ø>_Z<t>_P<pitch>[_N<count>]`
- **Hinweis**: Entlang Kurve; wenn `N` fehlt ⇒ `floor(L/pitch)+1`

### Rückwandnut — Channel (Fräsen)
- **Muster X**: `RBNUT_CH_X_W<w>[_Z<t>][_S<s>][_E<nnn>]_[M|P]`
- **Muster Y**: `RBNUT_CH_Y_W<w>[_Z<t>][_S<s>][_E<nnn>]_[M|P]`
- **Platzierung**: `M` = mittig zur Referenzlinie, `P` = einseitig positiv (X-Nut ⇒ Y+, Y-Nut ⇒ X+)
- **Überlauf**: ±5 mm (konfigurierbar)

### Rückwandnut — RNT-Makro (echtes CAD+T/Maestro)
- **Muster X**: `RBNUT_RNT_X_W<w>[_Z<t>]_C<code>_[M|P]`
- **Muster Y**: `RBNUT_RNT_Y_W<w>[_Z<t>]_C<code>_[M|P]`
- Entspricht Maestro-RNT-Makroaufrufen gemäß Handbuch; Details über Profile/Templates.

## Z-Strategien
- **A) Tech-Stepdown**: gemäß Technologie-Vorgaben
- **B) Layer-Stepdown**: `..._S<stepdown>` im Layernamen

## Namensregeln
- Eindeutige Namen (auch für ISO). Maximale Namenslänge standardmäßig 31 Zeichen (Maestro).
- Kollisionen werden durch systematische Kürzung/Nummerierung vermieden.

## Xilog-Emitter (Ausgabe)
- Erzeugt `.xcs`-Header/Body/Footer entsprechend Workplane und Profil.
- Optional ISO-Ausgabe je Operation.

## Nutzung der Maestro-Dokumente (lokal)
- **Text**: `maestro_editor_text.txt` → Volltextsuche, Seitenmarker `=== Page N ===`
- **Outline**: `maestro_editor_outline.json` → Titel/Seiten, schnelle Navigation, Querprüfung von Parametern
- **Quelle**: `Maestro Editor.pdf`

## Zielarchitektur (C# / Rhino 8 / .NET 7)
Modular, testbar, UI dünn. Übersicht:

```text
RhinoCNCExporter/
  RhinoCNCExporter.csproj            # .NET 7, Rhino 8
  PlugIn.cs                          # class PlugIn : Rhino.PlugIns.PlugIn
  Commands/
    RhinoCNCExporterCommand.cs       # Dockbares ExportPanel öffnen
    ExportXilogCommand.cs            # Legacy XCS-Dialogexport
  UI/
    ExportPanel.cs                   # Produktives Export-Panel
    ExportDialog.cs                  # Legacy Dialogexport
  Core/
    LayerParser/
      Specs.cs                       # DTOs: CutSpec, PocketSpec, DrillSpec, ...
      LayerRegex.cs                  # Regex & TryParse(...) – PURE
    Geometry/
      GeometryUtils.cs               # Polyline, Offsets, Orientation, GrooveHelpers
    Naming/
      NameService.cs                 # eindeutige Namen, max Länge
    Emitters/
      XilogEmitter.cs                # Header/Body/Footer, ISO optional
      EmitCut.cs / EmitPocket.cs / EmitDrill.cs / EmitRow.cs
      EmitGrooveChannel.cs / EmitGrooveRnt.cs
    Profiles/
      MachineProfile.cs              # Defaults, Tech-Mapping, RNT-Templates, DZ
      MaestroCadTProfile.cs          # Standardprofil (E010 etc.)
  docs/
    IMPLEMENTATION.md                # Umsetzungsschritte/Entscheide je Modul
    LAYER_CHEATSHEET.md              # Kurzreferenz Layer
    CHANGELOG.md
  tests/
    RhinoCNCExporter.Tests.csproj    # xUnit (Rhino 8)
    LayerParserTests.cs
    NamingTests.cs
    GeometryTests.cs
    EmitterTests.cs
```

## UI & Persistenz
- ExportPanel (Eto): Maschinenwahl, Export-Modus, 3D-Plattenvorschau, Zugabe X/Y, Block-Detection, Zielpfad, Export-Report.
- Export-Dialog: Legacy XCS-Schnellexport mit Z-Strategie A/B und „Nur Selektion“.
- Persistenz: produktiv aktuell keine separate globale Settings-Oberfläche; exportrelevante Laufzeitoptionen sitzen im ExportPanel.

## Tests & Qualität
- **Philosophie**: Pure Logik testbar ohne Rhino-UI. Geometrienahe Tests können Rhino.Geometry headless nutzen.
- **Kategorien**: Parser, Naming, Geometry, Emitter, Profile.
- **CI-Setup**:
  - `core-ci.yml` auf GitHub Hosted Runnern für Core + Tests, nur bei relevanten Core-/Test-/Workflow-Änderungen
  - `windows-plugin-build.yml` auf Self-Hosted Windows Runnern mit Rhino 8 für Plugin-Build + Yak-Package, automatisch nur auf `main` und ebenfalls nur bei relevanten Code-/Script-Pfaden, sonst manuell per `workflow_dispatch`
  - `release-package.yml` auf demselben Self-Hosted Runner für Tag-Releases, mit clean checkout ohne persistierte Credentials, Concurrency-Schranke und Timeout

### Ausführung
- Lokal Tests: `./scripts/test.ps1`
- Lokal Build: `./scripts/build.ps1`
- Lokal Package: `./scripts/package.ps1`
- Komplett lokal: `./scripts/build-and-install.ps1`
- Hinweis: Für Plugin-Build und Rhino.Geometry-nahe Tests muss Rhino 8 / RhinoCommon verfügbar sein.

### Beispiel-Testfälle (Auszug)
```csharp
// tests/LayerParserTests.cs
[Theory]
[InlineData("CUT_E010_Z16_S4_D9.5", "E010", 16.0, 4.0, 9.5)]
[InlineData("CUT_E15", "E015", 19.0, null, 9.5)] // Default DZ=19, Default Dia=9.5
public void Cut_Parse_Ok(string layer, string tech, double depth, double? sd, double dia)
{
    var ok = CutSpec.TryParse(layer, defaults: Defaults.Mock, out var spec);
    Assert.True(ok);
    Assert.Equal(tech, spec.Tech);
    Assert.Equal(depth, spec.Depth, 3);
    Assert.Equal(sd, spec.Stepdown);
    Assert.Equal(dia, spec.ToolDiameter, 3);
}
```

```csharp
// tests/GeometryTests.cs (Auszug)
[Fact]
public void Groove_X_Positive_Side_Endpoints()
{
    var line = new LineCurve(new Point3d(100, 200, 0), new Point3d(500, 200, 0));
    var ep = GrooveHelpers.EndpointsFromLine(line, Axis.X, Place.Positive, width:6.0, overtravel:5.0);
    Assert.Equal(95.0, ep.XStart, 3);
    Assert.Equal(505.0, ep.XEnd, 3);
    Assert.Equal(200.0, ep.YCenter, 3);
    Assert.Equal(200.0, ep.YStart, 3);
    Assert.Equal(206.0, ep.YEnd, 3);
}
```

```csharp
// tests/EmitterTests.cs (Auszug)
[Fact]
public void Rnt_Macro_Is_Formatted_As_Specified()
{
    var line = new LineCurve(new Point3d(0, 500, 0), new Point3d(2000, 500, 0));
    var s = GrooveRntEmitter.EmitX(
        name: "Nut in X-Richtung_1",
        line: line, width:5.5, depth:8.3, code:"066",
        placePositive:true, overtravel:35.09
    );
    Assert.Contains("CreateMacro(\"Nut in X-Richtung_1\",\"RNT\",", s);
    Assert.Contains(", 8.300,true,\"066\",\"-1\",false,false,true,", s);
}
```

## Roadmap (Auszug)
- C#-Skeleton (Plugin/Command/Panel)
- Parser-DTOs + Regex + Unit-Tests
- GeometryUtils (Polyline/Offsets/Groove) + Tests
- Emitter (CUT/POCKET/DRILL/ROW/GrooveCH/GrooveRNT) + Tests
- NameService + Tests
- ExportPanel & Export-Dialog
- Maschinenprofile (Maestro CAD+T default)
- Validierung/Warnings (Nut horizontal/vertikal, Layer-Mismatches, Template-Fehler)
- Packaging (.rhi), Beispieldateien, interne Abnahme

## CI/CD
- **Workflow 1: Core CI**
  - Datei: `.github/workflows/core-ci.yml`
  - Hosted Runner
  - baut `RhinoCNCExporter.Core` und `RhinoCNCExporter.Tests`
  - führt xUnit-Tests aus
- **Workflow 2: Windows Plugin Build**
  - Datei: `.github/workflows/windows-plugin-build.yml`
  - Self-hosted Windows Runner mit Label `Rhino8`
  - läuft aus Sicherheitsgründen nur auf trusted `push` und `workflow_dispatch`, nicht auf `pull_request`
  - ExportPanel-Workflow-CTAs zeigen offene vs. gesamte Zuweisungen direkt im Label (`2 offen / 6 gesamt`)
  - baut Plugin, führt Tests aus, erzeugt Yak-Package
  - lädt `.yak` und `.rhp` als Artifacts hoch
- **Workflow 3: Release Package**
  - Datei: `.github/workflows/release-package.yml`
  - läuft bei Tags wie `v0.2.0`
  - nutzt denselben self-hosted Windows Runner, aber jetzt ebenfalls mit clean checkout ohne persistierte Credentials, Concurrency-Schranke und Timeout
  - erzeugt GitHub Release mit `.yak`, `.rhp` und `manifest.yml`
- **Scripts**
  - `scripts/build.ps1`
  - `scripts/test.ps1`
  - `scripts/package.ps1`
  - `scripts/install.ps1`
  - `scripts/build-and-install.ps1`

## Multi-Agent Setup
- Worktree-Bootstrap: `./scripts/setup-agent-worktrees.ps1 -Ticket <name> -BaseBranch main`
- Empfohlene Rollen:
  - UI Agent
  - Core/Test Agent
  - CI/Build Agent
  - Docs Agent
  - Review Agent
- Ausführliche Regeln: `docs/MULTI-AGENT-WORKFLOW.md`

## Coding-Standards & Qualität
- C# 10/11, `nullable` enabled, IDisposable korrekt nutzen, PURE Core-Klassen wo möglich
- Fehler klar kommunizieren (Dialog + Log); keine stillen Fehler
- Keine Magic Numbers – alles über Profile/Settings (DZ, Offsets, Overtravel, ISO toggle, Default Ø, Templates)
- SemVer für Releases (MAJOR.MINOR.PATCH)

## Bekannte Grenzen / Annahmen
- Eingaben in mm, Welt-XY; Workplane „Top“
- RNT-Makro entspricht der gelieferten Signatur (abweichende Templates im Profil anpassen)
- Offsets (Pocket) via Curve.Offset (sharp) – sehr kleine Features können entfallen (Flächencheck)
- Maximale Namenslänge: 31 Zeichen (Maestro-Kompatibilität)

## Erweiterungsideen
- Tabs/Mikrostege via Layer-Flags (`_TAB3x10`)
- Lead-In/Out als Layer-Schalter (`_LI3_R`, `_LO3_T`)
- Unterseite (Bottom Workplane), Flip & Rotation
- Material-/Werkzeug-DB (CSV/JSON) mit Mapping E-Technologie ↔ Werkzeug
- Weitere Postprozessoren (CIX, MPR, Heidenhain) als austauschbare Emitter
- 2D-Vorschau der Werkzeugbahnen im Panel

## Beispiele und Tests
- Unter `tests/` liegen Beispieldateien (`*.pgmx`, `*.xcs`) zur manuellen/visuellen Prüfung.
- Automatisierte Tests folgen schrittweise im Zuge der C#-Portierung.

## Hinweise für Beiträge
- Änderungen am Verhalten in `docs/IMPLEMENTATION.md` dokumentieren (SOLL/IST, Motivation, Auswirkungen).
- Layer-Kurzreferenz als eigenständige Datei `docs/LAYER_CHEATSHEET.md` pflegen.
- PR-Template ausfüllen.
- Bei grösseren Änderungen zusätzlich `CONTEXT-HANDOFF.md` und relevante Docs aktualisieren.
- Für parallele Agent-Arbeit immer Worktrees verwenden, nicht denselben Checkout.

---

Fragen, Anpassungen oder nächste Schritte? Die Python-Referenz `RH_caminterface_v007.py` ist die Quelle der Wahrheit. Das Maestro-Handbuch liegt lokal als Text und Outline vor und erleichtert Validierung und Feinspezifikation.
