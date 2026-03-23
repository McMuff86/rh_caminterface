# Architektur: 3D-to-CNC Pipeline

**Datum:** 23. März 2026  
**Status:** Architektur-Entscheidung (ADR)  
**Entscheidung:** Radikal einfach. Kein CAD+T-Klon.

---

## Kernprinzip

> **Was du siehst = was gefräst wird.**

Ein 3D-Modell. Keine 2D-Doppel. Keine 500 Layer. Keine kryptischen Prefixe.

---

## 1. Das Problem mit CAD+T

CAD+T macht vieles, aber alles zu kompliziert:

| CAD+T | Unser Ansatz |
|-------|-------------|
| 522 Layer mit `AZC_CADT0001_LA`, `_LB`, `_LG`, `_LH`, `_LI`, `_LK` | **1 Layer pro Platte** |
| `.BES` + `.WRK` + `.WRK3D` pro Beschlag (3 Blöcke!) | **1 Block pro Beschlag** |
| 2D-Ansichten neben 3D-Modell im selben File | **Nur 3D. 2D wird generiert.** |
| Kryptische Block-Namen: `XCEBO402$3500$1200$1400$1200` | **Lesbare Namen: `Topfband_35`, `CLAMEX_P14`** |
| Separate Bauteil-Verwaltung auf eigenen Layern | **Solids/Surfaces = Platten. Fertig.** |

---

## 2. Modell-Struktur

### Layer-Hierarchie

```
Projekt_Name/
├── Korpus_1/
│   ├── Seite_links          ← Solid/Surface = Platte
│   ├── Seite_rechts         ← Solid/Surface = Platte
│   ├── Boden                ← Solid/Surface = Platte
│   ├── Deckel               ← Solid/Surface = Platte
│   ├── Rückwand             ← Solid/Surface = Platte
│   └── Sockel/
│       ├── Sockel_vorne
│       ├── Sockel_hinten
│       └── Sockel_traverse
├── Tür_links/
│   └── Türblatt             ← Solid/Surface = Platte
├── Tür_rechts/
│   └── Türblatt
└── Schublade_1/
    ├── Front
    ├── Seite_links
    ├── Seite_rechts
    ├── Boden
    └── Rückwand
```

**Regeln:**
- Jeder Sublayer mit einer geschlossenen Surface/Solid/Extrusion = eine Platte
- Plattendicke = Dicke des Solids (automatisch erkannt)
- Plattenabmessungen = Bounding Box des Solids
- Keine speziellen "WK_PIECE" Layer mehr nötig — das Solid IST das Werkstück

### Beschläge als Blöcke

Beschläge leben **auf dem Layer der Platte** an der sie sitzen:

```
Korpus_1/
├── Seite_links           ← Platte (Solid)
│   [Topfband_35 Block]   ← Insert auf diesem Layer
│   [Topfband_35 Block]   ← Insert auf diesem Layer
│   [CLAMEX_P14 Block]    ← Insert auf diesem Layer
│   [Lochreihe_32 Block]  ← Insert auf diesem Layer
├── Boden
│   [CLAMEX_P14 Block]    ← Gegenstück zum CLAMEX in der Seite
│   [Riffeldübel_8 Block]
```

**Zuordnung:** Block auf Layer X → Bohrung kommt in CNC-Programm von Platte X.

---

## 3. Block-Bibliothek

### Aufbau eines Beschlag-Blocks

```
Block "Topfband_35":
├── 3D-Geometrie          ← Visuell (Zylinder Ø35, Tiefe 13)
├── Insertion Point        ← = CNC-Bohrposition (Mitte)
└── UserText (Attribute):
    ├── CNC_Type           = "DRILL"
    ├── CNC_Diameter       = "35"
    ├── CNC_Depth          = "13"
    ├── CNC_Side           = "TOP"         (von oben bohren)
    └── CNC_TechCode       = "E009"
```

