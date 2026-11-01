<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->
# The bindings

nanoql crosses to other languages through one C ABI: its assemblies compiled with Native AOT into one shared library
(`nanoql.dll`, `libnanoql.so`, `libnanoql.dylib`) that exports a C function per member, described by a generated header,
`nanoql.h`. Every other language's layer is generated over it: Python (ctypes), C++ (a header with RAII) and Java (the
FFM API). This page gives the C ABI's rules, the tool that generates and builds it, and how it is checked; the
language pages follow.

## The tool: nanoql-bind

`nanoql-bind` (the .NET tool `NanoQL.Bind`, `src/NanoQL.Bind/`) reads built assemblies' metadata without running them,
selects what crosses, and writes the outputs. In the repository, `dotnet run --project src/NanoQL.Bind -c Release --`
runs it:

| Command | What it does |
|---|---|
| `update` | regenerates `bindings/c/include/nanoql.h`, `bindings/surface.json` and `bindings/coverage.md` |
| `check` | regenerates them into memory and compares their bytes with `bindings/`'s; a difference fails |
| `build --rid <rid> --out <folder> [--test-exports]` | generates the export shims, publishes the native host (`src/NanoQL.Native/`) with Native AOT, and writes `include/` and `lib/` |
| `shims --out <folder>` | writes the C# export shims the native host compiles (its build runs this) |
| `python --out <folder> [--test-exports]` | writes the Python package's generated modules into its folder (Python, below) |
| `twins --out <folder>`, `replay-c --out <folder>` | write the vector suite's recording twins and the C replayer's dispatch |
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

## The C ABI

### Names

- **`nql_<Type>_<member>`:** the type's nanoql name and the member's: `new` for a constructor, `get_<P>` for a getter,
  `set_<P>` for a static setter, `with_<P>` for a record's init-only property, `op_<Name>` for an operator (.NET's
  metadata name, `op_Addition`); static and extension members under their declaring type. A type whose simple name
  another type of the same prefix shares takes its namespace's last segment in front.
- **Overloads:** two underscores and the shortest run of the parameters' type names that tells the overload from the
  others: `nql_Calendar_Advance__Date_int` beside `__Date_Period`. Type names: `double`, `int`, `long`, `bool`,
  `string`, `Date`, `OptDate` for `Date?`, `doubleArray` for a collection of doubles, a nanoql type's name. Overloads
  whose C signatures are the same cross once: the one taking a `ReadOnlySpan`, else an `ImmutableArray`, else the first.
- **Generics:** a generic method crosses closed: per named factory where its type argument is a trait's, and at its
  constraint where each type parameter has one class or interface constraint, no constructor or value-type one
  (`nql_CalibratedModelExtensions_Calibrate` takes any calibrated model); any other generic method doesn't cross. A
  closed generic type a crossing function takes or returns crosses as a type of its own, its members too, named by its
  definition and its arguments: `nql_CalibrationResults_Gsr_get_Model`.
- **Released names never move:** each release writes its surface to `bindings/surface.shipped.json`, and the generator
  keeps every released function's and type's name. Only a new member is named against the names in use: a member that
  gains its first overload keeps its short name and the newcomer takes the suffix.
