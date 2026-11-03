// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fix QuantLib's master made to the YoY inflation cap/floor engines after the pin, which nanoql takes: #2877
/// (<c>5c93960e2</c>), the optionlet's time from the volatility's base date read with no lag,
/// <c>timeFromBase(fixingDate, Period(0, Days))</c>, as the engine's own variance is. QuantLib 1.44 reads it with the
/// structure's default lag (<c>inflationcapfloorengines.cpp:85-89</c>), so a fixing within one lag of the base date has
/// a negative time, its square root NaN, and the engine's <c>sqrtTime &gt; 0</c> test leaves the optionlet at its
/// intrinsic value.
/// </summary>
public class InflationUpstreamFixesTests
{
    [Fact]
    public void TestTimeValueNearVolatilityBaseDate()
    {
        // Upstream: QuantLib's master's case for #2877 (inflationcapfloor.cpp's testTimeValueNearVolatilityBaseDate)
        var today = new Date(21, Month.September, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var observationLag = new Period(3, TimeUnit.Months);
        var discountCurve = new FlatForward(today, 0.03, dc);
        double forward = 0.024, strike = 0.0225;
        var yoyCurve = new YoYInflationCurve(
            today, [new Date(1, Month.July, 2026), new Date(1, Month.July, 2027)], [forward, forward], Frequency.Monthly, dc);
        var index = new YoYInflationIndex(new EUHICP(), yoyCurve);
        var coupon = new YoYInflationCoupon(
            new Date(15, Month.November, 2026),
            1000000.0,
            new Date(15, Month.November, 2025),
            new Date(15, Month.November, 2026),
            0,
            index,
            observationLag,
            CPIInterpolationType.Flat,
            dc);
        ImmutableArray<CashFlow> leg = [coupon];
        Date fixingDate = coupon.FixingDate();
        double discount = coupon.Nominal(context) * coupon.AccrualPeriod() * discountCurve.Discount(coupon.Date);
        double vol = 0.40;
        string[] engineNames = ["Black", "unit-displaced Black", "Bachelier"];

        foreach (bool interpolated in new[] { false, true })
        {
            YoYOptionletVolatilitySurface volatility = ConstantYoYOptionletVolatility.Create(
                vol, 0, new NullCalendar(), BusinessDayConvention.Unadjusted, dc, observationLag, Frequency.Monthly,
                interpolated, context);

            Assert.True(fixingDate > volatility.BaseDate());
            Assert.True(fixingDate < volatility.BaseDate() + observationLag);
            Assert.True(volatility.TimeFromBase(fixingDate) < 0.0);

            // Flat observations use August 1; interpolated observations use August 15.
            Date observationDate = interpolated ? fixingDate : new Date(1, Month.August, 2026);
            double time = dc.YearFraction(volatility.BaseDate(), observationDate);
            double variance = volatility.TotalVariance(fixingDate, strike, new Period(0, TimeUnit.Days));
            Assert.True(variance > 0.0);
            Assert.True(Math.Abs(variance - (vol * vol * time)) < 1.0e-14);
            double stdDev = Math.Sqrt(variance);

            Func<YoYInflationCapFloor, InstrumentResults>[] engines =
            [
                c => YoYInflationBlackCapFloorEngine.Calculate(c, context, index, volatility, discountCurve),
                c => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(c, context, index, volatility, discountCurve),
                c => YoYInflationBachelierCapFloorEngine.Calculate(c, context, index, volatility, discountCurve),
            ];

            for (int which = 0; which < engines.Length; ++which)
            {
                foreach (YoYInflationCapFloorType type in new[] { YoYInflationCapFloorType.Cap, YoYInflationCapFloorType.Floor })
                {
                    string label = $"{engineNames[which]}, {type}, interpolated={interpolated}";
                    YoYInflationCapFloor option = type == YoYInflationCapFloorType.Cap
                        ? new YoYInflationCap(leg, [strike])
                        : new YoYInflationFloor(leg, [strike]);
                    OptionType optionType = type == YoYInflationCapFloorType.Cap ? OptionType.Call : OptionType.Put;
                    double expected = which == 2
                        ? BlackFormulaFunctions.BachelierBlackFormula(optionType, strike, forward, stdDev, discount)
                        : BlackFormulaFunctions.BlackFormula(optionType, strike, forward, stdDev, discount, which == 1 ? 1.0 : 0.0);
                    double intrinsic = discount * Math.Max(optionType == OptionType.Call ? forward - strike : strike - forward, 0.0);
                    InstrumentResults results = engines[which](option);
                    double price = results.NPV!.Value;
                    Assert.True(price > intrinsic, $"{label}: optionlet price {price} does not exceed intrinsic value {intrinsic}");
                    Assert.True(
                        Math.Abs(price - expected) < 1.0e-8, $"{label}: optionlet price {price} differs from formula value {expected}");
                    var stdDevs = (ImmutableArray<double>)results.AdditionalResults["optionletsStdDev"];
                    Assert.Single(stdDevs);
                    Assert.True(
                        Math.Abs(stdDevs[0] - stdDev) < 1.0e-14,
                        $"{label}: reported standard deviation {stdDevs[0]} differs from surface value {stdDev}");
                }
            }
        }
    }
}
