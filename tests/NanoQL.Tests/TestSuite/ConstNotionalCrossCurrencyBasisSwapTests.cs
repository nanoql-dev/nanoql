// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/constnotionalcrosscurrencybasisswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Coupons;
using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class ConstNotionalCrossCurrencyBasisSwapTests
{
    [Fact]
    public void TestBasisXCCYSwapPricing()
    {
        // Test cross-currency basis-swap pricing against known results...

        var context = new EvaluationContext { EvaluationDate = new Date(11, September, 2018) };

        // Create swap
        double spotFx = 1;
        double spread = 0;
        ConstNotionalCrossCurrencyBasisSwap xccy = MakeBasisXCCY(spotFx, spread, context);

        // Attach pricing engine
        ConstNotionalCrossCurrencyBasisSwapResults results = DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            xccy, context, new USDCurrency(), USDDiscountCurve(), new GBPCurrency(), GBPDiscountCurve(), spotFx);

        // Check values
        double tol = 0.01;
        double expectedNPV = 0.0;

        Check("NPV", results.NPV!.Value, expectedNPV, tol);

        double expBps = -4670.170509677384; // cached value
        Check("Leg 0 BPS", results.LegBPS[0]!.Value, expBps, tol);
    }

    [Fact]
    public void TestBasisONXCCYSwapPricing()
    {
        // Test cross-currency overnight-index basis-swap pricing against known results...

        var context = new EvaluationContext { EvaluationDate = new Date(11, September, 2018) };

        // Create swap
        double spotFx = 1;
        double spread = 0;
        ConstNotionalCrossCurrencyBasisSwap xccy = MakeONBasisXCCY(spotFx, spread, context);

        // Attach pricing engine
        ConstNotionalCrossCurrencyBasisSwapResults results = DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            xccy, context, new USDCurrency(), USDDiscountCurve(), new GBPCurrency(), GBPDiscountCurve(), spotFx);

        // Check values
        double tol = 0.01;
        double expectedNPV = 0.26367734931409;

        Check("NPV", results.NPV!.Value, expectedNPV, tol);

        double expBps = -4670.170509677384; // cached value
        Check("Leg 0 BPS", results.LegBPS[0]!.Value, expBps, tol);
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

    private static YieldTermStructure USDDiscountCurve() => Curve(
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

    private static YieldTermStructure USDProjectionCurve() => Curve(
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

    // QuantLib's GBP curves are its USD ones, node for node.
    private static YieldTermStructure GBPDiscountCurve() => USDDiscountCurve();

    private static YieldTermStructure GBPProjectionCurve() => USDProjectionCurve();

    private static Schedule Schedule(EvaluationContext context)
    {
        // Dates and calendars
        var payCalendar = new JointCalendar(new UnitedStates(UnitedStatesMarket.Settlement), new UnitedKingdom());
        Date referenceDate = context.EvaluationDate;
        referenceDate = payCalendar.Adjust(referenceDate);
        Date start = payCalendar.Advance(referenceDate, 2, TimeUnit.Days);
        Date end = start + new Period(5, TimeUnit.Years);
        return new Schedule(
            start,
            end,
            new Period(3, TimeUnit.Months),
            payCalendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            false,
            context);
    }

    private static ConstNotionalCrossCurrencyBasisSwap MakeBasisXCCY(
        double spotFx, double gbpSpread, EvaluationContext context)
    {
        // USD nominal
        double gbpNominal = 10000000.0;
        Schedule schedule = Schedule(context);

        // Indices
        var usdIndex = new USDLibor(new Period(3, TimeUnit.Months), USDProjectionCurve());
        var gbpIndex = new GBPLibor(new Period(3, TimeUnit.Months), GBPProjectionCurve());

        // Create swap
        return new ConstNotionalCrossCurrencyBasisSwap(
            gbpNominal,
            new GBPCurrency(),
            schedule,
            gbpIndex,
            gbpSpread,
            1.0,
            gbpNominal * spotFx,
            new USDCurrency(),
            schedule,
            usdIndex,
            0.0,
            1.0,
            context);
    }

    private static ConstNotionalCrossCurrencyBasisSwap MakeONBasisXCCY(
        double spotFx, double gbpSpread, EvaluationContext context)
    {
        // USD nominal
        double gbpNominal = 10000000.0;
        Schedule schedule = Schedule(context);

        // Indices
        var sofrIndex = new Sofr(USDProjectionCurve());
        var soniaIndex = new Sonia(GBPProjectionCurve());

        // Create swap
        return new ConstNotionalCrossCurrencyBasisSwap(
            gbpNominal,
            new GBPCurrency(),
            schedule,
            soniaIndex,
            gbpSpread,
            1.0,
            gbpNominal * spotFx,
            new USDCurrency(),
            schedule,
            sofrIndex,
            0.0,
            1.0,
            context,
            0, // payPaymentLag
            0, // recPaymentLag
            false, // payCompoundSpread
            0, // payLookbackDays
            false, // payObservationShift
            3, // payLockoutDays
            RateAveragingType.Compound, // payAveragingMethod
            false, // recCompoundSpread
            0, // recLookbackDays
            false, // recObservationShift
            2, // recLockoutDays
            RateAveragingType.Compound, // recAveragingMethod
            false); // telescopicValueDates
    }
}
