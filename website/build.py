"""Builds the YouLoader website into website/public.

    python website/build.py

Set SITE_URL to the real domain before deploying, e.g.
    SITE_URL=https://youloader.app python website/build.py
Everything that depends on the domain (canonical links, sitemap, social cards) comes from it.
"""

from __future__ import annotations

import hashlib
import html
import json
import os
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass, field
from datetime import date
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PAGES = ROOT / "pages"
STATIC = ROOT / "static"
OUT = ROOT / "public"

PLACEHOLDER_URL = "https://youloader.example"
SITE_URL = os.environ.get("SITE_URL", PLACEHOLDER_URL).rstrip("/")


def app_version() -> str:
    """The version from the app's project file, so there's one place to bump per release."""
    csproj = (ROOT.parent / "src" / "YouLoader" / "YouLoader.csproj").read_text(encoding="utf-8")
    match = re.search(r"<Version>([^<]+)</Version>", csproj)
    if not match:
        raise SystemExit("No <Version> in YouLoader.csproj")
    return match.group(1)


VERSION = app_version()
GITHUB = "https://github.com/SmileyBoy321/YouLoader"
DOWNLOAD_URL = f"{GITHUB}/releases/latest/download/YouLoader.exe"
RELEASES_URL = f"{GITHUB}/releases"
DONATE_URL = "https://ko-fi.com/smileyboyy"
SITE_NAME = "YouLoader"


@dataclass
class Page:
    path: str  # URL path, "/" or "/name/"
    source: str  # file in pages/
    title: str
    description: str
    crumb: str = ""
    faq: list[tuple[str, str]] = field(default_factory=list)
    in_sitemap: bool = True
    noindex: bool = False


# ---------------------------------------------------------------- FAQ content

HOME_FAQ = [
    ("Is YouLoader really free?",
     "<p>Yes. There's no trial, no premium tier, no ads and nothing to buy inside the app. "
     "It's open source under the MIT license. Donations are welcome but never required.</p>"),
    ("Is it safe? Why does Windows show a warning?",
     f"<p>All the code is public on <a href=\"{GITHUB}\">GitHub</a>, and every release is built automatically from that code by GitHub Actions. "
     "You can compare the file's SHA-256 checksum with the one on the release page.</p>"
     "<p>YouLoader isn't code-signed yet, so Windows SmartScreen may say “Windows protected your PC” the first time. "
     "Click <strong>More info</strong>, then <strong>Run anyway</strong>. A signing certificate is first on the donation wishlist.</p>"),
    ("Why does it download yt-dlp and ffmpeg the first time it starts?",
     "<p>YouLoader is the friendly front end. The heavy lifting is done by two respected open-source tools: "
     "<a href=\"https://github.com/yt-dlp/yt-dlp\">yt-dlp</a> fetches the media and <a href=\"https://ffmpeg.org\">ffmpeg</a> converts it. "
     "YouLoader downloads their official builds once, then keeps yt-dlp updated automatically, because YouTube changes often.</p>"),
    ("Should I pick Opus or MP3?",
     "<p>Opus, unless you play music on an old device or a car stereo. YouTube streams music as Opus, so saving it as Opus is a perfect copy at about 4 MB per song. "
     "MP3 is a conversion: bigger, never better, but it plays everywhere. The <a href=\"/opus-vs-mp3/\">Opus vs MP3 guide</a> explains it in detail.</p>"),
    ("Can it download whole playlists and channels?",
     "<p>Yes. Playlist, channel, SoundCloud set and artist links download everything into their own numbered folder. "
     "Paste the same link again later and YouLoader fetches only what's new.</p>"),
    ("Why isn't there a web version?",
     "<p>Web converters run every download on their own servers. YouTube blocks those servers, and sending files to millions of people costs a lot of money. "
     "That's why those sites are covered in ads and pop-ups. YouLoader runs on your own PC, so it costs nothing to run and needs no ads.</p>"),
    ("Is there a Mac or Linux version?",
     "<p>Windows 10 and 11 for now. The core is written in cross-platform .NET, so macOS and Linux versions are possible. "
     f"Follow the project on <a href=\"{GITHUB}\">GitHub</a> to hear about it first.</p>"),
    ("Is it legal to download videos from YouTube?",
     "<p>YouLoader is a tool, like a browser or a video recorder. Whether a particular download is allowed depends on where you live, "
     "who owns the content and the site's terms of service. YouTube's terms generally don't allow downloading unless YouTube offers a download button "
     "or you have the owner's permission.</p>"
     "<p>Good uses include your own uploads, Creative Commons and public-domain content, and anything you have permission for. Please respect creators.</p>"),
]

