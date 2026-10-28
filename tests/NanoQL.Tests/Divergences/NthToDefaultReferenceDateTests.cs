// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The n-th-to-default engine reads a default probability before a default curve's reference date as 0, and a discount
/// before the discount curve's as 1 (the S-xcr plan's XR8 (x), S-core's CR7 (xxxiv) class): on default curves dated T+2
/// QuantLib's reads at the evaluation date refuse ("negative time (-0.00821918) given"). QuantLib's values are the S-xcr
/// run's <c>c1_facts.cpp</c>; the check is QuantLib on the market restated from the evaluation date: each curve a hazard
/// curve of rate 0 to T+2 and the T+2 curve's rate after it.
/// </summary>
public class NthToDefaultReferenceDateTests
{
    private static readonly double[] Hazards = [0.01, 0.02, 0.03];

    [Theory]
    [InlineData(1, 0.03187023338496861)]
    [InlineData(2, 0.005180698713944951)]
    [InlineData(3, 0.00057068537790630448)]
    public void DefaultCurvesDatedAfterTheEvaluationDateReadNoDefaultBeforeIt(int n, double restated)
    {
        // QuantLib, T+2 curves: every order throws "negative time (-0.00821918) given"
        Date t2 = new TARGET().Advance(Today, 2, TimeUnit.Days);

        double onT2 = Ntd(Basket(Hazards.Select(h => (DefaultProbabilityTermStructure)Flat(t2, h))), n, 30.0).FairPremium!.Value;
        double onRestated = Ntd(Basket(Hazards.Select(h => (DefaultProbabilityTermStructure)new HazardRateCurve(
            [Today, t2, Today + new Period(20, TimeUnit.Years)], [0.0, 0.0, h], new Actual365Fixed()))), n, 30.0).FairPremium!.Value;

        Assert.Equal(restated, onRestated, 1e-10 * restated);
        Assert.Equal(onRestated, onT2, 1e-10 * restated);
    }

    private static Basket Basket(IEnumerable<DefaultProbabilityTermStructure> curves) =>
        new Basket(Today, ["A", "B", "C"], [10.0, 10.0, 10.0], PoolOf(["A", "B", "C"], [.. curves]), Context)
            .SetLossModel(Constant(3, 0.3));
}
