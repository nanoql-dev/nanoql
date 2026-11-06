<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Migrating from QuantLib's C++

nanoql's C++ layer, `nanoql.hpp`, keeps QuantLib's class names and member names, so most QuantLib code maps class by
class: the same `Date`, `MakeVanillaSwap` or `DiscountingSwapEngine`, and the same `discount(t)` or `fairRate()`. What
changes is how they are connected, and the C++ around them: objects held by value rather than by shared pointer, no
handles or quotes, and indexes and day counters taken by name from a convention set. This guide pairs QuantLib's C++
with nanoql's for the rates products, each pair building the same trade on the same quotes. [The C++ page](../cpp.md)
gives the install, the CMake package and a first program.

Each QuantLib block below follows `#include <ql/quantlib.hpp>` and `using namespace QuantLib;`, each nanoql block
`#include "nanoql.hpp"` and `using namespace nanoql;`, both with `#include <cstdio>` for `std::printf`, and each runs in
`main()` after the blocks above it.

## The concepts

| QuantLib | nanoql |
|---|---|
| `Settings::instance().evaluationDate() = today;` | `EvaluationContext context(today);`, passed to each call that reads it |
| `SimpleQuote`, `Handle<Quote>` | a `double`: a new quote means a new object, built from it |
| `Handle<YieldTermStructure>`, `RelinkableHandle<YieldTermStructure>` | the curve itself, passed where it is used |
| `ext::shared_ptr<T>`, `ext::make_shared<T>(…)` | `T` by value: each object holds a handle to its nanoql object, a copy takes another handle to the same object, and the destructor releases its own |
| `instrument->setPricingEngine(engine)`, then `instrument->NPV()` | `Engine::calculate(instrument, context, …)`, returning a results object whose values are `std::optional<double>` |
| observers and `LazyObject` | the application's layer above the library: rebuild on change, or memoize by inputs ([the stateless design](../stateless.md)) |
| `index->addFixing(date, value)`, held in a global store | `context.fixings().addFixing(index, date, value)`, in a new context: `context.withFixings(…)` |
| `ext::make_shared<Euribor6M>(h)`, `Actual365Fixed()`, `TARGET()` | by name from a convention set: `qs.iborIndex("Euribor6M", curve)`, `qs.dayCounter("Actual365Fixed")`, `qs.calendar("TARGET")`, with `ConventionSet qs = ConventionSet::quantLib();` ([conventions as data](../conventions.md)) |
| `PiecewiseYieldCurve<Discount, LogLinear>` | `PiecewiseLogLinearDiscount`, QuantLib-Python's name for it |
| `MakeVanillaSwap(5 * Years, index, 0.025).withNominal(10e6)`, converted to the swap | `MakeVanillaSwap(Period(5, TimeUnit::Years), index)`, then `.withFixedRate(0.025)`, `.withNominal(10e6)` and `.toVanillaSwap(context)` |
| `Date(15, October, 2026)`, `5 * Years` | `Date(15, Month::October, 2026)`, `Period(5, TimeUnit::Years)` |
| `QuantLib::Error`, from `QL_REQUIRE` | `nanoql::error`, a `std::runtime_error` with the C interface's code and the parameter it names |

## Curves

```cpp
// QuantLib
Date today(15, October, 2026);
Settings::instance().evaluationDate() = today;

auto quote = [](Rate r) { return Handle<Quote>(ext::make_shared<SimpleQuote>(r)); };

std::vector<std::pair<Integer, Rate>> swapQuotes = {
    {2, 0.0225}, {3, 0.0232}, {5, 0.0245}, {7, 0.0256},
    {10, 0.0270}, {15, 0.0283}, {20, 0.0288}, {30, 0.0285}};
std::vector<ext::shared_ptr<RateHelper>> helpers = {
    ext::make_shared<DepositRateHelper>(quote(0.0205), ext::make_shared<Euribor>(3 * Months)),
    ext::make_shared<DepositRateHelper>(quote(0.0210), ext::make_shared<Euribor6M>()),
};
for (auto [years, rate] : swapQuotes) {
    auto index = ext::make_shared<EuriborSwapIsdaFixA>(years * Years);
    helpers.push_back(ext::make_shared<SwapRateHelper>(quote(rate), index));
}
auto curve = ext::make_shared<PiecewiseYieldCurve<Discount, LogLinear>>(
    today, helpers, Actual365Fixed());
Handle<YieldTermStructure> curveHandle(curve);
```

```cpp
// nanoql
Date today(15, Month::October, 2026);
EvaluationContext context(today);
ConventionSet qs = ConventionSet::quantLib();
DayCounter actual365 = qs.dayCounter("Actual365Fixed");

std::vector<std::pair<int, double>> swapQuotes = {
    {2, 0.0225}, {3, 0.0232}, {5, 0.0245}, {7, 0.0256},
    {10, 0.0270}, {15, 0.0283}, {20, 0.0288}, {30, 0.0285}};
std::vector<BootstrapHelper_YieldTermStructure> helpers = {
    DepositRateHelper(0.0205, qs.iborIndex("Euribor", Period(3, TimeUnit::Months)), context),
    DepositRateHelper(0.0210, qs.iborIndex("Euribor6M"), context),
};
for (auto [years, rate] : swapQuotes) {
    SwapIndex index = qs.swapIndex("EuriborSwapIsdaFixA", Period(years, TimeUnit::Years));
    helpers.push_back(SwapRateHelper(rate, index, context));
}
PiecewiseLogLinearDiscount curve(today, helpers, actual365, context);
```

