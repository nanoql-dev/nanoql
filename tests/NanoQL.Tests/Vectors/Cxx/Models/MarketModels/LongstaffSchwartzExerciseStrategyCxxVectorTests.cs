// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Longstaff-Schwartz strategy vectors,
/// <c>tests/vectors/cxx/models/marketmodels/callability/lsstrategy.json</c>:
/// <see cref="LongstaffSchwartzExerciseStrategy"/> stepped on set curve states, its
/// decision at each exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LongstaffSchwartzExerciseStrategyCxxVectorTests
{
    [Fact]
    public void LongstaffSchwartzExerciseStrategyCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckStrategySteps("cxx/models/marketmodels/callability/lsstrategy");
}
