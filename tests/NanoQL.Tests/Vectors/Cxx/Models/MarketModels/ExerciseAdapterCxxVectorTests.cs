// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's exercise adapter vectors,
/// <c>tests/vectors/cxx/models/marketmodels/products/multistep/exerciseadapter.json</c>:
/// <see cref="ExerciseAdapter"/> stepped on set curve states, each step's counts, time indices and
/// amounts.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ExerciseAdapterCxxVectorTests
{
    [Fact]
    public void ExerciseAdapterCxxVector() =>
        MarketModelProductCxxInputs.CheckSteps("cxx/models/marketmodels/products/multistep/exerciseadapter");
}