```
Block "CLAMEX_P14":
├── 3D-Geometrie          ← Visuell (vereinfachte CLAMEX-Form)
├── Insertion Point        ← = CNC-Position
└── UserText (Attribute):
    ├── CNC_Type           = "MACRO"
    ├── CNC_MacroName      = "SawCut_Lamello"
    ├── CNC_MacroParams    = "9.5,{Y},{Z},...,E015,...,E004,...,E019,...,E032,270"
    ├── CNC_Side           = "TOP"
    └── CNC_Orientation    = "0"           (0°, 90°, 180°, 270°)
```

```
Block "Lochreihe_32":
├── 3D-Geometrie          ← Visuell (Reihe kleiner Zylinder)
├── Insertion Point        ← = Startpunkt der Reihe
└── UserText (Attribute):
    ├── CNC_Type           = "DRILLPATTERN"
    ├── CNC_Diameter       = "5"
    ├── CNC_Depth          = "13"
    ├── CNC_PatternX       = "1"
    ├── CNC_PatternY       = "10"         (Anzahl Löcher)
    ├── CNC_SpacingY       = "32"         (System 32)
    ├── CNC_Side           = "TOP"
    └── CNC_TechCode       = "E013"
```

### UserText Konvention (CNC_* Prefix)

| Key | Werte | Beschreibung |
|-----|-------|-------------|
| `CNC_Type` | `DRILL`, `DRILLPATTERN`, `MACRO`, `CUT`, `POCKET`, `GROOVE` | Bearbeitungstyp |
| `CNC_Diameter` | Zahl (mm) | Werkzeug-/Bohrdurchmesser |
| `CNC_Depth` | Zahl (mm) | Bearbeitungstiefe |
| `CNC_Side` | `TOP`, `BOTTOM`, `LEFT`, `RIGHT`, `FRONT`, `BACK` | Bearbeitungsseite |
| `CNC_TechCode` | `E010`, `E013`, etc. | Technologie-Code |
| `CNC_Orientation` | `0`, `90`, `180`, `270` | Rotation (Grad) |
| `CNC_MacroName` | `SawCut_Lamello`, `RNT`, etc. | Makro-Name |
| `CNC_MacroParams` | Komma-separiert | Makro-Parameter (Template mit Platzhaltern) |
| `CNC_PatternX/Y` | Zahl | Muster-Wiederholungen |
| `CNC_SpacingX/Y` | Zahl (mm) | Muster-Abstände |
| `CNC_StepDown` | Zahl (mm) | Zustellung pro Pass |
| `CNC_ToolDia` | Zahl (mm) | Werkzeugdurchmesser (bei Fräsen) |

---

## 4. Export-Pipeline

### Schritt 1: Platte erkennen

```
Für jeden Sublayer:
  → Finde die grösste geschlossene Surface/Solid/Extrusion
  → Das ist die Platte
  → Plattendicke = Solid-Höhe in Z (oder dünnste Dimension)
  → LPX, LPY = Bounding Box in der Plattenebene
  → DZ = Plattendicke
```

### Schritt 2: Bearbeitungen sammeln

```
Auf dem gleichen Layer:
  → Alle Block-Inserts mit CNC_Type UserText = Beschläge
  → Alle geschlossenen Curves (ohne CNC_Type) = Legacy CUT/POCKET (Layer-Konvention)
  → Alle offenen Curves = Legacy DRILLROW/GROOVE

Für Block-Inserts:
  → Position relativ zur Platte berechnen
  → Plattenkoordinaten: (0,0) = Ecke unten-links der Platte
  → Block-Position in Plattenkoordinaten umrechnen
```

### Schritt 3: Koordinaten-Transformation

```
Platte im 3D-Raum:
  → Kann beliebig im Raum liegen (Seite steht aufrecht!)
  → Plugin berechnet Platten-Ebene (Hauptfläche)
  → Transformiert alle Bearbeitungen in Platten-Lokalsystem
  → X/Y = Plattenebene, Z = Tiefe (von oben)

Beispiel:
  Seite_links steht aufrecht (Normalvektor in X-Richtung)
  → Plugin erkennt Plattenebene
  → Dreht alle Koordinaten ins Plattensystem
  → Export: X=Plattenlänge, Y=Plattenhöhe, Z=Tiefe
```

