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

## Other curves

- **Multi-curve:** an €STR curve for discounting and a Euribor curve bootstrapped on it, each swap helper given the
  discounting curve ([multi-curve bootstrapping](multi-curve.md)).
- **Interpolated curves** from given nodes: `InterpolatedDiscountCurve<TInterpolator>`,
  `InterpolatedZeroCurve<TInterpolator>`, `InterpolatedForwardCurve<TInterpolator>`.
- **Spreaded curves** over another curve: `ZeroSpreadedTermStructure`, `ForwardSpreadedTermStructure`, and the
  piecewise spreads.
- **Fitted bond curves**, the Nelson-Siegel family and splines: `FittedBondDiscountCurve` with its fitting methods.
