# Interaktive CAM-Funktionen — Benutzerhandbuch

**RhinoCNCExporter** — Interaktives CAM-System für Rhino 8  
**Version:** März 2026  
**Sprache:** Deutsch

---

## Inhaltsverzeichnis

1. [Übersicht](#1-übersicht)
2. [Schnellstart](#2-schnellstart)
3. [Befehle (Commands)](#3-befehle-commands)
4. [CNC Operations Panel](#4-cnc-operations-panel)
5. [Maschinenprofile](#5-maschinenprofile)
6. [Werkzeugbibliothek](#6-werkzeugbibliothek)
7. [Export-Workflow](#7-export-workflow)
8. [Tipps & Tricks](#8-tipps--tricks)

---

## 1. Übersicht

### Was ist das interaktive CAM-System?

Das interaktive CAM-System erweitert den RhinoCNCExporter um die Möglichkeit, CNC-Bearbeitungen **direkt auf Geometrie** in Rhino zu definieren — per Mausklick auf Kurven, Kanten und Punkte. Es ergänzt den bestehenden **Layer-basierten Export** (CUT_E010, POCKET_E020, DRILL_D5 etc.) um einen modernen, visuellen Workflow.

### Zwei Workflows — ein Plugin

| | Layer-basierter Export | Interaktives CAM |
|---|---|---|
| **Prinzip** | Layernamen definieren Operationen | Klick auf Geometrie → Dialog → Parameter |
| **Datenhaltung** | Layername-Konventionen | UserText auf Rhino-Objekten |
| **Visualisierung** | Keine (nur Layer-Farben) | Werkzeugbahn-Vorschau (2D + 3D) |
| **Panel** | ExportPanel (Dockbar) | CNC Operations Panel (Dockbar) |
| **Stärke** | Batch-Export vieler Platten | Einzelstück-Bearbeitung, Prototypen |
| **Export** | XCS / CIX direkt | Vorschau → Validierung → Export |

Beide Systeme koexistieren. Das ExportPanel (Layer-basiert) und das CNC Operations Panel (interaktiv) können gleichzeitig geöffnet sein.

### Unterstützte Bearbeitungen

- **Kontur** 🔴 — Aussenfräsung entlang einer Kurve (Schlichten/Schruppen)
- **Tasche** 🔵 — Geschlossene Fläche ausfräsen (konzentrische Bahnen)
- **Bohrung** 🟡 — Punkt- oder Kreisbohrung (optional Pickelbohren)
- **Nut** 🟢 — Nutfräsung entlang einer Kurve

---

## 2. Schnellstart

### Beispiel: Kontur an einer Plattenkante fräsen

**Voraussetzung:** Eine Platte (Brep/Extrusion) ist im Modell vorhanden.

1. **Panel öffnen:** Tippe `CNCPanel` in die Rhino-Befehlszeile → Das CNC Operations Panel dockt an

2. **Maschine wählen:** Im Panel oben das Maschinenprofil auswählen:
   - SCM (Xilog) — für `.xcs`-Dateien
   - Biesse (CIX) — für `.cix`-Dateien
   - MaestroCadT

3. **Kontur hinzufügen:** 
   - Tippe `CNCAddContour` oder klicke `+ Kontur` im Panel
   - Wähle eine oder mehrere Kanten der Platte (Brep-Edges funktionieren!)
   - Enter drücken → Konturdialog erscheint

4. **Parameter einstellen:**
   - **Werkzeug** auswählen (z.B. "Ø10.0 HM Router")
   - **Tiefe** eingeben (z.B. 19mm für Durchschnitt)
   - **Strategie:** Schlichten / Schruppen / Beides
   - OK klicken

5. **Ergebnis prüfen:**
   - Die Kante wird rot eingefärbt (Kontur-Farbe)
   - Werkzeugbahn-Vorschau erscheint auf dem Layer `CNC_Toolpaths::Contour`
   - Im Panel erscheint die Operation in der Liste

6. **Exportieren:**
   - `📤 Export CNC` im Panel klicken
   - Validierung läuft automatisch
   - Vorschau-Dialog zeigt den generierten CNC-Code
   - `Exportieren` klicken → Datei speichern

---

## 3. Befehle (Commands)

Alle Befehle in der Rhino-Befehlszeile eingeben oder über die Buttons im CNC Operations Panel starten.

### CNCAddContour

**Konturfräsung hinzufügen**

- **Eingabe:** Kurven oder Brep-Kanten auswählen
- **Dialog:** Werkzeug, Tiefe, Strategie (Schlichten/Schruppen/Beides)
- **Visualisierung:** Links/rechts-Offset (zeigt Werkzeugbreite) + Richtungspfeile
- **Farbe:** 🔴 Rot

```
Befehl: CNCAddContour
> Kurven oder Kanten auswählen (Enter wenn fertig)
> [Dialog: Werkzeug, Tiefe, Strategie]
> Werkzeugbahn wird erzeugt ✅
```

### CNCAddPocket

**Taschenfräsung hinzufügen**

- **Eingabe:** Geschlossene Kurven auswählen
- **Dialog:** Werkzeug, Tiefe, Zustellung (%), Eintauchstrategie
- **Visualisierung:** Konzentrische Offsetbahnen + Einstiegspunkt-Markierung
- **Farbe:** 🔵 Blau
- **Hinweis:** Die Kurve **muss geschlossen** sein — offene Kurven werden abgelehnt

### CNCAddDrill

**Bohrung hinzufügen**

- **Eingabe:** Punkte anklicken oder bestehende Punkte/Kreise auswählen
- **Dialog:** Werkzeug, Tiefe, Durchmesser, Pickelbohren (Ja/Nein), Pickeltiefe
- **Visualisierung:** Kreis am Bohrpunkt + Fadenkreuz
- **Farbe:** 🟡 Gelb

### CNCAddGroove

**Nutfräsung hinzufügen**

- **Eingabe:** Kurven oder Brep-Kanten auswählen
- **Dialog:** Werkzeug, Tiefe, Breite, Strategie
- **Visualisierung:** Links/rechts-Offset + Richtungspfeile (wie Kontur)
- **Farbe:** 🟢 Grün

### CNCRemoveOperation

**Bearbeitung entfernen**

- **Eingabe:** Objekte mit CNC-Operationen auswählen
- **Wirkung:** Entfernt UserText, Werkzeugbahn-Geometrie, stellt Originalfarbe wieder her
- **Bei Brep-Kanten:** Die extrahierte Kantenkurve wird komplett gelöscht

### CNCPanel

**CNC Operations Panel öffnen/schliessen**

- Wechselt die Sichtbarkeit des dockbaren Panels
- Das Panel kann wie jedes Rhino-Panel angedockt, schwebend oder als Tab verwendet werden

### CNCListOperations

**Alle Operationen auflisten**

- Gibt alle CNC-Operationen im Dokument in der Rhino-Ausgabe aus
- Zeigt: Objektname, Operationstyp, Werkzeug, Tiefe

---

## 4. CNC Operations Panel

Das Panel ist das Herzstück des interaktiven CAM-Systems. Öffnen mit `CNCPanel`.

### Aufbau (von oben nach unten)

#### 🔧 Kopfzeile
- Titel: "CNC Operations"
- Maschinen-Dropdown (siehe [Maschinenprofile](#5-maschinenprofile))

#### ➕ Schnellzugriff-Leiste
Vier Buttons zum schnellen Hinzufügen:
- `+ Kontur` → führt `CNCAddContour` aus
- `+ Tasche` → führt `CNCAddPocket` aus
- `+ Bohrung` → führt `CNCAddDrill` aus
- `+ Nut` → führt `CNCAddGroove` aus

#### 📋 Operationsliste (TreeGridView)
Zeigt alle CNC-Operationen im aktuellen Dokument:
- **Spalten:** Operation (mit Farb-Emoji), Werkzeug (Ø + Name), Tiefe
- **Klick** → Objekt im Viewport selektieren
- **Doppelklick** → Zoom auf Objekt
- **Rechtsklick** → Kontextmenü:
  - ✏️ Bearbeiten… — Dialog mit aktuellen Werten öffnen
  - ⏸ Deaktivieren / ▶ Aktivieren — Operation temporär ein-/ausschalten
  - 🗑 Entfernen — Operation löschen
  - 🔄 Toolpath neu generieren — Werkzeugbahn erneuern
  - 🎯 Im Viewport selektieren
  - 🔍 Zoom auf Objekt

#### 🔧 Eigenschaften
Wenn eine Operation ausgewählt ist, werden die Parameter angezeigt und können bearbeitet werden:
- Werkzeug-Dropdown (gefiltert nach Operationstyp)
- Checkbox `Operation aktiviert` für temporäres Ein-/Ausschalten
- Tiefe, Vorschub, und typspezifische Parameter
- `Anwenden` speichert Änderungen + regeneriert Werkzeugbahn

#### 🎛 Standardwerte
Vordefinierte Werte pro Operationstyp bearbeiten:
- Operationstyp-Dropdown wählen
- Werte ändern (Tiefe, Vorschub, Strategie, etc.)
- `💾 Speichern` — speichert im Dokument
- `↩ Zurücksetzen` — lädt Maschinenprofi-Standards

#### Aktions-Buttons
- `Alle generieren` — Werkzeugbahnen für alle Operationen erzeugen
- `Alle löschen` — Werkzeugbahn-Geometrie entfernen
- `Aktualisieren` — Operationsliste neu laden
- `🧹 Bereinigen` — Verwaiste Kantenkurven aufräumen
- `▶ Simulation` / `⏹ Stopp` — Werkzeugbahn-Animation starten/stoppen
- Geschwindigkeits-Dropdown: 1×, 2×, 5×, 10×

#### ✔ Validierung & Export
- `✔ Validieren` — Prüft alle Operationen auf Fehler
- `📤 Export CNC` — Validierung → Vorschau → Export

#### 📊 Statusleiste
Zeigt: Anzahl Operationen, Werkzeuge, Warnungen, aktives Maschinenprofil

---

## 5. Maschinenprofile

### Verfügbare Profile

| Profil | Format | Dateiendung | Typischer Einsatz |
|--------|--------|-------------|-------------------|
| SCM (Xilog) | XCS | `.xcs` | SCM-Maschinen mit Xilog-Steuerung |
| Biesse (CIX) | CIX | `.cix` | Biesse-Maschinen mit bSolid/BiesseWorks |
| MaestroCadT | XCS | `.xcs` | CAD+T Maestro-Integration |

### Profil wechseln

1. Im CNC Operations Panel den Maschinen-Dropdown oben ändern
2. Die Werkzeugbibliothek wird automatisch für das neue Profil geladen
3. Standardwerte passen sich an das Profil an (z.B. andere Vorschübe)
4. Das Profil wird im Dokument gespeichert (persistent über Sessions)

### Was ändert sich beim Profilwechsel?

- **Werkzeugbibliothek** — jedes Profil hat eigene Standard-Werkzeuge
- **Standardwerte** — Vorschub, Tiefe, Zustellung etc. sind profilspezifisch
- **Export-Format** — SCM → XCS, Biesse → CIX
- **Bestehende Operationen** bleiben unverändert (Werkzeugzuweisung wird nicht automatisch geändert)

---

## 6. Werkzeugbibliothek

### Werkzeuge verwalten

Die Werkzeugbibliothek wird pro Maschinenprofil gespeichert (JSON-Dateien).

**Werkzeugmanager öffnen:**
- Im Panel bei jedem Werkzeug-Dropdown die letzte Option "Werkzeuge verwalten…" wählen
- Oder direkt die Datei bearbeiten (fortgeschritten)

### Werkzeugeigenschaften

| Eigenschaft | Beschreibung |
|-------------|--------------|
| Name | Anzeigename (z.B. "HM Router 3-Schneider") |
| Durchmesser | Werkzeugdurchmesser in mm |
| Art | Router / Drill / Saw (bestimmt Kompatibilität) |
| Technologie-Code | Profilspezifischer Code (z.B. "E010" für SCM) |
| Vorschub | Standard-Vorschub in mm/min |
| Drehzahl | Spindeldrehzahl in U/min |
| Schneidenanzahl | Anzahl Schneiden |

### Werkzeugarten und Kompatibilität

| Werkzeugart | Kontur | Tasche | Bohrung | Nut |
|-------------|--------|--------|---------|-----|
| Router | ✅ | ✅ | ❌ | ✅ |
| Drill | ❌ | ❌ | ✅ | ❌ |
| Saw | ❌ | ❌ | ❌ | ✅ |

Die Werkzeug-Dropdowns in den Dialogen und im Panel filtern automatisch nach kompatiblen Werkzeugen.

### Standard-Werkzeuge

Jedes Profil kommt mit vordefinierten Werkzeugen. Beispiel SCM (Xilog):
- Ø6 HM Schaftfräser (Nuten, feine Konturen)
- Ø8 HM Router (Standardfräser)
- Ø10 HM Router 3-Schneider (Schwer-Zerspanung)
- Ø12 HM Router (grosse Konturen)
- Ø5 Bohrer (Standardbohrung)
- Ø8 Bohrer (Dübelloch)
- Ø35 Topfbohrer (Scharnierlöcher)

---

## 7. Export-Workflow

### Der vollständige Export-Ablauf

```
1. Operationen definieren (CNCAdd*-Befehle oder Panel-Buttons)
       ↓
2. Werkzeugbahnen prüfen (visuelle Kontrolle im Viewport)
       ↓
3. ✔ Validierung (automatisch oder manuell)
   ├── ❌ Fehler → Objekte werden markiert, Export blockiert
   └── ⚠ Warnungen → Export möglich, aber prüfen!
       ↓
4. 📤 Export CNC klicken
       ↓
5. Vorschau-Dialog
   ├── Platten-Liste links (mit Abmessungen)
   ├── CNC-Code rechts (Syntax-Highlighting)
   ├── 📋 Kopieren — Code in Zwischenablage
   ├── 📂 In Datei öffnen — temporäre Datei im Editor
   └── 📤 Exportieren — Datei speichern
       ↓
6. Datei speichern
   ├── Einzelne Platte → Datei-Dialog
   └── Mehrere Platten → Ordner-Dialog (eine Datei pro Platte)
```

### Validierungsprüfungen

| Prüfung | Schwere | Beschreibung |
|---------|---------|--------------|
| Kein Werkzeug zugewiesen | ❌ Fehler | Operation hat kein Werkzeug |
| Werkzeug nicht in Bibliothek | ⚠ Warnung | Werkzeugname nicht gefunden |
| Tiefe > Materialstärke | ⚠ Warnung | Frästiefe grösser als Platte |
| Tasche kleiner als Werkzeug | ❌ Fehler | Werkzeug passt nicht in die Tasche |
| Offene Kurve bei Tasche | ⚠ Warnung | Operation wird lokal übersprungen, bis die Kontur geschlossen ist |
| Vorschub nicht gesetzt | ⚠ Warnung | Standard wird verwendet |
| Verwaiste Kantenkurve | ⚠ Warnung | Quell-Brep wurde gelöscht |
| Doppelte Operation | ⚠ Warnung | Gleicher Typ zweimal auf demselben Objekt |

### Export-Vorschau

Der Vorschau-Dialog zeigt den generierten CNC-Code **vor dem Speichern**:
- **Syntax-Highlighting:** Kommentare (grün), G-Codes (blau), M-Codes (orange)
- **Zeilennummern** für einfache Referenzierung
- **Kopieren** in die Zwischenablage mit einem Klick
- **In Datei öffnen** — schreibt den Code in eine temporäre `.nc`-Datei und öffnet sie im Standard-Editor

### Plattengruppierung

Operationen werden automatisch nach Platten gruppiert:
- **Brep-Kanten:** Alle Operationen auf Kanten desselben Breps → eine Platte
- **Layer-Gruppierung:** Operationen auf dem gleichen Layer → eine Platte (Fallback)
- **Plattenabmessungen:** Automatisch aus Brep-BoundingBox, oder 1000×600×19mm Standard

---

## 8. Tipps & Tricks

### Brep-Kanten bearbeiten

Das interaktive CAM-System unterstützt die direkte Auswahl von **Brep-Kanten** (Edges):
- Wenn eine Kante ausgewählt wird, wird sie als eigenständige Kurve extrahiert
- Die extrahierte Kurve liegt auf dem Layer `CNC_EdgeCurves`
- Die Verbindung zum Quell-Brep wird gespeichert
- Kontur- und Nut-Operationen hängen damit an der extrahierten Kante statt am ganzen Brep
- Wenn der Quell-Brep gelöscht wird, können verwaiste Kurven mit `🧹 Bereinigen` aufgeräumt werden

**Vorteil:** Mehrere verschiedene Operationen auf verschiedenen Kanten desselben Breps sind möglich!

### 3D-Werkzeugbahn-Vorschau

- Im Panel die Checkbox `3D Toolpath-Vorschau` aktivieren
- Zeigt die Werkzeugbahnen mit Tiefendarstellung:
  - Obere Konturen an der Oberfläche
  - Untere Konturen in der Frästiefe
  - Vertikale Verbindungslinien
- Hilfreich um Kollisionen und Tiefenverhältnisse zu prüfen

### Simulation

- `▶ Simulation` zeigt eine Animation des Werkzeugs entlang der Werkzeugbahnen
- Deaktivierte Operationen werden automatisch übersprungen
- Ungültige Einzeloperationen blockieren die übrige Vorschau nicht mehr
- Geschwindigkeit einstellbar: 1×, 2×, 5×, 10×
- Zeigt: Werkzeugumriss (Kreis), Richtungspfeil, Fadenkreuz
- Farbcodiert nach Operationstyp

### Tastenkürzel

| Taste | Aktion |
|-------|--------|
| `Delete` / `Backspace` | Ausgewählte Operation entfernen |
| `F5` | Operationsliste aktualisieren |

### Undo / Rückgängig

Alle CAM-Operationen sind in Rhinos Undo-System integriert:
- `Ctrl+Z` macht die letzte Operation rückgängig
- Gilt für: Hinzufügen, Entfernen, Bearbeiten von Operationen
- Werkzeugbahn-Geometrie wird ebenfalls rückgängig gemacht

### Workflow-Empfehlungen

1. **Erst modellieren, dann bearbeiten** — Geometrie fertigstellen bevor CAM-Operationen definiert werden
2. **Maschinenprofil zuerst wählen** — damit die richtigen Werkzeuge und Standards geladen werden
3. **Validierung nutzen** — vor jedem Export die Validierung laufen lassen
4. **Vorschau prüfen** — den generierten CNC-Code im Vorschau-Dialog kontrollieren
5. **3D-Vorschau für Tiefenkontrolle** — bei Durchbrüchen und tiefen Taschen die 3D-Vorschau aktivieren

### Bekannte Einschränkungen

- **Kein Rhino-CAM-Ersatz:** Das System ist für einfache 2.5D-Bearbeitungen auf Platten ausgelegt, nicht für komplexe 3D-Fräsungen
- **Keine Werkzeugwechsel-Optimierung:** Operationen werden in der Reihenfolge der Definition exportiert
- **Horizontalbohrungen** nur über den Layer-basierten Workflow (HDRILL)
- **Build nur auf Windows:** Das Plugin benötigt RhinoCommon (Windows)

---

*Letzte Aktualisierung: 27. März 2026*
