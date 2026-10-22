// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The French HICP presets' vectors, <c>tests/vectors/indexes/inflation/frhicp.json</c>, <see cref="FRHICP"/> and
/// <see cref="YYFRHICP"/>: each index's inspectors; FRHICP's fixings stored and read back, and a conflicting one
/// refused; reads on the fixings alone; forecasts on a zero or year-on-year curve and a clone's on a second, one zero
/// curve's base date without a fixing; and CPI's lagged observations, past and forecast.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FRHICPVectorTests
{
    [Fact]
    public void FRHICPVector() => InflationIndexVectorTests.Check("indexes/inflation/frhicp");
}
