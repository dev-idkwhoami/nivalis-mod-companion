#!/usr/bin/env python3
"""Package only the plugin DLL in its installation directory structure."""
from pathlib import Path
import hashlib
import os
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
output = Path(os.environ.get("PACKAGE_DIR", "bin"))
if not output.is_absolute():
    output = root / output
output.mkdir(parents=True, exist_ok=True)
archive = output / f"Nivalis.ModCompanion-{version}.zip"
with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as package:
    for source, name in files:
        package.write(source, name)
digest = hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_suffix(".zip.sha256").write_text(f"{digest}  {archive.name}\n")
print(archive)
