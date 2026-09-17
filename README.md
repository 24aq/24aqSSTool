# 24aq SS Tools — Hacker Green v3

A Windows WPF launcher for organizing screenshare/forensics utilities.

## Main changes in v3

- Full black + hacker-green redesign
- 24aq branding
- Search box
- Hover effects on tool cards
- Terminal-style activity console
- ONLINE / status indicators
- Fixed missing `System.IO` imports
- Fixed XAML `&amp;` issue
- `.gitignore` included
- Targets `.NET 10` for Visual Studio 2026
- Safe PowerShell bootstrap template included
- No remote `Invoke-Expression`

## Run it

1. Open `24aqSSTool.csproj` in Visual Studio 2026.
2. Make sure `.NET desktop development` is installed.
3. Build -> Rebuild Solution.
4. Start with F5 or Ctrl+F5.

## Add a tool

Edit `tools.json`.

Direct file:

```json
{
  "name": "Example",
  "description": "Example forensic utility",
  "category": "Others",
  "sourceType": "GH",
  "downloadUrl": "https://github.com/OWNER/REPO/releases/download/v1.0/Example.exe",
  "fileName": "Example.exe",
  "sha256": "",
  "homePage": "https://github.com/OWNER/REPO"
}
```

If you only want the card to open the project's website, leave `downloadUrl` empty and set `homePage`.

## GitHub PowerShell launcher

`24aqSSTool.ps1` is included as a safer bootstrap template.

After publishing your EXE as a GitHub Release:
1. Replace `$DownloadUrl`.
2. Optionally add the release EXE's SHA-256 to `$ExpectedSha256`.
3. Commit the script.

Prefer downloading a fixed release asset rather than using `Invoke-Expression` on remote code.