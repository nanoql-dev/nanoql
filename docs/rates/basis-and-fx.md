<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Basis swaps and FX

A basis swap between two Euribor tenors, an FX forward, and a curve implied by FX swap points, all on the stable API;
then, experimental, a curve from cross-currency basis swaps. The market is flat: €STR at 2.80%, dollars at 4.00%, and
EUR/USD at 1.0850.

## A basis swap

Euribor 3M plus a spread against Euribor 6M, each forecast on its own curve and both discounted on €STR, as a
`FloatFloatSwap`. A spread is linear in the 3M leg's coupons, so the leg's BPS gives the fair spread:

[!code-csharp[](../examples/Rates/BasisAndFx.cs#basis)]

[!INCLUDE [the basis swap](../_generated/basis-swap-output.md)]

## An FX forward

Euros sold for dollars in a year, valued from the two discount curves and spot; struck at its fair rate, it is worth
nothing:

[!code-csharp[](../examples/Rates/BasisAndFx.cs#forward)]

[!INCLUDE [the forward](../_generated/fx-forward-output.md)]

## A curve from FX swap points

A euro curve, collateralized in dollars, bootstrapped from EUR/USD swap points with `FxSwapRateHelper`:

[!code-csharp[](../examples/Rates/BasisAndFx.cs#fxswaps)]

The curve gives back each quote:

[!INCLUDE [the swap points](../_generated/fx-swap-curve-output.md)]

## Experimental: a curve from cross-currency basis swaps

The cross-currency basis-swap helpers are experimental, as QuantLib's are: their members can change in any release.
Using them needs the opt-in, `NQL0001` in C# (here a `#pragma` in the sample's own file) and
`import nanoql.experimental` in Python ([concepts](../concepts.md)). A euro curve bootstrapped from EUR/USD basis
swaps on €STR and SOFR, collateralized in dollars:

[!code-csharp[](../examples/Rates/CrossCurrencyBasis.cs#xccy)]

[!INCLUDE [the basis quotes](../_generated/cross-currency-basis-output.md)]
