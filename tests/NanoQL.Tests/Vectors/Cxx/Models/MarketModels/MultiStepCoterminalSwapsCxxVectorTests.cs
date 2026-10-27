// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's multi-step coterminal swaps vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/multistep/multistepcoterminalswaps.json</c>:
/// <see cref="MultiStepCoterminalSwaps"/> stepped on set curve states, each step's counts, time indices and
/// amounts, and its values through the accounting engine over a few paths.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultiStepCoterminalSwapsCxxVectorTests
{
    [Fact]
    public void MultiStepCoterminalSwapsCxxVector() =>
        MarketModelProductCxxInputs.CheckSteps("cxx/models/marketmodels/products/multistep/multistepcoterminalswaps");
}
