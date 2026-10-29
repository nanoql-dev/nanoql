// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's tenor swaption volatility vectors, <c>tests/vectors/cxx/experimental/basismodels/tenorswaptionvts.json</c>:
/// QuantLib's case's Euribor 6M swaption volatilities transformed to other floating tenors, the volatility at option
/// and swap tenors and strikes, and the smile sections' at-the-money levels.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TenorSwaptionVTSCxxVectorTests
{
    [Fact]
    public void TenorSwaptionVTSCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/basismodels/tenorswaptionvts");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var bondBasis = new Thirty360(Thirty360Convention.BondBasis);
            var vts = new TenorSwaptionVTS(
                BasisModelInputs.SwaptionOf(c.Input("base"), context),
                BasisModelInputs.CurveOf(c.Input("discount")),
                BasisModelInputs.IndexOf(c.Input("baseIndex")),
                BasisModelInputs.IndexOf(c.Input("targIndex")),
                new Period(1, TimeUnit.Years),
                new Period(1, TimeUnit.Years),
                bondBasis,
                bondBasis,
                context);
            Period[] options = BasisModelInputs.PeriodsOf(c.Input("optionTenors"));
            Period[] swaps = BasisModelInputs.PeriodsOf(c.Input("swapTenors"));
            double[] strikes = c.Doubles("strikes");
            check.Output(c, "volatility", options.Select(o => (object?)swaps.Select(s => (object?)strikes.Select(k =>
                (Func<object?>)(() => vts.Volatility(o, s, k, true))).ToArray()).ToArray()).ToArray());
            check.Output(c, "atmLevel", options.Select(o => (object?)swaps.Select(s => (Func<object?>)(() =>
                vts.SmileSection(o, s, true).AtmLevel())).ToArray()).ToArray());
        }

        check.Complete();
    }
}
