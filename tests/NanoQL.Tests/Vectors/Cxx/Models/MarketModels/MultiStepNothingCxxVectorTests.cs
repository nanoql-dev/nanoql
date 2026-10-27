// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's product of nothing vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/multistep/multistepnothing.json</c>:
/// <see cref="MultiStepNothing"/> stepped on set curve states, its counts 0 and its path finished at its done index.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultiStepNothingCxxVectorTests
{
    [Fact]
    public void MultiStepNothingCxxVector() =>
        MarketModelProductCxxInputs.CheckSteps("cxx/models/marketmodels/products/multistep/multistepnothing");
}
