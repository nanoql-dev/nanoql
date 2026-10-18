// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A null helper among a curve's instruments is refused when the curve is built, with
/// <see cref="ArgumentException"/> naming the instruments. QuantLib's bootstrap sorts its helpers by their pillar dates
/// (<c>iterativebootstrap.hpp:158-159</c>, <c>bootstraphelper.hpp:251-255</c>), dereferencing a null one, a result C++
/// leaves undefined; the wheel's helper vector refuses <c>None</c> before QuantLib sees it.
/// </summary>
public class BootstrapNullHelperTests
{
    [Fact]
    public void ANullHelperAmongACurvesInstrumentsIsRefused()
    {
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        var deposit = new DepositRateHelper(
            0.03, new Period(6, TimeUnit.Months), 2, new TARGET(), BusinessDayConvention.ModifiedFollowing, false,
            new Actual360(), context);

        ArgumentException e = Assert.Throws<ArgumentException>(
            () => new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                today, [deposit, null!], new Actual365Fixed(), context));
        Assert.Equal("instruments", e.ParamName);
        Assert.StartsWith("null rate helper", e.Message, StringComparison.Ordinal);
    }
}
