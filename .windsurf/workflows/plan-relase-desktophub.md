---
description: Inspect changes since last tag, propose version bump, and draft release notes — no file changes or git actions
---
Plan a DesktopHub release without making any changes or running any git or build commands.

Follow these steps:

1. Confirm the working directory is desktop-widgets\DesktopHub\

2. Find the most recent git version tag:
     git describe --tags --abbrev=0

3. Review all commits since that tag:
     git log <last_tag>..HEAD --oneline

4. Review all changed files:
     git diff <last_tag>..HEAD --name-only

5. Summarize all changes grouped into:
   - New user-facing features
   - Improvements and refactors
   - Bug fixes
   - Admin / deployment / infrastructure changes

6. Decide if this is a PATCH, MINOR, or MAJOR release and explain why.

7. Propose the next version number.

8. Draft the full GitHub release markdown using this structure:

   # Version X.Y.Z - [Short headline]

   ## 🎉 New Features
   ### [Area]
   - bullets

   ## 🎨 Improvements
   ### [Area]
   - bullets

   ## 📌 Notes
   - bullets

   ## Assets
   - `DesktopHub.exe` — single-file self-contained build

9. Draft the updated What's New bullets for in-app display.

10. Show the exact commands that WOULD be run for the full release,
    but do not execute them.

Stop here. Do not modify any files. Do not run any build or git commands.
If anything is unclear from the repo state, say what is uncertain rather than guessing.