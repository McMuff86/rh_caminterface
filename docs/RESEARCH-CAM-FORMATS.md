# Deep Research: CNC-Formate & CAM-Software für Holzbearbeitung

> **Stand:** 2026-03-22  
> **Zweck:** Fundierte Grundlage für das RhinoCNCExporter Plugin (Rhino 8 C#)  
> **Autor:** Sentinel (Research Agent)

---

## 1. Maschinenformate

### 1.1 SCM Xilog / Maestro (.xcs)

#### Überblick
- **Hersteller:** SCM Group (Italien)
- **Software:** Xilog Plus (ältere Steuerung), Xilog Maestro (aktuelle Suite)
- **Dateiformat:** `.xcs` (Maestro Scripting Language / MSL), `.xxl` (Xilog Plus), `.pgm` / `.pgmx` (ISO-basiert)
- **Typ:** Textbasiert, zeilenorientiert, proprietäre Skriptsprache

#### MSL Scripting Language (XCS-Format)

Das XCS-Format ist eine **proprietäre Skriptsprache** (MSL = Maestro Scripting Language), die folgende Konzepte hat:

**Grundstruktur einer XCS-Datei:**
```
// Kommentar
SetMachiningParameters("IJ",1,10,196608,false);
CreateFinishedWorkpieceBox("name", DX, DY, DZ);
double DZ = 19.000;
SetWorkpieceSetupPosition(x_off, y_off, z_off, rot);

SelectWorkplane("Top");
// Operationen...
```

**Werkstück-Definition:**
- `CreateFinishedWorkpieceBox(name, length, width, thickness)` — Werkstückmaße
- `SetWorkpieceSetupPosition(x, y, z, rotation)` — Aufspann-Offset
- `SelectWorkplane("Top")` — Arbeitsebene wählen

**Verfügbare Operationen (aus Python-Referenz und MSL-Doku):**

| Operation | MSL-Funktion | Parameter |
|-----------|-------------|-----------|
| Konturfräsen | `CreatePolyline()` + `AddSegmentToPolyline()` + `CreateRoughFinish()` | Name, Tiefe, Technologie, Prozesstyp |
| Bohren | `CreateDrill()` | Name, X, Y, Tiefe, Durchmesser, Typ |
| Tasche | `CreatePolyline()` + Offset-Logik + `CreateRoughFinish()` | Konturen mit Stepover |
| Nut (Kanal) | `CreatePolyline()` + `CreateRoughFinish()` | Rechteck-Kontur mit Überlauf |
| Nut (RNT-Makro) | `CreateMacro("name","RNT",...)` | Position, Breite, Tiefe, Code |
| Sägen | `CreateCutX()` / `CreateCutY()` | Position, Tiefe |

**Konturfräsen im Detail:**
```javascript
// Polyline definieren
CreatePolyline("CUT_1", startX, startY);
AddSegmentToPolyline(x1, y1);
AddSegmentToPolyline(x2, y2);
// ...
// Kompensation & Strategien
SetCompensationMode(false);
SetApproachStrategy(false, true, 2);
SetRetractStrategy(false, true, 2.0, 2);
SetPneumaticHoodPosition(null);
// Operation erstellen
CreateRoughFinish("CUT_1_OP", depth, "", TypeOfProcess.GeneralRouting, "E010", "-1", 2, -1, -1, -1, 0);
ResetApproachStrategy();
ResetRetractStrategy();
```

**Bohren im Detail:**
```javascript
CreateDrill("DRILL_1", x, y, depth, diameter, "", TypeOfProcess.Drilling, "-1", "-1", 1, -1, -1, "P");
ResetPattern();
```

**RNT-Makro (Rückwandnut via Makro):**
```javascript
CreateMacro("name","RNT", x_start, y_center, width, -1, -1, -1, length, depth, true,
            "066", "-1", false, false, true, y_center, null, null, null, null, true);
```

**Wichtige Einschränkungen:**
- **Maximale Namenslänge:** 31 Zeichen (hart!)
- **Koordinatensystem:** Werkstück-Ecke unten links = Nullpunkt
- **Workplane:** Immer "Top" für Plattenbearbeitung
- **Kurven:** Controller konvertiert intern zu Liniensegmenten
- **Keine offizielle Format-Spezifikation** öffentlich verfügbar — Doku nur im MSL-Handbuch (173 Seiten, Scribd)

**MSL-Connector / CAD+T Integration:**
- SCM bietet den "MSL Connector" für externe Software-Integration
- Erlaubt externen CAD/CAM-Programmen, XCS-Dateien zu generieren
- Die Maschinensteuerung (Worktable, Niederhalter, Werkzeugpfade) wird von SCM übernommen

#### Quellen
- Maestro Scripting Language Manual (173 Seiten, Scribd: document/407837280)
- SCM XilogMaestro Broschüre: scmgroup.com/products/docs/software-xilog-maestro/
- Python-Referenz: `RH_caminterface_v007.py` (funktionale Implementation)
- Test-Dateien: `tests/test_01.xcs`, `tests/test_02.xcs`
- Fusion 360 XCS Post-Processor (Marek Skotak, LinkedIn, 2023)
- PolyBoard Pro PP: Erzeugt `.xcs` für Maestro und `.xxl` für Xilog Plus

---

### 1.2 Biesse CIX / BPP (.cix, .bpp)

#### Überblick
- **Hersteller:** Biesse Group (Italien)
- **Software:** bSolid (aktuelle 3D CAD/CAM), BiesseWorks (ältere 2D-Steuerung)
- **Dateiformate:** `.cix` (text-basiert, BEGIN/END Blöcke), `.bpp` (INI-Style), `.cid` (Variante)
- **Typ:** Textbasiert, makro-orientiert

#### CIX-Format — Detaillierte Spezifikation

**Grundstruktur:**
```
BEGIN ID CID3
	REL= 5.0
END ID

BEGIN MAINDATA
	LPX=800.00000
	LPY=320.00000
	LPZ=18.00000
	ORLST="5,8"
	SIMMETRY=1
	TLCHK=0
	TOOLING=""
	FCN=1.000000
	MATERIAL="wood"
	...
END MAINDATA

BEGIN MACRO
	NAME=BV
	PARAM,NAME=ID,VALUE="Confirmat"
	PARAM,NAME=SIDE,VALUE=0
	PARAM,NAME=CRN,VALUE="1,2,4,3"
	PARAM,NAME=X,VALUE=9
	PARAM,NAME=Y,VALUE=50
	PARAM,NAME=DP,VALUE=5
	PARAM,NAME=DIA,VALUE=7
	PARAM,NAME=THR,VALUE=YES
END MACRO
```

**MAINDATA-Sektion (Werkstück):**
| Parameter | Bedeutung |
|-----------|-----------|
| LPX | Werkstücklänge (X) |
| LPY | Werkstückbreite (Y) |
| LPZ | Werkstückdicke (Z) |
| ORLST | Origins-Liste (Referenzpunkte) |
| SIMMETRY | Symmetrie-Modus |
| FCN | Einheitenfaktor (1.0 = mm, 25.4 = inch) |
| MATERIAL | Materialtyp |
| JIGTH | Vorrichtungsdicke |
| CUSTSTR | Spannsystem-Parameter |
| CHKCOLL | Kollisionskontrolle |

**Seiten-System (SIDE):**
| Wert | Seite |
|------|-------|
| 0 | Oben (Top) |
| 1 | Unten (Bottom) |
| 2 | Links |
| 3 | Rechts |
| 4 | Vorne |
| 5 | Hinten |

**Ecken-System (CRN):**
- "1" = Ecke vorne links
- "2" = Ecke vorne rechts
- "3" = Ecke hinten rechts
- "4" = Ecke hinten links
- "1,2,4,3" = Alle Ecken (Spiegeln)

#### Operationen

**Bohren vertikal (BV):**
```
BEGIN MACRO
	NAME=BV
	PARAM,NAME=ID,VALUE="ShelfPin"
	PARAM,NAME=SIDE,VALUE=0
	PARAM,NAME=CRN,VALUE="1"
	PARAM,NAME=X,VALUE=50
	PARAM,NAME=Y,VALUE=70
	PARAM,NAME=Z,VALUE=0
	PARAM,NAME=DP,VALUE=13
	PARAM,NAME=DIA,VALUE=5
	PARAM,NAME=THR,VALUE=YES
	PARAM,NAME=RTY,VALUE=rpY
	PARAM,NAME=DX,VALUE=0
	PARAM,NAME=DY,VALUE=180
	PARAM,NAME=NRP,VALUE=2
END MACRO
```
- `RTY` = Repetition type (rpNO, rpX, rpY, rpXY)
- `NRP` = Anzahl Wiederholungen
- `THR` = Durchbohren (YES/NO)
- `TTP` = Tool Type

**Bohren horizontal (BH):**
- Gleiche Struktur, SIDE=2-5 für Seitenflächen

**Bohren generisch (BG):**
- Universelle Bohr-Operation, zusätzlich mit Werkzeugname (TNM)

**Schnitte (CUT_X, CUT_Y):**
```
BEGIN MACRO
	NAME=CUT_X
	PARAM,NAME=SIDE,VALUE=0
	PARAM,NAME=CRN,VALUE="1"
	PARAM,NAME=X,VALUE=0
	PARAM,NAME=Y,VALUE=300
	PARAM,NAME=Z,VALUE=0
	PARAM,NAME=DP,VALUE=8
	PARAM,NAME=L,VALUE=800
	PARAM,NAME=TNM,VALUE="LAMA120"
	PARAM,NAME=CRC,VALUE=2
END MACRO
```

**Schnitt generisch (CUT_G):**
- Für beliebig positionierte Schnitte: X, Y, Xe, Ye, Dp

**Fräsen mit Geometrie (ROUTG + GEO):**
```
BEGIN MACRO
	NAME=GEO
	PARAM,NAME=ID,VALUE="G1003.1001"
	PARAM,NAME=SIDE,VALUE=0
	PARAM,NAME=CRN,VALUE="1"
	PARAM,NAME=DP,VALUE=0
END MACRO

BEGIN MACRO
	NAME=START_POINT
	PARAM,NAME=X,VALUE=0
	PARAM,NAME=Y,VALUE=0
	PARAM,NAME=Z,VALUE=0
END MACRO

BEGIN MACRO
	NAME=LINE_EP
	PARAM,NAME=XE,VALUE=100
	PARAM,NAME=YE,VALUE=0
	PARAM,NAME=ZE,VALUE=0
END MACRO

BEGIN MACRO
	NAME=LINE_EP
	PARAM,NAME=XE,VALUE=100
	PARAM,NAME=YE,VALUE=50
	PARAM,NAME=ZE,VALUE=0
END MACRO

BEGIN MACRO
	NAME=ENDPATH
END MACRO

BEGIN MACRO
	NAME=ROUTG
	PARAM,NAME=ID,VALUE="P1001"
	PARAM,NAME=GID,VALUE="G1003.1001"
	PARAM,NAME=DIA,VALUE=12.000
	PARAM,NAME=DP,VALUE=5
	PARAM,NAME=TNM,VALUE="TOOL_12"
	PARAM,NAME=TTP,VALUE=103
	...
END MACRO
```

**Fräsen integriert (ROUT):**
- Kombination aus Geometrie + Fräsparametern in einem Block
- Geometrie direkt nach ROUT als START_POINT/LINE_EP/ARC_EPCE/ENDPATH

**Tasche (POCK):**
- Verweist auf GID (Geometrie-ID)
- Parameter: DIA, DP, TYP (Taschenstrategie)

**Geometrie-Elemente:**
| Klasse | CIX-Name | Beschreibung |
|--------|----------|-------------|
| StartPoint | START_POINT | Startpunkt (X, Y, Z) |
| LineEp | LINE_EP | Linie zum Endpunkt (XE, YE, ZE) |
| LincEp | LINC_EP | Inkrementelle Linie (XI, YI, ZI) |
| ArcEpCe | ARC_EPCE | Bogen mit Endpunkt + Zentrum |
| ArcEpRa | ARC_EPRA | Bogen mit Endpunkt + Radius |
| EndPath | ENDPATH | Pfad-Ende |

#### BPP-Format (INI-Style)

Das BPP-Format ist eine alternative Darstellung mit INI-Style-Syntax:
```ini
[HEADER]
TYPE=BPP
VER=150

[DESCRIPTION]
...

[MAINDATA]
LPX=800
LPY=320
LPZ=18

[PROGRAM]
BV ID="Confirmat" SIDE=0 CRN="1,2,4,3" X=9 Y=50 Z=0 DP=5 DIA=7 THR=YES TTP=1
CUT_X SIDE=0 CRN="1" X=0 Y=LPY-20 Z=0 DP=8 L=LPX TNM="LAMA120" CRC=2
ROUT ID="P1021" SIDE=0 CRN="2" Z=0 DP=5 DIA=12 THR=YES DIN=20 DOU=20
  START_POINT X=18 Y=0
  LINC_EP XI=0 YI=10
  ENDPATH
```

#### BppLib — Open-Source C# Referenz-Library

**Repository:** [github.com/viachpaliy/BppLib](https://github.com/viachpaliy/BppLib)

**Struktur:**
- `BppLib.Core` — Hauptlibrary für BPP/CIX-Erzeugung (NuGet verfügbar!)
- `BppLib.BppParser` — Parser für .bpp Dateien → `BiesseProgram`
- `BppLib.CixParser` — Parser für .cix Dateien → `BiesseProgram`
- `BppLib.CIDFile` — CID-Format Unterstützung
- `BppLib.Examples` — Beispiele

**Kern-Klasse `BiesseProgram`:**
```csharp
var pg = new BiesseProgram();
pg.Lpx = 800;
pg.Lpy = 320;
pg.Lpz = 18;
pg.Origins = "5,8";

// Bohren
pg.Operations.Add(new Bv{
    Id="Confirmat", Side=0, Crn="1,2,4,3",
    X=9, Y=50, Dp=5, Dia=7, Thr=true, Ttp=1
});

// Schnitt
pg.Operations.Add(new CutX{
    Side=0, Crn="1", X=0, Y=pg.Lpy-20,
    Dp=8, L=pg.Lpx, Tnm="LAMA120", Crc=ToolCorrection.Left
});

// Fräsen mit integrierter Geometrie
pg.Operations.Add(new Rout{
    Id="Milling", Side=0, Crn="2",
    Z=0, Dp=5, Dia=12, Thr=true, Din=20, Dou=20
});
pg.Operations.Add(new StartPoint{X=18, Y=0});
pg.Operations.Add(new LincEp{Xi=0, Yi=10});
pg.Operations.Add(new LincEp{Xi=100, Yi=0});
pg.Operations.Add(new EndPath());

// Ausgabe
File.WriteAllText("output.bpp", pg.AsBppCode());
File.WriteAllText("output.cix", pg.AsCixCode());
```

**Verfügbare Klassen:**

*Bohr-Operationen:* Bca, Bcl, Bg, BGeo, Bh, Bv, S32
*Schnitt-Operationen:* CutF, CutFR, CutG, CutGeo, CutX, CutY
*Fräs-Operationen:* Rout, RoutG, Pock
*Geometrie (Linien):* LincEp, LineAnXe, LineAnYe, LineEp, LineEpAnTp, LineEpTp, LineLnAn, LineLnTp, LineLnXe, LineLnYe
*Geometrie (Bögen):* AincAnCe, AincEpRa, ArcAnCe, ArcAnCeRaTp, ArcCeTs, ArcCeTsPk, ArcEpCe, ArcEpRa, ArcEpRaTp, ArcEpTp, ArcIpEp, ArcRaTs, ArcRaTsPk
*Figuren:* Circle3P, CircleCR, Ellipse, Oval, Polygon, Rectangle, Star
*Seiten:* WFC, WFG, WFGL, WFGPS, WFL
*Spezial:* Geo, GeoText, OffGeo, StartPoint, EndPath, Chamfer, ConnectorA, ConnectorB

**Bewertung für unser Plugin:**
- ✅ Perfekte C# Referenz — gleiche Sprache wie unser Plugin
- ✅ NuGet Package verfügbar — direkt integrierbar
- ✅ Parser + Writer für beide Formate
- ✅ Umfassende Operation-Abdeckung
- ⚠️ Eventuell als Dependency einbindbar statt selber zu implementieren

#### Quellen
- BppLib GitHub: github.com/viachpaliy/BppLib
- Autodesk Fusion 360 Biesse CIX Post-Processor: cam.autodesk.com/posts/post.php?name=biesse+cix
- Industry Arena Forum: Biesse CIX format discussions
- Reddit r/CNC: CIX format reverse engineering threads

---

### 1.3 Homag woodWOP MPR (.mpr, .mprx)

#### Überblick
- **Hersteller:** HOMAG Group (Deutschland)
- **Software:** woodWOP (aktuelle Versionen: 7, 8, 8.1, 9)
- **Dateiformate:** `.mpr` (ASCII, seit v4.0), `.mprx` (XML-Variante, seit v6), `.mprxe` (Exchange-Format, seit v8)
- **Typ:** ASCII-Text, sektionsbasiert, mit integriertem Formel-Parser
- **Offizielle Spec:** "woodWOP-Formatbeschreibung (MPR-Format)" Dok-Nr. 9-080-42-7190

#### MPR-Format — Vollständige Spezifikation

**Dateistruktur (5 Blöcke, feste Reihenfolge!):**

```
[H                          ← 1. Dateikopf (Header)
VERSION="4.0"
MAT="HOMAG"
...

[000                        ← 2. Variablentabelle (optional)
L="950.0"
KM="Laenge des Bauteils"
B="600.0"
KM="Breite des Bauteils"

[K                          ← 3. Koordinatensysteme (optional)
<00 \Koordinatensystem\
NR="05" XP="1500" ...

]1                          ← 4. Konturzüge (optional)
$E0 KP
X=0.0 Y=0.0 Z=0.0 KO=0
$E1 KL
X=1340.0 Y=10.0

<100 \WerkStck\             ← 5. Bearbeitungen
LA="L" BR="W" DI="T"

<102 \BohrVert\             ← Vertikale Bohrung
XA="50" YA="70" ...

!                           ← Dateiende
```

**Header [H]:**
```
[H
VERSION="4.0"
MAT="HOMAG"
OP="1"              // Optimiermodus (0=keine, 1=normal, 2=bestmöglich)
FM="1"              // Freifahrmodus (0-4)
NP="1"              // Normales Programm
GP="0"              // X-gespiegelt
GY="0"              // Y-gespiegelt
MI="0"              // Mirror-Anzeige
INCH="0"            // 0=mm, 1=inch
VIEW="NOMIRROR"     // Darstellungsart
_BSX=1840.000       // Errechnete Fertigteilmaße (informativ)
_BSY=600.000
_BSZ=19.000
_FNX=3.000          // Fertigteilversatz X
_FNY=3.000          // Fertigteilversatz Y
```

**Variablentabelle [000]:**
```
[000
L="950.0"
KM="Laenge"              // Pflicht-Kommentar nach jeder Variable!
B="600.0"
KM="Breite"
Mitte="L/2"
KM="Mittelachse"
```
- Max. 8 Zeichen Variablenname
- Erstes Zeichen muss Buchstabe sein
- Formeln erlaubt (nur vorher definierte Variablen)

**Formel-Parser — Vordefinierte Funktionen:**
| Funktion | Bedeutung |
|----------|-----------|
| L, W (=B), T (=D) | Werkstücklänge, -breite, -dicke |
| _BSX, _BSY, _BSZ | Fertigteilmaße |
| SIN(), COS(), TAN() | Trigonometrie (Grad!) |
| ARCSIN(), ARCCOS(), ARCTAN() | Inverse Trigonometrie |
| SQRT(), EXP(), LN() | Mathematik |
| MOD(), PREC() | Nachkomma-/Vorkomma-Stellen |
| IF() THEN() ELSE() | Bedingte Ausdrücke |
| AND, OR, NOT | Logische Operatoren |
| _cc, _cw, _CC, _CW | Drehsinne (0-3) |
| _lf, _ri | Werkzeugradiuskorrektur links/rechts |
| _mirror, _nonmirror | Spiegelungs-Variablen |
| @ | Relativbemaßung |
| STANDARD | NC-Generator-Standardwert |

**Koordinatensysteme [K]:**
- 20 vordefinierte KS an Werkstück-Ecken (00-03, A0-A3, B0-B3, C0-C3, D0-D3)
- Eigene KS: Nr. 4-99
- Parameter: NR, XP, YP, ZP, XF, YF, ZF, D1 (Drehwinkel), KI (Kippwinkel), D2, MI

**Konturzüge ]n:**
```
]1
$E0 KP                     // Startpunkt
X=0.0 Y=0.0 Z=0.0 KO=0
$E1 KL                     // Linie
X=1340.0 Y=10.0
$E2 KA                     // Kreisbogen
X=1400 Y=0 DS=0 R=300
```
- KP = Konturpunkt (Startpunkt)
- KL = Konturlinie
- KA = Konturkreisbogen (Arc)
- KR = Ecken runden
- KF = Konturfase
- KSL/KSA = Split-Elemente
- **Achtung:** Kontur-Parameter OHNE Anführungszeichen!

#### Bearbeitungs-Makros (Operations-IDs)

| ID | Name | Beschreibung |
|----|------|-------------|
| 100 | WerkStck | Werkstückbeschreibung |
| 101 | Kommentar | Kommentar |
| 102 | BohrVert | Vertikale Bohrung |
| 103 | BohrHoriz | Horizontale Bohrung |
| 104 | BohrUniv | Universal-/Raumbohrung |
| 105 | Konturfraesen | Konturfräsen auf Konturzug |
| 106 | Konturverleimen | Kantenanleimen |
| 107 | Buendigfraesen | Bündigfräsen |
| 108 | UfluBohr | Unterflurbohren |
| 109 | Nuten | Nuten und Sägen |
| 110 | Nut_R | Nuten im Raum |
| 112 | Tasche | Vertikale Tasche |
| 113 | HTasc | Horizontale Tasche |
| 114 | FreiFormTasche | Freiförmige Tasche |
| 115 | VTasche | Vektor-Tasche (Raum) |
| 117 | NC-Code | Freier NC-Code |
| 119 | Polygonzug | Polygonzug fräsen |
| 120 | Klink | Ecken ausklinken |
| 121 | Schleifen | Kontur schleifen |
| 122 | Messen | Messen |
| 123 | Block | Block-Makro |
| 124 | Komponente | Komponenten-Makro |
| 125 | Kappen | Kappen |
| 130+ | Diverse | CF-Aggregat-Operationen |

**Werkstück (ID 100):**
```
<100 \WerkStck\
LA="800" BR="600" DI="20"
AX="5" AY="5"              // Aufmaß X/Y
RNX="100" RNY="0" RNZ="0"  // Rohteilversatz
FNX="2.5" FNY="2.5"        // Fertigteilversatz
```

**Vertikale Bohrung (ID 102):**
```
<102 \BohrVert\
XA="50"     // X-Position
YA="70"     // Y-Position
DU="5"      // Durchmesser
TI="13"     // Tiefe
AN="0"      // Drehwinkel Bohrspindel
KO="0"      // Koordinatensystem
AB="1"      // Abfahrart
BM="0"      // Bearbeitungsmodus
```

**Nuten / Sägen (ID 109):**
```
<109 \Nuten\
XA="0" YA="300"
LA="800"        // Nutlänge
TI="8"          // Nuttiefe
NB="120"        // Nutbreite (Sägeblatt)
WI="0"          // Winkel
```

**Tasche (ID 112):**
```
<112 \Tasche\
XA="100" YA="100"
LA="200"        // Taschenlänge
BR="150"        // Taschenbreite
TI="10"         // Taschentiefe
RU="5"          // Eckenradius
```

**Konturfräsen (ID 105):**
```
<105 \Konturfraesen\
KN="1"          // Konturzug-Nummer
WZ="12"         // Werkzeug-Nr/Durchmesser
TI="8"          // Frästiefe
RK="_lf"        // Radiuskorrektur (links/rechts)
EA="1"          // Ein-/Ausfahrt-Typ
```

#### MPRX-Format (XML-Variante)
- Seit woodWOP 6 verfügbar
- XML-basiert, gleiche logische Struktur
- Konvertierung: `MPRXPreprocessor_U.exe input.mprx output.mpr`
- Seit woodWOP 8: MPRXE Exchange-Format

#### prgToMPR — Open-Source C# Konverter

**Repository:** [github.com/mustafayildizmuh/prgToMPR](https://github.com/mustafayildizmuh/prgToMPR)

- Konvertiert MasterWood MasterWork `.prg` → Homag woodWOP `.mpr`
- C# Windows Forms Anwendung
- Unterstützt: Bohrungen, Fräsungen (ohne Kreisbögen)
- **Nicht** unterstützt: Pocket, Kreisbewegungen
- Ziel: woodWOP 7.0 kompatibel

**Bewertung:** Begrenzt nützlich als Referenz für MPR-Header-Erzeugung und Grundstruktur, aber die offizielle Format-Beschreibung (Dok-Nr. 9-080-42-7190) ist wesentlich umfassender.

#### Quellen
- woodWOP-Formatbeschreibung 9-080-42-7190 (75 Seiten, idoc.pub/documents/woodwop-mpr4x-format-34wmxewgy8l7)
- CNCZone Forum: Breakdown of woodwop mpr code
- prgToMPR GitHub: github.com/mustafayildizmuh/prgToMPR
- HOMAG Software Forum: forum.homag.com
- HOMAG Docs: docs.homag.cloud

---

## 2. CAM-Software Analyse

### 2.1 woodWOP (Homag)

**Typ:** Proprietäres CNC-Programmiersystem für HOMAG/Weeke-Maschinen

**Stärken:**
- **Dialogbasierte Programmierung** — Kein Code schreiben nötig, grafische Makro-Dialoge
- **Mächtiger Formel-Parser** — IF/THEN/ELSE, Trigonometrie, Variablen direkt in Parametern
- **Makro-/Komponenten-System** — Wiederverwendbare Bearbeitungsblöcke
- **Parametrische Programme** — Variablen für Abmessungen, ein Programm für viele Teile
- **Optimierung** — Automatische Bearbeitungsreihenfolge, Bohr-Clustering
- **Nesting** — Seit v8: manuelles Nesting direkt in woodWOP
- **5-Achs-Fähig** — Vektor-Fräsen, 5-Achs-Interpolation

**woodWOP 8/8.1 Neue Features:**
- Kontur-Wizard für einfache Werkstück-Programmierung
- Erweiterte Variablentabelle
- Parameter-Sets für CAM-Makros
- Verleimungs-Wizard für Kantenanleimen
- TechEdit — neue intuitive Technologie-Datenbank
- Erweiterte Feature-Erkennung
- CAD-Funktion "Teilen"
- Neues MPRXE-Austauschformat
- Freiformtaschen mit verschiedenen Rampenmodi
- Haltebrücken (Holding Webs) für Nesting

**woodWOP 9:**
- Weiterentwicklung der manuellen Nesting-Funktionen
- Verbesserte Formel-Editor mit Syntax-Highlighting
- Massenänderung von Parameterwerten
- Wizard und Technologie-Datenbank für Kantenverleimung

**Lernen für unser Plugin:**
- Parametrisches System mit Variablen ist ein Muss
- Formel-Support in Parametern ist sehr mächtig
- Makro-Bibliothek-Konzept für wiederkehrende Operationen

### 2.2 bSolid (Biesse)

**Typ:** 3D CAD/CAM-Software für Biesse-Maschinen

**Stärken:**
- **Echte 3D-Visualisierung** — Werkstück in 3D mit allen Bearbeitungen
- **Digital Twin Simulation** — Vollständige Maschinensimulation inkl. Kollisionserkennung
- **Automatisierte Werkzeugpfad-Erzeugung** — Aus 3D-Modell direkt zu CNC
- **Modularer Aufbau** — Spezifische Module für verschiedene Fertigungsprozesse
- **5-Achs-Unterstützung** — Vollständige 5-Achs-Bearbeitung für Massivholz
- **Import-Fähigkeiten** — 3D-Modelle importieren und Bearbeitungen zuweisen
- **CIX-Output** — Erzeugt .cix, das von BiesseWorks/bSolid weiterverarbeitet wird zu .cni

**Besonders:**
- Kollisionserkennung basiert auf exaktem Digital Twin der Maschine
- Werkzeug-Visualisierung mit realen Profilen
- "Planning in just a few clicks" — Benutzerfreundlichkeit als Fokus
- Smart Nesting und Cutting-Optimierung
- Smartconnection für Produktions-Workflow-Management

**Lernen für unser Plugin:**
- 3D-Vorschau der Bearbeitungen wäre ein starkes Feature
- Kollisionserkennung als Premium-Feature für später

### 2.3 Maestro (SCM)

**Typ:** CNC-Steuerungssuite für SCM-Maschinen

**Stärken:**
- **MSL Scripting Language** — Vollständige Programmiersprache für CNC
- **MSL Connector** — API für externe Software-Integration
- **CAD+T Integration** — Nahtlose Anbindung an CAD+T (Möbel-CAD)
- **Maestro Lab** — Bibliothek vorgefertigter Makros für Türen, Fenster, Treppen, Möbel
- **Grafischer Editor** — Visual Programming für Makros
- **Multi-Maschinen-Support** — Eine Suite für alle SCM-Bearbeitungszentren

**Besonderheiten:**
- Der MSL Connector ist besonders relevant für unser Plugin — er definiert genau die API,
  über die externe Software mit SCM-Maschinen kommuniziert
- "Their specialisations, our control" — externe SW macht Programmierung, SCM macht Maschinensteuerung
- Fusion 360 hat bereits einen XCS-Postprozessor (3-5 Achsen)
- PolyBoard Pro PP erzeugt .xcs und .xxl nativ

**Lernen für unser Plugin:**
- Das XCS-Format über MSL-Connector ist der richtige Ansatz
- Makro-System von Maestro Lab als Inspiration für vorgefertigte Operationen

### 2.4 NC-Hops / Grasshopper CNC Plugins

**Kein "NC-Hops" Plugin gefunden.** Stattdessen relevante Alternativen:

**KaroroCAM (Food4Rhino, 2024/2025):**
- Cross-Platform CNC/G-Code Plugin für Rhino 8
- Benötigt Anemone Grasshopper Plugin
- Erzeugt G-Code für 3-Achsen-Router
- Relativ neu, Community-getrieben

**GCode Generator (Food4Rhino):**
- "The simplest way to convert your toolpath polyline to GCode"
- Einfach, aber nur G-Code output

**madCAM (madcamcnc.com):**
- Etabliertes CAM-Plugin für Rhino
- 3-Achs bis simultane 5-Achs-Fähigkeiten
- Unterstützt NURBS-Toolpaths
- Kommerziell

**Grasshopper-basierte G-Code-Generierung:**
- Verschiedene Open-Source-Ansätze auf discourse.mcneel.com
- Meist für 3D-Druck oder einfache Fräs-Operationen
- Grundprinzip: Polyline → G-Code Textausgabe

**Lernen für unser Plugin:**
- Kein existierendes Plugin erzeugt holzspezifische Formate (CIX, MPR, XCS)
- Hier ist eine echte Marktlücke!
- Grasshopper-Integration als optionaler Kanal (z.B. über Hops)

### 2.5 Mastercam

**Typ:** Industriestandard CAD/CAM-Software

**Stärken:**
- **Dynamic Milling** — Konstantes Werkzeug-Engagement, reduzierter Verschleiss
- **Umfassende Toolpath-Strategien** — 2D bis 5-Achs
- **Post-Prozessor-System** — Anpassbar für jede CNC-Steuerung
- **Breite Maschinenunterstützung** — Von einfachen Routern bis 5-Achs-Zentren
- **Automatisierung** — Wiederkehrende Aufgaben automatisierbar
- **Holzbearbeitung:** Mastercam wird auch für CNC-Router in der Holzindustrie eingesetzt

**Mastercam + Rhino:**
- Kein direktes Plugin, aber IGES/STEP Import
- Mastercam hat eigene CAD-Umgebung
- Für unseren Anwendungsfall (Plattenbearbeitung) ist Mastercam "Overkill"

**Bewertung:**
- Mastercam ist Industriestandard für komplexe Fräsarbeiten
- Für einfache Plattenbearbeitung (Bohren, Nuten, Taschen) sind die proprietären Formate (CIX, MPR, XCS) effizienter
- Mastercam erzeugt G-Code, nicht die proprietären Formate

### 2.6 RhinoCAM (MecSoft)

**Typ:** Rhino-integriertes CAM-Plugin von MecSoft Corporation

**Stärken:**
- **Vollständig in Rhino integriert** — Kein Programmwechsel nötig
- **2½ bis 5-Achs-Toolpaths** — Umfassende Bearbeitungsstrategien
- **Simulation** — Kollisionserkennung und Material-Abtragssimulation
- **Post-Prozessoren** — G-Code für diverse CNC-Steuerungen
- **Automatic Feature Machining (AFM)** — Automatische Feature-Erkennung
- **Nesting** — Seit RhinoCAM 2024 integriert
- **TURN-Modul** — Drehteile direkt in Rhino

**Features im Detail:**
- Pocket Recognition: Automatische Taschen-Erkennung mit passendem Stepover
- Profile Identification: Offene/geschlossene Profile automatisch erkannt
- Multi-Threading für Toolpath-Berechnung
- Assoziativ zu Geometrie-Änderungen in Rhino

**RhinoCAM vs. unser Plugin:**
- RhinoCAM ist generisch (Metall, Holz, etc.) und erzeugt G-Code
- Unser Plugin ist spezialisiert auf Holz-Plattenbearbeitung und erzeugt proprietäre Formate
- RhinoCAM kostet $1'000-$10'000+ (je nach Modul)
- Unser Plugin adressiert eine Nische, die RhinoCAM nicht abdeckt

---

## 3. Open-Source Libraries & Referenzen

### 3.1 Direkt nutzbare Libraries

| Library | Sprache | Format | NuGet | Bewertung |
|---------|---------|--------|-------|-----------|
| **BppLib** | C# | CIX, BPP | ✅ Ja | ⭐⭐⭐⭐⭐ Perfekt für Biesse-Emitter |
| **prgToMPR** | C# | MPR | ❌ | ⭐⭐ Begrenzt, aber MPR-Struktur-Referenz |

### 3.2 Post-Prozessoren als Referenz

| Quelle | Format | Sprache | Link |
|--------|--------|---------|------|
| Autodesk Biesse CIX | CIX | JavaScript | cam.autodesk.com/posts/post.php?name=biesse+cix |
| Fusion 360 XCS PP | XCS | JavaScript | LinkedIn (Marek Skotak) |
| PolyBoard Pro PP | XCS, XXL | Proprietär | wooddesigner.org |
| Vectric SCM Xilog ISO | PGM | Proprietär | forum.vectric.com |

### 3.3 Relevante Standards

**ISO 6983 (G-Code / DIN 66025):**
- Universeller Standard für NC-Programmierung
- Definiert G-/M-Codes für Werkzeugmaschinen
- Für Holzbearbeitung weniger relevant, da die drei Haupthersteller (SCM, Biesse, Homag) eigene proprietäre Formate nutzen
- G-Code wird hauptsächlich für generische CNC-Router verwendet (GRBL, Mach3, LinuxCNC)
- SCM Xilog kann PGM (ISO-basiert) UND XCS (proprietär) — XCS ist empfohlen

**ISO 14649 (STEP-NC):**
- Neuerer Standard, Feature-basiert statt Wegpunkt-basiert
- In der Praxis kaum verbreitet in der Holzindustrie

### 3.4 Fehlende Open-Source-Projekte

Für folgende Formate gibt es **keine** öffentlichen Open-Source-Libraries:
- SCM XCS/MSL — Nur die Fusion 360 Post-Prozessoren als Referenz
- Homag MPR-Writer — Nur die offizielle Formatbeschreibung
- Xilog Plus XXL — Kein öffentliches Material

---

## 4. Praxis: Typische Workflows

### 4.1 Typischer Workflow Plattenbearbeitung

```
1. DESIGN
   ├── CAD-Zeichnung (Rhino, SketchUp, CAD+T, PolyBoard)
   ├── Stückliste mit Maßen, Material, Kanten
   └── DXF/STEP Export oder natives Format

2. CAM-PROGRAMMIERUNG
   ├── Werkstück-Definition (Maße, Material, Dicke)
   ├── Bohrungen setzen (Beschläge, Dübel, Regalbodenträger)
   ├── Nuten definieren (Rückwand, Teilung)
   ├── Konturfräsen (Ausschnitte, Formen)
   ├── Taschen (Einlassungen, Griffmulden)
   ├── Kantenbearbeitung (Anleimen, Bündigfräsen)
   └── Sägeschnitte (Zuschnitt)

3. NESTING (optional)
   ├── Optimierung: Teile auf Rohplatten platzieren
   ├── Verschnitt minimieren
   ├── Maserungsrichtung beachten
   ├── Haltebrücken planen
   └── Schnittfolge optimieren

4. MASCHINENTRANSFER
   ├── CNC-Datei erzeugen (.xcs, .cix, .mpr)
   ├── Barcode/QR-Label drucken
   ├── Datei an Maschine übertragen (Netzwerk/USB)
   └── Maschine optimiert intern (Werkzeugreihenfolge, Aufspannung)

5. PRODUKTION
   ├── Platte auflegen, Barcode scannen
   ├── Maschine bearbeitet automatisch
   └── Fertigteil entnehmen
```

### 4.2 Typische Operationen

| Operation | Häufigkeit | Typische Parameter |
|-----------|-----------|-------------------|
| **Vertikale Bohrung** | ⭐⭐⭐⭐⭐ | Ø 3-35mm, Tiefe 5-Materialdicke |
| **Horizontale Bohrung** | ⭐⭐⭐⭐ | Ø 5-12mm, seitlich für Dübel/Beschläge |
| **Konturfräsen** | ⭐⭐⭐⭐ | Ø 6-16mm, Tiefe = Materialdicke, Ein-/Ausfahrt |
| **Nuten** | ⭐⭐⭐⭐ | Breite 3-12mm, Tiefe 4-12mm, für Rückwände/Teilungen |
| **Rechteck-Tasche** | ⭐⭐⭐ | Für Schlösser, Scharniere, Einlassungen |
| **Sägeschnitt** | ⭐⭐⭐ | Sägeblatt Ø 120-300mm, für Zuschnitt |
| **Freiförmtasche** | ⭐⭐ | Für organische Formen, Griffmulden |
| **Kantenverleimung** | ⭐⭐⭐⭐ | PVC/ABS 0.4-2mm, Vor-/Nachfräsen |
| **Lochreihen** | ⭐⭐⭐⭐⭐ | System 32 (32mm Raster), Regalbodenträger |

### 4.3 Typische Werkzeuge

| Werkzeug | Durchmesser | Einsatz |
|----------|------------|---------|
| Spiralfräser | 6, 8, 10, 12, 16mm | Konturfräsen, Taschen |
| Schaftfräser | 6, 8, 10, 12mm | Nuten, Konturen |
| Bohrer | 3, 4, 5, 6, 8, 10, 12, 15, 35mm | Dübel, Beschläge, Topfbänder |
| Sägeblatt | 100, 120, 150, 200mm | Zuschnitt, Nuten |
| Nutfräser | 4, 5, 6, 8mm | Rückwandnuten |
| Bündigfräser | 12.7mm (½") | Kantenbearbeitung |
| Profilfräser | Diverse | Fasern, Rundungen |

### 4.4 Werkzeugverwaltung (Tooling)

**SCM/Maestro:**
- Werkzeuge über Technologie-Codes referenziert (z.B. "E010")
- Werkzeugmagazin wird in der Maschinensteuerung verwaltet
- XCS referenziert Werkzeuge über TypeOfProcess und Technologie-String

**Biesse/bSolid:**
- Werkzeuge über TNM (Tool Name) oder DIA (Durchmesser) referenziert
- TTP (Tool Type) definiert Werkzeugtyp (103=Router, etc.)
- Magazinverwaltung in bSolid/BiesseWorks

**Homag/woodWOP:**
- Werkzeuge über WZ (Werkzeug-Nummer) referenziert
- Werkzeug-Datenbank in woodWOP mit Parametern
- Automatische Werkzeugauswahl basierend auf Durchmesser

### 4.5 Nesting

**Prinzip:** Mehrere Teile optimal auf einer Rohplatte platzieren, um Verschnitt zu minimieren.

**Software:**
- **OptiNest** (Wood Designer) — Standalone Nesting
- **intelliDivide** (HOMAG) — Cloud-basiertes Nesting
- **CutRite** (Biesse) — Biesse-integriertes Nesting
- **woodWOP 8+** — Manuelles Nesting direkt in woodWOP
- **bSolid** — Smart Nesting integriert

**Workflow:**
1. Stückliste → Nesting-Software
2. Algorithmus platziert Teile (Maserung, Abstand, Reste)
3. Nesting-Software erzeugt CNC-Programme pro Platte
4. Haltebrücken (Holding Tabs) zwischen Teilen
5. Nachbearbeitung: Brücken entfernen, schleifen

---

## 5. Erkenntnisse für unser Plugin

### 5.1 Strategische Positionierung

**Marktlücke:** Es gibt kein Rhino-Plugin, das direkt die proprietären Holzbearbeitungsformate (XCS, CIX, MPR) erzeugt. Alle existierenden CAM-Plugins (RhinoCAM, madCAM, KaroroCAM) erzeugen G-Code.

**Unser USP:** Direkt aus Rhino-Geometrie + Layer-Konventionen → maschinenfertige Programme für die drei großen Hersteller.

### 5.2 Format-Prioritäten

1. **SCM XCS** (bereits implementiert in Python-Referenz) — Fertigstellen
2. **Biesse CIX** (BppLib als NuGet-Dependency!) — Niedrigste Hürde dank Open-Source-Library
3. **Homag MPR** (umfassende Format-Doku vorhanden) — Aufwändigste Eigenentwicklung

### 5.3 Architektur-Empfehlungen

**BppLib Integration:**
- BppLib.Core als NuGet-Package direkt einbinden
- Statt eigenen CIX-Emitter zu schreiben: `BiesseProgram` zusammenbauen und `.AsCixCode()` aufrufen
- Parser (BppLib.BppParser, BppLib.CixParser) für Import/Validierung nutzbar

**MPR-Emitter:**
- Eigene Implementation nötig (keine Library verfügbar)
- Offizielle Formatbeschreibung (75 Seiten) als Spezifikation nutzen
- Besonders beachten: Formel-Support in Parametern (nicht nur Zahlen, auch "L/2" etc.)
- Konturzüge mit korrekter Syntax (ohne Anführungszeichen!)

**XCS-Emitter:**
- Python-Referenz 1:1 nach C# portieren
- Maestro-Handbuch für Detailfragen

### 5.4 Operations-Mapping (Universal → Format)

| Universal-Spec | XCS | CIX | MPR |
|---------------|-----|-----|-----|
| CutSpec | CreatePolyline + CreateRoughFinish | GEO + ROUTG oder ROUT | ]n + <105 Konturfräsen |
| DrillSpec | CreateDrill | BV (vertikal), BH (horizontal) | <102 BohrVert, <103 BohrHoriz |
| DrillRowSpec | Einzelne CreateDrill Aufrufe | BV mit RTY=rpX/rpY | Einzelne <102 oder S32 |
| PocketSpec | Offset-Konturen + CreateRoughFinish | POCK (GID-Referenz) | <112 Tasche |
| GrooveSpec (CH) | Rechteck-Kontur + CreateRoughFinish | CUT_G oder CUT_X/CUT_Y | <109 Nuten |
| GrooveSpec (RNT) | CreateMacro("RNT",...) | Kein Äquivalent | Kein direktes Äquivalent |
| SawSpec | — | CUT_X / CUT_Y | <109 Nuten (mit Sägeblatt) |

### 5.5 Nächste Schritte (priorisiert)

1. **Phase 1: XCS fertigstellen** — Emit*.cs implementieren aus Python-Referenz
2. **Phase 2: BppLib evaluieren** — NuGet Package einbinden, Biesse-Emitter als Wrapper
3. **Phase 3: MPR-Emitter** — Eigene Implementation basierend auf Formatbeschreibung
4. **Phase 4: Nesting** — Einfaches manuelles Nesting als Premium-Feature
5. **Phase 5: 3D-Vorschau** — Bearbeitungen im Rhino-Viewport visualisieren

---

## 6. Quellen

### Offizielle Dokumentation
1. woodWOP-Formatbeschreibung (MPR-Format), Dok-Nr. 9-080-42-7190, HOMAG, Nov 2009 (75 Seiten)
2. Maestro Scripting Language Manual, SCM Group (173 Seiten, via Scribd)
3. XilogMaestro Software Suite Broschüre, SCM Group
4. woodWOP 8.0 New Functions, docs.homag.cloud
5. woodWOP 8.1 New Features, homag.com
6. B_SOLID Product Page, biesse.com

### Open-Source Repositories
7. BppLib — github.com/viachpaliy/BppLib (C#, NuGet)
8. prgToMPR — github.com/mustafayildizmuh/prgToMPR (C#)

### Post-Prozessoren
9. Autodesk Biesse CIX Router Post — cam.autodesk.com/posts/post.php?name=biesse+cix
10. Fusion 360 XCS Post (Marek Skotak, 2023)
11. PolyBoard SCM/Morbidelli Integration — wooddesigner.org

### Community / Foren
12. CNCZone: SCM tech 80 (xilog) — cnczone.com/forums/
13. CNCZone: Breakdown of woodwop mpr code — cnczone.com/forums/
14. Industry Arena: Biesse CIX format — industryarena.com
15. Reddit r/CNC: Biesse CIX format definition — reddit.com/r/CNC/
16. WOODWEB CNC Forum: Various threads — woodweb.com
17. HOMAG Software Forum — forum.homag.com
18. eMastercam: WoodWop MPR files — emastercam.com

### CAM-Software
19. RhinoCAM / MecSoft — mecsoft.com/products/rhinocam/
20. madCAM — madcamcnc.com
21. KaroroCAM — food4rhino.com/en/app/karorocam
22. Mastercam — mastercam.com
23. OptiNest — wooddesigner.org/optinest-nesting-software/

### Projektinterne Referenzen
24. RH_caminterface_v007.py — Python-Referenzimplementation (XCS)
25. maestro_editor_text.txt — Extrahierter Maestro-Handbuch-Text
26. tests/test_01.xcs, test_02.xcs — XCS-Referenzausgaben
