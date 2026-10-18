// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An optional real input is a <c>double?</c>, whose <see langword="null"/> is "not given", so a value equal to
/// QuantLib's null sentinel, <c>Null&lt;Real&gt;()</c> (3.4028234663852886e38, the largest <c>float</c>), is a value.
/// QuantLib reads its sentinel as "not given" wherever a real input defaults to it. Each fact is one such input.
/// </summary>
public class NullRealSentinelTests
{
    private const double NullReal = 3.4028234663852886e38;

    /// <summary>
    /// An iterative bootstrap's bound: QuantLib takes the traits' bound for a <c>maxValue</c> of the sentinel
    /// (<c>iterativebootstrap.hpp:276-279</c>), so its curve is the default's, 0.8821578172517457 at four years. nanoql
    /// searches up to that value, and its solver lands elsewhere within the accuracy.
    /// </summary>
    [Fact]
    public void AnIterativeBootstrapsBoundOfQuantLibsNullIsAValue()
    {
        var today = new Date(16, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var helpers = new List<RateHelper>
        {
            new DepositRateHelper(
                0.03, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
                new Actual360(), context),
        };
        foreach ((int years, double rate) in new[] { (2, 0.031), (5, 0.032) })
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
        var byDefault = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(today, helpers, dc, context);
        var bounded = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            today, helpers, dc, context, bootstrap: new IterativeBootstrap(maxValue: NullReal));

        // QuantLib's value for both
        Assert.Equal(0.8821578172517457, byDefault.Discount(4.0));
        Assert.Equal(0.8821578172517482, bounded.Discount(4.0));
    }
}
