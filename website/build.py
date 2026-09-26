"""Builds the YouLoader website into website/public.

    python website/build.py

The site is hosted on Cloudflare Pages. Set SITE_URL to the address it's served from, e.g.
    SITE_URL=https://youloader.org python website/build.py
In Cloudflare Pages: build command "python website/build.py", output directory "website/public",
and an environment variable SITE_URL. Everything that depends on the address comes from it:
canonical links, the sitemap, social cards, and a path prefix if the site ever lives in a subfolder.
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
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parent
PAGES = ROOT / "pages"
STATIC = ROOT / "static"
OUT = ROOT / "public"

DEFAULT_SITE_URL = "https://youloader.pages.dev"
SITE_URL = os.environ.get("SITE_URL") or DEFAULT_SITE_URL
SITE_URL = SITE_URL.rstrip("/")

# "" on a domain of its own, or e.g. "/YouLoader" if the site is served from a subfolder. Pages are written with root-relative links
# ("/assets/..."), and this prefix is added to them when the site is built.
BASE_PATH = urlparse(SITE_URL).path.rstrip("/")


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
    render_faq_block: bool = True  # False when the page shows its questions its own way


# ---------------------------------------------------------------- FAQ content

HOME_FAQ = [
    ("Is YouLoader really free?",
     "<p>Yes. There's no trial, no premium tier, no ads and nothing to buy inside the app. "
     "It's open source under the MIT license. There's nothing to pay for, and nobody to donate to.</p>"),
    ("Is it safe? Why does Windows show a warning?",
     f"<p>All the code is public on <a href=\"{GITHUB}\">GitHub</a>, and every release is built automatically from that code by GitHub Actions. "
     "You can compare the file's SHA-256 checksum with the one on the release page.</p>"
     "<p>YouLoader isn't code-signed yet, so Windows SmartScreen may say “Windows protected your PC” the first time. "
     "Click <strong>More info</strong>, then <strong>Run anyway</strong>. Code signing is planned through a free program for open-source projects.</p>"),
    ("Why does it download yt-dlp and ffmpeg the first time it starts?",
     "<p>YouLoader is the friendly front end. The heavy lifting is done by two respected open-source tools: "
     "<a href=\"https://github.com/yt-dlp/yt-dlp\">yt-dlp</a> fetches the media and <a href=\"https://ffmpeg.org\">ffmpeg</a> converts it. "
     "YouLoader downloads their official builds once, then keeps yt-dlp updated automatically, because YouTube changes often.</p>"),
    ("Which MP3 quality should I pick?",
     "<p>320 kbps, the default. It's the highest quality MP3 has, and it sounds the same as YouTube to virtually everyone. "
     "If you keep a big library on a small device, the ~245 kbps setting saves about a quarter of the space, and almost nobody can hear the difference.</p>"),
    ("Can it download whole playlists and channels?",
     "<p>Yes. Playlist and channel links download everything into their own numbered folder. "
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

# ---------------------------------------------------------------- Common problems

# website/data/problems.json is exported from the app's own error explanations (see ProblemCatalogueTests),
# so the page always says exactly what the app says.
PROBLEMS_FILE = ROOT / "data" / "problems.json"

PROBLEM_SECTIONS = [
    ("start", "Before you start",
     "Something that happens before YouLoader even opens."),
    ("video", "This video can't be downloaded",
     "Some videos are locked on YouTube's side. These aren't bugs: YouLoader can only download what you could watch in a browser without signing in."),
    ("youtube", "YouTube is blocking or limiting downloads",
     "YouTube changes how it delivers videos from time to time and limits connections that download a lot. Most of these fix themselves."),
    ("saving", "Saving the file",
     "The download worked, but Windows wouldn't let YouLoader save it."),
    ("converting", "Converting to MP3 or MP4",
     "YouLoader uses a converter called ffmpeg to turn YouTube's streams into MP3 or MP4 files."),
    ("connection", "Connection problems", ""),
    ("app", "YouLoader itself", ""),
]

WEBSITE_ONLY_PROBLEMS = [
    {
        "id": "windows-protected-your-pc",
        "category": "start",
        "summary": "Windows protected your PC",
        "cause": "Windows SmartScreen shows this for programs it hasn't seen many people download yet, and for programs that "
                 "aren't code-signed. YouLoader isn't signed yet. The warning isn't about anything the program does: all the code is public on GitHub.",
        "suggestions": [
            "Click “More info”, then “Run anyway”.",
            "To double-check the file, compare its SHA-256 checksum with the one listed on the GitHub release page.",
        ],
    },
]


def load_problems() -> list[dict]:
    return WEBSITE_ONLY_PROBLEMS + json.loads(PROBLEMS_FILE.read_text(encoding="utf-8"))


def problems_by_section(problems: list[dict]) -> list[tuple[str, str, str, list[dict]]]:
    known = {key for key, _, _ in PROBLEM_SECTIONS}
    unknown = {p["category"] for p in problems} - known
    if unknown:
        raise SystemExit(f"problems.json has sections the website doesn't know: {unknown}")
    return [(key, title, intro, [p for p in problems if p["category"] == key])
            for key, title, intro in PROBLEM_SECTIONS]


def render_problem_index(problems: list[dict]) -> str:
    return "\n".join(
        f'      <a href="#section-{key}">{html.escape(title)} <span>{len(items)}</span></a>'
        for key, title, _, items in problems_by_section(problems) if items
    )


def render_problems(problems: list[dict]) -> str:
    parts = []
    for key, title, intro, items in problems_by_section(problems):
        if not items:
            continue
        parts.append(f'    <section class="problem-section" id="section-{key}">')
        parts.append(f"      <h2>{html.escape(title)}</h2>")
        if intro:
            parts.append(f"      <p>{html.escape(intro)}</p>")
        for p in items:
            steps = "".join(f"<li>{html.escape(s)}</li>" for s in p["suggestions"])
            parts.append(
                f'      <article class="problem" id="{p["id"]}">\n'
                f'        <h3>“{html.escape(p["summary"].rstrip("."))}”</h3>\n'
                f'        <p><strong>Why it happens:</strong> {html.escape(p["cause"])}</p>\n'
                f'        <p class="problem-steps-label">What to do</p>\n'
                f"        <ol>{steps}</ol>\n"
                f"      </article>"
            )
        parts.append("    </section>")
    return "\n".join(parts)


def problems_faq(problems: list[dict]) -> list[tuple[str, str]]:
    """The same content as structured data, so search engines understand each question and answer."""
    return [
        (f"What does “{p['summary'].rstrip('.')}” mean in YouLoader?",
         f"<p>{html.escape(p['cause'])} " + " ".join(html.escape(s) for s in p["suggestions"]) + "</p>")
        for p in problems
    ]


PAGES_LIST = [
    Page("/", "index.html",
         "YouLoader: Free YouTube to MP3 & MP4 Downloader, No Ads",
         "Download YouTube videos and playlists as MP3 or MP4 with a free Windows app. No ads, no pop-ups, no sign-up, no tracking. Open source.",
         faq=HOME_FAQ),
    Page("/youtube-to-mp3/", "youtube-to-mp3.html",
         "YouTube to MP3 Without Ads or Pop-ups · YouLoader",
         "Convert YouTube videos and playlists to 320 kbps MP3 with cover art and tags. A free Windows app with no ads, no pop-ups and no length limits.",
         crumb="YouTube to MP3", faq=MP3_FAQ),
    Page("/youtube-to-mp4/", "youtube-to-mp4.html",
         "YouTube to MP4 in 1080p & 4K, No Ads · YouLoader",
         "Save YouTube videos as MP4 in 480p, 720p, 1080p or up to 4K and 8K. Free Windows app, no watermark, no ads, no pop-ups.",
         crumb="YouTube to MP4", faq=MP4_FAQ),
    Page("/common-problems/", "common-problems.html",
         "YouTube Download Problems and How to Fix Them · YouLoader",
         "Why a YouTube download fails and how to fix it: private or blocked videos, bot checks, full disks, files in use, conversion errors and more.",
         crumb="Common problems", faq=problems_faq(load_problems()), render_faq_block=False),
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
    <a class="brand" href="/" aria-label="YouLoader home"><img src="/assets/logo-64.png" alt="" width="30" height="30"><span>YouLoader</span></a>
    <nav class="nav" aria-label="Main">
      <a class="nav-optional" href="/#formats">Formats</a>
      <a class="nav-optional" href="/#why-free">Why free</a>
      <a class="nav-optional" href="/#faq">FAQ</a>
      <a href="/common-problems/">Help</a>
      <a href="{GITHUB}">GitHub</a>
    </nav>
  </div>
</header>"""

