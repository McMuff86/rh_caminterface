# Block-Library Spezifikation

**Datum:** 23. März 2026  
**Version:** 1.0  
**Status:** Spezifikation  
**Basiert auf:** CAD+T DWG-Analyse (3 DWGs, 5507 Block-Inserts), 55 XCS-Produktionsdateien

---

## 1. UserText-Schema

### 1.1 Pflicht-Attribute

Jeder CNC-Block **MUSS** mindestens `CNC_Type` haben.

| Key | Datentyp | Pflicht | Beschreibung |
|-----|----------|---------|-------------|
| `CNC_Type` | String (Enum) | ✅ | Bearbeitungstyp: `DRILL`, `DRILLPATTERN`, `MACRO`, `CUT`, `POCKET`, `GROOVE`, `HDRILL` |

### 1.2 Optionale Attribute

| Key | Datentyp | Default | Beschreibung | Relevant für |
|-----|----------|---------|-------------|-------------|
| `CNC_Diameter` | double (mm) | — | Werkzeug-/Bohrdurchmesser | DRILL, DRILLPATTERN, HDRILL |
| `CNC_Depth` | double (mm) | — | Bearbeitungstiefe | Alle |
| `CNC_Side` | String | `TOP` | Bearbeitungsseite: `TOP`, `BOTTOM`, `LEFT`, `RIGHT`, `FRONT`, `BACK` | Alle |
| `CNC_TechCode` | String | `E010` | Technologie-Code (E004–E150) | Alle |
| `CNC_Orientation` | int (°) | `0` | Rotation: 0, 90, 180, 270 | MACRO, GROOVE |
| `CNC_MacroName` | String | — | Makro-Name (z.B. `SawCut_Lamello`, `RNT`, `Rectangle`) | MACRO |
| `CNC_MacroParams` | String | — | Komma-separierte Makro-Parameter (Template mit `{DZ}` Platzhaltern) | MACRO |
| `CNC_PatternX` | int | `1` | Wiederholungen in X | DRILLPATTERN |
| `CNC_PatternY` | int | `1` | Wiederholungen in Y | DRILLPATTERN |
| `CNC_SpacingX` | double (mm) | `0` | Abstand in X zwischen Wiederholungen | DRILLPATTERN |
| `CNC_SpacingY` | double (mm) | `32` | Abstand in Y zwischen Wiederholungen | DRILLPATTERN |
| `CNC_StepDown` | double (mm) | — | Zustellung pro Pass (Fräsen) | CUT, POCKET, GROOVE |
| `CNC_ToolDia` | double (mm) | — | Werkzeugdurchmesser (Fräsen) | CUT, POCKET, GROOVE |
| `CNC_Through` | bool | `false` | Durchgehende Bohrung (Tiefe = Plattendicke) | DRILL, HDRILL |
| `CNC_Description` | String | — | Beschreibung für UI/Tooltip | Alle |

### 1.3 Platzhalter in Attribut-Werten

| Platzhalter | Ersetzt durch | Beispiel |
|------------|---------------|---------|
| `{DZ}` | Plattendicke | `CNC_Depth = {DZ}-2` → Tiefe = Plattendicke minus 2mm |
| `{LPX}` | Plattenlänge | `CNC_MacroParams = ...{LPX}...` |
| `{LPY}` | Plattenbreite | |
| `{X}` | Block-Position X in Plattenkoordinaten | |
| `{Y}` | Block-Position Y in Plattenkoordinaten | |

### 1.4 Validierungsregeln

```
1. CNC_Type MUSS ein gültiger Wert sein
2. CNC_Diameter > 0 wenn angegeben
3. CNC_Depth > 0 wenn angegeben (oder Platzhalter)
4. CNC_Side MUSS ein gültiger Wert sein wenn angegeben
5. CNC_PatternX, CNC_PatternY >= 1 wenn angegeben
6. CNC_SpacingX, CNC_SpacingY >= 0 wenn angegeben
7. CNC_Orientation MUSS 0, 90, 180, oder 270 sein wenn angegeben
8. MACRO-Blöcke MÜSSEN CNC_MacroName haben
```

