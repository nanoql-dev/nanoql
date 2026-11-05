<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Multi-curve bootstrapping

This page follows QuantLib's MulticurveBootstrapping example: an overnight curve from OIS quotes, then a Euribor 6M
curve bootstrapped from a deposit, FRAs and swaps discounted on it, the swaps priced with each curve in its role. Its
values match that example's output. The example's quotes are Eonia's, here taken as €STR's, whose conventions are the
same. The sample, [`MulticurveBootstrapping.cs`](../examples/Rates/MulticurveBootstrapping.cs), carries QuantLib's
notice.

## The discounting curve

€STR from overnight, tomorrow-next and spot-next deposits, short OIS, OIS between ECB meeting dates, and OIS out to 30
years, on 11 December 2012. Both curves interpolate the discounts' logarithms by a monotonic cubic spline, QuantLib's
`MonotonicLogCubic`:

[!code-csharp[](../examples/Rates/MulticurveBootstrapping.cs#estr)]

## The forecasting curve

Euribor 6M from the 6M deposit, FRAs starting in one to eighteen months, and swaps out to 60 years. Each swap helper
takes the €STR curve as its discounting curve, so the bootstrap solves for the Euribor forwards that price each swap
at its quote with €STR discounting:

[!code-csharp[](../examples/Rates/MulticurveBootstrapping.cs#euribor)]

## Two swaps on the two curves

A five-year payer swap at 0.70%, and the same swap starting a year forward, each forecast on Euribor 6M and discounted
on €STR; then both again on a Euribor curve built on a five-year quote of 0.90%:

[!code-csharp[](../examples/Rates/MulticurveBootstrapping.cs#swaps)]

[!INCLUDE [the two swaps](../_generated/multi-curve-swaps-output.md)]

The spot swap's fair rate is the five-year quote, the bootstrap's condition. The forward swap's value doesn't move with
that quote: its floating leg runs from one year to six, and the deposit and FRAs fix the leg to one year while the
six-year quote fixes the leg to six, each on €STR discounting.

A new quote is a new curve: the forecasting curve is built again from its helpers, the €STR curve unchanged.