- **Parameters** keep their C# names; one a C or C++ keyword takes, or `self`, `result` or `has_value`, gets a trailing
  underscore.

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
| a tuple | | one out-parameter per element, by its name |
| `Matrix`, `Vector` | values by rows with the rows and columns; values and their count; one the member writes into (`coverage.md`'s list) by `double*`, written back after the call | the same, freed with `nql_free` |
| a generator | its snapshot's handle | `nql_<Generator>_Draws`: draws and weights, the next snapshot |
| a loss `Distribution` | | its normalized buckets' edges, widths, densities, cumulative and excess probabilities and averages |

An optional parameter's default is in the header's comment and in `surface.json`; C passes every parameter, and each
language layer applies the defaults.

### Generators

A generator's constructor returns an immutable snapshot holding a private copy of the generator as built.
`nql_<Generator>_Draws(snapshot, count, &values, &weights, &next)` copies it, draws `count` values (for a sequence
generator, `count` points of its dimension, by rows, the dimension out too) with their weights, and returns the copy as
the next snapshot. The same snapshot always gives the same draws, a stream continues from `next` with nothing
recomputed, and threads may draw from one snapshot at once. A member that takes a generator copies the snapshot's.

### The context and the conventions

- **The context** is a handle: `nql_EvaluationContext_new(evaluationDate)`, then `nql_EvaluationContext_with_<Field>`
  for each other field, each returning a new context.
- **Conventions by name:** the coded presets' constructors (`TARGET`, `Actual365Fixed`, `Euribor6M`, ...) don't cross;
  a convention set gives each by its name: `nql_ConventionSet_Calendar(set, "TARGET", &calendar)`,
  `nql_ConventionSet_IborIndex__string_Period(set, "Euribor", tenor, curve, &index)`. A set comes from
  `nql_ConventionSet_get_QuantLib`, or from XML: `nql_ConventionsXml_Read__string(path, &snapshot)` then
  `nql_ConventionSet_With`. A NULL set is the current provider read as a set: QuantLib's set where the default provider
  is the core's, refused where it is a user's provider. `nql_ConventionProvider_set_Default` sets the default; scopes
  (`ConventionProvider.Use`) don't cross.

### Bound engines

A member that takes one of nanoql's engines as a delegate (an instrument builder's pricer, a calibration helper's
engine) takes a bound engine: an immutable handle naming an engine with its inputs filled in.

- **`nql_<Engine>_Bind`** takes the inputs the engine's calculations take after the instrument and the context, one
  function per set of them, each input optional: NULL leaves it open (a value goes by pointer). **`nql_<Engine>_BindOn`**
  takes the instrument too, for a member that takes the context alone (a composite instrument).
