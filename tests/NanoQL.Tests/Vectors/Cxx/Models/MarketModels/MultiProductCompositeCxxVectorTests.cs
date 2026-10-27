// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's multi-product composite vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/multiproductcomposite.json</c>:
/// <see cref="MultiProductComposite"/> of parts finishing together, stepped on set curve states, each part's cash flows
/// scaled and remapped.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultiProductCompositeCxxVectorTests
{
    [Fact]
    public void MultiProductCompositeCxxVector() =>
        MarketModelProductCxxInputs.CheckSteps("cxx/models/marketmodels/products/multiproductcomposite");
}
