// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's tenor optionlet volatility vectors, <c>tests/vectors/cxx/experimental/basismodels/tenoroptionletvts.json</c>:
/// QuantLib's case's caplet volatilities transformed to a longer tenor at its two correlations, the volatility at terms
/// and strikes, and the smile sections' at-the-money levels and strike ranges.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TenorOptionletVTSCxxVectorTests
{
    [Fact]
    public void TenorOptionletVTSCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/basismodels/tenoroptionletvts");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            double rho = c.Input("correlation").GetProperty("rhoInf").GetDouble();
            double beta = c.Input("correlation").GetProperty("beta").GetDouble();
            double[] times = [0.0, 50.0];
            var corr = new TwoParameterCorrelation(
                default(Linear).Interpolate(times, [rho, rho]), default(Linear).Interpolate(times, [beta, beta]));
            var vts = new TenorOptionletVTS(
                BasisModelInputs.OptionletOf(c.Input("base"), context),
                BasisModelInputs.IndexOf(c.Input("baseIndex")),
                BasisModelInputs.IndexOf(c.Input("targIndex")),
                corr,
                context);
            Period[] tenors = BasisModelInputs.PeriodsOf(c.Input("tenors"));
            double[] strikes = c.Doubles("strikes");
            check.Output(c, "volatility", tenors.Select(p => (object?)strikes.Select(k => (Func<object?>)(() =>
                vts.Volatility(p, k, true))).ToArray()).ToArray());
            check.Output(c, "sectionVolatility", tenors.Select(p => (object?)strikes.Select(k => (Func<object?>)(() =>
                vts.SmileSection(vts.OptionDateFromTenor(p), true).Volatility(k))).ToArray()).ToArray());
            check.Output(c, "atmLevel", tenors.Select(p => (Func<object?>)(() =>
                vts.SmileSection(vts.OptionDateFromTenor(p), true).AtmLevel())).ToArray());
            check.Output(c, "minStrike", tenors.Select(p => (Func<object?>)(() =>
                vts.SmileSection(vts.OptionDateFromTenor(p), true).MinStrike())).ToArray());
            check.Output(c, "maxStrike", tenors.Select(p => (Func<object?>)(() =>
                vts.SmileSection(vts.OptionDateFromTenor(p), true).MaxStrike())).ToArray());
        }

        check.Complete();
    }
}