- **The member supplies its own arguments,** always the instrument and the context, and for some members more (a
  calibration helper's trial model, the quanto engine's process, YoY optionlet stripping's trial surface, the callable
  bond's OAS spread). Each goes into the engine's parameter of its role: an object into the one parameter of its type; a
  value, or an object two parameters can take, into the parameter `bindings/roles.json` names; a calibration helper's
  trial model always by its entry's names, cast to that parameter's type at each call (a model the engine can't take
  fails with `NQL_ERROR_INVALID_CAST`). The bound inputs fill the rest, and a parameter open on both sides keeps its
  default.
- **The calculation:** the member takes its engine's calculation for the input set `Bind` named, for the most derived
  instrument type its own is.
- **Refused with `NQL_ERROR_TYPE`** when the member takes the engine: an input both bound and supplied, a required one
  neither, an engine with no calculation for the member, an engine with no parameter for a role (the Black callable
  engines on the OAS members, where QuantLib's engine reads no spread).
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
- A 2.y release may remove or change only what was deprecated at least two releases and at least a year before. A
  member nanoql marks `[Obsolete]` is declared `NQL_DEPRECATED("...")` in the header and deprecated in every layer.

### Fixed functions

`nql_release`, `nql_retain`, `nql_type_name`, `nql_equals`, `nql_hash`, `nql_to_string`, `nql_free`,
`nql_last_error_code`, `nql_last_error_message`, `nql_last_error_param`, `nql_last_error_type`, `nql_version`,
`nql_abi_version`, and `nql_live_handles`, the handles issued and not released, for diagnostics.

## surface.json

The surface's language-neutral description, which every language back end reads and which another generator may read
too. Schema 1, its `"schema"` field raised on any change of meaning; one row a line.

- **`assemblies`:** each input's `prefix` and `name`.
- **`types`:** `name`, `prefix`, `cname`, `base` (the nearest public base of the inputs), `interfaces`, `shape`
  (`class`, `sealed`, `abstract`, `static`, `struct`, `interface`), `form` (how it crosses: `handle`, `value` for
  `Date` and `Period`, `boxed` for another struct, `static`, and for the working types `matrix`, `vector`, `snapshot`,
  `distribution` or `withheld`), `experimental`.
- **`enums`:** `name`, `prefix`, `cname`, `experimental`, `flags`, `members` as `[name, value]`.
- **`functions`:** `name` (the C name), `kind` (`new`, `method`, `static`, `get`, `set`, `with`, `field`, `indexer`,
  `operator`, `draws`, `bind`, `bindon`), `type`, `member`, `key` (the member's metadata key: declaring type, name and
  parameter types), `static`, `experimental`, `deprecated` (its message, when there is one), `overloaded`, `set` (a
  bind's engine input set), `params` (each `name`, `kind`, `type`, and `optional` with its `default`), `returns` (its
  `kind` and `type`), and `c`, the C parameters as `[type, name]`. A kind is a scalar (`double`, `int32`, `int64`,
  `bool`, `string`, `date`, `period`, `enum`, `complex`), a nullable value (`double?` and the like), an object
  (`handle`, `boxed`, `snapshot`, `engine`), a working type's form (`matrix`, `vector`, `distribution`), or a
  collection: `array:<element>`, `span:<element>`, `jagged:double`, `map:<key>:<value>`, `tuple:<element>,...`;
  `vector&` and `matrix&` for one the member writes into.
- **`presets`:** each withheld preset constructor: its `dictionary`, `name`, `member`, the convention set's `lookup`
  function, and its `args`, each a constant, the lookup's parameter or computed.
- **`engines`:** each engine input set: its `id`, `engine`, `inputs` and `calculations`.

## Checking the bindings

- **The surface:** `nanoql-bind check` keeps the checked-in outputs in step with the code. The tool's own tests,
  `bindings/NanoQL.Bind.Tests`, check its rules on nanoql's assemblies and on fixtures it binds as a user's assembly.
- **The test build:** `nanoql-bind build --test-exports` adds the assembly `bindings/NanoQL.Bind.TestTypes` under the
  prefix `nqlt` (an echo per kind, each refusal kind, a deprecated member, a user's generator, and the bound-engine
  cases' markets with their C# routes) and narrows handles' generations to 8 bits; the packages ship without it. Each
  language's conformance suite runs on it: `bindings/c/tests/` for C, `bindings/python/tests/` for Python.
- **The vector suite through each binding:** the recorder (`tools/replay/`) compiles `nanoql-bind twins` with its
  runtime, rewrites the test assembly's call sites into nanoql to call them, and runs the vector classes with their own
  checks against QuantLib's vectors. Each twin writes its call as its C function's: arguments and result in the C forms
  above, objects by ids (a struct by its value), doubles by their bits, a preset's constructor, called or reached by
  reflection, as its set's lookup, a record built by an object initializer as `new` and its `with_` calls, a struct made
  by `default` or `new T()` as its implicit constructor's call, a generator's draws as `Draws` calls along its
  snapshots. A call made from inside nanoql (a callback into the test code) isn't written. Each language's replayer then
  calls its own layer for every line and compares every result with C#'s, bit for bit, NaN as NaN, a new object by its
  class's name (a file-local class's without its compiler prefix, `<File>F<hash>__`, whose hash is of its source file's
  path, so a trace replays against a library built from another checkout); hash and equality calls aren't recorded (the
  conformance suites check them); a fact replays up to its first call that needs something the trace couldn't write (a
  delegate, an object a call that doesn't cross made, a generator a call that doesn't cross moved, or one a crossing
  call took, which C# shares where the C ABI copies; a call that writes into a `Matrix` or `Vector` argument crossing in
  only, which the C call doesn't give back). The C replayer is `bindings/c/replay/`, its dispatch generated by
  `nanoql-bind replay-c`; Python's is `bindings/python/tests/replay.py`, which calls each C function through the Python
  member the generated map names, an overload through its group's dispatch; where the dispatch, reading the arguments'
  own classes, takes another form than C#'s static choice, it counts the call and calls the recorded form. Given
  `bindings/replay-floor.json`, the C replayer also fails below the floor: at least as many facts replayed whole, each
  with calls, as the floor lists (the facts that reach only crossing members), at least its calls replayed, refusals
  matched, and the facts it names whole.

## Debugging into nanoql

- **Windows:** the native library's PDB holds line records for nanoql's C# code, so a debugger attached to a C, C++,
  Python or Java program steps from its call into nanoql's source, the generated export the frame above.
  `tools/replay/stepin.py` shows it from a C program: `python -I tools/replay/stepin.py <exe> nanoql.dll
  "*FlatForward___ctor*"` stops in `FlatForward`'s constructor and prints the stack's source lines. Visual Studio's
  mixed-mode debugging does the same from Python. Source Link is added once the repository is public; until then the
  debugger reads a source checkout.
- **Linux and macOS:** the build strips the DWARF line records into `libnanoql.so.dbg` and `libnanoql.dylib.dSYM`;
  point gdb at a checkout with `set substitute-path <the build's source root> <checkout>`, or lldb with
  `settings set target.source-map <the build's source root> <checkout>`.
- **Symbols ship apart** from the packages, as an archive per platform on the release.

## Platforms

The library is built natively on each of six platforms: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`,
`osx-arm64` and `osx-x64`; Linux in a `manylinux_2_28` container, so it runs on glibc 2.28 and later. It loads no ICU
(invariant globalization), so its messages read the same in every locale and it runs where libicu is absent. Windows
x64 is the validated platform; on the others, a result's last bits may differ where the C runtime's maths functions do.

## Python

- **Install:** `pip install nanoql`, a `py3-none-<platform>` wheel holding the library for Windows x64 and Arm64,
  Linux x64 and Arm64 (`manylinux_2_28`) and macOS x64 and Arm64 (14 or later): one wheel for every Python from 3.11,
  with NumPy 1.26 or later, and nothing compiled on install.
- **Names:** nanoql's type names; members in PEP 8's snake case (`curve.discount(5.0)`, `swap.fixed_rate`); enum
  members as nanoql's (`Compounding.Continuous`). A constructor is the class's call, a getter a property, a static
  member or an extension method a static method of its declaring class (`ConventionSet.quant_lib()`), a record's
  init-only property `with_<name>(value)`, an operator a static `op_<name>` and, for arithmetic and ordering, Python's
  operator. A member that would take another's name, or one of `Object`'s, takes a suffix: `close_`.
- **Values:** `datetime.date` in and out, `None` for no date; `nanoql.Period(length, units)` or a plain tuple in, a
  `Period` out, its operators and equality nanoql's (`Period(1, Years) == Period(12, Months)`); enums as `IntEnum`;
  NumPy arrays out (`float64` for doubles), any sequence in; a matrix a 2-D array by rows; a span nanoql writes into a
  writable, contiguous NumPy array; lists of objects; a dictionary for key–value results, a named tuple for a tuple,
  `nanoql.Distribution` for a loss distribution; a generator's `draws(count)` returns its values, weights and next
  snapshot.
- **Defaults and overloads:** keyword arguments with nanoql's defaults. An overload group is one method that picks its
  form by the arguments' types, exact matches first, then conversions (an `int` for a `float`, a tuple for a period, an
  `int` for an enum), an object nearer the form's class first; each form is declared with `typing.overload`, and at
  each argument count the forms whose leading parameters agree offer it once, through the shortest. The package carries
  `py.typed`.
- **Objects:** each holds a handle, released when Python collects it or at once by `close()` or a `with` block; a
  closed object raises `HandleError` before the call crosses. A result is of the most derived class the package knows;
  equality, hashing and `str()` are nanoql's.
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
- **Publishing:** CI's `publish-pypi` job, dispatched by hand with the `pypi` environment's approval, uploads the six
  bindings jobs' wheels through PyPI's trusted publishing, with no stored token.
