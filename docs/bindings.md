<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->
# The bindings

nanoql crosses to other languages through one C ABI: its assemblies compiled with Native AOT into one shared library
(`nanoql.dll`, `libnanoql.so`, `libnanoql.dylib`) that exports a C function per member, described by a generated header,
`nanoql.h`. Every other language's layer is generated over it: Python (ctypes), C++ (a header with RAII) and Java (the
FFM API). A release ships the C archive, which carries the C++ layer and its CMake package, and the Python wheel; the C
archive serves any language that calls C, and the Java layer is built and tested in the repository and not released.
This page gives the C ABI's rules, the tool that generates and builds it, and how it is checked; the language pages
follow.

## The tool: nanoql-bind

`nanoql-bind` (the .NET tool `NanoQL.Bind`, `src/NanoQL.Bind/`) reads built assemblies' metadata without running them,
selects what crosses, and writes the outputs. In the repository, `dotnet run --project src/NanoQL.Bind -c Release --`
runs it; outside it, the tool installed from NuGet runs as `nanoql-bind` (Outside the repository, below):

| Command | What it does |
|---|---|
| `update` | regenerates `bindings/c/include/nanoql.h`, `bindings/surface.json` and `bindings/coverage.md` |
| `check` | regenerates them into memory and compares their bytes with `bindings/`'s; a difference fails |
| `freeze` | writes a release's shipped files, `bindings/surface.shipped.json` and `bindings/excel/functions.shipped.json`, from the checked-in surface; it refuses, writing nothing, unless `check` and `excel check` are green |
| `build --rid <rid> --out <folder> [--test-exports]` | generates the export shims, publishes the native host (`src/NanoQL.Native/`) with Native AOT, and writes `include/` and `lib/` |
| `shims --out <folder>` | writes the C# export shims the native host compiles (its build runs this) |
| `python --out <folder> [--test-exports]` | writes the Python package's generated modules into its folder (Python, below) |
| `cpp --out <folder> [--test-exports]` | writes the C++ headers: `nanoql.hpp`, and a user assembly's own (C and C++, below) |
| `java --out <folder> [--prefix <prefix>]... [--test-exports]` | writes the Java layer's sources over jextract's, a directory per package: every prefix's, or the named ones alone (Java, below) |
| `headers --out <folder> [--test-exports]` | writes the C headers alone, without a native build: `nanoql.h`, and a user assembly's own |
| `twins --out <folder>`, `replay-c --out <folder>`, `replay-cpp --out <folder>`, `replay-java --out <folder>` | write the vector suite's recording twins and the C, C++ and Java replayers' dispatches |
| `report` | prints the surface's counts |

Each takes `--assembly <prefix>=<path>` for every assembly to cross (nanoql's two by default), `--roles <path>` for a
roles table (the repository's `bindings/roles.json` by default) and `--shipped <path>` for the last release's surface
(`bindings/surface.shipped.json` when it exists). The Native AOT build needs the platform's C toolchain: on Windows,
Visual Studio's C++ tools with the Visual Studio Installer's folder on `PATH`.

- **Checked in:** `nanoql.h`, `surface.json` and `coverage.md`, so any change to the C surface shows in a commit's diff.
  After a change to nanoql's public API, `nanoql-bind update` regenerates them and the commit carries the difference.
  The shims, the language layers and the native library are generated at build.
- **Assemblies built on nanoql:** one native library per process holds every assembly it prices with, nanoql's and a
  user's, since two Native AOT libraries can't share an object. `nanoql-bind build --assembly nql=NanoQL.dll --assembly
  nql=NanoQL.Extensions.dll --assembly acme=Acme.dll` builds one: the user's functions take its own prefix
  (`acme_...`) and its own header (`acme.h`, which includes `nanoql.h`), and a user's roles table sits beside nanoql's.
- **Outside the repository:** the tool installed from NuGet (`dotnet tool install --global NanoQL.Bind`) carries beside
  it nanoql's two assemblies and, under `defaults/`, the roles table, the released names and the native host's project
  and sources, so a user builds a library of their own in any folder. `nanoql-bind build` writes the host into its
  output and publishes it there, against the nanoql assemblies the generation reads: the two given with
  `--assembly nql=`, from one folder (a build output holding both), or the tool's own copies when none is given, so
  the library's nanoql is the one the user's assembly was built on; `include/` holds the C and C++ headers (`nanoql.h`
  and `acme.h`, `nanoql.hpp` and `acme.hpp`); `nanoql-bind cpp` writes the C++ headers alone, `nanoql-bind python`
  the user's Python package, and `nanoql-bind excel build` their add-in ([excel.md](excel.md)). The commands that
  work on the repository's own files refuse there (`update`, `check`, `freeze`, `--test-exports`, `excel update`,
  `excel check` and `excel package`), and so do `replay-cpp`, which writes the C++ replayer's dispatch, the
  repository's own test, and those writing the Java layer, which the repository builds and tests but doesn't
  release (`java` and `replay-java`).

## The C ABI

### Names

- **`nql_<Type>_<member>`:** the type's nanoql name and the member's: `new` for a constructor, `get_<P>` for a getter,
  `set_<P>` for a static setter, `with_<P>` for a record's init-only property, `op_<Name>` for an operator (.NET's
  metadata name, `op_Addition`). A type whose simple name another type of the same prefix shares takes its namespace's
  last segment in front.
