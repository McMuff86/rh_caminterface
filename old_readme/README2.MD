README.md — Rhino CNC Exporter (DXF/2D → Xilog/Maestro)
Leitfaden & Arbeitsvertrag für die C#-Portierung (Rhino 8) als professionelles Rhino-Plugin.
Ziel: Aus 2D-Geometrien + Layerregeln robuste Xilog (.xcs) Programme für Maestro erzeugen – reproduzierbar, validierbar, mit UI & Maschinenprofilen.

0) Codebasis (Prototyp)
Python-Prototyp (RhinoPython):
prototypes/python/rhino_maestro_generator.py
(Dieser Stand ist die funktionale Spezifikation — alle Regeln & Mappings sind hier umgesetzt. Die C#-Implementierung spiegelt dieses Verhalten 1:1, außer wir notieren bewusst Abweichungen in /docs/IMPLEMENTATION.md.)

1) Problem, Vision, Status
Problem: Zeichnung in Rhino → CAM → Post → Maschine = zersplittert, manuell, fehlerträchtig.
Vision: Rhino-Plugin, das aus Layer-basierten Regeln direkt maschinenfähige .xcs generiert, inkl. Technologien, Z-Strategien, Makros, Profilen & Vorschau.

Status (Prototyp kann das bereits):

Werkstück aus WK_PIECE (DX/DY), DZ als Default.

CUT (Konturfräsen), POCKET, DRILL, DRILLROW.

Rückwandnut als
a) Channel (RBNUT_CH_*): Linie→Rechteck, Mitte/Positiv-Seite, Z-Schritte.
b) RNT-Makro (RBNUT_RNT_*): echte Maestro/CAD+T-Makros, Mitte/Positiv-Seite, Überlauf.

Z-Strategien: A) Tech-Stepdown oder B) Layer-Stepdown (_S…).

Eindeutige Namen (auch für ISO) → keine „Name mehrfach“-Fehler.

2) Layerkonventionen (Spezifikation)
Einheiten: mm · Top Workplane · DXF: 2004 ASCII empfohlen.

Werkstück
WK_PIECE → geschlossene Kurve → CreateFinishedWorkpieceBox(DX,DY,DZ)
DZ (Default): 19 mm (profilabhängig konfigurierbar).

Konturen (Fräsen)
php-template
Kopieren
Bearbeiten
CUT_E<nnn>[_Z<tiefe>][_S<stepdown>][_D<werkzeugØ>]
z.B. CUT_E010_Z16_S4_D9.5
Taschen (Pocket)
php-template
Kopieren
Bearbeiten
POCKET_E<nnn>[_Z<t>][_S<s>][_D<Ø>][_O<offsetstep>]
(_O default = Ø × 0.7) – konzentrische Offsets nach innen.
Bohrungen
css
Kopieren
Bearbeiten
DRILL_D<Ø>[_Z<t>][_C P|L]
Geometrie: Punkt / Kreis / ArcCurve.IsCircle()  (P=Top/Positiv, L=Bottom/Negativ)
Lochreihen
php-template
Kopieren
Bearbeiten
DRILLROW_D<Ø>_Z<t>_P<pitch>[_N<count>]
Entlang Kurve; wenn N fehlt ⇒ floor(L/pitch)+1.
Rückwandnut — Channel (fräsen)
php-template
Kopieren
Bearbeiten
RBNUT_CH_X_W<w>[_Z<t>][_S<s>][_E<nnn>]_[M|P]  # X-gerichtet (horizontale Linie)
RBNUT_CH_Y_W<w>[_Z<t>][_S<s>][_E<nnn>]_[M|P]  # Y-gerichtet (vertikale Linie)
M = mittig zur Referenzlinie
P = einseitig positiv  (X-Nut ⇒ Y+, Y-Nut ⇒ X+)
Überlauf: ±5 mm (konfigurierbar)
Rückwandnut — RNT-Makro (echtes CAD+T/Maestro)
css
Kopieren
Bearbeiten
RBNUT_RNT_X_W<w>[_Z<t>]_C<code>_[M|P]
RBNUT_RNT_Y_W<w>[_Z<t>]_C<code>_[M|P]
Makroaufrufe (gemäß Referenz):

