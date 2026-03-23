---
trigger: always_on
---

# DesktopHub Release Workflow — Cascade Rule

This rule governs all build, release, version bump, and publish operations
for the DesktopHub project. When the user asks to bump, release, publish,
build for release, prepare a versioned build, or uses the /release-desktophub
or /plan-release-desktophub workflows — follow this protocol exactly.
Never skip, reorder, or abbreviate any step.

---

## SCOPE

This rule applies exclusively to DesktopHub release operations.
Project location: desktop-widgets\DesktopHub\

---

## STEP 1 — VERIFY WORKING DIRECTORY

Before any release work begins, confirm the current working directory is:
  desktop-widgets\DesktopHub\

Do not run any release commands from a subdirectory.
Run Get-Location to verify if there is any doubt.

---

## STEP 2 — ENVIRONMENT SETUP

Run these three commands in order before any build or versioning work.
Do not proceed if any command fails.

  $env:PATH = "C:\dotnet;$env:PATH"
  Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
  .\scripts\dev-env.ps1

---

## STEP 3 — INSPECT CHANGES SINCE LAST TAG

1. Find the most recent git version tag:
     git describe --tags --abbrev=0

2. Review all commits since that tag:
     git log <last_tag>..HEAD --oneline

3. Review all changed files since that tag:
     git diff <last_tag>..HEAD --name-only

4. Summarize what changed in plain English, grouped into:
   - New user-facing features
   - Improvements or refactors
   - Bug fixes
   - Admin / deployment / infrastructure changes

---

## STEP 4 — DETERMINE SEMANTIC VERSION BUMP

Using the changes identified in Step 3, decide:

  PATCH  — fixes, small improvements, no new user-facing features
  MINOR  — new features, new UI, new services, non-breaking additions
  MAJOR  — breaking changes, architectural overhauls, major UX shifts

Propose the next version number.
Do not guess — always base this on the actual git history.

---

## STEP 5 — UPDATE VERSION IN CSPROJ

Update the <Version> property in:
  src\DesktopHub.UI\DesktopHub.UI.csproj

Inside the <PropertyGroup> block:
  <Version>X.Y.Z</Version>

Then run the version update script:
  .\scripts\update-version.ps1 "<new_version>" "<short comma-separated summary>"

Example:
  .\scripts\update-version.ps1 "1.9.0" "Firebase sync, role editor, feeder schedules"

---

## STEP 6 — UPDATE WHAT'S NEW

Update the in-app "What's New" section with accurate bullet points
reflecting the actual changes since the last tag.
Do not reuse bullets from a previous release.
Do not invent features that were not actually added.

---

## STEP 7 — GENERATE GITHUB RELEASE MARKDOWN

Produce a release notes file using this exact structure:

---
# Version X.Y.Z - [Short descriptive headline]

## 🎉 New Features

### [Feature Area Name]

- Bullet describing the feature
- Bullet describing the feature

## 🎨 Improvements

### [Improvement Area Name]

- Bullet describing the improvement

## 📌 Notes

- Version bump type and reasoning
- Key implementation details worth noting
- Admin or deployment instructions if applicable

## Assets

- `DesktopHub.exe` — single-file self-contained build
---

All bullets must reflect actual changes found in Step 3.
Save the file to: releases\v<new_version>.md

---

## STEP 8 — BUILD RELEASE ARTIFACT

Run the publish command with the versioned output folder:

  dotnet publish src\DesktopHub.UI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o "src\DesktopHub.UI\bin\Release\net8.0-windows\win-x64\publish-v<new_version>"

Do not proceed to git steps if this command fails.

---

## STEP 9 — GIT COMMIT, TAG, AND PUSH

Run these in order only after the build succeeds:

  git add -A
  git commit -m "release: v<new_version>"
  git tag v<new_version>
  git push origin main --tags

---

## REQUIRED OUTPUT FOR EVERY RELEASE

Before executing Steps 8 and 9, always present:

1. Previous version
2. Proposed new version
3. Release type (patch / minor / major) and why
4. Summary of changes since last tag
5. Updated What's New bullets
6. Full GitHub release markdown
7. Exact commands that will be run
8. Any blockers, ambiguities, or missing information

---

## SAFETY — NEVER DO THESE

- Never skip the git log inspection
- Never guess the version bump without reading commit history
- Never run dotnet publish from a subdirectory
- Never push the tag before the build confirms success
- Never write release notes that include features not in the diff
- Never run environment setup steps out of order
- Never assume the environment is already configured