- **Statics** cross under their declaring type alone: a type carries the static and extension members it declares, and
  a named class (a sealed class naming a closed generic, QuantLib-Python's `%template` name for it) also those of the
  closing it names. A base's factory stays with its base, which crosses for it: `nql_LatentModel_GaussianCopulaPolicy_
  GaussianCopulaPolicyInitTraits_FromCorrelation` builds the latent model, and no named latent model carries a copy.
  Each language reaches a base's static through its own inheritance.
- **A type's own members:** a type surfaces the members it declares and, for a closing's name, those of its closed
  generic bases. A member a type declares with `new` hides its base's of the same C signature, as C#'s lookup binds it
  (a named class's own `Create` returns the named class); an override doesn't, since C# call sites name the virtual's
  declaration.
- **Overloads:** two underscores and the shortest run of the parameters' type names that tells the overload from the
  others: `nql_Calendar_Advance__Date_int` beside `__Date_Period`. Type names: `double`, `int`, `long`, `bool`,
  `string`, `Date`, `OptDate` for `Date?`, `doubleArray` for a collection of doubles, `ValueTuple` and its elements'
  names for a tuple (`ValueTupleOptdoubleOptdouble`), a nanoql type's name. Overloads whose C signatures are the same
  cross once: the one a type declares with `new`, else the one taking a `ReadOnlySpan`, else an `ImmutableArray`, else
  the first.
- **Generics:** a generic method crosses closed: per named factory where its type argument is a trait's, and at its
  constraint where each type parameter has one class or interface constraint, no constructor or value-type one
  (`nql_CalibratedModelExtensions_Calibrate` takes any calibrated model); any other generic method doesn't cross. A
  closed generic type a crossing function takes or returns crosses as a type of its own, its members too, named by its
  definition and its arguments: `nql_CalibrationResults_Gsr_get_Model`. An engine's calculations generic in their
  traits and statistics cross through its named classes, each a static class of them closed: `MCPR<X>Engine`
  and `MCLD<X>Engine` at `PseudoRandom` or `LowDiscrepancy` and `GeneralStatistics`, QuantLib's default statistics,
  for the 19 engines QuantLib-Python names and the six it doesn't, named as their siblings are; and
  `Binomial<Tree>BarrierEngine` and `Binomial<Tree>DoubleBarrierEngine` for the seven trees on Derman and Kani's
  assets, the wheel's closings (`nql_MCPREuropeanEngine_Calculate`, `nql_BinomialCRRBarrierEngine_Bind`). A curve,
  surface or generator QuantLib-Python names with a `%template` crosses as its named class, a sealed subclass of the
  closed generic (`ForwardFlatForwardCurve`, `PiecewiseLogCubicSpreadDiscount`, `YoYInflationCapFloorTermPriceSurface`,
  `InvCumulativeBurley2020SobolGaussianRsg`); the four the wheel deprecates (`LogLinearZeroCurve`, `LogCubicZeroCurve`,
  `NaturalCubicDiscountCurve`, `PiecewiseSplineCubicDiscount`) are deprecated in every layer, their messages the
  wheel's reasons with nanoql's replacements.
- **Released names never move:** each release writes its surface to `bindings/surface.shipped.json` (`nanoql-bind
  freeze`), and the generator keeps every released function's name, by the type it crosses under and its member, and
  every released type's. A type is read by its name without its assemblies' version, culture and public key token,
  which a closed generic's arguments carry and a release or a runtime moves. A released name the surface no longer
  holds, a removed function's, one whose parameters changed or a removed type's, stays in the file under `"retired"`,
  and no later function or type takes it. A name is held within its prefix: nanoql's released `nql_Calendar` leaves a
  user's `Calendar` its own name. Only a new member is named against the names in use: a member that gains its first
  overload keeps its short name and the newcomer takes the suffix.
- **Parameters** keep their C# names; one a C or C++ keyword takes, or `self`, `result` or `has_value`, gets a trailing
  underscore. A tuple's elements take the parameter's name and their own in snake case, C#'s tuple element names
  (`calibrationSet_option`), or `item1`... where C# names none; a returned tuple's elements their own (`first`). In a
  collection's tuple an element named `count` or `has_value` gets a trailing underscore, those being the collection's
  own.
- **An optional parameter of a type that doesn't cross,** null by default (a `TimeProvider`, a line search, the caller's
  functions), is left out of the C function and takes its default: `nql_BFGS_new(&optimizer)` builds QuantLib's
  default line search, and `nql_Date_TodaysDate(&today)` reads the computer's clock. Any other parameter that doesn't
  cross withholds its member, listed in `coverage.md` with why.

### Handles

- **One handle type,** `nql_handle`, for every object. The header names each parameter's expected type in its name and
  in the comment above the function.
- **Ownership:** every handle a function returns is the caller's, released once with `nql_release`; `nql_retain` gives
  a second owner, a new handle to the same object. NULL is the null reference.
- **Refusals:** a handle is a slot, a generation and the library's tag drawn when it loads, so a released, stale,
  never-issued or another library's handle is refused with `NQL_ERROR_HANDLE`, never followed. A handle of the wrong
  type is refused with `NQL_ERROR_TYPE`. A slot whose generation would wrap is retired.
- **Threads:** every object behind a handle is immutable, so any thread may use any handle at the same time, with no
  lock. Releasing a handle while another thread uses it is the caller's error.
- **Working types never cross as handles.** nanoql's working types (playbook §2.5: mutable, owned by one computation,
  each marked `[WorkingType]`) take other forms: `Matrix` and `Vector` as arrays, by rows; a generator as an immutable
  snapshot drawn in batches; a loss model's `Distribution` as its values. The stepping parts (finite-difference
  operators and schemes, lattices' discretized assets, market-model curve states and evolvers, optimizer states) stay
  inside nanoql, with the members that take or return them; an engine built from them is written in C# on nanoql and
  bound into the same library. A user's type that derives from a marked class, or implements a marked interface, is a
  working type too.
- **Results records** cross as objects: a getter per field. **`nql_type_name`** gives an object's .NET type, so a layer
  wraps a base-typed result as its own class.
- **A nanoql struct's** implicit parameterless constructor, `default(T)`, crosses as its `new`
  (`nql_BackwardFlat_new`, or `nql_IterativeBootstrap_new__void` beside its other constructors).

### Errors

Every function returns an `nql_status`: 0 for success, a positive code per refusal kind. Results come back through the
last parameters.

| Code | Name | Refusal |
|---|---|---|
| 0 | `NQL_OK` | none |
| 1 | `NQL_ERROR` | `NanoQLException`: QuantLib's own checks |
| 2 | `NQL_ERROR_ARGUMENT` | `ArgumentException` |
| 3 | `NQL_ERROR_ARGUMENT_NULL` | `ArgumentNullException`; a NULL `self` |
| 4 | `NQL_ERROR_ARGUMENT_RANGE` | `ArgumentOutOfRangeException` |
| 5 | `NQL_ERROR_INVALID_OPERATION` | `InvalidOperationException` |
| 6 | `NQL_ERROR_INVALID_CAST` | `InvalidCastException` |
| 7 | `NQL_ERROR_NOT_SUPPORTED` | `NotSupportedException` |
| 8 | `NQL_ERROR_KEY_NOT_FOUND` | `KeyNotFoundException` |
| 9 | `NQL_ERROR_HANDLE` | a released, stale, foreign or never-issued handle |
| 10 | `NQL_ERROR_TYPE` | a handle of the wrong type; a bound engine that can't serve the member |
| 11 | `NQL_ERROR_OUT_OF_MEMORY` | `OutOfMemoryException` |
| 12 | `NQL_ERROR_IO` | `IOException` and its kinds, `UnauthorizedAccessException`: a file not read or written |
| 13 | `NQL_ERROR_FORMAT` | malformed input: `XmlException`, `XmlSchemaValidationException`, `FormatException` |
| 99 | `NQL_ERROR_INTERNAL` | anything else: a defect, to report |

A kind outside the table maps by the member's own documentation: a documented `IndexOutOfRangeException` is
`NQL_ERROR_ARGUMENT_RANGE`, an undocumented one `NQL_ERROR_INTERNAL`. The calling thread's last refusal is
`nql_last_error_code`, `_message`, `_param` (the parameter as .NET names it) and `_type` (the .NET exception), valid
until the thread's next nanoql call.

### Values

| C# | C, in | C, out |
|---|---|---|
| `double`, `int`, `long`, `bool` | `double`, `int32_t`, `int64_t`, `bool` | a pointer to it |
| `string` | `const char*`, UTF-8, NUL-terminated, NULL for null | `char**`, freed with `nql_free` |
| `Date`, `Date?` | `nql_date`: QuantLib's serial (Excel's), 0 for no date | `nql_date*` |
| `Period` | `nql_period {length, units}` by value | `nql_period*` |
| an enum | `int32_t`, with `NQL_<Enum>_<Member>` | `int32_t*` |
| `T?` for a value `T` | `const T*`, NULL for none | `T*` and `bool* has_value` |
| `Complex` | `nql_complex {re, im}` | `nql_complex*` |
| an object, a nanoql struct | `nql_handle`; NULL for an optional struct is its default, `default(T)`, what C# passes for the omitted argument | `nql_handle*`, the caller's |
| a collection | a pointer and an `int32_t` count; a `ReadOnlySpan` of numbers is read in place | a pointer and a count, freed with `nql_free` (an array of handles: each released first) |
| `Span<double>`, `Span<int>` | a pointer and a count, written in place | |
| a collection of nullables | values and `has_value` flags | the same |
| a jagged array | its values flat by rows, their count, the rows' lengths and the rows' count | the same |
| a dictionary, key–value pairs | keys, values, count | the same |
| a tuple | one parameter per element, `<param>_<element>`, each in its element's form; C#'s default is each element's zero value, NULL for a nullable one | one out-parameter per element, by its name |
| a collection of tuples | one array per element, `<param>_<element>`, a nullable element with its `_has_value` flags, and `<param>_count`; NULL first array for null, arrays of no rows for empty | one array per element, by its name, then `has_value` flags for a collection of nullable tuples, then `count`, each freed with `nql_free` (each handle released first) |
| `Matrix`, `Vector` | values by rows with the rows and columns; values and their count; one the member writes into (`coverage.md`'s list) by `double*`, written back after the call | the same, freed with `nql_free` |
| a generator | its snapshot's handle; for a closed generator construction (`RandomSequenceGenerator<MersenneTwisterUniformRng>`), the snapshot of a class deriving it, checked at the call | `nql_<Generator>_Draws`: draws and weights, the next snapshot |
| a loss `Distribution` | | its normalized buckets' edges, widths, densities, cumulative and excess probabilities and averages |

A tuple's or a collection's elements are scalars, dates, enums, strings, nullable doubles, integers and dates, and
objects. A returned collection of tuples with a nullable element, a collection of nullable tuples in, and a tuple
holding a collection or a tuple don't cross, each listed in `coverage.md`. An optional parameter's default is in the
header's comment and in `surface.json`; C passes every parameter, and each language layer applies the defaults.

### Generators

A generator's constructor returns an immutable snapshot holding a private copy of the generator as built.
`nql_<Generator>_Draws(snapshot, count, &values, &weights, &next)` copies it, draws `count` values (for a sequence
generator, `count` points of its dimension, by rows, the dimension out too) with their weights, and returns the copy as
the next snapshot. The same snapshot always gives the same draws, a stream continues from `next` with nothing
recomputed, and threads may draw from one snapshot at once. A member that takes a generator copies the snapshot's. A
parameter of a closed generator construction takes the snapshot of a class deriving it (the Gaussian sequence
generators over `MersenneTwisterUniformRsg`, `KnuthUniformRsg` and the rest), refused with `NQL_ERROR_TYPE` for another
generator's; a member taking a construction no generator class derives doesn't cross.

### The context and the conventions

- **The context** is a handle: `nql_EvaluationContext_new(evaluationDate)`, then `nql_EvaluationContext_with_<Field>`
  for each other field, each returning a new context.
- **Conventions by name:** the coded presets' constructors (`TARGET`, `Actual365Fixed`, `Euribor6M`, ...) don't cross;
  a convention set gives each by its name: `nql_ConventionSet_Calendar(set, "TARGET", &calendar)`,
  `nql_ConventionSet_IborIndex__string_Period(set, "Euribor", tenor, curve, &index)`. A preset that takes arguments
  has a lookup taking them after its name, as QuantLib-Python's constructor takes them: the daily-tenor LIBORs'
  settlement days (`nql_ConventionSet_IborIndex__string_int(set, "DailyTenorUSDLibor", 2, curve, &index)`), AUCPI's
  frequency and revised flag and GenericCPI's with its lag and currency, and their year-on-year twins; the BMA index, no
  Ibor index, by `nql_ConventionSet_InterestRateIndex`, which gives the BMA index, or any Ibor or overnight index, by
  name or alias; the index lookups follow the set's aliases. A set comes from
  `nql_ConventionSet_get_QuantLib`, or from XML: `nql_ConventionsXml_Read__string(path, &snapshot)` then
  `nql_ConventionSet_With`. A NULL set is the current provider read as a set: QuantLib's set where the default provider
  is the core's, refused where it is a user's provider. `nql_ConventionProvider_set_Default` sets the default; scopes
  (`ConventionProvider.Use`) don't cross.

### Bound engines

A member that takes one of nanoql's engines as a delegate (an instrument builder's pricer, a calibration helper's
engine) takes a bound engine: an immutable handle naming an engine with its inputs filled in.

- **`nql_<Engine>_Bind`** takes the inputs the engine's calculations take after the instrument and the context, one
  function per set of them, each input optional: NULL leaves it open (a value goes by pointer, a collection of tuples
  by its first element's array). **`nql_<Engine>_BindOn`** takes the instrument too, for a member that takes the
  context alone (a composite instrument).
- **The member supplies its own arguments,** always the instrument and the context, and for some members more (a
  calibration helper's trial model, the quanto engine's process, YoY optionlet stripping's trial surface, the callable
  bond's OAS spread). Each goes into the engine's parameter of its role: an object into the one parameter of its type; a
  value, or an object two parameters can take, into the parameter `bindings/roles.json` names; a calibration helper's
  trial model always by its entry's names, cast to that parameter's type at each call (a model the engine can't take
  fails with `NQL_ERROR_INVALID_CAST`). The bound inputs fill the rest, and a parameter open on both sides keeps its
  default; a nullable value with no default is null, C#'s value for it (a Monte Carlo engine's `timeStepsPerYear` beside
  its bound `timeSteps`).
- **The calculation:** the member takes its engine's calculation for the input set `Bind` named, for the most derived
  instrument type its own is.
- **Refused with `NQL_ERROR_TYPE`** when the member takes the engine: an input both bound and supplied, a required one
  neither bound nor supplied (a non-nullable one with no default), an engine with no calculation for the member, an
  engine with no parameter for a role (the Black callable engines on the OAS members, where QuantLib's engine reads no
  spread).
- **The generator stops** on a value a member supplies with no entry in the roles table, an object two parameters take
  that the table names neither of, and two calculations tied for one member. `coverage.md` lists every pair, where its
  arguments go, and each calculation that forms none, with why.

### Experimental functions

nanoql's experimental API (NQL0001) crosses, each declaration compiled only under `#ifdef NANOQL_EXPERIMENTAL`, C's
form of C#'s opt-in. A member of a stable type whose signature names an experimental type is experimental too.

### Versions

- The first release is 2.0. A version is major and minor, with a third part only for a fix (2.1.1). `nanoql.h` defines
  `NQL_VERSION_MAJOR`, `NQL_VERSION_MINOR`, `NQL_VERSION_PATCH`, `NQL_VERSION` ("2.0") and `NQL_ABI_VERSION`, the major
  times 1,000 plus the minor (2000), which a fix keeps; the library answers `nql_version()` and `nql_abi_version()`. A
  2.y header works with the 2.y library alone; a program linking a separately installed library checks
  `assert(nql_abi_version() == NQL_ABI_VERSION)`.
- A 2.y release may remove or change the stable API only where it was deprecated at least two releases and at least a
  year before. A member nanoql marks `[Obsolete]` is declared `NQL_DEPRECATED("...")` in the header and deprecated in
  every layer.
- The experimental API (NQL0001, `NANOQL_EXPERIMENTAL`, the Excel add-in's experimental build) may change or go in any
  release, outside that window. Its released names still never move and are never reused: a removed function's name is
  retired.
- nanoql's assemblies carry the major version alone, `2.0.0.0`, for every 2.y release, since `surface.json` names
  closed generic types by assembly-qualified names that carry it.

### Fixed functions

`nql_release`, `nql_retain`, `nql_type_name`, `nql_equals`, `nql_hash`, `nql_to_string`, `nql_free`,
`nql_last_error_code`, `nql_last_error_message`, `nql_last_error_param`, `nql_last_error_type`, `nql_version`,
`nql_abi_version`, and `nql_live_handles`, the handles issued and not released, for diagnostics.

## surface.json

The surface's language-neutral description, which every language back end reads and which another generator may read
too. Schema 1, its `"schema"` field raised on any change of meaning; one row a line.

- **`assemblies`:** each input's `prefix` and `name`.
- **`types`:** `name`, `prefix`, `cname`, `base` (the nearest base that crosses: a public class of the inputs that isn't
  generic, or a closed construction), `interfaces`, `shape` (`class`, `sealed`, `abstract`, `static`, `struct`,
  `interface`), `form` (how it crosses: `handle`, `value` for `Date` and `Period`, `boxed` for another struct, `static`,
  and for the working types `matrix`, `vector`, `snapshot`, `distribution` or `withheld`), `experimental`.
- **`enums`:** `name`, `prefix`, `cname`, `experimental`, `flags`, `members` as `[name, value]`.
- **`functions`:** `name` (the C name), `kind` (`new`, `method`, `static`, `get`, `set`, `with`, `field`, `indexer`,
  `operator`, `draws`, `bind`, `bindon`), `type`, `member`, `key` (the member's metadata key: declaring type, name and
  parameter types), `static`, `experimental`, `deprecated` (its message, when there is one), `overloaded`, `set` (a
  bind's engine input set), `params` (each `name`, `kind`, `type`, and `optional` with its `default`), `returns` (its
  `kind` and `type`), and `c`, the C parameters as `[type, name]`. A kind is a scalar (`double`, `int32`, `int64`,
  `bool`, `string`, `date`, `period`, `enum`, `complex`), a nullable value (`double?` and the like), an object
  (`handle`, `boxed`, `snapshot`, `engine`), a working type's form (`matrix`, `vector`, `distribution`), or a
  collection: `array:<element>`, `span:<element>`, `jagged:double`, `map:<key>:<value>`, `tuple:<element>,...`,
  `array:tuple:<element>,...` for a collection of tuples, and as a result `array:tuple?:<element>,...` for one of
  nullable tuples; `vector&` and `matrix&` for one the member writes into. A tuple's or a collection of tuples' C#
  element names are its `items`. A reader meeting a kind it doesn't know refuses it.
- **`presets`:** each withheld preset constructor: its `dictionary`, `name`, `member`, the convention set's `lookup`
  function, and its `args`, each a constant, the lookup's parameter or computed.
- **`engines`:** each engine input set: its `id`, `engine`, `inputs` and `calculations`.

## Checking the bindings

- **The surface:** `nanoql-bind check` keeps the checked-in outputs in step with the code. The tool's own tests,
  `bindings/NanoQL.Bind.Tests`, check its rules on nanoql's assemblies and on fixtures it binds as a user's assembly.
- **The test build:** `nanoql-bind build --test-exports` adds the assembly `bindings/NanoQL.Bind.TestTypes` under the
  prefix `nqlt` (an echo per kind, each refusal kind, a deprecated member, a user's generator, a member that sleeps,
  members that read past an array or an index under one widened value each, with their checked twins, and the
  bound-engine cases' markets with their C# routes) and narrows handles' generations to 8 bits; the packages ship
  without it. Each language's conformance suite runs on it: `bindings/c/tests/` for C, `bindings/cpp/tests/` for C++,
  `bindings/python/tests/` for Python, `bindings/java/src/test/` (JUnit 5) for Java, the test types' Java layer compiled
  with its tests, as a user's package beside nanoql's.
- **The vector suite through each binding:** the recorder (`tools/replay/`) compiles `nanoql-bind twins` with its
  runtime, rewrites the test assembly's call sites into nanoql to call them, and runs the vector classes with their own
  checks against QuantLib's vectors; a red stops it unless `--keep-reds` keeps the trace, which CI passes except on
  Windows x64, the reference platform: elsewhere a vector red is reported, and the replays compare each layer with C#'s
  results on the same machine. Each twin writes its call as its C function's: arguments and result in the C forms
  above, objects by ids (a struct by its value), doubles by their bits, a preset's constructor, called or reached by
  reflection, as its set's lookup, a record built by an object initializer as `new` and its `with_` calls, a struct made
  by `default` or `new T()` as its implicit constructor's call, a generator's draws as `Draws` calls along its
  snapshots. A call made from inside nanoql (a callback into the test code) isn't written. Each language's replayer then
  calls its own layer for every line and compares every result with C#'s, bit for bit (off Windows x64, a double within
  `NANOQL_REPLAY_TOLERANCE` relatively, or else within `NANOQL_REPLAY_ABS_TOLERANCE`
  absolutely, is a match, tallied; CI sets 1e-12 and 1e-13), NaN as NaN, a new object by its
  class's name (a file-local class's without its compiler prefix, `<File>F<hash>__`, whose hash is of its source file's
  path, so a trace replays against a library built from another checkout); hash and equality calls aren't recorded (the
  conformance suites check them); a fact replays up to its first call that needs something the trace couldn't write (a
  delegate, an object a call that doesn't cross made, a generator a call that doesn't cross moved, or one a crossing
  call took, which C# shares where the C ABI copies; a call that writes into a `Matrix` or `Vector` argument crossing in
  only, which the C call doesn't give back). The C replayer is `bindings/c/replay/`, its dispatch generated by
  `nanoql-bind replay-c`; C++'s is `bindings/cpp/replay/`, its dispatch generated by `nanoql-bind replay-cpp`, which
  calls each C function through its C++ member with arguments of the member's own parameter types; Python's is
  `bindings/python/tests/replay.py`, which calls each C function through the Python member the generated map names, an
  overload through its group's dispatch; where the dispatch, reading the arguments' own classes, takes another form than
  C#'s static choice, it counts the call and calls the recorded form; Java's is
  `bindings/java/src/test/java/org/nanoql/replay/`, its dispatch generated by `nanoql-bind replay-java`, which calls
  each C function through its Java member, a fact stopping where Java's types can't hold an input (an enum value no
  constant has, which only a refusal's test passes, or a matrix of no rows and some columns), each such stop counted by
  its reason. A record's generated equality pair, which no class declares, goes through the language's own `==` or
  `equals` (`nql_equals`) in the C++, Python and Java replayers, its C function called by the C replayer alone. Given
  `bindings/replay-floor.json`, the C and Java replayers also fail below the floor: at least as many facts replayed
  whole, each with calls, as the floor lists (the facts that reach only crossing members), at least its calls replayed,
  refusals matched, and the facts it names whole.
- **Every function called, its arrays and indexes widened:** the Python replayer's `--sweep <outcomes.tsv>`, after its
  replay of the whole suite and before its objects are released, calls each function of the layer's table, those the
  trace doesn't call first, then those it calls: through its Python member, or, for a record's equality pair the layer
  leaves to `==`, through its C function, its answer checked against `==` and `!=`. Its standing arguments are the
  replay's objects by class, at most 40 of each, each taken at its first sight in the facts' name order, so that the
  recording's order of facts doesn't move them (but for a fact whose replay stops before it reaches an object another
  fact made), and values by kind and parameter name, the names from `bindings/surface.json` (`--surface` names another;
  a run with neither stops), up to three choices; the objects an uncalled function returns join them, in passes until
  one reaches nothing new. Then, on the choice that returned (each one tried, when none did), its widened attempts
  change one thing each, their objects joining nothing: an array-like argument empty, of one element or of twelve, and
  every one at once where there are two or more (`zero:`, `one:` and `many:`, with the parameter's name or `*`); and an
  index-like integer, by its name (`i`, `index`, `iName` and the like) at 1,000 and at -1 (`past:` and `neg:`). An
  orthogonal polynomial's order takes no index probe, and a map no array probe, each exemption with its reason. The
  calls run in a fresh temporary folder, removed after a clean sweep and kept after a red, each under a limit
  (`--limit`, 30 s), past which `faulthandler` ends the process, the call and its probe named on standard error. It
  fails on a Python-layer error, code 99 or code 9 in any attempt, standing or widened, and on an exclusion that names
  no function of the layer; the outcomes file gives each function's line: returned, refused with its code, unreached
  with the class it lacked, or excluded, a red's probe beside its kind, the trace's functions marked `trace`, and the
  widened attempts' outcomes last. `EXCLUDED` in `replay.py` lists the functions it skips, in both stages, each with its
  reason and its time. `--prefix` sweeps a user's package, `nqlt` for the test build, through its own members, importing
  it and its experimental module first, so a trace of its calls replays too. CI's Python step runs it on Windows x64; a
  replay of a subset doesn't sweep, as its objects aren't the whole suite's.
- **Parity with QuantLib-Python:** `tools/bindings/wheelparity.py` reads the wheel's `QuantLib.py` (the release
  `tools/vectors/requirements.txt` pins), `surface.json` and `bindings/wheel-routes.json`, and puts every class the
  wheel constructs, and every module function that constructs one, in one group: it crosses with a maker (a constructor,
  a factory, a preset lookup, an engine's calculation or `Bind`); a route the table names, with its reason, to a type
  the surface makes, an enum it carries, a C function it has or a language form; an exclusion with its reason (handles,
  observers and settings, SWIG's containers, the stepping parts, the caller's own functions); or a gap. `--fail-on-gap`
  exits 1 on a gap, and CI's bindings job runs it on Windows x64; `--report` writes the parity table. The table's notes
  keep the differences a crossing class still has (`GlobalBootstrap`'s initial guess).

## Debugging into nanoql

- **Windows:** the native library's PDB holds line records for nanoql's C# code, so a debugger attached to a C, C++,
  Python or Java program steps from its call into nanoql's source, the generated export the frame above.
  `tools/replay/stepin.py` shows it from a C program: `python -I tools/replay/stepin.py <exe> nanoql.dll
  "*FlatForward___ctor*"` stops in `FlatForward`'s constructor and prints the stack's source lines. Visual Studio's
  mixed-mode debugging does the same from Python. A release's library, built in release mode, maps nanoql's sources
  to its commit on GitHub (Source Link), so the debugger fetches them; a library built from a checkout reads the
  checkout.
- **Linux and macOS:** the build strips the DWARF line records into `libnanoql.so.dbg` and `libnanoql.dylib.dSYM`;
  point gdb at a checkout with `set substitute-path <the build's source root> <checkout>`, or lldb with
  `settings set target.source-map <the build's source root> <checkout>`.
- **Symbols ship apart** from the packages, as an archive per platform on the release, `nanoql-<version>-<rid>-symbols`
  beside the C archive: `nanoql.pdb`, `libnanoql.so.dbg` or `libnanoql.dylib.dSYM` (`tools/release/symbols.py`).

## Platforms

The library is built natively on each of five platforms: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64` and
`osx-arm64`; Linux in a `manylinux_2_28` container, so it runs on glibc 2.28 and later; Intel Macs aren't among them
(nanoql's C# library runs there; nanoql builds and tests no bindings for them). It loads no ICU (invariant
globalization), so its messages read the same in every locale and it runs where libicu is absent. Windows x64 is the
validated platform; on the others, a result's last bits may differ where the C runtime's maths functions do. A release
ships the platforms `release.yml` names in `RIDS`: Windows x64 and Linux x64, with Windows Arm64, Linux Arm64 and macOS
on Apple silicon in later releases.

## C and C++

- **Install:** an archive per platform on the release, `nanoql-<version>-<rid>-c-api.zip` on Windows and `.tar.xz`
  elsewhere: `include/` (`nanoql.h` and `nanoql.hpp`), `lib/` (`nanoql.dll` with its import library `nanoql.lib`,
  `libnanoql.so` or `libnanoql.dylib`, and `cmake/nanoql/`, the CMake package) and the notices: `LICENSE`, `NOTICE`,
  `THIRD_PARTY_NOTICES.md`, and the .NET runtime's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`, which Native AOT
  compiles into the library. The symbols ship apart. `python bindings/c/archive.py --library <native build folder> --out
  <folder>` writes one.
- **C:** include `nanoql.h` and link the library; the C ABI above is the whole of it.
- **C++:** `nanoql.hpp`, C++17, over `nanoql.h`, its CMake target `nanoql::cpp` (`find_package(nanoql CONFIG)`;
  [cpp.md](cpp.md) gives the install, CMake and a first program). `NANOQL_EXPERIMENTAL`, defined first, opts in to
  the experimental API. Its hand-written part is `bindings/cpp/runtime.hpp`; the tool writes the rest, never checked
  in, as `nanoql-bind build` does into `include/`. The bullets below describe it. C++ can also include `nanoql.h` as
  it is; its declarations sit in `extern "C"`.
- **Names:** namespace `nanoql`; nanoql's type names; members in QuantLib's camelCase (`curve.discount(5.0)`,
  `swap.fixedRate()`, `NPV()`, `yoyInflationIndex()`), a getter a member function; a static member or an extension
  method a static member of its declaring class (`ConventionSet::quantLib()`), a record's init-only property
  `with<Name>(value)`, an operator a static `op_<Name>` (a record's `==` and `!=` aside, under Objects) and, for
  arithmetic and ordering, C++'s operator (and `==` for `Date` and `Period`). A name a keyword or a type takes gets a
  trailing underscore: `Echo::double_`.
- **Objects:** each class owns a handle: a copy takes a new handle to the same object, a move takes the handle, the
  destructor releases it. A class's bases and interfaces are virtual bases, so an object converts to each
  (`const ICalibratedModel& m = gsr;`). Nanoql's null reference is a null object, `T(nullptr)`, which `if (obj)` tests;
  a constructor taking no arguments, where nanoql has one, makes the object (a struct's is `default(T)`); a constructor
  one argument can call is `explicit`, so no argument converts to an object implicitly, as in C#.
  `object_cast<T>(o)` gives an object as another class, unchecked until a member of a class it isn't refuses.
  Equality, `hashCode()`, `toString()` and `typeName()` are nanoql's, and a record's `==` and `!=` are that equality,
  with no `op_Equality` or `op_Inequality` of their own.
- **Values:** `Date` holds QuantLib's serial (`Date()` is no date; `serial()` the C form) with nanoql's members
  (`year()`, `month()`, `dayOfMonth()`, `Date::todaysDate()`); `Period` has `length()` and `units()`; enums are enum
  classes over `int32_t`; `std::string` (a null string comes back empty; a parameter whose default is null takes
  `std::optional<std::string>`, null as `std::nullopt`), `std::optional` for nullable values, `std::complex<double>`;
  `std::vector` out, and in `array_view<T>`, a read-only view of a vector or a braced list, or `nullptr` for null, which
  differs from empty; a key-value collection as pairs; `Matrix` by rows; a `std::vector<double>&` or `Matrix&` nanoql
  writes into in place; a tuple as `std::tuple` both ways (`{}` for C#'s default), a collection of tuples in as an
  `array_view<std::tuple<...>>` and out as a `std::vector` of them, of `std::optional` ones for nullable tuples; a loss
  distribution as `Distribution`; a generator's `draws(count)` its values, weights and next snapshot. A parameter of a
  closed generator construction is an `Object`, the snapshot of a class deriving it, checked at the call.
- **Defaults and overloads:** C++ default arguments with nanoql's defaults, at each argument count the forms whose
  leading parameters agree offering it once, through the shortest (§2.2's rule). A form whose C++ parameters another
  form's are takes its C overload suffix (`outerProduct_doubleArray` beside `outerProduct`), a constructor a
  static `create_...`. A braced list fits
  several collections' views, as in any C++ overload set: a vector names one.
- **Errors:** `nanoql::error`, a `std::runtime_error` with `code()`, `param()` and `type_name()`; under it
  `argument_error`, with `argument_null_error` and `argument_out_of_range_error` under that, and `handle_error` for a
  refused handle (codes 9 and 10).
- **Deprecation:** a member nanoql marks `[Obsolete]` is `[[deprecated]]` with its message.
- **A user's assembly:** a header of its own, `<prefix>.hpp`, its classes in namespace `<prefix>` extending nanoql's
  (`acme::ExponentialCurve` a `nanoql::YieldTermStructure`), linked against the library holding nanoql's assemblies and
  the user's.

## Python

- **Install:** `pip install nanoql`, a `py3-none-<platform>` wheel holding the library for Windows x64 and Linux x64
  (`manylinux_2_28`), with Windows Arm64, Linux Arm64 and macOS on Apple silicon (14 or later) in later releases: one
  wheel for every Python from 3.11, with NumPy 1.26 or later, and nothing compiled on install.
- **Names:** nanoql's type names; members in PEP 8's snake case (`curve.discount(5.0)`, `swap.fixed_rate`); enum members
  as nanoql's (`Compounding.Continuous`). A constructor is the class's call, a getter a property, a static member or an
  extension method a static method of its declaring class (`ConventionSet.quant_lib()`), a record's init-only property
  `with_<name>(value)`, an operator a static `op_<name>` (a record's `==` and `!=` aside, under Objects) and, for
  arithmetic and ordering, Python's operator. A member that would take another's name, or one of `Object`'s, takes a
  suffix: `close_`.
- **Constructors:** every class has its own: its constructors' dispatch, or a `TypeError` where none crosses, never a
  base's, which would build the base under the class's name. A preset's names its lookup by name: `USDCurrency()` raises
  "USDCurrency is made by ConventionSet.currency". An experimental constructor added on the opt-in replaces the
  refusal.
- **Values:** `datetime.date` in and out, `None` for no date; `nanoql.Period(length, units)` or a plain tuple in, a
  `Period` out, its operators and equality nanoql's (`Period(1, Years) == Period(12, Months)`); enums as `IntEnum`;
  NumPy arrays out (`float64` for doubles), any sequence in; a matrix a 2-D array by rows; a span nanoql writes into a
  writable, contiguous NumPy array; lists of objects; a dictionary for key–value results; a tuple in as a tuple (`None`
  for C#'s default) and out as a named tuple (`Result(first, second)`), a collection of tuples in as any sequence of
  them and out as a list of named tuples, `None` for a null tuple, its stubs typed (`Sequence[tuple[VanillaOption,
  float]]`), a tuple of the wrong length refused with a `TypeError`; `nanoql.Distribution` for a loss distribution; a
  generator's `draws(count)` returns its values, weights and next snapshot.
- **Defaults and overloads:** keyword arguments with nanoql's defaults. An overload group is one method that picks its
  form by the arguments' types, exact matches first, then conversions (an `int` for a `float`, a tuple for a period, an
  `int` for an enum), an object nearer the form's class first; each form is declared with `typing.overload`, and at
  each argument count the forms whose leading parameters agree offer it once, through the shortest. The package carries
  `py.typed`.
- **Objects:** each holds a handle, released when Python collects it or at once by `close()` or a `with` block; a closed
  object raises `HandleError` before the call crosses. A result is of the most derived class the package knows;
  equality, hashing and `str()` are nanoql's, and a record's `==` and `!=` are that equality, with no `op_equality` or
  `op_inequality` of their own. A member C# declares `new` with the inherited member's parameters and result overrides
  it here, as in Java: `optionDateFromTenor` on an `InterestRateVolSurface` is the surface's, where C# and C++, through
  a `VolatilityTermStructure`, call the base's.
- **Errors:** `NanoQLError`, with `code`, `param` and `type_name`; `ArgumentError`, also a `ValueError`, and under it
  `ArgumentNullError` and `ArgumentOutOfRangeError`; `HandleError` for a refused handle (codes 9 and 10).
- **Bound engines:** an engine's `bind(...)` and `bind_on(instrument, ...)` return a `BoundEngine` for the members that
  take one.
- **The experimental API:** `import nanoql.experimental` opts in: its classes, and the experimental members of stable
  classes, added on import. A member nanoql marks `[Obsolete]` warns with a `DeprecationWarning` carrying its message.
- **The library:** the package's own (`nanoql/lib/`); `NANOQL_LIBRARY`, or `nanoql.use_library(path)` before any object
  is made, loads another, such as a build holding a user's assembly; once an object exists, `use_library` raises
  `RuntimeError`, since objects of one library are foreign to another.
- **A user's assembly:** a package of its own, named by its prefix (`nanoql-bind python` writes it beside `nanoql/`
  for each `--assembly` prefix other than `nql`): its classes extend nanoql's (`acme.ExponentialCurve` a
  `nanoql.YieldTermStructure`), and importing it loads the library in its `lib/`, which holds nanoql's assemblies and
  the user's, in place of nanoql's own, so it is imported before any nanoql object is made. Its classes are kept apart
  from nanoql's by its prefix, a class named like one of nanoql's included. Two users' packages share a process only
  when built together, into one library.
- **Building:** `python bindings/python/build.py --library <native build folder> --out <folder>` writes the wheel: the
  tool's generated modules (`nanoql-bind python`, never checked in) over the hand-written runtime
  (`bindings/python/nanoql/_runtime.py`), the library, and the notices: `LICENSE`, `NOTICE`, `THIRD_PARTY_NOTICES.md`,
  and the .NET runtime's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`, which Native AOT compiles into the library, each a
  PEP 639 `License-File` under the `License-Expression` `pyproject.toml` gives. `--stage <folder> --test-exports`
  stages the test build's package instead, for the conformance suite.
- **Publishing:** a release's `release.yml` uploads its platforms' wheels through PyPI's trusted publishing,
  with no stored token, after the `pypi` environment's approval; its dry run uploads them to TestPyPI
  ([releasing.md](releasing.md)).

## Java

- **Not released:** the repository builds and tests the Java layer, and no release publishes it, so no Maven artifact
  holds it; a checkout builds its jars (Building, below): the main jar and the platform's natives jar, the same
  artifact with its classifier, `natives-windows-x64`, `natives-windows-arm64`, `natives-linux-x64`,
  `natives-linux-arm64` or `natives-macos-arm64`. It needs Java 22 or later (the FFM API), run with
  `--enable-native-access=ALL-UNNAMED`, and on Linux and macOS with the JDK's libjsig preloaded
  (`LD_PRELOAD=$JAVA_HOME/lib/libjsig.so`, `DYLD_INSERT_LIBRARIES=$JAVA_HOME/lib/libjsig.dylib`): the
  library's runtime installs SIGSEGV and SIGFPE handlers, which libjsig chains behind the JVM's. The variable has to
  reach the `java` process itself: on macOS a launcher that goes through `/bin/sh`, such as Surefire's fork or a
  shell-script launcher, drops `DYLD_INSERT_LIBRARIES`, so a wrapper named `java` sets it and runs the JDK's (CI's Java
  step writes one).
- **The layers:** jextract's, `org.nanoql.ffi.nanoql_h`, over `nanoql.h`, and the idiomatic layer `nanoql-bind java`
  writes over it; neither is checked in. The hand-written runtime is `bindings/java/src/main/java/org/nanoql/`.
- **Names:** a package per namespace (`org.nanoql.termstructures.FlatForward`), the experimental API's in
  `org.nanoql.experimental`; members in QuantLib's camelCase (`curve.discount(5.0)`, `swap.fixedRate()`,
  `yoyInflationIndex()`), a getter a method; a static member or an extension method a static method of its declaring
  class (`ConventionSet.quantLib()`), a record's init-only property `with<Name>(value)`, an operator a static `op<Name>`
  (`Period.opAddition`; a record's equality aside, under Objects). Date's members are statics of `org.nanoql.time.Date`
  taking a `LocalDate` (`Date.year(d)`). A name a keyword or one of `Object`'s takes a trailing underscore
  (`Echo.double_`), and so does a member Java's overriding rules refuse: a static beside an inherited instance member of
  its signature, or a member C# declares `new` with a result the inherited one's isn't
  (`OvernightIndexedSwapIndex.underlyingSwap_`); a form whose erased parameters another form's are takes its C overload
  suffix (`Echo.total_doubleArrayArray`), a constructor a static `create_...`.
- **Values:** `java.time.LocalDate`, `null` for no date; `Period`, a record with nanoql's equality
  (`new Period(1, TimeUnit.Years)` equals twelve months); enums as Java enums with `value()` and `of(int)`, a flags enum
  as a record holding any combination of its constants (`KahaleSmile.or(SabrSmile)`, `has`); a nullable as its boxed
  type (`Double`, `null` for none); `Complex`; numbers in primitive arrays (`double[]`, `int[]`), the rest in lists; a
  matrix a `double[][]` by rows, so a matrix of no rows keeps no column count; a jagged array a `List<double[]>`; a
  key–value collection a `Map` in nanoql's order; a tuple a `Tuple2` or `Tuple3` both ways (`null` for C#'s default), a
  collection of tuples a `List` of them, with a `null` entry for a null tuple out, a `null` in an element that takes
  none refused with a `NullPointerException` naming the parameter and the element ("rows: an element's count is
  null"); a parameter of a closed generator construction a `NanoObject`, the snapshot of a class deriving it, checked at
  the call; a loss distribution `Distribution`; a
  generator's `draws(count)` a `Draws` (values, weights, the next snapshot) or `SequenceDraws`; an array nanoql writes
  into, written in place.
- **Defaults and overloads:** Java has no default arguments, so a form with nanoql's defaults gets telescoping
  overloads, at each argument count the forms whose leading parameters agree offering it once, through the shortest.
- **Objects:** each `NanoObject` owns a handle, released by `close()` (a try-with-resources block) or by a cleaner once
  unreachable; a closed object throws `NanoQLException` with code 9 before the call crosses. Every member keeps the
  objects it reads handles from reachable until its call returns (`Reference.reachabilityFence`), so a temporary's
  handle isn't released mid-call. A result is of the most derived class the layer knows; `obj.as(Cls.class)` gives the
  object as another class, always a new object holding a new handle, for the caller to close, unchecked until a member
  of a class it isn't refuses with code 10. `equals`, `hashCode`, `toString` and `typeName()` are nanoql's, and a
  record's equality is that `equals`, with no `opEquality` or `opInequality`. A member C# declares `new` with the
  inherited member's parameters and result overrides it in Java, as in Python: through a `VolatilityTermStructure`,
  `optionDateFromTenor` on an `InterestRateVolSurface` is the surface's, where C# and C++ call the base's (the tool's
  tests list such members; that is the one).
- **Errors:** `NanoQLException`, a `RuntimeException`, and `NanoQLArgumentException`, an `IllegalArgumentException`,
  for codes 2 to 4; both are `NanoQLError`, with `code()`, `param()` and `typeName()`.
- **Bound engines:** an engine's `bind(...)` and `bindOn(instrument, ...)` return a `BoundEngine` for the members that
  take one.
- **The experimental API:** its classes in `org.nanoql.experimental`; a stable class's experimental members are
  statics of `org.nanoql.experimental.<Class>Experimental`, the object first, a constructor `create`. A member nanoql
  marks `[Obsolete]` is `@Deprecated`, its message in the javadoc.
- **The library:** the natives jar's. On Linux and macOS it is copied into a private temporary folder and deleted with
  it once loaded, which those systems allow; on Windows, which can't delete a library a process has loaded, it is
  copied once to the user's temporary folder under a name its bytes' SHA-256 keys (`nanoql-<hash>/`), and each later
  run loads that copy only when its whole SHA-256 is the jar's library's, writing a fresh one otherwise. The system
  property `nanoql.library`, or `NanoQL.useLibrary(path)` before any object is made, loads another, such as a build
  holding a user's assembly; once a library is loaded, `useLibrary` of another path throws `IllegalStateException`.
  `NanoQL.libraryPath()` gives the path it was loaded from.
- **A user's assembly:** a jar of its own beside nanoql's. `nanoql-bind java --assembly ... --prefix acme` writes its
  classes alone, in packages by its own namespaces (`acme.curves.ExponentialCurve`, a
  `org.nanoql.termstructures.YieldTermStructure`; its experimental ones in `acme.curves.experimental`), each calling its
  own jextract class, `acme.ffi.acme_h` over `acme.h` with its own functions alone (`stage.py`'s `jextract_user` gives
  the flags), and registering its classes through its own `acme.internal.AcmeTypes`, which its classes' static
  initializers run, so its objects come back as its classes. It compiles against nanoql's jar, and `nanoql.library`
  names the library `nanoql-bind build` made of nanoql's assemblies and the user's. The test build's test types are
  built this way, compiled with the conformance suite against nanoql's main classes.
- **Building:** `python bindings/java/stage.py --library <native build folder> --jextract <jextract>` (or
  `--ffi <folder>`, the layers jextract made, a folder per header) fills `bindings/java/target/`: jextract 22's layer
  over `nanoql.h`, the generated layer (`nanoql-bind java --prefix nql`), the notices (`LICENSE`, `NOTICE`,
  `THIRD_PARTY_NOTICES.md`, the .NET runtime's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT`) and the library, and prints
  the natives jar's classifier; `--test-exports` adds the test types' two layers as test sources. Then
  `mvn package -Dnanoql.classifier=<classifier>` in `bindings/java` writes the main, natives, sources and javadoc jars,
  each but the javadoc with the notices under `META-INF`.
  `python bindings/java/jar_check.py bindings/java/target <classifier>` checks them. CI runs jextract once, checked
  against its SHA-256, over `nanoql.h` and the test build's `nqlt.h`, which `nanoql-bind headers` writes without a
  native build; the headers hold fixed-width types only, so the one layer serves every platform.
