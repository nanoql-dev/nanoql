// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The South African CPI presets' vectors, <c>tests/vectors/indexes/inflation/zacpi.json</c>, <see cref="ZACPI"/> and
/// <see cref="YYZACPI"/>: each index's inspectors; ZACPI's fixings stored and read back, and a conflicting one refused;
/// reads on the fixings alone; forecasts on a zero or year-on-year curve and a clone's on a second, one zero curve's
/// base date without a fixing; and CPI's lagged observations, past and forecast.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZACPIVectorTests
{
    [Fact]
    public void ZACPIVector() => InflationIndexVectorTests.Check("indexes/inflation/zacpi");
}
