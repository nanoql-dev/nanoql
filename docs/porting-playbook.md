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
  - `// Not ported: <case> (<reason>)[; <case> (<reason>)]` on a test class: QuantLib test cases that can never port,
    because they exercise only code nanoql doesn't port (§6), each with the reason (`OperatorsTests.cs`'
    `testConsistency`, which builds only the deprecated `DZero` and `DPlusDMinus`). The porting map lists them as not
    ported and counts the test file ported once its other cases are. The licensing gate checks that each is a case of
    the class's QuantLib test file, neither ported nor on its `// Partial:` or `// Replaced:` line.
  - `// Moved: <case> line <n> (Divergences/<File>.cs)[; <case> line <n> (Divergences/<File>.cs)]` on a test class,
    with its `// Partial:` and `// Replaced:` lines: checks of the QuantLib cases it ports that assert a value where
    nanoql diverges, each moved to the `Divergences/` test that asserts nanoql's value in its place, with the check's
    line in QuantLib's test file at the pin (`// Moved: testExCouponAccruedAmountAroundAccrualEndDate line 1936
    (Divergences/TelescopicValueDatesTests.cs)`). The porting map notes each move on the file's row, whatever its
    status. The licensing gate checks that each is a case of the class's QuantLib test file, that the line holds code
    of the case's body, and that the `Divergences/` file exists.
- **Generated tables:** QuantLib's data tables (the primitive polynomials, Sobol's direction integers) aren't ported
  as code. A script of nanoql's own, `tools/tables/<QuantLib file>.py`, reads the QuantLib `.cpp` at the pin and
  writes each table to a binary file beside the type that reads it: little-endian 32-bit integers, laid out as
  QuantLib's arrays are (each dimension's coefficients ended by 0, each degree's polynomials by -1). The project
  embeds each file as a manifest resource with a fixed logical name. The loader, an internal type written by hand,
  carries the Upstream line, the copyright lines and the notice blocks of the QuantLib file, and reads its tables
  once, at first use, through `Assembly.GetManifestResourceStream`. A table holds only QuantLib's and third parties'
  data, so `REUSE.toml` annotates it with the upstream copyright lines and the data's licences, without Apache-2.0,
  and `THIRD_PARTY_NOTICES.md` carries their notices. The licensing gate regenerates the tables and compares the
  bytes (§8).
- **Files that replace a QuantLib file** may name it in their Upstream line, with its copyright lines, so the map
  shows which nanoql file takes its role: `EvaluationContext.cs` names `ql/settings.hpp` and `ql/settings.cpp`. A
  file may not name a file the name table marks as not ported.
- **Files with no upstream** (nanoql's own types, test helpers, tools) carry only the nanoql copyright line and the
  Apache-2.0 licence identifier, and are listed in `tools/no-upstream.txt`.

## 2 Translation rules

| QuantLib | nanoql |
|---|---|
| `Observable`, `Observer`, `registerWith`, `update`, `notifyObservers` | Removed. |
| `Handle<T>`, `RelinkableHandle<T>` | The object itself (`T`). Relinking means building a new dependent object. The one exception is a cycle of curves (`MultiCurve`): its internal handle is `MultiCurveMember`, a curve the cycle's indexes and helpers are built on, which `Build` links once to its member after checking their reference dates and day counters; every copy of a handle shares the link. Before `Build` a handle answers its reference date and day counter, both given when it is created, and the times they measure, and refuses every query that needs its curve. |
| `Quote`, `SimpleQuote`, `Handle<Quote>` | `double`. Quotes are input values. Overloads that become identical once a quote is a `double` collapse into one. **QuantLib's derived quotes** are sealed, immutable classes, each computing its `Value` once at construction from its inputs and the context, with QuantLib's inspectors: `DerivedQuote`, `CompositeQuote` and `MultiCompositeQuote` over doubles and delegates; `ForwardValueQuote`, `LastFixingQuote`, `FuturesConvAdjustmentQuote` and `ForwardSwapQuote` at the context's evaluation date, a new date a new quote; `ImpliedStdDevQuote` and `EurodollarFuturesImpliedStdDevQuote` solving once from the caller's guess, where QuantLib's start each later solve from the last one's result (`Divergences/ImpliedStdDevQuoteTests.cs`). `isValid` and the observers aren't ported: a quote that would be invalid refuses at construction, with QuantLib's text. |
| `LazyObject` (`performCalculations`, `calculated_`, `freeze`) | Computed in the constructor or a static factory and stored in readonly fields. **A cache only where it can't change an answer:** a computed value is kept for reuse only where it is keyed by everything the answer reads, computed from immutable state, safe across threads and measured to pay, and the class documents it, so every answer is what a fresh object returns, whatever was asked before and on whichever thread. A cache that can return a stale value is not ported (`Business252`'s, keyed by a calendar's name), and a memo that lives for one call belongs to a working type. `AndreasenHugeVolatilityInterpl` keeps QuantLib's local-volatility and price slices by time in `ConcurrentDictionary`s inside the immutable interpolation (the S-van plan's VR2): each slice is computed from the calibration alone, and the finite-difference and Monte Carlo engines that read a slice at every step keep QuantLib's cost (`Conventions/AndreasenHugeMemoTests.cs`). A bootstrapped curve (`PiecewiseYieldCurve`) runs its bootstrap in its constructor over a working state, `BootstrapState<TTermStructure>`, that no caller sees, with its helpers bound by copy to the curve itself and re-dated to its context, then freezes its nodes; the members its bootstrap calls on the curve under construction (`DiscountImpl`, `MaxDate`, `MaxTime`, `DayCounter`) are sealed. The bootstraps (`IterativeBootstrap`) are configuration structs (§2.6) implementing `IBootstrap`, which reproduce QuantLib's sequence, the node keeping the last value the solver evaluated. A curve is always built afresh, so QuantLib's warm restart after a quote change, and a fixed-reference curve's first evaluation date's pillars, are documented divergences (`Divergences/CurveRecalculationTests.cs`). A calibration inside an interpolation runs the same way: `SABRInterpolation` fits in its constructor over a working state no caller sees (XABR's internal `XabrFit<TSpecs, TInstance>`, whose cost function writes the fit's parameters and rebuilds its model instance at every evaluation, as QuantLib's writes its coefficient holder), then freezes; QuantLib's `update()` isn't ported, a new fit being a new interpolation. The restarts' Halton generator is built inside the fit with QuantLib's constant seed, 42, and no seed parameter is added. An optionlet stripper (`OptionletStripper1`, `OptionletStripper2`) strips in its constructor what QuantLib's first calculation strips, under the context it is built with, each shifted lognormal optionlet solve starting from QuantLib's first guess, 0.14; QuantLib's `OptionletStripper1` starts a recalculation from its last standard deviations, which `OptionletStripper2` reads, so its warm start is a documented divergence (`Divergences/OptionletStripperWarmStartTests.cs`). **A setter on a built curve:** QuantLib's `InflationTermStructure::setSeasonality` changes a built curve; here `SetSeasonality(seasonality, context)` returns a copy of the receiver's type through the protected internal virtual `WithSeasonality`, which a bootstrapped curve overrides to bootstrap its copy again from a cold start under the context given, the context a parameter because an override reads it. **The CMS market and its calibration:** `CmsMarket` computes its spreads, prices and errors in its constructor under the context it takes; QuantLib's `reprice` sets each pricer's volatility and reversion in place, and here `Reprice(volatility, meanReversion, context)` returns a new market on copies of the pricers, the receiver standing. The SABR cube's `Recalibration` overloads return a new cube with QuantLib's `updateAfterRecalibration` applied. `CmsMarketCalibration.Compute` returns a `CmsMarketCalibrationResults` record (the result, the recalibrated cube, the market repriced on it, the cube's parameters, the error and the end criteria); each cost evaluation builds its cube and market from the last evaluation's, as QuantLib's in-place changes carry from one evaluation to the next, and the calibration's own cube and market stand. **The stochastic-local volatility models:** `HestonSLVFDMModel` and `HestonSLVMCModel` calibrate in their constructors, immutable, their `LeverageFunction` the calibrated `FixedLocalVolSurface` and the finite-difference model's `LogEntries` an immutable list filled while it calibrates with logging on, where QuantLib's models calibrate at their first read and its `logEntries()` recalibrates on each read. **A global bootstrap's additional variables:** `AdditionalBootstrapVariables` is abstract and immutable, its `Initialize` QuantLib's `initialize` and its `Values` QuantLib's `update`, returning the values where QuantLib writes them into quotes; `GlobalBootstrap.VariableHelpers` builds the helpers that read them, at each evaluation of the cost function, after the curve's helpers and weighted with them, a later build that moves a pillar refused; the curve keeps the optimum's values (`AdditionalVariables`), where QuantLib leaves them in its quotes (`Divergences/GlobalBootstrapVariablesTests.cs`). **The local bootstrap:** `LocalBootstrap` is a readonly-struct bootstrap, a cold start as QuantLib's every calculation is, over an `ILocalInterpolator` (`ConvexMonotone`), another interpolator refused at run time where QuantLib's template fails to compile; `ConvexMonotone` is a D12 struct, its zero value QuantLib's default construction. **The credit basket and its loss model (the S-xcr plan's XR1):** `Basket` is immutable, of its terms and an evaluation context, computing QuantLib's `computeBasket()` at the context's date in its constructor, after its tranche's amounts; `SetLossModel(model)` returns a basket of the same terms whose `LossModel` is `model.SetBasket(basket)`, a copy of the model bound there, its `resetModel` precomputation run once, where QuantLib's basket and model link each other through a relinkable handle and reset through observers (`Divergences/LossModelBindingTests.cs`). A basket with no model refuses its statistics with QuantLib's text, and an engine refuses a basket of another evaluation date. QuantLib's flows that change a quote or set one model on several baskets become a model built and bound per basket. **Realized defaults (XR8 (ii), ORE's form):** the names defaulted by the evaluation date leave the pricing basket (`Basket.PricingBasket`): the lost notional writes the tranche down from its attachment, the recovered notional amortizes it from its detachment's side, the model is bound to the live names over the restated tranche, a latent model restricted to their rows, and the statistics add the realized tranche loss or take the order less the realized defaults; a default not settled for its key's seniority is refused, and a default after the evaluation date stays out of the restatement, the reads with a date argument counting it (`Divergences/BasketRealizedDefaultsTests.cs`). |
| A `mutable` cache outside `LazyObject` (`Coupon::accrualPeriod_`) | Recomputed on each call, unless it meets the `LazyObject` row's rule for a cache. |
| A virtual member a constructor reads (a base constructor that computes from members its derived classes override: `SwaptionVolatilityDiscrete`'s grid from the calendar, convention and day counter) | Sealed in the class whose constructor reads it: C# runs a derived override from a base constructor before the derived constructor has run, where C++ runs the base's own. A derived class that answers another object's values (QuantLib's swaption cube, its ATM structure's conventions) passes them to the constructor instead. `PiecewiseYieldCurve` seals the members its bootstrap calls on the same ground (the `LazyObject` row). |
| `Instrument` + `PricingEngine` | The instrument is an immutable object holding its terms. The engine is a static class whose `Calculate` runs QuantLib's four steps (§2.1). `swap.setPricingEngine(e); swap.NPV()` becomes `DiscountingSwapEngine.Calculate(swap, context, discountCurve).NPV`. A member that reads the attached engine's results takes those results and the context, and computes where QuantLib computes, on demand and outside the results' derivation: `Bond.CleanPrice(BondResults results, EvaluationContext context)`, `DirtyPrice(results, context)` and the theoretical `Yield(results, …, context, …)`, from the engine's settlement value and the settlement date and accrued amount the context gives. The results and the context must be one call's, which the members' documentation says: results read under another context mix two settlement dates, which nanoql can't detect. An engine that runs another (QuantLib's `QuantoEngine<Instr, Engine>`, `ForwardVanillaEngine<Engine>`) takes it as a delegate of D14's signature, run on the process it builds: `QuantoEngine.Calculate(option, context, process, foreignRiskFreeRate, fxVolatility, correlation, AnalyticEuropeanEngine.Calculate)`, the forward one `(o, c, p) => ForwardVanillaEngine.Calculate(o, c, p, AnalyticEuropeanEngine.Calculate)`; a quanto instrument keeps QuantLib's hierarchy, so it refuses results without the quanto sensitivities, and a user's own instrument reaches the quanto engine through its generic core. **An aggregate of instruments:** `CompositeInstrument` holds (instrument, engine, multiplier) components, each engine a `Func<EvaluationContext, InstrumentResults>` since nanoql's instruments carry none; `Add` and `Subtract` return new composites, and `Calculate(context)` sums the components' values. |
| `Settings::instance()` | `EvaluationContext`, passed to every function that reads it (§2.2). `Settings::instance().evaluationDate()` becomes `context.EvaluationDate`. |
| `IndexManager` fixing histories | `context.Fixings`, an immutable `IndexFixings` with value equality (§2.2). Its writers take the index and return a new store (`AddFixing`, `AddFixings`, `ClearFixings`), since QuantLib's checks read the index's name, `IsValidFixingDate` and `AllowsNativeFixings`; an index's `HasHistoricalFixing`, `PastFixing` (`double?`, null where QuantLib returns `Null<Real>()`) and `TimeSeries` take the context. Names compare as QuantLib's do, ASCII letters upper-cased and every other character as it is, the first spelling kept. A refused batch changes nothing, reads never write, and a fixing equal to QuantLib's null sentinel `Null<Real>()` is a value where QuantLib reads it as no fixing: three documented divergences. Index objects hold their conventions and, for an Ibor or overnight index, its forecasting curve, never fixings: the curve is an optional constructor argument, `null` being QuantLib's empty handle, and `Clone(forwarding)` is QuantLib's `clone(h)`, returning a plain `IborIndex` (an `OvernightIndex` for an overnight index) as QuantLib's does. QuantLib's `Index` is `FinancialIndex` (§3). **A fixing that covers a period:** `FinancialIndex`'s `protected internal virtual FixingDates(fixingDate)`, the date alone, is QuantLib's virtual `Index::addFixing`: `IndexFixings.AddFixing` writes the fixing on every date it returns, through the store's one write, with its checks and texts. An inflation index returns every day of the date's inflation period, and its reads key on the period's first day; `AddFixings` and `ClearFixings` store and clear the dates they're given, as QuantLib's non-virtual `addFixings` does. An index of one's own whose fixing covers a period overrides the hook. |
| `IborCoupon::Settings`, `ExchangeRateManager`, `Money::Settings`, the commodity settings | Fields of `EvaluationContext`: every QuantLib global that pricing reads becomes one field, added by the port that first needs it. `IborCoupon::Settings`' at-par switch is `context.UseAtParCoupons` (default `true`, QuantLib-Python's), read where QuantLib reads it: when an Ibor coupon pricer is built without an explicit choice. So a constructor that builds such a pricer (an Ibor leg's conversion, `VanillaSwap`, a swaption cube's, whose swap indexes' fixings build swaps, and `SwaptionVolatilityCube.AtmStrike`) takes the context, a pricer resolves the switch once, and a leg keeps the choice it was built with. `ExchangeRateManager` is `context.ExchangeRates`, an immutable `ExchangeRates` registry (default `ExchangeRates.Default`, QuantLib's known rates) whose `Add` returns a new registry and whose `Lookup(source, target, context, date, type)` reads the context's evaluation date when no date is given; it keys a pair by its two currencies, in the order of QuantLib's numeric key, where QuantLib's numeric key loses currencies coded 1,000 or more and confuses currencies sharing a code. `Money::Settings` is `context.MoneySettings`, a record (default no conversion and no base currency). `Money` is a value whose operators keep QuantLib's `NoConversion`, `==` and `!=` comparing a currency mismatch as unequal where QuantLib's throw; the conversion modes are methods that take the context (`Add`, `Subtract`, `Divide`, `Equals`, `LessThan`, `LessThanOrEqual`, `GreaterThan`, `GreaterThanOrEqual`, and `MoneyFunctions.Close` and `CloseEnough`), each converting the operand QuantLib's operator converts. |
| ECB's known dates (`ECB::knownDates`, `addDate`, `removeDate`: a static, mutable set no pricing reads) | `ECB`, a sealed immutable value equal by its dates: `ECB.Default` holds QuantLib's dates, `AddDate` and `RemoveDate` return copies, QuantLib's functions are its members (the two that read no dates static), and those that read the evaluation date take the context. Not a context field, since nothing prices from it. |
| `CashFlow::amount()`, `Coupon::rate()`, `nominal()` and `accruedAmount(d)`, and every member that reaches a fixing or the evaluation date | Take the context: `Amount(EvaluationContext context)`, `Rate(context)`, `Nominal(context)`, `AccruedAmount(Date d, context)`; a floating coupon's fixing members (`IndexFixing`, `AdjustedFixing`, `ConvexityAdjustment`, `Price`); an index's `Fixing`, `PastFixing`, `HasHistoricalFixing`, `TimeSeries` and `ForecastFixing`. A flow that knows its amount checks the context for null and doesn't read it otherwise. A member that no QuantLib override, in any port, reads it from takes none: `AccrualPeriod`, `AccrualDays`, `AccruedPeriod`, `AccruedDays`, `FixingDate`, and an index's `FixingDate`, `ValueDate`, `MaturityDate` and `IsValidFixingDate`. |
| The nominal a constructor copies from another coupon (a capped coupon's underlying, a `Bond`'s notional schedule) | `Coupon.ContractNominal`, public and non-virtual: the nominal the coupon was built with. Pricing calls `Nominal(context)`, which equals it for every coupon whose nominal doesn't depend on market data (QuantLib's FX-reset coupon is the exception); a constructor, which has no context, reads `ContractNominal`. |
| `FloatingRateCouponPricer` (`initialize(coupon)`, then its queries), `setPricer`, `setCouponPricer` | The coupon holds its pricer, given at construction or by `SetPricer`, a generic extension method that returns a copy of the receiver's static type through a `protected internal virtual WithPricer`; a capped or floored coupon's copy carries the pricer into a copy of its underlying. Pricers are stateless functions of the coupon and the context: `SwapletRate(coupon, context)`, `CapletRate(coupon, effectiveCap, context)` and the rest compute QuantLib's per-coupon state inside each call, and a by-product QuantLib keeps after a query is a member computed from the same arguments. `setCouponPricer(leg, pricer)` becomes `CouponPricerFunctions.SetCouponPricer(leg, pricer)`, which returns a new leg, all or nothing; its visitor is a `switch` that each later coupon type extends in its own port. On a built swap, `SetCouponPricer(swap, int leg, pricer)` returns a copy of the swap's own type through `Swap`'s `protected internal virtual WithLeg`. On a bond, `SetCouponPricer<T>(T bond, pricer) where T : Bond` returns a copy of the bond's own type with its floating coupons re-priced, through `Bond`'s `protected internal virtual WithCashflows`, a `MemberwiseClone` with the new leg, so its notional schedule and redemptions stand. The builders attach QuantLib's default pricers. The CMS pricers (the Hagan family, the linear TSR pricer) are immutable: QuantLib's `initialize` state is an internal working type each public member builds from the coupon and the context, the g-function with shifts and its Newton memo inside it. An internal virtual on `FloatingRateCouponPricer` returns a per-call view of a coupon's rates, by default the public members, so every other pricer and a user's subclass keep their values; the CMS pricers override it, and `CappedFlooredCoupon.Rate` reads it, so a capped or collared coupon builds that state once, as QuantLib initializes once. QuantLib's `MeanRevertingPricer` mixin is `IMeanRevertingPricer` (`MeanReversion`, `WithMeanReversion(double)`); `WithMeanReversion` and `WithSwaptionVolatility` return copies of the pricer's own type; the numeric Hagan pricer's integration limits are a member computed from an optionlet's arguments; the TSR pricer's settings are a sealed record. A digital coupon's call and put option rates replicate through capped and floored coupons on its underlying, which holds the pricer. The CMS spread pricers' base, `CmsSpreadCouponPricer`, holds QuantLib's correlation, and `WithCorrelation` returns a copy of the pricer's own type. |
| `BootstrapHelper::setTermStructure`, `RelativeDateBootstrapHelper` (the rate helpers) | A helper is immutable. `setTermStructure` becomes `SetTermStructure`, an extension method returning a copy of the helper's own static type through a `protected internal virtual WithTermStructure`, which a helper that prices against the curve overrides to rebuild on the copy what does (an index cloned on the curve, a swap's floating coupons copied with that index, each keeping its pricer); the helper it is called on stays unbound, and an unbound helper's `ImpliedQuote` throws QuantLib's "term structure not set". A helper whose dates follow the evaluation date takes the context, computes its dates in its constructor and records the evaluation date; a curve built under another evaluation date binds a copy re-dated through the helper's `InitializeDates(EvaluationContext)`, as QuantLib's helper re-runs `initializeDates()` when the global date moves. QuantLib's `RateHelper` and `RelativeDateRateHelper` typedefs are abstract classes, the relative-date mechanism living in `BootstrapHelper<TTermStructure>`. A quote, spread or convexity adjustment is a `double`. Each family of term structures adds its own `SetTermStructure` overload, since C# can't infer the family from a derived curve. |
| `telescopicValueDates` (the overnight coupon, `OvernightLeg`, the OIS constructors, `MakeOIS`, the OIS rate helper) | Kept wherever QuantLib takes it, with QuantLib's check (no lookback without the observation shift) and its one deliberate effect on results: on an averaged coupon it selects the averaging pricer's approximation. Its date optimisation isn't ported: every daily value date is built, so a telescopic coupon gives QuantLib's full-schedule result on any date, which is QuantLib's own result wherever its shortened dates are right, and the coupon's constructors read no evaluation date. Where its shortened dates are wrong, nanoql's results differ from QuantLib's telescopic coupon's: a coupon priced more than 7 business days after the later of its start and the evaluation date QuantLib built it with, a lockout (QuantLib freezes it at the front stub's last fixing date), an accrued amount inside the skipped dates, the exact averaging pricer, and the Black averaging pricer's caps and floors on the period rate. Two QuantLib tests assert such a value: an ex-coupon accrued amount on 27 March 2027, inside the skipped dates (`test-suite/overnightindexedcoupon.cpp:1936`), and a lockout leg's NPV in `testOvernightLegNPV` (`:1033`). Each port asserts QuantLib's full-schedule value in the divergence's tests instead, the case naming its moved check on a `// Moved:` line (§1). The date lists and these five results are documented divergences (`Divergences/TelescopicValueDatesTests.cs`). |
| `OvernightIndex`'s fixing over a span (the overnight coupon pricers' `1 + fixing * span` and `fixing * span`) | Two public virtuals on `OvernightIndex`: `GrowthFactor(fixing, span)`, `1 + fixing × span` by default, which the compounding pricers multiply, and `SimpleAccrual(fixing, span)`, `fixing × span` by default, which the averaging pricers sum: QuantLib's expressions, so an index keeps QuantLib's bits. An index whose fixings are quoted on another convention overrides both, its accrual its growth factor less one, as `Cdi`'s annually compounded rate does (`(1 + r)^τ`), where QuantLib grows every fixing as a simple rate; the Black pricers' forward parts compare a daily cap's or floor's strike, through the accrual, as the simple rate they estimate from the curve, reading the volatility at the strike as quoted, an index accruing simply keeping its strike as quoted: documented divergences (`Divergences/CdiCompoundingTests.cs`). |
| Term structures whose reference date moves with the evaluation date | `Create(..., EvaluationContext context)` factories. Every built term structure has a fixed reference date. A structure whose queries read the context, where QuantLib's query reads the evaluation date, the fixings or the at-par switch and the query API takes no context (the interpolated swaption cube's at-the-money forward at each query, through the swaption volatility API), keeps the context it was built with, an immutable record, and its queries use it: a query under another context still reads the build context, which the structure's remarks say. The members a port defines that read the context take it (`SwaptionVolatilityCube.AtmStrike`). |
| `Calendar`, `DayCounter` (Pimpl) | An abstract class with sealed implementations (§2.4). `addHoliday` and `removeHoliday` return a new calendar, as `ImmutableList<T>.Add` does, and so does a bespoke calendar's `addWeekend`. In QuantLib the change applies process-wide, because all instances of a calendar share one implementation; in nanoql it is part of the calendar's value. A day counter compares the data its counts read (§4). `Business252` ports no cache: QuantLib's two file-level caches are keyed by the calendar's name and never invalidated; the constructor counts each month of 1901 to 2199 once into a readonly table (the `LazyObject` row's form), and a month its calendar refuses is left to the count, where QuantLib's walk raises the calendar's refusal. |
| An empty Pimpl value (`Calendar()`, `DayCounter()`, `empty()`) | `null` (`Calendar?`, `DayCounter?`). A member that QuantLib lets take an empty value it can complete with accepts `null` and behaves as QuantLib does: it builds where QuantLib builds and fails where QuantLib fails, with QuantLib's text. An internal empty calendar stands for QuantLib's: it adjusts by `Unadjusted` and moves by weeks, months and years, and any other use throws `NanoQLException`, "no calendar implementation provided". A member whose every use of the value fails may refuse `null` on entry, naming its own parameter. Where QuantLib returns the empty value, nanoql returns `null` (`Schedule.Calendar`), and two empty calendars compare equal. Where a QuantLib object default and its empty value collide (`Schedule`'s dates constructor, whose default is `NullCalendar()`), the default keeps `null`: a documented divergence. |
| Visitor (`accept(AcyclicVisitor&)`) | A `switch` pattern match at the call site. |
| Templates and traits | Static abstract interface members for stateless traits. QuantLib's optional members, which its templates detect, are static virtual members whose defaults are QuantLib's behaviour without them (`IBootstrapTraits<TTermStructure>`'s `HasGlobalGuess` and `HasTransform`). Traits that name a curve type QuantLib's class derives from (`Discount::curve<I>::type`) carry that curve kind's formula and maximum-date rule as static members (`IYieldTraits.DiscountImpl` and `MaxDate`), which the interpolated curve of the kind and the piecewise curve on the traits both call; QuantLib's is-a relation is an interface, `IInterpolatedCurve<TTraits, TInterpolator>`, so a piecewise curve on `Discount` and `LogLinear` is an `IInterpolatedCurve<Discount, LogLinear>` as an `InterpolatedDiscountCurve<LogLinear>` is. A yield curve's own methods `Discount` and `ForwardRate` hide the traits of those names inside its class (CS0119), where the curve kind's formula is called directly. A parameter that is configuration is a generic struct parameter, and a generator is a class parameter (§2.6). A template over plain values becomes an ordinary generic method. A template member that needs more of its parameter than its class does (C++ instantiates only the members a caller uses) becomes a generic method with that constraint on the class's companion static class: a constructor becomes a factory (`RandomSequenceGenerator.Create<TRng>(dimensionality, seed)`), an instance member an extension method (`NextInt32Sequence<TRng>()`). A call that breaks the constraint fails to compile, as the C++ instantiation does. XABR's model specification is internal: `IXabrSpecs<TInstance>`, with static abstract members for QuantLib's `Model` concept, each model's specification a `readonly struct`; the named interpolations (`SABRInterpolation`) are sealed and compose the fit, and `IXabrInterpolation` is the public view of a fit's results. **The latent model (the S-xcr plan's XR2):** QuantLib's stateful copula policies are `readonly struct`s implementing `ICopulaPolicy<TSelf, TInitTraits>` with a static abstract `Create(factorWeights, initTraits)`, their zero values refused; `LatentModel<TCopula, TInitTraits>` is generic over the policy and its parameters, its integration an `LMIntegration` chosen by `LatentModelIntegrationType`, and QuantLib's `FactorSampler` specializations by generator type are working types the caller chooses (`LatentModelFactorSampler`, `BoxMullerFactorSampler`, `PolarStudentTFactorSampler`). The loss models' contract is `IDefaultLossModel`, every statistic a member refusing with QuantLib's text by default, with `DefaultLossModel` the abstract class that writes the binding once; a model on another base (`ConstantLossModel`) implements the interface over it. QuantLib's typedefs of these templates (`GaussianDefProbLM`, `TConstantLossLM`, …) are sealed subclasses adding constructors only. |
| A class template whose parameter is its own base (`DerivedYieldTermStructure<Base>`, `RelativeDerivedYieldTermStructure<Base>`), which C# can't express | Not ported as a type: each derived curve (`ZeroSpreadedTermStructure`) implements its members. Its reference date and day counter are the original curve's, passed to its base constructor, since a term structure's reference date is fixed when it is built; its calendar, settlement days and maximum date are read from the original when asked, as overrides of `TermStructure`'s virtuals; its extrapolation flag is the original's when it is built, set through `Extrapolator`'s `private protected` initializer. The name table marks `derivedtermstructure` replaced, and the other spread and implied curves QuantLib derives from the two templates take the same rule. |
| `Array`, `Matrix`, `TridiagonalOperator` | Mutable working types (§2.5). `Array` is renamed `Vector` (§3). |
| `Array::operator+=` and the other compound assignments of `Array` and `Matrix` | In place, through C# 14's instance compound-assignment operators (`public void operator +=(Vector other)`, and `-=`, `*=`, `/=` with vectors and scalars), as QuantLib's change their target; the binary operators return new objects, and `Clone()` is a deep copy. C# uses the instance operator only when the target is a variable (a local, a parameter, a field, an array element). On a property, or an indexer that doesn't return a `ref`, `x.P += y` calls the binary operator and the setter, so it rebinds. So a `std::vector<Array>` whose elements change in place becomes an array (`Vector[]`), never a `List<Vector>`, and the review checks every compound assignment whose target is a property or an indexer. |
| A function-call `operator()` (a distribution, an interpolation, a solver's function, `EndCriteria`'s check) | A method named `Evaluate`. An `operator()` that reads an element (`Matrix(i, j)`) becomes an indexer. |
| Random-number and path generators | Mutable working types with QuantLib's API (§2.7). |
| The Monte Carlo framework (`McSimulation`, `MonteCarloModel`, the single- and multi-variate traits, `PathPricer`, `MultiPath`, `MultiPathGenerator`) | `McSimulation<TPath, TStats>` an abstract, immutable base whose protected hooks (`PathPricer()`, `PathGenerator()`, `TimeGrid()` and the control variate's) each return a new object per call; its protected `Calculate` builds a new `MonteCarloModel`, runs QuantLib's `value` or `valueWithSamples` loop on it and returns it, so QuantLib's mutable `mcModel_` is the call's result (§2.7). `MonteCarloModel` and `MultiPathGenerator` are working types. Seeds are explicit. The draw-price-add loops allocate nothing per sample after their first batch, zero-allocation tests pinning them: the multi-dimensional one steps the process over `MultiPathGenerator`'s held buffers through `StochasticProcess`'s in-place pair, `InitialValues(Span<double>)` and `Evolve(t0, x0, dt, dw, Span<double>)` (the S-hes plan's HR2), whose defaults call the allocating forms and copy, so a landed process keeps its bits and a process that overrides them (Heston, Bates) gives the allocating forms' bits without allocating. `StochasticProcessArray` overrides the pair with QuantLib's arithmetic, the correlated increments written row by row in the matrix product's order and each process stepped on its own entry (the S-exo plan's ER1), so a basket's paths step without allocating. The basket payoffs (`BasketPayoff`, an extension point) take the assets' prices as a `ReadOnlySpan<double>` (`Evaluate(ReadOnlySpan<double>)`, `Accumulate`), which an array, a `Vector`'s storage or a stack buffer passes, so a path pricer passes the final prices through a buffer it holds where QuantLib builds an `Array` per path. The vanilla bases (the S-van plan's VR1): `MCVanillaEngine<TMc, TPath, TStats>` and `MCLongstaffSchwartzEngine<TOption, TMc, TMcCalibration, TPath, TState, TStats>` are abstract, unsealed and immutable over `McSimulation`, built per call by a static engine's `Calculate` with QuantLib's constructor checks; the Longstaff-Schwartz calibration paths, regression coefficients and exercise statistics are the call's (`LongstaffSchwartzPathPricer` a working type, its exercise times given beside its grid), the calibration seed's default the documented seed plus 1768237423 modulo 2³², and the builders (`MakeMCEuropeanEngine`, `MakeMCAmericanEngine`, `MakeMCDigitalEngine`) sealed records whose `ToEngine()` returns the engine delegate, the seed required. A Black-Scholes engine's paths run on its clock (`FdmBlackScholesOp.Clock`, the process read through `GeneralizedBlackScholesProcess.OnClock`, internal), the payoffs discounted there; a Heston engine's on the model's clock, the earliest of its curves' dates (`HestonProcess.OnClock`, internal). The barrier engine (the S-bar plan's BR1 (i)): `MCBarrierEngine` a static class over a private sealed simulation deriving `McSimulation<MonteCarloPath, TStats>` directly, as QuantLib's engine derives `McSimulation`; its path pricers, `BarrierPathPricer` and `BiasedBarrierPathPricer`, public and sealed with QuantLib's constructors and checks, the bridge's uniforms read as a view of its generator's buffer; the bridge's stream QuantLib's MT(5), MT(6) where the pricing seed is 5 on the Mersenne Twister traits, whose path uniforms MT(5) would repeat (`Divergences/McBarrierSeed5Tests.cs`); and `MakeMCBarrierEngine` a sealed record as the vanilla builders are. **The market models** (the S-mm plan's MR1): the models are immutable (`EvolutionDescription`, `MarketModel` with QuantLib's covariance caches computed once under a lock, and every model, correlation and factory), a matrix read returning a copy (`PseudoRoot(i)`, `Covariance(i)`, `Correlation(i)`); the curve states, evolvers and drift calculators are working types, with `Clone()` where QuantLib has `clone()`, their vector reads `ReadOnlySpan<double>` views of their own buffers, valid until the state is next set or the read is made with another argument, and their inputs spans; an evolver builds its Brownian generator from the factory it is given, copies each step's pseudo-root at construction and reads nothing shared, so one model serves many evolvers on many threads, and its `CurrentState` is its own curve state, read only by contract and valid until its next step; one evolution step allocates nothing after a path's warm-up (`Conventions/MarketModelAllocationTests.cs`). **The market-model products** (the S-mmp plan's MMPR1): the products, composites, exercise strategies, exercise values, node-data providers, basis systems and engines are working types, with `Clone()` where QuantLib has `clone()`; a product steps by `NextTimeStep(CurveState, Span<int>, MarketModelMultiProductCashFlow[][])` into buffers its caller sizes once, the cash flow a mutable struct; a pathwise product by `NextTimeStep(CurveState, Span<int>, MarketModelPathwiseMultiProductCashFlow[][])`, its cash flow a class over an amount buffer of 1 + the number of rates its caller sizes, copied by `CopyFrom` where QuantLib assigns one; a holder (a composite, an engine) holds clones of the products it is given, and an engine holds its caller's evolver by reference, one engine at a time; the pathwise engines copy each step's pseudo-root at construction, and the vega engines write their means and errors into spans their caller sizes (`NumberOfResults`), QuantLib's caller vectors; a path of the accounting, pathwise, vega and proxy Greek engines allocates nothing after a warm-up path (`Conventions/MarketModelProductAllocationTests.cs`), while `CollectNodeData`, the regressions and the upper-bound engine allocate as QuantLib's do, the regression and the
parametric optimisation returning records of their estimate and coefficients or parameters and changing the caller's
node data in place, QuantLib's reference outputs; the discounters, bump classes, pseudo-derivatives and historical analyses are immutable. QuantLib's composite `finalize` is `FinalizeComposite()`, C# reserving `Finalize()` for a finalizer. |
| The finite-difference solvers (`Fdm1DimSolver`, `Fdm2DimSolver`, `Fdm3DimSolver`, and the model solvers over them: `performCalculations`, `valueAt`, `thetaAt`) | Immutable and sealed: the rollback runs in the constructor, over the problem's working types (the operator, the scheme, the step conditions), where QuantLib's runs on the first query, and the queries interpolate the stored values as QuantLib's do (`Fdm3DimSolver`: a bicubic spline in each layer of the third coordinate, then a monotonic natural cubic spline across the layers). The operators step in place: a rollback of the Heston, Bates and Heston-Hull-White operators on Hundsdorfer's scheme, the engines' default, allocates nothing after the first (`Conventions/HestonAllocationTests.cs`; the S-hes plan's HD8); implicit Euler and TR-BDF2 run the Krylov paths, measured, not pinned. `FdmNdimSolver` takes its dimension from the mesher's layout, where QuantLib's is a template parameter, and reads its values through `MultiCubicSpline`, immutable and safe across threads, its second derivatives computed at construction by QuantLib's recursion and each evaluation's coefficients in scratch of its own, where QuantLib's evaluates through `mutable` members (the S-exo plan's ER3); the spline takes one to fifteen dimensions, QuantLib's list. |
| The statistics (`GeneralStatistics`, `IncrementalStatistics`, and `GenericGaussianStatistics<S>` and `GenericRiskStatistics<S>`, which derive from their type parameter) | `GeneralStatistics` and `IncrementalStatistics` sealed working types under `IStatistics`, `IEmpiricalStatistics` for the sample-keeping one; the Gaussian and empirical risk layers extension members over a constrained type parameter (`GaussianStatisticsExtensions` over `ISummaryStatistics`, a mean and a standard deviation, which `StatsHolder` holds; `RiskStatisticsExtensions` over `IEmpiricalStatistics`); QuantLib's `Statistics` and `RiskStatistics` typedefs read as `GeneralStatistics`. `IncrementalStatistics` reads Boost.Accumulators' arithmetic, ported under `src/NanoQL/Boost/Accumulators/`, in Boost's order: the weighted variance's recurrence on the lazy mean, the moments' powers by squaring, the minimum from the largest `double`. The sequence statistics (the S-mmp plan's MMPR2): `GenericSequenceStatistics<TStats> where TStats : IStatistics, new()` an unsealed working type, QuantLib's lazy dimension kept, each `IStatistics` and Gaussian member lifted over the components as an instance member returning `ImmutableArray<double>`, the empirical risk members C# 14 extension members constrained to `IEmpiricalStatistics` (`GenericSequenceStatisticsExtensions`), and QuantLib's protected members protected views; `SequenceStatistics` (over `GeneralStatistics`) unsealed, an extension point, and `SequenceStatisticsInc` (over `IncrementalStatistics`) sealed; `DiscrepancyStatistics` a sealed working type on that seam (the S-core plan's CR5 (b)), overriding `Add` and `Reset`, its constants fixed with the dimension and its own copy of the points; `Histogram` immutable, computed at construction; `ConvergenceStatistics<TStats, TRule>` sealed, holding its statistics, which C# can't derive from a type parameter, with `DoublingConvergenceSteps` a readonly struct. |
| `OptimizationMethod::minimize(Problem&, …)` | `Minimize(problem, endCriteria)` returns a `ProblemResults` record holding what QuantLib leaves in the `Problem` it changes: `CurrentValue`, `FunctionValue`, `GradientNormValue`, the two evaluation counts, and the `EndCriteriaType`. `Problem` is an immutable description (cost function, constraint, initial value), and a method holds only its configuration. The methods work on a `ProblemState`, a working type built from a `Problem` that carries QuantLib's mutable `Problem` API (`Reset`, `Value` and `Gradient`, which count, `CurrentValue`, whose setter copies, and `FunctionValue`). `Minimize(problemState, endCriteria)` continues from the state's current value, for line searches and hybrid methods; both call `protected abstract MinimizeCore(ProblemState, EndCriteria)`. A second `minimize` on one QuantLib `Problem` starts from the last optimum, where `Minimize(problem, …)` starts from the initial value each time: a documented divergence. A method keeps nothing between minimizations: a line-search method's state through one minimization (BFGS's inverse Hessian) is a `LineSearchBasedMethodState` its `NewMinimizationState` creates at each start and its five-argument `GetUpdatedDirection` reads, the four-argument one refusing by default (the S-core plan's CR5 (a)); a seeded optimizer (differential evolution, CMA-ES) takes its seed as a required constructor argument and builds its generator in each minimization, and simulated annealing copies the generator it is given, so a second minimization repeats the first where QuantLib's continues its stream (CR2 (c)); a configuration is a sealed record whose `With…` methods return copies (CD12); QuantLib's sorts of costs are MSVC's where its numbers rest on them, the partial sort's scan emulated and `std::sort` taken stably (CD5). |
| `Solver1D<Impl>` (the curiously recurring base) | `Solver1D<TSelf, TFunction>`. `TSelf` types the setters that return a modified copy (`SetMaxEvaluations`, `SetLowerBound`, `SetUpperBound`), and `TFunction` is the function concept the solver needs (`ISolver1DFunction`; `IDerivativeSolver1DFunction` for `NewtonSafe`). The public non-virtual `Solve<TF>(f, accuracy, guess, step)` and `Solve<TF>(f, accuracy, guess, xMin, xMax)`, `where TF : struct, TFunction`, run QuantLib's bracketing and then call `protected abstract SolveCore<TF>(f, accuracy, bracket)`, whose `Solver1DBracket` record holds what QuantLib's base sets before `solveImpl`. Each solver adds overloads taking delegates. |
| `Integrator` (`operator()`, `numberOfEvaluations`, `absoluteError`, `integrationSuccess`) | Immutable: it holds QuantLib's settings, and `SetAbsoluteAccuracy` and `SetMaxEvaluations` return copies. `Integrate(f, a, b)` is QuantLib's `operator()` and returns the value; `IntegrateWithStatistics(f, a, b)` returns an `IntegrationResult` of the value and the call's evaluation count, which QuantLib keeps in a mutable member it resets at each call. A by-product of an integration inside an engine comes back with its value the same way (`FourierIntegration`'s count). `IntegrationResult.AbsoluteError`, a `double?` init-only property beside its two positional members, is the integrator's estimate where QuantLib's sets one (the non-adaptive Gauss-Kronrod rule; tanh-sinh and exp-sinh), `null` where QuantLib's `absoluteError()` reads an unset member (every other integrator, the adaptive Gauss-Kronrod rule among them), and 0 on an empty interval. `Integrator.IntegrationSuccess(result)` is QuantLib's test of the result's count and error, and refuses a result with no error. The adaptive Gauss-Kronrod rule iterates over an explicit stack in QuantLib's order, so its values and refusals are QuantLib's on a stack deep enough for QuantLib's recursion, which overflows a default one. Exp-sinh runs on Boost's `exp_sinh`, ported under D19 as tanh-sinh is (the S-core plan's CR6). A moment-based Gaussian polynomial (`MomentBasedGaussianPolynomial`) keeps QuantLib's memo of its recursion (Revision 79) as a lazy table, filled to the index asked under a lock, so one polynomial serves rules built on several threads; without it a 12-node rule makes 511,659,097 moment calls. The trigonometric Gauss-Laguerre polynomials compute each moment and factorial by a loop per call, the same products in QuantLib's order, and keep nothing (the S-core plan's CR5 (d)). |
| Lattices and discretized assets (`Lattice`, `TreeLattice`, `TrinomialTree`, `DiscretizedAsset`: `initialize`, `rollback`, `partialRollback`, `statePrices`, `setSpread`) | **The discretized asset is a working type** (§2.5): mutable, owned by one calculation, never held by an immutable object; an engine builds its assets inside `Calculate`. Its `Values` is a `Span<double>` of the current step's size over its own buffer, which the asset's code reads and writes as QuantLib's `values_`. A lattice steps into the asset's second buffer and swaps the two (`StepValues(size)`, `CommitStep(size, time)`), and `Initialize` reserves both (`ReserveCapacity(width)`) at the widest step the rollback passes through, which a non-uniform grid can make wider than its first step's; `Reset` sets the values through the protected `ResetValues(size, value)`, QuantLib's `values_ = Array(size, value)`. So a rollback allocates nothing, where QuantLib's allocates an array per step, and a zero-allocation test pins it. Where QuantLib's composite builds a sub-asset per event, the port keeps one, re-initialized per event, its buffers reserved at the widest step of the composite's own rollback. **Lattices are immutable once built:** a tree lattice stores the state-price rows its construction computes, through the protected `ComputeStatePrices(until)` (Hull-White's tree computes them as it fits its parameter, before the tree is returned), and computes a row beyond them per call without keeping it; `SetSpread` returns a copy sharing the tree and its stored rows. A class whose constructor calls `ComputeStatePrices` seals the members it reads. A built lattice is thread-safe, and is reused by passing it to an engine's lattice overload. QuantLib's curiously recurring members (`size`, `discount`, `descendant`, `probability`, `stepback`) are abstract and virtual members, and `Tree.Branches` is QuantLib's `enum { branches = N }`. An engine whose asset QuantLib takes as a template parameter takes it as a type parameter beside the tree's, the asset built through a static abstract factory as the trees are (`IBinomialTree<TSelf>.Create`): `BinomialBarrierEngine.Calculate<TTree, TAsset>` with `TAsset : DiscretizedAsset, IDiscretizedBarrierAsset<TAsset>`, the plain and Derman-Kani barrier assets (the S-bar plan's BR2). |
| `CalibratedModel::calibrate`, `setParams`, `CalibrationHelper` | A static `Calibrate` that returns an immutable parameter object: `model.Calibrate(helpers, method, endCriteria, context, constraint, weights, fixParameters)`, a generic extension method on `ICalibratedModel`, returns `CalibrationResults<TModel>`, the calibrated model of the receiver's type with QuantLib's end criteria, problem values and number of function evaluations, which QuantLib keeps in the model. It runs QuantLib's sequence and counts: the checks of the helpers, the weights and the fixed parameters, in that order; the projection of the fixed parameters and the projected constraint; the minimization of the square root of the weighted sum of the squared calibration errors, each evaluation pricing the helpers against a new model built from its trial parameters; and one more evaluation for the problem values, which the count includes. `Value(params, helpers, context)` returns the cost and changes nothing, where QuantLib's leaves the model at the parameters. A helper prices against the model it is given, `CalibrationError(model, context)`; a Black helper holds its pricing engine as a delegate that takes the model being calibrated (the instrument, the context, then the model), set by `SetPricingEngine`, which returns a copy, and computes its market value at construction, under the context it is built with, through the base's protected `ComputeMarketValue`, which a derived constructor calls last. Models and their `Parameter`s are immutable: `Parameter.SetParam(i, x)` returns a copy, and QuantLib's `setParams` is `SetParams`, a generic extension method over `WithParams`, which copies the model, writes the copy's arguments and runs its `GenerateArguments` on the copy, refusing a parameter array of the wrong size before any write. The contract is the interface `ICalibratedModel` (`Arguments`, `Constraint`, `Params()`, `WithParams`), which QuantLib's abstract `CalibratedModel` implements with QuantLib's machinery, so a model with another base class implements it over its own. QuantLib's mixin bases `AffineModel` and `TermStructureConsistentModel` are the interfaces `IAffineModel` and `ITermStructureConsistentModel`, which an engine tests with `is` where QuantLib's uses `dynamic_cast`. A model whose QuantLib class overrides `calibrate` to fix parameters by default gives them through `ICalibratedModel.DefaultFixParameters`, a default interface member, empty by default, which `Calibrate` reads when the caller fixes none (the Markov-functional model's `FixedFirstVolatility()`). GSR's iterative calibrations, one calibration per helper each moving one parameter, return the last step's record, each step weighted by its own helper's weight. |
| The Gaussian1d models (`Gaussian1dModel`, `Gsr`, `MarkovFunctional`): the evaluation date and swaps the base caches, GSR's memo maps and `flushCache`, `Gsr::numeraireTime(T)` | `Gaussian1dModel` is an abstract, unsealed, immutable base implementing `ITermStructureConsistentModel`; `Gsr` and `MarkovFunctional` implement `ICalibratedModel` over it. A member that reads QuantLib's cached evaluation date or historic-fixings flag, or builds a swap under the context, takes the context (`ForwardRate`, `SwapRate`, `SwapAnnuity`). GSR's six memo maps and the base's swap cache live for one call, in the internal `Gaussian1dMemo`, which each engine call builds and passes through the model's internal members and each public member builds for itself: a price computes each term once, as QuantLib's first price does, and nothing outlives the call; there is no `flushCache`, since a parameter change builds a new model. A derived model of another assembly implements QuantLib's protected hooks `NumeraireImpl` and `ZerobondImpl` and sets its state process with `SetStateProcess` while its constructor runs. `Gsr.WithNumeraireTime(T)` and `GsrProcess.WithForwardMeasureTime` return copies, the process's core moved with it. A curve given on another clock than the model's is read at the instant QuantLib's argument denotes, by the date a time comes from (the mismatched-dates row). The Gaussian integrals' `std::erf` is QuantLib's own `ErrorFunction`. |
| `SeedGenerator` | An explicit input: a seed is a parameter, a `long` in [1, 4294967295] with no default, refused otherwise with `ArgumentOutOfRangeException`. QuantLib's seed 0, which draws a seed from the clock, is refused: a documented divergence. No library code reads the clock. |
| `QL_REQUIRE` on a public method's parameter | `ArgumentException`, with `ParamName` set. A null argument a member doesn't accept throws `ArgumentNullException`. |
| An enum argument a member always refuses: a value outside the type's members (C# lets any integer be cast to an enum), or a member it never handles (`Period(OtherFrequency)`) | `ArgumentOutOfRangeException`, naming the parameter, with QuantLib's message. A public member that switches on the value, stores it or computes with it checks it on entry, before any work that reads it; one that passes it on under the same parameter name to such a member relies on that member's check; one that only compares it with named members needs no check. This replaces QuantLib's `QL_FAIL`, in a switch's default or on a `case` label, wherever the value is an argument the member always refuses. A value refused only together with other arguments keeps the other rows' mapping: a mixed compounding in `ImpliedRate`, refused unless the compound factor is 1, and an intraday unit in `Calendar.Advance`, refused unless the count is 0, throw `NanoQLException`. |
| Behaviour QuantLib leaves undefined for an input: an unchecked read past an array's or a container's end (`operator[]`, an iterator), a NaN given to a sort, the front or back of an empty container, a signed overflow. An unsigned `Size` wrapping below zero is defined, and counts here only when the wrapped value reaches such a read | Refused before the undefined step, as the `QL_REQUIRE` rows map a check: `ArgumentException` (`ArgumentOutOfRangeException` for a value outside a range) for an argument the member checks on entry, `NanoQLException` for a value already stored. A documented divergence, with a `Divergences/` test that names QuantLib's lines; where QuantLib defines the error, its text is kept. Each refusal's type, message and `ParamName` is asserted by a `Conventions/` test (§9.4). **`Natural` and `Size`:** a negative `int` for one of QuantLib's unsigned parameters (fixing, settlement, lookback and lockout days) is refused on entry with `ArgumentOutOfRangeException` naming the parameter; QuantLib's `Integer` parameters (a payment lag) stay signed. |
| A C++ standard-library exception QuantLib lets escape for an input: `std::length_error` from a `reserve` or an allocation of a wrapped size, `std::out_of_range` from an `at()` (`Schedule::date`) | Refused at the same point, after QuantLib's earlier checks, with the type the `QL_REQUIRE` rows give (`ArgumentException` for an argument the member checks on entry, `NanoQLException` for a value already stored) and a text naming the cause ("empty schedule"), since the library's text is the platform's ("vector too long" on MSVC). A convention, not a divergence: QuantLib refuses there too. A `Conventions/` test asserts the type, message and `ParamName`, its comment naming QuantLib's line and exception (§9.4). Where QuantLib's own check refuses first, as a `QL_REQUIRE` printing the wrapped count, its text is the one to keep, and a text nanoql replaces is a documented divergence. |
| Other `QL_REQUIRE`, `QL_FAIL`, `QL_ENSURE` | `NanoQLException`, including a `QL_FAIL` on a value already stored. A value outside its enum can no longer be stored, so such a branch fails only for a member the operation doesn't handle (`Period(3, Hours).Frequency()`), as in QuantLib. The wording is kept; numbers in it use C# formatting. |
| A refusal raised inside a callee, on a value its caller computed from its own arguments or settings (a payment date from a payment lag, the schedule a builder builds) | Reaches the caller as the callee raised it: QuantLib's text, and the callee's exception type and `ParamName`. The caller doesn't map it again, so an instrument and the leg builder it uses refuse the same input alike. |
| `Null<T>()`, `ext::optional<T>` | `T?`. QuantLib's null `Date()` is `Date?` null; `default(Date)` is never a valid date, and public members given it throw `ArgumentException`. Ported `d == Date()` becomes `d is null`. |
| `ext::shared_ptr<T>`, `ext::make_shared` | A plain `T` reference; `new T(...)`. |
| `ext::function` | `Func<…>` or `Action<…>`. |
| `std::max`, `std::min` where an input can be NaN | QuantLib's semantics: `std::max(a, b)` is `a < b ? b : a` and `std::min(a, b)` is `b < a ? b : a`, never `Math.Max` or `Math.Min`, which propagate NaN where `std::max` keeps or drops it by position. |
| `std::sort` where elements can compare equal (a bond's coupons by date); `std::stable_sort` | A stable sort (`OrderBy` on the key). MSVC's `std::sort`, which built the wheel, runs an insertion sort up to 32 elements, which keeps equal keys in their order, and an introsort beyond, which reorders them. So nanoql gives QuantLib's order up to 32 elements and on any input without equal keys, and a `Divergences/` fact records equal keys beyond 32 (`Divergences/BondSortTests.cs`). `std::stable_sort` is a stable sort. |
| An engine or process reading structures on another day counter or reference date than its clock's (QuantLib reads them at the risk-free curve's times) | Each structure is read at the instants the clock's times denote, on its own clock (`TermStructure.OwnTime`, internal), and accrues nothing before its own reference date (`AccrualTime`), as QuantLib's analytic engines count it; where a structure shares the clock's day counter and reference date, QuantLib's read stands, so a market on one clock keeps QuantLib's bits (the S-eq plan's rulings of 2026-10-20 and 2026-10-21, under R8). A lattice or grid engine runs from the earliest reference date of the structures it reads, the risk-free curve's day counter its clock's: the finite-difference Black-Scholes engine on `FdmBlackScholesOp.Clock`, the binomial engine's tree, the analytic dividend engine's dividends, and the replicating variance-swap engine's time. A finite-difference engine assembled from the Black-Scholes mesher, operator and solver (QuantLib's barrier, rebate, Asian and shout engines) takes its maturity, dividend and exercise times and its conditions' reference date and day counter from that clock, where QuantLib takes them from `process->time`. A Monte Carlo engine on these processes runs its path grid from the same earliest date, its times on the risk-free curve's day counter, nothing accruing before a structure's date; the processes read their structures on the risk-free curve's clock, so a grid starting before that curve's date needs the risk-free curve read there as accruing nothing, where QuantLib's read throws: the vanilla Monte Carlo engines read the process on that clock (`GeneralizedBlackScholesProcess.OnClock`, internal, the process itself where the clock is the risk-free curve's) and discount their payoffs there. The analytic Heston engine, its calibration helper and its optimal alpha's date form run the model's time from the earliest curve date (`HestonProcess.ModelTime`, internal), the risk-free discount read by date. An engine reading a structure by date reads nothing accrued before its reference date (`AccruedDiscount`, `AccruedVariance`, `AccrualTimeTo`, internal), where QuantLib's read throws, so an option expiring before a later structure's date is priced. A Greek formula over one time takes each structure's term over its own time: the analytic engines' theta, which QuantLib's `blackScholesTheta` checks, and the European engine's rho beside a discount curve of its own, which an identity over QuantLib's own dividend rho and value checks (the user's ruling). QuantLib's engine on the market restated from the earliest date is the check (`Divergences/FdmMismatchedTimesTests.cs`, `Divergences/BinomialMismatchedTimesTests.cs`, `Divergences/DividendMismatchedTimesTests.cs`, `Divergences/ProcessMismatchedTimesTests.cs`, `Divergences/ExerciseBeforeReferenceDateTests.cs`, `Divergences/VarianceSwapMismatchedTimesTests.cs`, `Divergences/AnalyticThetaMismatchedTimesTests.cs`, `Divergences/EuropeanRhoDiscountCurveTests.cs`, `Divergences/HestonMismatchedTimesTests.cs`, `Divergences/McMismatchedTimesTests.cs`). **The CDS engines (from S-core):** the mid-point and integral engines read the discount before the discount curve's reference date as 1, and the survival and default probability before the probability curve's as 1 and 0, QuantLib's own premise for its two-date default probability, which excuses its first date only; QuantLib's reads refuse them (`Divergences/CdsEngineReferenceDateTests.cs`). **The credit portfolio (from S-xcr, its XR8 (x)):** the basket's probabilities, the latent models' unconditional probabilities, the n-th-to-default engine and the CDO engines read a default probability before a default curve's reference date as 0 and a discount before the discount curve's as 1, and the random models count a default's days from the evaluation date, a day before it, or before a curve's date, of probability 0 (`Divergences/NthToDefaultReferenceDateTests.cs`, `Divergences/CdoReferenceDateTests.cs`). |
| A closed form QuantLib evaluates with constant parameters on term structures (one zero rate, yield and volatility per maturity: the compound, chooser and extensible engines) | The formula with each parameter read as a variance to each date and a discount by date, exact under deterministic rates, dividends and volatility, its critical spot solved on the forward Black values; on flat markets QuantLib's algebra, so its values within the tolerance (R8; the S-exo plan's ruling of 2026-10-25). Each form is checked against the exact value at its first date, the payoff there integrated over the lognormal spot (`Divergences/CompoundTermStructureTests.cs`, `Divergences/SimpleChooserTermStructureTests.cs`, `Divergences/ComplexChooserTermStructureTests.cs`, `Divergences/HolderExtensibleTermStructureTests.cs`, `Divergences/WriterExtensibleTermStructureTests.cs`), and its Greeks against bumps of the form. The geometric Asians (the discrete and continuous average-price engines and the average-strike engine) price ln G exactly, normal with its mean and variance from each fixing's forward and variance by date, every time from the evaluation date; Levy's engine matches the average's two moments under the structures, its log-variance through log1p of the normalized covariance (the S-bar plan's BR4 (a)–(b); `Divergences/GeometricAsianTermStructureTests.cs`, `Divergences/LevyTermStructureTests.cs`). Their integrals run on a first 15-point estimate at 1e-12; a nested integral whose integrands have kinks at the structures' nodes runs on QuantLib's adaptive Gauss-Lobatto rule, where NanoQL's Kronrod rule accepts panels whose two estimates agree while both are off (Levy's covariance, 9e-9 off the exact value on shaped markets on Kronrod, within 7e-13 on Gauss-Lobatto; the user's ruling of 2026-10-26). Later slots measure their closed forms on term structures and bring them under this rule. The first-passage closed forms (the analytic barrier, binary barrier, double barrier, double-barrier binary, partial-time, soft and two-asset barrier engines) keep QuantLib's equivalent-constant reads, the variance and discounts to expiry on the volatility's clock: no exact closed form exists for a first passage under general deterministic term structures, only where the drift-to-variance ratio is constant; each engine documents its measured accuracy on shaped markets and names the term-structure-exact routes, the finite-difference Black-Scholes barrier engine and the Monte Carlo engines (the S-bar plan's BR4 (d)). The single-asset forms miss by −1.6% to +3.9% there; the two-asset engine, whose one correlation term can't carry a volatility that moves over the option's life, by −24% to +21%, kept and documented as QuantLib's formula (the user's ruling of 2026-10-26). |
| `std::min_element`, `std::max_element` where an element can be NaN | A loop with `<` keeping the first extreme, as the standard library's does, never LINQ's `Min` or `Max`, which treat NaN as the smallest value. |
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
| `io::` formatters (`io::ordinal`, `io::iso_date`) | Methods of the header's `<Header>Functions` class returning `string`. `io::formatted_date` is not ported: a `Date` converts to `DateOnly`, whose `ToString(format)` does that job. `DateParser::parseFormatted`, Boost's format engine on a string, isn't ported either: `DateOnly.ParseExact` does its job. |
| `operator<<` on a class | `ToString()` with QuantLib's text. |
| `operator<<` on an enum | Not ported where the enum's `ToString()` already gives QuantLib's text: its stream strings equal the member names and no alias shares a value. Otherwise the text is a static `ToString(value)` on the header's `<Header>Functions` class, and a class whose `ToString()` embeds it calls it. Each port records this check per enum. |
| Operators whose operands nanoql doesn't own (`Integer * TimeUnit`) | C# 14 extension operators, in a static class `<Type>Extensions` in the folder of the QuantLib file that declares them. |
| `std::fma` (in Boost code) | `Math.FusedMultiplyAdd`. |
| `std::log1p`, and a C runtime function .NET lacks | Written in the file of the port that calls it, from a published formulation, never one that loses precision: `double.LogP1` computes `Math.Log(1 + x)` and loses `x`'s low digits. `std::log1p` (in `Log1pFunctions`, Boost's `log1p` port) is Goldberg's (D. Goldberg 1991, Theorem 4), with `u = 1 + x`: `x` where `u` rounds to 1, and `Math.Log(u) * x / (u − 1)` otherwise; the code's own guard returns `Math.Log(u)` where `u` is +∞, which the ratio would turn into NaN. It is within a few ulp of the C runtime's, so an output that reaches it can differ from QuantLib's in its last places, within the tolerance (§7.3) (Choi's implied volatility: 4 of the wheel's 608 outputs, 1 ulp). `std::expm1` (in `Expm1Functions`) is Kahan's, as CPython's `_Py_expm1` computes it, with `u = exp(x)`: `x` where `u` is 1, −1 where `u − 1` is −1, and `(u − 1)·x / Math.Log(u)` otherwise; its guard returns `u` where `u` is +∞, where QuantLib's runtime returns +∞. It agrees with MSVC's on about three arguments in four, where `double.ExpM1` agrees on one in two, so an output that reaches it can differ from QuantLib's in its last places, within the tolerance. |
| A generated expression that cancels at small parameters or overflows at large ones (the Heston expansions' and COS's coefficients in κt) | Written once over the internal arithmetic interface `IRealArithmetic<T>`, QuantLib's expression unchanged, and evaluated in the arithmetic its parameters call for, between cuts measured per expression on a grid of the other inputs: QuantLib's double (`RealDouble`, the same IEEE operations), and so its bits, from the cut where QuantLib's double first departs from the expression's 50-digit value by more than 1e-12 relative up to the cut where its exponentials overflow; below the first, double-double (`DoubleDouble`, about 31 digits: Dekker's and Knuth's error-free sums and products through `Math.FusedMultiplyAdd`, QD's division, square root and exponential), down to the floor where double-double cancels in turn; below that, quad-double (`QuadDouble`, about 62 digits, the same library's), down to its own floor, and below it the cubic in the parameter through four quad-double values; above the second, an extended-range double (`ExtendedDouble`, a double and an integer exponent). |
| The numeric macros of `qldefines.hpp` (`QL_EPSILON`, `QL_MIN_POSITIVE_REAL`, …) and `mathconstants.hpp` (`M_PI`, `M_SQRT2`, …) | `internal const` fields of `NumericLimits` and `MathConstants`, keeping the macro names. `QL_EPSILON` is machine epsilon, `2.220446049250313E-16`, and `QL_MIN_POSITIVE_REAL` the smallest normal double; neither is `double.Epsilon`. |
| Deprecated members, `ql/legacy` | Not ported, whether QuantLib deprecates them by its note or by its `[[deprecated]]` attribute alone. |
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
- **The fixings and the at-par switch:** `Fixings` (`IndexFixings`, default `IndexFixings.Empty`, never null) and
  `UseAtParCoupons` (`bool`, default `true`). `IndexFixings` compares by content: the same names, by QuantLib's ASCII
  case-insensitive comparison and whichever spelling was kept, each with an equal history. `TimeSeries<T>` compares
  its dates and values element by element and computes its hash when it is built, so the store's hash combines one
  stored hash per history. So the record's synthesized equality holds: two contexts holding the same fixings are
  equal however their stores were built.
