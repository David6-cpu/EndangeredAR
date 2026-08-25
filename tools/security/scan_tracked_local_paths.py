#!/usr/bin/env python3
import argparse
import os
import re
import subprocess
import tempfile
from collections import Counter
from pathlib import Path


_SEGMENT = rb"[A-Za-z0-9._ ()@,+%=-]+"
_ABSTRACT_MARKERS = (b"...", b"<", b">", b"${", b"{")
_SYNTHETIC_FIXTURE_EXEMPTIONS = {
    Path("EndangeredAR/Assets/Tests/EditMode/AIProviderTests.cs"): (
        (
            b"file://" + b"/" + b"tmp/local-ai",
            "synthetic invalid endpoint fixture",
        ),
    ),
}


def _path_pattern(prefix):
    return re.compile(re.escape(prefix) + rb"(?:/" + _SEGMENT + rb")+")


def _generic_patterns(home, temp_dir):
    patterns = []
    if home:
        patterns.append(("user-home path", _path_pattern(os.fsencode(home))))
    if temp_dir:
        patterns.append(("temporary working path", _path_pattern(os.fsencode(temp_dir))))

    slash = b"/"
    patterns.extend(
        (
            (
                "user-home path",
                re.compile(slash + rb"(?:" + b"Users|home" + rb")/" + _SEGMENT + rb"(?:/" + _SEGMENT + rb")+"),
            ),
            (
                "temporary working path",
                re.compile(slash + rb"(?:" + b"private/" + rb")?" + b"tmp" + rb"(?:/" + _SEGMENT + rb")+"),
            ),
            (
                "Unity installation path",
                re.compile(slash + b"Applications" + slash + b"Unity" + rb"(?:/" + _SEGMENT + rb")+"),
            ),
        )
    )
    return patterns


def _is_abstract(value):
    return any(marker in value for marker in _ABSTRACT_MARKERS)


def scan_bytes(data, home=None, temp_dir=None):
    home = Path.home() if home is None else Path(home)
    temp_dir = Path(tempfile.gettempdir()) if temp_dir is None else Path(temp_dir)
    occupied = []
    counts = Counter()

    for category, pattern in _generic_patterns(home, temp_dir):
        for match in pattern.finditer(data):
            span = match.span()
            if _is_abstract(match.group()) or any(span[0] < end and span[1] > start for start, end in occupied):
                continue
            occupied.append(span)
            counts[category] += 1

    return dict(sorted(counts.items()))


def format_findings(findings):
    lines = []
    for path in sorted(findings, key=lambda value: value.as_posix()):
        summary = ", ".join(
            f"{category} ({count})"
            for category, count in sorted(findings[path].items())
        )
        lines.append(f"{path.as_posix()}: {summary}")
    return "\n".join(lines)


def tracked_paths(root):
    result = subprocess.run(
        ["git", "-C", str(root), "ls-files", "-z"],
        check=True,
        capture_output=True,
    )
    return [Path(os.fsdecode(value)) for value in result.stdout.split(b"\0") if value]


def _read_tracked_path(root, relative_path):
    path = root / relative_path
    if path.is_symlink():
        return os.fsencode(os.readlink(path))
    return path.read_bytes()


def _remove_synthetic_fixture_values(relative_path, data):
    for value, _reason in _SYNTHETIC_FIXTURE_EXEMPTIONS.get(relative_path, ()):
        data = data.replace(value, b"synthetic-invalid-endpoint-fixture")
    return data


def scan_paths(root, paths):
    root = Path(root).resolve()
    findings = {}
    for relative_path in paths:
        relative_path = Path(relative_path)
        data = _remove_synthetic_fixture_values(
            relative_path,
            _read_tracked_path(root, relative_path),
        )
        counts = scan_bytes(data)
        if counts:
            findings[relative_path] = counts

    if findings:
        print(format_findings(findings))
        print(f"Tracked local-path scan: {len(findings)} file(s) contain local paths.")
        return 1

    print("Tracked local-path scan: 0 findings.")
    return 0


def main(argv=None):
    parser = argparse.ArgumentParser(description="Scan tracked text and binary files for local absolute paths.")
    parser.add_argument("--root", type=Path, default=Path.cwd(), help="Repository root (defaults to the current directory).")
    args = parser.parse_args(argv)
    return scan_paths(args.root, tracked_paths(args.root))


if __name__ == "__main__":
    raise SystemExit(main())