### Schritt 4: Pro Platte exportieren

```
Für jede Platte:
  1. Header (LPX, LPY, DZ, Setup-Offsets)
  2. Bearbeitungen von oben (SelectWorkplane "Top")
  3. Bearbeitungen von unten (SelectWorkplane "Bottom") 
  4. Horizontalbohrungen (CreateWorkplane pro Seite)
  5. Makros (CLAMEX, RNT, etc.)
  6. Footer (XPARK)
  
  → Dateiname: {LayerName}[_N].xcs / .cix / .mpr (bei Namenskollisionen mit Suffix)
```

---

## 5. Workflow: Schreiner-Perspektive

### Neues Projekt starten

1. **Rhino öffnen** → Template "RhinoCNC Korpus" (vordefinierte Layer-Struktur)
2. **Platten zeichnen** als Solids/Extrusions (Seite, Boden, Deckel, Rückwand)
3. **Beschläge setzen** → `Insert` → Block aus Bibliothek wählen → Platzieren
4. **Konturen zeichnen** (optional) → Geschlossene Curves für Ausschnitte auf dem Platten-Layer
5. **Export** → `RhinoCNCExporter` Panel → "Alles exportieren" oder einzelne Platten wählen

### Beschlag platzieren (Detail)

```
1. Command: Insert (oder Toolbar-Button)
2. Block-Browser: 
   ├── Topfband 35mm
   ├── CLAMEX P14  
   ├── Riffeldübel 8x30
   ├── Lochreihe System 32
   └── ...
3. Klick auf Platte → Block wird platziert
4. Rotation falls nötig (90°/180°/270°)
5. Fertig. Position + Rotation = alle CNC-Infos.
```

### Export (Detail)

```
1. RhinoCNCExporter Panel öffnen
2. Plugin scannt: "Ich sehe 7 Platten, 28 Beschläge, 3 Konturen"
3. Maschine wählen: SCM / Biesse / Homag
4. Zugabe X/Y einstellen (Default 2.5mm)
5. "Exportieren" → Pro Platte eine Datei:
   ├── Seite_links.xcs
   ├── Seite_rechts.xcs
   ├── Boden.xcs
   ├── Deckel.xcs
   ├── Rückwand.xcs
   ├── Tür_links.xcs
   └── Tür_rechts.xcs
6. Fertig. Ab an die Maschine.
```

---

## 6. Migration: 2D → 3D

### Phase 1 (jetzt): Layer-Konventionen (2D)
- `CUT_E010_Z19` Layer mit geschlossenen Curves
- `DRILL_D5_Z13` Layer mit Kreisen/Punkten
- Funktioniert, ist getestet, bleibt als Fallback

### Phase 2: Block-basierte Beschläge (2.5D)
- Block-Bibliothek erstellen
- Block-Detection im ExportService
- Noch flach (alles auf Z=0), aber Beschläge als Blöcke
- **Kompatibel mit Phase 1** — beides gleichzeitig möglich

### Phase 3: Volle 3D-Pipeline
- Platten als Solids im Raum
- Automatische Plattenerkennung
- Koordinaten-Transformation (3D → Platten-Lokal)
- Multi-Platte Export
- **Kompatibel mit Phase 1+2**

### Rückwärts-kompatibel!

Jede Phase baut auf der vorherigen auf. Ein User der nur 2D Layer-Konventionen nutzt, kann das weiterhin tun. Ein User der 3D will, bekommt den vollen Workflow. Kein Breaking Change.

---

## 7. Block-Bibliothek: Starter-Set

### Muss (Phase 2)

| Block | CNC_Type | Häufigkeit |
|-------|----------|-----------|
| `Topfband_35` | DRILL (Ø35, T=13) | ⭐⭐⭐⭐⭐ |
| `Lochreihe_32` | DRILLPATTERN (Ø5, T=13, P=32) | ⭐⭐⭐⭐⭐ |
| `Riffeldübel_8` | DRILL (Ø8, T=10) + Stirnbohrung (Ø8, T=30) | ⭐⭐⭐⭐ |
| `CLAMEX_P14` | MACRO (SawCut_Lamello) | ⭐⭐⭐⭐ |
| `Exzenter_15` | DRILL (Ø15, T=14) + Topf (Ø25, T=12) | ⭐⭐⭐⭐ |
| `Montageverbinder_35` | DRILL (Ø35, T=14) | ⭐⭐⭐ |

