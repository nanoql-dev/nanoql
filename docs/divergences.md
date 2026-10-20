<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Divergences

Every documented divergence from QuantLib v1.44, one entry per test class under
`tests/NanoQL.Tests/Divergences/` with its file, its summary and its facts, generated from those files by
`tools/divergences.py`: regenerate it with `.venv\Scripts\python -I tools/divergences.py` rather than
editing it by hand.

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

## AnalyticEuropeanVolatilityResultTests

[`Divergences/AnalyticEuropeanVolatilityResultTests.cs`](../tests/NanoQL.Tests/Divergences/AnalyticEuropeanVolatilityResultTests.cs)

`AnalyticEuropeanEngine`'s "volatility" additional result for an option expiring on the evaluation date, priced when the reference date's events are included. QuantLib reports √(variance / time to expiry) (`analyticeuropeanengine.cpp:113`), 0/0 there, NaN. nanoql reports the Black volatility at the exercise date and strike, QuantLib's own value for that date; every other result is QuantLib's.

Facts:

- `TheVolatilityAtExpiryIsTheStructures`

## BMANullIndexTests

[`Divergences/BMANullIndexTests.cs`](../tests/NanoQL.Tests/Divergences/BMANullIndexTests.cs)

A null BMA index is refused on entry, with QuantLib's text for a missing index, "no index provided" (`floatingratecoupon.cpp:52`), naming the parameter. QuantLib's `AverageBMACoupon` reads its index's fixing days before its base checks the index (`averagebmacoupon.cpp:111-113`), `AverageBMALeg` never checks it (`:165-166`) and its coupons then read it, and `BMASwap` passes it to its BMA leg (`bmaswap.cpp:55-58`): results C++ leaves undefined, where the wheel stops on Boost's assertion.

Facts:

- `ANullBMAIndexIsRefusedWithQuantLibsText`

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

## BlackFormulaNullPayoffTests

[`Divergences/BlackFormulaNullPayoffTests.cs`](../tests/NanoQL.Tests/Divergences/BlackFormulaNullPayoffTests.cs)

A null payoff in a Black formula's payoff overload. QuantLib's fifteen payoff overloads read the payoff's option type or strike through the pointer before anything else (`blackformula.cpp:105, 144, 195, 257, 330, 456, 564, 597, 628, 675, 709, 742, 767, 958, 981`), a null dereference, undefined under `std::shared_ptr` and a Boost assertion by default. nanoql refuses it first, with `ArgumentNullException` naming `payoff`.

Facts:

- `ANullPayoffIsRefusedBeforeItIsRead`

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

## BlackVolTimeExtrapolationRefusalTests

[`Divergences/BlackVolTimeExtrapolationRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/BlackVolTimeExtrapolationRefusalTests.cs)

`BlackVolTimeExtrapolation` on too few times. QuantLib's surface overload reads `times[N-2]` unchecked under the linear strategy (`blackvoltimeextrapolation.cpp:49-50`), past the list's start with one time, where its curve overload refuses "at least two times required for volatility extrapolation" (`:86-88`); and both overloads' flat strategy read `times.back()` of an empty list (`:39`, `:44`). Both are results C++ leaves undefined. nanoql refuses them with `ArgumentException` naming `times`: the first with the curve overload's text, the second with "at least one time required for volatility extrapolation".

Facts:

- `TheSurfaceOverloadRefusesOneTimeUnderTheLinearStrategy`
- `TheFlatStrategyRefusesNoTimes`

## BondEmptyLegTests

[`Divergences/BondEmptyLegTests.cs`](../tests/NanoQL.Tests/Divergences/BondEmptyLegTests.cs)

A bond on an empty leg, the default of QuantLib's two public constructors, and a bond on a leg holding a null cash flow. QuantLib builds the bond on an empty leg and leaves its notional schedule empty (`bond.cpp:42, 72`), and its notional then reads the schedule's last date (`bond.cpp:117`), past the end of an empty vector, which is undefined: the wheel ends the process on `notional()`, `isTradable()`, `accruedAmount()` and `cleanPrice()`. nanoql builds the bond as QuantLib does, with QuantLib's defined answers (its NPV the expired bond's 0, its maturity date refused "empty leg", its redemption "multiple redemption cash flows given"), and its `Bond.Notional` refuses where QuantLib reads past the end, with `NanoQLException`. A null cash flow is dereferenced by QuantLib's sort (`bond.cpp:43-44, 74-75`), which is undefined, and the wheel refuses `None` in a leg; nanoql refuses it at construction with `ArgumentException`, as a swap does.

Facts:

- `ABondOnAnEmptyLegBuildsAndRefusesItsNotional`
- `ANullCashFlowIsRefusedAtConstruction`

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

## CPISwapFairRateTests

[`Divergences/CPISwapFairRateTests.cs`](../tests/NanoQL.Tests/Divergences/CPISwapFairRateTests.cs)

`CPISwap`'s fair rate. QuantLib derives it as `fixedRate - NPV / (legBPS[0] / 1bp)` (`cpiswap.cpp:195-199`), but a CPI coupon's sensitivity to its rate carries its index ratio, which the BPS leaves out, so the swap repriced at QuantLib's fair rate isn't worth zero (QuantLib's defect, R8). nanoql's `DiscountingSwapEngine` gives the rate that zeroes the swap, `fixedRate - NPV / (payer[0] S)`, S the NPV of the swap's CPI coupons rebuilt at a unit rate. On `InflationFixture`'s market, for a five-year payer swap of 1,000,000 against a 6-month Ibor leg on a curve flat at 4.5% plus 0.1%, at 2% on a base CPI of 306.1, QuantLib's fair rate is 0.011610586633227834 and the swap rebuilt at it is worth -4,112.158481168008; the root of QuantLib's own NPV, found by QuantLib's `Brent` through the wheel, is 0.012444035321727333, where the swap is worth 0.0, and it is nanoql's fair rate.

Facts:

- `TheFairRateZeroesTheSwap`

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

## CalibratedModelSideEffectTests

[`Divergences/CalibratedModelSideEffectTests.cs`](../tests/NanoQL.Tests/Divergences/CalibratedModelSideEffectTests.cs)

What QuantLib's calibrated model changes in itself. Its `setParams` writes each parameter as it reaches it (`model.cpp:138-149`): an array too short sets the leading parameters before refusing "parameter array too small", one too long sets them all before refusing "parameter array too big!" (the wheel: `[0.2, 0.01]` and `[0.2, 0.02]` left on a Hull-White model from `[0.1, 0.01]`). Its `value` sets the parameters it is given into the model before it prices the helpers (`model.cpp:46-47, 117-124`), so the model is left at them, and the helpers whose engines hold it price at them afterwards. nanoql's models are immutable: `SetParams` returns a new model, and refuses both with QuantLib's texts before anything is written, so the receiver keeps its parameters; and `Value` prices against a new model with the parameters and changes nothing.

Facts:

- `ARefusedSetParamsLeavesTheModelUnchanged`
- `ValueLeavesTheModelUnchanged`

## CalibrationNullHelperTests

[`Divergences/CalibrationNullHelperTests.cs`](../tests/NanoQL.Tests/Divergences/CalibrationNullHelperTests.cs)

A null helper among a calibration's. QuantLib's `calibrate` and `value` take a vector of helper pointers, which may hold a null one, and the calibration function dereferences each at its first evaluation (`model.cpp:49-51, 59-61`), undefined for a null pointer (the wheel's binding refuses `None` before the call). nanoql's `Calibrate` and `Value` copy the helpers and refuse a null one first, with `ArgumentException`, "null calibration helper", naming `helpers`.

Facts:

- `ANullHelperIsRefused`

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

A caplet paid between the discount curve's reference date and the evaluation date. QuantLib's arguments store `Null<Rate>()` as the forward of a coupon paid before the evaluation date (`capfloor.cpp:245-249`), and the Black and Bachelier engines price every caplet paid after the curve's reference date (`blackcapfloorengine.cpp:93-100`, `bacheliercapfloorengine.cpp:78-86`), so on a curve dated before the evaluation date they price that caplet on the sentinel, 3.4028234663852886e+38: the wheel's NPV of this 1Y cap is 1.7146697606502935e+38, its first caplet's, on both engines. nanoql's forward is a `double?`, null there, and the engines refuse a priced caplet without one, with `NanoQLException`. On a curve dated on the evaluation date the caplet is skipped and both price it as QuantLib does: 0.010031644352251482, its ATM forward null where QuantLib's is the sentinel.

Facts:

- `APricedCapletWithoutAForwardIsRefused`

## CapFloorTermVolCurveDateTests

[`Divergences/CapFloorTermVolCurveDateTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorTermVolCurveDateTests.cs)

A cap/floor term-volatility curve after the evaluation date moves. QuantLib's moving curve recomputes its option dates and times in `update()` before its base clears the cached reference date (`capfloortermvolcurve.cpp:155-167`, `termstructure.cpp:48-52`), so after a move its times are those of the reference date the previous evaluation date gave, and its spline is over them: built on 16 June 2025 with two settlement days, tenors 1Y, 2Y and 3Y at 0.20, 0.21 and 0.22, and moved to 20 June, the wheel's curve has the reference date 24 June 2025 and the maximum date 26 June 2028, and its volatility at 1.5 years is 0.20500509503283154, its value before the move. nanoql's curves have fixed reference dates (R1): the curve for 20 June is built under that date, and gives the value of a curve QuantLib builds on it, 0.20500761138338572.

