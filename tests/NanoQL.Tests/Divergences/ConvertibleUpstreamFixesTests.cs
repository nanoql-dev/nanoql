// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fix QuantLib's master made to the binomial convertible engine after the pin, which nanoql takes (R8, the
/// upstream sweep; the user's ruling SWR1 (a) of 2026-10-29): #2752 (<c>d51f02be2</c>, issue #2701) rolls the spot to
/// the bond's settlement date, <c>s0 /= D_r(settlement)</c>, after the dividends' present values are subtracted. The
/// tree's times run from the settlement date, and QuantLib 1.44 starts its stock at the evaluation date's spot
/// (<c>binomialconvertibleengine.hpp:93-104</c>), so a bond with a settlement lag prices its conversion on a spot short
/// of the carry over the lag. QuantLib's check: master's engine itself, its header and its discretized convertible
/// (#2878 among master's changes there) compiled, renamed, against the 1.44 install beside the install's own engine
/// (the sweep's SWR1 (a)), on lagged bonds whose structures share the risk-free curve's day counter and reference date:
/// a callable fixed-coupon convertible under each exercise on three trees, one with a dividend in the lag, and master's
/// own case, each master's price within R7 and 1.44's past it. Master's case below passes on 1.44 too: a dividend in
/// the lag is subtracted from the spot and never paid in the tree, as 1.44 has it. The vectors price each bond settling
/// on the evaluation date, where the fix divides by a discount of 1 (SWI2).
/// </summary>
public class ConvertibleUpstreamFixesTests
{
    [Fact]
    public void TestDividendBetweenEvaluationAndSettlement()
    {
        // Upstream: QuantLib's master's case for #2752 (convertiblebonds.cpp's
        // testDividendBetweenEvaluationAndSettlement)

        // Evaluation date with a non-zero settlement lag so that a dividend can fall strictly between the evaluation
        // date and the settlement date.
        var today = new Date(15, Month.January, 2024);
        var context = new EvaluationContext { EvaluationDate = today };

        Calendar calendar = new TARGET();
        DayCounter dayCounter = new Actual360();
        const int settlementDays = 3;
        Date settlementDate = calendar.Advance(today, settlementDays, TimeUnit.Days);

        // A dividend strictly between the evaluation date and settlement date.
        Date exDivDate = calendar.Advance(today, 1, TimeUnit.Days);
        Assert.True(exDivDate > today && exDivDate < settlementDate, "test setup: ex-div date must fall in the settlement lag");
        const double dividendAmount = 2.0;

        // Build a simple zero-coupon convertible bond.
        Date maturityDate = calendar.Advance(today, 2, TimeUnit.Years);
        const double redemption = 100.0;
        const double spot = 50.0;
        const double conversionRatio = 2.0;

        var process = new BlackScholesMertonProcess(
            spot,
            new FlatForward(today, 0.0, dayCounter),
            new FlatForward(today, 0.05, dayCounter),
            new BlackConstantVol(today, calendar, 0.20, dayCounter));

        var schedule = new Schedule(
            maturityDate - new Period(1, TimeUnit.Years),
            maturityDate,
            new Period(Frequency.Annual),
            calendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            context);

        var bond = new ConvertibleZeroCouponBond(
            new EuropeanExercise(maturityDate), conversionRatio, [], today, settlementDays, dayCounter, schedule, redemption);

        double priceWithDividend = BinomialConvertibleEngine.Calculate<CoxRossRubinstein>(
            bond, context, process, 500, 0.0, [new FixedDividend(dividendAmount, exDivDate)]).NPV!.Value;
        double priceWithoutDividend = BinomialConvertibleEngine.Calculate<CoxRossRubinstein>(
            bond, context, process, 500, 0.0, []).NPV!.Value;

        // The in-lag dividend must reduce the price: it is subtracted from s0 but never added back inside the tree
        // (which is anchored at the settlementDate).
        Assert.True(
            priceWithDividend < priceWithoutDividend,
            $"in-lag dividend did not reduce the convertible bond price: with dividend {priceWithDividend}, without "
            + $"{priceWithoutDividend}");

        // The price drop should be economically meaningful: roughly the dividend amount rolled forward to the
        // settlement date (zero rates here, so approximately the dividend amount itself). We use a wide tolerance
        // because the convertible's optionality dampens the spot sensitivity.
        const double expectedDrop = dividendAmount * conversionRatio;
        double actualDrop = priceWithoutDividend - priceWithDividend;
        Assert.True(actualDrop >= 0.5 * expectedDrop, $"in-lag dividend price drop is too small: {actualDrop} against ~{expectedDrop}");

        // master's engine on the case (1.44's: 103.96881252641805 and 106.61886985010659)
        AssertMasters(103.99449797595264, 103.96881252641805, priceWithDividend);
        AssertMasters(106.64830607141801, 106.61886985010659, priceWithoutDividend);
    }

