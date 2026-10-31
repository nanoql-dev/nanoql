# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The shared step of QLNet's vector shims (docs/porting-playbook.md §7.2): builds QLNet's vector generator,
tools/vectors/qlnet/QLNetVectors.csproj, into a temporary folder, its restore locked to packages.lock.json (QLNet
1.13.2 from nuget.org), and runs one subcommand, which writes one vector file under the output root.

A shim under tools/vectors/gen/qlnet/ calls main() with its subcommand and is run as any generator is:

    .venv\\Scripts\\python -I tools/vectors/gen/qlnet/<QLNet path>.py [<output root>]

It needs the .NET SDK that global.json names, not the QuantLib wheel. A change to anything under tools/vectors/qlnet/
reruns every QLNet shim (the reproducibility gate, §8).
"""
import os
import subprocess
import sys
import tempfile

QLNET = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.join(QLNET, "QLNetVectors.csproj")
REPO = os.path.dirname(os.path.dirname(os.path.dirname(QLNET)))
DEFAULT_OUT = os.path.join(REPO, "tests", "vectors")


def main(subcommand):
    """Builds the generator and runs subcommand into the output root given as the first argument (default
    tests/vectors)."""
    out_root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else DEFAULT_OUT)
    with tempfile.TemporaryDirectory() as artifacts:
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
        build = subprocess.run(["dotnet", "build", PROJECT, "-c", "Release", "--artifacts-path", artifacts,
                                "-p:RestoreLockedMode=true", "-nologo", "-v", "q"],
                               capture_output=True, text=True, env=env, cwd=REPO)
        if build.returncode != 0:
            raise SystemExit(f"QLNetVectors: the build failed\n{build.stdout.strip()}\n{build.stderr.strip()}")
        program = os.path.join(artifacts, "bin", "QLNetVectors", "release", "QLNetVectors.dll")
        if not os.path.isfile(program):
            raise SystemExit(f"QLNetVectors: the build wrote no {program}")
        run = subprocess.run(["dotnet", program, subcommand, out_root], capture_output=True, text=True, env=env,
                             cwd=REPO)
        if run.returncode != 0:
            raise SystemExit(f"QLNetVectors {subcommand}: failed\n{run.stdout.strip()}\n{run.stderr.strip()}")
