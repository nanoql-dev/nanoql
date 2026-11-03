<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Swaptions

A European swaption is the right to enter a swap on its expiry. `MakeSwaption` builds one from a swap index, whose
conventions give the underlying swap, an expiry and a strike, as QuantLib's does; the strike, if not given, is the
underlying's fair rate.

## Pricing on a volatility

Payer swaptions into a five-year Euribor swap, expiring in one year and in five, on the curve of
[the curves guide](curves.md), each priced on a 20% lognormal volatility and on a 0.60% normal one:

[!code-csharp[](../examples/Rates/Swaptions.cs#price)]

[!INCLUDE [each swaption's value](../_generated/swaptions-output.md)]

`BlackSwaptionEngine` prices by Black's formula and `BachelierSwaptionEngine` by Bachelier's, on the annuity of the
underlying's fixed leg. Each takes a volatility as a number, as here, or as a swaption volatility structure: a constant,
a matrix by expiry and tenor (`SwaptionVolatilityMatrix`), or a cube with a smile (`SabrSwaptionVolatilityCube`,
`InterpolatedSwaptionVolatilityCube`).

## Bermudan swaptions

A swaption with several exercise dates is priced on a short-rate model: `TreeSwaptionEngine` on any model with a
tree (Hull-White, Black-Karasinski, …), `FdHullWhiteSwaptionEngine` and `FdG2SwaptionEngine` by finite
differences, and `Gaussian1dSwaptionEngine` on the Gaussian1d models. `JamshidianSwaptionEngine` prices a European
one on an affine model in closed form.
