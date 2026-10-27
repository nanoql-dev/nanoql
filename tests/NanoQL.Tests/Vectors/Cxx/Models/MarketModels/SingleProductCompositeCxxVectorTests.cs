// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's single-product composite vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/singleproductcomposite.json</c>:
/// <see cref="SingleProductComposite"/> of the multi-product composite's parts, every part's cash flows its one
/// product's.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SingleProductCompositeCxxVectorTests
{
    [Fact]
    public void SingleProductCompositeCxxVector() =>
        MarketModelProductCxxInputs.CheckSteps("cxx/models/marketmodels/products/singleproductcomposite");
}
