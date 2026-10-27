// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's early-exercise optimisation vectors,
/// <c>tests/vectors/cxx/methods/montecarlo/parametricexercise.json</c>:
/// <see cref="ParametricExerciseFunctions.GenericEarlyExerciseOptimization"/> on collected
/// node data, its estimate, parameters and first row's cash flows.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ParametricExerciseCxxVectorTests
{
    [Fact]
    public void ParametricExerciseCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckRegressionOrOptimization("cxx/methods/montecarlo/parametricexercise");
}
