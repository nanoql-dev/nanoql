// SPDX-FileCopyrightText: Copyright (C) 2018 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/constnotionalcrosscurrencyfixedvsfloatingswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

// QuantLib's fairRate() and fairSpread() throw "not available" on a null result: the results here are null, which
// the case's Value reads would refuse.
[Trait("Oracle", "QuantLib")]
public class ConstNotionalCrossCurrencyFixedVsFloatingSwapTests
{
    [Fact]
    public void TestFixFloatXCCYSwapPricing()
    {
        // Test cross-currency fix/float swap pricing against known results...

        var context = new EvaluationContext { EvaluationDate = new Date(11, September, 2018) };
        bool usingAtParCoupons = context.UseAtParCoupons;

        // Create swap, USD 3M Libor vs TRY annual fixed
        double spotFx = 6.4304;
        double rate = 0.249;
        double spread = 0.0;
        ConstNotionalCrossCurrencyFixedVsFloatingSwap xccy = MakeFixFloatXCCYSwap(spotFx, rate, spread, context);

        // Attach pricing engine
        ConstNotionalCrossCurrencyFixedVsFloatingSwapResults results =
            DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
                xccy,
                context,
                new USDCurrency(),
                UsdDiscountCurve(),
                new TRYCurrency(),
                TryDiscountCurve(),
                1.0 / spotFx);

        // Check values
        double usdTolerance = 0.01;

        double expNpv = usingAtParCoupons ? 129777.91 : 129767.99;
        Check("NPV", results.NPV!.Value, expNpv, usdTolerance);

        double expPayLegNpv = -12286.45;
        double expPayLegBps = -2628.39;
        Check("Leg 0 NPV", results.LegNPV[0]!.Value, expPayLegNpv, usdTolerance);
        Check("Leg 0 BPS", results.LegBPS[0]!.Value, expPayLegBps, usdTolerance);
        Check("Leg 0 inCcyNPV", results.InCcyLegNPV[0]!.Value, expPayLegNpv * spotFx, usdTolerance * spotFx);
        Check("Leg 0 inCcyBPS", results.InCcyLegBPS[0]!.Value, expPayLegBps * spotFx, usdTolerance * spotFx);

        double expRecLegNpv = usingAtParCoupons ? 142064.36 : 142054.44;
        double expRecLegBps = 4735.03;
        Check("Leg 1 NPV", results.LegNPV[1]!.Value, expRecLegNpv, usdTolerance);
        Check("Leg 1 BPS", results.LegBPS[1]!.Value, expRecLegBps, usdTolerance);
        Check("Leg 1 inCcyNPV", results.InCcyLegNPV[1]!.Value, expRecLegNpv, usdTolerance);
        Check("Leg 1 inCcyBPS", results.InCcyLegBPS[1]!.Value, expRecLegBps, usdTolerance);

        double expectedFairRate = usingAtParCoupons ? 0.253937551076 : 0.253937173908;
        double expectedFairSpread = usingAtParCoupons ? -0.002740802104 : -0.002740592739;

