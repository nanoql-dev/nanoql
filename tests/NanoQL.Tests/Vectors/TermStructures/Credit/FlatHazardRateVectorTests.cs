// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The flat hazard rate vectors, <c>tests/vectors/termstructures/credit/flathazardrate.json</c>:
/// <see cref="FlatHazardRate"/> on a reference date and after settlement days, its dates, hazard rates, probabilities
/// and densities.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FlatHazardRateVectorTests
{
    private static readonly double[] Times = [0.0, 0.25, 1.0, 2.5, 10.0, 30.0];

    [Fact]
    public void FlatHazardRateVector()
    {
        var file = VectorFile.Load("termstructures/credit/flathazardrate");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            DefaultProbabilityTermStructure curve = CreditInputs.CurveOf(c.Input("curve"), c.Context());
            check.Output(c, "referenceDate", curve.ReferenceDate);
            check.Output(c, "maxDate", curve.MaxDate);
            check.Output(c, "hazardRate", Times.Select(t => (object?)curve.HazardRate(t)));
            check.Output(c, "survivalProbability", Times.Select(t => (object?)curve.SurvivalProbability(t)));
            check.Output(c, "defaultProbability", Times.Select(t => (object?)curve.DefaultProbability(t)));
            check.Output(c, "defaultDensity", Times.Select(t => (object?)curve.DefaultDensity(t)));
            check.Output(
                c, "survivalAfterAYear", curve.SurvivalProbability(curve.ReferenceDate + new Time.Period(1, Time.TimeUnit.Years)));
        }

        check.Complete();
    }
}
