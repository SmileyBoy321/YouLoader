"""Checks the built website for broken links, SEO mistakes and privacy leaks.

    python website/build.py && python website/test_site.py
"""

from __future__ import annotations

import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urlparse

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build  # noqa: E402

PUBLIC = build.OUT
PRIVATE_WORDS_FILE = Path(__file__).resolve().parent / ".private-words"


def forbidden_words() -> list[str]:
    """Placeholder text, plus private words (like a real name) that must never be published.

    Private words come from the PRIVATE_WORDS environment variable (comma-separated, e.g. a CI secret)
    or from website/.private-words (one per line, git-ignored), so the list itself is never published.
    """
    words = ["lorem ipsum", "todo"]
    words += [w for w in os.environ.get("PRIVATE_WORDS", "").split(",")]
    if PRIVATE_WORDS_FILE.exists():
        words += PRIVATE_WORDS_FILE.read_text(encoding="utf-8").splitlines()
    return [w.strip().lower() for w in words if w.strip()]


class PageParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.title = ""
        self.meta: dict[str, str] = {}
        self.canonical: str | None = None
        self.h1_count = 0
        self.links: list[str] = []
        self.assets: list[str] = []
        self.images_without_alt: list[str] = []
        self.json_ld: list[str] = []
        self.ids: set[str] = set()
        self._in_title = False
        self._in_json_ld = False

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        a = {k: v or "" for k, v in attrs}
        if "id" in a:
            self.ids.add(a["id"])
        if tag == "title":
            self._in_title = True
        elif tag == "meta":
            key = a.get("name") or a.get("property")
            if key:
                self.meta[key] = a.get("content", "")
        elif tag == "link":
            if a.get("rel") == "canonical":
                self.canonical = a.get("href")
            elif a.get("href"):
                self.assets.append(a["href"])
        elif tag == "h1":
            self.h1_count += 1
        elif tag == "a" and a.get("href"):
            self.links.append(a["href"])
        elif tag == "img":
            self.assets.append(a.get("src", ""))
            if "alt" not in a:
                self.images_without_alt.append(a.get("src", ""))
        elif tag == "source" and a.get("srcset"):
            # "a.webp 520w, b.webp 786w": every candidate must exist
            self.assets.extend(candidate.strip().split(" ")[0] for candidate in a["srcset"].split(","))
        elif tag == "script":
            if a.get("type") == "application/ld+json":
                self._in_json_ld = True
                self.json_ld.append("")
            elif a.get("src"):
                self.assets.append(a["src"])

    def handle_endtag(self, tag: str) -> None:
        if tag == "title":
            self._in_title = False
        elif tag == "script":
            self._in_json_ld = False

    def handle_data(self, data: str) -> None:
        if self._in_title:
            self.title += data
        if self._in_json_ld:
            self.json_ld[-1] += data


def resolve(path: str) -> Path:
    """Maps a URL path on the site to the file the host would serve."""
    clean = path.split("#")[0].split("?")[0]
    if build.BASE_PATH and clean.startswith(build.BASE_PATH + "/"):
        clean = clean[len(build.BASE_PATH):]
    target = PUBLIC / clean.lstrip("/")
    return target / "index.html" if clean.endswith("/") or target.is_dir() else target


