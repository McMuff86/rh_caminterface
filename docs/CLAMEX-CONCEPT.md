# CLAMEX-Konzept für RhinoCNCExporter

**Datum:** 23. März 2026  
**Status:** Konzept-Phase  
**Basiert auf:** 55 XCS-Produktionsdateien (SawCut_Lamello Makros)

## Executive Summary

CLAMEX P-System Verbinder werden in den XCS-Dateien über komplexe `CreateMacro(..., "SawCut_Lamello", ...)` Aufrufe mit ~48 Parametern realisiert. 

**WICHTIG:** Adi hat einen 3D-Block für CLAMEX und will **block-basierte 3D-Workflows** statt 2D-Layer-Konventionen!

## 1. PRIMARY Workflow: 3D-Block-basiert

### Block-Definition
- **Name:** `CLAMEX_P14` (oder ähnlich - exakte Namen von Adi)
- **Geometrie:** 3D-Block mit korrekten Abmessungen
- **Einfügung:** Rhino Insert Block Befehl
- **Position:** Definiert exakte CLAMEX-Position (X, Y, Z)
- **Rotation:** Bestimmt Orientierung (0°, 90°, 180°, 270°)

### Plugin Block-Erkennung
```csharp
// Pseudo-Code für Block-Detection
var clamexBlocks = doc.InstanceDefinitions
    .Where(def => def.Name.StartsWith("CLAMEX_"))
    .SelectMany(def => doc.Objects.OfType<InstanceReferenceObject>()
        .Where(obj => obj.InstanceDefinition == def));

foreach (var block in clamexBlocks)
{
    var name = block.InstanceDefinition.Name;        // "CLAMEX_P14"
    var position = block.InsertionPoint;             // World coordinates
    var rotation = block.Transformation.GetRotation(); // 0°-360°
    var clamexType = ExtractTypeFromName(name);      // "P14", "P15", etc.
    
    // → Generate CreateMacro(..., "SawCut_Lamello", ...) with 48 parameters
}
```

### Block-zu-Makro Mapping
**Block-Eigenschaften → XCS-Makro-Parameter:**
1. **Position:** Block.InsertionPoint → Makro P1, P2 (X, Y)
2. **Z-Koordinate:** Block.InsertionPoint.Z → relative Tiefe in Platte
3. **Rotation:** Block.Transformation → Orientierung (90°, 270°, etc.)
4. **Block-Name:** `CLAMEX_P14` → Bestimmt Typ-spezifische Parameter
5. **Orientierung:** Vertikal/Horizontal aus Z-Position ableiten

**Beispiel-Mapping:**
```csharp
// Block: CLAMEX_P14 bei (50, 60, 9.5) mit 0° Rotation
// → XCS Output:
CreateMacro("CLAMEX Vertikal_1","SawCut_Lamello",50,60.03,50,60.03,0,2,19,5,null,1,0.05,null,null,null,null,2,"3","E015",null,"3","E004",null,0,0,false,-1,0,null,0,false,"3","E019",null,null,null,null,null,null,null,4,null,null,14.3,null,"3","E032",270);
```

## 2. CLAMEX-Makro Parameter-Analyse

### Typische SawCut_Lamello Struktur (48 Parameter)
```xcs
CreateMacro("CLAMEX Horizontal_1","SawCut_Lamello",
  0,60.03,0,60.03,    // P1-4: Position (X1, Y1, X2, Y2)
  90,                 // P5: Orientierung (90°)
  2,19,5,null,1,0.05, // P6-11: Geometrie-Parameter
  null,null,null,null, // P12-15: Reserved?
  -1,"3","E015",null, // P16-19: Werkzeug 1 (E015)
  null,"E005",null,   // P20-22: Werkzeug 2 (E005)
  0,-1,false,-1,0,    // P23-27: Flags & Offsets
  null,0,false,       // P28-30: Mehr Flags
  null,"E022",null,   // P31-33: Werkzeug 3 (E022)
  null,null,null,null,null,null,null, // P34-40: Reserved
  2,null,null,14,     // P41-44: Depth-Parameter
  null,"3","E021",    // P45-47: Werkzeug 4 (E021)
  270,DZ-9.5          // P48-49: Rotation, Z-Offset
);
```

### Parameter-Kategorien
1. **P1-4:** Position (X1,Y1,X2,Y2) - meist identisch
2. **P5:** Orientierung (0°=vertikal, 90°=horizontal)
3. **P6-11:** Geometrie (Typ, Dicke, Durchmesser, etc.)
4. **P16-19, 20-22, 31-33, 45-47:** 4x Werkzeug-Definitionen
5. **P23-30:** Flags und Offsets
6. **P48-49:** Rotation und Z-Tiefe

