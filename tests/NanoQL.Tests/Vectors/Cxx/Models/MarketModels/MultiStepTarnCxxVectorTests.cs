// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's multi-step TARN vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/multistep/multisteptarn.json</c>:
/// <see cref="MultiStepTarn"/> stepped on set curve states, each step's counts, time indices and
/// amounts, and its values through the accounting engine over a few paths.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultiStepTarnCxxVectorTests
{
    [Fact]
    public void MultiStepTarnCxxVector() =>
        MarketModelProductCxxInputs.CheckSteps("cxx/models/marketmodels/products/multistep/multisteptarn");
}