    [Theory]
    [InlineData("European", "crr", false, 101.70636466416568, 101.69627459576031)]
    [InlineData("European", "jr", false, 101.79504864384128, 101.78748082547584)]
    [InlineData("European", "tian", false, 101.6727759424183, 101.66457496327097)]
    [InlineData("American", "crr", false, 110.17631290286373, 110.14109608058155)]
    [InlineData("American", "jr", false, 110.16440483737657, 110.12933627777755)]
    [InlineData("American", "tian", false, 110.16853759981697, 110.12249009758875)]
    [InlineData("Bermudan", "crr", false, 109.89489154313375, 109.85679668708318)]
    [InlineData("Bermudan", "jr", false, 109.92293030766106, 109.88894223297828)]
    [InlineData("Bermudan", "tian", false, 109.87080381712562, 109.83096764751569)]
    [InlineData("American", "crr", true, 110.62589272550039, 110.59038954190677)]
    public void ALaggedBondIsPricedOnTheSettlementDatesSpot(string exercise, string tree, bool dividendInLag, double master, double v144)
    {
        // a 5-year annual 4% convertible from two TARGET days after 15 October 2026, three settlement days, conversion
        // ratio 1, a call at 105 clean after two years, a soft call at 103 dirty (trigger 1.3) after three and a put at
        // 98 clean after four; spot 100, r 5%, q 2%, σ 20% on Actual/365 (Fixed), a credit spread of 1%, 101 steps; two
        // dividends of 1.5 at six and eighteen months, or one of 2.0 in the lag, the day after the evaluation date
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        Calendar cal = new TARGET();
        Date issue = cal.Advance(today, 2, TimeUnit.Days), maturity = cal.Advance(issue, 5, TimeUnit.Years);
        var schedule = new Schedule(
            issue, maturity, new Period(Frequency.Annual), cal, BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted, DateGenerationRule.Backward, false, context);
        Callability[] calls =
        [
            new(new BondPrice(105.0, BondPriceType.Clean), CallabilityType.Call, cal.Advance(issue, 2, TimeUnit.Years)),
            new SoftCallability(new BondPrice(103.0, BondPriceType.Dirty), cal.Advance(issue, 3, TimeUnit.Years), 1.3),
            new(new BondPrice(98.0, BondPriceType.Clean), CallabilityType.Put, cal.Advance(issue, 4, TimeUnit.Years)),
        ];
        Exercise ex = exercise switch
        {
            "European" => new EuropeanExercise(maturity),
            "American" => new AmericanExercise(issue, maturity),
            _ => new BermudanExercise([.. Enumerable.Range(1, 5).Select(k => cal.Advance(issue, k, TimeUnit.Years))]),
        };
        var bond = new ConvertibleFixedCouponBond(ex, 1.0, calls, issue, 3, [0.04], new Actual365Fixed(), schedule, 100.0);
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.02, dc), new FlatForward(today, 0.05, dc), new BlackConstantVol(today, new NullCalendar(), 0.20, dc));
        Dividend[] dividends = dividendInLag
            ? [new FixedDividend(2.0, cal.Advance(today, 1, TimeUnit.Days))]
            : [new FixedDividend(1.5, cal.Advance(today, 6, TimeUnit.Months)), new FixedDividend(1.5, cal.Advance(today, 18, TimeUnit.Months))];
        double npv = tree switch
        {
            "crr" => BinomialConvertibleEngine.Calculate<CoxRossRubinstein>(bond, context, process, 101, 0.01, [.. dividends]).NPV!.Value,
            "jr" => BinomialConvertibleEngine.Calculate<JarrowRudd>(bond, context, process, 101, 0.01, [.. dividends]).NPV!.Value,
            _ => BinomialConvertibleEngine.Calculate<Tian>(bond, context, process, 101, 0.01, [.. dividends]).NPV!.Value,
        };
        AssertMasters(master, v144, npv);
    }

    // within R7 of master's price, at the bond's scale of 100, and past it from 1.44's
    private static void AssertMasters(double master, double v144, double npv)
    {
        Assert.True(Math.Abs(npv - master) <= 1e-10 * Math.Max(Math.Max(Math.Abs(npv), Math.Abs(master)), 100.0), $"{npv} against master's {master}");
        Assert.True(Math.Abs(npv - v144) > 1e-10 * Math.Max(Math.Max(Math.Abs(npv), Math.Abs(v144)), 100.0), $"{npv} against 1.44's {v144}");
    }
}
