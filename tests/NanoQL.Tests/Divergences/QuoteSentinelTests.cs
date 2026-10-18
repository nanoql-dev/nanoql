// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A quote is a <see langword="double"/>, so a quote equal to QuantLib's null sentinel, <c>Null&lt;Real&gt;()</c>
/// (3.4028234663852886e38, the largest <c>float</c>), is a value. QuantLib's quotes are <c>SimpleQuote</c>s, and a
/// <c>SimpleQuote</c> holding the sentinel is invalid: a bootstrap refuses a helper quoting it ("has an invalid quote",
/// <c>iterativebootstrap.hpp:236-240</c>), and a curve refuses such a jump ("invalid 1st jump quote",
/// <c>yieldtermstructure.cpp:86-87</c>). nanoql has no invalid quote: a helper quoting it is bootstrapped, which fails
/// in the solver as any unreachable quote does, and such a jump is applied.
/// </summary>
public class QuoteSentinelTests
{
    private const double NullReal = 3.4028234663852886e38;

    [Fact]
    public void AQuoteEqualToQuantLibsNullIsAValue()
    {
        var today = new Date(16, Month.June, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var deposit = new DepositRateHelper(
            NullReal, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
            new Actual360(), context);

        // QuantLib: "1st instrument (maturity: December 18th, 2025, pillar: December 18th, 2025) has an invalid quote"
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                today, [deposit], new Actual365Fixed(), context));
        Assert.StartsWith("1st iteration: failed at 1st alive instrument", e.Message, StringComparison.Ordinal);

        // QuantLib: "invalid 1st jump quote"
        Date[] dates = [today, new Date(15, Month.June, 2030)];
        double[] discounts = [1.0, 0.85];
        var withJump = new InterpolatedDiscountCurve<LogLinear>(
            dates, discounts, new Actual365Fixed(), new TARGET(), [NullReal], [new Date(31, Month.December, 2025)]);
        var withoutJump = new InterpolatedDiscountCurve<LogLinear>(dates, discounts, new Actual365Fixed(), new TARGET());
        Assert.Equal(NullReal * withoutJump.Discount(2.0), withJump.Discount(2.0));
    }
}
