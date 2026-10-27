// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's pathwise discounter vectors, <c>tests/vectors/cxx/models/marketmodels/pathwisediscounter.json</c>:
/// <see cref="MarketModelPathwiseDiscounter"/>'s factors at each step's discount factors.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MarketModelPathwiseDiscounterCxxVectorTests
{
    [Fact]
    public void MarketModelPathwiseDiscounterCxxVector() =>
        MarketModelGreeksCxxInputs.CheckDiscounter("cxx/models/marketmodels/pathwisediscounter");
}