- **The exchange rates and the money settings:** `ExchangeRates` (default `ExchangeRates.Default`, never null) and
  `MoneySettings` (a record, default `new MoneySettings()`, never null). `ExchangeRates` compares by content: the same
  pairs, each with equal entries (rate, start and end dates) in the same order, its hash computed when it is built; so
  the record's synthesized equality holds for them too.
- **"Today":** the context has no "today". `Date.TodaysDate(TimeProvider?)` reads the clock at the API's edge, and no
  library code calls it.

### 2.3 Builders

QuantLib's builders (`MakeSchedule`, `FixedRateLeg`, `MakeVanillaSwap`, …) are immutable fluent builders.
- QuantLib's class and method names are kept (`From`, `To`, `WithTenor`, `Forwards()`, `EndOfMonth(bool)`).
- A builder is a `sealed record` holding its settings as init properties, and each method returns a modified copy
  (`with`), so a builder kept and reused is never changed under its holder.
- QuantLib's conversion operator becomes a method named `To<Type>()` (`MakeSchedule.ToSchedule()`,
  `FixedRateLeg.ToLeg()`). There is no implicit conversion, since the conversion can throw. A conversion that reads a
  context field takes the context (`IborLeg.ToLeg(context)`, whose default pricer reads the at-par switch).
