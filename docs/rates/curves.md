<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Curves

A yield curve gives discount factors, zero rates and forward rates from its reference date. `FlatForward` is the
simplest; a bootstrapped curve fits the market's quotes.

## Bootstrapping

Each quote becomes a rate helper: a deposit, a FRA, a futures contract, a swap or an overnight swap. The piecewise
curve finds, pillar by pillar, the values that reprice every helper. This one bootstraps a Euribor 6M curve from two
deposits and eight swaps, on illustrative quotes:

[!code-csharp[](../examples/Rates/Curves.cs#curve)]

A helper takes its quote as a number and the context it is dated in. The curve runs its bootstrap when it is built,
and is immutable from then on: a new quote means a new curve.

The curve's name gives what it interpolates and how. `PiecewiseLogLinearDiscount` interpolates the log of the discount
factors linearly; `PiecewiseLinearZero`, `PiecewiseCubicZero`, `PiecewiseFlatForward` and the rest are QuantLib's
other choices, and `PiecewiseYieldCurve<TTraits, TInterpolator, TBootstrap>` takes them as type arguments. The
`GlobalPiecewise` curves fit every pillar at once, with QuantLib's global bootstrap.

## Reading it

[!code-csharp[](../examples/Rates/Curves.cs#read)]

[!INCLUDE [the curve at a few tenors](../_generated/curves-read-output.md)]

`ZeroRate` and `ForwardRate` return an `InterestRate`, the rate with its day counter, compounding and frequency.

## A helper of your own

The rate helpers are an extension point: a curve bootstraps on any class derived from `RateHelper`. A helper whose
dates follow the evaluation date derives from `RelativeDateRateHelper`; one built on fixed dates derives from
`RateHelper` with its quote alone. This one is a negotiable certificate of deposit: its quote is the NCD's rate, and
the curve implies the coupon at which a bond over the NCD's schedule, from the evaluation date, is worth par.

[!code-csharp[](../examples/Rates/NcdRateHelper.cs#helper)]

- **Dates:** the constructor stores what the helper needs, then calls `InitializeDates`, which builds the instrument
  from the context's evaluation date and sets the protected date fields: `earliestDate`, `maturityDate` and
  `latestDate`, with `latestRelevantDate` or `pillarDate` where they differ. A curve built for another evaluation
  date calls it again on a copy of the helper, so one set of helpers serves curves for several dates, and the fields
  it sets can't be `readonly`.
- **The implied quote:** `ImpliedQuote` prices on the curve under construction, read through `TermStructure` after
  `RequireTermStructure()`. The bootstrap finds the curve on which each helper's implied quote is its quote.
- **Binding:** the bootstrap gives each helper a copy bound to the curve. A helper holding objects that price against
  the curve, such as an index forecasting on it, overrides `WithTermStructure` (as `protected override` from your own
  assembly), calls the base, and rebuilds them on the copy it returns, as `SwapRateHelper` does. This one reads the
  curve only in `ImpliedQuote`, so it needs no override.

The curve takes these helpers as it takes the library's:

[!code-csharp[](../examples/Rates/CustomRateHelpers.cs#curve)]

[!INCLUDE [each NCD on the curve](../_generated/curves-custom-helper-output.md)]

Each NCD's bond, its coupon the quoted rate, is worth par on the curve.

## Other curves

- **Multi-curve:** an €STR curve for discounting and a Euribor curve bootstrapped on it, each swap helper given the
  discounting curve ([multi-curve bootstrapping](multi-curve.md)).
- **Interpolated curves** from given nodes: `InterpolatedDiscountCurve<TInterpolator>`,
  `InterpolatedZeroCurve<TInterpolator>`, `InterpolatedForwardCurve<TInterpolator>`.
- **Spreaded curves** over another curve: `ZeroSpreadedTermStructure`, `ForwardSpreadedTermStructure`, and the
  piecewise spreads.
- **Fitted bond curves**, the Nelson-Siegel family and splines: `FittedBondDiscountCurve` with its fitting methods.
