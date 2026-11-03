// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The older CDO's maximum loss given default: the sum of the names' losses given default, which QuantLib's sums onto
/// an uninitialized value (<c>cdo.cpp:73-77</c>), so its <c>lgd()</c> is undefined; the vectors leave it out.
/// </summary>
public class OldCdoTests
{
    [Fact]
    public void ALaterCurvesProbabilityBeforeItsDateIsZero()
    {
        // the first curve dated today, the other three T+2: QuantLib refuses ("negative time (-0.00821918) given"), and
        // on the three restated from today (hazard 0 to T+2) reads -0.28223274402690984
        Date t2 = new TARGET().Advance(Today, 2, TimeUnit.Days);
        DefaultProbabilityTermStructure[] curves = [Flat(Today, 0.02), Flat(t2, 0.02), Flat(t2, 0.02), Flat(t2, 0.02)];
        var cdo = new CDO(
            0.03, 0.07, [10.0, 10.0, 10.0, 10.0], curves, new OneFactorGaussianCopula(0.3), true, FiveYears(), 0.02,
            new Actual360(), 0.4, 0.0, new FlatForward(Today, 0.05, new Actual365Fixed()), 50, new Period(1, TimeUnit.Years));
        Assert.Equal(-0.28223274402690984, cdo.Calculate(Context).NPV!.Value, 1e-13);
    }

    [Fact]
    public void TheMaximumLossIsTheSumOfTheLossesGivenDefault()
    {
        DefaultProbabilityTermStructure[] curves = [.. Enumerable.Range(0, 4).Select(_ => (DefaultProbabilityTermStructure)Flat(Today, 0.02))];
        var cdo = new CDO(
            0.03,
            0.07,
            [10.0, 20.0],
            curves,
            new OneFactorGaussianCopula(0.3),
            true,
            FiveYears(),
            0.02,
            new Actual360(),
            0.4,
            0.0,
            new FlatForward(Today, 0.05, new Actual365Fixed()),
            50);

        // the nominals repeat the last, 10, 20, 20, 20: losses given default 6, 12, 12, 12
        Assert.Equal(4, cdo.Size);
        Assert.Equal(70.0, cdo.Nominal);
        Assert.Equal(42.0, cdo.Lgd, 1e-13);
    }
}
