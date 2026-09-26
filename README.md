<div align="center">

<img src="src/YouLoader/Assets/logo.png" width="96" alt="YouLoader logo">

# YouLoader

**Downloads. Nothing else.**

A free, open-source YouTube downloader for Windows.<br>
MP3 and MP4, with no ads, no pop-ups, no sign-up and no tracking.

[![Download](https://img.shields.io/github/v/release/SmileyBoy321/YouLoader?label=Download&style=for-the-badge&color=e8372c)](https://github.com/SmileyBoy321/YouLoader/releases/latest/download/YouLoader.exe)
[![CI](https://img.shields.io/github/actions/workflow/status/SmileyBoy321/YouLoader/ci.yml?branch=master&style=for-the-badge&label=tests)](https://github.com/SmileyBoy321/YouLoader/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-16140f?style=for-the-badge)](LICENSE)

<img src="docs/screenshot.png" width="620" alt="The YouLoader window with MP3 and MP4 format choices and a download list">

</div>

## Why

Search for “YouTube to MP3” and you get sites full of pop-unders, fake download buttons and “allow notifications” traps. YouLoader is the opposite: a small app that runs on your own PC, does one job well, and asks for nothing in return.

It follows the example of [VLC](https://www.videolan.org/): **free forever, no ads, no tracking, open source.** There's nothing to buy and nothing to donate to. If it's useful, star the repo or tell a friend.

## Features

- **MP3 and MP4.** MP3 up to 320 kbps, which plays on every device and car stereo. MP4 from 480p up to 4K and 8K.
- **Playlists and channels** download into their own numbered folder. Paste the same link later and only new items are fetched.
- **Cover art and tags** are embedded automatically, with thumbnails cropped square for music players.
- **Several links at once**, two downloads in parallel, cancel and retry per item, and automatic retries when YouTube drops a connection.
- **No accidental duplicates.** Pasting a video that's already in the list (however the link is written) is skipped, with a note saying so.
- **Calm progress.** Speed and time left are averaged over a few seconds, so they count down smoothly instead of jumping around.
- **Keeps working when YouTube changes.** YouLoader updates its download engine (yt-dlp) automatically once a day.
- **Portable.** One `.exe`, nothing to install.

## Download

Get **[YouLoader.exe](https://github.com/SmileyBoy321/YouLoader/releases/latest/download/YouLoader.exe)** from the [latest release](https://github.com/SmileyBoy321/YouLoader/releases/latest). It runs on Windows 10 and 11 (64-bit).

On first launch it downloads the official builds of [yt-dlp](https://github.com/yt-dlp/yt-dlp) and [ffmpeg](https://ffmpeg.org/), and [Deno](https://deno.com/) if no JavaScript runtime is installed. It keeps them in `%LocalAppData%\YouLoader\tools`.

> Windows may say *“Windows protected your PC”* because the app isn't code-signed yet. Click **More info → Run anyway**. Each release lists the file's SHA-256 checksum so you can verify it.

## Which format should I pick?

| | MP3 | MP4 |
|---|---|---|
| **What** | Music | Video + audio |
| **Quality** | 320, ~245 or 192 kbps | 480p up to 4K/8K |
| **4-minute song** | ≈ 9.6 MB at 320 kbps | depends on resolution |
| **Plays on** | Everything, car stereos too | Everything at 1080p |

MP3 at 320 kbps sounds the same as YouTube to virtually everyone. For video that has to play on older TVs or editing software, pick 1080p.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
git clone https://github.com/SmileyBoy321/YouLoader.git
cd YouLoader
dotnet run --project src/YouLoader                  # run it
dotnet test                                         # unit tests
$env:YOULOADER_INTEGRATION = "1"; dotnet test       # plus real downloads from YouTube
dotnet publish src/YouLoader -c Release -o dist     # portable dist/YouLoader.exe
```

| Folder | What's inside |
|---|---|
| `src/YouLoader.Core` | Everything testable: yt-dlp arguments, output parsing, tool management, the download queue model |
| `src/YouLoader` | The WPF window |
| `tests/YouLoader.Tests` | Unit tests, plus integration tests that run real downloads |
| `website` | The landing page: `python website/build.py` builds it into `website/public`, which Cloudflare Pages serves |

## Releasing

Bump `<Version>` in `src/YouLoader/YouLoader.csproj`, commit, then tag and push:

```powershell
git tag v2.0.1
git push origin v2.0.1
```

GitHub Actions runs the tests, builds `YouLoader.exe` and publishes it with its checksum. The website's download button always points at the newest release.

## Please download responsibly

YouLoader is a tool, like a browser or a video recorder. Only download content you own, content that's openly licensed (such as Creative Commons or public domain), or content you have permission to download. Respect creators and each site's terms of service.

## License

[MIT](LICENSE). YouLoader relies on [yt-dlp](https://github.com/yt-dlp/yt-dlp) (Unlicense) and [ffmpeg](https://ffmpeg.org/) (GPL build), which it downloads separately at runtime.