X-gerichtet

xilog
Kopieren
Bearbeiten
CreateMacro("Nut in X-Richtung_*","RNT",
  x_start, y_center, width, -1,-1,-1, x_len, depth, true,
  "code","-1", false,false,true, y_center, null,null,null,null, true);
Y-gerichtet

xilog
Kopieren
Bearbeiten
CreateMacro("Nut in Y-Richtung_*","RNT",
  x_center, y_start, width, -1,-1,-1, y_len, depth, true,
  "code","-1", false,false,true, x_center, null,null,null,null, true);
3) Architektur (C# / Rhino 8 / .NET 7)
Ziele: modulare Kernlogik, testbar ohne UI, dünne Rhino-Abhängigkeit in Orchestrierung & Geometrie.

bash
Kopieren
Bearbeiten
RhinoCNCExporter/
  RhinoCNCExporter.csproj                 # .NET 7, Rhino 8
  PlugIn.cs                               # class PlugIn : Rhino.PlugIns.PlugIn
  Commands/
    ExportXilogCommand.cs                 # Benutzer-Entry (UI → Orchestrierung)
    SettingsCommand.cs                    # Panel öffnen
  UI/
    SettingsPanel.cs                      # Eto.Forms (Profile, DZ, Offsets, Templates)
    ExportDialog.cs                       # A/B Stepdown, Zielpfad, Preview-Zählung
  Core/
    LayerParser/
      Specs.cs                            # DTOs: CutSpec, PocketSpec, DrillSpec, ...
      LayerRegex.cs                       # Regex & TryParse(...) – PURE
    Geometry/
      GeometryUtils.cs                    # Polyline, Offsets, Orientation, GrooveHelpers
    Naming/
      NameService.cs                      # eindeutige Namen, max Länge
    Emitters/
      XilogEmitter.cs                     # Header/Body/Footer, ISO optional
      EmitCut.cs / EmitPocket.cs / EmitDrill.cs / EmitRow.cs
      EmitGrooveChannel.cs / EmitGrooveRnt.cs
    Profiles/
      MachineProfile.cs                   # Defaults, Tech-Mapping, RNT-Templates, DZ
      MaestroCadTProfile.cs               # Standardprofil (E010 etc.)
  docs/
    IMPLEMENTATION.md                     # Umsetzungsschritte/Entscheide je Modul
    LAYER_CHEATSHEET.md                   # Kurzreferenz Layer
    CHANGELOG.md
  prototypes/
    python/rhino_maestro_generator.py     # Python-Referenzimplementierung
  tests/
    RhinoCNCExporter.Tests.csproj         # xUnit (Rhino 8)
    LayerParserTests.cs
    NamingTests.cs
    GeometryTests.cs
    EmitterTests.cs
4) Build, Laufzeit & Packaging
Target: Rhino 8 (Windows), .NET 7

RhinoCommon: Referenz über Rhino 8-Installation (oder RhinoCommon NuGet, falls verfügbar für v8).

UI: Eto.Forms (im Rhino SDK enthalten).

Release: RHI-Paket mit Manifest.

Konfiguration: %APPDATA%/RhinoCNCExporter/config.json (Profile, DZ, Offsets, RNT-Templates, ISO-Option).

5) Unit-Tests (xUnit, Rhino 8)
Philosophie: Pure Logik testbar ohne Rhino UI. Geometrienahe Tests nutzen Rhino.Geometry (headless möglich).
Kategorien:

Parser-Tests (pure)

CUT_E010_Z16_S4_D9.5 → CutSpec{ Tech=E010, Depth=16, Stepdown=4, Dia=9.5 }

Fehlerhafte Layer → TryParse == false.

Naming-Tests (pure)

NameService generiert eindeutige, gekürzte Namen.

Geometry-Tests (Rhino.Geometry)

