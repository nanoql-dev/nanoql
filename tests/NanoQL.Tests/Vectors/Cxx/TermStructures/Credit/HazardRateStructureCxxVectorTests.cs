// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's vectors of the hazard-rate and default-density bases' default survival probability,
/// <c>tests/vectors/cxx/termstructures/credit/hazardratestructure.json</c>: the 48-point Gauss-Chebyshev integral of a
/// + b t and of c e^(−c t), on subclasses of <see cref="HazardRateStructure"/> and
/// <see cref="DefaultDensityStructure"/> like the harness's.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HazardRateStructureCxxVectorTests
{
    [Fact]
    public void HazardRateStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/credit/hazardratestructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            var reference = new Date(a[0].GetInt32());
            DefaultProbabilityTermStructure curve = c.Input("kind").GetString() == "hazard"
                ? new LinearHazard(reference, VectorCase.Real(a[1]), VectorCase.Real(a[2]))
                : new ExpDensity(reference, VectorCase.Real(a[1]));
            double[] times = [.. a[^1].EnumerateArray().Select(VectorCase.Real)];
            check.Output(c, "survival", () => times.Select(t => curve.SurvivalProbability(t)).ToArray());
        }

        check.Complete();
    }

    private sealed class LinearHazard(Date referenceDate, double a, double b)
        : HazardRateStructure(referenceDate, null, new Actual365Fixed())
    {
        public override Date MaxDate => Date.MaxDate;

        protected override double HazardRateImpl(double t) => a + (b * t);
    }

    private sealed class ExpDensity(Date referenceDate, double c)
        : DefaultDensityStructure(referenceDate, null, new Actual365Fixed())
    {
        public override Date MaxDate => Date.MaxDate;

        protected override double DefaultDensityImpl(double t) => c * Math.Exp(-c * t);
    }
}