        Check("Fair Fixed Rate", results.FairFixedRate!.Value, expectedFairRate, 1e-10);
        Check("Fair Spread", results.FairSpread!.Value, expectedFairSpread, 1e-10);
    }

    private static void Check(string what, double calculated, double expected, double tolerance) =>
        Assert.True(
            Math.Abs(calculated - expected) <= tolerance,
            $"Failed to reproduce {what}:\n    expected:   {expected}\n    calculated: {calculated}\n" +
            $"    error:      {Math.Abs(calculated - expected)}");

    private static DiscountCurve Curve((int Day, Month Month, int Year, double Df)[] nodes) =>
        new(
            [.. nodes.Select(n => new Date(n.Day, n.Month, n.Year))],
            [.. nodes.Select(n => n.Df)],
            new Actual365Fixed());

    private static YieldTermStructure UsdDiscountCurve() => Curve(
    [
        (11, September, 2018, 1), (14, September, 2018, 0.99994666951096), (20, September, 2018, 0.999627719221066),
        (27, September, 2018, 0.999254084816959), (04, October, 2018, 0.998837020905631),
        (15, October, 2018, 0.998176132423265), (13, November, 2018, 0.99644587210048),
        (13, December, 2018, 0.994644668243218), (14, January, 2019, 0.992596634984033),
        (13, February, 2019, 0.990636503861861), (13, March, 2019, 0.988809127958345),
        (13, June, 2019, 0.982417991680868), (13, September, 2019, 0.975723193871552),
        (13, March, 2020, 0.96219213956104), (14, September, 2020, 0.948588232418325),
        (13, September, 2021, 0.92279636773464), (13, September, 2022, 0.898345201557914),
        (13, September, 2023, 0.874715322269088), (15, September, 2025, 0.828658611114833),
        (13, September, 2028, 0.763030152740947), (13, September, 2030, 0.722238847877756),
        (13, September, 2033, 0.664460629674362), (13, September, 2038, 0.580288693473926),
        (14, September, 2043, 0.510857007600479), (14, September, 2048, 0.44941525649436),
        (13, September, 2058, 0.352389176933952), (13, September, 2068, 0.28183300653329),
    ]);

    private static YieldTermStructure UsdProjectionCurve() => Curve(
    [
        (11, September, 2018, 1), (13, December, 2018, 0.994134145990132), (19, December, 2018, 0.993695776146116),
        (20, March, 2019, 0.987047992958673), (19, June, 2019, 0.980016364694049),
        (18, September, 2019, 0.972708376777628), (18, December, 2019, 0.965277162951128),
        (18, March, 2020, 0.957799302363697), (14, September, 2020, 0.943264331984248),
        (13, September, 2021, 0.914816470778467), (13, September, 2022, 0.88764714641623),
        (13, September, 2023, 0.861475671008934), (13, September, 2024, 0.835944798717806),
        (15, September, 2025, 0.810833947617338), (14, September, 2026, 0.78631849267276),
        (13, September, 2027, 0.762267648509673), (13, September, 2028, 0.738613627359076),
        (13, September, 2029, 0.715502378943932), (13, September, 2030, 0.693380472578176),
        (13, September, 2033, 0.631097994110912), (13, September, 2038, 0.540797634630251),
        (14, September, 2043, 0.465599237331079), (14, September, 2048, 0.402119473746341),
        (13, September, 2058, 0.303129773289934), (13, September, 2068, 0.23210070222569),
    ]);

    private static YieldTermStructure TryDiscountCurve() => Curve(
    [
        (11, September, 2018, 1), (15, October, 2018, 0.979316826759248), (13, November, 2018, 0.959997676372812),
        (13, December, 2018, 0.939987819768341), (14, January, 2019, 0.917879348095857),
        (13, February, 2019, 0.897309447005875), (13, March, 2019, 0.878377243062539),
        (13, September, 2019, 0.76374502801031), (14, September, 2020, 0.595566112318217),
        (13, September, 2021, 0.483132147134316), (13, September, 2022, 0.402466076327945),
        (13, September, 2023, 0.345531820837392), (13, September, 2024, 0.298070398810781),
        (13, September, 2025, 0.264039803303106), (13, September, 2026, 0.237813130821584),
        (13, September, 2027, 0.216456097559999), (13, September, 2028, 0.200289181912326),
        (13, September, 2033, 0.122659501286113),
    ]);

    private static ConstNotionalCrossCurrencyFixedVsFloatingSwap MakeFixFloatXCCYSwap(
        double spotFx, double rate, double spread, EvaluationContext context)
    {
        // USD nominal
        double usdNominal = 10000000.0;

        // Shared settlement conventions
        var payConvention = BusinessDayConvention.Following;
        int payLag = 0;
        var payCalendar = new JointCalendar(
            new UnitedStates(UnitedStatesMarket.Settlement), new UnitedKingdom(), new Turkey());

        // Swap start and end date
        Date referenceDate = context.EvaluationDate;
        referenceDate = payCalendar.Adjust(referenceDate);
        Date start = payCalendar.Advance(referenceDate, 2, TimeUnit.Days);
        Date end = start + new Period(5, TimeUnit.Years);

        // Fixed TRY schedule
        var fixedSchedule = new Schedule(
            start,
            end,
            new Period(1, TimeUnit.Years),
            payCalendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            false,
            context);

        // Float USD schedule
        var floatSchedule = new Schedule(
            start,
            end,
            new Period(3, TimeUnit.Months),
            payCalendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            false,
            context);

        var index = new USDLibor(new Period(3, TimeUnit.Months), UsdProjectionCurve());

        // Create swap
        return new ConstNotionalCrossCurrencyFixedVsFloatingSwap(
            SwapType.Payer,
            usdNominal * spotFx,
            new TRYCurrency(),
            fixedSchedule,
            rate,
            new Actual360(),
            payConvention,
            payLag,
            payCalendar,
            usdNominal,
            new USDCurrency(),
            floatSchedule,
            index,
            spread,
            payConvention,
            payLag,
            payCalendar,
            context);
    }
}
