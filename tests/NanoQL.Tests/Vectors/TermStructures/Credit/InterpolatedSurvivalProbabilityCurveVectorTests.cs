// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interpolated survival-probability curve vectors,
/// <c>tests/vectors/termstructures/credit/interpolatedsurvivalprobabilitycurve.json</c>:
/// <see cref="SurvivalProbabilityCurve"/>'s nodes, its survival probabilities, hazard rates and default densities at
/// and between them and past the last, and its refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedSurvivalProbabilityCurveVectorTests
{
    [Fact]
    public void InterpolatedSurvivalProbabilityCurveVector() =>
        CreditInputs.CheckInterpolated<SurvivalProbabilityCurve>(
            "termstructures/credit/interpolatedsurvivalprobabilitycurve", c => c.Dates(), c => c.SurvivalProbabilities());
}
