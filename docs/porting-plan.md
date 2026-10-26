<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Porting plan

How the port of QuantLib v1.44 proceeds. The rules each port follows are in
[porting-playbook.md](porting-playbook.md), and every QuantLib file's status is in [porting-map.md](porting-map.md).
Every documented divergence from QuantLib, with the tests that assert it, is in [divergences.md](divergences.md).

## All of QuantLib, in batches

At the tag, QuantLib holds 1,456 units (an `.hpp`/`.cpp` pair under `ql/`) and 193,721 code lines. nanoql ports 1,261
units and 188,425 code lines, 35,011 of them experimental. The rest is deprecated, legacy, include-only, C++ platform
code, or plumbing that nanoql's design replaces (playbook §6).

The port proceeds bottom-up: a slot comes after the slots whose types it uses. Each unit belongs to the first slot
whose content reaches it. "Oracle" is the share of the batch's declared classes that QuantLib-Python exposes by name;
the rest are checked through QuantLib's own test cases, the classes that use them, or the C++ harness (playbook §7.1).

| Batch | Content | Units | Code lines | Share | Oracle |
|---:|---|---:|---:|---:|---:|
| 1 | Rates, bonds, inflation-linked bonds, equity, FX and credit default swaps: the first milestone | 413 | 69,083 | 37% | 72% |
| 2 | Conventions breadth | 96 | 7,906 | 4% | 95% |
| 3 | Rates and bonds beyond vanilla | 133 | 24,025 | 13% | 52% |
| 4 | Equity and FX beyond vanilla | 226 | 34,420 | 18% | 60% |
| 5 | Market models | 120 | 15,764 | 8% | 12% |
| 6 | Core remainder | 79 | 7,240 | 4% | 31% |
| 7 | Experimental remainder | 194 | 29,987 | 16% | 10% |

- **v0.1** is batches 1–6, all of QuantLib's non-experimental code: 1,067 units and 158,438 code lines. Batch 7 joins
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

## Later batches

In this order, each slot a family of units with the test cases that complete in it; a slot's own plan may pull or
cut units:

| Slot | Batch | Content | Units | Code lines |
|---|---:|---|---:|---:|
| S-cal | 2 | Calendars, day counters, currencies and indexes | 96 | 7,906 |
| S-sr | 3 | Short-rate models with their tree, analytic, finite-difference and Monte Carlo engines; convertible bonds; curves beyond vanilla | 66 | 7,739 |
| S-g1d | 3 | Gaussian1d models and engines, nonstandard and float-float swaptions; smile sections and volatility structures | 34 | 8,084 |
| S-cms | 3 | CMS pricers and the CMS market; digital, range-accrual and multiple-resets coupons; swaps, bonds and futures beyond vanilla | 33 | 8,202 |
| S-van | 4 | Vanilla engines: American approximations, finite differences and Monte Carlo; local volatility and volatility surfaces | 63 | 9,367 |
| S-hes | 4 | Heston's extensions: Bates, time-dependent, rough, stochastic-local and Heston-Hull-White; GJR-GARCH | 47 | 8,287 |
| S-exo | 4 | Basket, spread and other exotic options | 63 | 8,221 |
| S-bar | 4 | Barrier, lookback and Asian options; volatility estimators | 53 | 8,545 |
| S-mm | 5 | Market models: curve states, drifts, correlations, Brownian generators, models, evolvers and calibrations | 55 | 7,793 |
| S-mmp | 5 | Market models: products, callability, accounting engines and pathwise Greeks | 65 | 7,971 |
| S-core | 6 | The core remainder: optimisers, random numbers, copulas, statistics, quotes, integrals, interpolations, matrix utilities and the finite-difference remainder | 79 | 7,240 |
| S-conv | — | Conventions as data: XML round-tripping of convention values, a resolver, and QuantLib's presets as generated XML records checked against the coded ones. It ports no QuantLib units, and starts the extensions layer. | — | — |
| S-qlnet | — | QLNet's additions: mortgage-backed securities with prepayment, and accounting amortised cost, checked against QLNet with QLNet's notices kept. | — | — |
| S-xcr | 7 | Experimental credit: latent and loss models, baskets, CDOs, n-th to default, CDS options; cat bonds | 55 | 9,685 |
| S-xeq | 7 | Experimental equity, FX and energy: double barriers, exotics, variance gamma, path engines; volatility surfaces; energy finite differences | 72 | 10,107 |
| S-xrt | 7 | Experimental rates, inflation, commodities and maths | 67 | 10,195 |

