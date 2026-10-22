// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Australian CPI presets' vectors, <c>tests/vectors/indexes/inflation/aucpi.json</c>, <see cref="AUCPI"/>: AUCPI
/// quarterly and unrevised, and monthly and revised: each one's inspectors; fixings stored and read back, and a
/// conflicting one refused; reads on the fixings alone; forecasts on a zero curve and a clone's on a second; and CPI's
/// lagged observations, past and forecast.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AUCPIVectorTests
{
    [Fact]
    public void AUCPIVector() => InflationIndexVectorTests.Check("indexes/inflation/aucpi");
}
