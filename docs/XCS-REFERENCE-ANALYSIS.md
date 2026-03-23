# XCS Referenz-Analyse für RhinoCNCExporter

**Datum:** 23. März 2026  
**Analysierte Dateien:** 36 echte Produktions-XCS-Dateien  
**Quelle:** `~/projects/rh_caminterface/tests/references/`

## Executive Summary

Die Analyse der 36 Produktionsdateien zeigt erhebliche Unterschiede zu unserem aktuellen `XilogEmitter.cs`. Die wichtigsten Befunde:

- **Header-Format:** Produktionsdateien haben viel ausführlichere, kommentierte Header
- **Neue Macro-Typen:** `SawCut_Lamello` (CLAMEX-System) zusätzlich zu `RNT` und `XPARK`
- **Pattern-Bohrungen:** `CreatePattern()` für Bohrmuster wird häufig verwendet (122x)
- **Bogenverarbeitung:** `AddArc2PointCenterToPolyline()` für gerundete Ecken (12x)
- **Horizontale Bohrungen:** Komplexe `CreateWorkplane()`-Definitionen für Seitenbearbeitung
- **E-Codes:** 11 verschiedene Technologie-Codes (E004, E005, E009, E010, E013, E015, E019, E021, E022, E025, E032)

## 1. Befehls-Inventar

### Häufigkeitsverteilung (Top 20)
```
SelectWorkplane                 459 (alle Dateien)
CreateDrill                     399 (Bohrungen)
ResetPattern                    399 (nach jedem Drill)
AddSegmentToPolyline            217 (Konturzüge)
CreatePattern                   122 (Bohrmuster - FEHLT bei uns!)
CreateMacro                     103 (Makros)
SetApproachStrategy              52
SetRetractStrategy               52
SetPneumaticHoodPosition         50
ResetRetractStrategy             49
ResetApproachStrategy            48
CreatePolyline                   47
SetCompensationMode              46
CreateRoughFinish                46
SetWorkpieceSetupPosition        42
CreateWorkplane                  40 (Freie Ebenen - TEILWEISE bei uns)
SetMachiningParameters           36 (Header)
CreateFinishedWorkpieceBox       36 (Header)
AddArc2PointCenterToPolyline     12 (Bögen - FEHLT bei uns!)
CreateWorkplan                    6 (Workplanes)
```

### Befehle die wir NICHT implementiert haben
1. **`CreatePattern()`** - Für Bohrmuster/Arrays (122 Vorkommen!)
2. **`AddArc2PointCenterToPolyline()`** - Bögen in Konturen (12 Vorkommen)
3. **`CreateWorkplane("Freie Ebene_XXX", ...)`** - Horizontale Bohrungen
4. **`CreateMacro(..., "SawCut_Lamello", ...)`** - CLAMEX-Verbinder
5. **`CreateWorkplan()`** - Arbeitsebenen-Definition

## 2. Pattern-Analyse

### Header-Format (Produktion vs. Unser Emitter)

**Produktions-Header:**
```xcs
// *** Programm created by CAD+T CAM Interface, Version 24.34.01 ***
//**********************************************************
//**********************************************************
// *** Programmparameter setzen *** 
SetMachiningParameters("IJ",1,10,196608,false);
//**********************************************************
//**********************************************************
// *** Bauteil erstellen ***
CreateFinishedWorkpieceBox("Boden", 813.5, 380, 19);
//**********************************************************
//**********************************************************
// *** Bauteil Infos ***
//CreateMessage("Projekt","projekt_name",false,false);
//CreateMessage("Datei","datei_name.xcs",false,false);
//CreateMessage("Bemerkung"," ",false,false);
//**********************************************************
//**********************************************************
double DZ = 19;
//AddVariable("Entnehmen",0,0,1,"",false,true);
//**********************************************************
//**********************************************************
// *** Bauteil Offsets ***
SetWorkpieceSetupPosition(2.5,2.5,0,0);
//**********************************************************
//**********************************************************
```

**Unser Header:**
```xcs
// *** Programm created by Rhino→Maestro Generator ***
SetMachiningParameters("IJ",1,10,196608,false);
CreateFinishedWorkpieceBox("test_01", 2240.000, 300.000, 19.000);
double DZ = 19.000;
SetWorkpieceSetupPosition(2.5,2.5,0.0,0.0);

```

### Footer-Format

**Alle Produktionsdateien enden mit:**
```xcs
// Macro RNT
CreateMacro("Wegfahrschritt", "XPARK");
//**********************************************************
//**********************************************************
// *** Programm Ende ***
```

