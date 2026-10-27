// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pathwise swap vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/pathwise/pathwiseproductswap.json</c>:
/// <see cref="MarketModelPathwiseSwap"/> stepped on set curve states, each step's counts, time indices,
/// amounts and their derivatives.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MarketModelPathwiseSwapCxxVectorTests
{
    [Fact]
    public void MarketModelPathwiseSwapCxxVector() =>
        MarketModelProductCxxInputs.CheckPathwiseSteps("cxx/models/marketmodels/products/pathwise/pathwiseproductswap");
}
