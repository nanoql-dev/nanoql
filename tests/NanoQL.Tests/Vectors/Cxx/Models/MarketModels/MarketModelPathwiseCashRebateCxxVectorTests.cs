// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pathwise cash rebate vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/pathwise/pathwiseproductcashrebate.json</c>:
/// <see cref="MarketModelPathwiseCashRebate"/> stepped on set curve states, each step's counts, time indices,
/// amounts and their derivatives.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MarketModelPathwiseCashRebateCxxVectorTests
{
    [Fact]
    public void MarketModelPathwiseCashRebateCxxVector() =>
        MarketModelProductCxxInputs.CheckPathwiseSteps("cxx/models/marketmodels/products/pathwise/pathwiseproductcashrebate");
}
