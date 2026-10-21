// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/constnotionalcrosscurrencyswap.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
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
public class ConstNotionalCrossCurrencySwapTests
{
    private static readonly Date Today = new(11, September, 2018);

    [Fact]
    public void TestFixFixXCCYSwapPricing()
    {
        // Test fix/fix cross-currency swap pricing against known results...

        // Create swap
        double usdNominal = 125_000_000;
        double spotFx = 1.22;
        EvaluationContext context = Context();
        ConstNotionalCrossCurrencySwap xccySwap = MakeFixFixXCCYSwap(usdNominal, spotFx);

        // Attach pricing engine
        CrossCurrencySwapResults results = DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            xccySwap,
            context,
            new USDCurrency(),
            USDDiscountCurve(),
            new CHFCurrency(),
            CHFDiscountCurve(),
            1.0 / spotFx);

        // Check values
        double tolerance = 0.01;
        double expNpv = -21108172.67;

        Check("NPV", results.NPV!.Value, expNpv, tolerance);

        double expPayLegNpv = -17892458.36;
        double expPayLegBps = -58317.61;
        Check("Leg 0 NPV", results.LegNPV[0]!.Value, expPayLegNpv, tolerance);
        Check("Leg 0 BPS", results.LegBPS[0]!.Value, expPayLegBps, tolerance);
        Check("Leg 0 inCcyNPV", results.InCcyLegNPV[0]!.Value, expPayLegNpv, tolerance);
        Check("Leg 0 inCcyBPS", results.InCcyLegBPS[0]!.Value, expPayLegBps, tolerance);

