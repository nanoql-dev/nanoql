// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The implied curve on a day counter whose year fractions don't add (the S-sr plan's SSR6 (viii)). QuantLib reads the
/// original at the implied time plus the reference date's time on the original's clock
/// (<c>impliedtermstructure.hpp:85-95</c>), where its own formula states the original's ratio P(d)/P(ref). On the wheel,
/// a zero curve of 1%, 2%, 3% and 3.5% at 15 January 2016 and 1, 3 and 7 years later, implied at 31 March 2016, misses
/// the ratio over every date of four years by up to 1.25e-4 relative on Thirty360 Bond Basis and USA, and 3.7e-3 on
/// Actual/Actual ISMA without a schedule, and gives 30 and 31 March 2020, one time on Thirty360 from a month-end, one
/// discount, 0.8779622361147446, where the ratios are 0.878051925283182 and 0.8779622361147446; on Actual/365 (Fixed)
/// it holds within 2.2e-16. nanoql reads a date by date where QuantLib's sum and the date's own time on the original's
/// clock differ by more than 1e-12 relative, so a date's discount, zero rate and forward hold the ratio, and keeps
/// QuantLib's sum, and bits, elsewhere and for a time; a date before the implied reference date is refused first, with
/// QuantLib's text. QuantLib's check: the original curve's discounts by date.
/// </summary>
public class ImpliedTermStructureClockTests
{
    private static readonly Date OriginalReference = new(15, Month.January, 2016);
    private static readonly Date ImpliedReference = new(31, Month.March, 2016);
    private static readonly Date March30 = new(30, Month.March, 2020);
    private static readonly Date March31 = new(31, Month.March, 2020);

    public static TheoryData<string> NonAdditive => ["Thirty360 Bond Basis", "Thirty360 USA", "Actual/Actual ISMA"];

    [Theory]
    [MemberData(nameof(NonAdditive))]
    public void ADiscountByDateIsTheOriginalsRatio(string dayCounter)
    {
        (YieldTermStructure original, ImpliedTermStructure implied) = Curves(DayCounterOf(dayCounter));
        double refDf = original.Discount(ImpliedReference);
        for (int n = 0; n < 4 * 366; ++n)
        {
            Date d = ImpliedReference + n;
            double ratio = original.Discount(d) / refDf;
            Assert.True(Math.Abs((implied.Discount(d) / ratio) - 1.0) <= 1e-14, $"{d}: {implied.Discount(d)} against {ratio}");
        }

        Assert.NotEqual(implied.Discount(March30), implied.Discount(March31));
    }

    [Theory]
    [MemberData(nameof(NonAdditive))]
    public void AZeroRateByDateIsTheOriginalsRatio(string dayCounter)
    {
        DayCounter dc = DayCounterOf(dayCounter);
        (YieldTermStructure original, ImpliedTermStructure implied) = Curves(dc);
        double refDf = original.Discount(ImpliedReference);
        for (int n = 7; n < 4 * 366; n += 7)
        {
            Date d = ImpliedReference + n;
            double compound = refDf / original.Discount(d);
            double expected = InterestRate.ImpliedRate(
                compound, dc, Compounding.Continuous, Frequency.NoFrequency, ImpliedReference, d).Rate;
            double zero = implied.ZeroRate(d, dc, Compounding.Continuous).Rate;
            Assert.True(Math.Abs(zero - expected) <= 1e-13, $"{d}: {zero} against {expected}");
        }

        double z30 = implied.ZeroRate(March30, dc, Compounding.Continuous).Rate;
        double z31 = implied.ZeroRate(March31, dc, Compounding.Continuous).Rate;
        double e30 = InterestRate.ImpliedRate(
            refDf / original.Discount(March30), dc, Compounding.Continuous, Frequency.NoFrequency, ImpliedReference, March30).Rate;
        double e31 = InterestRate.ImpliedRate(
            refDf / original.Discount(March31), dc, Compounding.Continuous, Frequency.NoFrequency, ImpliedReference, March31).Rate;
        Assert.True(Math.Abs(z30 - e30) <= 1e-13 && Math.Abs(z31 - e31) <= 1e-13, $"{z30}, {z31} against {e30}, {e31}");
    }

