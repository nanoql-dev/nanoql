// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Bermudan swaption exercise value vectors,
/// <c>tests/vectors/cxx/models/marketmodels/callability/bermudanswaptionexercisevalue.json</c>:
/// <see cref="BermudanSwaptionExerciseValue"/> stepped on set curve states, its cash flow
/// at each step.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BermudanSwaptionExerciseValueCxxVectorTests
{
    [Fact]
    public void BermudanSwaptionExerciseValueCxxVector() =>
        MarketModelCallabilityCxxInputs.CheckExerciseValueSteps("cxx/models/marketmodels/callability/bermudanswaptionexercisevalue");
}
