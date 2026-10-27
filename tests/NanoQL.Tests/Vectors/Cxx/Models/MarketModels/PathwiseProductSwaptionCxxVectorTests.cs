// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pathwise coterminal swaptions vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/pathwise/pathwiseproductswaption.json</c>:
/// <see cref="MarketModelPathwiseCoterminalSwaptionsDeflated"/> and <see cref="MarketModelPathwiseCoterminalSwaptionsNumericalDeflated"/> stepped on set curve states, each step's counts, time indices,
/// amounts and their derivatives.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class PathwiseProductSwaptionCxxVectorTests
{
    [Fact]
    public void PathwiseProductSwaptionCxxVector() =>
        MarketModelProductCxxInputs.CheckPathwiseSteps("cxx/models/marketmodels/products/pathwise/pathwiseproductswaption");
}