### CLAMEX-Typen (aus Block-Namen ableiten)
- **P14:** Standard 14mm System
- **P15:** 15mm System
- **P20:** 20mm System für dicke Platten
- **Horizontal vs. Vertikal:** Aus Z-Position + Rotation ableiten

## 3. Block-Implementation Roadmap

### Phase 1: Block-Erkennung
- [ ] CLAMEX-Block-Definitionen von Adi erhalten
- [ ] Block-Detection in LayerParser implementieren
- [ ] Position, Rotation, Typ-Extraktion

### Phase 2: Parameter-Mapping
- [ ] Mapping-Tables für CLAMEX-Typen erstellen
- [ ] Position → XCS-Koordinaten Transformation
- [ ] Orientierung → Parameter-Set Mapping

### Phase 3: Makro-Generation
- [ ] SawCut_Lamello Makro-Emitter implementieren
- [ ] 48-Parameter Template pro CLAMEX-Typ
- [ ] Integration in XilogEmitter

## 4. Vision: 3D-to-CNC Pipeline (Phase 9+)

### Die große Vision: Wie CAD+T arbeitet

**Current Workflow (2D-Layer):**
```
Rhino 2D-Zeichnung → Layer-Analyse → CNC-Programm
```

**Zukunfts-Workflow (3D-Modell):**
```
3D-Korpus zeichnen → 3D-Analyse → Pro Platte: CNC-Programm
```

### 3D-Modell-basierter Workflow

#### Schritt 1: 3D-Korpus zeichnen
```
Küchenschrank in 3D:
├── Seite_links (19mm Solid)
├── Seite_rechts (19mm Solid)  
├── Boden (19mm Solid)
├── Deckel (19mm Solid)
├── Rückwand (10mm Solid)
├── Tablar (19mm Solid)
└── Beschläge als Blöcke:
    ├── CLAMEX_P14 (5x für Verbindungen)
    ├── TOPFBAND_35mm (2x für Türen)
    ├── DÜBEL_5mm (20x für Systemlöcher)
    └── RNT_6mm (4x für Rückwand-Nuten)
```

#### Schritt 2: 3D-Analyse durch Plugin
```csharp
var korpus = Analyze3DModel(doc);

foreach (var platte in korpus.Platten)
{
    var bearbeitungen = new List<Operation>();
    
    // 1. Plattengeometrie analysieren
    var kontur = ExtractOutline(platte.Geometry);
    var dicke = ExtractThickness(platte.Geometry);
    
    // 2. Beschläge finden die diese Platte betreffen
    var clamex = FindClamexForPlate(platte);
    var bohrungen = FindDrillsForPlate(platte);
    var nuten = FindGroovesForPlate(platte);
    
    // 3. Bearbeitungen generieren
    bearbeitungen.Add(new OutlineOperation(kontur));
    bearbeitungen.AddRange(clamex.Select(c => new ClamexOperation(c)));
    bearbeitungen.AddRange(bohrungen.Select(b => new DrillOperation(b)));
    bearbeitungen.AddRange(nuten.Select(n => new GrooveOperation(n)));
    
    // 4. CNC-Programm pro Platte exportieren
    var xcsFile = GenerateXCS(platte.Name, bearbeitungen);
    SaveToFile($"{platte.Name}.xcs", xcsFile);
}
```

#### Schritt 3: Intelligente Bearbeitungsableitung
**Das Plugin analysiert automatisch:**

1. **Plattengeometrie:**
   - Dicke → DZ-Variable
   - Kontur → Aussenkontur mit Brücken
   - Material → Setup-Position, Werkzeuge

2. **CLAMEX-Verbindungen:**
   - Block-Position → Makro-Koordinaten
   - Verbindungsrichtung → Horizontal/Vertikal
   - Nachbarplatten → Orientierung ableiten

3. **Systembohrungen:**
   - Dübel-Blöcke → CreateDrill mit Pattern
   - Topfband → Spezielle Bohrbilder
   - Griffe → Durchgangsbohrungen

4. **Nuten & Fasen:**
   - RNT-Blöcke → RNT-Makros
   - Fase-Geometrie → CreateBladeCut
   - Rundungen → AddArc2PointCenterToPolyline

### Workflow-Beispiel: Küchenschrank

#### Input: 3D-Rhino-Modell
```
Küchenschrank_60cm.3dm:
├── Korpus-Geometrie (6 Platten als Solids)
├── 12x CLAMEX_P14 Blöcke (Verbindungen)
├── 40x DÜBEL_5mm Blöcke (Systemlöcher)
├── 4x RNT_6mm Blöcke (Rückwand-Nuten)
└── 2x TOPFBAND_35mm (Türscharniere)
```

