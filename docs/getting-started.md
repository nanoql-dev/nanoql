<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Getting started

nanoql is a .NET 10 library. Add its package to a project:

```shell
dotnet add package NanoQL
```

The conventions' XML format and QLNet's additions are in a second package, `NanoQL.Extensions`
([conventions.md](conventions.md)); a first price needs only the first.

## A first swap

Price a five-year SOFR swap on a flat curve. The evaluation date goes in an `EvaluationContext`, the curve and the swap
are values, and the engine is a static function of them:

[!code-csharp[](examples/GettingStarted/FirstSwap.cs#swap)]

The results:

[!INCLUDE [the first swap's results](_generated/first-swap-output.md)]

`MakeOIS` takes QuantLib's defaults for every option it isn't given: here, SOFR's own calendar and day count, annual
payments, and two settlement days. `DiscountingSwapEngine.Calculate` returns a results record, whose `NPV`, `FairRate`
and each leg's `LegNPV` and `LegBPS` are read like any other property. The same inputs give the same results, on
any thread.

## Next

- [Concepts](concepts.md): values and functions, the evaluation context, conventions as data, and what is
  experimental.
- The rates guides: [dates and calendars](rates/dates-and-calendars.md), [curves](rates/curves.md),
  [indexes and fixings](rates/indexes-and-fixings.md), [swaps](rates/swaps.md),
  [caps and floors](rates/caps-and-floors.md) and [swaptions](rates/swaptions.md).
- [Pricing a USD SOFR swap to match the market](sofr-example.md): the conventions that move a price, set in XML.
- [The stateless design](stateless.md): rebuilding, memoizing and pricing scenarios in parallel.
- Coming from QuantLib-Python or QLNet: [from QuantLib-Python](migration/from-quantlib-python.md),
  [from QLNet](migration/from-qlnet.md).
- Beyond C#: [the bindings](bindings.md) for Python, C, C++ and Java, and [the Excel add-in](excel.md).
