---
description: Full DesktopHub release: env setup, version bump, what's new, publish build, commit, tag, push
---
Execute a full DesktopHub release following the DesktopHub Release Workflow rule exactly.

Follow this sequence in order:

PHASE 1 — ANALYSIS (do this first, present results before proceeding)

1. Confirm working directory is desktop-widgets\DesktopHub\

2. Find the most recent git version tag:
     git describe --tags --abbrev=0

3. Inspect all commits and changed files since that tag:
     git log <last_tag>..HEAD --oneline
     git diff <last_tag>..HEAD --name-only

4. Summarize changes:
   - New user-facing features
   - Improvements and refactors
   - Bug fixes
   - Admin / deployment changes

5. Determine release type: PATCH, MINOR, or MAJOR

6. Propose new version number

7. Draft GitHub release markdown

8. Draft updated What's New bullets

STOP HERE and present the following before doing anything else:
  - Previous version
  - Proposed new version
  - Release type and reasoning
  - Change summary
  - Full GitHub release markdown
  - What's New bullets
  - All exact commands that will be run

Wait for confirmation to proceed to Phase 2.

---

PHASE 2 — EXECUTION (only after Phase 1 is confirmed)

9. Set up the environment:
     $env:PATH = "C:\dotnet;$env:PATH"
     Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
     .\scripts\dev-env.ps1

10. Update <Version> in src\DesktopHub.UI\DesktopHub.UI.csproj

11. Run version script:
      .\scripts\update-version.ps1 "<new_version>" "<short summary>"

12. Update the in-app What's New section

13. Save release markdown to releases\v<new_version>.md

14. Run publish:
      dotnet publish src\DesktopHub.UI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o "src\DesktopHub.UI\bin\Release\net8.0-windows\win-x64\publish-v<new_version>"

    If the publish fails, STOP and report the error. Do not proceed.

15. Run git sequence:
      git add -A
      git commit -m "release: v<new_version>"
      git tag v<new_version>
      git push origin main --tags

16. Confirm completion and report:
    - New version live
    - Tag pushed
    - Release notes location
    - Build artifact location