Facts:

- `ACurveUnderALaterEvaluationDateIsANewCurve`

## CapFloorTermVolSurfaceDateTests

[`Divergences/CapFloorTermVolSurfaceDateTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorTermVolSurfaceDateTests.cs)

A cap/floor term-volatility surface after the evaluation date moves. QuantLib's moving surface recomputes its option dates in `update()` before its base clears the cached reference date (`capfloortermvolsurface.cpp:195-207`, `termstructure.cpp:39-52`), so after a move its dates are those of the reference date the previous evaluation date gave: built on 16 June 2025 with two settlement days and moved through 20, 25 and 30 June, the wheel's surface has the reference date 2 July 2025 and the option dates of 27 June's (29 June 2026, 28 June 2027, 27 June 2028, 27 June 2030), and its volatility at 1.5 years and 3.5% is 0.22743898630625792, at 30 June 2027 0.22282736247815785. nanoql's surfaces have fixed reference dates (R1): the surface for 30 June is built under that date, and gives the values of a surface QuantLib builds on it: the option dates of 2 July (2 July 2026, 2 July 2027, 3 July 2028, 2 July 2030), 0.2273976824337007 and 0.22280134235888954.

Facts:

- `ASurfaceUnderALaterEvaluationDateIsANewSurface`

## CapFloorTermVolSurfaceEmptyMatrixTests

[`Divergences/CapFloorTermVolSurfaceEmptyMatrixTests.cs`](../tests/NanoQL.Tests/Divergences/CapFloorTermVolSurfaceEmptyMatrixTests.cs)

A cap/floor term-volatility surface on an empty matrix. QuantLib's constructors on quotes size their matrix from the first row, `vols_(vols.size(), vols[0].size())`, before any check (`capfloortermvolsurface.cpp:45, 77`), which is undefined for no rows: the wheel ends the process (0xC0000005 in all 18 recorded runs, with or without tenors). Its constructors on a matrix check it first, in `checkInputs` (`capfloortermvolsurface.cpp:154-177`): "empty option tenor vector", or "mismatch between number of option tenors (2) and number of volatility rows (0)", the wheel's texts. nanoql's quote and matrix forms are one (R1's quote row), so an empty matrix reaches QuantLib's own checks, refused with `ArgumentException`.

Facts:

- `AnEmptyMatrixReachesQuantLibsChecks`

## CappedFlooredCouponNullUnderlyingTests

[`Divergences/CappedFlooredCouponNullUnderlyingTests.cs`](../tests/NanoQL.Tests/Divergences/CappedFlooredCouponNullUnderlyingTests.cs)

A capped coupon over a null underlying. QuantLib's constructor reads the underlying's terms in its base initializer (`capflooredcoupon.cpp:30-43`), dereferencing the null pointer: a Boost assertion in the default build, undefined under `std::shared_ptr`. nanoql refuses it with `ArgumentNullException`.

Facts:

- `ANullUnderlyingIsRefused`

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

## CdsExpiredAccrualRebateTests

[`Divergences/CdsExpiredAccrualRebateTests.cs`](../tests/NanoQL.Tests/Divergences/CdsExpiredAccrualRebateTests.cs)

An expired credit default swap's accrual-rebate NPV. QuantLib's `setupExpired` zeroes seven results and leaves the accrual rebate's NPV unset (`creditdefaultswap.cpp:215-220`), so it reads an uninitialized member: on the wheel, for `CreditFixture`'s expired swap priced by the mid-point engine, 7.565977700541159e-307 in one fresh process and 1.9925540477164e-311 in another (QuantLib's defect, R8). nanoql's expired results are all 0, the accrual rebate's NPV included.

Facts:

- `TheAccrualRebateNpvOfAnExpiredSwapIsZero`

## CdsImpliedHazardRateExpiredTests

[`Divergences/CdsImpliedHazardRateExpiredTests.cs`](../tests/NanoQL.Tests/Divergences/CdsImpliedHazardRateExpiredTests.cs)

The implied hazard rate of an expired credit default swap. QuantLib's `impliedHazardRate` calls the engine directly, without the expiry check (`creditdefaultswap.cpp:317-336`), and the engine finds every coupon occurred, so the objective is the target less a constant: for `CreditFixture`'s expired swap, under either model, a zero target returns the solver's guess, 0.016898148148148148, and a target of 1 fails to bracket ("unable to bracket root in 100 function evaluations …") (QuantLib's defect, R8). nanoql refuses an expired swap with a `NanoQLException`, "instrument expired", QuantLib's text in `Swaption::impliedVolatility`.

Facts:

- `AZeroTargetIsRefused`
- `ANonZeroTargetIsRefused`

## CouponPricerSettingTests

[`Divergences/CouponPricerSettingTests.cs`](../tests/NanoQL.Tests/Divergences/CouponPricerSettingTests.cs)

Setting pricers on a leg. QuantLib's `setCouponPricer` and `setCouponPricers` change the coupons in place, one after another (`couponpricer.cpp:461-466, 480-483`): a coupon that refuses the pricer leaves the earlier ones holding it. Their undefined steps: an empty pricer list on a non-empty leg reads the pricer before the list's start (`:481`), and a null cash flow is dereferenced (`:464, 482`; the first-matching forms swallow the failure and skip it, `:446-456`). nanoql returns a new leg, all or nothing, and refuses both inputs.

Facts:

- `ACouponRefusingThePricerLeavesNoNewLeg`
- `AnEmptyPricerListIsRefused`
- `ANullCashFlowInTheLegIsRefused`

## CpiLegBaseFixingTests

[`Divergences/CpiLegBaseFixingTests.cs`](../tests/NanoQL.Tests/Divergences/CpiLegBaseFixingTests.cs)

A CPI leg's base. QuantLib's `CPILeg` gives each coupon `laggedFixing(baseDate + lag, lag)` and the notional flow `laggedFixing(baseDate, 0M)` (`cpicoupon.cpp:112-121, 168-172`), so under a linear observation the notional flow misses the coupons' base unless the base date starts its month. With neither a base CPI nor a base date it gives every flow the base date start - lag (`:286-288`), so from a month-end start the coupons miss `laggedFixing(start, lag)` too, since (start - lag) + lag loses the start's day (QuantLib's defect, R8). nanoql's leg gives every flow one base: with neither, `laggedFixing(start, lag, interpolation)`, QuantLib's own answer for the same leg built with that value as its explicit base CPI, which the wheel gives; with a base date, the coupons' `laggedFixing(baseDate + lag, lag, interpolation)`, the notional flow included, which QuantLib's `CPICashFlow` given that base fixing pays. On `InflationFixture`'s market, for a two-year annual leg of 1,000,000 at 2%, linear, lagged three months: from 15 May 2024 with neither base, the base is 310.30694493402456 and the notional flow pays 63,562.49459146323, where QuantLib's reads 310.34168149586054 and pays 63,443.44998172313; from 31 May, the base is 310.882579387307 and the coupons pay 20,637.587250557182 and 21,256.7148680739 and the notional flow 62,835.74340369502, where QuantLib's pay 20,642.36496136218, 21,261.63591020305 and 62,844.226063026195 on the base date 29 February 2024; from 15 May with the base date 15 March 2024, off the start less the lag, the base is 311.54246644409534 and the notional flow pays 59,344.59661315317, where QuantLib's reads 311.5223403181773 and pays 59,413.036336353194.

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

`CPICouponPricer`'s optionlet once its fixing date is past. QuantLib then pays `max(indexFixing - strike, 0)`, a CPI level against a strike on the index ratio (`cpicouponpricer.cpp:84-94`), while before it the forward is `indexRatio(accrualEnd)` (`:104`; QuantLib's defect, R8). nanoql's fixed branch reads the ratio, as the live one does. QuantLib's check is its `blackFormula` at zero deviation, the live branch's own formula at zero variance, on the coupon's `indexRatio`, which the wheel binds. On `InflationFixture`'s market, for a coupon of 1,000,000 at 3% on a base CPI of 301 from 1 October 2023 to 1 April 2024, lagged two months and fixed at 309.80326478740244, its ratio 1.0292467268684466: the caplet rate struck at 1.01 is 9.263797943622073 from QuantLib's pricer (the C++ harness) and 0.0005774018060533992 from `blackFormula` and nanoql; the floorlet rate struck at 1.05 is 0.0 from QuantLib's pricer and 0.0006225981939466019 from `blackFormula` and nanoql.

Facts:

- `AFixedOptionletPaysOnTheIndexRatio`

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

A cubic spline on 2 points with a not-a-knot end condition: QuantLib's rows for that condition read `dx_[1]` and `dx_[n_-3]` (`cubicinterpolation.hpp:428-431,460-463`), past the end of `dx_`, whose one element is the only interval, a result C++ leaves undefined. nanoql refuses it with `NanoQLException`, directly and through the log-cubic interpolation, which builds the same spline.

Facts:

- `ANotAKnotSplineOnTwoPointsIsRefused`
- `ACubicCurveWithANotAKnotEndBuildsThroughTheFirstPassFallback`

## CurveRecalculationTests

[`Divergences/CurveRecalculationTests.cs`](../tests/NanoQL.Tests/Divergences/CurveRecalculationTests.cs)

A bootstrapped curve is always built afresh, under the context it is given. QuantLib's piecewise curve is a lazy object that recalculates when a quote or the evaluation date changes, and its recalculation differs from a fresh bootstrap in three ways: it starts from its previous data (`iterativebootstrap.hpp:211-218, 253-255`; `globalbootstrap.hpp:340-347, 387-390, 409-411`), it re-runs `initialize()`, which takes the pillar dates from the helpers, only on first use or for a moving curve (`iterativebootstrap.hpp:224-231`), and a moving curve never re-runs `YieldTermStructure::update()`, which re-sets its jumps (`piecewiseyieldcurve.hpp:220-231`, `yieldtermstructure.cpp:174-189`). nanoql's curve is QuantLib's fresh curve in each case. The expected values are the wheel's.

Facts:

- `ACurveOnAChangedQuoteIsBootstrappedAfresh`
- `AGlobalCurveOnAChangedQuoteIsBootstrappedAfresh`
- `AFixedReferenceCurveTakesItsContextsPillars`
- `AMovingCurvesJumpsFollowItsReferenceDate`

## DateAdvanceOverflowTests

[`Divergences/DateAdvanceOverflowTests.cs`](../tests/NanoQL.Tests/Divergences/DateAdvanceOverflowTests.cs)

Advancing a date by a length whose product overflows `int`: QuantLib computes `7*n` weeks, `month + n` months and `year + n` years in `Integer` (`date.cpp:172-201`), which overflows for lengths near the type's range, undefined. nanoql computes in `long` and refuses the out-of-range result with its range messages, so no change was needed; this test pins it.

Facts:

- `AnOverflowingAdvanceIsRefusedByTheDateRange`

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

## DiscretizedAssetStateTests

[`Divergences/DiscretizedAssetStateTests.cs`](../tests/NanoQL.Tests/Divergences/DiscretizedAssetStateTests.cs)

A discretized asset used before it is initialized, and an American option with fewer than two exercise times. QuantLib's `DiscretizedAsset` leaves its lattice null until `initialize` and dereferences it in `rollback`, `partialRollback` and `presentValue` (`discretizedasset.hpp:38-40, 182-199`), and its `DiscretizedOption` reads `exerciseTimes_[0]` and `[1]` for an American exercise (`discretizedasset.cpp:35-37`), past the end of a shorter vector. nanoql refuses the first with `NanoQLException`, "asset not initialized", and the second, where QuantLib's read would go past the times, with `NanoQLException`, "American exercise needs two exercise times".

Facts:

- `AnUninitializedAssetIsRefused`
- `AnAmericanOptionReadPastItsExerciseTimesIsRefused`

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

## ExerciseDatesTests

[`Divergences/ExerciseDatesTests.cs`](../tests/NanoQL.Tests/Divergences/ExerciseDatesTests.cs)

An exercise's dates read past their end. QuantLib's `Exercise::date(i)` is `dates_[i]` (`exercise.hpp:45`), which reads past the vector for an index out of range, undefined, where its twin `dateAt(i)` (`:46`) throws `std::out_of_range`, a convention (`Conventions/ExerciseErrorTests`). nanoql refuses the index, with `ArgumentOutOfRangeException` naming `index`, as `Schedule.Date(int)` does. And a swaption on an exercise with no dates, which QuantLib's public `Exercise(type)` builds (`exercise.hpp:41`): QuantLib's swaption reads its last date in `isExpired` (`dates().back()`, `swaption.cpp:159`), and its first in the engines and in `impliedVolatility` (`date(0)`, `blackswaptionengine.hpp:227`, `swaption.cpp:198`), undefined for an empty vector. nanoql's swaption builds on it and refuses where the date is read, with `NanoQLException` and QuantLib's text for the same input, `lastDate()`'s "no exercise date given" (`exercise.cpp:29`).

Facts:

- `AnIndexOutOfRangeIsRefused`
- `ASwaptionOnAnExerciseWithNoDatesIsRefusedWhereItsDateIsRead`

## ExpiredCallableBondSpreadTests

[`Divergences/ExpiredCallableBondSpreadTests.cs`](../tests/NanoQL.Tests/Divergences/ExpiredCallableBondSpreadTests.cs)

The option-adjusted spread family on an expired callable bond. QuantLib's `NPVSpreadHelper` sets the bond's arguments up and calls its engine's `calculate()` directly (`callablebond.cpp:262-277`), which skips `Instrument::calculate`'s expiry check (`instrument.hpp:130-139`), so the family prices an expired bond's arguments. On a bond called at 100 in September 2019 and maturing on 9 June 2025, evaluated on 16 September 2030, with Hull-White on a flat 3% curve dated 2 January 2020 and 40 steps, the wheel gives an OAS of inf for a clean price of 100, a clean price of inf for a spread of 0.01, and an effective duration and convexity of NaN. nanoql's family takes the engine as its `Calculate`, whose first step is the expiry check, so it would read the expired results, an NPV of 0: the OAS would be the solver's guess, 0, a value a caller can't tell from an answer. nanoql refuses the family on an expired bond instead, with `NanoQLException` and the text of QuantLib's own refusal of an expired bond's implied volatility (`callablebond.cpp:119`).

Facts:

- `AnExpiredBondsSpreadFamilyIsRefused`

## ExtrapolatorCopyTests

[`Divergences/ExtrapolatorCopyTests.cs`](../tests/NanoQL.Tests/Divergences/ExtrapolatorCopyTests.cs)

QuantLib's `enableExtrapolation` and `disableExtrapolation` set a flag on the object, for every holder of it. In nanoql they return a modified copy, of the receiver's own type, and leave the receiver unchanged.

Facts:

- `TheCopysFlagChangesAndTheReceiversDoesNot`
- `TheCopyKeepsItsTypeAndItsValues`
- `ANullReceiverThrows`

## FlatExtrapolator2DNullTests

[`Divergences/FlatExtrapolator2DNullTests.cs`](../tests/NanoQL.Tests/Divergences/FlatExtrapolator2DNullTests.cs)

A flat extrapolator decorating no interpolation. QuantLib's constructor calls the decorated interpolation's `update()` through the pointer it is given (`flatextrapolation2d.hpp:39-48, 62-64`), a null dereference, undefined under `std::shared_ptr` and a Boost assertion by default. nanoql refuses it with `ArgumentNullException` naming `decoratedInterpolation`.

Facts:

- `ANullDecoratedInterpolationIsRefused`

## FloatingLegUndefinedInputsTests

[`Divergences/FloatingLegUndefinedInputsTests.cs`](../tests/NanoQL.Tests/Divergences/FloatingLegUndefinedInputsTests.cs)

The floating leg builder given a null index: QuantLib's `FloatingLeg` reads `index->fixingDays()` as a default argument (`cashflowvectors.hpp:156, 169, 188, 204`), a null pointer's dereference, which is undefined. nanoql refuses it with `ArgumentNullException` naming `index`. The empty schedule, which QuantLib refuses with `std::length_error`, is a convention (`Conventions/EmptyScheduleTests`).

Facts:

- `ANullIndexIsRefused`

## ForwardFlatPrimitiveTests

[`Divergences/ForwardFlatPrimitiveTests.cs`](../tests/NanoQL.Tests/Divergences/ForwardFlatPrimitiveTests.cs)

The forward-flat interpolation's primitive past its last node. QuantLib's `primitive` beyond `x[n-1]` adds `(x - x[n-2])·y[n-2]` (`forwardflatinterpolation.hpp:93-97`), while its value there is `y[n-1]` (`:87-88`), so the primitive's slope isn't the value (QuantLib's defect, R8). nanoql's primitive beyond `x[n-1]` is `primitive[n-1] + (x - x[n-1])·y[n-1]`, and QuantLib's expression at and before it. On x = [0, 1, 2, 3] and y = [0.01, 0.02, 0.03, 0.05], at 4 and 5 it is 0.11 and 0.16, where QuantLib's is 0.09 and 0.12: QuantLib's own forward-flat forward curve integrates there as nanoql does, its `-ln D(t)` on the wheel 0.11000000000000003 and 0.16 (`forwardcurve.hpp:155-168`). At 2.5 and 3 both give 0.045 and 0.06.

Facts:

- `ThePrimitivePastTheLastNodeGrowsAtTheValueThere`

## FuturesNullMaturityTests

[`Divergences/FuturesNullMaturityTests.cs`](../tests/NanoQL.Tests/Divergences/FuturesNullMaturityTests.cs)

A custom futures helper built from dates with no end date keeps a null maturity, as QuantLib's does (`ratehelpers.cpp:126-128`), and the bootstraps drop it as expired; but its implied quote refuses the null maturity before it reads the curve, with the term structure's text for a date before its reference date. QuantLib computes a year fraction from the null date (`:133`), which a day counter reading the month or the day reads out of bounds, and its implied quote discounts to the null date (`:159-160`), which a curve whose day counter reads serial numbers alone refuses as a "negative time" and one reading the month or the day reads out of bounds: a result C++ leaves undefined, and a text nanoql can't form from a null date.

Facts:

- `ACustomFuturesHelperWithNoEndDateRefusesItsImpliedQuote`

## GridEngineSpreadTests

[`Divergences/GridEngineSpreadTests.cs`](../tests/NanoQL.Tests/Divergences/GridEngineSpreadTests.cs)

The tree engine's spread on a lattice reused across calls. QuantLib's time-grid engine builds its tree once and keeps it (`latticeshortratemodelengine.hpp:80-95`), and sets a spread on it only when the spread isn't 0 (`treecallablebondengine.cpp:69-74`), so the solver's last trial spread stays on the tree: on testCached's callable bond with an 80-step grid through its mandatory times, after a clean price at a spread, the wheel's OAS for a clean price of 104 is 0.0, where its time-steps engine gives 0.011561251367301573, and its NPV after the OAS is 11205.506593769049, against 11205.506591750021 before. nanoql's lattices are immutable and its engines hold nothing: the lattice overload, given one tree for every call, and the grid overload price at the spread each call is given, so the grid engine's OAS is the time-steps engine's, and the NPV after it is the NPV before.

Facts:

- `ASharedLatticeKeepsNoSpreadAcrossCalls`

## IborNegativeCountTests

[`Divergences/IborNegativeCountTests.cs`](../tests/NanoQL.Tests/Divergences/IborNegativeCountTests.cs)

The Ibor coupons' fixing days are QuantLib's `Natural`, an unsigned type, which can't hold a negative value; nanoql takes an `int` (or `Nullable{T}` for QuantLib's `Null<Natural>()`) and refuses a negative one on entry. From C++ a negative wraps to a large unsigned value, which QuantLib negates back where it advances by it (`floatingratecoupon.cpp:84-85`, `couponpricer.cpp:71-72`, `stubiborcoupon.cpp:214-215`): the wheel's coupon with -1 fixing days looks a day forward for its fixing, and the wrap of 2^31 negates `INT_MIN`, which is undefined.

Facts:

- `NegativeFixingDaysAreRefused`

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

## InitialGuessSizeTests

[`Divergences/InitialGuessSizeTests.cs`](../tests/NanoQL.Tests/Divergences/InitialGuessSizeTests.cs)

A global bootstrap's initial-guess function that gives fewer values than the curve has nodes after the first is refused when the curve is built, with `NanoQLException` "initial guess has n values but the curve has m pillars", QuantLib-Python's wording. QuantLib's optimizer starts from the guess, and the bootstrap writes each node from the optimizer's argument, whose size is the guess's (`globalbootstrap.hpp:394, 442-448`), reading past its end, a result C++ leaves undefined; the wheel's guess function refuses a guess of any other size than the nodes'. A longer guess is QuantLib's defined path where the traits' transforms don't read a node's time (the discount traits', and the zero-yield and forward-rate traits', which have none), kept: its extra values are variables no error term reads. The simple-zero traits' transforms read each node's time (`bootstraptraits.hpp:408-416`), and for a longer guess QuantLib reads past its times, which nanoql refuses, `NanoQLException` "initial guess longer than the curve's m pillars: the simple-zero transform reads node k".

Facts:

- `AGuessShorterThanTheCurvesPillarsIsRefused`
- `ALongerGuessUnderTheSimpleZeroTraitsIsRefused`

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

## IsdaImpliedWeekendTests

[`Divergences/IsdaImpliedWeekendTests.cs`](../tests/NanoQL.Tests/Divergences/IsdaImpliedWeekendTests.cs)

The ISDA model's implied hazard rate and conventional spread on a weekend. QuantLib's flat curve is `FlatHazardRate(0, WeekendsOnly(), …)` (`creditdefaultswap.cpp:352, 393`), which rolls a weekend to Monday, and its own ISDA engine then refuses it: on Saturday 15 March 2025, for `MakeCreditDefaultSwap(5Y, 1%)` on a discount curve flat at 3%, Actual/365 (Fixed), at a recovery of 40%, both throw "probability term structure reference date (March 17th, 2025 should be evaluation date (March 15th, 2025)" (QuantLib's defect, R8). nanoql's ISDA model dates its curve on the evaluation date. QuantLib's ISDA engine on QuantLib's `FlatHazardRate` dated there, solved by QuantLib's `Brent` on the wheel, gives the hazard rate 0.016833960395149727 for a zero target, and the fair spread at the conventional root, 0.009999999999972463; on the Friday before, the same oracle gives QuantLib's own 0.01683399613622196 and 0.009999999999972453. The mid-point model keeps QuantLib's curve and its weekend answers, 0.016853086104458855 and 0.009999999999985819.

Facts:

- `TheImpliedHazardRateIsSolvedOnTheEvaluationDate`
- `TheConventionalSpreadIsTheFairSpreadAtTheRoot`

## IsdaProtectionLegMaturityTests

[`Divergences/IsdaProtectionLegMaturityTests.cs`](../tests/NanoQL.Tests/Divergences/IsdaProtectionLegMaturityTests.cs)

The ISDA engine's protection leg on curves that end before the maturity. QuantLib integrates over the union of the two curves' nodes, adds the maturity only when the union is empty (`isdacdsengine.cpp:150-156`), and stops at the maturity only on meeting a node past it (`:167-172`), so it drops the protection from the curves' last node to the maturity (QuantLib's defect, R8). nanoql adds the maturity to the nodes when their last one is before it. On 15 March 2024, a 10-year CDS buying protection on 10,000,000 at 1% from the next day (quarterly on WeekendsOnly under the CDS rule, Actual/360 with the last period inclusive) on a hazard rate flat at 2% and a discount curve continuously flat at 3%, Actual/365 (Fixed): on a log-linear discount curve with yearly nodes to 5 years, nanoql's default leg is 945,721.5990409853, QuantLib's own on the flat curve and on nodes to 15 years, where QuantLib's on nodes to 5 years is 531,134.1471852273; the coupon leg is -820,172.4119002547 on every curve.

Facts:

- `TheProtectionRunsToTheMaturity`

## LaggedYoYRateBranchTests

[`Divergences/LaggedYoYRateBranchTests.cs`](../tests/NanoQL.Tests/Divergences/LaggedYoYRateBranchTests.cs)

`CPI.LaggedYoYRate` on a ratio index under linear interpolation. QuantLib interpolates the underlying's fixings and takes their ratio only when `!index->needsForecast(date)`, tested on the unlagged date (`inflationindex.cpp:83`), though the fixings that branch reads are those of the lagged date's period and the next; otherwise it interpolates the ratios. So a fixing it never reads changes the rate it returns: with UK RPI fixings to June 2025 and the evaluation date 20 August 2025, the rate for 15 July 2025 with a three-month lag is 0.035461466318187446 on the wheel, and 0.03545866305332557 once July's fixing is stored, a change of -2.8032648618780964e-06 (QuantLib's defect, R8). nanoql asks whether the latest fixing the ratio branch reads needs a forecast (the next period's first day, or the lagged period's own when the date starts its period), so the rate is 0.03545866305332557 with or without July's fixing: QuantLib's own answer with that fixing added, and its interpolate-then-divide value from the same April and May fixings. The other face: where the lag is shorter than the index's period, the unlagged date's fixing can be published while the next period's, which the ratio branch reads, isn't. QuantLib then takes the ratio branch and forecasts the underlying index, refusing without its zero curve, against its own comment that "forecasts need to be done through the YoY forecast curve, and not the underlying index" (`:84-90`): with UK RPI fixings to April 2024, the evaluation date 15 June 2024 and a YoY curve flat at 2%, the rate for 26 April 2024 with no lag throws "empty Handle cannot be dereferenced" on the wheel. nanoql interpolates the ratios through the YoY curve, 0.02218077895395275; on 1 April, which starts its period, both give 0.033084673723716485.

Facts:

- `AFixingTheRatioBranchDoesntReadChangesNothing`
- `ALagShorterThanThePeriodForecastsThroughTheYoYCurve`

## LocalVolCurveRefusalTests

[`Divergences/LocalVolCurveRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/LocalVolCurveRefusalTests.cs)

