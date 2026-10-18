// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A bootstrap is a configuration struct whose zero value is QuantLib's default construction (playbook §2.6):
/// <c>default(IterativeBootstrap)</c>, a curve's default argument, builds the curve
/// <c>new IterativeBootstrap()</c> builds, with QuantLib's defaults (accuracy and bounds not given, one attempt, factors
/// of 2, ten steps, a hundred evaluations); <c>default(GlobalBootstrap)</c> the curve <c>new GlobalBootstrap()</c>
/// builds, with QuantLib's (the curve's accuracy, Levenberg–Marquardt and end criteria of 1000 iterations and 10
/// stationary ones at it, no weights, helpers, dates, penalties or guess function).
/// </summary>
public class BootstrapZeroValueTests
{
    [Fact]
    public void AnIterativeBootstrapsZeroValueIsQuantLibsDefault()
    {
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        var helpers = new List<RateHelper>
        {
            new DepositRateHelper(
                0.030, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), context),
        };
        foreach ((int years, double rate) in new[] { (2, 0.031), (5, 0.032), (10, 0.033) })
        {
            helpers.Add(new SwapRateHelper(
                rate,
                new Period(years, TimeUnit.Years),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.Unadjusted,
                new Thirty360(Thirty360Convention.BondBasis),
                new Euribor6M(),
                context));
        }

        var dc = new Actual365Fixed();
        var byDefault = new PiecewiseYieldCurve<ZeroYield, Cubic, IterativeBootstrap>(today, helpers, dc, context);
        var zero = new PiecewiseYieldCurve<ZeroYield, Cubic, IterativeBootstrap>(
            today, helpers, dc, context, bootstrap: default);
        var constructed = new PiecewiseYieldCurve<ZeroYield, Cubic, IterativeBootstrap>(
            today, helpers, dc, context, bootstrap: new IterativeBootstrap());
        var explicitDefaults = new PiecewiseYieldCurve<ZeroYield, Cubic, IterativeBootstrap>(
            today,
            helpers,
            dc,
            context,
            bootstrap: new IterativeBootstrap(null, null, null, 1, 2.0, 2.0, false, 10, 100));

        Assert.Equal(constructed.Nodes(), byDefault.Nodes());
        Assert.Equal(constructed.Nodes(), zero.Nodes());
        Assert.Equal(constructed.Nodes(), explicitDefaults.Nodes());
        Assert.Equal(new IterativeBootstrap(null, null, null, 1, 2.0, 2.0, false, 10, 100), default);
    }

    [Fact]
    public void AGlobalBootstrapsZeroValueIsQuantLibsDefault()
    {
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        var helpers = new List<RateHelper>
        {
            new DepositRateHelper(
                0.030, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), context),
        };
        foreach ((int years, double rate) in new[] { (2, 0.031), (5, 0.032), (10, 0.033) })
        {
            helpers.Add(new SwapRateHelper(
                rate,
                new Period(years, TimeUnit.Years),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.Unadjusted,
                new Thirty360(Thirty360Convention.BondBasis),
                new Euribor6M(),
                context));
        }

        var dc = new Actual365Fixed();
        var byDefault = new PiecewiseYieldCurve<ZeroYield, Linear, GlobalBootstrap>(today, helpers, dc, context);
        var zero = new PiecewiseYieldCurve<ZeroYield, Linear, GlobalBootstrap>(
            today, helpers, dc, context, bootstrap: default);
        var constructed = new PiecewiseYieldCurve<ZeroYield, Linear, GlobalBootstrap>(
            today, helpers, dc, context, bootstrap: new GlobalBootstrap());
        var explicitDefaults = new PiecewiseYieldCurve<ZeroYield, Linear, GlobalBootstrap>(
            today,
            helpers,
            dc,
            context,
            bootstrap: new GlobalBootstrap(
                1e-12, new LevenbergMarquardt(1e-12, 1e-12, 1e-12), new EndCriteria(1000, 10, 1e-12, 1e-12, 1e-12)));

        Assert.Equal(constructed.Nodes(), byDefault.Nodes());
        Assert.Equal(constructed.Nodes(), zero.Nodes());
        Assert.Equal(constructed.Nodes(), explicitDefaults.Nodes());
        Assert.Equal(new GlobalBootstrap(null, null, null, null, null, null, null, null), default);
    }
}
