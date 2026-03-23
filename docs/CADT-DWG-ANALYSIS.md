# CAD+T DWG Analyse — Putz-Schrank (Staub Wolf)

**Datum:** 23. März 2026
**Quelle:** `Putz-Schrank.dwg` aus CAD+T exportiert
**Statistik:** 522 Layer, 2292 Objekte, 1562 Block-Inserts

## 1. Block-Systematik

CAD+T nutzt ein **Block-basiertes System** für alle Beschläge und Bearbeitungen.

### Beschläge (.BES Blöcke) — 22 Typen

| Block-Name | Typ | Inserts |
|-----------|-----|---------|
| `TOPF_GEBOHRT.BES` | Topfband (oben) | 8× |
| `TOPF_GEBOHRT_UNTEN.BES` | Topfband (unten) | 2× |
| `EXZ_TOPF_MIT_RAND_19.BES` | Exzenter + Topf 19mm | 8× |
| `MONTVERB_SCHRANK_35.BES` | Montageverbinder 35mm | 5× |
| `RAST_BOLZ_RAPID_24_ohne_Flanschbohrung.bes` | Rastbolzen Rapid 24 | 8× |
| `RIFFELDUEBEL_8.BES` | Riffeldübel Ø8 | 8× |
| `TAB_T_D5_VERN.BES` | Tablar-Träger D5 (vernickelt) | 12× |
| `PUF_2-5.BES` | Puffer 2.5mm | 4× |
| `SO_STEL_HOLZ_45.bes` | Sockelversteller Holz 45mm | 3× |
| `SO_STEL_HOLZ_Schraube_35_.BES` | Sockelversteller Schraube 35mm | 3× |
| `B_CT_E_110_BM1.BES` | CLAMEX P / Einweg 110° BM1 | 3× |
| `B_CT_E_110_BM2.BES` | CLAMEX P / Einweg 110° BM2 | 2× |
| `B_CT_M_110_BM1.BES` | CLAMEX P / Mehrweg 110° BM1 | 3× |
| `B_CT_M_110_BM2.BES` | CLAMEX P / Mehrweg 110° BM2 | 2× |
| `B_MP_0.BES` | Montageplatte | 10× |
| `Glutz_5341_Langschild_BB.BES` | Türbeschlag Glutz 5341 | 1× |
| `Glutz_5341_Langschild_BBØ.BES` | Türbeschlag Glutz (Gegenseite) | 1× |
| `Memphis_Drueckerpaar_8x84.BES` | Drücker Memphis 8x84 | 1× |
| `Memphis_Lochteil_8.BES` | Drücker Lochteil | 1× |
| `Griffp_200_17.BES` | Griffprofil 200mm | 2× |
| `MARKIERUNGSLOCH_FLAECHE_SE.BES` | Markierungsloch Fläche | 2× |
| `Oeffnungsanzeige.BES` | Öffnungsanzeige (Tür) | 2× |

### Bohrbilder (.WRK Blöcke) — 26 Typen (2D + 3D Paare)

| Block-Name | Was |
|-----------|-----|
| `BOHR_TOPFB_BOHR.WRK` / `.WRK3D` | Topfband-Bohrbild |
| `BOHR_TOPFB_GP.WRK` / `.WRK3D` | Topfband Grundplatte |
| `BOHR_D08_T12.WRK` / `.WRK3D` | Bohrung Ø8 T=12 |
| `BOHR_DUBD8_STIRN_30.WRK` / `.WRK3D` | Dübel Ø8 Stirn T=30 |
| `BOHR_ALLG_MARK.WRK` / `.WRK3D` | Allgemeine Markierung |
| `bohr_DUB_8x10.WRK` / `.WRK3D` | Dübel 8×10 |
| `bohr_Puffer.WRK` / `.WRK3D` | Puffer-Bohrung |
| `bohr_Ø03_T17.WRK` / `.WRK3D` | Bohrung Ø3 T=17 |
| `D08_T-20.WRK` / `.WRK3D` | Bohrung Ø8 T=20 |
| `DURCHGANGSLOCH_Ø25.WRK` / `.WRK3D` | Durchgangsloch Ø25 |
| `RAST_T_16_14T.WRK` / `.WRK3D` | Rast-Tablar T=16 14T |
| `LOCH_Ø03_T06.WRK` / `.WRK3D` | Loch Ø3 T=6 |
| `bohr_markierung_Flaeche.WRK` / `.WRK3D` | Markierung Fläche |