`LocalVolCurve` on a Black variance that falls in time. QuantLib returns the square root of the negative derivative, NaN (`localvolcurve.hpp:88-95`): a curve of 30% at six months and 10% at a year, built without the forced monotone variance, gives a process diffusion of NaN at t = 0.8. nanoql refuses with `NanoQLException`, "negative local vol^2 at time …; the black variance curve is not smooth enough", as QuantLib's `LocalVolSurface` refuses the same condition (`localvolsurface.cpp:148-151`).

Facts:

- `AFallingVarianceIsRefused`

## MakeSwaptionNullIndexTests

[`Divergences/MakeSwaptionNullIndexTests.cs`](../tests/NanoQL.Tests/Divergences/MakeSwaptionNullIndexTests.cs)

A swaption builder given no swap index is refused at construction, with `ArgumentNullException` naming `swapIndex`, as nanoql's swap builders refuse theirs. QuantLib's `MakeSwaption` takes it unchecked and dereferences it at the conversion, for its fixing calendar, its forwarding curve and its conventions (`makeswaption.cpp:57-58, 82, 112`): results C++ leaves undefined, where the wheel stops on Boost's assertion.

Facts:

- `ABuilderWithoutASwapIndexIsRefused`

## MinimizeContinuationTests

[`Divergences/MinimizeContinuationTests.cs`](../tests/NanoQL.Tests/Divergences/MinimizeContinuationTests.cs)

