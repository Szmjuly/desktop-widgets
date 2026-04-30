# Load Extractor v1.0.0

## Local Extractor Release

### Features

- Load HAP Zone Sizing Summary and Space Design Load Summary PDFs.
- Optionally include Air System Sizing Summary data.
- Use single combined PDF mode when the report sections are merged into one file.
- Review combined output by all zones, by system, or by zone.
- Search across room names and systems.
- Copy key output sections to the clipboard.
- Export combined load data to `.xlsx`.

### Build

```powershell
.\scripts\build-single-file.ps1 -Version "1.0.0"
```

### Notes

- The application is local-only and does not contact remote services at startup or during normal use.
- Runtime behavior is limited to reading selected PDFs, writing local logs, and exporting local Excel files.