**Pattern:** `.WRK` = 2D Darstellung, `.WRK3D` = 3D Darstellung. Immer Paare!

### CAD+T Interne Blöcke (AZC*)

| Prefix | Bedeutung | Anzahl |
|--------|-----------|--------|
| `AZCBODEF` | Bohrungs-Definition | 138 Inserts! |
| `AZCBTDAR` | Bauteil-Darstellung | 621 Inserts! |
| `AZCNUDEF` | Nuten-Definition | 6× |
| `AZCKNDEF` | Kanten-Definition | 4× |
| `AZCTASDEF` | Taschen-Definition | 5× |
| `AZCBOHR` | Bohrungen (komplex) | 38 obj im Block |
| `AZCSTAND` | Standard-Block | 32 obj |

### XCEBO Blöcke — Lochreihen-System!

**Naming:** `XCEBO{typ}${param1}${param2}${param3}${param4}`

| Prefix | Bedeutung |
|--------|-----------|
| `XCEBO400` | Lochreihe einseitig (4-5 obj) |
| `XCEBO402` | Lochreihe beidseitig (9 obj) |
| `XCEBO403` | Lochreihe + Bohrung (6 obj) |
| `XCEBO404` | Lochreihe komplex (7 obj) |
| `XCEBO410` | Spezial-Lochreihe |
| `XCEBO413` | Spezial |
| `XCEBO414` | Spezial |

**Parameter-Dekodierung (Hypothese):**
`XCEBO400$500$1200$1200$1200` → Typ 400, Y-Start=500, Raster?=1200, Höhe?=1200, ?=1200

43 verschiedene XCEBO-Varianten → das sind die vorkonfigurierten Lochreihen-Positionen!

## 2. Layer-Systematik

### CAD+T Layer-Naming

`AZC_{ansicht}_{layer-typ}`

| Layer-Teil | Bedeutung | Beispiele |
|-----------|-----------|-----------|
| `AZC_MODEL____` | 3D-Modell | `_LA` (Linien), `_LB` (Flächen), `_LK` (Kanten) |
| `AZC_ANSICHT__` | Ansichts-Darstellung | `_LA`, `_LB`, `_LK` |
| `AZC_GRUNDRIS_` | Grundriss | `_LA`, `_LB`, `_LF`, `_LI` |
| `AZC_SEITELIN_` | Seitenansicht links | `_LA`, `_LB` |
| `AZC_PBEREICH_` | P-Bereich (Produktion?) | `_LB`, `_LH`, `_LI` |
| `AZC_CADT0001_` | Bauteil 1 (Seite rechts?) | `_LA` bis `_LI` |
| `AZC_CADT0002_` | Bauteil 2 (Seite links?) | `_LA` bis `_LI` |
| `AZC_CADT0003_` | Bauteil 3 (Boden/Deckel?) | `_LA`, `_LB`, `_LG` |
| `AZC_CADT0004_` | Bauteil 4 | `_LA` bis `_LK` |
| `AZC_CADT0005_` | Bauteil 5 | `_LA` bis `_LI` |
| `AZC_BTVERWALT` | Bauteil-Verwaltung | Texte + Anmerkungen |

**Suffix-Bedeutung:**
- `_LA` = Linien Ansicht (Polylines)
- `_LB` = Linien Bohrung/Bearbeitung (Curves + Polylines)
- `_LG` = Geometrie (Hatches)
- `_LH` = Hatches/Schraffuren
- `_LI` = Info/Texte
- `_LK` = Kanten/Konturen
- `_LF` = Füllung

## 3. Erkenntnisse für RhinoCNCExporter

### Was CAD+T richtig macht (und wir übernehmen können):

1. **Block-basierte Beschläge:** Jeder Beschlag ist ein wiederverwendbarer Block mit eindeutigem Namen
   - `.BES` = Beschlag-Symbol (2D Darstellung + Metadaten)
   - `.WRK` = Werkstattzeichnungs-Darstellung (2D)
   - `.WRK3D` = 3D-Darstellung
   - → Wir: Beschlag-Blöcke mit UserText/Attributes für CNC-Parameter

