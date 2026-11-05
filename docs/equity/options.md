<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Equity options

This page follows QuantLib's EquityOption example: one put, valued with European, Bermudan and American exercise by
each engine family. Its values match that example's output, but for the one marked †. The sample,
[`EquityOptions.cs`](../examples/Equity/EquityOptions.cs), carries QuantLib's notice.

## The option and its market

A put struck at 40 on a stock at 36, with a 6% rate, no dividends and 20% volatility, expiring a year after its
settlement; the Bermudan one can be exercised every three months. The curves and the volatility are values, and the
process holds them:

[!code-csharp[](../examples/Equity/EquityOptions.cs#setup)]

## The engines

Closed forms and approximations: Black-Scholes, Black-Scholes with Vasicek rates, the Heston and Bates models with
their parameters set to the same volatility, the American approximations of Barone-Adesi and Whaley, Bjerksund and
Stensland and the QD+ fixed point, and the integral of the payoff:

[!code-csharp[](../examples/Equity/EquityOptions.cs#analytic)]

Finite differences and seven binomial trees, each on 801 time steps, for all three exercises:

[!code-csharp[](../examples/Equity/EquityOptions.cs#lattices)]

Monte Carlo: crude paths to an absolute tolerance, a Sobol sequence, and Longstaff and Schwartz's regression for
early exercise:

[!code-csharp[](../examples/Equity/EquityOptions.cs#montecarlo)]

## The values

[!INCLUDE [each method's values](../_generated/equity-options-output.md)]

† Longstaff and Schwartz's engine draws its calibration paths from the seed QuantLib documents, the pricing seed plus
an offset, where QuantLib's code takes its null. QuantLib's example given that seed prints this value
([the divergence](../divergences.md#longstaffschwartzcalibrationseedtests)).

Each engine is a static function of the option, the context and the process, and the Monte Carlo engines take their
seeds as arguments, so the table is the same on every run.
