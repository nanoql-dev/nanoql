<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->
<!-- The body quotes licence tags as examples; REUSE reads only the header above. -->
<!-- REUSE-IgnoreStart -->

# Porting playbook

The rules every file ported from QuantLib follows, and how each port is checked against QuantLib. The order of the
work is in [porting-plan.md](porting-plan.md), and what is ported so far is in [porting-map.md](porting-map.md).

## 1 Source, files and headers

- **Source:** QuantLib `v1.44` (`ab4cb476d35f7275834bc05b47d1324403d1823b`), read from a detached worktree of the tag:
  `git -C <QuantLib clone> worktree add --detach <path> v1.44`. Porting never reads a branch that has moved past the
  tag.
- **Files:** one `.cs` file per type, named after the type, in the folder the name table (`tools/porting-map.json`)
  gives its QuantLib file. Every type split from one QuantLib `.hpp`/`.cpp` pair carries that pair's header.
- **Header:** every ported file starts with lines of this form. The example is `ql/time/calendars/target.{hpp,cpp}`,
  whose two files carry the same single copyright line:

  ```
  // SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
  // SPDX-FileCopyrightText: 2026 The nanoql Authors
  // SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
  // Upstream: ql/time/calendars/target.hpp ql/time/calendars/target.cpp @ v1.44
  // Ported to C# from QuantLib and modified by the nanoql Authors.
  ```

  - **QuantLib's copyright lines:** one `SPDX-FileCopyrightText` line per distinct `Copyright (C)` line in the upstream
    files' header blocks, verbatim, in order of first appearance, the `.hpp` before the `.cpp`, read as below.
  - **QuantLib's standard notice** ("This file is part of QuantLib … WITHOUT ANY WARRANTY …") is not copied. The SPDX
    expression and `THIRD_PARTY_NOTICES.md`, which carries QuantLib's `LICENSE.TXT` verbatim, take its place.
  - **Third-party notice blocks** inside QuantLib files (MINPACK's licence in `lmdif.cpp`, MT19937's licence, FDLIBM's
    and Peter Jäckel's permission notices) are reproduced whole, every line in order, as a comment block after the
    header (`//` for a blank line), and once in `THIRD_PARTY_NOTICES.md`.
    - **The licence** joins the SPDX expression as SPDX's own templates match the text: `Minpack`, `SunPro`,
      `BSD-3-Clause` for MT19937's, otherwise `LicenseRef-<name>` with its text in `LICENSES/`. The Joe–Kuo
      direction numbers' licence matches no template, so it is `LicenseRef-JoeKuo`; `BSD-3-Clause` for QuantLib's own
      clause-3 variant is a label for QuantLib's code only.
    - **The list:** each block is a row of `tools/third-party-notices.txt`, with its QuantLib file, its first and last
      lines, its licence and, where the block's text isn't in the QuantLib file (a licence the file points to), its
      source under `LICENSES/`. A row covers every file whose Upstream line names its QuantLib file, so each type split
      from a pair carries the block. A port that meets a new block adds its row.
  - **Reading a notice:**
    - a block comment's leading `*` is comment syntax, not text (`primitivepolynomials.hpp` writes
      ` * Copyright (C) 2002 Peter Jäckel …`);
    - a block starts at its provenance paragraph when that paragraph carries a notice, as the `©` lines above Jäckel's
      notice in the primitive polynomials pair do;
    - a line that differs from a line of another file of the same port only by the replacement character U+FFFD,
      where that line has one character, is read as that line: the replacement character is a byte fault in
      QuantLib's copy, not the notice's text (`brownianbridge.hpp` has one where `brownianbridge.cpp` has the "ä" of
      "Jäckel"). A replacement character with no such twin fails the licensing gate, naming the file and line, and
      waits for a ruling.
  - **The nanoql line:** its year is the year the file was created.
  - **Ported tests** (`test-suite/<name>.cpp`) use the same form.
  - **Boost ports:** Boost's copyright lines, `AND BSL-1.0`, and an Upstream line of the form
    `Upstream: boost/<path> @ boost-1.90.0`. The licensing gate checks them against Boost 1.90.0's release tree (§8):
    the upstream file's notices as written, verbatim and in order. They are the lines of its leading comments, read
    past an include guard, that hold "Copyright", in any of Boost's forms (`Copyright John Maddock 2006.`,
    `(C) Copyright John Maddock 2006.`, `Copyright (c) 2006 Johan Rade`); a line that gives only the years takes its
    holders from the lines after it, joined by one space (`Copyright (c) 2000-2007 Joerg Walter, Mathias Koch, Gunter
    Winkler`). It also checks the SPDX expression, exactly `Apache-2.0 AND BSL-1.0`, and the file's place: the folder
    under `src/NanoQL/Boost/` that the name table's `boost` section gives the path's directory, in namespace
    `NanoQL.Numerics`. An Upstream line names QuantLib paths or Boost paths, never both.
- **Lines directly after the header,** with no blank line between, since the tools read only the header's comment
  block:
  - `// Partial: <members left>`: the type ports only part of its QuantLib file. A test class that ports only some of
    its QuantLib file's cases carries `// Partial: <cases left>`. Every file with a `// Partial:` line is listed in
    [porting-plan.md](porting-plan.md), and the port that completes the type removes the line.
  - `// Own-tested: <members> (<reason>)`: members the C++ harness can't drive, ported with nanoql's own tests,
    with the reason (§9.4).
  - `// Equality-tested: <test file>`: a type whose only code is equality that nanoql adds, checked by its own test
    (§9.5).
  - `// Adapted: <reason>` on a ported test method that QuantLib's case doesn't run as written, naming the lines it
    drops.
  - `// Replaced: <case> (<test file>)[; <case> (<test file>)]` on a test class: QuantLib test cases replaced for good
    by a nanoql test, each with that test's file under `tests/NanoQL.Tests/` (`// Replaced: nullDate
    (Divergences/NullDateTests.cs)`). The porting map lists them as replaced and counts them as done. The licensing
    gate checks that each is a case of the class's QuantLib test file, neither ported nor on its `// Partial:` line,
    and that the test file exists.
- **Files that replace a QuantLib file** may name it in their Upstream line, with its copyright lines, so the map
  shows which nanoql file takes its role: `EvaluationContext.cs` names `ql/settings.hpp` and `ql/settings.cpp`. A
  file may not name a file the name table marks as not ported.