Polyline-Sampling schließt sauber.

Pocket-Offset-Zählung bei typischen Formen.

Groove-Endpoints aus Linie (X/Y, M/P, Overtravel).

Emitter-Tests (pure text)

EmitGrooveRnt erzeugt exakt das RNT-Template (mit x_len/y_len).

EmitCut erzeugt A/B-Stepdown-Pässe in korrekter Reihenfolge.

ISO optional & eindeutig, wenn aktiviert.

Beispiel-Testcode (Auszug):

csharp
Kopieren
Bearbeiten
// tests/LayerParserTests.cs
using Xunit;
using RhinoCNCExporter.Core.LayerParser;

public class LayerParserTests
{
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
}
csharp
Kopieren
Bearbeiten
// tests/GeometryTests.cs
using Xunit;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Geometry;

public class GeometryTests
{
    [Fact]
    public void Groove_X_Positive_Side_Endpoints()
    {
        var line = new LineCurve(new Point3d(100, 200, 0), new Point3d(500, 200, 0));
        var ep = GrooveHelpers.EndpointsFromLine(line, Axis.X, Place.Positive, width:6.0, overtravel:5.0);

        Assert.Equal(95.0, ep.XStart, 3);    // minX - 5
        Assert.Equal(505.0, ep.XEnd, 3);     // maxX + 5
        Assert.Equal(200.0, ep.YCenter, 3);
        Assert.Equal(200.0, ep.YStart, 3);   // positive side ⇒ Y..Y+W
        Assert.Equal(206.0, ep.YEnd, 3);
    }

    [Fact]
    public void ToPolyline_Closed_LastEqualsFirst()
    {
        var rect = new Polyline(new [] {
            new Point3d(0,0,0), new Point3d(100,0,0),
            new Point3d(100,50,0), new Point3d(0,50,0), new Point3d(0,0,0)
        }).ToNurbsCurve();

        var pts = PolylineUtils.ToPoints(rect, tol:0.05);
        Assert.True(pts.Count >= 4);
        Assert.Equal(pts[0], pts[^1]);
    }
}
csharp
Kopieren
Bearbeiten
// tests/EmitterTests.cs
using Xunit;
using Rhino.Geometry;
using RhinoCNCExporter.Core.Emitters;

public class EmitterTests
{
    [Fact]
    public void Rnt_Macro_Is_Formatted_As_Specified()
    {
        var line = new LineCurve(new Point3d(0, 500, 0), new Point3d(2000, 500, 0));
        var s = GrooveRntEmitter.EmitX(
            name: "Nut in X-Richtung_1",
            line: line, width:5.5, depth:8.3, code:"066",
            placePositive:true, overtravel:35.09 // so dass x_start≈-35.09 / x_len≈? passt zu Beispiel
        );

        Assert.Contains("CreateMacro(\"Nut in X-Richtung_1\",\"RNT\",", s);
        Assert.Contains(", 8.300,true,\"066\",\"-1\",false,false,true,", s);
    }
}
Tests ausführen:

bash
Kopieren
Bearbeiten
dotnet test
Hinweis: Für Rhino.Geometry-Tests muss RhinoCommon v8 referenziert sein. In CI nur auf Windows-Runnern mit Rhino 8 SDK verfügbar machen (siehe CI-Setup).

6) UI & Persistenz
Settings Panel (Eto):
Maschinenprofil, DZ, Setup-Offsets, Default ToolØ, Corner-ISO Toggle, RNT-Templates (X/Y, editierbar), Layer-Cheatsheet.

Export-Dialog:
Z-Strategie A/B, Zielpfad, „Nur Selektion“, Preview-Zählung (Cuts/Pockets/Drills/Rows/Grooves).

Persistenz:
PlugIn.Settings + %APPDATA%/RhinoCNCExporter/config.json.
RNT-Templates je Profil anpassbar (kein Rebuild nötig).

