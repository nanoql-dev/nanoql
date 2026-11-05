// SPDX-FileCopyrightText: Copyright (C) 2008 Jose Aparicio
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: Examples/CDS/CDS.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using CultureInfo = System.Globalization.CultureInfo;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Credit;

/// <summary>
/// Credit default swaps: QuantLib's CDS example, a hazard curve bootstrapped from quoted spreads and the quotes
/// repriced on it (its example 1), then ISDA's standard model, its rate curve and a reference trade (its example 2).
/// </summary>
public static class CreditDefaultSwaps
{
    /// <summary>The example's outputs.</summary>
    /// <returns>The hazard curve, its survival probabilities, the repriced quotes, ISDA's rate curve and the trade.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        HazardCurve h = BuildHazardCurve();
        var nodes = new Table("Node", ">Hazard rate");
        foreach (var (date, rate) in h.Curve.Nodes())
        {
            nodes.Add(Table.D(date), G(rate));
        }

        var survival = new Table("Horizon", ">Survival probability");
        foreach (int years in new[] { 1, 2 })
        {
            survival.Add($"{years}Y", Table.Percent(h.Curve.SurvivalProbability(h.Today + new Period(years, TimeUnit.Years)), 6));
        }

        var repriced = new Table("Quote", ">Fair spread", ">NPV", ">Default leg", ">Coupon leg");
        foreach (var (tenor, result) in Reprice(h))
        {
            // The NPV is round-off, its sign the last bits'; rounded and added to 0.0, it prints as 0.00.
            repriced.Add(tenor, Table.Percent(result.FairSpread!.Value, 6), Table.N(Math.Round(result.NPV!.Value, 2) + 0.0, 2),
                G(result.DefaultLegNPV!.Value), G(result.CouponLegNPV!.Value));
        }

        Isda isda = BuildIsda();
        var curve = new Table("Pillar", ">Zero rate", ">Discount");
        foreach (var (date, zero, discount) in isda.Curve)
        {
            curve.Add(Table.D(date), G(zero), G(discount));
        }

        var trade = new Table("Reference trade", ">Value");
        trade.Add("First period's accrual on 24 October 2014", G(isda.Accrued));
        trade.Add("NPV", G(isda.Npv));