- **Files with no upstream** (nanoql's own types, test helpers, tools) carry only the nanoql copyright line and the
  Apache-2.0 licence identifier, and are listed in `tools/no-upstream.txt`.

## 2 Translation rules

| QuantLib | nanoql |
|---|---|
| `Observable`, `Observer`, `registerWith`, `update`, `notifyObservers` | Removed. |
| `Handle<T>`, `RelinkableHandle<T>` | The object itself (`T`). Relinking means building a new dependent object. |
| `Quote`, `SimpleQuote`, `Handle<Quote>` | `double`. Quotes are input values. Overloads that become identical once a quote is a `double` collapse into one. |
| `LazyObject` (`performCalculations`, `calculated_`, `freeze`) | Computed in the constructor or a static factory and stored in readonly fields. No caching. |
| A `mutable` cache outside `LazyObject` (`Coupon::accrualPeriod_`) | Recomputed on each call. |
| `Instrument` + `PricingEngine` | The instrument is an immutable object holding its terms. The engine is a static class whose `Calculate` runs QuantLib's four steps (§2.1). `swap.setPricingEngine(e); swap.NPV()` becomes `DiscountingSwapEngine.Calculate(swap, context, discountCurve).NPV`. |
| `Settings::instance()` | `EvaluationContext`, passed to every function that reads it (§2.2). `Settings::instance().evaluationDate()` becomes `context.EvaluationDate`. |
| `IndexManager` fixing histories | `context.Fixings`. Index objects hold conventions only. |
| `IborCoupon::Settings`, `ExchangeRateManager`, `Money::Settings`, the commodity settings | Fields of `EvaluationContext`: every QuantLib global that pricing reads becomes one field, added by the port that first needs it. |
| Term structures whose reference date moves with the evaluation date | `Create(..., EvaluationContext context)` factories. Every built term structure has a fixed reference date. |
| `Calendar`, `DayCounter` (Pimpl) | An abstract class with sealed implementations (§2.4). `addHoliday` and `removeHoliday` return a new calendar, as `ImmutableList<T>.Add` does. In QuantLib the change applies process-wide, because all instances of a calendar share one implementation; in nanoql it is part of the calendar's value. |
| An empty Pimpl value (`Calendar()`, `DayCounter()`, `empty()`) | `null` (`Calendar?`, `DayCounter?`). |
| Visitor (`accept(AcyclicVisitor&)`) | A `switch` pattern match at the call site. |
| Templates and traits | Static abstract interface members for stateless traits. A parameter that is configuration is a generic struct parameter, and a generator is a class parameter (§2.6). A template over plain values becomes an ordinary generic method. A template member that needs more of its parameter than its class does (C++ instantiates only the members a caller uses) becomes a generic method with that constraint on the class's companion static class: a constructor becomes a factory (`RandomSequenceGenerator.Create<TRng>(dimensionality, seed)`), an instance member an extension method (`NextInt32Sequence<TRng>()`). A call that breaks the constraint fails to compile, as the C++ instantiation does. |
| `Array`, `Matrix`, `TridiagonalOperator` | Mutable working types (§2.5). `Array` is renamed `Vector` (§3). |
| `Array::operator+=` and the other compound assignments of `Array` and `Matrix` | In place, through C# 14's instance compound-assignment operators (`public void operator +=(Vector other)`, and `-=`, `*=`, `/=` with vectors and scalars), as QuantLib's change their target; the binary operators return new objects, and `Clone()` is a deep copy. C# uses the instance operator only when the target is a variable (a local, a parameter, a field, an array element). On a property, or an indexer that doesn't return a `ref`, `x.P += y` calls the binary operator and the setter, so it rebinds. So a `std::vector<Array>` whose elements change in place becomes an array (`Vector[]`), never a `List<Vector>`, and the review checks every compound assignment whose target is a property or an indexer. |
| A function-call `operator()` (a distribution, an interpolation, a solver's function, `EndCriteria`'s check) | A method named `Evaluate`. An `operator()` that reads an element (`Matrix(i, j)`) becomes an indexer. |
| Random-number and path generators | Mutable working types with QuantLib's API (§2.7). |
| `OptimizationMethod::minimize(Problem&, …)` | `Minimize(problem, endCriteria)` returns a `ProblemResults` record holding what QuantLib leaves in the `Problem` it changes: `CurrentValue`, `FunctionValue`, `GradientNormValue`, the two evaluation counts, and the `EndCriteriaType`. `Problem` is an immutable description (cost function, constraint, initial value), and a method holds only its configuration. The methods work on a `ProblemState`, a working type built from a `Problem` that carries QuantLib's mutable `Problem` API (`Reset`, `Value` and `Gradient`, which count, `CurrentValue`, whose setter copies, and `FunctionValue`). `Minimize(problemState, endCriteria)` continues from the state's current value, for line searches and hybrid methods; both call `protected abstract MinimizeCore(ProblemState, EndCriteria)`. A second `minimize` on one QuantLib `Problem` starts from the last optimum, where `Minimize(problem, …)` starts from the initial value each time: a documented divergence. |
| `Solver1D<Impl>` (the curiously recurring base) | `Solver1D<TSelf, TFunction>`. `TSelf` types the setters that return a modified copy (`SetMaxEvaluations`, `SetLowerBound`, `SetUpperBound`), and `TFunction` is the function concept the solver needs (`ISolver1DFunction`; `IDerivativeSolver1DFunction` for `NewtonSafe`). The public non-virtual `Solve<TF>(f, accuracy, guess, step)` and `Solve<TF>(f, accuracy, guess, xMin, xMax)`, `where TF : struct, TFunction`, run QuantLib's bracketing and then call `protected abstract SolveCore<TF>(f, accuracy, bracket)`, whose `Solver1DBracket` record holds what QuantLib's base sets before `solveImpl`. Each solver adds overloads taking delegates. |
| `CalibratedModel::calibrate` | A static `Calibrate` that returns an immutable parameter object. |
| `SeedGenerator` | An explicit input: a seed is a parameter, a `long` in [1, 4294967295] with no default, refused otherwise with `ArgumentOutOfRangeException`. QuantLib's seed 0, which draws a seed from the clock, is refused: a documented divergence. No library code reads the clock. |
| `QL_REQUIRE` on a public method's parameter | `ArgumentException`, with `ParamName` set. A null argument a member doesn't accept throws `ArgumentNullException`. |
| An enum argument a member always refuses: a value outside the type's members (C# lets any integer be cast to an enum), or a member it never handles (`Period(OtherFrequency)`) | `ArgumentOutOfRangeException`, naming the parameter, with QuantLib's message. A public member that switches on the value, stores it or computes with it checks it on entry, before any work that reads it; one that passes it on under the same parameter name to such a member relies on that member's check; one that only compares it with named members needs no check. This replaces QuantLib's `QL_FAIL`, in a switch's default or on a `case` label, wherever the value is an argument the member always refuses. A value refused only together with other arguments keeps the other rows' mapping: a mixed compounding in `ImpliedRate`, refused unless the compound factor is 1, and an intraday unit in `Calendar.Advance`, refused unless the count is 0, throw `NanoQLException`. |
| Other `QL_REQUIRE`, `QL_FAIL`, `QL_ENSURE` | `NanoQLException`, including a `QL_FAIL` on a value already stored. A value outside its enum can no longer be stored, so such a branch fails only for a member the operation doesn't handle (`Period(3, Hours).Frequency()`), as in QuantLib. The wording is kept; numbers in it use C# formatting. |
| `Null<T>()`, `ext::optional<T>` | `T?`. QuantLib's null `Date()` is `Date?` null; `default(Date)` is never a valid date, and public members given it throw `ArgumentException`. Ported `d == Date()` becomes `d is null`. |
| `ext::shared_ptr<T>`, `ext::make_shared` | A plain `T` reference; `new T(...)`. |
| `ext::function` | `Func<…>` or `Action<…>`. |
| `std::vector<T>` / `std::map` stored in an object or taken as a public input | `ImmutableArray<T>` / `ImmutableSortedDictionary<,>`. Local computations may use arrays and spans. Public immutable objects never expose mutable containers. |
| `std::set<T>` stored | `ImmutableSortedSet<T>`. |
| A collection returned by a public method (`holidayList`, `dates()`) | `ImmutableArray<T>`, never null. |
| `begin()`/`end()` iterators | A public `GetEnumerator()` over the stored array, so `foreach` works. A function returning an iterator position (`lower_bound`) returns an index (`int`). |
| A mutator on a value type with a non-mutating counterpart (`Period::normalize()`, `Date::operator+=`) | Not ported: `Normalized()`, and C#'s compound operators, built from `+`. |
| A mutator on an immutable class (`Extrapolator::enableExtrapolation`) | The same name returns a modified copy. Where the copy must keep the receiver's static type, it is a generic extension method. |
| `boost::math::…`, uBLAS | Ported from Boost 1.90.0, uBLAS's LU among them; other uBLAS-backed `Matrix` operations wait until a port needs them. |
| A Boost.Math error under the default policy, which throws | A C# exception with Boost's message, its numbers in C# formatting: a domain error on the caller's argument (a probability outside [0, 1], a NaN) throws `ArgumentOutOfRangeException`; a domain error on a stored parameter (an infinite mean) and an overflow (the quantile of 0 or 1) throw `NanoQLException`. A `Conventions/` test asserts each type, which a vector's `{"throws": true}` can't. |
| `namespace detail` | `internal` types. |
| Anonymous namespaces | `file` types, and a `file static class` for their functions. Helpers shared by types split from one `.cpp` become an `internal` type named `<QuantLib file in PascalCase><Name>`. |
| Free functions | Static methods of a public static class per header, `<Header>Functions` (§3). |
| `io::` formatters (`io::ordinal`, `io::iso_date`) | Methods of the header's `<Header>Functions` class returning `string`. `io::formatted_date` is not ported: a `Date` converts to `DateOnly`, whose `ToString(format)` does that job. |
| `operator<<` on a class | `ToString()` with QuantLib's text. |
| `operator<<` on an enum | Not ported where the enum's `ToString()` already gives QuantLib's text: its stream strings equal the member names and no alias shares a value. Otherwise the text is a static `ToString(value)` on the header's `<Header>Functions` class, and a class whose `ToString()` embeds it calls it. Each port records this check per enum. |
| Operators whose operands nanoql doesn't own (`Integer * TimeUnit`) | C# 14 extension operators, in a static class `<Type>Extensions` in the folder of the QuantLib file that declares them. |
| `std::fma` (in Boost code) | `Math.FusedMultiplyAdd`. |
| The numeric macros of `qldefines.hpp` (`QL_EPSILON`, `QL_MIN_POSITIVE_REAL`, …) | `internal const` fields of `NumericLimits`, keeping the macro names. `QL_EPSILON` is machine epsilon, `2.220446049250313E-16`, and `QL_MIN_POSITIVE_REAL` the smallest normal double; neither is `double.Epsilon`. |
| Deprecated members, `ql/legacy` | Not ported. |
| `#ifdef QL_HIGH_RESOLUTION_DATE`, sessions, thread-safe observer options | The default build's branch only. The wheel's build flags are checked against this (§7). |
| Enums | C# enums with QuantLib's numeric values, so vectors carry them as integers. |
| A Pimpl wrapper whose public member adds logic before calling the implementation's virtual (`Calendar::isBusinessDay`) | A public non-virtual member that calls a `protected abstract <Name>Core` (`IsBusinessDay` calls `IsBusinessDayCore`), as `HashAlgorithm` does with `HashCore`. Where the wrapper only delegates (`DayCounter::yearFraction`), the virtual member is the public member itself. |
| A protected or private nested type (`Calendar::WesternImpl`) | Stays nested, at the same accessibility. Only public nested types move to the top level (§3). |

### 2.1 Engines

`XEngine.Calculate(instrument, context, <QuantLib's engine constructor parameters, in order, with QuantLib's defaults>)`
first checks its arguments, throwing `ArgumentNullException` for a null instrument, context or required input. Then it
reproduces `Instrument::calculate`, in order:

1. if `instrument.IsExpired(context)`, it returns `instrument.SetupExpired()` without running the engine body;
2. `instrument.Validate()`, from QuantLib's `arguments::validate`;
3. the engine body;
4. it returns `instrument.FetchResults(results)`, which applies the instrument's results derivations.

Steps 1, 2 and 4 are virtual helpers the instrument owns, under QuantLib's names, which every engine for it calls and
derived instruments override. Because the argument checks come first, an expired instrument priced without a required
input throws, where QuantLib returns its expired results.

**Results** are an `<Instrument>Results` record on an inheritance chain that mirrors QuantLib's (`InstrumentResults` ←
`SwapResults` ← `FixedVsFloatingSwapResults`).
- Members are named after QuantLib's instrument accessors: `NPV`, `ErrorEstimate`, `ValuationDate`,
  `AdditionalResults`, `LegNPV`, `LegBPS`, `StartDiscounts`, `EndDiscounts`, `NpvDateDiscount`.