QuantLib's `minimize` changes the `Problem` it is given, so a second `minimize` on the same problem starts from the first's optimum, its current value then. nanoql's `Problem` is immutable, and `OptimizationMethod.Minimize(Problem, EndCriteria)` starts from the problem's initial value each time (S2R11). `OptimizationMethod.Minimize(ProblemState, EndCriteria)` is QuantLib's: a second minimization of one state continues from the first's optimum, as QuantLib's `HybridSimulatedAnnealing` runs its local optimizer on the same problem.

Facts:

- `MinimizeProblemStartsFromTheInitialValueAndMinimizeStateContinues`

## MonteCarloUndefinedInputsTests

[`Divergences/MonteCarloUndefinedInputsTests.cs`](../tests/NanoQL.Tests/Divergences/MonteCarloUndefinedInputsTests.cs)

Inputs for which QuantLib's time grid, path, Brownian bridge and path generator are undefined (they read or write outside a vector, or call std::sort or std::lround outside their contracts) are refused explicitly in nanoql: an `ArgumentOutOfRangeException` naming the caller's argument, or a `NanoQLException` where the object itself is empty.

Facts:

- `ABrownianBridgeOfNoStepOrAShortOutputIsRefused`
- `ATimeGridsUndefinedInputsAreRefused`
- `AnEmptyPathsEndsAreRefused`
- `APathGeneratorOfNoStepIsRefused`

