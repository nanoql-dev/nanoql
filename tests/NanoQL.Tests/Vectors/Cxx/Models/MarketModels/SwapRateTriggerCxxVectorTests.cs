// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swap-rate trigger vectors,
/// <c>tests/vectors/cxx/models/marketmodels/callability/swapratetrigger.json</c>:
/// <see cref="SwapRateTrigger"/> stepped on set curve states, its decision at each exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwapRateTriggerCxxVectorTests
{
    [Fact]
    public void SwapRateTriggerCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckStrategySteps("cxx/models/marketmodels/callability/swapratetrigger");
}
