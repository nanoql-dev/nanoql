// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.TestSuite;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The tenor optionlet structure's range checked once, by the query (the S-xrt run's R2-4, fixed under R8). QuantLib's
/// <c>volatilityImpl</c> builds the section through <c>smileSection(optionTime)</c>, which checks the range again without
/// the caller's extrapolation flag (<c>tenoroptionletvts.hpp</c>), so a query allowed to extrapolate past the base
/// structure's last date refuses. On QuantLib 1.44 (the run's <c>c2\probes\xd_c2.py</c> through the harness:
/// test-suite/basismodels.cpp's caplet volatilities on 29 October 2026, the case's indexes, ρ∞ 0.3 and β 0.9) the
/// 30-year caplet's volatility with extrapolation refuses, "time (30.0219) is past max curve time (30.0137)", where its
/// smile section built with extrapolation gives 0.0059574843004036775, 0.006438206493492846 and 0.006675293658033079 at
/// 0, 1% and 2%; at 25 years the two agree. nanoql's volatility is that section's. QuantLib's own
/// <c>testTenoroptionletvts</c> asks for its 30-year caplets so and refuses on every evaluation date of 29 October to 11
/// November 2026 but the two Wednesdays (<c>xb_tenor_d.cpp</c>); on its test runner's date, 16 September 2015, a
/// Wednesday, it passes, so QuantLib's own run doesn't refuse.
/// </summary>
public class TenorOptionletExtrapolationTests
{
    [Theory]
    [InlineData(0.0, 0.0059574843004036775)]
    [InlineData(0.01, 0.006438206493492846)]
    [InlineData(0.02, 0.006675293658033079)]
    public void AThirtyYearCapletExtrapolatesToItsSmileSection(double strike, double quantLibSection)
    {
        var context = new EvaluationContext { EvaluationDate = new Date(29, Month.October, 2026) };
        YieldTermStructure proj3m = BasisModelsTests.GetYTS(Terms(), Proj3mRates(), context);
        YieldTermStructure proj6m = BasisModelsTests.GetYTS(Terms(), Proj3mRates(), context, 0.01);
        double[] times = [0.0, 50.0];
        var corr = new TwoParameterCorrelation(
            default(Linear).Interpolate(times, [0.3, 0.3]), default(Linear).Interpolate(times, [0.9, 0.9]));
        var vts = new TenorOptionletVTS(
            BasisModelsTests.GetOptionletTS(context), new Euribor6M(proj3m), new Euribor6M(proj6m), corr, context);
        var thirty = new Period(30, TimeUnit.Years);
        Assert.Equal(quantLibSection, vts.Volatility(thirty, strike, true), 1e-15);
        Assert.Equal(vts.SmileSection(vts.OptionDateFromTenor(thirty), true).Volatility(strike), vts.Volatility(thirty, strike, true));
        Assert.Throws<NanoQLException>(() => vts.Volatility(thirty, strike));
    }

    private static Period[] Terms() =>
    [
        new(0, TimeUnit.Days), new(1, TimeUnit.Years), new(2, TimeUnit.Years), new(3, TimeUnit.Years),
        new(5, TimeUnit.Years), new(7, TimeUnit.Years), new(10, TimeUnit.Years), new(15, TimeUnit.Years),
        new(20, TimeUnit.Years), new(61, TimeUnit.Years),
    ];

    private static double[] Proj3mRates() =>
    [
        -0.000483439, -0.000578569, -0.000383832, 0.000272656, 0.002478699, 0.005100113, 0.008750643, 0.012788095,
        0.014534052, 0.014942896,
    ];
}