**Unser Footer:**
```xcs
CreateMacro("Wegfahrschritt","XPARK");
```

### Workpiece-Definition
- **Setup-Position:** Konstant `SetWorkpieceSetupPosition(2.5,2.5,0,0)` (alle Dateien)
- **DZ-Variable:** Immer als `double DZ = [thickness];` definiert
- **Naming:** Sinnvolle Namen statt "test_01" (`"Boden"`, `"Tablar"`, `"Seite_rechts"`)

### Reihenfolge der Operationen
1. Header mit Kommentar-Blöcken
2. Machining Parameters
3. Workpiece Box
4. Commented Project Info (CreateMessage - auskommentiert)
5. DZ Variable
6. Setup Position
7. Bearbeitungen:
   - Aussenkontur (meist mit +10/-10 Offset für Brücken)
   - Vertikale Bohrungen
   - Horizontale Bohrungen (mit CreateWorkplane)
   - Makros (RNT, CLAMEX)
8. Footer

## 3. Technologie-Codes & Werkzeuge

### E-Codes in Produktion
- **E004, E005:** CLAMEX-Verbinder (verschiedene Durchmesser)
- **E009:** Spezialbohrungen
- **E010:** Standard-Fräsung (häufigste)
- **E013:** Bohrungen
- **E015:** CLAMEX-Bearbeitungen
- **E019, E021, E022:** CLAMEX-Sägeprozesse
- **E025:** Spezialbohrungen
- **E032:** Orientierte Bohrungen
- **E110, E150:** Finishing-Operationen (nur in 2_16_1_Revsionsdeckel.xcs)

### Werkzeug-Durchmesser (echte Daten)
- **2.5mm, 3.0mm:** Kleine Bohrungen
- **5.0mm:** Standard-Systembohrungen (Konfekt)
- **6.0mm:** RNT-Nuten (Breite)
- **8.0mm:** Große Systembohrungen
- **15.0mm:** Große Durchgangsbohrungen
- **35.0mm:** Große Aussparungen

### Bohrtiefen-Verteilung
- **13.0mm:** 124 Vorkommen (Standard-Systembohrung)
- **14.0mm:** 38 Vorkommen (Durchgangsbohrung)
- **10.0mm:** 30 Vorkommen (Flache Bohrung)
- **12.0mm:** 20 Vorkommen (CLAMEX-Tiefe)
- **2.0mm, 17.0mm:** Spezielle Tiefen

## 4. Horizontalbohrungen & Seitenbearbeitung

Produktion nutzt `CreateWorkplane()` für komplexe Orientierungen:

```xcs
CreateWorkplane("Freie Ebene_803",0,43,-9.5+DZ,-90.000,90);
SelectWorkplane("Freie Ebene_803");
CreateDrill("Horizontal freie Bohrung_1_L",0 ,0 ,30,8,"",TypeOfProcess.Drilling,"","-1",1,-1,-1,"P",0,0);
```

**Pattern:** `CreateWorkplane(name, x, y, z, rotX, rotY)`
- **Links:** rot = -90.0 
- **Rechts:** rot = +90.0
- **Z-Position:** -9.5+DZ (Mitte der Platte)

## 5. Makros & Spezielle Operationen

### RNT-Nuten (wie implementiert)
```xcs
CreateMacro("Nut in X-Richtung_821","RNT",-70,361.5,5.5,-1,-1,-1,883.5,5,true,"066","-1",false,false,true,361.5,null,null,null,null,true);
```

### CLAMEX-Verbinder (NICHT implementiert!)
```xcs
CreateMacro("CLAMEX Vertikal_1","SawCut_Lamello",9.5,50.03,9.5,50.03,0,2,19,5,null,1,0.05,null,null,null,null,2,"3","E015",null,"3","E004",null,0,0,false,-1,0,null,0,false,"3","E019",null,null,null,null,null,null,null,4,null,null,14.3,null,"3","E032",270);
```

**CLAMEX-Typen gefunden:**
- `CLAMEX Vertikal_X`: Vertikale Verbinder
- `CLAMEX Horizontal_X`: Horizontale Verbinder
- Verschiedene Orientierungen (90°, 270°)

### Pattern-Bohrungen (NICHT implementiert!)
```xcs
CreateDrill("Vertikale Bohrung_1",24,75,14.000,15,"",TypeOfProcess.Drilling,"-1","-1",1,-1,-1,"P");
CreatePattern(1,4,0,64,0,90);  // 4 Bohrugen im 64mm Abstand
ResetPattern();
```

## 6. Bogenverarbeitung