---

## 2. Block-Naming-Konvention

### 2.1 Grundformat

```
{Kategorie}_{Name}[_{Variante}]
```

| Teil | Beispiele |
|------|-----------|
| Kategorie | `Topfband`, `CLAMEX`, `Lochreihe`, `Duebel`, `Exzenter`, `Sockel`, `Griff` |
| Name | `35`, `P14`, `32`, `8x30`, `15` |
| Variante (optional) | `C_550` (Legrabox C, 550mm), `110_BM1` (110° Bohrmaschine 1) |

### 2.2 Beispiele

```
Topfband_35               → Topfband Ø35mm
CLAMEX_P14                → CLAMEX P-System 14mm
Lochreihe_32              → System 32 Lochreihe
Lochreihe_32_beidseitig   → System 32 beidseitig
Duebel_8x30               → Riffeldübel Ø8 T=30
Exzenter_15               → Exzenter Ø15 + Topf Ø25
Montageverbinder_35       → Montageverbinder Ø35
Puffer_2_5                → Puffer Ø3 Bohrung
Sockelversteller_45       → Sockelversteller 45mm
RNT_Nut_5_5               → Rückwandnut 5.5mm
Legrabox_C_550            → Legrabox C-Profil 550mm (komplett)
TipOn_L3                  → TipOn Einheit L3
```

### 2.3 Regeln

1. **Keine Sonderzeichen** ausser `_` (Unterstrich)
2. **Keine Leerzeichen** — Underscore statt Space
3. **Punkt durch Unterstrich ersetzen:** `2.5` → `2_5`
4. **Max. 31 Zeichen** (Maestro-Limit für Programmnamen, relevant für Dateinamen)
5. **ASCII only** — keine Umlaute (ä→ae, ö→oe, ü→ue)
6. **Case-Sensitive** — Standardmäßig PascalCase für Kategorie, Rest lowercase/Zahlen

---

## 3. Distribution

### 3.1 Empfohlene Strategie: Embedded + Yak

```
Distribution-Hierarchie:
1. EMBEDDED (Starter-Set)     → Im Plugin selbst, immer verfügbar
2. YAK PACKAGE (Erweiterung)  → Separates Yak-Package "RhinoCNC-BlockLibrary"
3. USER LIBRARY (Custom)      → Vom User erstellte Blöcke in ~/AppData/.../RhinoCNCExporter/Blocks/
```

### 3.2 Embedded Blocks (Starter-Set)

**Methode:** .3dm-Dateien als Embedded Resources in der Plugin-Assembly.

```
RhinoCNCExporter/
└── BlockLibrary/
    └── EmbeddedBlocks/
        ├── Topfband_35.3dm
        ├── CLAMEX_P14.3dm
        ├── Lochreihe_32.3dm
        ├── Duebel_8x30.3dm
        ├── Exzenter_15.3dm
        └── Montageverbinder_35.3dm
```

**Vorteil:** Kein separater Download. Plugin installiert → Blöcke verfügbar.  
**Nachteil:** Plugin-Grösse wächst. Updates brauchen Plugin-Update.

### 3.3 Yak Block-Library Package (Erweiterung)

```yaml
# manifest.yml für Block-Library Yak Package
name: rhinocnc-blocklibrary
version: 1.0.0
authors:
  - solid-ai.ai
description: "CNC Block Library for RhinoCNCExporter — 30+ Beschlag-Blöcke"
keywords:
  - cnc
  - blocks
  - fittings
  - furniture
```

**Inhalt:** Zusätzliche .3dm Dateien (Legrabox, TipOn, Konsolen, etc.)  
**Vorteil:** Erweiterbar ohne Plugin-Update.

### 3.4 User Library (Custom Blocks)

```
Windows: %APPDATA%\RhinoCNCExporter\Blocks\
macOS:   ~/Library/Application Support/RhinoCNCExporter/Blocks/
```