2. **Bohrbilder als Blöcke:** Nicht einzelne Kreise, sondern vordefinierte Bohrbilder
   - `BOHR_TOPFB_BOHR.WRK` = Topfband komplett (Topf + Grundplatte)
   - → Wir: Makro-Bibliothek als Block-Definitionen

3. **Lochreihen parametrisch:** `XCEBO` Blöcke kodieren Lochreihen-Parameter im Namen
   - → Wir: Ähnliches System mit `XCEBO` oder eigenen Block-Namen

4. **2D + 3D Paare:** Jeder Beschlag hat 2D und 3D Darstellung
   - → Wir: Block mit 3D-Geometrie (für Viewport) + CNC-Attribute (für Export)

5. **Bauteil-Layer:** Pro Bauteil ein Layer-Set (`CADT0001`, `CADT0002`, ...)
   - → Wir: Pro Platte ein Layer-Group oder Sublayer-Hierarchie

### Vision: Unser Block-System

```
RhinoCNCExporter Block Library:
├── Beschläge/
│   ├── TOPFBAND_35.3dm          (3D Block + CNC Attribute)
│   ├── CLAMEX_P14.3dm           (3D Block + CNC Attribute)
│   ├── RIFFELDUBEL_8x30.3dm
│   ├── EXZENTER_15.3dm
│   └── MONTAGEVERBINDER_35.3dm
├── Lochreihen/
│   ├── SYSTEM32_EINSEITIG.3dm
│   ├── SYSTEM32_BEIDSEITIG.3dm
│   └── SYSTEM32_MIT_EXZENTER.3dm
└── Sockel/
    ├── SOCKELVERSTELLER_45.3dm
    └── SOCKELFUSS.3dm
```

Jeder Block hat:
- **3D Geometrie:** Visuell im Viewport sichtbar
- **UserText/Attributes:** CNC-Parameter (Bohrtiefe, Durchmesser, Makro-Typ)
- **Insertion Point:** Definiert die CNC-Position
- **Rotation:** Definiert die Orientierung

---

# CAD+T DWG Analyse — Pult und Korpus (Novotny)

**Datum:** 23. März 2026
**Quelle:** `Pult_und_Korpus_Novotny.dwg` aus CAD+T exportiert
**Statistik:** 499 Layer, 3041 Objekte, 2176 Block-Inserts

## 1. Neue Beschlag-Typen (vs. Staub Wolf)

### Blum Legrabox-System (13 Block-Typen!)

| Block-Name | Typ | Inserts |
|-----------|-----|---------|
| `FS_LEG_C_550.BES` | Legrabox C-Profil 550mm | 6× |
| `FS_LEG_M_550.BES` | Legrabox M-Profil 550mm | 2× |
| `FS_LEG_FRO_BE_C_EXP.BES` | Front-Befestigung C Expando | 6× |
| `FS_LEG_FRO_BE_M_EXP.BES` | Front-Befestigung M Expando | 8× |
| `LEG.BES` | Legrabox Basis | 4× |
| `LEG_40_550_S.BES` | Legrabox 40mm 550 Slim | 8× |
| `LEG_HRWH_C_W.BES` | Höhenreduzierwange C Weiss | 6× |
| `LEG_HRWH_M_W.BES` | Höhenreduzierwange M Weiss | 2× |
| `LEG_KONTUR_C_LI.BES` | Kontur C Links | 3× |
| `LEG_KONTUR_C_RE.BES` | Kontur C Rechts | 3× |
| `LEG_KONTUR_M_LI.BES` | Kontur M Links | 1× |
| `LEG_KONTUR_M_RE.BES` | Kontur M Rechts | 1× |
| `550.BES` | Auszug-Länge 550mm | 4× |

**Erkenntnis:** Legrabox = ein ganzes System aus Einzel-Blöcken. C = niedrig (66mm), M = mittel (104mm). Front-Befestigung Expando = werkzeuglose Montage. Höhenreduzierwange = wenn Schublade niedriger als Zarge.

### Blum TipOn-System (5 Block-Typen)

| Block-Name | Typ | Inserts |
|-----------|-----|---------|
| `MOV_LEG_TipOn_L1.BES` | TipOn Legrabox L1 (kurz) | 2× |
| `MOV_LEG_TipOn_L3.BES` | TipOn Legrabox L3 (lang) | 6× |
| `MOV_LEG_TipOn_SY_AD.BES` | TipOn Synchronisations-Adapter | 8× |
| `TIPON.BES` | TipOn Einheit | 4× |
| `TIPONMARKIERUNG.BES` | TipOn Markierung (Bohrposition) | 8× |

