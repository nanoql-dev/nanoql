// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The UK HICP presets' vectors, <c>tests/vectors/indexes/inflation/ukhicp.json</c>, <see cref="UKHICP"/>: its
/// inspectors; its fixings stored and read back, and a conflicting one refused; reads on the fixings alone; forecasts
/// on a zero curve and a clone's on a second, one curve's base date without a fixing; and CPI's lagged observations,
/// past and forecast.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class UKHICPVectorTests
{
    [Fact]
    public void UKHICPVector() => InflationIndexVectorTests.Check("indexes/inflation/ukhicp");
}