- Per-leg results are `ImmutableArray<double?>`; additional results are `ImmutableSortedDictionary<string, object>`.
- A result QuantLib leaves null is `null`. Where QuantLib throws "result not available", "error estimate not provided"
  or "valuation date not provided" on reading it, nanoql returns the null and the caller checks it.
- Results records write out their value equality over their arrays and additional results, element by element,
  including a collection value among the additional results (§4).

### 2.2 The evaluation context

- **Which functions take it:** every function that reads the evaluation date or another context field, directly or
  through a callee, including where QuantLib reads it only to fill a null argument (a null effective date in
  `Schedule`, a null settlement date in `CashFlows::npv`).
- **Where it goes:** after QuantLib's parameters that have no default, before those that do, since C# requires
  optional parameters last. For engines that puts it second.
- **Its checks:** every public member that takes the context throws `ArgumentNullException` for a null one, first.
- **Its shape:** `EvaluationContext` is a `sealed record` class. `EvaluationDate` is a `required` property, and every
  other field is an init-only property whose default reproduces QuantLib's default setting, never a constructor
  parameter and never `required`. `default(Date)` is refused in `EvaluationDate`'s `init` accessor, so in `with` too.
  Variations are made with `with`.
- **Later fields** are added the same way, and no default changes once released, so existing callers keep compiling
  and existing binaries keep running.
- **Equality:** the record's synthesized equality holds while every field is a value. The port that adds a collection
  or registry field states that field's equality, because records compare such members by reference.
- **"Today":** the context has no "today". `Date.TodaysDate(TimeProvider?)` reads the clock at the API's edge, and no
  library code calls it.

### 2.3 Builders

QuantLib's builders (`MakeSchedule`, `FixedRateLeg`, `MakeVanillaSwap`, …) are immutable fluent builders.
- QuantLib's class and method names are kept (`From`, `To`, `WithTenor`, `Forwards()`, `EndOfMonth(bool)`).
- A builder is a `sealed record` holding its settings as init properties, and each method returns a modified copy
  (`with`), so a builder kept and reused is never changed under its holder.
- QuantLib's conversion operator becomes a method named `To<Type>()` (`MakeSchedule.ToSchedule()`,
  `FixedRateLeg.ToLeg()`). There is no implicit conversion, since the conversion can throw.

### 2.4 Kinds, sealing and extension points

- **Classes, not structs,** unless the type's instance is under 16 bytes, immutable and value-like (`Date`, `Period`),
  or it is a generic pair such as `Sample<T>`, which .NET's own pairs (`KeyValuePair<TKey,TValue>`,
  `ValueTuple<T1,T2>`) make structs whatever their type arguments: as a class, a sample would allocate on every draw.
  The configuration structs of §2.6 are the other structs.
- **Extension points:** the public bases that QuantLib's hierarchy derives from, abstract or unsealed, are nanoql's
  extension points: term structures, stochastic processes, volatility structures, calendars, day counters, the
  interpolation traits, events, cash flows, coupons, extrapolators, and instruments with their results records,
  including concrete bases QuantLib extends, such as `Swap` and `SimpleCashFlow`. Each port lists the extension points
  it adds. The API reference lists them, and the release notes call out every change to one.
- **Every other public class is sealed.** A subclass could add mutable state or change behaviour, breaking the
  immutable, thread-safe promise for every caller of the base type, and could break equality nanoql defines by
  content. Opening a sealed class later is a compatible change; sealing an open one is not. Users compose instead, or
  add members through C# 14 extension members.
