<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# A SABR smile

The 1Y into 10Y swaption smile of QuantLib's swaption cube tests' market data: an at-the-money vol and the market's
spreads to it at four strikes about the forward, fitted by SABR with β fixed. The fit's values match QuantLib's on the
same data. The sample, [`SabrSmile.cs`](../examples/Rates/SabrSmile.cs), carries QuantLib's notice.

## The smile

A flat 5% curve on 15 October 2026, the ten-year Euribor swap rate a year forward, and its quotes: 14.70% at the money,
and the spreads to it at −200, −50, +50 and +200 bp:

[!code-csharp[](../examples/Rates/SabrSmile.cs#smile)]

## The fit

`SABRInterpolation` fits α, ν and ρ with β fixed at 0.5, minimizing the vols' errors weighted by the strikes' Black
vegas, from SABR's default guesses:

[!code-csharp[](../examples/Rates/SabrSmile.cs#fit)]

[!INCLUDE [the fitted parameters](../_generated/sabr-parameters-output.md)]

## The smile section

The fitted parameters as a `SabrSmileSection`, the smile an engine reads at any strike:

[!code-csharp[](../examples/Rates/SabrSmile.cs#section)]

The smile on a 50 bp grid, beside the quotes:

[!INCLUDE [the smile](../_generated/sabr-smile-output.md)]

Five quotes and three free parameters leave a residual: the fit misses the wing quotes by up to 0.16 vol points, and
gives the smile between them.
