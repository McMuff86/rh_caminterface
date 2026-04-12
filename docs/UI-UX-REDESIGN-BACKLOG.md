# UI/UX Redesign Backlog — RhinoCNCExporter

**Datum:** 2026-04-12  
**Status:** vorgeschlagen, Start der Umsetzung läuft  
**Ziel:** Das Plugin von einer Button-Sammlung zu einem klaren CNC-Workflow umbauen.

---

## Leitprinzip

Das UI soll einer eindeutigen Produktionslogik folgen:

**Geometrie -> Feature -> Bearbeitung -> Toolpath -> Validierung -> Export**

Nicht mehr:
- irgendwo Defaults
- irgendwo Preview
- irgendwo Export
- und Commands nur, wenn man sie kennt

---

## Produktziel

Ein Anwender soll jederzeit beantworten können:
1. Was wurde erkannt?
2. Was ist noch nicht bearbeitet?
3. Welche Bearbeitung ist diesem Feature zugewiesen?
4. Wie sieht die Werkzeugbahn aus?
5. Kann exportiert werden?

---

## Phase A — Informationsarchitektur

### A1. Hauptscreen neu schneiden
- [ ] ExportPanel und interaktive CAM-Logik konzeptionell zu einem Hauptworkflow zusammenführen
- [ ] globale Hauptaktionen definieren: `Scannen`, `Bearbeitung zuweisen`, `Vorschau`, `Exportieren`
- [ ] Defaults und Werkzeugverwaltung aus dem Primärworkflow herauslösen

### A2. Hauptbereiche definieren
- [ ] **Setup**: Maschine, Material/Dicke, Dokument scannen
- [ ] **Features & Bearbeitungen**: zentrale Tabelle
- [ ] **Vorschau & Validierung**: Toolpath, 2D/3D, Simulation, Fehler
- [ ] **Export**: Ziel, Dateiformat, Exportstatus

---

## Phase B — Feature-zentrierter Workflow

### B1. Feature-Modell einführen
- [ ] internes `Feature`-Modell definieren
- [ ] erste Featuretypen:
  - [ ] Kreisloch
  - [ ] Punktloch
  - [ ] geschlossene Innenkontur
  - [ ] Aussenkontur
  - [ ] Nut-Kurve
- [ ] Statusmodell einführen:
  - [ ] `unassigned`
  - [ ] `assigned`
  - [ ] `previewed`
  - [ ] `invalid`
  - [ ] `export-ready`

### B2. Feature-Tabelle bauen
- [ ] zentrale Tabelle mit Spalten:
  - [ ] Status
  - [ ] Platte
  - [ ] Featuretyp
  - [ ] Geometrie
  - [ ] Bearbeitung
  - [ ] Werkzeug
  - [ ] Tiefe
  - [ ] Vorschau
- [ ] Filter für unzugewiesen / Fehler / ausgewählte Platte

### B3. Rechte Eigenschaftenleiste
- [ ] Feature-Info anzeigen
- [ ] Bearbeitung zuweisen:
  - [ ] Ignore
  - [ ] Drill
  - [ ] Circular Pocket
  - [ ] Inner Contour
  - [ ] Groove
- [ ] Parameter bearbeiten: Werkzeug, Tiefe, Strategie, Zustellung, Ramp/Entry
- [ ] `Auf ähnliche anwenden`

---

## Phase C — Toolpath-Kanonik

### C1. Eine Quelle für Preview, 3D Preview und Simulation
- [ ] `ToolpathPlanner` als kanonische Planquelle definieren
- [ ] `ToolpathAnimator` auf `ToolpathPlan` statt Quellgeometrie umstellen
- [ ] `ToolpathPreviewService` und `ToolpathVisualizer` harmonisieren

### C2. Visuelle Zustände verbessern
- [ ] Rapid Moves gestrichelt
- [ ] Roughing / Finishing visuell unterscheiden
- [ ] Drill-Plunges klar darstellen
- [ ] optional Reihenfolge / Nummerierung einblenden

---

## Phase D — Validierung & Export

### D1. Feature-basierte Validierung
- [ ] unzugewiesene Features melden
- [ ] Werkzeug fehlt
- [ ] Tiefe fehlt
- [ ] ungültige Geometrie
- [ ] nicht exportierbare Kombinationen melden

### D2. Exportstatus zurückspiegeln
- [ ] pro Feature / pro Platte Validierungsstatus anzeigen
- [ ] Exportergebnis im UI sichtbar machen

---

## Phase E — UI Automation / Smoke Tests

### E1. Automation-fähige Controls
- [ ] stabile semantische Control IDs einführen
- [ ] wichtigste Buttons/Felder/Grids benennen
- [ ] Dialogtitel stabilisieren

### E2. Erste Smoke-Tests auf Windows
- [ ] Rhino starten
- [ ] Plugin öffnen
- [ ] Testdatei mit Kreisloch laden
- [ ] Loch-Feature auswählen
- [ ] Bearbeitung `Inner Contour` oder `Circular Pocket` zuweisen
- [ ] Vorschau erzeugen
- [ ] validieren
- [ ] exportieren

---

## MVP: Erster durchgehender Workflow

### Muss zuerst funktionieren
- [ ] Platte scannen
- [ ] Kreisloch als Feature erkennen
- [ ] Bearbeitung zuweisen
- [ ] Werkzeug + Tiefe setzen
- [ ] Vorschau erzeugen
- [ ] validieren
- [ ] exportieren

Das ist der erste echte Qualitäts-Massstab. Wenn dieser Flow sauber funktioniert, trägt er den Rest.

---

## Direkt gestartete Umsetzung (heute)
- [x] Backlog dokumentiert
- [x] erster Schritt Richtung automation-fähiges UI begonnen
- [ ] Workflow-Hinweise im CAM-Panel schärfen
- [ ] stabile IDs für Primär-Controls fertig verdrahten
- [ ] nächste Codephase: Feature-orientierte Hauptliste vorbereiten