**Workflow:** User erstellt Block → Export als .3dm → Ablegen im Blocks-Ordner → Plugin scannt beim Start.

---

## 4. Starter-Set: 6 Blöcke

### 4.1 Topfband_35

**CNC-Typ:** Bohren (Ø35, T=13 + 2× Ø5 Grundplatte)

```
Geometrie:
  - Zylinder Ø35mm, Höhe 13mm (Topf)
  - 2× Zylinder Ø5mm, Höhe 14mm (Grundplatte, Abstand 52mm)

Insertion Point: Mitte des Ø35-Topfes (auf Plattenoberfläche)

UserText:
  CNC_Type        = DRILL
  CNC_Diameter    = 35
  CNC_Depth       = 13
  CNC_Side        = TOP
  CNC_TechCode    = E009
  CNC_Description = Topfband 35mm Bohrung (Blum, Hettich, Grass)

Generierter CNC-Code (XCS):
  SelectWorkplane("Top");
  CreateDrill("Topfband_35_1", {X}, {Y}, 13, 35, "", TypeOfProcess.Drilling, "-1", "-1", 1, -1, -1, "P");

Hinweis: Grundplatte-Bohrungen sind separate Blöcke oder als
  Multi-Operation in MachiningFactory implementiert (Phase 2+).
  Für Starter: nur der Topf selbst.
```

### 4.2 CLAMEX_P14

**CNC-Typ:** Makro (SawCut_Lamello, ~48 Parameter)

```
Geometrie:
  - Vereinfachte CLAMEX-Form (Box ~20×60×9.5mm)
  - Sichtbar als halbtransparentes Solid

Insertion Point: Mitte der CLAMEX-Fräsung (auf Plattenoberfläche)

UserText:
  CNC_Type         = MACRO
  CNC_MacroName    = SawCut_Lamello
  CNC_Side         = TOP
  CNC_Orientation  = 0
  CNC_Depth        = 9.5
  CNC_TechCode     = E015
  CNC_Description  = CLAMEX P14 Verbinder (Lamello)

Generierter CNC-Code (XCS):
  CreateMacro("CLAMEX Vertikal_1","SawCut_Lamello",
    {DZ}-9.5,{Y},{DZ}-9.5,{Y},
    {orientation},2,{DZ},5,null,1,0.05,null,null,null,null,
    2,"3","E015",null,"3","E004",null,0,0,false,-1,0,null,
    0,false,"3","E019",null,null,null,null,null,null,null,
    4,null,null,14.3,null,"3","E032",270);

Orientierung:
  0°   → Vertikal (CLAMEX in Plattenrichtung Y)
  90°  → Horizontal (CLAMEX in Plattenrichtung X)
  180° → Vertikal gespiegelt
  270° → Horizontal gespiegelt
```

### 4.3 Lochreihe_32

**CNC-Typ:** Drill-Pattern (Ø5, T=13, System 32)

```
Geometrie:
  - Reihe aus kleinen Zylindern Ø5mm, Abstand 32mm
  - Standardmäßig 10 Löcher (anpassbar über PatternY)

Insertion Point: Erstes Loch der Reihe (unten)

UserText:
  CNC_Type        = DRILLPATTERN
  CNC_Diameter    = 5
  CNC_Depth       = 13
  CNC_PatternX    = 1
  CNC_PatternY    = 10
  CNC_SpacingX    = 0
  CNC_SpacingY    = 32
  CNC_Side        = TOP
  CNC_TechCode    = E013
  CNC_Description = System 32 Lochreihe (10 Löcher, Abstand 32mm)

Generierter CNC-Code (XCS):
  SelectWorkplane("Top");
  CreateDrill("Lochreihe_32_1", {X}, {Y}, 13, 5, "", TypeOfProcess.Drilling, "-1", "-1", 1, -1, -1, "P");
  CreatePattern(1, 10, 0, 32, 0, 90);
  ResetPattern();
```

### 4.4 Duebel_8x30