## NegativeCountTests

[`Divergences/NegativeCountTests.cs`](../tests/NanoQL.Tests/Divergences/NegativeCountTests.cs)

QuantLib's fixing and settlement days are `Natural`, an unsigned type, which can't hold a negative value; nanoql takes an `int` and refuses a negative one on entry. From C++ a negative wraps to a large unsigned value, which QuantLib negates back where it advances by it (`interestrateindex.hpp:43, 106`, `termstructure.cpp:42`), and the wrap of 2^31 negates `INT_MIN`, which is undefined. The cash-flow solvers' maximum numbers of iterations are `Size`, unsigned too (`cashflows.hpp:273, 436`), where a negative wraps to a count no solve reaches; nanoql refuses it on entry likewise. The bonds' settlement days and a floating-rate bond's fixing days are `Natural` (`bond.hpp:89, 99`, `floatingratebond.hpp:44, 50`), which the bond advances by in `bond.cpp:167`, converting it to an `Integer`; their yields' and Z-spread's maximum evaluations are `Size` (`bond.hpp:180, 212`, `bondfunctions.hpp:165, 279`).

Facts:

- `NegativeFixingAndSettlementDaysAreRefused`
- `NegativeSettlementDaysOfAVolatilityStructureAreRefused`
- `NegativeMaximumIterationsOfTheCashFlowSolversAreRefused`

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

A stripper's implied volatilities on a second strip. QuantLib's `OptionletStripper1` keeps its standard deviations between calculations and passes them as the next calculation's guesses (`optionletstripper1.cpp:58-59, 150-153`), so a recalculated stripper warm-starts: on 16 June 2025, a 4 × 4 surface (1Y, 2Y, 3Y, 5Y by 2%, 3%, 4%, 5%) on a flat 4% curve and Euribor 6M, a quote moved and moved back, the wheel's re-strip differs from its first in 33 of 36 volatilities, by up to 6.8449e-7: the first optionlet at 2% is 0.30000093033375125 on the first strip and 0.3000002879738929 on the second. nanoql strips once, at construction, from QuantLib's first guess, 0.14 (S7R4): every stripper on those quotes gives QuantLib's first strip, which a fresh QuantLib stripper gives too.

Facts:

- `EveryStripGivesQuantLibsFirstStrip`

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

## PathGeneratorProcessTests

[`Divergences/PathGeneratorProcessTests.cs`](../tests/NanoQL.Tests/Divergences/PathGeneratorProcessTests.cs)

QuantLib's `PathGenerator` takes any `StochasticProcess` and casts it to `StochasticProcess1D` (`ext::dynamic_pointer_cast` in `pathgenerator.hpp`): a process that isn't 1-D casts to null, which QuantLib keeps, and its first draw dereferences it (`process_->x0()`). nanoql refuses such a process when the generator is built, in both constructors, with an `ArgumentException` naming the process.

Facts:

- `AProcessThatIsNotOneDimensionalIsRefused`

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

## QuantoTermStructureDayCounterTests

[`Divergences/QuantoTermStructureDayCounterTests.cs`](../tests/NanoQL.Tests/Divergences/QuantoTermStructureDayCounterTests.cs)

`QuantoTermStructure` on structures with different day counters. QuantLib reads every structure at the time the dividend curve's day counter measures, with its own comment that this assumes one day count and "It should be QL_REQUIREd" (`quantotermstructure.hpp:124-131`): two risk-free curves equal at every date, one on Actual/365 (Fixed) and one on Actual/360, give quanto zero rates of 5.0000% and 4.9589%. nanoql makes the check QuantLib names and refuses with `ArgumentException`, "all term structures must share one day counter", naming the first structure that differs; QuantLib's case (`testFDMQuantoHelper`, Actual/360 throughout) is unchanged.

Facts:

- `StructuresOnDifferentDayCountersAreRefused`

## QuoteSentinelTests

[`Divergences/QuoteSentinelTests.cs`](../tests/NanoQL.Tests/Divergences/QuoteSentinelTests.cs)

A quote is a `double`, so a quote equal to QuantLib's null sentinel, `Null<Real>()` (3.4028234663852886e38, the largest `float`), is a value. QuantLib's quotes are `SimpleQuote`s, and a `SimpleQuote` holding the sentinel is invalid: a bootstrap refuses a helper quoting it ("has an invalid quote", `iterativebootstrap.hpp:236-240`), and a curve refuses such a jump ("invalid 1st jump quote", `yieldtermstructure.cpp:86-87`). nanoql has no invalid quote: a helper quoting it is bootstrapped, which fails in the solver as any unreachable quote does, and such a jump is applied.

Facts:

- `AQuoteEqualToQuantLibsNullIsAValue`

## RateHelperNullIndexTests

[`Divergences/RateHelperNullIndexTests.cs`](../tests/NanoQL.Tests/Divergences/RateHelperNullIndexTests.cs)

A rate helper given a null index is refused on entry, naming the parameter. QuantLib's helpers dereference their index before any check (`ratehelpers.cpp:198, 206` for the deposit helper, `:533` for the swap helper, `:148` for the futures helper, `:312, 332, 350` for the FRA helper, `:469` for the swap helper on a swap index, `:688, 694` for the BMA swap helper, `oisratehelper.cpp:114` for the OIS helper), a result C++ leaves undefined; the wheel stops on Boost's assertion.

Facts:

- `AHelperGivenANullIndexIsRefused`
- `AFuturesFraOrOisHelperGivenANullIndexIsRefused`

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

## SabrSmileSectionParameterTests

[`Divergences/SabrSmileSectionParameterTests.cs`](../tests/NanoQL.Tests/Divergences/SabrSmileSectionParameterTests.cs)

A SABR smile section given fewer than four parameters. QuantLib reads α, β, ν and ρ from the vector unchecked (`sabrsmilesection.cpp:52-55`): with three, the wheel's ρ is −2.654651770048e-310, memory past the vector; with none, the process ends with an access violation. nanoql refuses fewer than four with `ArgumentException`, in the text of QuantLib's no-arbitrage SABR section's check (`noarbsabrsmilesection.cpp:46-48`).

Facts:

- `FewerThanFourParametersAreRefused`

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

## SeedZeroTests

[`Divergences/SeedZeroTests.cs`](../tests/NanoQL.Tests/Divergences/SeedZeroTests.cs)

QuantLib reads a seed of 0 as "no seed" and draws one from its `SeedGenerator` singleton, itself seeded from the clock; nanoql's seeds are explicit and no library code reads the clock, so every generator refuses 0 with `ArgumentOutOfRangeException`. QuantLib's `testSeedGenerator`, which only draws from the singleton, becomes this test. Two of QuantLib's seeds of 0 are deterministic, and nanoql refuses them too, one rule for every generator: `SobolRsg` reads its seed only for the dimensions beyond its set's table of direction integers, so `SobolRsg(32, 0)` draws what `SobolRsg(32, 1)` does; and `HaltonRsg` without a random start or shift never reads it.