**Erkenntnis:** TipOn = elektromechanisches Öffnungssystem für grifflose Fronten. Braucht Bohrungen in der Seite für die TipOn-Einheit + Markierungslöcher. L1/L3 = verschiedene Tiefen.

### Hängeschrank & Spezial

| Block-Name | Typ | Inserts |
|-----------|-----|---------|
| `KON_HG_FL_75kg_480.BES` | Konsole Hängeschrank flach 75kg 480mm | 1× |
| `KON_HG_FL_75kg_680.BES` | Konsole Hängeschrank flach 75kg 680mm | 1× |
| `Fenstergriff.bes` | Fenstergriff | 1× |
| `MARKIERUNGSLOCH_FLAECHE_FROSTAB.BES` | Frostschutz-Markierung | 4× |
| `C.BES` | CLAMEX C-System | 3× |
| `M.BES` | CLAMEX M-System | 1× |

### Lochreihen (XCEBO) — weniger Varianten, mehr Inserts

| Typ | Varianten | Inserts | Vs. Staub |
|-----|-----------|---------|-----------|
| XCEBO400 (einseitig) | 8 | 58× | Ähnlich |
| XCEBO402 (beidseitig) | 3 | 60× | Weniger Varianten, mehr Inserts |
| **Total** | **11** | **118×** | Staub: 43 Var / ~100 Inserts |

## 2. Vergleich Staub Wolf vs. Novotny

| | Staub Wolf (Putzschrank) | Novotny (Pult+Korpus) |
|---|---|---|
| **Layer** | 522 | 499 |
| **Objekte** | 2292 | 3041 |
| **Block-Inserts** | 1562 | 2176 |
| **.BES Typen** | 22 | 38 (+16 neue!) |
| **XCEBO Varianten** | 43 | 12 |
| **AZC Intern** | 781 | 1159 |
| **Highlight** | CLAMEX B_CT, Türbeschläge | Legrabox-System, TipOn |
| **Schubladen** | Keine | Komplett (Legrabox C+M) |
| **Hängeschrank** | Nein | Ja (Konsolen 75kg) |

## 3. Naming-Konventionen erkannt

### BES Block-Namen Systematik

```
{System}_{Detail}_{Variante}.BES

Beispiele:
  FS_LEG_C_550        = FrontSystem_Legrabox_C-Höhe_550mm
  FS_LEG_FRO_BE_C_EXP = FrontSystem_Legrabox_Front_Befestigung_C_Expando
  LEG_HRWH_C_W        = Legrabox_Höhenreduzierwange_C_Weiss
  LEG_KONTUR_C_LI     = Legrabox_Kontur_C_Links
  MOV_LEG_TipOn_L3    = Movento_Legrabox_TipOn_Länge3
  B_CT_E_110_BM1      = Beschlag_ClamexT_Einweg_110Grad_Bohrmaschine1
  SO_STEL_HOLZ_45     = Sockel_Stellfuss_Holz_45mm
  KON_HG_FL_75kg_480  = Konsole_Hängeschrank_Flach_75kg_480mm
```

### Hierarchie der Beschlag-Familien

```
Blum Beschläge:
├── Legrabox (Schubladen)
│   ├── Seitenprofile (C=66mm, M=104mm, K=144mm)
│   ├── Front-Befestigung (Expando, Clip)
│   ├── Höhenreduzierwangen
│   └── Konturen (Links/Rechts)
├── TipOn (Grifflos-Öffnung)
│   ├── Einheiten (L1, L3)
│   ├── Synchronisation
│   └── Markierungen
├── Topfbänder
│   ├── Standard (110°)
│   └── Unterliegend
└── Exzenter + Montageverbinder

Hettich/Andere:
├── Rastbolzen Rapid 24
├── Riffeldübel
├── Sockelversteller
└── Tablar-Träger

Türbeschläge:
├── Glutz 5341 Langschild
├── Memphis Drücker
└── Fenstergriff
```

## 4. Erkenntnisse für Block-Bibliothek