- **Extension points so far:** `Calendar`, `DayCounter`, `Event`, `CashFlow`, `Coupon`, `Extrapolator`,
  `TermStructure`, `YieldTermStructure`, `Instrument` and `TridiagonalOperatorTimeSetter` (abstract); `Swap`,
  `SimpleCashFlow`, `InstrumentResults`, `SwapResults` and `TridiagonalOperator` (unsealed).
- **Leg:** QuantLib's `Leg` is the global alias
  `global using Leg = System.Collections.Immutable.ImmutableArray<NanoQL.CashFlow>;` in `Types.cs`. Public signatures
  show `ImmutableArray<CashFlow>`.

### 2.5 Working types, views and values

- **Working types** are mutable, owned by one computation and not thread-safe, as an array is: `Vector`, `Matrix` and
  `TridiagonalOperator` (an indexer that sets, compound assignment, `Resize`, the in-place operations QuantLib's
  algorithms use, `TridiagonalOperator`'s row setters), `ProblemState`, and the random-number and path generators
  (§2.7). Later ports add lattice assets, finite-difference operators and schemes, and market-model evolvers and
  accumulators, and each names the working types it adds. Every other public type is immutable.
- **A copy where C++ copies:** a C# class is a reference, so a port copies explicitly (`Clone()`) wherever C++ copies
  an `Array` or `Matrix` by value (an assignment, a copy construction, a by-value parameter) and then changes either
  copy. The review checks each such site.
- **The boundary:** an immutable object never holds a working type it was given, and never exposes one it holds. It
  copies its inputs (into an `ImmutableArray<double>` or a private copy), and returns a fresh object, or an
  `ImmutableArray<double>` where the value is data it holds. Each constructor that takes a `Vector` or a `Matrix` has
  a `Conventions/` test that changing the argument afterwards changes nothing.
- **Views:** a generator's draw returns a view of its own buffer (§2.7), read-only to the caller and valid until the
  generator's next draw, which changes it, as QuantLib's `const&` is. The immutability promise doesn't cover a view; a
  caller that keeps one copies it.

### 2.6 Configuration as generic structs

- **A template parameter that configures a computation** (an interpolator factory, an inverse cumulative, a solver's
  function) is a generic parameter `where T : struct, I<Concept>`: a `readonly struct` implementing the concept,
  stateless (`Linear`) or holding its parameters (`Cubic`), passed as an instance. The interpolation factories
  implement `IInterpolator`: an instance `Interpolate(x, y)` and the static abstract `Global` and `RequiredPoints`.
  A consumer is generic over `TInterpolator : struct, IInterpolator` and takes the instance.
- **The zero value is QuantLib's default construction:** `default(T)`, `new T()`, an unassigned field and an array
  element are QuantLib's default-constructed object.
  - A field whose QuantLib default isn't zero is stored so that zero reads as that default: a value the constructor
    refuses (`InverseCumulativeNormal`'s sigma, where 0 reads as 1), or a nullable enum (`Cubic`'s derivative
    approximation, where null reads as Kruger).
  - No such struct declares a parameterless constructor. QuantLib's `const Interpolator& i = {}` becomes
    `TInterpolator i = default`, and a functor member QuantLib never assigns is a field initialised `= default`.
  - A type QuantLib can't default-construct (`LogCubic`, whose derivative approximation has no default) refuses its
    zero value in every member with `InvalidOperationException`, as `ImmutableArray<T>`'s default does.
  - QuantLib's presets that derive from a factory (`MonotonicLogCubic` and `KrugerLog`, from `LogCubic`) are
    parameterless structs of their own, so they can be type arguments, each with an implicit conversion to the
    factory for the calls that pass a preset where the factory is taken.
  - Each such struct has a `Conventions/` test that its zero value is QuantLib's default, or is refused.
- **Stepping state is a class parameter:** a generator changes on every draw, so a template over one takes a class
  (§2.7).
- **Named classes:** QuantLib's typedef names for traits (`PseudoRandom`, `LowDiscrepancy`, `PoissonPseudoRandom`) are
  named structs. QuantLib-Python's `%template` names are sealed subclasses of their closed generic class, one per
  closed generic, added by the port that ports the last of its type arguments; where QuantLib-Python names one type
  twice, the name whose arguments are QuantLib's own names. nanoql's own code uses the generic types, so a named class
  is a convenience, never a type argument. A generic struct (`Sample<T>`) gets none.

### 2.7 Random numbers and Monte Carlo paths

- **Generators** (`MersenneTwisterUniformRng`, `SobolRsg`, `HaltonRsg`, `RandomSequenceGenerator<TRng>`, the
  inverse-cumulative compositions, `PathGenerator<TGsg>`) are mutable classes with QuantLib's API: their draws
  (`Next()`, `NextSequence()`, `NextInt32()`, `SkipTo(n)`, `Antithetic()`) change them, as `System.Random`'s do.
  They aren't thread-safe, which their docs say.
- **A draw's sample** is a `Sample<T>`, a value and its weight, whose value is a view of the generator's buffer, valid
  until its next draw: `Sample<ReadOnlyMemory<double>>` for a sequence, `Sample<MonteCarloPath>` for a path. No draw
  allocates.
- **Inside a computation:** an engine or another immutable object takes a seed and builds its generators inside the
  call. No immutable object holds a generator, so engines stay pure functions of their inputs, the seed among them.
  Across the C interface and Excel, a generator is a pure function of its seed, drawn in batches.
- **Seeds and integers:** a seed is a `long` in [1, 4294967295] with no default (§2's `SeedGenerator` row). QuantLib's
  32-bit unsigned values (seeds, `NextInt32()`, Sobol's integer sequences, `SkipTo`'s argument, `BigNatural`
  arguments) are `long` in public members, each range-checked. Internals use `uint`, because QuantLib's
  `unsigned long` and `BigNatural` are 32 bits under MSVC, where the vectors are generated: their sums wrap at 2^32.
- **The concepts** are QuantLib's `RNG` and `RSG`, named in its vocabulary: `IRng` (`Sample<double> Next()`,
  `IRng Clone()`), `IInt32Rng : IRng` (`long NextInt32()`), and `IRsg` (`Dimension`, `NextSequence()`,
  `LastSequence()`, `IRsg Clone()`). They carry uniform and Gaussian generators alike, as QuantLib's templates do; a
  template whose input must be uniform says so in its docs. Construction from a seed in generic code is a static
  abstract factory on separate interfaces, used only as constraints: `ISeedableRng<TSelf>` (`static abstract TSelf
  Create(long seed)`) and `ISeedableRsg<TSelf>` (`static abstract TSelf Create(int dimension, long seed)`). An
  interface with a static abstract member can't be a type argument, hence the split.
- **A generic class asks of its parameter only what all its members need:** `RandomSequenceGenerator<TRng> where TRng
  : class, IRng`, since QuantLib's takes any generator. Its seed constructor is the companion's
  `RandomSequenceGenerator.Create<TRng>(dimensionality, seed)`, constrained to an `ISeedableRng`, and
  `nextInt32Sequence` the extension `NextInt32Sequence<TRng>()`, constrained to an `IInt32Rng` (§2's templates row).
  A composite whose QuantLib seed constructor builds a `RandomSequenceGenerator<RNG>` from a seed takes the RNG as its
  type parameter and builds the sequence generator through `Create<TRng>`; a seedable sequence generator of another
  type needs a factory of its own name, since C# can't overload on constraints alone.
- **Copies:** `Clone()` is a deep copy, the state and the buffers: the copy's next draw equals the original's, into
  its own buffer. Each generator declares it with its own return type, and a named class overrides it covariantly. A
  composite (`RandomSequenceGenerator`, `InverseCumulativeRng`, `InverseCumulativeRsg`, `PathGenerator`) clones the
  generator it is given, as QuantLib's by-value constructors copy it, so the caller's and the composite's states never
  alias. A factory that has just built its generator (the traits, `Create`) adopts it without a copy.
- **The traits:** `IRngTraits` declares `static abstract bool AllowsErrorEstimate` and `static abstract IRsg
  MakeSequenceGenerator(int dimension, long seed)`. A Monte Carlo engine keeps QuantLib's one traits parameter
  (`where TTraits : struct, IRngTraits`) and builds a `PathGenerator<IRsg>`. Called on the traits struct itself,
  `MakeSequenceGenerator` returns the closed generic, QuantLib's `rsg_type`, and an explicit implementation provides
  the interface's form. QuantLib's static `icInstance` isn't ported: an overload of `MakeSequenceGenerator` takes the
  inverse cumulative.

## 3 Naming

- **Types:**
  - QuantLib's names are kept, acronyms included (`NPV`, `OISRateHelper`), so a QuantLib name maps to one nanoql
    name. Three are renamed: `Array` becomes `Vector`, `Path` becomes `MonteCarloPath`, and `Index` gets its name in
    the port that brings it. A user who imports a clashing `System.*` namespace writes an alias
    (`using Calendar = NanoQL.Time.Calendar;`).
  - **Public nested types** move to the top level as `<Outer><Inner>`: `Option::Type` → `OptionType`,
    `Swap::Type` → `SwapType`, `DateGeneration::Rule` (a struct holding an enum) → `DateGenerationRule`.
  - **Renames** are recorded here. So far: `Array` → `Vector`, which clashes with `System.Numerics.Vector` in files
    that also import `System.Numerics`; and `Path` → `MonteCarloPath`, after its folder, `montecarlo`, since `Path`
    would clash with `System.IO.Path`. QuantLib's other names around it stay (`PathGenerator`, `MultiPath`).
- **Members:** the first letter is capitalised and the rest kept (`yearFraction` → `YearFraction`; `NPV` stays).
  - **Property or method:** a member is a property when it takes no arguments, has no side effect, is cheap, has no
    precondition that throws, doesn't return a copy or a new array, and is an attribute of the object, stored or
    derived (`Date.Month`, `Date.Weekday`). Otherwise it is a method (`Period.Frequency()`, which can throw;
    `Schedule.StartDate()`, which throws on an empty schedule).
  - A zero-argument accessor that shares its name with an overload taking arguments stays a method, since C# can't
    overload a property with a method (`Schedule.IsRegular()` beside `IsRegular(int)`).
  - A virtual accessor is a property only if every QuantLib override passes the test (`CashFlow.Date`); otherwise it
    is a method (`CashFlow.Amount()`, which some overrides compute).
  - A method named after a type hides that type inside the class (CS0119); a property named after its type doesn't.
    Where a method must keep such a name (`Schedule.Date(int)`), the class writes the type fully qualified.
- **Parameters:** QuantLib's camelCase names, unchanged.
- **Free functions:** one public static class per QuantLib header, named after the header's file name in PascalCase,
  split into words as the name table records them, plus `Functions`: `period.hpp` → `PeriodFunctions`,
  `comparison.hpp` → `ComparisonFunctions`, `dataformatters.hpp` → `DataFormattersFunctions`. Callers may write
  `using static NanoQL.Time.PeriodFunctions;`.
- **Operators:** `operator==`, `operator<` and `operator*` become operators on the types they take; where nanoql
  owns neither operand type, C# 14 extension operators (§2).
- **Files, folders and namespaces:**
  - one type per file, named after the type;
  - folders mirror QuantLib's directory tree in PascalCase, from the name table. A grouping subfolder, named after
    the QuantLib file, holds the types split from one file pair when it yields three or more (`Time/Date/`);
  - namespaces are `NanoQL` plus one level, one per QuantLib top-level directory: `NanoQL` for QuantLib's root
    files, `NanoQL.Coupons` (`ql/cashflows`, since `CashFlows` is a class there), `NanoQL.Numerics` (`ql/math`, since
    `NanoQL.Math` would hide `System.Math`), `NanoQL.Time`, `NanoQL.TermStructures`, `NanoQL.Instruments`,
    `NanoQL.PricingEngines`, `NanoQL.Utilities`, and so on. Deeper folders add no namespace segment, so IDE0130 is
    off. QuantLib's `Time` typedef gets no alias, since it would collide with `NanoQL.Time`.

## 4 Data conventions

- **`Date`:** a `readonly struct` over QuantLib's serial number, with the same range and arithmetic: serial 367
  (1 January 1901) to 109574 (31 December 2199). It converts to and from `DateOnly`. There is no time of day.
- **`Period`:** a `readonly struct`. Equality, `==` and `GetHashCode` follow QuantLib's equality and never throw:
  every zero-length period equals every other and hashes to one value; other periods compare normalised (years as
  months, weeks as days); an undecidable pair (`1M` against `30D`) compares unequal, where QuantLib's `==` throws.
  `CompareTo`, `<` and `>` follow QuantLib's ordering and throw `NanoQLException` on undecidable pairs, as QuantLib
  does.
- **Calendars** are equal when they are of the same type and have the same name and the same added and removed
  holidays, compared by content; the type keeps equality symmetric between a calendar and a subclass that compares
  more. A calendar built from others (a joint calendar) is equal to another only when its rule and its components
  are equal too, each component by the same rule. The hash combines the same values.
- **Day counters** are equal by name, as in QuantLib.
- **Missing values:** `T?`.
- **Collections:** stored as `ImmutableArray<T>`; inputs are taken as `IEnumerable<T>` or `ReadOnlySpan<T>` and
  copied. A record that holds collections and needs value equality writes it out, because `ImmutableArray<T>`
  compares references.
- **Integers:** public members use `int` for `Size`, `Natural` and `Integer`, and `long` for `BigInteger` and for
  QuantLib's 32-bit unsigned values (seeds, `BigNatural`, generators' integers; §2.7). Internals use unsigned types
  where QuantLib relies on unsigned arithmetic (MT19937 state, Sobol direction integers, Halton's sums): `uint` for
  QuantLib's `unsigned long` and `BigNatural`, their width under MSVC.
- **Aliases:** `Types.cs` makes global aliases for `Real`, `Rate`, `Spread`, `DiscountFactor`, `Volatility`,
  `Probability` (`double`), `Size`, `Integer`, `Natural` (`int`) and `BigInteger` (`long`), and the `Leg` alias (§2.4).
  `Decimal` and `Time` are written as `double`. The test project links `Types.cs`, so ported tests keep QuantLib's
  names.
- **Errors:** `ArgumentException` for a `QL_REQUIRE` on a public method's parameter, and
  `ArgumentOutOfRangeException` for an enum argument a member always refuses; QuantLib's other checks throw
  `NanoQLException` (§2's table), which derives from `Exception` and has the three standard constructors.
- **Experimental code:** types ported from `ql/experimental` carry `[Experimental("NQL0001")]`. NanoQL's own projects
  consent with `<NoWarn>$(NoWarn);NQL0001</NoWarn>`; a consumer that references one still gets the diagnostic.
- **Docs:** Doxygen comments become XML doc comments. A malformed comment or an unresolved `cref` fails the build, so
  a ported `\ref` is resolved or dropped.

## 5 Project layout

```
NanoQL/
  LICENSE                       Apache-2.0
  LICENSES/                     Apache-2.0.txt, BSD-3-Clause.txt, and the licences of ported third-party code
  NOTICE, THIRD_PARTY_NOTICES.md (QuantLib's LICENSE.TXT verbatim), README.md, CONTRIBUTING.md
  REUSE.toml                    licensing for files without a comment header
  global.json                   SDK 10.0.300 (rollForward latestFeature); the test runner, Microsoft.Testing.Platform
  Directory.Build.props         net10.0, nullable, warnings as errors, XML docs, embedded sources, symbol packages,
                                deterministic builds, StyleCop's SA1402 and SA1649 only
  .editorconfig                 IDE0130 off; SA1402 and SA1649 as errors
  NanoQL.slnx
  src/NanoQL/NanoQL.csproj      IsAotCompatible; NoWarn NQL0001
  src/NanoQL/<Dir>/[<File>/]<Type>.cs   one type per file, QuantLib's directories mirrored in PascalCase
  src/NanoQL/Boost/<Path>/<Type>.cs     Boost-derived internal types
  tests/NanoQL.Tests/           xUnit v3 on Microsoft.Testing.Platform, with Coverlet's extension (coverlet.MTP)
  tests/vectors/<ql path>.json  committed vectors (§7.2); tests/vectors/cxx/<ql path>.json, the C++ harness's
  tools/vectors/                the hash-pinned requirements, the generators gen/<ql path>.py and gen/cxx/<ql path>.py,
                                the shared helpers, the harness's client cxx.py and the twins' check agree.py
  tools/cxx/                    the C++ harness (§7.1): CMakeLists.txt, src/<ql path>.cpp, build.py
  tools/check-upstream.py       the licensing gate's upstream checks; tools/no-upstream.txt, its allowlist
  tools/porting-map.json        the name table
  tools/third-party-notices.txt the third-party notice blocks the licensing gate checks
  docs/porting-playbook.md, docs/porting-plan.md, docs/porting-map.md (generated)
```

- **The name table**, `tools/porting-map.json`, holds each QuantLib directory's folder and namespace, each ported
  file's word split and grouping subfolder, each ported test file's word split, the files nanoql replaces or doesn't
  port with the reason, and the renames. For Boost ports it holds the pin, `boostPin` (`boost-1.90.0`), and the `boost`
  section, which gives each Boost directory a port names its folder under `src/NanoQL/Boost/`
  (`"boost/math/tools": {"folder": "Math/Tools"}`); the section stays empty until the first Boost port. It is the one
  source of those lists.
- **Test folders** (`tests/NanoQL.Tests/`), each its own namespace `NanoQL.Tests.<Folder>`:
  - `TestSuite/`: QuantLib's own test cases, one class per QuantLib test file
    (`test-suite/daycounters.cpp` → `DayCountersTests.cs`). Methods keep the case name, capitalised
    (`testConsistency` → `TestConsistency`). Only this folder's files carry Upstream lines.
  - `Vectors/`: one test class per vector file; `Vectors/Cxx/` for the C++ harness's.
  - `Harness/`: the vector loader's and the tolerance's own tests.
  - `Divergences/`: each documented divergence from QuantLib.
  - `Conventions/`: nanoql's own types and rules.
  - `NoOracle/`: members the C++ harness can't drive, ported with nanoql's own tests (§9.4); none today.
- **The `Oracle` trait:** `[Trait("Oracle", "QuantLib")]` sits on the class, on every test class in `TestSuite/` and
  `Vectors/`, and on nothing else. A ported case asserts only QuantLib's expectations; where nanoql diverges, the
  assertion moves to `Divergences/`. A test checks the placement.
- **Line endings:** `*.json` and `docs/porting-map.md` keep LF on every checkout (`.gitattributes`), and generators
  write LF, so byte comparisons hold on a fresh clone.
- **Python:** the generators and tools run in a virtual environment at `.venv`, installed from the two hash-pinned
  files, with every input hash-checked:
  ```
  python -m venv .venv
  .venv\Scripts\python -m pip install --require-hashes --only-binary :all: -r tools\vectors\build-requirements.txt
  .venv\Scripts\python -m pip install --require-hashes --no-build-isolation -r tools\vectors\requirements.txt
  ```
  `reuse` publishes no Windows wheel, so it builds from its source distribution against the hash-checked build
  backend that the first file installs. Every Python script runs with `python -I`.

## 6 Not ported

- deprecated APIs, and `ql/legacy`;
- QuantLib's plumbing that §2 replaces or removes (observers, handles, lazy objects, `Settings`, the index manager,
  the pricing-engine base classes, the `std::visit` helpers), and the replaced types, errors and null helpers;
- C++ platform headers and the include-only `all.hpp` files;
- the test suite's infrastructure, which xUnit and the vector harness replace, and its files with nothing to port,
  among them the tests of `Settings`, `Observable` and `LazyObject`;
- data tables, which are generated instead, with their notices;
- uBLAS-backed `Matrix` operations other than the LU, until a port needs them;
- what the default QuantLib build leaves out.

`ql/experimental` is ported, marked `[Experimental]` (§4). [porting-map.md](porting-map.md) gives every QuantLib
file's status, with the reason for each file that isn't ported.

## 7 Validation

### 7.1 The oracle

Every port is checked against QuantLib itself:

1. **QuantLib's test suite:** the cases for each ported class, from `test-suite/<name>.cpp` at `v1.44`, ported with
   QuantLib's expected values and tolerances.
2. **Vectors** generated by QuantLib-Python 1.44 on Windows x64 (§7.2).
   - **Python-side names:** a generator calls a Python-side function (`MakeSchedule`, `FixedRateLeg`) only after
     reading its source and recording, in the generator, the C++ class it forwards to. Where the Python side adds
     logic of its own, the generator calls the wrapped class directly.
   - **Build flags:** a port of a feature that a build flag controls checks the wheel's setting first (at-par Ibor
     coupons are on), and the settings are rechecked whenever the pin moves.
3. **A C++ harness** built from the `v1.44` sources, for every public type that no wheel class and no ported QuantLib
   test can run, and for the members nothing else checks (§9.4). Its vectors use the same format (§7.2).
   - **Build:** `tools/cxx/build.py` sets it up outside the repository, under one root. Boost 1.90.0 comes from its
     release zip, refused unless its SHA-256 is Boost's published one. QuantLib is a `git archive` copy of the pin,
     built out of source with QuantLib's own CMake project (Release, the unity build, the static runtime, every code
     option off except `QL_FASTER_LAZY_OBJECTS`, as in the wheel) and installed. The harness, `tools/cxx/`, is a CMake
     project that finds that QuantLib as a package (`find_package(QuantLib 1.44 EXACT CONFIG)`). The compiler is the
     wheel's, VS 2022's MSVC 14.44; the harness uses Windows SDK 10.0.26100.0 and VS 2022's CMake and Ninja.
     ```
     .venv\Scripts\python -I tools\cxx\build.py <root> --quantlib <QuantLib v1.44 worktree>
     ```
     Each step runs only when its output is missing and prints its wall-clock. QuantLib is rebuilt from empty folders
     whenever its configure command or the toolchain's versions change, and the harness builds incrementally on every
     run. `--clean` is the run for a pin move or a toolchain change, each a slot plan's decision.
   - **Protocol:** one executable, `cxx-harness`, reads JSON-RPC 2.0 requests from standard input, one per line, and
     answers each on one line. A method is named after the QuantLib function it calls, qualified as in C++, with an
     overload's parameter types where QuantLib needs them to choose (`Date::isLeap`,
     `InterestRate::compoundFactor(Date,Date)`); the harness's own methods start `harness.`. Arguments follow the
     vectors' encoding, and an object is a spec with the vectors' input names (a day counter
     `{"type": "Thirty360", "convention": 0}`); a member function takes its object's spec first. Numbers round-trip
     exactly, and NaN and ±∞ travel as the bare tokens `NaN`, `Infinity` and `-Infinity`.
   - **Exceptions:** an exception from QuantLib, the construction of QuantLib values from the arguments included, is
     classed as the wheel classes it. A `QuantLib::Error` (code 1) and any other exception (code 2) raise
     `RuntimeError` in the client, recorded as `{"throws": true}`; `std::out_of_range` (code 3) raises `IndexError`,
     which stops the generator, as in the wheel. A failure of the harness itself (a malformed request, an unknown
     method, an argument it can't read, a spec type or key it doesn't know) uses JSON-RPC's own codes and stops the
     generator, so it never passes for a QuantLib throw.
   - **Its guard:** the binary carries a digest of its own sources, `tools/cxx/CMakeLists.txt` and every file under
     `tools/cxx/src/`. The client refuses a harness whose digest isn't the working tree's, or that reports another
     QuantLib, Boost, option set or compiler.
   - **Its self-check:** twins of wheel vectors, harness vectors that reproduce a wheel generator's cases
     (`cxx/harness/encoding`, `cxx/interestrate`). `tools/vectors/agree.py` lists them and checks each against its
     wheel file (§8), and each has its own test, nanoql against the harness.
   - **Adding methods:** each QuantLib unit's methods live in `tools/cxx/src/<ql path>.cpp`. A method first checks
     everything the harness itself reads, and builds every QuantLib value inside the call, so that a constructor's
     refusal is QuantLib's. A port adds its methods there, its generators under `tools/vectors/gen/cxx/` and its test
     classes under `Vectors/Cxx/`, then reruns `build.py`.