Facts:

- `EveryGeneratorRefusesSeedZero`

## SetSeasonalityColdStartTests

[`Divergences/SetSeasonalityColdStartTests.cs`](../tests/NanoQL.Tests/Divergences/SetSeasonalityColdStartTests.cs)

`InflationTermStructureExtensions.SetSeasonality` on a piecewise zero-inflation curve. QuantLib's `setSeasonality` marks the curve for recalculation, and its bootstrap restarts from the nodes it last found, so the curve it gives differs from one built with the seasonality from the start: on the wheel, a curve on nine UK RPI zero-coupon swaps from 2.9% to 3.25%, bootstrapped to 1e-14, differs from the fresh one by up to 7.9e-15 at its nodes, and its five-year zero rate is 0.030262300008121912 against the fresh curve's 0.0302623000081228 (the planner's `seasonality_rebuild.py`). nanoql's copy is bootstrapped from a cold start, as S5R1's curves are, so it is the curve built with the seasonality, bit for bit.

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

## SimplexEmptyInitialValueTests

[`Divergences/SimplexEmptyInitialValueTests.cs`](../tests/NanoQL.Tests/Divergences/SimplexEmptyInitialValueTests.cs)

Simplex on a problem with no variables: QuantLib sizes its function values at n + 1 = 1 and then reads the second (`simplex.cpp:99-106,119`), a result C++ leaves undefined. nanoql refuses it with `NanoQLException`, as Levenberg–Marquardt refuses it with QuantLib's own "no variables given".

Facts:

- `ASimplexWithNoVariablesIsRefused`

## SinkingNotionalsTests

[`Divergences/SinkingNotionalsTests.cs`](../tests/NanoQL.Tests/Divergences/SinkingNotionalsTests.cs)

A French amortization's notionals for a bond length that isn't positive, or at a frequency of no period. QuantLib's `sinkingNotionals` finds no periods in a zero length, or minus one in a length of minus one period, then writes past the end of a one-element vector or reads the first element of an empty one (`amortizingfixedratebond.cpp:131-147`), which is undefined: the wheel ends the process for 0Y, 0M and -1Y; below minus one period the vector's size wraps, and it throws "vector too long". At `Frequency.Once` or `Frequency.NoFrequency` the period has no days, and the time ratio divides by zero and converts the infinite ratio to an integer (`amortizingfixedratebond.cpp:99-104`), which is undefined: MSVC's smallest integer matches no positive length, so the wheel refuses "Bond frequency is incompatible with the maturity tenor". nanoql refuses a length that isn't positive, before its periods are counted, and a frequency of no period, with that text, each with `ArgumentException` naming its parameter.

Facts:

- `ABondLengthThatIsNotPositiveIsRefused`
- `AFrequencyOfNoPeriodIsRefused`

## SmileSectionFieldsTests

[`Divergences/SmileSectionFieldsTests.cs`](../tests/NanoQL.Tests/Divergences/SmileSectionFieldsTests.cs)

A smile section built by the parameterless constructor. QuantLib's `SmileSection() = default` (`smilesection.hpp:50`) leaves the exercise time, the volatility type and the shift uninitialized (`smilesection.hpp:86-92`), and the base's inspectors, its variance and its converted volatility read them (`smilesection.hpp:60-67, 112-115`; `smilesection.cpp:133`), indeterminate unless the section overrides them all, as QuantLib's own users of the constructor do. nanoql's parameterless constructor defines them: an exercise time of 0, `VolatilityType.ShiftedLognormal` and a shift of 0, so a section that keeps the base's members reads those values.

Facts:

- `AParameterlessSectionReadsDefinedFields`

## SobolPeriodEndTests

[`Divergences/SobolPeriodEndTests.cs`](../tests/NanoQL.Tests/Divergences/SobolPeriodEndTests.cs)

The Sobol generator's period end with the Gray code, where QuantLib's behaviour is undefined. Its `skipTo(4294967295)` computes `skip + 1`, which wraps to 0, and converts `log(0)`'s −∞ to an `unsigned int`, which C++ leaves undefined; its draw that takes the counter to 4294967295 finds the rightmost zero bit at 32 and reads row 32 of its 32 rows of direction integers (the wheel crashes there). nanoql refuses that skip with `ArgumentOutOfRangeException`, and throws "period exceeded" on that draw, one draw before QuantLib's own check. Without the Gray code QuantLib's end is defined, and nanoql keeps it (the harness's vectors, `cxx/math/randomnumbers/sobolrsg`).

Facts:

- `SkipToTheLastIndexIsRefusedWithTheGrayCode`
- `TheDrawThatWouldReachTheLastCounterThrowsWithTheGrayCode`

## SpreadedSmileSectionTypeTests

[`Divergences/SpreadedSmileSectionTypeTests.cs`](../tests/NanoQL.Tests/Divergences/SpreadedSmileSectionTypeTests.cs)

The converted volatility of a spreaded smile section. QuantLib's `SpreadedSmileSection` default-constructs its `SmileSection` base (`spreadedsmilesection.cpp:26-31`), so the base's `volatility(strike, type, shift)` compares the type asked for with an uninitialized member (`smilesection.cpp:131-136`), and the section returns its own volatility or one converted through option prices by chance, observed both ways. nanoql's spreaded section gives its base the underlying section's exercise time, day counter, volatility type and shift, so the comparison reads the underlying's type: asked for its own type and shift, it returns its own volatility.

Facts:

- `TheConvertedVolatilityComparesTheUnderlyingsType`

## StatePriceCacheTests

[`Divergences/StatePriceCacheTests.cs`](../tests/NanoQL.Tests/Divergences/StatePriceCacheTests.cs)

A tree's state prices read past the rows its construction stored. QuantLib's `TreeLattice::statePrices` keeps every row it computes in a `mutable` cache (`lattice.hpp:87, 112-117`), and `setSpread` changes only the tree's spread (`onefactormodel.hpp:105-108`), so a row read before the spread is set keeps its unspread values afterwards, and a row first read after it carries the spread. nanoql's lattices keep no row beyond those their construction stored (PR6.9), and `SetSpread` returns a copy: the copy's later rows carry the spread whatever was read before, so reading a row changes nothing.

Facts:

- `ARowReadBeforeTheSpreadIsSetDoesntKeepItsUnspreadValues`

## StochasticProcessDiscretizationRefusalTests

[`Divergences/StochasticProcessDiscretizationRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/StochasticProcessDiscretizationRefusalTests.cs)

`StochasticProcess`'s and `StochasticProcess1D`'s defaults on a process built without a discretization. QuantLib's expectation, standard deviation, covariance and variance call the discretization through a pointer (`stochasticprocess.cpp:36-52`, `:78-88`), which is null when the process was built without one, a result C++ leaves undefined. nanoql refuses with `NanoQLException`, "no discretization given".

Facts:

- `TheDefaultsRefuseWithoutADiscretization`

## StrippedOptionletAdapterEmptyStrikesTests

[`Divergences/StrippedOptionletAdapterEmptyStrikesTests.cs`](../tests/NanoQL.Tests/Divergences/StrippedOptionletAdapterEmptyStrikesTests.cs)

An adapter on a stripped optionlet with no strikes. No check of QuantLib's refuses empty strikes (`strippedoptionlet.cpp:84-115`), and its adapter, which builds its strike interpolations at its first calculation, builds. That calculation refuses them: its linear interpolation over the first date's strikes (`strippedoptionletadapter.cpp:109`) throws "not enough points to interpolate: at least 2 required, 0 provided" (`interpolation.hpp:83-86`), the wheel's text at its first extrapolated volatility. But its `minStrike()` and `maxStrike()` read the front and back of the empty strikes before any calculation (`strippedoptionletadapter.cpp:128, 132`), which a volatility without extrapolation reaches through its strike check: the wheel's process ends with an access violation, 0xC0000005, in 3 of 3 runs. nanoql's adapter does QuantLib's first calculation when it is built (S7R4), so it refuses the empty strikes there, with QuantLib's text, as an `ArgumentException` naming `stripper`, before the reads; and a base with no fixing dates, which builds no interpolation, is refused by `MinStrike` and `MaxStrike` with `NanoQLException`, "no strikes given" (S7D16).

Facts:

- `EmptyStrikesAreRefused`

## StrippedOptionletStrikesTextTests

[`Divergences/StrippedOptionletStrikesTextTests.cs`](../tests/NanoQL.Tests/Divergences/StrippedOptionletStrikesTextTests.cs)

The refusal of a stripped optionlet's non-increasing strikes at a fixing date after the first. QuantLib checks each date's strikes but prints the first date's (`strippedoptionlet.cpp:106-113`), and reads past them where the first date has fewer: strikes 2%, 3%, 4% at the first date and 2%, 3.5%, 3% at the second give QuantLib's "non increasing strikes at date 1: 2nd is 3.000000 %, 3rd is 4.000000 %", the first date's. nanoql prints the offending date's strikes (S7D16), "non increasing strikes at date 1: 2nd is 3.500000 %, 3rd is 3.000000 %", and a shorter first date reads nothing past its end.

Facts:

- `TheTextPrintsTheOffendingDatesStrikes`

## StrippedOptionletTimesTests

[`Divergences/StrippedOptionletTimesTests.cs`](../tests/NanoQL.Tests/Divergences/StrippedOptionletTimesTests.cs)

A stripped optionlet's fixing times after the evaluation date moves. QuantLib's `StrippedOptionlet` computes its times from the evaluation date at construction and never again (`strippedoptionlet.cpp:77-81`), while its adapter's reference date moves with the evaluation date: on 16 June 2025, with two settlement days, fixing dates 16 December 2025, 16 June 2026 and 16 December 2026, volatilities 20%/19%, 22%/21% and 25%/24% at 2% and 3%, the wheel's times are 0.4958904109589041, 0.9945205479452055 and 1.4958904109589042 and its adapter gives 0.22 at the second date and 2%; with the evaluation date moved to 16 September the times stay, the adapter's reference date is 18 September, and it gives 0.2098901098901099 there; a stripped optionlet built on 16 September has the times 0.24383561643835616, 0.7424657534246575 and 1.2438356164383562 and gives 0.22. nanoql's objects have fixed dates (R1): an adapter is built under a context. The stripped optionlet's times are frozen as QuantLib's are, so its adapter under the later context gives QuantLib's moved value, and the optionlet and adapter built under that context QuantLib's fresh one.

Facts:

- `TheTimesAreTheConstructionsAndTheAdapterIsTheContexts`

## SwapIndexNullIndexTests

[`Divergences/SwapIndexNullIndexTests.cs`](../tests/NanoQL.Tests/Divergences/SwapIndexNullIndexTests.cs)

A swap index given a null Ibor index, or an overnight-indexed swap index given a null overnight index, is refused on entry, naming the parameter. QuantLib's `SwapIndex` accepts a null Ibor index (`swapindex.cpp:38-43`) and dereferences it later, in `forwardingTermStructure()`, `underlyingSwap()` and the clones (`:65, 85, 95, 125, 136, 150`), and `OvernightIndexedSwapIndex` dereferences its overnight index in its base's initializer (`:193, 196`): results C++ leaves undefined, where the wheel stops on Boost's assertion.

Facts:

- `ASwapIndexGivenANullIborIndexIsRefused`
- `AnOvernightIndexedSwapIndexGivenANullOvernightIndexIsRefused`

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

## TelescopicValueDatesTests

[`Divergences/TelescopicValueDatesTests.cs`](../tests/NanoQL.Tests/Divergences/TelescopicValueDatesTests.cs)

A coupon built with `telescopicValueDates`. QuantLib builds its daily value dates only to 7 business days after the evaluation date it is built with, then jumps to the lockout or end dates (`overnightindexedcoupon.cpp:107-132`), and wherever the skipped dates are read its results are wrong, with no error. nanoql builds every daily value date whatever the flag says (the flag keeps its check and, on an averaged coupon, its choice of the averaging pricer's approximation), so a telescopic coupon gives QuantLib's full-schedule result: here, each expected value is QuantLib's for the same coupon built without the flag, the approximating pricer set on an averaged one, as the flag sets it.

Facts:

- `ATelescopicCouponGivesTheFullSchedulesResults`
- `ATelescopicCouponListsEveryDailyDate`
- `ALockoutLegsNPVIsTheFullSchedules`

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

## TreeSwaptionEngineExerciseTimeTests

[`Divergences/TreeSwaptionEngineExerciseTimeTests.cs`](../tests/NanoQL.Tests/Divergences/TreeSwaptionEngineExerciseTimeTests.cs)

A swaption with no exercise time on or after the reference date, priced on a lattice that initializes an asset at a negative time. QuantLib's tree engine initializes the swaption at its last exercise time and rolls it back to the first time not before the reference date, read through `*std::find_if` (`treeswaptionengine.cpp:88-93`), which reads past the times when none is: undefined. Its own lattices refuse the negative time first, at the initialization ("using inadequate time grid"), but a lattice of one's own may not, as a short-rate model of one's own may build. nanoql refuses it there, with `NanoQLException`, "no exercise time on or after the reference date".

Facts:

- `NoExerciseTimeOnOrAfterTheReferenceDateIsRefused`

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

## WeightedIndexNullComponentTests

[`Divergences/WeightedIndexNullComponentTests.cs`](../tests/NanoQL.Tests/Divergences/WeightedIndexNullComponentTests.cs)

A weighted index with a null component. QuantLib's `StubIborCoupon::WeightedIndex` reads every component's tenor, and the first one's conventions, in its base initializer (`stubiborcoupon.cpp:37-56`), before its own check, "null component index" (`:64`), so a null component is dereferenced. nanoql refuses it first, with `ArgumentException` and QuantLib's text, wherever it stands.

Facts:

- `ANullComponentIsRefusedBeforeItIsRead`

## XabrTransformEdgeTests

[`Divergences/XabrTransformEdgeTests.cs`](../tests/NanoQL.Tests/Divergences/XabrTransformEdgeTests.cs)

XABR's parameter transforms at their edges. QuantLib's SABR inverse transform doesn't clamp its arguments (`sabrinterpolation.hpp:103-114`): a ρ beyond 0.9999 takes `asin` of a value above 1, a NaN that the direct transform turns into −0.9999, so a fixed ρ of 0.99995 comes back as −0.9999, its sign flipped, and a free guess of 0.99995 fails the fit with "alpha must be positive: nan not allowed"; an α or ν below 1e-7, ν = 0 included, takes the square root of a negative number, a NaN the fit refuses ("alpha must be positive: nan not allowed", "nu must be non negative: nan not allowed"). nanoql's inverse clamps each argument to its direct transform's range, so such an input comes back as the nearest value the transform reaches, bit for bit: ρ = 0.9999, ν = 1e-7, and the fixed β of 0.6. The fits from those values are the wheel's fits from them. Two of them are well posed and agree within the vectors' tolerance; the three that pin a parameter at an edge can't reproduce the smile, so their optimum is flat: when the input volatilities move by one ulp, their free parameters move by up to 2.4e-3 in S7R6's measure, at the vectors' scale of 1, which is up to 24% of the small ones' own values. Those are asserted within 1e-2 in that measure. ZABR's inverse (`zabrinterpolation.hpp:78-88`) doesn't clamp either: α, β and ρ invert as SABR's do, so a ρ above 0.9999 takes `asin` of a value beyond 1, a NaN its direct transform turns into −0.9999, and an α below 1e-7 the square root of a negative number, a NaN the model then refuses ("alpha must be positive: nan not allowed"); ν and γ invert through `tan(π(y/5 − 0.5))` and `tan(π(y/1.9 − 0.5))`, whose period is π, so a ν above 5 or a γ above 1.9 comes back as another value inside the range the direct transform's comments name ("limit nu to 5.00", "limit gamma to 1.9", `zabrinterpolation.hpp:97-103`). On the wheel's ZABR cube a fixed ρ of 0.99995 came back as −0.9999, ν 6.0 as 0.9999999999999998 and γ 2.0 as 0.09999999999999978. nanoql's inverse clamps its arguments to the direct transform's range, so each comes back as the nearest value the transform reaches, bit for bit: ρ 0.9999, ν 5, γ 1.9, α 1e-7; and the fit from it is the fit from that value, bit for bit, whose parameters the wheel gives. Under S7R6's one-ulp moves of the volatilities, together and each alone, the γ fit's free parameters and the ν fit's α move by at most 5.4e-11, asserted within the vectors' tolerance; the others move by up to 7.7e-6, asserted within 1e-2, but for the α fit's ρ, which moves by 9.1e-2 and so isn't asserted (the wheel's is −0.026977128413889717).

Facts:

- `SabrsEdgesComeBackAsTheTransformsNearestValues`
- `ZabrsTransformKeepsEachEdgeInItsRange`

## YoYCapFloorEngineFixingTests

[`Divergences/YoYCapFloorEngineFixingTests.cs`](../tests/NanoQL.Tests/Divergences/YoYCapFloorEngineFixingTests.cs)

The year-on-year cap and floor engines' forward. QuantLib's engines take every optionlet's forward from the index's curve at the coupon's fixing date, `yoyTS->yoyRate(fixingDate)` (`inflationcapfloorengines.cpp:81`), and read no fixing, while QuantLib's own year-on-year pricer prices the same optionlet on the coupon's fixing (`inflationcouponpricer.cpp:99-109`; QuantLib's defect, R8). nanoql's engines read each optionlet's forward as its coupon's own fixing, `CPI.LaggedYoYRate` at its accrual end with its lag and interpolation, and keep QuantLib's zero deviation for a fixing at or before the volatility's base date. QuantLib's check is the wheel's capped year-on-year coupon priced by its `BlackYoYInflationCouponPricer`, the caplet's rate times its accrual, nominal and discount. On `InflationFixture`'s market, a ratio of UK RPI and a Black volatility of 15% lagged two months, each case a one-coupon cap of 1,000,000, Actual/365 (Fixed): a coupon from 1 July 2023 to 1 July 2024, lagged three months, struck at 3.2%, fixed at 0.033084673723716485 on 1 April 2024 where the curve reads 3%: QuantLib's engine prices it at 0.0, its pricer and nanoql at 1,085.739999233565; the same coupon on a curve based in May 2024: QuantLib's engine refuses "date (April 1st, 2024) is before base date (May 1st, 2024)", its pricer and nanoql price it at 1,085.739999233565; a coupon from 15 July 2024 to 15 July 2025, linear, struck at 2.5%, on a curve falling from 3% in April 2024 to 2% in April 2028: the engine's forward is the curve's rate at May 2025's start, 0.02729637234770705, the coupon's 0.027200547570157427; QuantLib's engine prices it at 2,896.7205361166657, its pricer and nanoql at 2,829.215810328031; a coupon from 3 July 2025 to 3 July 2026 with five fixing days, struck at 2.5%, fixing on 28 April 2026, on the same curve: the engine's forward is April's, 0.025003422313483917, the coupon's May's, 0.02479808350444901; QuantLib's engine prices it at 1,947.2600270718465, its pricer and nanoql at 1,846.091044619172.

Facts:

- `AFixedCapletPricesOnItsFixing`
- `AFixingBeforeTheCurvesBaseDateIsPriced`
- `ALinearObservationInterpolatesTheForward`
- `FixingDaysMoveTheFixingDateAlone`

## YoYInflationCapFloorEmptyLegTests

[`Divergences/YoYInflationCapFloorEmptyLegTests.cs`](../tests/NanoQL.Tests/Divergences/YoYInflationCapFloorEmptyLegTests.cs)

The last year-on-year coupon of a cap on an empty leg. QuantLib's `lastYoYInflationCoupon()` reads `yoyLeg_.back()` (`inflationcapfloor.cpp:109-115`), which is undefined for an empty leg; the wheel doesn't bind it. nanoql refuses with `NanoQLException` and the text QuantLib's `CashFlows` gives an empty leg, "empty leg", as S7's `CapFloor.LastFloatingRateCoupon` does. The rest of the cap is QuantLib's defined behaviour, as the wheel gives it: it is expired and prices at 0.

Facts:

- `TheLastCouponOfAnEmptyLegIsRefused`

## YoYInflationCapFloorNullCashFlowTests

[`Divergences/YoYInflationCapFloorNullCashFlowTests.cs`](../tests/NanoQL.Tests/Divergences/YoYInflationCapFloorNullCashFlowTests.cs)

A year-on-year cap on a leg holding a null cash flow. QuantLib builds the instrument, and its `isExpired()` dereferences each cash flow (`inflationcapfloor.cpp:94-99`), which is undefined for a null one; the wheel refuses `None` in a `Leg`. nanoql refuses it at construction with `ArgumentException`, "null cash flow in leg", from every constructor, naming the leg, as S7's `CapFloor` does.

Facts:

- `ANullCashFlowIsRefusedAtConstruction`

## YoYPricerMissingVolatilityTests

[`Divergences/YoYPricerMissingVolatilityTests.cs`](../tests/NanoQL.Tests/Divergences/YoYPricerMissingVolatilityTests.cs)

A year-on-year pricer with no volatility. QuantLib's `optionletRate` reads `capletVolatility()->baseDate()` before anything else (`inflationcouponpricer.cpp:99`), so its "missing optionlet volatility" check (`:112`) is unreachable and an empty handle throws "empty Handle cannot be dereferenced" on the wheel, for a fixed optionlet too (QuantLib's defect, R8). nanoql checks the volatility first, refusing every optionlet with QuantLib's own "missing optionlet volatility".

Facts:

- `TheMissingVolatilityIsNamed`

## ZabrModelEmptyStrikesTests

[`Divergences/ZabrModelEmptyStrikesTests.cs`](../tests/NanoQL.Tests/Divergences/ZabrModelEmptyStrikesTests.cs)

The ZABR model's vector members given no strikes. QuantLib's `ZabrModel::x`, which `lognormalVolatility`, `normalVolatility` and `localVolatility` call on their vector, reads `strikes[0]` and starts its ascending check at `strikes.begin() + 1` (`zabr.cpp:321-327`), past the end of an empty vector; the wheel doesn't bind the model. nanoql refuses an empty vector with `ArgumentException`, "no strikes given", naming the member's parameter; given strikes, each member returns QuantLib's values, its scalar form's among them.

Facts:

- `AnEmptyStrikeVectorIsRefused`

## ZabrRestartInstanceTests

[`Divergences/ZabrRestartInstanceTests.cs`](../tests/NanoQL.Tests/Divergences/ZabrRestartInstanceTests.cs)

The model a ZABR interpolation prices with after its restarts. QuantLib's model is a smile section built from a copy of the parameters (`zabrinterpolation.hpp:110-115`), rebuilt at each evaluation of the cost function (`xabrinterpolation.hpp:292-307`); after the restarts the fit writes the best restart's parameters back and doesn't rebuild it (`:230-234`). So where the restarts run out without an accepted fit, or Simplex's last trial isn't the vertex it returns, the interpolation's values and its `rmsError()` and `maxError()` are its last trial's, while `alpha()`… are the best restart's: the C++ harness's fits below. nanoql rebuilds the section from the best parameters, so its values and errors are those of QuantLib's section built at the parameters it reports. A cube whose fits' restarts run out records its last trial's errors beside its best parameters, and refuses them against its tolerance where nanoql's cube records its best parameters' errors and builds. Each fit's claim is asserted in the same run, bit for bit: its values and errors are its own section's at the parameters it reports. QuantLib's numbers are asserted within bounds above what S7R6's one-ulp moves of the five volatilities, together and each alone, change: the maximum-error fit's by up to 1.5e-11, within the vectors' tolerance; the Simplex fit's parameters by up to 1.6e-9 and its values by 2.9e-10, within 1e-8, both in S7R6's measure at scale 1.

Facts:

- `AFitWhoseRestartsRunOutPricesWithItsBestParameters`
- `ASimplexFitPricesWithTheVertexItReturns`
- `ACubeWhoseRestartsRunOutRecordsItsBestParametersErrors`

## ZabrSmileSectionParameterTests

[`Divergences/ZabrSmileSectionParameterTests.cs`](../tests/NanoQL.Tests/Divergences/ZabrSmileSectionParameterTests.cs)

A short-maturity ZABR smile section given fewer than five parameters. QuantLib's short-maturity `init` builds its model from `params_[0]` to `params_[4]` unchecked (`zabrsmilesection.hpp:139-152`), past the end of a shorter vector: the wheel's sections with α, β, ν and ρ alone gave the lognormal volatility 0.21300493901013692 at 4% in some runs and NaN in others, where γ = 1 given gives 0.21305710815847104. Only its local-volatility kernel checks the count, "zabr expects 5 parameters (alpha,beta,nu,rho,gamma) but (4) given" (`:158-160`). nanoql refuses fewer than five for every kernel, with that text, naming `zabrParameters`.

Facts:

- `FewerThanFiveParametersAreRefused`

## ZeroCouponInflationSwapConventionTests

[`Divergences/ZeroCouponInflationSwapConventionTests.cs`](../tests/NanoQL.Tests/Divergences/ZeroCouponInflationSwapConventionTests.cs)

`ZeroCouponInflationSwap`'s inflation-leg convention given as Following. QuantLib's default for it is `BusinessDayConvention()`, which is Following (0), and the constructor replaces that value by the fixed leg's convention (`zerocouponinflationswap.cpp:73-74`), so an explicit Following is silently replaced (QuantLib's defect, R8). nanoql's parameter is a nullable convention, null for the fixed leg's, so a given Following is kept. For a swap from 15 June 2024 to Saturday 31 May 2025 on the UK calendar, its fixed leg ModifiedFollowing, the wheel's inflation leg asked for Following pays on 30 May 2025, as the fixed leg does; nanoql's pays on 2 June.

Facts:

- `AGivenFollowingIsKept`

## ZeroCouponInflationSwapFairRateTests

[`Divergences/ZeroCouponInflationSwapFairRateTests.cs`](../tests/NanoQL.Tests/Divergences/ZeroCouponInflationSwapFairRateTests.cs)

`ZeroCouponInflationSwap.FairRate` where the legs pay on different days. QuantLib's fair rate is the inflation growth's rate, `growth^(1/T) - 1` (`zerocouponinflationswap.cpp:118-138`), whose comment reads "Always means that NPV is zero for _this_ instrument", which fails once the legs' conventions part their payment days (QuantLib's defect, R8). nanoql scales the growth's excess by the ratio of the legs' end discounts, read from the engine's results. On `InflationFixture`'s market, for a payer swap of 1,000,000 from 15 June 2024 to Saturday 31 May 2025, its fixed leg Following (paying 2 June) and its inflation leg Preceding (paying 30 May), QuantLib's fair rate is 0.03078176611785821 and the swap rebuilt at it is worth -9.332619118980801; the root of QuantLib's own NPV, found by QuantLib's `Brent` through the wheel, is 0.030791894092525723, and it is nanoql's fair rate.

Facts:

- `TheFairRateZeroesTheSwap`

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
