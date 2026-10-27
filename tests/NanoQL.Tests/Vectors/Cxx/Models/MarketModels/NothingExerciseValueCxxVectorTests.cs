// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's nothing exercise value vectors,
/// <c>tests/vectors/cxx/models/marketmodels/callability/nothingexercisevalue.json</c>:
/// <see cref="NothingExerciseValue"/> stepped on set curve states, its cash flow at each
/// step.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NothingExerciseValueCxxVectorTests
{
    [Fact]
    public void NothingExerciseValueCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckExerciseValueSteps("cxx/models/marketmodels/callability/nothingexercisevalue");
}
