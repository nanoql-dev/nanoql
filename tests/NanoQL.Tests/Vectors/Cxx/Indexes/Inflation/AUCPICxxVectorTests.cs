// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Australian CPI vectors, <c>tests/vectors/cxx/indexes/inflation/aucpi.json</c>:
/// <see cref="YYAUCPI"/>, which the wheel doesn't bind, quarterly and unrevised, and monthly and revised: its
/// inspectors, reads on the fixings alone, forecasts and a clone's, and <see cref="CPI"/>'s lagged year-on-year rates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AUCPICxxVectorTests
{
    [Fact]
    public void AUCPICxxVector() => InflationIndexVectorTests.Check("cxx/indexes/inflation/aucpi");
}
