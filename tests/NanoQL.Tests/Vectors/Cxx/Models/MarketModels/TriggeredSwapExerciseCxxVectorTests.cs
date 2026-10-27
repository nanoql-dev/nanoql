// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's triggered swap exercise vectors,
/// <c>tests/vectors/cxx/models/marketmodels/callability/triggeredswapexercise.json</c>:
/// <see cref="TriggeredSwapExercise"/> stepped on set curve states, its variable, guess
/// and decision at each exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TriggeredSwapExerciseCxxVectorTests
{
    [Fact]
    public void TriggeredSwapExerciseCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckProviderSteps("cxx/models/marketmodels/callability/triggeredswapexercise");
}