        yield return ("cds-hazard-rates", nodes);
        yield return ("cds-survival", survival);
        yield return ("cds-repriced", repriced);
        yield return ("cds-isda-curve", curve);
        yield return ("cds-isda-trade", trade);
    }

    /// <summary>Example 1's hazard curve, with its date, its discount curve and its quotes.</summary>
    /// <param name="Today">The evaluation date.</param>
    /// <param name="Context">The evaluation context.</param>
    /// <param name="Curve">The bootstrapped hazard-rate curve.</param>
    /// <param name="Discount">The discount curve.</param>
    /// <param name="Spreads">The quoted spreads, by tenor.</param>
    public sealed record HazardCurve(
        Date Today,
        EvaluationContext Context,
        PiecewiseDefaultCurve<HazardRate, BackwardFlat, IterativeBootstrap> Curve,
        YieldTermStructure Discount,
        IReadOnlyList<(Period Tenor, double Spread)> Spreads);

    /// <summary>Example 2's ISDA rate curve, as each pillar's zero rate and discount, and the reference trade.</summary>
    /// <param name="Curve">Each pillar's date, continuous zero rate and discount factor.</param>
    /// <param name="Accrued">The first coupon's accrued amount on 24 October 2014.</param>
    /// <param name="Npv">The reference trade's NPV on ISDA's engine.</param>
    public sealed record Isda(IReadOnlyList<(Date Date, double Zero, double Discount)> Curve, double Accrued, double Npv);

    /// <summary>Example 1: a hazard curve bootstrapped from four CDS spreads.</summary>
    /// <returns>The curve and its inputs.</returns>
    public static HazardCurve BuildHazardCurve()
    {
        // <bootstrap>
        Calendar calendar = new TARGET();
        Date today = calendar.Adjust(new Date(15, Month.May, 2007));
        var context = new EvaluationContext { EvaluationDate = today };
        var discount = new FlatForward(today, 0.01, new Actual365Fixed());

        int settlementDays = 1;
        double recoveryRate = 0.5;
        (Period Tenor, double Spread)[] quotes =
        [
            (new Period(3, TimeUnit.Months), 0.0150), (new Period(6, TimeUnit.Months), 0.0150),
            (new Period(1, TimeUnit.Years), 0.0150), (new Period(2, TimeUnit.Years), 0.0150),
        ];

        var helpers = new List<BootstrapHelper<DefaultProbabilityTermStructure>>();
        foreach (var (tenor, spread) in quotes)
        {
            helpers.Add(new SpreadCdsHelper(spread, tenor, settlementDays, calendar, Frequency.Quarterly,
                BusinessDayConvention.Following, DateGenerationRule.TwentiethIMM, new Actual365Fixed(), recoveryRate,
                discount, context));
        }

        var curve = new PiecewiseDefaultCurve<HazardRate, BackwardFlat, IterativeBootstrap>(
            today, helpers, new Actual365Fixed(), context);
        // </bootstrap>
        return new HazardCurve(today, context, curve, discount, quotes);
    }

    /// <summary>Example 1: each quoted CDS priced on the curve bootstrapped from it.</summary>
    /// <param name="h">The hazard curve.</param>
    /// <returns>Each quote's tenor and results.</returns>
    public static IReadOnlyList<(string Tenor, CreditDefaultSwapResults Result)> Reprice(HazardCurve h)
    {
        ArgumentNullException.ThrowIfNull(h);
        var results = new List<(string, CreditDefaultSwapResults)>();
        // <reprice>
        Calendar calendar = new TARGET();
        Date settlement = calendar.Advance(h.Today, 1, TimeUnit.Days);
        foreach (var (tenor, spread) in h.Spreads)
        {
            Date maturity = calendar.Adjust(settlement + tenor, BusinessDayConvention.Following);
            Schedule schedule = new MakeSchedule().From(settlement).To(maturity).WithFrequency(Frequency.Quarterly)
                .WithCalendar(calendar).WithTerminationDateConvention(BusinessDayConvention.Unadjusted)
                .WithRule(DateGenerationRule.TwentiethIMM).ToSchedule();
            var cds = new CreditDefaultSwap(ProtectionSide.Seller, 1e6, spread, schedule, BusinessDayConvention.Following,
                new Actual365Fixed());
            CreditDefaultSwapResults result = MidPointCdsEngine.Calculate(cds, h.Context, h.Curve, 0.5, h.Discount);
            results.Add((Name(tenor), result));
        }

        // </reprice>
        return results;
    }

    /// <summary>Example 2: ISDA's rate curve from deposits and swaps, and a reference trade on ISDA's engine.</summary>
    /// <returns>The curve's pillars and the trade's accrual and NPV.</returns>
    public static Isda BuildIsda()
    {
        // <isda>
        // QuantLib's example sets IborCoupon::Settings::createAtParCoupons(), the context's UseAtParCoupons.
        var context = new EvaluationContext { EvaluationDate = new Date(21, Month.October, 2014), UseAtParCoupons = true };
        var helpers = new List<BootstrapHelper<YieldTermStructure>>();
        foreach (var (months, rate) in new[]
                 {
                     (1, 0.000060), (2, 0.000450), (3, 0.000810), (6, 0.001840), (9, 0.002560), (12, 0.003370),
                 })
        {
            helpers.Add(new DepositRateHelper(rate, new Period(months, TimeUnit.Months), 2, new TARGET(),
                BusinessDayConvention.ModifiedFollowing, false, new Actual360(), context));
        }

        var euribor6M = new Euribor(new Period(6, TimeUnit.Months));
        foreach (var (years, rate) in new[]
                 {
                     (2, 0.002230), (3, 0.002760), (4, 0.003530), (5, 0.004520), (6, 0.005720), (7, 0.007050),
                     (8, 0.008420), (9, 0.009720), (10, 0.010900), (12, 0.012870), (15, 0.014970), (20, 0.017000),
                     (30, 0.018210),
                 })
        {
            helpers.Add(new SwapRateHelper(rate, new Period(years, TimeUnit.Years), new TARGET(), Frequency.Annual,
                BusinessDayConvention.ModifiedFollowing, new Thirty360(Thirty360Convention.BondBasis), euribor6M, context));
        }

        var rateCurve = PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>.Create(
            0, new WeekendsOnly(), helpers, new Actual365Fixed(), context);

        FlatHazardRate defaultCurve = FlatHazardRate.Create(0, new WeekendsOnly(), 0.016739207493630, new Actual365Fixed(),
            context);
        var schedule = new Schedule(new Date(22, Month.September, 2014), new Date(20, Month.December, 2019),
            new Period(3, TimeUnit.Months), new WeekendsOnly(), BusinessDayConvention.Following,
            BusinessDayConvention.Unadjusted, DateGenerationRule.CDS, false, context);
        var trade = new CreditDefaultSwap(ProtectionSide.Buyer, 100e6, 0.01, schedule, BusinessDayConvention.Following,
            new Actual360(), true, true, new Date(22, Month.October, 2014), null, new Actual360(true), true);
        CreditDefaultSwapResults result = IsdaCdsEngine.Calculate(trade, context, defaultCurve, 0.4, rateCurve, false,
            IsdaCdsEngineNumericalFix.Taylor, IsdaCdsEngineAccrualBias.NoBias, IsdaCdsEngineForwardsInCouponPeriod.Piecewise);
        // </isda>
        var pillars = helpers.Select(x => x.LatestDate!.Value).Select(d => (d,
            rateCurve.ZeroRate(d, new Actual365Fixed(), Compounding.Continuous, Frequency.Annual, true).Rate,
            rateCurve.Discount(d, true))).ToList();
        double accrued = ((FixedRateCoupon)trade.Coupons[0]).AccruedAmount(new Date(24, Month.October, 2014), context);
        return new Isda(pillars, accrued, result.NPV!.Value);
    }

    private static string Name(Period tenor) => tenor.Length.ToString(CultureInfo.InvariantCulture) +
        (tenor.Units == TimeUnit.Months ? "M" : "Y");

    // C++'s default stream: six significant digits, a small exponent's "e" in lower case.
    private static string G(double value) => value.ToString("G6", CultureInfo.InvariantCulture).Replace('E', 'e');
}
