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