    [Theory]
    [MemberData(nameof(NonAdditive))]
    public void AForwardByDateIsTheOriginalsRatio(string dayCounter)
    {
        DayCounter dc = DayCounterOf(dayCounter);
        (YieldTermStructure original, ImpliedTermStructure implied) = Curves(dc);
        for (int n = 7; n < 4 * 366; n += 7)
        {
            Date d1 = ImpliedReference + n, d2 = d1 + new Period(3, TimeUnit.Months);
            double compound = original.Discount(d1) / original.Discount(d2);
            double expected = InterestRate.ImpliedRate(compound, dc, Compounding.Continuous, Frequency.NoFrequency, d1, d2).Rate;
            double forward = implied.ForwardRate(d1, d2, dc, Compounding.Continuous).Rate;
            Assert.True(Math.Abs(forward - expected) <= 1e-13, $"{d1}: {forward} against {expected}");
        }
    }

    [Fact]
    public void ATimeReadIsQuantLibsSum()
    {
        var dc = new Thirty360(Thirty360Convention.BondBasis);
        (YieldTermStructure original, ImpliedTermStructure implied) = Curves(dc);
        double refTime = dc.YearFraction(OriginalReference, ImpliedReference);
        double refDf = original.Discount(ImpliedReference, true);
        for (int k = 0; k <= 80; ++k)
        {
            double t = k * 0.05;
            Assert.Equal(original.Discount(t + refTime, true) / refDf, implied.Discount(t));
        }
    }

    [Fact]
    public void AnAdditiveDayCounterKeepsQuantLibsBits()
    {
        (_, ImpliedTermStructure implied) = Curves(new Actual365Fixed());
        for (int n = 0; n < 4 * 366; ++n)
        {
            Date d = ImpliedReference + n;
            Assert.Equal(implied.Discount(implied.TimeFromReference(d)), implied.Discount(d));
        }

        // QuantLib's on the wheel
        Assert.True(Math.Abs((implied.Discount(March30) / 0.8780349956469877) - 1.0) <= 1e-15, $"{implied.Discount(March30)}");
        Assert.True(Math.Abs((implied.Discount(March31) / 0.8779465569260735) - 1.0) <= 1e-15, $"{implied.Discount(March31)}");
    }

    [Fact]
    public void ADateBeforeTheReferenceDateIsRefused()
    {
        var today = new Date(15, Month.January, 2025);
        var dc = new Actual365Fixed();
        var original = new FlatForward(today, 0.03, dc);
        var implied = new ImpliedTermStructure(original, today + new Period(1, TimeUnit.Years));

        // QuantLib's "negative time (-0.726027) given", where the original's ratio by date is 1.0220197555902324
        double t = implied.TimeFromReference(today + 100);
        NanoQLException e = Assert.Throws<NanoQLException>(() => implied.Discount(today + 100));
        Assert.Equal($"negative time ({t.ToString(CultureInfo.InvariantCulture)}) given", e.Message);
        Assert.StartsWith("negative time (-0.726027", e.Message);
    }

    private static DayCounter DayCounterOf(string name) => name switch
    {
        "Thirty360 Bond Basis" => new Thirty360(Thirty360Convention.BondBasis),
        "Thirty360 USA" => new Thirty360(Thirty360Convention.USA),
        _ => new ActualActual(ActualActualConvention.ISMA),
    };

    // A zero curve of 1%, 2%, 3% and 3.5% at 15 January 2016 and 1, 3 and 7 years later, implied at 31 March 2016.
    private static (YieldTermStructure Original, ImpliedTermStructure Implied) Curves(DayCounter dc)
    {
        Date[] nodes =
        [
            OriginalReference,
            OriginalReference + new Period(1, TimeUnit.Years),
            OriginalReference + new Period(3, TimeUnit.Years),
            OriginalReference + new Period(7, TimeUnit.Years),
        ];
        var original = new ZeroCurve(nodes, [0.01, 0.02, 0.03, 0.035], dc);
        return (original, new ImpliedTermStructure(original, ImpliedReference));
    }
}
