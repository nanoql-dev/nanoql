// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's one-step optionlets vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/onestep/onestepoptionlets.json</c>:
/// <see cref="OneStepOptionlets"/> stepped on set curve states, each step's counts, time indices and
/// amounts, and its values through the accounting engine over a few paths.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OneStepOptionletsCxxVectorTests
{
    [Fact]
    public void OneStepOptionletsCxxVector() =>
        MarketModelProductCxxInputs.CheckSteps("cxx/models/marketmodels/products/onestep/onestepoptionlets");
}
