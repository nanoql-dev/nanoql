<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Divergences

Every documented divergence from QuantLib v1.44, one entry per test class under
`tests/NanoQL.Tests/Divergences/` with its file, its summary and its facts, generated from those files by
`tools/divergences.py`: regenerate it with `.venv\Scripts\python -I tools/divergences.py` rather than
editing it by hand.

## AffineDiscountBondFactorsTests

[`Divergences/AffineDiscountBondFactorsTests.cs`](../tests/NanoQL.Tests/Divergences/AffineDiscountBondFactorsTests.cs)

An affine model's discount bond given an empty vector of factors. QuantLib's `OneFactorAffineModel::discountBond(now, maturity, Array factors)` reads `factors[0]` (`onefactormodel.hpp:132-134`), past the end of an empty array, which crashes the wheel with an access violation. nanoql refuses it with `ArgumentException`, "no factors given", naming `factors`; given a factor, it prices as QuantLib's, on the vector's first element.

Facts:

- `AnEmptyFactorVectorIsRefused`

## BMANullIndexTests

[`Divergences/BMANullIndexTests.cs`](../tests/NanoQL.Tests/Divergences/BMANullIndexTests.cs)

A null BMA index is refused on entry, with QuantLib's text for a missing index, "no index provided" (`floatingratecoupon.cpp:52`), naming the parameter. QuantLib's `AverageBMACoupon` reads its index's fixing days before its base checks the index (`averagebmacoupon.cpp:111-113`), `AverageBMALeg` never checks it (`:165-166`) and its coupons then read it, and `BMASwap` passes it to its BMA leg (`bmaswap.cpp:55-58`): results C++ leaves undefined, where the wheel stops on Boost's assertion.

Facts:

- `ANullBMAIndexIsRefusedWithQuantLibsText`

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

## CappedFlooredCouponNullUnderlyingTests

[`Divergences/CappedFlooredCouponNullUnderlyingTests.cs`](../tests/NanoQL.Tests/Divergences/CappedFlooredCouponNullUnderlyingTests.cs)

A capped coupon over a null underlying. QuantLib's constructor reads the underlying's terms in its base initializer (`capflooredcoupon.cpp:30-43`), dereferencing the null pointer: a Boost assertion in the default build, undefined under `std::shared_ptr`. nanoql refuses it with `ArgumentNullException`.

Facts:

- `ANullUnderlyingIsRefused`

## CouponPricerSettingTests

[`Divergences/CouponPricerSettingTests.cs`](../tests/NanoQL.Tests/Divergences/CouponPricerSettingTests.cs)

Setting pricers on a leg. QuantLib's `setCouponPricer` and `setCouponPricers` change the coupons in place, one after another (`couponpricer.cpp:461-466, 480-483`): a coupon that refuses the pricer leaves the earlier ones holding it. Their undefined steps: an empty pricer list on a non-empty leg reads the pricer before the list's start (`:481`), and a null cash flow is dereferenced (`:464, 482`; the first-matching forms swallow the failure and skip it, `:446-456`). nanoql returns a new leg, all or nothing, and refuses both inputs.

Facts:

- `ACouponRefusingThePricerLeavesNoNewLeg`
- `AnEmptyPricerListIsRefused`
- `ANullCashFlowInTheLegIsRefused`

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

## EmptyScheduleTests

[`Divergences/EmptyScheduleTests.cs`](../tests/NanoQL.Tests/Divergences/EmptyScheduleTests.cs)

A leg builder given an empty schedule, or a swap building its legs on one. QuantLib's conversion to a leg counts `schedule_.size()-1` periods, a `Size` wrapping below zero, and reserves them or reads `schedule_.date(0)` past the end of the schedule's dates (`fixedratecoupon.cpp:182-185`, `cashflowvectors.hpp:82-102`, `overnightindexedcoupon.cpp:592-603`, `averagebmacoupon.cpp:225-228`), which is undefined. nanoql refuses the schedule at that point of the conversion, with `NanoQLException`, "empty schedule", after the checks QuantLib makes before it, so that with several bad inputs QuantLib's first refusal is nanoql's; the pilot's `FixedRateLeg` threw `ArgumentOutOfRangeException` from the read.

Facts:

- `ALegBuilderRefusesAnEmptyScheduleAfterQuantLibsEarlierChecks`
- `TheAverageBMALegRefusesAnEmptyScheduleAfterItsNotionalCheck`

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

An exercise's dates read past their end. QuantLib's `Exercise::date(i)` is `dates_[i]` (`exercise.hpp:45`), which reads past the vector for an index out of range, undefined, where its twin `dateAt(i)` (`:46`) throws `std::out_of_range`. nanoql refuses the index in both, with `ArgumentOutOfRangeException` naming `index`, as `Schedule.Date(int)` does. And a swaption on an exercise with no dates, which QuantLib's public `Exercise(type)` builds (`exercise.hpp:41`): QuantLib's swaption reads its last date in `isExpired` (`dates().back()`, `swaption.cpp:159`), and its first in the engines and in `impliedVolatility` (`date(0)`, `blackswaptionengine.hpp:227`, `swaption.cpp:198`), undefined for an empty vector. nanoql's swaption builds on it and refuses where the date is read, with `NanoQLException` and QuantLib's text for the same input, `lastDate()`'s "no exercise date given" (`exercise.cpp:29`).

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

