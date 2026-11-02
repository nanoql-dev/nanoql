# Contributing to nanoql

## The faithfulness rule

The core mirrors QuantLib exactly. It changes only through ports of upstream QuantLib changes, or through fixes proven
against QuantLib. Anything QuantLib lacks (a new convention, model or feature) goes into the extensions layer,
`src/NanoQL.Extensions/`, with its own tests.

## Porting a QuantLib class

- One nanoql type per QuantLib class, with the same name where possible.
- Each ported file records the upstream path and the QuantLib release tag it came from.
- No observers, lazy objects or global state: inputs are immutable records; models and pricers are static functions.
- Results must match QuantLib's test vectors within the tolerance in docs/porting-playbook.md; the test suite
  blocks the merge otherwise.

## Code that may not be ported

Only code under an open-source license compatible with Apache 2.0 may be ported. Code from source-available
libraries, whose licenses don't permit it, may not; their methods may still be implemented from public sources such as
papers and documentation.

## How a change gets in

1. Fork the repository, and make your change on a branch of your fork.
2. Commit with `git commit -s` (below), and open a pull request against `main`.
3. CI runs the gates on the pull request, and checks that every commit is signed off. Windows is the reference
   system; results on Linux and macOS are reported, not yet required.
4. A maintainer reviews the change against the faithfulness rule and QuantLib's vectors, and runs any check CI can't.
5. The maintainer squash-merges it, editing the message to the repository's style and keeping your sign-off.

## Running the gates yourself

The gates are in docs/porting-playbook.md §8; each passes only with its denominator full. From the repository root:

- **Build:** `dotnet build NanoQL.slnx -c Release`: 0 errors and 0 warnings.
- **Tests and vectors:** `dotnet test --solution NanoQL.slnx -c Release`, then `python -I tools/vectors/agree.py`.
  Neither needs QuantLib: the vectors are committed under `tests/vectors/`.
- **Licensing:** `reuse lint`, then `python -I tools/check-upstream.py <QuantLib v1.44 worktree> --boost <Boost 1.90.0
  root> --qlnet <QLNet-v1.13.2 checkout>`, with the tools of `tools/vectors/requirements.txt`. The QLNet checkout is a
  clone of QLNet at its tag `QLNet-v1.13.2`, with no changes.
- **Reproducibility,** when the change adds or changes a vector generator under `tools/vectors/gen/`:
  `python -I tools/vectors/reproduce.py <generator>...` reruns them and compares their output byte for byte. They
  need QuantLib-Python 1.44, from the same requirements file; harness generators also need the C++ harness that
  `tools/cxx/build.py` builds. QLNet's generators, under `tools/vectors/gen/qlnet/`, need the .NET SDK instead, and
  nuget.org's QLNet 1.13.2 package, which `tools/vectors/qlnet/packages.lock.json` pins.
- **Coverage,** when the change ports code: the two coverage runs, one per oracle (QuantLib's and QLNet's), and
  `tools/check-coverage.py`, as the playbook's §8 and §9.5 give them.
- **Bindings,** when the change touches the public API or the bindings (docs/bindings.md):
  `dotnet run --project src/NanoQL.Bind -c Release -- check` keeps `bindings/` in step with the API, and `-- update`
  regenerates it; `dotnet build bindings/NanoQL.Bindings.slnx -c Release`, then
  `dotnet test --project bindings/NanoQL.Bind.Tests -c Release`. The C conformance suite runs on the test build:
  `dotnet run --project src/NanoQL.Bind -c Release -- build --rid <rid> --out <folder> --test-exports`, then
  `cmake -S bindings/c/tests -B <build folder> -DNANOQL_DIR=<folder>`, `cmake --build <build folder> --config Release`
  and `ctest --test-dir <build folder> -C Release`; C++'s the same way from `bindings/cpp/tests`; Python's on the test
  build's package, with NumPy installed:
  `python bindings/python/build.py --library <folder> --stage <stage folder> --test-exports`, then
  `python -m unittest discover -s bindings/python/tests` with the stage folder on `PYTHONPATH`; Java's with JDK 22 or
  later and Maven: `python bindings/java/stage.py --library <folder> --jextract <jextract 22> --test-exports`, then
  `mvn test -Dnanoql.library=<folder>/lib/<the library>` in `bindings/java`. The native build needs the platform's C
  toolchain (on Windows, Visual Studio's C++ tools, with the Visual Studio Installer's folder on `PATH`) and CMake.
- **Excel,** on Windows, when the change touches the public API or the Excel add-in (docs/excel.md):
  `dotnet build bindings/excel/NanoQL.Excel.slnx -c Release`, after the native build above, whose `NanoQL.Native.dll`
  the error table's test reads, then `dotnet test --project bindings/excel/NanoQL.Excel.Tests -c Release`.
  `dotnet run --project src/NanoQL.Bind -c Release -- excel check` keeps `bindings/excel/functions.json` in step with
  the API and `bindings/excel/list.json`, and `-- excel update` regenerates it. An add-in builds against the runtime
  package packed into a folder: `dotnet pack src/NanoQL.Excel -c Release -o <feed>`, then
  `dotnet run --project src/NanoQL.Bind -c Release -- excel build --runtime-source <feed> --out <folder>`, with
  `--experimental` for the experimental build; `-- excel package --runtime-source <feed> --out <folder>` writes the
  release's zip and its symbols zip. The suite inside Excel needs 64-bit Excel for Microsoft 365 with its
  multi-threaded calculation on, and Python: `python -I bindings/excel/xltests/run.py` builds the add-ins, runs the
  suite from its folder in Excel instances of its own, which load the add-ins for their session alone, and checks that
  it leaves your other Excel processes and Excel's settings as it found them. It puts back Excel's window position,
  which Excel rewrites when a test closes a second workbook, and says so; a test Excel rejects as busy before its first
  line runs once more, and it says that too (`run.py --self-test` checks that rule). It takes about seven minutes. Its
  `--python <interpreter>`, a Python with nanoql's package and NumPy, runs the swap's Python repro, which it otherwise
  leaves out. `python -I bindings/excel/xltests/stepin.py` needs pywin32 and netcoredbg 3.2.0 (`--netcoredbg <path>`
  unless it's on `PATH`). A repro export becomes a test as its vector and the workbook it came from, committed under
  `bindings/excel/tests/repro/` (docs/excel.md, "Repro export").

## Issues and questions

- **A bug:** open an issue with the inputs that reproduce it, and QuantLib's result for them if you have it.
- **A missing convention or feature:** use its issue form; a convention helps most with its published source.
- **A question or an idea:** GitHub Discussions.
- **A security vulnerability:** privately, as [SECURITY.md](SECURITY.md) says, never in a public issue.

Everyone taking part follows the [code of conduct](CODE_OF_CONDUCT.md).

## Sign your commits off (DCO)

nanoql uses the [Developer Certificate of Origin](https://developercertificate.org/) instead of a contributor license
agreement. Signing off a commit certifies that you wrote the change, or otherwise have the right to submit it under
the project's license:

    git commit -s

This adds a `Signed-off-by: Your Name <you@example.com>` line matching your git identity.
