// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The FRA after its value date (the S-rx plan's SRXR3 (vii)). QuantLib's <c>setupExpired</c> computes the forward rate
/// (<c>forwardrateagreement.cpp:84-87</c>): under the par approximation it reads the curve at the value date, before a
/// curve dated on the evaluation date, and throws "negative time (-0.00277778) given"; and <c>amount()</c> then returns
/// <c>amount_</c>, which nothing computed (<c>:74-77</c>; the wheel read 0.0 for a FRA first priced after its value
/// date) or which a live calculation left. nanoql's expired results carry QuantLib's zero NPV; for the indexed coupon,
/// the forward rate is its fixing and the amount QuantLib's own <c>calculateAmount</c> on it (<c>:110-117</c>), which
/// reads only the fixing, the strike, the notional and the two dates, so it equals QuantLib's <c>amount()</c> on the
/// fixing date, live with the fixing published; for the par approximation, whose curve can't reach the date, both are
/// null.
/// </summary>
public class ForwardRateAgreementExpiredTests
{
    private static readonly Date ValueDate = new(17, June, 2026);

    [Fact]
    public void AnExpiredIndexedFraCarriesItsFixingsAmount()
    {
        var curve = new FlatForward(new Date(15, June, 2026), 0.03, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var fra = new ForwardRateAgreement(index, ValueDate, PositionType.Long, 0.025, 1e6, curve);
        var context = new EvaluationContext
        {
            EvaluationDate = new Date(20, June, 2026),
            Fixings = IndexFixings.Empty.AddFixing(index, fra.FixingDate(), 0.031),
        };
        Assert.True(fra.IsExpired(context));

        ForwardRateAgreementResults results = fra.Calculate(context);
        Assert.Equal(0.0, results.NPV);
        Assert.Equal(0.031, results.ForwardRate!.Rate);

        // QuantLib's live amount() on 15 June, the fixing published: 3002.6827247294705
        Assert.Equal(3002.6827247294705, results.Amount);
    }

    [Fact]
    public void AnExpiredParFraHasNeitherAmountNorRate()
    {
        var today = new Date(18, June, 2026);
        var curve = new FlatForward(today, 0.03, new Actual360());
        var fra = new ForwardRateAgreement(
            new Euribor6M(curve), ValueDate, new Date(17, December, 2026), PositionType.Long, 0.025, 1e6, curve);
        var context = new EvaluationContext { EvaluationDate = today };
        Assert.True(fra.IsExpired(context));

        // QuantLib: NPV(), amount() and forwardRate() throw "negative time (-0.00277778) given"
        ForwardRateAgreementResults results = fra.Calculate(context);
        Assert.Equal(0.0, results.NPV);
        Assert.Null(results.Amount);
        Assert.Null(results.ForwardRate);
    }
}
