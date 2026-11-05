<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# A volatility surface and Heston's calibration

The DAX options market of 5 July 2002, thirteen strikes by eight expiries, as QuantLib's tests calibrate Heston's
model to it (from A. Sepp's "Pricing European-Style Options under Jump Diffusion Processes with Stochastic
Volatility"). The calibrated parameters match QuantLib's on the same data. The sample,
[`VolatilityAndHeston.cs`](../examples/Equity/VolatilityAndHeston.cs), carries QuantLib's notice.

## The surface

The quoted Black volatilities as a surface by expiry and strike, `BlackVarianceSurface`, which interpolates in
variance:

[!code-csharp[](../examples/Equity/VolatilityAndHeston.cs#surface)]

Read back at three of its strikes, it gives the quotes:

[!INCLUDE [the surface at three strikes](../_generated/dax-surface-output.md)]

Between the nodes it interpolates in variance, bilinearly by default.

## Heston's calibration

A `HestonModelHelper` per quote, each priced by the semi-analytic engine, and Levenberg-Marquardt minimizing the
vols' errors:

[!code-csharp[](../examples/Equity/VolatilityAndHeston.cs#calibrate)]

The calibrated model:

[!INCLUDE [the calibrated parameters](../_generated/heston-calibration-output.md)]

`Calibrate` returns a new model with its record (the criterion that ended the optimization, the problem's values,
the function evaluations), and leaves the starting model as it was.