**Gefunden in:** `2_16_1_Revsionsdeckel.xcs`

```xcs
CreatePolyline("Polylinie_2", 451.75, 0);
AddSegmentToPolyline(883.500,0.000);
AddArc2PointCenterToPolyline(903.5, 20, 883.5, 20, false);  // Eckenrundung
AddSegmentToPolyline(903.500,280.000);
AddArc2PointCenterToPolyline(883.5, 300, 883.5, 280, false);
```

**Format:** `AddArc2PointCenterToPolyline(endX, endY, centerX, centerY, clockwise)`

## 7. Vergleich mit unserem XilogEmitter

### ✅ Was unser Emitter richtig macht
- **Grundstruktur:** Header, Workpiece, Setup Position korrekt
- **CreatePolyline/AddSegmentToPolyline:** Identisch zur Produktion
- **CreateDrill:** Format stimmt überein
- **RNT-Makros:** Vollständig implementiert
- **CreateRoughFinish:** Parameter korrekt
- **Approach/Retract Strategies:** Stimmt überein

### ❌ Kritische Unterschiede

#### Header zu simpel
```diff
- // *** Programm created by Rhino→Maestro Generator ***
+ // *** Programm created by CAD+T CAM Interface, Version 24.34.01 ***
+ //**********************************************************
+ // *** Programmparameter setzen ***
+ // *** Bauteil erstellen ***
+ // *** Bauteil Offsets ***
```

#### Fehlende Features
1. **CreatePattern()** - Bohrmuster/Arrays
2. **AddArc2PointCenterToPolyline()** - Eckenrundungen
3. **CreateWorkplane()** - Horizontalbohrungen
4. **SawCut_Lamello-Makros** - CLAMEX-System
5. **Kommentierte CreateMessage** - Projekt-Info

#### Format-Unterschiede
- **Koordinaten:** `2.5` statt `2.5` (Setup-Position)
- **DZ-Format:** `19` statt `19.000`
- **Footer:** Fehlende Kommentar-Blöcke

## 8. Prioritierte Action Items

### 🔥 KRITISCH (Sofort implementieren)
1. **CreatePattern()** Support
   - 122 Vorkommen in Produktion!
   - Pattern: `CreatePattern(xCount, yCount, xSpacing, ySpacing, angle, direction)`
   - Nach CreateDrill, vor ResetPattern()

2. **Header-Kommentare verbessern**
   - Kommentar-Blöcke mit `//***...***`
   - Projekt-Info als auskommentierte CreateMessage
   - Professionellerer Look

### 🟡 WICHTIG (Nächste Iteration)
3. **AddArc2PointCenterToPolyline()** für Bögen
   - 12 Vorkommen in Produktion
   - Für gerundete Ecken in Konturen

4. **CreateWorkplane() für Horizontalbohrungen**
   - 40 freie Ebenen-Definitionen
   - Seitenbearbeitung ermöglichen

### 🔵 ERWÜNSCHT (Backlog)
5. **SawCut_Lamello-Makros** (CLAMEX)
   - Spezielles Verbindersystem
   - Komplexe Parameterliste

6. **CreateWorkplan()** Support
   - Arbeitsebenen-Definition
   - 6 Vorkommen in Produktion

7. **E-Code Expansion**
   - Aktuell nur E010, E015, E032
   - Produktion nutzt: E004, E005, E009, E013, E019, E021, E022, E025

## 9. Konkrete Empfehlungen

### XilogEmitter.cs Erweiterungen

#### 1. EmitHeader() erweitern
```csharp
public string EmitHeader(string programName, double dx, double dy, double dz)
{
    var lines = new List<string>
    {
        "// *** Programm created by CAD+T CAM Interface, Version 24.34.01 ***",
        "//**********************************************************",
        "//**********************************************************",
        "// *** Programmparameter setzen *** ",
        "SetMachiningParameters(\"IJ\",1,10,196608,false);",
        "//**********************************************************",
        "//**********************************************************", 
        "// *** Bauteil erstellen ***",
        F($"CreateFinishedWorkpieceBox(\"{programName}\", {dx}, {dy}, {dz});"),
        "//**********************************************************",
        "//**********************************************************",
        "// *** Bauteil Infos ***",
        $"//CreateMessage(\"Projekt\",\"projekt_name\",false,false);",
        $"//CreateMessage(\"Datei\",\"{programName}.xcs\",false,false);",
        $"//CreateMessage(\"Bemerkung\",\" \",false,false);",
        "//**********************************************************",
        "//**********************************************************",
        F($"double DZ = {dz};"),
        "//AddVariable(\"Entnehmen\",0,0,1,\"\",false,true);",
        "//**********************************************************",
        "//**********************************************************",
        "// *** Bauteil Offsets ***",
        "SetWorkpieceSetupPosition(2.5,2.5,0,0);",
        "//**********************************************************",
        "//**********************************************************",
        ""
    };
    return string.Join("\n", lines);
}
```

