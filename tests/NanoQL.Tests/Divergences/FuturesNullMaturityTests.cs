// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A custom futures helper built from dates with no end date keeps a null maturity, as QuantLib's does
/// (<c>ratehelpers.cpp:126-128</c>), and the bootstraps drop it as expired; but its implied quote refuses the null
/// maturity before it reads the curve, with the term structure's text for a date before its reference date. QuantLib
/// computes a year fraction from the null date (<c>:133</c>), which a day counter reading the month or the day reads
/// out of bounds, and its implied quote discounts to the null date (<c>:159-160</c>), which a curve whose day counter
/// reads serial numbers alone refuses as a "negative time" and one reading the month or the day reads out of bounds:
/// a result C++ leaves undefined, and a text nanoql can't form from a null date.
/// </summary>
public class FuturesNullMaturityTests
{
    [Fact]
    public void ACustomFuturesHelperWithNoEndDateRefusesItsImpliedQuote()
    {
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };
        var helper = new FuturesRateHelper(
            96.5, new Date(20, Month.February, 2024), null, new Actual360(), 0.0, FuturesType.Custom);

        // kept, as QuantLib keeps it, with a null maturity
        Assert.Null(helper.MaturityDate);
        Assert.Null(helper.PillarDate);

        var curve = new FlatForward(today, 0.03, new Actual360());
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => helper.SetTermStructure(curve).ImpliedQuote(context));
        Assert.Equal("date (null date) before reference date (January 15th, 2024)", e.Message);
    }
}