### Soll (Phase 2-3)

| Block | CNC_Type |
|-------|----------|
| `Tablar_Träger_D5` | DRILL (Ø5, T=10) |
| `Puffer_2.5` | DRILL (Ø3, T=6) |
| `Sockelversteller_45` | DRILL (Ø35 durchgehend) |
| `Dübel_8_Stirn` | H-DRILL (Ø8, T=30, seitlich) |
| `Rückwandnut_5.5` | GROOVE (RNT, W=5.5, T=8) |
| `Griffprofil_Fräsung` | CUT (Kontur) |

### Nice-to-have (Phase 3+)

| Block | CNC_Type |
|-------|----------|
| `Schloss_Einfräsung` | POCKET |
| `Scharnier_Oberlicht` | DRILL + CUT |
| `Kabelführung` | POCKET |
| `LED_Einlassung` | POCKET |
| `Griffmuschel` | POCKET (Freiform) |

---

## 8. Vergleich

| | CAD+T | Unser Plugin |
|---|-------|-------------|
| **Layer** | 522 | 10-30 (je nach Korpus-Grösse) |
| **Blöcke pro Beschlag** | 3 (.BES + .WRK + .WRK3D) | 1 (3D mit UserText) |
| **Lernkurve** | Wochen | Stunden |
| **Preis** | 5'000-15'000 CHF | Bruchteil davon |
| **2D nötig?** | Ja (2D-Werkstattzeichnungen) | Nein (3D-Viewport = Kontrolle) |
| **Maschinen** | Nur SCM via Schnittstelle | SCM + Biesse + Homag |
| **Plattform** | Eigenes Programm (AutoCAD-basiert) | Rhino 8 (bekannt, mächtig) |
| **Erweiterbar** | Nur durch CAD+T AG | Open Source Block-Library |

---

## 9. Anti-Patterns (was wir NICHT machen)

1. ❌ **Keine 2D/3D Parallel-Blöcke** — Ein Block, eine Darstellung
2. ❌ **Keine Layer-Explosion** — Maximal 2 Hierarchie-Ebenen (Korpus/Platte)
3. ❌ **Keine kryptischen Prefix-Systeme** — Lesbare Namen, immer
4. ❌ **Keine versteckte Logik in Layer-Namen** — CNC-Infos im Block (UserText), nicht im Layer
5. ❌ **Keine Pflicht-Layer** — Plugin erkennt Platten automatisch an Geometrie-Typ
6. ❌ **Kein Zwang zu 3D** — 2D Layer-Konventionen bleiben als Fallback (Phase 1)
7. ❌ **Keine eigene Block-Format-Erfindung** — Standard Rhino Blocks + Standard UserText

---

## 10. Bearbeitungs-Ansatz: Plugin-Commands + Face-Tagging

> **Entscheidung 23.03.2026:** Bearbeitungen werden über Plugin-Commands erstellt,
> die gleichzeitig die Geometrie UND die CNC-Metadaten setzen.
>
> **Implementierungsstatus 23.03.2026:** Noch nicht im produktiven Code.
> Aktuell läuft der Export weiterhin über die bestehende Layer-/Block-Pipeline.

### Konzept: Boolean + Tag in einem Schritt

Statt separate Beschlag-Blöcke oder reine Feature-Erkennung: **Das Plugin bietet
eigene Commands die die Bearbeitung direkt ins Solid einbauen und taggen.**

```
User: "AddDrill" → Klick auf Platte → Ø5, Tiefe 13
Plugin:
  1. Erstellt Zylinder (Ø5, H=13) an Klick-Position
  2. Boolean-Differenz → Loch ist sichtbar im Solid
  3. Taggt entstandene Faces: CNC_Type=DRILL, CNC_Diameter=5, CNC_Depth=13
  4. Fertig. Loch IST Teil der Platte. Bewegt sich mit.
```

