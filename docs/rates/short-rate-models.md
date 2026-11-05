<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Short-rate models

This page follows QuantLib's BermudanSwaption example: short-rate models calibrated to five co-terminal swaptions,
then a Bermudan swaption priced on them. Its values match that example's output, but for those marked † and ‡, which
two of nanoql's documented fixes of QuantLib move. The sample,
[`ShortRateModels.cs`](../examples/Rates/ShortRateModels.cs), carries QuantLib's notice. The example's fourth model,
Black-Karasinski, is left out: only the tree prices its swaptions, so nothing would check its values.

## The market

A flat curve, Euribor 6M on it, and the swaptions 1Y into 5Y, 2Y into 4Y, … 5Y into 1Y, each with its market vol:

[!code-csharp[](../examples/Rates/ShortRateModels.cs#market)]

## Calibration

G2 on its analytic formula, Hull-White on Jamshidian's, and Hull-White again on a tree, each by Levenberg-Marquardt:

[!code-csharp[](../examples/Rates/ShortRateModels.cs#calibrate)]

Each swaption's value on the calibrated model, as a Black vol, beside its market vol:

[!INCLUDE [the calibrations' vols](../_generated/short-rate-vols-output.md)]

[!INCLUDE [the calibrated parameters](../_generated/short-rate-parameters-output.md)]

## A Bermudan swaption

A payer swaption into the five-year swap, exercisable on each fixed coupon's start, at the money, out of it (the
strike 1.2 times) and in it (0.8 times), on each model's tree and by finite differences:

[!code-csharp[](../examples/Rates/ShortRateModels.cs#bermudan)]

[!INCLUDE [the Bermudan prices](../_generated/short-rate-bermudans-output.md)]

## The marks

- † G2's analytic swaption formula takes each fixed coupon on the leg's own day counter, where QuantLib's takes it on
  the curve's ([the divergence](../divergences.md#g2analyticswaptioncoupontests)). G2's calibration, and every value
  on it, moves. QuantLib, given nanoql's calibrated parameters and the coupons restated on the curve's day counter,
  gives the same model values, and its finite-difference engine the same prices.
- ‡ The tree values a swap on its own dates, where QuantLib's snaps a date within a week of an exercise date to it
  ([the divergence](../divergences.md#treeswaptionsnappingtests)). Every tree value moves, and so does the
  calibration on the tree. Each tree price is within 0.1 on the notional of 1,000 of the finite-difference price on
  the same model, and QuantLib's finite-difference engine, given nanoql's numerical Hull-White, gives its prices.
