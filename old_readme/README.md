## RH_caminterface — Rhino 2D → Xilog/Maestro Generator

Professioneller Leitfaden für die Python-Referenzimplementierung sowie die geplante C#-Portierung (Rhino 8). Ziel ist die reproduzierbare Erzeugung robuster Xilog (.xcs) Programme aus 2D-Geometrien in Rhino auf Basis klarer Layerregeln, inklusive Technologien, Z-Strategien, Makros, Maschinenprofilen und Vorschau.

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

## Layerkonventionen (Spezifikation)
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
    ExportXilogCommand.cs            # Benutzer-Entry (UI → Orchestrierung)
    SettingsCommand.cs               # Panel öffnen
  UI/
    SettingsPanel.cs                 # Eto.Forms (Profile, DZ, Offsets, Templates)
    ExportDialog.cs                  # A/B Stepdown, Zielpfad, Preview-Zählung
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

## Tests & Qualität
- **Philosophie**: Pure Logik testbar ohne Rhino-UI. Geometrienahe Tests können Rhino.Geometry headless nutzen.
- **Kategorien**: Parser, Naming, Geometry, Emitter, Profile.
- **CI-Idee**: Windows-Runner, `dotnet build` + `dotnet test`; RhinoCommon v8 nur bereitstellen, wo nötig.

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
- Settings-Panel & Export-Dialog
- Maschinenprofile (Maestro CAD+T default)
- Validierung/Warnings (Nut horizontal/vertikal, Layer-Mismatches, Template-Fehler)
- Packaging (.rhi), Beispieldateien, interne Abnahme

## CI/CD (Vorschlag)
- GitHub Actions (Windows-Runner)
- Schritte: `dotnet build` + `dotnet test`
- RhinoCommon-Bereitstellung: Secret `RHINO_SDK_PATH` oder Cache mit RhinoCommon v8 (Lizenzbedingungen beachten)
- Artefakte: `.rhp` + `manifest.yml` + `docs/*` + `samples/*` + `README`

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

---

Fragen, Anpassungen oder nächste Schritte? Die Python-Referenz `RH_caminterface_v007.py` ist die Quelle der Wahrheit. Das Maestro-Handbuch liegt lokal als Text und Outline vor und erleichtert Validierung und Feinspezifikation.


