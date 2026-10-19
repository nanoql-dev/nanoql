// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A caplet paid between the discount curve's reference date and the evaluation date. QuantLib's arguments store
/// <c>Null&lt;Rate&gt;()</c> as the forward of a coupon paid before the evaluation date (<c>capfloor.cpp:245-249</c>),
/// and the Black and Bachelier engines price every caplet paid after the curve's reference date
/// (<c>blackcapfloorengine.cpp:93-100</c>, <c>bacheliercapfloorengine.cpp:78-86</c>), so on a curve dated before the
/// evaluation date they price that caplet on the sentinel, 3.4028234663852886e+38: the wheel's NPV of this 1Y cap is
/// 1.7146697606502935e+38, its first caplet's, on both engines. nanoql's forward is a <c>double?</c>, null there, and the
/// engines refuse a priced caplet without one, with <see cref="NanoQLException"/>. On a curve dated on the evaluation
/// date the caplet is skipped and both price it as QuantLib does: 0.010031644352251482, its ATM forward null where
/// QuantLib's is the sentinel.
/// </summary>
public class CapFloorNullForwardTests
{
    [Fact]
    public void APricedCapletWithoutAForwardIsRefused()
    {
        var today = new Date(16, Month.June, 2025);
        var cal = new TARGET();
        var dc = new Actual365Fixed();
        Date early = cal.Advance(today, -3, TimeUnit.Months);

        CapFloor CapOn(YieldTermStructure curve, out EvaluationContext context)
        {
            var index = new Euribor6M(curve);
            Date start = cal.Advance(today, -8, TimeUnit.Months);
            var schedule = new Schedule(
                start, start + new Period(1, TimeUnit.Years), new Period(6, TimeUnit.Months), cal,
                BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward,
                false, new EvaluationContext { EvaluationDate = today });
            IndexFixings fixings = IndexFixings.Empty
                .AddFixing(index, new Date(14, Month.October, 2024), 0.05)
                .AddFixing(index, new Date(14, Month.April, 2025), 0.05);
            context = new EvaluationContext { EvaluationDate = today, Fixings = fixings };
            ImmutableArray<CashFlow> leg = new IborLeg(schedule, index)
                .WithNotionals([1.0])
                .WithPaymentDayCounter(index.DayCounter)
                .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
                .ToLeg(context);
            return new Cap(leg, [0.03]);
        }

        // The first coupon pays on 16 April 2025, after the curve's 17 March and before the evaluation date.
        var curve = new FlatForward(early, 0.04, dc);
        CapFloor cap = CapOn(curve, out EvaluationContext context);
        Assert.Equal(new Date(16, Month.April, 2025), cap.FloatingLeg[0].Date);
        Assert.False(cap.IsExpired(context));

        // QuantLib's value: 1.7146697606502935e+38 on both engines.
        Assert.Equal(
            "no forward for a caplet paid before the evaluation date",
            Assert.Throws<NanoQLException>(() => BlackCapFloorEngine.Calculate(cap, context, curve, 0.2)).Message);
        Assert.Equal(
            "no forward for a caplet paid before the evaluation date",
            Assert.Throws<NanoQLException>(() => BachelierCapFloorEngine.Calculate(cap, context, curve, 0.01)).Message);
        Assert.Throws<NanoQLException>(() => cap.ImpliedVolatility(0.01, curve, 0.2, context));

        // On a curve dated today the caplet is skipped, as in QuantLib.
        var todays = new FlatForward(today, 0.04, dc);
        CapFloor skipped = CapOn(todays, out EvaluationContext todaysContext);
        InstrumentResults results = BlackCapFloorEngine.Calculate(skipped, todaysContext, todays, 0.2);
        Assert.Equal(0.010031644352251482, results.NPV);
        Assert.Equal([null, 0.05], results.Result<ImmutableArray<double?>>("optionletsAtmForward"));
    }
}