MP3_FAQ = [
    ("Can I convert a whole YouTube playlist to MP3?",
     "<p>Yes. Paste the playlist link. Every song is saved into a folder named after the playlist, numbered in order. "
     "Paste it again later and only new songs are downloaded.</p>"),
    ("Does it keep the song title and cover art?",
     "<p>Yes. The title, uploader and date are written into the MP3 tags, and the video thumbnail is cropped to a square and embedded as album art.</p>"),
    ("Is there a length limit?",
     "<p>No. Hour-long mixes and full albums work the same as a three-minute song. There's no queue and no daily limit either.</p>"),
]

MP4_FAQ = [
    ("Can I download YouTube videos in 4K?",
     "<p>Yes. Pick MP4 and “Best available”. YouLoader takes the highest resolution the video offers, including 4K and 8K, and merges it with the best audio into one MP4.</p>"),
    ("Why won't my 4K file play?",
     "<p>4K videos on YouTube use the AV1 or VP9 codec. Windows 11 plays AV1 out of the box. On Windows 10, install the free "
     "“AV1 Video Extension” from the Microsoft Store, or use <a href=\"https://www.videolan.org/\">VLC</a>. If you need a file that plays anywhere, pick 1080p.</p>"),
    ("Does it download subtitles?",
     "<p>Not yet. It's on the list. If you'd like it sooner, say so on GitHub.</p>"),
]

OPUS_FAQ = [
    ("Will my phone play .opus files?",
     "<p>Android plays Opus natively, as do Chrome, Firefox, Edge, VLC and foobar2000. Windows 11's Media Player opens them too. "
     "For iPhones, older MP3 players and car stereos, pick MP3 instead.</p>"),
    ("Is Opus from YouLoader really lossless?",
     "<p>It's an exact copy of the audio YouTube sends, not a lossless master. Nothing is re-encoded, so no quality is lost on your side. "
     "It's the best version of the audio that YouTube makes available.</p>"),
]

SOUNDCLOUD_FAQ = [
    ("Can I download a whole SoundCloud playlist or album?",
     "<p>Yes. Paste a set, album, artist or likes link. Tracks are saved into their own folder, and running the same link again grabs only new tracks.</p>"),
    ("Why is the quality 128 kbps?",
     "<p>That's what SoundCloud streams for most tracks. No downloader can get more than the site sends. "
     "If the artist enabled SoundCloud's own Download button, use it: it's often the original file, and it supports the artist directly.</p>"),
]

