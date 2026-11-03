<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Caps and floors

A cap is a strip of calls on an index's fixings, a floor a strip of puts. `MakeCapFloor` builds one from its type, a
tenor and an Ibor index, as QuantLib's does: the strike, if not given, is the at-the-money rate of its floating leg.

## Pricing on a volatility

Six five-year Euribor caps and floors on the curve of [the curves guide](curves.md), each priced on a 20% lognormal
volatility and on a 0.60% normal one:

[!code-csharp[](../examples/Rates/CapsAndFloors.cs#price)]

[!INCLUDE [each cap and floor's value](../_generated/caps-floors-output.md)]

`BlackCapFloorEngine` prices each caplet by Black's formula, `BachelierCapFloorEngine` by Bachelier's. Each takes a
volatility as a number, as here, or as an optionlet volatility structure: `ConstantOptionletVolatility`, or one
stripped from a cap volatility surface (`OptionletStripper1`, `StrippedOptionletAdapter`). A shifted lognormal
volatility takes its displacement as a further argument.
