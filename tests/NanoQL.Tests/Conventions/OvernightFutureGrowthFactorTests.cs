// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The overnight-index future's past fixings on an index whose fixing isn't a simple rate: the compounded future grows
/// each past fixing by the index's <see cref="OvernightIndex.GrowthFactor"/> and the averaged one sums its
/// <see cref="OvernightIndex.SimpleAccrual"/>, so on <see cref="Cdi"/>, whose fixings are annual rates on Business/252,
/// a seasoned future prices as one on a generic index fed the simple-equivalent fixings <c>((1 + r)^τ − 1)/τ</c>, which
/// is QuantLib's route.
/// </summary>
public class OvernightFutureGrowthFactorTests
{
    private static readonly Date Today = new(15, Month.June, 2026);
    private static readonly Calendar Brazil = new Brazil(BrazilMarket.Settlement);
    private static readonly DayCounter B252 = new Business252(Brazil);
    private static readonly YieldTermStructure Curve =
        new FlatForward(Today, 0.10, B252, Compounding.Compounded, Frequency.Annual);

    [Theory]
    [InlineData(RateAveragingType.Compound)]
    [InlineData(RateAveragingType.Simple)]
    public void ASeasonedCdiFutureIsTheGenericIndexsOnTheSimpleEquivalentFixings(RateAveragingType averaging)
    {
        // a 3-month future from 21 business days before today, every past fixing 10% a year
        Date valueDate = Brazil.Advance(Today, -21, TimeUnit.Days);
        Date maturityDate = Brazil.Advance(valueDate, new Period(3, TimeUnit.Months));
        var cdi = new Cdi(Curve);
        var generic = new OvernightIndex("CTRL", 0, new BRLCurrency(), Brazil, B252, Curve);

        // each business day spans 1/252
        double simple = (Math.Pow(1.10, 1.0 / 252) - 1.0) * 252;
        var context = new EvaluationContext { EvaluationDate = Today };
        for (Date d = valueDate; d < Today; d = Brazil.Advance(d, 1, TimeUnit.Days))
        {
            context = context with { Fixings = context.Fixings.AddFixing(cdi, d, 0.10).AddFixing(generic, d, simple) };
        }

        double onCdi = new OvernightIndexFuture(cdi, valueDate, maturityDate, null, averaging).Calculate(context).NPV!.Value;
        double onGeneric = new OvernightIndexFuture(generic, valueDate, maturityDate, null, averaging).Calculate(context).NPV!.Value;
        Assert.True(Math.Abs(onCdi - onGeneric) <= 1e-12, $"{onCdi} {onGeneric}");
    }
}
