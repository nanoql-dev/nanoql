<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Concepts

nanoql's types and their names follow QuantLib's, which a QuantLib user will recognise: `Date`, `Calendar`,
`YieldTermStructure`, `Euribor6M`, `VanillaSwap`, `MakeOIS`, `BlackSwaptionEngine`. What differs is how they fit
together. This page covers that design; [the stateless design](stateless.md) shows the patterns an application builds
on it.

## Values and functions

Instruments, curves, indexes, volatility structures and models are immutable values, built once from their inputs.
Engines are static functions: `Calculate` takes the instrument, the evaluation context and the engine's market inputs,
and returns a results record.

[!code-csharp[](examples/Rates/Swaps.cs#vanilla)]

- **Inputs are values.** A rate helper takes its quote as a number, an index takes its forecasting curve, and an engine
  takes its discounting curve and its volatility. There are no quote or curve handles to relink: a new quote means a
  new curve, built from the new quotes.
- **Results are records.** The swap engine's record holds the NPV, the fair rate and spread, and each leg's NPV and
  basis-point value. A result QuantLib doesn't compute for an instrument is `null`.
- **No hidden state.** No object watches another. Apart from the working types, which their caller owns (arrays and
  matrices, random-number and path generators, a Monte Carlo simulation's state), no object changes once built,
  and the few that keep a cache key it by everything the answer reads. So the same inputs always give the same
  results, and calls run side by side on any number of threads.

## The evaluation context

QuantLib reads its evaluation date and settings from a global. nanoql passes them in an `EvaluationContext`, to every
function that reads them, directly or through a callee:

| Field | What it holds | Default |
|---|---|---|
| `EvaluationDate` | the date prices are as of | required |
| `Fixings` | the indexes' stored past fixings | none |
| `IncludeReferenceDateEvents`, `IncludeTodaysCashFlows`, `EnforcesTodaysHistoricFixings` | when an event on the reference date counts, as QuantLib's settings | QuantLib's |
| `UseAtParCoupons` | whether Ibor coupons are priced as par coupons | `true`, as QuantLib-Python |
| `ExchangeRates`, `MoneySettings` | the exchange rates, and how amounts in different currencies combine | QuantLib's known rates; no conversion |
| `CommoditySettings`, `UnitOfMeasureConversions`, `QuantitySettings` | the energy instruments' base currency and units | QuantLib's |

A context is a record: a variation is made with `with`, and two contexts holding the same values are equal. A past
fixing is stored in the context, never in the index:

[!code-csharp[](examples/Rates/IndexesAndFixings.cs#fixings)]

## Conventions as data

QuantLib keeps its market conventions in code: a calendar's holidays, an index's fixing days, a builder's defaults.
nanoql keeps QuantLib's values and reads each preset's and each builder's conventions through a `ConventionProvider`
when the object is built. With nothing configured, every object is QuantLib's. The `NanoQL.Extensions` package reads
and writes conventions as XML records, so a desk overrides a value, or adds a convention of its own, as data
([conventions.md](conventions.md); every shipped value in [the catalogue](conventions-catalogue.md)).

`ConventionProvider.Default` is the library's one global. It is read only when an object is built, never while it
prices, so a price doesn't depend on when or where it runs.

## Where nanoql differs from QuantLib

nanoql fixes QuantLib's defects where it finds them, and a few of its design choices give different results. Each
departure is a documented divergence, with the test that asserts it: [divergences.md](divergences.md) lists them.

## Experimental code

The types ported from QuantLib's experimental folder carry `[Experimental("NQL0001")]`, and so do the types and members
nanoql adds as experimental: the compiler reports NQL0001 where code uses them, until a project opts in with
`<NoWarn>$(NoWarn);NQL0001</NoWarn>`. The experimental API may change in any release, and its released names are never
reused.

## Deriving your own types

The public bases QuantLib's hierarchy derives from, abstract or unsealed, are the extension points: term structures,
stochastic processes, volatility structures, calendars, day counters, cash flows, coupons, instruments and more. A
type of your own derives from one, or implements one of the interfaces the library's types implement; the extension
points page lists them all. Every other public class is sealed, so that no subclass can add mutable state or change
equality behind its base's back. Compose instead, or add members through C# 14's extension members.
