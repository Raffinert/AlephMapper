"""Verify NuGet analyzer band selection with actual isolated compiler processes."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shlex
import shutil
import subprocess
import uuid
from xml.etree import ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def run(args, cwd=ROOT):
    result = subprocess.run([str(arg) for arg in args], cwd=cwd, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    print(result.stdout, end="", flush=True)
    if result.returncode:
        raise RuntimeError(f"Command failed ({result.returncode}): {args}")
    return result.stdout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package-dir", type=Path, default=ROOT / "artifacts/nuget")
    parser.add_argument("--compiler", action="append", help="Defaults: 4.8.0, 4.14.0, 5.0.0, sdk")
    args = parser.parse_args()
    packages = list(args.package_dir.resolve().glob("AlephMapper.*.nupkg"))
    if len(packages) != 1:
        raise RuntimeError(f"Expected one AlephMapper package in {args.package_dir}, got {packages}")
    with ZipFile(packages[0]) as archive:
        dlls = sorted(name for name in archive.namelist() if name.endswith(".dll"))
        expected = [f"analyzers/dotnet/roslyn{band}/cs/AlephMapper.dll" for band in ("4.8", "5.0")]
        if dlls != expected:
            raise RuntimeError(f"Unexpected package assemblies: {dlls}")
        nuspec = ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith(".nuspec"))))
        version = nuspec.find("{*}metadata/{*}version").text
    configured_version = ET.parse(ROOT / "source/Analyzer.Shared.props").findtext("./PropertyGroup/PackageVersion")
    if version != configured_version:
        raise RuntimeError(f"Package version {version} does not match source/Analyzer.Shared.props ({configured_version})")
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise RuntimeError("dotnet is required")
    output = ROOT / "artifacts/roslyn-compatibility" / uuid.uuid4().hex
    output.mkdir(parents=True)
    results = []
    for compiler in args.compiler or ["4.8.0", "4.14.0", "5.0.0", "sdk"]:
        if compiler not in ("4.8.0", "4.14.0", "5.0.0", "sdk"):
            raise RuntimeError(f"Unrecognized compiler: {compiler}")
        modern = compiler == "sdk" or compiler.startswith("5.")
        band = "5.0" if modern else "4.8"
        work = output / compiler
        work.mkdir()
        for name in ("Consumer.csproj", "Program.cs"):
            shutil.copy2(ROOT / "tests/PackageCompatibility" / name, work / name)
        config = ET.Element("configuration")
        sources = ET.SubElement(config, "packageSources")
        ET.SubElement(sources, "clear")
        ET.SubElement(sources, "add", key="local", value=str(args.package_dir.resolve()))
        ET.SubElement(sources, "add", key="nuget.org", value="https://api.nuget.org/v3/index.json")
        # The exact ID takes precedence over the wildcard: verify the package
        # just packed in this run, while resolving compiler toolsets from NuGet.
        mapping = ET.SubElement(config, "packageSourceMapping")
        local = ET.SubElement(mapping, "packageSource", key="local")
        ET.SubElement(local, "package", pattern="AlephMapper")
        nuget = ET.SubElement(mapping, "packageSource", key="nuget.org")
        ET.SubElement(nuget, "package", pattern="*")
        ET.ElementTree(config).write(work / "NuGet.Config", encoding="utf-8", xml_declaration=True)
        properties = [f"-p:PackageVersionUnderTest={version}", f"-p:ExpectedRoslynBand={band}",
                      f"-p:CompatibilityLanguageVersion={'14' if modern else '12'}"]
        if compiler != "sdk":
            properties.append(f"-p:CompatibilityCompilerVersion={compiler}")
        run([dotnet, "restore", work / "Consumer.csproj", "--configfile", work / "NuGet.Config",
             "--packages", work / "packages", *properties])
        installed_packages = list((work / "packages/alephmapper").rglob("*.nupkg"))
        if (len(installed_packages) != 1 or
                hashlib.sha256(installed_packages[0].read_bytes()).digest() != hashlib.sha256(packages[0].read_bytes()).digest()):
            raise RuntimeError(f"The consumer did not restore the package under test: {installed_packages}")
        if compiler != "sdk":
            tasks = work / "packages/microsoft.net.compilers.toolset" / compiler / "tasks/netcore"
            csc = tasks / "bincore/csc.dll"
            actual_version = run([dotnet, csc, "-version"]).strip()
            if not actual_version.startswith(compiler):
                raise RuntimeError(f"Wrong compiler binary: {actual_version}")
            wrapper = work / "compiler"
            wrapper.mkdir()
            stamp = work / "compiler-invoked.txt"
            if os.name == "nt":
                tool = wrapper / "csc.cmd"
                tool.write_text(f'@echo off\n> "{stamp}" echo {compiler}\n"{dotnet}" exec "{csc}" %*\n', encoding="utf-8")
            else:
                tool = wrapper / "csc"
                tool.write_text(f'#!/bin/sh\nprintf "%s\\n" {shlex.quote(compiler)} > {shlex.quote(str(stamp))}\nexec {shlex.quote(dotnet)} exec {shlex.quote(str(csc))} "$@"\n', encoding="utf-8")
                tool.chmod(0o755)
            # SDK10 otherwise still uses its inbox compiler despite an older Toolset
            # PackageReference. Select and verify the actual compiler explicitly.
            api = ".".join(compiler.split(".")[:2])
            properties += [f"-p:CompilerApiVersion=roslyn{api}", f"-p:CscToolPath={wrapper}", f"-p:CscToolExe={tool.name}"]
        log = run([dotnet, "build", work / "Consumer.csproj", "-c", "Release", "--no-restore", *properties])
        if compiler != "sdk" and (not stamp.exists() or stamp.read_text().strip() != compiler):
            raise RuntimeError("The requested compiler wrapper was not executed")
        expected_diagnostics = {"AM0019", "AM0021", "AM0022"}
        if modern:
            expected_diagnostics.add("AM0020")
        # Compiler output writes the ID followed by a colon, so inspect the
        # diagnostic prefix rather than relying on raw token membership.
        missing = {id for id in expected_diagnostics if f"{id}:" not in log}
        if missing:
            raise RuntimeError(f"Missing package-consumer diagnostics for {compiler}: {sorted(missing)}")
        if not modern and "AM0020:" in log:
            raise RuntimeError("Classic compiler fixture unexpectedly reported an extension-member diagnostic")
        run([dotnet, work / "bin/Release/net8.0/Consumer.dll"])
        results.append({"compiler": compiler, "selected_band": band, "status": "passed"})
    (output / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(f"All compiler/package checks passed. Results: {output / 'results.json'}", flush=True)


if __name__ == "__main__":
    main()
