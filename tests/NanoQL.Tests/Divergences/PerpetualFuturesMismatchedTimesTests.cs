// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The perpetual futures engine on a curve dated apart (the playbook's row for structures on another clock). QuantLib
/// reads its curves at the grid's times, the instrument's day counter's from the evaluation date
/// (<c>discountingperpetualfuturesengine.cpp:154-155</c>, <c>:172-177</c>), so a domestic curve settling two days on is
/// read as though its date were today's. nanoql reads each curve at the instants the grid's times denote, nothing
/// accruing before its own date. The check is nanoql on the curve restated from the evaluation date: a discount of 1 to
/// the curve's date and its rate beyond, which a log-linear discount curve on Actual/Actual (ISDA), whose year
/// fractions add, holds exactly.
/// </summary>
public class PerpetualFuturesMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter Dc = new ActualActual(ActualActualConvention.ISDA);

    [Fact]
    public void ADomesticCurveSettlingLaterIsReadFromItsOwnDate()
    {
        var settling = new FlatForward(Today + 2, 0.04, Dc);
        Date far = Today + 2 + (365 * 80);
        var restated = new DiscountCurve([Today, Today + 2, far], [1.0, 1.0, Math.Exp(-0.04 * Dc.YearFraction(Today + 2, far))], Dc);
        foreach (Period frequency in new[] { new Period(3, TimeUnit.Months), new Period(0, TimeUnit.Months) })
        {
            foreach (PerpetualFuturesFundingType funding in new[]
            {
                PerpetualFuturesFundingType.FundingWithPreviousSpot, PerpetualFuturesFundingType.FundingWithCurrentSpot,
            })
            {
                var futures = new PerpetualFutures(PerpetualFuturesPayoffType.Linear, funding, frequency, new NullCalendar(), Dc);
                double expected = Price(futures, restated), actual = Price(futures, settling);
                Assert.Equal(expected, actual, 1e-10 * Math.Abs(expected));
            }
        }
    }

    private static double Price(PerpetualFutures futures, YieldTermStructure domestic) =>
        DiscountingPerpetualFuturesEngine.Calculate(
            futures, Context, domestic, new FlatForward(Today, 0.02, Dc), 10000.0, [0.0], [0.2], [0.005]).NPV!.Value;
}
