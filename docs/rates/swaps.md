<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Swaps

The builders make swaps as QuantLib's do: `MakeVanillaSwap` from a tenor and an Ibor index, `MakeOIS` from a tenor and
an overnight index, each option not given taking the index's conventions or QuantLib's default. A builder ends in
`ToVanillaSwap(context)` or `ToOvernightIndexedSwap(context)`, the context giving the evaluation date its start is
counted from.

## A vanilla swap

Two five-year payer swaps on Euribor 6M at 2.50%, one spot and one starting in five years, on the curve of
[the curves guide](curves.md), which forecasts and discounts:

[!code-csharp[](../examples/Rates/Swaps.cs#vanilla)]

## An overnight swap

[!code-csharp[](../examples/Rates/Swaps.cs#overnight)]

Here €STR forecasts from the Euribor curve, to keep the example short; a desk's €STR swap would take an €STR curve,
bootstrapped from €STR swaps.

## The results

[!INCLUDE [each swap's results](../_generated/swaps-output.md)]

`DiscountingSwapEngine.Calculate` returns the swap's NPV, its fair rate and fair spread, and each leg's NPV and
basis-point value, the fixed leg first. A results record is a value: comparing two compares every result.

## Other swaps

The same engine prices `FloatFloatSwap`, `ZeroCouponSwap` and a `Swap` of any legs, each with its own results
record; the cross-currency swaps have engines of their own. A leg is built by its builder (`FixedRateLeg`,
`IborLeg`, `OvernightLeg` and the rest), from a schedule.