### 7.2 Vectors

- **Generators:** `tools/vectors/gen/<ql path>.py`, one per QuantLib source pair, each writing
  `tests/vectors/<ql path>.json` with a fixed seed.
- **Environment:** Python 3.12 on Windows x64, with the venv of §5.
- **Committed,** so contributors without Python can run the tests. Size budget: at most 1 MiB per file and 25 MiB in
  total under `tests/vectors/`. Cases are sampled, or encoded compactly when a whole domain fits the budget (every
  weekday holiday of a calendar from 1901 to 2199).
- **When they change:** only when the pin or a generator changes. Regenerating reproduces the committed bytes. A
  change never re-stamps earlier vectors: a byte difference is a red to diagnose.
- **Every case sets its evaluation context:** the generator sets the evaluation date and every `Settings` flag
  explicitly and writes them into `inputs.settings`, and sets, and clears afterwards, any other QuantLib global its
  case reads, writing it there too. An unset evaluation date reads as today's date, which would make a vector depend
  on the day it was generated. A generator that changes a shared QuantLib object (a calendar's holidays) restores it.
- **Schema.** A plain number in `expected` has scale 1.0; an object carries its own scale (§7.3):

  ```json
  { "upstream": "ql/instruments/swap.hpp",
    "quantlib": "1.44", "generator": "tools/vectors/gen/instruments/swap.py", "seed": 20261015,
    "cases": [ { "name": "...",
                 "inputs": { "settings": { "evaluationDate": 46309, "includeReferenceDateEvents": false,
                                           "includeTodaysCashFlows": null, "enforcesTodaysHistoricFixings": false } },
                 "expected": { "NPV": { "value": 1234.5, "scale": 1000000.0 }, "npvDateDiscount": 0.987 } } ] }
  ```

- **Encoding:** dates as QuantLib serial numbers; enums as QuantLib's integers. NaN and ±Infinity are the strings
  `"NaN"`, `"Infinity"` and `"-Infinity"`, read with `AllowNamedFloatingPointLiterals`, since JSON has no literal for
  them. QuantLib's null sentinels (`Null<Real>`, `Null<Integer>`, `Date()`) are JSON `null`.
- **Outputs:** an output is one scalar in `expected`: an array counts its elements, and a `{value, scale}` object or a
  `{"throws": true}` counts once. Booleans, strings and integers compare exactly; dates compare as serials; other
  numbers compare under §7.3.
- **Failure cases:** where QuantLib throws, the output is `{"throws": true}`, and nanoql must throw
  `ArgumentException` or `NanoQLException`. Where a documented divergence makes nanoql return a value instead (a
  `Period` `==` on an undecidable pair returns `false`), the vector test maps that output to the documented value and
  names the divergence.
- **One test per vector file:** it compares every case and output, collects every mismatch, fails with the first 50
  and the total, and asserts that the outputs it compared equal the outputs the file holds, so no output is skipped.
- **Counting:** `python -I tools/vectors/count.py` prints each file's cases, outputs, overridden outputs and bytes.
- **Harness generators:** `tools/vectors/gen/cxx/<ql path>.py` calls the C++ harness (§7.1) through
  `tools/vectors/cxx.py` (`with cxx.connect() as H:`, then `H.call(<method>, <arguments>)` where a wheel generator
  calls `ql.…`) and uses `vectors.py` for the encoding and the document. It writes `tests/vectors/cxx/<ql path>.json`,
  read by a test class under `Vectors/Cxx/` named `<Name>CxxVectorTests`. The client finds the harness through the
  environment variable `NANOQL_HARNESS`, set to the path `build.py` prints;
  `.venv\Scripts\python -I tools\vectors\cxx.py --self-test` checks it: the exact round trip, the exception
  classes, the faults, and the refusal of a mismatched harness.
- **Reproducing a harness vector:** rerun its generator with a harness built from the committed sources, which the
  client checks through the digest, and compare the bytes, as for the wheel's. QuantLib and Boost are pinned, so
  QuantLib's from-scratch build isn't part of it; it reruns only at a pin move or a toolchain change.

### 7.3 Tolerance

- **The test:** each compared output passes when |a − b| ≤ 1e-10 × max(|a|, |b|, scale), with that output's own scale.
- **Scale:** set only on outputs that are amounts, each at its own natural size: the notional for an NPV or a leg
  NPV, and the notional × 1e-4 for a BPS. Rates, discount factors and year fractions keep scale 1.0.
- **Overrides:** an output may set `"tolerance"` only with a `"reason"` naming the measured cause, and the vector
  gate reports how many outputs are overridden.
- **Ported QuantLib tests** keep QuantLib's own tolerances.
- **Why not bit identity:** IEEE 754 recommends correctly rounded mathematical functions but doesn't require them,
  and .NET's `Math.Exp` calls the platform's C runtime. 1e-10 relative is about 10⁶ ulp of headroom.

## 8 Gates

Every commit that ports code passes these gates, each with its denominator.

| Gate | Command | Passes when |
|---|---|---|
| Build | `dotnet build NanoQL.slnx -c Release` | 0 errors, 0 warnings, all projects built. The AOT and trim analyzers run through `IsAotCompatible`; SA1402 and SA1649 run as errors. |
| Tests | `dotnet test --solution NanoQL.slnx -c Release` | passed = total, failed 0, skipped 0. |
| Vectors | part of the tests, counted per vector file by `tools/vectors/count.py`; then `.venv\Scripts\python -I tools/vectors/agree.py` | every compared output within tolerance, counting outputs, not cases; with the number of overridden outputs. Every twin on `agree.py`'s list agrees with its wheel file: the same case names and inputs, every output within tolerance, and kinds and scales exactly; it prints the outputs agreeing M/M and how many are bit-identical. |
| Licensing | `.venv\Scripts\reuse lint`, then `.venv\Scripts\python -I tools/check-upstream.py <QuantLib v1.44 worktree> --boost <Boost 1.90.0 root>`, and the same with `--self-test` | (1) `reuse lint` reports the project compliant. (2) Every `.cs` file under `src/NanoQL/` and `tests/NanoQL.Tests/TestSuite/` carries an Upstream line whose paths exist at the pin, in the folder and namespace the name table gives, or is listed in `tools/no-upstream.txt`; so is every other `.cs`, `.py`, `.cpp` and `.hpp` file; every test file's namespace names its folder; and every `// Replaced:` line holds (§1). (3) Every `Copyright (C)` line of the named upstream files appears verbatim, read as §1 says; every third-party block `tools/third-party-notices.txt` lists for them appears whole, every line in order, in the file's header and in `THIRD_PARTY_NOTICES.md`, read from the row's source, with its licence in the SPDX expression; a Boost port's header holds as §1 says. (4) The files with a `// Partial:` line are exactly those `porting-plan.md` lists. (5) Regenerating `porting-map.md` reproduces the committed file; every file the name table marks replaced or not ported exists at the pin; and no Upstream line names a not-ported file. The self-test perturbs a copy of the tree once per check, with a synthetic Boost port among them and synthetic ports carrying third-party blocks (a starred notice and a replacement character among them), whose expected lines are written out rather than read through the checker, and passes only if each perturbation is caught. **The Boost root** is Boost 1.90.0's release tree: `tools/cxx/build.py`'s `boost_1_90_0` (§7.1), or `boost_1_90_0.zip` from `https://archives.boost.io/release/1.90.0/source/`, checked against its SHA-256, `bdc79f179d1a4a60c10fe764172946d0eeafad65e576a8703c4d89d49949973c`, and extracted with Python's `zipfile` (Windows' `tar` skips one file). `--boost` is required for `--self-test` and whenever an Upstream line names a Boost path. `reuse lint` also lints the ignored build output under a folder none of whose files is in git's index yet, a false red: in a change that adds such a folder, run it on a tree without build output (`git clean -fdX -- <new folders>`) or after `git add -N <new folders>`. |
| Reproducibility | `.venv\Scripts\python -I tools/vectors/reproduce.py [<generators>]`, a folder named by its path ending in `/` (`cxx/`); for harness generators, after `tools/cxx/build.py`, with `NANOQL_HARNESS` set and `tools/vectors/cxx.py --self-test` passing | rerunning each generator the commit adds or changes, or all of them when the shared helpers or the requirements change, writes bytes identical to the committed files, compared outside git. Harness generators rerun when the commit adds or changes them; all of them when the harness's source, its client, the shared helpers or the requirements change; and one also when a wheel generator it imports changes. |
| Coverage | `dotnet test --solution NanoQL.slnx -c Release --filter-trait "Oracle=QuantLib" --coverlet --coverlet-output-format cobertura --results-directory TestResults\G7` and the whole suite's run, `dotnet test --solution NanoQL.slnx -c Release --coverlet --coverlet-output-format cobertura --results-directory TestResults\G7all`, each from the repository root into an emptied folder; then `.venv\Scripts\python -I tools/check-coverage.py TestResults\G7 --all TestResults\G7all` | Every public type the commit ports that has executable lines has covered lines from the tests marked `Oracle=QuantLib` (§9.5). Types the report doesn't list (enums, interfaces, delegates, abstract types with no bodies, types holding only fields or constants) are listed apart by name and count, and so are types whose only code is equality nanoql adds. The line coverage of the commit's ported files is reported, with no threshold. It also prints, without failing, the commit's public members that no test of any kind reaches (§9.5); a missing or second whole-suite report is a red. |

