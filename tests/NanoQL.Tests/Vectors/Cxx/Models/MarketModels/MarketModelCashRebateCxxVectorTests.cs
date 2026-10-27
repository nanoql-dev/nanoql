// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's cash rebate vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/multistep/cashrebate.json</c>: <see cref="MarketModelCashRebate"/>
/// stepped once, each product paid its first amount.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MarketModelCashRebateCxxVectorTests
{
    [Fact]
    public void MarketModelCashRebateCxxVector() =>
        MarketModelProductCxxInputs.CheckSteps("cxx/models/marketmodels/products/multistep/cashrebate");
}
