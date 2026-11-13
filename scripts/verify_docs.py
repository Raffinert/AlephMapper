"""Check current Markdown navigation and fixture-backed C# snippets.

Run with --write after editing examples/Documentation/Program.cs to refresh the
displayed blocks, then run without --write as the CI check.
"""

from __future__ import annotations

import argparse
import re
import sys
import zipfile
from pathlib import Path
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / "examples/Documentation/Program.cs"
PAGES = [
    ROOT / "README.md",
    ROOT / "CONTRIBUTING.md",
    ROOT / "docs/generated-api-reference.md",
    ROOT / "docs/diagnostics.md",
    ROOT / "docs/advanced-usage.md",
    ROOT / "docs/sqlite-translation-evidence.md",
    ROOT / "docs/Adapt-Attribute.md",
    ROOT / "docs/extension-members-compatibility-matrix.md",
    ROOT / "docs/installation-and-compatibility.md",
    ROOT / "docs/architecture.md",
    ROOT / "docs/releasing-and-support.md",
    ROOT / "docs/oss-quality/features.md",
    ROOT / "docs/oss-quality/strict-semantics-design.md",
    ROOT / "examples/SampleApp/README.md",
]
EXAMPLE_RE = re.compile(
    r"<!-- docs-example:([\w-]+) -->\s*```csharp\n.*?```\s*<!-- /docs-example -->",
    re.DOTALL,
)
LINK_RE = re.compile(r"(?<!!)\[[^\]]+\]\(([^)]+)\)")
FENCE_RE = re.compile(r"^\s*(```|~~~)")


def snippets() -> dict[str, str]:
    source = FIXTURE.read_text(encoding="utf-8").splitlines()
    result: dict[str, str] = {}
    active: str | None = None
    body: list[str] = []
    for line in source:
        start = re.search(r"// docs-example:start ([\w-]+)", line)
        end = re.search(r"// docs-example:end ([\w-]+)", line)
        if start:
            if active is not None:
                raise ValueError("nested docs-example markers")
            active, body = start.group(1), []
        elif end:
            if active != end.group(1) or active in result:
                raise ValueError(f"unmatched/duplicate docs-example marker: {end.group(1)}")
            nonempty = [len(line) - len(line.lstrip()) for line in body if line.strip()]
            indent = min(nonempty, default=0)
            result[active] = "\n".join(line[indent:] for line in body).strip("\n")
            active = None
        elif active is not None:
            body.append(line)
    if active is not None:
        raise ValueError(f"unterminated docs-example marker: {active}")
    return result


def without_fences(markdown: str) -> str:
    lines = []
    fence: str | None = None
    for line in markdown.splitlines():
        match = FENCE_RE.match(line)
        if match:
            marker = match.group(1)
            if fence is None:
                fence = marker
            elif marker == fence:
                fence = None
            continue
        if fence is None:
            lines.append(line)
    return "\n".join(lines)


def anchors(markdown: str) -> set[str]:
    result: set[str] = set()
    counts: dict[str, int] = {}
    for line in without_fences(markdown).splitlines():
        if line.startswith("#"):
            title = line.lstrip("# ").strip()
            slug = re.sub(r"[^\w -]", "", title.lower(), flags=re.UNICODE)
            slug = re.sub(r"\s+", "-", slug)
            count = counts.get(slug, 0)
            counts[slug] = count + 1
            result.add(slug if count == 0 else f"{slug}-{count}")
        for explicit in re.findall(r'<a\s+(?:id|name)="([^"]+)"', line):
            result.add(explicit)
    return result


def check_links(page: Path, markdown: str) -> list[str]:
    errors = []
    for raw in LINK_RE.findall(without_fences(markdown)):
        target = raw.split(' "', 1)[0].strip("<>")
        if target.startswith(("mailto:", "plugin:")):
            continue
        parsed = urlsplit(target)
        if parsed.scheme in ("http", "https"):
            if parsed.netloc != "github.com" or not parsed.path.startswith("/Raffinert/AlephMapper/blob/main/"):
                continue  # External network checks are intentionally separate.
            local = ROOT / unquote(parsed.path.split("/blob/main/", 1)[1])
        elif parsed.scheme:
            continue
        else:
            local = (page.parent / unquote(parsed.path)).resolve() if parsed.path else page
        if not local.exists():
            errors.append(f"{page.relative_to(ROOT)}: missing link target {target}")
        elif parsed.fragment and local.is_file() and local.suffix.lower() == ".md":
            if unquote(parsed.fragment).lower() not in anchors(local.read_text(encoding="utf-8")):
                errors.append(f"{page.relative_to(ROOT)}: missing anchor {target}")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true", help="refresh marked Markdown snippets")
    parser.add_argument("--package-dir", type=Path, help="verify packaged README links and content")
    args = parser.parse_args()
    examples = snippets()
    errors: list[str] = []
    referenced: set[str] = set()
    for page in PAGES:
        if not page.exists():
            errors.append(f"missing page {page.relative_to(ROOT)}")
            continue
        original = page.read_text(encoding="utf-8")

        def replacement(match: re.Match[str]) -> str:
            name = match.group(1)
            referenced.add(name)
            if name not in examples:
                errors.append(f"{page.relative_to(ROOT)}: unknown snippet {name}")
                return match.group(0)
            return f"<!-- docs-example:{name} -->\n```csharp\n{examples[name]}\n```\n<!-- /docs-example -->"

        refreshed = EXAMPLE_RE.sub(replacement, original)
        if args.write and refreshed != original:
            page.write_text(refreshed, encoding="utf-8")
        elif refreshed != original:
            errors.append(f"{page.relative_to(ROOT)}: snippets differ from fixture; run python scripts/verify_docs.py --write")
        errors.extend(check_links(page, refreshed))
    for unused in examples.keys() - referenced:
        errors.append(f"unreferenced fixture snippet: {unused}")
    descriptors = ROOT / "source/Diagnostics/DiagnosticDescriptors.cs"
    declared = set(re.findall(r'"(AM\d{4})"', descriptors.read_text(encoding="utf-8")))
    catalog = ROOT / "docs/diagnostics.md"
    documented = set(re.findall(r"^## (AM\d{4})$", catalog.read_text(encoding="utf-8"), re.MULTILINE))
    if declared != documented:
        errors.append(f"diagnostic catalog mismatch: missing={sorted(declared - documented)}, extra={sorted(documented - declared)}")
    if args.package_dir:
        packages = sorted(args.package_dir.glob("AlephMapper.*.nupkg"))
        if len(packages) != 1:
            errors.append(f"expected exactly one AlephMapper .nupkg in {args.package_dir}, found {len(packages)}")
        else:
            with zipfile.ZipFile(packages[0]) as package:
                packaged_readme = package.read("README.md").decode("utf-8")
            if packaged_readme.replace("\r\n", "\n") != (ROOT / "README.md").read_text(encoding="utf-8"):
                errors.append("packaged README differs from repository README")
            for target in LINK_RE.findall(without_fences(packaged_readme)):
                parsed = urlsplit(target)
                if not parsed.scheme and parsed.path:
                    errors.append(f"packaged README has relative link: {target}")
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    print(f"Verified {len(referenced)} fixture snippets and links in {len(PAGES)} current pages.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
