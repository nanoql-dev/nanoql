// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interpolated default-density curve vectors,
/// <c>tests/vectors/termstructures/credit/interpolateddefaultdensitycurve.json</c>: <see cref="DefaultDensityCurve"/>'s
/// nodes, its survival probabilities, hazard rates and default densities at and between them and past the last, and its
/// refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedDefaultDensityCurveVectorTests
{
    [Fact]
    public void InterpolatedDefaultDensityCurveVector() =>
        CreditInputs.CheckInterpolated<DefaultDensityCurve>(
            "termstructures/credit/interpolateddefaultdensitycurve", c => c.Dates(), c => c.DefaultDensities());
}