PAGES_LIST = [
    Page("/", "index.html",
         "YouLoader: Free YouTube to MP3 & MP4 Downloader, No Ads",
         "Download YouTube and SoundCloud as Opus, MP3 or MP4 with a free Windows app. No ads, no pop-ups, no sign-up, no tracking. Open source.",
         faq=HOME_FAQ),
    Page("/youtube-to-mp3/", "youtube-to-mp3.html",
         "YouTube to MP3 Without Ads or Pop-ups · YouLoader",
         "Convert YouTube videos and playlists to 320 kbps MP3 with cover art and tags. A free Windows app with no ads, no pop-ups and no length limits.",
         crumb="YouTube to MP3", faq=MP3_FAQ),
    Page("/youtube-to-mp4/", "youtube-to-mp4.html",
         "YouTube to MP4 in 1080p & 4K, No Ads · YouLoader",
         "Save YouTube videos as MP4 in 480p, 720p, 1080p or up to 4K and 8K. Free Windows app, no watermark, no ads, no pop-ups.",
         crumb="YouTube to MP4", faq=MP4_FAQ),
    Page("/youtube-to-opus/", "youtube-to-opus.html",
         "YouTube to Opus: Best Quality, Smallest Files · YouLoader",
         "Save YouTube audio as Opus: an exact copy of the stream YouTube plays, with no re-encoding, at about 4 MB per song. Free and ad-free.",
         crumb="YouTube to Opus", faq=OPUS_FAQ),
    Page("/soundcloud-to-mp3/", "soundcloud-to-mp3.html",
         "SoundCloud to MP3 Downloader, Free & Ad-Free · YouLoader",
         "Download SoundCloud tracks, albums, playlists and artist pages as MP3 or Opus. Free Windows app with no ads, no pop-ups and no sign-up.",
         crumb="SoundCloud to MP3", faq=SOUNDCLOUD_FAQ),
    Page("/opus-vs-mp3/", "opus-vs-mp3.html",
         "Opus vs MP3: Which Sounds Better and Takes Less Space?",
         "Opus or MP3? How they compare in quality, file size and compatibility, why YouTube uses Opus, and why downloaded songs can sound louder.",
         crumb="Opus vs MP3"),
    Page("/privacy/", "privacy.html",
         "Privacy · YouLoader",
         "YouLoader has no ads, no analytics and no tracking, on this website or in the app. Here's exactly what connects to what.",
         crumb="Privacy"),
    Page("/404.html", "404.html",
         "Page not found · YouLoader",
         "This page doesn't exist.",
         in_sitemap=False, noindex=True),
]

# ---------------------------------------------------------------- Templates

DOWNLOAD_ICON = (
    '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" '
    'stroke-linejoin="round" aria-hidden="true"><path d="M12 3v12m0 0-5-5m5 5 5-5M4 20h16"/></svg>'
)

DOWNLOAD_BUTTON = (
    f'<a class="btn btn-primary" href="{DOWNLOAD_URL}">{DOWNLOAD_ICON}'
    f'<span>Download for Windows<small>v{VERSION} · Free · Windows 10 &amp; 11</small></span></a>'
)

HEADER = f"""<a class="skip" href="#main">Skip to content</a>
<header class="site-header">
  <div class="wrap">
    <a class="brand" href="/" aria-label="YouLoader home"><img src="/assets/logo-512.png" alt="" width="30" height="30"><span>YouLoader</span></a>
    <nav class="nav" aria-label="Main">
      <a class="nav-optional" href="/#formats">Formats</a>
      <a class="nav-optional" href="/#why-free">Why free</a>
      <a class="nav-optional" href="/#faq">FAQ</a>
      <a href="{GITHUB}">GitHub</a>
      <a class="nav-donate" href="{DONATE_URL}">♥ Donate</a>
    </nav>
  </div>
</header>"""

