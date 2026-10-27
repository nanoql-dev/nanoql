// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pathwise caplets vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/pathwise/pathwiseproductcaplet.json</c>:
/// <see cref="MarketModelPathwiseMultiCaplet"/>, <see cref="MarketModelPathwiseMultiDeflatedCaplet"/> and <see cref="MarketModelPathwiseMultiDeflatedCap"/> stepped on set curve states, each step's counts, time indices,
/// amounts and their derivatives.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PathwiseProductCapletCxxVectorTests
{
    [Fact]
    public void PathwiseProductCapletCxxVector() =>
        MarketModelProductCxxInputs.CheckPathwiseSteps("cxx/models/marketmodels/products/pathwise/pathwiseproductcaplet");
}
