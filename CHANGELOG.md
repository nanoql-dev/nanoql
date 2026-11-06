# Changelog

The notable changes in each release of nanoql. The format follows
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the versions follow nanoql's own rule (Versions,
below).

## [Unreleased]

### Added

- **The C++ layer:** each platform's C archive carries `nanoql.hpp`, C++17 over `nanoql.h`, and a CMake package in
  `lib/cmake/nanoql/`: `find_package(nanoql CONFIG)` gives `nanoql::cpp` and `nanoql::c`, and a version request
  follows CMake's SameMajorVersion rule. [The C++ page](docs/cpp.md) covers installing, CMake and a first program
  (#7).
- **`nanoql-bind` writes the C++ layer for a library of your own:** `cpp`, and `build`'s `include/` beside the C
  headers.

### Fixed

- **Python:** `OISRateHelper`'s tenor form and `SwapRateHelper`'s two forms taking a rate can be called without
  `forward_start` or `fwd_start`, as their defaults allow; they raised "no overload takes these arguments" (#8).

### Validation

- The coverage figures count only the lines a test executes: the test data the runner built while discovering the
  tests no longer counts as covered, so the same tests read slightly lower figures than 2.0's.

### Known limits

- The Excel add-ins are unsigned. A firm whose Excel requires add-ins signed by a trusted publisher signs the `.xll`
  with its own certificate.
- The add-ins run on the .NET 10 Desktop Runtime, Excel-DNA's just-in-time path, not compiled to native code.
- The bindings, the C++ layer among them, cover Windows x64 and Linux x64 (glibc 2.28 or later); Windows Arm64,
  Linux Arm64 and macOS on Apple silicon follow in later releases, and Intel Macs have none, though nanoql's .NET
  library runs there.
- Python 3.11 or later.
- The Java layer is built and tested in the repository and not released.

## [2.0] - 2026-11-03

nanoql's first public release is 2.0, the whole library rather than a preview. Its versions have two parts: a minor
release adds, and nothing stable is removed until it has been deprecated for at least two releases and a year. The
experimental API may change in any release, and its released names are never reused.

### Added

- **The library,** `NanoQL` on NuGet, for .NET 10: dates, calendars and day counts; interest-rate, credit, equity, FX,
  inflation and commodity term structures, bootstrapped or fitted; instruments with their cash flows; and the pricing
  engines and models over them, analytic, tree, finite-difference and Monte Carlo. Every pricer is a plain function of
  its inputs: there are no observers, lazy objects or global evaluation date.
- **The extensions,** `NanoQL.Extensions`: market conventions as XML records, which a desk reads, overrides and
  versions, with every preset nanoql ships; and instruments and models beyond the core.
- **Python,** `pip install nanoql`: one wheel per platform for Python 3.11 or later, nanoql's native library inside,
  no .NET runtime needed.
- **C,** an archive per platform on the GitHub Release: `nanoql.h`, the library and the notices, for C and any
  language that calls C. The symbols ship in an archive of their own.
- **Excel,** `nanoql-excel-2.0-win-x64.zip` on the GitHub Release: the stable add-in and the experimental one, and five
  example workbooks; their symbols in a zip of their own.
- **The bindings tool,** `NanoQL.Bind` on NuGet: a library, a Python package or an Excel add-in of your own, your
  assembly's functions beside nanoql's in one native library; and `NanoQL.Excel`, the add-in's runtime, which the tool's
  builds restore.
- **Debugging:** each NuGet package's symbols on NuGet's symbol server, and the native library's in the release's
  symbols archives, their sources mapped to this release's commit on GitHub.

### Validation

Checked against QuantLib 1.44 on Windows x64.

- **The vectors:** 3,348,122 reference outputs in 1,169 vector files, of which four files and 39,887 outputs check
  QLNet's additions against QLNet 1.13.2. Each output agrees within a relative 1e-10 of the larger of the two values and
  the output's scale (an amount's is its notional), except 4,854 (0.14%) held to a documented looser tolerance.
- **The tests:** QuantLib's own tests, ported at QuantLib's tolerances.
- **The types:** 2,281 of the 2,282 public types with executable code are run by a QuantLib-checked test, QLNet's
  additions by a QLNet-checked one. The one apart, `FactorReductionFunctions`, every output of which is QuantLib's
  defect fixed, is checked by its own tests. 303 more are listed apart, 291 with no executable code and 12 own-tested
  (six of them extensions, five experimental and one a term structure), and three are equality-only. 89.5% of the ported
  lines are covered.
- **The divergences:** 720 documented divergences from QuantLib, each with its own test class.

What each module's checks cover is on [the validation page](https://nanoql.org/validation/).

### Platforms

- **Windows x64:** the validated platform, where every test and vector passes; a failure there blocks a release.
- **Linux and macOS, the .NET library:** supported; their known differences, a result's last bits where the C
  runtime's maths functions differ, are counted in each release's notes.
- **The bindings,** the native library under C and Python: Windows x64 and Linux x64 (glibc 2.28 or later), each built
  and tested natively in every release run, its differences counted in each release's notes. Windows Arm64, Linux
  Arm64 and macOS on Apple silicon follow in later releases.

### Versions

- A version is major and minor, with a third part only for a fix (2.0.1). A minor release adds; a 2.y release removes
  or changes the stable API only where it was deprecated at least two releases and at least a year before, and a
  deprecated member says so in every language.
- The experimental API (the `NQL0001` diagnostic, C's `NANOQL_EXPERIMENTAL`, the experimental add-in) may change or go
  in any release. Its released names still never move and are never reused: a removed function's name is retired.
- A released C function's, type's or Excel function's name never moves, in any release.

### Extension points

The public bases QuantLib's hierarchy derives from, abstract or unsealed, are nanoql's extension points: term
structures, stochastic processes, volatility structures, calendars, day counters and the rest, listed on
[the extension points page](https://nanoql.org/docs/extension-points.html). Six generic types are open in 2.0 where
QuantLib's are sealed by their use:

- three extension points: `PiecewiseSpreadYieldCurve<,>`, `InterpolatedPiecewiseForwardSpreadedTermStructure<>` and
  `InterpolatedYoYOptionletStripper<>`;
- three open to nanoql's own named classes only, through `private protected` constructors:
  `InterpolatedYoYCapFloorTermPriceSurface<,>`, `InterpolatedYoYOptionletVolatilityCurve<>` and
  `KInterpolatedYoYOptionletVolatilitySurface<>`.

### Known limits

- The Excel add-ins are unsigned. A firm whose Excel requires add-ins signed by a trusted publisher signs the `.xll`
  with its own certificate.
- The add-ins run on the .NET 10 Desktop Runtime, Excel-DNA's just-in-time path, not compiled to native code.
- No bindings for Intel Macs; nanoql's .NET library runs there.
- Python 3.11 or later.
- The Java and C++ layers are built and tested in the repository and not released; C++ includes `nanoql.h` as it is.

[2.0]: https://github.com/nanoql-dev/nanoql/releases/tag/v2.0