FOOTER = f"""<footer class="site-footer">
  <div class="wrap">
    <div class="footer-grid">
      <div class="about">
        <a class="brand" href="/"><img src="/assets/logo-512.png" alt="" width="30" height="30" loading="lazy"><span>YouLoader</span></a>
        <p>A free, open-source YouTube and SoundCloud downloader for Windows. No ads, no tracking, no nonsense.</p>
      </div>
      <div>
        <h2>Download</h2>
        <ul>
          <li><a href="/youtube-to-mp3/">YouTube to MP3</a></li>
          <li><a href="/youtube-to-mp4/">YouTube to MP4</a></li>
          <li><a href="/youtube-to-opus/">YouTube to Opus</a></li>
          <li><a href="/soundcloud-to-mp3/">SoundCloud to MP3</a></li>
        </ul>
      </div>
      <div>
        <h2>Learn</h2>
        <ul>
          <li><a href="/opus-vs-mp3/">Opus vs MP3</a></li>
          <li><a href="/#faq">FAQ</a></li>
          <li><a href="/#why-free">Why it's free</a></li>
          <li><a href="/privacy/">Privacy</a></li>
        </ul>
      </div>
      <div>
        <h2>Project</h2>
        <ul>
          <li><a href="{GITHUB}">Source code</a></li>
          <li><a href="{RELEASES_URL}">All releases</a></li>
          <li><a href="{GITHUB}/issues">Report a problem</a></li>
          <li><a href="{DONATE_URL}">Donate</a></li>
        </ul>
      </div>
    </div>
    <div class="fineprint">
      <span>MIT licensed · No cookies · No analytics · Made in Estonia</span>
      <span>Only download content you own or have permission to use.</span>
    </div>
  </div>
</footer>"""


def page_url(page: Page) -> str:
    return SITE_URL + page.path


def strip_tags(text: str) -> str:
    return html.unescape(re.sub(r"<[^>]+>", "", text))


def render_faq(items: list[tuple[str, str]]) -> str:
    return "\n".join(
        f'<details>\n  <summary>{html.escape(q)}</summary>\n  <div class="answer">{a}</div>\n</details>'
        for q, a in items
    )


def structured_data(page: Page) -> list[dict]:
    data: list[dict] = []
    if page.path == "/":
        data.append({
            "@context": "https://schema.org",
            "@type": "SoftwareApplication",
            "name": SITE_NAME,
            "url": SITE_URL + "/",
            "description": page.description,
            "applicationCategory": "MultimediaApplication",
            "operatingSystem": "Windows 10, Windows 11",
            "softwareVersion": VERSION,
            "downloadUrl": DOWNLOAD_URL,
            "license": "https://opensource.org/licenses/MIT",
            "isAccessibleForFree": True,
            "offers": {"@type": "Offer", "price": "0", "priceCurrency": "USD"},
            "image": SITE_URL + "/assets/logo-512.png",
            "screenshot": SITE_URL + "/assets/screenshot.png",
            "sameAs": [GITHUB],
        })
        data.append({
            "@context": "https://schema.org",
            "@type": "WebSite",
            "name": SITE_NAME,
            "url": SITE_URL + "/",
        })
    elif page.crumb:
        data.append({
            "@context": "https://schema.org",
            "@type": "BreadcrumbList",
            "itemListElement": [
                {"@type": "ListItem", "position": 1, "name": SITE_NAME, "item": SITE_URL + "/"},
                {"@type": "ListItem", "position": 2, "name": page.crumb, "item": page_url(page)},
            ],
        })
    if page.faq:
        data.append({
            "@context": "https://schema.org",
            "@type": "FAQPage",
            "mainEntity": [
                {"@type": "Question", "name": q, "acceptedAnswer": {"@type": "Answer", "text": strip_tags(a)}}
                for q, a in page.faq
            ],
        })
    return data


def render(page: Page, css_version: str) -> str:
    body = (PAGES / page.source).read_text(encoding="utf-8")
    replacements = {
        "{{download_button}}": DOWNLOAD_BUTTON,
        "{{download_url}}": DOWNLOAD_URL,
        "{{github}}": GITHUB,
        "{{releases}}": RELEASES_URL,
        "{{donate}}": DONATE_URL,
        "{{version}}": VERSION,
        "{{faq}}": render_faq(page.faq),
        "{{crumbs}}": f'<nav class="crumbs" aria-label="Breadcrumb"><a href="/">YouLoader</a> / {html.escape(page.crumb)}</nav>',
    }
    for token, value in replacements.items():
        body = body.replace(token, value)
    leftover = re.findall(r"\{\{\w+\}\}", body)
    if leftover:
        raise SystemExit(f"{page.source}: unknown tokens {leftover}")

    title = html.escape(page.title)
    description = html.escape(page.description)
    url = page_url(page)
    robots = "noindex" if page.noindex else "index,follow,max-image-preview:large"
    schema = "\n".join(
        f'<script type="application/ld+json">{json.dumps(item, ensure_ascii=False)}</script>'
        for item in structured_data(page)
    )
    canonical = "" if page.noindex else f'<link rel="canonical" href="{url}">\n'

    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<meta name="description" content="{description}">