The helpers take their quotes as numbers and the context, the indexes come from the convention set, and the curve is
passed as itself: there is no handle. The vector holds the helpers as `BootstrapHelper_YieldTermStructure`, the type the
curve takes, which `RateHelper` derives from.

## Indexes and fixings

```cpp
// QuantLib
auto euribor = ext::make_shared<Euribor6M>(curveHandle);
Date yesterday = euribor->fixingCalendar().advance(today, -1, Days);
euribor->addFixing(yesterday, 0.0212);
std::printf("%.4f\n", euribor->fixing(yesterday));  // 0.0212
```

```cpp
// nanoql
IborIndex euribor = qs.iborIndex("Euribor6M", curve);
Date yesterday = euribor.fixingCalendar().advance(today, -1, TimeUnit::Days);
context = context.withFixings(context.fixings().addFixing(euribor, yesterday, 0.0212));
std::printf("%.4f\n", euribor.fixing(yesterday, context));  // 0.0212
```

QuantLib's fixing goes into a global store every Euribor 6M index reads; nanoql's goes into a new context, which the
variable now holds and each later call takes.

## Vanilla swaps

```cpp
// QuantLib
auto engine = ext::make_shared<DiscountingSwapEngine>(curveHandle);
ext::shared_ptr<VanillaSwap> swap = MakeVanillaSwap(5 * Years, euribor, 0.025)
    .withNominal(10e6)
    .withPricingEngine(engine);
std::printf("%.2f %.6f\n", swap->NPV(), swap->fairRate());  // -23323.32 0.024500
```

```cpp
// nanoql
VanillaSwap swap = MakeVanillaSwap(Period(5, TimeUnit::Years), euribor)
    .withFixedRate(0.025)
    .withNominal(10e6)
    .toVanillaSwap(context);
FixedVsFloatingSwapResults result = DiscountingSwapEngine::calculate(swap, context, curve);
std::printf("%.2f %.6f\n", *result.NPV(), *result.fairRate());  // -23323.32 0.024500
```

The builder ends in `toVanillaSwap(context)`, and the engine is a function of the swap, the context and the curve.

## Overnight swaps

```cpp
// QuantLib
Handle<YieldTermStructure> flat(
    ext::make_shared<FlatForward>(today, 0.036, Actual365Fixed()));
auto sofr = ext::make_shared<Sofr>(flat);
ext::shared_ptr<OvernightIndexedSwap> ois = MakeOIS(5 * Years, sofr, 0.036)
    .withNominal(10e6)
    .withPricingEngine(ext::make_shared<DiscountingSwapEngine>(flat));
std::printf("%.2f %.6f\n", ois->NPV(), ois->fairRate());  // 7041.24 0.036154
```

```cpp
// nanoql
FlatForward flat(today, 0.036, actual365);
OvernightIndex sofr = qs.overnightIndex("Sofr", flat);
OvernightIndexedSwap ois = MakeOIS(Period(5, TimeUnit::Years), sofr)
    .withFixedRate(0.036)
    .withNominal(10e6)
    .toOvernightIndexedSwap(context);
OvernightIndexedSwapResults oisResult =
    DiscountingSwapEngine::calculate(ois, context, flat);
std::printf("%.2f %.6f\n", *oisResult.NPV(), *oisResult.fairRate());  // 7041.24 0.036154
```

## Caps and floors

```cpp
// QuantLib
ext::shared_ptr<CapFloor> cap = MakeCapFloor(CapFloor::Cap, 5 * Years, euribor, 0.025)
    .withNominal(10e6);
cap->setPricingEngine(ext::make_shared<BlackCapFloorEngine>(curveHandle, 0.20));
std::printf("%.2f\n", cap->NPV());  // 117911.83
```

```cpp
// nanoql
CapFloor cap = MakeCapFloor(CapFloorType::Cap, Period(5, TimeUnit::Years), euribor)
    .withStrike(0.025)
    .withNominal(10e6)
    .toCapFloor(context);
InstrumentResults capResult = BlackCapFloorEngine::calculate(cap, context, curve, 0.20);
std::printf("%.2f\n", *capResult.NPV());  // 117911.83
```

## Swaptions

```cpp
// QuantLib
auto swapIndex = ext::make_shared<EuriborSwapIsdaFixA>(5 * Years, curveHandle);
ext::shared_ptr<Swaption> swaption = MakeSwaption(swapIndex, 1 * Years, 0.025)
    .withNominal(10e6);
swaption->setPricingEngine(ext::make_shared<BlackSwaptionEngine>(curveHandle, 0.20));
std::printf("%.2f\n", swaption->NPV());  // 111256.85
```

```cpp
// nanoql
SwapIndex swapIndex =
    qs.swapIndex("EuriborSwapIsdaFixA", Period(5, TimeUnit::Years), curve);
Swaption swaption = MakeSwaption(swapIndex, Period(1, TimeUnit::Years), 0.025)
    .withNominal(10e6)
    .toSwaption(context);
InstrumentResults swaptionResult =
    BlackSwaptionEngine::calculate(swaption, context, curve, 0.20);
std::printf("%.2f\n", *swaptionResult.NPV());  // 111256.85
```

Each pair prints the same values, shown in its comments. The rates guides show nanoql's for the same trades
([swaps](../rates/swaps.md), [caps and floors](../rates/caps-and-floors.md), [swaptions](../rates/swaptions.md)), and
[getting started](../getting-started.md) its overnight swap.