**CNC-Typ:** Bohrung + Horizontalbohrung (Ø8, T=10 Fläche + Ø8, T=30 Stirn)

```
Geometrie:
  - Zylinder Ø8mm, Höhe 10mm (Flächenbohrung)
  - Zylinder Ø8mm, Höhe 30mm (Stirnbohrung, horizontal)

Insertion Point: Mitte der Flächenbohrung (auf Plattenoberfläche)

UserText:
  CNC_Type        = DRILL
  CNC_Diameter    = 8
  CNC_Depth       = 10
  CNC_Side        = TOP
  CNC_TechCode    = E013
  CNC_Description = Riffeldübel Ø8×30 (Flächenbohrung)

Hinweis: Die zugehörige Stirnbohrung (Ø8, T=30, horizontal) sitzt 
  auf der ANDEREN Platte als separater Block "Duebel_8x30_Stirn" 
  mit CNC_Type=HDRILL und CNC_Side=LEFT/RIGHT.
```

### 4.5 Exzenter_15

**CNC-Typ:** Multi-Bohrung (Ø15, T=14 + Ø25, T=12 Topf)

```
Geometrie:
  - Zylinder Ø15mm, Höhe 14mm (Exzenterloch, durchgehend)
  - Zylinder Ø25mm, Höhe 12mm (Topf, von unten)

Insertion Point: Mitte Ø15 (auf Plattenoberfläche)

UserText:
  CNC_Type        = DRILL
  CNC_Diameter    = 15
  CNC_Depth       = {DZ}
  CNC_Through     = true
  CNC_Side        = TOP
  CNC_TechCode    = E013
  CNC_Description = Exzenter 15mm durchgehend + Topf Ø25×12 (von unten)

MachiningFactory generiert 2 Operationen:
  1. CreateDrill(..., 15, {DZ}, ...);       // Durchgangsloch Ø15, von oben
  2. SelectWorkplane("Bottom");
     CreateDrill(..., 25, 12, ...);         // Topf Ø25, T=12, von unten
```

### 4.6 Montageverbinder_35

**CNC-Typ:** Bohrung (Ø35, T=14)

```
Geometrie:
  - Zylinder Ø35mm, Höhe 14mm

Insertion Point: Mitte (auf Plattenoberfläche)

UserText:
  CNC_Type        = DRILL
  CNC_Diameter    = 35
  CNC_Depth       = 14
  CNC_Side        = TOP
  CNC_TechCode    = E009
  CNC_Description = Montageverbinder Ø35×14 (z.B. Hettich VB 35/16)

Generierter CNC-Code (XCS):
  SelectWorkplane("Top");
  CreateDrill("MonVerb_35_1", {X}, {Y}, 14, 35, "", TypeOfProcess.Drilling, "-1", "-1", 1, -1, -1, "P");
```

---

## 5. User-erstellte Blöcke

### 5.1 Anleitung: Eigenen Block erstellen

```
Schritt 1: Geometrie zeichnen
  → 3D-Geometrie die den Beschlag visuell darstellt
  → Kann vereinfacht sein (muss nicht perfekt aussehen)
  → Massstab: 1:1 in mm

Schritt 2: Insertion Point setzen
  → Der Insertion Point = wo die CNC-Bearbeitung ansetzt
  → Für Bohrungen: Mitte des Loches
  → Für Makros: Mittelpunkt der Bearbeitung
  → Für Fräsungen: Startpunkt der Kontur

Schritt 3: UserText setzen
  → Objekt auswählen → Properties → UserText
  → Mindestens CNC_Type setzen
  → Weitere CNC_* Attribute nach Bedarf
  → CNC_Description für Dokumentation (optional aber empfohlen)

Schritt 4: Block definieren
  → Alles auswählen → Block → BlockDefinition Name vergeben
  → Name nach Naming-Konvention: {Kategorie}_{Name}

Schritt 5: Testen
  → Block auf Platte platzieren
  → RhinoCNCExporter Export ausführen
  → Kontrolle: CNC-Code korrekt?

Schritt 6: Teilen (optional)
  → File → Export Selected → .3dm
  → Ablegen in User Library Ordner
  → Oder teilen via Yak Package
```