- **QuantLib's members and defaults are kept.** `WithPricingEngine` takes the engine as a delegate,
  `Func<TInstrument, EvaluationContext, TResults>`, and `WithDiscountingTermStructure` stays as QuantLib's shortcut.
  An engine or curve given to a builder computes what QuantLib's builder computes at build time (the par rate when no
  fixed rate is given; an at-the-money strike) and is never stored, since the built instrument holds no engine
  (§2.1). Where QuantLib's builder reads a curve out of its engine (`MakeCapFloor`'s at-the-money strike, on the Black
  or Bachelier engine's discount curve), a function can't give it, so the builder takes the curve itself through
  QuantLib's sibling member's name, `WithDiscountingTermStructure`, and computes the strike as QuantLib does;
  `WithPricingEngine` stays, used for nothing.
- **Their refusals.** A refusal of an empty value QuantLib uses unchecked keeps .NET's `ArgumentNullException` text,
  unless QuantLib checks the same input with a text of its own. A check QuantLib makes at conversion stays there, after
  QuantLib's earlier checks: the leg builders' conversions refuse an empty schedule with `NanoQLException`, "empty
  schedule", so the swaps that build their legs refuse it there too, and `MakeOIS`'s conversion refuses its rounding
  precision, where the swap's constructor refuses it.

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
- **Extension points so far:** `Calendar`, `DayCounter`, `Event`, `CashFlow`, `Coupon`, `Extrapolator`, `TermStructure`,
  `YieldTermStructure`, `Instrument`, `TridiagonalOperatorTimeSetter`, `Interpolation`, `Solver1D<TSelf, TFunction>`,
  `OptimizationMethod`, `CostFunction`, `Constraint`, `ParametersTransformation`, `StochasticProcess`,
  `StochasticProcess1D`, `StochasticProcessDiscretization`, `StochasticProcess1DDiscretization`, `FinancialIndex`,
  `InterestRateIndex`, `VolatilityTermStructure`, `OptionletVolatilityStructure`, `FloatingRateCouponPricer`,
  `IborCouponPricer`, `OvernightIndexedCouponPricer`, `FixedVsFloatingSwap`, `BootstrapHelper<TTermStructure>`,
  `RelativeDateBootstrapHelper<TTermStructure>`, `RateHelper`, `RelativeDateRateHelper`, `ZeroYieldStructure`, `Payoff`,
  `TypePayoff`, `StrikedTypePayoff`, `SmileSection`, `SwaptionVolatilityStructure`, `SwaptionVolatilityDiscrete`,
  `SwaptionVolatilityCube`, `Interpolation2D`, `Option`, `Lattice`, `TreeLattice`, `TreeLattice1D`, `Tree`,
  `DiscretizedAsset`, `CalibratedModel`, `Gaussian1dModel`, `ShortRateModel`, `OneFactorModel`, `OneFactorAffineModel`,
  `OneFactorModelShortRateDynamics`, `Parameter`, `CalibrationHelper`, `BlackCalibrationHelper`, `CallableBond`,
  `CallableBondVolatilityStructure`, `CapFloorTermVolatilityStructure`, `StrippedOptionletBase`, `OptionletStripper`,
  `InflationIndex`, `Region`, `InflationTermStructure`, `ZeroInflationTermStructure`, `YoYInflationTermStructure`,
  `Seasonality`, `CPIVolatilitySurface`, `YoYOptionletVolatilitySurface`, `InflationCoupon`, `InflationCouponPricer`,
  `DefaultProbabilityTermStructure`, `HazardRateStructure`, `DefaultDensityStructure`, `SurvivalProbabilityStructure`,
  `Claim`, `CdsHelper`, `BlackVolTermStructure`, `BlackVolatilityTermStructure`, `BlackVarianceTermStructure`,
  `LocalVolTermStructure`, `OneAssetOption`, `Dividend`, `Integrator`, `GaussianOrthogonalPolynomial`, `FdmMesher`,
  `FdmLinearOp`, `FdmLinearOpComposite`, `StepCondition`, `BoundaryCondition<TOperator>`, `FdmInnerValueCalculator`,
  `BinomialTree<T>`, `EqualProbabilitiesBinomialTree<T>`, `EqualJumpsBinomialTree<T>`, `MultiAssetOption`,
  `BasketPayoff`, `SpreadBlackScholesVanillaEngine`, `MCForwardVanillaEngine<TMc, TPath, TStats>`, `CurveState`,
  `MarketModel`, `MarketModelFactory`, `MarketModelEvolver`, `ConstrainedEvolver`, `PiecewiseConstantCorrelation`,
  `MarketModelVolProcess`, `PiecewiseConstantVariance`, `VolatilityInterpolationSpecifier`, `AlphaForm`,
  `CTSMMCapletCalibration`, `MarketModelMultiProduct`, `MarketModelComposite`, `MultiProductMultiStep`,
  `MultiProductOneStep`, `MarketModelPathwiseMultiProduct`, `ExerciseStrategy<TState>`, `MarketModelExerciseValue`,
  `MarketModelNodeDataProvider`, `MarketModelBasisSystem`, `MarketModelParametricExercise`,
  `HistoricalForwardRatesAnalysis`, `AdditionalBootstrapVariables`, `LineSearch`, `LineSearchBasedMethod` and
  `LineSearchBasedMethodState` (abstract);
  `DefaultType`, `DefaultProbKey`, `DefaultEvent`, `RecoveryRateModel`, `DefaultLossModel` and `LMIntegration`
  (S-xcr's credit portfolio; `RecoveryRateModel`, `DefaultLossModel` and `LMIntegration` abstract), and the latent
  models `LatentModel<TCopula, TInitTraits>`, `DefaultLatentModel<TCopula, TInitTraits>`,
  `ConstantLossLatentModel<TCopula, TInitTraits>` and `ConstantLossModel<TCopula, TInitTraits>` (unsealed);
  `DiscretizedOption`, `Vasicek`, `TermStructureFittingParameter`, `Projection`, `ZeroInflationIndex`,
  `YoYInflationIndex`, `MultiplicativePriceSeasonality`, `IndexedCashFlow`, `YoYInflationCoupon`,
  `YoYInflationCouponPricer`, `CPICouponPricer`, `YoYInflationCapFloor`, `GeneralizedBlackScholesProcess`,
  `LocalVolSurface`, `VanillaOption`, `OneAssetOptionResults`, `BlackCalculator`, `GaussianQuadrature`,
  `GaussJacobiPolynomial`, `Fdm1dMesher`, `TripleBandLinearOp`, `FdmCellAveragingInnerValue`, `AnalyticHestonEngine`,
  `MultiAssetOptionResults`, `ForwardVanillaOption`, `ForwardVanillaEngine`, `SequenceStatistics` and
  `CallSpecifiedMultiProduct`, whose upper-bound engine's decorated hedge derives from it (unsealed); `Swap`,
  `SimpleCashFlow`, `InstrumentResults`, `SwapResults`,
  `FixedVsFloatingSwapResults`, `OvernightIndexedSwapResults`, `Bond`, `BondResults`, `FixedRateBond`,
  `FloatingRateBond`, `CapFloor`, `BondHelper`, `Callability`, `IborIndex`, `OvernightIndex`, `Euribor`, `Bbsw`,
  `Bibor`, `EURLibor`, `DailyTenorEURLibor`, `SwapIndex`, `EquityIndex`, `Currency`, `Rounding`, `FloatingRateCoupon`,
  `IborCoupon`, `CappedFlooredCoupon`, `BlackIborCouponPricer`, `CompoundingOvernightIndexedCouponPricer`,
  `ArithmeticAveragedOvernightIndexedCouponPricer`, `TridiagonalOperator`, `CubicInterpolation`,
  `LogCubicInterpolation`, `MixedLinearCubicInterpolation`, `LogMixedLinearCubicInterpolation`,
  `RandomSequenceGenerator<TRng>`, `InverseCumulativeRng<TRng, TIc>`, `InverseCumulativeRsg<TUsg, TIc>`,
  `PathGenerator<TGsg>`, `Exercise`, `EarlyExercise`, `BermudanExercise`,
  `PiecewiseYieldCurve<TTraits, TInterpolator, TBootstrap>`,
  `PiecewiseZeroInflationCurve<TInterpolator, TBootstrap, TTraits>`,
  `PiecewiseYoYInflationCurve<TInterpolator, TBootstrap, TTraits>` and
  `PiecewiseDefaultCurve<TTraits, TInterpolator, TBootstrap>`, and the interpolated curves
  `InterpolatedDiscountCurve<TInterpolator>`, `InterpolatedZeroCurve<TInterpolator>`,
  `InterpolatedForwardCurve<TInterpolator>`, `InterpolatedSimpleZeroCurve<TInterpolator>`,
  `InterpolatedZeroInflationCurve<TInterpolator>`, `InterpolatedYoYInflationCurve<TInterpolator>`,
  `InterpolatedHazardRateCurve<TInterpolator>`, `InterpolatedDefaultDensityCurve<TInterpolator>` and
  `InterpolatedSurvivalProbabilityCurve<TInterpolator>`, `InterpolatedSmileSection<TInterpolator>`,
  `XabrSwaptionVolatilityCube<TModel>`, `ZabrSmileSection<TEvaluation>`, `AdaptiveRungeKutta<T>`,
  `TrapezoidIntegral<TIntegrationPolicy>` and `GenericSequenceStatistics<TStats>`, for their named classes (unsealed); `IInterpolator` (the interpolation
  traits: a factory of one's own overrides its `CheckPoints` with its construction checks, whose default refuses
  nothing, so that a curve on it takes a bootstrap's first-pass fallback where QuantLib's would), `IInterpolator2D` (the
  2-D factory concept), the bootstrap's concepts `IBootstrap`, `IBootstrapState`, `IBootstrapHelper`,
  `IBootstrapTraits<TTermStructure>`, `ILocalInterpolator` (the local bootstrap's interpolator), `IYieldTraits`,
  `IZeroInflationTraits`, `IYoYInflationTraits`,
  `IDefaultProbabilityTraits` and `IInterpolatedCurve<TTraits, TInterpolator>`, `ISolver1DFunction`,
  `IDerivativeSolver1DFunction` and `ISecondDerivativeSolver1DFunction` (the solvers' function concepts),
  `ITrapezoidIntegrationPolicy` (the trapezoid rules' concept), `IConvergenceSteps` (the convergence table's sampling
  rule), `IParametricExercise` (a parametric exercise rule, QuantLib's mixin base), the generator concepts `IRng`, `IInt32Rng`, `IInt64Rng`, `ISeedableRng<TSelf>`, `IRsg`, `ISeedableRsg<TSelf>`,
  `IInverseCumulative` and `IRngTraits`, and the credit portfolio's contracts `IDefaultLossModel` and `ICopulaPolicy<TSelf, TInitTraits>`, the model contracts `ICalibratedModel`, `IAffineModel` and
  `ITermStructureConsistentModel`, `IXabrInterpolation`, a fit's results, `IXabrCubeModel`, a cube's smile model, and
  the ZABR kernel tag `IZabrEvaluation`, whose members are internal, so that its tags are QuantLib's, and `IFdmScheme`,
  the finite-difference schemes' concept for `FiniteDifferenceModel<TScheme>` (interfaces); and `Solver1DBracket`,
  `ProblemState` and `BootstrapState<TTermStructure>`, sealed, which a solver's `SolveCore`, a method's `MinimizeCore`
  and a bootstrap's `Calculate` receive.
- **A bond's construction contract:** `Bond`'s derived constructors follow QuantLib's protected interface. They call
  the protected `Bond(settlementDays, calendar, issueDate)`, which builds no leg, then write the protected state
  (`cashflows`, `maturityDate`, and `redemptions` for a bond with its own) and call QuantLib's protected helpers,
  `AddRedemptionsToCashflows`, either `SetSingleRedemption` or `CalculateNotionalsFromCashflows`, which fill the
  notional schedule and the redemptions. The state and the helpers are written while the bond is built, by a
  derived constructor or on a fresh copy (`WithCashflows`), and never after: a convention the base can't enforce,
  since every member of a derived class can reach them, on which the bond's immutability rests. `Bond`'s remarks
  state it too. The protected constructor's three parameters take no defaults: a derived constructor's
  `base(settlementDays, calendar, issueDate)` reaches it, since C# prefers the overload that fills no default, and
  an outside `new Bond(…)`, which can't reach it, the public constructor with QuantLib's empty default leg.
- **Leg:** QuantLib's `Leg` is the global alias
  `global using Leg = System.Collections.Immutable.ImmutableArray<NanoQL.CashFlow>;` in `Types.cs`. Public signatures
  show `ImmutableArray<CashFlow>`.

### 2.5 Working types, views and values

- **Working types** are mutable, owned by one computation and not thread-safe, as an array is: `Vector`, `Matrix` and
  `TridiagonalOperator` (an indexer that sets, compound assignment, `Resize`, the in-place operations QuantLib's
  algorithms use, `TridiagonalOperator`'s row setters), `ProblemState`, and the random-number and path generators
  (§2.7), and the lattices' discretized assets (`DiscretizedAsset` and its subclasses, §2's lattice row). Later ports
  add finite-difference operators and schemes, and market-model evolvers and accumulators, and each names the working
  types it adds: S-mm's are the curve states, the evolvers, the drift calculators and the volatility processes;
  S-core's the Ornstein-Uhlenbeck and n-th order derivative operators, the storage condition and the old framework's
  TR-BDF2 scheme, each writing into the caller's span, beside an immutable jump mesher and numerical differentiation.
  Every other public type is immutable.
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
  The latent models' factor samplers and the polar Student t generator take one too, QuantLib's 0, the clock, refused, in [1, 4294967295] whatever the generator they build, since QuantLib's seed there is a 32-bit `BigNatural` (the S-xcr plan's XR4 (a)). One exception (the S-core plan's CR2 (a)): xoshiro256**'s seed and state words are QuantLib's `std::uint64_t`, a
  `long`'s 64 bits, any but 0 (`unchecked((long)10108360646465513120UL)` is QuantLib's seed), and its `NextInt64()` a
  `long` whose bits read unsigned are QuantLib's. QuantLib's own reductions stay and are documented: Ranlux's seed
  sequence takes the seed modulo 2147483563, Knuth's its low 30 bits; L'Ecuyer's arithmetic is 64-bit, Linux's
  `long`, which the wheel's 32-bit one meets below 2^31.
- **The concepts** are QuantLib's `RNG` and `RSG`, named in its vocabulary: `IRng` (`Sample<double> Next()`,
  `IRng Clone()`), `IInt32Rng : IRng` (`long NextInt32()`), `IInt64Rng : IRng` (`long NextInt64()`,
  `double NextReal()`, the ziggurat's), and `IRsg` (`Dimension`, `NextSequence()`,
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
  composite (`RandomSequenceGenerator`, `InverseCumulativeRng`, `InverseCumulativeRsg`, the Box-Muller,
  central-limit and ziggurat generators, `RandomizedLDS`, `PathGenerator`) clones the generator it is given, as QuantLib's by-value constructors copy it, so the caller's and the composite's states never
  alias. A factory that has just built its generator (the traits, `Create`) adopts it without a copy.
- **The traits:** `IRngTraits` declares `static abstract bool AllowsErrorEstimate` and `static abstract IRsg
  MakeSequenceGenerator(int dimension, long seed)`. A Monte Carlo engine keeps QuantLib's one traits parameter
  (`where TTraits : struct, IRngTraits`) and builds a `PathGenerator<IRsg>`. Called on the traits struct itself,
  `MakeSequenceGenerator` returns the closed generic, QuantLib's `rsg_type`, and an explicit implementation provides
  the interface's form. QuantLib's static `icInstance` isn't ported: an overload of `MakeSequenceGenerator` takes the
  inverse cumulative.
- **The Monte Carlo traits:** QuantLib's `SingleVariate<RNG>` and `MultiVariate<RNG>`, which name a path type and its
  generator, are `readonly struct`s over an `IRngTraits` implementing `IMcTraits<TPath>`: `AllowsErrorEstimate`,
  `MakeSequenceGenerator` and `MakePathGenerator`, which builds the `PathGenerator<IRsg>` or `MultiPathGenerator<IRsg>`
  QuantLib's `path_generator_type` names, behind `IPathGenerator<TPath>` (`Next()`, `Antithetic()`). An engine on
  `McSimulation<TPath, TStats>` takes them as `TMc : IMcTraits<TPath>`.
- **A Monte Carlo engine** derives from `McSimulation<TPath, TStats>`, holds its settings and its call's instrument and
  context, overrides the hooks, and reads the model its `Calculate` returns: its `SampleAccumulator` is QuantLib's
  `mcModel_->sampleAccumulator()`. Two calculations on one engine are each their own. The model reserves its
  accumulator once per batch (`IStatistics.Reserve`), so a one-dimensional batch allocates nothing per sample; a
  multi-path's process evolves a new vector a step, as QuantLib's does.
- **The vanilla engines' bases** (the S-van plan's VR1): `MCVanillaEngine` and `MCLongstaffSchwartzEngine` derive
  `McSimulation` with QuantLib's hooks; a concrete engine is a static class whose private simulation class derives a
  base and is built per call, so the Longstaff-Schwartz pricer, with its stored calibration paths, coefficients and
  exercise statistics, is one call's. The Black-Scholes process evolves without building rate objects, so the
  European and digital draw-price-add and the Longstaff-Schwartz pricing phase allocate nothing per sample.

## 3 Naming

- **Types:**
  - QuantLib's names are kept, acronyms included (`NPV`, `OISRateHelper`), so a QuantLib name maps to one nanoql
    name. Three are renamed: `Array` becomes `Vector`, `Path` becomes `MonteCarloPath`, and `Index` becomes
    `FinancialIndex`. A user who imports a clashing `System.*` namespace writes an alias
    (`using Calendar = NanoQL.Time.Calendar;`).
  - **Public nested types** move to the top level as `<Outer><Inner>`: `Option::Type` → `OptionType`,
    `Swap::Type` → `SwapType`, `DateGeneration::Rule` (a struct holding an enum) → `DateGenerationRule`.
  - **Renames** are recorded here. So far: `Array` → `Vector`, which clashes with `System.Numerics.Vector` in files that
    also import `System.Numerics`; and `Path` → `MonteCarloPath`, after its folder, `montecarlo`, since `Path` would
    clash with `System.IO.Path`. QuantLib's other names around it stay (`PathGenerator`, `MultiPath`). And `Index` →
    `FinancialIndex`, in namespace `NanoQL`, since `Index` clashes with `System.Index`, which every C# file sees;
    QuantLib's derived names stay (`InterestRateIndex`, `IborIndex`). And QuantLib's mixin bases, which are interfaces
    here and take .NET's `I` prefix: `AffineModel` → `IAffineModel`, `TermStructureConsistentModel` →
    `ITermStructureConsistentModel`, `ParametricExercise` → `IParametricExercise`; `ICalibratedModel` is the contract beside QuantLib's `CalibratedModel`, which keeps
    its name. And QuantLib's `Greeks` and `MoreGreeks`, the results a one-asset option's engine fills, are replaced by
    `OneAssetOptionResults`, which carries them; the porting map lists them as replaced. And
    `ExchangeRateManager` → `ExchangeRates`, the name of the immutable registry the context holds, where QuantLib's is
    a service's.
- **Members:** the first letter is capitalised and the rest kept (`yearFraction` → `YearFraction`; `NPV` stays).
  - **Property or method:** a member is a property when it takes no arguments, has no side effect, is cheap, has no
    precondition that throws, doesn't return a copy or a new array, and is an attribute of the object, stored or
    derived (`Date.Month`, `Date.Weekday`). Otherwise it is a method (`Period.Frequency()`, which can throw;
    `Schedule.StartDate()`, which throws on an empty schedule).
  - A zero-argument accessor that shares its name with an overload taking arguments stays a method, since C# can't
    overload a property with a method (`Schedule.IsRegular()` beside `IsRegular(int)`).
  - A virtual accessor is a property only if every QuantLib override passes the test (`CashFlow.Date`); otherwise it
    is a method (QuantLib's `CashFlow::amount()`, which some overrides compute, and which takes the context here).
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
- **Day counters** are equal when they are of the same type and name and the data their counts read is equal, where
  QuantLib compares names alone: `Business252` compares its calendar, as calendars compare; `ActualActual` with the
  ISMA or Bond convention and a schedule compares the period dates it derives from the schedule once, at construction
  (two over schedules it can't derive from, built from dates alone, compare those dates, and never equal one that
  derives); `Thirty360` with the ISDA or German convention compares its termination date. `Equals` is virtual and
  compares the runtime type, as the calendar's does; the hash combines the same values, and neither throws.
- **Bespoke calendars** are equal when their names, weekends, added holidays on working weekdays and removed holidays
  are: a holiday added before its weekday became a weekend day stays in `AddedHolidays`, as QuantLib records it, but
  doesn't count, so a calendar equals one rebuilt from its members with the weekend first.
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
  tests/NanoQL.Tests/           xUnit v3 on Microsoft.Testing.Platform, with Microsoft's code coverage extension
                                (Microsoft.Testing.Extensions.CodeCoverage), its settings in CodeCoverage.xml
  tests/vectors/<ql path>.json  committed vectors (§7.2); tests/vectors/cxx/<ql path>.json, the C++ harness's
  tools/vectors/                the hash-pinned requirements, the generators gen/<ql path>.py and gen/cxx/<ql path>.py,
                                the shared helpers, the harness's client cxx.py and the twins' check agree.py
  tools/cxx/                    the C++ harness (§7.1): CMakeLists.txt, src/<ql path>.cpp, build.py
  tools/tables/<file>.py        the generators of QuantLib's data tables (§1), which the licensing gate reruns
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
- data tables, which are generated instead, with their notices (§1);
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
- **Committed,** so contributors without Python can run the tests. Size budget: at most 1 MiB per file; the 25 MiB
  total under `tests/vectors/` is lifted for now, and each commit reports the folder's running total instead. Cases
  are sampled, or encoded compactly when a whole domain fits the budget (every weekday holiday of a calendar from 1901
  to 2199).
- **When they change:** only when the pin or a generator changes. Regenerating reproduces the committed bytes. A
  change never re-stamps earlier vectors: a byte difference is a red to diagnose.
- **Every case sets its evaluation context:** the generator sets the evaluation date and every `Settings` flag
  explicitly and writes them into `inputs.settings`, and sets, and clears afterwards, any other QuantLib global its
  case reads, writing it there too. An unset evaluation date reads as today's date, which would make a vector depend
  on the day it was generated. A generator that changes a shared QuantLib object (a calendar's holidays) restores it.
- **The at-par switch and the fixings:** `vectors.settings(..., use_at_par_coupons=..., fixings=[(index, [(date,
  value), ...]), ...])` sets the switch before the case builds its legs, since QuantLib reads it then, adds the
  fixings through each index, so QuantLib's checks run, and writes `"useAtParCoupons"` and `"fixings"`
  (`{name: [[serial, value], ...]}`, each name as QuantLib forms it) into `inputs.settings`; a key not given is not
  written. `vectors.case_settings(...)`, a context manager, restores the switch and clears the stored fixings after
  the case; the harness's client has the same pair, its fixings stored through an index of each name that accepts
  every date. `VectorCase.Context()` reads both keys, storing each history under its name through
  `VectorFixingIndex`, so the case's own index of that name reads it. The wheel inserts null entries on some reads
  (`hasHistoricalFixing` or `pastFixing` on a missing date), so a generator never reads a series after such a read,
  and a refused batch records only its throw, since the store after it differs by design.
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
| Build | `dotnet build NanoQL.slnx -c Release`, incremental while fold-ins land; with `--no-incremental` for the commit's gates | 0 errors, 0 warnings, all projects built. The AOT and trim analyzers run through `IsAotCompatible`; SA1402 and SA1649 run as errors. |
| Tests | `dotnet test --solution NanoQL.slnx -c Release`, plain, with `NANOQL_BITID_DIR` naming an emptied folder for the vectors' bit-identical tallies; never under coverage, whose instrumentation moves the last bits of some results | passed = total, failed 0, skipped 0. |
| Vectors | part of the tests, counted per vector file by `tools/vectors/count.py`; then `.venv\Scripts\python -I tools/vectors/agree.py` | every compared output within tolerance, counting outputs, not cases; with the number of overridden outputs. Every twin on `agree.py`'s list agrees with its wheel file: the same case names and inputs, every output within tolerance, and kinds and scales exactly; it prints the outputs agreeing M/M and how many are bit-identical. The bit-identical reading per vector file comes from the tests' run: `VectorAssert` writes each file's real outputs and those equal to the vector's bit for bit into `NANOQL_BITID_DIR`. |
| Licensing | `.venv\Scripts\reuse lint`, then `.venv\Scripts\python -I tools/check-upstream.py <QuantLib v1.44 worktree> --boost <Boost 1.90.0 root>` | (1) `reuse lint` reports the project compliant. (2) Every `.cs` file under `src/NanoQL/` and `tests/NanoQL.Tests/TestSuite/` carries an Upstream line whose paths exist at the pin, in the folder and namespace the name table gives, or is listed in `tools/no-upstream.txt`; so is every other `.cs`, `.py`, `.cpp` and `.hpp` file; every test file's namespace names its folder; and every `// Replaced:`, `// Moved:` and `// Not ported:` line holds (§1). (3) Every `Copyright (C)` line of the named upstream files appears verbatim, read as §1 says; every third-party block `tools/third-party-notices.txt` lists for them appears whole, every line in order, in the file's header and in `THIRD_PARTY_NOTICES.md`, read from the row's source, with its licence in the SPDX expression; a Boost port's header holds as §1 says. (4) The files with a `// Partial:` line are exactly those `porting-plan.md` lists. (5) Regenerating `porting-map.md` reproduces the committed file; every file the name table marks replaced or not ported exists at the pin; and no Upstream line names a not-ported file. (6) Regenerating the tables (§1) with each script under `tools/tables/` into a temporary folder writes bytes identical to the committed files, and every `.bin` file under `src/NanoQL/` is one they write. (7) Regenerating `divergences.md` from the `Divergences/` tests with `tools/divergences.py` reproduces the committed page byte for byte: one entry per test class, with its file, its summary and its facts' names; a difference names the first entry that differs, or the missing page. **The Boost root** is Boost 1.90.0's release tree: `tools/cxx/build.py`'s `boost_1_90_0` (§7.1), or `boost_1_90_0.zip` from `https://archives.boost.io/release/1.90.0/source/`, checked against its SHA-256, `bdc79f179d1a4a60c10fe764172946d0eeafad65e576a8703c4d89d49949973c`, and extracted with Python's `zipfile` (Windows' `tar` skips one file). `--boost` is required whenever an Upstream line names a Boost path. `reuse lint` also lints the ignored build output under a folder none of whose files is in git's index yet, a false red: in a change that adds such a folder, run it on a tree without build output (`git clean -fdX -- <new folders>`) or after `git add -N <new folders>`. |
| Reproducibility | `.venv\Scripts\python -I tools/vectors/reproduce.py [--jobs <n>] [<generators>]`, the generators run n at a time (the processor count by default), a folder named by its path ending in `/` (`cxx/`); for harness generators, after `tools/cxx/build.py`, with `NANOQL_HARNESS` set and `tools/vectors/cxx.py --self-test` passing | rerunning each generator the commit adds or changes, or all of them when the shared helpers or the requirements change, writes bytes identical to the committed files, compared outside git. Harness generators rerun when the commit adds or changes them; all of them when the harness's source, its client, the shared helpers or the requirements change; and one also when a wheel generator it imports changes. |
| Coverage | `dotnet test --solution NanoQL.slnx -c Release --filter-trait "Oracle=QuantLib" --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml --coverage-settings tests\NanoQL.Tests\CodeCoverage.xml --results-directory TestResults\G7`, the commit's tagged classes (`--filter-class`); at a slot's last commit also the rest of the suite's run, the same with `--filter-not-trait "Oracle=QuantLib"` (or `--filter-not-class` for each of the commit's classes) into `TestResults\G7rest`, the two runs' counts adding up to the suite's; each from the repository root into an emptied folder; then `.venv\Scripts\python -I tools/check-coverage.py TestResults\G7`, at the last commit with `--all TestResults\G7rest --since <the slot's base commit>` | Every public type the commit ports that has executable lines has covered lines from the tests marked `Oracle=QuantLib` (§9.5). Types the report doesn't list (enums, interfaces, delegates, abstract types with no bodies, types holding only fields or constants) are listed apart by name and count, and so are types whose only code is equality nanoql adds. The line coverage of the commit's ported files is reported, with no threshold. At a slot's last commit it also prints, without failing, the public members of the files the slot ports that no test of any kind reaches, reading the two runs' reports merged (§9.5); a missing or second report is a red. A constructor the source doesn't declare (field and property initialisers, a primary constructor's parameters) counts as no code. |

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

**Refusals of undefined paths** (§2) have no QuantLib oracle, since QuantLib's behaviour there is undefined. Each has a
`Divergences/` test naming QuantLib's lines, and a `Conventions/` test asserting its exception's type, message and
`ParamName`, one test per group of refusals.

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
