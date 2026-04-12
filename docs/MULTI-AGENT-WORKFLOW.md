# Multi-Agent Workflow for RhinoCNCExporter

## Ziel

Dieses Repo soll mit mehreren Coding-Agents parallel bearbeitet werden, ohne dass sie sich gegenseitig kaputtmachen. Gleichzeitig soll CI automatisch Core-Tests, Windows-Plugin-Build und Packaging übernehmen.

## Grundprinzipien

1. **Immer isoliert arbeiten**: pro Agent ein eigener Git-Worktree
2. **Klare File-Ownership**: kein paralleles Editieren derselben Datei
3. **Merge nur zentral**: nur der Orchestrator merged nach `main`
4. **Build vor Merge**: kein Merge ohne grünen Build
5. **Docs sind Teil der Arbeit**: Änderungen immer in README / docs / handoff spiegeln

## Rollenmodell

### 1. UI Agent
**Scope:**
- `RhinoCNCExporter/UI/*`
- UI-nahe Services

**Typische Aufgaben:**
- CAM Panel
- Workflow-Shell
- Dialoge
- Visualisierung / Zustände

### 2. Core/Test Agent
**Scope:**
- `RhinoCNCExporter.Core/*`
- `RhinoCNCExporter.Tests/*`

**Typische Aufgaben:**
- Pipeline-Logik
- Parser
- Snapshot-Modelle
- Regression-Tests

### 3. CI/Build Agent
**Scope:**
- `.github/workflows/*`
- `scripts/*`
- Packaging / Artifact-Logik

**Typische Aufgaben:**
- GitHub Actions
- Windows Runner Integration
- Build/Test/Package Scripts

### 4. Docs Agent
**Scope:**
- `README.md`
- `docs/*`
- `CONTEXT-HANDOFF.md`

**Typische Aufgaben:**
- User Guide aktualisieren
- Architektur- / Handoff-Doku pflegen
- Test- und Setup-Anleitungen schärfen

### 5. Review Agent
**Scope:**
- kein eigener Feature-Code
- liest Diffs und kommentiert Risiken

**Typische Aufgaben:**
- Architektur-Review
- Testlücken aufzeigen
- Naming / Konsistenz / Regression-Risiken markieren

## File-Ownership Matrix (Startpunkt)

| Rolle | Darf primär anfassen | Darf nicht parallel anfassen |
|---|---|---|
| UI | `RhinoCNCExporter/UI/*` | `RhinoCNCExporter.Core/*` |
| Core/Test | `RhinoCNCExporter.Core/*`, `RhinoCNCExporter.Tests/*` | `RhinoCNCExporter/UI/*` |
| CI/Build | `.github/workflows/*`, `scripts/*` | Feature-Code nur wenn explizit nötig |
| Docs | `README.md`, `docs/*`, `CONTEXT-HANDOFF.md` | Produktivcode |
| Review | keine Produktivdateien | alles |

Wenn zwei Rollen dieselbe Datei brauchen, wird die Arbeit **sequenziell** gemacht.

## Worktree-Setup

Script:

```powershell
.\scripts\setup-agent-worktrees.ps1 -Ticket cam-ui -BaseBranch main
```

Das erzeugt standardmässig:

- `.worktrees/ui`
- `.worktrees/tests`
- `.worktrees/ci`
- `.worktrees/docs`
- `.worktrees/review`

mit Branches:

- `swarm/cam-ui/ui`
- `swarm/cam-ui/tests`
- `swarm/cam-ui/ci`
- `swarm/cam-ui/docs`
- `swarm/cam-ui/review`

## Empfohlene Harness-Sessions

- `codex-rhino-ui`
- `codex-rhino-tests`
- `codex-rhino-ci`
- `codex-rhino-docs`
- `codex-rhino-review`

Jede Session arbeitet nur in ihrem eigenen Worktree.

## Standard-Ablauf pro Feature

### Wave 1: Vorbereitung
- Ziel definieren
- Scope aufteilen
- Worktrees erzeugen
- Ownership festlegen
- relevante Docs / Handoff lesen

### Wave 2: Parallel-Arbeit
- UI Agent implementiert Oberfläche / UX
- Core/Test Agent ergänzt Logik + Tests
- CI Agent pflegt Pipeline / Scripts
- Docs Agent dokumentiert den neuen Flow

### Wave 3: Review
- Review Agent liest Diffs
- Review-Kommentare werden eingearbeitet
- CI muss grün sein

### Wave 4: Merge
- Orchestrator merged **seriell**
- nach jedem Merge Build / Tests prüfen
- erst dann nächste Rolle mergen

## CI/CD Setup

### Workflow 1: Core CI
Datei: `.github/workflows/core-ci.yml`

Läuft auf GitHub Hosted Runnern und prüft:
- Build von `RhinoCNCExporter.Core`
- Build von `RhinoCNCExporter.Tests`
- xUnit-Tests
- Upload der Testresultate

### Workflow 2: Windows Plugin Build
Datei: `.github/workflows/windows-plugin-build.yml`

Läuft auf einem **self-hosted Windows Runner** mit Labels:
- `self-hosted`
- `Windows`
- `X64`
- `Rhino8`

Prüft:
- Plugin-Build
- Tests
- Yak-Package
- Upload von `.yak` und `.rhp` als Artifacts

**Sicherheitsregel:** Dieser Workflow soll nur auf trusted `push` und `workflow_dispatch` laufen, nicht auf `pull_request`, weil er einen self-hosted Windows Runner mit Rhino 8 nutzt.

### Workflow 3: Release Package
Datei: `.github/workflows/release-package.yml`

Läuft für Tag-Releases auf demselben **self-hosted Windows Runner** und erzeugt GitHub Releases mit `.yak`, `.rhp` und `manifest.yml`.

**Sicherheitsregel:** Auch dieser Workflow nutzt jetzt clean checkout ohne persistierte Credentials, `concurrency`-Schranke und expliziten Timeout, damit Release-Läufe auf dem öffentlichen Repo defensiv bleiben.

## Self-hosted Runner Empfehlung

Am besten auf dem Windows-Rechner einrichten, der auch Rhino 8 installiert hat.

Voraussetzungen:
- Rhino 8 installiert
- `yak.exe` unter `C:\Program Files\Rhino 8\System\yak.exe`
- .NET 7 SDK
- GitHub Actions Runner als Service
- Runner Label `Rhino8`

## Scripts

### Build
```powershell
.\scripts\build.ps1 -Config Release
```

### Tests
```powershell
.\scripts\test.ps1 -Config Release
```

### Package
```powershell
.\scripts\package.ps1 -Config Release
```

### Install lokal
```powershell
.\scripts\install.ps1 -Config Release
```

### Alles lokal wie bisher
```powershell
.\scripts\build-and-install.ps1
```

## Definition of Done

Ein Task gilt erst als fertig wenn:
- Code im richtigen Worktree liegt
- Build grün ist
- relevante Tests grün sind
- Doku aktualisiert ist
- PR-Template ausgefüllt ist
- Review-Agent oder menschliches Review stattgefunden hat

## Praktische Empfehlung für dieses Repo

Bei UI-lastigen Features:
- UI + Docs parallel
- Core/Test parallel nur wenn kein File-Overlap
- CI separat
- Review zuletzt

Bei Pipeline-/Emitter-Features:
- Core/Test zuerst
- UI danach
- Docs + CI parallel

## Nächster sinnvoller Ausbau

1. Smoke-Test-Script für Rhino Start / Plugin-Laden
2. nightly Windows Build
3. Release Workflow für GitHub Releases
4. automatische OpenClaw- / Telegram-Benachrichtigung bei grün/rot