The floating leg builders' undefined inputs. An empty schedule: QuantLib's `FloatingLeg` takes `schedule.size()-1` periods, a `Size` wrapping below zero, so its size checks all pass and `leg.reserve(n)` fails (`cashflowvectors.hpp:82-102`; `IborLeg` reaches it at `iborcoupon.cpp:286`). A null index: `FloatingLeg` reads `index->fixingDays()` as a default argument (`cashflowvectors.hpp:156, 169, 188, 204`). nanoql refuses the empty schedule where QuantLib reserves the leg, after its notional and in-arrears checks, with "empty schedule": `NanoQLException` from `IborLeg`'s conversion, and `ArgumentException` from `CashFlowVectorsFunctions.FloatingLeg`, whose schedule is an argument; and the null index with `ArgumentNullException`.

Facts:

- `AnEmptyScheduleIsRefusedByBothBuilders`
- `ANullIndexIsRefused`

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

A bilinear interpolation over a matrix smaller than its grid. QuantLib checks no matrix against the grid (`interpolation2d.hpp:72-83`), and its bilinear value reads `zData_[j+1][i+1]` for the cell holding the point (`bilinearinterpolation.hpp:50-53`), past the matrix's end for a matrix with fewer rows than y values or fewer columns than x values, undefined. nanoql refuses such a matrix when the interpolation is built, with `ArgumentException` naming the matrix's parameter (`zData`, and the factory's `z`) and QuantLib's own texts for a mismatched matrix (`kernelinterpolation2d.hpp:70-73`). A larger matrix, which QuantLib reads in its first rows and columns, is kept.

Facts:

- `AMatrixSmallerThanTheGridIsRefused`

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
- `ABondsAtmRatePriceOfQuantLibsNullIsAPrice`
- `ALegsAtmRateTargetOfQuantLibsNullIsATarget`
- `ASwaptionHelpersStrikeOfQuantLibsNullIsAStrike`

## OptionDateFromTimeTests

[`Divergences/OptionDateFromTimeTests.cs`](../tests/NanoQL.Tests/Divergences/OptionDateFromTimeTests.cs)

The option date of a time whose interpolated serial number is NaN, infinite or outside the integers. QuantLib's `SwaptionVolatilityDiscrete::optionDateFromTime` truncates the serial with `static_cast<Date::serial_type>` (`swaptionvoldiscrete.hpp:121-123`), whose result C++ leaves undefined there; MSVC's conversion, the platform the vectors are generated on, gives the smallest integer, which `Date`'s range check refuses. .NET's conversion saturates instead (NaN to 0, a large value to `int.MaxValue`), so a literal port would refuse other serials, or none: nanoql takes MSVC's serial in those cases, and the refusal is QuantLib's, `Date`'s `ArgumentOutOfRangeException` and text.

Facts:

- `ATimeWhoseSerialLeavesTheIntegersIsRefusedAsMsvcsSmallestInteger`

## OvernightIndexedCouponRefusalTests

[`Divergences/OvernightIndexedCouponRefusalTests.cs`](../tests/NanoQL.Tests/Divergences/OvernightIndexedCouponRefusalTests.cs)

The overnight coupons' undefined paths, each refused before the undefined step (the playbook's undefined-path rule), and the lookback of `int.MaxValue` days, which `int`? treats as a value where QuantLib's `Null<Natural>()` means none.

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

XABR's parameter transforms at their edges. QuantLib's SABR inverse transform doesn't clamp its arguments (`sabrinterpolation.hpp:103-114`): a ρ beyond 0.9999 takes `asin` of a value above 1, a NaN that the direct transform turns into −0.9999, so a fixed ρ of 0.99995 comes back as −0.9999, its sign flipped, and a free guess of 0.99995 fails the fit with "alpha must be positive: nan not allowed"; an α or ν below 1e-7, ν = 0 included, takes the square root of a negative number, a NaN the fit refuses ("alpha must be positive: nan not allowed", "nu must be non negative: nan not allowed"). nanoql's inverse clamps each argument to its direct transform's range, so such an input comes back as the nearest value the transform reaches, bit for bit: ρ = 0.9999, ν = 1e-7, and the fixed β of 0.6. The fits from those values are the wheel's fits from them. Two of them are well posed and agree within the vectors' tolerance; the three that pin a parameter at an edge can't reproduce the smile, so their optimum is flat: when the input volatilities move by one ulp, their free parameters move by up to 2.4e-3 in S7R6's measure, at the vectors' scale of 1, which is up to 24% of the small ones' own values. Those are asserted within 1e-2 in that measure.

Facts:

- `SabrsEdgesComeBackAsTheTransformsNearestValues`

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