<meta name="robots" content="{robots}">
{canonical}<meta name="color-scheme" content="light dark">
<meta name="theme-color" content="#f3efe6" media="(prefers-color-scheme: light)">
<meta name="theme-color" content="#13120f" media="(prefers-color-scheme: dark)">
<link rel="icon" href="/favicon.ico" sizes="any">
<link rel="apple-touch-icon" href="/apple-touch-icon.png">
<link rel="preload" href="/assets/fonts/instrument-serif.woff2" as="font" type="font/woff2" crossorigin>
<link rel="preload" href="/assets/fonts/geist.woff2" as="font" type="font/woff2" crossorigin>
<link rel="stylesheet" href="/styles.css?v={css_version}">
<meta property="og:type" content="website">
<meta property="og:site_name" content="{SITE_NAME}">
<meta property="og:title" content="{title}">
<meta property="og:description" content="{description}">
<meta property="og:url" content="{url}">
<meta property="og:image" content="{SITE_URL}/assets/og.png">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">
<meta property="og:image:alt" content="YouLoader: Downloads. Nothing else.">
<meta name="twitter:card" content="summary_large_image">
{schema}
</head>
<body>
{HEADER}
{body.strip()}
{FOOTER}
</body>
</html>
"""


def last_modified(source: Path) -> str:
    """Date of the last commit that touched a page, so the sitemap only changes when content does."""
    try:
        out = subprocess.run(
            ["git", "log", "-1", "--format=%cs", "--", str(source)],
            capture_output=True, text=True, cwd=ROOT, check=True,
        ).stdout.strip()
        if out:
            return out
    except (OSError, subprocess.CalledProcessError):
        pass
    return date.today().isoformat()


def sitemap(pages: list[Page]) -> str:
    entries = "\n".join(
        f"  <url><loc>{page_url(p)}</loc><lastmod>{last_modified(PAGES / p.source)}</lastmod></url>"
        for p in pages if p.in_sitemap
    )
    return f'<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n{entries}\n</urlset>\n'


def robots() -> str:
    return f"User-agent: *\nAllow: /\n\nSitemap: {SITE_URL}/sitemap.xml\n"


def build() -> None:
    if SITE_URL == PLACEHOLDER_URL:
        print(f"warning: SITE_URL is not set; using {PLACEHOLDER_URL}. Set it to the real domain before deploying.", file=sys.stderr)

    # Empty the folder rather than deleting it, so a local preview server can keep serving from it.
    OUT.mkdir(exist_ok=True)
    for child in OUT.iterdir():
        shutil.rmtree(child) if child.is_dir() else child.unlink()
    shutil.copytree(STATIC, OUT, dirs_exist_ok=True)

    css_version = hashlib.sha256((STATIC / "styles.css").read_bytes()).hexdigest()[:10]

    for page in PAGES_LIST:
        target = OUT / "404.html" if page.path == "/404.html" else OUT / page.path.strip("/") / "index.html"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(render(page, css_version), encoding="utf-8")

    (OUT / "sitemap.xml").write_text(sitemap(PAGES_LIST), encoding="utf-8")
    (OUT / "robots.txt").write_text(robots(), encoding="utf-8")
    print(f"Built {len(PAGES_LIST)} pages into {OUT} for {SITE_URL}")


if __name__ == "__main__":
    build()