7) Dokumentationsdisziplin
Alle Implementationsschritte/Entscheide wandern in /docs/IMPLEMENTATION.md, nicht ins README.
Strukturvorschlag pro Eintrag: Datum · Modul · Änderung · Motivation · Alternativen · Auswirkungen (Tests/Kompatibilität).

Layer-Kurzreferenz: /docs/LAYER_CHEATSHEET.md (1-Pager für Anwender).

Changelog: /docs/CHANGELOG.md (SemVer, Nutzerzentriert).

Beispiele/Samples: /samples/* (3DM/DXF + erwartete .xcs).

8) Roadmap (Next steps)
C#-Skeleton (Plugin, Command, Panel) – Build lauffähig.

Parser-DTOs (pure) + Regex + Unit-Tests.

GeometryUtils (Polyline/Offsets/Groove) + Tests.

Emitter (CUT/POCKET/DRILL/ROW/GrooveCH/GrooveRNT) + Tests.

NameService + Tests.

Settings-Panel & Export-Dialog.

Maschinenprofile (Maestro CAD+T default).

Validierung/Warnings (Horiz/Vert-Nut, Layer-Mismatches, Template-Fehler).

RHI-Packaging, Beispielpaket, interne Abnahme.

Erweiterungen: Tabs/Mikrostege, Lead-In/Out, Bottom-Workplane, Material+Tool-DB, weitere Posts.

9) CI/CD (Vorschlag)
GitHub Actions (Windows-Runner):

dotnet build + dotnet test

RhinoCommon Bereitstellung:

Option A: Pfad aus Secret RHINO_SDK_PATH einbinden

Option B: eigenständiger Artifacts-Cache mit RhinoCommon v8 DLLs (Lizenzbedingungen checken).

Artefakt: .rhp + manifest.yml + README.md + /docs/* + /samples/* → gepacktes .rhi.

10) Coding-Standards & Qualität
C# 10/11, nullable enabled, IDisposable korrekt nutzen, PURE Core-Klassen wo möglich.

Fehlerbilder klar kommunizieren (Dialog + Log), niemals schweigen.

Keine Magic Numbers – alles über Profile/Settings (DZ, Offsets, Overtravel, ISO toggle, Default Ø, Templates).

SemVer für Releases: MAJOR.MINOR.PATCH.

Telemetrie/Logging (opt-in): Nur anonyme Fehler-Events (Datei-Log & optional HTTP-Hook).

11) Bekannte Grenzen / Annahmen
Eingaben in mm, Welt-XY; Workplane „Top“.

RNT-Makro entspricht der gelieferten Signatur. Falls abweichend: Templates im Profil anpassen.

Offsets (Pocket) mit Curve.Offset (sharp) – sehr kleine Features können entfallen (Flächencheck).

Maximale Namenslänge standardmäßig 31 Zeichen (Maestro-Kompatibilität).

12) Erweiterungsideen
Tab/Mikrostege via Layer-Flags (_TAB3x10).

Lead-In/Out als Layer-Schalter (_LI3_R, _LO3_T).

Unterseite (Bottom Workplane), Flip & Rotation.

Material- & Werkzeug-DB (CSV/JSON) mit Mapping E-Technologie↔Werkzeug.

Weitere Postprozessoren (CIX, MPR, Heidenhain) als austauschbare Emitter.

2D Vorschau der Werkzeugbahnen im Panel.

13) Beispiel-Cheatsheet
objectivec
Kopieren
Bearbeiten
WK_PIECE

CUT_E010_Z16_S4_D9.5
POCKET_E015_Z8_O6
DRILL_D5_Z17_CP
DRILLROW_D5_Z17_P32
RBNUT_CH_X_W6_Z8_P
RBNUT_RNT_X_W5.5_Z8.3_C066_P
Noch etwas fürs künftige Ich (Cursor):
Python-Vorlage liegt in prototypes/python/rhino_maestro_generator.py.
Beim Port jede Logik testgetrieben übernehmen.

Wenn unsicher: Rhino Developer Docs konsultieren (RhinoCommon / Eto.Forms / RHI Packaging).