#### Processing: Plugin-Analyse
```
Analyzing 3D Model...
├── Found 6 plates: Seite_links, Seite_rechts, Boden, Deckel, Rückwand, Tablar
├── Found 12 CLAMEX connections
├── Found 40 system holes (5mm)
├── Found 4 RNT grooves (6mm)
└── Found 2 hinge drillings
```

#### Output: Pro Platte eine .xcs
```
Export Results:
├── Seite_links.xcs (Kontur + 6 CLAMEX + 8 Systemlöcher + 1 RNT)
├── Seite_rechts.xcs (Kontur + 6 CLAMEX + 8 Systemlöcher + 1 RNT + Topfband)  
├── Boden.xcs (Kontur + 4 CLAMEX + 10 Systemlöcher)
├── Deckel.xcs (Kontur + 4 CLAMEX + 10 Systemlöcher)
├── Rückwand.xcs (Kontur + 4 RNT-Nuten)
└── Tablar.xcs (Kontur + 4 CLAMEX + 8 Systemlöcher)
```

### Vorteile der 3D-Pipeline

#### Für den Schreiner
- **Natürlicher Workflow:** Wie in CAD+T gewohnt
- **3D-Visualisierung:** Korpus sehen statt abstrakte Layer
- **Automatische Bearbeitungen:** Plugin leitet alles ab
- **Fehler-Vermeidung:** 3D-Kollisionsprüfung möglich

#### Für die Software
- **Präzisere Daten:** 3D-Geometrie statt 2D-Interpretation
- **Intelligente Ableitung:** Verbindungen automatisch erkennbar  
- **Skalierbar:** Von Schublade bis Küche der gleiche Workflow
- **CAD+T-Kompatibel:** Ähnlicher Datenfluss

### Technische Herausforderungen

#### 3D-Geometrie-Analyse
- **Platte-Erkennung:** Solid → Plattendicke, Kontur
- **Verbindungs-Analyse:** Welche CLAMEX verbindet welche Platten?
- **Koordinaten-Transformation:** 3D-Block → 2D-CNC-Koordinaten
- **Kollisionsprüfung:** Beschläge vs. Bearbeitungen

#### Block-System
- **Block-Bibliothek:** Vollständige Beschlag-Library
- **Parameter-Extraktion:** Block-Eigenschaften → Makro-Parameter
- **Orientierungs-Logik:** 3D-Rotation → CNC-Orientierung
- **Varianten-Management:** Verschiedene CLAMEX-Typen

## 5. Implementation Strategy

### Phase 1-3: CLAMEX-Blocks (Sofort)
1. **Block-Detection:** CLAMEX-Blöcke erkennen und analysieren
2. **Parameter-Mapping:** Block → SawCut_Lamello Makro
3. **XCS-Integration:** In bestehenden XilogEmitter einbauen

### Phase 4-6: Erweiterte Blöcke (Q2 2026)
4. **Dübel-Blocks:** DÜBEL_5mm → CreateDrill Pattern
5. **RNT-Blocks:** RNT_6mm → RNT-Makro
6. **Topfband-Blocks:** TOPFBAND_35mm → Spezielle Bohrbilder

### Phase 7-8: 3D-Pipeline Foundation (Q3 2026) 
7. **Platten-Erkennung:** 3D-Solids → Einzelplatten
8. **Geometrie-Analyse:** Solid → Kontur + Dicke
9. **Multi-Plate Export:** Pro Platte eine .xcs

### Phase 9+: Vollständige 3D-Pipeline (2027+)
10. **Intelligente Verbindungsanalyse**
11. **Automatische Bearbeitungsableitung**
12. **CAD+T-Workflow-Parität**

## 6. CAD+T Referenz (Coming Soon)

Adi exportiert ein DWG aus CAD+T nach Google Drive für Workflow-Analyse:
- Wie arbeitet CAD+T in 3D?
- Welche Block-Typen werden verwendet?
- Wie läuft die CNC-Ableitung ab?

**→ Dieses Referenz-Material wird das Konzept verfeinern!**

## 7. Fazit

Der **block-basierte 3D-Workflow** ist viel professioneller als Layer-Konventionen und entspricht Adis gewohntem CAD+T-Workflow. Die Vision einer vollständigen 3D-to-CNC Pipeline ist ambitioniert aber machbar.

**Nächste Schritte:**
1. CLAMEX-Block-Definitionen von Adi erhalten
2. Block-Detection implementieren 
3. SawCut_Lamello Makro-Generation
4. CAD+T-Referenz analysieren
5. Langfristig: 3D-Pipeline entwickeln

**Das ist der Weg zu einem professionellen CNC-Workflow für Schreiner!**