// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The FRA, which QuantLib prices in the instrument with no engine, computes in
/// <see cref="ForwardRateAgreement.Calculate"/>: each call computes its results again from its context, nothing
/// cached, and the expiry reads the context's flags, as QuantLib's <c>simple_event(valueDate_).hasOccurred()</c> reads
/// the Settings' (<c>forwardrateagreement.cpp:70-72</c>). The values are QuantLib's on the wheel for a 6M Euribor FRA,
/// value date 17 June 2026, strike 2.5% on 1 million.
/// </summary>
public class ForwardRateAgreementCalculateTests
{
    private static readonly Date ValueDate = new(17, June, 2026);
    private static readonly Date FixingDate = new(15, June, 2026);

    [Fact]
    public void EachCallComputesFromItsContext()
    {
        var curve = new FlatForward(FixingDate, 0.03, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var fra = new ForwardRateAgreement(index, ValueDate, PositionType.Long, 0.025, 1e6, curve);
        Assert.Equal(FixingDate, fra.FixingDate());

        var context = new EvaluationContext { EvaluationDate = FixingDate };
        ForwardRateAgreementResults forecast = fra.Calculate(context);

        // today's fixing published: the indexed coupon's rate is it (QuantLib: amount() 3002.6827247294705, NPV()
        // 3002.18917361565)
        EvaluationContext published = context with
        {
            Fixings = context.Fixings.AddFixing(index, FixingDate, 0.031),
        };
        ForwardRateAgreementResults fixedRate = fra.Calculate(published);
        Assert.Equal(0.031, fixedRate.ForwardRate!.Rate);
        Assert.Equal(3002.6827247294705, fixedRate.Amount);
        Assert.Equal(3002.18917361565, fixedRate.NPV);

        // the first context's results again, computed anew
        Assert.NotEqual(fixedRate.ForwardRate.Rate, forecast.ForwardRate!.Rate);
        ForwardRateAgreementResults again = fra.Calculate(context);
        Assert.NotSame(forecast, again);
        Assert.Equal(forecast, again);
    }

    [Fact]
    public void ExpiryReadsTheContextsFlags()
    {
        var curve = new FlatForward(FixingDate, 0.03, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var fra = new ForwardRateAgreement(index, ValueDate, PositionType.Long, 0.025, 1e6, curve);

        // on the value date: expired, as QuantLib's Settings default counts the reference date's events as occurred,
        // and live when they count as not yet occurred
        var onValueDate = new EvaluationContext
        {
            EvaluationDate = ValueDate,
            Fixings = IndexFixings.Empty.AddFixing(index, FixingDate, 0.031),
        };
        Assert.True(fra.IsExpired(onValueDate));
        Assert.False(fra.IsExpired(onValueDate with { IncludeReferenceDateEvents = true }));
        Assert.False(fra.IsExpired(new EvaluationContext { EvaluationDate = ValueDate - 1 }));
        Assert.True(fra.IsExpired(new EvaluationContext { EvaluationDate = ValueDate + 1 }));
        Assert.Equal(0.0, fra.Calculate(onValueDate).NPV);
        Assert.Equal(3002.18917361565, fra.Calculate(onValueDate with { IncludeReferenceDateEvents = true }).NPV);
    }
}
