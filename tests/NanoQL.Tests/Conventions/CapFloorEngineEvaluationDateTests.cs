// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The cap and floor engines on a volatility value read the context's evaluation date: QuantLib's constructors on a
/// volatility build <c>ConstantOptionletVolatility(0, NullCalendar(), Following, vol, dc)</c>, whose reference date
/// moves with the evaluation date (<c>blackcapfloorengine.cpp:33-52</c>, <c>bacheliercapfloorengine.cpp:31-46</c>), and
/// its reference date is the engine's "today", from which the caplets' standard deviations count (playbook §2.2: a
/// function that reads the evaluation date through a callee takes the context). So the overload on a
/// <see cref="double"/> equals the structure overload on that volatility dated on the context's evaluation date, and an
/// instrument priced under a later evaluation date, on the same curve and fixings, has shorter standard deviations.
/// The implied volatility's engine reads it alike.
/// </summary>
public class CapFloorEngineEvaluationDateTests
{
    [Fact]
    public void AVolatilityValueIsDatedOnTheEvaluationDate()
    {
        var today = new Date(16, Month.June, 2025);
        var later = new Date(16, Month.July, 2025);
        var curve = new FlatForward(today, 0.04, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var context = new EvaluationContext { EvaluationDate = today };
        var schedule = new Schedule(
            new Date(18, Month.September, 2025), new Date(18, Month.September, 2028), new Period(6, TimeUnit.Months),
            new TARGET(), BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward, false, context);
        ImmutableArray<CashFlow> leg = new IborLeg(schedule, index).WithNotionals([1.0]).ToLeg(context);
        var cap = new Cap(leg, [0.04]);

        foreach (Date evaluationDate in (Date[])[today, later])
        {
            var c = new EvaluationContext { EvaluationDate = evaluationDate };
            OptionletVolatilityStructure dated = new ConstantOptionletVolatility(
                evaluationDate, new NullCalendar(), BusinessDayConvention.Following, 0.2, new Actual365Fixed());
            Assert.Equal(BlackCapFloorEngine.Calculate(cap, c, curve, dated), BlackCapFloorEngine.Calculate(cap, c, curve, 0.2));
            Assert.Equal(BachelierCapFloorEngine.Calculate(cap, c, curve, 0.01).NPV, BachelierCapFloorEngine.Calculate(
                cap, c, curve, new ConstantOptionletVolatility(evaluationDate, new NullCalendar(), BusinessDayConvention.Following, 0.01, new Actual365Fixed(), VolatilityType.Normal)).NPV);
        }

        ImmutableArray<double> stdDevsToday = BlackCapFloorEngine.Calculate(cap, context, curve, 0.2).Result<ImmutableArray<double>>("optionletsStdDev");
        ImmutableArray<double> stdDevsLater = BlackCapFloorEngine.Calculate(cap, context with { EvaluationDate = later }, curve, 0.2)
            .Result<ImmutableArray<double>>("optionletsStdDev");
        for (int i = 0; i < stdDevsToday.Length; i++)
        {
            Assert.True(stdDevsLater[i] < stdDevsToday[i], $"caplet {i}: {stdDevsLater[i]} >= {stdDevsToday[i]}");
        }

        // The implied volatility prices on a volatility dated on the context's evaluation date: the later price, solved
        // under the later context, gives back the volatility it was priced at.
        var laterContext = context with { EvaluationDate = later };
        double laterPrice = BlackCapFloorEngine.Calculate(cap, laterContext, curve, 0.2).NPV!.Value;
        Assert.Equal(0.2, cap.ImpliedVolatility(laterPrice, curve, 0.1, laterContext, 1e-12), 1e-10);
        Assert.NotEqual(0.2, cap.ImpliedVolatility(laterPrice, curve, 0.1, context, 1e-12), 1e-6);
    }
}
