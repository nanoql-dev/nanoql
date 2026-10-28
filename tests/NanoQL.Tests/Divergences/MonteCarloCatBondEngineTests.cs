// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo cat bond engine on a simulation of no path (QuantLib's #2839, taken from its master after the pin):
/// refused ("no Monte Carlo paths were generated"), where 1.44 divides 0 by 0. On <c>testCatBondInDoomScenario</c>'s
/// floating cat bond (22 November 2004, the index on a 2.5% curve, discounted on 3%) and an event set of no events
/// covering 2000 to 2003, which ends before the bond's first period, QuantLib 1.44 reads NPV and loss probability NaN
/// (the S-xcr run's <c>c4_facts.cpp</c>).
/// </summary>
public class MonteCarloCatBondEngineTests
{
    [Fact]
    public void ASimulationOfNoPathIsRefused()
    {
        var today = new Date(22, November, 2004);
        var context = new EvaluationContext { EvaluationDate = today };
        var riskFree = new FlatForward(today, 0.025, new Actual360());
        var discount = new FlatForward(today, 0.03, new Actual360());
        var schedule = new Schedule(
            new Date(30, November, 2004), new Date(30, November, 2008), new Period(Frequency.Semiannual),
            new UnitedStates(UnitedStatesMarket.GovernmentBond), BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward, false, context);
        var bond = CouponPricerFunctions.SetCouponPricer(
            new FloatingCatBond(
                1, 1000000.0, schedule, new USDLibor(new Period(6, TimeUnit.Months), riskFree), new ActualActual(ActualActualConvention.ISMA),
                new DigitalNotionalRisk(new NoOffset(), 100), context, BusinessDayConvention.ModifiedFollowing, 1, [], [], [], [], false, 100.0,
                new Date(30, November, 2004)),
            new BlackIborCouponPricer(context));
        var events = new EventSet([], new Date(1, January, 2000), new Date(31, December, 2003));

        var e = Assert.Throws<NanoQLException>(() => MonteCarloCatBondEngine.Calculate(bond, context, events, discount));
        Assert.Equal("no Monte Carlo paths were generated", e.Message);
    }
}
