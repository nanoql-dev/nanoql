<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Migrating from QLNet

QLNet and nanoql are both C# libraries whose types follow QuantLib's, so most QLNet code maps class by class: the same
`Date`, `Euribor6M`, `MakeVanillaSwap` or `BlackSwaptionEngine`. What changes is how they are connected, and the
names' casing: nanoql's members are .NET's PascalCase, where QLNet keeps QuantLib's camelCase. This guide pairs QLNet
code with nanoql's for the rates products, each pair building the same trade on the same quotes.
[The parity table](../parity/qlnet.md) lists the QuantLib classes nanoql ports, each with QLNet's class where it has
one.

## The platform

nanoql runs on .NET 10 alone. It uses static abstract interface members, which .NET Framework can't run, so code that
QLNet serves on .NET Framework through `netstandard2.0` moves to .NET 10 first.

## The concepts

| QLNet | nanoql |
|---|---|
| `Settings.setEvaluationDate(today)`, held per thread | `new EvaluationContext { EvaluationDate = today }`, passed to each call that reads it |
| `SimpleQuote`, `Handle<Quote>` | a `double`: a new quote means a new object, built from it |
| `Handle<YieldTermStructure>`, `RelinkableHandle<YieldTermStructure>` | the curve itself, passed where it is used |
| `instrument.setPricingEngine(engine)`, then `instrument.NPV()` | `Engine.Calculate(instrument, context, …)`, returning a results record |
| observers and lazy recalculation | the application's layer above the library: rebuild on change, or memoize by inputs ([the stateless design](../stateless.md)) |
| `index.addFixing(date, value)`, held in a global store | `context.Fixings.AddFixing(index, date, value)`, held by the context |
| a builder's implicit conversion, or `value()` | `.ToVanillaSwap(context)`, `.ToCapFloor(context)`, … |
| `swap.fairRate()`, `swap.legBPS(0)` | the results record's `FairRate`, `LegBPS[0]` |
| QLNet's additions: `MBSFixedRateBond`, `PSACurve`, `ConstantCPR`, `AmortizingBond` | the same classes, in the `NanoQL.Extensions` package |

## Curves

```csharp
using QLNet;

var today = new Date(15, Month.October, 2026);
Settings.setEvaluationDate(today);

(int Years, double Rate)[] swapQuotes =
    [(2, 0.0225), (3, 0.0232), (5, 0.0245), (7, 0.0256), (10, 0.0270), (15, 0.0283), (20, 0.0288), (30, 0.0285)];
var helpers = new List<RateHelper>
{
    new DepositRateHelper(new Handle<Quote>(new SimpleQuote(0.0205)), new Euribor(new Period(3, TimeUnit.Months))),
    new DepositRateHelper(new Handle<Quote>(new SimpleQuote(0.0210)), new Euribor6M()),
};
helpers.AddRange(swapQuotes.Select(q =>
    new SwapRateHelper(q.Rate, new EuriborSwapIsdaFixA(new Period(q.Years, TimeUnit.Years)))));
var curve = new PiecewiseYieldCurve<Discount, LogLinear>(today, helpers, new Actual365Fixed());
var curveHandle = new Handle<YieldTermStructure>(curve);
```

[!code-csharp[](../examples/Rates/Curves.cs#curve)]

The helpers take their quotes as numbers and the context, and the curve is passed as itself: there is no handle.

## Indexes and fixings

```csharp
var euribor = new Euribor6M(curveHandle);
Date yesterday = euribor.fixingCalendar().advance(today, -1, TimeUnit.Days);
euribor.addFixing(yesterday, 0.0212);
```

[!code-csharp[](../examples/Rates/IndexesAndFixings.cs#fixings)]

QLNet's fixing goes into a global store every Euribor 6M index reads; nanoql's goes into the context.

## Vanilla swaps

```csharp
var engine = new DiscountingSwapEngine(curveHandle);
VanillaSwap swap = new MakeVanillaSwap(new Period(5, TimeUnit.Years), euribor, 0.025)
    .withNominal(10e6)
    .withPricingEngine(engine);
Console.WriteLine($"{swap.NPV()} {swap.fairRate()}");
```

[!code-csharp[](../examples/Rates/Swaps.cs#vanilla)]

## Overnight swaps

```csharp
var flat = new Handle<YieldTermStructure>(new FlatForward(today, 0.036, new Actual365Fixed()));
OvernightIndexedSwap ois = new MakeOIS(new Period(5, TimeUnit.Years), new Sofr(flat), 0.036)
    .withNominal(10e6)
    .withPricingEngine(new DiscountingSwapEngine(flat));
Console.WriteLine($"{ois.NPV()} {ois.fairRate()}");
```

[!code-csharp[](../examples/GettingStarted/FirstSwap.cs#swap)]

## Caps and floors

```csharp
CapFloor cap = new MakeCapFloor(CapFloorType.Cap, new Period(5, TimeUnit.Years), euribor, 0.025)
    .withNominal(10e6)
    .withPricingEngine(new BlackCapFloorEngine(curveHandle, 0.20));
Console.WriteLine(cap.NPV());
```

[!code-csharp[](../examples/Rates/CapsAndFloors.cs#price)]

## Swaptions

```csharp
var swapIndex = new EuriborSwapIsdaFixA(new Period(5, TimeUnit.Years), curveHandle);
Swaption swaption = new MakeSwaption(swapIndex, new Period(1, TimeUnit.Years), 0.025)
    .withNominal(10e6)
    .withPricingEngine(new BlackSwaptionEngine(curveHandle, 0.20));
Console.WriteLine(swaption.NPV());
```

[!code-csharp[](../examples/Rates/Swaptions.cs#price)]
