<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Getting started

nanoql is a .NET 10 library, and Python, C and C++ reach it through its C interface ([in Python](#in-python) below).
From C#, add its package to a project:

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

## In Python

```shell
pip install nanoql
```

It needs Python 3.11 or later, on Windows x64 or Linux x64, and no .NET runtime
([which platforms](faq.md#which-platforms-does-nanoql-run-on)). The same swap:

```python
import datetime

from nanoql import ConventionSet, DiscountingSwapEngine, EvaluationContext, FlatForward, MakeOIS, Period, TimeUnit

today = datetime.date(2026, 10, 15)
context = EvaluationContext(today)
qs = ConventionSet.quant_lib()
curve = FlatForward(today, 0.036, qs.day_counter("Actual365Fixed"))

swap = (MakeOIS(Period(5, TimeUnit.Years), qs.overnight_index("Sofr", curve))
        .with_fixed_rate(0.036).with_nominal(10e6)
        .to_overnight_indexed_swap(context))

result = DiscountingSwapEngine.calculate(swap, context, curve)
print(f"NPV {result.npv:,.2f}, fair rate {result.fair_rate:.4%}")
```

It prints `NPV 7,041.24, fair rate 3.6154%`, the C# example's results. In Python a preset such as SOFR or
Actual/365 (Fixed) comes from `ConventionSet.quant_lib()` by name, where C# constructs its class.

That swap, a bootstrapped SOFR curve, €STR and Euribor curves with their swaps, a SABR smile, an American put and a
CDS run in
[a notebook you can open in Google Colab](https://colab.research.google.com/github/nanoql-dev/nanoql/blob/main/docs/notebooks/first-steps.ipynb), with nothing to install;
locally, `pip install nanoql jupyter matplotlib` runs it. [The bindings guide](bindings.md) gives the Python layer's rules.

## Next

- [Concepts](concepts.md): values and functions, the evaluation context, conventions as data, and what is
  experimental.
- The rates guides: [dates and calendars](rates/dates-and-calendars.md), [curves](rates/curves.md),
  [indexes and fixings](rates/indexes-and-fixings.md), [swaps](rates/swaps.md),
  [caps and floors](rates/caps-and-floors.md) and [swaptions](rates/swaptions.md).
- [Pricing a USD SOFR swap to match the market](sofr-example.md): the conventions that move a price, set in XML.
- [The stateless design](stateless.md): rebuilding, memoizing and pricing scenarios in parallel.
- Coming from QuantLib's C++, QuantLib-Python or QLNet: [from QuantLib's C++](migration/from-quantlib-cpp.md),
  [from QuantLib-Python](migration/from-quantlib-python.md), [from QLNet](migration/from-qlnet.md).
- Beyond C#: [the bindings](bindings.md) for Python and for C, which any language that calls C can use, the C++
  layer over it ([C++](cpp.md)), and [the Excel add-in](excel.md).
