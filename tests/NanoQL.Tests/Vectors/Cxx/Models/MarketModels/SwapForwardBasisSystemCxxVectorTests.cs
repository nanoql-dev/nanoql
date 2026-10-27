// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swap-forward basis system vectors,
/// <c>tests/vectors/cxx/models/marketmodels/callability/swapforwardbasissystem.json</c>:
/// <see cref="SwapForwardBasisSystem"/> stepped on set curve states, its values at each
/// exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwapForwardBasisSystemCxxVectorTests
{
    [Fact]
    public void SwapForwardBasisSystemCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckProviderSteps("cxx/models/marketmodels/callability/swapforwardbasissystem");
}