### Plugin-Commands für Bearbeitungen

| Command | Was passiert | Face-Tags |
|---------|-------------|-----------|
| `AddDrill` | Zylinder-Boolean → rundes Loch | CNC_Type=DRILL, Ø, Tiefe |
| `AddDrillRow` | Mehrere Zylinder-Booleans → Lochreihe | CNC_Type=DRILLPATTERN, Ø, Tiefe, Raster, Anzahl |
| `AddPocket` | Box-Boolean → rechteckige Tasche | CNC_Type=POCKET, L, B, Tiefe |
| `AddGroove` | Extrudierte Nut-Boolean | CNC_Type=GROOVE, Breite, Tiefe, Richtung |
| `AddClamex` | CLAMEX-Form-Boolean (aus 3D-Block) | CNC_Type=MACRO, MacroName, Orientierung |
| `AddCut` | Kontur als Durchschnitt | CNC_Type=CUT, Tiefe=DZ |

### Warum Face-Tags statt separate Objekte?

1. **Bewegt sich mit** — Tags sind Teil des Brep, kein separates Objekt das sich lösen kann
2. **Sichtbar** — Die Bearbeitung ist die echte Geometrie, keine unsichtbare Annotation
3. **Standard Rhino** — UserText an Brep-Faces ist eine offizielle Rhino-API
4. **Boolean = Wahrheit** — Was du siehst = was gefräst wird, keine Diskrepanz möglich
5. **Undo-kompatibel** — Rhino Undo macht Boolean + Tag gleichzeitig rückgängig

### Feature-Erkennung als Fallback

Wenn jemand Löcher manuell per Boolean-Differenz macht (ohne Plugin-Commands):
- Plugin analysiert zylindrische/planare Faces
- Boden-Face-Area → Durchmesser (A = π × r²)
- Wand-Face-Höhe → Tiefe
- Muster-Erkennung: Gleichmässiges Raster → Lochreihe
- **Best-Effort** — funktioniert für einfache Bohrungen, nicht für CLAMEX

**Proof of Concept (23.03.2026):** 6 Bohrungen Ø5 T=13 im 32mm Raster erfolgreich
aus Boolean-Solid erkannt (SumSurface Faces, BoundingBox + Area Analyse).

### Workflow-Vergleich

```
MIT Plugin-Commands (empfohlen):
  Platte zeichnen → AddDrill/AddPocket/AddClamex → Export
  ✅ 100% sicher, alle Metadaten, schnell

OHNE Plugin (Fallback):
  Platte zeichnen → Boolean-Differenzen von Hand → Export
  ⚠️ Feature-Erkennung versucht Löcher zu identifizieren
  ⚠️ Kein Wissen über Beschlag-Typ (Dübel vs. Topfband)
  ⚠️ CLAMEX nicht erkennbar

HYBRID (beides):
  Plugin-Commands für Beschläge + manuelle Booleans für Freiform
  ✅ Flexibel, getaggte Features = sicher, ungetaggte = Best-Effort
```

## 11. Offene Fragen

1. **Gegenstück-Erkennung:** CLAMEX in Seite + CLAMEX in Boden — automatisch paaren?
2. **Plattenebene-Erkennung:** Immer grösste Fläche? Was bei L-förmigen Platten?
3. **Material-Info:** Plattendicke aus Solid, aber Material/Dekor woher?
4. **Stückliste:** Automatische BOM-Generierung aus dem Modell?
5. **Nesting-Anbindung:** Pro Platte eine Datei, Nesting separat?
6. **Face-Tag Persistenz:** Bleiben Tags nach weiteren Boolean-Operationen erhalten?
7. **AddDrill UI:** Command-Line Eingabe oder Panel mit Presets?

---

*Dieses Dokument ist die Architektur-Grundlage. Änderungen hier = Änderungen am Fundament.*
*Erstellt: 23.03.2026 | Autor: Sentinel + Adi*