## 9 Checking every member

### 9.1 Which tests count

Only tests that check against QuantLib carry `[Trait("Oracle", "QuantLib")]`: the ported QuantLib tests and the vector
tests. Tests of nanoql's own rules (`Period` equality, the refusal of `default(Date)`) don't.

### 9.2 Ported test cases

A ported case keeps QuantLib's expected values and tolerances. A test that reads the evaluation date builds
`new EvaluationContext { EvaluationDate = new Date(16, Month.September, 2015) }`, the date QuantLib's test runner sets
globally; a test that changes `Settings` mid-case builds one context per setting. A case that reads
`Date::todaysDate()` reads `Date.TodaysDate()`, as QuantLib's does.

### 9.3 Divergences

Each documented divergence from QuantLib has a test in `Divergences/` that asserts nanoql's behaviour and names the
QuantLib behaviour it replaces.

### 9.4 Members without an oracle

A member with no oracle in its port (not exposed by the wheel, not asserted by a ported QuantLib test, and not reached
through a checked caller):
- **waits** for the later port that will check it (a QuantLib test case it ports, or a wheel binding), named on its
  file's `// Partial:` line;
- or, if nothing else ever will, **is vectored through the C++ harness** (§7.1) in its own port: a harness generator
  writes QuantLib's own outputs, every input the port's reasoning names among them, and a test under `Vectors/Cxx/`
  checks nanoql against them. Where a vector records a throw, which accepts either exception type, a test in
  `Conventions/` asserts the type nanoql throws;
