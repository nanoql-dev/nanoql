# Changelog

The notable changes in each release of nanoql. The format follows
[Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and the versions follow nanoql's own rule (Versions,
below).

## [Unreleased]

### Added

- **The bindings on three more platforms:** Python's wheels and the C archive, its C++ header and CMake package among
  it, for Windows Arm64, Linux Arm64 (glibc 2.28 or later) and macOS on Apple silicon (14 or later), each built and
  checked in every release run as Windows x64 and Linux x64 are, its replays of the vector suite bit for bit with .NET
  on the same machine (#19).

### Changed

- **Complex exponentials, powers, sines and cosines** take the angle's sine and cosine apart, in nanoql's own
  functions in place of `System.Numerics.Complex`'s, which take both from `Math.SinCos`: under .NET's JIT the
  platform's combined routine, an ulp off for some angles on macOS and on Linux x64 with glibc older than 2.36. There
  a result resting on them can move in its last bits, and now matches the native library's bit for bit: the Heston
  family's semi-analytic engines (Bates's and the COS engine among them), the Heston process's density and its
  Broadie–Kaya schemes, the variance-gamma FFT engines, and the complex `Expm1`, exponential integrals and Bessel
  functions. On Windows and on Linux Arm64 nothing moves.

### Fixed

- **`nanoql-bind build` on macOS:** a native library it built for `osx-arm64` could read a caller's floating-point
  values wrong, with no refusal, after an exception thrown and caught inside a call, an internal fallback's: .NET 10's
  Native AOT writes the unwind information of a function that saves d8 to d15 in the wrong bits
  ([dotnet/runtime#135616](https://github.com/dotnet/runtime/issues/135616)). The tool now corrects it before the
  link; rebuild such a library with this release's tool.

### Known limits

- The Excel add-ins are unsigned. A firm whose Excel requires add-ins signed by a trusted publisher signs the `.xll`
  with its own certificate.
- The add-ins run on the .NET 10 Desktop Runtime, Excel-DNA's just-in-time path, not compiled to native code.
- The bindings, the C++ layer among them, cover Windows x64 and Arm64, Linux x64 and Arm64 (glibc 2.28 or later) and
  macOS on Apple silicon (14 or later); Intel Macs have none, though nanoql's .NET library runs there.
- A .NET app of your own published with Native AOT for `osx-arm64` carries the same unwind fault until .NET ships its
  fix; `nanoql-bind fix-unwind` corrects its object before the link (the bindings guide, "On macOS").
- macOS's C archive isn't notarized: a browser download opened in Finder carries the quarantine attribute into its
  folder, which `xattr -dr com.apple.quarantine <folder>` clears.
- On macOS, a caller of the C library through its own libffi (ctypes, cffi's ABI mode, JNA) misreads a by-value period
  that five functions take on the stack, until its libffi carries the fix of libffi issue #750; nanoql's Python package
  and callers compiled by clang are unaffected (the bindings guide, "C and C++").
- Python 3.11 or later; NumPy 1.26 or later, 2.3 or later on Windows Arm64.
- The Java layer is built and tested in the repository and not released.

## [2.2] - 2026-11-07

### Added

- **Experimental: a tranche's expected loss from the saddle-point model's own tail.**
  `SaddlePointTailLossModel<TCopula, TInitTraits>` integrates the model's saddle-point tail over the tranche, where
  `SaddlePointLossModel` reads a large-pool loss. On a 100-name pool (hazard 1%, recovery 40%), where that reading puts
  the whole loss in the first tranche at correlation 0, it is within 2% of the recursive model on the same latent model
  on the 3% to 22% tranches at correlations from 0.3, at one and five years. At high correlation it refuses a tranche
  whose tail it can't read: one attaching close above zero, [0.1%, 3%] from 0.6, or a senior one crowding the pool's
  largest loss, [22%, 60%] at 0.9 (#13).
- **Experimental: the analytic GJR-GARCH engine within the no-arbitrage bounds.**
  `AnalyticGJRGARCHEngine.CalculateWithinBounds`, on an engine or static, returns `Calculate`'s results but refuses a
  price outside a call's or a put's no-arbitrage bounds beyond rounding: the Edgeworth price leaves them in both wings
  at every λ, and everywhere once the variance's moments grow. `Calculate` is unchanged (#14).
- **Where the closed-form barrier and lookback engines are exact:** each engine's remarks state its condition under term
  structures, on a volatility of time alone: (r − q)/σ² constant over the option's life; for a rebate paid at the hit,
  r/σ² and q/σ² each constant; for the partial-time windows, σ and r − q flat. Each names the engine to use outside it,
  where the library has one (#15).

### Fixed

- **Black's formula:** a price below zero within rounding, which its arithmetic can reach at tiny standard deviations,
  is 0, where QuantLib's check refused it ("negative value"): within 16 ε of the discounted larger of the forward and
  the strike, a few ulps of the forward. A larger negative and NaN still refuse. The Gaussian copula spread engine then
  prices volatilities within rounding of zero, from about 1e-14 to 1e-8, but for most from 4e-14 to 1.3e-9, which its
  density calculator refuses (#12).
- **Kahale's interpolated smile section:** a segment whose fit missed its right end's price, a jump in the section and
  an arbitrage, is refitted to the same ends and slopes, so nothing else in the section moves; on 163 SABR smiles of a
  sweep the fit missed on 60. The route is opt-in: `KahaleSmileSection`'s `interpolate`, and the Markov-functional
  model's `KahaleInterpolation` adjustment (#16).

### Known limits

- The Excel add-ins are unsigned. A firm whose Excel requires add-ins signed by a trusted publisher signs the `.xll`
  with its own certificate.
- The add-ins run on the .NET 10 Desktop Runtime, Excel-DNA's just-in-time path, not compiled to native code.
- The bindings, the C++ layer among them, cover Windows x64 and Linux x64 (glibc 2.28 or later); Windows Arm64,
  Linux Arm64 and macOS on Apple silicon follow in later releases, and Intel Macs have none, though nanoql's .NET
  library runs there.
- Python 3.11 or later.
- The Java layer is built and tested in the repository and not released.

## [2.1] - 2026-11-06

### Added

- **The C++ layer:** each platform's C archive carries `nanoql.hpp`, C++17 over `nanoql.h`, and a CMake package in
  `lib/cmake/nanoql/`: `find_package(nanoql CONFIG)` gives `nanoql::cpp` and `nanoql::c`, and a version request
  follows CMake's SameMajorVersion rule. [The C++ page](docs/cpp.md) covers installing, CMake and a first program
  (#7).
- **`nanoql-bind` writes the C++ layer for a library of your own:** `cpp`, and `build`'s `include/` beside the C
  headers.
- **Migrating from QuantLib's C++:** [a guide](docs/migration/from-quantlib-cpp.md) pairs QuantLib's C++ with nanoql's
  C++ layer for the rates products, beside the QuantLib-Python and QLNet guides.

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

[2.2]: https://github.com/nanoql-dev/nanoql/releases/tag/v2.2
[2.1]: https://github.com/nanoql-dev/nanoql/releases/tag/v2.1
[2.0]: https://github.com/nanoql-dev/nanoql/releases/tag/v2.0
