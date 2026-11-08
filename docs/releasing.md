<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Releasing nanoql

A release publishes, from one commit and its tag:

- **On NuGet:** `NanoQL`, `NanoQL.Extensions`, `NanoQL.Bind` and `NanoQL.Excel`, each with its symbol package on
  NuGet's symbol server.
- **On PyPI:** `nanoql`, a wheel per platform the release ships: `win_amd64`, `win_arm64`, `manylinux_2_28_x86_64`,
  `manylinux_2_28_aarch64` and `macosx_14_0_arm64`; no source distribution, since one would need the .NET SDK and the
  whole repository to build.
- **On GitHub, the release `v<version>`:** the C archive (the C and C++ headers, the library and its CMake package) and
  the native symbols archive for each platform the release ships, the Excel add-in's zip and its symbols zip,
  `SHA256SUMS` over them, and the notes: `CHANGELOG.md`'s section with the platforms' readings from the release run's
  own tests.
- **The platforms it ships** are `release.yml`'s `RIDS`, the bindings legs its `ci` call runs: Windows x64 and Arm64,
  Linux x64 and Arm64, and macOS on Apple silicon (ci.yml's `all`). A platform joins once its leg is green, by naming
  it in `RIDS` and in the set the `ci` call names, `ci.yml`'s matrix gaining one where none fits; a published
  release's files don't grow, so a platform ships from its first release on. The release run's tests run on the five
  systems too, Windows x64 the reference and the others reported in the notes.
- **The site,** nanoql.org, built from the tag.
- **The notebook,** `docs/notebooks/first-steps.ipynb`, isn't published: Colab opens it from `main` and its first
  cell installs the latest release from PyPI, so it uses released API alone. `notebook.yml` runs it so, weekly and
  on a pull request that changes it.

## What can't be undone

Every name and file is permanent from its first upload.

- **nuget.org** deletes no package: a mistake is unlisted and deprecated, and a fix ships as the next version
  (`2.0.1`).
- **PyPI** never takes a deleted file's name again: a mistake is yanked, and a fix ships as the next version.
- **A published GitHub Release** keeps its files (immutable releases): a fix is a new release.
- **The site** redeploys from a later commit.

## Before the tag

1. **The release commit:** the versions in step (`python -I tools/release/versions.py v<version>` checks the bindings
   tool's, the C header's, `surface.json`'s, `Directory.Build.props`'s, the wheel's and the Java layer's); the shipped
   names frozen (`dotnet run --project src/NanoQL.Bind -c Release -- freeze`, once `check` and `excel check` are
   green); the README's validation statement written from the release's gates run (`tools/docs/validation-report.py
   --readme`); `CHANGELOG.md`'s section for the version.
2. **The checks CI can't run,** on the release commit:
   - the suite inside Excel, `python -I bindings/excel/xltests/run.py` on a machine with 64-bit Excel
     (CONTRIBUTING.md); a red stops the release;
   - the SOFR example's trades (docs/sofr-example.md) priced again by an independent pricer, each within $0.01 on $10
     million; a residual past it stops the release.
3. **The dry run:** `release.yml` dispatched on `main` at the release commit. It runs `ci.yml` on every system and on
   the release's platforms, builds and inspects every package and file, writes the notes, logs in to NuGet and uploads
   the wheels to TestPyPI, and publishes nothing else. TestPyPI takes a filename once, as PyPI does, so a second dry
   run passes `testpypi: false`.

## The tag

An annotated tag `v<version>` at the release commit, pushed, runs `release.yml` again from the tag: `ci.yml`'s gates,
the packages and the files built and inspected, and the GitHub Release drafted with every file. Then, each after its
environment's approval: `nuget` pushes the four packages and their symbol packages, `pypi` the wheels, and
`release` publishes the draft.

- **Windows x64 blocks the release:** a red there stops the run. Linux's and macOS's test failures are counted in the
  notes. A bindings leg that can't go green ships best-effort only by the maintainers' decision, named in the
  repository variable `NANOQL_BEST_EFFORT` (its runtime identifier) for the tag's run, its red checks listed in the
  notes; the variable is cleared once the release is out, so the next release's run starts from none.
- **A publishing job that fails partway** is not rerun: nuget.org and PyPI each take a version's file once, so the rerun
  fails at the first file already up, and skipping what is up (`--skip-duplicate`, `skip-existing`) would hide a
  retagged release's files from two commits. Either the files it didn't push go up by hand from the same run's
  artifact (`nanoql-nuget`, `nanoql-python-*`), with a key or token scoped to those packages and deleted once used,
  or the version is abandoned, unlisted on nuget.org and yanked on PyPI, and the fix ships as the next version.
- **After it:** each package installed from its registry and checked (the first swap from NuGet, `pip install nanoql`
  with `bindings/python/tests/wheel_check.py`, each C archive with `bindings/c/tests/archive_check.py` and a user's C++
  project built through its CMake package, `bindings/cpp/consumer/`, the add-in's zip opened); then the site built
  from the tag and deployed.

## Set up once

- **GitHub:** the environments `nuget`, `pypi`, `testpypi` and `github-release`, each with a maintainer as its required
  reviewer; immutable releases on.
- **NuGet:** a trusted publishing policy, owned by the organization that owns the packages, naming the repository, the
  workflow `release.yml` and the environment `nuget`; the `nuget` environment's secret `NUGET_USER`, the nuget.org
  user name of a member of that organization, which `NuGet/login` exchanges with GitHub's token for a key that lasts
  an hour. No API key is stored.
- **PyPI and TestPyPI:** a trusted publisher on each, naming the repository, `release.yml` and the environment `pypi`
  or `testpypi`. No token is stored.
