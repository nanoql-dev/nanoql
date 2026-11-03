<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Indexes and fixings

An index knows its conventions (its tenor, fixing days, calendar, day counter and currency) and holds the curve it
forecasts from. The Ibor indexes (`Euribor6M`, `USDLibor`, `Tibor`, …) and the overnight ones (`Sofr`, `Estr`,
`Sonia`, `Tonar`, `Corra`, …) are QuantLib's presets.

## Past fixings and forecasts

A past fixing is stored in the evaluation context's `Fixings`; a fixing on a later date is forecast from the index's
curve. Today's fixing is the stored one if there is one, and otherwise forecast when asked to:

[!code-csharp[](../examples/Rates/IndexesAndFixings.cs#fixings)]

[!code-csharp[](../examples/Rates/IndexesAndFixings.cs#read)]

[!INCLUDE [each date's fixing](../_generated/indexes-fixings-output.md)]

`AddFixing` returns a new store, so a context's fixings never change under it. A fixing the store lacks on a past date
is refused, with the date and the index's name, as QuantLib refuses it.

## The same index on another curve

An index built without a curve can still be read for its past fixings and its dates. `index.Clone(curve)` gives the
same index forecasting from another curve, as QuantLib's `clone` does.

## Conventions

The presets take QuantLib's values. With the `NanoQL.Extensions` package an index's conventions can be overridden, and
a family member added (Euribor at two months, say), as data ([conventions.md](../conventions.md)).
