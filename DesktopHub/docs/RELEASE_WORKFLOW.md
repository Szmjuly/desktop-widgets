# DesktopHub Release Workflow

This document defines the exact release process for DesktopHub.
It is also used as reference by the Cascade AI release rule and workflows.

---

## Triggering a Release in Windsurf

| Command | What it does |
|---|---|
| `/plan-release-desktophub` | Inspect changes, propose version, draft notes — no execution |
| `/release-desktophub` | Full release: version, build, publish, commit, tag, push |

---

## Required Working Directory

All release commands must be run from:
  desktop-widgets\DesktopHub\

---

## Required Environment Setup

Run before any build or versioning work:
```powershell
$env:PATH = "C:\dotnet;$env:PATH"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\dev-env.ps1
```

---

## How to Inspect Changes Since Last Tag
```powershell
# Find last tag
git describe --tags --abbrev=0

# Review commits since last tag
git log <last_tag>..HEAD --oneline

# Review changed files since last tag
git diff <last_tag>..HEAD --name-only
```

---

## Semantic Version Decision Rules

| Type | When to use |
|---|---|
| PATCH | Bug fixes, small improvements, no new user-facing features |
| MINOR | New features, new UI panels, new services, non-breaking additions |
| MAJOR | Breaking changes, major architectural or UX overhauls |

---

## Files to Update

| File | What to change |
|---|---|
| `src\DesktopHub.UI\DesktopHub.UI.csproj` | `<Version>X.Y.Z</Version>` inside `<PropertyGroup>` |
| What's New source | Bullets for new release |
| `releases\v<version>.md` | GitHub release markdown (generated) |

---

## Version Update Script
```powershell
.\scripts\update-version.ps1 "<new_version>" "<short comma-separated summary>"
```

Example:
```powershell
.\scripts\update-version.ps1 "1.9.0" "Firebase sync, role editor, feeder schedules"
```

---

## Release Build Command
```powershell
dotnet publish src\DesktopHub.UI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o "src\DesktopHub.UI\bin\Release\net8.0-windows\win-x64\publish-v<new_version>"
```

---

## Git Sequence
```powershell
git add -A
git commit -m "release: v<new_version>"
git tag v<new_version>
git push origin main --tags
```

---

## GitHub Release Markdown Template
```markdown
# Version X.Y.Z - [Short headline]

## 🎉 New Features

### [Feature Area]

- bullet
- bullet

## 🎨 Improvements

### [Improvement Area]

- bullet

## 📌 Notes

- Version bump type and reasoning
- Key implementation notes
- Admin or deployment instructions if applicable

## Assets

- `DesktopHub.exe` — single-file self-contained build
```

---

## Release Checklist

- [ ] Working directory confirmed as `desktop-widgets\DesktopHub\`
- [ ] Environment setup ran without errors
- [ ] Git log inspected since last tag
- [ ] Semver bump type decided with reasoning
- [ ] `<Version>` updated in `.csproj`
- [ ] `update-version.ps1` ran successfully
- [ ] What's New bullets updated
- [ ] GitHub release markdown generated and saved to `releases\`
- [ ] `dotnet publish` completed without errors
- [ ] `git add -A` staged all changes
- [ ] Commit created with `release: vX.Y.Z` message
- [ ] Tag created
- [ ] Pushed to `origin main --tags`

---

## Safety Rules

- Never push a tag before the build confirms success
- Never write release notes for features not in the diff
- Never run publish from a subdirectory
- Never skip the git log inspection — always read actual changes