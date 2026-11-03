// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's affine hazard-rate curve vectors,
/// <c>tests/vectors/cxx/experimental/credit/interpolatedaffinehazardratecurve.json</c>, on Vasicek models whose x0
/// squares to the initial intensity: <see cref="InterpolatedAffineHazardRateCurve{T}"/>'s reads, linear and backward
/// flat, and the piecewise curve on <see cref="AffineHazardRate"/> bootstrapped on spread helpers.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterpolatedAffineHazardRateCurveCxxVectorTests
{
    [Fact]
    public void InterpolatedAffineHazardRateCurveCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/interpolatedaffinehazardratecurve");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            EvaluationContext context = c.Context();
            OneFactorAffineModel model = OneFactorAffineSurvivalStructureCxxVectorTests.ModelOf(a[0]);
            if (c.Input("kind").GetString() == "curve")
            {
                Date[] dates = [.. a[1].EnumerateArray().Select(e => new Date(e.GetInt32()))];
                double[] rates = [.. a[2].EnumerateArray().Select(VectorCase.Real)];
                OneFactorAffineSurvivalStructure curve = a[3].GetString() == "Linear"
                    ? new InterpolatedAffineHazardRateCurve<Linear>(dates, rates, new Actual365Fixed(), model)
                    : new InterpolatedAffineHazardRateCurve<BackwardFlat>(dates, rates, new Actual365Fixed(), model);
                OneFactorAffineSurvivalStructureCxxVectorTests.CheckReads(check, c, curve, a[4], a[5]);
                check.Output(c, "maxDate", curve.MaxDate);
                continue;
            }

            CdsHelper[] helpers = [.. a[2].EnumerateArray().Select(h => CreditInputs.HelperOf(h, context))];
            double[] times = [.. a[5].EnumerateArray().Select(VectorCase.Real)];
            var piecewise = new PiecewiseDefaultCurve<AffineHazardRate, BackwardFlat, IterativeBootstrap>(
                new Date(a[4].GetInt32()), helpers, new Actual365Fixed(), model, context);
            check.Output(c, "dates", piecewise.Dates());
            check.Output(c, "data", piecewise.Data());
            check.Output(c, "survivalProbability", times.Select(t => piecewise.SurvivalProbability(t, true)).ToArray());
        }

        check.Complete();
    }
}