        double expRecLegNpv = -3215714.30;
        double expRecLegBps = 58542.62;
        Check("Leg 1 NPV", results.LegNPV[1]!.Value, expRecLegNpv, tolerance);
        Check("Leg 1 BPS", results.LegBPS[1]!.Value, expRecLegBps, tolerance);
        Check("Leg 1 inCcyNPV", results.InCcyLegNPV[1]!.Value, expRecLegNpv * spotFx, tolerance * spotFx);
        Check("Leg 1 inCcyBPS", results.InCcyLegBPS[1]!.Value, expRecLegBps * spotFx, tolerance * spotFx);
    }

    [Fact]
    public void TestFloatFixXCCYSwapPricing()
    {
        // Test float/fix cross-currency swap pricing against known results...

        EvaluationContext context = Context();
        bool usingAtParCoupons = context.UseAtParCoupons;

        // Create swap
        double usdNominal = 10_000_000;
        double spotFx = 6.4304;
        ConstNotionalCrossCurrencySwap xccySwap = MakeFixFloatXCCYSwap(usdNominal, spotFx, context);

        // Attach pricing engine
        CrossCurrencySwapResults results = DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            xccySwap,
            context,
            new USDCurrency(),
            USDDiscountCurve(),
            new TRYCurrency(),
            TRYDiscountCurve(),
            1.0 / spotFx);

        // Check values
        double tolerance = 0.01;

        double expNpv = usingAtParCoupons ? 218961.99 : 218981.99;
        double npv = results.NPV!.Value;
        Check("NPV", npv, expNpv, tolerance);

        double expPayLegNpv = 77054.99;
        double expPayLegBps = -2591.34;
        Check("Leg 0 NPV", results.LegNPV[0]!.Value, expPayLegNpv, tolerance);
        Check("Leg 0 BPS", results.LegBPS[0]!.Value, expPayLegBps, tolerance);
        Check("Leg 0 inCcyNPV", results.InCcyLegNPV[0]!.Value, expPayLegNpv * spotFx, tolerance * spotFx);
        Check("Leg 0 inCcyBPS", results.InCcyLegBPS[0]!.Value, expPayLegBps * spotFx, tolerance * spotFx);

        double expRecLegNpv = usingAtParCoupons ? 141906.99 : 141926.99;
        double expRecLegBps = 4730.19;
        Check("Leg 1 NPV", results.LegNPV[1]!.Value, expRecLegNpv, tolerance);
        Check("Leg 1 BPS", results.LegBPS[1]!.Value, expRecLegBps, tolerance);
        Check("Leg 1 inCcyNPV", results.InCcyLegNPV[1]!.Value, expRecLegNpv, tolerance);
        Check("Leg 1 inCcyBPS", results.InCcyLegBPS[1]!.Value, expRecLegBps, tolerance);
    }

    [Fact]
    public void TestFloatFloatXCCYSwapPricing()
    {
        // Test float/float cross-currency swap pricing against known results...

        EvaluationContext context = Context();
        bool usingAtParCoupons = context.UseAtParCoupons;

        // Create swap
        double usdNominal = 125_000_000;
        double spotFx = 1.35;
        ConstNotionalCrossCurrencySwap xccySwap = MakeFloatFloatXCCYSwap(usdNominal, spotFx, context);

        // Attach pricing engine
        CrossCurrencySwapResults results = DiscountingConstNotionalCrossCurrencySwapEngine.Calculate(
            xccySwap,
            context,
            new USDCurrency(),
            USDDiscountCurve(),
            new GBPCurrency(),
            GBPDiscountCurve(),
            1.0 / spotFx);

        // Check values
        double tolerance = 0.01;

        double expNpv = 0.00;
        Check("NPV", results.NPV!.Value, expNpv, tolerance);

        double expPayLegNpv = usingAtParCoupons ? -1773829.64 : -1773772.22;
        double expPayLegBps = -59127.58;
        Check("Leg 0 NPV", results.LegNPV[0]!.Value, expPayLegNpv, tolerance);
        Check("Leg 0 BPS", results.LegBPS[0]!.Value, expPayLegBps, tolerance);
        Check("Leg 0 inCcyNPV", results.InCcyLegNPV[0]!.Value, expPayLegNpv, tolerance);
        Check("Leg 0 inCcyBPS", results.InCcyLegBPS[0]!.Value, expPayLegBps, tolerance);

        double expRecLegNpv = usingAtParCoupons ? 1773829.64 : 1773772.22;
        double expRecLegBps = 58317.61;
        Check("Leg 1 NPV", results.LegNPV[1]!.Value, expRecLegNpv, tolerance);
        Check("Leg 1 BPS", results.LegBPS[1]!.Value, expRecLegBps, tolerance);
        Check("Leg 1 inCcyNPV", results.InCcyLegNPV[1]!.Value, expRecLegNpv * spotFx, tolerance * spotFx);
        Check("Leg 1 inCcyBPS", results.InCcyLegBPS[1]!.Value, expRecLegBps * spotFx, tolerance * spotFx);
    }

    private static void Check(string what, double calculated, double expected, double tolerance) =>
        Assert.True(
            Math.Abs(calculated - expected) <= tolerance,
            $"Failed to reproduce {what}:\n    expected:   {expected}\n    calculated: {calculated}\n" +
            $"    error:      {Math.Abs(calculated - expected)}");

    private static EvaluationContext Context() => new() { EvaluationDate = Today };

    // Data for the tests
    private sealed class CommonVars
    {
        public CommonVars(Calendar c, BusinessDayConvention conv, DateGenerationRule r)
        {
            PayCalendar = c;
            StartDate = PayCalendar.Advance(Today, new Period(2, TimeUnit.Days));
            EndDate = PayCalendar.Advance(Today, new Period(5, TimeUnit.Years));
            Rule = r;
            Convention = conv;
        }

        public Date StartDate { get; }

        public Date EndDate { get; }

        public Calendar PayCalendar { get; }

        public DateGenerationRule Rule { get; }

        public BusinessDayConvention Convention { get; }

        public bool EndOfMonth => false;

        public DayCounter Dc { get; } = new Actual365Fixed();
    }

    private static DiscountCurve Curve((int Day, Month Month, int Year, double Df)[] nodes) =>
        new(
            [.. nodes.Select(n => new Date(n.Day, n.Month, n.Year))],
            [.. nodes.Select(n => n.Df)],
            new Actual365Fixed());

    private static YieldTermStructure CHFDiscountCurve() => Curve(
    [
        (11, September, 2018, 1.0), (14, September, 2018, 0.99998), (20, September, 2018, 0.99975),
        (27, September, 2018, 0.99945), (04, October, 2018, 0.99910), (15, October, 2018, 0.99855),
        (13, November, 2018, 0.99700), (13, December, 2018, 0.99540), (14, January, 2019, 0.99360),
        (13, February, 2019, 0.99190), (13, March, 2019, 0.99030), (13, June, 2019, 0.98430),
        (13, September, 2019, 0.97800), (13, March, 2020, 0.96500), (14, September, 2020, 0.95200),
        (13, September, 2021, 0.92700), (13, September, 2022, 0.90300), (13, September, 2023, 0.88000),
        (15, September, 2025, 0.83600), (13, September, 2028, 0.77300), (13, September, 2030, 0.73400),
        (13, September, 2033, 0.67800), (13, September, 2038, 0.59600), (14, September, 2043, 0.52800),
        (14, September, 2048, 0.46800), (13, September, 2058, 0.36700), (13, September, 2068, 0.29700),
    ]);

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

    private static YieldTermStructure TRYDiscountCurve() => Curve(
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

    // Helper functions
    private static ConstNotionalCrossCurrencySwap MakeFixFixXCCYSwap(double leg1Nominal, double spotFx)
    {
        Calendar payCalendar = new JointCalendar(new UnitedStates(UnitedStatesMarket.Settlement), new Switzerland());

        var vars = new CommonVars(payCalendar, BusinessDayConvention.Following, DateGenerationRule.Forward);

        var schedule = new Schedule(
            vars.StartDate,
            vars.EndDate,
            new Period(3, TimeUnit.Months),
            payCalendar,
            vars.Convention,
            vars.Convention,
            vars.Rule,
            vars.EndOfMonth,
            Context());

        double usdRate = 0.0575;
        double chfRate = 0.0201;

        // USD Leg
        ImmutableArray<CashFlow> usdLeg = new FixedRateLeg(schedule)
            .WithNotionals(leg1Nominal)
            .WithCouponRates(usdRate, vars.Dc)
            .WithPaymentAdjustment(vars.Convention)
            .WithPaymentCalendar(vars.PayCalendar)
            .ToLeg();
        Date aDate = payCalendar.Adjust(schedule.Dates[0], vars.Convention);
        usdLeg = [new SimpleCashFlow(-leg1Nominal, aDate), .. usdLeg, new SimpleCashFlow(leg1Nominal, usdLeg[^1].Date)];

        // CHF Leg
        ImmutableArray<CashFlow> chfLeg = new FixedRateLeg(schedule)
            .WithNotionals(leg1Nominal * spotFx)
            .WithCouponRates(chfRate, vars.Dc)
            .WithPaymentAdjustment(vars.Convention)
            .WithPaymentCalendar(vars.PayCalendar)
            .ToLeg();
        chfLeg =
        [
            new SimpleCashFlow(-leg1Nominal * spotFx, aDate), .. chfLeg,
            new SimpleCashFlow(leg1Nominal * spotFx, chfLeg[^1].Date),
        ];

        // Create swap
        return new ConstNotionalCrossCurrencySwap(usdLeg, new USDCurrency(), chfLeg, new CHFCurrency());
    }

    private static ConstNotionalCrossCurrencySwap MakeFixFloatXCCYSwap(
        double leg1Nominal, double spotFx, EvaluationContext context)
    {
        Calendar payCalendar = new JointCalendar(
            new UnitedStates(UnitedStatesMarket.Settlement), new UnitedKingdom(), new Turkey());

        var vars = new CommonVars(payCalendar, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Backward);
        var payConvention = BusinessDayConvention.Following;

        var floatSchedule = new Schedule(
            vars.StartDate,
            vars.EndDate,
            new Period(3, TimeUnit.Months),
            payCalendar,
            vars.Convention,
            vars.Convention,
            vars.Rule,
            vars.EndOfMonth,
            Context());

        var fixSchedule = new Schedule(
            vars.StartDate,
            vars.EndDate,
            new Period(1, TimeUnit.Years),
            payCalendar,
            vars.Convention,
            vars.Convention,
            vars.Rule,
            vars.EndOfMonth,
            Context());

        // TRY Leg
        double tryRate = 0.249;
        ImmutableArray<CashFlow> tryLeg = new FixedRateLeg(fixSchedule)
            .WithNotionals(leg1Nominal * spotFx)
            .WithCouponRates(tryRate, vars.Dc)
            .WithPaymentAdjustment(payConvention)
            .WithPaymentCalendar(payCalendar)
            .ToLeg();
        Date aDate = payCalendar.Adjust(fixSchedule.Dates[0], vars.Convention);
        tryLeg =
        [
            new SimpleCashFlow(-leg1Nominal * spotFx, aDate), .. tryLeg,
            new SimpleCashFlow(leg1Nominal * spotFx, tryLeg[^1].Date),
        ];

        // USD Leg
        var usdlibor3M = new USDLibor(new Period(3, TimeUnit.Months), USDProjectionCurve());
        ImmutableArray<CashFlow> usdLeg = new IborLeg(floatSchedule, usdlibor3M)
            .WithNotionals(leg1Nominal)
            .WithPaymentAdjustment(payConvention)
            .WithPaymentCalendar(payCalendar)
            .ToLeg(context);
        aDate = payCalendar.Adjust(floatSchedule.Dates[0], vars.Convention);
        usdLeg = [new SimpleCashFlow(-leg1Nominal, aDate), .. usdLeg];
        usdLeg = [.. usdLeg, new SimpleCashFlow(leg1Nominal, usdLeg[^1].Date)];

        // Create swap
        return new ConstNotionalCrossCurrencySwap(tryLeg, new TRYCurrency(), usdLeg, new USDCurrency());
    }

    private static ConstNotionalCrossCurrencySwap MakeFloatFloatXCCYSwap(
        double leg1Nominal, double spotFx, EvaluationContext context)
    {
        Calendar payCalendar = new JointCalendar(new UnitedStates(UnitedStatesMarket.Settlement), new UnitedKingdom());

        var vars = new CommonVars(payCalendar, BusinessDayConvention.Following, DateGenerationRule.Forward);

        var schedule = new Schedule(
            vars.StartDate,
            vars.EndDate,
            new Period(3, TimeUnit.Months),
            payCalendar,
            vars.Convention,
            vars.Convention,
            vars.Rule,
            vars.EndOfMonth,
            Context());

        // USD Leg
        var usdlibor3M = new USDLibor(new Period(3, TimeUnit.Months), USDProjectionCurve());
        ImmutableArray<CashFlow> usdLeg = new IborLeg(schedule, usdlibor3M)
            .WithNotionals(leg1Nominal)
            .WithPaymentAdjustment(vars.Convention)
            .WithPaymentCalendar(payCalendar)
            .ToLeg(context);
        Date aDate = payCalendar.Adjust(schedule.Dates[0]);
        usdLeg = [new SimpleCashFlow(-leg1Nominal, aDate), .. usdLeg];
        usdLeg = [.. usdLeg, new SimpleCashFlow(leg1Nominal, usdLeg[^1].Date)];

        // GBP Leg
        var gbpLibor3M = new GBPLibor(new Period(3, TimeUnit.Months), GBPProjectionCurve());
        ImmutableArray<CashFlow> gbpLeg = new IborLeg(schedule, gbpLibor3M)
            .WithNotionals(leg1Nominal * spotFx)
            .WithPaymentAdjustment(vars.Convention)
            .WithPaymentCalendar(payCalendar)
            .ToLeg(context);
        gbpLeg = [new SimpleCashFlow(-leg1Nominal * spotFx, aDate), .. gbpLeg];
        gbpLeg = [.. gbpLeg, new SimpleCashFlow(leg1Nominal * spotFx, gbpLeg[^1].Date)];

        // Create swap
        return new ConstNotionalCrossCurrencySwap(usdLeg, new USDCurrency(), gbpLeg, new GBPCurrency());
    }
}
