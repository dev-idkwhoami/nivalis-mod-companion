#!/usr/bin/env python3
"""Package only Companion-owned release files; never game references or configs."""
from pathlib import Path
import hashlib
import re
import xml.etree.ElementTree as ET
import zipfile

root = Path(__file__).resolve().parents[1]
version = ET.parse(root / "src/ModCompanion.csproj").findtext(".//Version")
if not version or not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
    raise SystemExit("Expected a three-part project version")
api = (root / "src/Api/SettingsRegistry.cs").read_text()
if f'PluginVersion = "{version}";' not in api:
    raise SystemExit("Project version and public plugin version differ")
dll = root / "bin/Nivalis.ModCompanion.dll"
if not dll.is_file():
    raise SystemExit("Build the project before packaging")
files = [(dll, "BepInEx/plugins/Nivalis.ModCompanion.dll")]
files += [(root / name, name) for name in ("README.md", "LICENSE")]
files += [(p, p.relative_to(root).as_posix()) for p in sorted((root / "docs").glob("*.md"))]
dist = root / "dist"
dist.mkdir(exist_ok=True)
archive = dist / f"ModCompanion-{version}.zip"
with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as package:
    for source, name in files:
        package.write(source, name)
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_suffix(".zip.sha256").write_text(f"{digest}  {archive.name}\n")
print(archive)
