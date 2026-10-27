// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's multi-step forwards vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/multistep/multistepforwards.json</c>:
/// <see cref="MultiStepForwards"/> stepped on set curve states, each step's counts, time indices and amounts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultiStepForwardsCxxVectorTests
{
    [Fact]
    public void MultiStepForwardsCxxVector() =>
        MarketModelProductCxxInputs.CheckSteps("cxx/models/marketmodels/products/multistep/multistepforwards");
}
