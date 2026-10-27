// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's parametric exercise adapter vectors,
/// <c>tests/vectors/cxx/models/marketmodels/callability/parametricexerciseadapter.json</c>:
/// <see cref="ParametricExerciseAdapter"/> stepped on set curve states, its decision
/// at each exercise.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ParametricExerciseAdapterCxxVectorTests
{
    [Fact]
    public void ParametricExerciseAdapterCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckStrategySteps("cxx/models/marketmodels/callability/parametricexerciseadapter");
}