def main() -> int:
    errors: list[str] = []
    titles: dict[str, str] = {}
    descriptions: dict[str, str] = {}
    parsed: dict[str, PageParser] = {}
    forbidden = forbidden_words()

    for page in build.PAGES_LIST:
        file = PUBLIC / "404.html" if page.path == "/404.html" else resolve(page.path)
        name = page.path
        if not file.exists():
            errors.append(f"{name}: not built")
            continue
        text = file.read_text(encoding="utf-8")
        p = PageParser()
        p.feed(text)
        parsed[page.path] = p

        lowered = text.lower()
        for word in forbidden:
            if word in lowered:
                errors.append(f"{name}: contains forbidden text {word!r}")
        if "{{" in text:
            errors.append(f"{name}: unreplaced template token")

        if p.h1_count != 1:
            errors.append(f"{name}: has {p.h1_count} <h1> elements, expected 1")
        if not 15 <= len(p.title) <= 65:
            errors.append(f"{name}: title is {len(p.title)} characters (aim for 15-65): {p.title!r}")
        description = p.meta.get("description", "")
        if not page.noindex and not 70 <= len(description) <= 160:
            errors.append(f"{name}: description is {len(description)} characters (aim for 70-160)")
        if p.title in titles.values():
            errors.append(f"{name}: duplicate title {p.title!r}")
        if description in descriptions.values():
            errors.append(f"{name}: duplicate description")
        titles[name], descriptions[name] = p.title, description

        if page.noindex:
            if "noindex" not in p.meta.get("robots", ""):
                errors.append(f"{name}: should be noindex")
        elif p.canonical != build.page_url(page):
            errors.append(f"{name}: canonical is {p.canonical!r}, expected {build.page_url(page)!r}")

        for key in ("og:title", "og:description", "og:image", "twitter:card"):
            if not p.meta.get(key):
                errors.append(f"{name}: missing {key}")

        for block in p.json_ld:
            try:
                data = json.loads(block)
                if "@type" not in data:
                    errors.append(f"{name}: JSON-LD without @type")
            except json.JSONDecodeError as e:
                errors.append(f"{name}: invalid JSON-LD ({e})")
        if page.faq and not any('"FAQPage"' in block for block in p.json_ld):
            errors.append(f"{name}: has FAQ but no FAQPage data")

        for src in p.images_without_alt:
            errors.append(f"{name}: image without alt text: {src}")

        # Nothing may load from another origin: that would break the no-tracking promise.
        for asset in p.assets:
            if urlparse(asset).netloc:
                errors.append(f"{name}: loads a third-party resource: {asset}")
            elif not resolve(asset).exists():
                errors.append(f"{name}: missing asset {asset}")

    for page_path, p in parsed.items():
        for link in p.links:
            parts = urlparse(link)
            if parts.scheme in ("http", "https", "mailto"):
                if parts.scheme == "http":
                    errors.append(f"{page_path}: insecure link {link}")
                continue
            path, _, fragment = link.partition("#")
            target = resolve(path or page_path)
            if not target.exists():
                errors.append(f"{page_path}: broken link {link}")
                continue
            if fragment:
                target_page = "/" + str(target.relative_to(PUBLIC).parent).replace("\\", "/").strip(".") + "/"
                target_page = target_page.replace("//", "/")
                ids = parsed.get(target_page, PageParser()).ids
                if fragment not in ids:
                    errors.append(f"{page_path}: link to missing anchor {link}")

    # Sitemap and robots
    tree = ET.parse(PUBLIC / "sitemap.xml")
    ns = {"s": "http://www.sitemaps.org/schemas/sitemap/0.9"}
    listed = {loc.text for loc in tree.findall(".//s:loc", ns)}
    expected = {build.page_url(p) for p in build.PAGES_LIST if p.in_sitemap}
    if listed != expected:
        errors.append(f"sitemap mismatch: missing {expected - listed}, extra {listed - expected}")
    robots = (PUBLIC / "robots.txt").read_text(encoding="utf-8")
    if f"Sitemap: {build.SITE_URL}/sitemap.xml" not in robots:
        errors.append("robots.txt doesn't point to the sitemap")

    for page_path, p in parsed.items():
        if "Content-Security-Policy" not in (resolve(page_path) if page_path != "/404.html" else PUBLIC / "404.html").read_text(encoding="utf-8"):
            errors.append(f"{page_path}: no Content-Security-Policy")

    # Every root-relative link must carry the base path, or it breaks when the site lives in a subfolder.
    if build.BASE_PATH:
        for file in [*PUBLIC.rglob("*.html"), PUBLIC / "styles.css"]:
            text = file.read_text(encoding="utf-8")
            prefix = re.escape(build.BASE_PATH.strip("/"))
            if (re.search(r'(?:href|src|srcset)="/(?!/)(?!' + prefix + r'/)', text)
                    or re.search(r'srcset="[^"]*, /(?!' + prefix + r'/)', text)
                    or 'url("/assets' in text):
                errors.append(f"{file.relative_to(PUBLIC)}: link without the {build.BASE_PATH} prefix")

    size = sum(f.stat().st_size for f in PUBLIC.rglob("*") if f.is_file())
    home_weight = sum(resolve(a).stat().st_size for a in parsed["/"].assets if resolve(a).exists())
    print(f"Checked {len(parsed)} pages, {len(listed)} sitemap entries. Site size {size / 1024:.0f} KB, home page assets {home_weight / 1024:.0f} KB.")

    if errors:
        print(f"\n{len(errors)} problem(s):")
        for e in errors:
            print(" -", e)
        return 1
    print("All checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
