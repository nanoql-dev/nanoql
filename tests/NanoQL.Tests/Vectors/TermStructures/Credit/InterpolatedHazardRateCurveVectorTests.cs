// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interpolated hazard-rate curve vectors,
/// <c>tests/vectors/termstructures/credit/interpolatedhazardratecurve.json</c>: <see cref="HazardRateCurve"/>'s nodes,
/// its survival probabilities, hazard rates and default densities at and between them and past the last, and its
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedHazardRateCurveVectorTests
{
    [Fact]
    public void InterpolatedHazardRateCurveVector() =>
        CreditInputs.CheckInterpolated<HazardRateCurve>(
            "termstructures/credit/interpolatedhazardratecurve", c => c.Dates(), c => c.HazardRates());
}
