<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->

# Credit default swaps

This page follows QuantLib's CDS example, its first two cases: a hazard curve bootstrapped from quoted spreads, then
ISDA's standard model. Its values match that example's output. The sample,
[`CreditDefaultSwaps.cs`](../examples/Credit/CreditDefaultSwaps.cs), carries QuantLib's notice.

## A hazard curve from spreads

Four CDS quoted at 150 bp, from three months to two years, with a recovery of 50% and a flat 1% discount curve. A
`SpreadCdsHelper` per quote, and a piecewise curve of flat hazard rates bootstrapped through them:

[!code-csharp[](../examples/Credit/CreditDefaultSwaps.cs#bootstrap)]

The curve's nodes and its survival probabilities:

[!INCLUDE [the hazard rates](../_generated/cds-hazard-rates-output.md)]

[!INCLUDE [the survival probabilities](../_generated/cds-survival-output.md)]

Each quoted CDS, priced on the curve by the mid-point engine:

[!code-csharp[](../examples/Credit/CreditDefaultSwaps.cs#reprice)]

[!INCLUDE [the repriced quotes](../_generated/cds-repriced-output.md)]

Each is worth nothing at its quoted spread, the bootstrap's condition, and its default and coupon legs offset.

## ISDA's standard model

ISDA's rate curve from deposits and swaps, a flat hazard rate, and a reference trade, five years of protection bought
on 100 million at 100 bp, priced by `IsdaCdsEngine`:

[!code-csharp[](../examples/Credit/CreditDefaultSwaps.cs#isda)]

The rate curve at its pillars:

[!INCLUDE [ISDA's rate curve](../_generated/cds-isda-curve-output.md)]

[!INCLUDE [the reference trade](../_generated/cds-isda-trade-output.md)]
