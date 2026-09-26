<div align="center">

<img src="src/YouLoader/Assets/logo.png" width="104" alt="YouLoader logo">

# YouLoader

### Downloads. Nothing else.

A free, open-source YouTube downloader for Windows.<br>
MP3 and MP4, with **no ads, no pop-ups, no sign-up and no tracking**.

[![Latest release](https://img.shields.io/github/v/release/SmileyBoy321/YouLoader?style=flat-square&label=release&color=d43127)](https://github.com/SmileyBoy321/YouLoader/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/SmileyBoy321/YouLoader/total?style=flat-square&color=16140f)](https://github.com/SmileyBoy321/YouLoader/releases)
[![Tests](https://img.shields.io/github/actions/workflow/status/SmileyBoy321/YouLoader/ci.yml?branch=master&style=flat-square&label=tests)](https://github.com/SmileyBoy321/YouLoader/actions/workflows/ci.yml)
[![Windows 10 & 11](https://img.shields.io/badge/Windows-10%20%26%2011-0078D4?style=flat-square)](#quick-start)
[![License: MIT](https://img.shields.io/badge/license-MIT-16140f?style=flat-square)](LICENSE)

### [⬇&nbsp; Download YouLoader.exe](https://github.com/SmileyBoy321/YouLoader/releases/latest/download/YouLoader.exe)

<sub>One portable file · nothing to install · about 64 MB</sub>

<br>

<img src="docs/screenshot.png" width="620" alt="The YouLoader window: a box for YouTube links, MP3 and MP4 choices with quality options, and a download list with progress, speed and time left">

</div>

<br>

## Contents

- [Why YouLoader](#why-youloader)
- [Features](#features)
- [Quick start](#quick-start)
- [Picking a format and quality](#picking-a-format-and-quality)
- [FAQ](#faq)
- [Privacy](#privacy)
- [Building from source](#building-from-source)
- [Contributing](#contributing)
- [Acknowledgements](#acknowledgements)

## Why YouLoader

Search for “YouTube to MP3” and you land on sites full of pop-unders, fake download buttons and “allow notifications” traps. They need all that to pay for servers that YouTube keeps blocking. YouLoader runs on your own PC instead, so it costs nothing to run and needs nothing from you.

|                              | Typical converter site          | YouLoader                              |
| ---------------------------- | ------------------------------- | -------------------------------------- |
| Ads, pop-ups, redirects      | Everywhere                      | **None, ever**                         |
| Fake “Download” buttons      | Common                          | **None**                               |
| Account or notifications     | Often pushed on you             | **Never asked**                        |
| Quality                      | Often capped at 128 kbps / 720p | **320 kbps MP3, video up to 4K/8K**    |
| Whole playlists and channels | Rarely                          | **Yes, into numbered folders**         |
| Length limits and queues     | Common                          | **None**                               |
| Tracking                     | Ad networks and analytics       | **None; the code is public**           |

It follows the example of [VLC](https://www.videolan.org/): **free forever, no ads, no tracking, open source.** There's nothing to buy and nothing to donate to. If it's useful, a ⭐ on this repo helps other people find it.

## Features

- 🎵 **MP3 up to 320 kbps**, with the song title, artist and square cover art filled in.
- 🎬 **MP4 from 480p up to 4K and 8K.** 1080p and below use H.264, which plays on every phone, TV and editor.
- 📚 **Playlists and channels** download into their own numbered folder. Paste the same link later and only new videos are fetched.
- 📋 **Paste many links at once.** A freshly copied YouTube link appears in the box by itself, and duplicates are skipped however the link is written.
- 📊 **Calm, honest progress.** Speed and time left are averaged so they count down smoothly, and whatever is downloading right now stays at the top.
- 💬 **Errors in plain English.** Every failed download has a **Why?** button that explains what happened and what to try, from “the file is open in another program” to “this video is private”.
- 🔄 **Keeps working when YouTube changes.** The download engine, [yt-dlp](https://github.com/yt-dlp/yt-dlp), updates itself once a day, and short hiccups are retried automatically.
- 🧳 **Portable.** One `.exe` that sets up everything it needs on first launch.

## Quick start

1. **[Download YouLoader.exe](https://github.com/SmileyBoy321/YouLoader/releases/latest/download/YouLoader.exe)** and open it. Nothing to install.
2. **Copy a YouTube link.** It appears in YouLoader by itself when you switch back, or use **Paste**.
3. **Pick MP3 or MP4 and press Enter.** Files are saved in `Downloads\YouLoader` (change it with **Change…**).

> [!NOTE]
> On first launch YouLoader downloads the official builds of [yt-dlp](https://github.com/yt-dlp/yt-dlp), [ffmpeg](https://ffmpeg.org/) and, if needed, [Deno](https://deno.com/): up to about 200 MB, once. They're kept in `%LocalAppData%\YouLoader\tools`.

> [!WARNING]
> Windows may show **“Windows protected your PC”** because YouLoader isn't code-signed yet. Click **More info → Run anyway**. Every release lists the file's SHA-256 checksum, so you can check the download is exactly what GitHub built from this code.

## Picking a format and quality

**MP3** is for music and plays on everything, car stereos included. **MP4** is for video.

| MP3 quality         | 4-minute song | What it means                                                                                                                                   |
| ------------------- | ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| **320 kbps** (best) | ≈ 9.6 MB      | *kbps* is how much sound is kept per second. 320 is the most MP3 can hold and sounds the same as YouTube.                                      |
| **~245 kbps VBR**   | ≈ 7.3 MB      | *VBR* (variable bitrate) spends more data on busy parts of a song and less on quiet ones. Sounds the same to almost everyone, about 25% smaller. |
| **192 kbps**        | ≈ 5.8 MB      | The smallest files. Fine for podcasts and talk.                                                                                                  |

| MP4 quality         | What you get                                                                                                 |
| ------------------- | ------------------------------------------------------------------------------------------------------------ |
| **Best available**  | The highest resolution the video has, up to 4K/8K. Usually AV1, which needs Windows 11 or VLC on older PCs. |
| **1080p**           | Full HD in H.264. Opens everywhere.                                                                          |
| **720p** / **480p** | Smaller files for phones and slow connections.                                                               |

> [!TIP]
> Downloaded songs can sound **louder** than on YouTube. That's normal: YouTube turns loud songs down while you watch, and a downloaded file keeps the original volume.

## FAQ

<details>
<summary><b>Is it really free?</b></summary>
<br>

Yes. No trial, no premium tier, no ads, nothing to buy and nothing to donate to. It's MIT-licensed open source.
</details>

<details>
<summary><b>Is it safe?</b></summary>
<br>

All the code is in this repository, and every release is built from it by [GitHub Actions](.github/workflows/release.yml), not on someone's PC. YouLoader only ever runs tools it downloaded from the official yt-dlp, ffmpeg and Deno release pages.
</details>

<details>
<summary><b>A download failed. What now?</b></summary>
<br>

Click **Why?** on the failed download. It explains the likely cause and what to try. If it keeps happening, click **Report a problem**: it opens a pre-filled issue here and copies the technical details for you, with your Windows user name hidden.
</details>

<details>
<summary><b>Why isn't there a web version?</b></summary>
<br>

A website has to download every video on its own servers. YouTube blocks those servers, and sending files to millions of people costs a lot of money, which is why converter sites are covered in ads. Running on your own PC avoids all of that.
</details>

<details>
<summary><b>Is there a Mac, Linux or Android version?</b></summary>
<br>

Windows 10 and 11 for now. The core is cross-platform .NET, so other versions are possible. Watch this repo to hear about them.
</details>

<details>
<summary><b>How do I uninstall it?</b></summary>
<br>

Delete `YouLoader.exe`, plus the folders `%AppData%\YouLoader` (settings) and `%LocalAppData%\YouLoader` (helper tools). That's everything.
</details>

## Privacy

- **No accounts, no analytics, no telemetry, no crash reporting.** Nothing about you or your downloads is ever sent anywhere.
- YouLoader connects only to **YouTube** (to download what you ask for) and **GitHub** (to fetch its helper tools, update yt-dlp, and check for a new YouLoader version).
- Settings live in `%AppData%\YouLoader\settings.json` on your PC.

> [!IMPORTANT]
> **Please download responsibly.** Only download videos you own, videos that are openly licensed (for example Creative Commons or public domain), or videos you have permission to download. Respect creators and YouTube's terms of service.

## Building from source

<details>
<summary><b>Build, test and publish</b></summary>
<br>

You need Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/SmileyBoy321/YouLoader.git
cd YouLoader

dotnet run --project src/YouLoader                 # run the app
dotnet test                                        # unit tests
$env:YOULOADER_INTEGRATION = "1"; dotnet test      # plus real downloads from YouTube
dotnet publish src/YouLoader -c Release -o dist    # portable dist\YouLoader.exe
```

| Folder                  | What's inside                                                                                          |
| ----------------------- | ------------------------------------------------------------------------------------------------------ |
| `src/YouLoader.Core`    | Everything testable: yt-dlp arguments, progress parsing, error explanations, tools, the download queue |
| `src/YouLoader`         | The WPF window                                                                                         |
| `tests/YouLoader.Tests` | Unit tests, plus integration tests that run real downloads                                             |
| `website`               | The landing page. `python website/build.py` builds it into `website/public`                            |

The real-download tests are best run on your own PC: YouTube often treats data-centre servers, like GitHub's, as bots.
</details>

<details>
<summary><b>Releasing a new version</b></summary>
<br>

Bump `<Version>` in `src/YouLoader/YouLoader.csproj`, commit, then tag and push:

```powershell
git tag v2.0.1
git push origin v2.0.1
```

GitHub Actions runs the tests, builds `YouLoader.exe` and publishes it with its checksum on the Releases page.
</details>

## Contributing

Bug reports and ideas are very welcome: [open an issue](https://github.com/SmileyBoy321/YouLoader/issues). For code changes, fork the repo, run `dotnet test`, and open a pull request. The one rule that never changes: **no ads, no tracking, no paywalls.**

## Acknowledgements

- [yt-dlp](https://github.com/yt-dlp/yt-dlp) does the downloading, and [FFmpeg](https://ffmpeg.org/) the converting. YouLoader is a friendly front end for their brilliant work.
- [Deno](https://deno.com/) runs the JavaScript YouTube needs, when no other runtime is installed.
- [VLC](https://www.videolan.org/) and VideoLAN, for showing that software can stay free, ad-free and respectful for decades.
- The screenshots and demo use Blender Foundation's open movies [*Big Buck Bunny*](https://peach.blender.org/), [*Sintel*](https://durian.blender.org/) and [*Tears of Steel*](https://mango.blender.org/) (CC BY), and [*Me at the zoo*](https://www.youtube.com/watch?v=jNQXAC9IVRw), the first video on YouTube.

## License

[MIT](LICENSE). YouLoader downloads [yt-dlp](https://github.com/yt-dlp/yt-dlp) (Unlicense), [FFmpeg](https://ffmpeg.org/) (GPL build) and [Deno](https://deno.com/) (MIT) separately at runtime; they are not bundled with it.