#### 2. Pattern-Bohrungen implementieren
```csharp
public string EmitDrillPattern(string baseName, double x, double y, double depth, double dia,
    int xCount, int yCount, double xSpacing, double ySpacing, 
    string plane = "Top", string side = "P")
{
    var lines = new List<string>
    {
        F($"SelectWorkplane(\"{plane}\");"),
        F($"CreateDrill(\"{baseName}\",{x:F3},{y:F3},{depth:F3},{dia:F3},\"\",TypeOfProcess.Drilling,\"-1\",\"-1\",1,-1,-1,\"{side}\");"),
        F($"CreatePattern({xCount},{yCount},0,{ySpacing:F3},0,90);"),
        "ResetPattern();",
        ""
    };
    return string.Join("\n", lines);
}
```

#### 3. Bogen-Support
```csharp
public string EmitPolylineWithArcs(string polyName, IReadOnlyList<PolylineSegment> segments,
    string opName, string tech, double depth, string plane = "Top")
{
    var lines = new List<string>
    {
        F($"SelectWorkplane(\"{plane}\");"),
        F($"CreatePolyline(\"{polyName}\", {segments[0].StartX:F3},{segments[0].StartY:F3});")
    };

    for (int i = 1; i < segments.Count; i++)
    {
        if (segments[i].IsArc)
        {
            lines.Add(F($"AddArc2PointCenterToPolyline({segments[i].EndX:F3}, {segments[i].EndY:F3}, {segments[i].CenterX:F3}, {segments[i].CenterY:F3}, {(segments[i].Clockwise ? "true" : "false")});"));
        }
        else
        {
            lines.Add(F($"AddSegmentToPolyline({segments[i].EndX:F3},{segments[i].EndY:F3});"));
        }
    }
    
    // ... Rest wie EmitPolylinePass
}
```

#### 4. Footer erweitern
```csharp
public string EmitFooter()
{
    return string.Join("\n", new[]
    {
        "//**********************************************************",
        "//**********************************************************",
        "",
        "",
        "",
        "",
        "// Macro RNT", 
        "CreateMacro(\"Wegfahrschritt\", \"XPARK\");",
        "//**********************************************************",
        "//**********************************************************",
        "// *** Programm Ende ***",
        ""
    });
}
```

### LayerParser Erweiterungen

#### Pattern-Detection
```csharp
// In LayerParser: Erkennung von DRILL_PATTERN_X_Y Layern
if (Regex.IsMatch(layerName, @"^DRILL_PATTERN_(\d+)_(\d+)"))
{
    var match = Regex.Match(layerName, @"^DRILL_PATTERN_(\d+)_(\d+)");
    var xCount = int.Parse(match.Groups[1].Value);
    var yCount = int.Parse(match.Groups[2].Value);
    // ... Pattern-Logik
}
```

## 10. Testing & Validation

### Test Cases hinzufügen
1. **Pattern-Bohrungen:** 3x4 Matrix, verschiedene Abstände
2. **Bögen:** Polyline mit AddArc2PointCenterToPolyline
3. **Header-Format:** Vergleich mit Produktions-Output
4. **Horizontale Bohrungen:** CreateWorkplane mit Rotation

### Regressions-Tests
- Bestehende test_01.xcs, test_02.xcs müssen weiterhin funktionieren
- Neue Features optional aktivierbar

## 11. Fazit

Die Produktions-XCS-Dateien zeigen, dass unser XilogEmitter die **Grundfunktionalität** korrekt implementiert, aber **wichtige Features** für professionelle CNC-Programmierung fehlen:

1. **Pattern-Bohrungen** sind essentiell (122 Vorkommen!)
2. **Header-Format** wirkt unprofessionell
3. **Bogenverarbeitung** für Design-Qualität
4. **Horizontalbohrungen** für 3D-Bearbeitung

**Nächste Schritte:**
1. CreatePattern() implementieren (höchste Priorität)
2. Header-Kommentare ausbauen 
3. Arc-Support für Rundungen
4. Horizontalbohrungen via CreateWorkplane()

Mit diesen Erweiterungen erreichen wir **Produktions-Qualität** und **100% Kompatibilität** zum CAD+T System.