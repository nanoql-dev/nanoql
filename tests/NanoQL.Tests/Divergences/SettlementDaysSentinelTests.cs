// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Settlement days are an <c>int</c>, or an <c>int?</c> where QuantLib may leave them unset, so
/// <see cref="int.MaxValue"/> is a value. QuantLib reads its null sentinel, <c>Null&lt;Natural&gt;()</c>, which equals
/// <c>INT_MAX</c>, as "not given": a swap rate helper given it starts at the index's spot date
/// (<c>ratehelpers.cpp:573-575</c>), and an OIS rate helper at its swap builder's default spot
/// (<c>oisratehelper.cpp:167-169</c>), each 18 June 2025 from 16 June. nanoql counts that many days from the evaluation
/// date, and the date leaves its range.
/// </summary>
public class SettlementDaysSentinelTests
{
    [Fact]
    public void SettlementDaysOfIntMaxValueAreAValue()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };

        SwapRateHelper Helper(int? settlementDays) => new(
            0.03,
            new Period(5, TimeUnit.Years),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M(),
            context,
            settlementDays: settlementDays);

        // QuantLib's value for INT_MAX, which nanoql gives for no settlement days
        Assert.Equal(new Date(18, Month.June, 2025), Helper(null).EarliestDate);
        Assert.Throws<ArgumentOutOfRangeException>(() => Helper(int.MaxValue));
    }

    [Fact]
    public void OisSettlementDaysOfIntMaxValueAreAValue()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(16, Month.June, 2025) };

        OISRateHelper Helper(int settlementDays) =>
            new(settlementDays, new Period(1, TimeUnit.Years), 0.03, new Estr(), context);

        // QuantLib's value for INT_MAX, the swap builder's default of 2 days for ESTR
        Assert.Equal(new Date(18, Month.June, 2025), Helper(2).EarliestDate);
        Assert.Throws<ArgumentOutOfRangeException>(() => Helper(int.MaxValue));
    }
}
