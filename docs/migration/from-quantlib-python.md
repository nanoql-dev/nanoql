<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Migrating from QuantLib-Python

nanoql is a .NET library whose types follow QuantLib's, so most QuantLib-Python code maps class by class: the same
`Date`, `Euribor6M`, `PiecewiseLogLinearDiscount` or `MakeVanillaSwap`, written in C#. What changes is how they are
connected. This guide pairs QuantLib-Python code with nanoql's for the rates products, each pair building the same
trade on the same quotes. [The parity table](../parity/quantlib-python.md) lists every class and function
QuantLib-Python constructs, with its nanoql form.

nanoql's own bindings for Python ([bindings.md](../bindings.md)) carry the same API to Python, over a native library.

## The concepts

| QuantLib-Python | nanoql |
|---|---|
| `ql.Settings.instance().evaluationDate = today` | `new EvaluationContext { EvaluationDate = today }`, passed to each call that reads it |
| `ql.SimpleQuote`, `ql.QuoteHandle` | a `double`: a new quote means a new object, built from it |
| `ql.YieldTermStructureHandle`, `ql.RelinkableYieldTermStructureHandle` | the curve itself, passed where it is used |
| `instrument.setPricingEngine(engine)`, then `instrument.NPV()` | `Engine.Calculate(instrument, context, …)`, returning a results record |
| observers and lazy recalculation | the application's layer above the library: rebuild on change, or memoize by inputs ([the stateless design](../stateless.md)) |
| `index.addFixing(date, value)`, held in a global store | `context.Fixings.AddFixing(index, date, value)`, held by the context |
| `ql.Settings`' other flags, `ql.IborCoupon.createAtParCoupons()` | the context's fields: `IncludeReferenceDateEvents`, `UseAtParCoupons`, … |
| `ql.MakeVanillaSwap(…, nominal=…)`, keyword arguments | `new MakeVanillaSwap(…).WithNominal(…)`, ending in `.ToVanillaSwap(context)` |
| `ql.Date(15, ql.October, 2026)`, `ql.Period(5, ql.Years)` | `new Date(15, Month.October, 2026)`, `new Period(5, TimeUnit.Years)` |

## Curves

```python
import QuantLib as ql

today = ql.Date(15, ql.October, 2026)
ql.Settings.instance().evaluationDate = today


def quote(rate):
    return ql.QuoteHandle(ql.SimpleQuote(rate))


swap_quotes = [(2, 0.0225), (3, 0.0232), (5, 0.0245), (7, 0.0256), (10, 0.0270), (15, 0.0283), (20, 0.0288),
               (30, 0.0285)]
helpers = [
    ql.DepositRateHelper(quote(0.0205), ql.Euribor(ql.Period(3, ql.Months))),
    ql.DepositRateHelper(quote(0.0210), ql.Euribor6M()),
] + [ql.SwapRateHelper(quote(rate), ql.EuriborSwapIsdaFixA(ql.Period(years, ql.Years))) for years, rate in swap_quotes]
curve = ql.PiecewiseLogLinearDiscount(today, helpers, ql.Actual365Fixed())
curve_handle = ql.YieldTermStructureHandle(curve)
```

[!code-csharp[](../examples/Rates/Curves.cs#curve)]

The helpers take their quotes as numbers and the context, and the curve is passed as itself: there is no handle.

## Indexes and fixings

```python
euribor = ql.Euribor6M(curve_handle)
yesterday = euribor.fixingCalendar().advance(today, -1, ql.Days)
euribor.addFixing(yesterday, 0.0212)
```

[!code-csharp[](../examples/Rates/IndexesAndFixings.cs#fixings)]

QuantLib-Python's fixing goes into a global store every Euribor 6M index reads; nanoql's goes into the context.

## Vanilla swaps

```python
engine = ql.DiscountingSwapEngine(curve_handle)
swap = ql.MakeVanillaSwap(ql.Period(5, ql.Years), euribor, 0.025, nominal=10e6, pricingEngine=engine)
print(swap.NPV(), swap.fairRate())
```

[!code-csharp[](../examples/Rates/Swaps.cs#vanilla)]

## Overnight swaps

```python
flat = ql.YieldTermStructureHandle(ql.FlatForward(today, 0.036, ql.Actual365Fixed()))
ois = ql.MakeOIS(ql.Period(5, ql.Years), ql.Sofr(flat), 0.036, nominal=10e6,
                 pricingEngine=ql.DiscountingSwapEngine(flat))
print(ois.NPV(), ois.fairRate())
```

[!code-csharp[](../examples/GettingStarted/FirstSwap.cs#swap)]

## Caps and floors

```python
cap = ql.MakeCapFloor(ql.CapFloor.Cap, ql.Period(5, ql.Years), euribor, 0.025, nominal=10e6)
cap.setPricingEngine(ql.BlackCapFloorEngine(curve_handle, quote(0.20)))
print(cap.NPV())
```

[!code-csharp[](../examples/Rates/CapsAndFloors.cs#price)]

## Swaptions

```python
swap_index = ql.EuriborSwapIsdaFixA(ql.Period(5, ql.Years), curve_handle)
swaption = ql.MakeSwaption(swap_index, ql.Period(1, ql.Years), 0.025, nominal=10e6)
swaption.setPricingEngine(ql.BlackSwaptionEngine(curve_handle, quote(0.20)))
print(swaption.NPV())
```

[!code-csharp[](../examples/Rates/Swaptions.cs#price)]

Each pair gives the same values; the rates guides show nanoql's ([swaps](../rates/swaps.md),
[caps and floors](../rates/caps-and-floors.md), [swaptions](../rates/swaptions.md)).
