<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Porting plan

How the port of QuantLib v1.44 proceeds. The rules each port follows are in
[porting-playbook.md](porting-playbook.md), and every QuantLib file's status is in [porting-map.md](porting-map.md).
Every documented divergence from QuantLib, with the tests that assert it, is in [divergences.md](divergences.md).

## All of QuantLib, in batches

At the tag, QuantLib holds 1,456 units (an `.hpp`/`.cpp` pair under `ql/`) and 193,721 code lines. nanoql ports 1,264
units and 188,546 code lines, 35,011 of them experimental. The rest is deprecated, legacy, include-only, C++ platform
code, or plumbing that nanoql's design replaces (playbook §6).

The port proceeds bottom-up: a slot comes after the slots whose types it uses. Each unit belongs to the first slot
whose content reaches it. "Oracle" is the share of the batch's declared classes that QuantLib-Python exposes by name;
the rest are checked through QuantLib's own test cases, the classes that use them, or the C++ harness (playbook §7.1).

| Batch | Content | Units | Code lines | Share | Oracle |
|---:|---|---:|---:|---:|---:|
| 1 | Rates, bonds, inflation-linked bonds, equity, FX and credit default swaps: the first milestone | 379 | 65,232 | 35% | 72% |
| 2 | Conventions breadth | 106 | 8,541 | 5% | 93% |
| 3 | Rates and bonds beyond vanilla | 137 | 25,032 | 13% | 49% |
| 4 | Equity and FX beyond vanilla | 227 | 34,535 | 18% | 61% |
| 5 | Market models | 118 | 15,618 | 8% | 12% |
| 6 | Core remainder | 103 | 9,601 | 5% | 37% |
| 7 | Experimental remainder | 194 | 29,987 | 16% | 10% |

- **v0.1** is batches 1–6, all of QuantLib's non-experimental code: 1,070 units and 158,559 code lines. Batch 7 joins
  it if it is ready, marked `[Experimental]`.
- **QuantLib's test suite:** 182 test files to port, 98,709 code lines. Most test files span batches, so each slot
  ports the test cases for its own units.
- **Frameworks that keep state between steps** settle how their state fits nanoql's stateless design in the slot that
  first ports them: random numbers and Monte Carlo paths (S2), lattices (S-bond), finite differences (S-eq), and
  market models (batch 5).

## Batch 1

In this order:

| Slot | Content |
|---|---|
| Pilot | The repository's conventions, the vector harness and the licensing checks; time (`Date`, `Period`, the TARGET, United States, United Kingdom, joint, weekends-only and null calendars, the Actual/360, Actual/365 (Fixed), 30/360 and Actual/Actual day counters, `Schedule` and `MakeSchedule`); and the pattern slice: `EvaluationContext`, `InterestRate`, `TermStructure`, `YieldTermStructure`, `FlatForward`, cash flows and the fixed-rate leg, part of `CashFlows`, `Instrument`, `Swap` and `DiscountingSwapEngine`, with an expired swap among the vectors. |
| S-cxx | The C++ harness. It completes the two partial ports that waited for it, the weekday's text and the business-day convention's, and ports no other QuantLib units. It builds QuantLib 1.44 from source against Boost 1.90.0's headers, sets up the Boost checkout the licensing gate reads for Boost ports, and writes the harness's first vectors: a pilot class the wheel also covers, matched against the wheel, then the pilot's members that only nanoql's own tests check, whose own tests the vectors replace. |
| S2 | Maths: interpolations, including the linear and log-linear ones and the cubic family; 1-D solvers; optimisers, with MINPACK's licence; distributions, including Boost.Math ports; MT19937, Halton and Sobol, with generated tables and their notices. It settles the state of random numbers and Monte Carlo paths, and how stateful template parameters are ported. 41 units, 6,807 code lines. |
| S3 | Indexes, coupons and swaps: `EvaluationContext.Fixings`; the Ibor and overnight indexes (SOFR, €STR, SONIA, Euribor, TONA, Tibor, CORRA), with the Japan and Canada calendars; Ibor and overnight coupons (compounded and averaged, lookback, lockout, observation shift) and the coupon pricers they need, with the constant optionlet volatility and two of the Black formulas; `VanillaSwap` and `FixedVsFloatingSwap`, `OvernightIndexedSwap`, `MakeVanillaSwap`, `MakeOIS`, and `DiscountingSwapEngine` extended with their results derivations; and `Duration`. It adds `context.UseAtParCoupons`. 43 units, 8,243 code lines. |
| S4 | Merged into S3. |
| S5 | Bootstrapping and swaptions: the rate helpers (deposit, FRA, futures with ASX dates, swap, OIS, BMA swap); `PiecewiseYieldCurve` with its iterative and global bootstraps, and the multicurve bootstrap; the interpolated discount, zero, simple-zero and forward curves; `SwapIndex` and `BMAIndex`; `Exercise` and the payoffs; `Swaption` and `MakeSwaption`, the Black and Bachelier swaption engines, and the rest of the Black formulas; the swaption volatility base, with the constant, discrete and matrix structures; `SmileSection` and `FlatSmileSection`; `Interpolation2D`, bilinear interpolation and flat 2-D extrapolation. 39 units, 7,596 code lines. |
| S6 | Merged into S5. |
| S-bond | Bonds: `Bond`; fixed-rate, floating-rate, zero-coupon and amortizing bonds; `DiscountingBondEngine` and `BondFunctions`; bond helpers; callable and puttable bonds, with OAS, the Hull-White tree engine and the Black engine; Hull-White calibrated to swaptions, with the Jamshidian and tree swaption engines; and the zero-spreaded curve. It settles the state of lattices. 37 units, 5,511 code lines, 6 of them experimental. |
| S7 | SABR, caps and floors: `sabr.cpp` (including Boost's `cubic_roots`), `SabrSmileSection`, `SABRInterpolation`, the swaption volatility cubes; `CapFloor` and `MakeCapFloor`, the Black and Bachelier cap/floor engines, `CapFloorTermVolSurface`, `CapFloorTermVolCurve` and the constant cap/floor term volatility, optionlet stripping; ZABR, with its smile section, interpolation and swaption volatility cube; and the spreaded smile section and the spreaded optionlet and swaption volatilities. 33 units, 6,691 code lines. |
| S8 | Merged into S7. |
| S-infl | Inflation-linked bonds and credit default swaps: `CPIBond` and `CPIBondHelper`; CPI coupons and their pricer; the inflation index, term-structure and seasonality bases; UK RPI, US CPI and EU HICP; the zero-inflation curve and its helpers, with the zero-coupon inflation swap; `CreditDefaultSwap` and `MakeCreditDefaultSwap`; the ISDA standard-model and mid-point engines; default-probability helpers and the piecewise default curve. 42 units, 7,575 code lines. |
| S-cds | Merged into S-infl. |
| S-eq | Equity: European and American vanilla options; the Black-Scholes processes; the analytic European, analytic Heston and finite-difference Black-Scholes engines; Heston calibration; variance swaps by replication; constant, surface and local volatility; the finite-difference core. It settles the state of finite differences. 81 units, 9,567 code lines. |
| S-rx | FRA, basis and cross-currency swaps, and FX: `ForwardRateAgreement`; `FloatFloatSwap` and the basis-swap rate helpers; the constant-notional and mark-to-market cross-currency swaps, their engines and rate helpers; exchange rates and `Money`, with `context.ExchangeRates` and `context.MoneySettings`; `FxForward` and its engine; barrier options with the analytic, finite-difference and Vanna-Volga engines; SVI smiles; the Black delta calculator and delta-volatility quotes. 34 units, 7,190 code lines, 11 of them experimental. |
| S-fx | Merged into S-rx. |
| S-bind | Bindings, at the end of batch 1: a language-neutral C interface over Native AOT's exports, with a generated C header, opaque handles released by the caller, and error codes with a last-error message; then a generated Python wrapper over that header, and one Windows x64 wheel that builds a curve and prices a swap from Python. It ports no QuantLib units. Its header and wheel are rebuilt and tested at milestones, the preview and the release, rather than in each slot. |
| S-xl | The Excel add-in, after S-bind: Excel-DNA on `net10.0-windows`; curve and swap functions from Excel; immutable objects behind handles keyed by their inputs; export of a cell's call in the vector format; debugging by stepping from Excel into nanoql's source. It ports no QuantLib units. Its functions are rebuilt and tested at milestones, as the bindings are. |

## Later batches

| Batch | Content |
|---:|---|
| 2 | Conventions breadth: the remaining calendars, day counters and currencies; the Ibor, overnight, swap and inflation indexes. |
| S-conv | Conventions as data: XML round-tripping of convention values, a resolver, and QuantLib's presets as generated XML records checked against the coded ones. It ports no QuantLib units, and starts the extensions layer. |
| S-qlnet | QLNet's additions: mortgage-backed securities with prepayment, and accounting amortised cost, checked against QLNet with QLNet's notices kept. |
| 3 | Rates and bonds beyond vanilla: short-rate models with their tree, finite-difference and Gaussian1d engines; Bermudan swaptions; CMS, digital and range-accrual coupons; inflation caps, floors and year-on-year swaps; the remaining yield-curve and volatility structures. |
| 4 | Equity and FX beyond vanilla: Asian, barrier, basket, cliquet, lookback, compound, chooser and quanto options; the remaining vanilla engines; Bates and the other equity models. |
| 5 | The LIBOR market-model framework. |
| 6 | Core remainder: what no product above reaches, mostly the rest of the finite-difference framework, optimisers, random-number generators, interpolations, matrix utilities, quotes and copulas. |
| 7 | Experimental remainder: credit portfolios and CDOs, experimental maths, commodities, volatility models, barrier engines, finite differences and inflation. Marked `[Experimental]`. |

After batch 6 (and batch 7 if it is ready): the documentation, with a validation report per module, and the v0.1
release. From the end of batch 1, private preview builds carry a pre-release version, `0.1.0-preview.<n>`.

## Partial ports

Files that port only part of their QuantLib file, each with a `// Partial:` line naming what is left (playbook §1).
The licensing gate checks that this list and the files match.

- `src/NanoQL/Coupons/CappedFlooredCoupon.cs`: `CappedFlooredCmsCoupon`, which waits for S-rx with `CmsCoupon`.
- `src/NanoQL/Coupons/CashFlowVectorsFunctions.cs`: `FloatingDigitalLeg`, which waits for B3.4 with the digital coupons.
- `src/NanoQL/Coupons/CouponPricer/CouponPricerFunctions.cs`: the visitor's cases for the CMS, CMS-spread, digital, range-accrual and multiple-resets coupons (the capped coupon's CMS and CMS-spread checks among them), which wait for S-rx and batch 3; the CMS pricer bases, which wait for B3.3.
- `src/NanoQL/Coupons/FixedRateLeg.cs`: `withLastPeriodDayCounter`, which waits for the credit default swap slot.
- `src/NanoQL/Coupons/YoYInflationLeg.cs`: `withCaps` and `withFloors`, which build capped and floored coupons and wait for B3.4's `CappedFlooredYoYInflationCoupon`.
- `src/NanoQL/Experimental/CallableBonds/CallableBondVolatilityStructure.cs`: the constructor for a structure managing its own reference date, `(dc, bdc)`, which no QuantLib 1.44 class uses and which waits for its first user.
- `src/NanoQL/Models/ShortRate/OneFactorModel/OneFactorModelShortRateTree.cs`: the constructor fitting the tree to a term structure numerically, with its solver helper, which waits for B3.1's `ExtendedCoxIngersollRoss`, its only user.
- `src/NanoQL/OptionType.cs`: `Greeks` and `MoreGreeks`, which wait for S-eq's engines.
- `src/NanoQL/StochasticProcess/StochasticProcess.cs`: `StochasticProcess`'s defaults of `expectation`, `stdDeviation` and `covariance`, which call the discretization and wait for S-eq's `EulerDiscretization`.
- `src/NanoQL/StochasticProcess/StochasticProcess1D.cs`: `StochasticProcess1D`'s defaults of `expectation`, `stdDeviation` and `variance`, which call the discretization and wait for S-eq's `EulerDiscretization`.
- `src/NanoQL/TermStructure.cs`: the constructors taking a day counter only.
- `src/NanoQL/TermStructures/GlobalBootstrap/GlobalBootstrap.cs`: `AdditionalBootstrapVariables`, the variables a global bootstrap adds to its optimization (a quote the optimizer writes and a helper reads), which waits for B6.2 with `SimpleQuoteVariables` and its user.
- `src/NanoQL/TermStructures/Volatility/CapFloor/CapFloorTermVolatilityStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: no QuantLib class calls it.
- `src/NanoQL/TermStructures/Volatility/Optionlet/OptionletStripper1.cs`: `capletVols()`, a matrix QuantLib allocates and never writes, not ported.
- `src/NanoQL/TermStructures/Volatility/Optionlet/OptionletVolatilityStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: its only QuantLib user, the spreaded optionlet volatility, passes its base's reference date to the reference-date constructor.
- `src/NanoQL/TermStructures/Volatility/Swaption/SabrSwaptionVolatilityCube/XabrSwaptionVolatilityCube{TModel}.cs`: the recalibration members (`recalibration`, `updateAfterRecalibration` and `sabrCalibrationSection`), which change a calibrated cube and wait for B3.3's `CmsMarketCalibration`, their only user.
- `src/NanoQL/TermStructures/Volatility/Swaption/SwaptionVolatilityStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: its only QuantLib user, the spreaded swaption volatility, passes its base's reference date to the reference-date constructor.
- `src/NanoQL/TermStructures/Volatility/ZabrModel.cs`: `fdPrice` (both forms) and `fullFdPrice`, the local-volatility and full finite-difference prices, which wait for B3.3 with the finite-difference units they price on (S-eq's meshers, layout and backward solver, S-rx's Dirichlet boundary, B3.3's glued mesher and its Dupire and ZABR operators).
- `src/NanoQL/TermStructures/Volatility/ZabrSmileSection/ZabrSmileSection{TEvaluation}.cs`: the local-volatility and full finite-difference kernels (the `ZabrLocalVolatility` and `ZabrFullFd` tags, with their strike grid, call prices and price spline), which wait for B3.3 with `ZabrModel`'s `fdPrice` and `fullFdPrice`.
- `src/NanoQL/TermStructures/VolatilityTermStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, which waits for its remaining users, S-eq's Black volatility structures.
- `src/NanoQL/TermStructures/Yield/ZeroYieldStructure.cs`: the constructor taking a day counter only.
- `src/NanoQL/TermStructures/YieldTermStructure.cs`: the constructors taking a day counter only.
- `src/NanoQL/Time/Date/Date.cs`: `io::formatted_date`, not ported.
- `src/NanoQL/Time/Date/DateFunctions.cs`: `io::formatted_date`, not ported.
- `tests/NanoQL.Tests/TestSuite/AmortizingBondTests.cs`: `testBrazilianAmortizingFixedRateBond`, which waits for batch 2's Brazil calendar and `Business252` day counter.
- `tests/NanoQL.Tests/TestSuite/BermudanSwaptionTests.cs`: `testCachedValues`' finite-difference checks, which wait for B3.2's `FdHullWhiteSwaptionEngine`; `testCachedG2Values`, which waits for B3.1's `G2` and B3.2's `FdG2SwaptionEngine`; `testTreeEngineTimeSnapping`, which waits for B3.2's `FdHullWhiteSwaptionEngine`; `testBermudanOISSwaptionWithHW` and `testBermudanOISSwaptionPreservesFeatures`, which wait for batch 2's `Eonia` and B3.2's `FdHullWhiteSwaptionEngine`; and `testBermudanOISSwaptionWithG2`, which waits for batch 2's `Eonia`, B3.1's `G2` and B3.2's `FdG2SwaptionEngine`.
- `tests/NanoQL.Tests/TestSuite/BondsTests.cs`: `testCachedFloating`, which waits for batch 2's `USDLibor`; `testBrazilianCached`, which waits for batch 2's Brazil calendar and `Business252` day counter; and `testRiskyBondWithGivenDates`, which waits for B3.5's `RiskyBondEngine` and S-infl's `FlatHazardRate`.
- `tests/NanoQL.Tests/TestSuite/BrownianBridgeTests.cs`: `testPathGeneration`, which waits for S-eq's Black-Scholes process.
- `tests/NanoQL.Tests/TestSuite/CPISwapTests.cs`: `consistency` and `cpibondconsistency`, which wait for batch 2's `GBPLibor`.
- `tests/NanoQL.Tests/TestSuite/CalendarsTests.cs`: the cases for calendars not yet ported.
- `tests/NanoQL.Tests/TestSuite/CashFlowsTests.cs`: the cases needing batch 2's USDLibor, NewZealand calendar and Bkbm indexes.
- `tests/NanoQL.Tests/TestSuite/DatesTests.cs`: the ECB, parsing and intraday cases.
- `tests/NanoQL.Tests/TestSuite/DayCountersTests.cs`: the cases for day counters not yet ported.
- `tests/NanoQL.Tests/TestSuite/DistributionsTests.cs`: the bivariate normal, binomial, Student t, chi-square, stochastic-collocation and Sankaran cases, whose distributions aren't ported yet.
- `tests/NanoQL.Tests/TestSuite/FunctionsTests.cs`: the modified Bessel, expm1 and log1p cases, whose functions aren't ported yet.
- `tests/NanoQL.Tests/TestSuite/IndexesTests.cs`: the custom-index, CDI and SHIR cases, which wait for batch 2's indexes and calendars.
- `tests/NanoQL.Tests/TestSuite/InflationTests.cs`: `testZeroIndex`'s UK HICP and quarterly AU CPI checks, which wait for batch 2's `UKHICP` and `AUCPI`.
- `tests/NanoQL.Tests/TestSuite/InterpolationsTests.cs`: the cases needing interpolations, integrals or extrapolators not yet ported (Simpson, Lagrange, Chebyshev, multi-dimensional splines, forward-flat, no-arbitrage SABR and the SABR transformations, kernel, Richardson, Laplace, B-splines, flat extrapolation).
- `tests/NanoQL.Tests/TestSuite/LowDiscrepancySequencesTests.cs`: the randomized, lattice, Faure and Burley 2020 cases and the discrepancy cases, whose generators and statistics aren't ported yet.
- `tests/NanoQL.Tests/TestSuite/MatricesTests.cs`: the cases needing matrix utilities not yet ported (decompositions, square roots, iterative solvers, sparse matrices, projections, Moore-Penrose).
- `tests/NanoQL.Tests/TestSuite/OdeTests.cs`: `testMatrixExponential` and `testMatrixExponentialOfZero`, which wait for B6.2's `Expm`.
- `tests/NanoQL.Tests/TestSuite/OptimizersTests.cs`: the hybrid simulated annealing, L-BFGS-B, differential evolution and CMA-ES cases, whose optimizers aren't ported yet.
- `tests/NanoQL.Tests/TestSuite/OvernightIndexedSwapTests.cs`: `testBootstrapRegression`, which waits for batch 2's `FedFunds`; `testMakeOisEndOfMonthRegression2453`, which waits for batch 2's `Aonia`; `testNonstandardSwapConversionPreservesObservationConventions`, which waits for B3.2's `NonstandardSwap`; and `testMakeOISDefaultSettlementDays`' lines for the indexes of batch 2 (EONIA, FedFunds, AONIA, SARON, NZOCR, DESTR, SWESTR, KOFR).
- `tests/NanoQL.Tests/TestSuite/PathGeneratorTests.cs`: `testMultiPathGenerator`, which waits for `MultiPathGenerator` (B3.2).
- `tests/NanoQL.Tests/TestSuite/PiecewiseYieldCurveTests.cs`: the consistency cases' FRA and futures checks (S-rx) and `testBMACurveConsistency` (batch 2's USDLibor); `testDefaultInstantiation`'s ForwardFlat (S-infl) and ConvexMonotone (B6.2) curves; `testConstructionWithExplicitBootstrap`'s LocalBootstrap curve (B6.2); `testHelperDatesFromNonBusinessEvaluationDate`'s BMA helper check (batch 2's USDLibor); and the cases that need B6.2's, S-rx's, B3.5's and batch 2's units.
- `tests/NanoQL.Tests/TestSuite/RngTraitsTests.cs`: `testRanLux`, whose generator isn't ported yet.
- `tests/NanoQL.Tests/TestSuite/ScheduleTests.cs`: the CDS cases, which need `cdsMaturity`.
- `tests/NanoQL.Tests/TestSuite/ShortRateModelsTests.cs`: `testSwaps`, which waits for B3.4's `TreeVanillaSwapEngine`; and the two extended Cox-Ingersoll-Ross cases, which wait for B3.1's `ExtendedCoxIngersollRoss`.
- `tests/NanoQL.Tests/TestSuite/SolversTests.cs`: the bisection, false-position, secant, Newton, Ridder and Halley cases, whose solvers aren't ported yet.
- `tests/NanoQL.Tests/TestSuite/SwapTests.cs`: `testInArrears`, which waits for batch 2's `SimpleDayCounter`; `testFixedTenorInferenceWithTerminationDate`, which waits for batch 2's `GBPLibor` and `Bbsw`; and `testSpotDateUsesFixingCalendar`, which waits for batch 2's Taiwan calendar.
- `tests/NanoQL.Tests/TestSuite/SwaptionTests.cs`: `testImpliedVolatilityOis` and `testCachedValue`'s overnight-indexed swaption (lines 449-466), which wait for batch 2's `Eonia`.
- `tests/NanoQL.Tests/TestSuite/TermStructuresTests.cs`: the cases needing curves not yet ported.
- `tests/NanoQL.Tests/TestSuite/TimeSeriesTests.cs`: `testIntervalPrice`, which waits for `IntervalPrice` (`ql/prices`, B4.2).
- `tests/NanoQL.Tests/TestSuite/ZabrTests.cs`: `testConsistency`'s local-volatility and full finite-difference sections and checks, which wait for B3.3 with `ZabrModel`'s `fdPrice` and `fullFdPrice`.