Batch 7's slots are marked `[Experimental]`. If batch 7 is ready for v0.1 it runs after batch 6 and before S-conv;
otherwise after v0.1's release.

After batch 6 (and batch 7 if it is ready), the release steps, which port nothing. Nothing ships before v0.1, the first
public release.

| Step | Content |
|---|---|
| S-bind | Bindings: a language-neutral C interface over Native AOT's exports, with a generated C header, opaque handles released by the caller, and error codes with a last-error message; then wrappers generated over that header, each with its package and running the vector suite. v0.1 ships four bindings: Python, the C header, C++, and Java through Java's Foreign Function & Memory API (JDK 22 or later, no JNI). It builds the bindings once, over v0.1's whole API, after S-conv. |
| S-xl | The Excel add-in, after S-bind: Excel-DNA on `net10.0-windows`; curve and swap functions from Excel; immutable objects behind handles keyed by their inputs; export of a cell's call in the vector format; debugging by stepping from Excel into nanoql's source. |
| Release | The documentation, with a validation report per module, and the v0.1 release. |

## Partial ports

Files that port only part of their QuantLib file, each with a `// Partial:` line naming what is left (playbook §1).
The licensing gate checks that this list and the files match.

- `src/NanoQL/Experimental/CallableBonds/CallableBondVolatilityStructure.cs`: the constructor for a structure managing its own reference date, `(dc, bdc)`, which no QuantLib 1.44 class uses and which waits for its first user.
- `src/NanoQL/Methods/FiniteDifferences/StepCondition.cs`: `NullCondition`, which no QuantLib 1.44 class or test uses and which waits for its first user.
- `src/NanoQL/Models/MarketModels/BrownianGenerators/SobolBrownianGenerator/SobolBrownianGenerator.cs`: `Burley2020SobolBrownianGenerator` and `Burley2020SobolBrownianGeneratorFactory`, which wait for S-mm with `Burley2020SobolRsg`.
- `src/NanoQL/Numerics/Integrals/FourierIntegration.cs`: the exp-sinh algorithm, which waits for S-core's exp-sinh integrator.
- `src/NanoQL/TermStructure.cs`: the constructors taking a day counter only.
- `src/NanoQL/TermStructures/Credit/DefaultDensityStructure.cs`: `survivalProbabilityImpl`'s default, a Gauss-Chebyshev integral of the default density, which waits for S-xcr's curves, its only users.
- `src/NanoQL/TermStructures/Credit/HazardRateStructure.cs`: `survivalProbabilityImpl`'s default, a Gauss-Chebyshev integral of the hazard rate, which waits for S-xcr's curves, its only users.
- `src/NanoQL/TermStructures/Credit/PiecewiseDefaultCurve/PiecewiseDefaultCurve{TTraits,TInterpolator,TBootstrap}.cs`: the constructor on a one-factor affine model, which waits for S-xcr's `AffineHazardRate` traits, the only traits whose curve takes a model.
- `src/NanoQL/TermStructures/GlobalBootstrap/GlobalBootstrap.cs`: `AdditionalBootstrapVariables`, the variables a global bootstrap adds to its optimization (a quote the optimizer writes and a helper reads), which waits for S-core with `SimpleQuoteVariables` and its user.
- `src/NanoQL/TermStructures/Volatility/CapFloor/CapFloorTermVolatilityStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: no QuantLib class calls it.
- `src/NanoQL/TermStructures/Volatility/EquityFX/BlackVolTermStructure/BlackVarianceTermStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: its QuantLib users follow another structure, and pass its reference date to the reference-date constructor instead.
- `src/NanoQL/TermStructures/Volatility/EquityFX/BlackVolTermStructure/BlackVolTermStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: its QuantLib users follow another structure, and pass its reference date to the reference-date constructor instead.
- `src/NanoQL/TermStructures/Volatility/EquityFX/BlackVolTermStructure/BlackVolatilityTermStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: its QuantLib users follow another structure, and pass its reference date to the reference-date constructor instead.
- `src/NanoQL/TermStructures/Volatility/EquityFX/LocalVolTermStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: its QuantLib users follow another structure, and pass its reference date to the reference-date constructor instead, as `LocalVolCurve` and `LocalVolSurface` do.
- `src/NanoQL/TermStructures/Volatility/Optionlet/OptionletStripper1.cs`: `capletVols()`, a matrix QuantLib allocates and never writes, not ported.
- `src/NanoQL/TermStructures/Volatility/Optionlet/OptionletVolatilityStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: its only QuantLib user, the spreaded optionlet volatility, passes its base's reference date to the reference-date constructor.
- `src/NanoQL/TermStructures/Volatility/Swaption/SwaptionVolatilityStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: its only QuantLib user, the spreaded swaption volatility, passes its base's reference date to the reference-date constructor.
- `src/NanoQL/TermStructures/VolatilityTermStructure.cs`: the constructor for a structure managing its own reference date, `(bdc, dc)`, not ported: its QuantLib users follow another structure, and pass its reference date to the reference-date constructor instead, as the local volatility structures do.
- `src/NanoQL/TermStructures/Yield/ZeroYieldStructure.cs`: the constructor taking a day counter only.
- `src/NanoQL/TermStructures/YieldTermStructure.cs`: the constructors taking a day counter only.
- `src/NanoQL/Time/Date/Date.cs`: `io::formatted_date`, not ported.
- `src/NanoQL/Time/Date/DateFunctions.cs`: `io::formatted_date`, not ported.
- `src/NanoQL/Time/DayCounters/YearFractionToDateFunctions.cs`: `roundToDayStart`, which waits for S-exo's spread option tests, its only callers.
- `src/NanoQL/Utilities/DataParsers/DateParser.cs`: `parseFormatted`, not ported: a `DateOnly`'s `ParseExact` does its job.
- `tests/NanoQL.Tests/TestSuite/AndreasenHugeVolatilityInterplTests.cs`: `testDifferentOptimizers`, which waits for S-core's BFGS.
- `tests/NanoQL.Tests/TestSuite/BarrierOptionTests.cs`: `testHaugValues`' binomial checks, which wait for S-bar's `BinomialBarrierEngine`; `testBabsiriValues`' and `testBeagleholeValues`' Monte Carlo checks, which wait for S-bar's `MCBarrierEngine`; `testDividendBarrierOption`'s and `testDividendBarrierOptionWithDividendsPastMaturity`'s Heston checks, which wait for batch 4's `FdHestonBarrierEngine`; `testLocalVolAndHestonComparison`, which waits for batch 4's local volatility and Heston barrier engines; and the four perturbative cases, which wait for S-xeq's `PerturbativeBarrierOptionEngine`.
- `tests/NanoQL.Tests/TestSuite/BasketOptionTests.cs`: the cases on the spread, two-asset and American Monte Carlo engines, which wait for S-exo's C2.
- `tests/NanoQL.Tests/TestSuite/BrownianBridgeTests.cs`: `testPathGeneration`, which waits for S-mmp's `SequenceStatistics`.
- `tests/NanoQL.Tests/TestSuite/CmsSpreadTests.cs`: `testCouponPricing`, which waits for S-xrt's `LognormalCmsSpreadPricer`.
- `tests/NanoQL.Tests/TestSuite/CreditDefaultSwapTests.cs`: `testCachedValue`'s integral-engine checks, which wait for S-core's `IntegralCdsEngine`.
- `tests/NanoQL.Tests/TestSuite/DistributionsTests.cs`: the bivariate normal, Student t and stochastic-collocation cases, whose distributions aren't ported yet.
- `tests/NanoQL.Tests/TestSuite/DividendOptionTests.cs`: the cash-dividend engine's cases, which wait for S-exo's `CashDividendEuropeanEngine`.
- `tests/NanoQL.Tests/TestSuite/EuropeanOptionTests.cs`: `testFFTEngines`, which waits for S-xeq's `FFTVanillaEngine`.
- `tests/NanoQL.Tests/TestSuite/FdHestonTests.cs`: `testFdmHestonBarrierVsBlackScholes`, `testFdmHestonBarrier` and `testMethodOfLinesAndCN`, which wait for S-bar's `FdHestonBarrierEngine`.
- `tests/NanoQL.Tests/TestSuite/FdSabrTests.cs`: `testFdmSabrOp` and `testOosterleeTestCaseIV`, which wait for S-core's Richardson extrapolation and Sobol Brownian bridge sequences.
- `tests/NanoQL.Tests/TestSuite/FdmLinearOpTests.cs`: `testDerivativeWeightsOnNonUniformGrids`, `testBiCGstab` and `testGMRES`, which wait for S-core.
- `tests/NanoQL.Tests/TestSuite/GaussianQuadraturesTests.cs`: `testMomentBasedGaussianPolynomial` and `testGaussLaguerreCosinePolynomial`, which wait for S-core; `testNonCentralChiSquared` and `testNonCentralChiSquaredSumOfNodes`, which wait for S-xrt.
- `tests/NanoQL.Tests/TestSuite/HestonModelTests.cs`: `testFdBarrierVsCached` (S-bar), and `testAllIntegrationMethods` and `testCosHestonCumulants` (S-core).
- `tests/NanoQL.Tests/TestSuite/HestonSlvModelTests.cs`: `testMoustacheGraph`, which waits for S-bar's `FdHestonDoubleBarrierEngine` and `AnalyticDoubleBarrierBinaryEngine`, and `testDiffusionAndDriftSlvProcess`, which waits for S-core's Sobol Brownian bridge sequences.
- `tests/NanoQL.Tests/TestSuite/HybridHestonHullWhiteProcessTests.cs`: `testZeroBondPricing`, which waits for S-core's Sobol Brownian bridge sequences.
- `tests/NanoQL.Tests/TestSuite/IntegralsTests.cs`: `testExpSinh`, `testTwoDimensionalIntegration` and `testFolinIntegration` (S-core); and `testPiecewiseIntegral` (S-xrt).
- `tests/NanoQL.Tests/TestSuite/InterpolationsTests.cs`: the cases needing interpolations or extrapolators not yet ported (no-arbitrage SABR and the SABR transformations, kernel, Richardson, Laplace).
- `tests/NanoQL.Tests/TestSuite/LowDiscrepancySequencesTests.cs`: the randomized, lattice, Faure and Burley 2020 cases and the discrepancy cases, whose generators and statistics aren't ported yet.
- `tests/NanoQL.Tests/TestSuite/MatricesTests.cs`: the orthogonal-projection, QR and Moore-Penrose cases, whose matrix utilities aren't ported yet.
- `tests/NanoQL.Tests/TestSuite/NthOrderDerivativeOpTests.cs`: every case but `testSparseMatrixApply`, which wait for S-core's `NthOrderDerivativeOp`.
- `tests/NanoQL.Tests/TestSuite/OdeTests.cs`: `testMatrixExponential` and `testMatrixExponentialOfZero`, which wait for S-core's `Expm`.
- `tests/NanoQL.Tests/TestSuite/OptimizersTests.cs`: the hybrid simulated annealing, L-BFGS-B, differential evolution and CMA-ES cases, whose optimizers aren't ported yet.
- `tests/NanoQL.Tests/TestSuite/PiecewiseBlackVarianceSurfaceTests.cs`: `testSabrEquivalence`, which waits for S-xeq's `SABRVolTermStructure`.
- `tests/NanoQL.Tests/TestSuite/PiecewiseYieldCurveTests.cs`: `testDefaultInstantiation`'s ConvexMonotone curve (S-core); `testConstructionWithExplicitBootstrap`'s LocalBootstrap curve (S-core); `testConvexMonotoneForwardConsistency`, `testLocalBootstrapConsistency` and `testGlobalBootstrapVariables` (S-core).
- `tests/NanoQL.Tests/TestSuite/QuantoOptionTests.cs`: the analytic and forward cases and `testPDEOptionValues`, which wait for S-exo's quanto engines; the barrier case, which waits for S-bar's quanto barrier option; and `testDoubleBarrierValues`, which waits for S-xeq's quanto double-barrier option.
- `tests/NanoQL.Tests/TestSuite/RngTraitsTests.cs`: `testRanLux`, whose generator isn't ported yet.
- `tests/NanoQL.Tests/TestSuite/SolversTests.cs`: the bisection, false-position and secant cases, whose solvers aren't ported yet.
- `tests/NanoQL.Tests/TestSuite/StatsTests.cs`: `testSequenceStatistics` and `testConvergenceStatistics`, which wait for S-mmp's sequence and convergence statistics.
- `tests/NanoQL.Tests/TestSuite/SwingOptionTests.cs`: every case but `testFdBSSwingOption`, which wait for S-xeq's extended Ornstein-Uhlenbeck processes, jump mesher and engines.
- `tests/NanoQL.Tests/TestSuite/TimeSeriesTests.cs`: `testIntervalPrice`, which waits for `IntervalPrice` (`ql/prices`, S-bar).
- `tests/NanoQL.Tests/TestSuite/VarianceSwapsTests.cs`: `testMCVarianceSwap`, which waits for S-exo's `MCVarianceSwapEngine`.