FOOTER = f"""<footer class="site-footer">
  <div class="wrap">
    <div class="footer-grid">
      <div class="about">
        <a class="brand" href="/"><img src="/assets/logo-64.png" alt="" width="30" height="30" loading="lazy"><span>YouLoader</span></a>
        <p>A free, open-source YouTube downloader for Windows. No ads, no tracking, no nonsense.</p>
      </div>
      <div>
        <h2>Download</h2>
        <ul>
          <li><a href="/youtube-to-mp3/">YouTube to MP3</a></li>
          <li><a href="/youtube-to-mp4/">YouTube to MP4</a></li>
        </ul>
      </div>
      <div>
        <h2>Learn</h2>
        <ul>
          <li><a href="/#faq">FAQ</a></li>
          <li><a href="/common-problems/">Common problems</a></li>
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
        "{{version}}": VERSION,
        "{{faq}}": render_faq(page.faq) if page.render_faq_block else "",
        "{{problem_index}}": render_problem_index(load_problems()) if "{{problem_index}}" in body else "",
        "{{problems}}": render_problems(load_problems()) if "{{problems}}" in body else "",
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
<meta http-equiv="Content-Security-Policy" content="default-src 'self'; img-src 'self' data:; style-src 'self'; font-src 'self'; script-src 'none'; base-uri 'none'; form-action 'none'">
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


ROOT_RELATIVE = re.compile(r'(?P<attr>\b(?:href|src|srcset)=")/(?!/)')
CSS_ROOT_RELATIVE = re.compile(r'url\("/(?!/)')
SRCSET = re.compile(r'srcset="[^"]*"')


def with_base_path(text: str) -> str:
    """Points root-relative links at the site's folder, e.g. /assets/x.png -> /YouLoader/assets/x.png."""
    if not BASE_PATH:
        return text
    text = ROOT_RELATIVE.sub(lambda m: f'{m.group("attr")}{BASE_PATH}/', text)
    text = SRCSET.sub(lambda m: m.group(0).replace(", /", f", {BASE_PATH}/"), text)
    return CSS_ROOT_RELATIVE.sub(f'url("{BASE_PATH}/', text)


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
    # Empty the folder rather than deleting it, so a local preview server can keep serving from it.
    OUT.mkdir(exist_ok=True)
    for child in OUT.iterdir():
        shutil.rmtree(child) if child.is_dir() else child.unlink()
    shutil.copytree(STATIC, OUT, dirs_exist_ok=True)

    css_version = hashlib.sha256((STATIC / "styles.css").read_bytes()).hexdigest()[:10]

    for page in PAGES_LIST:
        target = OUT / "404.html" if page.path == "/404.html" else OUT / page.path.strip("/") / "index.html"
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(with_base_path(render(page, css_version)), encoding="utf-8")

    styles = OUT / "styles.css"
    styles.write_text(with_base_path(styles.read_text(encoding="utf-8")), encoding="utf-8")

    (OUT / "sitemap.xml").write_text(sitemap(PAGES_LIST), encoding="utf-8")
    (OUT / "robots.txt").write_text(robots(), encoding="utf-8")
    print(f"Built {len(PAGES_LIST)} pages into {OUT} for {SITE_URL}")


if __name__ == "__main__":
    build()