- `NoOracle/` keeps only a member the harness can't drive. Its file's `// Own-tested:` line names the member and the
  reason, each test cites the QuantLib lines its expected values come from and carries no `Oracle` trait, the code
  review rechecks the derivation, and the porting map lists the member. None is known.

### 9.5 The coverage gate's type check

`tools/check-coverage.py` reads the one Cobertura report in the results folder.
- **The types:** the top-level public types of the `.cs` files under `src/NanoQL/` that the commit adds or changes
  (the working tree against `HEAD`, untracked files included) and that carry an Upstream line. Files that declare no
  type are skipped. Report entries are matched to those files by path, so nested, `file` and compiler-generated
  classes count toward their file's type.
- **Red when:** the folder doesn't hold exactly one report; the commit ports a public type and the report doesn't
  list the `NanoQL` package; a listed type has no covered line; a ported file carries `ExcludeFromCodeCoverage`; or a
  type whose only executable lines are its equality members lacks an `// Equality-tested:` line naming an existing
  test.
- **Listed apart:** the types the report doesn't list, with their kinds; and the types whose only executable lines are
  equality members nanoql adds where QuantLib has no `operator==`, each with its `// Equality-tested:` line. A type
  that ports a QuantLib `operator==` needs an `Oracle=QuantLib` test like any other.
- **It prints** types T/T and lines L/M for the commit and over every ported file, and the commit's own-tested
  members.
- **The member line** (`--all`, from the whole suite's report): "members no test reaches: N" and the list, the public
  members of the same files whose report lines have no hit from any test, tagged or not. It never fails the gate; the
  review reads it. A member counts as public when its declaration and every type around it are declared `public`, so
  a member of a protected nested type or of a `file` type doesn't count. A member is a report method of a class the
  compiler didn't generate, named as the report names it (`Period.get_Length()`, `TimeUnitExtensions.op_Multiply(…)`,
  `Period..ctor(…)`); the code of a lambda or local function counts toward the member whose source holds it, and an
  iterator or async method, whose code is all in its generated class, is named by that class. Red only when the whole
  suite's folder doesn't hold exactly one report, or a `NanoQL` class of it resolves to no file. What it can't see: a
  member with no declaration of its own (an implicit or primary constructor, a positional record's members);
  auto-property accessors, which the report leaves out; and an explicit interface implementation, reached through
  its interface but not declared `public`.

<!-- REUSE-IgnoreEnd -->
