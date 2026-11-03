<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Divergences

Every documented divergence from QuantLib v1.44, one entry per test class under
`tests/NanoQL.Tests/Divergences/` with its file, its summary and its facts, generated from those files by
`tools/divergences.py`: regenerate it with `.venv\Scripts\python -I tools/divergences.py` rather than
editing it by hand.

## AbcdCalibrationFixedATests

[`Divergences/AbcdCalibrationFixedATests.cs`](../tests/NanoQL.Tests/Divergences/AbcdCalibrationFixedATests.cs)

The abcd calibration's fixed a: QuantLib's transformation gives a as a positive a + d less d (`abcdcalibration.cpp`'s `AbcdParametersTransformation`), so with a fixed and d free it fixes a + d, and the fit moves a. On set 1 with a fixed at −0.06, QuantLib returns a = −0.11449872096226477 and d = 0.22449872096226478, a + d held at 0.11 [the harness]. nanoql keeps a at −0.06, d above max(0, −a); the fit is valid, at a minimum of the cost over b, c and d (each gradient within the end criteria's tolerance), and a restart from a second guess reaches it too.

Facts:

- `AFixedAKeepsItsGuess`

## AbcdCalibrationGuardTests

[`Divergences/AbcdCalibrationGuardTests.cs`](../tests/NanoQL.Tests/Divergences/AbcdCalibrationGuardTests.cs)

The abcd calibration's guard: QuantLib's transformation leaves b free, so its optimizer can step to a trial point the abcd function's validation refuses, and the fit throws. On set 6, a dip whose total variance grows, QuantLib refuses with "b (-0.374029) less than -0.267779: negative function value at stationary point 2.45429" [the harness]; nanoql's guard fits it validly, at a minimum (each parameter's gradient within the end criteria's tolerance) that a restart from a second guess reaches too. And on the sets QuantLib completes, 0, 1, 4 and 5, the guard never acts: their fits are QuantLib's bit for bit, set 1's among them although its gradient is above the tolerance.

Facts:

- `ADipQuantLibRefusesIsFittedAtAMinimum`
- `TheFitsQuantLibCompletesKeepItsBits`

## AbcdMaximumTests

[`Divergences/AbcdMaximumTests.cs`](../tests/NanoQL.Tests/Divergences/AbcdMaximumTests.cs)

The abcd function's maximum. Its location is QuantLib's master's (#2841, after the pin): QuantLib 1.44 returns the stationary point 1/c − a/b whatever b's sign, a minimum when b < 0. Its value is the function at that location: QuantLib returns d whenever b = 0 or a ≤ 0. QuantLib's values are the wheel's; the check is QuantLib's own `operator()`.

Facts:

- `ANegativeBsStationaryPointIsAMinimum`
- `TheMaximumValueIsTheFunctionAtItsLocation`

## AbcdPrimitiveTests

[`Divergences/AbcdPrimitiveTests.cs`](../tests/NanoQL.Tests/Divergences/AbcdPrimitiveTests.cs)

The abcd function's primitive at a small c: QuantLib's closed form divides by powers of c and cancels, so the covariance of a = 0.1, b = 0.2, d = 0.15 over the first year for fixings at 2 and 3 years reads, on the wheel, 0.41570091639483625 at c = 1e-4, 0.4112554826093383 at 1e-5, 0.26249003480977007 at 1e-6 and −2220.182821037571 at 1e-7, where its Gauss-Lobatto integral is 0.4157007, 0.4158201, 0.4158320 and 0.41583320. nanoql switches to a cancellation-free series where QuantLib's form cancels; the check is the integral of the covariance's integrand, `AbcdSquared`, by Gauss-Lobatto, the `integrals` cases' route, and QuantLib's bits where it doesn't cancel.

Facts:

- `ASmallCsCovarianceIsItsIntegral`

## AbcdTermStructures

[`Divergences/AbcdTermStructures.cs`](../tests/NanoQL.Tests/Divergences/AbcdTermStructures.cs)

The term structures of Black volatilities the abcd facts fit, sets 0 to 6 over 0.5 to 20 years (`tools/vectors/gen/cxx/termstructures/volatility/abcdcalibration.py`'s): falling (0), rising (1), two whose total variance shrinks (2, 3), two dips whose total variance grows (4, 5), and a dip QuantLib refuses (6); and the cost's gradient a fit is checked by.

## AdaptiveRungeKuttaSizeTests

[`Divergences/AdaptiveRungeKuttaSizeTests.cs`](../tests/NanoQL.Tests/Divergences/AdaptiveRungeKuttaSizeTests.cs)

An ordinary differential equation whose function returns a vector of another length than its argument's. QuantLib's `AdaptiveRungeKutta` reads the function's value at every index of the solution vector (`adaptiverungekutta.hpp:111-113, 219-247`), past the end of a shorter one, and ignores the extra values of a longer one; the wheel's `RungeKutta` refuses both with its own wrapper's check, "Python function did not return a list of correct size" (`ode.i:79-81`), which QuantLib doesn't make. nanoql refuses both with `NanoQLException`, "the ODE function returned n values for m".

Facts:

- `AFunctionOfAnotherLengthIsRefused`

## AffineDiscountBondFactorsTests

[`Divergences/AffineDiscountBondFactorsTests.cs`](../tests/NanoQL.Tests/Divergences/AffineDiscountBondFactorsTests.cs)

An affine model's discount bond given an empty vector of factors. QuantLib's `OneFactorAffineModel::discountBond(now, maturity, Array factors)` reads `factors[0]` (`onefactormodel.hpp:132-134`), past the end of an empty array, which crashes the wheel with an access violation. nanoql refuses it with `ArgumentException`, "no factors given", naming `factors`; given a factor, it prices as QuantLib's, on the vector's first element.

Facts:

- `AnEmptyFactorVectorIsRefused`

## AffineHazardRateTests

[`Divergences/AffineHazardRateTests.cs`](../tests/NanoQL.Tests/Divergences/AffineHazardRateTests.cs)

The affine hazard-rate curve's initial intensity: the model's dynamics' short rate at the process's x0, where QuantLib squares x0. On nodes today and one, three and five years out (Actual/365 Fixed), with the Cox-Ingersoll-Ross model r0 0.02, θ 0.03, k 0.5, σ 0.05, QuantLib's curve of zero hazard rates reads S(5) 0.90893263808262059, P(0, 5, r0²), where its own base structure on the same model reads 0.87690828065332527, P(0, 5, r0); on hazard rates 0.01 to 0.04 its S(5) is 0.79418153056948726, and its conditional survival from one to three years at an intensity of 0.025 is 0.90156675873632208, which reads no x0. On Vasicek r0 0.02, a 0.1, b 0.03, σ 0.01, whose x0 is r0 − b, its curve reads 0.96957583433..., its base 0.89655379153....

Facts:

- `TheCurveOfZeroHazardRatesIsTheBaseStructure`
- `TheCurvesSurvivalIsQuantLibsOnTheModelsOwnIntensity`

## AlphaFinderTests

[`Divergences/AlphaFinderTests.cs`](../tests/NanoQL.Tests/Divergences/AlphaFinderTests.cs)

`AlphaFinder`'s fixes, QuantLib's master's finder after the pin (#2910, `dbe958954`), and its b where a final part fails. QuantLib's master fixes (a) and (b) on the same quantities, returns from its bisection at an exact root and where its point stops moving, and returns the final part's success; its cases are below. `alphafinder.cpp:335-346`, `:405-415`: where the turning point's value at α₀ is at most the target, QuantLib 1.44 returns α₀ without checking `finalPart`'s result, which fails where the variance left is negative, and its search returns its α unchecked too. nanoql returns false there, as QuantLib's master does, its α QuantLib's and its b NaN; the alpha calibration refuses the step, as the master's does (`Divergences/CTSMMCapletAlphaFormCalibrationTests.cs`). (a) `:372-373`: the downward search's loop tests `topValue`, which it never changes; on the hyperbolic inputs QuantLib's `solve` returns false where the acceptable α lie strictly below α₀, and nanoql's loop, testing `bottomValue`, finds α −0.3111 at the target; on a second set of inputs, QuantLib's returns false and nanoql's α 0.41286, whose final part fails. (b) `:117`, `:145`: `Minimize` calls the feasibility test on the objective's value, not on the trial α, so where that value isn't an acceptable α it stops at its first trial: on the hyperbolic inputs QuantLib returns α −0.48497 with homogeneity failure 3.3880, where the acceptable α run from −0.7519 to −0.3112 and the least failure is 3.3628 at −0.7519 (nanoql with QuantLib's test restored reproduces QuantLib's alpha-form calibration at maximum homogeneity bit for bit); on a third set of inputs, QuantLib's α 0.5168373510089983 fails by 0.12497903935064759 and nanoql's 0.4467697074628475 by 0.12229674463151122, and where the small α QuantLib's test reads are acceptable both return 1.2307430953608208e-08. (c) `:30-111`: the bisections and `Minimize` loop on the bracket's width alone, so at tolerance 0 QuantLib's search never ends (killed after 20 s); nanoql's also stops when the bracket no longer moves. (d) Where a final part fails QuantLib leaves b at its caller's value; nanoql's is NaN. The master's minimization tests the trial α before it reads the objective there, as nanoql's does; the alpha calibration's fallback keeps 1.44's order, the objective's value read first and tested. Checks: QuantLib's arithmetic restated at an α (the turning point's value, the final part, the homogeneity failure), and the alpha-form calibration's QuantLib case at tolerance 0 against 1e-15.

Facts:

- `AHalfSolvedInitialAlphaIsRefused`
- `TheDownwardSearchOnRoundOnesInput`
- `MaximumHomogeneityOnRoundOnesInputs`
- `TheDownwardSearchFindsTheAcceptableAlphaBelowAlphaZero`
- `MaximumHomogeneityFindsTheAcceptableMinimum`
- `ToleranceZeroEnds`
- `TestAlphaFinderExactBisectionRoot`
- `TestAlphaFinderBisectionStagnation`
- `TestAlphaFinderMaximumHomogeneity`
- `TestAlphaFinderDownwardInteriorSearch`
- `TestAlphaFinderInteriorSearch`
- `TestAlphaFinderRejectsNegativeRemainingVariance`
- `TestAlphaFinderNoRealSolution`
- `TestMaximumHomogeneityCalibration`

## AmericanBasketStrikeScalingTests

[`Divergences/AmericanBasketStrikeScalingTests.cs`](../tests/NanoQL.Tests/Divergences/AmericanBasketStrikeScalingTests.cs)

The Longstaff-Schwartz basket engine's state scaled by the strike. QuantLib scales the assets' prices by 1/K (`mcamericanbasketengine.cpp:48-50`, `:67-73`): at a zero strike, an exchange of the basket for nothing, the scale is infinite and the price NaN, and at a negative strike the scale's sign swaps the minimum and the maximum (an American maximum call 101.16 on the test's market where the European is 116.67). nanoql scales by 1/|K|, and by 1 at a zero strike; a positive scale leaves the regression's polynomial span unchanged: QuantLib's N-dimensional finite-difference engine at K ≤ 0, and QuantLib's bits at K > 0, are the checks.

Facts:

- `AZeroStrikeMeetsTheFiniteDifferenceEngine`
- `ANegativeStrikeKeepsTheMaximumAndMeetsTheFiniteDifferenceEngine`
- `APositiveStrikeKeepsQuantLibsBits`

## AmericanBoundsOnTimeTests

[`Divergences/AmericanBoundsOnTimeTests.cs`](../tests/NanoQL.Tests/Divergences/AmericanBoundsOnTimeTests.cs)

The American window's bounds on the tree (upstream's fix after v1.44, #2878: `51c6263b7`, merged as `eaeac5651`): `TimeGrid(T, N)` builds its last time as (T/N)·N, an ulp off T, the discretized assets snap their exercise times to the grid but start at the unsnapped maturity, and QuantLib 1.44's American branch compares the two with plain `<=` and `>=`; so where the last time rounds below T the exercise is never applied at expiry, and where it rounds above, a window ending at maturity is rejected. Upstream compares both bounds with `isOnTime`, as the European branch does, in `DiscretizedVanillaOption` and `DiscretizedConvertible`. On the wheel, at the upstream cases' market (21 September 2026, 91 days to expiry on Actual/365 (Fixed), spot 100, r = 5%, q = 0, σ = 15%), where the last time rounds below T at 51 steps and above it at 101: the CRR call struck at 85 at 51 steps, American 16.05280871761558 against European 16.074532566935556; exercisable only at maturity at 101 steps, American 0.0 against European 16.075288548193498; and the zero-coupon convertible (conversion ratio 2, redemption 100, no credit spread) at 51 steps, American at maturity 98.76131265936051 against European 200.00004055297964. nanoql takes upstream's fix; its cases are upstream's (`europeanoption.cpp`'s `testBinomialAmericanExerciseAtMaturity` and `testBinomialAmericanExerciseOnlyAtMaturity`, `convertiblebonds.cpp`'s `testBinomialConversionAtMaturity`), until the pin moves to a release that carries the fix.

Facts:

- `TheAmericanExerciseAtMaturityIsApplied`
- `AnExerciseOnlyAtMaturityIsTheEuropean`
- `TheConvertibleConvertsAtMaturity`

## AmericanExerciseAtTheRootTests

[`Divergences/AmericanExerciseAtTheRootTests.cs`](../tests/NanoQL.Tests/Divergences/AmericanExerciseAtTheRootTests.cs)

American options worth exercising on the reference date, after QuantLib's master's #2879 and #2914, which upstream's cases check (`AmericanOptionTests`). QuantLib 1.44's binomial engine reports the Black-Scholes theta from the tree's value and Greeks where the option is exercised at the root, where the intrinsic value is time-independent and the equation doesn't hold: 5.7500000000149 for a put at 115 on 100 and 9.149999999935266 for a call at 85, 91 days, on 201 Cox-Ross-Rubinstein steps; and its finite-difference engine's interpolant undershoots the exercise value between nodes, 9.999997372915146 for a call at 100 on 110 over 90 days (q 2%, σ 10%) on its default grid, below its intrinsic 10, which its implied volatility then can't reach ("root not bracketed: f[0.0001,4] -> [2.627085e-06,6.603111e+01]") (QuantLib-Python 1.44). nanoql's binomial theta is 0 at the root; its finite-difference value is floored at the exercise value where the holder can exercise on the clock's reference date, and the implied volatility returns its lower bound where the price error there is within rounding; elsewhere QuantLib's bits.

Facts:

- `TheBinomialThetaAtTheRootIsZero`
- `TheFiniteDifferenceValueIsTheExerciseValueAndItsVolatilityTheLowerBound`

## AmericanPayoffAtExpiryKnockedOutTests

[`Divergences/AmericanPayoffAtExpiryKnockedOutTests.cs`](../tests/NanoQL.Tests/Divergences/AmericanPayoffAtExpiryKnockedOutTests.cs)

The knock-out at expiry with the strike on the spot, under QuantLib's master's low-volatility fix (#2855, taken as a fix). With the strike on the spot, the knock-out is already knocked out, and QuantLib sets both probabilities to 0 (`americanpayoffatexpiry.cpp:126-160`); #2855 then reads N(D₂) = 0 as an underflow and replaces it by e^(2μ ln(H/S))·N(D₂) = N(D₂) with a power of 1, so the knock-out prices −D·K·N(D₂): a call paying 10, S = H = 100, r 5%, vol 20%, one year, −4.19, where QuantLib 1.44 prices 0 (QuantLib-Python 1.44, and by hand: D₂ = −μσ = −0.15). nanoql takes #2855's form only where N(D₂) is the formula's. QuantLib's check: its own 1.44 value, 0, a knocked-out option's.

Facts:

- `AKnockOutWithTheStrikeOnTheSpotIsWorthNothing`

## AmericanPayoffAtHitNegativeRateTests

[`Divergences/AmericanPayoffAtHitNegativeRateTests.cs`](../tests/NanoQL.Tests/Divergences/AmericanPayoffAtHitNegativeRateTests.cs)

The American payoff at the hit under negative rates. QuantLib's closed form takes λ = √(μ² − 2 ln D / σ²T) (`americanpayoffathit.cpp:75`, `:90`), the square root of a negative number where r < 0 and μ² < −2r/σ², so the value is NaN: at r −1%, q −1.5% and 20%, and at r −0.75%, q −0.4% and 8%, a down-and-in put at 90 and an up-and-in call at 110 paying 5 (QuantLib-Python 1.44). nanoql's value there is the payoff's present value at the hit integrated over the first-passage density, which is QuantLib's closed form where λ is real; its Greeks are that integral's derivatives. QuantLib's checks: its closed form, the same quantity where it works (the integral here computed by the test); and its finite-difference barrier engine (400 × 800) where it doesn't: the cash at the hit is a knock-out's rebate on a payoff that pays nothing, within that engine's grid error. Near the spot, where the Greeks' integrals grow as 1/b and 1/b², the value and Greeks are checked against the integrals computed to 40 digits, independently of the quadrature.

Facts:

- `WhereLambdaIsRealTheFirstPassageIntegralIsQuantLibsClosedForm`
- `WhereLambdaIsNotRealTheValueIsQuantLibsFiniteDifferenceRebate`
- `NearTheSpotTheValueAndGreeksAreTheFirstPassageIntegralsToTheirPrecision`

## AmericanPayoffZeroVarianceTests

[`Divergences/AmericanPayoffZeroVarianceTests.cs`](../tests/NanoQL.Tests/Divergences/AmericanPayoffZeroVarianceTests.cs)

The American digital payoffs below a variance of ε. QuantLib divides by the variance there: the at-hit value is NaN out of the money (`americanpayoffathit.cpp:87-102`), and the at-expiry one takes the spot's side of the strike where the limit takes the forward's (`americanpayoffatexpiry.cpp:112-121`). On a cash of 10, S 100, q 0, r 5% and one year, QuantLib gives at σ = 0: at the hit at 102, NaN; at expiry at 102, 0; the knock-out at expiry at 102 (or 103), 9.512294245007139; and its own formula at σ = 1e-6 gives 9.80392156862745, 9.512294245007139 and 0 (QuantLib-Python 1.44). nanoql's underlying follows its forward: paid at the hit when the forward path reaches the strike, at expiry if it did (knock-in) or didn't (knock-out). QuantLib's check: its own formula at σ = 1e-6, which nanoql's engine there computes.

Facts:

- `AtTheHitTheForwardPathIsPaidWhenItReachesTheStrike`
- `AtExpiryTheForwardsSideOfTheStrikeCounts`
- `TheKnockOutIsPaidWhereTheForwardNeverReachesTheStrike`
- `TheGreeksAtTheHitAreTheLimitsDerivatives`

## AnalyticDigitalAmericanMismatchedTimesTests

[`Divergences/AnalyticDigitalAmericanMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticDigitalAmericanMismatchedTimesTests.cs)

The analytic digital American engine on structures dated differently. QuantLib's engine reads the dividend curve and the volatility at the exercise date through their own clocks (`analyticdigitalamericanengine.cpp:53-58`), so an expiry before a structure's reference date throws. nanoql reads each structure by date, nothing accrued before its own reference date. QuantLib's check: its engine on the market restated from the earliest date, every structure accruing from today exactly what the original accrues from its own date (QuantLib-Python 1.44).

Facts:

- `LaterDatedStructuresAreReadFromTheirOwnDates`
- `AnExpiryBeforeTheStructuresDatesAccruesNothingFromThem`

## AnalyticEuropeanVolatilityResultTests

[`Divergences/AnalyticEuropeanVolatilityResultTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticEuropeanVolatilityResultTests.cs)

`AnalyticEuropeanEngine`'s "volatility" additional result for an option expiring on the evaluation date, priced when the reference date's events are included. QuantLib reports √(variance / time to expiry) (`analyticeuropeanengine.cpp:113`), 0/0 there, NaN. nanoql reports the Black volatility at the exercise date and strike, QuantLib's own value for that date; every other result is QuantLib's.

Facts:

- `TheVolatilityAtExpiryIsTheStructures`

## AnalyticGjrGarchDividendTests

[`Divergences/AnalyticGjrGarchDividendTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticGjrGarchDividendTests.cs)

The analytic GJR-GARCH engine with a dividend yield. QuantLib's engine prices on the rate r − q and discounts the call's payoff at r − q too (`analyticgjrgarchengine.cpp`), so its price at (r, q) is its price at (r − q, 0): a 180-day call at the money on the gjrgarchmodel test's model, r 5% and q 3%, is 3.49762, where the forward's measure gives D_q times that, 3.44608. nanoql's call is D_q times QuantLib's engine at (r − q, 0), and its put follows by parity, C − P = D_q·S − D_r·K. QuantLib's check: its engine at q = 0, where nanoql's is QuantLib's bit for bit (the engine's vectors), and parity.

Facts:

- `ThePriceIsTheDividendDiscountTimesTheZeroYieldPrice`
- `PutAndCallKeepParity`

## AnalyticGjrGarchMomentTests

[`Divergences/AnalyticGjrGarchMomentTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticGjrGarchMomentTests.cs)

`AnalyticGJRGARCHEngine`'s moments. Where two of the variance's growth factors m₁, m₂ and m₃, or one of them and 1, are equal, QuantLib's divided differences of their powers are 0/0 and its price NaN: at α = β = γ = 0, all three 0, and in the integrated GARCH β 0.9, α 0.1, γ 0, λ 0, where m₁ is 1. nanoql takes each divided difference at its limit, a polynomial in the factors, continuous with QuantLib's prices with the factors 1e-8 and 1e-10 apart (QuantLib-SWIG 1.44); elsewhere QuantLib's arithmetic. Two of QuantLib's defects are kept, documented on the engine: its fourth moment, whose kurtosis at the deterministic limit (α = γ = 0) is about 3 − Σh where the log return is normal, so a call prices above Black-Scholes at the same total variance; and its Edgeworth price, which leaves the no-arbitrage bounds where the skewness and kurtosis are large, as on QuantLib's own model at λ 0.2 and a year. Both are QuantLib's prices, here bit for bit or within the engine's last bits (the C runtime's `pow` apart).

Facts:

- `AtEqualGrowthFactorsTheMomentsTakeTheirLimit`
- `AtTheDeterministicLimitTheKurtosisIsQuantLibs`
- `ThePriceCanLeaveTheNoArbitrageBoundsAsQuantLibs`

## AnalyticGjrGarchPayoffRefusalTests

[`Divergences/AnalyticGjrGarchPayoffRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticGjrGarchPayoffRefusalTests.cs)

The analytic GJR-GARCH engine refuses a payoff that isn't a plain vanilla. QuantLib's engine reads the payoff as a `StrikedTypePayoff` and prices the plain vanilla on its strike and type (`analyticgjrgarchengine.cpp`), so a cash-or-nothing or an asset-or-nothing option is priced as the call it isn't. nanoql refuses them, as QuantLib's Monte Carlo GJR-GARCH engine does ("non-plain payoff given").

Facts:

- `ANonPlainPayoffIsRefused`

## AnalyticH1HWBandTests

[`Divergences/AnalyticH1HWBandTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticH1HWBandTests.cs)

H1HW where its fit has no decaying exponential. The engine fits E[√v_t] ≈ a + b·e^(−ct), b = √v₀ − a, c = −log((Λ(1) − a)/b) (`analytich1hwengine.cpp:103-134`): where the ratio isn't positive c is NaN and so is the price, at ρ_Sr = 0 too, where H1HW is the analytic Heston-Hull-White engine; where it exceeds 1, c < 0 and the fit grows exponentially, on QuantLib's own ten-year parameters at v₀/θ 0.15 a call 1.70 below its FD Heston-Hull-White engine (QuantLib 1.44). nanoql takes the fit's limits there: at or below 0, and where b = 0, c = +∞, the b terms dropped; at or above 1, c = 0; QuantLib's fit and bits in between.

Facts:

- `AtNoEquityRateCorrelationH1HWIsTheAnalyticHestonHullWhiteEngine`
- `ThePriceIsContinuousAcrossTheBandsEdges`
- `OnQuantLibsTenYearParametersH1HWHoldsTheFiniteDifferenceEngine`

## AnalyticH1HWSmallReversionTests

[`Divergences/AnalyticH1HWSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticH1HWSmallReversionTests.cs)

H1HW's I₄ at small reversion and at c = λ. QuantLib's I₄ = −1/λ (b/c (1 − e^(−cT)) + aT + a/λ (e^(−λT) − 1) + b/(c − λ) e^(−cT)(1 − e^(−T(λ − c)))) (`analytich1hwengine.cpp:126-130`) cancels as λT falls to order λ, its relative error about 2ε/(λT)², 1e-12 at λT 1e-2, and its last term is 0/0 at c = λ, a NaN price. nanoql evaluates the expression in double-double below λT = 2e-2, in quad-double below 1e-10 and by the cubic below 1e-12, QuantLib's double above, and writes the last term b·e^(−cT)·(−expm1((c − λ)T))/(c − λ), exact everywhere, −b·T·e^(−cT) at c = λ. Checked against the expression in 50 digits at a 0.2, b 0.03, c 0.4 and T 10.

Facts:

- `BelowTheCutI4HoldsTheExactExpression`
- `AtCEqualToLambdaI4IsItsLimit`

## AnalyticHestonHullWhiteSmallReversionTests

[`Divergences/AnalyticHestonHullWhiteSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticHestonHullWhiteSmallReversionTests.cs)

The analytic Heston-Hull-White engine's variance offset at small reversion. QuantLib's m = σ²/(2a²)(t + 2/a e^(−at) − 1/(2a) e^(−2at) − 3/(2a)) (`analytichestonhullwhiteengine.cpp:57-67`) cancels to order (a·t)³, its relative error about ε/(a·t)³; nanoql takes the Black-Scholes-Merton Hull-White engine's series below a·t = 1e-2, QuantLib's closed form and bits above. Checked against the closed form in 50 digits, at σ 0.01 and t 2.

Facts:

- `BelowTheCutTheSeriesHoldsTheClosedForm`
- `AboveTheCutQuantLibsClosedFormStands`

## AnalyticPdfHestonTailsTests

[`Divergences/AnalyticPdfHestonTailsTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticPdfHestonTailsTests.cs)

The analytic PDF Heston engine's tails beyond its window. QuantLib integrates the payoff against the density over ln F ± 8√V (`analyticpdfhestonengine.cpp:54-65`) and drops the mass beyond: on its own markets a half-year put at 0.7F 7% low and at 0.5F 53% low (the far-from-Feller set), at a vol of variance of 1 and correlation −0.9 a five-year put at half the forward 18% low (QuantLib-Python 1.44); its own test checks calls, whose right tail is thin there. nanoql adds the tails' values where they're worth more than the integrations' accuracy: the payoff a line a + bS between its strikes, a piece's value is a times its probability plus bF times its probability under the share measure, both from the spot's distribution at the window's edges and the strikes; QuantLib's price where they aren't. The window's value is QuantLib's to the bit, the price less the additional result `tailsValue`. The check: QuantLib's analytic engine.

Facts:

- `AFatLeftTailIsAdded`
- `OnQuantLibsVectorMarketsThePricesAreTheAnalyticEngines`
- `DigitalAndAssetPutsTakeTheirTails`
- `AStrikeBeyondTheWindowSplitsItsTail`
- `WhereTheTailsAreWithinTheAccuracyQuantLibsPriceStands`
- `ATailBelowTheDistributionsResolutionOrWorthLessThanTheAccuracyIsLeftOut`
- `APayoffNotLinearOnATailWithMassIsRefused`
- `TheShareMeasuresDistributionKeepsPutCallParity`

## AnalyticPdfHestonWindowTests

[`Divergences/AnalyticPdfHestonWindowTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticPdfHestonWindowTests.cs)

The analytic PDF Heston engine's integration window. QuantLib centres the window on x₀ + ln(D_r/D_q) (`analyticpdfhestonengine.cpp:54`), the forward's mirror about the spot, where the density of ln S_T centres on ln F = x₀ + ln(D_q/D_r), as `NanoQL.Methods.HestonRNDCalculator` reads it: at a carry of 10% over ten years with v₀ = θ = 0.0025 the window misses the density and the call at 100 prices 0.00025 where the analytic engine gives 63.24. nanoql centres the window on ln F, QuantLib's window where the carry is 0. The check: QuantLib's analytic engine.

Facts:

- `AtAHighCarryThePdfEngineMatchesTheAnalyticEngine`
- `AtANegativeCarryThePdfEngineMatchesTheAnalyticEngine`

## AnalyticThetaMismatchedTimesTests

[`Divergences/AnalyticThetaMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticThetaMismatchedTimesTests.cs)

The analytic engines' theta on structures dated differently. QuantLib's Black theta divides the payoff discount's, the forward's and the variance's terms by one time, the volatility's in the European engine (`analyticeuropeanengine.cpp:92-98`) and the risk-free curve's in the dividend engine (`analyticdividendeuropeanengine.cpp:108-111`), exact only where the structures share it. On a risk-free curve settling T+2 beside a dividend curve and a volatility dated today, the two engines read different thetas for the same option: the dividend engine's −83.74 at a week where the decay is −34.44, and 0 on the risk-free curve's own date, `BlackCalculator::theta`'s at a zero time. nanoql's engines divide each term by its own structure's time, on the engine's day counter, none for a structure whose time is 0. That is QuantLib's `blackScholesTheta` (`greeks.cpp:25-35`, the binomial engine's theta) at each structure's own rate on QuantLib's analytic value, delta and gamma, to the 1e-11 its rates read over 1e-4 years carry, and the identity −(r ρ / T_r + q ρ_q / T_q + σ ν / (2 T_v)) over QuantLib's own rho, dividend rho and vega to 1e-14. With no dividend the two engines now read the same theta. Where the structures share their times, it is QuantLib's.

Facts:

- `EachTermRunsOverItsOwnStructuresTime`
- `OnTheRiskFreeCurvesDateItsTermIsNone`
- `APayoffDiscountCurveRunsOverItsOwnTime`
- `EachEnginesThetaIsPerItsOwnDayCountersYear`

## AndreasenHugeCallPutErrorTests

[`Divergences/AndreasenHugeCallPutErrorTests.cs`](../tests/NanoQL.Tests/Divergences/AndreasenHugeCallPutErrorTests.cs)

Andreasen-Huge's call-put calibration error by strike. QuantLib takes each option's put or call error by comparing the forward with the j-th grid point, `fwd > gridInFwd_[j]` (`andreasenhugevolatilityinterpl.cpp:418`), about an eighth of the smallest strike, so it always takes the puts': on calls struck 101.5 to 130 from 15 October 2026, all above the forward, it gives (1.6410985795898742e-08, 1.148950832474151e-06, 2.1308681968043031e-07), where each option's vega-scaled error on its out-of-the-money side, from the calibrated prices, gives (1.6414905070833644e-08, 1.1491274031493572e-06, 2.1310003360549513e-07) (QuantLib-Python 1.44). nanoql compares the forward with the strike, the out-of-the-money side; QuantLib's check, the per-option errors from the calibrated prices, gives nanoql's values. Where every strike is below the forward the two choices agree, and QuantLib's values stand within bounds, the calibration's errors being ill-conditioned: the C runtime and processor it runs on move them. The straddling strikes' largest error moves by 1.8e-8 relative on GitHub's windows-2022 runner, 5.6e-8 on macOS and 1.7e-8 on Linux (CI run 41146085965), held to 1e-6; below the forward the smallest error, a residual of 2.9e-9, moves by 1.36e-6 on the windows-2022 runner, 4e-15 absolute (CI run 41543305018), held to 2e-5, and the largest and the average, not yet reached on a runner, stay at 1e-6 until a run measures them. Every closeness check runs before the test fails, so a failure shows each difference.

Facts:

- `TheErrorIsEachOptionsOutOfTheMoneySide`

## ApproximationThetaMismatchedTimesTests

[`Divergences/ApproximationThetaMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/ApproximationThetaMismatchedTimesTests.cs)

The American approximations' theta where early exercise is never optimal, on structures dated differently. There Barone-Adesi-Whaley, Ju and Bjerksund-Stensland report the European results, and QuantLib's theta divides every term by the volatility's time (`baroneadesiwhaleyengine.cpp:200-204`, `juquadraticengine.cpp:108-112`, `bjerksundstenslandengine.cpp:145-149`), as its analytic European engine does. nanoql's analytic European engine divides each term by its own structure's time (`Divergences/AnalyticThetaMismatchedTimesTests.cs`, checked there against QuantLib's `blackScholesTheta`), and the approximations' European branch is that engine's. QuantLib's check: through the analytic engine's. QuantLib's thetas, its approximations' and its analytic engine's alike, recorded (QuantLib-Python 1.44). Where the structures share their dates, QuantLib's bits (the engines' vectors).

Facts:

- `ANeverExercisedPutsThetaIsTheAnalyticEngines`
- `ANeverExercisedCallsThetaIsTheAnalyticEngines`

## ApproximationUpstreamFixesTests

[`Divergences/ApproximationUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/ApproximationUpstreamFixesTests.cs)

Two fixes QuantLib's master made to the American approximations after the pin, which nanoql takes, with QuantLib 1.44's values recorded (QuantLib-Python 1.44). #2854 (`b19a8aa53`): below a variance of ε, Bjerksund-Stensland's approximation divides by zero, and QuantLib 1.44 returns NaN; the master returns the European value floored at the exercise value. #2906 (`513d6002a`): for short-dated, highly volatile options Ju's correction can take the value below the European one, a lower bound for the American; the master floors it there, with the European delta and gamma. QuantLib's check: the master's own cases, `TestSuite/AmericanOptionTests.cs`' `TestBaroneAdesiWhaleyAndJuAtLowVolatility` and `TestJuNotBelowEuropeanValue`; here the values themselves.

Facts:

- `BjerksundStenslandAtZeroVarianceIsTheFlooredEuropeanValue`
- `JuBelowTheEuropeanValueIsTheEuropeanResults`

## ApproximationWindowTests

[`Divergences/ApproximationWindowTests.cs`](../tests/NanoQL.Tests/Divergences/ApproximationWindowTests.cs)

The American approximations on an exercise window opening after the reference date. QuantLib's Barone-Adesi-Whaley, Bjerksund-Stensland, Ju, QD+ and QD fixed-point engines read only the last date (`baroneadesiwhaleyengine.cpp:161-166`, `bjerksundstenslandengine.cpp:463-469`, `juquadraticengine.cpp:61-66`, `qdplusamericanengine.cpp:178-184`), so a put (S 80, K 100, σ 25%, r 5%, a year) exercisable in its last 3 months prices as exercisable now: 20.28 to 20.40, where QuantLib's FD engine gives 18.999. nanoql prices the window by the discount to its opening times the expectation, over S(t₁)'s density, of the engine's own American from the opening (`ApproximationWindow`). QuantLib's check: its FD engine on the window (4,000 × 4,000), each engine within its own error on the option exercisable now (its value against the FD engine's there) (QuantLib-Python 1.44). A window opening on the reference date is QuantLib's, bit for bit.

Facts:

- `AnEnginesWindowValueIsTheFdEnginesToItsOwnError`
- `AnEnginesWindowValueDeltaAndGammaAreTheFdEnginesToItsOwnError`
- `AWindowOpeningOnTheReferenceDateIsQuantLibs`
- `ANodesRefusalRefusesTheWindow`

## AsianDuplicateFixingsTests

[`Divergences/AsianDuplicateFixingsTests.cs`](../tests/NanoQL.Tests/Divergences/AsianDuplicateFixingsTests.cs)

A fixing date given twice (fixed). The average counts it twice, as QuantLib's analytic engines count it, where QuantLib's finite-difference condition weighs every node by the first duplicate's count and its Monte Carlo grid keeps the time once, so its pricers count it once: on spot 100, q 3%, r 6%, σ 20% (Act/360), a call at 100 fixing at 90, 180, 180, 270 and 360 days prices 6.624103653021713 by QuantLib's FD engine (100 × 100 × 50) where the contract is worth 6.0461 ± 0.0032, its arithmetic Monte Carlo 6.115069708532511 (the schedule counted once, 6.1358), and its geometric Monte Carlo 5.934198148917606 ± 0.0144 against its analytic geometric engine's 5.884809360030421, 3.4σ. nanoql weighs each node by its fixings' count. Checks: an exact Monte Carlo of the average with the date counted twice, QuantLib's Mersenne twister and inverse normal, the FD engine within its error and the grid's own, measured on the schedule without the duplicate; and the geometric engine.

Facts:

- `TheFiniteDifferenceEngineCountsTheDateTwice`
- `TheMonteCarloEnginesCountTheDateTwice`

## AsianLateSettlementTests

[`Divergences/AsianLateSettlementTests.cs`](../tests/NanoQL.Tests/Divergences/AsianLateSettlementTests.cs)

The Asian engines on a market whose structures settle after the evaluation date (fixed). The option splits its fixings by the evaluation date, and QuantLib's engines read each fixing to come on the process's time from the structures' date, so a fixing today or tomorrow has a negative time: on a market settling two days on (spot 100, r 5%, q 1%, σ 20%, Act/365 Fixed), a call at 100 fixing today, tomorrow and every 30 days to 360 days prices 5.541332629516704 by the analytic geometric engine and 5.490134556097302 ± 0.055 by its Monte Carlo, the average losing two of its fourteen fixings, 18% high, and 5.687334138697442 ± 0.057 by the arithmetic Monte Carlo, 17% high; Turnbull-Wakeman refuses it with "negative time (-0.00547945) given", Choi with "first fixing date is in the past" and the FD engine with "Fixing dates must not contain past date"; and Levy refuses an averaging from today with "current average required for seasoned option". nanoql takes such a fixing at the clock's origin, the spot with no variance and no discount accrued, so a past fixing at the spot, and Levy's span before the origin at the spot. Check: the seasoned equivalent, the first two fixings past at the spot, which each engine prices as it, and which QuantLib prices 4.694309394952492, 4.6519094182914005, 4.874857833169246, 4.935628853043604, 4.921817927818181 and 4.954431596530007, and Levy with a current average at the spot 5.3936726841449385, each within the tolerance. Where only the volatility, or only the curves, settle two days on, Turnbull-Wakeman and Choi, which read each structure on its own, take the fixings before the latest structure's date at the spot, where QuantLib refuses them ("negative time", "first fixing date is in the past"); the check QuantLib's engines on the seasoned option, 4.945115990140009 and 4.9312430644836445, and 4.964671374839958 and 4.951052106848293.

Facts:

- `TheAnalyticGeometricEngineKeepsEveryFixing`
- `TheGeometricMonteCarloKeepsEveryFixing`
- `TheArithmeticMonteCarloKeepsEveryFixing`
- `TurnbullWakemanPricesTodaysFixings`
- `ChoiPricesTodaysFixings`
- `TurnbullWakemanPricesTodaysFixingsWhereTheStructuresSettleApart`
- `ChoiPricesTodaysFixingsWhereTheStructuresSettleApart`
- `TheFiniteDifferenceEnginePricesTodaysFixings`
- `LevyPricesAnAveragingFromToday`
- `TheHestonGeometricEngineKeepsEveryFixing`

## AsianMismatchedTimesTests

[`Divergences/AsianMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/AsianMismatchedTimesTests.cs)

The Asian engines on structures that measure time differently (fixed). QuantLib's engines read each structure on its own day counter's time as though they shared one clock, so on an Actual/360 risk-free curve (r 5%) beside Actual/365 Fixed dividends (2%) and volatility (25%) they are off their own values on the market restated onto one clock with the discounts and variances by date unchanged: a call at 100 on twelve fixings every 30 days by the FD engine +0.478%, by the arithmetic Monte Carlo +0.469%, Levy's continuous call over a year +0.482%, and the Heston geometric call (v₀ = θ 0.04, κ 2, vol of vol 0.5, ρ −0.5) −0.174%. nanoql runs the FD and Monte Carlo engines on `FdmBlackScholesOp.Clock`, Levy on the volatility's clock, and the Heston engine on `FdmHestonOp.Clock`, each structure read at the instants its times denote. Check: each engine on the restated market, to rounding for the grids and paths (the same numbers drawn) and within the tolerance for the integrals.

Facts:

- `TheFiniteDifferenceEnginePricesAsOnTheRestatedMarket`
- `TheMonteCarloEnginePricesAsOnTheRestatedMarket`
- `LevyPricesAsOnTheRestatedMarket`
- `TheHestonGeometricEnginePricesAsOnTheRestatedMarket`

## AsianUnsortedDatesTests

[`Divergences/AsianUnsortedDatesTests.cs`](../tests/NanoQL.Tests/Divergences/AsianUnsortedDatesTests.cs)

The all-past-fixings constructor's date order (fixed). QuantLib sorts the first constructor's fixing dates and keeps the second's in the caller's order, then splits them at the evaluation date by counting, so reversed dates read the wrong dates as past and the engines integrate them backwards: on spot 100, q 3%, r 6%, σ 20% (Act/360), a call at 100 fixing 60 and 30 days ago at 95 and 105 and monthly over a year prices 6.156224656227607 by Turnbull-Wakeman and 6.605506555285245 by the FD engine on the dates reversed, where sorted they price 4.69672540647389 and 4.71727034815397. nanoql sorts both constructors' dates, stably. Check: the sorted dates' route, bit for bit.

Facts:

- `TurnbullWakemanPricesReversedDatesAsSorted`
- `TheFiniteDifferenceEnginePricesReversedDatesAsSorted`

## AsianUpstreamFixesTests

[`Divergences/AsianUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/AsianUpstreamFixesTests.cs)

The fix QuantLib's master made to Turnbull and Wakeman's engine after the pin, which nanoql takes: #2954 (`f461b7852`) prices the last fixing on the evaluation date, at time 0, with a zero standard deviation, its σ 0, its delta `discount · α · E[A] / S` and its spot volatilities read as volatilities. QuantLib 1.44 divides the log moment ratio by the last time, 0, and its Black calculator refuses the NaN standard deviation (`turnbullwakemanasianengine.cpp:117-120`). nanoql reads the spot volatilities at the future average's own strike, where master reads them at K − A/m; on a flat volatility they agree. Master's every-fixing-past branch refuses an option with no fixings at all, "no fixings given", which nanoql's `DiscreteAveragingAsianOption` accepts and 1.44's engine priced on an average of 0.

Facts:

- `AnOptionWithNoFixingsIsRefused`
- `TestTurnbullWakemanLastFixingToday`

## AssetSwapNonParRepaymentTests

[`Divergences/AssetSwapNonParRepaymentTests.cs`](../tests/NanoQL.Tests/Divergences/AssetSwapNonParRepaymentTests.cs)

The asset swap's final repayment: QuantLib pays a given non-par repayment as an amount (`assetswap.cpp:125-134`), while `fairNonParRepayment` reads it per 100 of the bond's notional (`:284-296`), and with none given it repays the bond's redemption but stores 100 (`:130`). On the wheel, a 7-year 4% bond issued 15 March 2024 on a flat 3% curve, swapped at its clean price on Euribor 6M on 23 October 2026: on a 1e6 face a given repayment of 100 pays 100, the NPV 876,253.3609798652 and the fair spread −0.21178102152342376 at par, where with none given both are 0; its fair repayment (QuantLib's formula on the wheel's results, which hide the member), 199.99 with 100 given and 100 with none, fed back leaves 876,165.7356437672 and 876,253.3609798652; on a face of 100 redeemed at 101 the fair repayment is 99.99999999999999, which fed back leaves 0.8763409950793744. nanoql pays a given repayment as that share of the notional, and with none given stores the redemption per 100 of notional, 101 here. QuantLib's check: its fair repayment fed back gives an NPV of 0 only on the consistent reading; on a face of 100 redeemed at 100 the two agree, and QuantLib's cases keep their bits.

Facts:

- `AGivenRepaymentIsPerHundredOfTheNotional`
- `TheFairRepaymentFedBackIsAtPar`
- `ARedemptionAboveParIsTheStoredRepayment`

## AtmAdjustedSmileSectionTests

[`Divergences/AtmAdjustedSmileSectionTests.cs`](../tests/NanoQL.Tests/Divergences/AtmAdjustedSmileSectionTests.cs)

The ATM-adjusted section's prices: QuantLib's option price, digital, vega and density are the source's at the adjusted strike (`atmadjustedsmilesection.hpp:49-69`), priced on the source's forward, while the section's level is the given one and its volatility the source's recentred: without recentering every price ignores the level it reports, and with it the prices agree with the section's own level and volatility only under Bachelier's formula. On the harness, over a SABR smile at a forward of 2% shifted by 1% with an exercise time of 2, a section at a level of 2.5% prices a call at 1% at 0.010130320486628017, the source's, and recentred a call at 2.5% at 0.002100781256289137, the source's at 2%. nanoql prices on the section's own level and volatility, as AtmSmileSection does. QuantLib's check: its own SmileSection formulas on that level and volatility.

Facts:

- `ThePricesAreOnTheSectionsOwnLevel`
- `TheRecentredPricesAreOnTheSectionsOwnLevel`

## AverageBasketPayoffWeightCountTests

[`Divergences/AverageBasketPayoffWeightCountTests.cs`](../tests/NanoQL.Tests/Divergences/AverageBasketPayoffWeightCountTests.cs)

The average basket payoff on a number of prices other than its weights'. QuantLib's `inner_product` runs over the weights and reads the prices unchecked (`basketoption.hpp:77-81`): on two assets, three weights read past the shorter array, so its Monte Carlo and finite-difference engines price a run-dependent value (the 2-D engine 2.4177 on one run, the Monte Carlo 11.902 and 11.896 on two), and one weight prices asset 1 alone (the 2-D engine 13.1679), where its Choi, Deng-Li-Zhou and single-factor engines refuse ("wrong number of weights arguments in payoff", `choibasketengine.cpp:97-98`). nanoql's payoff refuses a price count other than its weights' with that text: QuantLib's own check, everywhere.

Facts:

- `MoreWeightsThanAssetsAreRefused`
- `FewerWeightsThanAssetsAreRefused`

## BFGSTests

[`Divergences/BFGSTests.cs`](../tests/NanoQL.Tests/Divergences/BFGSTests.cs)

BFGS's inverse Hessian, one per minimization: QuantLib's method keeps it as a member, built at its first minimization only (`bfgs.cpp:29-32`), so a second minimization on the same object starts from the first's final Hessian, and one of another dimension reads it out of range; nanoql's starts each minimization from the identity, so a second is the first. QuantLib's values are its own class's.

Facts:

- `ASecondMinimizationStartsFromTheIdentity`

## BMANullIndexTests

[`Divergences/BMANullIndexTests.cs`](../tests/NanoQL.Tests/Divergences/BMANullIndexTests.cs)

A null BMA index is refused on entry, with QuantLib's text for a missing index, "no index provided" (`floatingratecoupon.cpp:52`), naming the parameter. QuantLib's `AverageBMACoupon` reads its index's fixing days before its base checks the index (`averagebmacoupon.cpp:111-113`), `AverageBMALeg` never checks it (`:165-166`) and its coupons then read it, and `BMASwap` passes it to its BMA leg (`bmaswap.cpp:55-58`): results C++ leaves undefined, where the wheel stops on Boost's assertion.

Facts:

- `ANullBMAIndexIsRefusedWithQuantLibsText`

## BSplineRepeatedKnotTests

[`Divergences/BSplineRepeatedKnotTests.cs`](../tests/NanoQL.Tests/Divergences/BSplineRepeatedKnotTests.cs)

Cubic B-splines on a repeated knot. QuantLib's recursion divides by a knot span that is zero on a repeated knot (`bspline.cpp:56-57`), giving NaN, so a cubic B-spline fit on the knots −30, −20, 0, 5, 10, 10, 15, 20, 25 and 30 throws "N_th cubic B-spline must be nonzero at t=0" on the wheel, where the knots 10 and 10.001 fit, the parameters 0.9, 0.8, 0.7, 0.6 and 0.5 discounting 0.9194765141427983 at t = 7. nanoql reads a term over a zero span as 0, the Cox–de Boor convention. QuantLib has no value on a repeated knot, so its check is its own values on knots a gap apart, which nanoql gives bit for bit, converging to the convention's as the gap closes.

Facts:

- `ARepeatedKnotIsTheLimitOfKnotsAGapApart`

## BachelierGreeksTests

[`Divergences/BachelierGreeksTests.cs`](../tests/NanoQL.Tests/Divergences/BachelierGreeksTests.cs)

The Bachelier calculator's theta, vanna and volga. QuantLib's theta reads the spot gamma where the time decay reads the forward gamma, missing (S/F)²; its vanna, the vega's sensitivity to the spot by its documentation, misses D·F/S; its volga divides by σ√T where the derivative in σ divides by σ, missing √T (`bacheliercalculator.cpp:263-271`, `:350-370`). At F/S 1.04 and T 2 they are −1.9078, −0.016563 and 0.0052986 where the price bumped gives −1.8295, −0.015586 and 0.0074933; QuantLib's cases run at T 1, which hides the volga's, and record the theta and vanna at F/S 1.02, −4.3159316452046594 and 0.0048333514600356151 (their checks moved here from `testBachelierCalculatorGreeks`). nanoql takes each missing factor, the vanna taking the spot as `BlackCalculator.Vanna` does. The checks: Bachelier's price in a spot model, F = S·e^{(r−q)T}, D = e^{−rT}, standard deviation σ_N·√T, bumped in T, in S and σ_N, and in σ_N twice.

Facts:

- `TheThetaReadsTheForwardGamma`
- `TheVannaIsTheVegasSensitivityToTheSpot`
- `TheVolgaIsTheVegasSensitivityToTheVolatility`

## BachelierPayoffTests

[`Divergences/BachelierPayoffTests.cs`](../tests/NanoQL.Tests/Divergences/BachelierPayoffTests.cs)

The Bachelier calculator on payoffs other than the plain vanilla. QuantLib's visitor sets alpha, beta and x for cash-or-nothing, asset-or-nothing and gap payoffs, which its value and Greeks never read (`bacheliercalculator.cpp:137-185`), so each prices 3.03, the vanilla's, at K 100, F 100, σ√T 8, where the payoffs are worth 4.75 (cash 10), 50.53 and 7.78 (second strike 90). nanoql values each by Bachelier's closed form and takes the Greeks as its derivatives. The checks: the closed forms under a normal forward, F_T ~ N(F, σ²), and their bumps (forward, deviation and strike by 1e-5 each way, the gamma's by 1e-3).

Facts:

- `ACashOrNothingPayoffIsWorthTheDiscountedProbability`
- `AnAssetOrNothingPayoffIsWorthTheDiscountedTruncatedForward`
- `AGapPayoffIsWorthItsBachelierValue`

## BachelierPutBranchTests

[`Divergences/BachelierPutBranchTests.cs`](../tests/NanoQL.Tests/Divergences/BachelierPutBranchTests.cs)

The Bachelier calculator's put branch. QuantLib tells a call from a put by `alpha >= 0` (`bacheliercalculator.cpp:102-114`, `:183-188`), and a put's alpha, N(d) − 1, rounds to 0 where N(d) rounds to 1, so a put far out of the money prices as the call: F 100, K 50, σ√T 1, D 0.95 prices 47.5 where its value is 0, and at a zero deviation an out-of-the-money put (K 99) prices 0.95. nanoql tells them apart by the payoff's type. The checks: Bachelier's put formula, and QuantLib's own call through parity.

Facts:

- `AFarOutOfTheMoneyPutIsWorthBacheliersValue`
- `AnOutOfTheMoneyPutAtAZeroDeviationIsWorthNothing`

## BallandGeometricMeanTests

[`Divergences/BallandGeometricMeanTests.cs`](../tests/NanoQL.Tests/Divergences/BallandGeometricMeanTests.cs)

Balland's and the iterative Balland's geometric mean (fixed). Both evolvers take `sqrt(f·f₀)` with f₀ the model's time-0 forward at every step, undisplaced (`lognormalfwdrateballand.cpp:127`, `lognormalfwdrateiballand.cpp:119`, `:135`), not the step's start, so the scheme doesn't converge to the model as the steps refine: on a flat 30% model of ten semi-annual rates its caplets sit 6.25 standard errors from Black at a million paths, and on a model at 4% its caplets are 0.15% to 0.97% above the predictor-corrector's on the same paths at four steps a period, 0.03% to 0.87% at one (QuantLib's form emulated, 65,536 paths); and the undisplaced product is negative once a displaced forward goes below 0, so the forward is NaN: 1,450 of 2,000 paths at displacement 2%, forwards 0.2%, volatility 30%. nanoql takes the geometric mean of the displaced forwards at the step's start and end, sqrt((f_end + d)(f_start + d)) − d. Checks: on an evolution of one step from time 0, undisplaced, QuantLib's arithmetic (the vectors), bit for bit; on multi-step evolutions the caplets within 4 standard errors of Black, within 0.05% of the predictor-corrector's at four steps a period (the fix 0.010% at most), and converging to them as the steps refine; no NaN path displaced; and a set initial state, whose forwards QuantLib's mean ignores, the evolution of a model starting there, bit for bit.

Facts:

- `TheCapletsConvergeToTheModelsAsTheStepsRefine`
- `ASetStateIsTheEvolutionOfAModelStartingThere`
- `ADisplacedForwardBelowZeroStaysANumber`

## BaroneAdesiWhaleyCriticalPriceTests

[`Divergences/BaroneAdesiWhaleyCriticalPriceTests.cs`](../tests/NanoQL.Tests/Divergences/BaroneAdesiWhaleyCriticalPriceTests.cs)

Barone-Adesi-Whaley's critical price where its Newton iteration leaves its range. A put's step can leave (0, K) and a call's seed or step (K, ∞), and QuantLib's next Black formula throws "forward + displacement (0 + 0) must be positive" (`baroneadesiwhaleyengine.cpp:59-130`); Ju's engine calls the same critical price (`juquadraticengine.cpp:126`) and throws with it (QuantLib-Python 1.44). nanoql takes the root of QuantLib's own equation over the range, by Brent's method. QuantLib's check: the equation's residual at the root, within QuantLib's own tolerance, and its FD engine (2,000 × 2,000) on the option. Where Newton stays in range the critical price is QuantLib's, bit for bit (the engines' vectors).

Facts:

- `APutWhoseNewtonStepLeavesZeroToKIsPricedOnTheEquationsRoot`
- `ACallWhoseSeedLeavesKToInfinityIsPricedOnTheEquationsRoot`
- `JusEngineSharesTheRoot`
- `TestBaroneAdesiWhaleyAndJuWithCarryAboveVolatility`

## BarrierClosedFormMismatchedTimesTests

[`Divergences/BarrierClosedFormMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/BarrierClosedFormMismatchedTimesTests.cs)

The barrier family's closed forms on structures that measure time differently (fixed). QuantLib's double-barrier, double-barrier binary, partial-time, soft and two-asset engines read each rate as its curve's zero rate over the risk-free curve's time and the volatility over the same time, and combine them as though every structure shared that clock, so on an Actual/360 risk-free curve beside an Actual/365 Fixed volatility they are off their own values on the market restated onto one clock with the same discounts and variances by date (−3.33%, +0.88% and +1.64% on the double barrier, the partial-time and the soft barrier). nanoql reads each rate on the volatility's clock, the curve's zero rate times its own time over the volatility's, and each time on it, as the analytic barrier engine reads them; the two-asset engine on the clock across both processes, the first's risk-free day counter. QuantLib's check: its own engine on the market restated onto that clock, each flat structure's rate or volatility scaled so its discounts or variances by date are unchanged, which nanoql's engine prices with QuantLib's bits (the vectors).

Facts:

- `TheDoubleBarrierPricesAsOnTheRestatedMarket`
- `TheDoubleBarrierBinaryPricesAsOnTheRestatedMarket`
- `ThePartialTimeBarrierPricesAsOnTheRestatedMarket`
- `TheSoftBarrierPricesAsOnTheRestatedMarket`
- `TheTwoAssetBarrierPricesAsOnTheRestatedMarket`

## BarrierMismatchedTimesTests

[`Divergences/BarrierMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/BarrierMismatchedTimesTests.cs)

The analytic barrier engine on structures that measure time differently (fixed). QuantLib reads every structure by the exercise date (`analyticbarrierengine.cpp:128-170`), each rate over its own curve's time and the variance over the volatility's, and combines them as though they shared a clock: on a 5% Actual/360 risk-free curve a one-year down-and-out call at 90, strike 100, rebate 2, is 9.489629845557584 against 9.494843368487192 on the Actual/365 Fixed curve with the same discount factors, and in-out parity fails by 3.7e-5; on a risk-free curve settling two days later, or beside a dividend curve and volatility dated later, it is off its own engine on the market restated from the earliest date by up to 2.6e-3; and an exercise before the risk-free curve's date throws "negative time" (QuantLib-Python 1.44). nanoql reads each rate on the volatility's clock, the curve's zero rate times its own accrual time to the exercise over the volatility's, and each discount and the variance as accrued, nothing before a structure's own date (the playbook's row for structures on another clock): a factor of 1.0, and QuantLib's bits, where the clocks agree; the curve's own rate where the volatility accrues nothing, an option expiring on the evaluation date. QuantLib's check: its own engine on the market restated from the earliest date, each restated structure on Actual/365 Fixed (a curve dated later a discount curve at 1 up to its date, a volatility dated later a variance curve at 0 up to its date), and its master's value at expiry.

Facts:

- `AnAct360MarketPricesAsQuantLibOnTheEquivalentCurves`
- `InOutParityHoldsOnMixedDayCounters`
- `AnOptionExpiringOnTheEvaluationDateIsPricedAtExpiry`
- `AnImpliedVolatilityOnMixedDayCountersIsTheOneOnTheEquivalentCurves`
- `OnACurveSettlingLaterTheEngineIsQuantLibsOnTheRestatedMarket`
- `OnStructuresDatedLaterTheEngineIsQuantLibsOnTheRestatedMarket`
- `AnExerciseBeforeTheRiskFreeCurvesDateIsPriced`
- `AnImpliedVolatilityOnACurveSettlingLaterIsTheOneOnTheRestatedMarket`

## BarrierNegativeRateRebateTests

[`Divergences/BarrierNegativeRateRebateTests.cs`](../tests/NanoQL.Tests/Divergences/BarrierNegativeRateRebateTests.cs)

A knock-out's rebate under negative rates (fixed). QuantLib's analytic engine takes `λ = √(μ² + 2r/σ²)` (`analyticbarrierengine.cpp:243-264`), the square root of a negative number where r < 0 and μ² < −2r/σ², so a knock-out with a rebate is NaN: at r −1%, q −1.5% and a volatility of 20%, a one-year down-and-out call at 90, strike 100, with 5 paid at the hit, where QuantLib's finite-difference engine gives 9.889092750723153 (QuantLib-Python 1.44). Where λ² < 0, nanoql's rebate term is the rebate's present value at the hit, `R ∫₀ᵀ e^(−rt) f(t) dt`, f the first-passage density of the log-price to the barrier, integrated by `GaussLobattoIntegral`; with the spot on the barrier it is the rebate, paid now. Where λ² ≥ 0 QuantLib's closed form stands, with its bits. QuantLib's checks: its closed form, the same quantity where it works, and its finite-difference engine (400 × 800) where it doesn't, within that engine's own grid error.

Facts:

- `WhereLambdaIsRealTheIntegralIsQuantLibsClosedForm`
- `WhereLambdaIsNotRealTheRebateIsQuantLibsFiniteDifferenceValue`
- `WithTheSpotOnTheBarrierTheRebateIsPaidNow`

## BarrierNegativeRebateTests

[`Divergences/BarrierNegativeRebateTests.cs`](../tests/NanoQL.Tests/Divergences/BarrierNegativeRebateTests.cs)

A negative rebate (fixed). QuantLib's analytic barrier engine pays a rebate only where `rebate() > 0` (`analyticbarrierengine.cpp:227`, `:244`), so a rebate of −3 prices as none: on a one-year call struck at 100 (spot 100, r 5%, q 2%, volatility 25%) its four barrier types are 2.0 to 2.1 from QuantLib's finite-difference engine, which prices the rebate (QuantLib-Python 1.44). nanoql tests `rebate != 0`, which leaves zero and positive rebates on QuantLib's bits; the Vanna-Volga engine and the implied volatility, which price through the analytic engine, follow. QuantLib's checks: its finite-difference engine, and the price's linearity in the rebate.

Facts:

- `ANegativeRebateIsPricedAsQuantLibsFiniteDifferenceEngineDoes`
- `AVannaVolgaKnockOutsNegativeRebateIsLinearThroughZero`
- `AnImpliedVolatilityWithANegativeRebateRepricesItsTarget`

## BarrierUpstreamFixesTests

[`Divergences/BarrierUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/BarrierUpstreamFixesTests.cs)

The fixes QuantLib's master made to the barrier family after the pin, which nanoql takes, each with the master's own case. #2876 (`5f4811e7e`): a start-window knock-in put prices the European on the call payoff and process the put-call symmetry builds, where 1.44 prices the put's, so in-out parity fails off the money. #2880 (`ca8e70946`): Dr78's degenerate correlations, taken with the bivariate normal; the partial-time engine finite where d2 is near 0 on a term-structure market. #2881 (`014321f39`): double knock-outs struck outside the corridor integrate only over terminal spots inside it, and price 0 beyond the far barrier; and a one-term series at low volatility. #2883 (`b9bb7bedb`): the soft barrier against the average of single-barrier options with barriers in [L, U], by Simpson's rule over QuantLib's analytic barrier engine; the master refuses the directions Haug's form doesn't cover, which nanoql prices by the average, so the case asserts the average there too. #2878 (`eaeac5651`): the binomial barrier asset's American window bounded by `isOnTime` at both ends, so the exercise at expiry isn't skipped.

Facts:

- `TestInOutParity`
- `TestAnalyticEngineNearZeroD2`
- `TestStrikesOutsideCorridor`
- `TestLowVolatility`
- `TestSoftBarrierDirection`
- `TestBinomialExerciseAtMaturity`

## BarrierZeroVolatilityTests

[`Divergences/BarrierZeroVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/BarrierZeroVolatilityTests.cs)

The analytic barrier engine at zero volatility (taken from QuantLib's master ahead of the pin). QuantLib 1.44 divides by the zero standard deviation (`analyticbarrierengine.cpp:181-238`): a one-year up-and-out put at 107, strike 105, rebate 4, prices NaN (QuantLib-Python 1.44). QuantLib's master fixed it after the pin (#2853, `e29c64126`, merged as `2768fafdc` on 2026-10-14): with a variance below machine epsilon the underlying follows its forward, a spot on the barrier counts as a hit, a knock-in pays the vanilla payoff or the rebate at maturity, and a knock-out its rebate at the hit or the vanilla payoff. This is master's `testZeroVolatility`, until the pin moves to a release that carries it.

Facts:

- `TestZeroVolatility`

## BaseCorrelationLossModelTests

[`Divergences/BaseCorrelationLossModelTests.cs`](../tests/NanoQL.Tests/Divergences/BaseCorrelationLossModelTests.cs)

The base-correlation model after a default: its equity baskets hold the live names, their notionals and recoveries, where QuantLib's take the live notionals on the whole pool and its basket refuses ("unmatched data entry sizes in basket"). A (10) defaulted in June 2006 and settled at 0.25, B (20) and C (30) alive, [10%, 50%] from 1 January 2006, recoveries 0.3, 0.5, 0.6, the Gaussian LHP base on a flat surface of 0.3: QuantLib's model on the basket of the live names (B and C over [0, 45%]) reads 2.2611633149989752 at five years.

Facts:

- `TheModelPricesTheLiveNames`
- `AnEquityTranchesAttachmentLegIsZero`

## BasisSystemFunctionCountTests

[`Divergences/BasisSystemFunctionCountTests.cs`](../tests/NanoQL.Tests/Divergences/BasisSystemFunctionCountTests.cs)

The basis systems' function counts (fixed). QuantLib adjusts only the last exercise's count (`swapbasissystem.cpp:46`, `swapforwardbasissystem.cpp:53`, `:56`), so an earlier exercise in the last periods reports more functions than its own values give: on six semi-annual rates, a swap basis system exercised at 2.6 and 2.8 counts 3 and 2 where its values give 2 and 2, and a swap-forward basis system at every rate time but the last counts 10 at its fifth exercise where its values give 6. nanoql counts each exercise's functions from its own rate, as its values decide, which the node-data collection and the Longstaff-Schwartz strategy size their buffers by. Check: QuantLib's values' lengths.

Facts:

- `EachExercisesCountIsItsValues`

## BasketConstructionOrderTests

[`Divergences/BasketConstructionOrderTests.cs`](../tests/NanoQL.Tests/Divergences/BasketConstructionOrderTests.cs)

A basket's remaining tranche computed after its attachment and detachment amounts: QuantLib's constructor computes the basket's evaluation-date state before it sets the amounts (`basket.cpp:57-68`), so the remaining amounts read 0 and 0 until the first statistic recomputes them. QuantLib's values are recorded in the test; the check is QuantLib's values after its first statistic.

Facts:

- `TheRemainingTrancheIsSetAtConstruction`

## BasketExpiryBeforeReferenceDateTests

[`Divergences/BasketExpiryBeforeReferenceDateTests.cs`](../tests/NanoQL.Tests/Divergences/BasketExpiryBeforeReferenceDateTests.cs)

The basket engines that read their structures by date on an option expiring before a later structure's date. QuantLib reads each discount, variance and deviation by date from the structure's own reference date (`vectorbsmprocessextractor.cpp:48`, `:71`, `:80`, `:89-90`, which Choi's, the single-factor and the n-dimensional engines read, and `choibasketengine.cpp:174-188`, Choi's nodes), so an option expiring before a risk-free curve's or a volatility's date is refused with "negative time". Each read here accrues nothing before its structure's date (the playbook's row for structures on another clock): the option is priced as QuantLib prices the market restated from the evaluation date, a curve's discount 1 and a volatility's variance 0 up to its date, and as nanoql's 2-D finite-difference engine prices it on its clock.

Facts:

- `ARiskFreeCurveDatedAfterExpiryDiscountsNothing`
- `AVolatilityDatedAfterExpiryAccruesNoVariance`

## BasketLiveReadsTests

[`Divergences/BasketLiveReadsTests.cs`](../tests/NanoQL.Tests/Divergences/BasketLiveReadsTests.cs)

A basket's live reads by the live names, not by the live list's positions: QuantLib's `remainingNotionals(d)` and `remainingProbabilities(d)` read the first live-list-length positions of the pool (`basket.cpp:196-220`), so after the first of three names defaults they read the first two names. QuantLib's values are recorded in the test; the check is QuantLib on the basket of the live names alone, whose reads are those names' notionals and curves.

Facts:

- `TheLiveReadsTakeTheLiveNames`

## BasketPayoffEmptyTests

[`Divergences/BasketPayoffEmptyTests.cs`](../tests/NanoQL.Tests/Divergences/BasketPayoffEmptyTests.cs)

The minimum and maximum basket payoffs on no prices. QuantLib dereferences `std::min_element` and `std::max_element` of the empty range, its end (`basketoption.hpp:56-58`, `:65-67`), undefined. nanoql refuses an empty basket (the playbook's undefined-behaviour row).

Facts:

- `AnEmptyBasketIsRefused`

## BasketRealizedDefaultsTests

[`Divergences/BasketRealizedDefaultsTests.cs`](../tests/NanoQL.Tests/Divergences/BasketRealizedDefaultsTests.cs)

A basket with realized defaults restated on its live names: the lost notional writes the tranche down from its attachment and the recovered notional amortizes it from its detachment's side, the model prices the live names over the restated tranche and the realized tranche loss is added; the event counts take the order less the realized defaults. QuantLib's adds the whole settled loss to the expected tranche loss, tranches the live loss against min(D, A + max(0, L − A)) where its header means max(0, A − L), and counts the defaulted name among the live ones (`basket.cpp:292-299`, `:325-328`, `:345-369`). On A (10) and B (20) alive and C (30) defaulted and settled at 0.4, QuantLib's values are recorded in the test; the checks are the tranche's loss enumerated over the live names' defaults at independence, and QuantLib on the restated basket (the live names, the restated ratios).

Facts:

- `TheTrancheIsRestatedOnTheLiveNames`
- `TheExpectedTrancheLossIsTheTranchesLoss`
- `TheEventCountsTakeTheOrderLessTheRealizedDefaults`
- `TheNameReadsTakePoolPositions`
- `AFirstNameDefaultedAtItsOwnRecoveryLeavesTheLiveNamesPrices`
- `TheProbabilityOverALossReadsTheWholeTranche`
- `ThePercentileAndShortfallAddTheRealizedTrancheLoss`
- `TheLossDistributionIsThePools`
- `AProgrammedDefaultIsntRealized`

## BasketZeroVolatilitiesTests

[`Divergences/BasketZeroVolatilitiesTests.cs`](../tests/NanoQL.Tests/Divergences/BasketZeroVolatilitiesTests.cs)

Choi's and the n-dimensional finite-difference basket engines where every volatility is 0. QuantLib's fails on paths it doesn't intend: Choi's single-factor nodes take their intrinsic branch, which sets no root (`singlefactorbsmbasketengine.cpp:186`), and its read of the node's "d" throws "invalid map<K, T> key" (`choibasketengine.cpp:217`); the n-dimensional engine meshes every principal direction at √0 (`fdndimblackscholesvanillaengine.cpp:155-165`) and fails with "vector too long". At volatilities of 1e-8 both give the deterministic value, the discounted payoff at the forward basket. Here Choi's nodes take the root's limit, ±∞ by the side of the strike the forward basket is on, and the n-dimensional mesh is floored at QL_EPSILON²: the deterministic value, the single-factor engine's intrinsic and QuantLib's engines at 1e-8 are the checks.

Facts:

- `ChoiPricesTheDeterministicValue`
- `TheNdimEnginePricesTheDeterministicValue`

## BatesDetJumpSmallReversionTests

[`Divergences/BatesDetJumpSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/BatesDetJumpSmallReversionTests.cs)

The deterministic-intensity Bates engines at small κ_λ. Their term weights the constant-intensity term by the intensity's average, (x − 1 + e^{−x})θ_λ/(xλ) + (1 − e^{−x})/x at x = κ_λt, which QuantLib writes as differences that cancel as x falls (`batesengine.cpp:80`, `:140`): with θ_λ 0.6 beside λ 0.3 a call prices 9.8118999810864693 at κ_λ = 1e-12 where the constant-intensity engine, its limit, gives 9.8119169388898158 (QuantLib 1.44). Below x = 1e-2 nanoql writes the weights as x·φ₂(−x) and −expm1(−x)/x, which don't cancel, QuantLib's expressions and bits at and above it. Checks: the limit, and, since the term is linear in the intensity, the price equal to the constant-intensity engine's at λ_eff = θ_λ·w₁ + λ·w₂, the weights w₁ = (x − 1 + e^{−x})/x and w₂ = (1 − e^{−x})/x evaluated in 50 digits (Python's `decimal`), either side of the cut.

Facts:

- `TheDetJumpPriceTendsToTheConstantIntensityOne`
- `TheWeightsAreTheirFiftyDigitValues`

## BatesDoubleExpUpJumpTests

[`Divergences/BatesDoubleExpUpJumpTests.cs`](../tests/NanoQL.Tests/Divergences/BatesDoubleExpUpJumpTests.cs)

The double-exponential Bates models where the up-jumps' mean size η_u reaches 1 (a refusal). The up-jumps' exponential moment, which the model's drift compensates, is finite for η_u below 1 only ("the condition η1 > 1 is necessary and sufficient for the random variable J_t to have finite expectation", Gulisashvili and Vives, η1 = 1/η_u), yet QuantLib's engine prices beyond it: a 100 call 3,040,812 at η_u = 1.01, a plausible 26.39 at 1.5, NaN at 1 (QuantLib 1.44). nanoql's engines refuse a model at η_u ≥ 1, the models' constraint on η_u is (0, 1), so a construction or a calibration step there is refused, and below 1 QuantLib's values stand.

Facts:

- `AnUpJumpMeanOfOneOrMoreIsRefused`
- `TheConstraintRefusesACalibrationStepThere`
- `BelowOneQuantLibsValuesStand`

## BehrensFisherHighOrderTests

[`Divergences/BehrensFisherHighOrderTests.cs`](../tests/NanoQL.Tests/Divergences/BehrensFisherHighOrderTests.cs)

The convolved Student t past order 171, its factorial and power scaled together: QuantLib's series divides a factorial by a power, both of which leave the doubles from degree 172 on (`convolvedstudentt.cpp:124-127`), so on two t's of order 173 its cumulative reads −∞ and its inverse refuses ("root not bracketed"). QuantLib's values are recorded in the test; the check is the same series in 50 digits, within the tolerance: the scaled terms carry the logarithms' rounding through the series' alternating sum, 4e-12 at most here.

Facts:

- `TheSeriesIsAccuratePastOrder171`
- `TheInverseFindsItsRoot`

## BetaRiskTests

[`Divergences/BetaRiskTests.cs`](../tests/NanoQL.Tests/Divergences/BetaRiskTests.cs)

BetaRisk's event process: the inter-arrival times cumulated, a Poisson process, where QuantLib draws each event's time afresh, a geometric count whose dates come unordered. At a two-year return period over three years (2 January 2015 to 2018, maximum loss 100, mean 10, standard deviation 15) QuantLib's mean count reads 3.48466 for λT = 1.5, and 51,727 of 10⁵ paths unordered; on `testBetaRisk`'s risk (return period 100 years) QuantLib reads a count mean of 0.030433 and variance 0.031354832510999998, a loss mean of 0.30134899766945406 and variance 9.7413702370590478 over 10⁶ paths, its checks at 1% passing on its seed. QuantLib's draws are kept up to the parting path: of its first 24 paths with events, only the two with two events move, their second date alone.

Facts:

- `TheCountIsPoissonsAtAHighRate`
- `TheCountIsPoissonsOnTestBetaRisksRisk`
- `AMultiEventPathMovesItsLaterDatesOnly`

## BiCGstabLastIterationTests

[`Divergences/BiCGstabLastIterationTests.cs`](../tests/NanoQL.Tests/Divergences/BiCGstabLastIterationTests.cs)

`BiCGstab` converging in its last allowed iteration. QuantLib's final checks test the iteration count before the error (`bicgstab.cpp:87-88`): a full step that meets the tolerance at the bottom of the last iteration still increments the count to the maximum, so the solve is refused, "max number of iterations exceeded" (a half step that meets it breaks out before the increment, and passes). nanoql tests the error first, QuantLib's own criterion; the count's refusal keeps its text, and a breakdown short of the tolerance keeps QuantLib's "could not converge".

Facts:

- `ASystemSolvedInTheLastIterationIsSolved`
- `ABreakdownShortOfTheToleranceIsRefused`

## BinaryBarrierRebateTests

[`Divergences/BinaryBarrierRebateTests.cs`](../tests/NanoQL.Tests/Divergences/BinaryBarrierRebateTests.cs)

The binary barrier engine's rebate (fixed). The option takes a rebate and QuantLib's engine never reads it: a down-and-out cash-or-nothing call, cash 10, strike 100, barrier 90 at r 5%, q 1%, σ 20% over a year prices 3.678406488567 at rebates 0 and 5. nanoql prices it by the analytic barrier engine's rebate terms, a knock-out's paid at the hit and a knock-in's at expiry if never hit. Checks: QuantLib's routes for the same payments, a one-touch at the hit by its analytic digital American engine (2.786734823341 below 90, 3.267438031090 above 110 for 5) and a strike-0 cash-or-nothing knock-out by this engine (2.060934934335 and 1.600028420441). A knock-out already hit is worth its rebate, paid now, where QuantLib's engine prices 0: its check QuantLib's one-touch at the same spot, 5.000000000000 on and beyond the barrier, and the engine's own value a hair inside, 5.000015883 at 90.0001.

Facts:

- `AKnockOutsRebateIsAOneTouchAtTheHit`
- `AKnockInsRebateIsANoTouchAtExpiry`
- `AKnockOutAlreadyHitIsWorthItsRebate`

## BinaryBarrierZeroVolatilityTests

[`Divergences/BinaryBarrierZeroVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/BinaryBarrierZeroVolatilityTests.cs)

The binary barrier engine below machine-epsilon variance (fixed). QuantLib's indicators read the spot and its power of the barrier overflows to 0, so a down-and-out cash-or-nothing call whose forward stays above the barrier prices 0 at σ of 0 and 1e-8, where its limit is cash × D = 9.512294 and σ 1e-4 gives 9.51229425. nanoql follows the forward, which hits the barrier, if at all, when it reaches it, as QuantLib's master prices the analytic barrier engine (#2853). Checks: the engine at σ 1e-4, and #2853's rule on a forward that falls through the barrier.

Facts:

- `TheUnderlyingFollowsItsForward`

## BinomialBarrierMismatchedTimesTests

[`Divergences/BinomialBarrierMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/BinomialBarrierMismatchedTimesTests.cs)

The binomial barrier engine's tree on structures that measure time differently (fixed). QuantLib flattens each curve from the risk-free curve's reference date on its own day counter and runs the tree over the risk-free curve's time (`binomialbarrierengine.hpp:99-125`), so a volatility on another day counter integrates the wrong variance. nanoql's tree is `BinomialVanillaEngine`'s: on the risk-free curve's day counter from the earliest reference date, each structure on another day counter flattened on it from its variance or discount at maturity. QuantLib's check: its own engine on the market restated onto the risk-free curve's day counter, the volatility scaled so its variances by date are unchanged; and a risk-free curve settling two days after the others, flattened from its own date on the tree from the earliest, where QuantLib's tree starts at the risk-free curve's date.

Facts:

- `AnAct365VolatilityBesideAnAct360CurvePricesAsOnTheRestatedMarket`
- `ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock`

## BinomialDoubleBarrierMismatchedTimesTests

[`Divergences/BinomialDoubleBarrierMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/BinomialDoubleBarrierMismatchedTimesTests.cs)

The binomial double-barrier engine's tree and steps (fixed). QuantLib flattens each structure from the risk-free curve's reference date on its own day counter and runs the tree over the risk-free curve's time (`binomialdoublebarrierengine.hpp:73-117`), so a volatility on another day counter integrates the wrong variance; and it builds the grid and the lattice on the step count given where the Leisen-Reimer and Joshi trees build on one more at an even count, its 200 steps 3.8467695315 against 201's 3.8822505099 on a Leisen-Reimer tree (QuantLib 1.44). nanoql's tree is the binomial barrier engine's and its grid and lattice take the tree's own steps. Checks: the market restated onto the risk-free curve's day counter; the tree's steps.

Facts:

- `AnAct365VolatilityBesideAnAct360CurvePricesAsOnTheRestatedMarket`
- `AnEvenCountOnALeisenReimerTreeIsTheTreesOddOne`

## BinomialEvenStepsTests

[`Divergences/BinomialEvenStepsTests.cs`](../tests/NanoQL.Tests/Divergences/BinomialEvenStepsTests.cs)

The Leisen-Reimer and Joshi trees on an even step count. Both build their tree on the next odd count (`binomialtree.cpp:106-109`, `:148-151`), while QuantLib's engine builds its time grid and its lattice on the count given (`binomialengine.hpp:111-116`, `bsmlattice.hpp:77-79`), so it rolls back a tree built for n + 1 steps over n: at 250 steps a one-year at-the-money call reads 9.1899281 on the Leisen-Reimer tree, 3.7e-2 below the analytic engine's 9.2270055, where at 251 it reads 6.0e-6 below. nanoql's engine takes its count from the tree it built, so at 250 steps it computes QuantLib's engine at 251, step for step; every odd count and every other tree is QuantLib's.

Facts:

- `AnEvenCountRollsBackTheTreeItBuilt`

## BinomialLossModelTests

[`Divergences/BinomialLossModelTests.cs`](../tests/NanoQL.Tests/Divergences/BinomialLossModelTests.cs)

The adjusted binomial's weight: mixed with the two-point mass at the floor and ceiling of the expected count, the binomial is weighted so that the conditional distribution has the pool's mean and variance, O'Kane's defining property; QuantLib's weight takes −(c − m)² − ((f − m)² − c²)(c − m) for the two-point mass's variance, so its distribution misses the variance it exists to match. On three names (notionals 10, 20, 30, hazards 0.02, 0.03, 0.015, recoveries 0.4, 0.3, 0.5, correlation 0.3) QuantLib's binomial reads the whole pool's expected loss 3.6067712419499616 at five years, its mean, which no weight moves, and the [10%, 40%] tranche's 2.0417230578123919, where the exact recursive model reads 1.8818433951861697. And the loss distribution integrates a vector over the factor, where QuantLib's quadrature leaves its last node unweighted (a fix): nanoql's is the scalar route's, point by point.

Facts:

- `TheConditionalDistributionHasThePoolsMeanAndVariance`
- `QuantLibsWeightMissesThePoolsVariance`
- `TheWholePoolsExpectedLossIsTheMeansWhateverTheWeight`
- `ATranchesExpectedLossReadsTheWeight`
- `TheLossDistributionIsTheScalarRoutes`
- `ThePercentileAtOneIsATrancheLoss`
- `ARealizedDefaultLeavesTheLiveNames`

## BinomialMismatchedTimesTests

[`Divergences/BinomialMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/BinomialMismatchedTimesTests.cs)

The binomial engine on structures that measure time differently. QuantLib reads the risk-free rate, the dividend yield and the volatility at the maturity date, each on its own day counter, flattens each on that day counter, and runs the tree over the risk-free curve's time to maturity (`binomialengine.hpp:84-105`), so a dividend curve or a volatility on another day counter or reference date accrues over the wrong time: a one-year put on a dividend curve and a volatility on Act/360 beside a risk-free curve on Act/365 reads 8.6306060 on 801 Cox-Ross-Rubinstein steps, against QuantLib's analytic engine's 8.7101995, which reads every structure by date. nanoql runs the tree on the risk-free curve's day counter from the earliest reference date of the structures, as the finite-difference engine's grid, and flattens such a structure on it, its rate or volatility the one with its discount or variance at the maturity date: QuantLib's own engine on the market restated so, each structure accruing nothing before its own date. A market whose structures share the risk-free curve's day counter and reference date is QuantLib's.

Facts:

- `StructuresOnAnotherDayCounterAreReadByDate`
- `StructuresOnAnotherReferenceDateAreReadByDate`
- `StructuresDatedAfterTheRiskFreeCurveAccrueNothingBeforeTheirDate`
- `StructuresSharingTheRiskFreeTimesAreQuantLibs`

## BisectionTests

[`Divergences/BisectionTests.cs`](../tests/NanoQL.Tests/Divergences/BisectionTests.cs)

The bisection's exit on a midpoint whose value is close to 0: QuantLib returns its running root, the bracket's low side, where the midpoint's value is above 0, so x − 1.5 + 1e-30 on [0, 3] returns 0; nanoql returns the midpoint, 1.5. Where the midpoint's value is at or below 0 the two are QuantLib's own exit. QuantLib's value is its own.

Facts:

- `AMidpointCloseToZeroAboveItIsTheRoot`
- `AMidpointAtOrBelowZeroIsQuantLibsExit`

## BivariateDr78DegenerateCorrelationsTests

[`Divergences/BivariateDr78DegenerateCorrelationsTests.cs`](../tests/NanoQL.Tests/Divergences/BivariateDr78DegenerateCorrelationsTests.cs)

QuantLib's fix after v1.44, #2880 (`ca8e70946`), to Drezner's bivariate normal, taken as a fix, with its three cases in `test-suite/distributions.cpp` on master, here until the pin moves: at ρ = ±1, where QuantLib 1.44 divides by zero, the probabilities are Φ(min(a, b)) and max(0, Φ(a) + Φ(b) − 1); near zero bounds the recursion's derived correlations, by hypot forms, stay continuous and symmetric; and near |ρ| = 1 a one-ulp overshoot of a derived correlation is clamped, where QuantLib 1.44 throws.

Facts:

- `TestBivariateDr78DegenerateCorrelations`
- `TestBivariateDr78NearZero`
- `TestBivariateDr78NearDegenerateCorrelations`

## BivariateInfiniteArgumentTests

[`Divergences/BivariateInfiniteArgumentTests.cs`](../tests/NanoQL.Tests/Divergences/BivariateInfiniteArgumentTests.cs)

West's bivariate normal at an infinite bound. QuantLib's integrand meets ∞ − ∞ there (`bivariatenormaldistribution.cpp:166-176`, `eqn3`'s exponent, and `eqn6`'s beyond |ρ| = 0.925): NaN at +∞ for ρ > 0 and at −∞ for ρ < 0, and wherever the other bound is 0, where Drezner's returns Φ(b) or 0. nanoql returns the other bound's Φ at +∞ and 0 at −∞, in either argument: QuantLib's own value at ±40 is the check.

Facts:

- `APlusInfiniteBoundIsTheOtherBoundsMarginal`
- `AMinusInfiniteBoundIsNothing`

## BivariateStudentTests

[`Divergences/BivariateStudentTests.cs`](../tests/NanoQL.Tests/Divergences/BivariateStudentTests.cs)

The bivariate Student t-distribution beside the line h = ρk at |ρ| = 1: QuantLib's x(m, h, k) returns 0 wherever its denominator is below 1e-8, which flips x from 1 to 0 within about 1e-4 of the line, so its value there is off by up to 1.9e-5 and can be negative; nanoql takes the limit only at an exact 0/0. The references are the distribution's limits at ρ = ±1, F(min(h, k)) at ρ 1 and max(0, F(h) − F(−k)) at −1 (QuantLib's own boundary test's formulas), here ½ and 0; QuantLib's values are its own.

Facts:

- `BesideTheLineAtRhoOneTheLimitsHold`
- `AwayFromTheLineQuantLibsValuesAreKept`

## BjerksundStenslandMismatchedTimesTests

[`Divergences/BjerksundStenslandMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/BjerksundStenslandMismatchedTimesTests.cs)

Bjerksund-Stensland's Greeks on structures dated differently. QuantLib's American branch takes the dividend, risk-free and volatility times from the risk-free curve's reference date (`bjerksundstenslandengine.cpp:248-251`, `:320`, `:366`), so where a curve settles T+2 the rate sensitivity of the other runs over the wrong time. nanoql takes each structure's time from its own reference date. QuantLib's check: its own NPVs bumped by ±1e-6 in the rate, which nanoql's rho and dividend rho match to the difference's own accuracy (QuantLib-Python 1.44). Where the structures share their dates, QuantLib's bits (the engine's vectors).

Facts:

- `ADividendCurveSettlingLaterTakesItsOwnTime`
- `ARiskFreeCurveSettlingLaterTakesItsOwnTime`

## BjerksundStenslandOverflowTests

[`Divergences/BjerksundStenslandOverflowTests.cs`](../tests/NanoQL.Tests/Divergences/BjerksundStenslandOverflowTests.cs)

Bjerksund-Stensland's phi family where a power overflows against an underflowing normal tail. `phi` returns e^λ·[N(d) − (I/S)^κ·N(d − 2 ln(I/S)/√v)] (`bjerksundstenslandengine.cpp:37-48`); on puts with q ≫ r and a long expiry the symmetric call's (I/S)^κ overflows while the N factor underflows, the product is NaN, and the European floor misses it: QuantLib prices puts at σ 5%, 30 years, r 0.1%, q 30% NaN (QuantLib-Python 1.44). The Greeks' functions carry the same powers beside erfc and Gaussian densities. nanoql takes each such product as one exponent, through log N's tail, where the power isn't finite or the tail is 0. QuantLib's check: its FD engine (2,000 × 2,000) on the value, delta and gamma; the other Greeks against the engine's own NPVs bumped by ±1e-6; QuantLib's bits where the guard doesn't fire.

Facts:

- `TheValueDeltaAndGammaAreTheFdEngines`
- `TheOtherGreeksAreTheBumpedValues`
- `WhereTheGuardDoesntFireTheBitsAreQuantLibs`

## BjerksundStenslandTriggerTests

[`Divergences/BjerksundStenslandTriggerTests.cs`](../tests/NanoQL.Tests/Divergences/BjerksundStenslandTriggerTests.cs)

Bjerksund-Stensland where its 1993 trigger falls below its lower bound B₀. Where bT + 2√v < 0 the trigger's exponent is positive and I falls below B₀ (`bjerksundstenslandengine.cpp:196-206`): an infeasible exercise boundary, on which QuantLib's closed form falls below its European value, which its floor then returns, up to 3.4 below its own FD engine on its own Greeks case's inputs. nanoql puts the trigger at the perpetual boundary B∞ there: a flat boundary at or above B₀ is a feasible exercise rule, so its value is a lower bound of the American (Bjerksund and Stensland 2002, §3). QuantLib's check: its QD+ engine, which the trigger's value stays below and within 0.5% of, and its FD engine (1,000 × 1,000) recorded (QuantLib-Python 1.44, on `TestBjerksundStenslandAmericanGreeks`' market: q on Act/360, r on Act/365 (Fixed), the volatility on 30/360).

Facts:

- `BelowB0ThePerpetualTriggerGivesALowerBound`
- `AtOrAboveTheBoundaryTheOptionIsExercised`
- `TheValueJumpsAtTheRegionsEdge`
- `FarBelowTheForwardThePerpetualTriggerIsStillPriced`

## BjerksundStenslandUnsetGreeksTests

[`Divergences/BjerksundStenslandUnsetGreeksTests.cs`](../tests/NanoQL.Tests/Divergences/BjerksundStenslandUnsetGreeksTests.cs)

Bjerksund-Stensland's three Greeks it never sets. QuantLib's engine assigns its results from a local `OneAssetOption::results` it never resets (`bjerksundstenslandengine.cpp:123`, `:157`, `:183`), so the forward delta, elasticity and in-the-money probability it reports are uninitialized memory: denormals such as 7.756808686434e-312, 7.745284637023e-312 and 2.1219957905e-314 for a one-year put at 100, varying by run (QuantLib-Python 1.44). nanoql reports none, as every engine that doesn't compute one, in each of the engine's branches.

Facts:

- `TheForwardDeltaElasticityAndInTheMoneyProbabilityAreNone`

## BlackCalculatorItmProbabilityTests

[`Divergences/BlackCalculatorItmProbabilityTests.cs`](../tests/NanoQL.Tests/Divergences/BlackCalculatorItmProbabilityTests.cs)

`BlackCalculator`'s in-the-money probabilities for a put. QuantLib's `itmCashProbability()` and `itmAssetProbability()` return N(d2) and N(d1) for every payoff (`blackcalculator.hpp:129-135`), a call's, and the analytic engine reports the first for puts (`analyticeuropeanengine.cpp:105`): a put struck at 90 on a forward of 100 at 20% reads 0.665238, where QuantLib's own cash-or-nothing put values 0.334762. nanoql's put reads 1 − N(d2) and 1 − N(d1), the expressions QuantLib's cash-or-nothing and asset-or-nothing puts value, which check it.

Facts:

- `APutsProbabilitiesAreItsDigitalsValues`

## BlackCalculatorVannaVolgaTests

[`Divergences/BlackCalculatorVannaVolgaTests.cs`](../tests/NanoQL.Tests/Divergences/BlackCalculatorVannaVolgaTests.cs)

`BlackCalculator`'s vanna and volga. QuantLib's volga, vega × d1 × d2 / (σ√T), divides by the standard deviation where the derivative in σ divides by σ (`blackcalculator.cpp:511-523`), so it is the volga over √T: against the difference of QuantLib's own vega in σ, a call struck at 100 on a forward of 104 at 25% reads half of it at T = 0.25 and twice it at T = 4. And QuantLib's vanna and volga use the plain vanilla's closed form, through d1 and d2, for every payoff (`:494-523`): a cash-or-nothing call reads a vanna of 0.0054 where its vega's difference in the spot is −0.60, and a volga of −0.15 where it is 18.9. nanoql multiplies the volga by √T, and for payoffs other than the plain vanilla differentiates QuantLib's vega through the payoff's alpha and beta; the plain vanilla's are QuantLib's formulas, and QuantLib's own check is its vega's differences.

Facts:

- `TheVolgaIsTheVegasDerivativeInTheVolatility`
- `ADigitalsVannaAndVolgaAreItsVegasDerivatives`

## BlackCalculatorZeroVolatilityTests

[`Divergences/BlackCalculatorZeroVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/BlackCalculatorZeroVolatilityTests.cs)

`BlackCalculator`'s Greeks at zero volatility. QuantLib's zero-volatility branches of `delta`, `deltaForward` and `strikeSensitivity` tell a call from a put by `alpha_ >= 0` (`blackcalculator.cpp:214-228`, `:256-270`, `:438-452`), but alpha is 0 for an out-of-the-money put and for every cash-or-nothing payoff, which then get a call's Greeks: a put struck at 90 on a forward of 100 reads a forward delta of 1 and a delta of 1.0204 at a spot of 98, though its value is 0 at every nearby spot; a cash-or-nothing call struck at 100 on a forward of 110 reads 0.95, though its value doesn't move. The theta, theta per day, elasticity and forward elasticity read the deltas, and the rho and dividend rho the forward delta (`:287-310`, `:360-369`, `:395-418`), so QuantLib's put reads an elasticity of 1.8e308, a rho of 100 and a dividend rho of −100. nanoql computes the three from alpha and beta as the nonzero branch does, its derivative terms zero, so each of the nine reads its limit: the delta is QuantLib's own value's difference in the spot, and every result of a plain vanilla call is QuantLib's, but at a standard deviation of exactly ε, where QuantLib's constructor computes d2 (`:76`) and its Greeks take the zero-volatility branch: there an at-the-money call's strike sensitivity reads N(−ε/2) where QuantLib's reads ½, 1 ulp apart.

Facts:

- `AnOutOfTheMoneyPutsGreeksAreItsValuesLimits`
- `ACashOrNothingCallsGreeksAreItsValuesLimits`

## BlackCallableEngineTests

[`Divergences/BlackCallableEngineTests.cs`](../tests/NanoQL.Tests/Divergences/BlackCallableEngineTests.cs)

The Black engine on a bond whose one callability has occurred, and the option-adjusted spread through the Black engine. QuantLib's engine checks that the full schedule holds one callability, then reads the first of the live ones, those after the settlement date (`blackcallablebondengine.cpp:127-133`): when the one callability has occurred, that list is empty, and QuantLib reads past its end, which is undefined (the wheel read indeterminate memory, and happened to refuse "must have exercise Date >= settlement Date"). nanoql refuses with that text. The Black engine reads no spread (`blackcallablebondengine.cpp:125-175`), so the spread for a clean price can't be bracketed: a defined path, kept as QuantLib's, where the solver refuses "unable to bracket root".

Facts:

- `AnOccurredCallabilityIsRefusedWithQuantLibsText`
- `TheOASThroughTheBlackEngineCantBeBracketed`

## BlackCdsOptionEngineTests

[`Divergences/BlackCdsOptionEngineTests.cs`](../tests/NanoQL.Tests/Divergences/BlackCdsOptionEngineTests.cs)

The CDS option's time before the discount curve's date: floored at 0, the intrinsic value, where QuantLib's is negative and its standard deviation NaN. On 31 August 2006, a knock-out payer option exercising the next day on a five-year buyer swap from a month out (spread 0.0005, notional 10⁶, quarterly, TARGET, Actual/360), the hazard rate 0.001 flat from today and the discount curve 5% flat from two days out, QuantLib throws ("stdDev (-nan(ind)) must be non-negative"); the Black formula at a standard deviation of 0 on its swap's fair spread 0.00060383738773871025 and coupon-leg NPV −2212.1143600025548 reads 459.4003530439083. The same option not knocking out adds its front-end protection, read as the credit engines read a curve before its date: a discount of 1 and a default probability of 0, where QuantLib refuses at the time first.

Facts:

- `AnExerciseBeforeTheCurvesDateIsTheIntrinsicValue`
- `AKnockInsFrontEndProtectionReadsTheCurvesBeforeTheirDates`

## BlackFormulaNullPayoffTests

[`Divergences/BlackFormulaNullPayoffTests.cs`](../tests/NanoQL.Tests/Divergences/BlackFormulaNullPayoffTests.cs)

A null payoff in a Black formula's payoff overload. QuantLib's fifteen payoff overloads read the payoff's option type or strike through the pointer before anything else (`blackformula.cpp:105, 144, 195, 257, 330, 456, 564, 597, 628, 675, 709, 742, 767, 958, 981`), a null dereference, undefined under `std::shared_ptr` and a Boost assertion by default. nanoql refuses it first, with `ArgumentNullException` naming `payoff`.

Facts:

- `ANullPayoffIsRefusedBeforeItIsRead`

## BlackFormulaUpstreamFixesTests

[`Divergences/BlackFormulaUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/BlackFormulaUpstreamFixesTests.cs)

The fix QuantLib's master made to the exact Bachelier implied volatility after the pin, which nanoql takes as written: #2924 (`e61abd2f5`) returns 0 only where the time value isn't positive and the price is close to its intrinsic value. QuantLib 1.44 returns 0 wherever the price is close to the intrinsic value (`blackformula.cpp:919-920`), so a deep in-the-money price just above it, or a tiny real time value out of the money, inverts to 0 in place of its volatility. Of the vectors' 600 Bachelier implied volatilities, 23 leave 1.44's 0, which the generator leaves out of the file. In 14 the time value is real and tiny, and the fix recovers the generating volatility; in 9 a deep in-the-money price sits one ulp above its intrinsic value after the division by a discount of 0.95, and the fix inverts that rounding into a volatility of about 0.09, where the generating one is far smaller and 1.44 returns 0. At a discount of 1 those prices are exact, and the fix returns 0 too. One of each kind is below, from the vectors' inputs. Two of the vectors' argument cases move too: an infinite price after the division by the discount (a discount of 0, or an infinite price), which 1.44 finds close to the intrinsic value and returns 0 for, and which the fix's positive time value inverts to NaN, as QuantLib's master's arithmetic does. And a smile section's normal volatility, whose conversion inverts the section's price at a discount of 1 (`smilesection.cpp:131-155`): 16 of the flat, SABR and ZABR sections' vectors leave 1.44's 0, one of them below.

Facts:

- `TestBachelierImpliedVolJustAboveIntrinsic`
- `TestBachelierImpliedVolJustAboveIntrinsicDiscounted`
- `ATinyRealTimeValueInvertsToItsVolatility`
- `AnInTheMoneyPriceOneUlpAboveIntrinsicInvertsItsRounding`
- `AnInfinitePriceIsNoLongerZero`
- `ASmileSectionsTinyPremiumConvertsToItsNormalVolatility`

## BlackIborBelowShiftTests

[`Divergences/BlackIborBelowShiftTests.cs`](../tests/NanoQL.Tests/Divergences/BlackIborBelowShiftTests.cs)

The Black Ibor pricer's option struck below −shift on a lognormal volatility (its Ibor part): QuantLib's `BlackIborCouponPricer::optionletRate` calls `blackFormula` at the effective strike (`couponpricer.cpp`), which refuses a strike below −shift ("strike + displacement (-0.005 + 0) must be non-negative"), so a capped or floored Ibor coupon struck there, and a digital Ibor coupon whose replication strikes reach there, refuse; at −shift itself the formula prices the intrinsic. At or below −shift a caplet is the forward less the strike and a floorlet 0, the formula's limit there, on a forward above −shift; a forward at or below −shift reaches the formula and its refusal, as QuantLib's. QuantLib's check: its Black formula at −shift + 1e-12, and its digital coupon struck 1e-12 above its replication's reach (the wheel and the C++ harness).

Facts:

- `ACapAtOrBelowMinusShiftIsTheForwardsIntrinsic`
- `ADigitalAtMinusShiftReplicatesToOne`
- `AForwardAtOrBelowMinusShiftIsRefused`

## BlackKarasinskiTreeFittingTests

[`Divergences/BlackKarasinskiTreeFittingTests.cs`](../tests/NanoQL.Tests/Divergences/BlackKarasinskiTreeFittingTests.cs)

Black-Karasinski's trees on one model: QuantLib's `BlackKarasinski::tree` writes the model's one fitting parameter `phi_` (`blackkarasinski.cpp:64`, `:79-81`), so building a second tree on another grid rewrites the first's fitting: on the wheel, a grid engine's swaption, exercised on its swap's start date, which the discretized swaption's snapping leaves alone, prices 0.007389451726576681 alone and 0.007389042124133561 once a second grid engine has built its tree on the same model. nanoql's tree owns its fitting, as the Hull-White tree does, so a lattice prices the same whatever is built after it. QuantLib's check: a fresh model's tree on the first grid, 0.007389451726576681.

Facts:

- `ATreeKeepsItsFittingWhenAnotherIsBuilt`

## BlackVarianceEmptyRefusalTests

[`Divergences/BlackVarianceEmptyRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/BlackVarianceEmptyRefusalTests.cs)

`BlackVarianceCurve` and `BlackVarianceSurface` with no dates. QuantLib's constructors read `dates.back()` of the empty list in their initializers, before any check (`blackvariancecurve.cpp:35`, `blackvariancesurface.cpp:36`), a result C++ leaves undefined; on the wheel each crashes the process. nanoql refuses with `ArgumentException`, "no dates given", naming `dates`.

Facts:

- `ACurveWithNoDatesIsRefused`
- `ASurfaceWithNoDatesIsRefused`

## BlackVarianceSurfaceFloorTests

[`Divergences/BlackVarianceSurfaceFloorTests.cs`](../tests/NanoQL.Tests/Divergences/BlackVarianceSurfaceFloorTests.cs)

`BlackVarianceSurface`'s variance past its strikes. QuantLib extrapolates the bilinear variance linearly in strike with no floor under its default extrapolation (`blackvariancesurface.cpp:67-84`), where its variance curve and its time extrapolation floor theirs at zero (`blackvariancecurve.cpp:68`, `blackvoltimeextrapolation.cpp:44`): volatilities of 30% at 90 and 20% at 110 read a variance of −0.010 and a NaN volatility at 130. nanoql floors the variance at zero, QuantLib's convention for a Black variance; inside the grid the variances are QuantLib's.

Facts:

- `TheExtrapolatedVarianceIsFlooredAtZero`

## BlackVasicekVolatilityTests

[`Divergences/BlackVasicekVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/BlackVasicekVolatilityTests.cs)

The Black-Vasicek engine's equity volatility and dividends. QuantLib reads the equity volatility at time 0 (`blackVol(t, K)` with t = 0, `analyticeuropeanvasicekengine.cpp:78`) and leaves the dividend curve out, so a volatility curve rising from 10% to 30% prices on 10% and a dividend yield changes nothing. nanoql integrates the equity's term variance over [0, T] and takes the dividend discount into the forward. QuantLib's check: the engine's limit as the short rate's volatility vanishes, QuantLib's analytic European engine on the Vasicek bond's flat rate with the term volatility and the dividend curve (QuantLib-Python 1.44; QuantLib's own engine through the harness). On a flat volatility and no dividend, QuantLib's bits (the engine's harness vectors). QuantLib's master fixes the same class after v1.44 (#2927: the volatility at maturity, the spot discounted by the dividend curve), whose two cases `EuropeanOptionTests` ports as checks of this engine; it stays until the pin moves to a release carrying the fix.

Facts:

- `TheEquityVolatilityIsItsTermVariance`
- `TheDividendsEnterTheForward`

## BlackVolTimeExtrapolationRefusalTests

[`Divergences/BlackVolTimeExtrapolationRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/BlackVolTimeExtrapolationRefusalTests.cs)

`BlackVolTimeExtrapolation` on too few times. QuantLib's surface overload reads `times[N-2]` unchecked under the linear strategy (`blackvoltimeextrapolation.cpp:49-50`), past the list's start with one time, where its curve overload refuses "at least two times required for volatility extrapolation" (`:86-88`); and both overloads' flat strategy read `times.back()` of an empty list (`:39`, `:44`). Both are results C++ leaves undefined. nanoql refuses them with `ArgumentException` naming `times`: the first with the curve overload's text, the second with "at least one time required for volatility extrapolation".

Facts:

- `TheSurfaceOverloadRefusesOneTimeUnderTheLinearStrategy`
- `TheFlatStrategyRefusesNoTimes`

## BlackYoYBelowShiftTests

[`Divergences/BlackYoYBelowShiftTests.cs`](../tests/NanoQL.Tests/Divergences/BlackYoYBelowShiftTests.cs)

The Black year-on-year pricer's option struck at or below 0 (its year-on-year part): QuantLib's `BlackYoYInflationCouponPricer` prices an optionlet by `blackFormula` at the coupon's effective strike, unshifted (`inflationcouponpricer.cpp:178-188`), which refuses a strike below 0, so a coupon capped or floored below 0 refuses: on the wheel, the vectors' at-the-money coupon (a forward of 3.05%) capped at −1% refuses ("strike + displacement (-0.01 + 0) must be non-negative"), at −1e-12 likewise, and at 0 pays 0.0. nanoql's caplet there is the forward less the strike and its floorlet 0, the formula's limits, so a coupon capped below 0 pays its cap and one floored there its swaplet. QuantLib's check: its Black formula at 0 and at 1e-12, a coupon capped at 1e-12 paying 9.999986949615902e-13.

Facts:

- `ACouponCappedBelowZeroPaysItsCap`

## BondEmptyLegTests

[`Divergences/BondEmptyLegTests.cs`](../tests/NanoQL.Tests/Divergences/BondEmptyLegTests.cs)

A bond on an empty leg, the default of QuantLib's two public constructors, and a bond on a leg holding a null cash flow. QuantLib builds the bond on an empty leg and leaves its notional schedule empty (`bond.cpp:42, 72`), and its notional then reads the schedule's last date (`bond.cpp:117`), past the end of an empty vector, which is undefined: the wheel ends the process on `notional()`, `isTradable()`, `accruedAmount()` and `cleanPrice()`. nanoql builds the bond as QuantLib does, with QuantLib's defined answers (its NPV the expired bond's 0, its maturity date refused "empty leg", its redemption "multiple redemption cash flows given"), and its `Bond.Notional` refuses where QuantLib reads past the end, with `NanoQLException`. A null cash flow is dereferenced by QuantLib's sort (`bond.cpp:43-44, 74-75`), which is undefined, and the wheel refuses `None` in a leg; nanoql refuses it at construction with `ArgumentException`, as a swap does.

Facts:

- `ABondOnAnEmptyLegBuildsAndRefusesItsNotional`
- `ANullCashFlowIsRefusedAtConstruction`

## BondForwardIncomeTests

[`Divergences/BondForwardIncomeTests.cs`](../tests/NanoQL.Tests/Divergences/BondForwardIncomeTests.cs)

The bond forward's income: QuantLib's `BondForward::spotIncome` sums the coupons' amounts, on the bond's face, paid between settlement and delivery (`bondforward.cpp:59-83`), while its spot value is the dirty price, per 100 (`:86-87`). On the wheel, on QuantLib's test-suite bond (2.5%, 15 August 2015 to 2046, annual on TARGET, Actual/Actual (ISDA)) and curve (flat 0.04977%) on 7 March 2022, the bond settling on the curve's reference date, where QuantLib's spot value is a reference-date value (`BondForwardSpotDateTests`), delivering on 10 September 2022, after the August coupon: on a face of 100 the income is 2.492603415097658 and the forward value 158.4458479330134 (clean 158.2540671110956); on QuantLib's own face, 1e5, the income is 2,492.6034150976584 and the forward value −2,332.3067807511693. Before the coupon, on 10 March, the income is 0 and the forward value 160.89828121347867 on either face. A face of 100 isn't enough: an amortizing bond's dirty price is per 100 of its notional at settlement, so on a 4% bond of notionals 100, 75, 50 and 25 (2022 to 2032, annual on TARGET), 50 on 15 January 2025, delivering on 3 April 2025 after the March coupon and amortization, QuantLib's income is 26.872912556834198 and its forward value 81.3910471000986 on a flat 2.6% curve. nanoql's income is per 100 of the bond's notional at its settlement date, as the spot value is. QuantLib's check: its face-100 forward on the same bond, the forward being per 100; and its forward on the amortizing bond's notionals doubled, 100 at settlement, 54.3684086579997. The clean forward follows the forward's basis: QuantLib's `cleanForwardPrice` subtracts the accrued amount at delivery per 100 of the notional at delivery (`bondforward.cpp:49-51`), so on a 4% bond of notionals 100, 100, 50 and 50 (15 July 2024 to 2028, annual, Actual/Actual (ISMA)) delivering on 15 October 2026, after its step to 50, on a flat 3% curve on 15 January 2025, its clean forward is 50.278704784168255 from a dirty one of 51.286923962250434; nanoql's takes the accrued amount per 100 of the notional at settlement, 50.78281437320934. QuantLib's check: its forward on a face-100 bullet, whose flows after delivery are twice the amortizing bond's, halved: 51.286923962250434 dirty and 50.78281437320934 clean.

Facts:

- `AfterACouponTheForwardIsPerHundred`
- `BeforeACouponTheForwardIsQuantLibs`
- `AnAmortizedBondsForwardIsPerHundredOfItsNotional`
- `TheCleanForwardAcrossAStepIsOnTheForwardsBasis`

## BondForwardSpotDateTests

[`Divergences/BondForwardSpotDateTests.cs`](../tests/NanoQL.Tests/Divergences/BondForwardSpotDateTests.cs)

The bond forward's spot value and its date: QuantLib's forward value is the spot value less the income over the discount at delivery (`forward.cpp:61-65`), its spot the bond's dirty price, a value at the bond's settlement date (`bondforward.cpp:86-87`), its income and both discounts from the curve's reference date, so a bond that doesn't settle on that date gives a forward high by 1/P(ref, settlement). QuantLib's own `testThatForwardValueIsEqualToSpotValueIfNoIncome` asserts the identity the factor breaks, within 1e-2 on a 0.05% curve three days out. On the wheel, on a 5-year 4% bullet from 17 January 2025 (annual on TARGET, Actual/Actual (ISMA)) settling in 2 days, on a flat 3% curve on 15 January 2025, its dirty price is 104.35728008689668; QuantLib's forward delivering on its settlement date, the forward settling the same day, is 104.37443611832695, and 104.41733853717292 delivering on 22 January, no coupon between, 1/P(ref, settlement) = 1.0001643970733618 high. nanoql carries the spot back to the reference date on the forward's discount curve before the income is netted, so its forward is the dirty price grown from settlement. QuantLib's check, its own forwards on the same bullet settling on the reference date, where its spot is a reference-date value: 104.35728008689668 to 17 January, the 2-day bond's dirty price (the identity), and 104.40017545386986 to 22 January; nanoql's on the bond settling in 2 days give the same.

Facts:

- `TheSpotIsCarriedBackFromTheBondsSettlement`

## BondHelperArgumentTests

[`Divergences/BondHelperArgumentTests.cs`](../tests/NanoQL.Tests/Divergences/BondHelperArgumentTests.cs)

A bond helper on a bond with no cash flows, or on no bond. QuantLib's helper copies the bond it is given (`make_shared<Bond>(*bond)`, `bondhelpers.cpp:33`), which dereferences a null one, and reads its last cash flow's date (`bondhelpers.cpp:37`), past the end of an empty leg: both undefined, and the wheel ends the process on the second. nanoql refuses a null bond with `ArgumentNullException`, and a bond with no cash flows with `ArgumentException`, in QuantLib's text for a derived bond built with none ("bond with no cashflows!", `fixedratebond.cpp:67`).

Facts:

- `AHelperOnABondWithNoCashFlowsIsRefused`
- `AHelperOnANullBondIsRefused`

## BondSortTests

[`Divergences/BondSortTests.cs`](../tests/NanoQL.Tests/Divergences/BondSortTests.cs)

The order of a bond's equal-dated coupons. QuantLib's constructors sort the cash flows they are given with `std::sort` (`bond.cpp:43-44, 74-75`), which isn't stable: MSVC's, which built the wheel, runs an insertion sort up to 32 elements, which keeps equal-dated coupons in their order, and an introsort beyond, which reorders them. nanoql sorts stably, so it gives QuantLib's order up to 32 cash flows and on any leg without equal dates, and the coupons' own order beyond. The order is observable wherever a function reads the first coupon on a date, or compares consecutive coupons' nominals.

Facts:

- `EqualDatedCouponsKeepTheirOrderBeyondThirtyTwo`

## BootstrapNullHelperTests

[`Divergences/BootstrapNullHelperTests.cs`](../tests/NanoQL.Tests/Divergences/BootstrapNullHelperTests.cs)

A null helper among a curve's instruments is refused when the curve is built, with `ArgumentException` naming the instruments, and one among a global bootstrap's additional helpers when the bootstrap is built, naming the additional helpers. QuantLib's bootstraps read their helpers' pillar dates (`iterativebootstrap.hpp:158-159`, `bootstraphelper.hpp:251-255`; `globalbootstrap.hpp:281-282, 290-294`), dereferencing a null one, a result C++ leaves undefined; the wheel's helper vector refuses `None` before QuantLib sees it.

Facts:

- `ANullHelperAmongACurvesInstrumentsIsRefused`
- `ANullHelperAmongAGlobalBootstrapsAdditionalHelpersIsRefused`

## BotswanaTuesdayProvisoTests

[`Divergences/BotswanaTuesdayProvisoTests.cs`](../tests/NanoQL.Tests/Divergences/BotswanaTuesdayProvisoTests.cs)

Botswana's Tuesday proviso. QuantLib's header quotes the Public Holidays Act: "if 2nd January, 1st October or Boxing Day falls on a Monday, the following Tuesday shall be observed as a public holiday" (`botswana.hpp`), and its code observes that Tuesday for January 2nd alone (`botswana.cpp`, `d == 3 && w == Tuesday` in January): Tuesday December 27th after a Monday Boxing Day, and Tuesday October 2nd after a Monday October 1st, are business days in QuantLib in every such year from 1901 to 2199. nanoql observes them by the January branch's own expression; QuantLib's January 2nd branch is asserted beside them.

Facts:

- `TheTuesdayAfterAMondayBoxingDayIsAHoliday`
- `TheTuesdayAfterAMondayFirstOfOctoberIsAHoliday`

## BsmHullWhiteMismatchedTimesTests

[`Divergences/BsmHullWhiteMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/BsmHullWhiteMismatchedTimesTests.cs)

The BSM-Hull-White engine's variance offset on structures counted differently. QuantLib takes the Hull-White terms over the risk-free curve's time and the equity volatility in the covariance as its own structure quotes it (`analyticbsmhullwhiteengine.cpp:86-102`), so a volatility on Act/360 beside a curve on Act/365 enters the covariance at a rate its variance doesn't run at. nanoql takes the equity volatility as its variance per unit of the risk-free curve's time. QuantLib's check: its own engine on the market restated on one day counter, the volatility scaled by √(365/360) (QuantLib-Python 1.44). Where the structures share their counts, QuantLib's bits (the engine's vectors).

Facts:

- `TheCovarianceReadsTheVolatilityPerTheCurvesTime`

## BsmHullWhiteSmallReversionTests

[`Divergences/BsmHullWhiteSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/BsmHullWhiteSmallReversionTests.cs)

The BSM-Hull-White engine's variance offset at small mean reversion (§3.3's small-reversion class). QuantLib's closed form, v = σ²/a²·(t + 2e^(−at)/a − e^(−2at)/(2a) − 3/(2a)) and μ = 2ρση/a·(t − (1 − e^(−at))/a) (`analyticbsmhullwhiteengine.cpp:97-110`), cancels to σ²t³/3 and ρσηt² with a relative error of about 3ε/(a·t)³, used down to (a·t)⁴ = ε. nanoql takes the series to (a·t)⁴ below a·t = 1e-2 and the closed form above. QuantLib's check: its own closed form evaluated to 50 digits (Python's decimal) on both sides of the cut, priced by QuantLib's analytic European engine on the total variance η²t + v + μ (QuantLib-Python 1.44).

Facts:

- `BelowTheCutTheSeriesIsTheClosedFormsExactValue`
- `AboveTheCutTheClosedFormIsKept`

## BuilderTests

[`Divergences/BuilderTests.cs`](../tests/NanoQL.Tests/Divergences/BuilderTests.cs)

QuantLib's builders (`MakeSchedule`, `FixedRateLeg`) change themselves and return a reference to themselves, and convert implicitly. nanoql's return a modified copy, so a builder kept and reused never changes under its holder, and convert through `ToSchedule()` and `ToLeg()`, which throw where QuantLib's conversions throw. And a leg builder can't be given a schedule built from dates on QuantLib's empty calendar: `Schedule`'s dates constructor reads `null` as QuantLib's default there, `NullCalendar`, so such a schedule has no spelling, and `OvernightLeg` pays on the schedule's own dates where QuantLib's falls back to `WeekendsOnly` for an empty schedule calendar (`overnightindexedcoupon.cpp:583-586`).

Facts:

- `AMakeScheduleReusedAfterAWithCallIsUnchanged`
- `AFixedRateLegReusedAfterAWithCallIsUnchanged`
- `TheInterestRateOverloadsBuildTheSameLeg`
- `ToScheduleThrowsWhereQuantLibsConversionThrows`
- `ToLegThrowsWhereQuantLibsConversionThrows`
- `ADatesScheduleOnQuantLibsEmptyCalendarHasNoSpelling`

## Business252CacheTests

[`Divergences/Business252CacheTests.cs`](../tests/NanoQL.Tests/Divergences/Business252CacheTests.cs)

Business/252's month counts. QuantLib caches them in two file-level maps keyed by the calendar's name, filled once and never invalidated (`business252.cpp:31-68`, `:87-105`): after a holiday edit every `Business252` on the calendar's name, a new one included, counts the days the first count saw, and two calendars of one name share their counts. nanoql counts each month once per day counter, on its own calendar, so a count is always its calendar's `businessDaysBetween`, QuantLib's own check.

Facts:

- `ACountAfterAHolidayEditIsTheEditedCalendars`
- `TwoCalendarsOfOneNameCountTheirOwnDays`

## CPICapFloorPriceSurfaceTests

[`Divergences/CPICapFloorPriceSurfaceTests.cs`](../tests/NanoQL.Tests/Divergences/CPICapFloorPriceSurfaceTests.cs)

A CPI price surface's put-call fills compounded over the maturity in years (fixed). QuantLib's surface compounds the at-the-money rate and the strike over `mat.length()`, the maturity's length in its own unit (`cpicapfloortermpricesurface.hpp:248-252`), so a maturity in months compounds over its month count. On QuantLib 1.44 (test-suite/inflationcpicapfloor.cpp's case with its maturities written as 36 to 360 months), the 36-month cap filled at the floor strike of −1% is 2.137020233466961, where the same surface on 3 to 30 years fills 0.11890267275321309; QuantLib's case passes either way, reading only the quoted strikes. nanoql compounds over QuantLib's own `years` of the period: 36 months price as 3 years, QuantLib's bits, and a maturity in weeks or days, which has no length in years, is refused.

Facts:

- `AMaturityInMonthsFillsAsTheSameInYears`
- `AMaturityInWeeksIsRefused`

## CPISwapFairRateTests

[`Divergences/CPISwapFairRateTests.cs`](../tests/NanoQL.Tests/Divergences/CPISwapFairRateTests.cs)

`CPISwap`'s fair rate. QuantLib derives it as `fixedRate - NPV / (legBPS[0] / 1bp)` (`cpiswap.cpp:195-199`), but a CPI coupon's sensitivity to its rate carries its index ratio, which the BPS leaves out, so the swap repriced at QuantLib's fair rate isn't worth zero (QuantLib's defect). nanoql's `DiscountingSwapEngine` gives the rate that zeroes the swap, `fixedRate - NPV / (payer[0] S)`, S the NPV of the swap's CPI coupons rebuilt at a unit rate. On `InflationFixture`'s market, for a five-year payer swap of 1,000,000 against a 6-month Ibor leg on a curve flat at 4.5% plus 0.1%, at 2% on a base CPI of 306.1, QuantLib's fair rate is 0.011610586633227834 and the swap rebuilt at it is worth -4,112.158481168008; the root of QuantLib's own NPV, found by QuantLib's `Brent` through the wheel, is 0.012444035321727333, where the swap is worth 0.0, and it is nanoql's fair rate.

Facts:

- `TheFairRateZeroesTheSwap`

## CTSMMCapletAlphaFormCalibrationTests

[`Divergences/CTSMMCapletAlphaFormCalibrationTests.cs`](../tests/NanoQL.Tests/Divergences/CTSMMCapletAlphaFormCalibrationTests.cs)

The alpha-form calibration where a step's final part fails, which QuantLib's master refuses after the pin and nanoql refuses in the pass the calibration returns, and its fallback at maximum homogeneity. A step whose final part fails: QuantLib 1.44's solve returns it as solved, the swap rate's last volatility left at its buffer's 0, and its calibration returns `true` with no failure (`alphafinder.cpp:335-346`, `:405-415`; `capletcoterminalalphacalibration.cpp:202-203`, its count commented out beside its throw). In a sweep of 7,800 inputs, 517 calibrations return so, their swaptions missing by 6.3e-4 to 0.249. One at QuantLib's own α₀ and bounds: displaced 2%, caplets × 0.95, three factors, QuantLib 1.44's swaptions missing by 0.0347198 and its caplet root-mean-square error 0.0109794. QuantLib's master's solve returns false there (#2910, `dbe958954`), so its calibration throws "alpha form failure", and so does nanoql's where the pass it returns has such a step: on that sweep 4,091 calibrations at QuantLib's settings, 517 on the linear-hyperbolic form and 3,574 on the inverse-linear one; and so does the inverse-linear form at α₀ 0 in [−1, 1], whose calibration and pass function the vectors held with 1.44's numbers (the generator now has them at ±0.5, where every step solves). Master throws at the first such step in any pass; nanoql counts one in an earlier pass, whose successor re-targets the caplets and can fit, so a calibration whose returned pass fits keeps QuantLib 1.44's numbers: displaced 2%, caplets × 1.2, five factors, α₀ 0.5, which QuantLib converges to a caplet root-mean-square error of 4.6924312876899316e-05 and master refuses; at α-solving tolerances of 0.05 and 0.2 up to 172 more, at maximum homogeneity too. Maximum homogeneity: the fixed minimization's α at one caplet can leave a later caplet no α where QuantLib's first trial leaves room, so the fixed calibration throws on 50 of the swept inputs QuantLib calibrates; the pass then runs again on QuantLib's test of the objective's value, for a caplet with no α, not for a failed final part, which is counted: at α-solving tolerances of 0.01 and above, 147 of 33,600 maximum-homogeneity calibrations have a failed final part at the first pass, and on 18 of them a fallback for it too would complete where nanoql refuses, QuantLib 1.44's numbers on 15. On caplets × 0.95 and eight factors QuantLib's caplet error is 3.4739757936758017e-07 and its swaptions exact (the vectors hold the case bit for bit). On QuantLib's case the fixed calibration completes, more homogeneous: its deformation, the sum over rates and steps of the squared change of each step's standard deviation, 0.0010560 against QuantLib's 0.0018891. Checks: the master's refusal on the half-solved cases; QuantLib's numbers on the fallback; the fit and the deformation on QuantLib's case.

Facts:

- `AHalfSolvedStepIsRefused`
- `ACalibrationWhoseReturnedPassFitsKeepsQuantLibsNumbers`
- `AFailedFinalPartIsNotTheFallbacksTrigger`
- `TheInverseLinearFormsHalfSolvedStepsAreRefused`
- `MaximumHomogeneityOnQuantLibsCaseIsMoreHomogeneous`
- `WhereTheFixLeavesACapletNoAlphaQuantLibsTestRuns`

## CTSMMCapletMaxHomogeneityDeformationTests

[`Divergences/CTSMMCapletMaxHomogeneityDeformationTests.cs`](../tests/NanoQL.Tests/Divergences/CTSMMCapletMaxHomogeneityDeformationTests.cs)

The calibrations' deformation size. QuantLib's maximally homogeneous calibration sums, for each caplet i, the squared change of the step index's own volatility once per moved volatility, `(theseNewVols[i] − secondRateVols[i])²` for each `j < i + 2` (`capletcoterminalmaxhomogeneity.cpp:370-371`), where its loop runs over j: nanoql sums each moved volatility's own squared change (fixed; QuantLib's tests only print it). And QuantLib's alpha-form and original calibrations never compute it, returning the initializer's 987654321 (`ctsmmcapletcalibration.cpp:131`, `:199`), nor its periodic calibration: nanoql's is null there. Checks: the deformation rebuilt from the results' calibrated and given volatilities, each step's volatility times the root of its length; QuantLib's sum restated on the same volatilities, 0.00177 on QuantLib's case.

Facts:

- `TheDeformationSumsEachMovedVolatilitysChange`
- `TheAlphaFormAndOriginalCalibrationsHaveNone`

## CalendarEqualityTests

[`Divergences/CalendarEqualityTests.cs`](../tests/NanoQL.Tests/Divergences/CalendarEqualityTests.cs)

Calendars are immutable values. QuantLib compares calendars by name only, and its `addHoliday`, `removeHoliday` and `resetAddedAndRemovedHolidays` change the holidays of every instance of the calendar, since all instances share one implementation. In nanoql an edit returns a modified copy, the added and removed holidays are part of the calendar's value, and a joint calendar also compares its rule and its components.

Facts:

- `CalendarsAreEqualByNameAndByTheirAddedAndRemovedHolidaysComparedByContent`
- `AddHolidayRemoveHolidayAndResetLeaveTheReceiverUnchanged`
- `ANewTargetAndTheOriginalDontSeeAnEditMadeThroughACopy`
- `JointCalendarsWhoseComponentsDifferByAnAddedHolidayAreUnequal`
- `JointCalendarsBuiltFromEqualComponentsAreEqualWithEqualHashes`
- `CalendarsOfDifferentKindsAreUnequalFromEitherSide`

## CalendarListEndTests

[`Divergences/CalendarListEndTests.cs`](../tests/NanoQL.Tests/Divergences/CalendarListEndTests.cs)

A holiday or business-day list ending on Date's last day. QuantLib's loops step their date one day past the list's end (`calendar.cpp:281`, `:292`), so a list to 31 December 2199 throws "Date's serial number (109575) outside allowed range", in every calendar. nanoql's loops stop at the end; the lists are QuantLib's own to 30 December and its own `isHoliday` of the 31st.

Facts:

- `AHolidayListEndsOnDatesLastDay`
- `ABusinessDayListEndsOnDatesLastDay`

## CalibratedModelSideEffectTests

[`Divergences/CalibratedModelSideEffectTests.cs`](../tests/NanoQL.Tests/Divergences/CalibratedModelSideEffectTests.cs)

What QuantLib's calibrated model changes in itself. Its `setParams` writes each parameter as it reaches it (`model.cpp:138-149`): an array too short sets the leading parameters before refusing "parameter array too small", one too long sets them all before refusing "parameter array too big!" (the wheel: `[0.2, 0.01]` and `[0.2, 0.02]` left on a Hull-White model from `[0.1, 0.01]`). Its `value` sets the parameters it is given into the model before it prices the helpers (`model.cpp:46-47, 117-124`), so the model is left at them, and the helpers whose engines hold it price at them afterwards. nanoql's models are immutable: `SetParams` returns a new model, and refuses both with QuantLib's texts before anything is written, so the receiver keeps its parameters; and `Value` prices against a new model with the parameters and changes nothing.

Facts:

- `ARefusedSetParamsLeavesTheModelUnchanged`
- `ValueLeavesTheModelUnchanged`

## CalibrationBasketNominalTests

[`Divergences/CalibrationBasketNominalTests.cs`](../tests/NanoQL.Tests/Divergences/CalibrationBasketNominalTests.cs)

The calibration basket's nominal: QuantLib's match reads a negative nominal as the flipped swap type (`basketgeneratingengine.hpp:156-157`), but floors the solution's nominal at 1e-6 before dropping its sign (`basketgeneratingengine.cpp:213-214`, `:230`), so a flipped solution's helper gets 1e-6. On the wheel, a Bermudan payer exercised in one, two and three years into a 5-year inverse floater (fixed 3%; floating 4% − 0.5 × Euribor 6M) on 1,000,000, on a GSR model (a = σ = 0.01) on a flat 3% curve, gets helpers of nominal 1e-6 at strikes 0.0215898859149053, 0.02160167978480207 and 0.02162134419969126. The receiver at −3% fixed with gearing 0.5 and spread −4%, worth the same in every state, solves unflipped: nominals 490917.1124356477, 491116.4435245846 and 491433.33826786536, from another guess (a strike of −3%), at the same strikes within 4e-9. nanoql keeps the solution's size: QuantLib's strikes, and nominals within 3.1e-6 of the unflipped solution's, the two starts' spread. QuantLib's check: its match on the unflipped equivalent.

Facts:

- `AFlippedSolutionKeepsItsNominal`

## CalibrationNullHelperTests

[`Divergences/CalibrationNullHelperTests.cs`](../tests/NanoQL.Tests/Divergences/CalibrationNullHelperTests.cs)

A null helper among a calibration's. QuantLib's `calibrate` and `value` take a vector of helper pointers, which may hold a null one, and the calibration function dereferences each at its first evaluation (`model.cpp:49-51, 59-61`), undefined for a null pointer (the wheel's binding refuses `None` before the call). nanoql's `Calibrate` and `Value` copy the helpers and refuse a null one first, with `ArgumentException`, "null calibration helper", naming `helpers`.

Facts:

- `ANullHelperIsRefused`

## CalibrationSmallReversionTests

[`Divergences/CalibrationSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/CalibrationSmallReversionTests.cs)

Hull-White calibrations that reach small mean reversion: QuantLib 1.44's Vasicek B and A cut to their a = 0 forms below √ε and cancel above it (`vasicek.cpp:36-58`), and nanoql's are QuantLib's master's, free of the cancellation (#2882). Where a calibration's optimum lies near a = 0, QuantLib's optimizer reads the cancelling forms: its calibrated parameters and the cost it reports there are 1.44's arithmetic, not the model's, and a start that 1.44's cancellation turns away from a = 0 reaches the boundary there instead. The calibrations are `models/model.json`'s, on their inputs; that file keeps the outputs its stability filter finds stable. QuantLib's check: its cost where its forms hold, at an ordinary reversion, equal to nanoql's within the tolerance, and its own interior minimum, which nanoql's cost has too.

Facts:

- `ACalibrationAtSmallReversionReportsTheCostOfItsParameters`
- `AFarStartReachesTheBoundaryMinimum`

## CallSpecifiedEndTests

[`Divergences/CallSpecifiedEndTests.cs`](../tests/NanoQL.Tests/Divergences/CallSpecifiedEndTests.cs)

The call-specified product's end. QuantLib's ends with its underlying (`callspecifiedmultiproduct.cpp:167`), so its tested Bermudan, a call-specified product on `MultiStepNothing` done at its first step, ends at once alone and is worth its value only inside a composite that steps a finished part again: on six semi-annual rates, 8,191 Sobol paths, `testCallableSwapNaif`'s Bermudan 0.0069062 in QuantLib's composite and 0.0053606 alone. nanoql keeps QuantLib's end but on an underlying with no possible cash-flow times, which can't end it while an exercise time remains, stops stepping a finished underlying, and, once ended with its underlying, stays ended, with no call and no cash flow. Checks: QuantLib's composite values, bit for bit, alone and inside; QuantLib's callable identity.

Facts:

- `ABermudanOnNothingIsWorthItsValueAloneAndInside`
- `ACallableEndedWithItsUnderlyingStaysEnded`

## CallSpecifiedIdleStepTests

[`Divergences/CallSpecifiedIdleStepTests.cs`](../tests/NanoQL.Tests/Divergences/CallSpecifiedIdleStepTests.cs)

The call-specified product's counts at an idle step (fixed). At a step where neither its underlying nor its rebate steps, an exercise, strategy or rebate time off the underlying's grid, QuantLib's product writes no counts (`callspecifiedmultiproduct.cpp:144-167`, `pathwiseproductcallspecified.cpp:136-153`), and the accounting engine adds the previous step's flows again: a 2% receiver swap on six semi-annual rates callable 0.1 before each rate time and never called, 8,191 paths, QuantLib −0.061168 ± 0.00039 against the swap's exact −0.033604. nanoql zeroes every count at each step's start, as QuantLib's composites zero the parts they don't step. Checks: QuantLib's bits on schedules on the grid (QuantLib's cases, the vectors); off it, the swap's exact value.

Facts:

- `AnUncalledProductOffTheGridIsItsUnderlying`

## CallabilityTypeAfterSettledCallabilityTests

[`Divergences/CallabilityTypeAfterSettledCallabilityTests.cs`](../tests/NanoQL.Tests/Divergences/CallabilityTypeAfterSettledCallabilityTests.cs)

A callable bond's live callabilities after one on or before the settlement date, of the other type. QuantLib 1.44 keeps the dates and prices of the live callabilities only (`callablebond.cpp:448-452`), but its tree applies the i-th of them by the type of the i-th entry of the whole schedule (`discretizedcallablefixedratebond.cpp:179`), so after a settled put a live call is applied as a put, and after a settled call a live put as a call. QuantLib fixed it after v1.44 (issue #2871, PR #2875, commit `20c2d3ac6`), keeping each live callability's type with its date and price, and nanoql fixes it as QuantLib does. This is upstream's `testCallabilityTypeAfterSettledCallability`: on testCached's bond, the price with the settled callability ahead equals the price without it, where QuantLib 1.44 prices the settled put ahead of the calls at 106.0716945553609 for 102.76445466543099, and the settled call ahead of the puts at 102.90405256617298 for 114.89193072303775. The class retires, and the case joins `CallableBondsTests`, when the pin moves to a release with the fix.

Facts:

- `ASettledCallabilityLeavesTheLiveOnesTypesAlone`

## CallableBondScheduleTests

[`Divergences/CallableBondScheduleTests.cs`](../tests/NanoQL.Tests/Divergences/CallableBondScheduleTests.cs)

A callable bond on an empty coupon schedule, and a put/call schedule holding a null callability. QuantLib's `CallableFixedRateBond` passes its base the schedule's last date unchecked (`schedule.dates().back()`, `callablebond.cpp:499`), past the end of an empty vector, which is undefined: the wheel ends the process (0xC0000005), where `FixedRateBond`, which reads the schedule's `endDate()`, refuses "empty Schedule: no end date". nanoql reads `Schedule.EndDate` and refuses with that text. QuantLib's base constructor reads each callability's date to find the last one (`callablebond.cpp:43-50`), dereferencing a null one; nanoql refuses it with `ArgumentException`, naming the schedule.

Facts:

- `AnEmptyScheduleIsRefusedWithTheSchedulesOwnText`
- `ANullCallabilityIsRefusedAtConstruction`

## CallableSnapKeptTests

[`Divergences/CallableSnapKeptTests.cs`](../tests/NanoQL.Tests/Divergences/CallableSnapKeptTests.cs)

The callable bond tree's snap, kept: QuantLib's discretized callable bond moves a call up to a week before a coupon onto the coupon, its price discounted forward (`discretizedcallablefixedratebond.cpp:59-101`, "To avoid mispricing, we snap exercise dates to the closest coupon date"). Against Jamshidian's decomposition of the issuer's call on Hull-White (0.1, 0.01) on a flat 4% curve, on a 10-year 5% bond callable at a clean 100 once, the snap adds about 0.0006 per day, 0.0046 per 100 at seven days (4.5e-5 relative): the closed form is 103.177955 a week before the fifth coupon, where the 500-step tree prices 103.18258278794376 on the wheel; 103.191502 on the coupon (the tree 103.19140397824488) and 103.176019 eight days before (103.17602442460134), beyond the week. A tree without the snap is within 1e-4 of the closed form, but QuantLib's `testUnexercisableCallability` (`callablebonds.cpp:673-735`) checks at 1e-10 that an unreachable call one to seven days before a coupon leaves the price alone, which only the snap achieves: without it the grid gains a time and the price moves 2.4e-3. nanoql keeps QuantLib's snap and its bits; QuantLib's own test stands in `TestSuite/CallableBondsTests.cs`.

Facts:

- `ACallInTheWeekBeforeACouponIsQuantLibsSnap`

## CapFloorCmsTests

[`Divergences/CapFloorCmsTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorCmsTests.cs)

A CMS caplet on the Gaussian1d cap/floor engine: QuantLib's engine tests only for an Ibor index (`gaussian1dcapfloorengine.cpp:47-48`) and otherwise prices the coupon period's simple forward (`:81-85`, `:153-157`), so a CMS caplet reads no swap rate. Its same-model route is the float-float swaption engine on a European exercise at the fixing into the CMS coupon against a fixed one, which estimates the coupon by the model's swap rate at its fixing (`gaussian1dfloatfloatswaptionengine.cpp:460-464`). On the wheel, on a GSR model (a = σ = 0.01) on a flat 3% curve, a one-year caplet on 1,000,000 of the 10-year EUR swap rate (ISDA fix A), fixing in one, two or five years, is worth, against the float-float route: at 3% in a year 3845.936360680116 against 4153.063226583255; at 2%, 3% and 4.5% in two years 11120.554478976486, 5251.888776300536 and 1001.0246098467576 against 11894.414352660851, 5692.585769904653 and 1092.524780338987; at 3% in five years 7470.393687093831 against 8282.86559093049. nanoql prices a CMS coupon on the model's swap rate at its fixing, the float-float engine's estimate, within 8.0e-9 of that route on the engines' default grid (64 points a side, 7 standard deviations), where each route moves by up to 1.1e-3 as the grid refines, as an Ibor caplet does (the payoff's kink). QuantLib's check: that engine on the one-coupon swaption. A CMS spread caplet alike: on the 10-year rate less half the 2-year's, the float-float engine estimates its coupon by the geared spread of the model's two swap rates (`:465-480`, `:543-558`); at 1.5% in a year 2101.104009657879, at 1%, 1.5% and 2% in two years 6129.950526822571, 2916.7134855385843 and 1027.7328956208546, at 1.5% in five years 4399.2812060734495, where QuantLib's cap engine, on the period's simple forward, gives 14629.465167876351, 19008.050510174733, 14876.81979668136, 11120.554478976486 and 15437.407755498794, its coupon on a `LognormalCmsSpreadPricer`; nanoql's within 2e-8 of the float-float route.

Facts:

- `ACmsCapletReadsTheModelsSwapRate`
- `ACmsSpreadCapletReadsTheModelsTwoSwapRates`

## CapFloorEmptyLegTests

[`Divergences/CapFloorEmptyLegTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorEmptyLegTests.cs)

The last floating-rate coupon of a cap on an empty leg. QuantLib's `lastFloatingRateCoupon()` reads `floatingLeg_.back()` (`capfloor.cpp:187-193`), which is undefined for an empty leg; the wheel doesn't bind it. nanoql refuses with `NanoQLException` and the text QuantLib's `CashFlows` gives an empty leg, "empty leg", as the wheel's `startDate()` and `maturityDate()` on the same cap do. The rest of the cap is QuantLib's defined behaviour: it is expired, prices at 0 and has an ATM rate of 0.

Facts:

- `TheLastCouponOfAnEmptyLegIsRefused`

## CapFloorNullCashFlowTests

[`Divergences/CapFloorNullCashFlowTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorNullCashFlowTests.cs)

A cap on a leg holding a null cash flow. QuantLib builds the instrument, registering with each cash flow, and its `isExpired()` dereferences each (`capfloor.cpp:142-146, 172-177`), which is undefined for a null one; the wheel refuses `None` in a `Leg` ("in method 'new_Leg', argument 1 of type 'std::vector< ext::shared_ptr< CashFlow > > const &'"). nanoql refuses it at construction with `ArgumentException`, "null cash flow in leg", as a swap does, from every constructor, naming the leg; a `default` array, which isn't a leg, is refused alike.

Facts:

- `ANullCashFlowIsRefusedAtConstruction`

## CapFloorNullForwardTests

[`Divergences/CapFloorNullForwardTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorNullForwardTests.cs)

A caplet paid between the discount curve's reference date and the evaluation date. QuantLib's arguments store `Null<Rate>()` as the forward of a coupon paid before the evaluation date (`capfloor.cpp:245-249`), and the Black and Bachelier engines price every caplet paid after the curve's reference date (`blackcapfloorengine.cpp:93-100`, `bacheliercapfloorengine.cpp:78-86`), so on a curve dated before the evaluation date they price that caplet on the sentinel, 3.4028234663852886e+38: the wheel's NPV of this 1Y cap is 1.7146697606502935e+38, its first caplet's, on both engines; the Gaussian1d engine, whose caplets fixed by the model curve's reference date take the known payoff (`gaussian1dcapfloorengine.cpp:63-66`), 1.720316308005896e+38 on a GSR model (a = σ = 0.01) on the same curve. nanoql's forward is a `double?`, null there, and the engines refuse a priced caplet without one, with `NanoQLException`. On a curve dated on the evaluation date the caplet is skipped and both price it as QuantLib does: 0.010031644352251482, its ATM forward null where QuantLib's is the sentinel.

Facts:

- `APricedCapletWithoutAForwardIsRefused`

## CapFloorOvernightTests

[`Divergences/CapFloorOvernightTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorOvernightTests.cs)

The Gaussian1d cap/floor engine's compounded overnight coupons: an overnight index is an Ibor index, so QuantLib's engine prices a compounded coupon on its last day's forward times the whole accrual (`gaussian1dcapfloorengine.cpp:76-79`), no QuantLib route pricing a compounded caplet on a Gaussian1d model; on the wheel, a year's SOFR caplet at the money a year forward on this curve's GSR model (a = 0.03, σ = 0.009) is worth 5,781.30 per million, where the Monte Carlo below gives 3,949.84 ± 20.78 and nanoql 3,945.48. On GSR nanoql values it as a Black option on the window's compounding factor, lognormal given the state at the window's start, its log-variance the discrete sum's over the fixing dates. The checks, independent of QuantLib's engine: the variance against its direct double sum over the fixing dates on QuantLib's own GSR process; a Monte Carlo of the daily compounding under Hull-White on constant parameters (the same model), exact in the state and the path discount between fixings (Mersenne Twister, a fixed seed), within three standard errors, for a future caplet over a month and over a year, a seasoned one, and ones with a payment lag, an observation shift, a lookback without the shift and a lockout; and the payoff at a vanishing volatility, paid on the index's day counter and on another. On the Markov-functional model, which defines no short rate, the coupon is refused.

Facts:

- `TheWindowsVarianceIsTheDirectDoubleSum`
- `AFutureCapletOverAMonthIsTheMonteCarlos`
- `AFutureCapletOverAYearIsTheMonteCarlos`
- `ASeasonedCapletIsTheMonteCarlos`
- `ACapletPaidTwoDaysLateIsTheMonteCarlos`
- `ACapletWithAnObservationShiftIsTheMonteCarlos`
- `ACapletWithALookbackWithoutTheShiftIsTheMonteCarlos`
- `ACapletWithALockoutIsTheMonteCarlos`
- `AVanishingVolatilityGivesThePayoff`
- `TheMarkovFunctionalModelRefusesTheCoupon`

## CapFloorTermVolCurveDateTests

[`Divergences/CapFloorTermVolCurveDateTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorTermVolCurveDateTests.cs)

A cap/floor term-volatility curve after the evaluation date moves. QuantLib's moving curve recomputes its option dates and times in `update()` before its base clears the cached reference date (`capfloortermvolcurve.cpp:155-167`, `termstructure.cpp:48-52`), so after a move its times are those of the reference date the previous evaluation date gave, and its spline is over them: built on 16 June 2025 with two settlement days, tenors 1Y, 2Y and 3Y at 0.20, 0.21 and 0.22, and moved to 20 June, the wheel's curve has the reference date 24 June 2025 and the maximum date 26 June 2028, and its volatility at 1.5 years is 0.20500509503283154, its value before the move. nanoql's curves have fixed reference dates: the curve for 20 June is built under that date, and gives the value of a curve QuantLib builds on it, 0.20500761138338572.

Facts:

- `ACurveUnderALaterEvaluationDateIsANewCurve`

## CapFloorTermVolSurfaceDateTests

[`Divergences/CapFloorTermVolSurfaceDateTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorTermVolSurfaceDateTests.cs)

A cap/floor term-volatility surface after the evaluation date moves. QuantLib's moving surface recomputes its option dates in `update()` before its base clears the cached reference date (`capfloortermvolsurface.cpp:195-207`, `termstructure.cpp:39-52`), so after a move its dates are those of the reference date the previous evaluation date gave: built on 16 June 2025 with two settlement days and moved through 20, 25 and 30 June, the wheel's surface has the reference date 2 July 2025 and the option dates of 27 June's (29 June 2026, 28 June 2027, 27 June 2028, 27 June 2030), and its volatility at 1.5 years and 3.5% is 0.22743898630625792, at 30 June 2027 0.22282736247815785. nanoql's surfaces have fixed reference dates: the surface for 30 June is built under that date, and gives the values of a surface QuantLib builds on it: the option dates of 2 July (2 July 2026, 2 July 2027, 3 July 2028, 2 July 2030), 0.2273976824337007 and 0.22280134235888954.

Facts:

- `ASurfaceUnderALaterEvaluationDateIsANewSurface`

## CapFloorTermVolSurfaceEmptyMatrixTests

[`Divergences/CapFloorTermVolSurfaceEmptyMatrixTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorTermVolSurfaceEmptyMatrixTests.cs)

A cap/floor term-volatility surface on an empty matrix. QuantLib's constructors on quotes size their matrix from the first row, `vols_(vols.size(), vols[0].size())`, before any check (`capfloortermvolsurface.cpp:45, 77`), which is undefined for no rows: the wheel ends the process (0xC0000005 in all 18 recorded runs, with or without tenors). Its constructors on a matrix check it first, in `checkInputs` (`capfloortermvolsurface.cpp:154-177`): "empty option tenor vector", or "mismatch between number of option tenors (2) and number of volatility rows (0)", the wheel's texts. nanoql's quote and matrix forms are one, so an empty matrix reaches QuantLib's own checks, refused with `ArgumentException`.

Facts:

- `AnEmptyMatrixReachesQuantLibsChecks`

## CapPseudoDerivativeBracketTests

[`Divergences/CapPseudoDerivativeBracketTests.cs`](../tests/NanoQL.Tests/Divergences/CapPseudoDerivativeBracketTests.cs)

The cap pseudo-derivative's implied volatility. QuantLib solves for the flat undisplaced Black volatility in [0.99, 1.01] times its least displaced and greatest scaled caplet volatility (`swaptionpseudojacobian.cpp:331`), which away from the money on a displaced model misses the root: on six semi-annual rates, FlatVol 20%, 3 factors, displacement 2%, the cap from rate 1 to 5, its bracket [0.198, 0.332323] refuses strikes 0.5%, 1% and 2% "root not bracketed". nanoql solves by Brent's bracketing from QuantLib's guess there. Checks: the volatility reprices the model's cap through the class's undisplaced Black sum; QuantLib's bits where its bracket holds the root (3.2%).

Facts:

- `AwayFromTheMoneyTheVolatilityRepricesTheCap`
- `WhereQuantLibsBracketHoldsTheRootTheVolatilityIsQuantLibs`

## CapletCoterminalPeriodicTests

[`Divergences/CapletCoterminalPeriodicTests.cs`](../tests/NanoQL.Tests/Divergences/CapletCoterminalPeriodicTests.cs)

The periodic calibration's failures and passes (fixed). QuantLib returns the inner calibration's `calibrate` value, a `bool`, as its failure count (`capletcoterminalperiodic.cpp:125-126`), and its `++iterationsDone` runs only when the loop goes on (`:171`), one short of the passes when the errors end it: on QuantLib's case it returns 1, its inner calibration's `failures()` 0, and at caplets × 0.7 it returns 0 with 3 failures, each counting 5 passes with 6 rows of its volatility matrix filled. nanoql returns the inner failures and the passes run. Checks: the inner calibration's own failures on the last pass's variances, and the matrix's filled rows.

Facts:

- `TheFailuresAreTheInnerCalibrationsAndThePassesTheRowsFilled`

## CapletFixingExpiryTests

[`Divergences/CapletFixingExpiryTests.cs`](../tests/NanoQL.Tests/Divergences/CapletFixingExpiryTests.cs)

A caplet's expiry on the analytic and tree cap engines: QuantLib's analytic engine values a caplet still to be fixed as a bond option expiring at its accrual start (`analyticcapfloorengine.cpp:94-112`) and its tree exercises it there (`discretizedcapfloor.cpp:53-55`), where the instrument fixes its rate, QuantLib's Monte Carlo engine reads it and its Black engine takes the volatility, at the fixing date, so a caplet with a fixing lag is priced on the variance to its start. On the wheel, a 1-year semiannual cap at 4% on Euribor 6M from six months after the spot date, two fixing days, notional 1,000,000, on Hull-White (0.1, 0.01) on a flat 4% Actual/365 (Fixed) curve on 15 January 2025, the analytic engine prices 3,024.081283636271 and the tree (200 steps) 3,019.994708839063; QuantLib's own five-argument bond option (`hullwhite.cpp:108-131`), the option at the fixing on the bond from the start, which its Jamshidian engine uses, gives 3,005.838255466084. nanoql's tree values each caplet still to be fixed before its start at its fixing, on every model, from its two discount bonds rolled back there, and its analytic engine on Hull-White takes the five-argument option; Vasicek's, CIR's and G2's analytic options keep QuantLib's expiry at the start, since QuantLib has no such option for them. A caplet fixed on its start is QuantLib's on both engines. QuantLib's check: its five-argument bond option, its Monte Carlo engine, and the tree converging to both.

Facts:

- `TheHullWhiteAnalyticOptionExpiresAtTheFixing`
- `TheTreeConvergesToTheFixingValue`
- `TheMonteCarloEngineAgreesWithTheFixingValue`
- `OnVasicekTheAnalyticOptionKeepsQuantLibsExpiry`

## CapletInArrearsTests

[`Divergences/CapletInArrearsTests.cs`](../tests/NanoQL.Tests/Divergences/CapletInArrearsTests.cs)

An in-arrears caplet on the short-rate cap engines: QuantLib's analytic and tree engines value a caplet still to be fixed at its start on its accrual period, reading neither its later fixing nor its index's own period (`analyticcapfloorengine.cpp:94-112`, `discretizedcapfloor.cpp:53-55`), so they price an in-arrears cap as the cap in advance; its Monte Carlo engine forecasts the rate from the bond to the coupon's start, before the fixing (`mchullwhiteengine.cpp:80-90`). On the wheel, a 1-year semiannual cap at 4% on Euribor 6M in arrears, from six months after the spot date, two fixing days, notional 1,000,000, its coupons on a Black pricer at 20%, on Hull-White (0.1, 0.01) on a flat 4% Actual/365 (Fixed) curve on 15 January 2025: the analytic engine 3,024.081283636271 and the tree (200 steps) 3,019.994708839063, the in-advance cap's prices; QuantLib's Monte Carlo route (the port's before the fix, which reproduces it on its vectors) 4,063.1343135221905 ± 6.317811808654881 (200,000 antithetic samples, seed 42). No QuantLib route prices it, so the check is independent: each caplet's value at its fixing is a one-dimensional Gaussian integral under the fixing's forward measure, 3,885.098792896677 by quadrature to about 1e-15. nanoql's trees value an in-arrears caplet at its fixing on every model from three bonds, to its payment and to its index period's start and end; its Monte Carlo engine forecasts on the index's period and values the payment at the fixing; its analytic engine refuses it.

Facts:

- `TheTreeConvergesToTheGaussianIntegral`
- `TheMonteCarloEngineAgreesWithTheGaussianIntegral`
- `AtZeroVolatilityTheValueIsTheDeterministicPayoff`
- `TheAnalyticEngineRefusesIt`

## CapletVarianceCurveSmileTests

[`Divergences/CapletVarianceCurveSmileTests.cs`](../tests/NanoQL.Tests/Divergences/CapletVarianceCurveSmileTests.cs)

The caplet variance curve's smile sections: QuantLib's is a `FlatSmileSection(t, atmVol, dayCounter())` (`capletvariancecurve.hpp:86-91`), shifted lognormal with shift 0 whatever the curve's type and displacement, so a normal curve's section prices its normal volatility as a lognormal one. nanoql's section carries the curve's type and displacement; the check is the curve's own `CapletVarianceCurve.VolatilityType` and `CapletVarianceCurve.Displacement`.

Facts:

- `TheSectionsKeepTheCurvesTypeAndDisplacement`

## CappedFlooredCouponNullUnderlyingTests

[`Divergences/CappedFlooredCouponNullUnderlyingTests.cs`](../tests/NanoQL.Tests/Divergences/CappedFlooredCouponNullUnderlyingTests.cs)

A capped coupon over a null underlying. QuantLib's constructor reads the underlying's terms in its base initializer (`capflooredcoupon.cpp:30-43`), dereferencing the null pointer: a Boost assertion in the default build, undefined under `std::shared_ptr`. nanoql refuses it with `ArgumentNullException`.

Facts:

- `ANullUnderlyingIsRefused`

## CappedYoYEffectiveStrikeTests

[`Divergences/CappedYoYEffectiveStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/CappedYoYEffectiveStrikeTests.cs)

A capped year-on-year coupon's effective strike without its option: QuantLib's `CappedFlooredYoYInflationCoupon::effectiveCap` returns (C − b) / a whether or not the fixing is capped (`capflooredinflationcoupon.cpp:126-128`), and its constructor sets C only for a cap it was given (`:27-49`, the member declared at `.hpp:135`), so the effective cap of a coupon with no cap reads a member nothing set, undefined behaviour; its `effectiveFloor` likewise. On the wheel, the vectors' at-the-money coupons gave such values as 1.69e-306 and 1.43e-311, and 0.029 for the effective floor of the coupon with neither option, the floor of a coupon built before it. nanoql's is null, as QuantLib's `CappedFlooredCoupon`'s `Null<Rate>()` on the same members (`capflooredcoupon.cpp:123-135`). QuantLib's check: its effective strikes where the coupon has the option (the vectors), and its capped Ibor coupon's null where it hasn't.

Facts:

- `AnEffectiveStrikeIsNullWithoutItsOption`

## CappedYoYPricerStateTests

[`Divergences/CappedYoYPricerStateTests.cs`](../tests/NanoQL.Tests/Divergences/CappedYoYPricerStateTests.cs)

A capped year-on-year coupon read again: QuantLib's `CappedFlooredYoYInflationCoupon::rate` prices its caplet and floorlet on the pricer's state (`capflooredinflationcoupon.cpp:92-105`), which its swaplet rate sets only when first computed, the coupon's `InflationCoupon::rate` being cached (`inflationcoupon.cpp:64-75`), so with one pricer on a leg a coupon read again after another reads that other coupon's state: on the wheel, on the vectors' `one cap` leg (six annual coupons capped at 3.5%, the Black pricer), the first coupon's amount read after the leg's rates is 28,665.871612869618 on 1e6, where read first it is 30,024.190259687595, its rate's. QuantLib's capped Ibor and digital coupons read their underlying's pricer the same way after its cached rate, and its stripped coupon initializes the pricer first (`strippedcapflooredcoupon.cpp:46-47`). nanoql's pricers take the coupon on every call, so a coupon's amount is the same however often and in whatever order its leg is read. QuantLib's check: its amounts read in turn, each coupon's rate and amount before the next coupon's (the vectors).

Facts:

- `ACouponReadAgainAfterItsLegKeepsItsAmount`

## CappedYoYPricerTests

[`Divergences/CappedYoYPricerTests.cs`](../tests/NanoQL.Tests/Divergences/CappedYoYPricerTests.cs)

Setting an inflation pricer on a capped year-on-year coupon: QuantLib's `setCouponPricer` calls the non-virtual `InflationCoupon::setPricer` (`inflationcouponpricer.cpp`), so a `CappedFlooredYoYInflationCoupon` built on an underlying coupon gets the pricer on itself only, while its rate reads the underlying's rate and the underlying's pricer (`capflooredinflationcoupon.cpp:88-105`): the coupon keeps the underlying's old pricer, and the new one is unused. nanoql's copy carries the pricer into its underlying (`CappedFlooredYoYInflationCoupon`'s `WithPricer`). QuantLib's check: its coupon with the pricer set on both, which the coupon on its own terms is, priced by the pricer (the vectors): on the at-the-money coupon capped at 3.1%, the unit-displaced Black pricer's 0.026633082577577766, where QuantLib's coupon on an underlying holding the Black pricer keeps that pricer's 0.028900831678540094.

Facts:

- `ThePricerReachesTheUnderlying`

## CdiCloneTests

[`Divergences/CdiCloneTests.cs`](../tests/NanoQL.Tests/Divergences/CdiCloneTests.cs)

`Cdi`'s clone. QuantLib's `Cdi` overrides `forecastFixing` (`cdi.cpp:20-33`) but not `clone`, so `clone(h)`, "a copy of itself linked to a different forwarding curve" (`iborindex.hpp:60-62`), is a plain `OvernightIndex` of the same name, forecasting by the generic formula: on a flat 10.75% Business/252 curve its fixing for 28 March 2024 is 0.10752293232819721 against `Cdi`'s 0.11349086074650594, and an OIS helper bootstrapping a CDI curve clones its index. nanoql's `Cdi.Clone` returns a `Cdi`. QuantLib's check: `Cdi`'s own forecast on the new curve (QuantLib-Python 1.44).

Facts:

- `TheClonesForecastIsANewCdisOnItsCurve`
- `TheCloneIsACdi`

## CdiCompoundingTests

[`Divergences/CdiCompoundingTests.cs`](../tests/NanoQL.Tests/Divergences/CdiCompoundingTests.cs)

CDI's annual fixings in the overnight coupons. `Cdi` forecasts an annually compounded rate on Business/252 (`cdi.cpp:20-33`), CDI's quoting convention, and its published fixings are annual rates, while QuantLib's overnight pricers grow every fixing as a simple rate, `1 + r·τ` (`overnightindexedcouponpricer.cpp:118-126`), and sum `r·τ` in the averaging ones. A coupon's amount then depends on its route: on a flat 10% curve a coupon locked out for 2 or 5 days moves off the curve's telescopic amount by +37.986528 and +94.968959 on 1,000,000, where a generic index's doesn't; a seasoned coupon's past grows by 1.0083664854 where CDI's convention gives 1.0079741404; and the arithmetic pricer sums the annual rates as simple ones. nanoql's compounding pricers grow a fixing by the index's `OvernightIndex.GrowthFactor`, and its averaging pricers sum the index's `OvernightIndex.SimpleAccrual`: for `Cdi` `(1 + r)^τ` and `(1 + r)^τ − 1`, CDI's daily factor as B3's DI factor states it, and by default `1 + r·τ` and `r·τ`, so every other index keeps QuantLib's bits. The Black pricers' forward parts compare a daily cap's or floor's strike with a simple rate estimated from the curve; a strike quoted in CDI's annual convention, which caps the past fixings as quoted, is compared as its simple equivalent, where QuantLib compares it as quoted, about 47bp too high at 10%. QuantLib's checks: its own telescopic amount; `InterestRate(r, Business252, Compounded, Annual)`'s compound factor, over the past and, for the Black pricers' past parts, on the capped fixing; a generic index fed the simple-equivalent fixings `((1 + r)^τ − 1)/τ`, in the arithmetic pricer, and at the simple-equivalent strike, in the Black pricers' forward parts (QuantLib-Python 1.44). QuantLib's amounts are recorded beside nanoql's.

Facts:

- `ALockedOutCouponIsTheCurvesTelescopicAmount`
- `ASeasonedCouponGrowsItsPastAtTheAnnualRate`
- `TheArithmeticPricerSumsTheSimpleEquivalentFixings`
- `TheBlackCompoundingPricersPastPartGrowsTheCappedFixingsAtTheAnnualRate`
- `TheBlackAveragingPricersPastPartSumsTheCappedFixingsDailyGrowth`
- `TheBlackCompoundingPricersForwardPartReadsTheStrikesSimpleEquivalent`
- `TheBlackAveragingPricersForwardPartReadsTheStrikesSimpleEquivalent`
- `TheBlackCompoundingPricersForwardPartReadsTheSmileAtTheQuotedStrike`
- `TheBlackAveragingPricersForwardPartReadsTheSmileAtTheQuotedStrike`

## CdoLeverageTests

[`Divergences/CdoLeverageTests.cs`](../tests/NanoQL.Tests/Divergences/CdoLeverageTests.cs)

The CDO engines' leverage, additional fair premium and fair upfront: every leg and the upfront levered, the fair rates unlevered, the additional fair premium the instrument's, the fair upfront the side-invariant rate that zeroes the value over the first accrual's discount. QuantLib's values on the eight-name [3%, 7%] tranche at 2% running: unlevered, the mid-point fair premium 0.10676171982795336 and NPV −0.94866792850998161, the integral 0.10608680303646907 and −0.94135991232162608; at twice the notional, the mid-point's 0.053380859913976682 and −0.72998440531768671 (its premium leg alone levered), the integral's unchanged; the additional fair premium the instrument's negated; the buyer's fair upfront −0.2964587276593692 where the seller's is 0.2964587276593692, its NPV at it 1.8972059114097959; a forward start's NPV at its fair upfront −0.020592559070216399.

Facts:

- `TheLeveredLegsKeepTheUnleveredRates`
- `TheAdditionalFairPremiumIsTheInstruments`
- `TheFairUpfrontZeroesTheValueOnEitherSide`

## CdoRealizedDefaultsTests

[`Divergences/CdoRealizedDefaultsTests.cs`](../tests/NanoQL.Tests/Divergences/CdoRealizedDefaultsTests.cs)

A CDO and a random model on a basket with a realized default: the CDO's premium leg, upfront and remaining notional stand on the tranche's outstanding notional, and the random models simulate the live names alone. A (10) and B (20) alive and C (30) defaulted in June 2006 and settled at 0.4, the [10%, 50%] tranche from 1 January 2006: QuantLib refuses the pool model on the realized basket ("sizes differ: 3 vs 2"), and on the restated basket (A and B, [0, 40%]) its mid-point CDO at 1% upfront and 2% running reads NPV −0.28931799290637461, premium 0.98631843637350081, protection 1.3956199920495744, fair premium 0.025866624453865835 and remaining notional 12, its integral CDO NPV −0.28056449432303487; at correlation 0 its random model on the restated basket reads P(≥ 2) 0.0088 and on the realized one P(≥ 3) 0.0004. A basket whose first name has defaulted, the models' recoveries and rows differing by name, checks each model's restriction to the live rows, a seasoned CDO's paid first coupon and the random model's shifted loss distribution against QuantLib on its restated basket.

Facts:

- `TheCdoIsQuantLibsOnTheRestatedBasket`
- `TheRandomModelSimulatesTheLiveNames`
- `TheLhpModelPricesTheLiveNamesRecoveries`
- `TheRandomDefaultModelSimulatesTheLiveNamesRecoveries`
- `TheRandomModelsLossDistributionIsShiftedByTheRealizedTrancheLoss`
- `TheRandomLossModelPairsTheLiveNamesRows`
- `ASeasonedCdoStartsFromTheRealizedLoss`

## CdoReferenceDateTests

[`Divergences/CdoReferenceDateTests.cs`](../tests/NanoQL.Tests/Divergences/CdoReferenceDateTests.cs)

The CDO and the random models on default curves dated after the evaluation date: a default probability before a curve's reference date is 0 and a random model counts its days from the evaluation date, so T+2 curves read as their restatement from the evaluation date. QuantLib's mid-point CDO on T+2 curves throws ("negative time (-0.00821918) given") and on the restated curves reads NPV −0.94756528135798312, fair premium 0.10656135275585535; its random model counts from each curve's date, reading P(≥ 1, a year) 0.1215 on the T+2 curves and throwing on the restated ones ("t < 0", its day solver bracketing below 0 where the hazard is 0).

Facts:

- `TheCdoOnT2CurvesIsTheRestatedOnes`
- `TheRandomModelCountsFromTheEvaluationDate`
- `ASeasonedCdosUpfrontIsDiscountedFromTheEvaluationDate`

## CdsConventionalSpreadNullTests

[`Divergences/CdsConventionalSpreadNullTests.cs`](../tests/NanoQL.Tests/Divergences/CdsConventionalSpreadNullTests.cs)

The conventional spread where the engine leaves the fair spread null. QuantLib's `conventionalSpread` returns the results' fair spread unchecked (`creditdefaultswap.cpp:422`), so it returns `Null<Real>()`, 3.4028234663852886e+38, as a spread under either model: for `CreditFixture`'s expired swap, and for its live swap at a zero running spread, whose root is a zero hazard rate and no coupon leg. nanoql's results are `double?`, so QuantLib's sentinel has no form here (the forced-departure rule): nanoql refuses with `NanoQLException`, "instrument expired" for the expired swap and QuantLib's own "fair spread not available" for the live one.

Facts:

- `AnExpiredSwapIsRefused`
- `AZeroRunningSpreadIsRefused`

## CdsEmptyScheduleTests

[`Divergences/CdsEmptyScheduleTests.cs`](../tests/NanoQL.Tests/Divergences/CdsEmptyScheduleTests.cs)

A credit default swap on an empty schedule with no protection start. QuantLib's constructors initialize the protection start to `schedule[0]` before `init` checks the schedule (`creditdefaultswap.cpp:56, 81`), and `Schedule::operator[]` reads `dates_[i]` unchecked unless `QL_EXTRA_SAFETY_CHECKS` is defined (`schedule.hpp:169-173`): an empty vector's first element, which is undefined. On the wheel, both constructors end the process with an access violation (exit code 0xC0000005); given a protection start, both refuse with `init`'s "CreditDefaultSwap needs a non-empty schedule.". nanoql checks the schedule first, and refuses with that text, an `ArgumentException` naming `schedule`, whether or not a protection start is given.

Facts:

- `AnEmptyScheduleIsRefusedBeforeItsFirstDateIsRead`

## CdsEngineReferenceDateTests

[`Divergences/CdsEngineReferenceDateTests.cs`](../tests/NanoQL.Tests/Divergences/CdsEngineReferenceDateTests.cs)

The CDS engines' reads before a curve's reference date, on curves that settle after the evaluation date and on curves dated the evaluation date alike: QuantLib's integral engine integrates the default leg from the evaluation date, reading the discount and the default probability before a T+2 curve's reference date, both engines read the survival at a coupon paying before a T+2 hazard curve's date and the mid-point engine its default date or its period's end there, and both read a live coupon's past period (one paid after a weekend that ended it) before curves dated the evaluation date, where QuantLib's curves refuse a negative time. nanoql's engines read nothing accrued before a curve's reference date: a discount of 1, a survival of 1 and a default probability of 0, QuantLib's own premise for its two-date default probability. Each value is QuantLib's on the market restated from the evaluation date, a curve dated then that accrues nothing up to the T+2 date and is the T+2 curve after it (QuantLib 1.44), or, for curves dated the evaluation date, restated alike from the past period's start; QuantLib's refusals, with their times, are its values on the curves given.

Facts:

- `TheIntegralEngineOnAT2DiscountCurveIsQuantLibsOnTheRestatedCurve`
- `TheMidPointEngineOnAT2DiscountCurveIsQuantLibs`
- `TheMidPointEngineOnAShortFirstPeriodIsQuantLibsOnTheRestatedCurve`
- `TheIntegralEngineOnAT2HazardCurveIsQuantLibsOnTheRestatedCurve`
- `ACouponPayingBeforeAT2HazardCurveIsQuantLibsOnTheRestatedCurve`
- `ACouponPayingAfterAT2HazardCurveIsQuantLibs`
- `ALiveCouponWhosePeriodEndedBeforeCurvesDatedTheEvaluationDateIsQuantLibsOnTheRestatedCurves`
- `QuantLibsReadsBeforeTheReferenceDateAreRefused`

## CdsExpiredAccrualRebateTests

[`Divergences/CdsExpiredAccrualRebateTests.cs`](../tests/NanoQL.Tests/Divergences/CdsExpiredAccrualRebateTests.cs)

An expired credit default swap's accrual-rebate NPV. QuantLib's `setupExpired` zeroes seven results and leaves the accrual rebate's NPV unset (`creditdefaultswap.cpp:215-220`), so it reads an uninitialized member: on the wheel, for `CreditFixture`'s expired swap priced by the mid-point engine, 7.565977700541159e-307 in one fresh process and 1.9925540477164e-311 in another (QuantLib's defect). nanoql's expired results are all 0, the accrual rebate's NPV included.

Facts:

- `TheAccrualRebateNpvOfAnExpiredSwapIsZero`

## CdsImpliedHazardRateExpiredTests

[`Divergences/CdsImpliedHazardRateExpiredTests.cs`](../tests/NanoQL.Tests/Divergences/CdsImpliedHazardRateExpiredTests.cs)

The implied hazard rate of an expired credit default swap. QuantLib's `impliedHazardRate` calls the engine directly, without the expiry check (`creditdefaultswap.cpp:317-336`), and the engine finds every coupon occurred, so the objective is the target less a constant: for `CreditFixture`'s expired swap, under either model, a zero target returns the solver's guess, 0.016898148148148148, and a target of 1 fails to bracket ("unable to bracket root in 100 function evaluations …") (QuantLib's defect). nanoql refuses an expired swap with a `NanoQLException`, "instrument expired", QuantLib's text in `Swaption::impliedVolatility`.

Facts:

- `AZeroTargetIsRefused`
- `ANonZeroTargetIsRefused`

## CevAtExpiryTests

[`Divergences/CevAtExpiryTests.cs`](../tests/NanoQL.Tests/Divergences/CevAtExpiryTests.cs)

The CEV engine at a time of 0 (a run find). The formula's non-centrality x₀/t is infinite at t = 0, which Boost refuses ("Non centrality parameter is inf"), so QuantLib throws on an option exercised today with the reference date's events included; and an exercise before the discount curve's reference date throws QuantLib's "negative time" (`analyticcevengine.cpp:104-111`). nanoql reads the curve by date, nothing accrued before its reference date (the playbook's row for structures on another clock), and at t = 0 the value is the payoff at f0, the limit QuantLib's own values approach: a day out, its call at K 90 on f0 100 is 9.999178115968299 (QuantLib-Python 1.44; σ 20%, r 3% on Actual/365 (Fixed), from 15 October 2026).

Facts:

- `ExercisedTodayTheValueIsThePayoff`
- `ExercisedBeforeTheCurvesDateTheValueIsThePayoff`

## CevDensityBelowZeroTests

[`Divergences/CevDensityBelowZeroTests.cs`](../tests/NanoQL.Tests/Divergences/CevDensityBelowZeroTests.cs)

The CEV density below zero (a run find). Where the Sankaran guess's Brent fails, QuantLib's inverse starts a second Brent from the forward (`cevrndcalculator.cpp:105-120`), which can step below zero, where X(f) = f^(2(1 − β)) is NaN and Boost refuses it: QuantLib throws "Non centrality parameter is -nan(ind)" at a quantile that exists. nanoql's density and distribution are 0 below zero, the absorbing boundary, and QuantLib's values at and above it stand, so the second Brent finds the quantile to its accuracy, 1e-8 in the forward. The check: the distribution brackets the probability within that accuracy (QuantLib-Python 1.44; f0 100, α = σ·f0^(1 − β)).

Facts:

- `WhereQuantLibStepsBelowZeroTheQuantileIsFound`
- `BelowZeroTheDensityAndDistributionAre0`

## CevNearOneTests

[`Divergences/CevNearOneTests.cs`](../tests/NanoQL.Tests/Divergences/CevNearOneTests.cs)

The CEV engine and density near β = 1. At β = 1 `CEVCalculator`'s δ = (1 − 2β)/(1 − β) divides by zero (`analyticcevengine.cpp:36`) and Boost refuses the infinite degrees of freedom; near it Boost's non-central chi-squared refuses or runs for seconds, from |β − 1| ≤ 1e-4 at σ 5% and 0.1 years to 1e-6 at σ 80% and 10 years; `CEVRNDCalculator` refuses β = 1 (`cevrndcalculator.cpp:39`). nanoql prices Black's formula at Hagan's CEV-equivalent volatility (the SABR volatility at ν = 0) for |β − 1| < 1e-3, exactly Black's at β = 1; its density refuses the band but β = 1, where it is the lognormal density. QuantLib's check: its CEV formula where it still prices inside the band, and at the band's edge (QuantLib-Python 1.44; f0 100, σ 20%, r 3% on Actual/365 (Fixed), from 15 October 2026).

Facts:

- `InTheBandTheValueIsBlackAtHagansVolatility`
- `AtBetaOneTheValueIsBlacks`
- `TheDensityRefusesTheBand`
- `AtBetaOneTheDensityIsLognormal`
- `AtBetaOneTheDensitysInverseIsTheLognormals`

## CevTailTests

[`Divergences/CevTailTests.cs`](../tests/NanoQL.Tests/Divergences/CevTailTests.cs)

The CEV formula's small tails. `CEVCalculator::value` takes a tail as 1 − cdf (or Γ-ratio − cdf, `analyticcevengine.cpp:50-80`), which loses most of its digits where the tail is small: a 36-day call at K 150 comes out about 11% low. nanoql takes Boost's complement where QuantLib's difference is below 1e-3, and QuantLib's arithmetic and bits elsewhere. The check: Hagan's ν = 0 Black (QuantLib's `unsafeSabrLogNormalVolatility`), within 2% of the CEV value at |β − 1| = 1e-2 and 36 days, and QuantLib's FD CEV engine (200 × 2,000), 5.70e-11 and 6.83e-11 (f0 100, σ 20%, r 3% on Actual/365 (Fixed), from 15 October 2026).

Facts:

- `ASmallTailTakesTheComplement`
- `AboveTheGuardQuantLibsBitsStand`

## ChiSquaredSeriesRefusalTests

[`Divergences/ChiSquaredSeriesRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/ChiSquaredSeriesRefusalTests.cs)

`NonCentralCumulativeChiSquareDistribution` past QuantLib 1.44's series' range. When its first term falls below the normal range (`chisquaredistribution.cpp:54-55`), the series runs on subnormal numbers, then zeros; when the Poisson weight's start, exp(−ncp/2) (`:39-41`), falls below it, from ncp 1,416.8, every term carries fewer bits, and from ncp 1,490.3 every term is 0, which the stop (`:82`) passes as converged; and when its loop ends at `itrmax` with its bound still negative, the bound passes as converged (`:79-88`), so above df + 20,000 a partial sum returns. nanoql takes QuantLib's master's series, upstream's #2908: it skips the underflowing weights and terms by their logarithms, so the first two return the distribution's values, Boost's within upstream's 1e-10; and it refuses a loop that ends before its bound can be computed, "didn't converge", as nanoql refused it before.

Facts:

- `PastQuantLibsRangeTheSeriesIsUpstreams`
- `ASeriesStoppedUnconvergedAboveDfIsRefused`
- `ASeriesStoppedUnconvergedInTheLargeDfWindowIsRefused`

## ChoiCertainExerciseTests

[`Divergences/ChoiCertainExerciseTests.cs`](../tests/NanoQL.Tests/Divergences/ChoiCertainExerciseTests.cs)

Choi's engine at a negative effective strike (a refusal lifted). QuantLib's Choi engine refuses it ("effective strike should to be positive", `choiasianengine.cpp:92`), where its Turnbull-Wakeman engine prices the certain exercise in closed form, the discounted forward of the average less the strike: on spot 100, q 3%, r 6%, σ 20% (Act/360), six past fixings summing to 900 and six monthly fixings to come, a call at 60 prices 63.505838672811066 by Turnbull-Wakeman where QuantLib's Choi throws. nanoql's Choi engine prices it by Turnbull-Wakeman's branch, its value alone, as its other branches set. Check: Turnbull-Wakeman's value.

Facts:

- `ChoiPricesTheCertainExerciseAsTurnbullWakeman`

## CirFellerConstraintTests

[`Divergences/CirFellerConstraintTests.cs`](../tests/NanoQL.Tests/Divergences/CirFellerConstraintTests.cs)

The Cox-Ingersoll-Ross model's Feller constraint (K1): QuantLib's is σ's own `VolatilityConstraint`, built at the constructor's k and θ (`coxingersollross.cpp:26-40`, `:52`), so a calibration that moves k or θ tests σ against values the model no longer has, and can end outside the Feller region: on the wheel, from (r0, θ, k, σ) = (0.03, 0.035, 0.3, 0.05), Levenberg-Marquardt on the six swaption helpers of `tools/vectors/gen/models/model.py`'s 2025 set ends at θ = 536.3132523984605, k = 5.407661646569119e-07, σ = 0.033099933008455235, r0 = 0.02927429342689504, where σ² = 1.10e-3 exceeds 2kθ = 5.80e-4. nanoql tests σ² < 2kθ on each trial vector's own k and θ, through the model's parameter constraint. QuantLib's check: the test equals QuantLib's constraint built at each vector's k and θ; and a calibration with k and θ fixed, where the two tests agree at every trial, is QuantLib's to the last bit.

Facts:

- `TheTestIsQuantLibsConstraintAtEachVectorsSpeedAndLevel`
- `ACalibrationWithTheSpeedAndLevelFixedIsQuantLibs`
- `AFreeCalibrationStaysInTheFellerRegion`

## CirProcessMomentsTests

[`Divergences/CirProcessMomentsTests.cs`](../tests/NanoQL.Tests/Divergences/CirProcessMomentsTests.cs)

The Cox-Ingersoll-Ross process's moments: QuantLib's `variance` (`coxingersollrossprocess.cpp:32-38`) reads the constructor's x0 where the step's state is given, and its level term, θσ²(1 − e^(−kΔt))²/k, is twice that of its own `evolve`'s s2 (`coxingersollrossprocess.hpp:117-118`); its `diffusion` is the constant σ (`:94-96`), where the header's SDE is dr = k(θ − r)dt + σ√r dW. On the C++ harness, with k = 0.3, σ = 0.1, x0 = 0.04, θ = 0.05 and Δt = 0.25, the variance is 9.808235281791348e-05 at the states 0.01, 0.04 and 0.1 alike, and the diffusion 0.1. nanoql's variance is evolve's s2 at the state, written with (1 − e^(−kΔt))/k as Δt(1 − e^(−x))/x, x = kΔt, the Ornstein-Uhlenbeck process's cancellation-free form, and its diffusion σ√x. QuantLib's check: its own evolve formula, and the header's SDE.

Facts:

- `TheVarianceIsEvolvesS2AtTheState`
- `TheDiffusionIsTheSdes`

## CirTreeConvergenceTests

[`Divergences/CirTreeConvergenceTests.cs`](../tests/NanoQL.Tests/Divergences/CirTreeConvergenceTests.cs)

The Cox-Ingersoll-Ross trees: since 1.23 QuantLib's tree reads the model's dynamics, `CoxIngersollRossProcess` on the short rate itself (`coxingersollross.cpp:126-131`), whose variance depends on the state, but the trinomial tree spaces its nodes by the process's standard deviation at its initial value, so its prices stall away from the model's own analytic ones: on the wheel, a 3-year semiannual cap at 4% on Euribor 6M from six months after 15 January 2025, its caplets fixed on their starts, notional 1,000,000, on a flat 4% curve, prices 19,590.94789923626, 19,596.379468056322 and 19,571.725938834417 on CIR (r0 = θ = 0.04, k = 0.3, σ = 0.1) with 50, 200 and 800 steps, 2.3% above the analytic engine's 19,147.73327146658; and 20,232.01252305427, 20,225.765498780183 and 20,193.198962616287 on extended CIR (θ = 0.04, k = 0.3, σ = 0.1, x0 = 0.04), 2.8% above 19,674.220099061884. nanoql's trees are QuantLib's before 1.23, on y = √r, whose diffusion σ/2 is constant, the short rate y², the extended model's shift fitted numerically on the tree. QuantLib's check: its own analytic engine, which the trees converge to at first order. The caplets are fixed on their starts, where the analytic engine's option, which on CIR keeps QuantLib's expiry at the start, and the tree's, at the fixing, expire alike; QuantLib's engines don't read a caplet's fixing lag, so its prices are those of the two fixing days.

Facts:

- `TheCoxIngersollRossTreeConvergesToTheAnalyticPrice`
- `TheExtendedCoxIngersollRossTreeConvergesToTheAnalyticPrice`

## CliquetThetaTests

[`Divergences/CliquetThetaTests.cs`](../tests/NanoQL.Tests/Divergences/CliquetThetaTests.cs)

The cliquet and performance engines' theta. QuantLib's reads each period's rate over the period after its reset (`analyticcliquetengine.cpp:92-94`, `analyticperformanceengine.cpp:91-93`), where the period's value decays with its weight over the time before the reset: a one-period cliquet's theta 0.2813 where QuantLib's forward engine's is 0.0 on a dividend curve flat at zero before the reset. nanoql reads the rate to each reset. The check is QuantLib's forward and forward-performance engines on each period's forward-start option: the values and thetas summed period by period, on the rising market, whose dividends are 0 to three months.

Facts:

- `TheCliquetThetaIsTheForwardEnginesPeriodByPeriod`
- `ThePerformanceThetaIsTheForwardPerformanceEnginesPeriodByPeriod`

## CmaesTests

[`Divergences/CmaesTests.cs`](../tests/NanoQL.Tests/Divergences/CmaesTests.cs)

CMA-ES's generator, one per minimization, and its ranking of equal costs: QuantLib's optimizer keeps its Mersenne Twister as a member (`cmaes.cpp:50`), so a second minimization on the same object continues the first's stream, where nanoql's builds one from the seed in each minimization; and QuantLib ranks the samples with `std::sort` (`cmaes.cpp:190-191`), which MSVC runs as an insertion sort up to 32 elements and an introsort beyond, where nanoql sorts stably: equal costs keep QuantLib's order in a population of 32 or fewer, and their own beyond. QuantLib's values are its own class's.

Facts:

- `ASecondMinimizationRepeatsTheFirst`
- `EqualCostsKeepTheirOrderToAPopulationOf32`

## CmsCalibrationFixedReversionTests

[`Divergences/CmsCalibrationFixedReversionTests.cs`](../tests/NanoQL.Tests/Divergences/CmsCalibrationFixedReversionTests.cs)

A fixed reversion given to the CMS market calibration: QuantLib passes it through the transform's round trip, √(m²) = |m| (`cmsmarketcalibration.cpp:362-365` with `:188-191`; the matrix and parametric overloads `:430-433`, `:520-523`), and reports m (`:373-374`), so a negative reversion prices as its absolute value: on the wheel, on the vectors' market with the linear TSR pricer, a fixed −0.03 fits to an error of 0.010816623721669666, +0.03's, where QuantLib's own fixed path with no reversion given, on pricers built at −0.03, fits to 0.010817563120824606. nanoql passes the reversion as given. QuantLib's check: that path, the pricers' own reversion repriced as it stands (`:188-189`); here on nanoql's pricers, whose TSR prices on the SABR cube differ from QuantLib's.

Facts:

- `AGivenNegativeReversionPricesAsGiven`
- `TheMatrixAndParametricOverloadsPassItAsGiven`

## CmsCalibrationGuessTests

[`Divergences/CmsCalibrationGuessTests.cs`](../tests/NanoQL.Tests/Divergences/CmsCalibrationGuessTests.cs)

The CMS market calibration's guess with the reversion fixed: QuantLib's vector overload passes the betas to the optimizer untransformed on that path alone (`cmsmarketcalibration.cpp:359-366`), where its free path and its matrix and parametric overloads transform them (`:379-381`, `:426-427`), and reads the result back through the transform (`:372`), so a guess of β starts the fit at e^(−β²): on the wheel, on the vectors' market quoting the spreads the cube's own fit implies (0.0017046615749698334, 0.0018719617461963108, 0.0020272382060141435, 0.0022422177072925265), the fit from a guess of 0.5 returns a beta of 0.5000000003809772 and an error of 4.659445968967119e-16, and on those spreads 2bp lower a beta of 0.9999985865285671, another fit. nanoql transforms the guess on every path. QuantLib's check: its vector overload given the guess's transform, √(−ln 0.5), which starts the fit at a beta of 0.5: there a beta of 0.5000000000000001 and an error of 0, and 2bp lower 1e-06 and 0.00019999259008147676. The matrix overload, which transforms, calibrates a beta per swap length, another problem on this market, and refuses a market of one length ("not enough points to interpolate").

Facts:

- `TheFixedPathStartsAtTheGuess`
- `FromTheGuessItFindsQuantLibsFit`

## CmsCalibrationReversionTransformTests

[`Divergences/CmsCalibrationReversionTransformTests.cs`](../tests/NanoQL.Tests/Divergences/CmsCalibrationReversionTransformTests.cs)

The CMS market calibration's reversion transform: QuantLib fits the reversion through y = m² unconstrained and reads it back as √y (`cmsmarketcalibration.hpp:84-89`), so a fit that ends at y below 0 returns a NaN reversion beside finite betas and error: on the wheel, on the vectors' market, Simplex(0.1) fits with the reversion free on the Standard model, whose prices don't read it, return a NaN reversion on spreads from every guess of 0.05 to 1e-4 (a beta of 0.2425733030330998, an error of 0.011716914297575421), and finite ones on prices and forward prices. nanoql reads √|y|, QuantLib's map wherever y ≥ 0. QuantLib's check: its values on every fit, betas within 1e-7 relative, errors within 1e-12 relative, and the reversion within 1e-5 where QuantLib's is finite, an ill-conditioned fit's bounds: the C runtime and processor a fit runs on move its last bits, its beta 9.7e-12 relative on GitHub's windows-2022 runner, 5.4e-11 on macOS and 1.6e-8 on Linux, and its error up to 4.8e-15 (CI run 41146085965). On forward prices the two Simplex paths agree to the bit for 114 evaluations; at the 115th, at the same point, one SABR section fit of the recalibrated cube, the 10-year expiry on the 2-year tenor, lands on the other of two solutions QuantLib's own fit flips between when its beta moves one ulp (an ill-conditioned section fit, whose outputs the vectors' stability filter drops), the costs 3.5e-12 apart, and from there the fit lands 2.8e-8 away in beta.

Facts:

- `TheLevenbergMarquardtFitsKeepQuantLibsValues`
- `TheSimplexFitsHaveAFiniteReversion`

## CmsMarketInputRefusalTests

[`Divergences/CmsMarketInputRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/CmsMarketInputRefusalTests.cs)

`CmsMarket`'s quotes and weights of other sizes. QuantLib checks the first row of bid and ask spreads only, reading it unchecked when there is none (`cmsmarket.cpp:63-69`), and reads every row's two columns per index (`:80`); its weighted errors read the weights unchecked (`:207`, `:218`), its calibration checking them when it is built: each an undefined read past a row's or a matrix's end. nanoql refuses each on entry, the quotes before the first read.

Facts:

- `AnEmptyOrRaggedQuoteTableIsRefused`
- `WeightsOfAnotherShapeAreRefused`

## CmsSpreadCouponDiscountCurveTests

[`Divergences/CmsSpreadCouponDiscountCurveTests.cs`](../tests/NanoQL.Tests/Divergences/CmsSpreadCouponDiscountCurveTests.cs)

The lognormal CMS spread pricer without a coupon discount curve discounts each coupon on its own first swap index's curve. QuantLib's `initialize` fills its empty coupon curve handle with the first coupon's index's curve and keeps it (`lognormalcmsspreadpricer.cpp`, `couponDiscountCurve_ = …`), so one pricer serving coupons on different indexes discounts every later coupon on the first's curve: its price is the price on a pricer given that curve, which this fact computes as QuantLib's. Rates don't read the curve.

Facts:

- `EachCouponIsDiscountedOnItsOwnIndexCurve`

## CmsSpreadUnequalShiftsTests

[`Divergences/CmsSpreadUnequalShiftsTests.cs`](../tests/NanoQL.Tests/Divergences/CmsSpreadUnequalShiftsTests.cs)

The lognormal CMS spread pricer with unequal shifts, as QuantLib's master after 1.44 (#2874, taken ahead of the pin). QuantLib 1.44's `optionletPrice` branches on the strike where the integrand's sign turns on the displaced strike k + g₁s₁ + g₂s₂, and its negative branch shifts the second rate by the first's shift. On QuantLib 1.44 (#2874's set-up below, ρ 0.5) its caplets and floorlets are NaN at strikes 0.008 and 0.002 on shifts 0 and 0.01, and at −0.008 and −0.002 on shifts 0.01 and 0; the cap at −0.002 is 0.003275576228 on shifts 0 and 0.01, where #2874's case caches 0.0080778180, and put-call parity breaks by 2.032e-2 there and 2.040e-2 on shifts 0.01 and 0 at −0.012. nanoql's is #2874's: every price finite, parity to 1e-12, and #2874's two cached caplets to 1e-8. Equal shifts take the same branch either way, so QuantLib's `testCouponPricing` keeps 1.44's bits.

Facts:

- `TestShiftedLognormalDifferentShifts`

## CoinitialZedMatrixTests

[`Divergences/CoinitialZedMatrixTests.cs`](../tests/NanoQL.Tests/Divergences/CoinitialZedMatrixTests.cs)

The coinitial Z matrix (fixed). QuantLib's `SwapForwardMappings::coinitialSwapZedMatrix` scales each row from its diagonal rightward (`for (Size j=i; j<n; ++j)`, `swapforwardmappings.cpp:152-153`), the coterminal version's loop, though a coinitial swap's row is nonzero left of its diagonal, so those entries stay raw derivatives, blind to the displacement: on six semi-annual rates at 3% + 0.4% i, entry [5][0] is 0.174871 at displacements 0 and 2%, where the elasticity of S₅ + d in f₀ + d is 0.131951 and 0.146315. No QuantLib code calls it. nanoql scales every entry. Checks: each row is QuantLib's own `cmSwapZedMatrix(cs, i + 1, d)`'s first row, the same calls in the same order, bit for bit; and the bumped elasticity.

Facts:

- `EachRowIsTheConstantMaturityFirstRow`

## CommodityCashFlowAmountTests

[`Divergences/CommodityCashFlowAmountTests.cs`](../tests/NanoQL.Tests/Divergences/CommodityCashFlowAmountTests.cs)

`CommodityCashFlow.Amount` undiscounted. QuantLib's `amount()` returns the discounted amount (`commoditycashflow.hpp:56`), where `CashFlow::amount()` is documented as "the actual amount paid at the cash flow date" (`cashflow.hpp:62-66`), so a consumer reading these as cash flows discounts them twice: on a payer at 1.5, April's `amount()` is 496.72311389766833 for a payment of 500. nanoql's `CommodityCashFlow.Amount` is the 500, the discounted one `CommodityCashFlow.DiscountedAmount`. Checks: each amount times its discount factor is its discounted amount, and where the swap's curves are one, the discounted amounts sum to the NPV before the secondary costs.

Facts:

- `AnAmountIsWhatIsPaidOnTheDate`

## CommodityCurveTests

[`Divergences/CommodityCurveTests.cs`](../tests/NanoQL.Tests/Divergences/CommodityCurveTests.cs)

The commodity curve's node times from its reference date, and its basis curve read by date (fixed). QuantLib's moving-date curve follows the evaluation date, and its `setPrices` times the nodes from the first price's date (`commoditycurve.cpp:68-87`): with prices 1, 2 and 3 at ten, forty and seventy days it prices 2 at 35 and 39 days and 3 at 65 and 69, the next node's, where its constructor from the same dates prices 1 and 2. And it reads a basis curve at its own time, on its own reference date and day counter (`commoditycurve.hpp:205-219`): a basis dated thirty days later on Actual/360, at 1, 2 and 3 from thirty, sixty and 120 days, adds 2 at 45 and 59 days and 3 at 119, where the basis curve's own price there is 1, 1 and 2. nanoql times the nodes from the reference date (`CommodityCurve.Create`, a copy from `CommodityCurve.SetPrices`) and reads a basis at the date on its own clock; where the curves share their reference date and day counter both readings are QuantLib's.

Facts:

- `TheMovingCurvePricesAsTheCurveFromItsDates`
- `ABasisCurveDatedApartIsReadAtTheDate`

## CommodityIndexNoFixingsTests

[`Divergences/CommodityIndexNoFixingsTests.cs`](../tests/NanoQL.Tests/Divergences/CommodityIndexNoFixingsTests.cs)

An index with no fixings read as having no quote date (fixed). QuantLib's `lastQuoteDate()` reads `timeSeries().lastDate()`, which throws "empty timeseries" on an empty history, so the swaps and the future refuse an index with no fixings that the swaps' own branch prices from the forward curve with a warning (the payer swap throws on every unit). nanoql's `CommodityIndex.LastQuoteDate` is null there, and every date is priced from the forward curve, QuantLib's warning recorded. Check: the instruments on an index with no fixings price as on one whose fixings all precede their first period.

Facts:

- `AVanillaSwapOnAnIndexWithNoFixingsPricesFromTheCurve`
- `ABasisSwapOnTwoIndexesWithNoFixingsPricesFromTheCurves`
- `AFutureOnAnIndexWithNoFixingsPricesFromTheCurve`

## CommodityKnownFactorsTests

[`Divergences/CommodityKnownFactorsTests.cs`](../tests/NanoQL.Tests/Divergences/CommodityKnownFactorsTests.cs)

Two of QuantLib's known unit factors stored the right way round. QuantLib's `(LitreUnitOfMeasure, GallonUnitOfMeasure, 3.78541)` says one litre is 3.785 gallons and its `(GallonUnitOfMeasure, MBUnitOfMeasure, 1000 * 42)` that one gallon is 42,000 thousand-barrels (`unitofmeasureconversionmanager.cpp:115-122`), where NIST Handbook 44, Appendix C, gives one gallon as 3.785 411 784 litres and, "by custom", 42 gallons a barrel; so a direct lookup converts 1,000 litres into 3,785.41 gallons and 1,000 thousand-barrels into 2.381e-2 gallons, the values QuantLib's `testDirect` asserts, its "actual" built from the same backward numbers. nanoql stores one gallon as 3.78541 litres and one thousand-barrel as 42,000 gallons: 264.17 gallons and 42,000,000 gallons, which its `testDirect` asserts.

Facts:

- `AThousandLitresAreTwoHundredSixtyFourGallons`
- `AThousandThousandBarrelsAreFortyTwoMillionGallons`
- `TheTwoRowsAgreeWithTheBarrelsTheyTriangulateThrough`

## CommodityRegistryTests

[`Divergences/CommodityRegistryTests.cs`](../tests/NanoQL.Tests/Divergences/CommodityRegistryTests.cs)

The units of measure, commodity types and payment terms as values. QuantLib keeps a process-wide registry for each, keyed by a unit's name, a type's code and a term's name, and a second construction with a known key returns the first construction's data whatever it was given: on QuantLib 1.44 `UnitOfMeasure("Barrels", "B2", Mass)` after `("Barrels", "BBL", Volume)` has code BBL and type Volume, `CommodityType("Gasoil", "HOX")` after `("Heating Oil", "HOX")` is named Heating Oil, and `PaymentTerm("Term R", TradeDate, 10, TARGET)` after `("Term R", PricingDate, 5, NullCalendar)` counts 5 days from the pricing date on the null calendar. nanoql has no registry: each holds the data it is given. Equality stays QuantLib's: a unit by code, a type by code, a term by name.

Facts:

- `ASecondUnitWithAKnownNameKeepsItsOwnCodeAndType`
- `ASecondTypeWithAKnownCodeKeepsItsOwnName`
- `ASecondTermWithAKnownNameKeepsItsOwnEventOffsetAndCalendar`

## ComplexChooserEqualTermsTests

[`Divergences/ComplexChooserEqualTermsTests.cs`](../tests/NanoQL.Tests/Divergences/ComplexChooserEqualTermsTests.cs)

QuantLib's fix after v1.44 to the complex chooser engine (#2891, `bc3a54105`): its maturities measured from the evaluation date, where 1.44 measures them from the choosing date and its critical value from twice it (`analyticcomplexchooserengine.cpp:45-46`, `:88`, `:96`), −12% on a flat market; 1.44's own case passes by placing its maturities after the choosing date (`ChooserOptionTests`' moved line). nanoql takes the fix. The check is master's case, `testComplexChooserWithEqualTermsMatchesSimpleChooser`: a complex chooser with equal strikes and maturities is the simple chooser, within 1e-5, here until the pin moves.

Facts:

- `TestComplexChooserWithEqualTermsMatchesSimpleChooser`

## ComplexChooserTermStructureTests

[`Divergences/ComplexChooserTermStructureTests.cs`](../tests/NanoQL.Tests/Divergences/ComplexChooserTermStructureTests.cs)

The complex chooser engine on term structures. QuantLib evaluates Rubinstein's formula on one zero rate, dividend yield and volatility per maturity and solves its critical value on the times between the dates (`analyticcomplexchooserengine.cpp:161-179`), so on a rising market it is −19.5% off with #2891's defect; nanoql reads the variances to each date, the correlations from their ratios and the discounts by date, its critical spot solving the forward Black values from the choosing date, on West's bivariate normal. The check is the option's exact value at the choosing date, the larger of the call and the put there over the lognormal spot, within the critical value's accuracy, 1e-4 (QuantLib's Newton stops at 0.001 in the difference of the two); on a flat market, the exact value likewise and Haug's, QuantLib's master case, within its 1e-4.

Facts:

- `ARisingMarketMeetsTheExactValue`
- `AFallingMarketMeetsTheExactValue`
- `AFlatMarketMeetsTheExactValueAndHaugs`

## CompositeClockTests

[`Divergences/CompositeClockTests.cs`](../tests/NanoQL.Tests/Divergences/CompositeClockTests.cs)

The composite curve's second curve on another clock. QuantLib reads the second curve's zero rate at the composite's time (`compositezeroyieldstructure.hpp:136-137`), a time on the first curve's clock, which the second curve counts from its own reference date on its own day counter; QuantLib's case dates both curves 10 November 2017 on Actual/365 (Fixed) (`termstructures.cpp:542`). On the wheel, the difference of a 4%–5.5% zero curve and a 1%–3.5% one, both from 15 January 2025, gives the zero rate 0.025136986301369922 at a year with the second on Actual/360, where QuantLib's own composite over the second restated on Actual/365 (Fixed) gives 0.024722222222222062; with the second dated 30 days earlier, 0.020201342281879166 at five years, where the second implied at 15 January 2025 gives 0.01968474385438166. nanoql reads the second curve at the instant the composite's time denotes, its zero rate the one its discount there implies over that time. QuantLib's check: its own composite with the second curve restated on the first's reference date and day counter, at the restated curve's nodes.

Facts:

- `ASecondCurveOnAnotherDayCounterIsReadAtTheInstant`
- `ASecondCurveDatedEarlierIsReadAtTheInstant`

## CompoundNoCriticalSpotTests

[`Divergences/CompoundNoCriticalSpotTests.cs`](../tests/NanoQL.Tests/Divergences/CompoundNoCriticalSpotTests.cs)

The compound engine where the daughter is a put whose largest value, K_D·D(T_M, T_D) (95.123 here), doesn't exceed the mother's strike. QuantLib's Brent bracket [1e-6, 1000·K_D] holds no root and it refuses ("root not bracketed", `analyticcompoundoptionengine.cpp:91-95`), where the mother is never exercised (a call on the put is worth 0) or always (a put on the put is worth K_M·D(T_M) less the put today). nanoql prices those limits. The checks: the limits, exact; and just inside the domain (K_M 95), QuantLib's values within Drezner's accuracy, the put on the put within 0.06 of its limit at that strike.

Facts:

- `ACallOnThePutIsWorthNothing`
- `APutOnThePutIsTheDiscountedStrikeLessThePut`

## CompoundTermStructureTests

[`Divergences/CompoundTermStructureTests.cs`](../tests/NanoQL.Tests/Divergences/CompoundTermStructureTests.cs)

The compound engine on term structures. QuantLib solves its critical spot on the volatility at a date as far from the curves' start as the daughter's maturity from the mother's and on the discounts over that span, then reads the daughter's volatility at its maturity and the mother's at the mother's strike (`analyticcompoundoptionengine.cpp:73-95`, `:185-188`, `:258-262`), so on a rising market a put on a call is +1,090% off and its Greeks miss its own bumped values by −45% (delta) to +2,908% (vega); nanoql reads Geske's formula with the variances to each maturity at the daughter's strike, the correlation √(V_M/V_D) and the discounts by date, its critical spot solving the daughter's forward Black value over the mother's period, on West's bivariate normal, and takes the form's own Greeks. The checks: the option's exact value at the mother's maturity, the mother's payoff on the daughter's Black value over the lognormal spot, within Brent's accuracy, 1e-6; on a flat market, QuantLib's value, delta and theta within Drezner's accuracy, the value nearer the exact one, and its gamma and vega, which read no bivariate normal, within its last bits; and on rising and falling markets, the delta and gamma against bumps of the spot, the vega against a parallel shift of the volatilities and the theta against the evaluation date moved with each date's zero rate and volatility held.

Facts:

- `ARisingMarketMeetsTheExactValue`
- `AFallingMarketMeetsTheExactValue`
- `AFlatMarketIsQuantLibsWithinDreznersAccuracy`
- `TheGreeksOnARisingMarketMeetBumps`
- `TheGreeksOnAFallingMarketMeetBumps`

## ConstrainedEulerCovarianceTests

[`Divergences/ConstrainedEulerCovarianceTests.cs`](../tests/NanoQL.Tests/Divergences/ConstrainedEulerCovarianceTests.cs)

The constrained Euler evolver's step covariances (fixed). QuantLib's `LogNormalFwdRateEulerConstrained::setConstraintType` builds every step's covariances from the pseudo-root of the step it is called at (`lognormalfwdrateeulerconstrained.cpp:117`), so a later step shifts its forwards by another step's covariances and misses its own constraint, though the code states the intent ("shift each rate by multiplier * weighting of index rate across the step", `:191`): on FlatVol of a 3-month first period then semi-annual, each step's first alive rate constrained to its initial value plus 0.2% and the type set at step 0, the constrained forward lands at step 0 and misses by +0.00097, −0.00196, −0.00323 and −0.00857 at steps 1 to 4. nanoql builds step i's covariances from step i's pseudo-root. Checks: QuantLib's own evolver with its type set again before each step, so each step's covariances are its own (the vectors), bit for bit; and the constraint landing to rounding at every step.

Facts:

- `EachStepsConstraintLands`

## ConstrainedEulerDisplacementTests

[`Divergences/ConstrainedEulerDisplacementTests.cs`](../tests/NanoQL.Tests/Divergences/ConstrainedEulerDisplacementTests.cs)

The constrained Euler evolver's displaced constraint (fixed). QuantLib's `setThisConstraint` takes each step's constraint in logs displaced by `displacements_[i]` for step i (`lognormalfwdrateeulerconstrained.cpp:146`), the displacement of the rate with the step's index, not of the rate constrained, and past the rates when steps outnumber them: on eight steps over four rates displaced 0.1% to 0.4%, rate 0 constrained to 3.5% at step 1 (its type set at that step) lands at 3.6%. nanoql displaces each constraint by its constrained rate's displacement. Checks: on equal displacements QuantLib's arithmetic (the vectors), bit for bit; and the displaced constraint landing to rounding, at steps past the rates too.

Facts:

- `ADisplacedConstraintLands`

## ConventionalRecoveryNoSeniorityTests

[`Divergences/ConventionalRecoveryNoSeniorityTests.cs`](../tests/NanoQL.Tests/Divergences/ConventionalRecoveryNoSeniorityTests.cs)

ISDA's conventional recovery refused for `Seniority.NoSeniority`: QuantLib's `conventionalRecovery` indexes its five-entry table by the seniority (`recoveryratequote.hpp:36-37`), and `NoSeniority` is 5, past its end: an undefined read. The check is the table's five entries.

Facts:

- `NoSeniorityHasNoConventionalRecovery`

## ConvertibleClockTests

[`Divergences/ConvertibleClockTests.cs`](../tests/NanoQL.Tests/Divergences/ConvertibleClockTests.cs)

The binomial convertible engine's clock. QuantLib's engine flattens the volatility at the last exercise date on its own day counter, σ = blackVol(T), and its tree reads it over the risk-free curve's year fraction, so a volatility on another day counter integrates the wrong variance (`binomialconvertibleengine.hpp:80-125`): on the wheel, a 5-year zero-coupon convertible (conversion ratio 2, redemption 100, three settlement days) on a spot of 50, flat 5% and 2% curves on Actual/360 and a 15% volatility on Actual/365 (Fixed), from 15 January 2025, a credit spread of 0.5%, Cox-Ross-Rubinstein at 101 steps, prices 95.56792008293525, its price with the volatility read on Actual/360; on the market restated on one clock, the volatility 0.15·√(τ365/τ360) = 0.14896905994842624 on Actual/360, it prices 95.50250080004932. With QuantLib's master's spot at the settlement date (#2752, which nanoql takes ahead of the pin), master's engine prices 95.61364905182225 and 95.548271017185556 on the two markets (`Divergences/ConvertibleUpstreamFixesTests.cs`). nanoql flattens a structure on another day counter or reference date from its variance or discount between the risk-free curve's reference date and the last exercise date, over that interval on the risk-free day counter. QuantLib's check: its own engine on the restated market. The engine's NPV stays QuantLib's, the bond's value at its settlement date, which its settlement value equals: a named departure from the playbook's lattice clock, which QuantLib's convertible cases assert (`convertiblebonds.cpp:170`, `:178`, `:218`, against the plain bond's settlement value); on the wheel, the deep out-of-the-money convertible (a conversion ratio of 1e-16, 1,001 steps) prices 75.69370775331546, within 3e-3 of the plain zero-coupon bond's settlement value on the spreaded curve, 75.69077550691921, and 0.06 above its NPV at the curve's date, 75.63297824267259.

Facts:

- `AVolatilityOnAnotherDayCounterIsFlattenedOnTheRiskFreeClock`
- `TheNPVIsTheValueAtSettlement`

## ConvertibleUpstreamFixesTests

[`Divergences/ConvertibleUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/ConvertibleUpstreamFixesTests.cs)

The fix QuantLib's master made to the binomial convertible engine after the pin, which nanoql takes: #2752 (`d51f02be2`, issue #2701) rolls the spot to the bond's settlement date, `s0 /= D_r(settlement)`, after the dividends' present values are subtracted. The tree's times run from the settlement date, and QuantLib 1.44 starts its stock at the evaluation date's spot (`binomialconvertibleengine.hpp:93-104`), so a bond with a settlement lag prices its conversion on a spot short of the carry over the lag. QuantLib's check: master's engine itself, its header and its discretized convertible (#2878 among master's changes there) compiled, renamed, against the 1.44 install beside the install's own engine, on lagged bonds whose structures share the risk-free curve's day counter and reference date: a callable fixed-coupon convertible under each exercise on three trees, one with a dividend in the lag, and master's own case, each master's price within the tolerance and 1.44's past it. Master's case below passes on 1.44 too: a dividend in the lag is subtracted from the spot and never paid in the tree, as 1.44 has it. The vectors price each bond settling on the evaluation date, where the fix divides by a discount of 1.

Facts:

- `TestDividendBetweenEvaluationAndSettlement`
- `ALaggedBondIsPricedOnTheSettlementDatesSpot`

## ConvexMonotoneTests

[`Divergences/ConvexMonotoneTests.cs`](../tests/NanoQL.Tests/Divergences/ConvexMonotoneTests.cs)

The convex-monotone interpolation's sections. Hagan and West's method defines a section's forwards by two properties: the section averages its value, and the forwards are continuous at the nodes. QuantLib's split of a section's quadratic to keep it non-negative breaks both where it splits a quadratic that never dips below 0 inside the section; its averages' discriminant, 0 in exact arithmetic beside a floored end forward, rounds a hair below 0, and the unsplit quadratic dips below 0; and positivity forced on negative averages floors their sections and end forwards at 0, breaking the averages. nanoql splits only where the quadratic dips, takes a discriminant within 1e-12 bAv² of 0 as 0, and forces no positivity on a section of negative average or the end forward beside one. QuantLib's values are its own class's; each section is read through its own helper, as QuantLib's lookup takes the next section's helper at the node itself: its average by its primitive just before its right end, and its forward there as the limit of two readings before it.

Facts:

- `AQuadraticIsSplitOnlyWhereItDipsBelowZero`
- `EverySectionKeepsItsAverageAndTheForwardsTheirContinuity`
- `ADiscriminantRoundedBelowZeroIsZero`
- `NegativeAveragesKeepTheirAverages`
- `ANegativeRateCurveReadsNoSpuriousForward`

## CopulaEdgeTests

[`Divergences/CopulaEdgeTests.cs`](../tests/NanoQL.Tests/Divergences/CopulaEdgeTests.cs)

The copulas on the unit square's edges: every copula is defined on [0, 1]² with C(u, 0) = C(0, v) = 0, C(u, 1) = u and C(1, v) = v, and QuantLib's checks accept the edges, but its formulas miss them: Hüsler-Reiss is NaN at (0, 0) and wherever an argument is 1 but at (1, 1) (−ln 1 is −0, so the ratio's logarithm is that of −∞), and at θ 0 on every edge point but (1, 1); Galambos and Ali-Mikhail-Haq are NaN at (0, 0); the Gaussian copula is NaN at (0, 0) and (1, 1) at ρ 0.5, at (0, 1) and (1, 0) at −0.5, and 2e-10 off on the edges at 1; Clayton below 0 takes the power of a negative base. nanoql's copulas return the boundary values before their formulas. QuantLib's values are its own classes'.

Facts:

- `EveryCopulaTakesItsBoundaryValuesOnTheEdges`
- `QuantLibsFormulasMissTheEdges`

## CopulaFormTests

[`Divergences/CopulaFormTests.cs`](../tests/NanoQL.Tests/Divergences/CopulaFormTests.cs)

The copulas where QuantLib's formulas fail: Clayton's maximum taken after the power, so a negative base gives a positive value; Clayton's, Frank's and Plackett's formulas cancelling near independence and near the edges; Frank's overflowing at large |θ|; Gumbel's and Galambos' powers leaving the doubles' range at large θ; Clayton's overflowing at large θ near 0; Ali-Mikhail-Haq's denominator cancelling as θ nears 1 and its arguments 0; and Galambos' and Ali-Mikhail-Haq's product x·y underflowing near (0, 0), Galambos' to NaN where x and y are subnormal. nanoql's forms are within a few ulps of a high-precision evaluation of QuantLib's own formula (at 60 digits and more, and at 800), where QuantLib's double formula gives the values beside them.

Facts:

- `ClaytonsMaximumIsTakenBeforeThePower`
- `AliMikhailHaqKeepsItsDigitsNearItsBound`
- `FrankKeepsItsDigitsAtEveryTheta`
- `GumbelAndGalambosFactorTheirLargerTermAtLargeTheta`
- `GalambosAndAliMikhailHaqKeepTheirProductBelowTheNormalRange`
- `ClaytonKeepsItsDigitsNearIndependenceAndAtLargeTheta`
- `PlackettTakesTheStableRoot`
- `WhereQuantLibsFormulaHoldsItsValueIsKept`

## CopulaRngNearIndependenceTests

[`Divergences/CopulaRngNearIndependenceTests.cs`](../tests/NanoQL.Tests/Divergences/CopulaRngNearIndependenceTests.cs)

The Clayton and Frank copula generators near independence (fixed). Their conditional inverses cancel as θ → 0: QuantLib's Clayton draw is 7.1e-5 off at θ 1e-12 and 2.5e-7 at 1e-10, its Frank draw 2.4e-4 and 8.3e-8 (the references: 40-digit mpmath of QuantLib's own formulas on the same uniforms). nanoql's `log1p`/`expm1` forms below |θ| 1e-2 meet the references to the last bits; at and above 1e-2 the forms are QuantLib's.

Facts:

- `TheDrawsMeetTheirReferences`
- `QuantLibsFormsCancelAtTheta1e12`

## CosHestonLargeKappaTests

[`Divergences/CosHestonLargeKappaTests.cs`](../tests/NanoQL.Tests/Divergences/CosHestonLargeKappaTests.cs)

The COS Heston engine's cumulants past κt = 100. QuantLib's c₁ to c₄ (`coshestonengine.cpp:177-286`) take powers of e^{κt} whose products overflow: c₄ is NaN at κt = 200, c₃ from 300, c₂ at 400, and the engine's truncation range with them, so it prices NaN. nanoql evaluates each cumulant past its upper cut in an extended-range double, QuantLib's expression unchanged, and QuantLib's double, and so its bits, up to it. The check: QuantLib's expressions in Boost's 50-digit `cpp_bin_float_50`, QuantLib's cumulants at κt = 100, and QuantLib's analytic engine.

Facts:

- `PastTheUpperCutTheCumulantsAreTheFiftyDigitValues`
- `PastTheUpperCutThePriceMatchesTheAnalyticEngine`

## CosHestonSmallKappaTests

[`Divergences/CosHestonSmallKappaTests.cs`](../tests/NanoQL.Tests/Divergences/CosHestonSmallKappaTests.cs)

The COS Heston engine's cumulants at small κt. QuantLib's c₁ to c₄ (`coshestonengine.cpp:177-286`) are expansions in e^{κt} that cancel in double as κt falls: c₄ is off by 2.7% at κt = 5e-3, c₃ by 0.23% at 1e-3, c₂ by 600% at 1e-6 and c₁ by 8e-8 at 1e-9. nanoql evaluates each below its cut (2e-3, 0.2, 0.5 and 1), where QuantLib's double first departs from the expression's exact value by more than 1e-12 relative, in double-double, QuantLib's expression unchanged; below double-double's own floor (1e-9, 1e-6, 1e-3 and 5e-3) in quad-double; below quad-double's floor (1e-12, 1e-12, 5e-10 and 2e-7) by the cubic in κ through four quad-double values there; and in QuantLib's double, and so its bits, from the cut up. The check: QuantLib's expressions in Boost's `cpp_bin_float` at 100 digits, and at 250 below the double-double floors, QuantLib's cumulants at the cuts, and QuantLib's analytic engine.

Facts:

- `AtAndBelowTheCutTheCumulantsAreTheReferenceValues`
- `AtTheCutTheCumulantsAreContinuous`
- `AtASmallKappaThePriceMatchesTheAnalyticEngine`

## CosHestonSmallSigmaTests

[`Divergences/CosHestonSmallSigmaTests.cs`](../tests/NanoQL.Tests/Divergences/CosHestonSmallSigmaTests.cs)

The COS Heston engine's characteristic function at small σ. QuantLib's COS engine computes the classic form, divided by σ² (`coshestonengine.cpp:132-150`), which cancels as σ falls: its at-the-money price is off by 2.9e-11 relative at σ = 1e-3, 3.7e-5 at 1e-6 and 3.4% at 1e-8, where the analytic engine's Andersen and Lake form, QuantLib's own, holds. nanoql's COS engine takes the analytic engine's form of the same function below σ = 0.07, the cut where the classic form first departs from it by more than 1e-12 relative, measured on a grid of six parameter sets, ten u and nine times to 30 years; above it QuantLib's form and bits. The check: QuantLib's analytic engine, and the two forms within 1e-12 at the cut.

Facts:

- `BelowTheCutCosMatchesTheAnalyticEngine`
- `AtTheCutTheTwoFormsAgree`

## CouponPricerSettingTests

[`Divergences/CouponPricerSettingTests.cs`](../tests/NanoQL.Tests/Divergences/CouponPricerSettingTests.cs)

Setting pricers on a leg. QuantLib's `setCouponPricer` and `setCouponPricers` change the coupons in place, one after another (`couponpricer.cpp:461-466, 480-483`): a coupon that refuses the pricer leaves the earlier ones holding it. Their undefined steps: an empty pricer list on a non-empty leg reads the pricer before the list's start (`:481`), and a null cash flow is dereferenced (`:464, 482`; the first-matching forms swallow the failure and skip it, `:446-456`). nanoql returns a new leg, all or nothing, and refuses both inputs.

Facts:

- `ACouponRefusingThePricerLeavesNoNewLeg`
- `AnEmptyPricerListIsRefused`
- `ANullCashFlowInTheLegIsRefused`

## CpiLegBaseFixingTests

[`Divergences/CpiLegBaseFixingTests.cs`](../tests/NanoQL.Tests/Divergences/CpiLegBaseFixingTests.cs)

A CPI leg's base. QuantLib's `CPILeg` gives each coupon `laggedFixing(baseDate + lag, lag)` and the notional flow `laggedFixing(baseDate, 0M)` (`cpicoupon.cpp:112-121, 168-172`), so under a linear observation the notional flow misses the coupons' base unless the base date starts its month. With neither a base CPI nor a base date it gives every flow the base date start - lag (`:286-288`), so from a month-end start the coupons miss `laggedFixing(start, lag)` too, since (start - lag) + lag loses the start's day (QuantLib's defect). nanoql's leg gives every flow one base: with neither, `laggedFixing(start, lag, interpolation)`, QuantLib's own answer for the same leg built with that value as its explicit base CPI, which the wheel gives; with a base date, the coupons' `laggedFixing(baseDate + lag, lag, interpolation)`, the notional flow included, which QuantLib's `CPICashFlow` given that base fixing pays. On `InflationFixture`'s market, for a two-year annual leg of 1,000,000 at 2%, linear, lagged three months: from 15 May 2024 with neither base, the base is 310.30694493402456 and the notional flow pays 63,562.49459146323, where QuantLib's reads 310.34168149586054 and pays 63,443.44998172313; from 31 May, the base is 310.882579387307 and the coupons pay 20,637.587250557182 and 21,256.7148680739 and the notional flow 62,835.74340369502, where QuantLib's pay 20,642.36496136218, 21,261.63591020305 and 62,844.226063026195 on the base date 29 February 2024; from 15 May with the base date 15 March 2024, off the start less the lag, the base is 311.54246644409534 and the notional flow pays 59,344.59661315317, where QuantLib's reads 311.5223403181773 and pays 59,413.036336353194.

Facts:

- `TheNotionalFlowSharesTheCouponsBase`
- `AMonthEndStartKeepsItsDay`
- `ABaseDateGivesTheNotionalFlowTheCouponsBase`

## CpiPricerCouponTypeTests

[`Divergences/CpiPricerCouponTypeTests.cs`](../tests/NanoQL.Tests/Divergences/CpiPricerCouponTypeTests.cs)

`CPICouponPricer` given a coupon that isn't a CPI coupon. QuantLib's `initialize` casts the coupon without a check and then reads its fixed rate through the null pointer (`cpicouponpricer.cpp:111-112`), which is undefined; the wheel can't reach it. nanoql refuses with "CPI coupon needed", after the year-on-year pricer's "year-on-year inflation coupon needed" (`inflationcouponpricer.cpp:138`).

Facts:

- `AYearOnYearCouponIsRefused`

## CpiPricerFixedOptionletTests

[`Divergences/CpiPricerFixedOptionletTests.cs`](../tests/NanoQL.Tests/Divergences/CpiPricerFixedOptionletTests.cs)

`CPICouponPricer`'s optionlet once its fixing date is past. QuantLib then pays `max(indexFixing - strike, 0)`, a CPI level against a strike on the index ratio (`cpicouponpricer.cpp:84-94`), while before it the forward is `indexRatio(accrualEnd)` (`:104`; QuantLib's defect). nanoql's fixed branch reads the ratio, as the live one does. QuantLib's check is its `blackFormula` at zero deviation, the live branch's own formula at zero variance, on the coupon's `indexRatio`, which the wheel binds. On `InflationFixture`'s market, for a coupon of 1,000,000 at 3% on a base CPI of 301 from 1 October 2023 to 1 April 2024, lagged two months and fixed at 309.80326478740244, its ratio 1.0292467268684466: the caplet rate struck at 1.01 is 9.263797943622073 from QuantLib's pricer (the C++ harness) and 0.0005774018060533992 from `blackFormula` and nanoql; the floorlet rate struck at 1.05 is 0.0 from QuantLib's pricer and 0.0006225981939466019 from `blackFormula` and nanoql.

Facts:

- `AFixedOptionletPaysOnTheIndexRatio`

## CrossCurrencySwapCompoundedSpreadFairSpreadTests

[`Divergences/CrossCurrencySwapCompoundedSpreadFairSpreadTests.cs`](../tests/NanoQL.Tests/Divergences/CrossCurrencySwapCompoundedSpreadFairSpreadTests.cs)

The cross-currency swaps' fair spread on an overnight leg compounding its spread daily (fixed). Each swap derives a leg's fair spread from its BPS, `spread − NPV/(BPS/1bp)` (`constnotionalcrosscurrencybasisswap.cpp:180-187`, `constnotionalcrosscurrencyfixedvsfloatingswap.cpp:161-163`, `mtmcrosscurrencybasisswap.cpp:235-249`), which is exact only while the leg's amount moves one for one with its spread; compounded daily, the amount is convex in it. On a five-year basis swap paying SOFR compounded with a 2-day lookback (QuantLib's path that compounds the spread) against ESTR + 1%, QuantLib's fair pay spread leaves the swap worth −2,387.78 on 1,100,000. nanoql solves a compounding leg's fair spread on the swap's own pricing, as the float-float swap does for capped legs; a leg that doesn't compound keeps QuantLib's BPS route and its bits. QuantLib's check: its own NPV, whose Brent root the solved spreads equal (QuantLib-Python 1.44).

Facts:

- `TheBasisSwapsCompoundingLegsFairSpreadIsSolved`
- `TheFixedVsFloatingSwapsCompoundingLegsFairSpreadIsSolved`
- `TheMtMSwapsCompoundingLegsFairSpreadIsSolved`

## CrossCurrencySwapNpvDateFxTests

[`Divergences/CrossCurrencySwapNpvDateFxTests.cs`](../tests/NanoQL.Tests/Divergences/CrossCurrencySwapNpvDateFxTests.cs)

The constant-notional cross-currency engine's spot with an explicit FX settlement date and an NPV date away from the curves' reference date (fixed). QuantLib's engine takes its spot as the NPV date's rate, "w.r.t. a settlement equal to the npv date", and uses it as is when no FX settlement date is given (`discountingconstnotionalcrosscurrencyswapengine.hpp:54-67`, `.cpp:127`); given one, it moves the spot to the reference date (`.cpp:128-137`) and multiplies values discounted to the NPV date by it, so a USD 1.1M against EUR 1M two-year swap priced three months on is off its consistent NPV by −46.593470, with a settlement date of T+2 and with one on the reference date, which QuantLib reads as unset (`:74-77`). nanoql moves an explicitly settled spot to the NPV date, `S × D_dom(s)/D_for(s) × D_for(npv)/D_dom(npv)`, as QuantLib's own MtM engine does (`discountingmtmcrosscurrencybasisswapengine.cpp:103-121`); without one, and with the NPV date on the reference date, QuantLib's bits stand. QuantLib's check: its own engine with no settlement date and the spot moved to the NPV date, 26,787.661557906977 (QuantLib-Python 1.44).

Facts:

- `AnExplicitFxSettlementDateMovesTheSpotToTheNpvDate`
- `AnExplicitFxSettlementDateOnTheReferenceDateMovesTheSpotToo`

## CrossCurrencySwapZeroBpsTests

[`Divergences/CrossCurrencySwapZeroBpsTests.cs`](../tests/NanoQL.Tests/Divergences/CrossCurrencySwapZeroBpsTests.cs)

The constant-notional cross-currency swaps' fair spread and fixed rate on a leg whose BPS is zero (fixed). Both swaps test the leg's BPS against `Null` alone before dividing by it (`constnotionalcrosscurrencybasisswap.cpp:180-187`, `constnotionalcrosscurrencyfixedvsfloatingswap.cpp:157-163`), so a zero-notional leg's fair pay spread and fair rate are an infinity. nanoql gives null, the results' "not available", as QuantLib's sibling swaps do with their `legBPS != 0` checks (`fixedvsfloatingswap.cpp:201-207`, `floatfloatswap.cpp:560`, `:566`; `mtmcrosscurrencybasisswap.cpp:238-246`, which refuses); the other leg's keeps QuantLib's value. QuantLib's check: those siblings, and its `testZeroBpsFairSpread`, which asserts the float-float swap's "fair spread 1 not available" (QuantLib-Python 1.44).

Facts:

- `TheBasisSwapsFairPaySpreadOnAZeroNotionalLegIsNull`
- `TheFixedVsFloatingSwapsFairRateOnAZeroNotionalLegIsNull`

## CubeAtmCloneTests

[`Divergences/CubeAtmCloneTests.cs`](../tests/NanoQL.Tests/Divergences/CubeAtmCloneTests.cs)

The cube's at-the-money strike (#2849): QuantLib 1.44 builds a plain SwapIndex from the base index's conventions (`swaptionvolcube.cpp:89-145`), where its master, after the pin, clones the base to the tenor (`base->clone(swapTenor)`, merged as 87065c808), so a base index that overrides `clone` gives its own clone's fixing. For every swap index QuantLib defines the two agree, the overnight-indexed one included, since none overrides `clone(tenor)`; nanoql's cube clones the base, as the master. QuantLib's check: the master's code, and for QuantLib's indices 1.44's bits.

Facts:

- `TheAtmStrikeIsTheBaseIndexsClonesFixing`

## CubeAtmExtrapolationTests

[`Divergences/CubeAtmExtrapolationTests.cs`](../tests/NanoQL.Tests/Divergences/CubeAtmExtrapolationTests.cs)

A cube's at-the-money structure's extrapolation. QuantLib's cube turns extrapolation on in the structure it is given (`swaptionvolcube.cpp:68`), for every holder of it: on the wheel, the at-the-money matrix refused a 40-year option before a cube was built on it ("date (June 16th, 2065) is past max curve date (June 16th, 2055)") and returned 0.09799630288922362 after, at a strike of 3%, while the cube's own flag stayed off. nanoql's cube holds a copy of the structure with extrapolation enabled, and leaves the caller's structure as it was.

Facts:

- `TheCallersStructureIsLeftUnchanged`

## CubeIndexTests

[`Divergences/CubeIndexTests.cs`](../tests/NanoQL.Tests/Divergences/CubeIndexTests.cs)

A cube's per-strike inspectors given an index past the strike spreads. QuantLib checks neither index: the SABR cube's `marketVolCube(i)` (`sabrswaptionvolatilitycube.hpp:218-220`) and the interpolated cube's `volSpreads(i)` (`interpolatedswaptionvolatilitycube.hpp:67`) read past their vectors of matrices: the wheel's interpolated cube returned an 8237205143316950851 × 7008297511184330863 "matrix" for `volSpreads(5)` on QuantLib's five strike spreads. nanoql refuses the index with `ArgumentOutOfRangeException`.

Facts:

- `ASabrCubesMarketVolatilitiesRefuseAnIndexPastTheStrikes`
- `AnInterpolatedCubesSpreadsRefuseAnIndexPastTheStrikes`

## CubicNotAKnotTwoPointsTests

[`Divergences/CubicNotAKnotTwoPointsTests.cs`](../tests/NanoQL.Tests/Divergences/CubicNotAKnotTwoPointsTests.cs)

A cubic spline on 2 points with a not-a-knot end condition: QuantLib 1.44's rows for that condition read `dx_[1]` and `dx_[n_-3]` (`cubicinterpolation.hpp:428-431,460-463`), past the end of `dx_`, whose one element is the only interval, a result C++ leaves undefined, and on 3 points two not-a-knot ends make the system singular. QuantLib's master refuses both at construction, a not-a-knot end needing 3 points and two needing 4 (#2888, `7b21b6145`), which nanoql takes: an `ArgumentException` on `x` with QuantLib's text, as nanoql maps the constructor's Lagrange and Akima counts beside it, directly and through the log-cubic interpolation, which builds the same spline. Its place is master's, among the constructor's checks, so it comes before a Periodic end's "not implemented yet", which update raises. One departure from master: the count applies under the spline alone, where master's applies under every scheme, as only the spline's rows read the conditions (`cubicinterpolation.hpp:425`, `:457`), so the local and overshooting schemes keep QuantLib 1.44's values on 2 and 3 points, which the C++ vectors check, where master refuses them; a refusal of inputs 1.44 prices right fixes no defect.

Facts:

- `ANotAKnotSplineOnTwoPointsIsRefused`
- `TestNotAKnotWithFewPoints`
- `ACubicCurveWithANotAKnotEndBuildsThroughTheFirstPassFallback`

## CurrencyNameAndCodeTests

[`Divergences/CurrencyNameAndCodeTests.cs`](../tests/NanoQL.Tests/Divergences/CurrencyNameAndCodeTests.cs)

Two currencies with one name. QuantLib's `operator==` compares names alone (`currency.hpp:179-182`), and it names both BGL, the lev of 1962 (numeric code 100), and BGN, the lev of 1999 (975), "Bulgarian lev" (`europe.cpp:35`, `:389`): QuantLib takes them for one currency, so 1,000 BGL plus 1 BGN is 1,001 BGL, a EUR/BGN rate exchanges 1,000 BGL into 511.29 EUR, a lookup from BGL to BGN is 1.0 whatever the registry holds, and a forward from BGL to BGN is refused as one currency. nanoql compares currencies by name and code, so they are two; each expected value is QuantLib's own with BGL renamed apart ("Bulgarian lev (1962)"), its data otherwise QuantLib's. The exchange-rate registry tells currencies apart by QuantLib's numeric code and chains as QuantLib's `chain` does, so a currency equal to another with another numeric code is chained as QuantLib chains it.

Facts:

- `BglAndBgnAreTwoCurrencies`
- `TheRegistryTellsCurrenciesApartByTheirNumericCodesAsQuantLibs`

## CurveRecalculationTests

[`Divergences/CurveRecalculationTests.cs`](../tests/NanoQL.Tests/Divergences/CurveRecalculationTests.cs)

A bootstrapped curve is always built afresh, under the context it is given. QuantLib's piecewise curve is a lazy object that recalculates when a quote or the evaluation date changes, and its recalculation differs from a fresh bootstrap in three ways: it starts from its previous data (`iterativebootstrap.hpp:211-218, 253-255`; `globalbootstrap.hpp:340-347, 387-390, 409-411`), it re-runs `initialize()`, which takes the pillar dates from the helpers, only on first use or for a moving curve (`iterativebootstrap.hpp:224-231`), and a moving curve never re-runs `YieldTermStructure::update()`, which re-sets its jumps (`piecewiseyieldcurve.hpp:220-231`, `yieldtermstructure.cpp:174-189`). nanoql's curve is QuantLib's fresh curve in each case. The expected values are the wheel's.

Facts:

- `ACurveOnAChangedQuoteIsBootstrappedAfresh`
- `AGlobalCurveOnAChangedQuoteIsBootstrappedAfresh`
- `AFixedReferenceCurveTakesItsContextsPillars`
- `AMovingCurvesJumpsFollowItsReferenceDate`

## CvaEngineFixingLoopTests

[`Divergences/CvaEngineFixingLoopTests.cs`](../tests/NanoQL.Tests/Divergences/CvaEngineFixingLoopTests.cs)

The CVA engine's loop over the fixed payment dates: QuantLib's `CounterpartyAdjSwapEngine` skips the dates before the counterparty curve's reference date with `while (*nextFD < priceDate) ++nextFD;` (`cvaswapengine.cpp:128`), with no end check, so when every fixed payment is before that date it reads past the dates, undefined behaviour. nanoql's loop stops at the end, and the swap is then worth its NPV, its fair rate the swap's. QuantLib's checks: its values where its loop stays in range (the C++ harness's vectors, a seasoned swap among them); and a seasoned swap against a fresh one with the same remaining flows, which QuantLib's loop prices alike, the seasoned swap skipping its past payments: the vectors' seasoned 5-year payer from 16 April 2022 at its fair rate, -1,880.3521932368576 and 0.025867936451198614, restarted on 16 April 2024.

Facts:

- `ASeasonedSwapSkipsItsPastPayments`
- `EveryPaymentBeforeThePriceDateLeavesTheSwapsValue`

## CvaSwaptionStrikeTests

[`Divergences/CvaSwaptionStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/CvaSwaptionStrikeTests.cs)

The CVA engine's swaptionlets: QuantLib strikes every swaptionlet and its reverse at the swap's risk-free fair rate, `-baseSwapRate * legNPV[1] / legNPV[0]` (`cvaswapengine.cpp:147`, `:170`, `:179`), which the fixed rate cancels out of, so its adjustment is the same whatever the swap's rate. On the harness, on the vectors' 5-year payer from 17 January 2025 on 1e7 (Black 15%, a flat 2% hazard rate, 40% recovery, no investor curve), QuantLib's adjustment is -2,625.223121590 at 1%, at the fair rate 0.03147227737464738 and at 5%, where the swap is worth 990,581.19 and -854,739.96, and the receiver's -2,617.287664647 at each. nanoql strikes them at the swap's fixed rate, the contract's own exposure: its adjustment is QuantLib's at the fair rate (the harness's vectors, each swap at its fair rate), and follows the exposure off par: -21,968.948298009 at 1% and -75.016505939 at 5%. The independent check off par is the swaptions' parity: each period's payer and receiver swaptions differ by the forward swap at their strike, so a payer swap's adjustment less the receiver's at one rate is minus the forward swaps at that rate weighted by the parties' losses given default over the periods, at every volatility. The fair rate keeps QuantLib's, its first-order root of the adjusted NPV on swaptionlets struck at the risk-free fair rate: QuantLib's 0.031415371873538545 at 1%, 0.03141537187353853 at the fair rate and 0.03141537187353854 at 5%, nanoql's to the bit, the adjusted NPV at it -26.87; on the contract's strikes it would be 0.030996068704439883 at 1%, the adjusted NPV at it 19,112.14, and 0.03147065128358219 at 5%.

Facts:

- `OffParTheAdjustmentFollowsTheExposure`
- `APayerLessAReceiverIsTheForwardSwapsAtTheRate`
- `TheFairRateIsQuantLibsAtEveryRate`

## DataParserStrictnessTests

[`Divergences/DataParserStrictnessTests.cs`](../tests/NanoQL.Tests/Divergences/DataParserStrictnessTests.cs)

Malformed input to the data parsers. QuantLib reads the numbers with `std::stoi`, which skips leading blanks and stops at the first non-digit: `DateParser::parseISO` checks only the length and the dashes (`dataparsers.cpp:106-114`), so "2006-1a-15" parses as 15 January 2006 and "2006-ab-15" throws `std::invalid_argument` past QuantLib's checks; `PeriodParser` reads a period's number from its first sign or digit (`:77-81`), so "x5D" and "5xD" parse as 5D. ISO 8601's YYYY-MM-DD is four, two and two digits, and QuantLib's own texts state the grammar: nanoql refuses a field or a number whose text isn't digits (a period's with an optional sign), with QuantLib's texts, "invalid format" and "unable to parse the number of units of ... in '...'". Every well-formed input parses as QuantLib's.

Facts:

- `AnIsoDateFieldMustBeDigits`
- `APeriodsNumberMustBeASignAndDigits`

## DateAdvanceOverflowTests

[`Divergences/DateAdvanceOverflowTests.cs`](../tests/NanoQL.Tests/Divergences/DateAdvanceOverflowTests.cs)

Advancing a date by a length whose product overflows `int`: QuantLib computes `7*n` weeks, `month + n` months and `year + n` years in `Integer` (`date.cpp:172-201`), which overflows for lengths near the type's range, undefined. nanoql computes in `long` and refuses the out-of-range result with its range messages, so no change was needed; this test pins it.

Facts:

- `AnOverflowingAdvanceIsRefusedByTheDateRange`

## DateIntervalTests

[`Divergences/DateIntervalTests.cs`](../tests/NanoQL.Tests/Divergences/DateIntervalTests.cs)

`DateInterval.IsDateBetween` with its flags as named (fixed). QuantLib's `else if` follows the `includeFirst` test, so a start date that passes `date >= startDate` then fails `date > startDate`, and the end likewise (`dateinterval.hpp:51-63`): on 1 to 31 January 2025 both ends are excluded under all four flag pairs, the middle included. nanoql includes an end exactly when its flag says. Check: every date of the interval and a day either side, under the four pairs.

Facts:

- `EachEndIsIncludedAsItsFlagSays`
- `TheDefaultFlagsIncludeBothEnds`

## DatedFxSwapRateHelperTests

[`Divergences/DatedFxSwapRateHelperTests.cs`](../tests/NanoQL.Tests/Divergences/DatedFxSwapRateHelperTests.cs)

An FX swap helper built from dates has no fixing days, convention or end-of-month flag, and its `FxSwapRateHelper.FixingDays`, `FxSwapRateHelper.BusinessDayConvention` and `FxSwapRateHelper.EndOfMonth` refuse. QuantLib's dated constructor leaves the three members uninitialized (`ratehelpers.cpp:772-785`) and its inspectors return them (`ratehelpers.hpp:398-401`): reading an indeterminate value, a result C++ leaves undefined.

Facts:

- `ADatedFxSwapHelperRefusesItsFixingDaysConventionAndEndOfMonth`

## DefaultRoundingTests

[`Divergences/DefaultRoundingTests.cs`](../tests/NanoQL.Tests/Divergences/DefaultRoundingTests.cs)

QuantLib's default `Rounding()` initializes only its type, to `None`: `precision_` and `digit_` have no initializer (`rounding.hpp:74, 87, 89`), so `precision()` and `roundingDigit()` read indeterminate values, a result C++ leaves undefined. nanoql's default rounding has no precision or digit: `Rounding.Precision` and `Rounding.RoundingDigit` throw `NanoQLException`. Its type and its rounding are QuantLib's: `RoundingType.None`, and every value back unchanged.

Facts:

- `ADefaultRoundingHasNoPrecisionOrDigit`

## DeltaSurfaceMismatchedTimesTests

[`Divergences/DeltaSurfaceMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/DeltaSurfaceMismatchedTimesTests.cs)

The delta-quoted volatility surface's curves on another clock (the mismatched-dates class). QuantLib reads its domestic and foreign curves at the surface's own times (`blackvolsurfacedelta.cpp:96-97`, `:210`), so curves on another day counter, or dated otherwise, are read at other instants: under a surface on Actual/365 (Fixed) from 15 October 2026, curves at 4% and 1% on Actual/360 give a one-year forward of 1.2159363500651499 where the same curves restated on Actual/365 (Fixed), each rate times 365/360 and so the same discount at every date, give 1.2164430957757022; and QuantLib's own cases' curves, floating from 1 January 2010, a TARGET holiday, and so dated 4 January, are read three days on: their one-month at-the-money volatility is 0.13010360399762275, where the curves restated on the surface's clock give 0.13008061079668704 (QuantLib-Python 1.44). nanoql reads each curve at the instant a time denotes, on its own clock; QuantLib's check, its surface on the curves restated on the surface's clock, gives nanoql's values. Where the curves share the surface's day counter and reference date, QuantLib's bits stand. QuantLib's cases on the floating curves are moved to the fixed surface's values (`TestSuite/BlackVolSurfaceDeltaTests.cs`).

Facts:

- `OnCurvesOnAnotherDayCounterItReadsThemAtTheInstants`
- `OnQuantLibsFloatingCurvesItReadsThemAtTheInstants`
- `WhereTheCurvesShareTheSurfacesClockItIsQuantLibs`

## DeltaSurfaceSmileSectionTests

[`Divergences/DeltaSurfaceSmileSectionTests.cs`](../tests/NanoQL.Tests/Divergences/DeltaSurfaceSmileSectionTests.cs)

The delta-quoted volatility surface's smile section and its forward. QuantLib's `blackVolSmile` passes the at-the-money volatility as the section's at-the-money level, or 1 without an at-the-money quote (`blackvolsurfacedelta.cpp:118`, `:135`), which `InterpolatedSmileSection` takes as the forward (`interpolatedsmilesection.hpp:52-58`) and its option prices, vega, digital prices and density read as the forward (`smilesection.cpp:78`, `:120`, `:124`, `:135`): on QuantLib's cases' surface, the six-month section prices on a forward of 0.11, the at-the-money volatility, where the surface's own forward, its `atmLevel(t)`, is S·P_f/P_d, about 1.17. nanoql's section takes the surface's forward; its volatilities are QuantLib's. The checks: the forward from QuantLib's curves, and the section's prices against Black's formula at the section's own volatilities.

Facts:

- `TheSectionPricesOnTheSurfacesForward`

## DeltaVolQuoteAtmDeltaTests

[`Divergences/DeltaVolQuoteAtmDeltaTests.cs`](../tests/NanoQL.Tests/Divergences/DeltaVolQuoteAtmDeltaTests.cs)

The ATM quote's delta. QuantLib's ATM constructor leaves `delta_` uninitialized (`deltavolquote.cpp:32-37`), so its `delta()` reads whatever the memory held: on the wheel, after a 25-delta quote, 0.25 (QuantLib-Python 1.44). Here an ATM quote has no delta, `null`; the delta-and-volatility constructor's is the one given, as QuantLib's. QuantLib's check: that constructor, unchanged.

Facts:

- `AnAtmQuoteHasNoDelta`

## DengLiZhouNegativeStrikeTests

[`Divergences/DengLiZhouNegativeStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/DengLiZhouNegativeStrikeTests.cs)

Deng, Li and Zhou's engine at a negative strike with exactly one negative weight. QuantLib adds |K| as a zero-volatility asset of weight +1 and lumps it with the positive leg into one lognormal (`denglizhoubasketengine.cpp:86-96`, `:117-140`), where the payoff's identity, the call on (w, K) the put on (−w, −K), reaches the method's own setting of one positive weight: on a two-asset spread at K = −10 the direct route is 0.08 to 0.09 above the identity's, the put at ρ 0.9 0.852254 against 0.766746 (Choi 0.766703, the Monte Carlo 0.7682 ± 0.0035). nanoql prices those inputs by the identity, and every other negative strike by QuantLib's branch, so `testDengLiZhouWithNegativeStrike` (two negative weights) keeps 3.34412: QuantLib's engine on the flipped inputs, bit for bit, and Choi's engine are the checks.

Facts:

- `ASpreadAtANegativeStrikeIsTheFlippedPut`
- `TwoPositiveWeightsAndOneNegativeMeetChoi`

## DengLiZhouZeroVolatilityTests

[`Divergences/DengLiZhouZeroVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/DengLiZhouZeroVolatilityTests.cs)

Deng, Li and Zhou's basket engine at a zero volatility of its positive leg: a real asset, or a negative strike's zero-volatility pseudo-asset where it is the only positive weight. QuantLib divides 0 by 0 and its `max(0.0, NaN)` returns 0 (`denglizhoubasketengine.cpp:173-179`, `:238`), where the price is continuous (9.36646274 at 1e-12 on the test's market). nanoql floors the positive leg's standard deviation at QL_EPSILON², Choi's engine's floor (`choibasketengine.cpp:76-78`): the limit and Choi's engine are the checks.

Facts:

- `AZeroVolatilityOfThePositiveAssetPricesItsLimit`
- `ANegativeStrikesPseudoAssetAloneOnThePositiveSidePricesItsLimit`

## DerivedCurveExtrapolationTests

[`Divergences/DerivedCurveExtrapolationTests.cs`](../tests/NanoQL.Tests/Divergences/DerivedCurveExtrapolationTests.cs)

Two derived curves drop the extrapolation flag. The spread discount curve reads its base without it (`spreaddiscountcurve.hpp:157`), and so does the ultimate-forward curve at its cut-off and before it (`ultimateforwardtermstructure.hpp:168`, `:173`), where the zero- and forward-spreaded, implied and composite curves pass it. On the wheel, asked for the discount at 11 years with extrapolation over a 2%–3% zero curve to 10 years from 15 January 2025 that doesn't extrapolate, the four siblings give 0.640328216626791 (both spreaded at 1%), 0.714823513455133 and 1.0, while the spread discount and ultimate-forward curves, and the piecewise spread curve bootstrapped over that base, throw "time (11.0055) is past max curve time (10.0055)". nanoql reads the base with extrapolation on, as the siblings do. QuantLib's check: the siblings' own reads, and the base's own extrapolated values.

Facts:

- `TheSpreadDiscountAndUltimateForwardCurvesExtrapolateTheirBase`
- `ThePiecewiseSpreadCurveExtrapolatesItsBase`

## DifferentialEvolutionTests

[`Divergences/DifferentialEvolutionTests.cs`](../tests/NanoQL.Tests/Divergences/DifferentialEvolutionTests.cs)

Differential evolution's generator, one per minimization: QuantLib's optimizer keeps its Mersenne Twister as a member (`differentialevolution.hpp:164`), so a second minimization on the same object continues the first's stream; nanoql's builds one from the seed in each minimization, so a second is the first. QuantLib's values are its own class's.

Facts:

- `ASecondMinimizationRepeatsTheFirst`

## DigitalAmericanKnockOutAtHitTests

[`Divergences/DigitalAmericanKnockOutAtHitTests.cs`](../tests/NanoQL.Tests/Divergences/DigitalAmericanKnockOutAtHitTests.cs)

The knock-out digital engine on a payoff at the hit. QuantLib's `AnalyticDigitalAmericanKOEngine` passes its `knock_in()` only to the at-expiry formula; on a payoff at the hit it prices the knock-in (`analyticdigitalamericanengine.cpp:60-77`), whose formula has no knock-out: on Haug's case 1 (a put at 100, S 105, r 10%, vol 20%, half a year, 15 at the hit) the knock-out engine gives the knock-in's 9.726393830820774 (QuantLib-Python 1.44). A knock-out pays at expiry if the strike was never reached, so nanoql refuses the payoff at the hit, a forced departure that refuses rather than return the other option's value.

Facts:

- `AKnockOutAtTheHitIsRefused`

## DigitalCouponExCouponTests

[`Divergences/DigitalCouponExCouponTests.cs`](../tests/NanoQL.Tests/Divergences/DigitalCouponExCouponTests.cs)

A digital coupon's ex-coupon date and fixing convention: QuantLib's `DigitalCoupon` builds its floating coupon from twelve of the underlying's terms but not its ex-coupon date or its fixing convention, which take their defaults, no date and Preceding (`digitalcoupon.cpp:39-50`, `floatingratecoupon.hpp:60-61`), so a digital coupon on an underlying that goes ex-coupon accrues to its payment as if it didn't, and one on an underlying whose fixing convention isn't Preceding can fix on another date than its underlying. nanoql's keeps both of the underlying's. QuantLib's check: the underlying coupon's ex-coupon date and accrued amounts, which a digital coupon with neither option reproduces (the wheel): a 6-month Euribor coupon from 19 January 2026, paid 20 July 2026 and ex-coupon on 13 July, on the harness vectors' 3.1% curve and 20% lognormal volatility, accrues 14,892.889329889716 on 1e6 by 12 July and −599.1392259151036 on 13 July; and the capped coupon on the same underlying, which passes both terms (`capflooredcoupon.cpp:30-43`): on a coupon from Saturday 18 January 2025 with 0 fixing days and a Modified Following fixing convention, QuantLib's digital coupon fixes on 17 January where its underlying fixes on 20 January (the C++ harness).

Facts:

- `TheUnderlyingsExCouponDateIsKept`
- `TheUnderlyingsFixingConventionIsKept`
- `WithNeitherOptionItAccruesAsItsUnderlying`

## DigitalLegNakedOptionTests

[`Divergences/DigitalLegNakedOptionTests.cs`](../tests/NanoQL.Tests/Divergences/DigitalLegNakedOptionTests.cs)

The digital legs' naked option flag: QuantLib's `DigitalCmsLeg`, `DigitalIborLeg` and `DigitalCmsSpreadLeg` leave `nakedOption_` uninitialized unless `withNakedOption` is called (`digitalcmscoupon.cpp`, `digitaliborcoupon.cpp`, `digitalcmsspreadcoupon.cpp`, their constructors), so a leg built without it reads an indeterminate value, undefined behaviour. nanoql's legs default to `false`, the covered option, the coupon's own default. QuantLib's check: its legs built with `withNakedOption(false)` (the C++ harness's vectors): on the `cash calls at 5%` legs, the Ibor leg's first coupon pays 0.030579238011458898 and the CMS leg's 0.06130050779731958, where the naked legs pay 0 and 0.010000000000000286.

Facts:

- `TheIborAndCmsLegsDefaultToACoveredOption`
- `TheCmsSpreadLegDefaultsToACoveredOption`

## DiscrepancyStatisticsTests

[`Divergences/DiscrepancyStatisticsTests.cs`](../tests/NanoQL.Tests/Divergences/DiscrepancyStatisticsTests.cs)

The discrepancy statistics' lazy dimension and pairing: QuantLib computes its constants for the dimension at construction, so a statistics of dimension 0, whose first sample sets it, keeps dimension 0's constants; and it pairs the components' stored samples by position, which a percentile read, sorting each component in place, breaks. nanoql fixes the constants with the dimension and keeps its own copy of the points. On 200 three-dimensional Sobol points (seed 1) QuantLib gives 0.005923430933947052 with the dimension given, 0.666 with it lazy and 0.102 after a percentile read halfway; nanoql gives the first in all three.

Facts:

- `ALazyDimensionTakesItsConstantsFromTheFirstSample`
- `APercentileReadLeavesThePointsPaired`

## DiscreteIntegratorFewPointsTests

[`Divergences/DiscreteIntegratorFewPointsTests.cs`](../tests/NanoQL.Tests/Divergences/DiscreteIntegratorFewPointsTests.cs)

The discrete integrators with too few evaluations. With fewer than two, QuantLib's count of intervals, an unsigned `Size`, wraps below zero and its loop runs about 2⁶⁴ times (`discreteintegrals.cpp:74-79`, `:92`); nanoql refuses them, at construction and where the count is set below two since. With two, one interval, QuantLib's Simpson integrator closes its odd interval count by an end correction that counts f(a) a second time: on exp over [0, 1] it gives 2.5258075808961893, where its own `DiscreteSimpsonIntegral` on the same two points gives the trapezoid's 1.8591409142295225 (the C++ harness); nanoql's integrator gives the trapezoid there. QuantLib's check: its array form.

Facts:

- `BelowTwoEvaluationsTheIntegratorsRefuse`
- `AtTwoEvaluationsSimpsonIsTheTrapezoid`

## DiscretizedAssetStateTests

[`Divergences/DiscretizedAssetStateTests.cs`](../tests/NanoQL.Tests/Divergences/DiscretizedAssetStateTests.cs)

A discretized asset used before it is initialized, and an American option with fewer than two exercise times. QuantLib's `DiscretizedAsset` leaves its lattice null until `initialize` and dereferences it in `rollback`, `partialRollback` and `presentValue` (`discretizedasset.hpp:38-40, 182-199`), and its `DiscretizedOption` reads `exerciseTimes_[0]` and `[1]` for an American exercise (`discretizedasset.cpp:35-37`), past the end of a shorter vector. nanoql refuses the first with `NanoQLException`, "asset not initialized", and the second, where QuantLib's read would go past the times, with `NanoQLException`, "American exercise needs two exercise times".

Facts:

- `AnUninitializedAssetIsRefused`
- `AnAmericanOptionReadPastItsExerciseTimesIsRefused`

## DistributionMembersTests

[`Divergences/DistributionMembersTests.cs`](../tests/NanoQL.Tests/Divergences/DistributionMembersTests.cs)

Distribution's members on its own density, uniform within each bucket, and the unused classes' slips, each checked against its closed form. QuantLib's values: on a uniform of ten buckets over [0, 10] its shortfalls read 5.625 and 9 for 5.25 and 9.5, and its tranche on [2, 5] keeps a probability of 0.6, its shortfall reading the tranche's 3 at 0.5 to 0.99; its cumulative density in the first bucket reads index −1 (`distribution.cpp:228`), its convolution assigns where it adds, its binomial distribution reads a member it never sets, and its binomial probability of at least 0 events wraps to 0.

Facts:

- `TheShortfallIsTheUniformsTailMean`
- `ATrancheKeepsTheMassAboveItsDetachment`
- `ATranchesShortfallStopsAtItsWidth`
- `ATranchesCumulativeDensityStopsAtItsWidth`
- `TheCumulativeDensityStartsFromZero`
- `TheConvolutionIsTheSumsDistribution`
- `TheBinomialDistributionReadsItsVolume`
- `AtLeastNoEventIsCertain`

## DividendMismatchedTimesTests

[`Divergences/DividendMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/DividendMismatchedTimesTests.cs)

Cash dividends on structures that measure time differently. QuantLib's analytic dividend engine counts dividends from the risk-free curve's reference date (`analyticdividendeuropeanengine.cpp:43-56`), and its finite-difference engine places them on the risk-free curve's times, where this one's is negative, so a dividend paid between the evaluation date and a risk-free curve settling later is left out: on a one-year put, a 2.0 dividend paid the day after the evaluation date doesn't move the analytic price. On the market restated from the evaluation date, the risk-free curve discounting nothing before its own date, both engines count it. nanoql's engines run from the earliest reference date of the structures, each discounting nothing before its own, so they count it, and price the market as QuantLib's engines price it restated: the analytic engine's value, delta, gamma and vega, and the finite-difference engine's value under both cash-dividend models. The analytic engine's rho keeps QuantLib's times, the risk-free curve's own, so a dividend before its reference date adds nothing to it: QuantLib's rho on the market as given, with that dividend's value taken off the spot. Its theta runs each structure over its own time (`AnalyticThetaMismatchedTimesTests`), QuantLib's `blackScholesTheta` at the spot less the dividends' value plus QuantLib's own dividend term. QuantLib's analytic engine also reads the dividend curve's rate per its own day counter's year in the theta, and the curve at the risk-free curve's time in the rho (`:93-105`); here each is read per the risk-free curve's time and by date, as on the curve restated on that day counter, QuantLib's read where the two curves share their times. Where every structure shares the risk-free curve's day counter and reference date, the settlement date, the times and the reads are QuantLib's.

Facts:

- `ADividendInTheGapIsTheOptions`
- `ADividendAfterTheGapIsQuantLibs`
- `ADividendCurveSettlingWithTheRiskFreeCurveIsReadByDate`
- `ADividendCurveOnAnotherDayCounterIsReadPerTheRiskFreeCurvesTime`

## DoubleBarrierLowVolatilityTests

[`Divergences/DoubleBarrierLowVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/DoubleBarrierLowVolatilityTests.cs)

The analytic double-barrier engine at low volatility (fixed). The series' powers (U/L)^(n·μ₁) overflow where μ₁ = 2b/σ² + 1 is large, and the product with a vanishing normal difference is NaN, which QuantLib's `std::max(0, ·)` turns into 0: a knock-out call whose forward stays inside the corridor prices 0 at σ = 1e-4 and at every σ below about 1.6% at a 4% carry. nanoql takes each term's power and normal difference in logs where the power overflows, QuantLib's technique in the binary barrier engine, each difference of same-signed arguments from the small tail, and follows the forward below machine-epsilon variance. Checks: the deterministic limit D·(F − K), and continuity with QuantLib's finite values above the overflow; and the image series in 50 digits where the small tail keeps the digits a plain difference cancels, a call at 101 on [98, 102] at spot 100.5, r 4%, q 1%, σ 0.7% over a year: 0.00590299270662953, where QuantLib's five terms give 0.0059033353, 5.8e-5 off, and its 50 terms 0, by overflow.

Facts:

- `AKnockOutWhoseForwardStaysInsideIsWorthItsForwardPayoff`
- `TheValueIsContinuousThroughTheOverflow`
- `TheSmallTailKeepsTheDigitsAPlainDifferenceCancels`

## DoubleBarrierRebateTests

[`Divergences/DoubleBarrierRebateTests.cs`](../tests/NanoQL.Tests/Divergences/DoubleBarrierRebateTests.cs)

The analytic double-barrier engine's rebate (fixed). QuantLib's engine never reads `DoubleBarrierOption`'s rebate, where its finite-difference Heston double-barrier engine pays a knock-out's at the hit (`fdhestondoublebarrierengine.cpp:96-99`): a knock-out call on [80, 120] prices 1.420391 at rebates 0 and 5, where the FD engine at a vanishing vol of vol gives 1.426200 and 3.920732. nanoql takes QuantLib's single-barrier convention: a knock-out's rebate paid at the first touch of either barrier, R × (KIKO + KOKI) at unit cash through the double-barrier binary engine, and a knock-in's paid at expiry where neither is touched, R × the binary knock-out at unit cash and strike 0. Checks: the survival route on QuantLib's image series (0.60784983008 per unit rebate on [80, 120] at 20% over a year), QuantLib's FD Heston engine on a corridor its grid resolves, and QuantLib's binary engine.

Facts:

- `AKnockOutsRebateIsPaidAtTheFirstTouch`
- `AKnockInsRebateIsPaidAtExpiryWhereNeitherBarrierIsTouched`

## DoubleBarrierSeriesTests

[`Divergences/DoubleBarrierSeriesTests.cs`](../tests/NanoQL.Tests/Divergences/DoubleBarrierSeriesTests.cs)

The analytic double-barrier engine's series length (fixed). QuantLib sums n from −`series` to `series`, five by default (`analyticdoublebarrierengine.cpp:176`), unconverged on narrow corridors and long terms: [95, 105] at a volatility of 50% over two years prices a knock-out 3.3e-3 where 50 terms and QuantLib's binomial double-barrier engine give about 0. nanoql takes `series` as the minimum and adds terms while the next pair exceeds machine epsilon of the sum, QuantLib's value wherever five have converged. Checks: 50 terms; QuantLib's binomial engine, 5.5e-88 (QuantLib-Python 1.44's).

Facts:

- `ANarrowCorridorOverTwoYearsIsWorthItsConvergedSeries`

## DoubleBarrierUpstreamFixesTests

[`Divergences/DoubleBarrierUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/DoubleBarrierUpstreamFixesTests.cs)

Upstream's fix after v1.44 the discretized double-barrier assets take (master first): #2878 (QuantLib's master `eaeac5651`), the American window's bounds compared with `isOnTime` as well (`discretizeddoublebarrieroption.cpp:61-66`), where QuantLib 1.44's plain comparison skips the exercise at expiry when the grid's last time rounds below maturity. QuantLib's own case, `DoubleBarrierOptionTests.TestBinomialExerciseAtMaturity`, checks the plain asset; here the Derman-Kani asset, which reads the plain one's check, on that case's market, until the pin moves to a release that carries the fix.

Facts:

- `TheDermanKaniAssetExercisesAnAmericanAtMaturity`

## DoubleBinaryHuiCheckTests

[`Divergences/DoubleBinaryHuiCheckTests.cs`](../tests/NanoQL.Tests/Divergences/DoubleBinaryHuiCheckTests.cs)

The double-barrier binary's convergence check on Hui's series (fixed). QuantLib checks only its 99th term, |term| < 1e-8 (`analyticdoublebarrierbinaryengine.cpp:98-110`), and that term carries sin(99πx/Z), so at x/Z = j/99 the check passes unconverged: on [80, 125] at 5%, r 3%, q 2.7%, two days, the spot at x/Z = 1/9 gets a unit-cash knock-out of 1.000413699, above its discounted cash 0.9998356, which no knock-out can exceed. nanoql decides Hui's convergence by a bound on the dropped tail free of its sines, the terms' envelope, and takes QuantLib's image series where it fails. Check: the image series at x/Z = j/99 (0.9998356299486).

Facts:

- `ASpotWhereTheLastSineVanishesIsPricedConverged`

## DoubleBinaryKikoTests

[`Divergences/DoubleBinaryKikoTests.cs`](../tests/NanoQL.Tests/Divergences/DoubleBinaryKikoTests.cs)

The double-barrier binary's KIKO and KOKI options (fixed). QuantLib sums their series to i = 999 and checks only the last term, |term| < 1e-8 (`analyticdoublebarrierbinaryengine.cpp:156-169`), while the terms decay as 1/i³: at small variance it passes its check unconverged (a KOKI 8.4% low on [95, 105] at a volatility of 1% over two years) or refuses ("serie did not converge sufficiently fast"). nanoql keeps QuantLib's sum, its value and bits, wherever Abel's bound on the dropped tail is within 1e-11 of the sum, and elsewhere integrates the absorption flux of QuantLib's own image-series killed density at the barrier, each image paired with its reflection, in log time between the roots that bound its mass, on the adaptive Gauss-Kronrod rule scaled by a first estimate. Checks: Kummer's transformation of QuantLib's series in 40 or 50 digits, the survival route on QuantLib's image series, and the flux in 30 digits.

Facts:

- `QuantLibsTruncatedKokiIsTheSeriesSum`
- `TheCaseQuantLibRefusesIsPriced`
- `AtTheKummerFormsResonanceTheFluxMatchesTheSurvivalRoute`
- `OnAPegLikeMarketTheFluxMatchesThe50DigitValue`
- `TheSpotJustAboveTheLowerBarrierMatchesKummer`
- `ACorridorNarrowAgainstItsVarianceMatchesKummer`
- `QuantLibsOwnCaseOutsideAbelsBoundMovesByItsTruncation`
- `ADriftDominatedMarketMatchesTheFluxIn30Digits`

## DoubleBinaryKnockedInTests

[`Divergences/DoubleBinaryKnockedInTests.cs`](../tests/NanoQL.Tests/Divergences/DoubleBinaryKnockedInTests.cs)

A double-barrier binary knock-in already knocked in (fixed). It pays its cash at expiry, where the payoff is in the money, as QuantLib's own header says ("the Double Knock In pays at end", `analyticdoublebarrierbinaryengine.hpp:38-41`); QuantLib's degenerate branch pays the cash undiscounted, 10.0 at a spot beyond the barrier where its formula's limit just inside is cash × D(T), and whatever the payoff's strike and type (`.cpp:237-246`). nanoql prices it as the European cash-or-nothing it is, at the payoff's strike and type: cash × D(T) at QuantLib's strike-0 call. QuantLib's check: its analytic European engine on the payoff, and the engine's own value a hair inside the corridor. QuantLib's `testHaugValues` checks at lines 171-172 move here.

Facts:

- `QuantLibsStrikeZeroCallPaysTheCashDiscounted`
- `AStruckPutKnockedInIsItsEuropean`

## DoubleBinaryRebateTests

[`Divergences/DoubleBinaryRebateTests.cs`](../tests/NanoQL.Tests/Divergences/DoubleBinaryRebateTests.cs)

The double-barrier binary engine's rebate. QuantLib's engine never reads `DoubleBarrierOption`'s rebate: a cash-or-nothing call, cash 10, strike 100, knocked out on [80, 120], prices 1.574843057685 at rebates 0 and 5 at r 5%, q 1%, σ 20% over a year, and 0 already knocked out. The facts run on `DoubleBarrierRebateTests`' market (q 2%). nanoql prices it as the double barrier engine does (`DoubleBarrierRebateTests`): a knock-out's paid at the first touch of either barrier, now where one is already touched, R × (KIKO + KOKI) at unit cash; a knock-in's at expiry where neither is touched, R × the unit-cash knock-out at strike 0, and none once knocked in. A KIKO's or KOKI's, which no convention fixes (at the knock-out's touch, or at expiry if the knock-in never comes), is refused. Checks: the survival route on QuantLib's image series (0.60784983008 per unit rebate on [80, 120] at 20% over a year), and the double barrier engine's own rebate on the same option.

Facts:

- `AKnockOutsRebateIsPaidAtTheFirstTouch`
- `AKnockInsRebateIsPaidAtExpiryWhereNeitherBarrierIsTouched`
- `AKnockOutAlreadyTouchedIsWorthItsRebateAndAKnockInNone`
- `AKikoOrKokiRebateIsRefused`

## DoubleBinarySmallVarianceTests

[`Divergences/DoubleBinarySmallVarianceTests.cs`](../tests/NanoQL.Tests/Divergences/DoubleBinarySmallVarianceTests.cs)

The double-barrier binary knock-out at small variance (a refusal lifted). QuantLib refuses "serie did not converge sufficiently fast" where the corridor is wide against σ√T, where Hui's eigenfunction series converges slowest (`analyticdoublebarrierbinaryengine.cpp:110`). nanoql takes the cash-or-nothing part of QuantLib's own double-barrier image series there (its `AnalyticDoubleBarrierEngine::callKO`), which converges fastest exactly there, its terms in logs and its normal differences from the small tail. Checks: Hui's value where it converges, and the image series in 50 digits where it doesn't, which QuantLib's finite-difference Heston double-barrier engine gives to ten digits on [95, 105] at 2% over a day.

Facts:

- `WhereHuisSeriesConvergesItsValueStands`
- `WhereQuantLibRefusesTheImageSeriesPrices`

## DoubleBinaryStrikeTests

[`Divergences/DoubleBinaryStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/DoubleBinaryStrikeTests.cs)

The double-barrier binary's payoff strike and type (fixed). Hui's series integrates the killed density over the whole corridor and never reads the payoff's strike or type (`analyticdoublebarrierbinaryengine.cpp:78`, "this is not used ?"), so a knock-out with L 80, U 120, cash 10 prices 6.69467 for every strike and both types, a put struck at 0 among them. nanoql integrates over the payoff's region, [max(K, L), U] for a call and [L, min(K, U)] for a put, in closed form, QuantLib's value standing for a call at K ≤ L and a put at K ≥ U. Checks: an independent Crank-Nicolson solve (the call and put at K 100, 3.046527 and 3.648146, the full corridor 6.694673, extrapolated to N = 4,000), QuantLib's finite-difference Heston double-barrier engine at a vanishing vol of vol, and call + put = the full corridor.

Facts:

- `TheCallAndThePutAtTheMoneyMatchAnIndependentSolve`
- `TheFiniteDifferenceHestonEngineAgreesAtAVanishingVolOfVol`
- `CallPlusPutIsTheFullCorridor`

## ECBNoKnownDatesTests

[`Divergences/ECBNoKnownDatesTests.cs`](../tests/NanoQL.Tests/Divergences/ECBNoKnownDatesTests.cs)

ECB's functions with no known dates. QuantLib's refusal "ECB dates after ... are unknown" reads `*knownDates().rbegin()` (`ecb.cpp:233-234`, `:245-246`), which is undefined on an empty set, as `removeDate` can leave it. nanoql refuses with "no ECB dates are known", the type QuantLib's refusal has.

Facts:

- `AnEmptySetIsRefused`

## ECBRangeStartTests

[`Divergences/ECBRangeStartTests.cs`](../tests/NanoQL.Tests/Divergences/ECBRangeStartTests.cs)

ECB's functions at the start of Date's range. QuantLib's `isECBdate(d)` is `nextDate(d - 1) == d`, and a month's date is `nextDate(Date(1, m, y) - 1)` (`ecb.hpp:42`, `:81-84`; `ecb.cpp:200`), so on 1 January 1901 both step out of the range and throw "Date's serial number (366) outside allowed range". nanoql reads the first known date on or after the month's first day, and membership in the known dates up to the last one, which are QuantLib's answers on every other date; QuantLib's own `nextDate` for the following month is the check.

Facts:

- `IsECBdateAnswersOnDatesFirstDay`
- `AMonthsDateIsFoundFromDatesFirstDay`

## EnergyBasisSwapTests

[`Divergences/EnergyBasisSwapTests.cs`](../tests/NanoQL.Tests/Divergences/EnergyBasisSwapTests.cs)

An energy basis swap's basis converted from its own currency into the base (fixed). QuantLib converts it from the base currency into the basis currency (`energybasisswap.cpp:126-129`), the reverse of every other conversion in the swap: on QuantLib 1.44 (1,000 barrels in March 2025, the pay index at 70 and the receive index at 73 dollars a barrel, a basis of 2 euros a barrel at 1.25 dollars a euro, flat zero curves) the swap with the basis on the receive leg prices 4,600 and on the pay leg 1,400, the basis read as 1.6 dollars. nanoql reads it as 2.5 dollars: 5,500 and 500, the hand values (73 + 2.5 − 70) × 1,000 and (73 − 70 − 2.5) × 1,000. A basis in the base currency is QuantLib's either way.

Facts:

- `ABasisInEurosIsWorthItsDollars`

## EnergyFutureTests

[`Divergences/EnergyFutureTests.cs`](../tests/NanoQL.Tests/Divergences/EnergyFutureTests.cs)

The energy future's lots, secondary costs and quote (fixed), on a future of 10 units at 1.5 a unit on an index at 2 a unit. QuantLib's quantity conversion factor already carries the index's lot and the value multiplies by it again: lot 1,000 prices 5,000,000 for (2 − 1.5) × 10 × 1,000 = 5,000. nanoql multiplies once. QuantLib charges a unit cost converted to the base unit on the quantity in its own unit without its lots (`energyfuture.cpp:107-108`): a fee of 0.1 a unit on 10 units reads 1 in barrels, 42 in gallons and 1,000 in thousand-barrels, 1 each by hand (the future prices 4, 8,778 and 4,999,000). nanoql charges it on the base-unit quantity the value uses, lots included. Within QuantLib's window (the last quote date the evaluation date or the day before) QuantLib reads the evaluation date's fixing alone, so with the last fixing yesterday it refuses ("missing quote for [FIDX1]"), and reads it through the index's fixing, which refuses a Saturday evaluation on a TARGET index with Friday's fixing ("January 18th, 2025 is not a valid fixing date"). nanoql reads the stored history's entry for the evaluation date, else the day before's.

Facts:

- `TheLotMultipliesTheValueOnce`
- `AUnitCostIsChargedOnTheQuantityDelivered`
- `YesterdaysFixingQuotesTheFutureWhenTodaysIsMissing`
- `TodaysFixingQuotesTheFutureAsQuantLibs`
- `ASaturdayEvaluationOnAWeekdayIndexReadsFridaysFixing`

## EnergySwapPaymentTests

[`Divergences/EnergySwapPaymentTests.cs`](../tests/NanoQL.Tests/Divergences/EnergySwapPaymentTests.cs)

An energy swap's payments in the currency of the leg the net amount goes through, at that currency's rate (fixed). QuantLib's vanilla swap files its fixed leg under the pay currency and its floating leg under the receive currency whichever way it faces (`energyvanillaswap.cpp:104-113`), and nanoql keeps that; but it pairs a payer's payment currency with the other leg's rate, and gives a receiver a pay-receive flag of 0, so a receiver's payment never changes currency (`:220-225`); the basis swap pairs a payment's currency with the other leg's rate too (`energybasisswap.cpp:263-266`). On QuantLib 1.44 (paying dollars and receiving euros at 1.25 dollars a euro, 3% flat), a payer vanilla swap at 1.5 on 1,000 barrels a month from March to May 2025 on an index at 2 is paid 496.72311389766810 "EUR", its dollar amount, in April, and a payer at 2.5 pays −397.37849111813631 "USD", its euro amount; a receiver pays and is paid in dollars. A basis swap on 1,000 barrels in March, its pay index at 70, is paid 2,980.3386833860422 "EUR" with its receive index at 73 and pays −2,384.2709467087875 "USD" at 67, the other leg's amount each time. nanoql pays a payer in euros and takes its payments in dollars, each at its own rate (397.37849111813467 EUR and −496.72311389767009 USD); a receiver is paid its fixed leg in dollars, QuantLib's payments, and pays its floating leg in euros; a basis swap pays 2,384.27 EUR and takes −2,980.34 USD. The NPV, in the base currency, is QuantLib's either way.

Facts:

- `APaymentIsInTheCurrencyOfItsLegAtThatCurrencysRate`
- `QuantLibsAprilPaymentsPairTheOtherLegsRate`
- `AReceiversFixedLegPaymentsAreQuantLibs`
- `ABasisSwapsPaymentIsInTheCurrencyOfItsLegAtThatCurrencysRate`

## EnergyUnitConversionTests

[`Divergences/EnergyUnitConversionTests.cs`](../tests/NanoQL.Tests/Divergences/EnergyUnitConversionTests.cs)

The energy instruments' unit conversions (fixed). QuantLib's `calculateUomConversionFactor` returns the looked-up conversion's stored factor whatever its orientation, so it is one unit's value in the other for 25 of the 30 ordered pairs of its six petroleum units and the reciprocal for five (BBL→MB, BBL→kl, BBL→KL_tk, GAL→BBL, l→BBL), and its call sites multiply a quantity by the factor from the base to its unit and a price by the factor from its unit to the base, so one is inverted: a payer vanilla swap on 1,000 units a month at a 0.5 spread prices 500 in barrels, 882,000 in gallons and 500,000,000 in thousand-barrels, and a fee of 0.1 a unit reads 100, 176,400 and 10⁸ (flat zero curves). nanoql's factor is one unit's value in the other, the conversion's own convert of a unit quantity; a quantity converts to the base by the factor from its unit, and a price, an index's forward price, a curve's basis and a unit cost by the factor from the base (or the index's unit) to theirs. The check is the hand value, the spread and the fee converted through barrels by QuantLib's known factors, on every unit; QuantLib's bits stand on the base unit, where every factor is 1.

Facts:

- `TheFactorIsOneUnitsValueInTheOther`
- `QuantLibsStoredFactorIsTheReciprocalOnFivePairs`
- `APayerSwapInAnyUnitIsWorthItsSpreadTimesItsQuantity`
- `AUnitCostIsChargedPerUnitDelivered`
- `EveryUnitCombinationPricesItsHandValue`
- `AnIndexsForwardPriceConvertsIntoItsUnitAsAPrice`
- `ABasisCurveInThousandBarrelsAddsItsPricePerBarrel`

## EngineSequenceTests

[`Divergences/EngineSequenceTests.cs`](../tests/NanoQL.Tests/Divergences/EngineSequenceTests.cs)

A static engine reproduces QuantLib's `Instrument::calculate` in order: if the instrument has expired, its expired results; otherwise the arguments' validation, the engine body, and the instrument's fetch of the results. The engine checks its own arguments first, so an expired swap priced without a curve throws, where QuantLib returns the expired results without reading the engine.

Facts:

- `ALiveSwapRunsValidateThenTheBodyThenFetchResults`
- `AnExpiredSwapReturnsItsExpiredResultsWithoutTheBody`
- `ValidateRunsBeforeTheBody`
- `TheResultsAreWhatFetchResultsReturns`
- `TheArgumentChecksRunBeforeTheExpiryCheck`
- `ALegFailureNamesTheLegAndKeepsTheCause`

## EquityIndexDatedCurveTests

[`Divergences/EquityIndexDatedCurveTests.cs`](../tests/NanoQL.Tests/Divergences/EquityIndexDatedCurveTests.cs)

The equity index's forecast on curves dated before the evaluation date. QuantLib's formula is I(t, T) = I(t, t) P_D(t, T) / P_R(t, T) from the evaluation date t (`equityindex.hpp:42-50`), but its forecast reads each curve's discount at T from the curve's own reference date (`equityindex.cpp:84-88`), so a curve dated before t carries its discount from its own date to t into the forward. nanoql reads each curve from t, its discount at T over its discount at t, nothing accruing before its reference date; QuantLib's own forecast on the curves restated at t is the check, and on a curve dated on or after t the read is QuantLib's bit for bit. A fixing before a later-dated curve's reference date reads that curve as accruing nothing yet, where QuantLib's discount refuses the date ("negative time").

Facts:

- `BothCurvesDatedBeforeTheEvaluationDate`
- `OnlyTheRiskFreeCurveDatedBeforeTheEvaluationDate`
- `ACurveDatedAfterTheEvaluationDateReadsAsQuantLibs`
- `ACurveWhoseDiscountAtItsOwnDateIsntOneReadsAsQuantLibs`

## EuropeanRhoDiscountCurveTests

[`Divergences/EuropeanRhoDiscountCurveTests.cs`](../tests/NanoQL.Tests/Divergences/EuropeanRhoDiscountCurveTests.cs)

The analytic European engine's rho with a payoff discount curve of its own. QuantLib's rho moves the forward's rate and the payoff discount's over one time, the discount curve's day counter from the risk-free curve's reference date (`analyticeuropeanengine.cpp:81-86`), exact only where the two curves share their times. Against the value's central difference with both curves' zero rates moved, it is 3.5% off at a week and 0.19% at a year with the risk-free curve settling T+2 and the discount curve dated today, 1.6% and 0.19% the other way round, and 1.4% and 1.1% with the discount curve on Act/360. nanoql moves each rate over its own curve's time: the identity −(T_r / T_q) ρ_q − T_d V over QuantLib's own dividend rho and value, each time its own curve's, which meets the central difference to 2e-9; no QuantLib function computes it. Where the two curves share their date and day counter, the rho is QuantLib's.

Facts:

- `EachRateMovesOverItsOwnCurvesTime`
- `CurvesSharingTheirTimesAreQuantLibs`

## EventSetTests

[`Divergences/EventSetTests.cs`](../tests/NanoQL.Tests/Divergences/EventSetTests.cs)

An event set's simulation from 29 February: its periods take 28 February in years without one, where QuantLib throws ("day outside month (2) day-range [1,28]"). On events of 100 on 1 March 2001, 50 on 15 June 2002, 70 on 28 February 2003, 30 on 1 March 2004 and 20 on 10 January 2006, covering 2001 to 2010, the simulation from 28 February 2012 to 28 February 2014 reads in QuantLib the paths (serial numbers) 40969 100, 41440 50, 41698 70; 41334 30; 41284 20; and an empty one, then none.

Facts:

- `ASimulationFrom29FebruaryIsQuantLibsFrom28February`

## ExchangeRateChainRefusalTests

[`Divergences/ExchangeRateChainRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/ExchangeRateChainRefusalTests.cs)

Chaining two exchange rates over one pair of currencies. QuantLib's `ExchangeRate::chain` matches the rates' currencies in order (`exchangerate.cpp:57-73`), so EUR→USD at 1.1 with EUR→USD at 1.2 chains into USD→USD at 1.0909, which exchanges 100 USD into 109.09 USD. nanoql refuses two rates over one pair with QuantLib's own "exchange rates not chainable" (`:74`); rates over different pairs chain as QuantLib's.

Facts:

- `TwoRatesOverOnePairAreRefused`

## ExchangeRateKeyTests

[`Divergences/ExchangeRateKeyTests.cs`](../tests/NanoQL.Tests/Divergences/ExchangeRateKeyTests.cs)

The exchange-rate registry's key for currencies coded 1,000 or more. QuantLib keys a pair by `min(code) × 1000 + max(code)` and finds a currency's pairs by the key's thousands and units (`exchangeratemanager.cpp:89-98`), so a currency coded 1,000 or more (QuantLib's crypto currencies, 10000 to 10007, or any currency of one's own) matches none of its pairs: with BTC/EUR and EUR/USD held, a lookup from BTC to USD throws "no conversion available" while the reverse chains, and no chain passes through BTC. nanoql keys a pair by its two currencies, in QuantLib's order, so both lookups chain, and chains pass through it.

Facts:

- `APairWithACodeOf10000IsLookedUpBothWays`
- `AChainPassesThroughACodeOf10000`
- `QuantLibsBitcoinIsLookedUpBothWaysAndThroughAChain`

## ExerciseBeforeReferenceDateTests

[`Divergences/ExerciseBeforeReferenceDateTests.cs`](../tests/NanoQL.Tests/Divergences/ExerciseBeforeReferenceDateTests.cs)

Options expiring on or before a structure's reference date. QuantLib's analytic, binomial and Heston engines read each structure at the exercise date, so a put expiring between the evaluation date and a risk-free curve settling T+2 throws "negative time"; on the curve's own date the Heston engine's integrations read 0/0 (`analytichestonengine.cpp:644-674`) and the dividend engine's theta is 0. QuantLib's finite-difference engine on the market restated from today, every structure accruing nothing before its own date, prices them, as nanoql's does on its clock. nanoql's other engines read each structure as accruing nothing before its own date, so each prices them as QuantLib's engine prices the restated market: the analytic engines' value and Greeks, and the binomial engine's value, delta, gamma and theta; the Heston engine's model time runs from the earliest curve date (`HestonMismatchedTimesTests`), and at its time 0, on or before every curve's date, its value is the forward's intrinsic value, as QuantLib's European engine reads it at zero volatility. The rho of an option expiring before the risk-free curve's date is 0, as no risk-free rate reaches it. Where every structure is dated after the exercise, the finite-difference and binomial engines' clocks have no time for it, and they refuse it as QuantLib's do on structures dated together.

Facts:

- `TheAnalyticEnginesPriceAPutExpiringInTheGap`
- `AVolatilityDatedAfterTheExerciseAccruesNoVariance`
- `TheBinomialEnginePricesAPutExpiringInTheGap`
- `TheHestonEnginePricesTheForwardsIntrinsicValueAtItsTimeZero`
- `TheFiniteDifferenceEnginePricesItOnItsClock`

## ExerciseDatesTests

[`Divergences/ExerciseDatesTests.cs`](../tests/NanoQL.Tests/Divergences/ExerciseDatesTests.cs)

An exercise's dates read past their end. QuantLib's `Exercise::date(i)` is `dates_[i]` (`exercise.hpp:45`), which reads past the vector for an index out of range, undefined, where its twin `dateAt(i)` (`:46`) throws `std::out_of_range`, a convention (`Conventions/ExerciseErrorTests`). nanoql refuses the index, with `ArgumentOutOfRangeException` naming `index`, as `Schedule.Date(int)` does. And a swaption on an exercise with no dates, which QuantLib's public `Exercise(type)` builds (`exercise.hpp:41`): QuantLib's swaption reads its last date in `isExpired` (`dates().back()`, `swaption.cpp:159`), and its first in the engines and in `impliedVolatility` (`date(0)`, `blackswaptionengine.hpp:227`, `swaption.cpp:198`), undefined for an empty vector. nanoql's swaption builds on it and refuses where the date is read, with `NanoQLException` and QuantLib's text for the same input, `lastDate()`'s "no exercise date given" (`exercise.cpp:29`).

Facts:

- `AnIndexOutOfRangeIsRefused`
- `ASwaptionOnAnExerciseWithNoDatesIsRefusedWhereItsDateIsRead`

## ExoticMismatchedTimesTests

[`Divergences/ExoticMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/ExoticMismatchedTimesTests.cs)

The exotic engines on structures counted or dated apart (the playbook's row for structures on another clock). QuantLib's closed forms read each curve's rate on its own day counter and every time on the risk-free curve's, so with the dividend curve on Act/360 (its rate restated, the same discounts by date) the compound option moves +0.21%, the complex chooser +0.02%, the holder- and writer-extensible options +0.13% and +0.12%, and the simple chooser refuses ("Risk-free rate and dividend yield must have the same day counter"); with the risk-free curve settling two days on, against its restatement from the evaluation date, −0.36%, −0.41%, −0.24%, −0.23% and +0.01%. Its forward engine's theta reads the dividend rate on that curve's own day counter (−1.4% on Act/360) and its dividend rho the reset on the risk-free curve's time (−0.10% at T+2); its Monte Carlo grids run on the risk-free curve's time, the forward engine's +0.16% on Act/360 dividends and +0.01% at T+2, the performance engine's +0.10% on Act/360. nanoql's forms read discounts and variances by date and every time on the process's clock, and the forward engines' reset terms in the dividend rho and the performance rho on the shifted curve's own time. The checks: nanoql on the market restated onto one clock, which on structures dated and counted alike keeps QuantLib's bits; for the reset terms, the cliquet and performance engines on the same one-period option and the prices bumped in the curve's own zero rate.

Facts:

- `TheClosedFormsReadADividendCurveOnItsOwnDays`
- `TheClosedFormsPriceARiskFreeCurveSettlingLaterFromTheEvaluationDate`
- `TheForwardAndCliquetEnginesReadEveryTimeOnTheClock`
- `TheMonteCarloGridsRunOnTheClock`
- `TheForwardEnginesResetTermsReadTheShiftedCurvesOwnTime`

## ExoticTermStructureMarkets

[`Divergences/ExoticTermStructureMarkets.cs`](../tests/NanoQL.Tests/Divergences/ExoticTermStructureMarkets.cs)

The markets and the independent check of the term-structure facts: a flat market, a rising one (zero rates 1% to 6% and dividends 0 to 3% from three months to a year, volatilities 0.15, 0.30, 0.30 at three months, a year and two) and a falling one (the rates reversed, volatilities 0.30, 0.25, 0.25); and the exact value of an option at its first date under deterministic rates, dividends and volatility, its payoff there integrated over the lognormal spot by Gauss-Lobatto at an absolute accuracy of 1e-13, split at the payoff's kinks.

## ExpectedShortfallNoLossTests

[`Divergences/ExpectedShortfallNoLossTests.cs`](../tests/NanoQL.Tests/Divergences/ExpectedShortfallNoLossTests.cs)

The expected shortfall where no sample lies below the value-at-risk's level: QuantLib refuses "no data below the target" (riskstatistics.hpp:196-202). With no loss the level is 0, and the port returns it, the value QuantLib's own comment gives ("must be a loss, i.e., capped at 0.0 and negated", line 203); where the tail is an atom at the level, every tail sample sits at it, and the port returns the level, the value-at-risk, QuantLib's own limit as the atom splits. The average shortfall keeps QuantLib's refusal: a conditional average over no data has no value. QuantLib's checks: its own shortfall and value-at-risk on the same samples, and its expected shortfall on the split atom.

Facts:

- `SamplesWithNoLossHaveNoShortfall`
- `ATailThatIsAnAtomHasTheAtomsShortfall`

## ExpiredCallableBondSpreadTests

[`Divergences/ExpiredCallableBondSpreadTests.cs`](../tests/NanoQL.Tests/Divergences/ExpiredCallableBondSpreadTests.cs)

The option-adjusted spread family on an expired callable bond. QuantLib's `NPVSpreadHelper` sets the bond's arguments up and calls its engine's `calculate()` directly (`callablebond.cpp:262-277`), which skips `Instrument::calculate`'s expiry check (`instrument.hpp:130-139`), so the family prices an expired bond's arguments. On a bond called at 100 in September 2019 and maturing on 9 June 2025, evaluated on 16 September 2030, with Hull-White on a flat 3% curve dated 2 January 2020 and 40 steps, the wheel gives an OAS of inf for a clean price of 100, a clean price of inf for a spread of 0.01, and an effective duration and convexity of NaN. nanoql's family takes the engine as its `Calculate`, whose first step is the expiry check, so it would read the expired results, an NPV of 0: the OAS would be the solver's guess, 0, a value a caller can't tell from an answer. nanoql refuses the family on an expired bond instead, with `NanoQLException` and the text of QuantLib's own refusal of an expired bond's implied volatility (`callablebond.cpp:119`).

Facts:

- `AnExpiredBondsSpreadFamilyIsRefused`

## ExponentialCorrelationTests

[`Divergences/ExponentialCorrelationTests.cs`](../tests/NanoQL.Tests/Divergences/ExponentialCorrelationTests.cs)

The exponential forward correlation's three departures. One rate is accepted: QuantLib requires `numberOfRates_ > 1` under the text "Rate times must contain at least two values", so it refuses one rate when given two times (`expcorrelations.cpp:82-83`), where its own time-homogeneous correlation accepts one. At an exponent other than 1, correlation times that skip a rate time but the last are refused: QuantLib's FIXME asks for the check (`:105-106`), and without it each period's matrix is taken at the period's midpoint, after a rate that is alive at the period's start has fixed, so the models give that rate no variance there: on nine rates fixing 0.5 … 4.5 and times {2.0, 4.5}, FlatVol's total variance of rate 0 is 0 against σ²T 0.02, rate 4's 0.08 against 0.10. And a last correlation time past the last rate time is refused with a text naming the time it checks, where QuantLib's calls it the next-to-last (`:107-110`).

Facts:

- `OneRateIsTheTimeHomogeneousCorrelationOfOneRate`
- `TimesSkippingARateTimeAreRefusedAtAnExponentOtherThanOne`
- `ALastTimePastTheLastRateTimeIsNamedAsTheLast`

## ExponentialJump1dMesherTests

[`Divergences/ExponentialJump1dMesherTests.cs`](../tests/NanoQL.Tests/Divergences/ExponentialJump1dMesherTests.cs)

The jump mesher's distribution and density at a finite time and its stationary distribution. QuantLib integrates its density from 1e-100, which holds no mass while βt is below about 230, so at β 100, λ 4, η 2.5 its distribution meets the closed form's to 6e-16 at t 1, misses 1.06e-4 at t 5, and from t 8 its integrator refuses, "accuracy not reached"; nanoql integrates the closed form's route over the time since the last jump, its norm 1 − e^(−λt) by `expm1`, which QuantLib's subtraction loses at small λt (it refuses at t 1e-12, "max number of iterations reached"). QuantLib's density at t, a difference of regularised incomplete gammas, cancels as βt falls and where ηx is large, and refuses at intensities from β, where it takes Γ(1 − λ/β) ("positive argument required", its harness at β 4, λ 4, η 5 and β 0.2, λ 6, η 0.3); nanoql's is the route's derivative, which computes there too, its terms one exponential so that none leaves the double range before its peak. QuantLib's stationary distribution holds the mass to min(x, ε)/η where it means x, wrong with η below 1 and x below ε/η; there nanoql takes its series at ηx. The references are the route at 40 digits, QuantLib's density formula continued in mpmath and the Gamma(λ/β, η) cdf; QuantLib's values are its own and its harness's.

Facts:

- `WhereQuantLibsCutHoldsNoMassItsValuesAreMet`
- `WhereEtaXReachesOneTheComplementKeepsTheDigits`
- `AtSmallTimesTheNormKeepsItsDigits`
- `BeyondItTheDistributionKeepsItsMass`
- `TheDensityAtSmallAndLargeTimesKeepsItsDigits`
- `TheDensityComputesWhereItsTermsLeaveTheDoubleRange`
- `WhereEtaXIsLargeQuantLibsDensityCancels`
- `BelowEpsOverEtaTheStationarySeriesTakesEtaX`
- `BelowZeroTheStationaryFormsAreZero`
- `AtIntensitiesFromBetaTheDistributionAndDensityCompute`

## ExponentialSplinesFixedKappaTests

[`Divergences/ExponentialSplinesFixedKappaTests.cs`](../tests/NanoQL.Tests/Divergences/ExponentialSplinesFixedKappaTests.cs)

Exponential splines with a fixed κ. QuantLib's discount function sums one term fewer than its size (`nonlinearfittingmethods.cpp:84`, `:91`), right when the last parameter is κ and wrong when κ is fixed: the last coefficient is dead. On the wheel, constrained at zero with nine coefficients and κ = 0.5, the coefficients 0.3, −0.2, 0.15, 0.1, −0.05, 0.08, 0.02 and −0.01 discount 0.4584577385303755 at t = 1, as they do with the last one 0.49, where the method with κ free gives 0.46441195516211947 and 0.1667011235749239. nanoql sums every coefficient when κ is fixed. QuantLib's check: its free-κ method's discount function at the same coefficients and κ.

Facts:

- `AFixedKappaDiscountsAsTheFreeMethodAtThatKappa`

## ExtendedBlackScholesProcessTests

[`Divergences/ExtendedBlackScholesProcessTests.cs`](../tests/NanoQL.Tests/Divergences/ExtendedBlackScholesProcessTests.cs)

The extended Black-Scholes process's Milstein, Euler and predictor steps in the log price (fixed). QuantLib's Milstein adds the asset's correction ½σ²(ΔW² − 1)Δt to the log price, its one-step mean 1.245e-3 high at Δt 0.25 and σ 0.4; its Euler step and its predictor centre on the asset's mean x₀e^((r−q)Δt), which lacks the −σ²Δt/2, 2.02e-2 high a step there, and refuse a strike-dependent volatility ("not implemented"). On QuantLib 1.44 (x₀ 100, r 0.03, q 0, a flat 0.4, Δt 0.25, ΔW from −2 to 2) Milstein steps to 70.29285698, 80.85603163, 96.80224498, 120.6230249 and 156.4396302, Euler to 67.53663464, 82.48943182, 100.7528195, 123.0597717 and 150.3055446, and the predictor-corrector, the exact step there, to 66.19931967, 80.85603163, 98.75778005, 120.6230249 and 147.3292954. nanoql's three steps are that exact step, their one-step mean e^((r−q)Δt), and the predictor-corrector runs on a smile. There QuantLib's corrector, its diffusion the mean of the two ends' on the same increment, would converge to the Stratonovich equation, its error flat in the step count (9.7e-2 at 4, 16 and 64 steps on the first smile below, E[S_T] 0.097 low, and 0.565 low on the second); nanoql's takes each end's drift less ½σ·∂σ/∂ln S. On two SABR smiles each scheme converges, on the same Brownian increments, to a fine Milstein path of 16,384 steps, itself checked against a fine Euler path: Euler at order ½, Milstein and the predictor-corrector at order 1.

Facts:

- `EachStepIsTheExactStepOnFlatParameters`
- `TheOneStepMeanIsTheForward`
- `ThePredictorCorrectorRunsOnASmile`
- `EachSchemeConvergesToTheProcessOnASmile`

## ExtendedOrnsteinUhlenbeckExpectationTests

[`Divergences/ExtendedOrnsteinUhlenbeckExpectationTests.cs`](../tests/NanoQL.Tests/Divergences/ExtendedOrnsteinUhlenbeckExpectationTests.cs)

Documented divergences of `ExtendedOrnsteinUhlenbeckProcess`'s expectation. The trapezoidal rule's (1 − e^(−a·dt))/(a·dt) is 0/0 at a speed of 0 (QuantLib's NaN) and cancels below (2.2e-5 lost at 1e-12); here it is a series below a·dt of 1e-2. QuantLib's Gauss-Lobatto rule integrates b(x)e^(ax) to an absolute 1e-4, out of reach once e^(a(t₀+dt)) is large: it throws from a(t₀+dt) of 35; here it integrates the bounded b(x)e^(−a(t₀+dt−x)) past 30, at a relative 1e-10 beside the absolute accuracy. Both against the closed form x₀e^(−a·dt) + (c₀ + c₁T)(1 − e^(−a·dt)) − c₁((1 − e^(−a·dt))/a − dt·e^(−a·dt)) for b(t) = c₀ + c₁t, T = t₀ + dt, which the trapezoidal rule matches exactly for linear b.

Facts:

- `AtASpeedOfZeroTheTrapezoidalExpectationIsTheValue`
- `TheTrapezoidalExpectationIsTheClosedFormForLinearLevels`
- `PastTheCutTheGaussLobattoExpectationIsTheClosedForm`
- `AtTheCutTheGaussLobattoExpectationIsContinuous`

## ExtrapolatorCopyTests

[`Divergences/ExtrapolatorCopyTests.cs`](../tests/NanoQL.Tests/Divergences/ExtrapolatorCopyTests.cs)

QuantLib's `enableExtrapolation` and `disableExtrapolation` set a flag on the object, for every holder of it. In nanoql they return a modified copy, of the receiver's own type, and leave the receiver unchanged.

Facts:

- `TheCopysFlagChangesAndTheReceiversDoesNot`
- `TheCopyKeepsItsTypeAndItsValues`
- `ANullReceiverThrows`

## FactorReductionTests

[`Divergences/FactorReductionTests.cs`](../tests/NanoQL.Tests/Divergences/FactorReductionTests.cs)

The factor reduction's loadings and its last pass: QuantLib reads the loadings from the matrix's row at the largest eigenvalue's index, the input's first row, so an exact one-factor matrix with loadings (0.9, 0.7, 0.5, 0.3, 0.8) gives (0.546, 0.63, 0.45, 0.27, 0.72); nanoql's principal factor iteration gives the loadings, to QuantLib's stopping tolerance of 1e-6 relative change. And QuantLib refuses a reduction that converges on its last allowed pass: its own iteration on that matrix converges at its second pass, which maxIters 1 allows, and refuses there, "convergence not reached after 1 iterations". QuantLib's values are its own.

Facts:

- `AnExactOneFactorMatrixGivesItsLoadings`
- `ConvergenceOnTheLastAllowedPassIsAccepted`
- `AZeroFirstLoadingStillIterates`

## Fd2dMismatchedTimesTests

[`Divergences/Fd2dMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/Fd2dMismatchedTimesTests.cs)

The two-dimensional finite-difference basket engine on structures that measure time differently. QuantLib's engine runs on the first process's risk-free times (`fd2dblackscholesvanillaengine.cpp:58-67`) and reads the second process's curves and volatility at them (`fdm2dblackscholesop.cpp:53-54`, `:96-107`), each of which measures time from its own reference date in its own day counter: with asset 2's curves and volatility on Act/360, their discounts and variances by date unchanged, a spread call prices 0.22% below the same market restated on Act/365, and on a risk-free curve settling two business days after the other structures 0.53% below. nanoql's grid runs on the clock spanning both processes' structures, from their earliest reference date on the first process's risk-free day counter, each structure read at the instants its times denote and accruing nothing before its own date (the playbook's row for structures on another clock), so it prices the restated market: QuantLib's engine on that market is the check. A market on one clock keeps QuantLib's arithmetic.

Facts:

- `TheSecondAssetsCurvesOnAnotherDayCounterPriceTheRestatedMarket`
- `ARiskFreeCurveSettlingLaterIsPricedFromTheEarliestDate`
- `TheSecondAssetsVolatilityOnItsOwnClockPricesTheRestatedMarket`

## Fd2dTwoRiskFreeCurvesTests

[`Divergences/Fd2dTwoRiskFreeCurvesTests.cs`](../tests/NanoQL.Tests/Divergences/Fd2dTwoRiskFreeCurvesTests.cs)

The two-dimensional finite-difference basket engine on two risk-free curves. Each asset's operator discounts at its own risk-free rate, and QuantLib's mixed term adds back the first process's (`fdm2dblackscholesop.cpp:106-116`), so its price discounts at the second's, where every analytic two-asset engine discounts on the first's curve: a spread call with asset 2 on a curve at 8% and asset 1 at 5% prices 2.96% below the same market restated onto the first curve (asset 2's dividend yield q₂ + r₁ − r₂, the same forward). nanoql's mixed term adds back the second's rate, so the price discounts on the first process's curve: QuantLib's engine on the restated market is the check, within the scheme's splitting error, the two curves' rates sitting in the two directions' operators where the restated market's sit in one (6e-6 relative here). On one risk-free curve the two read the same rate.

Facts:

- `TheMixedTermAddsBackTheSecondRateSoThePriceDiscountsOnTheFirstCurve`

## FdBarrierMismatchedTimesTests

[`Divergences/FdBarrierMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/FdBarrierMismatchedTimesTests.cs)

The finite-difference barrier and rebate engines on structures that measure time differently. QuantLib takes the maturity from the process's time, the risk-free curve's (`fdblackscholesbarrierengine.cpp:89`, `fdblackscholesrebateengine.cpp:73`), and the dividends' and the knock-in's vanilla conditions' reference date and day counter from the risk-free curve (`:118-120`, `:106-107`), while the operator and the mesher read the other structures at those times on their own clocks: on a risk-free curve on Actual/360, settling two days later, or beside a dividend curve and volatility dated later, its barrier engine is off its own engine on the market restated from the earliest date by 1.6e-3 to 9.6e-3 relative, the knock-ins most, and its rebate engine by 1.1e-4 to 3.6e-4 (QuantLib-Python 1.44). nanoql's engines take their times from the engine's clock (`FdmBlackScholesOp.Clock`), the earliest reference date of the structures on the risk-free curve's day counter, the operator reading each structure at the instants those times denote, nothing accrued before its own date (the playbook's row for structures on another clock), so a knock-in's three parts price on one clock. QuantLib's check: its own engines on the market restated from the earliest date on the clock's day counter (a curve dated later a discount curve at 1 up to its date, a volatility dated later a variance curve at 0 up to its date), to 1e-12, as the finite-difference vanilla engine meets it. Where every structure shares the risk-free curve's date and day counter, the clock is the risk-free curve, and the engines keep QuantLib's bits.

Facts:

- `OnAnAct360RiskFreeCurveTheBarrierEngineIsQuantLibsOnTheRestatedMarket`
- `OnACurveSettlingLaterWithACashDividendTheBarrierEngineIsQuantLibsOnTheRestatedMarket`
- `OnStructuresDatedLaterTheBarrierEngineIsQuantLibsOnTheRestatedMarket`
- `OnACurveSettlingLaterTheRebateEngineIsQuantLibsOnTheRestatedMarket`

## FdCevNearOneTests

[`Divergences/FdCevNearOneTests.cs`](../tests/NanoQL.Tests/Divergences/FdCevNearOneTests.cs)

The finite-difference CEV and SABR engines near β = 1. QuantLib's mesher reads the CEV density's quantiles through Boost's non-central chi-squared, which throws within the band ("Series did not converge" at β 0.9999 on a year at α 0.75, f0 2.1; "beta can not be one" at 1) (QuantLib 1.44). Within |β − 1| < 1e-3 nanoql's mesher takes its bounds from the lognormal limit's quantiles, the CEV density at β = 1, and the engines' upper boundary is the analytic engine's value, Black's formula at Hagan's equivalent volatility there (`Divergences/CevNearOneTests.cs`). The checks: the lognormal's quantiles in closed form, and the analytic CEV engine.

Facts:

- `TheMeshersBoundsAreTheLognormalLimitsQuantiles`
- `TheEnginesInTheBandAreTheAnalyticEngines`

## FdCirModelTests

[`Divergences/FdCirModelTests.cs`](../tests/NanoQL.Tests/Divergences/FdCirModelTests.cs)

The finite-difference engine for a Black-Scholes equity under a Cox-Ingersoll-Ross short rate. QuantLib's operator's rate diffusion is σ²r where the CIR generator's is ½σ²r, and its covariance 2ρσ_Sσ where it is ρσ_Sσ√r (`fdmcirop.cpp:55-60`, `:74-82`): through its own machinery a cash-like put's implied bond is the CIR bond at σ√2 (0.8612436 at σ 0.15, where the CIR bond is 0.8609795 and at σ√2 0.8612489), it blows up (1e32 to 1e226) at ρ ±0.5 on its schemes for every σ, and at σ 0.3 even at ρ = 0; and its rate grid, the process's time-averaged quantiles, leaves the rate's path where it drifts or σ is small (3.58 to 15.7 where the deterministic-rate limit is 4.21 and 3.28). Its test's 4.275 is its own operator on its 10-step grid (4.27310 refined). nanoql's operator is the CIR generator's, its rate grid uniform on [0, max(upper, r₀, θ, E[r_T])], upper the last point of QuantLib's, where at r = 0 the equation is its own boundary condition, and its default scheme Hundsdorfer's, where QuantLib's modified Hundsdorfer blows up on fine grids when the rate drifts up fast. The checks: QuantLib's CIR model's closed-form bond (the rates part, every ρ); the deterministic-rate limit against Black's formula at the mean path's average rate; and a Monte Carlo of QuantLib's CIR process, the log spot's step exact given the rate's path and draws, for ρ ≠ 0 and the rising rate. Flat Actual/365 (Fixed) curves from 15 October 2026.

Facts:

- `ACashLikePutsBondIsTheCirModels`
- `QuantLibsBondIsTheCirBondAtTwiceTheVariance`
- `AtANonZeroCorrelationTheValueIsTheMonteCarlos`
- `ARisingRateAtThirtyPercentIsTheMonteCarlos`
- `TheDeterministicRateLimitIsBlacksFormula`
- `TheDefaultSchemeHoldsTheFastDriftUp`

## FdCirQuantoTests

[`Divergences/FdCirQuantoTests.cs`](../tests/NanoQL.Tests/Divergences/FdCirQuantoTests.cs)

The finite-difference CIR engine with a quanto helper. QuantLib's engine passes the helper to its equity mesher alone, and its operator takes none (`fdcirvanillaengine.cpp:60-63`, `fdmcirop.cpp`), so a quanto prices without the quanto drift, on a mesher centred for it. nanoql's operator subtracts the helper's adjustment with the grid's short rate as the domestic one, r − q − ½v − (r − r_f + ρσσ_fx): the quanto drift r_f − q − ½v − ρσσ_fx, the domestic rate only discounting. QuantLib's check: as the rate's volatility goes to 0 with r₀ = θ the helper's domestic rate, the engine is QuantLib's finite-difference Black-Scholes engine on the same quanto helper.

Facts:

- `AsTheRatesVolatilityVanishesItIsTheBlackScholesQuanto`

## FdHestonBarrierMismatchedTimesTests

[`Divergences/FdHestonBarrierMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonBarrierMismatchedTimesTests.cs)

The finite-difference Heston barrier engines on structures that measure time differently (fixed). QuantLib reads the process's time, the risk-free curve's, and the dividend curve at it (`fdhestonbarrierengine.cpp:72`), as though the two curves shared a clock. nanoql's times run on `FdmHestonOp.Clock`, each curve read at the instants they denote. QuantLib's check: its own engine on the market restated onto the risk-free curve's day counter, the dividend rate scaled so its discounts by date are unchanged; and a risk-free curve settling two days after the dividend curve, read from its own date on the clock from the earliest, where QuantLib's times start at the risk-free curve's date.

Facts:

- `AnAct365DividendCurveBesideAnAct360CurvePricesAsOnTheRestatedMarket`
- `ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock`

## FdHestonDoubleKnockInTests

[`Divergences/FdHestonDoubleKnockInTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonDoubleKnockInTests.cs)

The FD Heston double knock-in (a refusal lifted). QuantLib refuses "only Knock-Out double barrier options are supported" (`fdhestondoublebarrierengine.cpp:52`), where its single-barrier FD Heston engine prices knock-ins as vanilla + rebate − knock-out (`fdhestonbarrierengine.cpp:165-196`). nanoql prices the double knock-in so: QuantLib's FD Heston vanilla less its double knock-out without a rebate, plus the rebate, paid at expiry where no barrier is touched, as R × the engine's cash-or-nothing knock-out at strike 0 (the analytic engines' convention), every leg with the knock-out's leverage function and mixing factor. Checks: at a vanishing vol of vol, QuantLib's own legs, 11.124212 − 1.897300 = 9.226912, and its analytic double knock-in 9.242178, the FD grid's 0.17%; the binary leg against QuantLib's analytic double-barrier binary engine; and parity on a stochastic-local model.

Facts:

- `WithoutARebateTheKnockInIsTheVanillaLessTheKnockOut`
- `TheRebateIsTheBinaryKnockOut`
- `EveryLegReadsTheLeverageFunction`

## FdHestonHighCarryTests

[`Divergences/FdHestonHighCarryTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonHighCarryTests.cs)

The FD Heston engine's equity grid at high carry and low volatility. QuantLib's engine builds its equity mesher on a helper process whose curves its call binds in each other's places (`fdhestonvanillaengine.cpp:126-128`), so the mesher walks the forward's mirror about the spot. Where the carry is large against the volatility the grid's far edge falls inside the distribution and QuantLib's price stops converging: a five-year call at 1.25 times the forward, carry 0.30 and volatility 0.1, prices 0 on every grid, where the analytic engine gives 1.5732 (QuantLib-Python 1.44); where cash dividends take the mirrored walk below zero, its bound isn't a number and QuantLib's engine refuses ("end must be larger than start"). nanoql keeps QuantLib's mesher wherever its range holds the true forwards' band, the walk's ends on the helper's curves in place widened by the volatility's quantile, half the mesher's own margin, and widens it to that band through QuantLib's own constraints where it doesn't, a bound that isn't a number falling short. The multi-strike solve, whose mesher takes no constraints, solves each strike alone where its grid doesn't hold the band at the points the solve is read at, S·K/Kᵢ for the option's strike K, their forwards quanto-adjusted where a quanto helper is given; the two paths run on different grids, so a strike's value steps where the switch moves, by up to about 0.4% at carry 0.06. The band holds the strike too, widened by the same half margin, where an option far from the forwards takes its value. The check: QuantLib's components given those constraints through the harness (`tests/vectors/cxx/pricingengines/vanilla/fdhestonvanillaengine.json`), and the analytic engine's price under refinement.

Facts:

- `AHighCarryCallConvergesToTheAnalyticEngine`
- `ANegativeCarryPutConvergesToTheAnalyticEngine`
- `AStrikeBeyondTheForwardsBandKeepsItsTail`
- `AMarketInsideTheBandKeepsQuantLibsBits`
- `TheMultiStrikeSolveTakesEachStrikeAloneWhereItsGridFallsShort`
- `AReferenceStrikeAwayFromTheSpotTakesEachStrikeAlone`
- `AQuantoForwardFarFromTheDomesticTakesEachStrikeAlone`
- `CashDividendsTakingTheMirroredWalkBelowZeroPrice`

## FdHestonHullWhiteDividendsTests

[`Divergences/FdHestonHullWhiteDividendsTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonHullWhiteDividendsTests.cs)

The FD Heston-Hull-White engine with cash dividends: `FdmDividendHandler`'s three-dimensional defect (`Divergences/FdmDividendHandler3dTests.cs`) reaching the engine. QuantLib's handler moves, on the engine's grid of the log spot, the variance and the short rate, only the lines through the other directions' first points, so most of the grid misses each dividend: an American call with dividends of 2 and 3 is 8.768 at a vanishing short-rate volatility, where QuantLib's FD Heston engine gives 6.738 (QuantLib-Python 1.44). nanoql's handler moves every line, and the engine there is the FD Heston engine's price.

Facts:

- `AtAVanishingRateVolatilityTheDividendsAreTheFiniteDifferenceHestonEnginesPrice`

## FdHestonHullWhiteHighCarryTests

[`Divergences/FdHestonHullWhiteHighCarryTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonHullWhiteHighCarryTests.cs)

The FD Heston-Hull-White engine where the carry is large against the volatility, or a strike far from the forwards. QuantLib sizes the equity mesher on a helper whose curves its call binds in each other's places (`fdhestonhullwhitevanillaengine.cpp:115-131`), so the grid walks the forward's mirror: a five-year call at 1.25 times the forward, carry 0.30 and volatility 0.1, priced 0 where the analytic Heston-Hull-White engine gives 1.7595, on the default grid and on twice its points; and a year's put at 0.7 of the forward, carry −0.2, priced 0 for 0.0068, its strike beyond QuantLib's grid (QuantLib-Python 1.44). nanoql's mesher is QuantLib's wherever its range holds the true forwards' band and the strike's, at half the mesher's margin, and is widened to the bands where it doesn't; its multi-strike solve solves each strike alone where its grid doesn't hold the forwards' band at its read points. Checked against the analytic engine, at no equity-rate correlation, under refinement.

Facts:

- `AtHighCarryTheCallConvergesToTheAnalyticEngine`
- `AStrikeBeyondTheForwardsBandKeepsItsTail`
- `AtHighCarryTheMultiStrikeSolveSolvesEachStrikeAlone`

## FdHestonHullWhiteStrikesReferenceTests

[`Divergences/FdHestonHullWhiteStrikesReferenceTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonHullWhiteStrikesReferenceTests.cs)

The FD Heston-Hull-White engine's multiple strikes after a reference whose payoff isn't a plain vanilla (the FD Heston engine's twin; `fdhestonhullwhitevanillaengine.cpp:78-95`, `:175-188`). QuantLib's cache stores the reference payoff's scaled values V(S·d)/d under plain vanilla keys and serves them to later plain vanillas: after a cash-or-nothing call (100, cash 10) the plain calls at 80, 100 and 120 are served 6.848, 5.968 and 1.616 where their direct prices are 23.033, 8.076 and 1.106 (QuantLib 1.44). nanoql's `CalculateStrikes` solves each strike's plain vanilla alone after such a reference, QuantLib's engine priced for that strike.

Facts:

- `AfterANonPlainReferenceEachStrikeIsSolvedAlone`

## FdHestonKnockInLeverageTests

[`Divergences/FdHestonKnockInLeverageTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonKnockInLeverageTests.cs)

The FD Heston knock-in's legs (fixed). QuantLib prices a knock-in as the vanilla, plus the rebate, less the knock-out, and prices the vanilla and the rebate legs without the leverage function and mixing factor the knock-out leg uses (`fdhestonbarrierengine.cpp:173-194`), so a stochastic-local knock-in mixes pure Heston into it: on the market below the pure-Heston vanilla is 8.782 for the stochastic-local 7.419. nanoql prices every leg on the model it is given. Check: in-out parity on the stochastic-local model against QuantLib's FD Heston vanilla and rebate engines with the leverage function.

Facts:

- `EveryLegReadsTheLeverageFunction`

## FdHestonMismatchedTimesTests

[`Divergences/FdHestonMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonMismatchedTimesTests.cs)

The finite-difference Heston and Bates engines on structures dated or counted differently. QuantLib's engines run from the risk-free curve's reference date (`process->time`) and read the dividend curve, the leverage function and the Bates spread curve at the risk-free curve's times (`fdmhestonop.cpp:55-89`, `fdmbatesop.cpp:45-58`). nanoql's run on `FdmHestonOp.Clock`, from the earliest reference date of the structures read, on the risk-free curve's day counter, each structure read at the instants its times denote, nothing accruing before its own date, the leverage at its instant. QuantLib's check: its engines on the market restated on one clock, which nanoql's engines on the original market reproduce to rounding (QuantLib-Python 1.44).

Facts:

- `ADividendCurveOnAnotherDayCounterIsReadAtItsOwnTimes`
- `ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock`
- `ALeverageFunctionOnItsOwnClockIsReadAtItsInstant`
- `TheBatesSpreadedDividendCurveIsReadAtItsOwnTimes`

## FdHestonMultipleStrikesLeverageTests

[`Divergences/FdHestonMultipleStrikesLeverageTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonMultipleStrikesLeverageTests.cs)

The multiple-strikes calculation with a leverage function. QuantLib's cache serves each strike V(S·d)/d from one solve (`fdhestonvanillaengine.cpp:209-224`), which assumes the model scales with the spot; a spot-dependent leverage breaks it: with a leverage of 1.4, 1.0 and 0.7 at strikes 50, 100 and 150, after a call at 100 it serves 50.50657855051417, 27.518542374323449, 0.8299478410251605, 0.057935950793473072 and 0.0010848235476194543 at 50, 75, 125, 150 and 200 where its engine priced for each strike alone gives 50.704063145305852, 28.070741991049783, 0.43296316837484017, 0.0063957853450491283 and 2.489998860627156e-05 (QuantLib 1.44). nanoql's `CalculateStrikes` solves each strike alone there. Check: QuantLib's engine priced for each strike alone, and at 50 and 200, whose strikes the equity grid's band holds, QuantLib's components on that widened grid, 50.705877448953522 and 2.4854769620642274e-05, which give its engine's bits at the other strikes (QuantLib 1.44).

Facts:

- `EachStrikeIsTheEnginesPriceForItAlone`
- `EachStrikeIsQuantLibsEnginePricedForItAlone`

## FdHestonSkewFactorTests

[`Divergences/FdHestonSkewFactorTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonSkewFactorTests.cs)

The Heston variance mesher's skew factor bounded at σ_m/κ = 2. QuantLib's factor, max(1, σ_m/κ)^1.5 (`fdmhestonvariancemesher.cpp:122-128`), widens the equity mesher at small κ until its prices leave QuantLib's own analytic engine, refining the grid not bringing them back: an at-the-money put (v₀ = θ 0.04, ρ −0.7, a year, r 3%, q 1%) prices 4.8106 for 3.7045 at σ 1.0 and κ 0.1, 26.48 for 3.590 at κ 0.02 (15.69 on 200 × 200 × 100) and 48.52 for 6.2144 at σ 0.3 and κ 1e-8 (QuantLib-Python 1.44). The bounded factor, min(2, max(1, σ_m/κ))^1.5, brings them within the grid's own error of the analytic engine, an American put converges as the grid is refined, and wherever σ_m/κ ≤ 2, every QuantLib case among them, QuantLib's engine stands, the Kahl-Jaeckel ratio at the cap itself.

Facts:

- `LargeRatiosPriceWithinTheGridsErrorOfTheAnalyticEngine`
- `AnAmericanPutConvergesUnderRefinement`
- `AtRatiosUpToTwoQuantLibsEngineStands`

## FdHestonStrikesReferenceTests

[`Divergences/FdHestonStrikesReferenceTests.cs`](../tests/NanoQL.Tests/Divergences/FdHestonStrikesReferenceTests.cs)

The multiple-strikes calculation on a reference that isn't a plain vanilla. QuantLib's cache stores the reference payoff's values scaled, V(S·d)/d, under plain-vanilla keys at each strike (`fdhestonvanillaengine.cpp:209-224`) and serves them to a later plain vanilla (`:174-191`); V(S·d)/d is a vanilla at Kᵢ only for a plain-vanilla reference. After a cash-or-nothing reference (100, cash 10) it serves plain calls at 80, 100 and 120 6.8209430073262878, 5.8085085805369312 and 1.6286553941678532, after an asset-or-nothing one 91.147695950275022, 66.230378622429299 and 17.487133347226365, where its engine priced for each strike alone gives 22.93727546246711, 8.144976787928341 and 1.2010639430197885 (QuantLib 1.44). nanoql's `CalculateStrikes` solves each strike's plain vanilla alone there. Check: QuantLib's engine priced for each strike alone.

Facts:

- `ACashOrNothingReferenceGetsEachStrikeSolvedAlone`
- `AnAssetOrNothingReferenceGetsEachStrikeSolvedAlone`

## FdNdimMismatchedTimesTests

[`Divergences/FdNdimMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/FdNdimMismatchedTimesTests.cs)

The n-dimensional finite-difference basket engine on structures that measure time differently. QuantLib's engine takes its maturity from the first process's risk-free times and reads every inner value's discounts at them (`fdndimblackscholesvanillaengine.cpp:58-63`, `:134-136`): with asset 2's dividend curve and volatility on Act/360, their discounts and variances by date unchanged, a spread call prices 0.16% below the same market restated on Act/365, and an American average put on a risk-free curve settling two business days after the other structures 0.19% below. nanoql runs on the clock spanning every process's structures, each read at the instants its times denote and accruing nothing before its own date, so it prices the restated market: QuantLib's engine on that market is the check. A market on one clock keeps QuantLib's arithmetic.

Facts:

- `TheSecondAssetsStructuresOnAnotherDayCounterPriceTheRestatedMarket`
- `ARiskFreeCurveSettlingLaterIsPricedFromTheEarliestDate`

## FdNdimSingularCovarianceTests

[`Divergences/FdNdimSingularCovarianceTests.cs`](../tests/NanoQL.Tests/Divergences/FdNdimSingularCovarianceTests.cs)

The n-dimensional finite-difference basket engine on a singular covariance. QuantLib meshes each principal direction over 1.3√λ·√T standard normal quantiles (`fdndimblackscholesvanillaengine.cpp:144-163`): an eigenvalue of 0 (a correlation of 1, a zero volatility) meshes its direction at one point, which its spline refuses ("Dimension 1 : invalid data"), and a rounding negative eigenvalue meshes NaN, while its price is continuous towards the singular case (an eigenvalue of 1e-10 prices 10.3739, its Monte Carlo engine 10.3459 ± 0.0506 at ρ = 1). nanoql floors the mesh's eigenvalue at the largest one's times machine epsilon, the operator keeping QuantLib's: QuantLib's own value at near-singular covariances and the Monte Carlo engine are the checks.

Facts:

- `ACorrelationOfOnePricesTheLimit`
- `AZeroVolatilityPricesTheLimit`

## FdNdimTwoRiskFreeCurvesTests

[`Divergences/FdNdimTwoRiskFreeCurvesTests.cs`](../tests/NanoQL.Tests/Divergences/FdNdimTwoRiskFreeCurvesTests.cs)

The n-dimensional finite-difference basket engine on two risk-free curves. QuantLib's American branch builds every asset's forward on the first process's curve (`fdndimblackscholesvanillaengine.cpp:60-68`, `:175-187`), so with asset 2 on a curve at 8% and asset 1 at 5% an American average put prices 9.3498 whatever asset 2's rate, 5.2% above the same market restated onto the first curve (asset 2's dividend yield q₂ + r₁ − r₂, the same forward), and its European branch refuses two curves through its final discount ("interest rates need to be the same for all underlyings", `vectorbsmprocessextractor.cpp:52-57`). nanoql reads each forward on its own process's curves and discounts on the first's in both branches, as QuantLib's spread and Monte Carlo basket engines read two curves: QuantLib's engine on the restated market is the check, and the two-dimensional engine, which drifts each asset on its own curve, prices the American within the grids' error. On one risk-free curve the reads are QuantLib's.

Facts:

- `TheAmericanBranchReadsEachForwardOnItsOwnCurve`
- `TheEuropeanBranchPricesWhereQuantLibRefuses`
- `TheAmericanMeetsTheTwoDimensionalEngine`

## FdSabrZeroNuTests

[`Divergences/FdSabrZeroNuTests.cs`](../tests/NanoQL.Tests/Divergences/FdSabrZeroNuTests.cs)

The finite-difference SABR engine at ν = 0. QuantLib's parameter check accepts ν = 0 (`sabr.cpp:157`), but the log volatility's grid collapses and its concentrating mesher refuses ("end must be larger than start", `fdsabrvanillaengine.cpp:87-95`); so it does at a tiny positive ν, where the grid's span is a few ulps of ln α or none, and a little above that it returns NaN; at ν = 1e-8 it prices as the CEV engines. SABR at ν = 0 is the CEV process with α its volatility, and wherever ν√T is at most 1e-10·max(1, |ln α|) nanoql's engine prices on the finite-difference CEV engine with its own grids, damping steps, scaling, ε and scheme. QuantLib's check: its FD CEV engine, and its FD SABR engine at ν = 1e-8.

Facts:

- `AtZeroNuItIsTheCevEngine`
- `AtATinyNuItIsTheCevEngine`

## FdShoutSwingCirMismatchedTimesTests

[`Divergences/FdShoutSwingCirMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/FdShoutSwingCirMismatchedTimesTests.cs)

The shout, swing and CIR engines on structures dated differently. QuantLib's engines run on the risk-free curve's times and read the dividend curve and the volatility at those times on their own clocks (`fdblackscholesshoutengine.cpp:65-73`, `fdsimplebsswingengine.cpp:47-77`, `fdcirvanillaengine.cpp:64-69`): on a risk-free curve settling two business days after the other structures, QuantLib's shout engine reads 14.261505884225283 and its swing engine 17.000771278512975 (QuantLib-Python 1.44). nanoql's run on `FdmBlackScholesOp.Clock`, from the earliest reference date, each structure accruing nothing before its own, as its finite-difference Black-Scholes engine does (`Divergences/FdmMismatchedTimesTests.cs`). QuantLib's check: its own engines on the market restated from today, the risk-free curve discounting nothing before its date; for the CIR engine, whose QuantLib values are another model's (`Divergences/FdCirModelTests.cs`), nanoql's own engine on that market.

Facts:

- `TheShoutEnginePricesTheRestatedMarket`
- `TheSwingEnginePricesTheRestatedMarket`
- `TheCirEnginePricesTheRestatedMarket`

## FdmBoundaryDirectionTests

[`Divergences/FdmBoundaryDirectionTests.cs`](../tests/NanoQL.Tests/Divergences/FdmBoundaryDirectionTests.cs)

A boundary's direction outside the layout. QuantLib's `FdmIndicesOnBoundary` writes `newDim[direction]` (`fdmindicesonboundary.cpp:31-32`) and `FdmDirichletBoundary` reads `locations(direction)`, past their vectors' ends: undefined behaviour. Here both refuse the direction on entry with `ArgumentOutOfRangeException`, before any read; a direction in the layout is QuantLib's.

Facts:

- `TheIndicesRefuseADirectionOutsideTheLayout`
- `TheDirichletBoundaryRefusesADirectionOutsideTheLayout`

## FdmCevOpDirectionTests

[`Divergences/FdmCevOpDirectionTests.cs`](../tests/NanoQL.Tests/Divergences/FdmCevOpDirectionTests.cs)

`FdmCEVOp` in a direction other than 0. QuantLib's operator builds its second derivative on direction 0 whatever its direction (`fdmcevop.cpp:39-40`), so that in direction 1 of a two-dimensional mesher it differentiates along the other axis, with the CEV coefficients of direction 1: it differs from its own operator in direction 0 of the transposed mesher by 1.107. nanoql's operator differentiates along the direction given. QuantLib's check: its operator in direction 0, its engine's, on the transposed mesher, whose bits nanoql's direction 0 keeps (the operator's vectors).

Facts:

- `InDirectionOneItIsDirectionZeroOnTheTransposedMesher`

## FdmDividendHandler3dTests

[`Divergences/FdmDividendHandler3dTests.cs`](../tests/NanoQL.Tests/Divergences/FdmDividendHandler3dTests.cs)

`FdmDividendHandler` on a grid of three dimensions. At a dividend's time QuantLib moves, for each direction other than the equity's, only the lines along the equity direction through that direction's points with every further coordinate 0 (`fdmdividendhandler.cpp:84-103`): all of a two-dimensional grid, but in three dimensions the lines with two nonzero other coordinates stay as they were, 6 of a 3 × 5 × 4 grid's 12, while the lines it moves equal its one-dimensional handler's. nanoql moves every line as the one-dimensional handler does.

Facts:

- `EveryLineMovesAsTheOneDimensionalHandlerMovesIt`

## FdmDupireApplyMixedTests

[`Divergences/FdmDupireApplyMixedTests.cs`](../tests/NanoQL.Tests/Divergences/FdmDupireApplyMixedTests.cs)

The Dupire operator's mixed term: a one-direction operator has none, but QuantLib's `FdmDupire1dOp::apply_mixed` returns its input (on the wheel, [1, 2, 3, 4, 5] for [1, 2, 3, 4, 5]), which a scheme adding the mixed term would count twice. nanoql's gives zeros, as every other one-direction operator's does, QuantLib's `FdmBlackScholesOp`'s the check. ZABR's own price never reads it: Douglas runs on `apply`.

Facts:

- `TheMixedTermIsZero`

## FdmHestonVarianceMesherFallbackTests

[`Divergences/FdmHestonVarianceMesherFallbackTests.cs`](../tests/NanoQL.Tests/Divergences/FdmHestonVarianceMesherFallbackTests.cs)

The Heston variance mesher's fallbacks, QuantLib's master since 1.44 (#2908's mesher part and #2940, taken as fixes). Where the variance falls fast (v₀ 0.25, θ 0.0025, κ 10, σ 0.1), 1.44's time-averaged quantiles don't reach v₀ and every finite-difference Heston engine throws "interpolation range is [4.01809, 5.16225] x [0, 0.0483231]: extrapolation at (4.60517, 0.25) not allowed" (QuantLib-Python 1.44); master falls back to the default mesh, evenly spaced over θ and v₀ ± 4σ√(θ/2κ), whose locations are those 1.44 builds on its own default branch, and the engine prices within its grid's accuracy of QuantLib's analytic engine. A distribution too narrow for the grid's minimum steps falls back to the same default mesh before any quantile is computed, the mesh 1.44 reaches only after its quantiles fail; at σ 1e-4 master's later checks would reach it too, and at 5.6e-4 and 6.8e-4 (v₀ = θ 0.04, κ 1) that test alone does, the later checks passing the quantile grid.

Facts:

- `AFastFallingVarianceIsPricedOnTheDefaultMesh`
- `TheFallbacksLocationsAreTheDefaultMesh`
- `ADistributionTooNarrowForTheGridFallsBackBeforeItsQuantiles`
- `ADistributionTooNarrowForTheGridStaysOnTheDefaultMesh`

## FdmMismatchedTimesTests

[`Divergences/FdmMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/FdmMismatchedTimesTests.cs)

The finite-difference Black-Scholes engine on structures that measure time differently. Its times are the risk-free curve's, measured from its reference date in its day counter (`blackscholesprocess.cpp:149-152`), and QuantLib reads the dividend curve, the volatility, the local volatility's curves, the quanto helper's structures and the escrowed dividends' curve at those times (`fdmblackscholesop.cpp:51-66`, `fdmblackscholesmesher.cpp:63-103`, `fdmquantohelper.cpp:43-55`, `localvolsurface.cpp:82-118`, `escroweddividendadjustment.cpp`), each of which measures time from its own reference date in its own day counter: a one-year put on a risk-free curve on Act/360, with the discount of one on Act/365 at every date, prices 0.93% above the same market restated on one day counter, and one on a risk-free curve settling two business days after its other structures 0.74% below QuantLib's analytic engine, which counts each structure from its own reference date. nanoql's grid runs on the risk-free curve's day counter from the earliest reference date of the structures it reads, and reads each structure at the instants its times denote, on its own clock, accruing nothing before its own reference date, as the analytic engine counts it: the days before a later-dated structure's date are calendar time on the grid. So the engine prices the market restated from the earliest date, each structure accruing nothing before its own, and QuantLib's own engine on that market is the check, price and theta: exact between Actual/N day counters, and on others, read through the dates and linear within a day, its analytic engine within the grid's error. The theta is the change over the grid's first calendar day, QuantLib's engine's on the restated market: where a structure is dated after the evaluation date, that day accrues none of its dividends or variance, so the theta differs from the formula theta of the analytic and binomial engines, even in sign. The clock is public (`FdmBlackScholesOp.Clock`), and a solver assembled from the engine's types on it is the engine. A market whose structures are dated with the risk-free curve, on its day counter, is read as QuantLib reads it, and keeps QuantLib's bits.

Facts:

- `ARiskFreeCurveOnAnotherDayCounterPricesTheRestatedMarket`
- `ARiskFreeCurveSettlingLaterIsPricedFromTheEarliestDate`
- `EveryMaturityAndGridRunsFromTheEarliestDate`
- `ASolverAssembledOnTheEnginesClockIsTheEngine`
- `EachStructureTheEngineReadsIsReadAtItsInstants`
- `OtherDayCountersAgreeWithTheAnalyticEngine`
- `AStructureDatedAfterTheRiskFreeCurveAccruesNothingBeforeItsDate`
- `AZeroDividendCurveOnAnotherDayCounterIsQuantLibs`

## FdmUndefinedPathRefusalTests

[`Divergences/FdmUndefinedPathRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/FdmUndefinedPathRefusalTests.cs)

The finite-difference core's inputs QuantLib leaves undefined, each refused before the undefined step. A mesher of no point writes the first and last of an empty vector, and runs its loop to the unsigned size less one (`uniform1dmesher.hpp:41-50`, `concentrating1dmesher.cpp:61-103`); a mesher of one point divides by zero and leaves that point at its end and its distances null. A one-point splitting solve runs its back substitution from the unsigned size less two (`triplebandlinearop.cpp:301-303`). An empty layout takes the element before its dimensions' first and the last of an empty spacing (`fdmlinearoplayout.hpp:37-42`). A composite of more meshers than its layout's dimensions reads past them (`fdmmeshercomposite.cpp:80-83`). Bands of different sizes, added or multiplied, are read past the shorter's end (`triplebandlinearop.cpp:163-188`).

Facts:

- `AUniformMesherOfNoPointIsRefused`
- `AUniformMesherOfOnePointIsRefused`
- `AConcentratingMesherOfNoPointIsRefused`
- `AConcentratingMesherOfOnePointIsRefused`
- `AOnePointSolveIsRefused`
- `AnEmptyLayoutIsRefused`
- `MoreMeshersThanDimensionsAreRefused`
- `BandsOfDifferentSizesAreRefused`

## FdmWienerOpDirectionTests

[`Divergences/FdmWienerOpDirectionTests.cs`](../tests/NanoQL.Tests/Divergences/FdmWienerOpDirectionTests.cs)

The Wiener operator in a direction past its count. QuantLib's `apply_direction` and `solve_splitting` read `ops_[direction]` unchecked (`fdmwienerop.cpp:71-79`), past its operators' vector, undefined. nanoql refuses such a direction (the playbook's undefined-behaviour row).

Facts:

- `ADirectionPastTheOperatorsIsRefused`

## FellerConstraintSizeTests

[`Divergences/FellerConstraintSizeTests.cs`](../tests/NanoQL.Tests/Divergences/FellerConstraintSizeTests.cs)

The Heston model's Feller constraint given fewer than three parameters is refused, with `ArgumentException` "the Feller constraint reads three parameters". QuantLib's test reads theta, kappa and sigma as the first three of any array (`hestonmodel.hpp:71-73`), past a shorter one's end, a result C++ leaves undefined. Three parameters or more are QuantLib's defined path, kept.

Facts:

- `FewerThanThreeParametersAreRefused`
- `ThreeParametersAreRead`

## FilonIntegralTests

[`Divergences/FilonIntegralTests.cs`](../tests/NanoQL.Tests/Divergences/FilonIntegralTests.cs)

Filon's coefficients at small θ = t·h: QuantLib's closed forms for α, β and γ cancel as θ falls, so its ∫₀¹ cos(tx) dx on 100 intervals is 5.7e-13 off at t 1, 2.4e-4 off at 1e-4, 5.7e-16 for 1 at 1e-6 and NaN at 0; nanoql takes their Taylor series below |θ| 1.7, within 1e-15 of sin t/t, and QuantLib's closed forms, bit for bit, from 1.7. The references are sin t/t and (1 − cos t)/t at 40 digits; QuantLib's values are its own.

Facts:

- `AtSmallThetaTheCoefficientsKeepTheirDigits`
- `AtThetaZeroTheRuleIsSimpsons`
- `FromThetaOnePointSevenQuantLibsClosedFormsAreKept`

## FireflyAlgorithmTests

[`Divergences/FireflyAlgorithmTests.cs`](../tests/NanoQL.Tests/Divergences/FireflyAlgorithmTests.cs)

The firefly algorithm (fixed). QuantLib's attraction loop stops at k < i − 1, so each firefly misses the next brighter one and the second brightest is attracted by none; its moved fireflies' new values go to the sorted population's place their index names, another firefly's, so values and positions part after the first move; and pure differential evolution assigns its random base through a reference, overwriting the best firefly. nanoql attracts each firefly to every brighter one, which a recording intensity counts, i(i − 1)/2 of M brightest a round; keeps each value with its firefly, which the intensity checks against the cost at every round; and copies the base, so pure differential evolution converges on the shifted sphere, where QuantLib's population of six collapses onto copies of its members and stalls at 1.2e-3 (6.0e-16 for the copy). The decreasing walk's δ squares at each round's first walk from the second round: each walk's scale, read against a walk at δ 1 on the same seed, is δ₀^(2^(r − 1)) in round r, where QuantLib's count squares once every Mfa + 1 walks, so with one firefly δ₀² still in the third round.

Facts:

- `EachFireflyIsAttractedByEveryBrighterOne`
- `ThePopulationKeepsEachFireflysValue`
- `PureDifferentialEvolutionConverges`
- `TheDecreasingWalksDeltaSquaresEachRound`

## FittedCurveRecalculationTests

[`Divergences/FittedCurveRecalculationTests.cs`](../tests/NanoQL.Tests/Divergences/FittedCurveRecalculationTests.cs)

The fitted curve's recalculation path. A fitted curve is always built afresh, fitted from the given guess. QuantLib's curve is a lazy object whose fit ends by keeping its solution as its guess (`fittedbonddiscountcurve.cpp:294-295`), which its L2 penalty reads (`:331-332`), so a curve recalculated after a quote moves starts from, and is penalized towards, its previous fit, though its header says the penalty applies "starting from the initial guess" (`fittedbonddiscountcurve.hpp:182-184`). On the wheel, a Nelson-Siegel fit with L2 penalties of 0.5 over ten bonds, from 16 June 2025, recalculated after the sixth bond's price moves by 0.5, discounts 0.664584692842777 at ten years after 395 evaluations, where a fresh curve on the moved prices from the same guess discounts 0.6644661886683543 after 427. nanoql's curve on the moved prices is QuantLib's fresh curve.

Facts:

- `ACurveOnMovedPricesIsTheFreshFit`

## FixedLocalVolNodeExtrapolationTests

[`Divergences/FixedLocalVolNodeExtrapolationTests.cs`](../tests/NanoQL.Tests/Divergences/FixedLocalVolNodeExtrapolationTests.cs)

The fixed local volatility surface's constant extrapolation in strike at a node time. Between nodes QuantLib holds a strike outside a time's strikes at the nearest one, but at a node, and for every time before the first, which it clamps onto the first node, it evaluates the strike interpolation beyond its range (`fixedlocalvolsurface.cpp:135-137`, `:143-161`): on three strikes 80, 100 and 120 and three times from 15 October 2026, linear in both, at the second node and strike 60 it gives 0.25, where a nanosecond either side it gives 0.22999999995989012 and 0.23000000001994536; before the first node 0.24, where a nanosecond after it 0.2200000000401099 (QuantLib-Python 1.44). nanoql clamps at a node too; QuantLib's check, its own off-node value as the time tends to the node, gives nanoql's values. Inside a time's strikes QuantLib's bits stand.

Facts:

- `AtANodeAStrikeOutsideIsHeldAtTheNearest`
- `BeforeTheFirstNodeAStrikeOutsideIsHeldAtTheNearest`

## FlatExtrapolator2DNullTests

[`Divergences/FlatExtrapolator2DNullTests.cs`](../tests/NanoQL.Tests/Divergences/FlatExtrapolator2DNullTests.cs)

A flat extrapolator decorating no interpolation. QuantLib's constructor calls the decorated interpolation's `update()` through the pointer it is given (`flatextrapolation2d.hpp:39-48, 62-64`), a null dereference, undefined under `std::shared_ptr` and a Boost assertion by default. nanoql refuses it with `ArgumentNullException` naming `decoratedInterpolation`.

Facts:

- `ANullDecoratedInterpolationIsRefused`

## FlatPayoffExtrapolationTests

[`Divergences/FlatPayoffExtrapolationTests.cs`](../tests/NanoQL.Tests/Divergences/FlatPayoffExtrapolationTests.cs)

Flat payoff extrapolation's right tail: with `flatPayoffExtrapolation`, QuantLib's Gaussian1d engines integrate past the grid's last node the constant `p[n − 2]`, the second-last node's value (the swaption engine `gaussian1dswaptionengine.cpp:166-171`, the nonstandard, float-float and cap/floor engines, `Gaussian1dModel::zerobondOption` and the Markov-functional model alike), where the left tail takes `p[0]`, its boundary node. On the wheel, a 5Y into 10Y payer at 2% on a GSR model (a = 0.02, σ = 0.01) on a flat 3% curve, on a grid of 3 standard deviations and 16 points a side, is worth 0.102661800916881; on a grid of 12 standard deviations and 256 points, where the tail's weight vanishes, 0.10415806152703168, and so on the nonstandard engine; on the float-float engine, the same payer with its first leg fixed in all but name (a zero gearing on Euribor 1Y), 0.10173470853181348 against 0.10416117493270043; a 5-year cap at 2% on Euribor 6M from the same start, 0.060558824087763516 against 0.06245438129328943; a put struck at 0.8 on the 10-year bond from the same exercise, on the model's `zerobondOption`, 0.07911771510819356 against 0.08048473087856346; on the harness, the Markov-functional model's own premium of its first calibration point's call at 4.57%, calibrated to five CMS 10y swaptions at 20% on 32 points a side, on 3 standard deviations 0.0004285947663144643 and on 7 0.0004751894691285253. nanoql takes `p[n − 1]`. For a payoff that increases past the grid, the fixed tail lies between QuantLib's and the true one, the one assertion the two values can't both pass; the nonstandard and cap/floor facts ask a tenth of the way from QuantLib's at least, since QuantLib's own tail ties its value only within `erf`'s last bits. No QuantLib 1.44 case sets the flag. QuantLib's check: the same engine on the widened grid. The Markov-functional model's calibration reads the tail too (`markovfunctional.cpp:516-519`), seen alone in its puts, whose payoff vanishes past the grid: on 2 standard deviations, QuantLib's puts on 32 points move as the grid refines, its second-last node meeting the last, and nanoql's on 32 points lie closer to QuantLib's on 512 at each strike from 2.3% to 15%, by up to fifteen times. QuantLib's check: its own section on the refined grid. QuantLib's master fixes the Markov-functional model's two price functions after the pin (#2916, `0bbf88cb3`), the same expression; its case is below, a check of nanoql's model.

Facts:

- `TestFlatPayoffExtrapolationUsesLastGridPoint`
- `TheSwaptionEnginesRightTailTakesTheLastNode`
- `TheNonstandardEnginesRightTailTakesTheLastNode`
- `TheFloatFloatEnginesRightTailTakesTheLastNode`
- `TheCapFloorEnginesRightTailTakesTheLastNode`
- `TheZerobondOptionsRightTailTakesTheLastNode`
- `TheMarkovFunctionalModelsRightTailTakesTheLastNode`
- `TheMarkovFunctionalCalibrationsRightTailTakesTheLastNode`

## FloatFloatBasketRefusalTests

[`Divergences/FloatFloatBasketRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/FloatFloatBasketRefusalTests.cs)

The float-float engine's calibration basket refuses, with QuantLib's master's texts (#2845, after the pin), the two expiries its initial guess can't be built at: master's `testGaussian1dFloatFloatSwaptionCalibrationInitialGuessBoundaries`, on its fixture (Euribor 6M and 3M, 100 a leg, a flat 5% from the settlement date, GSR σ = 0.01, a = 0.03), the evaluation date fixed at 15 January 2025. QuantLib 1.44 refuses both with a schedule's text instead: on the wheel, "effective date (October 21st, 2025) later than or equal to termination date (October 21st, 2025)" and "effective date (January 19th, 2027) later than or equal to termination date (January 19th, 2027)" (the first exercise a one-date Bermudan with a one-element rebate vector, the wheel binding no constructor with one rebate).

Facts:

- `AnExpiryAfterLeg1sLastResetIsRefused`
- `Leg1NotionalsSummingToZeroAreRefused`

## FloatFloatRebateTests

[`Divergences/FloatFloatRebateTests.cs`](../tests/NanoQL.Tests/Divergences/FloatFloatRebateTests.cs)

The float-float engine's rebate: QuantLib values it at y = 0 on the model curve, where the nonstandard engine values it on the grid from its payment date. On a Bermudan payer exercised yearly from a year out into a 5-year swap of 1,000,000, its first leg fixed at 3% in all but name (a zero gearing on Euribor 1Y, 30/360) against Euribor 6M, on GSR (a = 0.01, σ = 0.01) on a flat 2.7%, with rebates of 10,000, −5,000, 25,000 and 0 paid 252 TARGET days after each exercise, the rebates add 8481.523651884276 on the wheel's float-float engine and 8226.793645308986 on its nonstandard engine for the same swap and exercise, 3.1% apart. nanoql's float-float engine values the rebate as the nonstandard engine does; the check is that engine, the two engines' rebates within 1e-4.

Facts:

- `TheRebateIsValuedAsTheNonstandardEngineValuesIt`

## FloatFloatSwapOneDateScheduleTests

[`Divergences/FloatFloatSwapOneDateScheduleTests.cs`](../tests/NanoQL.Tests/Divergences/FloatFloatSwapOneDateScheduleTests.cs)

A float-float swap on a schedule of one date. Its leg has no coupon, so each of its vectors is empty, and QuantLib reads the first cap of each leg unchecked (`floatfloatswap.cpp:170`, `:182`): undefined behaviour, which on the wheel ends the process (0xC0000005). nanoql refuses the schedule just before the read, with an `ArgumentException` naming it, from either constructor.

Facts:

- `TheConstantTermsConstructorRefusesIt`
- `ThePerCouponConstructorRefusesIt`

## FloatFloatSwapOptionalLegFairSpreadTests

[`Divergences/FloatFloatSwapOptionalLegFairSpreadTests.cs`](../tests/NanoQL.Tests/Divergences/FloatFloatSwapOptionalLegFairSpreadTests.cs)

A float-float swap's fair spread on a capped or floored leg (fixed). QuantLib derives every fair spread from the leg's BPS, `spread[0] − NPV / (BPS / 1bp)` (`floatfloatswap.cpp:545-571`), which is exact only while each coupon's amount moves one for one with its spread: once a cap or floor binds, the swap rebuilt at QuantLib's fair spread is worth thousands. nanoql's engine solves a capped or floored leg's fair spread on the swap's own pricing, and gives none where no spread zeroes the NPV; an uncapped leg keeps QuantLib's BPS route and its bits. QuantLib's check: its own NPV of the swap rebuilt at the solved spread, −2.9e-11 on 1,000,000 for each of the three solved cases (QuantLib-Python 1.44: Brent on the swap's NPV, accuracy 1e-12).

Facts:

- `ACappedOrFlooredLegsFairSpreadZeroesTheNpv`
- `NoFairSpreadWhereNoSpreadZeroesTheNpv`
- `ARootPastTheDomainsEndIsFound`
- `NoFairSpreadWhereTheSearchRunsOutAtAFullShift`

## FloatFloatSwapRefusalTextTests

[`Divergences/FloatFloatSwapRefusalTextTests.cs`](../tests/NanoQL.Tests/Divergences/FloatFloatSwapRefusalTextTests.cs)

The float-float swap's refusal of a second leg's nominals that don't match its schedule. QuantLib's check names "schedule2 size" and prints the nominals' size in its place (`floatfloatswap.cpp:119-122`): three nominals on a schedule of five dates read "nominal2 size (3) does not match schedule2 size (3)". nanoql prints the schedule's size, as QuantLib's first leg's check two lines up does, its wording kept.

Facts:

- `TheSecondLegsCheckPrintsTheSchedulesSize`

## FloatingDigitalLegSpreadTests

[`Divergences/FloatingDigitalLegSpreadTests.cs`](../tests/NanoQL.Tests/Divergences/FloatingDigitalLegSpreadTests.cs)

The digital legs' zero-gearing period: QuantLib's `FloatingDigitalLeg` builds a period of zero gearing as a fixed coupon at the period's spread, read with a default of 1.0 (`cashflowvectors.hpp:282-286`), where `FloatingLeg` reads 0.0, so with no spreads given the period pays 100%: on the C++ harness, a two-year semiannual digital Ibor leg from 17 January 2025 with gearings (0, 1) and no spreads pays its first coupon at a fixed 1.0, at 0.0 with a spread of 0 given and at 0.01 with 0.01. nanoql reads 0.0, as `FloatingLeg` does. QuantLib's check: its Ibor leg on the same schedule and gearings, whose first coupon is fixed at 0.0 with no spreads given (the wheel).

Facts:

- `AZeroGearingPeriodWithoutASpreadPaysNothing`
- `TheCmsLegReadsTheSameDefault`

## FloatingLegUndefinedInputsTests

[`Divergences/FloatingLegUndefinedInputsTests.cs`](../tests/NanoQL.Tests/Divergences/FloatingLegUndefinedInputsTests.cs)

The floating leg builder given a null index: QuantLib's `FloatingLeg` reads `index->fixingDays()` as a default argument (`cashflowvectors.hpp:156, 169, 188, 204`), a null pointer's dereference, which is undefined. nanoql refuses it with `ArgumentNullException` naming `index`. The empty schedule, which QuantLib refuses with `std::length_error`, is a convention (`Conventions/EmptyScheduleTests`).

Facts:

- `ANullIndexIsRefused`

## ForwardFlatPrimitiveTests

[`Divergences/ForwardFlatPrimitiveTests.cs`](../tests/NanoQL.Tests/Divergences/ForwardFlatPrimitiveTests.cs)

The forward-flat interpolation's primitive past its last node. QuantLib's `primitive` beyond `x[n-1]` adds `(x - x[n-2])·y[n-2]` (`forwardflatinterpolation.hpp:93-97`), while its value there is `y[n-1]` (`:87-88`), so the primitive's slope isn't the value (QuantLib's defect). nanoql's primitive beyond `x[n-1]` is `primitive[n-1] + (x - x[n-1])·y[n-1]`, and QuantLib's expression at and before it. On x = [0, 1, 2, 3] and y = [0.01, 0.02, 0.03, 0.05], at 4 and 5 it is 0.11 and 0.16, where QuantLib's is 0.09 and 0.12: QuantLib's own forward-flat forward curve integrates there as nanoql does, its `-ln D(t)` on the wheel 0.11000000000000003 and 0.16 (`forwardcurve.hpp:155-168`). At 2.5 and 3 both give 0.045 and 0.06.

Facts:

- `ThePrimitivePastTheLastNodeGrowsAtTheValueThere`

## ForwardForwardMappingsTests

[`Divergences/ForwardForwardMappingsTests.cs`](../tests/NanoQL.Tests/Divergences/ForwardForwardMappingsTests.cs)

The forward-forward Jacobian and Y matrix (fixed). QuantLib's `ForwardForwardJacobian` is 1/τ_long minus the long forward's derivative (`value = df*tau[m]*P(m+1)/P(m) - 1`, then `-value/bigTau`, `forwardforwardmappings.cpp:51-53`), and `YMatrix`'s long forward is (P_end/P_start − 1)/τ_long, the negative of g/(1 + τ_long g) (`:86`): on twelve semi-annual forwards at 5% + 0.1% i in periods of 2, QuantLib's J[0][0] is 0.48725 where the derivative is 0.5125, and its Y[0][0] −0.50077 where the elasticity is +0.5013; on quarterly forwards in periods of 4, J[0][0] 0.74012 where the derivative is 0.2599. nanoql's derivative is (P_start/P_end) τ_m (P_{m+1}/P_m)/τ_long and its long forward (P_start/P_end − 1)/τ_long, QuantLib's own `RestrictCurveState` forward. Checks: central bumps of that forward, and of its logarithm against the short forwards' for Y, undisplaced and with long displacements other than the short ones, Y's scaling by (f_j + d_j)/(g_l + D_l).

Facts:

- `TheJacobianIsTheLongForwardsDerivative`
- `TheYMatrixIsTheLongForwardsElasticity`
- `TheDisplacedYMatrixIsTheDisplacedLongForwardsElasticity`

## ForwardMeasureTimeUnsetTests

[`Divergences/ForwardMeasureTimeUnsetTests.cs`](../tests/NanoQL.Tests/Divergences/ForwardMeasureTimeUnsetTests.cs)

A forward-measure process built without its forward measure time: QuantLib's default constructors leave `T_` uninitialized (`forwardmeasureprocess.hpp:42`, `:46`, `:60`, `:64`), so the forward Hull-White and G2 processes' drift and expectation read an indeterminate value, undefined behaviour (the playbook's undefined-behaviour row). nanoql's refuse with "forward measure time not set" until `WithForwardMeasureTime` gives one.

Facts:

- `AnUnsetForwardMeasureTimeIsRefused`

## ForwardNonPlainPayoffTests

[`Divergences/ForwardNonPlainPayoffTests.cs`](../tests/NanoQL.Tests/Divergences/ForwardNonPlainPayoffTests.cs)

The forward and forward-performance engines on a payoff that isn't plain vanilla. QuantLib builds the inner option's plain vanilla payoff from the payoff's type alone (`forwardengine.hpp:84-86`), so a cash-or-nothing forward start prices 10.456, the plain vanilla's, where its value is 5.003. nanoql refuses it, with the text of QuantLib's Monte Carlo forward engines: the refusal's type and text are the facts.

Facts:

- `TheForwardEngineRefusesACashOrNothingPayoff`
- `TheForwardPerformanceEngineRefusesACashOrNothingPayoff`

## ForwardRateAgreementExpiredTests

[`Divergences/ForwardRateAgreementExpiredTests.cs`](../tests/NanoQL.Tests/Divergences/ForwardRateAgreementExpiredTests.cs)

The FRA after its value date. QuantLib's `setupExpired` computes the forward rate (`forwardrateagreement.cpp:84-87`): under the par approximation it reads the curve at the value date, before a curve dated on the evaluation date, and throws "negative time (-0.00277778) given"; and `amount()` then returns `amount_`, which nothing computed (`:74-77`; the wheel read 0.0 for a FRA first priced after its value date) or which a live calculation left. nanoql's expired results carry QuantLib's zero NPV; for the indexed coupon, the forward rate is its fixing and the amount QuantLib's own `calculateAmount` on it (`:110-117`), which reads only the fixing, the strike, the notional and the two dates, so it equals QuantLib's `amount()` on the fixing date, live with the fixing published; for the par approximation, whose curve can't reach the date, both are null.

Facts:

- `AnExpiredIndexedFraCarriesItsFixingsAmount`
- `AnExpiredParFraHasNeitherAmountNorRate`

## ForwardSpreadFirstDateTests

[`Divergences/ForwardSpreadFirstDateTests.cs`](../tests/NanoQL.Tests/Divergences/ForwardSpreadFirstDateTests.cs)

The piecewise forward-spreaded curve before its first spread date. QuantLib's zero rate at a time adds the spread's integral over the time, which it takes from the interpolation's first time (`piecewiseforwardspreadedtermstructure.hpp:152-166`), leaving out the flat spread its own `calcSpread` applies before that time, and reading the interpolation extrapolated backwards before it. On the wheel, spreads of 2% and 3% 100 and 150 days after 15 January 2025 over a flat 2% Actual/365 (Fixed) curve, QuantLib's test set-up (`termstructures.cpp:325-370`), give the forward 4.4% at 120 days, but every discount past 100 days is e^(0.02·100/365) − 1 = 0.5494% too high, 0.99224937 at 120 days against 0.98682726; and QuantLib's own backward-flat case (`termstructures.cpp:508`), on a flat 2% Actual/360 base, gives the zero rate 2.3019% at 110 days, its spread's average 0.30%, where the forwards' average is 2.10%. nanoql integrates the spread from the reference date, flat before the first spread's time. QuantLib's check: its own curve with the first spread prepended at the reference date, whose integral starts at 0, gives nanoql's curve bit for bit on the case's set-up; the zero rate is the forwards' average.

Facts:

- `TheSpreadIsIntegratedFromTheReferenceDate`
- `QuantLibsBackwardFlatZeroRateLeavesOutTheFlatSpread`

## ForwardVegaTermStructureTests

[`Divergences/ForwardVegaTermStructureTests.cs`](../tests/NanoQL.Tests/Divergences/ForwardVegaTermStructureTests.cs)

The forward-start family's vega off a flat volatility. QuantLib's forward engine returns the inner engine's vega on the implied volatility from the reset (`forwardengine.hpp:160-161`) and the cliquet and performance engines Black's per period (`analyticcliquetengine.cpp:106-107`, `analyticperformanceengine.cpp:106-107`): each the derivative in the period's forward volatility σ_f, where a parallel shift ε of the volatilities moves it by (σ₂t₂ − σ₁t₁)/(σ_f·Δt)·ε. On a variance curve with its nodes at the dates, rising (0.10, 0.30, 0.30 at one, two and three years), QuantLib's forward vega misses its own price under that shift by −17.5%, the cliquet's by −9.6%, the performance engine's by −9.8% and the quanto forward's by −18.4%; falling (0.30, 0.25, 0.25), by −6.5%, −3.3%, −3.4% and −6.8%. nanoql scales each period's vega by that factor: exactly 1 where the period's two volatilities are equal, zero among them, so QuantLib's bits stand there; at a zero forward variance between unequal volatilities the one-sided limit, infinite at the forward and 0 off it. The checks: QuantLib's prices under a parallel shift of 1e-6 each way (its tests' bump), the forward-performance engine, which the wheel lacks, against its own; on a flat zero volatility, QuantLib's bits; at equal total variances, the one-sided bumps.

Facts:

- `TheForwardVegaIsThePriceUnderAParallelShift`
- `TheCliquetVegaIsThePriceUnderAParallelShift`
- `ThePerformanceVegasAreThePricesUnderAParallelShift`
- `TheQuantoForwardVegaIsThePriceUnderAParallelShift`
- `AFlatZeroVolatilityKeepsQuantLibsBits`
- `AtEqualTotalVariancesTheVegaIsTheOneSidedLimit`

## FranceMoveableFeastTests

[`Divergences/FranceMoveableFeastTests.cs`](../tests/NanoQL.Tests/Divergences/FranceMoveableFeastTests.cs)

France's Ascension and Whit Monday. QuantLib 1.44 holds them on May 10th and May 21st (`france.cpp:60-63`), right only when Easter Monday is April 2nd; QuantLib's master moves them with Easter, Easter Monday plus 38 and 49 days (#2893, `16414224f`), with five new test lines, which nanoql takes ahead of the pin, as a fix. The lines below are upstream's; QuantLib 1.44's answers are recorded beside them.

Facts:

- `AscensionAndWhitMondayFollowEaster`

## FuturesNullMaturityTests

[`Divergences/FuturesNullMaturityTests.cs`](../tests/NanoQL.Tests/Divergences/FuturesNullMaturityTests.cs)

A custom futures helper built from dates with no end date keeps a null maturity, as QuantLib's does (`ratehelpers.cpp:126-128`), and the bootstraps drop it as expired; but its implied quote refuses the null maturity before it reads the curve, with the term structure's text for a date before its reference date. QuantLib computes a year fraction from the null date (`:133`), which a day counter reading the month or the day reads out of bounds, and its implied quote discounts to the null date (`:159-160`), which a curve whose day counter reads serial numbers alone refuses as a "negative time" and one reading the month or the day reads out of bounds: a result C++ leaves undefined, and a text nanoql can't form from a null date.

Facts:

- `ACustomFuturesHelperWithNoEndDateRefusesItsImpliedQuote`

## FwdPeriodAdapterTests

[`Divergences/FwdPeriodAdapterTests.cs`](../tests/NanoQL.Tests/Divergences/FwdPeriodAdapterTests.cs)

The period adapter's default displacements (fixed) and its pseudo-roots on the fixed Y matrix. With no displacements given, QuantLib's k-th long rate's is the running sum of the first (k + 1)·period short ones over the period, its `sum` never reset and its index from 0, not the offset (`fwdperiodadapter.cpp:54-64`): on twelve semi-annual rates displaced 1%, 0.01, 0.02, … 0.06 in periods of 2, and on displacements 0.1% to 1.2%, 0.0015, 0.005, 0.0105 … where the periods' means are 0.0015, 0.0035, 0.0055 … (the C++ harness's `FwdPeriodAdapter`). nanoql takes each period's own short displacements' mean, from the offset. And the adapter's pseudo-roots are the Y matrix times the short model's, where QuantLib's Y is its defective one, its long forwards' variances 3 times too high on quarterly forwards in periods of 4. Checks: equal short displacements give QuantLib's own model for that one displacement given (the one-displacement branch), bit for bit; the means; and each step's long covariance the short covariance through the long forwards' elasticities by central bumps of QuantLib's restricted state.

Facts:

- `EqualDisplacementsAreTheOneGiven`
- `EachLongDisplacementIsItsPeriodsMean`
- `TheLongCovariancesAreTheShortOnesThroughTheElasticities`

## FxResetTodaysFixingTests

[`Divergences/FxResetTodaysFixingTests.cs`](../tests/NanoQL.Tests/Divergences/FxResetTodaysFixingTests.cs)

The discounting FX reset pricer's rate for a reset fixed today (fixed). QuantLib forecasts today's rate when the registry's lookup fails, as a fixing not yet published, but catches every `Error` to do so (`fxresetcashflows.cpp:92-98`): a published fixing of zero or below, which its own check refuses ("FX fixing … must be positive", `:126-129`), is forecast instead, 1.100090414674525 on the facts' curves, while the same fixing yesterday is refused. nanoql forecasts only a missing rate, which the registry refuses with either of its two texts, the smart lookup's "no conversion available" and the direct lookup's "no direct conversion available", which a pair triangulated through the other currency reaches (`exchangeratemanager.cpp:137-146`, `:65-79`); every other refusal passes. QuantLib's check: the same fixing on a past date (QuantLib-Python 1.44).

Facts:

- `TodaysNonPositiveFixingIsRefused`
- `ATriangulatedPairsMissingFixingIsForecast`

## G2AnalyticSwaptionCouponTests

[`Divergences/G2AnalyticSwaptionCouponTests.cs`](../tests/NanoQL.Tests/Divergences/G2AnalyticSwaptionCouponTests.cs)

G2's analytic swaption engine's coupons: QuantLib's pricing function takes each fixed coupon as the rate times the difference of its payment times on the curve's day counter (`g2.cpp:161-162`), whatever the fixed leg's day counter, so a leg on another prices coupons it doesn't pay: on the wheel, a 1Y into 5Y payer at 3% on Euribor 6M, exercised on the swap's start as the engine assumes (`g2swaptionengine.hpp:36-37`), notional 1,000,000, on G2 (0.07, 0.011, 0.3, 0.008, −0.6) on a flat 2.7% Actual/365 (Fixed) curve that discounts and forecasts, prices 8,570.124924904925 on an Actual/360 fixed leg and on a 30/360 one alike; the same Actual/360 coupons restated on the curve's day counter (the rate times 365/360) price 7,909.079470896306, and QuantLib's finite-difference engine (200 × 150 × 150) prices the Actual/360 leg 7,906.788499287265; on the 30/360 leg the finite-difference engine reads 8,593.472518199607 (100 × 100 × 100) and 8,595.225571597253 (200 × 150 × 150), its tree 8,580.247796560228 (100 steps). nanoql's coupons are the corrected rate times the fixed coupons' accrual periods. QuantLib's check: its own analytic engine on the restated leg; its finite-difference engine on the 30/360 leg, within its grid; and a zero fixed rate, whose only coupon is the notional, 122,984.01710640144 on every day counter.

Facts:

- `AnActual360LegIsItsCouponsRestatedOnTheCurvesDayCounter`
- `AThirty360LegIsQuantLibsFiniteDifferencePrice`
- `AZeroFixedRateIsQuantLibsOnEveryDayCounter`

## G2CancellationTests

[`Divergences/G2CancellationTests.cs`](../tests/NanoQL.Tests/Divergences/G2CancellationTests.cs)

The G2 model at small mean reversion: QuantLib 1.44's `V` (`g2.cpp:89-100`) sums O(a²t³) differences of O(1/a) terms, so its zero bond's error grows like 1/a³: on the wheel, 0.8997958885633307 at a = 1e-9, 58,421.40718574682 at 1e-7 and 0.8800439590760323 at 1e-4 (against 0.88003659…, 0.88003659… and 0.88004395350237115). nanoql's are QuantLib's master's forms (#2885, `81fac76b8`): the three terms through one function of at and bt, its series below aτ + bτ = 2, and B with expm1. QuantLib's check: upstream's new test line, `testG2DiscountBondForSmallMeanReversion`, against its 60-digit references at upstream's tolerance; and, at a = 1e-4, QuantLib's own formulas evaluated in 60 digits.

Facts:

- `TheZeroBondNearZeroReversion`
- `TheZeroBondAtSmallReversionAgainstQuantLibsFormulasInSixtyDigits`

## G2ProcessCorrelationBoundTests

[`Divergences/G2ProcessCorrelationBoundTests.cs`](../tests/NanoQL.Tests/Divergences/G2ProcessCorrelationBoundTests.cs)

The G2 process's conditional correlation: QuantLib's step correlation is ρ times the ratio of the factors' kernels (`g2process.cpp:100-111`, `:223-234`), which at a = b and ρ = 1 rounds just above 1, so the standard deviation's √(1 − ρ²) is NaN: on the wheel, the half-year step's [1][1] at a = b = 0.1, σ = η = 0.01, ρ = 1 is NaN, its [1][0] 0.006897919322666819. At a zero step the ratio is 0/0, NaN, and so are the standard deviation's second row and the covariance's terms that read it. nanoql's correlation is ρ·f((a+b)Δt)/√(f(2aΔt)f(2bΔt)), f(x) = (1 − e^(−x))/x, held in [−1, 1] and ρ itself at a = b, its limit at Δt = 0. QuantLib's check: the bound, and the step's limit.

Facts:

- `ThePerfectlyCorrelatedStepIsBounded`
- `TheZeroStepIsItsLimit`

## GarchConvergenceTests

[`Divergences/GarchConvergenceTests.cs`](../tests/NanoQL.Tests/Divergences/GarchConvergenceTests.cs)

GARCH's minimizer convergence (QuantLib's master, #2844, taken). QuantLib 1.44 reads the minimizer's point whatever its end criterion (`garch.cpp:534-536`, "TODO: check return value from minimize()"), so a calibration stopped by its iterations returns a point short of the optimum as calibrated. nanoql refuses it, "GARCH(1,1) calibration did not converge: optimizer stopped with end criterion …", and the mode-based calibration catches the refusal and falls back to its initial guess, as master does; QuantLib's own cases, whose dummy optimizer returns `None`, expect that guess. Check: five Simplex iterations on QuantLib's calibration series, refused from a given guess, and from the moment-matching guess its values, 0.265749, 0.156956 and 0.230964 (QuantLib's `testCalibration`).

Facts:

- `AMinimizerStoppedByItsIterationsIsRefusedOrFallsBackToTheGuess`

## GarchDemeanedVarianceTests

[`Divergences/GarchDemeanedVarianceTests.cs`](../tests/NanoQL.Tests/Divergences/GarchDemeanedVarianceTests.cs)

GARCH's calibrations on squared returns less their mean (refused). QuantLib's two `calibrate_r2` overloads that subtract the mean from the squared returns (`garch.cpp:512-523`, `:544-556`) feed the cost negative "squared returns", so the conditional variance can fall below zero, whose log the likelihood takes, NaN; QuantLib calls neither. nanoql's cost refuses a conditional variance that isn't positive, which only those overloads can produce under the constraint (ω > 0, α, β ≥ 0 on squared returns). Check: on QuantLib's calibration series from (ω, α, β) = (0.01, 0.5, 0), each overload refused with its text, the constraint GARCH's own or a given one.

Facts:

- `ANonPositiveConditionalVarianceIsRefused`
- `ANonPositiveConditionalVarianceIsRefusedUnderAGivenConstraint`

## GarchGradientTests

[`Divergences/GarchGradientTests.cs`](../tests/NanoQL.Tests/Divergences/GarchGradientTests.cs)

GARCH's cost gradient (fixed). QuantLib's `Garch11CostFunction::gradient` and `valueAndGradient` take ∂σ²ₜ/∂θ as its direct term (1, u²ₜ₋₁, σ²ₜ₋₁), dropping β·∂σ²ₜ₋₁/∂θ (`garch.cpp:95-138`), so against central differences of its own value it runs at 0.13 to 0.80 times the true gradient and a gradient method stops short of the optimum: L-BFGS-B from QuantLib's start at cost 0.46017717 where the exact gradient reaches Nelder-Mead's optimum, 0.46008564. QuantLib's own paths, the Simplex and the ACF fit, never read it, so its cases don't move. nanoql's gradient is the recursive one, ∂σ²ₜ/∂θ = (1, u²ₜ₋₁, σ²ₜ₋₁) + β ∂σ²ₜ₋₁/∂θ. Checks, on QuantLib's own calibration series: the conjugate gradient from (ω, α, β) = (0.2, 0.2, 0.2) ends where the cost's central differences vanish, and at the Simplex's optimum.

Facts:

- `TheConjugateGradientEndsWhereTheCostIsStationary`
- `TheConjugateGradientReachesTheSimplexOptimum`

## GarchSeries

[`Divergences/GarchSeries.cs`](../tests/NanoQL.Tests/Divergences/GarchSeries.cs)

The series of the GARCH facts: QuantLib's own calibration case, 50,000 daily returns from 7 July 1962 simulated by GARCH(1,1) at α 0.2, β 0.3 and V_L 0.4 on QuantLib's Mersenne twister at seed 48 and inverse normal, as `garch.cpp`'s `testCalibration` builds it.

## Gaussian1dCapFloorDiscountTests

[`Divergences/Gaussian1dCapFloorDiscountTests.cs`](../tests/NanoQL.Tests/Divergences/Gaussian1dCapFloorDiscountTests.cs)

The Gaussian1d cap/floor engine's discount curve: QuantLib's engine discounts the floating leg on the discount curve but the strike's leg on the model curve (`gaussian1dcapfloorengine.cpp:86-89`, `:158-161`), where its swaption engine discounts both on the discount curve. On the wheel, on a GSR model (a = σ = 0.01) on a flat 3% Actual/365 (Fixed) curve, a caplet at 3% on 1,000,000 of Euribor 6M fixing in a year is worth 1934.325172314739 with a 2% discount curve, where QuantLib's swaption engine gives the one-period swaption into the same coupon against 3% 1898.5255274075153; without the discount curve the caplet's 1870.0808332724143 is the swaption's. nanoql discounts the strike's leg on the discount curve. QuantLib's check: its swaption engine on the one-period swaption.

Facts:

- `TheStrikesLegIsDiscountedOnTheDiscountCurve`

## Gaussian1dCapFloorFixedCapletTests

[`Divergences/Gaussian1dCapFloorFixedCapletTests.cs`](../tests/NanoQL.Tests/Divergences/Gaussian1dCapFloorFixedCapletTests.cs)

A caplet fixed before today on the Gaussian1d cap/floor engine: QuantLib's engine values its known payoff undiscounted (`gaussian1dcapfloorengine.cpp:63-66`, `:138-141`). On the wheel, a caplet at 2.5% on 1,000,000 of Euribor 6M fixed at 3.15% on 16 December 2024, paid on 18 June 2025 (accrual 182/360), seen from 15 January 2025, is worth 3286.1111111111104 on a GSR model on a flat 3% curve, where Black's engine gives 3244.7791791813, the payoff discounted to the payment. nanoql discounts it. QuantLib's check: Black's engine's value of the known payoff.

Facts:

- `TheKnownPayoffIsDiscounted`

## Gaussian1dCapFloorResultsTests

[`Divergences/Gaussian1dCapFloorResultsTests.cs`](../tests/NanoQL.Tests/Divergences/Gaussian1dCapFloorResultsTests.cs)

The Gaussian1d cap/floor engine's at-the-money forwards: QuantLib's engine declares `optionletsAtmForward` and leaves it 0 (`gaussian1dcapfloorengine.cpp:33`, `:219`): on the wheel, a 3-year cap at 3% on Euribor 6M from a year out, on a GSR model on a flat 3% curve, reports six zeros. nanoql reports each coupon's forward, as Black's engine does. QuantLib's check: Black's engine's results.

Facts:

- `TheAtmForwardsAreTheCoupons`

## Gaussian1dForwardRateEndDateTests

[`Divergences/Gaussian1dForwardRateEndDateTests.cs`](../tests/NanoQL.Tests/Divergences/Gaussian1dForwardRateEndDateTests.cs)

The forward rate's end date (QuantLib's #2848, taken ahead of the pin): QuantLib 1.44's `Gaussian1dModel::forwardRate` ends the period by advancing the value date by the index's tenor on its fixing calendar (`gaussian1dmodel.cpp:44-46`); QuantLib's master after the pin ends it at the index's `maturityDate`, which an index of its own overrides. For EURLibor 6M fixed on 20 February 2025, the value date is 24 February and the plain advance 26 August, where its maturity date is 25 August: on the wheel, a GSR model on a flat 3% curve (a = σ = 0.01) gives 0.029812686783318977 at y = 0.4. nanoql's check: the model's own zero bonds over the period to the maturity date.

Facts:

- `ThePeriodEndsAtTheIndexsMaturityDate`

## Gaussian1dMismatchedTimesTests

[`Divergences/Gaussian1dMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/Gaussian1dMismatchedTimesTests.cs)

The Gaussian1d model and engines on curves on another clock (the playbook's row for structures on another clock): QuantLib reads a discount or forwarding curve at the model curve's times, so a curve on another day counter with the same discount factors by date moves a price. On the wheel, on a GSR model (a = σ = 0.01) on a flat 3% Actual/365 (Fixed) curve, a 5Y into 10Y payer at 3% is worth 0.07095344971961592 with a 2% discount curve on Actual/360, where the same curve restated on Actual/365 (Fixed) gives 0.07076745548830388; with the index forecast on a 3.1% Actual/360 curve, 0.06649840575430056 against 0.06817679085140739 restated. On a model curve on 30/360 dated the 30th, a time doesn't tell the 30th of a 31-day month from the 31st: from 30 June 2025, a Bermudan exercised on 30 January 2026, 30 July 2026 and 30 March 2027 into a 5-year payer at 3%, with a 2.5% Actual/365 (Fixed) discount curve, is worth 0.029341545499651206, against 0.02934088072814103 on the curve restated on 30/360 at every date the engine reads; its Digital probability of the exercise on the 30th (the C++ harness) 0.00013119665716211765 against 0.00013119666147197958, and a zero-bond option expiring on 30 January 2026 0.014136151799917162 against 0.014102533018978234. nanoql reads each curve at the instant QuantLib's argument denotes: by the date QuantLib derives a time from (an exercise, a fixing, an option's expiry), where a read by the time would take the 31st; and the numeraire time, which has no date and whose factor cancels, at the instant it denotes on the model's clock. The nonstandard engine reads alike: on the same swaptions as nonstandard swaptions, QuantLib's same numbers. So does the cap/floor engine at a caplet's fixing: a caplet at 3% on 1,000,000 of Euribor 6M fixing in two years, forecast on the 3.1% Actual/360 curve, is worth 2693.625283396357 against 2793.958767701246 restated; on the 30/360 market, one fixing on 30 January 2026 with the discount curve 1528.9285427087634, where QuantLib's swaption engine gives the one-period swaption into the same coupon 1509.9042444210352 and, on the market restated, 1509.8482273231564, the strike's leg read on the discount curve too. So does the float-float engine, at its exercises and its fixing events: on the same swaptions as float-float swaptions, their first legs fixed at 3% in all but name (a zero gearing on Euribor 1Y, 30/360), QuantLib's engine gives 0.0709550276548306 on the Actual/360 discount curve against 0.07076902784741748 restated; on the 30/360 market, where Euribor 6M fixes on 30 July 2026, 0.029342639252770517 against 0.029341974297365136, and the Digital probability of the exercise on the 30th 0.00013119665716211765 against 0.00013119666147197958, the swaption engine's; with rebates of 0.005, 0.002 and 0.001 paid on the exercise dates, whose bond to their payment is 1 on any curve and in any state (so a reading at y = 0 moves none), 0.030447425948888763 against 0.030446802464572535. QuantLib's check: its engine on the market restated on the model curve's clock (on the 30/360 market, a discount curve with a node at each date to 2031 whose 30/360 time is new, and at the numeraire's 60 years); for a caplet with a discount curve, its swaption engine on the one-period swaption.

Facts:

- `ADiscountCurveOnAnotherDayCounterIsReadByDate`
- `AForwardingCurveOnAnotherDayCounterIsReadByDate`
- `AnExerciseOnThe30thOfA31DayMonthIsReadByDate`
- `TheDigitalProbabilitiesReadTheExerciseByDate`
- `AZerobondOptionExpiringOnThe30thIsReadByDate`
- `TheNonstandardEnginesDiscountCurveOnAnotherDayCounterIsReadByDate`
- `TheNonstandardEnginesDigitalProbabilitiesReadTheExerciseByDate`
- `ACapletsForwardingCurveOnAnotherDayCounterIsReadByDate`
- `ACapletFixingOnThe30thIsReadByDate`
- `TheFloatFloatEnginesDiscountCurveOnAnotherDayCounterIsReadByDate`
- `TheFloatFloatEnginesExercisesAndFixingsOnThe30thAreReadByDate`
- `TheFloatFloatEnginesRebatesOnThe30thAreReadByDate`

## Gaussian1dNumeraireExtrapolationTests

[`Divergences/Gaussian1dNumeraireExtrapolationTests.cs`](../tests/NanoQL.Tests/Divergences/Gaussian1dNumeraireExtrapolationTests.cs)

The numeraire's read of a given curve: GSR's numeraire at t = 0 reads a curve it is given at the numeraire time without extrapolation (`gsr.cpp:216-220`), where it reads its own with it, so on a 30-year curve QuantLib's `numeraire(0, 0, yts)` throws "time (60) is past max curve time (30.0192)", and so does every Gaussian1d engine given that discount curve. nanoql reads it with extrapolation. QuantLib's check: its own route, the model built on that curve, whose numeraire at 0 is 0.1319979111949979, and at (1.5, 0.3) 0.1358278427215987, the value QuantLib's given-curve read gives there too. The Markov-functional model's numeraire reads a given curve so too, at t = 0 and in the ratio past it (`markovfunctional.cpp:775-785`): on a 10-year curve its numeraire time of 15 years throws "time (15.0192) is past max curve time (10.0055)"; QuantLib's check, the same curve with its extrapolation enabled. In the ratio past t = 0 the model's own curve is read too, without extrapolation, where the calibration reads it with (`:783-785`, `:797-799`, against `:467-469`): a model on a 10-year curve calibrated to 1y to 5y into 10y swaptions, its numeraire time 15.02, refuses every read with a given curve, "time (15.01917808219178) is past max curve time (10.005479452054795)"; nanoql reads its own curve with extrapolation there. QuantLib's check: its own curve given, where the ratio is 1, the prices without one.

Facts:

- `AGivenCurveIsReadWithExtrapolation`
- `TheMarkovFunctionalModelReadsAGivenCurveWithExtrapolation`
- `TheMarkovFunctionalModelReadsItsOwnCurveWithExtrapolationInTheRatio`

## Gaussian1dSmileSectionCapletTests

[`Divergences/Gaussian1dSmileSectionCapletTests.cs`](../tests/NanoQL.Tests/Divergences/Gaussian1dSmileSectionCapletTests.cs)

The Gaussian1d smile section on an Ibor index. Its caplet: QuantLib's starts on the fixing date (`gaussian1dsmilesection.cpp:61-62`, `:88-91`), so it fixes two business days before the date of the forward it is quoted against; nanoql's starts on the fixing's value date, so it fixes on the section's date. On a GSR model (a = 0.01, σ = 0.01) on a flat 2.7% and Euribor 6M fixing in a year, QuantLib's formula with its caplet (struck at the forward, which it would read) gives an at-the-money volatility of 0.3707922589726553, its caplet fixing on 13 January 2026; the caplet fixing on the section's date, 15 January, 0.37188265489834393, both on the wheel. And its constructor: QuantLib 1.44 refuses every Ibor section, with or without a forwarding curve, "cannot calculate ATM without a BlackCapFloorEngine or BachelierCapFloorEngine" [the harness], its `MakeCapFloor` without a strike asking for an engine that gives its curve; the annuity reads only the caplet's dates, so nanoql's builds it: its level on the index's curve or, without one, the model's, its prices on an index with a curve.

Facts:

- `TheAtmVolatilityIsTheCapletFixingOnTheSectionsDate`
- `AnIborSectionIsBuilt`

## GaussianCopulaZeroVarianceTests

[`Divergences/GaussianCopulaZeroVarianceTests.cs`](../tests/NanoQL.Tests/Divergences/GaussianCopulaZeroVarianceTests.cs)

The Gaussian copula spread engine where a volatility accrues no variance to expiry: at a zero volatility, and at an expiry on the volatility's own date. QuantLib reads the smile at expiry through its density calculator, which refuses a smile with no variance ("non-positive at-the-money volatility", `gaussiancopulaspreadengine.cpp`, `smilesectionrndcalculator.cpp`), where Kirk's and the other spread engines price both; nanoql puts that asset at its forward. The checks: Kirk's engine, exact at a zero second volatility, and Black's put on the second asset at a zero first volatility, each within the engine's quadrature (QuantLib's own copula cases hold 1e-3), and the engine's own value at a volatility of 1e-6, which QuantLib's prices too, within 1e-5; the deterministic value at both zero and at an expiry on the volatilities' date; Kirk's value at an expiry before it. A surface whose strikes stop below the forward prices QuantLib's bits: the test for no variance is the density calculator's own, at the forward on the smile section, which reads every strike with extrapolation.

Facts:

- `AZeroSecondVolatilityMeetsKirk`
- `AZeroFirstVolatilityIsBlacksPutOnTheSecond`
- `BothZeroAreTheDeterministicValue`
- `AnExpiryOnTheVolatilitiesDateIsAtTheForwards`
- `AnExpiryBeforeTheVolatilitiesDateIsAtTheForwards`
- `ASurfaceWhoseStrikesStopBelowTheForwardIsQuantLibs`

## GaussianQuadratureRefusalTests

[`Divergences/GaussianQuadratureRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/GaussianQuadratureRefusalTests.cs)

`MultiDimGaussianIntegration` with no dimension. QuantLib writes `spacing[0] = 1` on an empty vector (`gaussianquadratures.cpp:101-102`), an undefined write; nanoql refuses on entry, after QuantLib's own allocations.

Facts:

- `NoDimensionIsRefused`

## GemanRoncoroniDeviationTests

[`Divergences/GemanRoncoroniDeviationTests.cs`](../tests/NanoQL.Tests/Divergences/GemanRoncoroniDeviationTests.cs)

The Geman-Roncoroni process's standard deviation over a step at small mean reversion θ₁, in its cancellation-free form through expm1 below 2θ₁Δt 1e-2, σ√Δt at θ₁ = 0 (fixed). QuantLib's √(σ²(1 − e^(−2θ₁Δt))/(2θ₁)) cancels as θ₁ falls and is 0/0 at 0. On QuantLib 1.44 (σ² 10, a 0, Δt 0.004) the deviation is 0.19999999994736442 at θ₁ 1e-6, 0.20000056338475591 at 1e-9, 0.19992005623875167 at 1e-12 and NaN at 0, for 0.1999999996, 0.1999999999996, 0.1999999999999996 and 0.2; nanoql's meets 40-digit references (mpmath) to a few ulps at every θ₁, QuantLib's formula at 35.

Facts:

- `TheDeviationMeetsItsReference`

## GeneralizedHullWhiteSmallReversionTests

[`Divergences/GeneralizedHullWhiteSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/GeneralizedHullWhiteSmallReversionTests.cs)

Generalized Hull-White's analytical fitting parameter and its Ornstein-Uhlenbeck process's variance at small and negative reversion, in their cancellation-free forms through expm1 below |a| 1e-2 (fixed). QuantLib cuts both at a < √ε to the zero-reversion forms σt and σ²Δt, which jump there and cancel just above, and which every negative reversion takes. On QuantLib 1.44 (σ 0.01, a flat 3% curve, φ at 5 years, the variance over a year) the variance at −0.05 is 1e-4, 4.92e-2 low, and φ(5) 0.0312500000015574, 3.63e-4 low; at 1.4e-8 the variance is 1e-4, 1.4e-8 relative off; at 2e-8 4.47e-11. nanoql's values meet 40-digit references (mpmath) to a few ulps at every reversion from −0.1 to 0.1, QuantLib's formula from |a| 1e-2.

Facts:

- `TheProcessVarianceMeetsItsReference`
- `TheFittingParameterMeetsItsReference`
- `QuantLibsCutIsFarOffAtANegativeReversion`

## GenericLSRegressionBasisSizeTests

[`Divergences/GenericLSRegressionBasisSizeTests.cs`](../tests/NanoQL.Tests/Divergences/GenericLSRegressionBasisSizeTests.cs)

The regression's basis size (fixed). QuantLib takes the number of basis functions from path 0, valid or not (`genericlsregression.cpp:39`), and the node-data collection leaves an invalid path's values empty, so a path ended before an exercise, first among the paths, makes QuantLib copy every valid path's values past a one-entry buffer: a crash. nanoql takes the first valid path's, and refuses an exercise no path reaches with the parametric optimisation's text, "no valid paths". Check: QuantLib's regression on the same data with the invalid path moved last, bit for bit at the exercise the path misses, within the reordered sums' rounding at the one before.

Facts:

- `AnInvalidFirstPathIsSkipped`
- `AnExerciseNoPathReachesIsRefused`

## GeometricAsianProductRangeTests

[`Divergences/GeometricAsianProductRangeTests.cs`](../tests/NanoQL.Tests/Divergences/GeometricAsianProductRangeTests.cs)

The geometric running product outside double's range (fixed). QuantLib's Monte Carlo geometric pricers guard the product against overflow only (`mc_discr_geom_av_price.cpp:45-57`), so 252 daily fixings at spot 0.01 price 0 where the analytic engine gives 0.059 × the spot; and the all-past-fixings constructor multiplies the past fixings into the running product (`asianoption.cpp:95`), so 120 past fixings at 1,000 make the analytic and Monte Carlo engines price ∞. nanoql flushes the product into the average where the next factor would underflow it, as where it would overflow, and carries the past fixings' log-sum beside QuantLib's product, its readers taking the log where the product isn't finite and normal. Check: homogeneity, the same contract with the market and the fixings scaled into range and the price scaled back (r 5%, q 0, σ 20%, Act/365 Fixed).

Facts:

- `TheMonteCarloAtASmallSpotIsTheScaledPrice`
- `TheAnalyticEngineOnALargeSeasonedProductIsTheScaledPrice`
- `TheMonteCarloOnALargeSeasonedProductIsTheScaledPrice`

## GeometricAsianTermStructureTests

[`Divergences/GeometricAsianTermStructureTests.cs`](../tests/NanoQL.Tests/Divergences/GeometricAsianTermStructureTests.cs)

The geometric Asian engines on term structures. QuantLib's discrete and continuous average-price engines read one Black volatility at the exercise date and the zero rates to it for every fixing (`analytic_discr_geom_av_price.cpp:92-108`, `analytic_cont_geom_av_price.cpp:57-78`), and the average-strike engine one volatility at the spot: on a rising market (forward rates 1%, 3%, 6%, dividends 0%, 1%, 3%, forward volatilities 15%, 25%, 35% to half a year, a year and two) a call at 100 on twelve fixings every 46 days prices 7.937526857004053, continuously over 548 days 7.38232785034412 and on the average strike 7.943289362148803, and on the falling market (each reversed) 7.896230538510976, 7.377847985865606 and 7.895540021985099: +37.4% and −18.9% off the exact discrete price, +39.4% and −19.2% off the continuous one. nanoql prices the average exactly under deterministic structures, each fixing's forward and variance read by date, and takes the form's own Greeks. Checks: the exact law of ln G (and of ln S_T beside it) computed here, the continuous one's integrals by Gauss-Lobatto split at the structures' nodes; the Greeks against bumps of the spot, of the strike, of every volatility in parallel and of each curve's zero rates; and the continuous form at a variance integral below 2.2e-4 and at a mean whose integrand is rounding noise, each against the flat closed form.

Facts:

- `TheDiscreteFormOnARisingMarketIsExact`
- `TheDiscreteFormOnAFallingMarketIsExact`
- `TheContinuousFormOnARisingMarketIsExact`
- `TheContinuousFormOnAFallingMarketIsExact`
- `TheAverageStrikeOnARisingMarketIsExact`
- `TheAverageStrikeOnAFallingMarketIsExact`
- `TheDiscreteGreeksOnARisingMarketMeetBumps`
- `TheDiscreteGreeksOnAFallingMarketMeetBumps`
- `TheContinuousGreeksOnARisingMarketMeetBumps`
- `TheContinuousGreeksOnAFallingMarketMeetBumps`
- `AShortAveragingAtLowVolatilityIsTheClosedForm`
- `AMeanOfRoundingNoiseIsTheClosedForm`

## GeometricAverageStrikeForwardStartTests

[`Divergences/GeometricAverageStrikeForwardStartTests.cs`](../tests/NanoQL.Tests/Divergences/GeometricAverageStrikeForwardStartTests.cs)

The geometric average-strike engine on a forward-starting average (fixed). QuantLib measures the fixing times from the first fixing (`analytic_discr_geom_av_strike.cpp:57-101`), so it prices a forward-starting average as one starting today and misses e^(−q t₁): on spot 100, q 3%, r 6%, σ 20% (Act/360), QuantLib's own case, a call on ten fixings every 36 days from 36 days on, exercised at the last, prices 4.971092526326882, the value of the same average started today (QuantLib's test expects 4.97109). nanoql measures every time from the evaluation date, in the exact form, and prices 4.956201596311162. Checks: the forward start's value e^(−q t₁) times the value today on a flat market, and an exact Monte Carlo.

Facts:

- `TheForwardStartIsTheValueTodayCarriedByTheDividends`
- `AnExactMonteCarloMeetsTheForwardStart`

## GjrGarchExpiryTests

[`Divergences/GjrGarchExpiryTests.cs`](../tests/NanoQL.Tests/Divergences/GjrGarchExpiryTests.cs)

The analytic GJR-GARCH engine at expiry. QuantLib's engine counts the days to the maturity as T = lround(days·t) and divides by T (`analyticgjrgarchengine.cpp:62`): an option expiring on the evaluation date, priced with `includeReferenceDateEvents`, is 0/0, NaN. nanoql prices the forward's discounted intrinsic value there, as the analytic Heston engine does at model time 0. QuantLib's check: its analytic European engine at the same date, the intrinsic value whatever the volatility.

Facts:

- `AnOptionExpiringTodayPricesTheDiscountedIntrinsicValue`

## GjrGarchMismatchedTimesTests

[`Divergences/GjrGarchMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/GjrGarchMismatchedTimesTests.cs)

The GJR-GARCH engines on a risk-free curve dated after the evaluation date. QuantLib's engines run from the risk-free curve's reference date (`process->time`): their days to the maturity are short by the curve's settlement, and an option expiring before the curve's date is refused ("negative time given"). nanoql's run on the process's model clock, from the earliest reference date of its curves, each curve accruing nothing before its own date. QuantLib's check: its engines on the market restated on one clock, the risk-free discount 1 to its date and 5% after (a log-linear discount curve), which nanoql's engines on the original market reproduce to rounding (QuantLib-Python 1.44 and QuantLib 1.44).

Facts:

- `ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock`
- `AnOptionExpiringBeforeTheRiskFreeCurvesDatePrices`

## GlobalBootstrapVariablesTests

[`Divergences/GlobalBootstrapVariablesTests.cs`](../tests/NanoQL.Tests/Divergences/GlobalBootstrapVariablesTests.cs)

Where a global bootstrap's additional variables end up: QuantLib's bootstrap writes the optimizer's values into the quotes its variables manage (`globalbootstrap.hpp:450-451`, `globalbootstrapvars.cpp:34-38`), so they hold the optimum after the curve is built. nanoql's variables are immutable and its curve keeps the optimum, `PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}.AdditionalVariables`: on QuantLib's testGlobalBootstrapVariables, the volatility QuantLib's quote holds after the bootstrap, 0.09861506355792292 (the C++ harness's vector, `cxx/termstructures/globalbootstrapvars.json`, "QuantLib's case"). A curve on a bootstrap with no variables keeps none.

Facts:

- `TheCurveKeepsTheOptimum`

## GmresBreakdownTests

[`Divergences/GmresBreakdownTests.cs`](../tests/NanoQL.Tests/Divergences/GmresBreakdownTests.cs)

`GMRES` on a lucky breakdown, and a zero restart count. QuantLib's loop stops when h(j+1, j) falls below ε² (`gmres.cpp:101-102`) without rotating the column that found it, and solves with the columns before it (`:128-131`). At the first step there are none: k is 0, `y[k-1]` writes before its array, and QuantLib's process crashes on diag(2, 3) with b = (1, 0), an eigenvector. At a later step the dropped column turns a solved system into a refusal: on diag(1, 1, 3, 3) with b = (1, 1, 1, 1), the second step's vector is exactly zero, and QuantLib's `solve` throws "could not converge" where its `solveWithRestart(5)` returns the exact solution. nanoql takes the breakdown as the method defines it, the Krylov space holding the exact solution: it rotates the column and solves with it. `solveWithRestart`'s count bounds the restarts, `restart − 1` after the first cycle (`:54`), and at 0 the bound wraps to none, so QuantLib can run forever where every positive count ends; nanoql refuses 0 on entry, as QuantLib refuses a zero `maxIter`.

Facts:

- `ABreakdownAtTheFirstStepSolvesTheSystem`
- `ABreakdownAtALaterStepSolvesWhatQuantLibsRestartedSolveSolves`
- `AZeroRestartCountIsRefused`

## GridEngineSpreadTests

[`Divergences/GridEngineSpreadTests.cs`](../tests/NanoQL.Tests/Divergences/GridEngineSpreadTests.cs)

The tree engine's spread on a lattice reused across calls. QuantLib's time-grid engine builds its tree once and keeps it (`latticeshortratemodelengine.hpp:80-95`), and sets a spread on it only when the spread isn't 0 (`treecallablebondengine.cpp:69-74`), so the solver's last trial spread stays on the tree: on testCached's callable bond with an 80-step grid through its mandatory times, after a clean price at a spread, the wheel's OAS for a clean price of 104 is 0.0, where its time-steps engine gives 0.011561251367301573, and its NPV after the OAS is 11205.506593769049, against 11205.506591750021 before. nanoql's lattices are immutable and its engines hold nothing: the lattice overload, given one tree for every call, and the grid overload price at the spread each call is given, so the grid engine's OAS is the time-steps engine's, and the NPV after it is the NPV before.

Facts:

- `ASharedLatticeKeepsNoSpreadAcrossCalls`

## GsrForwardMeasureTimeTests

[`Divergences/GsrForwardMeasureTimeTests.cs`](../tests/NanoQL.Tests/Divergences/GsrForwardMeasureTimeTests.cs)

GSR's forward measure time (QuantLib's #2842, taken ahead of the pin): QuantLib 1.44's `GsrProcess::setForwardMeasureTime` moves the process's time and leaves its core's (`gsrprocess.hpp:85-88`), so a process on step times 1 and 2, volatilities 0.01, 0.012 and 0.009 and reversion 0.03, built at T = 10 and moved to 20, still gives the expectation E(x(1.5) | x(0) = 0) of T = 10, −0.001238869348433307, where a process built at 20 gives −0.002344324730472735, and its drift at (1, 0) mixes the two times, −0.0010387193164901856 against −0.001988418783549303; and it accepts a time below the last step time (1.5). QuantLib's master after the pin moves the core with the process and refuses such a time with its text, "forward measure time (1.5) must be greater than or equal to the last vol/reversion pillar time (2)", as nanoql's `WithForwardMeasureTime` and `Gsr.WithNumeraireTime` do. QuantLib's check: its process built at the new time.

Facts:

- `TheCopyMovesTheCoresTime`
- `ATimeBelowTheLastStepTimeIsRefused`

## GsrIterativeWeightsTests

[`Divergences/GsrIterativeWeightsTests.cs`](../tests/NanoQL.Tests/Divergences/GsrIterativeWeightsTests.cs)

GSR's iterative calibrations with weights: QuantLib's `calibrateVolatilitiesIterative` passes the whole weight vector to each one-helper step (`gsr.hpp:116-120`), so every weight vector of more than one weight is refused: "mismatch between number of instruments (1) and weights (3)". nanoql weights each step by its own helper's weight. QuantLib's check: its one-helper calibration with that weight, step by step, which on the wheel ends at the parameters (0.02, 0.006141458164186859, 0.005500939169047698, 0.004884008618675642), its last step's end criterion 3 (stationary function) after 12 evaluations.

Facts:

- `EachStepIsWeightedByItsOwnHelpersWeight`

## GsrSmallReversionTests

[`Divergences/GsrSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/GsrSmallReversionTests.cs)

GSR's process core at small reversions: QuantLib switches to its zero-reversion formulas below |a| < 1e-4 (`gsrprocesscore.cpp:72-75`), so its outputs jump at the cut, and just above it its two terms over a² cancel to second order. On the wheel, a European 5Y into 10Y payer at 3% on a flat 3% curve with σ = 0.01 is worth 0.06892731403656928 at a = 1e-4·(1 − 1e-6) and 0.068867423550291 at a = 1e-4·(1 + 1e-6), a jump of 8.7e-4. nanoql computes each term below |a| < 1e-2 in a form continuous in a (`expm1` and φ₂(u) = (eᵘ − 1 − u)/u²), QuantLib's zero-reversion formulas at a = 0 exactly, and QuantLib's formulas and bits at and above 1e-2. QuantLib's checks: its own formulas at the switch, which the forms meet at the process's members, on a process with step dates as without: the measured gaps, 1.3e-12 on one piece and 2e-12 on the stepped process here, are the switch's own step of a acting on the expectation's cancelling parts; each side, at its own reversion, is within about 2e-13 of exact; and its own zero-reversion formulas, the forms' limit, which the process meets as a → 0, each term moving in proportion to a. A price answers a change of its inputs at the 1e-12 level by about as much on this swaption, and by up to 2e-9 on 10-year expiries, on either side of the switch, through the engine's grid and spline, so the price fact at the switch (1.1e-12 measured) checks the engine at one point on each side, not the formulas. nanoql's prices here differ from QuantLib's in their last places only by the C runtime's `erf`: with it, QuantLib's bits at a = 0 and a = 1e-2.

Facts:

- `TheFormsMeetQuantLibsFormulasAtTheSwitch`
- `TheSteppedProcessMeetsQuantLibsFormulasAtTheSwitchAndTheLimit`
- `PricesAreContinuousAcrossQuantLibsCut`
- `TheProcessIsSmoothAcrossQuantLibsCut`
- `TheZeroReversionIsTheFormsLimit`

## GsrWithParamsTests

[`Divergences/GsrWithParamsTests.cs`](../tests/NanoQL.Tests/Divergences/GsrWithParamsTests.cs)

GSR's parameters set on a built model: QuantLib's `Gsr::generateArguments` flushes the process core, which recomputes its zero-reversion flags from the core's old reversions, before it sets the new ones (`gsr.hpp:144-149`, `gsrprocesscore.cpp:68-75`, `:41-49`), so after `setParams` the flags lag one parameter set, and a calibration prices each trial point with the previous point's flags wherever the two straddle QuantLib's cut. On the wheel, a European 5Y into 10Y payer at 3% on a flat 3% curve with σ = 0.01, on a model built at a = 0 and set to a = 0.05, is worth 0.06892731403656928, a = 0's price, where the model built at a = 0.05 gives 0.04929346290668454; set from 0.05 to 0, NaN. nanoql's `WithParams` builds a new core from the new reversions. QuantLib's check: the model built at the same parameters, which QuantLib's own second `setParams` with the same values gives too. nanoql's prices here differ from QuantLib's in their last places only by the C runtime's `erf`.

Facts:

- `ParametersSetOnAModelGiveTheModelBuiltAtThem`

## HaganAnalyticNormalisationTests

[`Divergences/HaganAnalyticNormalisationTests.cs`](../tests/NanoQL.Tests/Divergences/HaganAnalyticNormalisationTests.cs)

The analytic Hagan pricer's normalisation: Hagan's analytic forms add sign·G′·annuity·(…) to (discount/annuity)·C(K) (`conundrumpricer.cpp:520-547`, `:573-576`), which presumes G = P(Tp)/A0, where the numeric pricer divides by G (`:472-490`). The shift g-functions' Z omits P(0,Tp)/P(0,T0) (`:783-786`), so their analytic convexity term is high by exactly P/P(Tp) against the numeric twin: on the wheel, on QuantLib's cms.cpp market (flat 5%, its at-the-money matrix), a 1-year-delay coupon fixing in 5 years prices 0.05235802683487254 on the analytic parallel shifts against 0.052312587540360674 numeric, a convexity adjustment 1.045 times the numeric one where the standard model's is 0.994. On an index discounting on a curve of its own (3% against a 5% forwarding curve), every analytic model is 7.9% above its numeric twin. nanoql scales the shift models' analytic term by P(0,Tp)/P(0,T0) on their own curve, and on an exogenous discounting curve takes discount·G′/G for annuity·G′, as the numeric pricer normalises its integrand; the standard and exact-yield models on one curve keep QuantLib's values. QuantLib's check: its numeric twins, which neither part changes.

Facts:

- `TheShiftModelsTermIsQuantLibsTimesTheirDiscountRatio`
- `TheNumericValuesStand`
- `TheShiftModelsAgreeWithTheStandardModel`
- `AnExogenousDiscountingCurveIsNormalised`

## HaganShiftShapeTests

[`Divergences/HaganShiftShapeTests.cs`](../tests/NanoQL.Tests/Divergences/HaganShiftShapeTests.cs)

The shape of the Hagan g-function's shift: QuantLib's `shapeOfShift` computes (1 − e^(−a·x))/a, which cancels as the reversion a nears 0 from above, and reads a negative reversion as 0 (`conundrumpricer.cpp:929-938`): on the wheel, on a flat 3% curve and a 20% volatility, a 1-year coupon fixing in 10 years prices 0.03239868837727989 on the numeric pricer's non-parallel shifts at a = 0 and at every negative a, and 0.032398699033410853 at a = 1e-12, 1.1e-8 away (the analytic pricer 9.5e-9). nanoql takes −expm1(−a·x)/a below 1e-4, negative reversions included, and x at 0, with QuantLib's form at and above 1e-4. QuantLib's checks: its own values at 0 and at 1e-4, on the numeric pricer, whose values stay QuantLib's.

Facts:

- `ASmallReversionIsContinuous`
- `ANegativeReversionIsRead`

## HestonAnalyticMismatchedTimesTests

[`Divergences/HestonAnalyticMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/HestonAnalyticMismatchedTimesTests.cs)

The Heston extensions' analytic engines, surface and density on curves dated or counted differently. QuantLib's engines run the model's time from the risk-free curve's reference date (`process->time`: `coshestonengine.cpp:66`, `exponentialfittinghestonengine.cpp:221`, `hestonexpansionengine.cpp:70`, `analyticpdfhestonengine.cpp:44`, `analyticptdhestonengine.cpp:268-270`), and the PDF engine, the surface and the density calculator read the dividend curve at the risk-free curve's times (`analyticpdfhestonengine.cpp:51-52`, `hestonblackvolsurface.cpp:75-92`, `hestonrndcalculator.cpp:117-118`). nanoql's run on the model's clock, from the earliest reference date of the curves on the risk-free curve's day counter, each curve read at the instant a time denotes, nothing accruing before its own date; the density calculator's times are the risk-free curve's, as `BSMRNDCalculator`'s. The check: QuantLib on the market restated on one clock, which nanoql on the original market reproduces to rounding (QuantLib-Python 1.44, and the C++ harness for the expansions and the det-jump Bates engines).

Facts:

- `CosExponentialFittingAndTheExpansionsRunFromTheEarliestDate`
- `ThePdfEngineReadsEachCurveAtItsInstant`
- `TheTimeDependentModelRunsOnTheModelsClock`
- `TheRoughEngineRunsFromTheEarliestDate`
- `TheBatesEnginesRunFromTheEarliestDate`
- `TheSurfaceAndTheDensityCalculatorReadEachCurveAtItsInstant`

## HestonContinuousGeometricAsianTests

[`Divergences/HestonContinuousGeometricAsianTests.cs`](../tests/NanoQL.Tests/Divergences/HestonContinuousGeometricAsianTests.cs)

The continuous geometric Asian Heston engine's `Phi` and start date (fixed). QuantLib's coefficient of w, `a4_ = logS0*qdcf/dcf − ρv₀/σ + ρκθτ/σ` (`analytic_cont_geom_av_price_heston.cpp:245`), multiplies the log spot by the carry factor where log S_T's constant adds log(D_q/D_r), and adds ρκθτ/σ where that constant, as QuantLib's own a3 for the average, subtracts it: its `Phi(0, 1)`, the expected terminal spot, reads 81.92 against the forward 103.05 (QuantLib 1.44). The prices read w = 0 only, so they are QuantLib's. And QuantLib's engine prices an option with a start date as unseasoned from today, where QuantLib's Black-Scholes continuous engine refuses one; nanoql refuses it with that engine's text. Checks: the forward; at a vanishing vol of vol, Black-Scholes's moments of the average and the spot.

Facts:

- `PhiAtTheSpotsArgumentIsTheForward`
- `AtAVanishingVolOfVolPhisMomentsAreBlackScholess`
- `ThePricesAreQuantLibs`
- `AnOptionWithAStartDateIsRefused`
- `AnAverageStartingTodayIsTheUnseasonedOne`

## HestonDensityTransformCutTests

[`Divergences/HestonDensityTransformCutTests.cs`](../tests/NanoQL.Tests/Divergences/HestonDensityTransformCutTests.cs)

Where the Heston density's Fourier integral stops. QuantLib's density and distribution integrate the characteristic function after the substitution u = e^{−cx}, returning 0 below u = QL_EPSILON (`hestonrndcalculator.cpp:59-86`), so the integral stops at x = −ln ε/c, with c = min(10, max(1e-4, √(1 − ρ²)/σ))·(v₀ + κθt). At a small vol of variance over long terms, and at short terms, the characteristic function, about e^{−Vx²/2} with V the expected integrated variance, is still large there: QuantLib's PDF engine prices a twenty-year call at twice the forward at 6.81 where its analytic engine gives 0.97. nanoql caps c at √(−ln ε·V/2), so the cut falls where e^{−Vx²/2} is below QL_EPSILON, V the PDF engine's window variance; where QuantLib's constant already cuts below it, QuantLib's constant and bits. The check: QuantLib's analytic engine for the prices, its strike derivative for the distribution, and QuantLib's density where the cap is idle.

Facts:

- `WhereQuantLibsCutComesEarlyThePdfEngineMatchesTheAnalyticEngine`
- `WhereQuantLibsCutComesEarlyTheDistributionMatchesTheAnalyticDigital`
- `WhereQuantLibsCutComesLateTheDensityIsQuantLibs`

## HestonExpansionLargeKappaTests

[`Divergences/HestonExpansionLargeKappaTests.cs`](../tests/NanoQL.Tests/Divergences/HestonExpansionLargeKappaTests.cs)

The LPP2 and LPP3 expansions past κt = 100. QuantLib's coefficients (`hestonexpansionengine.cpp:128-720`) take powers of e^{κt} whose products overflow: LPP3's z₀ is inf at κt = 140, its z₁ NaN and its z₃ 0, LPP2's z₀ inf at κt = 200 and NaN from 300, so the engine prices the forward's discounted intrinsic value under the volatility's 1e-8 floor, or throws. nanoql evaluates each coefficient past its upper cut in an extended-range double, QuantLib's expression unchanged, and QuantLib's double, and so its bits, up to it. The check: QuantLib's expressions in Boost's 50-digit `cpp_bin_float_50`, QuantLib's analytic engine, and QuantLib's implied volatilities at κt ≤ 100.

Facts:

- `PastTheUpperCutTheCoefficientsAreTheFiftyDigitValues`
- `PastTheUpperCutThePricesMatchTheAnalyticEngine`
- `UpToTheUpperCutTheImpliedVolatilitiesAreQuantLibs`

## HestonExpansionSmallKappaTests

[`Divergences/HestonExpansionSmallKappaTests.cs`](../tests/NanoQL.Tests/Divergences/HestonExpansionSmallKappaTests.cs)

The LPP2 and LPP3 expansions at small κt, from a small κ or a short term. QuantLib's coefficients (`hestonexpansionengine.cpp:128-720`) are expansions in e^{κt} that cancel in double as κt falls: LPP3's z₃ is −0.196 for 1.310 at κt = 5e-3 and 3.6e8 for 0.478 at a term of 52 minutes at κ 1.5, where its volatility three standard deviations up is 142, and its price at κ = 1e-4 is 9.7% below the analytic engine's; LPP2's z₁ is −73.1 for −0.189 at κt = 5e-7. nanoql evaluates each coefficient below its cut (LPP2's 0.5, 0.2 and 0.5, LPP3's 2, 1, 1 and 1), where QuantLib's double first departs from the expression's exact value by more than 1e-12 relative, in double-double, QuantLib's expression unchanged; below double-double's own floor (LPP2's 2e-6, 5e-7 and 2e-6, LPP3's 1e-3, 5e-4, 5e-5 and 5e-3) in quad-double; below quad-double's floor (LPP2's 1e-12 each, LPP3's 5e-10, 2e-10, 1e-12 and 5e-8) by the cubic in κ through four quad-double values there; and in QuantLib's double, and so its bits, from the cut up. The check: QuantLib's expressions in Boost's `cpp_bin_float` at 100 digits, and at 250 below the double-double floors, QuantLib's implied volatilities at the cuts, QuantLib's analytic engine, and at short terms QuantLib's Forde expansion.

Facts:

- `AtAndBelowTheCutLpp2sCoefficientsAreTheReferenceValues`
- `AtAndBelowTheCutLpp3sCoefficientsAreTheReferenceValues`
- `AtTheCutTheCoefficientsAreContinuous`
- `AtASmallKappaThePricesMatchTheAnalyticEngine`
- `AtShortTermsTheCoefficientsAreTheReferenceValues`

## HestonForwardByDateTests

[`Divergences/HestonForwardByDateTests.cs`](../tests/NanoQL.Tests/Divergences/HestonForwardByDateTests.cs)

The Heston helper's and `AnalyticHestonEngineOptimalAlpha`'s forward on a dividend curve that measures time differently. QuantLib's helper reads both curves at the risk-free curve's time to the exercise date for its Black price and its option's type (`hestonmodelhelper.cpp:68-69`, `:89-91`), where its engine reads them by date (`analytichestonengine.cpp:648-653`): on a dividend curve on Act/360 beside a risk-free curve on Act/365, the helper's one-year at-the-money market value is a put at the forward 101.0050, 7.2184670, while QuantLib's analytic European engine prices that put at 7.2362869 on the forward 100.9629, so a calibration fits its model's prices to the wrong market values. QuantLib's `OptimalAlpha` takes a time and reads both curves at it (`:219-223`). nanoql's helper reads both curves by the exercise date, its market value the analytic engine's price, and `AnalyticHestonEngineOptimalAlpha` gains a date form that reads the forward by date, as the engine does; on curves that share the risk-free curve's day counter and reference date both are QuantLib's.

Facts:

- `TheHelpersMarketValueReadsTheDividendCurveByDate`
- `OptimalAlphasDateFormReadsTheForwardByDate`

## HestonForwardEngineTests

[`Divergences/HestonForwardEngineTests.cs`](../tests/NanoQL.Tests/Divergences/HestonForwardEngineTests.cs)

The analytic Heston forward engine's propagator and outer integral. QuantLib's propagator e^(−(Bν + Λ)/2) · I(√(ΛBν)) underflows times overflows to NaN for resets just above its 1e-3 cut, at a vol of vol of 0.2 for resets of 0.00101 to 0.005 years and forward-start prices of 1 to 7 days (`analytichestonforwardeuropeanengine.cpp:218-231`) (QuantLib 1.44); at κ̂ = κ − ρσ = 0 its B is 0/0 and its R 0·∞ (`:115-117`, `:222`). nanoql takes the propagator through QuantLib's exponentially weighted Bessel function where QuantLib's product isn't finite and nonzero, and B through x/(−expm1(−x)) below |κ̂t| of 1e-2. That exposes QuantLib's outer integral: its 128 nodes over [0, max(2, ...)] in the reset variance carry the propagator's mass to 1.188 at a one-day reset (vol of vol 0.2), pricing 10.376, and where 2κθ < σ² lose it, 0.979 at 182 days (vol of vol 0.5), pricing 5.100. Where QuantLib's nodes miss the propagator's unit mass by more than 1e-6 nanoql takes the expectation in closed form: QuantLib's characteristic function from the reset is affine in the reset variance, so its expectation is the CIR transition's moment-generating function, each probability one Fourier integral. Checks: the Monte Carlo forward Heston engine within three standard errors on resets of a day to six months and R from 6 to 0.05, an independent quadrature of the closed form, the propagator's mass, QuantLib's values where its product is finite and where its nodes carry the mass, continuity at κ̂ = 0 and at a reset today. QuantLib's inner integral, 128 nodes to φ = 100, also fails short tenors (a one-day call at the money 0.297 against the vanilla's 0.420, calls out of the money below 0): its probabilities stand only where within 1e-7 of the resolved route's, the resolved integral on pieces scaled to the strike factor's period. Checks: the analytic Heston vanilla at a reset today, an independent split into pieces, no call below 0 at short tenors.

Facts:

- `TheForwardStartPriceAgreesWithMonteCarlo`
- `WhereQuantLibsNodesMissTheMassThePriceIsTheClosedFormsIndependentQuadrature`
- `ThePriceIsContinuousAtAResetToday`
- `ThePropagatorCarriesUnitMassWhereQuantLibsIsNaN`
- `ThePropagatorIsQuantLibsWhereItsProductIsFinite`
- `TheForwardStartPriceIsQuantLibsWhereItsNodesCarryTheMass`
- `ThePriceIsContinuousAtAZeroKappaHat`
- `AResetTodayIsTheAnalyticHestonVanilla`
- `ShortTenorsArePricedOnTheResolvedIntegral`
- `AtAResetTodayShortExpiriesAreTheAnalyticHestonVanilla`
- `NoCallIsPricedBelowZeroOnShortTenors`

## HestonGeometricAsianCutoffTests

[`Divergences/HestonGeometricAsianCutoffTests.cs`](../tests/NanoQL.Tests/Divergences/HestonGeometricAsianCutoffTests.cs)

The Heston geometric Asian engine's Fourier cutoff (fixed). QuantLib integrates on (0, ξ_max], ξ_max 100, by Gauss-Legendre at 128 points (`analytic_discr_geom_av_price_heston.cpp:48-55`, `:62`), cut before the integrand decays at short maturities and small variance: on a short-dated market (r 5%, q 0, κ 2, ρ −0.5, six fixings), ten days at v₀ = θ 0.0025 and vol of vol 0.1 price 0.11843188016798013 and thirty days 0.37302951787635213, where the integrals converge to 0.24347 and 0.50345, and QuantLib's Monte Carlo geometric Heston engine gives 0.24341 ± 0.0005 and 0.50309 ± 0.0009; the ten-day call at 200 at v₀ 0.04 and vol of vol 0.5 prices −0.06101169378323358. nanoql cuts where Phi's modulus has decayed to 1e-14 of its value on the contour's axis, or at ξ_max where that is later. Checks: QuantLib's Monte Carlo engine within three standard errors, QUADPACK on two contours at the tolerance on the engine's scale, and the integral run to four times its decay point.

Facts:

- `TheShortMarketReachesItsIntegral`
- `TheSmallVarianceMarketReachesItsIntegral`
- `TheDeepCallIsItsIntegralToFourTimesTheDecayPoint`

## HestonGeometricAsianExplosionTests

[`Divergences/HestonGeometricAsianExplosionTests.cs`](../tests/NanoQL.Tests/Divergences/HestonGeometricAsianExplosionTests.cs)

The Heston geometric Asian engine near the moment explosion (fixed). QuantLib integrates its undamped form on (0, 100], so it neither finds where E[Gˢ] explodes nor needs to; the damped contour does, and a test of F's sign at each interval's end admits contours past the explosion and prices the ten-year call at K = 1.5 F_G (vol of vol 1, ρ −0.9) at 0, the call strip shrinks to nothing where s* nears 1 (no end under the cap), Phi(1, 0) is 0/0 for one fixing at 30 years and vol of vol 2 (s* − 1 = 0 in double; ZeroDivisionError), and QuantLib's Phi is 0/0 at κ = 0 (sinh(0)/0, NaN). nanoql takes the explosions by the closed-form first zero of the Riccati factors, runs the out-of-the-money option on its own strip or Lewis's middle strip, takes F_G exact where every fixing time is one, the forms' limits at γ = 0, and floors each price at its lower bound. Checks, at the tolerance on the engine's scale: QuantLib's `AnalyticHestonEngine` at 1e-15 at one fixing; QUADPACK on two strips elsewhere; κ = 0 against κ 1e-12; and spot 0.01 against spot 100 scaled, never below its floor. Markets: r 5%, q 0, v₀ = θ 0.04, κ 0.5 unless given.

Facts:

- `TheClosedFormTestKeepsTheCallItsExplosionAdmits`
- `TheTwoStripsAgreeAtThirtyYearsOfSixFixings`
- `TheMiddleStripPricesTheMoneyWhereSStarNearsOne`
- `TheMiddleStripPricesTheWingWhereSStarNearsOne`
- `TheExactForwardPricesWhereSStarIsOne`
- `BothNarrowStripsGiveWayToTheMiddleOne`
- `KappaZeroIsKappaToZero`
- `ASmallSpotIsSpotOneHundredScaledAndNeverBelowItsFloor`

## HestonGeometricAsianIntrinsicTests

[`Divergences/HestonGeometricAsianIntrinsicTests.cs`](../tests/NanoQL.Tests/Divergences/HestonGeometricAsianIntrinsicTests.cs)

The Heston geometric Asian engine's switch to the intrinsic value (fixed). Without it the quadrature reaches its cap where the integrand oscillates over thousands of periods before it decays (an hour at v₀ 0.0025 at the calls at 120 and 200), and at v₀ = θ = 0 the integrand overflows. nanoql prices the point mass at v₀ = θ = 0, and the discounted intrinsic where a Chernoff bound on the extrinsic value, from Phi at real s up to the moment explosion, is at or below 1e-12 × max(the discounted intrinsic, D·F_G/100); a switch on the strike's distance in standard deviations of ln G would price the one-year call at 50 at its intrinsic, 2.4e-3 off. Checks: the bound's value 0 at the hour, the point mass's own value, and QUADPACK on two contours at the tolerance on the engine's scale. The short-dated market: r 5%, q 0, κ 2, ρ −0.5, six fixings.

Facts:

- `TheHoursDeepCallsAreTheirIntrinsic`
- `ZeroVarianceIsThePointMass`
- `TheYearsDeepCallKeepsItsExtrinsicValue`

## HestonGeometricAsianLowerEndTests

[`Divergences/HestonGeometricAsianLowerEndTests.cs`](../tests/NanoQL.Tests/Divergences/HestonGeometricAsianLowerEndTests.cs)

The Heston geometric Asian engine's integral near ξ = 0 (fixed). QuantLib maps ξ ∈ [−1, 1] to (0.5 + 1e-8 + 0.5ξ)·ξ_max (`analytic_discr_geom_av_price_heston.cpp:49`), so it leaves out [0, 1e-8·ξ_max], where the real integrand is finite and flat, and every price carries −D × the prefactor × 1e-8·ξ_max·f(0⁺)/π: on a one-year market (r 5%, q 0, v₀ = θ 0.04, κ 2, vol of vol 0.5, ρ −0.5, six fixings every 60 days) the call at 200 prices −1.8234540127607532e-05 and the call at 100 6.189850115518784, 2.3e-7 low. nanoql's damped contour is regular at 0 and runs from it. Checks, at the tolerance on the engine's scale: QuantLib's undamped integrand by QUADPACK from 1e-12·ξ_max with the left-out stretch as its rectangle, the deep call not negative; and the starts at 1e-9·ξ_max and 1e-12·ξ_max agreeing on the money.

Facts:

- `TheDeepCallIsTheIntegralFromNearZero`
- `TheMoneyIsTheIntegralFromEitherStart`

## HestonGeometricAsianMarkets

[`Divergences/HestonGeometricAsianMarkets.cs`](../tests/NanoQL.Tests/Divergences/HestonGeometricAsianMarkets.cs)

The markets and the tolerance of the Heston geometric Asian facts: flat curves on Actual/365 Fixed (r 5%, q 0 unless given), the fixing and exercise dates as days from the evaluation date, the strike given; and the tolerance at the engine's own scale, 1e-10 × max(the price, the lower bound D·max(±(F_G − K), 0), D·F_G/100). The references: QuantLib's `AnalyticHestonEngine` at 1e-15 at one fixing, QUADPACK on the route's contour and on a second, and QuantLib's formula in 40 digits. A market whose fixings fall within a day is restated on days by the model's time change: over c times the time, r, q, v₀, κ, θ and the vol of vol each over c and ρ kept, which leaves the average's law and the discount unchanged.

## HestonGeometricAsianSingularityTests

[`Divergences/HestonGeometricAsianSingularityTests.cs`](../tests/NanoQL.Tests/Divergences/HestonGeometricAsianSingularityTests.cs)

The Heston geometric Asian engine at a vanishing square root (fixed). QuantLib's `F` tests |κ² − 2σ²| < 1e-8 for the root √(κ² − 2z₁σ²), dropping z₁, and returns 1 + ½(κ − z₂σ²) without τ (`analytic_discr_geom_av_price_heston.cpp:77-87`): on r 5%, q 0, v₀ = θ 0.04, κ 1, ρ −0.5, twelve fixings every 30 days, the call at 100 prices −7.9379393764773996 at vol of vol 0.70710678 and −7.937939339862728 at √½, where 0.7071068 gives 5.567081837356833. nanoql takes the limit 1 + ½(κ − z₂σ²)τ where |κ² − 2z₁σ²| is below 1e-8, and the forms' limits at γ = 0. Check: QUADPACK on the route's contour either side and at the point, at the tolerance on the engine's scale; QuantLib's own cases, at vol of vol 0.39 to 1, are unchanged (the test suite's).

Facts:

- `TheVanishingRootIsContinuous`

## HestonGeometricAsianSmallParameterTests

[`Divergences/HestonGeometricAsianSmallParameterTests.cs`](../tests/NanoQL.Tests/Divergences/HestonGeometricAsianSmallParameterTests.cs)

The Heston geometric Asian engine at small and long parameters (fixed). QuantLib's Phi adds κ²θT/σ² and subtracts (2κθ/σ²)·Σ ln Fᵢ, and its ω̃ subtracts 2F̃/(σ²F) from κ/σ², terms of order 1/σ² that nearly cancel at small vol of vol (`analytic_discr_geom_av_price_heston.cpp:165`, `:187`, `:197`): on a year of six fixings its formula in double is 1.0e-9 off its own value in 30 digits at vol of vol 1e-3 and 1.5e-7 at 1e-4; its cosh and sinh pass 709 before the cut at 30 years and vol of vol 2, where a cut short of the overflow leaves the price moving with α by up to 12%; QuantLib's undamped integrand never ends on NanoQL's rule at small variance near the forward; and the deterministic variance's (1 − e^(−κt))/κ is 1.1e-7 off at κ 1e-9. nanoql takes the stable form, ω̃ through tanh and term3 − term4 through log1p, a cubic in σ below 1e-4 through the Black-Scholes limit (its variance by expm1) and the stable form at 1e-4, 2e-4 and 3e-4, and the damped contour. Checks, at the tolerance on the engine's scale: QuantLib's formula in 40 digits (with its limits), QUADPACK on the route's contour and on a second, and the price the same across two strips. Markets: r 5%, q 0; a year is 360 days of six fixings every 60, v₀ = θ 0.04, κ 2, ρ −0.5, unless given.

Facts:

- `AtVolOfVolAThousandthTheStableFormIsTheFormula`
- `AtVolOfVolTenToTheMinusSevenTheBridgeIsTheFormula`
- `ADayAtVarianceAMillionthIsPricedOnTheDampedContour`
- `ThirtyYearsAtVolOfVolTwoReachTheCutPastTheOverflow`
- `AtKappaTenToTheMinusNineTheBridgesVarianceIsExact`

## HestonGeometricAsianStrikeScaleTests

[`Divergences/HestonGeometricAsianStrikeScaleTests.cs`](../tests/NanoQL.Tests/Divergences/HestonGeometricAsianStrikeScaleTests.cs)

The Heston geometric Asian engine across strikes and spots (fixed). On the damped contour, a strip chosen by width alone sends a deep out-of-the-money option on the other side through parity from a large in-the-money integral (no end under the cap: puts at K/F 0.01 and 0.001, calls at K/F 30 to 1,000); the tolerance at scale 1 asks a small price at spot 1e4 for 5e-14 of the forward (no end at spot 1e4 and 1e6); the halfway α leaves the integrand on the forward's scale at large spots; a single breakpoint set accepts a panel whose two rules alias the oscillation and agree by chance (1.88 tolerances off at spot 1e4); and distinct dates on one 30/360 time put Phi(1, 0) at its one-date 0/0. nanoql runs the out-of-the-money option on its own strip where that strip is at least 0.5 wide and on the middle strip where it isn't, the other by parity; Lord and Kahl's α; the tolerance at max(the price, the lower bound, D·F_G/100), every spot computing spot 100's contract scaled; two breakpoint sets that must agree, a third on disagreement; F_G exact where every fixing time is one. Checks, at the tolerance on the engine's scale: QuantLib's `AnalyticHestonEngine` at 1e-15 at one fixing, itself checked against QUADPACK on the route's contour at two α, QUADPACK the reference where they part by more than half the tolerance (the K/F 1e5 call, 11.8 tolerances off QuantLib); spot 100's price scaled; QUADPACK elsewhere. Markets: r 5%, q 0, v₀ = θ 0.04 unless given.

Facts:

- `AtSpotAMillionTheTwiceForwardCallIsQuantLibs`
- `AtSpotAMillionTheThriceForwardCallIsQuantLibs`
- `AtSpotTenMillionTheThriceForwardCallIsSpotOneHundredsScaled`
- `TheDeepPutIsQuantLibs`
- `TheThousandTimesForwardCallIsQuantLibs`
- `TheThreeHundredTimesForwardCallIsQuantLibs`
- `TheTailAtSpotTenThousandIsQuadpacks`
- `TwoDatesOnOneThirtyThreeSixtyTimeTakeTheExactForward`
- `TheMoneyIsPricedWhereTheCallStripIsEmpty`
- `TheAliasedCallAtSpotOneHundredIsQuantLibs`
- `TheAliasedCallAtSpotTenThousandIsQuantLibs`
- `TheSmallVarianceCallAtSpotTenThousandIsQuantLibs`
- `TheSmallVarianceCallAtSpotAMillionIsQuantLibs`
- `TheSmallVariancePutAtSpotTenThousandIsQuantLibs`
- `TheSmallVariancePutAtSpotAMillionIsQuantLibs`
- `TheHundredThousandTimesForwardCallIsQuadpacks`

## HestonGreensFctSemiAnalyticalTests

[`Divergences/HestonGreensFctSemiAnalyticalTests.cs`](../tests/NanoQL.Tests/Divergences/HestonGreensFctSemiAnalyticalTests.cs)

`FdmHestonGreensFct`'s semi-analytical algorithm reads `HestonProcess.Pdf`, the fixed density (`Divergences/HestonPdfTests.cs`), where QuantLib 1.44's returns NaN at every point: on the equity Heston process at 0.02 years, the plain and log transformations, QuantLib's Green's function is NaN at each of the nine points below (the harness, `FdmHestonGreensFct::get`); nanoql's is the density at each node, times v under the log transformation, positive and finite.

Facts:

- `TheDensityIsTheProcesssAtEachNode`

## HestonHullWhiteMismatchedTimesTests

[`Divergences/HestonHullWhiteMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/HestonHullWhiteMismatchedTimesTests.cs)

The Heston-Hull-White engines and process on structures dated or counted differently. QuantLib's finite-difference operator reads the dividend curve at the risk-free curve's times (`fdmhestonhullwhiteop.cpp:62`), the hybrid process its dividend forward rate likewise (`hybridhestonhullwhiteprocess.cpp:126`), and the engines take their times from the risk-free curve's date. nanoql's run on the Heston process's model clock, from the earliest reference date of its curves, each read at the instants its times denote, the Hull-White parts on the risk-free curve restated on the clock. QuantLib's check: its engines and process on the market restated on one clock, which nanoql's on the original market reproduce to rounding (QuantLib-Python 1.44 and QuantLib 1.44).

Facts:

- `TheFiniteDifferenceEngineReadsTheDividendCurveAtItsOwnTimes`
- `TheHybridProcessReadsTheDividendRateAtItsOwnTimes`
- `TheAnalyticEnginesRunOnTheEarliestClock`

## HestonMismatchedTimesTests

[`Divergences/HestonMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/HestonMismatchedTimesTests.cs)

The Heston model on curves dated differently. QuantLib's analytic Heston engine runs the model's time from the risk-free curve's reference date (`analytichestonengine.cpp:644-653`), so beside a risk-free curve settling T+2 and a dividend curve dated today the variance accrues only from T+2, where the dividend accrues from today: a one-year put reads 7.816084368797552 against 7.863347278811837 on the market restated from today, the risk-free curve accruing nothing before T+2, and a put expiring before or on the curve's date throws "negative time" or reads 0/0. Its calibration helper and its optimal alpha's date form read the same time. nanoql's engine, helper and alpha run the model's time on the risk-free curve's day counter from the earliest curve date, each curve accruing nothing before its own, and read the risk-free discount by date: QuantLib's engine, helper and alpha on the restated market. Where the dividend curve isn't dated before the risk-free curve, the time is QuantLib's.

Facts:

- `TheModelsTimeRunsFromTheEarliestCurveDate`
- `TheHelpersMaturityRunsFromTheEarlierCurveDate`
- `TheOptimalAlphasDateFormReadsTheEnginesTime`

## HestonModelTimeZeroTests

[`Divergences/HestonModelTimeZeroTests.cs`](../tests/NanoQL.Tests/Divergences/HestonModelTimeZeroTests.cs)

The Heston analytic engines at the model's time 0, an option expiring on the evaluation date with reference-date events included. QuantLib's PDF engine window is zero-wide and prices a call at 90 at 0; the time-dependent engine's Gauss-Laguerre integrates sin(φ(x − ln K))/φ, which doesn't decay, and prices it 10.0687; exponential fitting's average variance is 0/0 and it throws "stdDev (-nan(ind)) must be non-negative"; COS's cumulants at t = 0 are rounding noise, so at the forward its bounds test fails and the series prices 1.7e-7 where the intrinsic value is 0. nanoql prices the forward's discounted intrinsic value at the model's time 0 in the PDF, time-dependent, exponential-fitting, COS and expansion engines, before any cumulant or coefficient. The check: QuantLib's COS and expansions off the forward, bit for bit, and its analytic European engine at zero volatility.

Facts:

- `ThePdfTimeDependentAndFittingEnginesPriceTheIntrinsicValue`
- `OffTheForwardCosAndTheExpansionsAreQuantLibs`
- `AtTheForwardCosPricesTheIntrinsicZero`

## HestonPdfTests

[`Divergences/HestonPdfTests.cs`](../tests/NanoQL.Tests/Divergences/HestonPdfTests.cs)

`HestonProcess.Pdf`, the joint density of the log spot and the variance (fixed). QuantLib's (`hestonprocess.cpp:300-334`) integrates over the integrated variance up to twice a Cornish-Fisher quantile whose skew and kurtosis are finite differences of the characteristic function at a step of 1e-2: they rest on its last bits (one ulp on v moves QuantLib's value at κ 2, a quarter of a year, from −5.27 to 9.31), and the cut-off can fall where the integrand is still a fifth of its peak; and its 128-point Gauss-Laguerre rule stops near u = 500, where at short times the characteristic function hasn't decayed. nanoql inverts the log spot's characteristic function given the variance, from the integrated variance's. The checks: the log spot's marginal, the density integrated over the variance, is the log spot's density from Heston's characteristic function as QuantLib's analytic Heston engine computes it (`AnalyticHestonEngine.ChF`), inverted here, on a process meeting Feller's condition and one far from it, from under three weeks to a year; and where QuantLib's range holds the integrand, QuantLib's value (the C++ harness and QuantLib's own functions; r and q on flat Actual/365 (Fixed) curves from 15 October 2026).

Facts:

- `WhereQuantLibsDensityFailsItIsPositive`
- `TheLogSpotsMarginalIsTheCharacteristicFunctionsDensity`
- `WhereQuantLibsRangeHoldsTheIntegrandItsValueStands`
- `WhereTheVariancesDensityIsZeroTheDensityIsZero`

## HestonSlvFdmMandatoryDatesTests

[`Divergences/HestonSlvFdmMandatoryDatesTests.cs`](../tests/NanoQL.Tests/Divergences/HestonSlvFdmMandatoryDatesTests.cs)

The finite-difference SLV model's mandatory dates. QuantLib appends their times to its exponential grid unsorted and builds the leverage surface on that list (`hestonslvfdmmodel.cpp:325-330`, `:412`): a mandatory date inside the period refuses ("dates must be sorted unique!"), one at the end date ("need strikes for every time step"), and one on the grid's own time ("dates must be sorted unique!"), each on QuantLib-SWIG 1.44. nanoql runs the surface and the loop on the grid's times, sorted and made unique: a date at the end or on the grid's own time is QuantLib's calibration without it, bit for bit, and one inside the period prices the local volatility's options as the calibration without it does, QuantLib's own measure of a calibration.

Facts:

- `AMandatoryDateAtTheEndIsTheCalibrationWithoutIt`
- `AMandatoryDateOnTheGridsOwnTimeIsTheCalibrationWithoutIt`
- `AMandatoryDateInsideThePeriodCalibratesToTheLocalVolatility`

## HestonSlvFdmMixingGreensTests

[`Divergences/HestonSlvFdmMixingGreensTests.cs`](../tests/NanoQL.Tests/Divergences/HestonSlvFdmMixingGreensTests.cs)

The finite-difference SLV model's first density is the Green's function of the process its forward operator evolves, the variance's volatility the mixing factor times σ. QuantLib builds its operator and variance mesher on the mixed σ and its Green's function on the unmixed σ (`hestonslvfdmmodel.cpp:297`, `:346`, `:415`, `:418`), a density the operator's process doesn't have. The independent check is the mixed process's own density (`HestonProcess.Pdf`): against it the Gaussian algorithm on the mixed σ is within 5% in L1 at the first step, on the unmixed σ 76% off. At a mixing factor of 1 the density is QuantLib's; no QuantLib case runs the model with another.

Facts:

- `TheFirstDensityIsTheMixedProcesssGreensFunction`
- `TheMixedSigmasDensityIsTheMixedProcesssAndTheUnmixedIsnt`

## HestonSlvMcLocalVolStepEndTests

[`Divergences/HestonSlvMcLocalVolStepEndTests.cs`](../tests/NanoQL.Tests/Divergences/HestonSlvMcLocalVolStepEndTests.cs)

The Monte Carlo SLV calibration reads the local volatility at each step's end, where QuantLib 1.44 reads it at the step's start (`hestonslvmcmodel.cpp:181`), so a time-dependent local volatility's leverage lags it by a step; QuantLib's master takes the step's end (#2860; its case is `TestSuite.HestonSlvModelTests.TestMonteCarloCalibrationTimeDependentLocalVol`). On that case's market, at the grid's third time, QuantLib 1.44's leverage is the second time's local volatility over √v₀ (QuantLib-SWIG 1.44), nanoql's the third's.

Facts:

- `TheLeverageFollowsTheLocalVolatilityAtTheStepsEnd`

## HestonSlvMismatchedTimesTests

[`Divergences/HestonSlvMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/HestonSlvMismatchedTimesTests.cs)

The stochastic-local volatility models and the Heston Green's function on structures counted differently. QuantLib's models run their time grids from the risk-free curve's reference date and read the dividend curve and the local volatility at the risk-free curve's times (`hestonslvmcmodel.cpp:47-55`, `hestonslvfdmmodel.cpp:300-330`), its Green's function the rates likewise. nanoql's run on `FdmHestonOp.Clock`, each structure read at the instants its times denote, a local volatility on another clock read through an adapter that extrapolates where the local volatility does. QuantLib's check: its models on the market restated on one clock (r 4% on Act/360, q 2%·360/365 on Act/360), which nanoql's on the original market (q 2% on Act/365) reproduce (QuantLib-Python 1.44 and the C++ harness); the finite-difference calibration within 1e-6 relative, its spread across machines: 3.2e-9 under one-ulp moves of its inputs on one machine, and 1.5e-8 on GitHub's windows-2022 runner and 1.6e-8 on macOS, whose C runtimes and processors move its last bits (CI run 41146085965).

Facts:

- `TheMonteCarloModelReadsTheDividendCurveAtItsOwnTimes`
- `TheFiniteDifferenceModelReadsTheDividendCurveAtItsOwnTimes`
- `TheGreensFunctionReadsTheRatesOnTheClock`
- `ALocalVolatilityOnAnotherClockExtrapolatesWhereItDoes`

## HestonTermZeroMembersTests

[`Divergences/HestonTermZeroMembersTests.cs`](../tests/NanoQL.Tests/Divergences/HestonTermZeroMembersTests.cs)

The COS engine's members and the LPP expansions at a term of 0. QuantLib's cumulants at t = 0 (`coshestonengine.cpp:177-286`) leave rounding residue, c₂ −5.1e-19 and c₃ −2.2e-18 here, and its skew c₃/c₂^{3/2} is NaN; its LPP2 and LPP3 coefficients (`hestonexpansionengine.cpp:128-720`) divide 0 by 0 at a term of 0, and their implied volatility is the 1e-8 floor. nanoql's members at t = 0 are their limits, the cumulants, skew and kurtosis 0, and the expansions' coefficients theirs: the first three Forde, Jacquier and Lee's leading order, √v₀, ρσ/(4√v₀) and (1 − 5ρ²/2)σ²/(24 v₀^{3/2}), LPP3's z₃ ρ(8ρ² − 5)σ³/(96 v₀^{5/2}). The check: QuantLib's Forde expansion at a term of 0, whose variance is the square of that leading order, and QuantLib's z₃ in Boost's 250-digit `cpp_bin_float` at a term of 1e-6, next to its limit.

Facts:

- `AtTimeZeroTheCosMembersAreZero`
- `AtATermOfZeroTheExpansionsAreTheirLimits`
- `AtATermOfOneMillionthLpp3sZ3IsTheReferenceValueNextToItsLimit`

## HestonTinyKappaTests

[`Divergences/HestonTinyKappaTests.cs`](../tests/NanoQL.Tests/Divergences/HestonTinyKappaTests.cs)

The Heston density, the PDF engine and exponential fitting at a tiny κ. QuantLib's PDF engine window 8√(θt + (v₀ − θ)(1 − e^{−κt})/κ) (`analyticpdfhestonengine.cpp:46-48`) and exponential fitting's average variance (`exponentialfittinghestonengine.cpp:252`) lose the (v₀ − θ) term once 1 − e^{−κt} rounds to 0, and `HestonRNDCalculator`'s density, which the PDF engine integrates, throws "Interval contains no more machine number" at every x from κ = 1e-16, where its values agree to six digits from 1e-8 to 1e-14. nanoql takes −expm1(−κt) below κt = 1e-2 in both windows, and raises the density's κ to 1e-12/t where κt is below 1e-12, the density moving by about κt relative there, and its inverse distribution's starting guess takes the same κ, where QuantLib's (1 − e^{−κt})/(tκ) is NaN at κ 0 and the inversion throws. The check: the density at that κ, bit for bit, QuantLib's within its complex functions' last bits, QuantLib's analytic engine, and the distribution at the inverse's quantiles.

Facts:

- `AtATinyKappaThePdfEnginePrices`
- `BelowTheFloorTheDensityIsQuantLibsAtTheFloor`
- `ExponentialFittingIsContinuousAsKappaFalls`
- `BelowTheFloorTheInverseDistributionInverts`

## HistogramTests

[`Divergences/HistogramTests.cs`](../tests/NanoQL.Tests/Divergences/HistogramTests.cs)

The histogram's quantiles and given breaks: QuantLib's Freedman-Diaconis rule takes Hyndman and Fan's type 8 quantile, as its comment says, but its interpolation weight drops the method's p/3 term, so ten squares bin into 3 where type 8 gives 2; given breaks that repeat count their bins before QuantLib removes the repeats, so breaks 20, 20 and 50 give 4 bins over 2 breaks (counts 4, 3, 0, 3); and at the upper boundary's probability QuantLib's quantile reads a 0 past its sorted samples, where type 8 clamps to the largest. No public input reaches that last one: QuantLib's two probabilities, ¼ and ¾, meet the boundary at no whole number of samples, so its fact reads the private quantile. QuantLib's values are its own.

Facts:

- `TheFreedmanDiaconisQuantilesAreType8`
- `RepeatedBreaksCountTheirBinsOnce`
- `TheUpperBoundarysQuantileIsTheLargestSample`

## HolderExtensibleCriticalPricesTests

[`Divergences/HolderExtensibleCriticalPricesTests.cs`](../tests/NanoQL.Tests/Divergences/HolderExtensibleCriticalPricesTests.cs)

QuantLib's fix after v1.44 to the holder-extensible engine (#2892, `453e6605c`): the put's correlation sign, the Newton seeds at the strike, the put's derivative, the empty extension region and the call's with dividends (`analyticholderextensibleoptionengine.cpp:87-88`, `:96-197`); 1.44 prices a put −1.063 where its value is 7.428. nanoql takes the fix. The check is master's case, `testHolderExtensibleOptionCriticalPrices`: options whose extension region is empty or unbounded against values from numerical integration at the first expiry, within 1e-4, here until the pin moves.

Facts:

- `TestHolderExtensibleOptionCriticalPrices`

## HolderExtensibleTermStructureTests

[`Divergences/HolderExtensibleTermStructureTests.cs`](../tests/NanoQL.Tests/Divergences/HolderExtensibleTermStructureTests.cs)

The holder-extensible engine on term structures. QuantLib evaluates Longstaff's formula on one zero rate, dividend yield and volatility, read at the first maturity, for both periods (`analyticholderextensibleoptionengine.cpp:279-297`), so on a rising market the call is −3.5% off; nanoql reads the variances to each date, the correlation from their ratio and the discounts by date, its critical prices solving the forward Black values from the first maturity, on West's bivariate normal (with #2892's fixes). The check is the option's exact value at the first maturity, the larger of the vanilla's payoff, the extended option less the premium and nothing, over the lognormal spot, within the critical prices' accuracy, 1e-4 (QuantLib's Newton loops stop at 0.001); on a flat market, QuantLib's call within Drezner's accuracy and nearer the exact value.

Facts:

- `ARisingMarketMeetsTheExactValue`
- `AFallingMarketMeetsTheExactValue`
- `AFlatMarketIsQuantLibsWithinDreznersAccuracy`

## HouseholderOppositeTests

[`Divergences/HouseholderOppositeTests.cs`](../tests/NanoQL.Tests/Divergences/HouseholderOppositeTests.cs)

The Householder reflection of a vector on the side opposite its unit vector e. QuantLib's reflection vector takes its near-parallel branches by ε = |a − (a·e)e|²/(a·e)² alone (`householder.cpp:47-72`): for a = −|a|e, ε is 0 and the zero vector maps a to itself, −|a|e, and near −e the series branch, derived for a ≈ |a|e, maps a to neither, where the reflection maps a to |a|e, as QuantLib's test holds for vectors on e's side (`matrices.cpp:951-1002`). nanoql takes QuantLib's general branch, (a − |a|e)/|a − |a|e|, wherever a·e is negative, which has no cancellation there: QuantLib's general branch is the check.

Facts:

- `AVectorOppositeEReflectsOntoE`

## HouseholderZeroVectorTests

[`Divergences/HouseholderZeroVectorTests.cs`](../tests/NanoQL.Tests/Divergences/HouseholderZeroVectorTests.cs)

The Householder transformation's matrix for the zero vector. QuantLib's divides the vector by its norm (`householder.cpp:32-42`), so the matrix is NaN, where the transformation the zero vector defines, x ↦ x − 2(v·x)v, is the identity. Choi's basket engine reaches it at a zero volatility, where the reflection onto e₁ of a vector already along e₁ is the zero vector, and its integration orders `lround(NaN)` then fail with `bad array new length` (`choibasketengine.cpp:136-167`), while Choi's price is continuous down to a volatility of 1e-16. nanoql's matrix of the zero vector is the identity: QuantLib's own transformation of the zero vector, and Choi's limit, are the checks.

Facts:

- `TheZeroVectorsMatrixIsTheIdentity`
- `ChoiAtAZeroVolatilityPricesItsLimit`

## HullWhiteForwardProcessSmallReversionTests

[`Divergences/HullWhiteForwardProcessSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/HullWhiteForwardProcessSmallReversionTests.cs)

The forward Hull-White process at small mean reversion: QuantLib's `M_T` (`hullwhiteprocess.cpp:134-146`), σ²/a²[(1 − e^(−u)) − ½(e^(−v) − e^(−v−2u))], cuts at QL_EPSILON and above it cancels two terms of order σ²(t − s)/a: on the wheel, on a flat 4% curve with σ = 0.01, M_T(0, 0.5, 10) is 0.005551115122216288 at a = 1e-9, 0 at 1e-12 and 5,551,115,123.125778 at 1e-15, against its limit σ²(t − s)[(T − t) + ½(t − s)] = 4.875e-4, so the half-year expectation from 0.04 is −5,551,115,123.085763 at 1e-15; its drift is NaN at a = 0; and the Monte Carlo cap engine that reads it prices a 5-year cap on Euribor 6M at 3% (notional 1,000,000, on a flat 2.7% discount curve) 13,362.29 ± 129.78 at a = 1e-9, 10,000 antithetic samples, seed 42, on the harness, against the analytic engine's 19,600.69. nanoql's M_T is σ²(t − s)f(u)[(T − t)f(v) + ½e^(−v)(t − s)f(u)], f(x) = (1 − e^(−x))/x, every term positive. QuantLib's check: the limit; QuantLib's closed form at ordinary a, 3.0368938327613056e-4 at a = 0.1, within the tolerance; and the analytic engine for the Monte Carlo one.

Facts:

- `M_TAtSmallReversionIsItsLimit`
- `TheDriftAtZeroReversionIsItsLimit`
- `TheMonteCarloCapAtSmallReversionIsTheAnalyticOnes`

## HullWhiteProcessSmallReversionTests

[`Divergences/HullWhiteProcessSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/HullWhiteProcessSmallReversionTests.cs)

The Hull-White process at small mean reversion: QuantLib's drift's σ²/(2a)(1 − e^(−2at)) (`hullwhiteprocess.cpp:39`) has no a → 0 branch, so at a = 0, which the constructor accepts, the drift is NaN, and below it cancels: on the wheel, on a flat 4% curve with σ = 0.01, the drift at (1, 0.04) is NaN at a = 0 and 9.99982319171977e-05 at a = 1e-12, −1.8e-5 relative off its limit σ²t + f′(t), 1.00000444e-4, and the one-year expectation 0.04004999779003289 at 1e-12 against QuantLib's own a = 0 expectation, 0.04005000000218044 (its variance 0.0001 at both). nanoql writes the term σ²t·(1 − e^(−x))/x, x = 2at. QuantLib's check: the limit, σ²t + f′(t) with f′ QuantLib's difference quotient of the curve's forward over 1e-4 (`:40-44`), and QuantLib's own a = 0 expectation and variance.

Facts:

- `TheDriftAtZeroReversionIsItsLimit`
- `TheProcessAtSmallReversionIsItsLimit`

## HybridHestonHullWhiteCurveTests

[`Divergences/HybridHestonHullWhiteCurveTests.cs`](../tests/NanoQL.Tests/Divergences/HybridHestonHullWhiteCurveTests.cs)

The hybrid Heston-Hull-White process with its forward Hull-White process on another curve. QuantLib builds the hybrid's Hull-White model on the Heston process's risk-free curve but steps the short rate on the given process's curve (`hybridhestonhullwhiteprocess.cpp:37-46`, `:64`, `:137`, `:179`), so its numeraire prices a bond on one curve at a rate taken from the other: a year's put at the money, priced 6.23248 with the forward process on the Heston curve, is 6.11498 with it on a flat 3% curve, 3.6 Monte Carlo errors away (QuantLib 1.44). nanoql rebuilds the forward process on the Heston curve with the given reversion, volatility and forward-measure time, so the hybrid is the one on the Heston curve, and the three engines agree on it.

Facts:

- `AForwardProcessOnAnotherCurveIsRebuiltOnTheHestonCurve`
- `TheThreeEnginesAgreeOnAForwardProcessOnAnotherCurve`

## HybridHestonHullWhiteStepTests

[`Divergences/HybridHestonHullWhiteStepTests.cs`](../tests/NanoQL.Tests/Divergences/HybridHestonHullWhiteStepTests.cs)

The hybrid Heston-Hull-White process's step terms at small reversion. QuantLib's m₂, m₄, m₅, v₁ and v₁₂ (`hybridhestonhullwhiteprocess.cpp:127-165`) combine terms of order one into results of order (a·dt)ᵖ: at a·dt 1e-6 its m₄ is −1.15e-5 for 4.33e-7, and at 5e-13·0.2 its v₁ is −2.5e16, a negative variance the step refuses. nanoql evaluates QuantLib's expressions in double-double below a·dt = 1e-1, in quad-double below 5e-7 and below 1e-12 by the cubic through four quad-double values, the step's length t − s exact there, QuantLib's double and bits from the cut up; the cut and floors measured against the expressions in 250 digits.

Facts:

- `TheStepTermsHoldTheExactValuesAtAndBelowTheCut`
- `TheTermsAreContinuousAcrossTheCut`
- `QuantLibsStepStandsAtTheCut`

## IborNegativeCountTests

[`Divergences/IborNegativeCountTests.cs`](../tests/NanoQL.Tests/Divergences/IborNegativeCountTests.cs)

The Ibor coupons' fixing days are QuantLib's `Natural`, an unsigned type, which can't hold a negative value; nanoql takes an `int` (or `Nullable{T}` for QuantLib's `Null<Natural>()`) and refuses a negative one on entry. From C++ a negative wraps to a large unsigned value, which QuantLib negates back where it advances by it (`floatingratecoupon.cpp:84-85`, `couponpricer.cpp:71-72`, `stubiborcoupon.cpp:214-215`): the wheel's coupon with -1 fixing days looks a day forward for its fixing, and the wrap of 2^31 negates `INT_MIN`, which is undefined.

Facts:

- `NegativeFixingDaysAreRefused`

## ImpliedStdDevQuoteTests

[`Divergences/ImpliedStdDevQuoteTests.cs`](../tests/NanoQL.Tests/Divergences/ImpliedStdDevQuoteTests.cs)

`ImpliedStdDevQuote`'s solve after its inputs move: QuantLib's quote starts each solve after the first from the last one's result (`impliedstddevquote.cpp:34`, `:59`); nanoql's quote is immutable, a new forward or price is a new quote solved from its caller's guess, which may be the last quote's value. QuantLib's call at a 4% forward and strike, priced 2%, solved from 0.15 to 1e-6, gives 1.3489795003921468; its forward moved to 5%, the second solve, from that value, gives 0.83176179858025456, and a fresh quote from 0.15 gives 0.83176159858703613, two roots within the accuracy.

Facts:

- `AMovedForwardIsSolvedFromTheCallersGuess`

## ImpliedTermStructureClockTests

[`Divergences/ImpliedTermStructureClockTests.cs`](../tests/NanoQL.Tests/Divergences/ImpliedTermStructureClockTests.cs)

The implied curve on a day counter whose year fractions don't add. QuantLib reads the original at the implied time plus the reference date's time on the original's clock (`impliedtermstructure.hpp:85-95`), where its own formula states the original's ratio P(d)/P(ref). On the wheel, a zero curve of 1%, 2%, 3% and 3.5% at 15 January 2016 and 1, 3 and 7 years later, implied at 31 March 2016, misses the ratio over every date of four years by up to 1.25e-4 relative on Thirty360 Bond Basis and USA, and 3.7e-3 on Actual/Actual ISMA without a schedule, and gives 30 and 31 March 2020, one time on Thirty360 from a month-end, one discount, 0.8779622361147446, where the ratios are 0.878051925283182 and 0.8779622361147446; on Actual/365 (Fixed) it holds within 2.2e-16. nanoql reads a date by date where QuantLib's sum and the date's own time on the original's clock differ by more than 1e-12 relative, so a date's discount, zero rate and forward hold the ratio, and keeps QuantLib's sum, and bits, elsewhere and for a time; a date before the implied reference date is refused first, with QuantLib's text. QuantLib's check: the original curve's discounts by date.

Facts:

- `ADiscountByDateIsTheOriginalsRatio`
- `AZeroRateByDateIsTheOriginalsRatio`
- `AForwardByDateIsTheOriginalsRatio`
- `ATimeReadIsQuantLibsSum`
- `AnAdditiveDayCounterKeepsQuantLibsBits`
- `ADateBeforeTheReferenceDateIsRefused`

## ImpliedVolTermStructureDateTests

[`Divergences/ImpliedVolTermStructureDateTests.cs`](../tests/NanoQL.Tests/Divergences/ImpliedVolTermStructureDateTests.cs)

The implied Black-volatility structure on a day counter whose year fractions don't add, and before its original's reference date. QuantLib reads its original between the reference date's time on the original's clock and that time plus the query's (`impliedvoltermstructure.hpp:101-109`): on Thirty360, from 15 October 2026 at 20% and implied at 31 January 2027, its variance at 28 February 2027 is 0.0031111111111111114, 3.70% above the original's forward variance by date, 0.0030000000000000009, and at 30 June 2027 0.67% above it; implied at 30 November 2026, its variance at 31 March 2027 is 0.83% below it (QuantLib 1.44). nanoql reads a date by date where the sum misses the date's own time on the original's clock, and keeps QuantLib's bits elsewhere, as on every date of Actual/365 (Fixed). Implied at 15 July 2026, before the original's date, QuantLib's variance at 15 January 2027 is 0.02016438356164384, the original's variance read three months before its own date added; nanoql's accrues none before it, the original's 0.01008219178082192, and at 15 September 2026 none at all, where QuantLib refuses the negative time; on Thirty360 from 31 August 2026, a date the sum misses is read by date from the original's own date.

Facts:

- `OnThirty360ADateReadsTheOriginalsVarianceByDate`
- `OnAnAdditiveDayCounterEveryDateKeepsQuantLibsBits`
- `AReferenceDateBeforeTheOriginalsAccruesNothingBeforeIt`

## IncrementalStatisticsLeadingZeroWeightTests

[`Divergences/IncrementalStatisticsLeadingZeroWeightTests.cs`](../tests/NanoQL.Tests/Divergences/IncrementalStatisticsLeadingZeroWeightTests.cs)

`IncrementalStatistics`' variance after samples of no weight: QuantLib's `variance()` (`incrementalstatistics.cpp:47-54`) reads Boost's iterative weighted variance, σ² ← σ²(S − w)/S + (x − μ)²w/(S − w) from the second sample on (`weighted_variance.hpp:89-97`), whose second term is 0/0 while every earlier sample weighs 0, and x/0 while their weights vanish in the sum, below its rounding, so the variance is NaN or infinite for good. The port resets the variance to 0 there, the first term's value and the second's limit (a skip there would keep the earlier samples' variance as the set's). QuantLib's check: its `Statistics` on the same samples, and its `IncrementalStatistics` with the samples of no weight moved after the first one that carries weight, which Boost's recurrence reads without a 0/0.

Facts:

- `LeadingSamplesOfNoWeightLeaveTheVarianceOfTheOthers`
- `LeadingWeightsTheSumAbsorbsLeaveTheVarianceOfTheOthers`

## IndexFixingsTests

[`Divergences/IndexFixingsTests.cs`](../tests/NanoQL.Tests/Divergences/IndexFixingsTests.cs)

QuantLib's `IndexManager` is a mutable singleton; nanoql's `IndexFixings` is an immutable value in the evaluation context. Three behaviours differ.

Facts:

- `ARefusedBatchChangesNothing`
- `ReadingNeverWrites`
- `QuantLibsNullSentinelIsAValue`
- `FewerValuesThanDatesAreRefused`

## IndexedCashFlowNoBaseDateTests

[`Divergences/IndexedCashFlowNoBaseDateTests.cs`](../tests/NanoQL.Tests/Divergences/IndexedCashFlowNoBaseDateTests.cs)

An indexed cash flow with no base date. QuantLib's `baseFixing()` is `index_->fixing(baseDate())` (`indexedcashflow.hpp:62`), so on `Date()` a zero-inflation index first takes the date's inflation period (`inflationindex.cpp:205`), reading `Date()`'s month before the date tables, which is undefined. nanoql refuses the base fixing, and so the amount, with `NanoQLException` and the text QuantLib's `CPICashFlow` gives a missing base, "no base date specified" (`cpicoupon.cpp:164`).

Facts:

- `TheBaseFixingIsRefused`

## InflationCouponPricerSettingTests

[`Divergences/InflationCouponPricerSettingTests.cs`](../tests/NanoQL.Tests/Divergences/InflationCouponPricerSettingTests.cs)

Setting an inflation pricer on a leg. QuantLib's `setCouponPricer` for inflation pricers casts each cash flow to an inflation coupon and sets the pricer on the coupons in place, one after another (`inflationcouponpricer.cpp:27-34`): a null cash flow casts to null and is skipped, and a coupon refusing the pricer, "pricer given is wrong type", leaves the earlier ones holding it. Both answers are defined. The wheel shows the in-place change: given a Black year-on-year pricer and a leg of a year-on-year coupon then a CPI coupon, it refuses "pricer given is wrong type", and the year-on-year coupon, which refused "pricer not set" before, then prices at 0.03 on `InflationFixture`'s flat curve. The null skip alone is off the wheel, which refuses `None` in a `Leg`. nanoql returns a new leg, all or nothing, as its floating-rate setter does, and refuses a null cash flow, as every nanoql leg does, with `ArgumentException`, "null cash flow in leg", naming the leg.

Facts:

- `ANullCashFlowIsRefused`
- `ACouponRefusingThePricerLeavesNoNewLeg`

## InflationCurveBaseDateTests

[`Divergences/InflationCurveBaseDateTests.cs`](../tests/NanoQL.Tests/Divergences/InflationCurveBaseDateTests.cs)

A piecewise zero-inflation curve whose base date is a function, given a seasonality of more than one cycle. QuantLib's lazy constructor gives its base `Date()` (`piecewisezeroinflationcurve.hpp:84`), and a seasonality of more than one cycle checks itself against that date (`seasonality.cpp:70-76`), whose month reads before the date tables, which is undefined. nanoql's curve passes the base date its function computes, so the check reads the real one. QuantLib's check is its curve with that base date given: on `InflationFixture`'s fixings and five UK RPI zero-coupon swaps from 2.9% to 3.15%, with two equal twelve-month cycles, the wheel's curve based on 1 April 2024 has the nodes 0.027822937658004258 (twice), 0.028915718188625192, 0.029630103472299253, 0.030288922160623725 and 0.03140364761530262; with a second cycle 0.1% higher, it refuses "seasonality is inconsistent with inflation term structure, factors 1.00049 and later factor 1.00149, 1 years later from inflation curve with base date at April 30th, 2024".

Facts:

- `TheSeasonalityChecksTheComputedBaseDate`

## InflationCurveFirstDateTests

[`Divergences/InflationCurveFirstDateTests.cs`](../tests/NanoQL.Tests/Divergences/InflationCurveFirstDateTests.cs)

The year-on-year inflation curve given no rates. QuantLib's constructor passes `rates[0]` to its base before checking the count (`interpolatedyoyinflationcurve.hpp:103-108`), which reads past an empty vector, undefined. nanoql refuses first, with QuantLib's checks of the count in its order: "too few dates" naming `dates`, or the count mismatch naming `rates`. Given no dates, QuantLib's `dates.at(0)` throws `std::out_of_range` first, a convention (`Conventions/InterpolatedInflationCurveCheckTests`).

Facts:

- `NoRatesAreRefusedBeforeTheFirstIsRead`

## InflationLastFixingDateTests

[`Divergences/InflationLastFixingDateTests.cs`](../tests/NanoQL.Tests/Divergences/InflationLastFixingDateTests.cs)

An inflation index's last fixing date after a read of a date the store lacks. QuantLib's `hasHistoricalFixing` reads the history through the index manager's non-const accessor, which inserts a null entry at the date, and `ZeroInflationIndex::lastFixingDate` (`inflationindex.cpp:190-195`) then attributes the last fixing to that entry's period: after probing 1 May 2024, UK RPI's last fixing date moves from 1 April to 1 May 2024, a month with no fixing, measured on the wheel (853 entries for 851 stored). nanoql's reads never write (the fixing store's divergence), so the last fixing date stays 1 April 2024, QuantLib's answer before the read.

Facts:

- `AReadOfAMissingDateDoesntMoveTheLastFixingDate`

## InflationUpstreamFixesTests

[`Divergences/InflationUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/InflationUpstreamFixesTests.cs)

The fix QuantLib's master made to the YoY inflation cap/floor engines after the pin, which nanoql takes: #2877 (`5c93960e2`), the optionlet's time from the volatility's base date read with no lag, `timeFromBase(fixingDate, Period(0, Days))`, as the engine's own variance is. QuantLib 1.44 reads it with the structure's default lag (`inflationcapfloorengines.cpp:85-89`), so a fixing within one lag of the base date has a negative time, its square root NaN, and the engine's `sqrtTime > 0` test leaves the optionlet at its intrinsic value.

Facts:

- `TestTimeValueNearVolatilityBaseDate`

## InitialGuessSizeTests

[`Divergences/InitialGuessSizeTests.cs`](../tests/NanoQL.Tests/Divergences/InitialGuessSizeTests.cs)

A global bootstrap's initial-guess function that gives fewer values than the curve has nodes after the first is refused when the curve is built, with `NanoQLException` "initial guess has n values but the curve has m pillars", QuantLib-Python's wording. QuantLib's optimizer starts from the guess, and the bootstrap writes each node from the optimizer's argument, whose size is the guess's (`globalbootstrap.hpp:394, 442-448`), reading past its end, a result C++ leaves undefined; the wheel's guess function refuses a guess of any other size than the nodes'. A longer guess is QuantLib's defined path where the traits' transforms don't read a node's time (the discount traits', and the zero-yield and forward-rate traits', which have none), kept: its extra values are variables no error term reads. The simple-zero traits' transforms read each node's time (`bootstraptraits.hpp:408-416`), and for a longer guess QuantLib reads past its times, which nanoql refuses, `NanoQLException` "initial guess longer than the curve's m pillars: the simple-zero transform reads node k".

Facts:

- `AGuessShorterThanTheCurvesPillarsIsRefused`
- `ALongerGuessUnderTheSimpleZeroTraitsIsRefused`

## InnerValueProxyTests

[`Divergences/InnerValueProxyTests.cs`](../tests/NanoQL.Tests/Divergences/InnerValueProxyTests.cs)

The finite-difference inner value's curve: QuantLib's `FdmAffineModelSwapInnerValue` links relinkable handles, at each point of the mesh, to a new `FdmAffineModelTermStructure` at the point's state, and its swap's floating coupons, cloned onto them, forecast on it (`fdmaffinemodelswapinnervalue.hpp`). nanoql has no relinkable handles: the inner value holds, for each exercise date, a private proxy curve pair whose state it sets at each point, its swap rebuilt on the index cloned onto the forwarding proxy. QuantLib's route, a fresh immutable `FdmAffineModelTermStructure` pair and a swap rebuilt on it for every point, gives the same value bit for bit, at five points of each of three exercise dates, on Hull-White discounting on a flat 2.7% curve and forwarding on a flat 3.1% one.

Facts:

- `TheProxyIsAFreshCurveAtEveryPoint`

## IntegralEngineZeroVarianceTests

[`Divergences/IntegralEngineZeroVarianceTests.cs`](../tests/NanoQL.Tests/Divergences/IntegralEngineZeroVarianceTests.cs)

The integral engine at zero variance. QuantLib integrates the payoff over ±10√variance against a density scaled by 1/√(2π·variance) (`integralengine.cpp:78-83`), both 0 or ∞ at zero variance, so its value is NaN (QuantLib-Python 1.44). nanoql takes the limit, the discounted payoff at the forward. QuantLib's check: its analytic European engine at the same zero volatility.

Facts:

- `AtZeroVarianceTheValueIsTheDiscountedPayoffAtTheForward`

## IntegralHestonVarianceOptionTests

[`Divergences/IntegralHestonVarianceOptionTests.cs`](../tests/NanoQL.Tests/Divergences/IntegralHestonVarianceOptionTests.cs)

The integral Heston variance-option engine's put, other payoffs, start date and dividend curve. QuantLib sums any payoff but a call against the integrated variance's density on 4,096 points of spacing √(2π)/64, about one standard deviation of it, so its put case prices 0.0466796 (QuantLib's `testIntegralHeston`); nanoql prices a put by parity on the call's integral, 0.0465915, and refuses other payoffs, which QuantLib's grid prices 13% to 24% off. Checks: an independent price, the integrated CIR variance's characteristic function (the CIR bond formula at λ = −iu, averaged over the start variance's non-central χ² law) inverted by Gil-Pelaez's distribution function integrated over [0, K], which gives QuantLib's call case to 1e-10. QuantLib's engine measures the variance from the evaluation date whatever the option's start, so a start 180 days ahead prices as one starting today, 0.9104618670 (QuantLib 1.44); nanoql prices a forward start on the variance integrated from its start, each integrand's expectation over the start variance's non-central χ² law the CIR transform, and refuses a seasoned option. QuantLib refuses a process whose dividend handle isn't empty; nanoql ignores the dividend curve, which the integrated variance's law doesn't read. Checks: a Monte Carlo of the integrated variance (the exact mean-reverting step with a truncated diffusion and trapezoid integration, four steps a day, 100,000 antithetic pairs on QuantLib's Mersenne twister) within three standard errors, for a call (the one-dimensional integral) and a put (the two-dimensional one); the start tending to the evaluation date.

Facts:

- `AForwardStartIsTheMonteCarloOfItsIntegratedVariance`
- `APutIsItsIndependentPrice`
- `OtherPayoffsAreRefused`
- `AStartTendingToTheEvaluationDateTendsToQuantLibsValue`
- `TheDividendCurveIsntRead`

## InterestRateUpstreamFixesTests

[`Divergences/InterestRateUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/InterestRateUpstreamFixesTests.cs)

The fix QuantLib's master made to `InterestRate.ImpliedRate(double, DayCounter, Compounding, Frequency, double)` after the pin, which nanoql takes: #2898 (`588c2dd5b`) refuses the mixed compoundings, `Compounding.SimpleThenCompounded` and `Compounding.CompoundedThenSimple`, before its unit compound factor's branch. QuantLib 1.44 refuses them only in its other branch (`interestrate.cpp:127-129`), so at a compound factor of 1 it returns a zero rate on a rule it can't apply to any other factor. #2898's null-rate checks have no counterpart: a nanoql rate is never null, so its two null-rate cases aren't ported.

Facts:

- `TestImpliedRateRejectsMixedCompoundingAtUnity`

## InterestRateVolSurfaceOptionDateTests

[`Divergences/InterestRateVolSurfaceOptionDateTests.cs`](../tests/NanoQL.Tests/Divergences/InterestRateVolSurfaceOptionDateTests.cs)

The interest-rate volatility surface's option dates. QuantLib's `InterestRateVolSurface::optionDateFromTenor`, the index's fixing date, hides its base's non-virtual member, so `atmVol(Period)` and `smileSection(Period)`, defined on the bases, date the option on the calendar while the surface's own option dates and volatility spreads use the index's (`interestratevolsurface.hpp:69`, `blackatmvolcurve.cpp:40-44`); nanoql dates every tenor on the surface by the index. Checks: a tenor's at-the-money volatility and smile section are those at the index's date; at one month, where the calendar's date (15 June 2026) and the index's (17 June) differ, the calendar date's volatility differs.

Facts:

- `ATenorIsDatedByTheIndex`
- `WhereTheCalendarsDateDiffersTheIndexsStands`

## InterpolatedSmileSectionCountTests

[`Divergences/InterpolatedSmileSectionCountTests.cs`](../tests/NanoQL.Tests/Divergences/InterpolatedSmileSectionCountTests.cs)

An interpolated smile section given more strikes than standard deviations. QuantLib sizes its volatilities by the standard deviations and interpolates over the strikes' count of them (`interpolatedsmilesection.hpp:117, 124-125`), reading past the volatilities: over strikes 1%, 2%, 3% and 4% with standard deviations 0.4 and 0.3, the wheel's linear section gives 0.14999999999999994 at 2.5% and 0.0 at 3.5%, memory past the vector. nanoql refuses more strikes than standard deviations with `ArgumentException`, and keeps QuantLib's behaviour for fewer, ignoring the extra standard deviations.

Facts:

- `MoreStrikesThanStandardDeviationsAreRefused`

## Interpolation2DSizeTests

[`Divergences/Interpolation2DSizeTests.cs`](../tests/NanoQL.Tests/Divergences/Interpolation2DSizeTests.cs)

A bilinear or bicubic-spline interpolation over a matrix smaller than its grid. QuantLib checks no matrix against the grid (`interpolation2d.hpp:72-83`): its bilinear value reads `zData_[j+1][i+1]` for the cell holding the point (`bilinearinterpolation.hpp:50-53`), and its bicubic spline builds a row spline over each row's first x values and reads a row per y value (`bicubicsplineinterpolation.hpp:58-79`), past the matrix's end for a matrix with fewer rows than y values or fewer columns than x values, undefined: the wheel's bicubic spline over a 2 by 2 matrix on a 3 by 3 grid gives 3.337890625 at (0.25, 0.25). nanoql refuses such a matrix when the interpolation is built, with `ArgumentException` naming the matrix's parameter (`zData`, and the factories' `z`) and QuantLib's own texts for a mismatched matrix (`kernelinterpolation2d.hpp:70-73`). A larger matrix, which QuantLib reads in its first rows and columns, is kept; the bicubic spline's derivatives in x, which QuantLib computes at an x value per matrix column (`bicubicsplineinterpolation.hpp:81-108, 137-149`), reading the x values past their end for a wider matrix, read its first columns, an x value each.

Facts:

- `AMatrixSmallerThanTheGridIsRefused`

## InterpolationUpstreamFixesTests

[`Divergences/InterpolationUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/InterpolationUpstreamFixesTests.cs)

The fix QuantLib's master made to Kruger's cubic after the pin, which nanoql takes: #2952 (`32de8d923`), a zero derivative at a node where the adjacent slopes' product is at most 0, as the harmonic scheme has it. QuantLib 1.44 tests `S_[i-1]*S_[i] < 0.0` (`cubicinterpolation.hpp:634`), so where a −0 slope meets a +0 one the harmonic mean 2/(1/S₀ + 1/S₁) is 2/(−∞ + ∞), NaN, and the interpolation is NaN on both adjacent intervals.

Facts:

- `TestKrugerZeroSlope`

## IsdaImpliedWeekendTests

[`Divergences/IsdaImpliedWeekendTests.cs`](../tests/NanoQL.Tests/Divergences/IsdaImpliedWeekendTests.cs)

The ISDA model's implied hazard rate and conventional spread on a weekend. QuantLib's flat curve is `FlatHazardRate(0, WeekendsOnly(), …)` (`creditdefaultswap.cpp:352, 393`), which rolls a weekend to Monday, and its own ISDA engine then refuses it: on Saturday 15 March 2025, for `MakeCreditDefaultSwap(5Y, 1%)` on a discount curve flat at 3%, Actual/365 (Fixed), at a recovery of 40%, both throw "probability term structure reference date (March 17th, 2025 should be evaluation date (March 15th, 2025)" (QuantLib's defect). nanoql's ISDA model dates its curve on the evaluation date. QuantLib's ISDA engine on QuantLib's `FlatHazardRate` dated there, solved by QuantLib's `Brent` on the wheel, gives the hazard rate 0.016833960395149727 for a zero target, and the fair spread at the conventional root, 0.009999999999972463; on the Friday before, the same oracle gives QuantLib's own 0.01683399613622196 and 0.009999999999972453. The mid-point model keeps QuantLib's curve and its weekend answers, 0.016853086104458855 and 0.009999999999985819.

Facts:

- `TheImpliedHazardRateIsSolvedOnTheEvaluationDate`
- `TheConventionalSpreadIsTheFairSpreadAtTheRoot`

## IsdaProtectionLegMaturityTests

[`Divergences/IsdaProtectionLegMaturityTests.cs`](../tests/NanoQL.Tests/Divergences/IsdaProtectionLegMaturityTests.cs)

The ISDA engine's protection leg on curves that end before the maturity. QuantLib integrates over the union of the two curves' nodes, adds the maturity only when the union is empty (`isdacdsengine.cpp:150-156`), and stops at the maturity only on meeting a node past it (`:167-172`), so it drops the protection from the curves' last node to the maturity (QuantLib's defect). nanoql adds the maturity to the nodes when their last one is before it. On 15 March 2024, a 10-year CDS buying protection on 10,000,000 at 1% from the next day (quarterly on WeekendsOnly under the CDS rule, Actual/360 with the last period inclusive) on a hazard rate flat at 2% and a discount curve continuously flat at 3%, Actual/365 (Fixed): on a log-linear discount curve with yearly nodes to 5 years, nanoql's default leg is 945,721.5990409853, QuantLib's own on the flat curve and on nodes to 15 years, where QuantLib's on nodes to 5 years is 531,134.1471852273; the coupon leg is -820,172.4119002547 on every curve.

Facts:

- `TheProtectionRunsToTheMaturity`

## IsotropicRandomWalkTests

[`Divergences/IsotropicRandomWalkTests.cs`](../tests/NanoQL.Tests/Divergences/IsotropicRandomWalkTests.cs)

The isotropic random walk's direction from three dimensions (fixed). QuantLib's hyperspherical angles are each uniform in [0, π], where a uniform direction needs densities ∝ sin^(d−k−1)φ, so its first coordinate's second moment is ½ in any dimension, against 1/d. nanoql's direction is Muller's, d normals scaled to unit length (IMSL's RNSPH above four dimensions). On a unit radius at a fixed seed its moments are E[xᵢ²] = 1/d and E[xᵢxⱼ] = 0 within three standard errors, d 3 to 6, and its first coordinate's law, the density ∝ (1 − x²)^((d−3)/2) on [−1, 1], passes a binned χ² test at the 0.1% level; in one and two dimensions the walk is QuantLib's, bit for bit (the harness's vectors).

Facts:

- `TheMomentsAreTheUniformDirections`
- `TheFirstCoordinatesLawPassesAChiSquaredTest`

## IsraelRangeEndTests

[`Divergences/IsraelRangeEndTests.cs`](../tests/NanoQL.Tests/Divergences/IsraelRangeEndTests.cs)

Israel's three markets at the ends of Date's range. Their rules read a neighbouring day by date arithmetic, up to 21 days back (Simchat Torah through Sukkot, Yom Kippur and the New Year) and one day ahead (the eves) (`israel.cpp:374-383`), so QuantLib's `isBusinessDay` throws "Date's serial number (...) outside allowed range" on the dates of January 1st to 21st, 1901, that no earlier rule settles (15 of them for the Tel Aviv stock exchange and SHIR, 14 for Telbor), and on December 31st, 2199. nanoql reads the neighbour by serial number, in no table there (the tables hold 2000 to 2050), so the rule reads false and the answer is QuantLib's weekend and fixed-date rules, as on the dates just inside the range.

Facts:

- `TheFirstThreeWeeksOf1901Answer`
- `TheLastDayOf2199Answers`

## IssuerEventsOnOneDateTests

[`Divergences/IssuerEventsOnOneDateTests.cs`](../tests/NanoQL.Tests/Divergences/IssuerEventsOnOneDateTests.cs)

An issuer keeps every event, two on one date in the order given: QuantLib's event set orders by date alone (`issuer.hpp:36-38`), so it keeps the first event of a date and drops the second, where its own documentation lets several events take place on one date. QuantLib's values are recorded in the test; the check is QuantLib's issuer with each event alone.

Facts:

- `BothEventsOnADateCount`

## JamshidianBracketTests

[`Divergences/JamshidianBracketTests.cs`](../tests/NanoQL.Tests/Divergences/JamshidianBracketTests.cs)

The Gaussian1d Jamshidian engine's bracket: QuantLib solves for y* on the fixed bracket [−8, 8] (`gaussian1djamshidianswaptionengine.cpp:142-147`) and refuses short-dated swaptions away from the money: on the wheel, on a GSR model (a = 0.02, σ = 0.01) on a flat 3% curve, a 1-day payer on a 5-year swap at 1% throws "root not bracketed: f[-8,8] -> [7.575587e-02,1.111667e-01]", as does a 1-day receiver at 6% and a 1-month receiver at 6% ("[-2.604803e-01,-2.535408e-02]"), which Hull-White's Jamshidian engine on the equal constant-parameter model prices at 0.09363555834330271, 0.13506769382915945 and 0.13466680676149342. nanoql's bracket is QuantLib's first and is doubled only where it doesn't bracket the root, so where QuantLib brackets, its solver's call and path are QuantLib's. QuantLib's checks: Hull-White's Jamshidian engine and QuantLib's own values where it prices.

Facts:

- `ShortDatedSwaptionsAwayFromTheMoneyArePriced`
- `WhereQuantLibBracketsItsPathStands`

## JamshidianNoFixedCouponTests

[`Divergences/JamshidianNoFixedCouponTests.cs`](../tests/NanoQL.Tests/Divergences/JamshidianNoFixedCouponTests.cs)

The Gaussian1d Jamshidian engine with no fixed coupon after the exercise: QuantLib reads `fixedResetDates[startIndex]` past the vector's end as the coupon bond's value date (`gaussian1djamshidianswaptionengine.cpp:84-87`, `:104`, `:107`, `:139`, `:159`, `:163`), undefined behaviour: on the wheel, a 2-year payer at 3% (annual fixed and 6-month floating coupons) on a GSR model (a = 0.02, σ = 0.01) on a flat 3% curve, exercised 13 months out, throws "negative time (-3.73411e+06) given", and a different refusal each run. nanoql takes the first floating reset on or after the exercise as the value date at every one of those reads, the start of the coupons QuantLib's Gaussian1d swaption engine exercises into, which values the swaption at 0.014428105830926762 (13 months) and 0.014437260795231834 (18 months); with no coupon after the exercise (20 months), the swaption is worth 0, that engine's convention. QuantLib's check: its Gaussian1d swaption engine on the same swaption and model, to the two engines' integration error.

Facts:

- `ASwaptionWithNoFixedCouponAfterItsExerciseIsPricedOnItsFloatingResets`
- `WithNoCouponAfterItsExerciseTheSwaptionIsWorthNothing`

## JumpDiffusionReferenceDateTests

[`Divergences/JumpDiffusionReferenceDateTests.cs`](../tests/NanoQL.Tests/Divergences/JumpDiffusionReferenceDateTests.cs)

The jump-diffusion engine on structures dated differently. QuantLib rebuilds each term's flat curve and constant volatility on the risk-free curve's reference date with the time measured from the volatility's (`jumpdiffusionengine.cpp:62-67`, `:113-114`), so with no jumps it isn't the Black-Scholes price on a curve settling T+2. nanoql builds both on the volatility's reference date; its rho is then per unit of the risk-free curve's own rate over its own time, and theta's risk-free term runs over that time, as the analytic European engine's. QuantLib's check: with no jumps, its analytic European engine on the same market, every output but the theta, which is the analytic engine's (`Divergences/AnalyticThetaMismatchedTimesTests.cs`) (QuantLib-Python 1.44, QuantLib's jump-diffusion engine emulated by its rebuilt structures). Where the structures share their dates and counts, QuantLib's bits (the engine's harness vectors).

Facts:

- `WithNoJumpsARiskFreeCurveSettlingLaterGivesTheAnalyticResults`
- `WithNoJumpsAVolatilitySettlingLaterGivesTheAnalyticValue`

## JumpDiffusionZeroVolatilityTests

[`Divergences/JumpDiffusionZeroVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/JumpDiffusionZeroVolatilityTests.cs)

The jump-diffusion engine's vega and theta at zero diffusive volatility. For the no-jump term v₀ = 0, so its vega weight √(variance/t)/v₀ and its theta's jump term i·jumpSquareVol/(2v₀t²) are 0/0 (`jumpdiffusionengine.cpp:110`, `:123-127`): QuantLib's vega and theta are NaN at σ = 0 (the C++ harness). nanoql takes their limits, 1 and 0, the values they take at any positive volatility. QuantLib's check: its own Greeks at σ = 1e-12, the sum taken to 1e-12, and the limit they approach from σ = 1e-8.

Facts:

- `AtZeroVolatilityTheVegaAndThetaAreTheirLimits`

## KInterpolatedYoYVolatilityTests

[`Divergences/KInterpolatedYoYVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/KInterpolatedYoYVolatilityTests.cs)

The k-interpolated year-on-year surface reads its stripped curves at its own time (fixed). QuantLib's `volatilityImpl(Time)` turns the time back into a date (whole years and the fraction's days at 365 a year) and reads each curve at that date through the curve's own lag, so the curves are read a lag early (`kinterpolatedyoyoptionletvolatilitysurface.hpp`). On QuantLib 1.44 (through QuantLib-Python: test-suite/inflationvolatility.cpp's euro surface observed flat, stripped at a slope of −0.5) the surface at 1 March 2009 and the strike of −1% is 0.011930033082068286, where its own strip, read through QuantLib's `Dslice`, is 0.011319592966991554. nanoql's surface at a date and a strike is that strike's stripped curve at the date, both through the public date route, wherever the two observe the index alike and the surface is on the stripper's clock, the price surface's reference date and day counter (settlement days 0, as here); on another clock it reads the curves early or late by the clocks' difference, up to 2.7e-5 at 2 settlement days and 4.2e-5 on Actual/360 on this market, within QuantLib's lag error. QuantLib's case reads `Dslice`, which is QuantLib's.

Facts:

- `TheSurfaceAtAStrikeIsItsStrippedCurve`

## KahaleAtmLevelTests

[`Divergences/KahaleAtmLevelTests.cs`](../tests/NanoQL.Tests/Divergences/KahaleAtmLevelTests.cs)

Kahale's at-the-money level on a shifted source: QuantLib's section fits on the shifted forward and strikes and returns that forward as its level (`kahalesmilesection.hpp:142`, `.cpp:59`), so every member of the base that reads the level, the vega and the converted volatilities, reads the forward plus the shift. On the wheel, over a SABR smile at a forward of 1% shifted by 2% with an exercise time of 5, its level is 0.03, its vega at 0.5% 2.1203568910002753e-05 where the source's is 2.0058237006534898e-04, and its normal volatility at 0.5% 0.00972286988319294 where the source's is 0.0036525005165526547, at a strike where the section keeps the source's prices and volatilities. nanoql's level is the one given or the source's. QuantLib's check: the source section's own members wherever the section keeps its prices; at a shift of 0 the two levels are QuantLib's.

Facts:

- `TheLevelIsTheSourcesAndTheMembersReadingItTheSources`

## KahaleEraseTests

[`Divergences/KahaleEraseTests.cs`](../tests/NanoQL.Tests/Divergences/KahaleEraseTests.cs)

Kahale's failed interpolation: a segment that fails to fit erases the grid's next point and, in QuantLib, the function slot at the grid index after the segment's (`kahalesmilesection.cpp:163`), past the slots' end once the core's left index is 2 or more, undefined behaviour: on a harness section whose smile is 0.2 − k + 10k² at a level of 3% over a year, interpolated on the default grid, the core's left index is 7 and the C++ harness exits. nanoql erases the unfitted slot after the segment's own. QuantLib's check: its own section on the grid cut to start at the core, left index 1, where its erase is in range and every slot either erase removes is unfitted: from the core to the segment where nanoql's fit also refuses a cancelled function (KahaleSegmentFitTests), the prices agree bit for bit.

Facts:

- `ALeftIndexOfTwoOrMoreErasesTheSegmentsNextSlot`

## KahaleLeftWingTests

[`Divergences/KahaleLeftWingTests.cs`](../tests/NanoQL.Tests/Divergences/KahaleLeftWingTests.cs)

Kahale's left wing on a shifted source: after fitting the wing, QuantLib's section checks a digital at (k₁ − shift)/2 (`kahalesmilesection.cpp:109-110`), k₁ the wing's shifted end, the midpoint of [0, k₁ − shift] where the wing spans [−shift, k₁ − shift]; once the core's left strike is negative unshifted the point lies in the core, the check fails and the core moves right, the wing replacing the source's prices below it. nanoql checks at the wing's midpoint, (k₁ − 2·shift)/2, QuantLib's expression at a shift of 0. QuantLib's check: its own section on the smile restated unshifted (the forward and the strikes plus the shift, no shift), whose prices at K + shift a shifted section's at K equal. On the wheel, over a SABR smile at a forward of 1% shifted by 3%, QuantLib's shifted section's call at -1% is 0.020066641655499824 where its section restated unshifted gives 0.02015567637929827 at 2%; with interpolation, on a SABR smile at an exercise time of 1 that QuantLib's interpolation fits soundly once restated unshifted, its shifted section's call at 0% is 0.010484248860552634 against 0.015632415044761726 at 3%.

Facts:

- `AShiftedSectionIsItsUnshiftedRestatement`
- `AShiftedInterpolatedSectionIsItsUnshiftedRestatement`

## KahaleSegmentFitTests

[`Divergences/KahaleSegmentFitTests.cs`](../tests/NanoQL.Tests/Divergences/KahaleSegmentFitTests.cs)

Kahale's interpolated segments: QuantLib's fit of a segment's function accepts any root of its solver (`kahalesmilesection.cpp:150-173`), even where the function's forward runs to 10¹⁴ and more, whose Black call and constant then cancel, so its prices there are the cancellation's. On the wheel, over an unshifted SABR smile at a forward of 4% with an exercise time of 5 (α 0.02, β 0.5, ν 0.3, ρ −0.2), QuantLib's interpolated section prices the call struck at 0.4%, a grid point, at 0.0361328125 where the source gives 0.0360042, and seven strikes from 0.46% to 1.46% below their intrinsic value, by up to 8.1e-4. nanoql solves such a segment again, and evaluates it, anchored at its left end's price, the Black call's change taken through complementary normal integrals, so no term cancels; a segment whose rounding is within 1e-8 of its ends' prices, relative, is QuantLib's, solved and evaluated as QuantLib's. QuantLib's checks: the source's prices at the core's grid points, which every segment reproduces, and QuantLib's own bits where its segments are sound, at any scale.

Facts:

- `ACancellingSegmentIsFittedWithoutTheCancellation`
- `ASoundSegmentIsQuantLibsAtAnyScale`

## KahaleShiftedPutTests

[`Divergences/KahaleShiftedPutTests.cs`](../tests/NanoQL.Tests/Divergences/KahaleShiftedPutTests.cs)

Kahale's shifted puts: where its own functions price a strike below the forward, QuantLib's section implies the volatility from the put on the unshifted strike against the shifted forward (`kahalesmilesection.cpp:252-256`), so on a shifted source every such strike's volatility reads 0. On the wheel, over a SABR smile at a forward of 1% shifted by 2% with an exercise time of 5, the volatility at -1% is 0.0 plain and interpolated, and with interpolation at 0.5% too, where the section's own call prices are 0.0200181 and 0.0063525. nanoql implies it from the put on the shifted strike. QuantLib's check: the section's own call prices, which Black's formula on the shifted strike and forward at nanoql's volatility gives back, through put-call parity, within the volatility solve's accuracy.

Facts:

- `ThePutBranchsVolatilityGivesTheSectionsOwnPrices`

## KlugeExtOUDiffusionTests

[`Divergences/KlugeExtOUDiffusionTests.cs`](../tests/NanoQL.Tests/Divergences/KlugeExtOUDiffusionTests.cs)

Documented divergence of `KlugeExtOUProcess.Diffusion`: QuantLib writes the gas row at row `size()` and column `factors()`, one past each end of its `size() × factors()` matrix, so its gas row is 0 (and the writes corrupt the heap); here the gas row is the last, ρσ_u in the first column and √(1 − ρ²)σ_u in the last, so the diffusion's square is the covariance of the power and gas Brownian motions.

Facts:

- `TheDiffusionsSquareIsTheCovariance`

## KronrodAdaptiveDepthTests

[`Divergences/KronrodAdaptiveDepthTests.cs`](../tests/NanoQL.Tests/Divergences/KronrodAdaptiveDepthTests.cs)

The adaptive Gauss-Kronrod rule's recursion: QuantLib halves the interval by recursion bounded by its evaluations only (`kronrodintegral.cpp:386-439`), so at a tolerance it can't reach it overflows a default stack before the bound refuses: integrating e^(−x²) cos 3x and √|x − 1.3| over [0, 4], QuantLib's C++ dies of a stack overflow at 1e-14 on its default stack (exit −1073741571), which in .NET ends the process uncaught. nanoql iterates over an explicit stack in QuantLib's order. QuantLib's check: its own values, counts and refusals, on a thread with a 1 GB stack: at tolerances 1e-6 to 1e-13 the values and counts below, and at 1e-14 and 1e-15 "maximum number of function evaluations exceeded".

Facts:

- `QuantLibsValuesAtTolerancesItReaches`
- `TolerancesItCantReachAreRefused`

## LBFGSBTests

[`Divergences/LBFGSBTests.cs`](../tests/NanoQL.Tests/Divergences/LBFGSBTests.cs)

L-BFGS-B's gradient norm on its relative function-reduction stop: QuantLib's minimization breaks there after moving to the new point, so the problem keeps the projected gradient's norm of the iterate before (`lbfgsb.cpp:528`, `:638-646`); nanoql's reports the norm at the point it returns. QuantLib's values are its own class's.

Facts:

- `TheReportedGradientNormIsThePointReturnedOnes`

## LaggedYoYRateBranchTests

[`Divergences/LaggedYoYRateBranchTests.cs`](../tests/NanoQL.Tests/Divergences/LaggedYoYRateBranchTests.cs)

`CPI.LaggedYoYRate` on a ratio index under linear interpolation. QuantLib interpolates the underlying's fixings and takes their ratio only when `!index->needsForecast(date)`, tested on the unlagged date (`inflationindex.cpp:83`), though the fixings that branch reads are those of the lagged date's period and the next; otherwise it interpolates the ratios. So a fixing it never reads changes the rate it returns: with UK RPI fixings to June 2025 and the evaluation date 20 August 2025, the rate for 15 July 2025 with a three-month lag is 0.035461466318187446 on the wheel, and 0.03545866305332557 once July's fixing is stored, a change of -2.8032648618780964e-06 (QuantLib's defect). nanoql asks whether the latest fixing the ratio branch reads needs a forecast (the next period's first day, or the lagged period's own when the date starts its period), so the rate is 0.03545866305332557 with or without July's fixing: QuantLib's own answer with that fixing added, and its interpolate-then-divide value from the same April and May fixings. The other face: where the lag is shorter than the index's period, the unlagged date's fixing can be published while the next period's, which the ratio branch reads, isn't. QuantLib then takes the ratio branch and forecasts the underlying index, refusing without its zero curve, against its own comment that "forecasts need to be done through the YoY forecast curve, and not the underlying index" (`:84-90`): with UK RPI fixings to April 2024, the evaluation date 15 June 2024 and a YoY curve flat at 2%, the rate for 26 April 2024 with no lag throws "empty Handle cannot be dereferenced" on the wheel. nanoql interpolates the ratios through the YoY curve, 0.02218077895395275; on 1 April, which starts its period, both give 0.033084673723716485.

Facts:

- `AFixingTheRatioBranchDoesntReadChangesNothing`
- `ALagShorterThanThePeriodForecastsThroughTheYoYCurve`

## LagrangeZeroNodeTests

[`Divergences/LagrangeZeroNodeTests.cs`](../tests/NanoQL.Tests/Divergences/LagrangeZeroNodeTests.cs)

The Lagrange interpolation at its nodes. QuantLib finds the node by a binary search for the first x not below x − 10ε|x| and returns its y when that x is less than 10ε|x| above (`lagrangeinterpolation.hpp:114-120`): at x = 0 the band is empty, so a node at zero falls through to the barycentric sum, which divides by zero (NaN); and on descending x values the search never lands on a node, so every node's value is NaN (QuantLib-Python 1.44). nanoql scans the nodes in order with the band closed. QuantLib's check: the node's y, and its own values beside the nodes.

Facts:

- `ANodeAtZeroGivesItsValue`
- `DescendingNodesGiveTheirValues`

## LastFixingQuoteTests

[`Divergences/LastFixingQuoteTests.cs`](../tests/NanoQL.Tests/Divergences/LastFixingQuoteTests.cs)

`LastFixingQuote`'s reference date on a store holding later fixings: QuantLib's is the earlier of the store's last date and the evaluation date (`lastfixingquote.cpp:40-43`), the evaluation date itself on a store holding later fixings, as a backtest on one full history does, whose fixing is refused on a non-fixing day or forecast where it is missing. nanoql's is the last stored date on or before the evaluation date: Euribor 6M fixed through 30 April 2021, on Saturday 13 March 2021 and Good Friday 2 April 2021, where QuantLib refuses the evaluation date's fixing, nanoql's quote is the last fixing available, on the 12th and the 1st, and is QuantLib's own quote on the store cut at the evaluation date.

Facts:

- `TheReferenceDateIsTheLastStoredFixingOnOrBeforeTheEvaluationDate`

## LatentModelVectorConstructorTests

[`Divergences/LatentModelVectorConstructorTests.cs`](../tests/NanoQL.Tests/Divergences/LatentModelVectorConstructorTests.cs)

The latent model on one factor's weights builds its Gaussian policy from them: QuantLib's constructor default-constructs the policy first (`latentmodel.hpp:667-680`), whose default reads the first row of an empty matrix (`gaussiancopulapolicy.hpp:40-43`): QuantLib crashes. The check is QuantLib's matrix constructor on the same weights, bit for bit.

Facts:

- `TheVectorConstructorIsTheMatrixOne`

## LatticeRsgTests

[`Divergences/LatticeRsgTests.cs`](../tests/NanoQL.Tests/Divergences/LatticeRsgTests.cs)

The lattice rules' bound and skip: QuantLib's `LatticeRule::getRule` checks the number of points against 2.9^20, about 1.8e9, where its message and its tables say 2^20, so 2^22 points are accepted; nanoql checks 2^20. And QuantLib's `LatticeRsg::skipTo(n)` moves n points on, where its other sequences' `skipTo` moves to the n-th: after 3 draws, `skipTo(10)` draws the 13th point (0.0126953125, 0.0146484375 on rule A at 1,024 points in two dimensions); nanoql's draws the 10th, as a fresh sequence skipped to 10 does in QuantLib (0.009765625, 0.857421875).

Facts:

- `TheRulesHoldUpToTwoToTheTwentyPoints`
- `SkipToMovesToThePoint`

## LeastSquaresStandardErrorTests

[`Divergences/LeastSquaresStandardErrorTests.cs`](../tests/NanoQL.Tests/Divergences/LeastSquaresStandardErrorTests.cs)

The least squares' standard errors. QuantLib's `GeneralLinearLeastSquares` scales the modelling uncertainty by the residuals' standard deviation over n − 2 degrees of freedom whatever the basis' size (`generallinearleastsquares.hpp:142-147`): for a quadratic fitted to seven points, a multiplier of 0.20401 where R's `lm`, over n − p, gives 0.22809 (QuantLib 1.44). nanoql's errors are R's: σ̂ √[(AᵀA)⁻¹]ⱼⱼ with σ̂² the residual sum of squares over n − p, p the coefficients estimated. QuantLib's checks: that closed form, its own modelling uncertainty times σ̂; and at p = 2, where n − 2 is R's, QuantLib's own errors.

Facts:

- `TheStandardErrorsTakeNMinusPDegreesOfFreedom`
- `ForAStraightLineTheErrorsAreQuantLibs`

## LevyEffectiveStrikeTests

[`Divergences/LevyEffectiveStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/LevyEffectiveStrikeTests.cs)

Levy's engine at an effective strike at or below zero (fixed). A seasoned option's effective strike is X = K − ((T − T₂)/T)·A; QuantLib takes log(X) and prices NaN wherever X ≤ 0 (on spot 100, q 5%, r 10%, σ 15% (Act/360), averaging from 180 days ago to 90 days on, the current average 300 or 285 with the call at 95 or 100, every value NaN). nanoql prices the certain exercise, the call Se − X·e^(−rT₂) and the put 0, the formula's own limit as X → 0⁺. Check: the formula at a tiny positive X, where N(d₁) and N(d₂) are 1 in double.

Facts:

- `AnEffectiveStrikeAtOrBelowZeroIsTheCertainExercise`

## LevyTermStructureTests

[`Divergences/LevyTermStructureTests.cs`](../tests/NanoQL.Tests/Divergences/LevyTermStructureTests.cs)

Levy's engine on term structures and at its closed form's singular points (fixed). QuantLib reads one volatility and the zero rates at maturity (`continuousarithmeticasianlevyengine.cpp:81-90`), +38.5% and −17.2% off a Monte Carlo on rising and falling markets; and its closed form, taken wherever |b| > 1000·QL_EPSILON, cancels near the carries b = 0, −σ²/2 and −σ² and at small volatility: on spot 100, the call at 100 over a year from today (r 5%, σ 20%, Act/365 Fixed), it prices 1.4982854223412403 at b = 3e-13 (−65.8% off its own formula in 50 digits) and NaN at b = −σ²/2 and −σ², and NaN at σ 1e-8 and b 3%. nanoql integrates the two moments under the structures by date, the covariance on QuantLib's adaptive Gauss-Lobatto rule, the log-variance through log1p of the normalized covariance, so nothing cancels, and at zero variance takes the average as its forward. A Kronrod rule for the covariance missed the exact value by −8.9e-9 and +4.3e-9 on those markets, fooled by the kinks at the structures' nodes, so it runs on Gauss-Lobatto. Checks: the exact value, Levy's formula on the moments in 30 digits (5.7995941086251922 rising, 9.6602716681694337 falling), within the tolerance, and the same formula on the moments computed here by Gauss-Lobatto split at the nodes, and a Monte Carlo, on those markets; QuantLib's formula in 50 digits at the singular points and at small volatility, within the tolerance; the forward's payoff at σ = 0. QuantLib's master fixes the same class after v1.44 by another route (#2926: a cancellation-free series for the second moment, log1p, the intrinsic forward at zero variance), whose three cases `AsianOptionsTests` ports as checks of this engine, its mean taken as spot·t₂ plus the integral of F/spot − 1 so it is exact at zero carry; it stays until the pin moves to a release carrying the fix.

Facts:

- `ARisingMarketIsLevysFormulaOnItsMoments`
- `AFallingMarketIsLevysFormulaOnItsMoments`
- `ACarryOfZeroIsTheFormulaInFiftyDigits`
- `ACarryOfMinusHalfTheVarianceIsTheFormulaInFiftyDigits`
- `ACarryOfMinusTheVarianceIsTheFormulaInFiftyDigits`
- `ASmallVolatilityIsTheFormulaInFiftyDigits`
- `ZeroVolatilityIsTheForwardsPayoff`

## LhpProgrammedDefaultTests

[`Divergences/LhpProgrammedDefaultTests.cs`](../tests/NanoQL.Tests/Divergences/LhpProgrammedDefaultTests.cs)

The LHP model under a default programmed after the evaluation date: its averages read the notionals and recoveries of the names alive at the evaluation date, so its statistics are its model's without the event. QuantLib's reads the notionals alive at the statistic's date beside the evaluation date's probabilities and recoveries, past its vectors, undefined (four runs: 2.24789, 2.24363, 1.77310 and a throw); without the event it reads 12.282321555009048 at ten years on the basket vector's programmed case.

Facts:

- `AProgrammedDefaultLeavesTheModelsStatistics`

## LineSearchReturnTests

[`Divergences/LineSearchReturnTests.cs`](../tests/NanoQL.Tests/Divergences/LineSearchReturnTests.cs)

A line-search-based minimization ends at the point its line search last accepted: QuantLib's `LineSearchBasedMethod::minimize` returns on the function tolerance or the iteration limit (linesearchbasedmethod.cpp:98-103) before it sets the problem's current value (line 105), so its problem holds the point before with the function value of the point after. The port sets the current value first, the failed search's own line (line 112). QuantLib's check: the point its line search last accepted, read from the cost function's trace on the wheel. The hypersphere and lower-diagonal pseudo square roots, which minimize with `ConjugateGradient` (pseudosqrt.cpp:166-244), move with it.

Facts:

- `ConjugateGradientEndsAtItsLastAcceptedPoint`
- `TheHypersphereAndLowerDiagonalRootsMoveWithIt`

## LinearFlatPrimitiveTests

[`Divergences/LinearFlatPrimitiveTests.cs`](../tests/NanoQL.Tests/Divergences/LinearFlatPrimitiveTests.cs)

The linear-flat interpolation's primitive outside its range, the flat value's integral (fixed). QuantLib's `LinearFlatInterpolationImpl::primitive` extends the end intervals' lines past the ends, where its value is flat, so its primitive's slope there isn't its value: on x 1, 2, 3 and y 1, 3, 2 its primitive at 0.5 is −0.25 for the flat value's −0.5, and at 4 it is 6 for 6.5; on a single point its primitive and derivative read before their arrays (`generalizedhullwhite.hpp`). nanoql's primitive is the flat value's integral outside the range, its derivative there the value's 0, and a single point's the flat value's; inside the range all three are QuantLib's.

Facts:

- `OutsideTheRangeThePrimitiveIntegratesTheFlatValue`
- `ASinglePointIsFlat`

## LinearTsrNormalVegaRatioTests

[`Divergences/LinearTsrNormalVegaRatioTests.cs`](../tests/NanoQL.Tests/Divergences/LinearTsrNormalVegaRatioTests.cs)

The linear TSR pricer's vega-ratio strategy on a normal section: QuantLib's `strikeFromVegaRatio` reads `SmileSection::vega` outside its `try` (`lineartsrpricer.cpp:227-228`), which refuses a normal section (`smilesection.cpp:119-128`), so every normal-volatility coupon on the strategy refuses: on the wheel, on a flat 3% curve and a 0.8% normal volatility, "vega for normal smilesection not yet implemented". nanoql's search reads a normal section's vega from QuantLib's own Bachelier derivative (`bachelierBlackFormulaStdDevDerivative`) times √T, the ratio scale-free; `SmileSection.Vega` keeps QuantLib's refusal. QuantLib's checks: its lognormal values, and the bound found has the ratio of QuantLib's Bachelier derivatives the strategy asks.

Facts:

- `ALognormalSectionKeepsQuantLibsValue`
- `ANormalSectionsBoundsHaveTheRatio`

## LinearTsrPriceThresholdTests

[`Divergences/LinearTsrPriceThresholdTests.cs`](../tests/NanoQL.Tests/Divergences/LinearTsrPriceThresholdTests.cs)

The linear TSR pricer's price-threshold strategy: QuantLib's `strikeFromPrice` starts Brent at the swap rate, the bracket's end (`lineartsrpricer.cpp:263`), which Brent refuses (`solver1d.hpp:203-206`), and the refusal is swallowed, and the threshold it passes is the vega ratio's member (`:310`): every threshold falls back to the default bounds, so on the wheel, on a flat 3% curve and a 20% volatility, a 1-year coupon fixing in 10 years prices 0.032367510616761526 at every threshold, the default rate bound's price. nanoql searches with the threshold from the bracket's midpoint, as the vega-ratio search does. QuantLib's check: its rate-bound strategy on the bounds the search finds, and the out-of-the-money price at each bound the threshold, within the search's accuracy.

Facts:

- `TheBoundsAreTheThresholdsStrikes`
- `TheThresholdMovesThePrice`

## LinearTsrReversionCutTests

[`Divergences/LinearTsrReversionCutTests.cs`](../tests/NanoQL.Tests/Divergences/LinearTsrReversionCutTests.cs)

The linear TSR pricer's reversion cut: QuantLib's `GsrG` reads every reversion with |a| below 1e-4 as 0 (`lineartsrpricer.cpp:71-79`), so the model's slope jumps there: on the wheel, on a flat 3% curve and a 20% volatility, a 1-year coupon fixing in 10 years prices 0.03235538786871865 for every a below 1e-4 and 0.03235551192457936 at 1e-4, a jump of 1.2e-7 in its convexity adjustment of 1.9e-3. nanoql takes −expm1(−a·τ)/a below the cut (§3.3's expm1 class) and τ at a = 0, with QuantLib's form at and above it, which loses at most 1e-12 there. QuantLib's checks: its own values at 0 and at the cut.

Facts:

- `ThePriceIsContinuousAcrossTheCut`
- `TheLimitAtZeroIsQuantLibsValue`

## LinearTsrSectionForwardTests

[`Divergences/LinearTsrSectionForwardTests.cs`](../tests/NanoQL.Tests/Divergences/LinearTsrSectionForwardTests.cs)

The linear TSR pricer's forward on a section with an at-the-money level of its own: QuantLib wraps a section without an at-the-money level at the coupon's forward and keeps one that has its own (`lineartsrpricer.cpp:177-181`), so on such a section the options price on the section's forward while the model's split and singular terms take the coupon's. The SABR and ZABR cubes build each section on an interpolated forward (`sabrswaptionvolatilitycube.hpp:1015-1029`): on QuantLib's cms.cpp SABR cube over a 1%–5% zero curve, a 1-year coupon fixing in 5 years has a section forward 5.7e-3 below its own, and QuantLib's TSR prices its caplet at F + 1% 1.83e-3 below QuantLib's numeric Hagan pricer and its floorlet at F − 1% 1.29e-3 above. nanoql wraps every section at the coupon's forward, so every TSR price on those cubes moves; a matrix's sections have no at-the-money level, and the interpolated cube's carry the coupon's forward, so there the prices are QuantLib's. A ZABR section, whose own price is its kernel's, is priced through the volatility it reports. QuantLib's checks: its values on a matrix and the interpolated cube, and its numeric Hagan pricer on the SABR cube.

Facts:

- `AMatrixAndTheInterpolatedCubeKeepQuantLibsValues`
- `OnTheSabrCubeOverASlopedCurveTheOptionsAgreeWithHagan`
- `AZabrSectionIsPricedThroughItsVolatility`

## LinearTsrStdDevBoundsTests

[`Divergences/LinearTsrStdDevBoundsTests.cs`](../tests/NanoQL.Tests/Divergences/LinearTsrStdDevBoundsTests.cs)

The linear TSR pricer's standard-deviations strategy: QuantLib sets both bounds for either option, so a caplet is integrated from below its strike, takes the lognormal width k·σ without √T where the normal width has it, and subtracts the shift twice (`lineartsrpricer.cpp:318-342`): on the wheel, on a flat 3% curve and a 20% volatility, a 1-year coupon fixing in 10 years prices 0.03276470349655507, 0.03364469105154387 and 0.03424293117331103 at k = 3, 5 and 10, moving away from the full range's 0.032367510616761526 as k grows. nanoql sets each option's own bound, the width k·σ·√T and the shift once. QuantLib's check: its rate-bound strategy on the corrected bounds, and the full range as k grows.

Facts:

- `TheBoundsAreTheRateBoundsAtThoseStrikes`
- `OnAShiftedSectionTheShiftIsSubtractedOnce`
- `TheyConvergeToTheFullRange`

## LinearTsrVolatilityDayCounterTests

[`Divergences/LinearTsrVolatilityDayCounterTests.cs`](../tests/NanoQL.Tests/Divergences/LinearTsrVolatilityDayCounterTests.cs)

The linear TSR pricer's day counter after a new volatility: QuantLib's constructor stores the volatility's day counter (`lineartsrpricer.cpp:61`, `volDayCounter_`), which the model's slope reads (`GsrG`, `:73`), and `setSwaptionVolatility` leaves it, so a pricer given another volatility prices the slope on the old one's day counter: on the wheel, on a flat 3% curve, a 1-year coupon fixing in 10 years on a pricer built on an Actual/365 volatility and given an Actual/360 one prices 0.0323995567530028, where a pricer built on the Actual/360 volatility prices 0.03239972383363776. nanoql's copy reads its own volatility's day counter, so it prices as a pricer built on it. QuantLib's check: the pricer built on the new volatility.

Facts:

- `ACopyPricesAsAPricerBuiltOnItsVolatility`

## LocalVolCurveRefusalTests

[`Divergences/LocalVolCurveRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/LocalVolCurveRefusalTests.cs)

`LocalVolCurve` on a Black variance that falls in time. QuantLib returns the square root of the negative derivative, NaN (`localvolcurve.hpp:88-95`): a curve of 30% at six months and 10% at a year, built without the forced monotone variance, gives a process diffusion of NaN at t = 0.8. nanoql refuses with `NanoQLException`, "negative local vol^2 at time …; the black variance curve is not smooth enough", as QuantLib's `LocalVolSurface` refuses the same condition (`localvolsurface.cpp:148-151`).

Facts:

- `AFallingVarianceIsRefused`

## LocalVolMismatchedTimesTests

[`Divergences/LocalVolMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/LocalVolMismatchedTimesTests.cs)

Structures dated or counted apart through the local-volatility readers. QuantLib's Andreasen-Huge interpolation reads the dividend curve at the risk-free curve's times (`andreasenhugevolatilityinterpl.cpp:299`, `:414`, `:462`, `:547`, `:623`); its Black-Scholes, generalized Black-Scholes and local-volatility density calculators read their curves and volatility at one time; and its forward operators read like `FdmBlackScholesOp`. So a dividend curve on Actual/360 under a market on Actual/365 (Fixed) is read at other instants: from 15 October 2026, curves at 3% and 1%, the Andreasen-Huge forward at half a year is 101.0050167084168 where the dividend curve restated on Actual/365 (Fixed), its rate times 365/360 and so the same discount at every date, gives 100.99800271468963; the density calculators' cumulative probabilities at 105 are 0.6208898362432407, where the restated market gives 0.6210392949047169 (QuantLib-Python 1.44). nanoql reads each structure at the instant a time denotes, on its own clock: the risk-free curve's for the interpolation and the Black-Scholes calculators, the local volatility's for its calculator, and `FdmBlackScholesOp`'s for the forward operators. QuantLib's check, its readers on the restated market, gives nanoql's values. Where every structure shares the clock, QuantLib's bits stand (the vectors). On its default grid the local-volatility calculator solves to its local volatility's last date, the last representable one for a structure without a maximum date, which a curve dated later reads too.

Facts:

- `AndreasenHugeReadsTheDividendCurveAtTheInstants`
- `TheDensityCalculatorsReadEachStructureAtTheInstants`
- `TheForwardOperatorsReadEachStructureAtTheInstants`
- `OnItsDefaultGridToTheLastDateTheLocalVolCalculatorReadsACurveDatedLater`

## LocalVolSurfaceMismatchedTimesTests

[`Divergences/LocalVolSurfaceMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/LocalVolSurfaceMismatchedTimesTests.cs)

`LocalVolSurface` on curves that measure time differently from its Black surface. QuantLib reads the risk-free and dividend curves at the surface's times (`localvolsurface.cpp:82-118`), each measuring time from its own reference date in its own day counter. nanoql reads each curve at the instant the surface's time denotes, on its own clock, so a surface on Act/360 over curves on Act/365 gives the local volatility of the same surface restated on Act/365, the same variances at every date: QuantLib's own surface on that market is the check, a local volatility being a rate per the surface's own year, so the two differ by √(365/360). Dupire's time derivative steps 1e-4 on each surface's own clock, so they agree to 1e-6; curves on the surface's day counter and reference date are QuantLib's.

Facts:

- `CurvesOnAnotherDayCounterAreReadAtTheSurfacesInstants`

## LognormalStrikeBelowShiftTests

[`Divergences/LognormalStrikeBelowShiftTests.cs`](../tests/NanoQL.Tests/Divergences/LognormalStrikeBelowShiftTests.cs)

A lognormal CMS option struck at or below −shift (its Hagan and TSR parts): QuantLib's Hagan caplet on a lognormal volatility prices `optionletPrice(Call, max(effectiveCap, 1e-10))` (`conundrumpricer.cpp:171-178`), E[R] − 1e-10 for a strike below 0, dropping −K; its linear TSR pricer refuses the strike in its section ("strike + displacement (-0.0045371 + 0) must be non-negative"). On the wheel, on a flat 3% curve and a 20% volatility, a capped CMS coupon fixing in a year with a spread of 1% and a cap of 0.5% pays 0.010000000099999993 on the analytic pricer and 0.009996991868222522 on the numeric one, where R > 0 makes the right rate the cap; and a digital replicated by caplets at ±1e-4 around 0 gives a probability of 0.49999950000000765. nanoql prices a caplet at or below −shift, on a forward above −shift, as the coupon's expected rate less the strike and a floorlet there as 0, the limits of each pricer's own formula, in each pricer before it prices an option. A forward at or below −shift has no lognormal rate distribution, and QuantLib's refusal stands there. QuantLib's checks: its own swaplets, which give the expected rate, its values above −shift, and its refusals on such a forward.

Facts:

- `ACapBelowTheSpreadPaysTheCap`
- `ADigitalAtZeroReplicatesToOne`
- `OnAShiftedSectionTheBoundIsMinusShift`
- `AForwardAtOrBelowMinusShiftIsRefused`

## LongstaffSchwartzCalibrationSeedTests

[`Divergences/LongstaffSchwartzCalibrationSeedTests.cs`](../tests/NanoQL.Tests/Divergences/LongstaffSchwartzCalibrationSeedTests.cs)

The Longstaff-Schwartz calibration seed's default. QuantLib's documentation says the calibration seed is the pricing seed plus an offset, "to avoid identical paths in calibration and pricing" (`mclongstaffschwartzengine.hpp:63-71`), but its constructor tests `seedCalibration != Null<Real>()` on a `BigNatural` already narrowed from `Null<Size>()` (`:148`), so its default is always the narrowed null: 4294967295 through C++, 2147483647 (`nullInt()`) through the wheel. nanoql's default is the documented one, (seed + 1768237423) mod 2³². QuantLib's check: its engine with that seed passed explicitly, whose bits the fixed default reproduces; and QuantLib's default's bits through the narrowed null passed explicitly.

Facts:

- `TheDefaultCalibrationSeedIsTheDocumentedOne`

## LongstaffSchwartzExerciseTimesTests

[`Divergences/LongstaffSchwartzExerciseTimesTests.cs`](../tests/NanoQL.Tests/Divergences/LongstaffSchwartzExerciseTimesTests.cs)

The Longstaff-Schwartz pricer's exercise times. QuantLib's pricer exercises at every time of its grid (`longstaffschwartzpathpricer.hpp:111-133`, `:143-200`), while its engine adds steps between a Bermudan option's dates and never puts an American option's earliest date on the grid (`mclongstaffschwartzengine.hpp:218-225`): a Bermudan on more steps than dates, and an American option whose window opens later, price as an American exercisable throughout. nanoql's pricer exercises only at the grid's exercise times. QuantLib's checks: its finite-difference engine (800 × 800) on the Bermudan and the window, within the Monte Carlo error; and on one step per date, QuantLib's own bits (QuantLib-Python 1.44).

Facts:

- `ABermudanOrAWindowExercisesOnlyAtItsExerciseTimes`
- `OnOneStepPerDateTheBitsAreQuantLibs`

## LookbackExtremeBeyondSpotTests

[`Divergences/LookbackExtremeBeyondSpotTests.cs`](../tests/NanoQL.Tests/Divergences/LookbackExtremeBeyondSpotTests.cs)

A running extremum beyond the spot (fixed). The spot is observed, so a running maximum below it or a minimum above it is impossible; QuantLib's engines price it as given: on spot 100, r 5%, q 0, σ 20% over a year, a fixed call struck at 90 with a running maximum of 80 at 30.175098740702154, where the spot as the maximum gives 28.67991950233946, and a floating put with that maximum at 19.69144860119077 for 14.29056770740371. nanoql reads the extremum as max(minmax, S) or min(minmax, S), in the analytic and Monte Carlo engines. Check: QuantLib's value at minmax = S.

Facts:

- `AFixedLookbacksExtremumIsTheSpotWhereItLiesBeyondIt`
- `AFloatingLookbacksExtremumIsTheSpotWhereItLiesBeyondIt`

## LookbackMismatchedTimesTests

[`Divergences/LookbackMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/LookbackMismatchedTimesTests.cs)

The analytic lookback engines on structures that measure time differently (fixed). QuantLib's engines read each rate as its curve's zero rate over the risk-free curve's time, and the volatility over the same time, so on an Actual/360 risk-free curve beside an Actual/365 Fixed volatility they are off their own values on the market restated onto one clock with the same discounts by date: on spot 100, r 5% (Actual/360), q 2%, σ 25% over a year, the floating call +0.468%, the fixed call +0.599%, the partial floating call +0.435% and the partial fixed call +0.563%. nanoql reads each rate on the volatility's clock, the curve's zero rate times its own time over the volatility's, and each time on it, as the analytic barrier engine reads them. QuantLib's check: its own engine on the restated market, the risk-free rate scaled to Actual/365 Fixed so its discounts by date are unchanged, which nanoql's engines price with QuantLib's bits (the vectors).

Facts:

- `TheFloatingLookbackPricesAsOnTheRestatedMarket`
- `TheFixedLookbackPricesAsOnTheRestatedMarket`
- `ThePartialFloatingLookbackPricesAsOnTheRestatedMarket`
- `ThePartialFixedLookbackPricesAsOnTheRestatedMarket`

## LookbackPartialWindowTodayTests

[`Divergences/LookbackPartialWindowTodayTests.cs`](../tests/NanoQL.Tests/Divergences/LookbackPartialWindowTodayTests.cs)

The partial lookbacks' windows opening or closing today or before it (fixed, as for the partial-time barrier). QuantLib's partial formulas take f₁ = 0/0 for a window date today at the money and √t₁ of a negative time before it: on spot 100, r 5%, q 1%, σ 20% over a year, its partial fixed call struck at 100 opening today and its partial floating call closing today are NaN, and a window dated yesterday throws the bivariate normal's "rho must be >= -1.0 (NaN not allowed)"; its Monte Carlo engine prices a window opened before today as opening today and folds today's spot into a window that has closed. nanoql takes each limit: a fixed window opening today is the full fixed lookback with the spot as its maximum; a floating window closing today the European at λ times the minimum (or maximum) with the spot, and one closed before today the European at λ times the extremum as given; a fixed window opened before today is refused, the option holding no extremum observed since. Checks: QuantLib's full fixed engine and its analytic European engine, and the partial engine a day out. "Before today" is before the evaluation date and "today" runs from it to the clock's origin, inclusive: on a market settling two days after the evaluation date, a window dated on it or a day after takes today's limit, where reading a clock's time refused the fixed window and dropped the spot from the floating one, 24% low; the checks the full fixed engine and the European engine on that market, the Monte Carlo's full fixed lookback on the same paths to the bit.

Facts:

- `AFixedWindowOpeningTodayIsTheFullLookback`
- `AFloatingWindowClosingTodayIsTheEuropean`
- `AFixedWindowOpenedBeforeTodayIsRefused`
- `AFloatingWindowClosedBeforeTodayIsTheEuropeanAtItsExtremum`
- `OnALaterMarketAFixedWindowOpeningBeforeTheClocksOriginIsTheFullLookback`
- `OnALaterMarketAFloatingWindowClosingBeforeTheClocksOriginIsTheEuropeanWithTheSpot`
- `OnALaterMarketAWindowDatedBeforeTheEvaluationDateIsPast`

## LookbackSmallCarryTests

[`Divergences/LookbackSmallCarryTests.cs`](../tests/NanoQL.Tests/Divergences/LookbackSmallCarryTests.cs)

The analytic lookback engines near zero carry. As the carry b = r − q vanishes, each formula's terms over 2b/σ² cancel: over a year at σ 20%, QuantLib's floating call (spot 100, minimum 95) is off its 50-digit value by 2.0e-9 at b = 1e-7 and 1.4e-4 at 1e-12, its partial fixed call (strike 100, the window from half a year) by 1.4e-9 and 1.9e-4, and every engine is NaN at b = 0 (#2856 takes the full engines' limit below |λ| of 1e-7; the partial engines stay NaN). nanoql evaluates the full engines' formulas in double-double below u = |2b√T/σ| of 1e-3, the cubic through double-double values below 1e-18, and #2856's limit at 0; and the partial engines' as the cubic in b through QuantLib's double values at ±c and ±2c, c the carry at a cut of 6e-4 to 2e-3 in u. Checks: the formulas at 50 digits, the bivariate normal by quadrature, within 1e-12 of max(|value|, 1); over fit, corner and holdout grids the worst is 1.6e-13 for the full engines and 1.1e-12 for the partial ones (the engines on the holdout grid among them). QuantLib's master fixes the partial engines after the pin (#2925, `2d86c20e3`): the limit below |2b/σ²| of 1e-7 and 1.44's form above it; nanoql keeps its cubic, and the master's three cases below check it.

Facts:

- `TheFloatingLookbackHoldsItsDigits`
- `TheFixedLookbackHoldsItsDigits`
- `ThePartialFloatingLookbackHoldsItsDigitsAndPricesZeroCarry`
- `ThePartialFixedLookbackHoldsItsDigitsAndPricesZeroCarry`
- `TestPartialLookbackZeroCarry`
- `TestPartialLookbackZeroCarryBoundaryLimits`
- `TestPartialLookbackZeroCarryContinuity`

## LookbackSmallVolatilityTests

[`Divergences/LookbackSmallVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/LookbackSmallVolatilityTests.cs)

The analytic lookback engines at small, non-zero volatilities (fixed). λ = 2b/σ² grows as σ falls, so QuantLib's s^(−λ) overflows to +∞ while its normal or bivariate factor underflows to 0, and their product is NaN where the value is finite: on spot 100, r 5%, q 1% over a year, a fixed call struck at 105 with a running maximum of 110 is 4.75614712250357 at σ 0.3% and NaN in QuantLib, as is every engine from about σ 0.3% at a moneyness of 1.1 (σ² below 2|b ln s|/709). nanoql takes such a product in logs, exp(a ln s + ln P), ln P finite in the tails (`LookbackTails`), where QuantLib's isn't finite, QuantLib's product and bits elsewhere. Check: QuantLib's formulas at 80 digits, the bivariate normal by quadrature outward from its peak (two independent references, agreeing on the full engines), within 1e-12 of max(|value|, 1), and the values far below 1 to 1e-9 relative. The partial fixed call at 110 is the full one's to 20 digits, its window's opening, half a year in, unreached at these volatilities. The rows at σ 1e-4 and 1e-6 hold the bivariate normal's arguments of order 1e4 and 1e5, where its exponent's terms of order h² and h must cancel analytically.

Facts:

- `TheFixedLookbackIsFinite`
- `TheFloatingLookbackIsFinite`
- `ThePartialFixedLookbackIsFinite`
- `ThePartialFloatingLookbackIsFinite`

## LookbackUpstreamFixesTests

[`Divergences/LookbackUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/LookbackUpstreamFixesTests.cs)

The fix QuantLib's master made to the lookback family after the pin, which nanoql takes, with the master's own case. #2856 (`9c44b7345`): the full fixed and floating lookbacks at zero carry, where 1.44's formulas are 0/0, take their limit; the master switches to it below |λ| of 1e-7, nanoql at zero carry alone, the formula in double-double around it (`LookbackSmallCarryTests`).

Facts:

- `TestAnalyticContinuousLookbackZeroCarry`

## LookbackZeroVolatilityTests

[`Divergences/LookbackZeroVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/LookbackZeroVolatilityTests.cs)

The analytic lookback engines at zero volatility (#2853's class). Each of QuantLib's formulas divides by the volatility and is NaN at σ = 0: on spot 100, r 5%, q 1% over a year, its floating call is NaN, 3.8820409248455316 at σ = 1e-8. nanoql takes each engine's limit below machine-epsilon variance, the payoff on the forward path, which is monotone, so its extremum over the window lies at an end: the floating call's minimum the spot, the fixed call's maximum the forward, the partial fixed call's the forward, the partial floating call's minimum over [0, t₁] the spot. Check: QuantLib's values at σ = 1e-8.

Facts:

- `EachEngineTakesTheForwardPathsPayoff`

## LossModelBindingTests

[`Divergences/LossModelBindingTests.cs`](../tests/NanoQL.Tests/Divergences/LossModelBindingTests.cs)

A loss model bound to a basket by copy, its precomputation run there, once: QuantLib's model holds a relinkable handle to its basket, so a model set on two baskets in turn can keep the first's precomputation (the recursive model's weights and the random models' simulations: 26.31 and 28.72 against a fresh model's 49.47 and 94.475). Each binding here is a fresh model's; the check is QuantLib's fresh model, bit for bit.

Facts:

- `EachBindingIsAFreshModels`

## MakeSwaptionNullIndexTests

[`Divergences/MakeSwaptionNullIndexTests.cs`](../tests/NanoQL.Tests/Divergences/MakeSwaptionNullIndexTests.cs)

A swaption builder given no swap index is refused at construction, with `ArgumentNullException` naming `swapIndex`, as nanoql's swap builders refuse theirs. QuantLib's `MakeSwaption` takes it unchecked and dereferences it at the conversion, for its fixing calendar, its forwarding curve and its conventions (`makeswaption.cpp:57-58, 82, 112`): results C++ leaves undefined, where the wheel stops on Boost's assertion.

Facts:

- `ABuilderWithoutASwapIndexIsRefused`

## MaltaWeekendTests

[`Divergences/MaltaWeekendTests.cs`](../tests/NanoQL.Tests/Divergences/MaltaWeekendTests.cs)

Malta's weekend. QuantLib's header lists Saturdays and Sundays, as the Malta Stock Exchange's trading calendar it cites does, but its code overrides `WesternImpl`'s weekend with Friday and Saturday (`malta.cpp:30-32`), so every Friday is a holiday and every Sunday a business day; the code, the header and QuantLib's test came in one commit, the test listing what the code gives. nanoql keeps `WesternImpl`'s weekend with QuantLib's own rules, which answer as QuantLib's on Mondays to Thursdays and Saturdays. `testMalta`'s list on Fridays and Sundays (`test-suite/calendars.cpp:4142-4174`) moves here, QuantLib's recorded beside nanoql's.

Facts:

- `FridaysAreBusinessDaysAndSundaysWeekendDays`
- `TestMaltasListOnFridaysAndSundays`

## MargrabeExpiredGreeksTests

[`Divergences/MargrabeExpiredGreeksTests.cs`](../tests/NanoQL.Tests/Divergences/MargrabeExpiredGreeksTests.cs)

An expired Margrabe option's assets' deltas and gammas. QuantLib's option never initializes them (`margrabeoption.hpp:55`, members its constructor leaves unset) and its expired path sets only `MultiAssetOption`'s results (`multiassetoption.cpp:74-77`, through `instrument.hpp:132-133`), so `delta1()` and the others read uninitialized memory (`margrabeoption.cpp:32-54`), which is undefined. nanoql refuses the read as QuantLib's results refuse an unset value: the four are `null`, the NPV and the multi-asset Greeks 0 as QuantLib's (the playbook's undefined-behaviour row).

Facts:

- `BothEnginesLeaveTheAssetsGreeksUnset`

## MargrabeGreeksQuantityTests

[`Divergences/MargrabeGreeksQuantityTests.cs`](../tests/NanoQL.Tests/Divergences/MargrabeGreeksQuantityTests.cs)

The European Margrabe engine's Greeks at quantities other than 1. QuantLib's gammas drop the quantities and its theta takes them squared (`analyticeuropeanmargrabeengine.cpp:101-104`): a first gamma of 0.01154 against 0.02307 bumped at Q₁ = 2, a second of 0.02136 against 0.06409 at Q₂ = 3, and a theta of −8.361 against −9.013 by a date bump, on the test's market; QuantLib's own case pins the spreadsheet's values, which reproduce its formulas (`margrabeoption.cpp:250`, `:259`, `:268`, moved here). nanoql takes each quantity once: QuantLib's own prices bumped are the check, and its bits at quantities of 1. QuantLib's master makes the same fix after the pin (#2918, `17a720edf`), upstream's three expressions token for token, and restates the spreadsheet rows' gammas and theta, the rows above them with each spot divided by its quantity: its rows are below, as a check of the fix.

Facts:

- `EachGammaIsItsDeltasDerivative`
- `TheThetaIsTheValuesDecay`
- `TestEuroExchangeTwoAssetsQuantityRows`

## MarketModelCompositeEndTests

[`Divergences/MarketModelCompositeEndTests.cs`](../tests/NanoQL.Tests/Divergences/MarketModelCompositeEndTests.cs)

The composites' ends. QuantLib's composites never record a part's end: `compositeproduct.cpp:43` resets the flag `multiproductcomposite.cpp:49` and `singleproductcomposite.cpp:46` test, which nothing sets, so a finished part steps again at each of its later times; and a composite's path ends when the parts it stepped at a step are finished, whatever the others' later times. Forwards beside a 1% rebate at 1.0, 1.5 and 2.0, which finishes at each step: QuantLib's composite pays the rebate three times, 0.0276525, where alone it pays once; forwards struck 50 bp low beside a 1% rebate at 0.75: QuantLib's composite ends the path at 0.75, its later forwards 0. nanoql's composites record each part's end, step only unfinished parts, end a path when every part is finished or at their last step, and the single composite's count is 0 at a step where no part steps (`singleproductcomposite.cpp:63` writes it only for a stepped part). Checks: QuantLib's own composite values, bit for bit, on the parts it steps the same; each forward's exact value; and the single composite's cash flows, those of the multi composite's products, bit for bit. A swaption (2, 5) beside a 1% rebate at 0.75 and a 2% one at 2.25, in one product: QuantLib's single composite ends the path at 0.75, 0.0094334, the first rebate's; recording each part's end but writing the count only for a stepped part, it pays the swaption's step again at 2.0, where no part steps, 0.0354032 for the multi composite's 0.0314175 (1,023 paths).

Facts:

- `AFinishedPartIsntSteppedAgain`
- `APathEndsOnlyWhenEveryPartHasEnded`
- `AStepWhereNoPartStepsPaysNothing`
- `ASwaptionsIdleStepPaysNothingInOneProduct`

## MarketModelDifferencesTests

[`Divergences/MarketModelDifferencesTests.cs`](../tests/NanoQL.Tests/Divergences/MarketModelDifferencesTests.cs)

The market-model differences (fixed). QuantLib's `rateVolDifferences` divides rate i's variance difference by the i-th evolution time (`marketmodeldifferences.cpp:50`), not the rate's reset time: FlatVol at 25% against 20% on quarterly steps over semi-annual rates gives 0.2121 for every rate where the volatilities differ by √(25%² − 20%²) = 15%, √2 too high, and reads past the evolution times where the rates outnumber the steps. Its `rateInstVolDifferences` checks the rate's index against the number of steps (`:68`): on three steps rate 4 of 6 is refused, "the index given is greater than the number of steps", and on fine steps an index past the rates reads past the covariance. nanoql divides by the rate's reset time, or by the evolution's last time where that comes first, since the total covariance accrues nothing past it: on an evolution ending at 1.5 over resets to 3.0, the reset time alone gives 0.1299, 0.1162 and 0.1061 on the last three rates. It checks the index against the number of rates. Checks: FlatVol's √(σ₁² − σ₂²); QuantLib's bits where the evolution times are the rate times.

Facts:

- `ARatesVolatilityIsOverTheTimeItsVarianceAccrues`
- `OnTheRateTimesTheDifferencesAreQuantLibs`
- `ARatesIndexIsCheckedAgainstTheRates`

## MarketModelDiscounterBracketTests

[`Divergences/MarketModelDiscounterBracketTests.cs`](../tests/NanoQL.Tests/Divergences/MarketModelDiscounterBracketTests.cs)

The discounter's bracket (fixed). QuantLib's `MarketModelDiscounter` takes the first rate time at or after the payment as its period's start (`discounter.cpp:31`, `lower_bound`), so a payment strictly inside a period but the last is extrapolated from the next period, against the class's own names (`before_`, `beforeWeight_`) and its weight's [0, 1] form: on forwards rising 0.5% a period, in the last bond, a payment at 1.25 is worth 1.10935174 bonds and one at 0.75 1.12738126, each 1.2e-3 from its own period's log-linear interpolation, the next period's line carried back. nanoql takes the last rate time at or before the payment, clamped to the periods as QuantLib clamps. Checks: the bracketing bonds' log-linear interpolation, its weight in [0, 1]; and QuantLib's bits on a rate time, before the first and after the last, and in the last period.

Facts:

- `APaymentInsideAPeriodInterpolatesItsOwnPeriod`
- `APaymentOnARateTimeOrOutsideThePeriodsIsQuantLibs`
- `ThePathwiseDiscounterInterpolatesItsOwnPeriodToo`

## MarketModelPathwiseDeflatedCapletTests

[`Divergences/MarketModelPathwiseDeflatedCapletTests.cs`](../tests/NanoQL.Tests/Divergences/MarketModelPathwiseDeflatedCapletTests.cs)

The deflated caplets' deflator derivative (fixed). Caplet i pays (fᵢ − Kᵢ) aᵢ P(Tᵢ₊₁)/P(T₀), whose derivative with respect to fⱼ, j ≤ i, carries −τⱼ P(Tⱼ₊₁)/P(Tⱼ) times it, τⱼ the rate's own period; QuantLib multiplies by the caplet's accrual aⱼ (`pathwiseproductcaplet.cpp:233`), right only where the accruals are the periods. On six semi-annual rates with accruals 0.5 × 365/360, caplet 3 struck at 3.1% on the forwards: QuantLib's derivatives with respect to f₀, f₁ and f₂ are −4.7570e-4, −4.7547e-4 and −4.7524e-4, 365/360 of the amount's own central differences (deltas 3.2e-5 from the undeflated caplet's on the same paths). nanoql multiplies by the period. Checks: the amount's central differences; QuantLib's bits where the accruals are the periods.

Facts:

- `TheDeflatorsDerivativeIsOverTheRatesPeriod`
- `ThroughThePathwiseEngineTheDeflatedDeltasAreTheUndeflated`
- `WhereTheAccrualsAreThePeriodsTheAmountsAreQuantLibs`

## MarketModelPathwiseSwapStrikeTests

[`Divergences/MarketModelPathwiseSwapStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/MarketModelPathwiseSwapStrikeTests.cs)

The pathwise swap's one strike (fixed). QuantLib's `MarketModelPathwiseSwap` expands one strike to every rate, then checks the strikes it was given, not the expanded ones (`pathwiseproductswap.cpp:59`), so it refuses one strike with the message that offers it, "strikes.size() does not equal numberOfRates or 1". nanoql checks the expanded strikes. Check: the same strike given for every rate, bit for bit.

Facts:

- `OneStrikeIsEveryRatesStrike`

## MarketModelTextTests

[`Divergences/MarketModelTextTests.cs`](../tests/NanoQL.Tests/Divergences/MarketModelTextTests.cs)

Refusals whose QuantLib text names another value than its check reads (fixed). `EvolutionDescription` checks the last evolution time against the last fixing time, `rateTimes[rateTimes.size()-2]`, and prints `rateTimes[numberOfRates_-2]`, the time before it, a read before the vector at one rate (`evolutiondescription.cpp:51-54`: −1.29e-231 at one rate, 0.5 for a check of 1.0 at two). `LMMCurveState::setOnDiscountRatios` checks the first valid index against the number of rates and prints one more as its bound (`lmmcurvestate.cpp:76-79`), found while porting it. The exponential correlation's text is in `ExponentialCorrelationTests`.

Facts:

- `TheLastEvolutionTimesRefusalNamesTheLastFixingTime`
- `TheDiscountRatiosFirstIndexRefusalNamesItsBound`

## MarketModelUndefinedReadsTests

[`Divergences/MarketModelUndefinedReadsTests.cs`](../tests/NanoQL.Tests/Divergences/MarketModelUndefinedReadsTests.cs)

The market-model framework's inputs whose QuantLib path is undefined, each refused before the undefined step (under the playbook's row for undefined behaviour). The refusals' types, texts and parameters are asserted by `Conventions/MarketModelRefusalTests.cs`.

Facts:

- `ACurveStatesRateReadAtTheNumberOfRatesIsRefused`
- `TheNumerairesFunctionsReadsPastTheirInputsAreRefused`
- `FlatVolsReadsPastItsCorrelationAreRefused`
- `TheConstantMaturityDriftsOnNoSpanningForwardsAreRefused`
- `MergeTimesSortAndSearchOfInvalidSetsAreRefused`
- `TheEvolvedMatricesReadPastANarrowMatrixIsRefused`
- `TheConversionsWritesPastTheirBuffersAreRefused`
- `TheIndexesQuantLibsUnsignedArithmeticWrapsAreRefused`
- `TheEvolversAndTheVolatilityProcessesReadsPastTheirStateAreRefused`
- `TheModelsVariancesAndAdaptersReadsPastTheirInputsAreRefused`
- `TheCalibrationsReadsPastTheirInputsAreRefused`

## MarkovFunctionalOutputsTests

[`Divergences/MarkovFunctionalOutputsTests.cs`](../tests/NanoQL.Tests/Divergences/MarkovFunctionalOutputsTests.cs)

The Markov-functional model's outputs after a recalculation: QuantLib's `modelOutputs` clears every output but the at-the-money levels and the annuities, which it appends to (`markovfunctional.cpp:618-648`), so its first call after a recalculation (a changed market or `forceArbitrageIndices`) returns them twice, the stale ones first. nanoql computes the outputs per call: a copy calibrated again gives a fresh model's. QuantLib's check: its own first call on a fresh model.

Facts:

- `ARecalibratedCopysOutputsAreAFreshModels`

## MarkovFunctionalUpstreamFixesTests

[`Divergences/MarkovFunctionalUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/MarkovFunctionalUpstreamFixesTests.cs)

The fixes QuantLib's master made to the Markov-functional model after the pin, which nanoql takes: #2912 (`b762b0b02`) strikes the upper rate bound's digital at the bound, where QuantLib 1.44 subtracts the smile's shift (`markovfunctional.cpp:432-436`), so on a shifted smile the model's rates are capped below the bound the rest of the model reads unshifted; it asks for at least two grid points, where 1.44 asks for one (`markovfunctional.hpp:168-172`), and on one the grid's spacing divides by zero; and its refusal of two smile pretreatments is restated, "At most one of …". Its count of the pretreatments nanoql has (`MfSmileFlagsTests`). The master's cases below are its tests of the settings, the grid points, the shifted smile's bound, the SABR and payoff-extrapolation fits and the trace. Not ported: `testNullSwapIndex` and `testNullIborIndex` (nanoql's constructors refuse a null index before the model reads it), `testTimeZeroNumeraireAfterDateChange`, `testModelOutputsAfterRecalculation` and `testModelOutputsAfterMarketUpdate` (no observers: nanoql's outputs are computed once from the model's own state, `MarkovFunctionalOutputsTests`).

Facts:

- `TestConflictingSmileSettings`
- `TestSmileSettingsValidation`
- `TestMinimumYGridPoints`
- `TestModelOutputsTrace`
- `TestSabrSmileCalibration`
- `TestShiftedSmileUpperRateBound`
- `TestPayoffExtrapolation`

## McAsianControlVariateTests

[`Divergences/McAsianControlVariateTests.cs`](../tests/NanoQL.Tests/Divergences/McAsianControlVariateTests.cs)

The arithmetic Monte Carlo engine's geometric control variate (fixed). QuantLib's control path pricer discounts at the last fixing's time (`mc_discr_arith_av_price.hpp:175-178`) where its analytic control engine discounts at the exercise date, so the controlled price is biased when the exercise follows the last fixing: on spot 100, r 8%, q 0, σ 25% (Act/365 Fixed), a call at 100 on twelve fixings every 30 days prices 7.70961 controlled for 7.86180 ± 0.024 uncontrolled with the exercise 90 days after the last fixing, and 6.810 for 7.402 at 365 days (200,000 samples, seed 42). And the control's running product underflows at small spots while its analytic value doesn't: at spot 0.01 with 252 daily fixings the controlled price is +99% to +103% off the uncontrolled one. nanoql discounts the control path at the exercise date and flushes the product into the average where the next factor would underflow it. Checks: the engine without the control within the two estimates' errors, on QuantLib's own pricers and analytic geometric engine; and the controlled price at spot 0.01 against the same contract at spot 100, scaled.

Facts:

- `TheControlMeetsTheUncontrolledPriceNinetyDaysAfterTheLastFixing`
- `TheControlMeetsTheUncontrolledPriceAYearAfterTheLastFixing`
- `AtASmallSpotTheControlledPriceIsTheScaledOne`

## McBarrierExerciseTests

[`Divergences/McBarrierExerciseTests.cs`](../tests/NanoQL.Tests/Divergences/McBarrierExerciseTests.cs)

The Monte Carlo barrier engine's exercise. QuantLib's engine never reads the exercise's type and prices any exercise to its last date: a down-and-out put struck at 100, barrier 80, on spot 100, r 5%, q 1%, σ 20% over a year, 20 steps, 20,000 pseudo-random samples at seed 42, prices 1.670455625787 for an American exercise, its European value, where QuantLib's binomial engine (Cox-Ross-Rubinstein, Derman and Kani's asset, 400 steps) prices the American at 6.364401494576 and the European at 1.674490747474. nanoql refuses a non-European exercise, as the family's European engines do: the paths price a European exercise only, early exercise wanting a regression. The binomial engine prices American barrier options and stays.

Facts:

- `ANonEuropeanExerciseIsRefused`

## McBarrierMismatchedTimesTests

[`Divergences/McBarrierMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/McBarrierMismatchedTimesTests.cs)

The Monte Carlo barrier engine's grid on structures that measure time differently (fixed). QuantLib's grid runs on the risk-free curve's time and its process reads the other structures at it, as though they shared that clock. nanoql's paths and discounts run on the engine's clock, `FdmBlackScholesOp.Clock`, each structure read at the instants its times denote. QuantLib's check: its own engine on the market restated onto the clock, the dividend rate and the volatility scaled so their discounts and variances by date are unchanged, on the same seed: the paths draw the same numbers, so the prices agree to rounding; and a risk-free curve settling two days after the others, read from its own date on the clock from the earliest, where QuantLib's grid starts at the risk-free curve's date.

Facts:

- `Act365StructuresBesideAnAct360CurvePriceAsOnTheRestatedMarket`
- `ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock`

## McBarrierSeed5Tests

[`Divergences/McBarrierSeed5Tests.cs`](../tests/NanoQL.Tests/Divergences/McBarrierSeed5Tests.cs)

The Monte Carlo barrier engine at seed 5 (fixed). QuantLib's bridge draws its uniforms from a constant MT(5) of the path's dimension, so on pseudo-random paths at seed 5 each step's increment and its extreme read the same uniforms: a down-and-out call at 100, barrier 95, r 5%, σ 30% over a year on 10 steps and 200,000 samples prices 6.860215 ± 0.042713, +31.9 standard errors above the analytic 5.498097, where seeds 4 and 6 are within 1.6. nanoql's bridge reads MT(6) at seed 5 on the Mersenne Twister traits. Low-discrepancy paths share nothing with MT(5), so they keep QuantLib's stream and its values, `testBabsiriValues`' among them. Checks: the analytic barrier engine, and QuantLib's low-discrepancy values at seed 5.

Facts:

- `PseudoRandomPathsAtSeedFiveAreUnbiased`
- `LowDiscrepancyPathsAtSeedFiveKeepQuantLibsValues`

## McBasketMismatchedTimesTests

[`Divergences/McBasketMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/McBasketMismatchedTimesTests.cs)

The Monte Carlo European basket engine on structures that measure time differently. QuantLib's engine evolves every process on the first's times (`mceuropeanbasketengine.hpp:169-170`; `stochasticprocessarray.cpp`'s time), each process reading its structures at them: with asset 2's dividend curve and volatility on Act/360, their discounts and variances by date unchanged, a spread call prices 0.58% below the same market restated on Act/365 on the same seed, and on a risk-free curve settling two business days after the other structures 0.53% below. nanoql's paths run on the clock spanning every process's structures, each read at the instants its times denote and accruing nothing before its own date, so on the same seed they price the restated market: QuantLib's engine on that market is the check, well inside its error estimate. A market on one clock keeps QuantLib's draws and arithmetic.

Facts:

- `TheSecondAssetsStructuresOnAnotherDayCounterPriceTheRestatedMarket`
- `ARiskFreeCurveSettlingLaterIsPricedFromTheEarliestDate`

## McDigitalSeed76Tests

[`Divergences/McDigitalSeed76Tests.cs`](../tests/NanoQL.Tests/Divergences/McDigitalSeed76Tests.cs)

The Monte Carlo digital engine at seed 76. QuantLib's pricer draws the Brownian bridge's uniform numbers from `PseudoRandom::urng_type(76)` with the path generator's dimension (`mcdigitalengine.hpp:177-179`), so at the pricing seed 76 without antithetic variates each step's bridge draws the uniform number of the step's own increment: a down-and-in put at 80 (cash 10, S 100, r 5%, vol 25%, one year, 12 steps, 100,000 samples) prices 3.4762897047890697 ± 0.01477, 5.7 errors above QuantLib's analytic engine, 3.3915878484599937, where seed 77 gives 0.8 errors above (QuantLib-Python 1.44). nanoql's bridge takes the seed 77 at the pricing seed 76; every other seed keeps QuantLib's 76 and its bits. QuantLib's check: its analytic engine.

Facts:

- `AtSeed76TheBridgeDrawsItsOwnNumbers`

## McDigitalStepVolatilityTests

[`Divergences/McDigitalStepVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/McDigitalStepVolatilityTests.cs)

The Monte Carlo digital engine's crossing volatility (with an independent check). QuantLib's `DigitalPathPricer` reads the volatility of step i at the step's end time, `diffusion(timeGrid[i+1], S_i)`, under the comment "initial (timeGrid[i+1]) for the time being" (`mcdigitalengine.cpp:53-54`, `:80-82`), so on a time-dependent volatility each step's Brownian bridge reads the next step's. nanoql reads it at the step's start, (t_i, S_i). The check, QuantLib's having none: on a volatility constant within each grid step the bridge's crossing probability is exact, so the fixed engine on a coarse grid matches the same engine on a fine one within their errors, where QuantLib's is far off. The volatility: forward volatilities of 10%, 40%, 10% and 40% over four quarters of 91 days (a `BlackVarianceCurve`), an up-and-in call at 115 and a down-and-in put at 85 paying 10 at the hit; QuantLib's engine (seed 42, 100,000 samples) gives the call 7.362620907677973 on 4 steps against 6.395034183222466 on 64, the put 6.14377515430365 against 5.417421714551026 (QuantLib-Python 1.44).

Facts:

- `OnAVolatilityConstantPerStepACoarseGridMatchesAFineOne`

## McDigitalWindowTests

[`Divergences/McDigitalWindowTests.cs`](../tests/NanoQL.Tests/Divergences/McDigitalWindowTests.cs)

The Monte Carlo digital engine on an exercise window. QuantLib's analytic engine refuses a window ("American option with window exercise not handled yet", `analyticdigitalamericanengine.cpp:41-43`), and its Monte Carlo engine prices from today: an up-and-in call at 110 paying 10 (S 100, r 5%, vol 25%, one year; 365 steps, 20,000 samples, seed 1) gives 7.164398475241645 with the window opening today and in 180 days alike (QuantLib-Python 1.44). nanoql's engine puts the window's opening on the grid and counts crossings from it, a path already past the strike there paid then. QuantLib's check: its own closed form, the cash at the hit from the opening (`AmericanPayoffAtHit`), integrated over the lognormal density of the underlying at the opening and discounted to today.

Facts:

- `AWindowCountsCrossingsFromItsOpening`
- `AWindowOpenTodayIsQuantLibs`

## McDoubleBarrierTests

[`Divergences/McDoubleBarrierTests.cs`](../tests/NanoQL.Tests/Divergences/McDoubleBarrierTests.cs)

The Monte Carlo double-barrier engine's clock and exercise (fixed). QuantLib runs the paths and the discounts on the risk-free curve's time and reads each structure there on its own clock (`mcdoublebarrierengine.hpp:164-201`), and prices any exercise to its last date, never reading its type. nanoql runs them on the barrier engines' clock, each structure read at the instants its times denote, and refuses a non-European exercise as the Monte Carlo barrier engine does. Checks: the market restated onto the risk-free curve's day counter; the refusal's text.

Facts:

- `AnAct365VolatilityBesideAnAct360CurvePricesAsOnTheRestatedMarket`
- `ANonEuropeanExerciseIsRefused`

## McHestonMismatchedTimesTests

[`Divergences/McHestonMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/McHestonMismatchedTimesTests.cs)

The Monte Carlo Heston engine on curves dated or counted differently (the playbook's row for structures on another clock). QuantLib's engine runs its paths on the process's times, the risk-free curve's from its reference date, reads the dividend curve at those times and discounts at the grid's last time on the risk-free curve (`mceuropeanhestonengine.hpp:121-129`). nanoql's runs its paths on the model's clock, from the earliest curve date on the risk-free curve's day counter, each curve read at the instants the grid's times denote, and discounts there. QuantLib's check: its engine on the market restated on one clock, from the same seed, which the fixed engine on the original market reproduces to rounding (QuantLib-Python 1.44).

Facts:

- `ADividendCurveOnAnotherDayCounterIsReadAtItsOwnTimes`
- `ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock`

## McHullWhiteCollarTests

[`Divergences/McHullWhiteCollarTests.cs`](../tests/NanoQL.Tests/Divergences/McHullWhiteCollarTests.cs)

The Monte Carlo Hull-White cap engine on a collar: QuantLib's path pricer reads the cap rates only for a cap (`mchullwhiteengine.cpp:54`, `:94-99`), so it prices a collar as its floor alone, long: on the C++ harness, a 3-year collar at 3.5% / 2.5% on Euribor 6M from the spot date, notional 1,000,000, on Hull-White (0.05, 0.01) on a flat 2.7% curve, 2,000 samples, seed 42, prices 8,734.001429909456, its floor's price, where its cap prices 3,713.709005573455. nanoql prices the collar as its cap less its floor, as the analytic and tree engines do. QuantLib's check: its own cap and floor on the same paths.

Facts:

- `TheCollarIsItsCapLessItsFloor`

## McLookbackLambdaTests

[`Divergences/McLookbackLambdaTests.cs`](../tests/NanoQL.Tests/Divergences/McLookbackLambdaTests.cs)

The Monte Carlo partial floating lookback's λ (fixed). QuantLib's pricer pays S_T − min where the instrument's payoff, and QuantLib's analytic engine, is S_T − λ min: a partial floating call at λ 1.2 (running minimum 100) whose window ends 90 days into 360, on spot 100, r 5%, q 2%, σ 20%, 4 steps, 100,000 antithetic samples at seed 42, prices at 7.747172763156529, its value at λ 1. nanoql pays λ times the minimum. Check: the discrete payoff's exact value on the engine's window {0, 90 days}, the quadrature `McLookbackWindowTests` takes: 3.042965056630764, within three of the engine's error estimates.

Facts:

- `ThePartialFloatingPayoffReadsLambda`

## McLookbackMismatchedTimesTests

[`Divergences/McLookbackMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/McLookbackMismatchedTimesTests.cs)

The Monte Carlo lookback engine's grid on structures that measure time differently (fixed). QuantLib's grid runs on the risk-free curve's time and its process reads the other structures at it, as though they shared that clock. nanoql's paths and discount run on the engine's clock, `FdmBlackScholesOp.Clock`, each structure read at the instants its times denote. Check: the engine on the market restated onto the clock, the dividend rate and the volatility scaled so their discounts and variances by date are unchanged, on the same seed: the paths draw the same numbers, so the prices agree to rounding; and a risk-free curve settling two days after the others, read from its own date on the clock from the earliest, where QuantLib's grid starts at the risk-free curve's date, against the market restated with every structure today.

Facts:

- `Act365StructuresBesideAnAct360CurvePriceAsOnTheRestatedMarket`
- `ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock`

## McLookbackSeasonedTests

[`Divergences/McLookbackSeasonedTests.cs`](../tests/NanoQL.Tests/Divergences/McLookbackSeasonedTests.cs)

The Monte Carlo lookback engine on seasoned options (fixed). QuantLib's pricers take the extremum from the path's second node and never read the running extremum, so a seasoned option prices as a new one: on spot 100, r 6%, q 0, σ 10% over a year, 2,000 steps, 20,000 antithetic samples at seed 1, a fixed call struck at 100 with a running maximum of 115 at 11.196910337699553 and a floating call with a running minimum of 90 at 10.747874194538674, their values with the extremum at the spot, where the analytic engines give 16.195934579797157 and 15.741880586612329. nanoql folds the observed extremum into the path's. Check: QuantLib's analytic engines on the seasoned options, which the Monte Carlo converges to, within QuantLib's own Monte Carlo test's 0.1.

Facts:

- `ASeasonedFixedLookbackReadsItsRunningMaximum`
- `ASeasonedFloatingLookbackReadsItsRunningMinimum`

## McLookbackWindowTests

[`Divergences/McLookbackWindowTests.cs`](../tests/NanoQL.Tests/Divergences/McLookbackWindowTests.cs)

The Monte Carlo partial lookbacks' window (fixed). QuantLib's partial pricers snap the window's date to the closest time of a regular grid that doesn't carry it, and the partial fixed pricer drops the window's first node: a partial floating call (running minimum 100, λ 1) whose window ends 37 days into a year, on spot 100, r 5%, q 2%, σ 20%, 4 steps, 100,000 antithetic samples at seed 42, snaps its window to the first node and reads the spot a quarter on, at 7.805558043540716. nanoql makes the window's date a time of the grid, its start node included, so on this grid the window is {0, 37 days}. Check: the discrete payoff's exact value on that window, e^(−rT) E[(S_T − λ min(m, S_t₁))⁺], a quadrature over S_t₁ of the Black call on S_T given it: 10.221335405994491, within three of the engine's error estimates (the continuous window's analytic value, 11.617971695171292, lies above both, the discrete minimum over two nodes missing the path's dips between them). And a partial fixed call struck at 100 whose window opens 300 days into the year, on the same paths, snaps its window to the grid's node at three quarters and drops it, so it reads the terminal spot alone, a vanilla call, at 9.179431603675994; nanoql's window is {300 days, a year}, its exact value e^(−rT) E[(max(S_t₁, S_T) − K)⁺] 10.686101666470105, the engine within three of its error estimates.

Facts:

- `APartialWindowsDateIsATimeOfTheGrid`
- `APartialFixedWindowReadsItsFirstNode`

## McMismatchedTimesTests

[`Divergences/McMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/McMismatchedTimesTests.cs)

The Monte Carlo vanilla engines on structures dated or counted differently. QuantLib's engines run their paths on the process's times, the risk-free curve's from its reference date (`mcvanillaengine.hpp:152-163`, `mclongstaffschwartzengine.hpp:213-237`), and read the other structures at those times: a dividend curve or a volatility dated before the risk-free curve, or on another day counter, is read off its own clock. nanoql's engines run their paths from the earliest structure's reference date on the risk-free curve's day counter (`FdmBlackScholesOp.Clock`), each structure read at the instants the grid's times denote, nothing accruing before its own date, and the payoffs discounted there. QuantLib's check: its own engines on the market restated on one clock, from the same seeds, which the fixed engines on the original market reproduce to rounding (QuantLib-Python 1.44).

Facts:

- `ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock`
- `AVolatilityOnAnotherDayCounterIsReadAtItsOwnTimes`
- `OnOneClockTheBitsAreQuantLibs`

## McPerformanceResetTodayTests

[`Divergences/McPerformanceResetTodayTests.cs`](../tests/NanoQL.Tests/Divergences/McPerformanceResetTodayTests.cs)

The Monte Carlo performance engine on a cliquet with a reset today. Its grid takes the resets as mandatory times (`mcperformanceengine.hpp:141-149`) and the time grid merges a reset at time 0 into its start, so QuantLib's path pricer refuses on its count check ("discounts/options mismatch", `mcperformanceengine.cpp:33`), where the analytic engine prices 0.125531. nanoql's path pricer reads each reset's index from the grid, as QuantLib's Monte Carlo forward engines do. The checks: the analytic engine (QuantLib's bits) within two of the Monte Carlo error estimates; with the first reset a day later, QuantLib's Monte Carlo value, bit for bit.

Facts:

- `AResetTodayIsPricedFromTheGridsStart`

## McSimulationUndefinedTests

[`Divergences/McSimulationUndefinedTests.cs`](../tests/NanoQL.Tests/Divergences/McSimulationUndefinedTests.cs)

The tolerance loop of `McSimulation{TPath, TStats}.Value` refuses what QuantLib skips or leaves undefined (mcsimulation.hpp:118-134): a NaN error estimate, which QuantLib's comparison with the tolerance skips, returning the mean (after playbook §2's NaN row for `std::max_element`); and a next-batch estimate past `Size`'s range, which QuantLib converts to `Size`, undefined there (the undefined-path row): a tolerance of 0 makes the estimate infinite.

Facts:

- `ANaNErrorEstimateIsRefused`
- `ANextBatchPastTheSamplesRangeIsRefused`

## McVarianceSwapMismatchedTimesTests

[`Divergences/McVarianceSwapMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/McVarianceSwapMismatchedTimesTests.cs)

The Monte Carlo variance-swap engine on a risk-free curve settling later (the playbook's row for structures on another clock). QuantLib's grid runs on the risk-free curve's time (`mcvarianceswapengine.hpp:191-201`), so with that curve settling two days on the paths run two days short and the volatility, dated today, is read at the shorter times. nanoql runs the grid on the process's clock, from the earliest structure's date, each structure read there accruing nothing before its own date, as the vanilla Monte Carlo engines do. The check is nanoql on the risk-free curve restated from the evaluation date (a discount of 1 to its date and its rate beyond): the same paths and value.

Facts:

- `ARiskFreeCurveSettlingLaterRunsTheGridFromTheEvaluationDate`

## MfEndDateTests

[`Divergences/MfEndDateTests.cs`](../tests/NanoQL.Tests/Divergences/MfEndDateTests.cs)

The Markov-functional model's caplet end dates (#2848's class): a caplet calibration point, and the outputs' forward rates and caplets, end at the value date advanced by the index's tenor on its fixing calendar (`markovfunctional.cpp:278`, `:949`, `:1109`), where the index's own end is its maturity date, which an index of its own may override. On the wheel, USD Libor 6M fixing on 9 April 2025 values on 11 April and matures on 14 October 2025, where the advance gives 13 October. nanoql ends each at the index's maturity date, as QuantLib's master computes the Gaussian1d forward rate after the pin. QuantLib's check: the index's own maturity date, and for an index whose maturity is the advance (Euribor), QuantLib's bits. The outputs' caplets read the end twice, the forward rate's and the caplet's (`:949`, `:1109`): a call struck at 0 pays the floating leg, so its price is P(0, t) − P(0, end) for any end the two share with the calibration point, QuantLib's own martingale identity, which nanoql's holds to 7e-8; QuantLib's end a day early at the forward's site moves it by 4e-5, at the caplet's by 5e-3.

Facts:

- `ACapletPointEndsAtItsIndexsMaturityDate`
- `TheOutputsCapletsEndWhereTheCalibrationPointDoes`

## MfSmallReversionTests

[`Divergences/MfSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/MfSmallReversionTests.cs)

The Markov-functional state process at small reversions: QuantLib's variance over a step, (e^{2au} − e^{2as})/(2a)·σ², cancels as a falls below 1e-2, and cuts to its zero formula only below its epsilon (`mfstateprocess.cpp:26-27`, `:81-84`, `:98-110`). On the harness, the variance over [0, 3] without step times at a volatility of 1 is 2.9976021664879227 at a = 1e-14 (−8.0e-4 from the limit, 3), 3.000044657142098 at 1e-12 (+1.5e-5) and 3.0000000927898895 at 1e-8. nanoql's is σ²e^{2as}·expm1(2a(u − s))/(2a) below 1e-2, its zero formula kept below QuantLib's epsilon, and QuantLib's form and bits from 1e-2. QuantLib's checks: its own form at the switch, which the expm1 form meets, and its own zero formula, the forms' limit, which the variance meets as a → 0 in proportion to a.

Facts:

- `TheVarianceMeetsQuantLibsFormAtTheSwitch`
- `TheVarianceTendsToTheZeroReversion`

## MfSmileFlagsTests

[`Divergences/MfSmileFlagsTests.cs`](../tests/NanoQL.Tests/Divergences/MfSmileFlagsTests.cs)

The Markov-functional model's smile pretreatments: QuantLib's settings refuse only all three of Kahale's, SABR's and a custom smile, though the refusal's text says "Only one … can be specified" (`markovfunctional.hpp:162-167`), so on the wheel Kahale's smile with SABR's validates, and the model then pretreats with Kahale's and ignores SABR's. nanoql refuses any two, as QuantLib's master does after 1.44 (#2912, `Divergences/MarkovFunctionalUpstreamFixesTests.cs`), with the master's text, "At most one of …". QuantLib's check: the master's own cases.

Facts:

- `AnyTwoSmilePretreatmentsAreRefused`

## MfStateProcessSdeTests

[`Divergences/MfStateProcessSdeTests.cs`](../tests/NanoQL.Tests/Divergences/MfStateProcessSdeTests.cs)

The Markov-functional state process, dx = σ(t) e^{at} dW: QuantLib's diffusion omits the factor e^{at} (`mfstateprocess.cpp:62-66`), and its variance without step times omits the volatility's square (`:80-84`). On the harness, at a = 0.05 and σ = 0.8 without steps, its diffusion at t = 2 is 0.8 and its variance over [0, 1] 1.0517091807564771, where the same process stepped once past the horizon gives 0.6730938756841454. nanoql's are its equation's. QuantLib's checks: its own stepped variance, of which the diffusion's square is the derivative and which the variance without steps equals with one step past the horizon. The Markov-functional model's prices don't move: they read the variance's ratios, which its level leaves.

Facts:

- `TheDiffusionIsTheEquations`
- `TheVarianceWithoutStepsIsTheSteppedOne`

## MinimizeContinuationTests

[`Divergences/MinimizeContinuationTests.cs`](../tests/NanoQL.Tests/Divergences/MinimizeContinuationTests.cs)

QuantLib's `minimize` changes the `Problem` it is given, so a second `minimize` on the same problem starts from the first's optimum, its current value then. nanoql's `Problem` is immutable, and `OptimizationMethod.Minimize(Problem, EndCriteria)` starts from the problem's initial value each time. `OptimizationMethod.Minimize(ProblemState, EndCriteria)` is QuantLib's: a second minimization of one state continues from the first's optimum, as QuantLib's `HybridSimulatedAnnealing` runs its local optimizer on the same problem.

Facts:

- `MinimizeProblemStartsFromTheInitialValueAndMinimizeStateContinues`

## MonteCarloCatBondEngineTests

[`Divergences/MonteCarloCatBondEngineTests.cs`](../tests/NanoQL.Tests/Divergences/MonteCarloCatBondEngineTests.cs)

The Monte Carlo cat bond engine on a simulation of no path (QuantLib's #2839, taken from its master after the pin): refused ("no Monte Carlo paths were generated"), where 1.44 divides 0 by 0. On `testCatBondInDoomScenario`'s floating cat bond (22 November 2004, the index on a 2.5% curve, discounted on 3%) and an event set of no events covering 2000 to 2003, which ends before the bond's first period, QuantLib 1.44 reads NPV and loss probability NaN.

Facts:

- `ASimulationOfNoPathIsRefused`

## MonteCarloUndefinedInputsTests

[`Divergences/MonteCarloUndefinedInputsTests.cs`](../tests/NanoQL.Tests/Divergences/MonteCarloUndefinedInputsTests.cs)

Inputs for which QuantLib's time grid, path, Brownian bridge and path generator are undefined (they read or write outside a vector, or call std::sort or std::lround outside their contracts) are refused explicitly in nanoql: an `ArgumentOutOfRangeException` naming the caller's argument, or a `NanoQLException` where the object itself is empty.

Facts:

- `ABrownianBridgeOfNoStepOrAShortOutputIsRefused`
- `ATimeGridsUndefinedInputsAreRefused`
- `AnEmptyPathsEndsAreRefused`
- `APathGeneratorOfNoStepIsRefused`
- `AnEmptyMultiPathsPathSizeIsRefused`

## MoorePenroseWideTests

[`Divergences/MoorePenroseWideTests.cs`](../tests/NanoQL.Tests/Divergences/MoorePenroseWideTests.cs)

The Moore-Penrose inverse of a wide matrix (fixed). QuantLib's reads the singular values past their end and then throws "matrices with different sizes (3x2, 3x3) cannot be multiplied" on a 2 × 3 matrix. nanoql's is ((Aᵀ)⁺)ᵀ, Penrose's identity; on wide, tall, rank-deficient and zero matrices it meets Penrose's four conditions, A A⁺ A = A, A⁺ A A⁺ = A⁺ and A A⁺, A⁺ A symmetric.

Facts:

- `ThePenroseConditionsHold`
- `AWideInverseIsItsTransposesTransposed`

## MultiCubicSplineBoundaryTests

[`Divergences/MultiCubicSplineBoundaryTests.cs`](../tests/NanoQL.Tests/Divergences/MultiCubicSplineBoundaryTests.cs)

The multi-cubic spline at and beyond a dimension's last grid point, and at a coordinate that isn't a number. QuantLib treats the last point as extrapolation (`multicubicspline.hpp:550`), so it refuses it, its documented bug ("cannot interpolate at the grid points on the boundary surface", `:463-464`); with extrapolation allowed, its flat value there and beyond takes the last interval's index past it and reads one value past the data (`:553-554` with `:295`), undefined; and a NaN coordinate fails both of its range comparisons, so its search returns the grid's end and reads past it (`:557-558`). nanoql interpolates the last point on its last interval, its value the node's, reads the last value beyond it with no read past the data, and refuses a NaN coordinate: the node's value and QuantLib's just inside are the checks.

Facts:

- `TheLastGridPointIsInterpolatedOnItsLastInterval`
- `BeyondTheGridTheLastValueIsReadWithNoReadPastTheData`
- `ACoordinateThatIsntANumberIsRefused`

## MultiProductPathwiseWrapperDeflatedTests

[`Divergences/MultiProductPathwiseWrapperDeflatedTests.cs`](../tests/NanoQL.Tests/Divergences/MultiProductPathwiseWrapperDeflatedTests.cs)

The pathwise wrapper on an already-deflated product (fixed). QuantLib's `MultiProductPathwiseWrapper` hands the accounting engine a deflated product's amount as it is (`multisteppathwisewrapper.cpp:69`), and the engine discounts it again: deflated caplets struck at the forwards, wrapped, come out 1.8% to 11.7% below `MultiStepOptionlets` on the same paths. nanoql divides a deflated product's amount by its payment time's money-market deflator on the current state (`MarketModelDiscounter.NumeraireBonds` at numeraire 0), the factor the pathwise engine skips for it. Checks: QuantLib's `MultiStepOptionlets` on the same paths; QuantLib's bits on an undeflated product (the vectors, QuantLib's `testInverseFloater`).

Facts:

- `AWrappedDeflatedCapletIsItsOptionlet`

## MultiStepTarnPaymentTimesTests

[`Divergences/MultiStepTarnPaymentTimesTests.cs`](../tests/NanoQL.Tests/Divergences/MultiStepTarnPaymentTimesTests.cs)

The TARN's floating payment times (fixed). QuantLib's `MultiStepTarn` takes the floating coupons' payment times and appends the fixed ones in their place (`multisteptarn.cpp:50`), so it discounts the floating coupons at the fixed times: on six semi-annual rates with the fixed coupons paid at each period's start and the floating at its end, its cash-flow times are the starts twice, and on a model of 1e-8 volatility its value is −0.351659, 1.3e-3 above the deterministic closed form. nanoql pays the floating coupons at their own times. Checks: QuantLib's value where the two schedules coincide (the vectors); the deterministic model's closed form.

Facts:

- `TheFloatingCouponsArePaidAtTheirOwnTimes`
- `ADeterministicModelGivesTheClosedForm`

## MultipleResetsFairSpreadTests

[`Divergences/MultipleResetsFairSpreadTests.cs`](../tests/NanoQL.Tests/Divergences/MultipleResetsFairSpreadTests.cs)

The compounded multiple-resets swap's fair spread: QuantLib's `FixedVsFloatingSwap::fairSpread` is the spread less the NPV over the floating leg's BPS (`fixedvsfloatingswap.cpp`), linear in the spread, while on a compounding leg the spread compounds with each fixing, so at it the swap misses par: on the wheel, on the vectors' 5-year payer of monthly resets compounded quarterly on 1e7, QuantLib's fair spread from a spread of 0 is −0.00044495679505344586, where the swap is worth −1.078e-5 a unit of nominal, and from 2% −0.000582486889730293, worth −6.618e-4. nanoql's is the spread at which the swap is worth zero, solved from QuantLib's estimate; on an averaging leg the spread is linear, and QuantLib's formula stands. QuantLib's checks: its NPV at the solved spread, the root of its own swap's NPV, −0.00044267984438780547, within 1.5e-8 of par on 1e7 (a secant on the wheel); and on the vectors' averaged swap, its fair spread, −0.001341481056075899, bit for bit, where its swap is worth −4.7e-10.

Facts:

- `TheCompoundedFairSpreadPricesAtPar`
- `TheAveragedFairSpreadIsQuantLibs`

## MultipleResetsZeroResetsTests

[`Divergences/MultipleResetsZeroResetsTests.cs`](../tests/NanoQL.Tests/Divergences/MultipleResetsZeroResetsTests.cs)

Zero resets per coupon: QuantLib's checks of the resets per coupon are themselves a modulo by them (`multipleresetscoupon.cpp:159`, `multipleresetsswap.cpp:57`), and the swap divides by them in its base constructor's arguments before (`multipleresetsswap.cpp:49`), so zero resets is an integer division by zero, undefined behaviour, which no check refuses. nanoql's leg and swap refuse zero resets first, naming them.

Facts:

- `ZeroResetsAreRefusedFirst`

## NegativeCountTests

[`Divergences/NegativeCountTests.cs`](../tests/NanoQL.Tests/Divergences/NegativeCountTests.cs)

QuantLib's fixing and settlement days are `Natural`, an unsigned type, which can't hold a negative value; nanoql takes an `int` and refuses a negative one on entry. From C++ a negative wraps to a large unsigned value, which QuantLib negates back where it advances by it (`interestrateindex.hpp:43, 106`, `termstructure.cpp:42`), and the wrap of 2^31 negates `INT_MIN`, which is undefined. The cash-flow solvers' maximum numbers of iterations are `Size`, unsigned too (`cashflows.hpp:273, 436`), where a negative wraps to a count no solve reaches; nanoql refuses it on entry likewise. The bonds' settlement days and a floating-rate bond's fixing days are `Natural` (`bond.hpp:89, 99`, `floatingratebond.hpp:44, 50`), which the bond advances by in `bond.cpp:167`, converting it to an `Integer`; their yields' and Z-spread's maximum evaluations are `Size` (`bond.hpp:180, 212`, `bondfunctions.hpp:165, 279`).

Facts:

- `NegativeFixingAndSettlementDaysAreRefused`
- `NegativeSettlementDaysOfAVolatilityStructureAreRefused`
- `NegativeMaximumIterationsOfTheCashFlowSolversAreRefused`

## NoArbSabrShortExpiryTests

[`Divergences/NoArbSabrShortExpiryTests.cs`](../tests/NanoQL.Tests/Divergences/NoArbSabrShortExpiryTests.cs)

Doust's no-arbitrage SABR model at short expiries. QuantLib integrates the price, the digital and the forward error over [K, U] on one Gauss-Lobatto interval, which misses the density's spike as the expiry falls: out of the money from T 0.005 (2.18e-12 against Hagan's 2.18e-6), at the money from 0.003 (3.3e-9 against 1.97e-4), and at 0.001 its forward adjustment fails too, the numerical forward 8.8e-4 off (QuantLib 1.44); nanoql splits each integral at the forward ± 8 widths Fσ_I√T where σ_I√T is below 0.1, QuantLib's single interval at and above. Checks: Hagan's prices (the SABR section) at strikes 0.9F to 1.1F within QuantLib's own consistency measure, 1e-5; the numerical forward within 1e-6 of the forward.

Facts:

- `AtShortExpiriesThePricesAreHagans`
- `AtShortExpiriesTheNumericalForwardIsTheForward`

## NonCentralChiSquaredPolynomialTests

[`Divergences/NonCentralChiSquaredPolynomialTests.cs`](../tests/NanoQL.Tests/Divergences/NonCentralChiSquaredPolynomialTests.cs)

The non-central χ² polynomial's recursion on exact moments, α and β rounded once. QuantLib sums each moment's closed form in double and runs the ill-conditioned moment recursion in double: at QuantLib's own CLV cases its 14 nodes are 2.6e-2 relative off the exact ones at ν 6, λ 31.7, and 0.61 at ν 9, λ 31.7, the smallest node 2.97 for 7.66; at λ 50 and 75 Boost refuses a negative node and at λ 100 the rule doesn't return. nanoql's nodes meet references from the same exact moments by the Hankel matrix's Cholesky factor and the Jacobi matrix's eigenvalues at 160 digits (mpmath), an independent route, to 1e-13 relative.

Facts:

- `TheNodesMeetTheirReferences`
- `TheRuleHoldsAtALargeNonCentrality`
- `TheMomentsStopAtQuantLibsTable`

## NonstandardInitialGuessTests

[`Divergences/NonstandardInitialGuessTests.cs`](../tests/NanoQL.Tests/Divergences/NonstandardInitialGuessTests.cs)

The nonstandard engine's initial guess for a calibration basket (#2845's class): QuantLib averages the fixed nominals by dividing their sum by the count of those above 1e-8 (`gaussian1dnonstandardswaptionengine.cpp:123-133`), a count that can be 0 while the sum is positive, where QuantLib's master refuses the float-float engine's like cases ("no leg 1 reset dates remain at calibration expiry"). On the wheel, a Bermudan payer exercised in one, two and three years into a 5-year swap on fixed and floating nominals of 5e-9 refuses its basket with "forward + displacement (-nan(ind) + 0) must be positive", the NaN guess's. nanoql refuses at the guess, in the master's words for its class.

Facts:

- `FixedNominalsNoneAbove1e8AreRefused`

## NonstandardSwapRoundingTests

[`Divergences/NonstandardSwapRoundingTests.cs`](../tests/NanoQL.Tests/Divergences/NonstandardSwapRoundingTests.cs)

A nonstandard swap converted from an overnight-indexed swap with a rounding precision: QuantLib's conversion copies the source's observation conventions (`nonstandardswap.cpp:60-66`) but not its rounding precision, and builds the overnight leg without one (`:188-200`), so a rounded source's coupons differ from the converted swap's. On the wheel, from 30 June 2025 on a flat 3.37% Actual/360 curve, a payer on 1,000,000 SOFR from one year out for two years, semiannual, against 3% fixed, is worth 7519.320757824375 with a precision of 4, where the swap QuantLib's conversion builds (its legs as `:188-200` set them up, built on the wheel through the vector constructor) is worth 7500.351898887035, the unrounded source's. nanoql keeps the precision, so the converted swap is worth the source's, the identity QuantLib's `testNonstandardSwapConversionPreservesObservationConventions` asserts of the conventions it copies.

Facts:

- `TheConversionKeepsTheRoundingPrecision`

## NonstandardSwapUpstreamFixesTests

[`Divergences/NonstandardSwapUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/NonstandardSwapUpstreamFixesTests.cs)

The fix QuantLib's master made to the nonstandard swap's conversion from a vanilla or overnight-indexed swap after the pin, which nanoql takes: #2956 (`a069a0955`), each leg's own nominals, the last one repeated to the leg's length. QuantLib 1.44 repeats the swap's `nominal()` on every coupon (`nonstandardswap.cpp:39-44`), which refuses an amortizing swap, "nominal is not constant".

Facts:

- `TestNonstandardCopyOfAmortisingOvernightSwap`

## NormalFwdRatePcInitialStateTests

[`Divergences/NormalFwdRatePcInitialStateTests.cs`](../tests/NanoQL.Tests/Divergences/NormalFwdRatePcInitialStateTests.cs)

The normal predictor-corrector evolver's set initial state (fixed). QuantLib's `NormalFwdRatePc::setForwards` has the drift computation for its loop body (`normalfwdratepc.cpp:80-81`), so the starting forwards stay the model's while the drifts are the new state's: on four semi-annual rates at 4%, normal volatility 0.8%, a state set at 7% starts its first path at the model's 4%, the first step's forwards 0.040197, 0.038239, 0.036115 and 0.038379. nanoql starts from the forwards given, as the log-normal evolvers do. Check: QuantLib's own evolver on a model whose initial rates are the given forwards (its arithmetic, the vectors'), bit for bit.

Facts:

- `ASetStateIsTheEvolutionOfAModelStartingThere`

## NthToDefaultReferenceDateTests

[`Divergences/NthToDefaultReferenceDateTests.cs`](../tests/NanoQL.Tests/Divergences/NthToDefaultReferenceDateTests.cs)

The n-th-to-default engine reads a default probability before a default curve's reference date as 0, and a discount before the discount curve's as 1: on default curves dated T+2 QuantLib's reads at the evaluation date refuse ("negative time (-0.00821918) given"). QuantLib's values are recorded in the test; the check is QuantLib on the market restated from the evaluation date: each curve a hazard curve of rate 0 to T+2 and the T+2 curve's rate after it.

Facts:

- `DefaultCurvesDatedAfterTheEvaluationDateReadNoDefaultBeforeIt`

## NthToDefaultUpfrontTests

[`Divergences/NthToDefaultUpfrontTests.cs`](../tests/NanoQL.Tests/Divergences/NthToDefaultUpfrontTests.cs)

The n-th-to-default upfront on the swap's own nominal: QuantLib's is the basket's remaining notional times the rate (`integralntdengine.cpp:157-163`), 5 for a 1,000 nominal on a 100 basket (the swap at its fair premium reads an NPV of 5, its upfront). The check is QuantLib where the nominal is the basket's notional.

Facts:

- `TheUpfrontIsTheNominalsRate`

## NullDateTests

[`Divergences/NullDateTests.cs`](../tests/NanoQL.Tests/Divergences/NullDateTests.cs)

QuantLib's null date, `Date()`, is a `Date?` null in nanoql, and `default(Date)` (serial 0) is never a valid date: no constructor produces it, and public members given it throw `ArgumentException`, whether they read it or not. QuantLib's `nullDate` case, which checks that the null date's serial number and hash can be read, becomes the last test.

Facts:

- `NoConstructorProducesSerialZero`
- `MembersGivenTheDefaultDateThrow`
- `MembersThatDontReadTheDefaultDateRefuseItToo`
- `AMissingDateIsNull`
- `SerialNumberEqualityHashAndTextOfTheDefaultDateDontThrow`
- `CashFlowRateCurveAndEngineMembersRefuseTheDefaultDate`

## NullRealSentinelTests

[`Divergences/NullRealSentinelTests.cs`](../tests/NanoQL.Tests/Divergences/NullRealSentinelTests.cs)

An optional real input is a `double?`, whose `null` is "not given", so a value equal to QuantLib's null sentinel, `Null<Real>()` (3.4028234663852886e38, the largest `float`), is a value. QuantLib reads its sentinel as "not given" wherever a real input defaults to it. Each fact is one such input.

Facts:

- `AnIterativeBootstrapsBoundOfQuantLibsNullIsAValue`
- `AMixedInterpolationsLeftConditionValueOfQuantLibsNullIsAValue`
- `AGlobalBootstrapsAccuracyOfQuantLibsNullIsAValue`
- `AnImpliedStandardDeviationsGuessOfQuantLibsNullIsAGuess`
- `ASmileSectionsAtmLevelOfQuantLibsNullIsALevel`
- `AnInterpolatedSmileSectionsAtmLevelOfQuantLibsNullIsALevel`
- `ASwaptionBuildersStrikeOfQuantLibsNullIsAStrike`
- `AMakeCapFloorsStrikeOfQuantLibsNullIsAStrike`
- `ABondsAtmRatePriceOfQuantLibsNullIsAPrice`
- `ALegsAtmRateTargetOfQuantLibsNullIsATarget`
- `ASwaptionHelpersStrikeOfQuantLibsNullIsAStrike`

## OldCdoTests

[`Divergences/OldCdoTests.cs`](../tests/NanoQL.Tests/Divergences/OldCdoTests.cs)

The older CDO's maximum loss given default: the sum of the names' losses given default, which QuantLib's sums onto an uninitialized value (`cdo.cpp:73-77`), so its `lgd()` is undefined; the vectors leave it out.

Facts:

- `ALaterCurvesProbabilityBeforeItsDateIsZero`
- `TheMaximumLossIsTheSumOfTheLossesGivenDefault`

## OneFactorStudentCopulaRangeTests

[`Divergences/OneFactorStudentCopulaRangeTests.cs`](../tests/NanoQL.Tests/Divergences/OneFactorStudentCopulaRangeTests.cs)

The Student-factor copulas' factor range: QuantLib's three Student classes pass no minimum, so the base's −5 stands and the integrals run over [−5, 10], losing the factor's left tail, where defaults are nearly certain; nanoql's run over [−maximum, maximum]. The copula's identity, the integral of p equal to p, is the check: QuantLib's reads 0.000897975 for 0.001 and 0.00947913 for 0.01 on Student 5/5 at correlation 0.3, 0.000102474 and 0.00857744 on Student-Gaussian ν 3 at 0.6, where the symmetric range reads them within 2.3% and 0.6%.

Facts:

- `TheIntegralOfAProbabilityIsTheProbability`

## OperatorSplittingZeroVolatilityTests

[`Divergences/OperatorSplittingZeroVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/OperatorSplittingZeroVolatilityTests.cs)

The operator-splitting spread engine at a zero volatility of the second asset. QuantLib's corrections are 0·∞ there (`operatorsplittingspreadengine.cpp:60-65`, `:138-139`), so the price is NaN, from a volatility of about 1e-200, where its variance underflows; above, its corrections tend to 0 and its price to Kirk's, exact there (9.9375389513, and 9.937538951281 at 1e-12 on the test's market). nanoql takes the corrections' limit, 0: QuantLib's own price at a tiny volatility and Kirk's engine are the checks.

Facts:

- `BothOrdersPriceKirksLimit`
- `ThePutFollowsByParity`

## OptionDateFromTimeTests

[`Divergences/OptionDateFromTimeTests.cs`](../tests/NanoQL.Tests/Divergences/OptionDateFromTimeTests.cs)

The option date of a time whose interpolated serial number is NaN, infinite or outside the integers. QuantLib's `SwaptionVolatilityDiscrete::optionDateFromTime` truncates the serial with `static_cast<Date::serial_type>` (`swaptionvoldiscrete.hpp:121-123`), whose result C++ leaves undefined there; MSVC's conversion, the platform the vectors are generated on, gives the smallest integer, which `Date`'s range check refuses. .NET's conversion saturates instead (NaN to 0, a large value to `int.MaxValue`), so a literal port would refuse other serials, or none: nanoql takes MSVC's serial in those cases, and the refusal is QuantLib's, `Date`'s `ArgumentOutOfRangeException` and text.

Facts:

- `ATimeWhoseSerialLeavesTheIntegersIsRefusedAsMsvcsSmallestInteger`

## OptionletStripperDontThrowTests

[`Divergences/OptionletStripperDontThrowTests.cs`](../tests/NanoQL.Tests/Divergences/OptionletStripperDontThrowTests.cs)

A stripper told not to throw, after a strip that failed. QuantLib's `OptionletStripper1` leaves 0 for an optionlet whose volatility it can't imply and keeps it as that optionlet's next guess (`optionletstripper1.cpp:150-171`); a guess of 0 is the solver's lower bound, which `blackFormulaImpliedStdDev` refuses ("guess (0) < xMin_ (0)"), so once its quotes are good again the re-strip leaves 0 there still: on the warm-start case's setup with the 1Y row at 60% and the others at 10%, the second optionlet's volatilities are 0, 0, 0.08686109830399297 and 0, and with the quotes back at the good surface the wheel's re-strip gives 0, 0, 0.21151008145592823 and 0, where a fresh stripper gives 0.28960633277893066, 0.24306750297546387, 0.2115100814559281 and 0.20306367926200938. nanoql strips once, from QuantLib's first guess: the failed strip gives QuantLib's zeros, and a stripper on the good quotes QuantLib's fresh values.

Facts:

- `AFailedOptionletLeavesNoGuessBehind`

## OptionletStripperWarmStartTests

[`Divergences/OptionletStripperWarmStartTests.cs`](../tests/NanoQL.Tests/Divergences/OptionletStripperWarmStartTests.cs)

A stripper's implied volatilities on a second strip. QuantLib's `OptionletStripper1` keeps its standard deviations between calculations and passes them as the next calculation's guesses (`optionletstripper1.cpp:58-59, 150-153`), so a recalculated stripper warm-starts: on 16 June 2025, a 4 × 4 surface (1Y, 2Y, 3Y, 5Y by 2%, 3%, 4%, 5%) on a flat 4% curve and Euribor 6M, a quote moved and moved back, the wheel's re-strip differs from its first in 33 of 36 volatilities, by up to 6.8449e-7: the first optionlet at 2% is 0.30000093033375125 on the first strip and 0.3000002879738929 on the second. nanoql strips once, at construction, from QuantLib's first guess, 0.14: every stripper on those quotes gives QuantLib's first strip, which a fresh QuantLib stripper gives too.

Facts:

- `EveryStripGivesQuantLibsFirstStrip`

## OrnsteinUhlenbeckVarianceTests

[`Divergences/OrnsteinUhlenbeckVarianceTests.cs`](../tests/NanoQL.Tests/Divergences/OrnsteinUhlenbeckVarianceTests.cs)

The Ornstein-Uhlenbeck variance at small speed: QuantLib's `variance` (`ornsteinuhlenbeckprocess.cpp:34-42`) is σ²Δt below |a| = √ε, dropping the terms of order aΔt, and above it σ²(1 − e^(−2aΔt))/(2a), whose 1 − e^(−2aΔt) cancels: 8.3e-8 relative at a = 1.5e-8 and Δt = 0.01, 1.4e-8 below the cut at a = 1.4e-8 and Δt = 1. nanoql's is σ²Δt·(1 − e^(−x))/x, x = 2aΔt, the cancellation-free form of QuantLib's master's kernels. QuantLib's check: its own closed form at ordinary speed and its own limit at a = 0; at small speed, the series σ²Δt(1 − aΔt + ⅔(aΔt)²), exact there to well below the tolerance.

Facts:

- `TheVarianceAtSmallSpeedIsItsSeries`
- `TheVarianceAtOrdinarySpeedAndAtZeroIsQuantLibs`

## OvernightCompoundedSpreadForecastTests

[`Divergences/OvernightCompoundedSpreadForecastTests.cs`](../tests/NanoQL.Tests/Divergences/OvernightCompoundedSpreadForecastTests.cs)

A compounded overnight coupon's spread compounded daily over the forecast range (fixed). Where the telescopic formula applies, QuantLib's compounding pricer multiplies both compound factors, with and without the spread, by the same discount ratio (`overnightindexedcouponpricer.cpp:191-195`), so the spread is dropped: a SOFR coupon with a 1.23456% spread on a flat 3.12345% curve forecasts 0.030928216305566813, the no-spread rate, while the same coupon with the same fixings published compounds the spread, 0.043388368686242844 (−124.6bp). nanoql compounds the spread day by day on the forecast fixings, as QuantLib does outside the range and once the fixings are published; a spread added rather than compounded, a zero spread and a lookback that leaves the formula aside keep QuantLib's bits. QuantLib's check: its own published route (QuantLib-Python 1.44).

Facts:

- `ADailyCompoundedSpreadIsCompoundedOverTheForecastRange`
- `TheOtherSpreadsKeepQuantLibsBits`

## OvernightFutureTodayFixingTests

[`Divergences/OvernightFutureTodayFixingTests.cs`](../tests/NanoQL.Tests/Divergences/OvernightFutureTodayFixingTests.cs)

A compounded overnight-index future on its value date: QuantLib's `compoundedRate` compounds the past fixings strictly before the evaluation date and forecasts from it, so on the value date today's published fixing is ignored, where the averaged future reads it (`overnightindexfuture.cpp`). On the wheel, a SOFR future from 23 October 2026 to 25 January 2027 on a flat 5% Actual/360 curve prices 94.96721860668744 compounded with or without a 7% fixing for today, where averaged it prices 94.99926113524772 without and 94.93546459731232 with it. nanoql's compounded future uses today's fixing once published. QuantLib's check: the same future the next business day (26 October), the value date's fixing then past, 94.90261063438486 compounded and 94.93546459731232 averaged.

Facts:

- `TodaysFixingIsUsedOnTheValueDate`

## OvernightIndexedCouponRefusalTests

[`Divergences/OvernightIndexedCouponRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/OvernightIndexedCouponRefusalTests.cs)

The overnight coupons' undefined paths, each refused before the undefined step (the playbook's undefined-path rule); the empty schedule with payment dates, where nanoql's text replaces QuantLib's; and the lookback of `int.MaxValue` days, which `int`? treats as a value where QuantLib's `Null<Natural>()` means none.

Facts:

- `AnEmptyValueDateListIsADegenerateSchedule`
- `ACappedCouponRefusesANullUnderlying`
- `AnOvernightLegRefusesAnEmptySchedule`
- `ARoundingPrecisionOutsideItsRangeIsRefused`
- `NegativeLookbackAndLockoutDaysAreRefused`
- `ALookbackOfIntMaxValueIsAValue`

## ParameterIndexTests

[`Divergences/ParameterIndexTests.cs`](../tests/NanoQL.Tests/Divergences/ParameterIndexTests.cs)

A parameter's index out of range, and its parameters before they are set. QuantLib's `Parameter::setParam(i, x)` writes `params_[i]` unchecked (`parameter.hpp:51`): a silent heap write past a constant parameter's one value, an access violation on a null parameter's empty array. nanoql refuses the index with `ArgumentOutOfRangeException` naming `i`, before the copy it returns. QuantLib's `ConstantParameter(constraint)` and `PiecewiseConstantParameter(times)` leave their parameters uninitialized (`parameter.hpp:64-65, 78-83, 134-141`), so a read before they are set is indeterminate; nanoql's are 0.

Facts:

- `AnIndexOutOfRangeIsRefused`
- `ParametersBeforeTheyAreSetAreZero`

## PartialTimeCoverTodayTests

[`Divergences/PartialTimeCoverTodayTests.cs`](../tests/NanoQL.Tests/Divergences/PartialTimeCoverTodayTests.cs)

The partial-time barrier engine with the cover event on the evaluation date (fixed). QuantLib's formulas take t₁ = 0 into Drezner's bivariate normal with infinite arguments and fail ("case not handled"). nanoql takes the formulas' limit: the start window monitors only the spot, so a knock-out is the vanilla and a knock-in 0; the end windows monitor the barrier from today to expiry, so B1 is the standard knock-out on the spot's side of the barrier and B2 the standard knock-out on its own side, out at once with the spot beyond it, the knock-ins by in-out parity. A cover event before the evaluation date stays refused as QuantLib refuses it ("negative time"). Checks: QuantLib's analytic European and barrier engines, and the engine's own values at a cover event a day out.

Facts:

- `TheStartWindowIsTheVanillaOrNothing`
- `TheEndWindowsAreTheStandardBarrierOption`

## PartialTimeEndKnockInTests

[`Divergences/PartialTimeEndKnockInTests.cs`](../tests/NanoQL.Tests/Divergences/PartialTimeEndKnockInTests.cs)

The partial-time end-window knock-ins (a refusal lifted). QuantLib refuses "Down-and-in partial-time end barrier is not implemented" and its up twin (`analyticpartialtimebarrieroptionengine.cpp:66`, `:91`), where it prices the start window's knock-in as its European less its knock-out (`CIA`). nanoql applies the same parity to QuantLib's end-window knock-out: B1 at any strike, B2 wherever QuantLib prices its knock-out. Checks: the parity on QuantLib's own engines, and an independent Monte Carlo for a call at 100, barrier 90, window [6 months, 1 year], r 5%, q 2%, σ 25%: 0.934510 ± 0.019692, against parity's 11.123762 − 10.161263 = 0.962499.

Facts:

- `AB1KnockInIsTheEuropeanLessItsKnockOut`
- `AB2KnockInIsTheEuropeanLessItsKnockOut`

## PartialTimeExerciseTests

[`Divergences/PartialTimeExerciseTests.cs`](../tests/NanoQL.Tests/Divergences/PartialTimeExerciseTests.cs)

The partial-time barrier engine's exercise. QuantLib's engine never reads the exercise's type: on spot 100, r 5%, q 1%, σ 20% over a year, a call struck at 100, barrier 90, cover event at 182 days, its start-window and end-window B1 down-and-outs price an American exercise at their European values, 8.258475774804 and 9.269946137985, and its start-window down-and-in crashes ("Boost assertion failed: px != 0", the European leg built on a null exercise). nanoql refuses a non-European exercise, as the family's European engines do: Heynen and Kat's formulas price a European exercise only. The European values stand, within a measured 3.3e-5 of QuantLib's (West's bivariate normal for Drezner's).

Facts:

- `ANonEuropeanExerciseIsRefused`

## PartialTimeRebateTests

[`Divergences/PartialTimeRebateTests.cs`](../tests/NanoQL.Tests/Divergences/PartialTimeRebateTests.cs)

The partial-time barrier engine's rebate (fixed). The instrument takes a rebate and no formula of QuantLib's engine reads it: 11.116881 at rebates 0 and 5. nanoql prices a start-window knock-out's rebate, paid at the hit before the cover event, as QuantLib's analytic barrier engine prices a knock-out's rebate to the cover event; no formula covers the end windows' or the knock-ins' rebates, which are refused ("rebate not supported for this partial-time barrier"). Check: QuantLib's analytic barrier engine with the cover event as expiry.

Facts:

- `AStartWindowKnockOutsRebateIsTheBarrierEnginesToTheCoverEvent`
- `AnEndWindowsOrAKnockInsRebateIsRefused`

## PartialTimeTouchedTests

[`Divergences/PartialTimeTouchedTests.cs`](../tests/NanoQL.Tests/Divergences/PartialTimeTouchedTests.cs)

A start-window partial-time barrier with the spot beyond its barrier (refused). QuantLib checks no trigger and prices −4.174446 for a down-and-out call at spot 80, barrier 85, where the family's analytic, binomial and Monte Carlo engines refuse "barrier touched". nanoql refuses it so; the end windows, whose monitoring starts at the cover event, are unaffected.

Facts:

- `AStartWindowSpotBeyondTheBarrierIsRefused`

## ParticleSwarmInertiaTests

[`Divergences/ParticleSwarmInertiaTests.cs`](../tests/NanoQL.Tests/Divergences/ParticleSwarmInertiaTests.cs)

The particle swarm's adaptive and decreasing inertias (fixed). QuantLib's adaptive counter is unsigned, so its first improvement wraps it to 2⁶⁴ − 1 and halves the inertia as if the swarm had stalled; its decreasing inertia never sets its swarm size, which it loops over, and never advances its iteration, so it never decreases. On the inertia form with c₁ = c₂ = 0 each velocity changes only by the inertia's factor, so a recording topology reads each iteration's factor from the velocities, on a linear cost whose leading particle's drift improves the swarm, an improvement at a net count of 0 among them: nanoql's are each inertia's formula.

Facts:

- `TheDecreasingInertiaFallsLinearlyToTheThreshold`
- `TheAdaptiveInertiaCountsImprovementsLessStalls`

## ParticleSwarmTopologyTests

[`Divergences/ParticleSwarmTopologyTests.cs`](../tests/NanoQL.Tests/Divergences/ParticleSwarmTopologyTests.cs)

The particle swarm's topologies (fixed). QuantLib's global topology keeps the largest personal best and skips the best particle, whose social best stays at the zero vector, so it steers the swarm toward the worst; its k-neighbours window runs from i − K − 1 to i + K − 1 and a particle that is its window's best takes particle 0's position; its clubs topology reads one past its clubs, takes a club's index for a particle, weighs a club's worst only on a member that isn't its new best, leaves and joins clubs past its bounds, and reads particle 0 for a club left empty. nanoql's set every particle's social best to the best personal best of its neighbourhood, the whole swarm, the window [i − K, i + K] wrapping around, or its clubs' members, which a recording inertia checks after each `FindSocialBest`; each converges on the shifted sphere; and a clubs topology driven directly keeps its invariants at every call: each club's best and worst over its members, each particle's one leave or join within [min, max] and at a reset toward the default, and each social best the best personal best among its clubs' members, on configurations where particles join clubs left empty and particle 0, the swarm's best, isn't their club-mate, and with one particle's personal best NaN, which QuantLib's comparisons pass over and nanoql's too; and the clubs topology converges on a cost that is NaN near the box's centre, the Sobol start's first point, on twelve particles over twenty seeds, where QuantLib's comparisons replaced by a first member's NaN stall up to 3.1e-4.

Facts:

- `TheGlobalSocialBestIsTheBestPersonalBest`
- `EachKNeighboursSocialBestIsItsWindowsBest`
- `EachClubsSocialBestIsAParticlesPersonalBest`
- `EachClubsCallKeepsTheClubsInvariants`
- `AClubsWorstIsWeighedOverEveryMember`
- `EachTopologyConvergesOnTheShiftedSphere`
- `TheClubsSwarmConvergesWhereTheBoxsCentreIsNaN`

## PascalTriangleTests

[`Divergences/PascalTriangleTests.cs`](../tests/NanoQL.Tests/Divergences/PascalTriangleTests.cs)

Pascal's triangle on Windows: QuantLib's coefficients are `BigNatural`, a 32-bit `unsigned long` under MSVC, so its rows wrap from order 35; nanoql's are 64-bit, exact to order 67, Linux QuantLib's values. QuantLib's values are its own on the wheel's platform.

Facts:

- `FromOrderThirtyFiveTheRowsAreExact`
- `EachRowIsTheBinomialCoefficients`

## PathGeneratorProcessTests

[`Divergences/PathGeneratorProcessTests.cs`](../tests/NanoQL.Tests/Divergences/PathGeneratorProcessTests.cs)

QuantLib's `PathGenerator` takes any `StochasticProcess` and casts it to `StochasticProcess1D` (`ext::dynamic_pointer_cast` in `pathgenerator.hpp`): a process that isn't 1-D casts to null, which QuantLib keeps, and its first draw dereferences it (`process_->x0()`). nanoql refuses such a process when the generator is built, in both constructors, with an `ArgumentException` naming the process.

Facts:

- `AProcessThatIsNotOneDimensionalIsRefused`

## PathwiseDeltaTests

[`Divergences/PathwiseDeltaTests.cs`](../tests/NanoQL.Tests/Divergences/PathwiseDeltaTests.cs)

The pathwise delta engine's recursion and layout. QuantLib's Euler-step Jacobian reads the drift's derivative at the step's end, where the Euler drift is taken at its start (`pathwiseaccountingengine.cpp:167`), and drops the displacement from the rates' ratios and the drift term (`:169-170`, `:285`); its results put product i's deltas at (i + 1)·P + j, which overwrites another product's when P ≠ R (`:306`). On caplets at the forwards, FlatVol 20%, 3 factors, the money-market measure, the Mersenne twister (seed 42), 1,023 paths: six semi-annual rates at displacements 0 and 2%, six unequal periods at 2%, and the semi-annual rates paid 0.2 before their periods' ends, which takes the discounter's bracket through the engine: nanoql's deltas are the same-path differences of the engine's price, whose bits are QuantLib's where the payments are on rate times; QuantLib's are off by its recursion. Checks: same-path central differences of the price; each product alone.

Facts:

- `TheDeltasAreThePricesSamePathDifferences`
- `TwoCapsOnNineRatesEachHoldTheirOwnDeltas`

## PathwiseVegaTests

[`Divergences/PathwiseVegaTests.cs`](../tests/NanoQL.Tests/Divergences/PathwiseVegaTests.cs)

The pathwise vega engines: the delta engine's recursion, the pseudo-root Jacobian's drift sum and displacement, and the Jacobian's one-step discount factors, which QuantLib's engines take at the step's end (`pathwiseaccountingengine.cpp:494`, `:910`) where its own test takes them at the start (`marketmodel.cpp:2899-2900`). On caplets at the forwards, FlatVol 20%, 3 factors, the money-market measure, the Mersenne twister (seed 42), 1,023 paths, one bump a step (factor 0 of every alive rate): six semi-annual rates at displacements 0 and 2%, six unequal periods at 2%, and the semi-annual rates paid 0.2 before their periods' ends: nanoql's vegas, from both engines, are the same-path differences of the price in the pseudo-roots; QuantLib's are off. Checks: same-path central differences of the price, the pseudo-roots bumped along the bump; the two engines against each other.

Facts:

- `TheVegasAreThePricesSamePathDifferences`

## PearsonNegativeStrikePutTests

[`Divergences/PearsonNegativeStrikePutTests.cs`](../tests/NanoQL.Tests/Divergences/PearsonNegativeStrikePutTests.cs)

Pearson's spread engine where the conditional strike, the second forward at a node plus the strike, is zero or less. QuantLib's integrand takes the call's intrinsic value there whatever the type (`pearsonspreadengine.cpp:73-75`), so a put prices that intrinsic (136.84 on the test's market where Choi's engine gives 0.00304, Deng-Li-Zhou's 0.00303 and a quadrature 0.00313). nanoql's put takes its conditional value, 0 there: Choi's engine and the put-call parity, which QuantLib's put breaks, are the checks. QuantLib's master makes the same fix after the pin (#2955, `8d98cbd3c`), its cases below as a check of nanoql's.

Facts:

- `APutAtNegativeConditionalStrikesMeetsChoi`
- `PutCallParityHoldsAtNegativeConditionalStrikes`
- `TestPearsonSpreadEngineNegativeStrikes`
- `TestPearsonSpreadEngineVanillaLimit`

## PeriodEqualityTests

[`Divergences/PeriodEqualityTests.cs`](../tests/NanoQL.Tests/Divergences/PeriodEqualityTests.cs)

Period equality never throws: QuantLib's `==` is `!(p1 < p2 || p2 < p1)` and throws where the order can't be decided (1M against 30D); nanoql's returns `false` there. Ordering still throws, as QuantLib's does.

Facts:

- `UndecidablePairsCompareUnequalWithoutThrowing`
- `OrderingStillThrowsOnUndecidablePairs`
- `CompareToFollowsQuantLibsOrderingWhereItIsDecidable`
- `EveryZeroLengthPeriodIsEqualWithOneHash`
- `ExactlyComparablePeriodsCompareNormalisedWithEqualHashes`
- `PeriodsHashConsistentlyInHashSets`

## PeriodNegationTests

[`Divergences/PeriodNegationTests.cs`](../tests/NanoQL.Tests/Divergences/PeriodNegationTests.cs)

A period of length `int.MinValue` has no negation. QuantLib's unary minus negates the length (`period.hpp:156`) and `frequency()` takes its absolute value (`period.cpp:71`), both overflowing there, which is undefined; nanoql refuses both with `NanoQLException`, where it used to wrap and to throw `OverflowException`. An ex-coupon, last-recent or tenor period reaches them.

Facts:

- `NegatingTheMostNegativeLengthIsRefused`
- `TheFrequencyOfTheMostNegativeLengthIsRefused`

## PeriodTextTests

[`Divergences/PeriodTextTests.cs`](../tests/NanoQL.Tests/Divergences/PeriodTextTests.cs)

QuantLib's short period text throws "unknown time unit" for milliseconds and microseconds (`period.cpp`). `PeriodFunctions.ShortPeriod` keeps that throw, while `Period.ToString`, which .NET expects never to throw, falls back to a text of its own. A unit outside the enum can't be constructed.

Facts:

- `ToStringNeverThrows`
- `ShortPeriodThrowsAsQuantLibDoes`

## PerpetualFuturesDivergentTailTests

[`Divergences/PerpetualFuturesDivergentTailTests.cs`](../tests/NanoQL.Tests/Divergences/PerpetualFuturesDivergentTailTests.cs)

The perpetual futures engine's tail past its horizon. QuantLib refuses only a non-positive last funding rate (`discountingperpetualfuturesengine.cpp:86-88`), while its flat tail converges only where k + r_f − r_d > 0 (continuous funding) or (1 + k)·e^{Δt(r_f − r_d)} > 1 (discrete); below that its closed form returns the divergent series' analytic continuation: on QuantLib's case market (r_d 4%, r_f 2%, interest-rate difference 0.005) continuous funding at k 0.01 prices −4,999.998 and quarterly funding at k 0.004 a plausible 9,876.34; and where r_f > r_d it refuses a non-positive rate whose tail converges. nanoql refuses exactly where the tail diverges, a discrete rate of −1 or below among them, and prices a convergent tail at any rate. The checks: the refusals' type and text; Ackerer, Hugonnier and Jermann's constant-rate closed forms, QuantLib's own case's.

Facts:

- `AContinuousDivergentTailIsRefused`
- `ADiscreteDivergentTailIsRefused`
- `ADiscreteFundingRateOfMinusOneOrBelowIsRefused`
- `ANonPositiveFundingRateOnAConvergentTailIsPriced`

## PerpetualFuturesMismatchedTimesTests

[`Divergences/PerpetualFuturesMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/PerpetualFuturesMismatchedTimesTests.cs)

The perpetual futures engine on a curve dated apart (the playbook's row for structures on another clock). QuantLib reads its curves at the grid's times, the instrument's day counter's from the evaluation date (`discountingperpetualfuturesengine.cpp:154-155`, `:172-177`), so a domestic curve settling two days on is read as though its date were today's. nanoql reads each curve at the instants the grid's times denote, nothing accruing before its own date. The check is nanoql on the curve restated from the evaluation date: a discount of 1 to the curve's date and its rate beyond, which a log-linear discount curve on Actual/Actual (ISDA), whose year fractions add, holds exactly.

Facts:

- `ADomesticCurveSettlingLaterIsReadFromItsOwnDate`

## PerturbativeBarrierEngineTests

[`Divergences/PerturbativeBarrierEngineTests.cs`](../tests/NanoQL.Tests/Divergences/PerturbativeBarrierEngineTests.cs)

The perturbative barrier engine's distribution helpers and second-order derivatives (fixed; the helpers internal, read through reflection). QuantLib's port of Genz's TVPACK has three lines that aren't Genz's: Plackett's integrand takes √(BA − R·BB) where Genz squares it (`perturbativebarrieroptionengine.cpp:1260`; upstream's own fix after v1.44, #2935, which re-caches QuantLib's two order-2 cases at 0.8943754 where v1.44 cached its defective code's 0.8943769); the Student branch belongs to the degrees-of-freedom test, where QuantLib's attaches it to the normal integrand's cut (`:1261-1268`), so a normal integrand past the cut returns 0.5 or NaN, not 0; and the bivariate normal's |ρ| ≥ 0.925 branch takes (1 − x²)² where Genz's `BVND` takes √(1 − x²) (`:1398`). QuantLib's trivariate normal misses an independent one by up to 0.0129 at six points (0.247037493575 against 0.253775665773, 0.051555378193 against 0.049633455514, 0 against 0.012895124168, 0 against 0.006207519614, 0.144908828458 against 0.144207845916, 0.001686899450 against 0.001348181042) and by up to 0.92 at 1,500 random ones; QuantLib's bivariate normal misses QuantLib's own We04DP by up to 5.1e-3 (QuantLib 1.44). The second-order term's derivative helpers aren't their functions' derivatives in a: the F helper's factor reads τ − p where its function reads τ − s, and the E helper's two normal limits carry a stray inner square root and its x-derivative of Φ₂(x, y) − Φ(x) the wrong sign. nanoql takes Genz's three lines and derives the helpers term by term from ∂Φ₂(h, k; ρ)/∂h = φ(h) Φ((k − ρh)/√(1 − ρ²)). Checks: We04DP; a trivariate normal integrated on 50 panels of 64-point Gauss-Legendre; five-point differences; the finite-difference barrier engine; the analytic barrier engine on a flat market.

Facts:

- `OrderTwoIsQuantLibMastersCacheNotItsV144Cache`
- `TheBivariateNormalIsWe04DPPastPointNineTwoFive`
- `TheTrivariateNormalIsGenzsAtThePlannersPoints`
- `TheTrivariateNormalIsGenzsAtFifteenHundredPoints`
- `TheSecondOrderHelpersAreTheirFunctionsDerivatives`
- `OrderTwoApproachesTheFiniteDifferencesOnAStronglyTimeDependentMarket`
- `OnAFlatMarketEachOrderIsTheAnalyticBarrierPrice`

## PiecewiseIntegralNudgeTests

[`Divergences/PiecewiseIntegralNudgeTests.cs`](../tests/NanoQL.Tests/Divergences/PiecewiseIntegralNudgeTests.cs)

The piecewise integral's nudges at critical points at or below 0 (fixed). QuantLib's nudges each piece's ends inward by multiplying by 1 + ε, which moves a negative point the wrong way and 0 not at all, so on the two-interval segment rule a step from 0 to 1 at −1 integrates to 3.5 over [−3, 3], exactly 4, and at 0 to 3.75, exactly 3; at +1 QuantLib's is exact. nanoql's nudge is mirrored below 0 and is the next double at 0, so every piece excludes its critical points and the rule is exact on each constant piece.

Facts:

- `AStepIntegratesExactly`
- `QuantLibsNudgesMissTheSteps`

## PiecewiseYoYVolatilityNodesTests

[`Divergences/PiecewiseYoYVolatilityNodesTests.cs`](../tests/NanoQL.Tests/Divergences/PiecewiseYoYVolatilityNodesTests.cs)

A bootstrapped year-on-year volatility curve's nodes from its dates and data (QuantLib's master's #2941, taken ahead of the pin). QuantLib 1.44's `nodes()` returns a list only the data constructor fills (`yoyinflationoptionletvolatilitystructure2.hpp`), so a `PiecewiseYoYOptionletVolatilityCurve`'s is empty: on QuantLib 1.44 (QuantLib's `testYoYPriceSurfaceToVol` with #2941's inspectable stripper) each of the 11 stripped curves has 0 nodes beside 8 dates and 8 data. nanoql's, as master's, are its dates with its data; #2941's own check is in `TestSuite/InflationVolatilityTests.cs`.

Facts:

- `AStrippedCurvesNodesAreItsDatesAndData`

## PolarStudentTSamplerTests

[`Divergences/PolarStudentTSamplerTests.cs`](../tests/NanoQL.Tests/Divergences/PolarStudentTSamplerTests.cs)

The polar Student t factor sampler draws its factors from one uniform stream in turn, each scaled to unit variance: QuantLib's gives each t generator its own copy of one uniform generator, so the systemic draw k equals the idiosyncratic stream's draw k, and leaves its draws raw, of variance ν/(ν − 2) (`latentmodel.hpp:779-794`, `polarstudenttrng.hpp:86`). QuantLib's values are recorded in the test (ν 5, 5, weight √0.3, 200,000 paths at seed 42); the checks, which QuantLib's sampler fails, are each factor's sample variance within three standard errors of 1, the simulated default frequency against the model's probability and the inversion sampler's, and the systemic draws against the idiosyncratic stream's.

Facts:

- `TheFactorsAreOfUnitVariance`
- `TheDefaultFrequencyIsTheModels`
- `TheSystemicDrawsArentTheIdiosyncraticStreams`

## PoolKeyOrderTests

[`Divergences/PoolKeyOrderTests.cs`](../tests/NanoQL.Tests/Divergences/PoolKeyOrderTests.cs)

A pool's default keys, read in the pool's order with each name's own key: QuantLib's `defaultKeys()` lists them in name order (`pool.cpp:77-82`), so names C and A entering with keys KA and KB read C's probability on KB's curve; and its `clear()` keeps the cleared names' keys (`pool.cpp:34-38`), so a name added after it reads a cleared name's key. QuantLib's values are recorded in the test; the checks are QuantLib on the pool added in name order, and on a fresh pool.

Facts:

- `EachNameReadsItsOwnKey`
- `AClearedPoolHoldsNoKey`

## PremiumAdjustedPutStrikeTests

[`Divergences/PremiumAdjustedPutStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/PremiumAdjustedPutStrikeTests.cs)

The premium-adjusted puts beyond the non-adjusted delta's range (fixed). `deltaFromStrike` returns a premium-adjusted put delta whose size exceeds the foreign discount factor (spot) or 1 (forward), and `strikeFromDelta` refuses it through its non-adjusted guess (`blackdeltacalculator.cpp:102-111`, `:143-147`): with spot 1, discount factors 0.99 and 0.97 and a standard deviation of 0.2, the strikes 1.4, 2.0 and 3.0 give deltas QuantLib refuses as "Spot delta out of range." and "Forward delta out of range." (QuantLib-Python 1.44). nanoql starts QuantLib's Brent from the forward on QuantLib's own bracket, `[0, 100·spot]` (`:150`), where the guess is out of range. QuantLib's check: its own `deltaFromStrike` at the solved strike, and the strikes the deltas came from.

Facts:

- `ASpotPremiumAdjustedPutsStrikeIsSolved`
- `AForwardPremiumAdjustedPutsStrikeIsSolved`

## PricingPeriodTests

[`Divergences/PricingPeriodTests.cs`](../tests/NanoQL.Tests/Divergences/PricingPeriodTests.cs)

A daily schedule's periods counting both their ends (fixed). QuantLib's `createPricingPeriods` gives a period `[start, start + 1M − 1]` the quantity `perDay × (end − start)`, one day short: 10 barrels a day from January to March 2025 makes 300, 270 and 300 barrels where the months have 31, 28 and 31 days. nanoql's is the daily quantity times the period's days, FpML's `PerCalendarDay` ("the Quantity or Notional Quantity is deemed to apply once per Calendar Day"): 310, 280 and 310. Check: each period's quantity is the sum of its days' quantities.

Facts:

- `ADailyPeriodsQuantityIsItsDaysTimesTheDailyQuantity`

## ProcessMismatchedTimesTests

[`Divergences/ProcessMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/ProcessMismatchedTimesTests.cs)

The Black-Scholes and Heston processes on structures that measure time differently. A process's times are the risk-free curve's (`blackscholesprocess.cpp:149-152`), and QuantLib reads the dividend curve, the volatility and the local volatility at those times (`blackscholesprocess.cpp:65-130`, `hestonprocess.cpp:474-475`), each of which measures time from its own reference date in its own day counter: on a dividend curve and a volatility on Act/360 beside a risk-free curve on Act/365, the Black-Scholes process evolves a year 2.8e-4 away from the same market restated on one day counter. nanoql reads each structure at the instants the times denote, on its own clock, as the finite-difference engine does (`FdmMismatchedTimesTests`): QuantLib's process on the market restated on the risk-free curve's day counter is the check. A structure accrues nothing before its own reference date, as QuantLib's analytic engines count it; the days before the process's time 0, the risk-free curve's reference date, aren't the process's, but an engine's that runs from the earliest date, as the finite-difference engine does. Every read is at the instants, so the exact evolve and the discretization's agree. A structure on the risk-free curve's day counter and reference date is read as QuantLib reads it.

Facts:

- `TheBlackScholesProcessReadsEachStructureAtItsInstants`
- `TheHestonProcessReadsTheDividendCurveAtItsInstants`
- `EachStructureAccruesNothingBeforeItsOwnDate`

## ProxyGreekVegaTests

[`Divergences/ProxyGreekVegaTests.cs`](../tests/NanoQL.Tests/Divergences/ProxyGreekVegaTests.cs)

The proxy Greeks. QuantLib's constrained evolver weighs a path by the shifted Gaussians' density ratio alone (`lognormalfwdrateeulerconstrained.cpp:200-212`), which misses the transition densities' ratio of step deviations a volatility bump changes: `testGreeks`' proxy vegas, which it prints and doesn't assert, take the wrong sign, 81 to 131 standard errors off Black's. nanoql's evolver takes the unbumped model as its reference and carries √(var_reference / var_own). On six semi-annual rates, FlatVol 20%, 3 factors, digital caplets paying 1% at the forwards, the money-market measure, 8,191 Sobol paths (seed 42), each step's forward constrained, forwards bumped 1e-6 and volatilities 1e-4: nanoql's deltas, gammas and vegas within 4 standard errors of Black's; QuantLib's weight, the evolvers' own models as references, its vegas far off. Checks: Black's formula on each caplet's total variance; QuantLib's accounting engine on the volatility-bumped models, the same paths, on plain caplets; QuantLib's weight bit for bit where the reference is the evolver's own model (the evolvers' and the proxy engine's vectors).

Facts:

- `TheProxyGreeksAreBlacksWithinTheirErrors`
- `TheProxyVegasAreTheAccountingEnginesSamePathDifferences`

## ProxyIborTests

[`Divergences/ProxyIborTests.cs`](../tests/NanoQL.Tests/Divergences/ProxyIborTests.cs)

`ProxyIbor`'s forecast, the gearing times the proxied fixing plus the spread, and its coupons forecasting through it (fixed). QuantLib's `forecastFixing` returns gearing × proxied × spread, and an Ibor coupon on the proxy forecasts from the proxy's forwarding curve, which it lacks. On QuantLib 1.44 (gearing 2 on Euribor 6M, spread 0.001) the proxied fixing 0.0298114585668 forecasts 5.96229171335e-05 for 0.0606229171335, and the coupon's rate refuses "null term structure set to this instance of PX6M Actual/360". nanoql's forecast is gearing × proxied + spread, and the coupon's rate is it.

Facts:

- `TheForecastIsTheGearedProxiedFixingPlusTheSpread`
- `ACouponOnTheProxyForecastsThroughIt`

## PseudoSqrtEmptyMatrixTests

[`Divergences/PseudoSqrtEmptyMatrixTests.cs`](../tests/NanoQL.Tests/Divergences/PseudoSqrtEmptyMatrixTests.cs)

An empty matrix's pseudo square root is refused with QuantLib's master's text, "matrix must not be empty", before the decomposition reads it (#2846, f3f789067), where QuantLib 1.44 refuses it inside the symmetric Schur decomposition with "null matrix given" (symmetricschurdecomposition.cpp:29).

Facts:

- `AnEmptyMatrixIsRefusedBeforeItsDecomposition`

## QdFpTanhSinhSchemeTests

[`Divergences/QdFpTanhSinhSchemeTests.cs`](../tests/NanoQL.Tests/Divergences/QdFpTanhSinhSchemeTests.cs)

The QD fixed-point engine's tanh-sinh scheme with no fixed-point step. QuantLib's Gauss-Legendre scheme refuses m = 0 ("at least one fixed point iteration step is needed", `qdfpamericanengine.cpp:44`), but its tanh-sinh scheme checks nothing, and its m − 1 naive steps wrap to 2⁶⁴ − 1, so the engine never ends (QuantLib-Python 1.44 in a child process, killed after 30 seconds). nanoql refuses m = 0 with the Gauss-Legendre scheme's text. With no node QuantLib's engine refuses when it prices ("not enough points to interpolate: at least 2 required, 1 provided"), and nanoql's likewise.

Facts:

- `NoFixedPointStepIsRefused`
- `NoNodeIsRefusedWhenPricing`

## QdMismatchedTimesTests

[`Divergences/QdMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/QdMismatchedTimesTests.cs)

The QD+ and QD fixed-point engines on structures counted differently. QuantLib reads the volatility as `blackVol(T, K)` at the risk-free curve's time (`qdplusamericanengine.cpp:179-184`), so a volatility on Act/360 beside curves on Act/365 is read at a time its variance doesn't run over: about −0.6% on a one-year option. nanoql reads σ² as the volatility's variance at the maturity date over the engine's time, and r and q from each curve's discount over it. QuantLib's check: its own engines on the market restated on one day counter, the volatility scaled by √(365/360) so its variance to the maturity is the original's (QuantLib-Python 1.44). Where the structures share their counts, QuantLib's bits (the engines' vectors).

Facts:

- `QdPlusReadsTheVolatilitysOwnVariance`
- `QdFixedPointReadsTheVolatilitysOwnVariance`

## QuantityRoundingTests

[`Divergences/QuantityRoundingTests.cs`](../tests/NanoQL.Tests/Divergences/QuantityRoundingTests.cs)

Quantity's conversions unrounded. QuantLib's `convertTo` rounds every converted quantity to its target unit's rounding (`quantity.cpp:33-40`), and every unit's is `Rounding(0)`, to the closest whole unit, which its public constructor can't change: under automated conversion 2 MB == 1,500 BBL is true and 1,500 BBL == 2 MB false, 2 MB / 1,500 BBL is 1, 2 MB + 1,500 BBL is 4 MB and 1 BBL + 20 GAL is 1 BBL. nanoql's units round nothing, QuantLib's default `Rounding()`: the comparisons are symmetric and the ratio and sums exact, 1.333…, 3.5 MB and 1.476… BBL.

Facts:

- `EqualityIsSymmetric`
- `TheRatioIsExact`
- `TwoThousandBarrelsAndFifteenHundredAreThreeAndAHalf`
- `ABarrelAndTwentyGallonsAreOnePointFourSevenSixBarrels`
- `EveryUnitsRoundingLeavesAnAmountAsItIs`

## QuantoGreeksTermStructureTests

[`Divergences/QuantoGreeksTermStructureTests.cs`](../tests/NanoQL.Tests/Divergences/QuantoGreeksTermStructureTests.cs)

The quanto engine's derived Greeks. QuantLib takes the rho, vega, qrho, qvega and qlambda from the inner dividend rho by one factor at expiry each (`quantoengine.hpp:140-171`), assuming the input moves the quanto zero rate in parallel. With the dividend curve on Act/360, whose clock the quanto curve keeps, a shift of the risk-free rate moves it by the ratio of the two curves' times: rho −18.4754 where the price under the shift moves −17.2347, the quanto Greeks 1.4% off. For a forward-start quanto, whose forward engine reads the quanto curve at the reset and at expiry, an input whose contribution varies in time moves the two unevenly: on a volatility curve (0.10, 0.20, 0.30 at one, two and three years) QuantLib's qvega and qlambda are 25.5% short of its own price bumped in the FX volatility and the correlation, and under an FX volatility curve the vega 3.5% over. nanoql reads each input's change to the quanto curve's log discount by date at the dates the inner engine reads. The checks: on Act/360 dividends, the market restated onto Act/365, where QuantLib's formula is exact; on the curves, QuantLib's own prices bumped by 1e-5 each way in each input.

Facts:

- `ADividendCurveOnItsOwnDaysMovesTheDriftByDate`
- `AForwardStartQuantoOnAVolatilityCurveMeetsQuantLibsBumps`
- `AForwardStartQuantoOnAnExchangeRateVolatilityCurveMeetsQuantLibsBumps`

## QuantoSmileTests

[`Divergences/QuantoSmileTests.cs`](../tests/NanoQL.Tests/Divergences/QuantoSmileTests.cs)

The quanto engine on a skew. QuantLib reads the underlying's volatility for the drift at the payoff's strike, but for `qvega` and `qlambda` at the spot (`quantoengine.hpp:155-166`), so on a skew of 0.30 at strike 60 to 0.10 at 140 (FX volatility 10%, correlation 0.3, a year) they miss its own price bumped in the FX volatility and the correlation by −27.6% at strike 70 and +56.9% at 130, calls and puts alike, exact at 100; and for a quanto forward-start option it reads the drift at the payoff's strike, which the forward engine never uses, where it strikes at the moneyness times the spot (3.1098 at payoff strike 0, 3.2384 at 100, 3.2452 at 105, the moneyness times the spot). nanoql reads both at the drift's strike, the forward's at the moneyness times the spot. The checks: the price bumped in each quote, the prices QuantLib's bits; QuantLib's quanto forward with its payoff struck at the moneyness times the spot, bit for bit, from any payoff strike.

Facts:

- `QvegaIsThePriceBumpedInTheExchangeRateVolatility`
- `QlambdaIsThePriceBumpedInTheCorrelation`
- `TheQuantoForwardDriftIsReadAtTheForwardEnginesStrike`

## QuantoTermStructureDayCounterTests

[`Divergences/QuantoTermStructureDayCounterTests.cs`](../tests/NanoQL.Tests/Divergences/QuantoTermStructureDayCounterTests.cs)

`QuantoTermStructure` on structures with different day counters. QuantLib reads every structure at the time the dividend curve's day counter measures, with its own comment that this assumes one day count and "It should be QL_REQUIREd" (`quantotermstructure.hpp:124-131`): two risk-free curves equal at every date, one on Actual/365 (Fixed) and one on Actual/360, give quanto zero rates of 5.0000% and 4.9589%. nanoql reads each structure at the instants the curve's times denote, on its own clock, so both give QuantLib's value on one day counter; QuantLib's case (`testFDMQuantoHelper`, Actual/360 throughout) is QuantLib's arithmetic.

Facts:

- `StructuresOnDifferentDayCountersAreReadAtTheirInstants`

## QuoteSentinelTests

[`Divergences/QuoteSentinelTests.cs`](../tests/NanoQL.Tests/Divergences/QuoteSentinelTests.cs)

A quote is a `double`, so a quote equal to QuantLib's null sentinel, `Null<Real>()` (3.4028234663852886e38, the largest `float`), is a value. QuantLib's quotes are `SimpleQuote`s, and a `SimpleQuote` holding the sentinel is invalid: a bootstrap refuses a helper quoting it ("has an invalid quote", `iterativebootstrap.hpp:236-240`), and a curve refuses such a jump ("invalid 1st jump quote", `yieldtermstructure.cpp:86-87`). nanoql has no invalid quote: a helper quoting it is bootstrapped, which fails in the solver as any unreachable quote does, and such a jump is applied.

Facts:

- `AQuoteEqualToQuantLibsNullIsAValue`

## RandomModelBindingTests

[`Divergences/RandomModelBindingTests.cs`](../tests/NanoQL.Tests/Divergences/RandomModelBindingTests.cs)

A random model bound to two baskets in turn: each binding is a fresh model's simulation. QuantLib's model keeps its first basket's paths: set on three names at 2% then on three at 5%, it reads 3.447 on both, where a fresh model reads 8.007 on the second.

Facts:

- `EachBindingSimulatesItsBasket`

## RandomModelStatisticsTests

[`Divergences/RandomModelStatisticsTests.cs`](../tests/NanoQL.Tests/Divergences/RandomModelStatisticsTests.cs)

The random models' statistics where QuantLib's misread their paths, each checked on the paths themselves or against an exact model; QuantLib's values: the default correlation, Pearson's on the paths: QuantLib divides the joint frequency by n − 1, a name with itself reading 1.0002212831946919 on 5,000 paths; the random loss model's recovery of 256/256: QuantLib's 8 bits hold it as 0, a full loss, reading an expected loss of 5.775 at recoveries of 0.9995; the horizon, a parameter: QuantLib's 4,050 days stop the expected loss of ten names at 3% and correlation 0.3 at 16.9836 at 12 and 20 years, where its recursive model reads 18.149740938526683 and 27.0848313951936; the n-th event, every default of a day counted: QuantLib keys its order by day, so on the random-default vector's whole pool the n-th event's probabilities sum to 0.1685 and 0.071 where at least 4 and 5 events read 0.169 and 0.072.

Facts:

- `TheCorrelationIsThePathsPearsons`
- `AFullRecoveryLosesNothing`
- `TheHorizonIsAParameter`
- `TheNthEventCountsEveryDefaultOfADay`

## RandomizedLdsTests

[`Divergences/RandomizedLdsTests.cs`](../tests/NanoQL.Tests/Divergences/RandomizedLdsTests.cs)

The randomized sequence's modulo: QuantLib wraps a coordinate's sum only where it exceeds 1.0, so a sum of exactly 1.0 stays outside [0, 1); nanoql wraps it to 0.0. With a low-discrepancy and a pseudo-random stand-in each returning 0.5 in every coordinate, QuantLib's point is (1, 1, 1), nanoql's (0, 0, 0). Elsewhere QuantLib's values, bit for bit (the harness vectors).

Facts:

- `ASumOfExactlyOneWrapsToZero`

## RangeAccrualLegTests

[`Divergences/RangeAccrualLegTests.cs`](../tests/NanoQL.Tests/Divergences/RangeAccrualLegTests.cs)

The range accrual leg's null entries: QuantLib's `RangeAccrualLeg` builds `Leg leg(n)`, n null cash flows, then pushes its n coupons after them (`rangeaccrual.cpp`, its `operator Leg`), so the leg holds 2n flows, the first n null, which setCouponPricer and every reader of the leg dereference: on the wheel, the vectors' semiannual leg of four periods has eight flows, and setCouponPricer refuses it ("Boost assertion failed: px != 0"). nanoql's leg holds the n coupons. QuantLib's check: its coupons, the leg's second half, bit for bit (the vectors).

Facts:

- `TheLegHoldsItsCouponsAlone`

## RangeAccrualObservationTenorTests

[`Divergences/RangeAccrualObservationTenorTests.cs`](../tests/NanoQL.Tests/Divergences/RangeAccrualObservationTenorTests.cs)

A range accrual with no observation dates: QuantLib's `RangeAccrualLeg` defaults its observation tenor to `Period()`, so each coupon's observation schedule holds its start and end alone, and the BGM pricer divides a sum over no observations by their number, 0/0: on the wheel, the vectors' semiannual leg built without a tenor pays a rate of NaN, and a coupon whose observation schedule holds its start and end alone pays NaN on each of the pricer's three routes. nanoql's leg refuses a floating coupon with no observation tenor, and its pricer a coupon with no observation dates, each with a text naming what is missing.

Facts:

- `ALegWithoutAnObservationTenorIsRefused`
- `ACouponWithNoObservationDatesIsRefused`

## RangeAccrualStartedCouponTests

[`Divergences/RangeAccrualStartedCouponTests.cs`](../tests/NanoQL.Tests/Divergences/RangeAccrualStartedCouponTests.cs)

A range accrual in progress: QuantLib's BGM pricer prices an observation from its expiry, so a coupon whose start is past is priced on QuantLib's call-spread route and smile correction with the before-fixing phase clamped (`rangeaccrual.cpp:403-423`, `:474-478`), but its no-smile route takes the phase unclamped (`:365-372`), and an observation whose expiry is past is priced by no route (NaN): on the wheel, on the C++ range accrual vectors' Euribor 6M, curve and fixings, a weekly coupon from 10 January 2025 (its first observation on 17 January, fixing on the evaluation date, 15 January) pays 0.033335938211808636 on the no-smile route at volatilities 10% and 30% and 0.03337731105469358 on the call-spread route, and the no-smile route refuses at 30% and 10% (0.03465775920672742 by call spread); a coupon from 19 December 2024 refuses on every route. nanoql's no-smile route clamps the phase as the other two do, and an observation whose fixing is known, its fixing date before the evaluation date, or on it once published, is counted by its fixing f, the deflator times 1{f > l} − 1{f > u}, the two digitals QuantLib's range reads, its date past or not. QuantLib's checks: its call-spread route, within that route's rounding; its pricer on the coupon restated with only its observations not yet known (the same start, end and fixings), scaled by their share, plus the known part, with the 19 December coupon's fourth known observation dated after the evaluation date (16 January, fixing on 14 January), and a coupon from 9 January whose one known observation is that one, which QuantLib's pricer takes as a digital on a day's expiry, 0.014901944616257642 at 2.92% to 4% where the known fixing gives 0.015369074793388015; the 19 December coupon below 3.2%, from 2.6% (known fixings 3.3022%, above, 2.589% and 2.5592%, below, and 2.9258%) and from 0, a lower trigger at or below the pricer's ε/2, which QuantLib's digital takes as 1 (`:347`); on a coupon's start date, QuantLib's values on every route; and the smile-correction route, which reads the no-smile digital (`:474-478`), so QuantLib's equals its unclamped no-smile route on the 10 January coupon, 0.033335938211808636 at 10% and 30%, and refuses at 30% and 10%, where nanoql's equals its clamped one, the correction 0 once the start is past.

Facts:

- `BeforeItsFirstObservationTheNoSmileRouteIsTheCallSpreadRoutes`
- `KnownObservationsAreCountedByTheirFixings`
- `AnObservationFixedBeforeItsDateIsKnown`
- `KnownObservationsOutOfTheirRangeCountNothing`
- `BeforeItsFirstObservationTheSmileCorrectionRouteIsTheNoSmileRoutes`
- `OnItsStartDateEveryRouteIsQuantLibs`

## RankReducedSqrtNegativeEigenvalueTests

[`Divergences/RankReducedSqrtNegativeEigenvalueTests.cs`](../tests/NanoQL.Tests/Divergences/RankReducedSqrtNegativeEigenvalueTests.cs)

`rankReducedSqrt` with a retained negative eigenvalue: QuantLib floors negative eigenvalues under `Spectral` only (`pseudosqrt.cpp:496`), so the eigenvalues `None` accepts down to −1e-16 (`:486-491`) and those of Higham's adjusted matrix, positive semi-definite only to its projections' tolerance (`:498-505`), reach `std::sqrt(eigenValues[i])` (`:531`): a NaN column, and `normalizePseudoRoot` skips every row whose norm is NaN (`:64`), leaving the others unnormalized. The port floors the root at 0. QuantLib's check: its own root at the rank that keeps the non-negative eigenvalues, with zero columns appended, bit for bit (the harness's).

Facts:

- `HighamsNegativeAdjustedEigenvalueGivesAZeroColumn`
- `NonesRoundOffNegativeEigenvalueGivesAZeroColumn`

## RateHelperNullIndexTests

[`Divergences/RateHelperNullIndexTests.cs`](../tests/NanoQL.Tests/Divergences/RateHelperNullIndexTests.cs)

A rate helper given a null index is refused on entry, naming the parameter. QuantLib's helpers dereference their index before any check (`ratehelpers.cpp:198, 206` for the deposit helper, `:533` for the swap helper, `:148` for the futures helper, `:312, 332, 350` for the FRA helper, `:469` for the swap helper on a swap index, `:688, 694` for the BMA swap helper, `oisratehelper.cpp:114` for the OIS helper), a result C++ leaves undefined; the wheel stops on Boost's assertion.

Facts:

- `AHelperGivenANullIndexIsRefused`
- `AFuturesFraOrOisHelperGivenANullIndexIsRefused`

## RateHelperUpstreamFixesTests

[`Divergences/RateHelperUpstreamFixesTests.cs`](../tests/NanoQL.Tests/Divergences/RateHelperUpstreamFixesTests.cs)

The fix QuantLib's master made to the rate helpers after the pin, which nanoql takes: #2899 (`46ef4b7b6`), the deposit's and the indexed FRA's implied quote forecast from the curve being bootstrapped, `forecastFixing`. QuantLib 1.44 reads `fixing(fixingDate, true)`, which takes a stored fixing for a fixing date before the evaluation date (`ratehelpers.cpp:210-215`, `:360-364`): on a weekend or holiday evaluation date the helpers' fixing date is the business day before it, so a bootstrap without that day's fixing throws, and one with it quotes the stored fixing in place of the curve's forecast. The C++ vectors' three deposits whose fixing precedes 15 January 2024, Martin Luther King Day on the US SOFR calendar, or a given Friday, leave their implied quotes out, two of them below.

Facts:

- `TestWeekendDepositAndIndexedFraForecast`
- `ADepositFixingBeforeAHolidayEvaluationDateForecastsItsQuote`

## RatePseudoRootJacobianTests

[`Divergences/RatePseudoRootJacobianTests.cs`](../tests/NanoQL.Tests/Divergences/RatePseudoRootJacobianTests.cs)

The analytic pseudo-root Jacobians. The derivative of an Euler step's end rate j along rate k's root carries the drift's sum e_j = Σ_{k<j} τ_k (f_k + d_k) P_k a_k and the displaced rate f_j + d_j; QuantLib's sums the terms without τ_k and multiplies by τ_j, right on equal periods only, and carries f_j (`ratepseudorootjacobian.cpp:183`, `:191`, `:197`, `:280`, `:294`, `:298`). On six unequal periods, FlatVol 20%, 3 factors, displacement 2%, the money-market measure, step 0 from the forwards with Gaussians 0.3, −0.5, 1.1, every element bumped 1e-6: nanoql's analytic Jacobians meet the numerical within QuantLib's own test's 5e-11; QuantLib's misses by 1.98e-4. Checks: QuantLib's `RatePseudoRootJacobianNumerical`, its bits.

Facts:

- `TheAnalyticJacobiansAreTheNumerical`

## RecursiveLossModelTests

[`Divergences/RecursiveLossModelTests.cs`](../tests/NanoQL.Tests/Divergences/RecursiveLossModelTests.cs)

The recursive model's percentile and loss distribution, a model set on two baskets and a basket with a realized default. QuantLib's percentile at 0 and 1 returns the distribution's probability, 0.66930409955890324 and 1.0000000003070473 on five names at five years; on two names of notionals 10 and 30 it labels the attainable losses 0, 6, 18 and 24 as 0, 6, 12 and 18, their cumulatives 0.79243383385445709, 0.86063723644644652, 0.9729912416586074 and 1.0000000003070473; a model set on a basket of notionals 10, 20, 30 and then on one of 30, 20, 15 at hazard 0.05 reads 11.950518324119658 where a fresh model reads 7.9670122160797714. The cumulatives integrate a vector over the factor, where QuantLib's quadrature leaves its last node unweighted, so they agree to that node's weight.

Facts:

- `ThePercentileEndsAreLosses`
- `TheDistributionIsLabelledByItsAttainableLosses`
- `AModelSetOnTwoBasketsWeighsEach`
- `ARealizedDefaultLeavesTheLiveNames`
- `ThePercentileAtOneIsATrancheLoss`
- `AShortfallTheDistributionNeverReachesIsRefused`

## RendistatoUnsolvedSwapsTests

[`Divergences/RendistatoUnsolvedSwapsTests.cs`](../tests/NanoQL.Tests/Divergences/RendistatoUnsolvedSwapsTests.cs)

The Rendistato calculator's unsolved swaps (a defect found while porting it): QuantLib's calculator stops at the first swap whose duration exceeds the basket's (`btp.cpp`, `performCalculations`), so the later swaps' rates and durations stay `Null<Real>()` and their yields the solver's starting guess, `swapBondYields_(nSwaps_, 0.05)`: `swapYields()` reports a yield of 5% for a swap it never solved. On the harness, on three BTPs (3% 2029, 3.35% 2030, 3.1% 2031; outstandings 12, 18 and 6; clean prices 99.1, 101.4 and 99.8) on Euribor 6M on a flat 3.1% curve discounted on a flat 2.9% one on 15 January 2025, the basket's duration is 4.878701860912479, the sixth swap's 5.398671198108271, and swaps seven to fifteen have null rates and durations and yields of 0.05. nanoql's rates, yields and durations of the swaps it never solves are null alike. QuantLib's check: its own null rates and durations, which mark the same swaps.

Facts:

- `TheSwapsNeverSolvedHaveNoYield`

## ReplicatingVarianceSwapDividendTests

[`Divergences/ReplicatingVarianceSwapDividendTests.cs`](../tests/NanoQL.Tests/Divergences/ReplicatingVarianceSwapDividendTests.cs)

The replicating variance-swap engine at a nonzero dividend yield. QuantLib's fair variance is 2·r − (2/T)·((S/D − f)/f + ln(f/S)) + Π/D, with r and D the risk-free zero rate and discount (`replicatingvarianceswapengine.hpp:153-175`), as if the dividend yield were zero, where the log contract's replication needs the drift r − q and the forward S·D_q/D_r: at a flat 20% volatility, 5% rates and a 3% yield, QuantLib reads 0.0412007, against 0.0417209 on the equivalent zero-dividend process, whose risk-free discount is D_r/D_q. nanoql's drift is 2·(z_r − z_q) and its forward S·D_q/D_r, the dividend curve read by the maturity date, so it reads the equivalent process's value; at a zero yield it is QuantLib's, and the NPV keeps QuantLib's discounting.

Facts:

- `TheDividendYieldEntersTheDriftAndTheForward`

## ReplicatingVarianceSwapRefusalTests

[`Divergences/ReplicatingVarianceSwapRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/ReplicatingVarianceSwapRefusalTests.cs)

The replicating variance-swap engine's lowest put strike. The portfolio adds an end strike max(k − dk, 0) below the lowest put strike k, and weighs the put at k by the log payoff's slope down to it (`replicatingvarianceswapengine.hpp:94-142`): at or below the step dk the end strike is 0, whose log payoff is infinite, and QuantLib's variance and NPV are NaN. nanoql refuses, "put strikes must be greater than dk"; a put strike above the step prices as QuantLib's.

Facts:

- `APutStrikeAtOrBelowTheStepIsRefused`

## ResultsNullTests

[`Divergences/ResultsNullTests.cs`](../tests/NanoQL.Tests/Divergences/ResultsNullTests.cs)

A result QuantLib leaves null is `null` in nanoql's results records. QuantLib's accessors throw on reading such a result: `NPV()` "NPV not provided", `errorEstimate()` "error estimate not provided", `valuationDate()` "valuation date not provided", and the swap's per-leg accessors "result not available" (`instrument.hpp`, `swap.hpp`).

Facts:

- `UnsetResultsAreNull`
- `TheDiscountingEngineLeavesTheErrorEstimateNull`
- `ALegStartingBeforeTheCurveHasNoStartDiscount`
- `AnEmptyLegHasNoStartOrEndDiscount`
- `AnExpiredSwapHasNoValuationDate`
- `PerLegResultsAnEngineDoesNotReturnAreNull`

## RiskyAssetSwapOptionTests

[`Divergences/RiskyAssetSwapOptionTests.cs`](../tests/NanoQL.Tests/Divergences/RiskyAssetSwapOptionTests.cs)

The risky asset swap option at a zero standard deviation: the formula's limit, the intrinsic value, where QuantLib's reads 0·∞, NaN. On 31 August 2006, the payer swap of nominal 100 at spread 0.01 (an annual fixed and a quarterly float schedule on TARGET to 31 August 2011, Actual/360, recovery 0.4, the discount curve 5% flat, the hazard rate 0.02 flat), the option expiring a year out at a market spread of 0.012 reads 35.967035007653337 at a volatility of 0.2, 0.890356389102676 at 1e-9 and NaN at 0 in QuantLib.

Facts:

- `AZeroVolatilityIsTheIntrinsicValue`
- `AnOptionExpiringTodayIsExpired`

## RiskyAssetSwapTests

[`Divergences/RiskyAssetSwapTests.cs`](../tests/NanoQL.Tests/Divergences/RiskyAssetSwapTests.cs)

The risky asset swap's annuities, recovery integral and reads before the discount curve's date, on 31 August 2006 with the discount curve 5% flat (Actual/360) and the hazard rate 0.02 flat (Actual/365 Fixed), nominal 100, spread 0.01, an annual fixed and a quarterly float schedule on TARGET to 31 August 2011, both legs Actual/360: QuantLib's fixed annuity runs over the float schedule: at recovery 0.4 its fair spread reads 0.011808418663949119 and the package at it 99.57571577267737, not par. Where the schedules coincide, at recovery 0 (whose integral reads nothing), its fair spread is 0.019050924808858876 and its NPV 95.97072563457229, the receiver's at a 4% coupon −96.194787454490481. The recovery value's Euler sum stops a day short of each period's end: on five annual periods of a 5% continuous discount curve and a 0.02 hazard rate (Actual/365 Fixed, recovery 0.4) QuantLib's reads 0.033672884840343142, the closed form R·h/(r + h)·(1 − e^(−(r + h)T)) 0.033765376341026734. On a discount curve from two TARGET days out QuantLib throws ("negative time (-0.0111111) given"); on that curve restated from today, discounting 1 to its date, it reads NPV 95.971197157591376 and fair spread 0.019044839323900529 where the schedules coincide at recovery 0. A seasoned swap's past periods are QuantLib's reads, which refuse a date before the curve's: on 15 January 2026, the curves from today, a swap from 15 March 2024 to 15 March 2030 (annual fixed, quarterly float, no calendar) refuses in QuantLib ("negative time (-1.60833) given"), where reading 1 there would price every past period at par.

Facts:

- `WhereTheSchedulesCoincideTheSwapIsQuantLibs`
- `OnEqualSchedulesWithOtherDayCountersTheFairSpreadIsOverTheFloatAnnuity`
- `ThePackageIsAtParAtTheFairSpread`
- `TheRecoveryValueRunsToEachPeriodsEnd`
- `AT2DiscountCurveReadsAsItsRestatement`
- `ASeasonedSwapsPastPeriodsAreRefused`
- `AnExpiredSwapHasNoFairSpread`

## RomaniaUpstreamHolidaysTests

[`Divergences/RomaniaUpstreamHolidaysTests.cs`](../tests/NanoQL.Tests/Divergences/RomaniaUpstreamHolidaysTests.cs)

Romania's Epiphany and St John the Baptist, January 6th and 7th from 2024, and Orthodox Good Friday from 2018. QuantLib 1.44 has none of them (`romania.cpp:45-78`); QuantLib's master adds them to the public calendar (#2919, `657346746`), and so to the Bucharest stock exchange's, which derives from it, with nine new test lines, which nanoql takes ahead of the pin, as a fix. The lines below are upstream's; QuantLib 1.44's answers are recorded beside them.

Facts:

- `EpiphanyStJohnAndOrthodoxGoodFriday`

## RoughHestonTimeFormTests

[`Divergences/RoughHestonTimeFormTests.cs`](../tests/NanoQL.Tests/Divergences/RoughHestonTimeFormTests.cs)

The rough Heston engine's time form of the price. QuantLib's `priceVanillaPayoff(payoff, Time)` reads both curves at the risk-free curve's time (`analyticroughhestonengine.cpp:594-600`), where its date form reads each by date: on a dividend curve on another day counter the two forms of one maturity price apart. nanoql's time form is on the model's clock, each curve read at the instant the time denotes, so a time and the date it denotes price alike, the date form QuantLib's.

Facts:

- `ATimeAndTheDateItDenotesPriceAlike`

## RoundingPrecisionRangeTests

[`Divergences/RoundingPrecisionRangeTests.cs`](../tests/NanoQL.Tests/Divergences/RoundingPrecisionRangeTests.cs)

QuantLib documents a rounding's precision as lying in [0, 16] and doesn't check it: "supplying an unsupported rounding value may result in undefined behavior" (`rounding.hpp:31-34`). Its code reads a 32-entry table of powers of ten at `precision & 0x1F` (`rounding.cpp:30-37`), whose entries past 16 are zero: a precision of 17 to 31, or -1, scales by zero and gives NaN, and others alias (32 rounds as 0, 33 as 1, -16 as 16). nanoql refuses a precision outside the documented range when the rounding is built, with `ArgumentOutOfRangeException` on `precision` and the text QuantLib's overnight-indexed swap uses for the same check (`overnightindexedswap.cpp:178-180`).

Facts:

- `APrecisionOutsideZeroToSixteenIsRefused`

## SabrCubeDenseParametersTests

[`Divergences/SabrCubeDenseParametersTests.cs`](../tests/NanoQL.Tests/Divergences/SabrCubeDenseParametersTests.cs)

The dense parameters of a SABR cube that is neither at-the-money-calibrated nor calibrated in a single pass, which has none. QuantLib's `denseSabrParameters()` browses a default-constructed `Cube`, whose layer count is uninitialized (`sabrswaptionvolatilitycube.hpp:134, 416-424, 1045-1048, 1408`); the wheel returned a 0 × 2 matrix on QuantLib's 3 × 3 cube, a shape read from an uninitialized member. nanoql refuses it with `NanoQLException`.

Facts:

- `ACubeWithoutDenseParametersRefusesThem`

## SabrCubeFixedFlagsTests

[`Divergences/SabrCubeFixedFlagsTests.cs`](../tests/NanoQL.Tests/Divergences/SabrCubeFixedFlagsTests.cs)

A SABR cube given fewer fixed flags than the model's four parameters. QuantLib's SABR traits read `paramIsFixed[0..3]` unchecked (`sabrswaptionvolatilitycube.hpp:98`): with three flags the wheel reads the fourth as free, fitting ρ to −0.9999; with none, the process ends with an access violation at the first query. nanoql refuses fewer flags with `System.ArgumentException`, in the form of QuantLib's ZABR traits' check ("ZABR model requires 5 fixed flags, but 4 provided", `zabrswaptionvolatilitycube.hpp:128`), and ignores flags past the fourth, as QuantLib does. The refusal is the model's, `CreateInterpolation`'s, naming its own `paramIsFixed`, and reaches the cube's caller as the model raised it.

Facts:

- `FewerFixedFlagsThanParametersAreRefused`

## SabrCubeGuessGridTests

[`Divergences/SabrCubeGuessGridTests.cs`](../tests/NanoQL.Tests/Divergences/SabrCubeGuessGridTests.cs)

The grid a SABR cube's parameter guesses sit on. QuantLib's cube builds its guesses in its constructor (`sabrswaptionvolatilitycube.hpp:357-359, 370-385`) on the grid its base computed from the evaluation date adjusted on the at-the-money structure's calendar (`swaptionvolcube.cpp:37-42`); its first calculation recomputes the grid from the at-the-money structure's reference date (`swaptionvoldiscrete.cpp:180-190`) and never rebuilds the guesses, so where the two dates differ the fits start from guesses interpolated off their nodes. With β fixed at guesses of 0.3, 0.5 and 0.7 by option tenor, on an at-the-money matrix fixed on Wednesday 18 June 2025 and an evaluation date of Monday 16 June, the wheel's β at the 10-year option node is 0.4998783824870782. nanoql builds the guesses on the at-the-money structure's grid, the cube's only one, so the node's β is its guess, 0.5000000000000001 after the fit's transforms, as QuantLib's is when the structure is dated on the evaluation date.

Facts:

- `TheGuessesSitOnTheAtTheMoneyStructuresGrid`

## SabrCubeGuessShapeTests

[`Divergences/SabrCubeGuessShapeTests.cs`](../tests/NanoQL.Tests/Divergences/SabrCubeGuessShapeTests.cs)

A SABR cube's parameter guesses with fewer rows than option and swap tenor pairs, or a row with fewer guesses than the model's parameters. QuantLib reads `parametersGuessQuotes_[j*nSwapTenors_+k][i]` unchecked (`sabrswaptionvolatilitycube.hpp:364-367, 377-382`): on QuantLib's 3 × 3 cube, 8 rows, a row of 3 guesses or no rows end the process with an access violation at construction. nanoql refuses them with `System.ArgumentException`, in the words of the volatility spreads' checks, and ignores rows and guesses past those it reads, as QuantLib does.

Facts:

- `FewerGuessesThanTheCubeReadsAreRefused`

## SabrGuessNonFiniteTests

[`Divergences/SabrGuessNonFiniteTests.cs`](../tests/NanoQL.Tests/Divergences/SabrGuessNonFiniteTests.cs)

Le Floc'h and Kennedy's SABR guess on a non-finite argument. QuantLib computes on it (`sabr.cpp:289-385`): a NaN or infinite volatility makes the smile's moments NaN, ν NaN, and ρ the sign of a NaN through `boost::math::sign` (`sabr.cpp:332, 365`), which reads the NaN's sign bit, so it depends on which NaN an operation keeps, and the compiler's choice and .NET's differ on some inputs. On strikes 3%, 3% and 4% around a forward of 3.5%, with a NaN first volatility, the wheel gives α NaN, β 0.5, ν NaN and ρ 1; with an infinite third volatility, α NaN, β 0.5, ν NaN and ρ −1. None of them is a guess a caller can use, so nanoql refuses a non-finite argument with `ArgumentException` naming it.

Facts:

- `ANonFiniteArgumentIsRefused`

## SabrInterpolatedSmileSectionStrikesTests

[`Divergences/SabrInterpolatedSmileSectionStrikesTests.cs`](../tests/NanoQL.Tests/Divergences/SabrInterpolatedSmileSectionStrikesTests.cs)

The SABR-interpolated section's strikes: QuantLib reads a strike per volatility, past the strikes' end where there are fewer strikes than volatilities (`sabrinterpolatedsmilesection.cpp:111-119`), undefined behaviour. nanoql refuses fewer strikes than volatilities; more strikes are ignored past the volatilities, as QuantLib's.

Facts:

- `FewerStrikesThanVolatilitiesAreRefused`

## SabrSmileSectionParameterTests

[`Divergences/SabrSmileSectionParameterTests.cs`](../tests/NanoQL.Tests/Divergences/SabrSmileSectionParameterTests.cs)

A SABR smile section given fewer than four parameters. QuantLib reads α, β, ν and ρ from the vector unchecked (`sabrsmilesection.cpp:52-55`): with three, the wheel's ρ is −2.654651770048e-310, memory past the vector; with none, the process ends with an access violation. nanoql refuses fewer than four with `ArgumentException`, in the text of QuantLib's no-arbitrage SABR section's check (`noarbsabrsmilesection.cpp:46-48`).

Facts:

- `FewerThanFourParametersAreRefused`

## SabrVolSurfaceFixingTests

[`Divergences/SabrVolSurfaceFixingTests.cs`](../tests/NanoQL.Tests/Divergences/SabrVolSurfaceFixingTests.cs)

The SABR volatility surface's fixing read. QuantLib's smile at time t reads the index's fixing on the date ⌊365t⌋ days after the reference date, which the index refuses ("Fixing date … is not valid") on two days in seven (QuantLib 1.44); nanoql reads it on that date adjusted on the fixing calendar by Following, the adjustment QuantLib's interest-rate surface makes to its reference date, the identity on a fixing date. Checks: on a weekend the section's forward is Monday's fixing and its date the weekend's; on business days the forward is the day's own fixing.

Facts:

- `OnAWeekendTheSectionReadsMondaysFixing`
- `OnABusinessDayTheSectionReadsItsOwnFixing`

## SabrVolatilityTypeTests

[`Divergences/SabrVolatilityTypeTests.cs`](../tests/NanoQL.Tests/Divergences/SabrVolatilityTypeTests.cs)

A volatility type outside `VolatilityType`. QuantLib's SABR functions and the SABR fit take any value other than `Normal` as lognormal (`sabr.cpp:87, 142`), while `SmileSection::optionPrice` takes any value other than `ShiftedLognormal` as Bachelier's (`smilesection.cpp:84-88`): on the wheel, type 2 gives the SABR volatility 1.125831262004863 at a strike of 4% on a forward of 3.5%, the lognormal one, and a SABR section of type 2 prices a call there at 0.4466461204232711, Bachelier's price on that lognormal volatility, where the lognormal section's is 0.013594842378425748. nanoql refuses the value on entry with `ArgumentOutOfRangeException`, as its other members taking an enum do.

Facts:

- `AnUnknownVolatilityTypeIsRefused`

## SabrZeroSmileTests

[`Divergences/SabrZeroSmileTests.cs`](../tests/NanoQL.Tests/Divergences/SabrZeroSmileTests.cs)

A smile of zero volatilities fitted with vega weights. Every strike's vega weight is 0, and QuantLib normalizes the weights by their zero sum (`xabrinterpolation.hpp:144-160`): the weights are NaN, every cost the optimizer sees is NaN, and the fit returns the round-tripped guess with a NaN root-mean-square error, a finite maximum error and no end criteria. On 31 strikes from 3% to 9% and a forward of 3.9%, the wheel's fit keeps α 0.4472135954999579, β 0.5000000000000001, ν 0.6324555320336759 and ρ 0, a volatility of 2.4439735411535746 at 4%. nanoql refuses the smile with `ArgumentException`, "vega weights are all zero".

Facts:

- `AZeroSmileWithVegaWeightsIsRefused`

## SaddlePointLossModelTests

[`Divergences/SaddlePointLossModelTests.cs`](../tests/NanoQL.Tests/Divergences/SaddlePointLossModelTests.cs)

The saddle-point model's tail reads, on pools of n names at hazard 0.03, recovery 0.4 and notional 1: Above the whole tranche the probability is 0: on 100 names over [0, 30%] QuantLib's reads 0.0016176810243568769 at a fraction of 1.5 (it compares the fraction with the detachment amount); and the shortfall is at most the tranche's remaining notional, 30, where QuantLib's caps it at the tranche notional times the remaining ratio, 9.0000000000000302. Where a factor node's names are all at 0 or 1, QuantLib's saddle search throws ("invalid range"): at the evaluation date, and on 100 names from correlation 0.8 at five years, 0.85 from three months, 0.9 from a month and 0.95 throughout; its tail elsewhere reads the exact (recursive) model's within 1% at 10% and 7% to 8% at 1% a year out (0.2165 for 0.2021 at 0.7, 0.1899 for 0.1755 at 0.75). Here those nodes are exact, the tail within 2% at 10% through correlation 0.9 and 5% at 0.95, and 10% at 1%, of the exact one. A tail outside [0, 1] is refused: on ten names, 26.815515432090283 a day out at correlation 0.548 and 2.6118595103824398 at a week at 0.3, where the exact ones are 0.00073 and 0.0055. The percentile reads the exact distribution's within its loss unit where single nodes leave [0, 1]: on 100 names over the whole pool, QuantLib's 4.9485835 at 90% a year out at correlation 0.3, 55.627361 at 99% ten years out at 0.5 and 0.064772838 at 50% 30 days out at 0.1, the exact 4.8, 55.8 and 0 at its loss unit 0.6; its search solves below QuantLib's bracket, so it isn't QuantLib's to the digit. Where the level falls outside the bracket QuantLib places on its name of largest loss, it returns the bracket's end: on 95 names at hazard 0.03 and notional 1 with five at 0.001 and 3, it reads 0.97841938 at 1% a year out at correlation 0.7 and 0.20082286 at 10% 30 days out at 0.9, and throws a year out at 0.9, where the exact tails are 0.198289, 0.00494071 and 0.0564909; here the bracket widens and the point is solved. Above the largest loss a factor node reaches, the widened search's end reads 0/0 in the density and the conditional shortfall, so a tranche detaching above the pool's largest loss read NaN; here both read 0 there, as the tail does, and the tranche reads its shortfall detaching at the largest loss. The split's contributions read ∞/∞ at a widened point on QuantLib's form; here they take the cumulants' treatment and add up to the loss.

Facts:

- `NoTrancheLossExceedsTheTranche`
- `ATranchesShortfallIsItsTrancheLoss`
- `ARealizedDefaultLeavesTheLiveNames`
- `TheEvaluationDatesTailIsExact`
- `TheDegenerateNodesAreExact`
- `ATailOutsideItsDomainIsRefused`
- `ASmallPoolsTailInsideTheDomainIsQuantLibs`
- `ThePercentileIsTheExactOnesWithinItsLossUnit`
- `AHeterogeneousPoolsTailIsTheExactOnes`
- `ATrancheDetachingAboveTheLargestLossReadsItsShortfallThere`
- `TheDensityAboveTheLargestLossIsZero`
- `AHeterogeneousPoolsSplitAddsUpToTheLoss`
- `TheLossDistributionIsTheExactOnesWithinTheExpansionsAccuracy`

## SeasonedAsianSmileTests

[`Divergences/SeasonedAsianSmileTests.cs`](../tests/NanoQL.Tests/Divergences/SeasonedAsianSmileTests.cs)

The seasoned arithmetic Asians on a smile (fixed). With f future fixings of m, an accrued sum A and the strike K, max(avg − K, 0) = (f/m)·max(avg_f − K_f, 0) with K_f = (K − A/m)·m/f, so a seasoned price is exactly (f/m) times the unseasoned price at K_f. QuantLib's Turnbull-Wakeman engine reads its variances at K − A/m and its Choi engine its correlations at K − A/m and its volatilities at K (`turnbullwakemanasianengine.cpp:94-95`, `choiasianengine.cpp:104-122`), so on a smile whose skew decays with maturity (0.22 + skewⱼ·(100 − K)/100, skews 0.30, 0.18 and 0.08 at 60, 120 and 240 days; spot 100, r 5%, q 0) they break the identity, Turnbull-Wakeman by +42% and +1,302% and Choi by −2.6% to +306% on six past fixings and six monthly ones to come. nanoql reads both at the future average's own strike K_f. Check: the identity, against each engine's own unseasoned value at K_f.

Facts:

- `TurnbullWakemanKeepsTheSeasoningIdentity`
- `ChoiKeepsTheSeasoningIdentity`

## SeedZeroTests

[`Divergences/SeedZeroTests.cs`](../tests/NanoQL.Tests/Divergences/SeedZeroTests.cs)

QuantLib reads a seed of 0 as "no seed" and draws one from its `SeedGenerator` singleton, itself seeded from the clock; nanoql's seeds are explicit and no library code reads the clock, so every generator refuses 0 with `ArgumentOutOfRangeException`. QuantLib's `testSeedGenerator`, which only draws from the singleton, becomes this test. Two of QuantLib's seeds of 0 are deterministic, and nanoql refuses them too, one rule for every generator: `SobolRsg` reads its seed only for the dimensions beyond its set's table of direction integers, so `SobolRsg(32, 0)` draws what `SobolRsg(32, 1)` does; and `HaltonRsg` without a random start or shift never reads it.

Facts:

- `EveryGeneratorRefusesSeedZero`

## SerbiaEasterTests

[`Divergences/SerbiaEasterTests.cs`](../tests/NanoQL.Tests/Divergences/SerbiaEasterTests.cs)

Serbia's Easter. QuantLib's Belgrade calendar derives from `WesternImpl` (`serbia.hpp:50`), so its Good Friday and Easter Monday follow Western Easter, where the Belgrade Stock Exchange's calendar, which the header cites, closes on Orthodox Easter: May 3rd and 6th, 2024, April 10th and 13th, 2026. nanoql takes `OrthodoxImpl`'s Easter Monday, the one QuantLib's Romania, Russia, Ukraine and North Macedonia read, with the same weekend. `testSerbia`'s Easter dates (`test-suite/calendars.cpp:4275-4302`) move here, QuantLib's recorded beside nanoql's.

Facts:

- `GoodFridayAndEasterMondayFollowOrthodoxEaster`
- `TestSerbiasEasterDates`

## SetSeasonalityColdStartTests

[`Divergences/SetSeasonalityColdStartTests.cs`](../tests/NanoQL.Tests/Divergences/SetSeasonalityColdStartTests.cs)

`InflationTermStructureExtensions.SetSeasonality` on a piecewise zero-inflation curve. QuantLib's `setSeasonality` marks the curve for recalculation, and its bootstrap restarts from the nodes it last found, so the curve it gives differs from one built with the seasonality from the start: on the wheel, a curve on nine UK RPI zero-coupon swaps from 2.9% to 3.25%, bootstrapped to 1e-14, differs from the fresh one by up to 7.9e-15 at its nodes, and its five-year zero rate is 0.030262300008121912 against the fresh curve's 0.0302623000081228. nanoql's copy is bootstrapped from a cold start, as every bootstrapped curve is, so it is the curve built with the seasonality, bit for bit.

Facts:

- `TheCopyIsTheCurveBuiltWithTheSeasonality`

## SetSeasonalityReceiverTests

[`Divergences/SetSeasonalityReceiverTests.cs`](../tests/NanoQL.Tests/Divergences/SetSeasonalityReceiverTests.cs)

`InflationTermStructureExtensions.SetSeasonality` returns a copy and leaves its receiver unchanged, whether it accepts the seasonality or refuses it. QuantLib's `setSeasonality` changes its curve, and assigns before it checks (`inflationtermstructure.cpp:78-82`), while `MultiplicativePriceSeasonality::isConsistent` throws rather than returning false (`seasonality.cpp:82-84`), so a refused seasonality stays on QuantLib's curve: a zero curve flat at 3%, refused a 24-factor seasonality inconsistent with it, then has a seasonality, and its zero rate on 1 May 2026 is 0.030493063761148242 on the wheel, not 0.03.

Facts:

- `TheReceiverIsUnchangedWhetherTheSeasonalityIsAcceptedOrRefused`

## SettlementDaysSentinelTests

[`Divergences/SettlementDaysSentinelTests.cs`](../tests/NanoQL.Tests/Divergences/SettlementDaysSentinelTests.cs)

Settlement days are an `int`, or an `int?` where QuantLib may leave them unset, so `int.MaxValue` is a value. QuantLib reads its null sentinel, `Null<Natural>()`, which equals `INT_MAX`, as "not given": a swap rate helper given it starts at the index's spot date (`ratehelpers.cpp:573-575`), and an OIS rate helper at its swap builder's default spot (`oisratehelper.cpp:167-169`), each 18 June 2025 from 16 June; and a swaption helper starts its swap at the index's value date (`swaptionhelper.cpp:161-167`), whose settlement days' value it never reads. nanoql counts that many days from the evaluation date, and the date leaves its range; its swaption helper takes the path of any settlement days given.

Facts:

- `SettlementDaysOfIntMaxValueAreAValue`
- `OisSettlementDaysOfIntMaxValueAreAValue`
- `AFloatingRateBondsFixingDaysOfIntMaxValueAreAValue`
- `ASwaptionHelpersSettlementDaysOfIntMaxValueAreAValue`

## SharedNumericCodeTests

[`Divergences/SharedNumericCodeTests.cs`](../tests/NanoQL.Tests/Divergences/SharedNumericCodeTests.cs)

Two currencies sharing a numeric code in the exchange-rate registry. CNY and CNH both carry ISO 4217's 156 (`asia.cpp:44`, `:248`), and QuantLib keys a pair by the numeric codes (`exchangeratemanager.cpp:89-98`), so a USD/CNY rate and a USD/CNH rate share one entry list: with both held, a lookup of USD to CNY returns the USD/CNH rate added last (100 USD exchange into 715 CNH), and a USD/CNY rate answers a lookup of USD to CNH, shadowing a chain through EUR. nanoql keys a pair by its two currencies, so each lookup finds its own pair's rate, or chains.

Facts:

- `CnyAndCnhAreLookedUpApart`
- `AUsdCnyRateNoLongerShadowsAChainForCnh`

## SharedPricerStateTests

[`Divergences/SharedPricerStateTests.cs`](../tests/NanoQL.Tests/Divergences/SharedPricerStateTests.cs)

Two capped coupons sharing one pricer. QuantLib's capped coupon prices its caplet with whatever per-coupon state the last `initialize(coupon)` left in the shared pricer (`capflooredcoupon.cpp:86-96`, `couponpricer.cpp:96-113, 139-148`): once both underlyings' rates have been read, the first capped coupon's caplet reads the second underlying's dates, gearing and spread, and its rate is 0.02266598964839617 against 0.030228654513094377 alone. nanoql's pricers hold no per-coupon state, so each capped coupon prices as it would alone, whatever was read before.

Facts:

- `ACappedCouponPricesAsItWouldAloneWithASharedPricer`

## ShortUniformSequenceTests

[`Divergences/ShortUniformSequenceTests.cs`](../tests/NanoQL.Tests/Divergences/ShortUniformSequenceTests.cs)

QuantLib's `InverseCumulativeRsg::nextSequence` maps the uniform sample at every index below the dimension it read at construction (`inversecumulativersg.hpp:89-90`), so a uniform sequence generator that draws fewer values reads past the end of its sample, which C++ leaves undefined. None of QuantLib's generators does, but `IRsg` is an extension point: nanoql's composite refuses such a draw with `NanoQLException`. A longer draw is read as QuantLib reads it, its first values only.

Facts:

- `ADrawShorterThanTheDimensionIsRefused`

## ShoutDividendStrikeTests

[`Divergences/ShoutDividendStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/ShoutDividendStrikeTests.cs)

The finite-difference shout engine with a dividend between the shout and expiry. QuantLib's inner value takes the intrinsic value at the spot, the escrowed level less the dividend adjustment, but strikes the residual option at the escrowed level (`fdmshoutloginnervaluecalculator.cpp:47-73`), handing the dividends' present value back: on `testLargeDividendShoutNPV`'s option, a call at 80 on 100 with a dividend of 30 at three months, its engine gives 24.90999 = 20 + 4.90999, a shout now whose residual is at the money on the escrowed 70, and its test's reference, a shout without dividends up to the dividend date, prices the same handed-back residual. A shout at τ pays max(S_T − K, S_τ − K) = (S_τ − K) + (S_T − S_τ)⁺, so the residual is struck at the spot S_τ. nanoql strikes it there, its volatility read there; without dividends the two levels are one, and QuantLib's bits stand. The checks: the payoff identity at t₀, and, independently, a binomial programme of the same stopping problem on the escrowed process.

Facts:

- `AShoutNowIsTheIntrinsicValueAndACallStruckAtTheSpot`
- `TheValueIsABinomialProgrammesOfTheStoppingProblem`

## SimpleChooserTermStructureTests

[`Divergences/SimpleChooserTermStructureTests.cs`](../tests/NanoQL.Tests/Divergences/SimpleChooserTermStructureTests.cs)

The simple chooser engine on term structures. QuantLib evaluates Rubinstein's formula on the zero rates and the volatility at maturity, its times from the evaluation date (`analyticsimplechooserengine.cpp:49-60`), so on a rising market it is +22% off; nanoql prices Rubinstein's decomposition at the choosing date, C(K, T) + D_q(t, T)·P(K·D_r(t, T)/D_q(t, T), t), each by the analytic European engine. The checks: the option's exact value at the choosing date, the larger of the call and the put there over the lognormal spot, within 1e-9; the European engine's two options, bit for bit; and QuantLib's value on a flat market.

Facts:

- `ARisingMarketMeetsTheExactValueAndTheEuropeanEngine`
- `AFallingMarketMeetsTheExactValueAndTheEuropeanEngine`
- `AFlatMarketIsQuantLibs`

## SimplexEmptyInitialValueTests

[`Divergences/SimplexEmptyInitialValueTests.cs`](../tests/NanoQL.Tests/Divergences/SimplexEmptyInitialValueTests.cs)

Simplex on a problem with no variables: QuantLib sizes its function values at n + 1 = 1 and then reads the second (`simplex.cpp:99-106,119`), a result C++ leaves undefined. nanoql refuses it with `NanoQLException`, as Levenberg–Marquardt refuses it with QuantLib's own "no variables given".

Facts:

- `ASimplexWithNoVariablesIsRefused`

## SimulatedAnnealingTests

[`Divergences/SimulatedAnnealingTests.cs`](../tests/NanoQL.Tests/Divergences/SimulatedAnnealingTests.cs)

Simulated annealing's generator, copied per minimization, and its NaN guard: QuantLib's optimizer holds its generator and continues its stream in a second minimization, where nanoql's draws from a copy each time; and QuantLib's guard on the initial vertices tests `ytry_`, the previous trial's value, left from an earlier minimization or unset (`simulatedannealing.hpp:166`), where its trial path tests the value just computed (`:121-123`), so a vertex whose value is NaN enters the simplex. nanoql's guards each vertex's value, and the vertices a contraction moves alike. QuantLib's values are its own class's.

Facts:

- `ASecondMinimizationRepeatsTheFirst`
- `AVertexWhoseValueIsNaNEntersAtTheLargestDouble`
- `AContractedVertexWhoseValueIsNaNEntersAtTheLargestDouble`

## SingleProductCompositeBufferTests

[`Divergences/SingleProductCompositeBufferTests.cs`](../tests/NanoQL.Tests/Divergences/SingleProductCompositeBufferTests.cs)

The single composite's buffer (fixed). QuantLib's `SingleProductComposite::maxNumberOfCashFlowsPerProductPerStep` sums its parts' maxima per product, ignoring their numbers of products (`singleproductcomposite.cpp:32`), so a part of several products paying at one step writes past the buffer the accounting engine sizes by it: a rebate of three products, each paying at every step, gives 1 for 3 cash flows a step, and coterminal swaps corrupt the heap. nanoql counts each part's maximum times its products. Checks: the multi composite's products, summed.

Facts:

- `APartOfSeveralProductsFitsTheBuffer`

## SingleSenioritySettlementTests

[`Divergences/SingleSenioritySettlementTests.cs`](../tests/NanoQL.Tests/Divergences/SingleSenioritySettlementTests.cs)

An event settled at one recovery rate settles the seniority it names: QuantLib's single-rate settlement fills every other seniority with ISDA's conventional recovery (`defaultevent.cpp:63-75`), so a failure to pay settled for SubLT2 at 0.3 reads 0.65 for SecDom, where each seniority settles at its own auction (ISDA's 2008 Icelandic protocols). And a bankruptcy settled by a map settles the seniorities it holds, where QuantLib's refuses a map without all five (`defaultevent.cpp:210-213`). QuantLib's values are recorded in the test; the check is QuantLib's map constructor holding the seniority alone, which reads `Null<Real>()` for the others.

Facts:

- `AFailureToPaySettlesTheSeniorityItNames`
- `ABankruptcySettlesTheSeniorityItNamesOrItsMapHolds`

## SinkingNotionalsTests

[`Divergences/SinkingNotionalsTests.cs`](../tests/NanoQL.Tests/Divergences/SinkingNotionalsTests.cs)

A French amortization's notionals for a bond length that isn't positive, or at a frequency of no period. QuantLib's `sinkingNotionals` finds no periods in a zero length, or minus one in a length of minus one period, then writes past the end of a one-element vector or reads the first element of an empty one (`amortizingfixedratebond.cpp:131-147`), which is undefined: the wheel ends the process for 0Y, 0M and -1Y; below minus one period the vector's size wraps, and it throws "vector too long". At `Frequency.Once` or `Frequency.NoFrequency` the period has no days, and the time ratio divides by zero and converts the infinite ratio to an integer (`amortizingfixedratebond.cpp:99-104`), which is undefined: MSVC's smallest integer matches no positive length, so the wheel refuses "Bond frequency is incompatible with the maturity tenor". nanoql refuses a length that isn't positive, before its periods are counted, and a frequency of no period, with that text, each with `ArgumentException` naming its parameter.

Facts:

- `ABondLengthThatIsNotPositiveIsRefused`
- `AFrequencyOfNoPeriodIsRefused`

## SmallReversionTests

[`Divergences/SmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/SmallReversionTests.cs)

The short-rate models' other members at small mean reversion. G2's swaption pricing function (`g2.cpp:119-160`) cancels in its mux and sigmax: on the wheel, a one-period payer swaption, which is (1 + Kτ) puts on its bond struck at 1/(1 + Kτ), prices 0.0038981761747141413 at a = b = 1e-4 against that bond option's 0.003898173326452401, 0.00017515607132449288 at 1e-6 against 0.003898519697671894, and at 1e-7 refuses "root not bracketed". Hull-White's `convexityBias` (`hullwhite.cpp:150`) cuts (1 − e^(−ax))/a at QL_EPSILON and cancels above it: 7.593680693951388e-05 at a = 1e-10, 7.594298930423405e-05 at 1e-12 and 7.801188790257374e-05 at 1e-14, against its own a = 0 value 7.593678809059146e-05. G2's bond option reads `sigmaP` (`g2.cpp:59-87`), which cancels likewise: 0.013016600554919378 at a = b = 1e-12 against 0.01301666498797277 at 1e-8. nanoql's are written in cancellation-free forms. QuantLib's check: its second routes, the bond option for the swaption and the a = 0 branch for the bias, and, for the bond option, Black's formula on the a → 0 limit of sigmaP, (s − t)√(t(σ² + η² + 2ρση)) at a = b.

Facts:

- `G2sOnePeriodSwaptionIsItsBondOption`
- `HullWhitesConvexityBiasAtSmallReversionIsItsLimit`
- `G2sBondOptionAtSmallReversionIsBlacksOnTheLimit`

## SmileSectionFieldsTests

[`Divergences/SmileSectionFieldsTests.cs`](../tests/NanoQL.Tests/Divergences/SmileSectionFieldsTests.cs)

A smile section built by the parameterless constructor. QuantLib's `SmileSection() = default` (`smilesection.hpp:50`) leaves the exercise time, the volatility type and the shift uninitialized (`smilesection.hpp:86-92`), and the base's inspectors, its variance and its converted volatility read them (`smilesection.hpp:60-67, 112-115`; `smilesection.cpp:133`), indeterminate unless the section overrides them all, as QuantLib's own users of the constructor do. nanoql's parameterless constructor defines them: an exercise time of 0, `VolatilityType.ShiftedLognormal` and a shift of 0, so a section that keeps the base's members reads those values.

Facts:

- `AParameterlessSectionReadsDefinedFields`

## SmileSectionUtilsShiftTests

[`Divergences/SmileSectionUtilsShiftTests.cs`](../tests/NanoQL.Tests/Divergences/SmileSectionUtilsShiftTests.cs)

The smile utilities' endpoints on a shifted section: where a section's strike range is limited, QuantLib puts the range's ends in its grid with a moneyness of the shifted strike over the unshifted level (`smilesectionutils.cpp:100-101`, `:107-108`), where every other point's is over the shifted level. On the harness, over a section shifted by 1% at a level of 5% with strikes from 0 to 10%, its grid's ends read 0.2 and 2.2 where its own formula gives 1/6 and 11/6, and with a larger shift its grid comes out unsorted. nanoql divides by the shifted level. QuantLib's check: its own interior formula, (k + shift)/(f + shift).

Facts:

- `TheEndpointsTakeTheInteriorsMoneyness`

## SmmDriftAliveTests

[`Divergences/SmmDriftAliveTests.cs`](../tests/NanoQL.Tests/Divergences/SmmDriftAliveTests.cs)

The coterminal swap market model's drifts on a curve state set from the step's first alive rate (fixed). QuantLib's `SMMDriftCalculator::compute` reads every annuity from the first rate (`smmdriftcalculator.cpp:86-89`), so a state set from its first alive rate on, all the drifts read, is refused ("invalid index"). nanoql reads them from the first alive rate. Check: the drifts on the same curve set from the first rate, QuantLib's arithmetic (its vectors), bit for bit, and QuantLib's own numbers there.

Facts:

- `TheDriftsOfAStateSetFromTheFirstAliveRateAreThoseOfTheWholeCurve`

## SobolPeriodEndTests

[`Divergences/SobolPeriodEndTests.cs`](../tests/NanoQL.Tests/Divergences/SobolPeriodEndTests.cs)

The Sobol generator's period end with the Gray code, where QuantLib's behaviour is undefined. Its `skipTo(4294967295)` computes `skip + 1`, which wraps to 0, and converts `log(0)`'s −∞ to an `unsigned int`, which C++ leaves undefined; its draw that takes the counter to 4294967295 finds the rightmost zero bit at 32 and reads row 32 of its 32 rows of direction integers (the wheel crashes there). nanoql refuses that skip with `ArgumentOutOfRangeException`, and throws "period exceeded" on that draw, one draw before QuantLib's own check. Without the Gray code QuantLib's end is defined, and nanoql keeps it (the harness's vectors, `cxx/math/randomnumbers/sobolrsg`).

Facts:

- `SkipToTheLastIndexIsRefusedWithTheGrayCode`
- `TheDrawThatWouldReachTheLastCounterThrowsWithTheGrayCode`

## SoftBarrierBandAverage

[`Divergences/SoftBarrierBandAverage.cs`](../tests/NanoQL.Tests/Divergences/SoftBarrierBandAverage.cs)

The soft barrier's definition as #2883's test states it, "the average of the single-barrier options with barriers in [L, U]", computed independently of the engine: composite Simpson's rule over QuantLib's analytic barrier engine's knock-ins, the barriers on the spot's far side knocked in (the vanilla), the band split at the spot and the strike, so each piece's integrand is smooth. The soft-barrier divergence facts check the engine against it.

## SoftBarrierCoincidentBarriersTests

[`Divergences/SoftBarrierCoincidentBarriersTests.cs`](../tests/NanoQL.Tests/Divergences/SoftBarrierCoincidentBarriersTests.cs)

The soft barrier with coincident or nearly coincident barriers (fixed). Where U − L < 1e-4 QuantLib prices its analytic barrier engine on flat Actual/360 curves rebuilt from TARGET's adjusted evaluation date (`analyticsoftbarrierengine.cpp:250-282`), so a process on Actual/365 Fixed prices 10.187731 where the analytic engine on it gives 10.159379; and the switch is absolute in price units, so the same contract changes value with the underlying's scale: a down-in call struck at the spot on the band [0.990, 0.995] × spot prices 0.1027057 × spot at spots 100 and 1 and 0.1063173 × spot at 0.02 and 0.01, a 0.5% band taken as a hard barrier. Haug's own difference quotient cancels as the band narrows, beyond 1e-12 below a relative width of about 1e-3. nanoql prices the single barrier at U = L only, through the analytic barrier engine on the process itself, Haug's form where (U − L)/U ≥ 3e-3, and the band average below it. Checks: QuantLib's analytic barrier engine on the same process at U = L, and homogeneity, the contract scaled to spot 100.

Facts:

- `CoincidentBarriersAreTheSingleBarrierOnTheProcessItself`
- `ABandNarrowerThan1e4IsTheBandAverage`
- `ANarrowBandAtASmallSpotIsTheContractScaled`
- `ABandBelowTheRelativeCutAtASmallSpotIsTheContractScaled`

## SoftBarrierDomainTests

[`Divergences/SoftBarrierDomainTests.cs`](../tests/NanoQL.Tests/Divergences/SoftBarrierDomainTests.cs)

Haug's soft-barrier form outside its domain (fixed). The closed form integrates the standard knock-in's branch for a barrier at or below the strike, so it is the average only for a down barrier on a call with U ≤ K, or an up barrier on a put with L ≥ K, the spot outside the band; QuantLib's cases all hold S 100, X 100, U 95. With the band reaching above the strike it is high by up to 1.5%, and with the spot inside the band, the barriers above it already knocked in, it prices 12.286 for 8.803. nanoql takes the band average wherever the band reaches across the strike or the spot is inside it. Check: the band average of QuantLib's analytic barrier engine by Simpson's rule (`SoftBarrierBandAverage`), and a deep out-of-the-money average, a call at 300 with the spot inside the band, below 2.2e-4, which an unscaled tolerance would refuse at construction.

Facts:

- `ABandAcrossTheStrikeIsTheBandAverage`
- `ASpotInsideTheBandIsTheBandAverage`
- `ADeepOutOfTheMoneyAverageIsPriced`

## SoftBarrierExerciseTests

[`Divergences/SoftBarrierExerciseTests.cs`](../tests/NanoQL.Tests/Divergences/SoftBarrierExerciseTests.cs)

The soft barrier engine's exercise. QuantLib's engine never reads the exercise's type and prices any exercise to its last date: a down-and-in call at 90 on the band [85, 90], an American exercise to a year, prices 2.906045178994 on the vectors' first market, its European value; its own implied volatility refuses a non-European soft barrier ("engine not available for non-European soft barrier option"). nanoql refuses a non-European exercise, as the family's European engines do: the soft barrier's average, the route off Haug's domain, runs on the analytic barrier engine, which prices European options only.

Facts:

- `ANonEuropeanExerciseIsRefused`

## SoftBarrierLowVolatilityTests

[`Divergences/SoftBarrierLowVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/SoftBarrierLowVolatilityTests.cs)

The soft barrier at small volatility (fixed). Haug's form is NaN from σ = 0.02 down at a 4% carry over a year, where its powers overflow. nanoql takes the band average wherever Haug's value isn't finite, its adaptive Gauss-Kronrod rule split at the strike, the spot, the forward and their transition widths. Checks: the band average by Simpson's rule (`SoftBarrierBandAverage`); the deterministic limit, the knocked-in fraction of the band times the forward payoff (a down-in call at 80 on [90, 99], r 1%, q 5%, 5.166684728565), which the average approaches as σ², 1.0e-5 below it at σ 1e-4 (QUADPACK's value) and 1.0e-7 at 1e-5 (a 200,000-interval Simpson's rule across the step at the forward agreeing with the engine to 6e-14, where QUADPACK, missing the step's width, met the limit itself), so within 1e-10 from σ 1e-7; and QUADPACK's average at σ 1% on the same market, 5.2810970596, where a first-estimate scale at 1e-12 never ends.

Facts:

- `WhereHaugsFormIsNotANumberTheValueIsTheBandAverage`
- `AtVanishingVolatilityTheValueIsTheDeterministicLimit`
- `AtOnePercentTheValueIsQuadpacksAverage`

## SoftBarrierRateLimitsTests

[`Divergences/SoftBarrierRateLimitsTests.cs`](../tests/NanoQL.Tests/Divergences/SoftBarrierRateLimitsTests.cs)

The soft barrier's rate limits (a refusal lifted). QuantLib refuses "Interest rate must be between -5% and 100%" and "Dividend yield must be between -10% and 100%" (`analyticsoftbarrierengine.cpp:174-175`), limits no formula needs. nanoql doesn't port them: every rate takes the engine's routes, here the band average, Haug's form being NaN at b + σ² = 2.5e-3 and σ 1.0. Check: the band average by Simpson's rule (`SoftBarrierBandAverage`), and an independent value, 3.761756955001.

Facts:

- `ADividendYieldAbove100PercentIsPriced`

## SoftBarrierSingularCarryTests

[`Divergences/SoftBarrierSingularCarryTests.cs`](../tests/NanoQL.Tests/Divergences/SoftBarrierSingularCarryTests.cs)

The soft barrier at Haug's singular carries (fixed). Haug's term 1 divides by 2(μ + ½), zero at b = −σ² (`analyticsoftbarrierengine.cpp:141-142`): 47.207 at r 0, q 4%, σ 20% where both sides give 1.37353; around both singular points, b = 0 and b = −σ², its double cancels beyond the tolerance out to about 1e-5 of the carry, and QuantLib's nudge at |r − q| < 1e-10, r moved by 1e-6, is itself off by 1.2e-6 to 4.2e-6. nanoql takes the band average, the soft barrier's definition, wherever |b| or |b + σ²| is below 2e-3, superseding the nudge. Check: the band average of QuantLib's analytic barrier engine, by Simpson's rule (`SoftBarrierBandAverage`).

Facts:

- `AtACarryOfMinusTheVarianceRateTheValueIsTheBandAverage`
- `AtZeroCarryTheValueIsTheBandAverage`

## SouthKoreaElection2025Tests

[`Divergences/SouthKoreaElection2025Tests.cs`](../tests/NanoQL.Tests/Divergences/SouthKoreaElection2025Tests.cs)

South Korea's 2025 presidential election day. QuantLib 1.44 misses June 3rd, 2025, when the snap election closed the country and the Korea Exchange; QuantLib's master adds it to the settlement calendar, which KRX inherits (#2894, `46c4d4ed9`), with the date in both test cases' 2025 lists, which nanoql takes ahead of the pin, as a fix. The 2025 lists of `testSouthKoreanSettlement` and `testKoreaStockExchange` (`test-suite/calendars.cpp:1772-1788`, `:2443-2460`) move here, with upstream's line; QuantLib 1.44's answer is recorded beside them.

Facts:

- `The2025ListsHoldTheElectionDay`

## SparseIluTests

[`Divergences/SparseIluTests.cs`](../tests/NanoQL.Tests/Divergences/SparseIluTests.cs)

The sparse ILU's fill levels: QuantLib records each new entry's level from the levels' nonzero entries by the entries' rank among the row's nonzero values, which a value that cancels to 0 shifts, so a later row reads another entry's level; nanoql records each entry's own. On the first of random 8 × 8 matrices with exact cancellations whose factors move at fill level 1 (Mersenne Twister seed 5, the 36th), QuantLib drops U's fill entry (4, 7) and keeps U(7, 7) at 4; the levels at their own positions keep (4, 7) at −0.5 and give U(7, 7) 4.1875. QuantLib's factors and those of its own constructor with the one read changed are its own.

Facts:

- `EachFillEntryKeepsItsOwnLevel`

## SparseMatrixPlusAssignTests

[`Divergences/SparseMatrixPlusAssignTests.cs`](../tests/NanoQL.Tests/Divergences/SparseMatrixPlusAssignTests.cs)

`SparseMatrix`'s in-place sum of a matrix that stores a nonzero element outside the target is refused, with `ArgumentException` "element (i, j) outside the m x n matrix". uBLAS checks the sizes only in its debug builds (`BOOST_UBLAS_CHECK`, `detail/matrix_assign.hpp:1057-1058`); under `NDEBUG`, which QuantLib's builds use, `make_conformant` stores such an element in the target (`:147-148`). In a row outside, its insertion writes row starts past the target's rows (`matrix_sparse.hpp:3070-3073`), a result C++ leaves undefined; in a column outside, it stores an element past the target's columns, which a product then reads past its vector. An addend of other sizes whose elements all lie inside the target, a zero outside among them, is uBLAS's defined path, kept.

Facts:

- `ANonzeroElementOutsideTheMatrixIsRefused`
- `AnAddendInsideTheMatrixIsAdded`

## SparseMatrixProductTests

[`Divergences/SparseMatrixProductTests.cs`](../tests/NanoQL.Tests/Divergences/SparseMatrixProductTests.cs)

`SparseMatrixFunctions.Prod` on a matrix with more rows than columns. QuantLib's `prod` sizes its result by the columns and writes it by the rows (`sparsematrix.hpp:51-69`), so such a matrix writes past the result, and one with fewer rows pads it. nanoql sizes the result by the rows. QuantLib's `testSparseMatrixMemory` asserts the column-sized result, `Array({0, 18, 0, 84})` for an 8×4 matrix (`matrices.cpp:753-755`); the check moved here from `MatricesTests.cs`.

Facts:

- `TheProductHasARowsLength`

## SphereCylinderClampTests

[`Divergences/SphereCylinderClampTests.cs`](../tests/NanoQL.Tests/Divergences/SphereCylinderClampTests.cs)

The sphere-cylinder optimizer's clamp (fixed). QuantLib's constructor stores the cylinder's radius s, then clamps its parameter to 0 (`spherecylinder.cpp:87`), so its intersection test reads the clamped radius and its solves the stored one: at r 1, α 0.5 and z (0.6, 0.1, 0.5), s −0.2 projects to (0.359, −0.141, 0.923) and s 0 to (0.5, 0, 0.866). nanoql stores the clamped radius. QuantLib's one caller passes s ≥ 0. Check: QuantLib's own optimizer at s 0, for every s below 0.

Facts:

- `ANegativeRadiusIsTheZeroRadius`

## SpreadApproximationStrikeTests

[`Divergences/SpreadApproximationStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/SpreadApproximationStrikeTests.cs)

Bjerksund and Stensland's and the operator-splitting spread engines where the second forward and the strike sum to zero or less. QuantLib takes the log of a negative ratio and returns NaN (`bjerksundstenslandspreadengine.cpp:48`, `operatorsplittingspreadengine.cpp:53`), where Kirk's engine, their sibling, refuses through Black's check ("forward (−2.19458) must be positive"). nanoql refuses before the log, "f2 + strike must be positive for this approximation": a refusal, the approximations having no value there.

Facts:

- `BjerksundStenslandRefuses`
- `OperatorSplittingRefusesAtEitherOrder`

## SpreadFittingClockTests

[`Divergences/SpreadFittingClockTests.cs`](../tests/NanoQL.Tests/Divergences/SpreadFittingClockTests.cs)

The spread fitting method's discounting curve on another clock. QuantLib's discount function reads the discounting curve at the fitted curve's time (`nonlinearfittingmethods.cpp:414-416`), which the discounting curve counts from its own reference date on its own day counter, rebased to the fitted curve's reference date. On the wheel, a curve fitted on 15 January 2025 over Nelson-Siegel parameters 0, 0, 0 and 0.5, whose factor is exactly 1, discounts 0.9810709211266746 at a year over a zero curve dated 30 days earlier, where QuantLib's own fit over that curve implied at the fitted curve's date gives 0.9790412094890624; over a flat 3% Actual/360 curve, 0.9704455335485082 where the curve restated on Actual/365 (Fixed) gives 0.9700412654712278. nanoql reads the discounting curve at the instant the fitted curve's time denotes. QuantLib's check: its own fitted curve over the discounting curve restated on the fitted curve's reference date and day counter.

Facts:

- `ADiscountingCurveDatedEarlierIsReadAtTheInstant`
- `ADiscountingCurveOnAnotherDayCounterIsReadAtTheInstant`

## SpreadedHazardRateCurveTests

[`Divergences/SpreadedHazardRateCurveTests.cs`](../tests/NanoQL.Tests/Divergences/SpreadedHazardRateCurveTests.cs)

The spreaded curves' survival: exact, where QuantLib's integrates the hazard rate with its base's 48-point Gauss-Chebyshev rule. On a flat hazard rate of 0.02 (Actual/365 Fixed) with an additive spread of 0.01, or a factor of 0.5, QuantLib reads 0.97044033656246698, 0.8606849301062508 and 0.74077854891200179 at 1, 5 and 10 years, for the exact e^(−0.03t); and on a stepped original (0.01, 0.01, 0.03, 0.06 backward flat on today and one, three and five years out) spread by 0, 0.96078257879155837 and 0.87807505391690122 at two and four years, for the original's 0.96078943915232318 and 0.8781676061138235. The survival is the exact integral of the hazard rate the curve reports, so on originals whose rate isn't their jump-free survival's it is QuantLib's within the 48-point rule's error: a linear survival curve (1, 0.98, 0.94, 0.90, 0.80 today and 1, 3, 5 and 10 years out) and a linear density curve (0.02, 0.02, 0.025, 0.03, 0.03) with a jump of 0.95 at two years, whose rate is the density over the jumped survival; and backward-flat affine curves (0.01, 0.01, 0.015, 0.02, 0.02) on Vasicek models, whose rate is the deterministic part alone. A user's subclass that reports its own rate is integrated with QuantLib's rule, as QuantLib integrates every original: on a structure deriving the one-factor affine survival structure with a deterministic rate of 0.02 (Vasicek r0 = b = 0.02), the additive spread of 0.01 reads QuantLib's 0.8606849301062508 at five years. A piecewise curve of each kind spreads as the interpolated curve on its nodes.

Facts:

- `TheSurvivalIsExact`
- `OnEveryKindOfOriginalTheSurvivalIsTheReportedRates`
- `AUsersSubclassIsIntegratedAsQuantLibDoes`
- `APiecewiseCurveSpreadsAsTheInterpolatedCurveOnItsNodes`
- `ASpreadOfZeroIsTheOriginal`

## SpreadedSmileSectionTypeTests

[`Divergences/SpreadedSmileSectionTypeTests.cs`](../tests/NanoQL.Tests/Divergences/SpreadedSmileSectionTypeTests.cs)

The converted volatility of a spreaded smile section. QuantLib's `SpreadedSmileSection` default-constructs its `SmileSection` base (`spreadedsmilesection.cpp:26-31`), so the base's `volatility(strike, type, shift)` compares the type asked for with an uninitialized member (`smilesection.cpp:131-136`), and the section returns its own volatility or one converted through option prices by chance, observed both ways. nanoql's spreaded section gives its base the underlying section's exercise time, day counter, volatility type and shift, so the comparison reads the underlying's type: asked for its own type and shift, it returns its own volatility.

Facts:

- `TheConvertedVolatilityComparesTheUnderlyingsType`

## SquareRootAndersenDegenerateStepTests

[`Divergences/SquareRootAndersenDegenerateStepTests.cs`](../tests/NanoQL.Tests/Divergences/SquareRootAndersenDegenerateStepTests.cs)

The square-root volatility process's step where its scheme degenerates or leaves its domain. QuantLib's quadratic form takes b² = 2ψ⁻¹ − 1 + √(2ψ⁻¹(2ψ⁻¹ − 1)) (`squarerootandersen.cpp:102-103`), NaN where the conditional variance is 0 (no vol of variance: ψ = 0, a = 0, v = 0·∞), where the variance and its mean are both 0 (an absorbed 0 at θ = 0: ψ = 0/0), and where ψ is below about 1.5e-154 (the product overflows): from v₀ 0.09, θ 0.04, κ 4000 on times 1, 1.0002 and 1.0006, QuantLib's states are finite at vol of variance 3e-76 and NaN at 1e-76 and below (the C++ harness's `SquareRootAndersen.paths`; at κ 1 every state is NaN at 0). nanoql takes the form's limit there, the conditional mean. And QuantLib chooses its form by ψ against any cut point (`:100`), where the quadratic form holds for ψ ≤ 2 only and the exponential one for ψ ≥ 1: a cut below 1 gives ψ below 1 the exponential form, its p no probability, the mean 3.38% low and the variance 12.6% high at ψ 0.5 and cut 0.4 (64 standard errors at 2 million draws), and a cut above 2 gives ψ above 2 the quadratic form's square root of a negative, NaN. nanoql clamps the cut to [1, 2]. Checks: QuantLib's states at the NaN band's edge bit for bit; the conditional mean below it and CIR's deterministic path at 0; an absorbed 0 stays 0; every cut outside [1, 2] QuantLib's own value at 1 or 2 (the vectors hold cuts 1 and 2), bit for bit; the moments restored at cut 0.4; and an invalid process's negative or NaN conditional variance (κθ below 0) still QuantLib's NaN.

Facts:

- `AVanishingVarianceStepsToItsMean`
- `NoVolOfVarianceIsCirsDeterministicPath`
- `AnAbsorbedZeroStaysZero`
- `AnInvalidProcessStaysNaN`
- `ACutOutsideTheDomainIsItsNearestEnd`
- `ACutBelowOneKeepsTheMoments`

## SquareRootAndersenFirstStepTests

[`Divergences/SquareRootAndersenFirstStepTests.cs`](../tests/NanoQL.Tests/Divergences/SquareRootAndersenFirstStepTests.cs)

The square-root volatility process's first step (fixed). QuantLib's `SquareRootAndersen` fills the first evolution step's sub-step lengths but not their e^{−κΔt}, left 0 (`squarerootandersen.cpp:51-52`), so the first step reverts in full: from v₀ 0.16, θ 0.04, κ 1, ε 0.5 the mean of v at 0.5 over 20,000 Mersenne-twister paths is 0.0391 against CIR's 0.1128. QuantLib's `testStochVolForwardsAndOptionlets` prices the SVD-DD evolver's caplets against `AnalyticHestonEngine` within 4 standard errors at θ = v₀ = 1 (`marketmodel.cpp:4326`, `:4330`), where the first step can't show; at v₀ 0.25, v₀ 2 or θ 0.5 its caplets are 6.3 to 38.6 standard errors off, and with the first step filled within 1.71. nanoql takes e^{−κΔt} for the first step's sub-steps too. Checks: QuantLib's own process where its e^{−κΔt} underflows to 0 itself (the vectors), bit for bit; CIR's mean and variance at the first step; and QuantLib's test at v₀ ≠ θ.

Facts:

- `TheFirstStepHasCirsMoments`
- `TheStochasticVolatilityCapletsAreHestonsAtAnyStartingVariance`
- `QuantLibsTestRoutePricesHestonsCapletsAtAnyStartingVariance`

## SquareRootAndersenSmallReversionTests

[`Divergences/SquareRootAndersenSmallReversionTests.cs`](../tests/NanoQL.Tests/Divergences/SquareRootAndersenSmallReversionTests.cs)

The square-root volatility process at small reversion (fixed with an independent check). QuantLib's conditional variance divides (1 − e^{−κΔt}) by κ (`squarerootandersen.cpp:96-97`), which cancels about ε/(κΔt) relative: 9e-5 at κ 1e-12, the variance 0 at 1e-16, so its path is NaN at the next step, and NaN at κ 0, where the limit is v ε² Δt (at κ 1e-16 and 0 from v₀ = θ 0.04, ε 0.5 on times 0.5 and 1, QuantLib's state is NaN after the second step and the first). nanoql takes −expm1(−κΔt)/κ below κΔt = 1e-2, Δt at κ 0, and QuantLib's form at and above it. Checks: the κ → 0 limit, CIR without reversion, at κ 0, 1e-16 and 1e-12; and the two forms' agreement across the cut.

Facts:

- `SmallReversionIsTheLimitWithoutReversion`
- `TheFormsAgreeAcrossTheCut`

## SquareRootAndersenStepsTests

[`Divergences/SquareRootAndersenStepsTests.cs`](../tests/NanoQL.Tests/Divergences/SquareRootAndersenStepsTests.cs)

The square-root volatility process's number of steps (fixed). QuantLib's `SquareRootAndersen::numberSteps` is `dt_.size()*numberSubSteps_` (`squarerootandersen.cpp:80`), where `dt_` already holds every sub-step: 48 for 3 evolution times of 4 sub-steps, though `variatesPerStep` counts per evolution step. nanoql's is the evolution steps a path takes. Checks: the `nextstep` calls a path takes, as QuantLib's own sizing of its path (`vPath_`, one value per sub-step and the start) allows, the next refused.

Facts:

- `ThePathTakesOneStepPerEvolutionTime`

## SquareRootCLVNonCentralityTests

[`Divergences/SquareRootCLVNonCentralityTests.cs`](../tests/NanoQL.Tests/Divergences/SquareRootCLVNonCentralityTests.cs)

The square-root CLV model's non-centrality 4κe^(−κt)x₀/(σ²(1 − e^(−κt))) with 1 − e^(−κt) through expm1 below κt 1e-2 (fixed). QuantLib's 1 − e^(−κt) cancels as κt falls: at κ 1e-4 and a day, κt 2.7e-7, its non-centrality is 7.0e-11 relative off the 40-digit value (mpmath), 3284.99955000002, and the collocation points move with it. nanoql's points, on bounds that don't clamp, are the Gauss nodes at the reference non-centrality.

Facts:

- `ThePointsAreTheNodesAtTheReferenceNonCentrality`

## SquareRootCLVRescaleTests

[`Divergences/SquareRootCLVRescaleTests.cs`](../tests/NanoQL.Tests/Divergences/SquareRootCLVRescaleTests.cs)

The square-root CLV model's collocation points raised to the pMin quantile where QuantLib's rescale puts them below it. QuantLib maps the Gauss nodes by a·x + (xMin − x.front()), which keeps them inside [q(pMin), q(pMax)] at every date of QuantLib's two cases but breaks the lower bound the method states wherever pMin's quantile exceeds the map's lowest point: on the vanilla case's nodes at pMin 1e-3 and pMax 0.999 the lowest point falls at probability 6.5e-5, at pMin 1e-2 at 1.4e-3, and on the mapping case's year date at pMin 1e-3 at 3.5e-4 with no low clamp. nanoql's map is QuantLib's raised by the shortfall: its lowest point at the pMin quantile, the affine map onto [q(pMin), q(pMax)] where the low bound clamps, continuous in pMin.

Facts:

- `ALowClampMapsOntoTheQuantileBounds`
- `TheBandRaisesTheLowestPointToTheQuantile`
- `TheMapIsContinuousInPMin`

## SquareRootProcessTruncationTests

[`Divergences/SquareRootProcessTruncationTests.cs`](../tests/NanoQL.Tests/Divergences/SquareRootProcessTruncationTests.cs)

`SquareRootProcess` below 0: QuantLib's diffusion is σ√x (`squarerootprocess.cpp:40-42`), and its default Euler step x + a(b − x)dt + σ√x√dt·dw leaves [0, ∞), so a path that steps below 0 turns NaN for good. The port's diffusion is σ√max(x, 0), the partial truncation QuantLib's `HestonProcess` takes for its variance (`hestonprocess.cpp:350-359`), the drift on the state as it is. QuantLib's check: the Heston process's partially truncated variance step with no correlation, on the same draws.

Facts:

- `AStepFromANegativeStateIsHestonsPartialTruncation`
- `AnEulerPathBelowZeroStaysFinite`

## SquareRootStationaryDensityTests

[`Divergences/SquareRootStationaryDensityTests.cs`](../tests/NanoQL.Tests/Divergences/SquareRootStationaryDensityTests.cs)

The square-root process's stationary density past α ≈ 170. QuantLib computes β^α·v^{α−1}·e^{−βv−lnΓ(α)} (`squarerootprocessrndcalculator.cpp:71`), whose first factor overflows and the product is NaN from α of about 170 (σ ≤ 0.02 at κ 2, θ 0.04). nanoql computes the same density in logarithms, exp(α ln β + (α − 1) ln v − βv − lnΓ(α)), where the product isn't finite, and QuantLib's product where it is. The check: Boost's gamma density, the stationary law being Gamma(α, θ/α), through the harness (`tools/cxx/src/boost/math/distributions/gamma.cpp`); QuantLib's bits where the product is finite.

Facts:

- `PastTheOverflowTheDensityIsBoostsGammaDensity`
- `WhereTheProductIsFiniteTheDensityIsQuantLibs`

## StandardGFunctionTenorTests

[`Divergences/StandardGFunctionTenorTests.cs`](../tests/NanoQL.Tests/Divergences/StandardGFunctionTenorTests.cs)

The standard g-function's swap length: QuantLib passes the swap index's tenor as its length in the tenor's own units (`conundrumpricer.cpp:130`, `swapTenor_.length()` into a `Size`), so an 18-month index reads as 18 years: on the wheel, on a flat 3% curve and a 20% volatility, a 1-year coupon fixing in 10 years on an 18-month index with an annual fixed leg prices 0.030653808259814307 on the analytic Hagan pricer's standard model, a convexity adjustment of 3.04e-4 against the exact-yield model's 7.13e-5. Read in years the length doesn't repair it: that swap starts with a 6-month stub, and the payment's delay δ is counted in the first period's length (`:126`), where the model presumes equal periods, so the adjustment comes out −3.25e-4. nanoql reads the length in years where the tenor is a whole number of fixed-leg periods (18 months on a semiannual leg, 3 periods), and refuses the standard model otherwise, naming the exact-yield model, which reads the schedule. QuantLib's checks: its own whole-year tenors, and its exact-yield model on the same swap.

Facts:

- `WholeYearTenorsKeepQuantLibsValues`
- `WholePeriodsAreReadInYears`
- `AStubSwapIsRefused`

## StatePriceCacheTests

[`Divergences/StatePriceCacheTests.cs`](../tests/NanoQL.Tests/Divergences/StatePriceCacheTests.cs)

A tree's state prices read past the rows its construction stored. QuantLib's `TreeLattice::statePrices` keeps every row it computes in a `mutable` cache (`lattice.hpp:87, 112-117`), and `setSpread` changes only the tree's spread (`onefactormodel.hpp:105-108`), so a row read before the spread is set keeps its unspread values afterwards, and a row first read after it carries the spread. nanoql's lattices keep no row beyond those their construction stored, and `SetSpread` returns a copy: the copy's later rows carry the spread whatever was read before, so reading a row changes nothing.

Facts:

- `ARowReadBeforeTheSpreadIsSetDoesntKeepItsUnspreadValues`

## StatisticsUndefinedInputsTests

[`Divergences/StatisticsUndefinedInputsTests.cs`](../tests/NanoQL.Tests/Divergences/StatisticsUndefinedInputsTests.cs)

Inputs for which QuantLib's statistics are undefined are refused (playbook §2's undefined-path row): a NaN sample where the percentiles sort the samples with `std::sort`, whose ordering a NaN breaks (generalstatistics.hpp:247-252), with a `NanoQLException`; and fewer weights than samples given to `AddSequence`, whose loop reads past the weights' end (generalstatistics.hpp:174-179, incrementalstatistics.hpp:142-147), with an `ArgumentException` naming them. Where nothing sorts, a NaN sample is read as QuantLib reads it.

Facts:

- `ANaNSampleCantBeSorted`
- `FewerWeightsThanSamplesAreRefused`

## StatisticsZeroWeightTests

[`Divergences/StatisticsZeroWeightTests.cs`](../tests/NanoQL.Tests/Divergences/StatisticsZeroWeightTests.cs)

Where the samples a member averages weigh 0 in all, the statistics refuse, where QuantLib divides 0 by 0 and returns NaN: `GeneralStatistics.ExpectationValue(Func{double, double}, Func{double, bool})`, which every empirical member reads, refuses with the text QuantLib's percentile gives a set of zero weight, "empty sample set" (generalstatistics.hpp:115-133, generalstatistics.cpp:94-95); and `IncrementalStatistics`'s skewness and kurtosis refuse as its mean does, "sampleWeight_= 0, unsufficient" (incrementalstatistics.cpp:42). QuantLib's check: its own refusals.

Facts:

- `EveryEmpiricalMemberRefusesSamplesOfNoWeight`
- `IncrementalSkewnessAndKurtosisRefuseSamplesOfNoWeight`

## StochasticProcessDiscretizationRefusalTests

[`Divergences/StochasticProcessDiscretizationRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/StochasticProcessDiscretizationRefusalTests.cs)

`StochasticProcess`'s and `StochasticProcess1D`'s defaults on a process built without a discretization. QuantLib's expectation, standard deviation, covariance and variance call the discretization through a pointer (`stochasticprocess.cpp:36-52`, `:78-88`), which is null when the process was built without one, a result C++ leaves undefined. nanoql refuses with `NanoQLException`, "no discretization given".

Facts:

- `TheDefaultsRefuseWithoutADiscretization`

## StrippedOptionletAdapterEmptyStrikesTests

[`Divergences/StrippedOptionletAdapterEmptyStrikesTests.cs`](../tests/NanoQL.Tests/Divergences/StrippedOptionletAdapterEmptyStrikesTests.cs)

An adapter on a stripped optionlet with no strikes. No check of QuantLib's refuses empty strikes (`strippedoptionlet.cpp:84-115`), and its adapter, which builds its strike interpolations at its first calculation, builds. That calculation refuses them: its linear interpolation over the first date's strikes (`strippedoptionletadapter.cpp:109`) throws "not enough points to interpolate: at least 2 required, 0 provided" (`interpolation.hpp:83-86`), the wheel's text at its first extrapolated volatility. But its `minStrike()` and `maxStrike()` read the front and back of the empty strikes before any calculation (`strippedoptionletadapter.cpp:128, 132`), which a volatility without extrapolation reaches through its strike check: the wheel's process ends with an access violation, 0xC0000005, in 3 of 3 runs. nanoql's adapter does QuantLib's first calculation when it is built, so it refuses the empty strikes there, with QuantLib's text, as an `ArgumentException` naming `stripper`, before the reads; and a base with no fixing dates, which builds no interpolation, is refused by `MinStrike` and `MaxStrike` with `NanoQLException`, "no strikes given".

Facts:

- `EmptyStrikesAreRefused`

## StrippedOptionletStrikesTextTests

[`Divergences/StrippedOptionletStrikesTextTests.cs`](../tests/NanoQL.Tests/Divergences/StrippedOptionletStrikesTextTests.cs)

The refusal of a stripped optionlet's non-increasing strikes at a fixing date after the first. QuantLib checks each date's strikes but prints the first date's (`strippedoptionlet.cpp:106-113`), and reads past them where the first date has fewer: strikes 2%, 3%, 4% at the first date and 2%, 3.5%, 3% at the second give QuantLib's "non increasing strikes at date 1: 2nd is 3.000000 %, 3rd is 4.000000 %", the first date's. nanoql prints the offending date's strikes, "non increasing strikes at date 1: 2nd is 3.500000 %, 3rd is 3.000000 %", and a shorter first date reads nothing past its end.

Facts:

- `TheTextPrintsTheOffendingDatesStrikes`

## StrippedOptionletTimesTests

[`Divergences/StrippedOptionletTimesTests.cs`](../tests/NanoQL.Tests/Divergences/StrippedOptionletTimesTests.cs)

A stripped optionlet's fixing times after the evaluation date moves. QuantLib's `StrippedOptionlet` computes its times from the evaluation date at construction and never again (`strippedoptionlet.cpp:77-81`), while its adapter's reference date moves with the evaluation date: on 16 June 2025, with two settlement days, fixing dates 16 December 2025, 16 June 2026 and 16 December 2026, volatilities 20%/19%, 22%/21% and 25%/24% at 2% and 3%, the wheel's times are 0.4958904109589041, 0.9945205479452055 and 1.4958904109589042 and its adapter gives 0.22 at the second date and 2%; with the evaluation date moved to 16 September the times stay, the adapter's reference date is 18 September, and it gives 0.2098901098901099 there; a stripped optionlet built on 16 September has the times 0.24383561643835616, 0.7424657534246575 and 1.2438356164383562 and gives 0.22. nanoql's objects have fixed dates: an adapter is built under a context. The stripped optionlet's times are frozen as QuantLib's are, so its adapter under the later context gives QuantLib's moved value, and the optionlet and adapter built under that context QuantLib's fresh one.

Facts:

- `TheTimesAreTheConstructionsAndTheAdapterIsTheContexts`

## StudentDistributionTests

[`Divergences/StudentDistributionTests.cs`](../tests/NanoQL.Tests/Divergences/StudentDistributionTests.cs)

The Student t density at large degrees of freedom and the cumulative function at large ν: QuantLib takes each gamma function as the exponential of its logarithm, which overflows from n 342 (0 there, NaN from 343), and its denominator's product overflows earlier at large x (0 at n 330, x 30); nanoql takes the gammas' ratio as Boost's tgamma_delta_ratio and the power by log1p wherever a factor overflows, to n = 2³¹ − 1. QuantLib's cumulative function takes the incomplete beta's complement at 1 − n/(n + x²), which loses the argument's digits as n grows (6.4e-10 relative at 1e6, 4.6e-9 at 1e8); nanoql takes the complementary argument x²/(n + x²) directly where n/(n + x²) exceeds ½. Below the centre QuantLib's ½ − ½·(1 − I) cancels as the probability falls (1.3e-6 relative at n 10, x −30, and 0 for 3.1e-24 at n 30); nanoql takes ½·I directly. Both through Boost's incomplete beta, as QuantLib's own loses digits in its log-gamma prefactor as n grows; at n 1 below the centre, ½·(1 − I), as Boost's complement there rounds 1 − z. And the quantile at n 4 and 6, whose Boost forms cancel near ½ (±0 within 1e-9 of it), takes one Newton step on the cumulative function there. The references are 50-digit evaluations of the definitions; QuantLib's values beside them are its own.

Facts:

- `TheDensityAtLargeDegreesOfFreedomIsFinite`
- `TheDensityComputesAtTheLargestDegreesOfFreedom`
- `WhereQuantLibsFactorsAreFiniteItsDensityIsKept`
- `TheCumulativeFunctionKeepsItsDigitsAtLargeNu`
- `TheLowerTailKeepsItsDigits`
- `AtOneDegreeOfFreedomBelowTheCentreTheCauchyDistributionKeepsItsDigits`
- `AtFourAndSixDegreesOfFreedomTheQuantileNearTheCentreIsItsCumulativeFunctionsInverse`
- `AboveTheCentreQuantLibsFormulaIsKept`
- `AtSmallNuTheComplementaryArgumentAgreesWithQuantLib`

## StulzDegenerateTests

[`Divergences/StulzDegenerateTests.cs`](../tests/NanoQL.Tests/Divergences/StulzDegenerateTests.cs)

Stulz's engine where the assets move together or one doesn't move. At a correlation of 1 with equal volatilities, or at two zero volatilities, its combined variance is 0 or a rounding negative, and QuantLib's derived correlations are NaN or infinite, refused by the bivariate normal ("rho must be >= -1.0 (-nan(ind) not allowed)", `stulzengine.cpp:44-48`); at one zero volatility its bivariate normal reads an infinite bound and returns NaN (We04DP's, `Divergences/BivariateInfiniteArgumentTests.cs`). nanoql prices the co-moving limit, the minimum the asset of the smaller forward and the maximum that of the larger by Black's formula, and the bivariate normal's limit at an infinite bound: QuantLib's `blackFormula` (15.7766093, between Stulz's minimum 15.7766090 and maximum 15.7766097 at ρ = 1 − 1e-15 on the test's market), QuantLib's own value near the limit, and the deterministic value are the checks.

Facts:

- `AssetsMovingTogetherPriceBlacksFormula`
- `OneZeroVolatilityPricesItsLimit`
- `TwoZeroVolatilitiesPriceTheDeterministicValue`

## StulzNegativeStrikeTests

[`Divergences/StulzNegativeStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/StulzNegativeStrikeTests.cs)

Stulz's engine at a negative strike. QuantLib's `log(forward1/strike)` (`stulzengine.cpp:62`) makes the minimum NaN and Black's check refuses the maximum ("strike + displacement (-10 + 0) must be non-negative"), where both payoffs are linear: the call is the call struck at zero less the discounted strike, the put worth nothing (96.0373 and 117.9873 at K = −10 on the test's market, the Monte Carlo engine 96.067 and 118.007). nanoql prices those forms: QuantLib's own K = 0 values less K·D, and the Monte Carlo engine, are the checks.

Facts:

- `ACallIsTheZeroStrikeCallLessTheDiscountedStrike`
- `APutIsWorthNothing`

## SuoWangDoubleBarrierTests

[`Divergences/SuoWangDoubleBarrierTests.cs`](../tests/NanoQL.Tests/Divergences/SuoWangDoubleBarrierTests.cs)

The Suo-Wang double-barrier engine's four classes (fixed). QuantLib clips one strike limit only (`suowangdoublebarrierengine.cpp:54-55`, #2881's class), so a knock-out call struck above H prices −0.1694572673, a put struck below L −0.3467932631, and the knock-in call 0.9992298671 against the analytic engine's 0.8297725998; it sums ten terms from −5 to 4 (`:77`), 0.004975355507 on a narrow corridor over two years where 50 terms give −3.5e-13 and the binomial engine 1e-70; it ignores the rebate, 1.454185879 with a rebate of 5 as without where the binomial Derman-Kani engine gives 3.982 (knock-in 6.818712906 against 9.194); and its powers overflow to NaN at a volatility of 1e-4 and of 0 (QuantLib 1.44). nanoql clips both limits to the corridor, converges the series, prices the rebate as the analytic engine does and takes the analytic engine's knock-out where a term overflows. Checks: the analytic engine, QuantLib's binomial Derman-Kani values, the deterministic limit.

Facts:

- `AKnockOutStruckOutsideTheCorridorPricesNothing`
- `AKnockInStruckAboveTheCorridorIsTheAnalyticEngines`
- `ANarrowCorridorOverTwoYearsIsWorthItsConvergedSeries`
- `ARebateIsPaidAsTheAnalyticEnginePaysIt`
- `AtLowVolatilityTheKnockOutIsTheAnalyticEngines`
- `AtAVolatilityOfAFewPercentCancellingSumsGiveWayToTheAnalyticEngine`
- `TheKnockOutIsTheAnalyticEnginesWithinTheSumsAgreement`
- `QuantLibsValueStandsWhereItsTermsHaveConverged`

## SvdNonFiniteRefusalTests

[`Divergences/SvdNonFiniteRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/SvdNonFiniteRefusalTests.cs)

The singular value decomposition of a non-finite matrix. QuantLib's main loop has no test for too many iterations (`svd.cpp:276-280`, "Here is where a test for too many iterations would go"): on a 5 × 3 matrix with one NaN or one ∞ it runs past 20 seconds (QuantLib 1.44). And the American Monte Carlo engine accepts the second-kind Chebyshev basis, √w(x)Pₙ(x) with w = (1 − x)^½(1 + x)^½, NaN above 1: a call regresses its in-the-money paths, S/K > 1, so every regression meets NaN columns and QuantLib's engine hangs. nanoql refuses a non-finite entry on entry, naming it, and the engine's call reaches its caller refused, as raised. A finite matrix whose singular values lie past the range of doubles never ends either, in QuantLib and in the port of its arithmetic; nanoql refuses it at the main loop's first non-finite singular value or superdiagonal entry, which none of 578 matrices QuantLib decomposes reaches (a probe over entries from 1e290 to the limit, four shapes). Each fact runs under a timeout, since without the refusal it doesn't return.

Facts:

- `ANonFiniteEntryIsRefusedNamingIt`
- `AFiniteMatrixWhoseSingularValuesLeaveTheDoublesIsRefused`
- `TheSecondKindChebyshevBasisOnACallIsRefusedThroughIt`

## SviFixedParameterTests

[`Divergences/SviFixedParameterTests.cs`](../tests/NanoQL.Tests/Divergences/SviFixedParameterTests.cs)

SVI fits with b or a fixed (fixed). QuantLib's direct transform keeps a free b inside b(1+|ρ|) < 4 and a free a inside a + bσ√(1−ρ²) > 0, but copies a fixed one as given while ρ, σ and b move (`sviinterpolation.hpp:112-128`), and every cost evaluation builds a section whose parameters' check refuses the excess (`xabrinterpolation.hpp:291-306`, `svismilesection.cpp:39-46`): on a seven-strike skew (a year, forward 100) QuantLib's fit throws midway with b fixed at 3.5 ("b(1+|rho|) must be less than or equal to 4, (b=3.5, rho=0.250507)"), with a, b and σ fixed at −0.05, 1 and 0.1 (at ρ 0.929163), and with a and σ fixed at −0.1 and 0.05 (at b 0.069537) (the C++ harness). Here, where a trial would fail QuantLib's check, the free parameters move onto the set it admits, each rounded inward: ρ capped first at the tightest bound the fixed parameters set, then σ or b raised to meet a's constraint. A fixed set no ρ admits is refused before the fit, with QuantLib's text. A trial the check passes is QuantLib's, so a fit QuantLib completes is QuantLib's. QuantLib's check: a fresh section at the reported parameters reproduces the fitted volatilities and error.

Facts:

- `AFitWithBFixedAt3Point5Completes`
- `AFitWithABAndSigmaFixedCapsRho`
- `AFitWithAAndSigmaFixedRaisesBAndCapsRho`
- `AFixedBAbove4IsRefusedBeforeTheFit`

## SviTransformEdgeTests

[`Divergences/SviTransformEdgeTests.cs`](../tests/NanoQL.Tests/Divergences/SviTransformEdgeTests.cs)

SVI's inverse transforms at their edges. QuantLib's inverse (`sviinterpolation.hpp:98-108`) takes `asin` of ρ/0.999999, `√(σ − 10⁻⁶)`, `√(a − 10⁻⁶ + bσ√(1−ρ²))` and `tan` of an argument that passes π/2 once b(1+|ρ|) exceeds 4·0.999999, each of which a value its parameters' check accepts can push out of range: on a seven-strike skew (a year, forward 100) QuantLib's fit refuses a fixed ρ of 0.9999995 ("b (nan) must be non negative"), a fixed σ of 5·10⁻⁷ ("sigma (-nan(ind)) must be positive") and an a guessed at −bσ√(1−ρ²) ("a + b sigma sqrt(1-rho^2) (a=-nan(ind), …) must be non negative"), and fits a b guessed at 4/(1+|ρ|) from a wrapped guess (the C++ harness). Here each argument is clamped into its transform's range (`asin`'s to [−1, 1], each square root's floored at 0, the `tan` argument capped at π/2), as SABR's and ZABR's transforms are clamped. QuantLib's check: its own fit from the clamped value, which the direct transform maps back to.

Facts:

- `ARhoFixedBeyondTheTransformsRangeFitsAsItsClampedValue`
- `ASigmaFixedBelowTheTransformsRangeFitsAsItsClampedValue`
- `AnAGuessedAtItsConstraintsEdgeFitsAsItsClampedValue`
- `ABGuessedAtItsConstraintsEdgeFitsAsItsClampedValue`

## SwapIndexNullIndexTests

[`Divergences/SwapIndexNullIndexTests.cs`](../tests/NanoQL.Tests/Divergences/SwapIndexNullIndexTests.cs)

A swap index given a null Ibor index, or an overnight-indexed swap index given a null overnight index, is refused on entry, naming the parameter. QuantLib's `SwapIndex` accepts a null Ibor index (`swapindex.cpp:38-43`) and dereferences it later, in `forwardingTermStructure()`, `underlyingSwap()` and the clones (`:65, 85, 95, 125, 136, 150`), and `OvernightIndexedSwapIndex` dereferences its overnight index in its base's initializer (`:193, 196`): results C++ leaves undefined, where the wheel stops on Boost's assertion.

Facts:

- `ASwapIndexGivenANullIborIndexIsRefused`
- `AnOvernightIndexedSwapIndexGivenANullOvernightIndexIsRefused`

## SwapRatePcInitialStateTests

[`Divergences/SwapRatePcInitialStateTests.cs`](../tests/NanoQL.Tests/Divergences/SwapRatePcInitialStateTests.cs)

The swap-rate evolvers' initial state from another curve state (fixed). QuantLib's `LogNormalCotSwapRatePc` and `LogNormalCmSwapRatePc::setInitialState` cast the state to their own type and dereference the result (`lognormalcotswapratepc.cpp:92-93`, `lognormalcmswapratepc.cpp:93-94`), null for any other: given an `LMMCurveState` the process stops (0xC0000005, 3 runs of 3). nanoql reads the base's coterminal and constant-maturity swap rates, which every curve state answers. Checks: on the evolver's own state type QuantLib's call, the same rates, so the same evolution bit for bit (the vectors set the state there); and from an LMM or another state on the same curve, that evolution again.

Facts:

- `TheCoterminalEvolverStartsFromAnyStatesCoterminalRates`
- `TheConstantMaturityEvolverStartsFromAnyStatesSwapRates`

## SwapRefusalTests

[`Divergences/SwapRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/SwapRefusalTests.cs)

The swaps' and their builders' undefined paths, each refused before the undefined step (the playbook's undefined-path rule).

Facts:

- `MakeOISRefusesANullIndex`
- `MakeVanillaSwapRefusesANullIndex`
- `NegativeCountsAreRefused`

## SwaptionHelperArgumentTests

[`Divergences/SwaptionHelperArgumentTests.cs`](../tests/NanoQL.Tests/Divergences/SwaptionHelperArgumentTests.cs)

A swaption helper on a null index. QuantLib's constructors keep the index unchecked and its first calculation dereferences it (`swaptionhelper.cpp:153`), which the wheel's build refuses with "Boost assertion failed: px != 0" at the helper's first use, its construction succeeding. nanoql's helper computes at construction and refuses the null index there, with `ArgumentNullException` naming it, in each constructor.

Facts:

- `ANullIndexIsRefusedAtConstruction`

## SwaptionHelperAtParTests

[`Divergences/SwaptionHelperAtParTests.cs`](../tests/NanoQL.Tests/Divergences/SwaptionHelperAtParTests.cs)

When a swaption helper reads the at-par switch. QuantLib's helper builds its swaps, whose Ibor legs read the global switch, at its first calculation and again at each recalculation after a notification (`swaptionhelper.cpp:152-199, 212-214`; `calibrationhelper.hpp:73`), so a helper built at par gives the indexed swap's values once the switch is flipped and its quote moves (the wheel: a 1Y x 5Y helper's market value 0.011261310927040481 at par, then 0.011261495561724687, the value of a helper built with indexed coupons). nanoql's helper builds its swaps under the context it is given at construction, and keeps them: its prices under a context with the other switch are its own swap's.

Facts:

- `TheAtParSwitchIsReadAtConstruction`

## SwaptionImpliedVolatilityGridTests

[`Divergences/SwaptionImpliedVolatilityGridTests.cs`](../tests/NanoQL.Tests/Divergences/SwaptionImpliedVolatilityGridTests.cs)

The frozen-coefficient swaption volatility on an evolution that doesn't hold its expiry (a refusal). QuantLib's `SwapForwardMappings::swaptionImpliedVolatility` sums every step starting before the expiry (`swapforwardmappings.cpp:194`, `:216-217`), so a step straddling it adds the later variance: FlatVol at 20% on eleven semi-annual rates at 4%, yearly steps, swaption (0, 2) 0.221024 against 0.197607 on every fixing (+11.9%), (0, 10) +34.3%, (2, 10) +12.0%; and it reads the first displacement alone (`:183`). QuantLib's frozen-coefficient adapters refuse both ("skipping … rate time", "not equal to the previous ones", `fwdtocotswapadapter.cpp:44`, `:50-57`), and so does nanoql here. A grid that holds the expiry keeps QuantLib's value: (1, 4) on yearly steps is the every-fixing value, 0.195828.

Facts:

- `AnExpiryOffTheEvolutionIsRefused`
- `AnExpiryOnTheEvolutionKeepsTheEveryFixingValue`
- `UnequalDisplacementsAreRefused`

## SwaptionImpliedVolatilityTests

[`Divergences/SwaptionImpliedVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/SwaptionImpliedVolatilityTests.cs)

An implied volatility whose bracket starts at −1. QuantLib's helper starts its volatility quote at −1.0, "so that calculation is forced at first ... call", and runs the engine only when the volatility asked differs from the quote (`swaption.cpp:55-92`, lines 61-64 and 87-91); the solver's first call is at the bracket's lower end (`math/solver1d.hpp:187`), so a lower end of −1.0 skips the engine and reads its NPV result, which nothing has set (`instrument.hpp:121`, `pricingengine.hpp:63-73`): an indeterminate value, and a run-dependent result. nanoql's helper prices at every call: at −1.0 the price is the price at volatility 1, the variance being the volatility squared, so the result depends on the inputs alone.

Facts:

- `ABracketStartingAtMinusOnePricesAtVolatilityOne`

## SwaptionNullArgumentTests

[`Divergences/SwaptionNullArgumentTests.cs`](../tests/NanoQL.Tests/Divergences/SwaptionNullArgumentTests.cs)

A swaption given no swap or no exercise is refused on entry, with `ArgumentNullException` naming the parameter. QuantLib's constructor takes both unchecked and dereferences them before its arguments' checks ("swap not set", "exercise not set", `swaption.cpp:177-178`) could refuse them: results C++ leaves undefined, where the wheel stops on Boost's assertion.

Facts:

- `ASwaptionWithoutASwapIsRefused`
- `ASwaptionWithoutAnExerciseIsRefused`

## SwaptionVolatilityEmptyGridTests

[`Divergences/SwaptionVolatilityEmptyGridTests.cs`](../tests/NanoQL.Tests/Divergences/SwaptionVolatilityEmptyGridTests.cs)

A discrete swaption volatility structure on no option tenors, no option dates or no swap tenors is refused with `ArgumentException` naming the parameter: "no option tenors given", "no option dates given", "no swap tenors given". QuantLib's `SwaptionVolatilityDiscrete` reads the first element of each in its checks (`optionTenors_[0]`, `optionDates_[0]`, `swapTenors_[0]`, `swaptionvoldiscrete.cpp:137, 125, 148`), before any check of its subclass could refuse the grid: a read past an empty vector's end, undefined, which crashes the wheel. nanoql refuses each where QuantLib reads it, in QuantLib's order.

Facts:

- `AnEmptyGridIsRefused`

## SwingExerciseDateTimesTests

[`Divergences/SwingExerciseDateTimesTests.cs`](../tests/NanoQL.Tests/Divergences/SwingExerciseDateTimesTests.cs)

`SwingExercise`'s date times. QuantLib's Bermudan base sorts the dates and leaves the seconds in the order given (`vanillaswingoption.cpp:52-69`), so date times given out of order pair each second with another date: days 3 and 1 at 0 and 43,200 seconds become day 1 at 0 and day 3 at 43,200; and its fixed step moves one day whenever the seconds pass a day (`vanillaswingoption.cpp:37-48`), so a two-day step from day 1 to day 9 exercises on each of days 1 to 9 (QuantLib 1.44). nanoql sorts the date times together, and a step moves as many days as it holds. Given in order, and at steps under a day, QuantLib's date times.

Facts:

- `DateTimesGivenOutOfOrderKeepTheirPairs`
- `AStepOfDaysMovesThatManyDays`

## SwingExerciseSecondsTests

[`Divergences/SwingExerciseSecondsTests.cs`](../tests/NanoQL.Tests/Divergences/SwingExerciseSecondsTests.cs)

The simple swing engine on exercises within a day. QuantLib's engine times each exercise by its date (`fdsimplebsswingengine.cpp:72-76`) where `SwingExercise::exerciseTimes` adds its seconds (`vanillaswingoption.cpp:83-97`): 60 half-daily exercises price as the 30 daily ones, 6.0120197306323737 with 5 rights and 11.479815522463735 with 10, on a put forward at 30, spot 30, r 14%, q 2%, σ 40%, Actual/Actual (ISDA) from 15 October 2026 (QuantLib 1.44). nanoql times them by the exercise's date times on the engine's clock, the maturity the last of them. The check, independent of QuantLib: with every right forced (the minimum and the maximum the number of exercises) the value is the sum of the forward payoffs' values at the exercise times, K·D(t) − S₀·D_q(t), a closed form.

Facts:

- `WithEveryRightForcedTheValueIsTheForwardsAtTheExerciseTimes`
- `HalfDailyExercisesAreWorthMoreThanDailyOnes`

## SymmetricSchurNaNSortTests

[`Divergences/SymmetricSchurNaNSortTests.cs`](../tests/NanoQL.Tests/Divergences/SymmetricSchurNaNSortTests.cs)

A NaN eigenvalue is refused before the eigenvalues are sorted (playbook §2's undefined-path row): QuantLib sorts the (eigenvalue, eigenvector) pairs with `std::sort` and `std::greater` (symmetricschurdecomposition.cpp:116-124), whose ordering a NaN breaks. A NaN on a diagonal whose other elements are 0 ends the Jacobi loop at once and reaches the sort; a matrix of one row sorts nothing, and keeps QuantLib's NaN.

Facts:

- `ANaNEigenvalueCantBeSorted`

## TCopulaQuantileCentreTests

[`Divergences/TCopulaQuantileCentreTests.cs`](../tests/NanoQL.Tests/Divergences/TCopulaQuantileCentreTests.cs)

The Student t copula policy's quantile near ½ at 4 and 6 degrees of freedom, one Newton step on Boost's cumulative function there (as the inverse cumulative Student distribution does, applied where the policy reads Boost's quantile): Boost's quantile takes Shaw's forms, which cancel near ½, returning ±0 for ±2.67e-9 within 1e-9 of ½ and 58% off at ½ − 1e-8 at 4 degrees of freedom (`Numerics.InverseCumulativeStudent`). The convolution refuses even orders, so only a policy built without variables reads them. QuantLib's value is Boost's quantile times the variance factor; the check is Boost's cumulative function at the result, which returns the probability within the quantile's conditioning.

Facts:

- `TheQuantileInvertsTheCumulativeNearTheCentre`

## TRBDF2Tests

[`Divergences/TRBDF2Tests.cs`](../tests/NanoQL.Tests/Divergences/TRBDF2Tests.cs)

The old TR-BDF2's trapezoidal stage: QuantLib builds the stage's implicit part at t − Δt, where the stage ends at t − αΔt, so a time-dependent operator steps at first order; nanoql builds it at t − αΔt. On u_t = c(t)·u_xx on 101 points from sin(πx), rolled back from 1 to 0 with Dirichlet rows, sin(πx) is the discrete operator's eigenvector, so the semi-discrete solution at x ½ is exp(−λ_h ∫c), λ_h = (2/h²)(1 − cos πh), and the errors are the scheme's own. At c = 1 nanoql's values are QuantLib's, bit for bit; at c = 1 + t nanoql's errors fall fourfold per halving, QuantLib's don't. QuantLib's values are its own.

Facts:

- `AtAConstantOperatorQuantLibsSchemeIsKept`
- `AtATimeDependentOperatorTheSchemeIsSecondOrder`

## TelescopicValueDatesTests

[`Divergences/TelescopicValueDatesTests.cs`](../tests/NanoQL.Tests/Divergences/TelescopicValueDatesTests.cs)

A coupon built with `telescopicValueDates`. QuantLib builds its daily value dates only to 7 business days after the evaluation date it is built with, then jumps to the lockout or end dates (`overnightindexedcoupon.cpp:107-132`), and wherever the skipped dates are read its results are wrong, with no error. nanoql builds every daily value date whatever the flag says (the flag keeps its check and, on an averaged coupon, its choice of the averaging pricer's approximation), so a telescopic coupon gives QuantLib's full-schedule result: here, each expected value is QuantLib's for the same coupon built without the flag, the approximating pricer set on an averaged one, as the flag sets it.

Facts:

- `ATelescopicCouponGivesTheFullSchedulesResults`
- `ATelescopicCouponListsEveryDailyDate`
- `TheBasisHelpersTelescopicCouponsListEveryDailyDate`
- `ALockoutLegsNPVIsTheFullSchedules`

## TenorOptionletExtrapolationTests

[`Divergences/TenorOptionletExtrapolationTests.cs`](../tests/NanoQL.Tests/Divergences/TenorOptionletExtrapolationTests.cs)

The tenor optionlet structure's range checked once, by the query (fixed). QuantLib's `volatilityImpl` builds the section through `smileSection(optionTime)`, which checks the range again without the caller's extrapolation flag (`tenoroptionletvts.hpp`), so a query allowed to extrapolate past the base structure's last date refuses. On QuantLib 1.44 (through the C++ harness: test-suite/basismodels.cpp's caplet volatilities on 29 October 2026, the case's indexes, ρ∞ 0.3 and β 0.9) the 30-year caplet's volatility with extrapolation refuses, "time (30.0219) is past max curve time (30.0137)", where its smile section built with extrapolation gives 0.0059574843004036775, 0.006438206493492846 and 0.006675293658033079 at 0, 1% and 2%; at 25 years the two agree. nanoql's volatility is that section's. QuantLib's own `testTenoroptionletvts` asks for its 30-year caplets so and refuses on every evaluation date of 29 October to 11 November 2026 but the two Wednesdays; on its test runner's date, 16 September 2015, a Wednesday, it passes, so QuantLib's own run doesn't refuse.

Facts:

- `AThirtyYearCapletExtrapolatesToItsSmileSection`

## TenorSwaptionLengthTests

[`Divergences/TenorSwaptionLengthTests.cs`](../tests/NanoQL.Tests/Divergences/TenorSwaptionLengthTests.cs)

The tenor swaption structure's swaps at the swap length in whole months (fixed). QuantLib casts the swap length to whole years before it multiplies by twelve (`((BigInteger)swapLength * 12.0)`, `tenorswaptionvts.cpp`), so an 18-month tenor builds 12-month swaps: on QuantLib 1.44 (through the C++ harness: test-suite/basismodels.cpp's markets on 15 June 2026, Euribor 6M volatilities to a true Euribor 3M) the at-the-money level at a 1-year option on an 18-month swap is −0.00018491732414727124, the 12-month swap's, and at a 5-year option 0.010348353529043619. nanoql's 18-month level is the fair rate of the 18-month target swap, the vanilla swap on the 18-month schedule; whole-year tenors are QuantLib's, the 12-month level QuantLib's to the last bits.

Facts:

- `AnEighteenMonthSwapIsEighteenMonthsLong`

## TimeSeriesFewerValuesTests

[`Divergences/TimeSeriesFewerValuesTests.cs`](../tests/NanoQL.Tests/Divergences/TimeSeriesFewerValuesTests.cs)

QuantLib's constructor from two sequences reads one value per date from the values' start, with no bound (`timeseries.hpp:64-69`: `values_[*(dBegin++)] = *(vBegin++)`), so fewer values than dates reads past their end, a result C++ leaves undefined. nanoql refuses it with `ArgumentException` on `values`, "fewer values than dates", and builds nothing. Surplus values stay unread, as in QuantLib.

Facts:

- `FewerValuesThanDatesAreRefused`

## TimeSeriesFindTests

[`Divergences/TimeSeriesFindTests.cs`](../tests/NanoQL.Tests/Divergences/TimeSeriesFindTests.cs)

QuantLib's `TimeSeries::find` inserts a date the series doesn't hold, with a `Null<T>()` datum, and returns an iterator to it (`timeseries.hpp:233-242`), as its non-const `operator[]` does (`timeseries.hpp:102-105`): `testUtilities` checks that the size grows from 3 to 4 (`test-suite/timeseries.cpp:173-174`). nanoql's series is immutable and ports neither: `TimeSeries{T}.TryGetValue` reads a missing date as absent and leaves the series unchanged.

Facts:

- `LookingUpAMissingDateLeavesTheSeriesUnchanged`

## TqrEigenDecompositionCycleTests

[`Divergences/TqrEigenDecompositionCycleTests.cs`](../tests/NanoQL.Tests/Divergences/TqrEigenDecompositionCycleTests.cs)

`TqrEigenDecomposition` where QuantLib's iteration never returns. QuantLib loops while an off-diagonal element is above its test (`tqreigendecomposition.cpp:47`), with no bound on the iterations: under `NoShift` it never returns on eigenvalues in pairs of opposite sign, and under any strategy on a NaN, an infinite off-diagonal element or entries whose squares overflow, which QuantLib's Gauss-Jacobi quadrature reaches with parameters of 1e200 (its recurrence's alpha is ∞ − ∞, `gaussianorthogonalpolynomial.cpp:119`). While one eigenvalue converges, the next step is a function of the diagonal and off-diagonal alone, so a state repeated bit for bit proves the iteration endless: nanoql's cycle test refuses there, and every input QuantLib answers keeps QuantLib's result. Each input below never returns in QuantLib (killed at 5 s in the C++ harness, 10 s on the wheel). The test catches the inputs whose state repeats, as a zero diagonal's does, but not every pair: under `NoShift` a pair whose magnitudes differ slightly converges at their ratio without repeating a state, and one equal only to rounding may never repeat one either. Such an input runs as long as QuantLib's, without end once the ratio rounds to 1: a QuantLib defect kept, since no bound on the iterations would separate it from slow inputs QuantLib answers.

Facts:

- `PairedEigenvaluesUnderNoShiftAreRefused`
- `ANaNOffDiagonalIsRefused`
- `AGaussJacobiQuadratureWithOverflowingParametersIsRefused`

## TrBdf2TimeDependentTests

[`Divergences/TrBdf2TimeDependentTests.cs`](../tests/NanoQL.Tests/Divergences/TrBdf2TimeDependentTests.cs)

`TrBDF2Scheme{TTrapezoidalScheme}` with time-dependent coefficients. QuantLib's step sets the operator over its trapezoidal sub-step's interval, [t − α·dt, t], and solves its BDF2 stage with that operator (`trbdf2scheme.hpp:106-124`), so where a curve's rate moves within the step the stage reads the wrong one: first order, or stalling. nanoql sets the operator once over the whole step, [t − dt, t], the interval QuantLib's other schemes set, for both stages. On a zero curve whose forward rate jumps at half a year, an at-the-money call priced on 800 points lies within 1e-3 of the analytic engine at 365 and 640 steps, where QuantLib's engine errs by 2.7e-2 and 2.0e-2. QuantLib's TR-BDF2 cases are all on flat inputs, where the two forms differ in the last bits only (the scheme's vectors, within the tolerance).

Facts:

- `BothStagesOverTheWholeStepConvergeOnATimeDependentCurve`

## TrapezoidMidPointTests

[`Divergences/TrapezoidMidPointTests.cs`](../tests/NanoQL.Tests/Divergences/TrapezoidMidPointTests.cs)

`TrapezoidIntegral{TIntegrationPolicy}` with `MidPoint`. QuantLib starts the recursion from the trapezoid's estimate, (f(a) + f(b))·(b − a)/2, for either rule (`trapezoidintegral.hpp:58-82`), while the midpoint rule's refinement, (I + dx·Σ(f(x + dx/6) + f(x + 5dx/6)))/3 (`:101-117`), triples a midpoint sum only from a midpoint estimate: the start's error then falls by a third a tripling, first order, and `exp` on [0, 1] at 1e-6 takes QuantLib 531,442 evaluations and reads 1.7182822244001084, 3.9e-7 from e − 1. nanoql starts from the one-interval midpoint estimate, (b − a)·f((a + b)/2).

Facts:

- `TheMidPointRuleStartsFromTheMidPoint`

## TreeCapFloorSeasonedTests

[`Divergences/TreeCapFloorSeasonedTests.cs`](../tests/NanoQL.Tests/Divergences/TreeCapFloorSeasonedTests.cs)

The tree cap engine on a seasoned cap: QuantLib's `DiscretizedCapFloor::mandatoryTimes` keeps the started caplets' negative start times (`discretizedcapfloor.cpp:46-51`), where the discretized swap's filters `t >= 0`, so the grid refuses them and the engine prices nothing: on the wheel, a 3-year semiannual cap at 3% on Euribor 6M, started on 15 July 2024, its fixings stored at 3.5% and 3.25%, notional 1,000,000, on Hull-White (0.05, 0.01) on a flat 3% curve on 15 January 2025, refuses "negative times not allowed" on the tree, where the analytic engine prices it 8,985.844563131719. nanoql keeps the non-negative times, as the discretized swap does, and pays a caplet fixed on or before the reference date its known amount. QuantLib's check: the analytic engine's price, which the tree converges to; both value a caplet still to be fixed at its fixing (`CapletFixingExpiryTests`), where QuantLib's analytic price with those caplets on its five-argument bond option at their fixings is 8,956.86225257607 on the wheel.

Facts:

- `TheSeasonedCapConvergesToTheAnalyticPrice`
- `ASeasonedCapInArrearsConvergesToItsGaussianIntegral`

## TreeKnownCouponTests

[`Divergences/TreeKnownCouponTests.cs`](../tests/NanoQL.Tests/Divergences/TreeKnownCouponTests.cs)

A coupon between its fixing and its start on the trees. QuantLib's discretized cap knows a caplet by its start: it values the caplet as an option on its bond whenever the start is on the grid (`discretizedcapfloor.cpp:55`) and reads the fixing only when the start is past (`:91`); its discretized swap likewise knows a floating coupon by its reset, the accrual start (`discretizedswap.cpp:28-33`, `:81-90`). So between a coupon's fixing and its start the trees ignore the fixing. nanoql knows a caplet, and a swap's coupon behind the tree swap engine and `DiscretizedSwap`'s public constructor, by its fixing date, and pays its amount; the tree swaption keeps QuantLib's start-date rule, its par convention at exercise. QuantLib's check: the analytic cap engine and the discounting swap engine, which the trees then converge to or equal; and QuantLib's own tree swaption.

Facts:

- `ACapAfterItsFirstFixingIsTheAnalyticEngines`
- `ASwapAfterItsFirstFixingIsTheDiscountingEngines`
- `TheTreeSwaptionKeepsQuantLibsStartDateRule`
- `DiscretizedSwapsPublicConstructorKnowsACouponByItsFixingDate`

## TreeSwaptionEngineExerciseTimeTests

[`Divergences/TreeSwaptionEngineExerciseTimeTests.cs`](../tests/NanoQL.Tests/Divergences/TreeSwaptionEngineExerciseTimeTests.cs)

A swaption with no exercise time on or after the reference date, priced on a lattice that initializes an asset at a negative time. QuantLib's tree engine initializes the swaption at its last exercise time and rolls it back to the first time not before the reference date, read through `*std::find_if` (`treeswaptionengine.cpp:88-93`), which reads past the times when none is: undefined. Its own lattices refuse the negative time first, at the initialization ("using inadequate time grid"), but a lattice of one's own may not, as a short-rate model of one's own may build. nanoql refuses it there, with `NanoQLException`, "no exercise time on or after the reference date".

Facts:

- `NoExerciseTimeOnOrAfterTheReferenceDateIsRefused`

## TreeSwaptionSnappingTests

[`Divergences/TreeSwaptionSnappingTests.cs`](../tests/NanoQL.Tests/Divergences/TreeSwaptionSnappingTests.cs)

The tree swaption's snapping and payments: QuantLib's discretized swaption snaps a swap date within a week of an exercise date to it, either way (`discretizedswaption.cpp:93-111`), so a swap starting on its usual two-day lag after the exercise is valued from the exercise date; it rebuilds the swap on its snapped schedules' dates with a `NullCalendar` (`:113-119`), so its coupons pay on those dates, without the swap's payment calendar, convention or lag; and its discretized swap values a floating coupon as a par floater to its payment date (`discretizedswap.cpp:199-218`). On Hull-White (0.1, 0.01) on a flat 4% curve, 800 steps: a European 5Y into 5Y payer on 1e6 at 4% errs against Jamshidian's engine by 50.521421552461106 on the lag and 13.786750153689354 without it (at 5%, 11.848729083691069 and −6.138422108044324); the Bermudan exercised two business days before each fixed reset, against the finite-difference engine (400 × 800), by 69.91132298134835 and 12.102443341802427; a Bermudan on an ESTR swap paying two days late, against the finite-difference engine (400 × 400), by 82.16860415945848 and 71.98241275852342 at 3.5% (64.7080268507616 and 60.15433851566377 at 4.5%) with and without the payment lag. QuantLib's tree swap engine, on a 5-year payer on 1e6 at 4% whose unadjusted floating dates fall three times on a weekend and pay the next business day, prices 3,947.8257420389527 against the discounting engine's 3,461.83299033012 (on the harness: the wheel has no tree swap engine). nanoql snaps no date after an exercise date (the week before still snaps, with #1327's post-adjustment, which fixed #1143), pays each coupon on the swap's own payment date, a snapped one on the swap's payment rule applied to its snapped date, and values a floating coupon paid after its accrual end as its forward over its own accrual period paid on its payment date; its tree swap is then within 1.34 of the discounting engine, at 50 steps as at 3,200, the model's adjustment for the payment delay (1.26) and the par coupons' fixing periods (0.08, its value when the same coupons pay on their accrual ends). QuantLib's checks: Jamshidian's and the finite-difference engines, within the tree's own error on the unlagged swap; the discounting engine; and QuantLib 1.25's cached tree values, which `TestSuite/BermudanSwaptionTests.cs` restates, with QuantLib's `testTreeEngineTimeSnapping` there within its 1.0.

Facts:

- `AEuropeanOnALaggedSwapErrsAsTheUnlaggedOne`
- `ABermudanOnALaggedSwapErrsAsTheUnlaggedOne`
- `ALaggedOvernightBermudanErrsAsTheUnlaggedOne`
- `TheTreeSwapEngineTakesALateCouponsForwardOverItsAccrual`

## TridiagonalOperatorSizeOneTests

[`Divergences/TridiagonalOperatorSizeOneTests.cs`](../tests/NanoQL.Tests/Divergences/TridiagonalOperatorSizeOneTests.cs)

A tridiagonal operator of size 1 can be built (QuantLib's `identity(1)`, or three diagonals of sizes 0, 1 and 0), but QuantLib's `applyTo`, `solveFor` and `SOR` then read past its arrays (`tridiagonaloperator.cpp:70-74,106-108,131-133`; `solveFor`'s `n_-2` wraps), a result C++ leaves undefined. nanoql refuses the three with `NanoQLException`.

Facts:

- `ApplyingOrSolvingASizeOneOperatorIsRefused`

## TrinomialTreeConversionTests

[`Divergences/TrinomialTreeConversionTests.cs`](../tests/NanoQL.Tests/Divergences/TrinomialTreeConversionTests.cs)

The trinomial tree's node offset when it is NaN or outside the integers, and an empty grid. QuantLib converts `std::floor((m - x0_)/dx_[i+1] + 0.5)` to an `Integer` (`trinomialtree.cpp:99`), which C++ leaves undefined there; MSVC, the platform the vectors are generated on, gives the smallest integer, and the classical regime's probability check then refuses, as the wheel does for a NaN reversion ("negative probability in trinomial tree (unfloored regime) at step 0, node 0: p1=-nan(ind), p2=-nan(ind), p3=-nan(ind) (v=nan, dx=nan, e=-nan(ind))"). .NET's conversion saturates instead (NaN to 0, a large value to `int.MaxValue`), so nanoql converts as MSVC does and refuses where QuantLib does, with its text, its NaNs spelled as .NET spells them. For a NaN the offset never reaches the text, since the probabilities are NaN whatever it is; an offset beyond the integers does. QuantLib's step count is unsigned, so an empty grid wraps it and fails allocating ("vector too long"); nanoql's reaches QuantLib's own check, "null time steps for trinomial tree", which QuantLib gives a one-point grid.

Facts:

- `ANaNReversionIsRefusedByTheProbabilityCheck`
- `AnOffsetBeyondTheIntegersConvertsToTheSmallestInteger`
- `AnEmptyGridIsRefusedWithQuantLibsOwnText`

## TrinomialTreeIndexTests

[`Divergences/TrinomialTreeIndexTests.cs`](../tests/NanoQL.Tests/Divergences/TrinomialTreeIndexTests.cs)

The trees' and lattices' indices out of range. QuantLib's `TrinomialTree` reads `branchings_[i-1]`, `k_[index]`, `probs_[branch][index]` and `dx_[i]` unchecked (`trinomialtree.hpp:84-103`), and `TreeLattice::statePrices(i)` extends its cache to any `i` through `size(i+1)` (`lattice.hpp:97-118`), reading past the trees beyond the grid's last time. nanoql refuses each index that reads past an array with `ArgumentOutOfRangeException` naming it, and a negative one as an unsigned count; a node's underlying and descendant, which QuantLib computes from the column's offsets for any node and branch without reading at them, it computes as QuantLib does.

Facts:

- `ATreesIndicesOutOfRangeAreRefused`
- `StatePricesBeyondTheGridAreRefused`

## TurnbullWakemanPastFixingsTests

[`Divergences/TurnbullWakemanPastFixingsTests.cs`](../tests/NanoQL.Tests/Divergences/TurnbullWakemanPastFixingsTests.cs)

Turnbull-Wakeman with every fixing past (fixed). Where the effective strike is positive, QuantLib's engine reads `times.back()` of an empty vector of fixing times and crashes the process (0xC0000005); its certain-exercise branch covers only an effective strike at or below zero. nanoql prices the known payoff discounted, as QuantLib's Choi engine prices it (`choiasianengine.cpp:71-77`): on spot 100, q 3%, r 6%, σ 20% (Act/360), a put at 110 on two past fixings at 95 and 105, exercised in a month. Check: Choi's engine, and the payoff on the average discounted.

Facts:

- `EveryFixingPastPricesTheKnownPayoff`
- `TestTurnbullWakemanAllFixingsInThePast`

## TwoAssetBarrierDividendTests

[`Divergences/TwoAssetBarrierDividendTests.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetBarrierDividendTests.cs)

The two-asset knock-in's vanilla (fixed). QuantLib's `call()` and `put()` drop asset 1's dividend yield, S₁N(d₁) for S₁e^((b₁−r)T)N(d₁), so its knock-in breaks in-out parity: a down-and-in call at 100, barrier 90 on asset 2, prices 6.497953 and 6.961125 at q₁ of 3% and 8% where vanilla − knock-out is 4.785982 and 3.270239, above the vanilla itself at 8%; an up-and-in put at barrier 110 prices 1.466729 at 8% for 5.464209. nanoql carries the dividends. Check: in-out parity against QuantLib's knock-out and its analytic European engine on process 1. At q₁ 3% QuantLib's put is NaN: its d₂ is −5.6e-17, where Drezner's 1978 algorithm returns NaN, and nanoql's bivariate normal is We04DP. QuantLib's master makes the same fix after the pin (#2917, `48010e233`), its case below as a check of nanoql's.

Facts:

- `ACallKnockInIsTheVanillaLessTheKnockOut`
- `APutKnockInIsTheVanillaLessTheKnockOut`
- `TestInOutParityWithDividends`

## TwoAssetBarrierExerciseTests

[`Divergences/TwoAssetBarrierExerciseTests.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetBarrierExerciseTests.cs)

The two-asset barrier engine's exercise. QuantLib's engine never reads the exercise's type and prices any exercise to its last date: a down-and-out call struck at 100 on the first asset (spot 100, r 5%, q 1%, σ 20%), barrier 90 on the second (q 2%, σ 30%), ρ 0.5, over a year prices 4.219587870592 for an American exercise, its European value. nanoql refuses a non-European exercise, as the family's European engines do: the formula prices a European exercise only. The European value stands, within a measured 3.3e-5 of QuantLib's (West's bivariate normal for Drezner's).

Facts:

- `ANonEuropeanExerciseIsRefused`

## TwoAssetBarrierSmileTests

[`Divergences/TwoAssetBarrierSmileTests.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetBarrierSmileTests.cs)

The two-asset barrier's read of asset 2's volatility (fixed). QuantLib reads it at asset 1's strike, a level on another price scale: asset 2 at 5,000 on a smile, the barrier at 4,500, a down-and-out call at strikes 90, 100 and 110 prices 6.054081, 4.647889 and 2.965222 on volatilities read at 90 to 110, where the smile's volatility at the barrier is 34.37%. nanoql reads it at the barrier, asset 2's own level; the read is a choice inside a constant-volatility formula, so its check is that it reads one level consistently. Checks: QuantLib's values on flat volatilities, 5.893116254296, 4.038304931649 and 2.557384709654 on QuantLib's Dr78, to its six decimal places on the formula's terms of weight 100; and on the smile, the engine equals itself with asset 2's volatility flat at its barrier level.

Facts:

- `OnFlatVolatilitiesTheEngineIsQuantLibs`
- `OnASmileAssetTwoIsReadAtItsBarrier`

## TwoAssetBarrierTwoCurvesTests

[`Divergences/TwoAssetBarrierTwoCurvesTests.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetBarrierTwoCurvesTests.cs)

The two-asset barrier's second risk-free curve (fixed). QuantLib takes asset 2's carry from process 1's rate, b₂ = r₁ − q₂, so the price ignores process 2's curve: a down-and-out call at 100, barrier 90 on asset 2, r₁ 5%, q₂ 1%, prices 4.547879 at r₂ of 0, 5% and 10%. nanoql reads b₂ = r₂ − q₂ and discounts on process 1's curve. Check: QuantLib's engine on the market restated onto process 1's curve, asset 2's dividend yield q₂ + r₁ − r₂: 4.045769328445, 4.547878997954 and 5.044213261884 on QuantLib's Dr78, to its six decimal places on the formula's terms of weight 100.

Facts:

- `AssetTwosCarryReadsItsOwnCurve`

## TwoAssetCorrelationBivariateTests

[`Divergences/TwoAssetCorrelationBivariateTests.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetCorrelationBivariateTests.cs)

The two-asset correlation engine's bivariate normal. QuantLib's engine reads Drezner's 1978 algorithm (`analytictwoassetcorrelationengine.cpp:44`), "six decimal places accuracy" by its header (`bivariatenormaldistribution.hpp:33`), where QuantLib's default bivariate normal is West's; the value moves by 2.5e-7 on QuantLib's own case. nanoql reads West's: a quadrature over the joint lognormal (`TwoAssetCorrelationQuadrature`) is the check, at 1e-9, which QuantLib's value meets within Drezner's accuracy.

Facts:

- `TheValueMeetsTheQuadrature`

## TwoAssetCorrelationNegativeStrikeTests

[`Divergences/TwoAssetCorrelationNegativeStrikeTests.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetCorrelationNegativeStrikeTests.cs)

The two-asset correlation engine at a negative second strike. QuantLib checks the first strike but not the second (`analytictwoassetcorrelationengine.cpp:45`), whose `log(s2/X2)` is NaN and reaches Drezner's "case not handled", where the call, (S₂ − X₂)·1{S₁ > X₁}, is s₂e^{(b₂−r)T}N(y₁ + ρσ₂√T) − X₂e^{−rT}N(y₁) (57.670585 at X₂ = −10 on the test's market; QuantLib's X₂ = 0 value is the same form, 52.851193) and the put worth nothing. nanoql prices those forms: a quadrature over the joint lognormal (`TwoAssetCorrelationQuadrature`) and QuantLib's X₂ = 0 value are the checks.

Facts:

- `TheCallMeetsTheQuadratureAndTheZeroStrikeValue`
- `ThePutIsWorthNothing`

## TwoAssetCorrelationQuadrature

[`Divergences/TwoAssetCorrelationQuadrature.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetCorrelationQuadrature.cs)

An independent value of a two-asset correlation option on flat continuous markets: the discounted expectation of the second asset's payoff where the first is in the money, integrated over the second asset's driving normal, each first-asset probability conditional on it in closed form, the payoff's kink an end of the integral; Gauss-Lobatto at an absolute accuracy of 1e-13.

## TwoAssetCorrelationSmileTests

[`Divergences/TwoAssetCorrelationSmileTests.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetCorrelationSmileTests.cs)

The two-asset correlation engine on a smile. QuantLib reads the second asset's volatility at the first strike X₁ (`analytictwoassetcorrelationengine.cpp:54-56`), so on a smile it prices the flat-volatility value at X₁ (9.758 on the test's smile, where X₂'s volatility gives 4.239). nanoql reads it at X₂: QuantLib's engine on a flat volatility equal to the smile's at X₂ is the check, bit for bit.

Facts:

- `TheSecondVolatilityIsReadAtTheSecondStrike`

## TwoAssetMarket

[`Divergences/TwoAssetMarket.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetMarket.cs)

The two-asset divergence tests' market: flat curves and volatilities from one date, two-asset correlation matrices, spread and basket options expiring in a year.

## TwoAssetMismatchedTimesTests

[`Divergences/TwoAssetMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetMismatchedTimesTests.cs)

The two-asset engines on structures dated apart (the playbook's row for structures on another clock). The spread engines, Stulz, the Gaussian copula and the cash-dividend engine read by date, which QuantLib refuses for an option expiring before a later structure's date ("negative time"); the two-asset correlation engine reads each volatility at its own process's time and everything else at the second's (+0.84% on the test's market with asset 2's curves on Act/360), and the American Margrabe engine builds flat curves from the evaluation date with rates over the risk-free curve's time (`analytictwoassetcorrelationengine.cpp:54-66`, `analyticamericanmargrabeengine.cpp:53-79`). nanoql's by-date reads accrue nothing before a structure's date, and its clocked engines read every structure on the clock across both processes; the cash-dividend engine dates and counts each dividend's volatility on the clock, where QuantLib sizes it on the risk-free curve's time but reads it on the volatility's own (+0.48% with the volatility on Act/360 under curves on Act/365, `cashdividendeuropeanengine.cpp:139-151`). QuantLib's engine on the market restated from the evaluation date, onto one clock, is the check.

Facts:

- `AnExpiryBeforeTheRiskFreeCurvesDateDiscountsNothing`
- `ARiskFreeCurveSettlingLaterIsPricedFromTheEvaluationDate`
- `ACashDividendVolatilityCountingItsOwnDaysIsReadOnTheClock`

## TwoAssetTwoRiskFreeCurvesTests

[`Divergences/TwoAssetTwoRiskFreeCurvesTests.cs`](../tests/NanoQL.Tests/Divergences/TwoAssetTwoRiskFreeCurvesTests.cs)

Stulz's, both Margrabe engines and the two-asset correlation engine on a second risk-free curve. QuantLib reads the second process's risk-free curve three ways: Kirk's family and the Monte Carlo basket engines forward the second asset on its own curves and discount on the first's (`spreadblackscholesvanillaengine.cpp:54-68`, `mceuropeanbasketengine.hpp:196`); Stulz and the European Margrabe engine build the second forward on the first process's discount (`stulzengine.cpp:140-152`, `analyticeuropeanmargrabeengine.cpp:61-72`), the two-asset correlation engine takes its rate from the first process alone (`:64-66`), and the American Margrabe engine reads no risk-free rate (`analyticamericanmargrabeengine.cpp:69-79`); at r₁ 5%, r₂ 10% on the test's market Stulz's minimum call is −94 standard errors from the Monte Carlo engine and Margrabe's exchange +134. nanoql reads Kirk's way in each, the American Margrabe engine's transformed rate the second asset's yield on the first curve (q₂ + r₁ − r₂), and the European Margrabe theta the second asset's carry there: each engine on the market restated onto the first process's curve, the second asset's yield q₂ + r₁ − r₂ for the same forward, is the check, the European Margrabe theta's among them, with the Monte Carlo basket engines beside.

Facts:

- `StulzPricesTheRestatedMarket`
- `TheEuropeanMargrabeEnginePricesTheRestatedMarket`
- `TheEuropeanMargrabeThetaIsTheRestatedMarkets`
- `TheAmericanMargrabeEnginePricesTheRestatedMarket`
- `TheTwoAssetCorrelationEnginePricesTheRestatedMarket`

## UfrSmallAlphaTests

[`Divergences/UfrSmallAlphaTests.cs`](../tests/NanoQL.Tests/Divergences/UfrSmallAlphaTests.cs)

The ultimate-forward curve's kernel at small α. QuantLib's B(x) = (1 − e^(−αx))/(αx) (`ultimateforwardtermstructure.hpp:169`) is unguarded: on the wheel, over a flat 3% Actual/365 (Fixed) curve from 15 January 2025 with an LLFR of 3%, a UFR of 4% and a 20-year first smoothing point, α = 0 refuses the 60-year zero rate with "positive compound factor required", α = 1e-12 gives 0.029999995243582964 against its limit 0.03, and α = 0.1 gives 0.035031562480595566. nanoql's kernel is −expm1(−αx)/(αx), and 1 at 0, as upstream's own `oneMinusExpOverX` after the pin. QuantLib's check: the limit, and its own values at ordinary α.

Facts:

- `ASmallAlphaGivesTheLimit`

## UnitConversionSearchTests

[`Divergences/UnitConversionSearchTests.cs`](../tests/NanoQL.Tests/Divergences/UnitConversionSearchTests.cs)

The registry's chained search with its forbidden list passed down (fixed). QuantLib's `smartLookup` forbids each unit it starts from to the searches below it, "in order to avoid cycles", but drops the list when it recurses (`unitofmeasureconversionmanager.cpp:190`), so a search whose first candidate leads back recurses without end: its known factors' first entry pairs thousand-barrels and barrels, so every chained search from either overflows the stack. On QuantLib 1.44 (each lookup in its own process) barrels to metric tonnes overflows (exit 127) with and without a user's metric-tonne-to-gallon factor of 300, as gallons to metric tonnes does, through barrels; metric tonnes to barrels returns 7.14286, its first candidate the user's factor. nanoql passes the list down, as QuantLib's `ExchangeRateManager` passes its own; wherever QuantLib's search returns it never revisits a unit above it, so the answer is QuantLib's. Checks: the chains against the hand value through gallons, the refusal where no chain exists, and QuantLib's 7.14286.

Facts:

- `BarrelsToTonnesChainsThroughTheUsersGallonFactor`
- `GallonsToTonnesTriangulatesThroughBarrels`
- `TonnesToBarrelsIsQuantLibs`
- `BarrelsToTonnesWithNoPathIsRefused`

## UpperBoundEngineTests

[`Divergences/UpperBoundEngineTests.cs`](../tests/NanoQL.Tests/Divergences/UpperBoundEngineTests.cs)

The upper-bound engine's exercise values. QuantLib's engine reads each exercise value at every exercise date from an `ExerciseAdapter` done at its first, relying on its composite to step a finished part again (`upperboundengine.cpp`'s warning); under the composites' end that adapter stops, so nanoql's two exercise-value parts pay the exercise value at every exercise date, done at their last step. On a hedge that ends before the last exercise date (a TARN's knock-out) the decorated hedge stops with it, so nanoql treats no date after the step at which the hedge ends as an exercise date, and a call-specified product stays ended after its underlying. On six semi-annual rates, FlatVol 20%, the money-market-plus measure: QuantLib's tested callable receiver swap and a Bermudan whose nothing-underlying ends at the last step, QuantLib's values bit for bit; a callable TARN with a cash rebate growing from 1% to 6%, against the same engine on the TARN made inert after its end, where QuantLib's engine is right: QuantLib's engine on the plain TARN +0.0226 ± 0.0007 (34 standard errors), nanoql's within 4. Checks: QuantLib's engine on its tested configurations and on the inert TARN, bit for bit; the paired difference.

Facts:

- `OnQuantLibsTestedCallableSwapTheBoundIsQuantLibs`
- `ABermudanOnANothingUnderlyingIsQuantLibs`
- `OnAHedgeOfSixProductsTheBoundIsQuantLibs`
- `ACallableTarnPairsWithItsInertTwin`
- `OnCoterminalAndCmsEvolversTheBoundIsQuantLibs`
- `OnANormalEvolverEveryPathsGapIsAtLeastZero`

## VannaVolgaDoubleBarrierTests

[`Divergences/VannaVolgaDoubleBarrierTests.cs`](../tests/NanoQL.Tests/Divergences/VannaVolgaDoubleBarrierTests.cs)

The Vanna-Volga double-barrier engine's classes. QuantLib passes the foreign discount twice to the smile's interpolation (`vannavolgadoublebarrierengine.hpp:144`), whose constructor takes the domestic one first, as QuantLib's single-barrier engine passes it: the vanilla on the smile moves by 1.4e-4 at QuantLib's rates and 6.0e-4 at a foreign rate of 3% (QuantLib 1.44). nanoql passes the domestic discount first; its vanilla equals the single-barrier engine's on the same quotes. nanoql also carries the single-barrier engine's fixes: each curve read at the instant the quotes' time denotes on the volatility's clock, an out option on or beyond a barrier worth its rebate, an in option's rebate the inner engine's, and the already-out and already-in tests inclusive at the barriers. QuantLib's own case builds its curves on Actual360 beside the engine's Actual/365F volatility, where QuantLib's inner engine accrues the variance over 365/360 risk-free years and its own discounts read each curve's clock: these reads move 16 of its 40 values past its 1e-4, by up to 1.11e-3 (eight strikes, each knock-in and knock-out: the calls at 1.13321 and 1.22687 and the puts at 1.38843 and 1.46047 over a year, the calls at 1.06145 and 1.19545 and the puts at 1.44298 and 1.56345 over two; QuantLib's cached 0.036217, 0.144130, 0.025532, 0.074560, 0.036798, 0.060490, 0.047325, 0.111030, 0.059136, 0.199810, 0.042694, 0.103890, 0.061539, 0.093460, 0.081464, 0.177040 against nanoql's 0.035420, 0.144927, 0.025147, 0.074945, 0.036576, 0.060712, 0.046837, 0.111519, 0.058023, 0.200923, 0.042200, 0.104384, 0.061129, 0.093870, 0.080606, 0.177897), and QuantLib's own engine misses 20 of the 40 on the case restated to Actual/365F (worst 1.04e-3): the cache belongs to QuantLib's mixed clocks (QuantLib 1.44). On the restated case nanoql is QuantLib's engine with the domestic discount, within 1e-12 on all 40.

Facts:

- `OnTheRestatedCaseTheEngineIsQuantLibsWithTheDomesticDiscount`
- `TheSmilesVanillaIsTheSingleBarrierEngines`
- `OnAFlatSmileTheEngineIsItsInnerEngineWithARebate`
- `OnABarrierAnOutOptionIsItsRebateAndAnInOptionItsVanilla`
- `EachCurveIsReadAtTheQuotesInstant`
- `TheSurvivalProbabilityIsItsConvergedSeries`

## VannaVolgaMismatchedTimesTests

[`Divergences/VannaVolgaMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/VannaVolgaMismatchedTimesTests.cs)

The Vanna-Volga engine's own reads on curves that measure time differently from its volatility (fixed). QuantLib dates the volatility by the evaluation date on Actual/365 Fixed (`vannavolgabarrierengine.cpp:83-86`) and prices its barrier through the analytic engine on it, but reads everything else at the quotes' time on each curve's own clock (`:97-137`, `:194-231`, `:313`): on Actual/360 curves a one-year up-and-out call at 1.40, strike 1.25, on a skewed smile, is 0.008044987558286639 against 0.007989598182293868 on the Actual/365 Fixed curves with the same discount factors, and on curves dated at the FX spot date, two days later, the common set-up, it is off its own engine on the curves restated from the evaluation date by 9.1e-5 to 2.8e-2, the down-and-in most (QuantLib-Python 1.44). nanoql reads each curve at the instant the quotes' time denotes on the volatility's clock, nothing accrued before the curve's own date (the playbook's row for structures on another clock): its discount factors, and the touch probability's zero rates, the logarithms of those discounts over the time, as the analytic engine's are read. Where the curves are dated today on Actual/365 Fixed the reads are QuantLib's, with its bits. QuantLib's check: its own engine on the curves restated from the evaluation date on Actual/365 Fixed (a curve dated later a discount curve at 1 up to its date), where the quotes' time is the exercise's and where it isn't.

Facts:

- `OnAct360CurvesTheEngineIsQuantLibsOnTheEquivalentCurves`
- `OnAct365FixedCurvesTheEngineIsQuantLibsOwn`
- `OnCurvesDatedAtTheSpotDateTheEngineIsQuantLibsOnTheRestatedCurves`
- `WhereTheQuotesTimeIsNotTheExercisesTheEngineIsQuantLibsOnTheRestatedCurves`

## VannaVolgaRebateTests

[`Divergences/VannaVolgaRebateTests.cs`](../tests/NanoQL.Tests/Divergences/VannaVolgaRebateTests.cs)

The Vanna-Volga engine with a rebate (fixed). QuantLib prices the out option with its rebate and the in option by parity, `vanilla − outPrice`, capping `outPrice` at the vanilla (`vannavolgabarrierengine.cpp:174-193`, `:326-339`), and gives an out option beyond the barrier 0 (`:141-168`): so a knock-in loses its own rebate and subtracts the knock-out's, a capped knock-out loses its rebate, and a knock-out at the barrier loses its rebate too. On a flat smile, where the method's adjustment vanishes and the engine should give the analytic engine's price, a 0.05 rebate leaves an up-and-in at 1.40, strike 1.25, at 0.06807979413354334 against 0.11711444573142975 (QuantLib-Python 1.44). nanoql prices each option's rebate-free part and bounds it as QuantLib does, then adds its own rebate: an out option's smile-adjusted rebate at the hit (QuantLib's own price where no bound binds), an in option's rebate at expiry by the analytic engine; with the spot at or beyond the barrier an out option is its rebate, due now, and an in option QuantLib's vanilla. A zero rebate keeps QuantLib's bits. QuantLib's checks: its own engine's rebate-free parts and uncapped prices, and its analytic engine.

Facts:

- `OnAFlatSmileAKnockInWithARebateIsTheAnalyticEnginesPrice`
- `OnAFlatSmileAKnockOutQuantLibCapsIsTheAnalyticEnginesPrice`
- `OnASkewedSmileAKnockInIsQuantLibsRebateFreePriceAndItsRebateAtExpiry`
- `AtOrBeyondTheBarrierAKnockOutIsItsRebateAndAKnockInTheVanilla`
- `AnUncappedKnockOutWithARebateIsQuantLibsOwnPrice`
- `AZeroRebateKeepsQuantLibsValues`

## VarianceGammaFftTests

[`Divergences/VarianceGammaFftTests.cs`](../tests/NanoQL.Tests/Divergences/VarianceGammaFftTests.cs)

The variance-gamma and FFT engines where QuantLib's fail. QuantLib's analytic engine takes the gamma density as Γ(t/ν)·ν^(t/ν), an overflow times an underflow past t/ν of about 170, so at ν 0.002 over a year it throws "Interval contains no more machine number", and starts its bound at 15√(νt), below the gamma time's mean once ν < t/225; nanoql takes the density in logs there and starts the bound beyond the mean where ν < t/100, and the price rises toward Black-Scholes as ν falls, within the FFT engine's ordinary accuracy of it. QuantLib's FFT engine damps at α 1.25 even past the moment E[S^(α+1)], pricing 0.494 against the analytic 48.34 at ν 2, θ 0.3, σ 0.5 (QuantLib 1.44); nanoql damps inside the strip, on the put side near its boundary, matching independent quadratures within 1e-5. QuantLib sizes the grid from the largest strike's log alone, undefined below e^(−λ) ("vector too long" on an FX call at 0.85) and too coarse in frequency wherever its size falls below 2^14 (any strike between 1 and about 60); nanoql sizes it from the largest |log K| and at least 2^14, QuantLib's size on every QuantLib case. QuantLib's grid at α 1.25, period λn, aliases the damped call's tail once σ√T passes about 1.2, an at-the-money call pricing −298.8926 against 66.80 at σ√T 2 (QuantLib 1.44); nanoql doubles the grid until two successive grids agree within 5e-5 of the spot, QuantLib's grid where it already agrees. Checks: the analytic engines, Black-Scholes's limit, the independent quadratures.

Facts:

- `AtSmallNuTheAnalyticPriceRisesTowardBlackScholes`
- `PastTheDampingsMomentTheFftIsTheAnalyticPrice`
- `NearTheMomentBoundaryTheFftIsTheIndependentQuadrature`
- `AtStrikesNearOneTheFftIsTheAnalyticPrice`
- `AListsLargestStrikeSetsItsExpirysGrid`
- `PastSigmaRootTOf12TheGridIsDoubledToTheAnalyticPrice`
- `PastTheTailGrowthBoundTheFftRefuses`
- `AVarianceGammaCallAtALargeVarianceIsTheAnalyticPrice`
- `ANearBoundaryListFromStrike20To400IsTheIndependentQuadrature`

## VarianceSwapMismatchedTimesTests

[`Divergences/VarianceSwapMismatchedTimesTests.cs`](../tests/NanoQL.Tests/Divergences/VarianceSwapMismatchedTimesTests.cs)

The replicating variance-swap engine on structures dated differently. QuantLib's fair variance is per year of the risk-free curve's own time (`replicatingvarianceswapengine.hpp:149`, `:170-171`, `:211-213`), the portfolio's options priced on each structure's own times, so where a risk-free curve settling T+2 sits beside a volatility dated today, QuantLib divides the volatility's variance by the risk-free curve's shorter time: 2.33 times high at a week and 1.1% at a year, the ratio of the two times. nanoql's variance is per year of the swap's time on the risk-free curve's day counter from the earliest reference date, each structure accruing nothing before its own: QuantLib's on the market restated from today. Where no structure is dated before the risk-free curve, it is QuantLib's.

Facts:

- `TheVarianceIsPerYearOfTheSwapsTime`

## VasicekCancellationTests

[`Divergences/VasicekCancellationTests.cs`](../tests/NanoQL.Tests/Divergences/VasicekCancellationTests.cs)

The Vasicek model at small mean reversion: QuantLib 1.44's A and B cut to their a = 0 forms below √ε, dropping terms of order a·τ, and above it subtract O(1/a) terms (`vasicek.cpp:36-58`), so its zero bond is anywhere between 4.6e-163 and 9.1e131 for a between 1.5e-8 and 1e-7 (the wheel, below) and still off by 5.4e-4 at 1e-6. nanoql's are QuantLib's master's forms (#2882, `da854f617`): B as τ·(1 − e^(−aτ))/(aτ) and the exponent of A by the φ and h series below aτ = 1, the bond option's variance likewise. QuantLib's check: upstream's new test lines, `testVasicekDiscountFactorJustAboveSmallMeanReversionCutoff` and `testVasicekDiscountBondOptionForSmallMeanReversion`, against 60-digit references at upstream's tolerances.

Facts:

- `TheZeroBondJustAboveTheCutoff`
- `TheBondOptionAtSmallReversion`

## VecerEngineTests

[`Divergences/VecerEngineTests.cs`](../tests/NanoQL.Tests/Divergences/VecerEngineTests.cs)

Vecer's Asian engine at its grid's upper end and on mixed clocks. QuantLib interpolates between the nodes below and above the initial point, so where the point lies on the grid's upper end it reads one node past the grid (`continuousarithmeticasianvecerengine.cpp:185-190`); nanoql takes the last node's value there, the interpolation's own limit. QuantLib measures the time from the evaluation date on the risk-free day counter beside each structure's own zero rate and volatility; nanoql reads each on the engine's clock, so the market restated on one day counter prices the same. Checks: the price continuous as the point reaches the upper end; the restated market's price, against QuantLib's reading of the unrestated rates and volatility.

Facts:

- `AtTheGridsUpperEndThePriceIsTheLastNode`
- `AMarketOnMixedDayCountersIsTheMarketRestatedOnOne`

## VectorQuadratureWeightTests

[`Divergences/VectorQuadratureWeightTests.cs`](../tests/NanoQL.Tests/Divergences/VectorQuadratureWeightTests.cs)

The Gauss-Hermite vector integration weights every node: QuantLib's applies its first node's weight inside a `std::for_each` that discards the result (`multidimquadrature.hpp:61-64`), so node n − 1 enters unweighted. QuantLib's values are recorded in the test; the check is QuantLib's scalar rule per component, bit for bit.

Facts:

- `TheVectorRuleIsTheScalarRulePerComponent`
- `ALatentModelsVectorExpectationIsItsScalarOnes`

## VegaBumpCollectionCheckTests

[`Divergences/VegaBumpCollectionCheckTests.cs`](../tests/NanoQL.Tests/Divergences/VegaBumpCollectionCheckTests.cs)

The vega bump collection's checks. QuantLib's `isFull` and `isNonOverlapping` are true where an element fails (`vegabumpcluster.cpp:179`, `:218`): on six semi-annual rates, FlatVol 20%, 3 factors, its own default collection, given as its clusters, reports not full, overlapping and not sensible; half its clusters report full and one cluster given twice non-overlapping. nanoql's are true where no element fails. Checks: QuantLib's default collection's flags, true.

Facts:

- `TheChecksAreTrueWhereNoElementFails`

## VegaStressedProcessTests

[`Divergences/VegaStressedProcessTests.cs`](../tests/NanoQL.Tests/Divergences/VegaStressedProcessTests.cs)

The vega-stressed process's stress reaching its standard deviation, variance and step (fixed). QuantLib's take the exact forms from the term structures on a strike-independent volatility, which read no diffusion, so the stress never reaches them. On QuantLib 1.44 (stress 0.1 on a flat 0.2, r − q 0.03) the diffusion is 0.3 but the deviation over a year 0.2 and the step from 100 at a draw of 1 123.367806, the unstressed process's. nanoql's go through the Euler discretization with the stressed diffusion inside the box and the plain one outside; with no stress they are QuantLib's, and the expectation is the unstressed one, the mean of the asset reading no volatility.

Facts:

- `TheStressReachesTheStepInsideTheBox`
- `OutsideTheBoxTheStepIsThePlainOne`
- `WithNoStressTheProcessIsQuantLibs`

## VeryFastAnnealingSamplerTests

[`Divergences/VeryFastAnnealingSamplerTests.cs`](../tests/NanoQL.Tests/Divergences/VeryFastAnnealingSamplerTests.cs)

The very fast annealing's sampler, fixed. QuantLib's starts each coordinate's loop from lower − 1, which rounds to the lower bound itself at every magnitude from 2⁵⁴, so it returns that bound with no draw (`hybridsimulatedannealingfunctors.hpp:190-198`); nanoql's draws each coordinate at least once. QuantLib's step T·((1 + 1/T)^e − 1) is infinite where 1/T overflows, below about 5.6e-309, so its loop never lands in the box, and NaN at T = 0, which its loop returns; nanoql's takes the same value as T^(1 − e)·(1 + T)^e − T, which agrees with QuantLib's form where both are finite, so at T = 0 it holds the point; and an annealing on the very fast temperature with an end temperature of 0 runs past the overflow to its iteration limit. Above T 100 QuantLib's 1 + 1/T loses the digits of 1/T, its step 2.3e-2 off at 1e14 and 0 from about 1e16, where every draw holds the point: nanoql's, T·expm1(e·log1p(1/T)) there, is within 4 ulps of a 50-digit reference from 1e3 to 1e20 (mpmath on the exact doubles), and QuantLib's bits at 100 and below.

Facts:

- `ABoxPast2To54IsDrawnNotItsLowerBound`
- `TheStepsRewrittenFormIsQuantLibsWhereBothAreFinite`
- `ATemperatureWhoseReciprocalOverflowsDraws`
- `AnAnnealingRunsPastTheTemperaturesOverflow`
- `TheStepAboveTemperature100KeepsItsDigits`
- `TheStepAt100AndBelowIsQuantLibs`
- `AHighTemperatureMovesThePoint`

## VolatilityBumpInstrumentJacobianTests

[`Divergences/VolatilityBumpInstrumentJacobianTests.cs`](../tests/NanoQL.Tests/Divergences/VolatilityBumpInstrumentJacobianTests.cs)

The bump instrument Jacobian's refusal. QuantLib marks an instrument computed before computing it (`bumpinstrumentjacobian.cpp:53`), so after its pseudo-derivative refuses, a second read returns the zero-filled vector: on six semi-annual rates, FlatVol 20%, 3 factors, displacement −1%, a cap from rate 1 to 5, refused by Black's formula's check, QuantLib's second read returns 63 zeros and the bump matrix a row it never wrote (`Matrix(rows, columns)` leaves its storage uninitialized: 1.6e-311 and 3.0e-311 in two runs). nanoql's per-instrument cache refuses at every read. Checks: QuantLib's first read's refusal.

Facts:

- `ARefusalIsRefusedAtEveryRead`

## VolatilityInterpolationSpecifierOffsetTests

[`Divergences/VolatilityInterpolationSpecifierOffsetTests.cs`](../tests/NanoQL.Tests/Divergences/VolatilityInterpolationSpecifierOffsetTests.cs)

The abcd specifier's in-between short rates at an offset (fixed). QuantLib builds the variance of the short rate in slot `i + j·period + offset` on reset index `i + j·period`, an earlier rate's (`volatilityinterpolationspecifierabcd.cpp:169-170`): in periods of 2 at offset 1 over twelve semi-annual short rates, slots 1 to 4 reset at indexes 0 to 3, each slot's variance ending a step early (the C++ harness's `VolatilityInterpolationSpecifierabcd`). nanoql resets each slot at its own index. Checks: each in-between slot's variance is QuantLib's own `PiecewiseConstantAbcdVariance` on the two long rates' mean abcd at that index, bit for bit; at offset 0, QuantLib's tested case, the vectors' bits.

Facts:

- `EachInBetweenSlotResetsAtItsOwnIndex`

## VolatilityShortSeriesTests

[`Divergences/VolatilityShortSeriesTests.cs`](../tests/NanoQL.Tests/Divergences/VolatilityShortSeriesTests.cs)

The volatility estimators on series too short for them (§2.2's undefined row, refused). QuantLib's `Garch11::calculate` on fewer than two points reads before the series' start (`garch.cpp:385-388`), `SimpleLocalEstimator` and the open-close Garman-Klass estimators increment an empty series' begin, and `ConstantEstimator` advances past the end when its window is longer than the series. nanoql refuses each before the read, with an `ArgumentException` naming the series. Check: each refusal's type, parameter and text.

Facts:

- `GarchOnOnePointIsRefused`
- `AConstantWindowLongerThanTheSeriesIsRefused`
- `TheSimpleLocalEstimatorOnAnEmptySeriesIsRefused`
- `AnOpenCloseGarmanKlassEstimatorOnAnEmptySeriesIsRefused`

## WeekendOnDateHolidayListTests

[`Divergences/WeekendOnDateHolidayListTests.cs`](../tests/NanoQL.Tests/Divergences/WeekendOnDateHolidayListTests.cs)

A holiday list without weekends for a market whose weekend changed on a date. The Tel Aviv stock exchange kept Friday and Saturday until January 4th, 2026 (`israel.cpp:436-446`), while `isWeekend(Weekday)` answers today's weekend, Saturday and Sunday, so QuantLib's `holidayList(..., includeWeekEnds = false)` (`calendar.cpp:277-286`) holds every Friday before 2026; QuantLib's own TASE test says "using checkHolidays with includeWeekEnds = false doesn't work in this case" and filters the list by each date's weekend instead (`test-suite/calendars.cpp:3699-3722`). Tadawul kept Thursday and Friday until June 28th, 2013 (`saudiarabia.cpp:31-37`), and its list holds every Thursday before July 2013 the same way. nanoql's list leaves out the days that are weekend days on their own date, which equals the TASE test's filtered list; a joint calendar combines its components' weekends on the date as its weekend combines theirs. `IsWeekend(Weekday)` keeps QuantLib's answer.

Facts:

- `TheTelAvivListsAreQuantLibsTestsFilteredLists`
- `TheTadawulListLeavesOutEachDatesOwnWeekend`
- `AJointCalendarCombinesItsComponentsWeekendsOnTheDate`

## WeightedIndexNullComponentTests

[`Divergences/WeightedIndexNullComponentTests.cs`](../tests/NanoQL.Tests/Divergences/WeightedIndexNullComponentTests.cs)

A weighted index with a null component. QuantLib's `StubIborCoupon::WeightedIndex` reads every component's tenor, and the first one's conventions, in its base initializer (`stubiborcoupon.cpp:37-56`), before its own check, "null component index" (`:64`), so a null component is dereferenced. nanoql refuses it first, with `ArgumentException` and QuantLib's text, wherever it stands.

Facts:

- `ANullComponentIsRefusedBeforeItIsRead`

## WriterExtensibleTermStructureTests

[`Divergences/WriterExtensibleTermStructureTests.cs`](../tests/NanoQL.Tests/Divergences/WriterExtensibleTermStructureTests.cs)

The writer-extensible engine on term structures. QuantLib evaluates Longstaff's formula on one zero rate, dividend yield and volatility, read at the first exercise, for both periods (`analyticwriterextensibleoptionengine.cpp:61-87`), so on a rising market the call is −2.4% off; nanoql reads the variances to each date, the correlation from their ratio and the discounts by date. The check is the option's exact value at the first exercise, the vanilla's payoff in the money and the extended option's Black value out of it, over the lognormal spot, within 1e-9; on a flat market, QuantLib's values within its last bits.

Facts:

- `ARisingMarketMeetsTheExactValue`
- `AFallingMarketMeetsTheExactValue`
- `AFlatMarketIsQuantLibs`

## XabrTransformEdgeTests

[`Divergences/XabrTransformEdgeTests.cs`](../tests/NanoQL.Tests/Divergences/XabrTransformEdgeTests.cs)

XABR's parameter transforms at their edges. QuantLib's SABR inverse transform doesn't clamp its arguments (`sabrinterpolation.hpp:103-114`): a ρ beyond 0.9999 takes `asin` of a value above 1, a NaN that the direct transform turns into −0.9999, so a fixed ρ of 0.99995 comes back as −0.9999, its sign flipped, and a free guess of 0.99995 fails the fit with "alpha must be positive: nan not allowed"; an α or ν below 1e-7, ν = 0 included, takes the square root of a negative number, a NaN the fit refuses ("alpha must be positive: nan not allowed", "nu must be non negative: nan not allowed"). nanoql's inverse clamps each argument to its direct transform's range, so such an input comes back as the nearest value the transform reaches, bit for bit: ρ = 0.9999, ν = 1e-7, and the fixed β of 0.6. The fits from those values are the wheel's fits from them. Two of them are well posed and agree within the vectors' tolerance; the three that pin a parameter at an edge can't reproduce the smile, so their optimum is flat: when the input volatilities move by one ulp, their free parameters move by up to 2.4e-3 in the stability measure, at the vectors' scale of 1, which is up to 24% of the small ones' own values. Those are asserted within 1e-2 in that measure. ZABR's inverse (`zabrinterpolation.hpp:78-88`) doesn't clamp either: α, β and ρ invert as SABR's do, so a ρ above 0.9999 takes `asin` of a value beyond 1, a NaN its direct transform turns into −0.9999, and an α below 1e-7 the square root of a negative number, a NaN the model then refuses ("alpha must be positive: nan not allowed"); ν and γ invert through `tan(π(y/5 − 0.5))` and `tan(π(y/1.9 − 0.5))`, whose period is π, so a ν above 5 or a γ above 1.9 comes back as another value inside the range the direct transform's comments name ("limit nu to 5.00", "limit gamma to 1.9", `zabrinterpolation.hpp:97-103`). On the wheel's ZABR cube a fixed ρ of 0.99995 came back as −0.9999, ν 6.0 as 0.9999999999999998 and γ 2.0 as 0.09999999999999978. nanoql's inverse clamps its arguments to the direct transform's range, so each comes back as the nearest value the transform reaches, bit for bit: ρ 0.9999, ν 5, γ 1.9, α 1e-7; and the fit from it is the fit from that value, bit for bit, whose parameters the wheel gives. Under one-ulp moves of the volatilities, together and each alone, the γ fit's free parameters and the ν fit's α move by at most 5.4e-11, asserted within the vectors' tolerance; the others move by up to 7.7e-6, asserted within 1e-2, but for the α fit's ρ, which moves by 9.1e-2 and so isn't asserted (the wheel's is −0.026977128413889717).

Facts:

- `SabrsEdgesComeBackAsTheTransformsNearestValues`
- `ZabrsTransformKeepsEachEdgeInItsRange`

## YearFractionToDateNaNTests

[`Divergences/YearFractionToDateNaNTests.cs`](../tests/NanoQL.Tests/Divergences/YearFractionToDateNaNTests.cs)

`yearFractionToDate` on a NaN time. QuantLib casts `round(NaN * 365.25)` to an integer through `boost::numeric_cast`, whose range check a NaN passes (`yearfractiontodate.cpp:40-41`), so the cast is undefined: the wheel reads -2147439651 days and throws "Date's serial number (-2147439651) outside allowed range". nanoql refuses a NaN on entry with `ArgumentException` naming `t`.

Facts:

- `ANaNTimeIsRefusedOnEntry`

## YoYCapFloorEngineFixingTests

[`Divergences/YoYCapFloorEngineFixingTests.cs`](../tests/NanoQL.Tests/Divergences/YoYCapFloorEngineFixingTests.cs)

The year-on-year cap and floor engines' forward. QuantLib's engines take every optionlet's forward from the index's curve at the coupon's fixing date, `yoyTS->yoyRate(fixingDate)` (`inflationcapfloorengines.cpp:81`), and read no fixing, while QuantLib's own year-on-year pricer prices the same optionlet on the coupon's fixing (`inflationcouponpricer.cpp:99-109`; QuantLib's defect). nanoql's engines read each optionlet's forward as its coupon's own fixing, `CPI.LaggedYoYRate` at its accrual end with its lag and interpolation, and keep QuantLib's zero deviation for a fixing at or before the volatility's base date. QuantLib's check is the wheel's capped year-on-year coupon priced by its `BlackYoYInflationCouponPricer`, the caplet's rate times its accrual, nominal and discount. On `InflationFixture`'s market, a ratio of UK RPI and a Black volatility of 15% lagged two months, each case a one-coupon cap of 1,000,000, Actual/365 (Fixed): a coupon from 1 July 2023 to 1 July 2024, lagged three months, struck at 3.2%, fixed at 0.033084673723716485 on 1 April 2024 where the curve reads 3%: QuantLib's engine prices it at 0.0, its pricer and nanoql at 1,085.739999233565; the same coupon on a curve based in May 2024: QuantLib's engine refuses "date (April 1st, 2024) is before base date (May 1st, 2024)", its pricer and nanoql price it at 1,085.739999233565; a coupon from 15 July 2024 to 15 July 2025, linear, struck at 2.5%, on a curve falling from 3% in April 2024 to 2% in April 2028: the engine's forward is the curve's rate at May 2025's start, 0.02729637234770705, the coupon's 0.027200547570157427; QuantLib's engine prices it at 2,896.7205361166657, its pricer and nanoql at 2,829.215810328031; a coupon from 3 July 2025 to 3 July 2026 with five fixing days, struck at 2.5%, fixing on 28 April 2026, on the same curve: the engine's forward is April's, 0.025003422313483917, the coupon's May's, 0.02479808350444901; QuantLib's engine prices it at 1,947.2600270718465, its pricer and nanoql at 1,846.091044619172.

Facts:

- `AFixedCapletPricesOnItsFixing`
- `AFixingBeforeTheCurvesBaseDateIsPriced`
- `ALinearObservationInterpolatesTheForward`
- `FixingDaysMoveTheFixingDateAlone`

## YoYCapFloorPriceSurfaceTests

[`Divergences/YoYCapFloorPriceSurfaceTests.cs`](../tests/NanoQL.Tests/Divergences/YoYCapFloorPriceSurfaceTests.cs)

The year-on-year price surface's swap-rate dates and repricing check (fixed). The dates: QuantLib takes a valid maturity's date as `cfMaturities_.at(counter)`, counting only the valid maturities (`yoycapfloortermpricesurface.hpp:506-508`), so once an intersection fails every later date is an earlier maturity's. On QuantLib 1.44 (through the C++ harness: test-suite/inflationvolatility.cpp's euro market with the nominal curve's first date on the surface's reference date, observed flat, its 3-year caps ten times their quotes, so the 3-year intersection isn't found) the dates are 40505, 40505, 41236, 41966, 43062, 44888 and 46714, the 5-year rate on the 3-year date. nanoql's are each maturity's option date, 40505 to 50367, at the times QuantLib's time list gives; the rates are QuantLib's. The check: QuantLib checks the curve reprices its helpers at the surface's reference date plus whole years, where the helpers sit on the nominal curve's; with the nominal curve's dates 91 days later it refuses, "could not reprice helper 0, data 0.0244147, implied quote 0.0244373". nanoql checks each helper at its own maturity and builds the surface; where the two reference dates are 4 days apart, as in QuantLib's case, the check passes either way and the values are QuantLib's.

Facts:

- `ASwapRateIsDatedAtItsOwnMaturityAfterAFailedIntersection`
- `ANominalCurveDatedLaterStillRepricesItsHelpers`

## YoYInflationCapFloorEmptyLegTests

[`Divergences/YoYInflationCapFloorEmptyLegTests.cs`](../tests/NanoQL.Tests/Divergences/YoYInflationCapFloorEmptyLegTests.cs)

The last year-on-year coupon of a cap on an empty leg. QuantLib's `lastYoYInflationCoupon()` reads `yoyLeg_.back()` (`inflationcapfloor.cpp:109-115`), which is undefined for an empty leg; the wheel doesn't bind it. nanoql refuses with `NanoQLException` and the text QuantLib's `CashFlows` gives an empty leg, "empty leg", as `CapFloor.LastFloatingRateCoupon` does. The rest of the cap is QuantLib's defined behaviour, as the wheel gives it: it is expired and prices at 0.

Facts:

- `TheLastCouponOfAnEmptyLegIsRefused`

## YoYInflationCapFloorNullCashFlowTests

[`Divergences/YoYInflationCapFloorNullCashFlowTests.cs`](../tests/NanoQL.Tests/Divergences/YoYInflationCapFloorNullCashFlowTests.cs)

A year-on-year cap on a leg holding a null cash flow. QuantLib builds the instrument, and its `isExpired()` dereferences each cash flow (`inflationcapfloor.cpp:94-99`), which is undefined for a null one; the wheel refuses `None` in a `Leg`. nanoql refuses it at construction with `ArgumentException`, "null cash flow in leg", from every constructor, naming the leg, as `CapFloor` does.

Facts:

- `ANullCashFlowIsRefusedAtConstruction`

## YoYPricerMissingVolatilityTests

[`Divergences/YoYPricerMissingVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/YoYPricerMissingVolatilityTests.cs)

A year-on-year pricer with no volatility. QuantLib's `optionletRate` reads `capletVolatility()->baseDate()` before anything else (`inflationcouponpricer.cpp:99`), so its "missing optionlet volatility" check (`:112`) is unreachable and an empty handle throws "empty Handle cannot be dereferenced" on the wheel, for a fixed optionlet too (QuantLib's defect). nanoql checks the volatility first, refusing every optionlet with QuantLib's own "missing optionlet volatility".

Facts:

- `TheMissingVolatilityIsNamed`

## ZabrModelEmptyStrikesTests

[`Divergences/ZabrModelEmptyStrikesTests.cs`](../tests/NanoQL.Tests/Divergences/ZabrModelEmptyStrikesTests.cs)

The ZABR model's vector members given no strikes. QuantLib's `ZabrModel::x`, which `lognormalVolatility`, `normalVolatility` and `localVolatility` call on their vector, reads `strikes[0]` and starts its ascending check at `strikes.begin() + 1` (`zabr.cpp:321-327`), past the end of an empty vector; the wheel doesn't bind the model. nanoql refuses an empty vector with `ArgumentException`, "no strikes given", naming the member's parameter; given strikes, each member returns QuantLib's values, its scalar form's among them.

Facts:

- `AnEmptyStrikeVectorIsRefused`

## ZabrRestartInstanceTests

[`Divergences/ZabrRestartInstanceTests.cs`](../tests/NanoQL.Tests/Divergences/ZabrRestartInstanceTests.cs)

The model a ZABR interpolation prices with after its restarts. QuantLib's model is a smile section built from a copy of the parameters (`zabrinterpolation.hpp:110-115`), rebuilt at each evaluation of the cost function (`xabrinterpolation.hpp:292-307`); after the restarts the fit writes the best restart's parameters back and doesn't rebuild it (`:230-234`). So where the restarts run out without an accepted fit, or Simplex's last trial isn't the vertex it returns, the interpolation's values and its `rmsError()` and `maxError()` are its last trial's, while `alpha()`… are the best restart's: the C++ harness's fits below. nanoql rebuilds the section from the best parameters, so its values and errors are those of QuantLib's section built at the parameters it reports. A cube whose fits' restarts run out records its last trial's errors beside its best parameters, and refuses them against its tolerance where nanoql's cube records its best parameters' errors and builds. Each fit's claim is asserted in the same run, bit for bit: its values and errors are its own section's at the parameters it reports. QuantLib's numbers are asserted within bounds above what one-ulp moves of the five volatilities, together and each alone, change: the maximum-error fit's by up to 1.5e-11, within the vectors' tolerance; the Simplex fit's parameters by up to 1.6e-9 and its values by 2.9e-10, within 1e-8, both in the stability measure at scale 1.

Facts:

- `AFitWhoseRestartsRunOutPricesWithItsBestParameters`
- `ASimplexFitPricesWithTheVertexItReturns`
- `ACubeWhoseRestartsRunOutRecordsItsBestParametersErrors`

## ZabrSmileSectionParameterTests

[`Divergences/ZabrSmileSectionParameterTests.cs`](../tests/NanoQL.Tests/Divergences/ZabrSmileSectionParameterTests.cs)

A short-maturity ZABR smile section given fewer than five parameters. QuantLib's short-maturity `init` builds its model from `params_[0]` to `params_[4]` unchecked (`zabrsmilesection.hpp:139-152`), past the end of a shorter vector: the wheel's sections with α, β, ν and ρ alone gave the lognormal volatility 0.21300493901013692 at 4% in some runs and NaN in others, where γ = 1 given gives 0.21305710815847104. Only its local-volatility kernel checks the count, "zabr expects 5 parameters (alpha,beta,nu,rho,gamma) but (4) given" (`:158-160`). nanoql refuses fewer than five for every kernel, with that text, naming `zabrParameters`.

Facts:

- `FewerThanFiveParametersAreRefused`

## ZeroCouponInflationSwapBpsNpvDateTests

[`Divergences/ZeroCouponInflationSwapBpsNpvDateTests.cs`](../tests/NanoQL.Tests/Divergences/ZeroCouponInflationSwapBpsNpvDateTests.cs)

`ZeroCouponInflationSwap.FixedLegBPS` at an NPV date: QuantLib's `fixedLegBPS` discounts the fixed flow's move by the fixed leg's end discount, read from the curve's reference date (`zerocouponinflationswap.cpp:147-154`), while the engine gives the leg NPVs at its NPV date, so with a later NPV date the BPS isn't the fixed leg's NPV move. On the wheel, on the vectors' first swap (a payer of 1,000,000 at 3.2% to 15 June 2029), QuantLib's BPS is 464.37247408343586 at every NPV date, where the fixed leg's NPV moves by 483.32387426443165 for a basis point at an NPV date a year after the evaluation date and 523.5785024892248 three years after. nanoql's is the end discount over the NPV date's discount, the NPV move; at the default NPV date QuantLib's bits (the vectors). QuantLib's check: its own fixed leg NPVs at the rate and a basis point above it, at the NPV date.

Facts:

- `AtAnNpvDateTheBpsIsTheFixedLegsMove`

## ZeroCouponInflationSwapConventionTests

[`Divergences/ZeroCouponInflationSwapConventionTests.cs`](../tests/NanoQL.Tests/Divergences/ZeroCouponInflationSwapConventionTests.cs)

`ZeroCouponInflationSwap`'s inflation-leg convention given as Following. QuantLib's default for it is `BusinessDayConvention()`, which is Following (0), and the constructor replaces that value by the fixed leg's convention (`zerocouponinflationswap.cpp:73-74`), so an explicit Following is silently replaced (QuantLib's defect). nanoql's parameter is a nullable convention, null for the fixed leg's, so a given Following is kept. For a swap from 15 June 2024 to Saturday 31 May 2025 on the UK calendar, its fixed leg ModifiedFollowing, the wheel's inflation leg asked for Following pays on 30 May 2025, as the fixed leg does; nanoql's pays on 2 June.

Facts:

- `AGivenFollowingIsKept`

## ZeroCouponInflationSwapFairRateTests

[`Divergences/ZeroCouponInflationSwapFairRateTests.cs`](../tests/NanoQL.Tests/Divergences/ZeroCouponInflationSwapFairRateTests.cs)

`ZeroCouponInflationSwap.FairRate` where the legs pay on different days. QuantLib's fair rate is the inflation growth's rate, `growth^(1/T) - 1` (`zerocouponinflationswap.cpp:118-138`), whose comment reads "Always means that NPV is zero for _this_ instrument", which fails once the legs' conventions part their payment days (QuantLib's defect). nanoql scales the growth's excess by the ratio of the legs' end discounts, read from the engine's results. On `InflationFixture`'s market, for a payer swap of 1,000,000 from 15 June 2024 to Saturday 31 May 2025, its fixed leg Following (paying 2 June) and its inflation leg Preceding (paying 30 May), QuantLib's fair rate is 0.03078176611785821 and the swap rebuilt at it is worth -9.332619118980801; the root of QuantLib's own NPV, found by QuantLib's `Brent` through the wheel, is 0.030791894092525723, and it is nanoql's fair rate.

Facts:

- `TheFairRateZeroesTheSwap`

## ZeroCouponSwapFairPaymentDelayTests

[`Divergences/ZeroCouponSwapFairPaymentDelayTests.cs`](../tests/NanoQL.Tests/Divergences/ZeroCouponSwapFairPaymentDelayTests.cs)

The zero-coupon swap's fair payment with a payment delay: QuantLib's `fairFixedPayment` divides the floating leg's NPV by its fixed leg's end discount (`zerocouponswap.cpp:147-153`), read at `CashFlows::maturityDate` of that leg (`discountingswapengine.cpp:97-99`), the accrual end for the fixed-rate constructor's coupon, paid a payment delay later (`cashflows.cpp:52-64`), so that swap's fair rate fed back leaves a non-zero NPV: on the wheel, on the vectors' 5-year receiver on 1e8 from 17 January 2025 at 3.2% on Actual/365 (Fixed), −1,013.7182254604995, −14,173.113207207993, −42,397.21247311309 and −131,035.65942218527 at delays of 1, 10, 30 and 90 days, where the fixed-payment constructor's fair payment, 16,776,091.202675004 at every delay, fed back gives 0. nanoql discounts at the fixed flow's payment date. QuantLib's check: its fixed-payment constructor on the same swap, its fair payment and rate. At an NPV date: the floating leg's NPV is given at the engine's NPV date (`cashflows.cpp:498`) and its end discount from the curve's reference date (`discountingswapengine.cpp:97-101`), so QuantLib's fair payment is high by the NPV date's discount's reciprocal: on the same swap without a delay, 17,200,779.97339953 at an NPV date of 15 January 2026 and 18,085,160.07066775 at 17 January 2028, fed back through the fixed-payment constructor at those dates 384,195.3384123612 and 1,245,139.539481705. nanoql takes the NPV back by the NPV date's discount, as QuantLib's asset swap does (`assetswap.cpp:273`, `:294`). QuantLib's check: its fair payment with no NPV date, and the fed-back NPV.

Facts:

- `TheFairPaymentIsTheFixedPaymentConstructors`
- `TheFairRateFedBackIsAtPar`
- `AtAnNpvDateTheFairPaymentIsTheReferenceDates`

## ZeroGearingWithoutDayCounterTests

[`Divergences/ZeroGearingWithoutDayCounterTests.cs`](../tests/NanoQL.Tests/Divergences/ZeroGearingWithoutDayCounterTests.cs)

A floating leg whose period has a zero gearing becomes a fixed-rate coupon on the leg's payment day counter (`cashflowvectors.hpp:134-136`; for an overnight leg, `overnightindexedcoupon.cpp`'s `OvernightLeg`). Without one, QuantLib builds that coupon on an empty day counter and fails only when it is priced, "no day counter implementation provided" (`daycounter.hpp:118`); nanoql's `FixedRateCoupon` needs a day counter, so the leg is refused once it is built, after every refusal QuantLib raises while building it, with QuantLib's text: a builder's stored setting with `NanoQLException`, and the function form's argument with `ArgumentException`.

Facts:

- `AZeroGearingPeriodWithoutAPaymentDayCounterIsRefusedAtBuild`
- `AnOvernightLegsZeroGearingPeriodWithoutAPaymentDayCounterIsRefusedAtBuild`

## ZeroSpreadedCompoundingTests

[`Divergences/ZeroSpreadedCompoundingTests.cs`](../tests/NanoQL.Tests/Divergences/ZeroSpreadedCompoundingTests.cs)

The zero-spreaded curve's compounding and frequency are checked when it is built (the playbook's enum row): a value outside its enum is refused with `ArgumentOutOfRangeException` naming the parameter, "unknown compounding convention" or "unknown frequency (99)". QuantLib stores both and computes with them at every query (`zerospreadedtermstructure.hpp:90-98`): an unknown compounding builds and each query that reads a zero rate fails, "null interest rate", since `impliedRate` leaves its rate null for it (`interestrate.cpp:111-132`) and the rate's compound factor refuses that (`:47`); an unknown frequency builds and computes, a compounded spread as one compounded 99 times a year (a discount factor of 0.9607928351209212 at one year on a flat 3% Actual/365 (Fixed) curve with a 1% spread), a continuous one as if it had none.

Facts:

- `AnUnknownCompoundingOrFrequencyIsRefusedWhenTheCurveIsBuilt`

## QLNet 1.13.2

Every documented divergence from QLNet 1.13.2, the oracle of the extensions' ports of QLNet, one
entry per test class under `tests/NanoQL.Tests/Divergences/QLNet/`, each recording QLNet's value
beside nanoql's.

### AmortizedCostBonds

[`Divergences/QLNet/AmortizedCostBonds.cs`](../tests/NanoQL.Tests/Divergences/QLNet/AmortizedCostBonds.cs)

The bonds the amortised-cost divergence facts build: QLNet's AB1 and AB2 (`testAmortizingBond1`, `testAmortizingBond2`) and their variants, on `NullCalendar`.

### AmortizedCostEntriesTests

[`Divergences/QLNet/AmortizedCostEntriesTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/AmortizedCostEntriesTests.cs)

`AmortizingBond`'s amortisations. Each pays the leg's own coupon, day-counted, where QLNet's pays the face amount times the rate over the frequency (`AmortizingBond.cs:58`), so that on Actual/365 (Fixed) QLNet's carrying amount misses the face amount at maturity, 1,000,367.33 on a million; nanoql's is the remaining flows' value at the effective rate at each coupon date. Each is signed, the interest less the coupon, where QLNet's sign is the bond's being at a premium (`:118-124`): a premium given a rate above its coupon accretes, where QLNet's amortises it, 42,875.52 on AB1 at 8% after a period where nanoql's is 43,948.48. A given rate of 0 is taken, where QLNet solves instead (`:60`; −0.0238 on AB1). The last closes to the face amount, where QLNet's cents leave AB2 at 500,000.02. The bond's cash flows are its coupons and redemption, its calendar the one given, where QLNet's hold 41 accounting entries and coupons with overridden amounts among its 83, on TARGET (`:43`, `:127-128`).

Facts:

- `ACouponDatesCarryingAmountIsTheRemainingFlowsValueOnAnyDayCounter`
- `AnAmortisationsSignIsItsInterestLessItsCoupon`
- `AGivenRateOfZeroIsTaken`
- `TheLastAmortisationClosesToTheFaceAmount`
- `TheCashFlowsAreTheBondsLeg`

### AmortizedCostPartialPeriodTests

[`Divergences/QLNet/AmortizedCostPartialPeriodTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/AmortizedCostPartialPeriodTests.cs)

Between coupon dates, `AmortizingBond.AmortizationValue` accrues on the carrying amount at the period's start, over the coupon's own period: QLNet accrues every period on the cost, with no reference period (`AmortizingBond.cs:163-172`), so its carrying amount jumps at each coupon date, by 28.28 on a quarterly discount bond (96,952.32 the day before 15 June 2016, 96,980.60 on it) and by 68.96 before 15 December 2019. The check is QLNet's own `AmortizationValue` at the period's start and its `FixedRateCoupon` amounts on that carrying amount since then (the vectors' between-dates cases).

Facts:

- `TheCarryingAmountIsContinuousAtACouponDate`

### AmortizedCostTradeDateTests

[`Divergences/QLNet/AmortizedCostTradeDateTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/AmortizedCostTradeDateTests.cs)

`AmortizingBond` solves its effective rate on the trade date, at initial recognition, as IFRS 9's and ASC 310-20's interest method require: the price paid, the cost plus the accrued interest purchased, against the remaining flows. QLNet takes the cost as the carrying amount on the coupon date on or before the trade, and solves there (`AmortizingBond.cs:205-207`); its first period then runs from that coupon date while its `AmortizationValue` accrues from the trade, so on AB2 its carrying amount jumps 96.34 at the first coupon date (471,724.66 the day before, 471,821.00 on it), and with no coupon before the trade it solves at its global evaluation date, a trade on 1 May 2010 of a bond issued 15 March 2010 yielding 0.0546 evaluated at the issue date, 0.0620 evaluated on 1 January 2012, and throwing evaluated after maturity. On a purchase on a coupon date the two methods agree, which the vectors check. The checks here: the rate is QuantLib's own `CashFlows.Yield` on the price paid at the trade date; the carrying amount is the cost on the trade date, continuous at the coupon dates, and the face amount at maturity; and each coupon date's is the remaining flows' value at the effective rate.

Facts:

- `TheRateIsTheYieldOfThePricePaidAtTheTradeDate`
- `TheCarryingAmountIsContinuousFromTheCost`
- `EachCouponDatesCarryingAmountIsTheRemainingFlowsValue`
- `ATradeInTheFirstPeriodDoesntReadTheEvaluationDate`

### MbsFlowOrderTests

[`Divergences/QLNet/MbsFlowOrderTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/MbsFlowOrderTests.cs)

`MBSFixedRateBond`'s flows on a date are its coupon, then its scheduled principal, then its prepayment, as a bond's redemptions follow its coupons on a date (QuantLib's `bond.cpp:325-328`). QLNet's are the prepayment, the scheduled principal and the coupon (`MBSFixedRateBond.cs:70-72`). The amounts and dates are QLNet's bit for bit (the vectors); a sum over a date's flows, as `PVDifference` takes, adds them in this order.

Facts:

- `ADatesCouponComesFirst`

### MbsFrequencyTests

[`Divergences/QLNet/MbsFrequencyTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/MbsFrequencyTests.cs)

A pool of a frequency other than monthly prepays at its model's rate: a period survives the product of its months' survival, each month's SMM at its own start, and a period counted in weeks or days its start's monthly survival to the power of the months it spans, each calendar month's share of it. QLNet applies one month's SMM a period (`MBSFixedRateBond.cs:61-62`), so a CPR of 8% prepays at 2.74% a year quarterly and 1.38% semi-annually, and its yields refuse such a pool ("root not bracketed", its bracket and formula being monthly). The checks: QLNet's own MBS fed the period's SMM (the vectors' quarterly and semi-annual cases); the constant-CPR closed form, a span's balance (1 − CPR)^(T/12) of the level-payment schedule's, T its months by an independent count; and the yields' closed form at par, the pass-through rate per period compounded to a month and to a half year.

Facts:

- `AConstantCprPrepaysAtItsRate`
- `AShortPeriodsPoolFollowsTheClosedForm`
- `TheYieldsAtParAreThePassThroughRatesEquivalents`

### MbsPayOffTests

[`Divergences/QLNet/MbsPayOffTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/MbsPayOffTests.cs)

A pool's pay-off. Under SIFMA's cap (`PsaCapAndFloorTests`), a 2,000% PSA pool prepays its whole balance at the 25th period, where its CPR reaches 100%, and its flows after are 0 but for one period's residue of rounding, 7.45e-9; QLNet's CPR passes 1 there and its flows from the 26th prepayment and 27th coupon are NaN, 335 of them. After a pay-off `MBSFixedRateBond.MonthlyYield`, `MBSFixedRateBond.BondEquivalentYield`, `Yield` and `Price` refuse the pool, its balance 0, as `BondFunctions` refuses a bond not tradable at its settlement: QLNet's yields throw too, on its stale notional (376,325,238.28 on a pool at CPR 1 settled 1 June 2007), where a solve on the pool's balance would return 0, QLNet's `PVDifference` being exactly 0 at both ends of its bracket. QLNet's 25th prepayment stands within six decimal places: GitHub's windows-2022 runner's C runtime and processor give it one ulp more, 59878137.771088034 (CI run 41543305018).

Facts:

- `A2000PercentPsaPoolsFlowsAfterThePayOffAreZero`
- `ThePaidOffPoolsYieldsAreRefused`

### MbsPaymentDatesTests

[`Divergences/QLNet/MbsPaymentDatesTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/MbsPaymentDatesTests.cs)

`MBSFixedRateBond`'s flows pay on each period's end adjusted by its payment convention on its calendar. QLNet's MBS takes the convention and ignores it (`MBSFixedRateBond.cs:67-69`): on TARGET with Following, its default, the Fabozzi pool's second payment is 1 April 2007, a Sunday, and 148 of its 358 payment dates aren't business days. The check is QLNet's level-payment base bond on the same calendar and convention, whose dates the vectors' TARGET case reads; the amounts don't move.

Facts:

- `FlowsPayOnTheAdjustedPeriodEnds`
- `TheAmountsAndTheYieldDontMove`

### MbsPoolBalanceTests

[`Divergences/QLNet/MbsPoolBalanceTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/MbsPoolBalanceTests.cs)

`MBSFixedRateBond`'s notional schedule and redemptions are the pool's expected ones, so its yields price the pool's balance at the settlement. QLNet's MBS keeps its level-payment base bond's notionals (`MBSFixedRateBond.cs:39`, `:96`), the balance with no prepayment: on the Fabozzi pool settled 1 February 2012, QLNet's notional is 371,882,785.38 against the pool's 291,474,567.29, and its monthly yield 0.0017466, where a pool priced at its own balance yields its pass-through rate, 0.055/12 a month. The check is QLNet's own `PVDifference` and `Brent` on the pool's balance (the vectors' seasoned cases); here, that yield. The yields also count the settlement date's flows as paid whatever the context's settings, as the balance does, where QLNet's include them under either setting that does: on that payment date its monthly yield moves to 0.0018220.

Facts:

- `TheNotionalIsThePoolsBalance`
- `ASeasonedPoolAtItsBalanceYieldsItsPassThroughRate`
- `TheYieldsPriceTheSettlementDatesFlowsAsPaid`

### MbsPoolMonthsTests

[`Divergences/QLNet/MbsPoolMonthsTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/MbsPoolMonthsTests.cs)

`MBSFixedRateBond` reads its prepayment model at its start's anniversaries: period i's month at the start plus the pool's age plus i months, so a pool on the PSA curve takes SF-6's MONTH in every period (`PsaMonthCountTests`). QLNet reads a schedule date plus the age (`MBSFixedRateBond.cs:61`, `:84`), the same date from a start before the 29th, and on its Bond Basis month: from a start on 31 January 2007 its pool takes MONTH 1's CPR in its second period and MONTH 13's in its 14th (0.2% and 2.6% at 100% PSA), skipping MONTHs 2 and 14. The check is SIFMA's formula, each period's CPR SF-6's at its MONTH; QLNet's bits hold from a start before the 29th (the vectors).

Facts:

- `APoolFromAMonthEndTakesSf6sMonthInEveryPeriod`
- `ThePublicSmmIsTheModelAtADatePlusTheAge`

### MbsPools

[`Divergences/QLNet/MbsPools.cs`](../tests/NanoQL.Tests/Divergences/QLNet/MbsPools.cs)

The pools the MBS divergence facts build: QLNet's Fabozzi pool (`testMBSFixedBondCached`) and its variants, 400 million from 1 February 2007, 30/360 Bond Basis.

### MbsYieldBracketTests

[`Divergences/QLNet/MbsYieldBracketTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/MbsYieldBracketTests.cs)

`MBSFixedRateBond.MonthlyYield` solves in QLNet's bracket of [0, 1]% a month where it holds the root, and otherwise by Brent's step search from QLNet's guess, above −100% a month. QLNet's solve refuses a yield outside its bracket (`MBSFixedRateBond.cs:97`): a 13% pass-through priced at par throws "root not bracketed: f[0,1]". The checks: the PV difference at the root, and the closed form at par, the pass-through rate over 12.

Facts:

- `AYieldAboveQLNetsBracketIsSolved`
- `AYieldBelowQLNetsBracketIsSolved`

### MbsYoungPoolTests

[`Divergences/QLNet/MbsYoungPoolTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/MbsYoungPoolTests.cs)

A pool whose original length is shorter than its remaining term, so that its age is negative, prepays at MONTH 1 until its loans' first month, as SIFMA's PSA model prices it (Chapter SF, SF-6, "MONTH equals 1 for all prior months"; its same-month pools are `Reference/SifmaPassThroughTests`'). QLNet's PSA falls to 0 and below there (`PSACurve.cs:38`), so an original length of 300 on a 358-month term gives 57 negative prepayments, the first −3,611,165.28. The check is SIFMA's formula, and QLNet's bits wherever the age is 0 or more (the vectors).

Facts:

- `NoPrepaymentIsNegative`
- `TheMonthsBeforeTheLoansFirstPrepayAtMonthOne`

### PrepayRateRangeTests

[`Divergences/QLNet/PrepayRateRangeTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/PrepayRateRangeTests.cs)

A prepayment rate outside SIFMA's range is refused at construction (Chapter SF: an SMM is "the percentage of the mortgage loans outstanding at the beginning of a month assumed to terminate during the month", SF-5, and SF-6 caps the CPR at 100), where QLNet takes it: `ConstantCPR(1.01)` gives an SMM of NaN, `ConstantCPR(-0.01)` a negative SMM, and a negative PSA multiplier negative rates (`ConstantCPR.cs:25-36`, `PSACurve.cs:29-46`). `ConstantCPR` refuses a CPR outside [0, 1] and `PSACurve` a negative or non-finite multiplier, with `ArgumentOutOfRangeException`. QLNet's values are QLNet 1.13.2's.

Facts:

- `AConstantCprOutsideZeroToOneIsRefused`
- `ANegativePsaMultiplierIsRefused`

### PsaCapAndFloorTests

[`Divergences/QLNet/PsaCapAndFloorTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/PsaCapAndFloorTests.cs)

`PSACurve` follows SIFMA's PSA formula at its two ends (Chapter SF, SF-6), where QLNet's `PSACurve.getCPR` (`PSACurve.cs:35-41`) has neither bound. Above 1,666⅔% PSA, QLNet's CPR passes 1 and its SMM is NaN, so an MBS's flows are NaN; nanoql caps the CPR at 100%, its SMM 1. Before the curve's start, QLNet's month falls to 0 and below, its CPR 0 and then negative; nanoql counts every prior month as the first, as SIFMA's "MONTH equals 1 for all prior months". QLNet's values are QLNet 1.13.2's; elsewhere the two agree bit for bit (the vectors).

Facts:

- `ACprAboveOneIsCappedAtOne`
- `BelowTheCapTheRatesAreQLNets`
- `AMonthBeforeTheStartCountsAsTheFirst`

### PsaMonthCountTests

[`Divergences/QLNet/PsaMonthCountTests.cs`](../tests/NanoQL.Tests/Divergences/QLNet/PsaMonthCountTests.cs)

`PSACurve`'s MONTH is SF-6's, "the accrual period during which the age of the mortgage loans increases from MONTH – 1 to MONTH": the whole calendar months from the curve's start, plus one. QLNet counts the 30/360 Bond Basis days from the start over 30, plus one (`PSACurve.cs:37-38`). From a start on the 29th to the 31st, February's anniversary is under 30 such days after January's, so QLNet repeats the month there and skips the next (from 31 January 2007, MONTH 1 on 28 February and 3 on 31 March; MONTH 13 on 29 February 2008); from a start on the 1st it counts a 31st a month ahead (MONTH 2 on 31 March 2007 from 1 March), and from a 31st the 30th of a 31-day month (MONTH 3 on 30 March 2007). The check is SIFMA's formula (`Reference/SifmaPrepaymentTests`); QLNet's bits hold wherever the two counts agree (the vectors, which leave out the dates where they don't). QLNet's values are QLNet 1.13.2's.

Facts:

- `TheMonthIsTheCalendarMonthsFromTheStart`
- `WhereTheCountsAgreeTheRatesAreQLNets`