### 5.2 Template-Datei

Wird als `Block_Template.3dm` im Plugin mitgeliefert:
- Enthält ein Beispiel-Block mit allen UserText-Feldern
- Anleitung als Text-Objekt im File
- Leerer Rahmen zum Befüllen mit eigener Geometrie

### 5.3 Validierung

Beim Import eines User-Blocks prüft `BlockLibraryService`:

```
✅ CNC_Type vorhanden und gültig
✅ Insertion Point definiert (nicht Origin)
✅ Geometrie vorhanden (nicht leer)
⚠️ CNC_Diameter fehlt (Warnung bei DRILL)
⚠️ CNC_Depth fehlt (Warnung bei DRILL)
⚠️ CNC_MacroName fehlt (Fehler bei MACRO)
ℹ️ CNC_Description fehlt (Info, kein Fehler)
```

---

## 6. Block-zu-CNC Mapping (MachiningFactory Referenz)

### Übersicht

| CNC_Type | → Machining | → IEmitter Methode |
|----------|------------|-------------------|
| `DRILL` | DrillMachining | EmitDrill |
| `DRILLPATTERN` | DrillPatternMachining | EmitDrillPattern |
| `MACRO` | MacroMachining | (direkte Makro-Ausgabe) |
| `CUT` | RoutingMachining | EmitPolylinePass |
| `POCKET` | PocketMachining | EmitPolylinePass (Offset-Loops) |
| `GROOVE` | GrooveRntMachining | EmitRntX / EmitRntY |
| `HDRILL` | HorizontalDrillMachining | EmitWorkplane + EmitDrill |

### Spezialfälle

**Multi-Operation Blöcke** (MachiningFactory generiert mehrere Machinings):

| Block | Operationen |
|-------|------------|
| Exzenter_15 | 1× DrillMachining (Ø15, Top) + 1× DrillMachining (Ø25, Bottom) |
| Duebel_8x30 | 1× DrillMachining (Ø8, Top) — Stirn als separater Block |
| Topfband_35 (erweitert) | 1× DrillMachining (Ø35) + 2× DrillMachining (Ø5, Grundplatte) |

**Orientierungsabhängige Blöcke:**

| Block | Orientierung | Auswirkung |
|-------|-------------|-----------|
| CLAMEX_P14 | 0° | SawCut_Lamello mit P5=0 (vertikal) |
| CLAMEX_P14 | 90° | SawCut_Lamello mit P5=90 (horizontal) |
| Lochreihe_32 | 0° | Pattern in Y-Richtung |
| Lochreihe_32 | 90° | Pattern in X-Richtung |

---

## 7. Prioritäten (basierend auf DWG-Analyse)

### Häufigkeiten aus 3 CAD+T DWGs (5507 Block-Inserts total)

| Rang | Block-Typ | Inserts | Starter-Set? |
|------|-----------|---------|-------------|
| 1 | Lochreihe (XCEBO*) | 424× | ✅ Lochreihe_32 |
| 2 | CAD+T Intern (AZC*) | 1940× | ❌ (nicht relevant) |
| 3 | Topfband | 20× | ✅ Topfband_35 |
| 4 | CLAMEX | 20× | ✅ CLAMEX_P14 |
| 5 | Riffeldübel | 16× | ✅ Duebel_8x30 |
| 6 | Exzenter | 16× | ✅ Exzenter_15 |
| 7 | Montageverbinder | 5× | ✅ Montageverbinder_35 |
| 8 | Legrabox (Komplett) | ~50× | Phase 2 (Yak) |
| 9 | TipOn | ~28× | Phase 2 (Yak) |
| 10 | Rastbolzen | 16× | Phase 2 (Yak) |

---

*Dieses Dokument ist die Spezifikation für alle Block-Library-Implementierungen.*