### Was wir NICHT 1:1 übernehmen
- Die 13 Legrabox-Blöcke brauchen wir nicht alle als CNC-Blöcke
- Viele sind nur **visuelle Darstellungen** (Wangen, Konturen) ohne CNC-Relevanz
- Für CNC relevant sind nur die **Bohrungen** in der Seitenwand/Boden

### Was CNC-relevant ist pro System

**Legrabox:**
- Seitenwand: 2× Lochreihe für Schiene (XCEBO402) + 2× Bohrung Front-Befestigung
- Boden: Bohrung für Dübelverbindung
- → 1 "Legrabox-Paket" Block der alle Bohrungen enthält

**TipOn:**
- Seitenwand: 1× Bohrung für TipOn-Einheit + Markierungsloch
- → 1 "TipOn" Block

**Topfband:**
- Seitenwand: Topf (Ø35, T13) + Grundplatte (2× Ø5)
- → 1 "Topfband" Block (wie gehabt)

### Unser Ansatz: CNC-Pakete statt Einzel-Blöcke

```
Statt 13 Legrabox-Blöcke:
  → 1 Block "Legrabox_C_550" der ALLE Bohrungen definiert
  → UserText: CNC_Type=LEGRABOX, CNC_Height=C, CNC_Length=550
  → Plugin kennt das Bohrbild für Legrabox C 550mm
  → Generiert: Lochreihen + Front-Befestigung + Dübelbohrungen

Statt 5 TipOn-Blöcke:
  → 1 Block "TipOn_L3" 
  → UserText: CNC_Type=TIPON, CNC_Variant=L3
  → Plugin kennt das Bohrbild
```

**Vorteil:** Der Schreiner setzt EINEN Block, nicht 5-13 Einzel-Blöcke. Das Plugin weiss was zu bohren ist.

---

# CAD+T DWG Analyse — Innenschubladen (Marti Optik)

**Datum:** 23. März 2026
**Quelle:** `Innenschubladen_MartiOptik.dwg` aus CAD+T exportiert
**Statistik:** 487 Layer, 2246 Objekte, 1769 Block-Inserts

## 1. Neue Beschlag-Typen

### Blum Legrabox INSIDE (Innenschubladen — 6 neue Block-Typen!)

| Block-Name | Typ | Inserts |
|-----------|-----|---------|
| `LEG_I.BES` | Legrabox INSIDE Basis | 4× |
| `LEG_I_FRO_HA_M_W.bes` | Inside Front-Halter M Weiss | 8× |
| `LEG_I_FRO_STABI.BES` | Inside Front-Stabilisator | 4× |
| `LEG_I_FRO_STABI_0.BES` | Inside Front-Stabi Variante 0 | 4× |
| `LEG_KONTUR_IS_M_LI.BES` | Inside Kontur M Links | 4× |
| `LEG_KONTUR_IS_M_RE.BES` | Inside Kontur M Rechts | 4× |

**Erkenntnis:** Legrabox INSIDE = Innenschubladen-System (Schublade in Schublade). Komplett eigene Block-Familie mit `_I` / `_IS` Suffix. Auszuglänge 350mm statt 550mm.

### DistaCube (Abstandhalter — 2 neue Block-Typen)

| Block-Name | Typ | Inserts |
|-----------|-----|---------|
| `DISTACUBE_30_S.BES` | DistaCube 30mm Seite | 8× |
| `DISTACUBE_30_vorne.BES` | DistaCube 30mm Vorne | 8× |

**Erkenntnis:** DistaCube = Blum Abstandhalter für Innenschubladen. Definiert den Abstand zwischen Aussen- und Innenschublade. CNC-relevant: Bohrungen für die Befestigung.

### Topfband-Kollision (neuer Block-Typ!)

| Block-Name | Typ | Inserts |
|-----------|-----|---------|
| `TOPFBAND_KOLLISION.BES` | Topfband Kollisions-Check | 8× |

**Erkenntnis:** Nicht CNC-relevant — reiner Planungs-Block der prüft ob Schublade und Scharnier kollidieren. Interessant für Validierung, aber kein CNC-Output.

### XCEBO401 — Neuer Lochreihen-Typ!

| Block-Name | Inserts |
|-----------|---------|
| `XCEBO401$250$0$200$0` | 8× |
| `XCEBO401$500$1200$1300$1200` | 96× (!!) |

