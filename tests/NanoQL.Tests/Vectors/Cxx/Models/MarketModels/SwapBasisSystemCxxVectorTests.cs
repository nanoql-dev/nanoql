// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swap basis system vectors,
/// <c>tests/vectors/cxx/models/marketmodels/callability/swapbasissystem.json</c>:
/// <see cref="SwapBasisSystem"/> stepped on set curve states, its values at each exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwapBasisSystemCxxVectorTests
{
    [Fact]
    public void SwapBasisSystemCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckProviderSteps("cxx/models/marketmodels/callability/swapbasissystem");
}
