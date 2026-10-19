// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The option-adjusted spread family on an expired callable bond. QuantLib's <c>NPVSpreadHelper</c> sets the bond's
/// arguments up and calls its engine's <c>calculate()</c> directly (<c>callablebond.cpp:262-277</c>), which skips
/// <c>Instrument::calculate</c>'s expiry check (<c>instrument.hpp:130-139</c>), so the family prices an expired bond's
/// arguments. On a bond called at 100 in September 2019 and maturing on 9 June 2025, evaluated on 16 September 2030,
/// with Hull-White on a flat 3% curve dated 2 January 2020 and 40 steps, the wheel gives an OAS of inf for a clean
/// price of 100, a clean price of inf for a spread of 0.01, and an effective duration and convexity of NaN. nanoql's
/// family takes the engine as its <c>Calculate</c>, whose first step is the expiry check, so it would read the expired
/// results, an NPV of 0: the OAS would be the solver's guess, 0, a value a caller can't tell from an answer. nanoql
/// refuses the family on an expired bond instead, with <see cref="NanoQLException"/> and the text of QuantLib's own
/// refusal of an expired bond's implied volatility (<c>callablebond.cpp:119</c>).
/// </summary>
public class ExpiredCallableBondSpreadTests
{
    [Fact]
    public void AnExpiredBondsSpreadFamilyIsRefused()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.September, 2030) };
        var calendar = new TARGET();
        var runnerDate = new Date(16, Month.September, 2015);
        Date issue = calendar.Adjust(runnerDate - 100);
        Schedule schedule = new MakeSchedule().From(issue).To(calendar.Advance(issue, 10, TimeUnit.Years))
            .WithCalendar(calendar).WithFrequency(Frequency.Semiannual)
            .WithConvention(BusinessDayConvention.ModifiedFollowing).WithRule(DateGenerationRule.Backward).ToSchedule();
        var bond = new CallableFixedRateBond(
            3, 100.0, schedule, [0.05], new Thirty360(Thirty360Convention.BondBasis),
            BusinessDayConvention.ModifiedFollowing, 100.0, issue,
            [
                new Callability(
                    new BondPrice(100.0, BondPriceType.Clean), CallabilityType.Call,
                    calendar.Advance(runnerDate, 4, TimeUnit.Years)),
            ]);
        var curve = new FlatForward(new Date(2, Month.January, 2020), 0.03, new Actual365Fixed());
        var model = new HullWhite(curve);
        CallableBondResults Tree(CallableBond b, EvaluationContext x, double s) =>
            TreeCallableFixedRateBondEngine.Calculate(b, x, model, 40, null, s);
        var dc = new Actual365Fixed();

        Assert.Equal(new Date(9, Month.June, 2025), bond.MaturityDate());
        Assert.True(bond.IsExpired(context));
        Assert.Equal(0.0, Tree(bond, context, 0.01).NPV);

        // QuantLib 1.44: inf, inf, NaN and NaN
        Refused(() => bond.OAS(100.0, curve, dc, Compounding.Continuous, Frequency.NoFrequency, Tree, context));
        Refused(() => bond.CleanPriceOAS(
            0.01, curve, dc, Compounding.Continuous, Frequency.NoFrequency, Tree, context));
        Refused(() => bond.EffectiveDuration(
            0.01, curve, dc, Compounding.Continuous, Frequency.NoFrequency, Tree, context));
        Refused(() => bond.EffectiveConvexity(
            0.01, curve, dc, Compounding.Continuous, Frequency.NoFrequency, Tree, context));
    }

    private static void Refused(Func<double> spreadFamily)
    {
        NanoQLException e = Assert.Throws<NanoQLException>(() => spreadFamily());
        Assert.Equal(typeof(NanoQLException), e.GetType());
        Assert.Equal("instrument expired", e.Message);
    }
}