**Erkenntnis:** XCEBO401 ist neu — nur bei Marti Optik. 96× ein einzelner Typ → das sind die Innenschubladen-Lochreihen. Hypothese: XCEBO401 = einseitige Lochreihe für Innenschublade (vs. XCEBO400 = einseitig Standard, XCEBO402 = beidseitig).

## 2. Vergleich alle 3 DWGs

| | Staub Wolf | Novotny | Marti Optik |
|---|---|---|---|
| **Möbel-Typ** | Putzschrank | Pult + Korpus | Innenschubladen |
| **Layer** | 522 | 499 | 487 |
| **Objekte** | 2292 | 3041 | 2246 |
| **Block-Inserts** | 1562 | 2176 | 1769 |
| **.BES Typen** | 22 | 38 | 23 |
| **XCEBO Inserts** | ~100 | 118 | 206 |
| **XCEBO401** | — | — | 104 (NEU!) |
| **Legrabox** | Nein | Standard (C+M) | INSIDE (Innenschublade) |
| **TipOn** | Nein | Ja (5 Typen) | Nein |
| **CLAMEX** | B_CT (5 Typen) | C.BES + M.BES | B_CT_E (1 Typ) |
| **Topfband** | Standard | Standard | Standard + Kollision |
| **DistaCube** | Nein | Nein | Ja |
| **Hängeschrank** | Nein | Ja (Konsolen) | Nein |

## 3. Gesamtbild: Beschlag-Familien über alle 3 DWGs

```
Blum Legrabox Familie:
├── Standard (C=66mm, M=104mm)     → Novotny
├── INSIDE (Innenschubladen)        → Marti Optik
├── Auszuglängen: 350mm, 550mm
├── Front-Befestigung: Expando, Halter, Stabilisator
├── Höhenreduzierwangen
└── DistaCube (Abstandhalter)       → Marti Optik

Blum TipOn Familie:                 → Novotny
├── L1 (kurz), L3 (lang)
├── Synchronisation
└── Markierungen

CLAMEX Familie:
├── B_CT_E_110 (Einweg)            → Staub, Marti
├── B_CT_M_110 (Mehrweg)           → Staub
├── C.BES, M.BES (vereinfacht)     → Novotny
└── B_MP_0 (Montageplatte)         → Staub, Marti

Topfband Familie:
├── TOPF_GEBOHRT (Standard)        → alle 3
├── TOPF_GEBOHRT_UNTEN             → Staub, Marti
├── EXZ_TOPF_MIT_RAND_19           → Staub, Novotny
└── TOPFBAND_KOLLISION (Check)     → Marti

Verbinder Familie:
├── RIFFELDUEBEL_8                  → Staub, Novotny
├── RAST_BOLZ_RAPID_24             → Staub, Novotny
├── MONTVERB_SCHRANK_35            → Staub
└── TAB_T_D5_VERN (Tablar-Träger) → Staub

Lochreihen (XCEBO):
├── XCEBO400 (einseitig)           → alle 3
├── XCEBO401 (einseitig Inside?)   → nur Marti
├── XCEBO402 (beidseitig)          → alle 3
├── XCEBO403 (+ Bohrung)           → nur Staub
├── XCEBO404 (komplex)             → nur Staub
├── XCEBO410 (Spezial)             → nur Staub
├── XCEBO413 (Spezial)             → nur Staub
└── XCEBO414 (Spezial)             → nur Staub
```

## 4. Fazit für Block-Bibliothek

### CNC-Pakete Priorität (basierend auf Häufigkeit über alle 3 DWGs)

| Priorität | CNC-Paket | Vorkommen |
|-----------|-----------|-----------|
| 🔴 P1 | Lochreihe System 32 (XCEBO) | 424× total |
| 🔴 P1 | Topfband 35mm | 20× |
| 🔴 P1 | Exzenter + Topf | 16× |
| 🟠 P2 | Legrabox Standard (C/M) | ~50× |
| 🟠 P2 | CLAMEX (E/M Varianten) | ~20× |
| 🟠 P2 | Riffeldübel 8mm | 16× |
| 🟡 P3 | Legrabox INSIDE | ~40× |
| 🟡 P3 | TipOn | ~28× |
| 🟡 P3 | Rastbolzen Rapid 24 | 16× |
| 🟢 P4 | DistaCube | 16× |
| 🟢 P4 | Hängeschrank-Konsolen | 2× |
| 🟢 P4 | Sockelversteller | 7× |
