// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using QLNet;

namespace NanoQL.QLNetVectors;

/// <summary>
/// <c>tests/vectors/qlnet/Instruments/Bonds/MBSFixedRateBond.json</c>: QLNet's <c>MBSFixedRateBond</c> over pools of
/// PSA speeds and CPRs, seasonings, tenors, pass-through rates above and below the WAC, and pay-offs. Each output is
/// QLNet's own, or for a fix QLNet checks, QLNet's own route to it:
/// <list type="bullet">
/// <item>each period's coupon, scheduled principal and prepayment, with the coupon's nominal, and the bond factors:
/// QLNet's expected flows;</item>
/// <item>the payment dates: QLNet's own on a pool paying unadjusted; on one whose convention adjusts on its calendar,
/// the coupon dates of QLNet's level-payment base bond on the same calendar and convention, which QLNet's MBS
/// ignores;</item>
/// <item>a quarterly or semi-annual pool's flows: QLNet's MBS fed a model whose SMM is the period's, one less the
/// product of its months' survival on QLNet's own model;</item>
/// <item>the yields at par on the start date: QLNet's <c>MonthlyYield</c> and <c>BondEquivalentYield</c>; at a seasoned
/// settlement, QLNet's <c>PVDifference</c> and <c>Brent</c> on the pool's balance there, the coupon's nominal, where
/// QLNet's own yield prices its stale level-payment notional;</item>
/// <item>the yields of a pool paid off at the settlement, and of one whose flows QLNet's PSA leaves NaN past the
/// pay-off: QLNet throws;</item>
/// <item>QLNet's <c>Utils.PVDifference</c> at three yields.</item>
/// </list>
/// A 2,000% PSA pool's flows stop at the period before QLNet's first NaN, where QLNet's CPR passes 1.
/// </summary>
internal static class MBSFixedRateBondVectors
{
    private static readonly DayCounter BondBasis = new Thirty360(Thirty360.Thirty360Convention.BondBasis);

    /// <summary>Builds the file's cases.</summary>
    /// <returns>The document.</returns>
    public static VectorDocument Build()
    {
        var doc = new VectorDocument(
            "qlnet/Instruments/Bonds/MBSFixedRateBond", "src/QLNet/Instruments/Bonds/MBSFixedRateBond.cs");
        var start = new Date(1, 2, 2007);
        var psa100 = Psa(start, 1.0);
        Pool("Fabozzi, PSA 100%", start, 358, 360, Frequency.Monthly, 0.06, 0.055, psa100, null, null, doc);
        Pool("Fabozzi, PSA 100%, TARGET Following", start, 358, 360, Frequency.Monthly, 0.06, 0.055, psa100, "TARGET",
             BusinessDayConvention.Following, doc);
        Pool("CPR 8%", start, 358, 360, Frequency.Monthly, 0.06, 0.055, Cpr(0.08), null, null, doc);
        Pool("PSA 250%, seasoned 40 months", start, 320, 360, Frequency.Monthly, 0.07, 0.065, Psa(start, 2.5), null,
             null, doc);
        Pool("CPR 0, 10 years", start, 120, 120, Frequency.Monthly, 0.045, 0.04, Cpr(0.0), null, null, doc);
        Pool("no model", start, 60, 60, Frequency.Monthly, 0.05, 0.045, null, null, null, doc);
        Pool("PSA 0%", start, 240, 240, Frequency.Monthly, 0.05, 0.045, Psa(start, 0.0), null, null, doc);
        Pool("PSA 1000%", start, 240, 240, Frequency.Monthly, 0.05, 0.045, Psa(start, 10.0), null, null, doc);
        Pool("PSA 1500%", start, 360, 360, Frequency.Monthly, 0.08, 0.075, Psa(start, 15.0), null, null, doc);
        Pool("pass-through above the WAC", start, 180, 180, Frequency.Monthly, 0.05, 0.06, Psa(start, 1.5), null, null,
             doc);
        Pool("a 13% pass-through, its flows", start, 120, 120, Frequency.Monthly, 0.135, 0.13, Cpr(0.05), null, null,
             doc, yields: false);
        Pool("PSA 100%, seasoned, settled 2012-02-01", start, 358, 360, Frequency.Monthly, 0.06, 0.055, psa100, null,
             null, doc, new Date(1, 2, 2012));
        Pool("CPR 12%, settled 2009-06-01", start, 240, 240, Frequency.Monthly, 0.07, 0.065, Cpr(0.12), null, null,
             doc, new Date(1, 6, 2009));
        Pool("quarterly, CPR 8%", start, 360, 360, Frequency.Quarterly, 0.06, 0.055, Cpr(0.08), null, null, doc);
        Pool("semi-annual, PSA 150%", start, 360, 360, Frequency.Semiannual, 0.06, 0.055, Psa(start, 1.5), null, null,
             doc);
        Pool("CPR 1 from the start, settled after the pay-off", start, 60, 60, Frequency.Monthly, 0.05, 0.045, Cpr(1.0),
             null, null, doc, new Date(1, 6, 2007), ownYields: true);
        Pool("PSA 2000%", start, 360, 360, Frequency.Monthly, 0.06, 0.055, Psa(start, 20.0), null, null, doc,
             new Date(1, 6, 2009));
        return doc;
    }

    private static (Obj Spec, IPrepayModel Model) Psa(Date start, double multiplier) =>
        (new Obj { ["type"] = "PSACurve", ["startDate"] = start.serialNumber(), ["multiplier"] = multiplier },
         new PSACurve(start, multiplier));

    private static (Obj Spec, IPrepayModel Model) Cpr(double cpr) =>
        (new Obj { ["type"] = "ConstantCPR", ["cpr"] = cpr }, new ConstantCPR(cpr));

    private static void Pool(
        string name,
        Date start,
        int tenor,
        int original,
        Frequency frequency,
        double wac,
        double passThrough,
        (Obj Spec, IPrepayModel Model)? model,
        string? calendarName,
        BusinessDayConvention? convention,
        VectorDocument doc,
        Date? settlement = null,
        bool yields = true,
        bool ownYields = false)
    {
        const double face = 400000000;
        Date settle = settlement ?? start;
        Obj settings = CaseSettings.Set(settle);
        Calendar calendar = calendarName == "TARGET" ? new TARGET() : new NullCalendar();
        BusinessDayConvention paymentConvention = convention ?? BusinessDayConvention.Unadjusted;
        var bondTenor = new Period(tenor, TimeUnit.Months);
        var originalLength = new Period(original, TimeUnit.Months);
        IPrepayModel? qlnetModel = model?.Model;
        if (qlnetModel is not null && frequency != Frequency.Monthly)
        {
            // QLNet's own MBS on the period's SMM: one less the product of its months' survival on QLNet's model.
            qlnetModel = new PeriodModel(qlnetModel, 12 / (int)frequency);
        }

        var bond = new MBSFixedRateBond(0, calendar, face, start, bondTenor, originalLength, frequency, wac,
                                        passThrough, BondBasis, qlnetModel, paymentConvention);
        List<CashFlow> flows = bond.expectedCashflows();
        var coupons = flows.OfType<FixedRateCoupon>().ToList();
        var scheduled = flows.OfType<AmortizingPayment>().ToList();
        var prepay = flows.OfType<VoluntaryPrepay>().ToList();

        // The flows QLNet computes, up to the period before its first NaN (its PSA's CPR past 1).
        int periods = coupons.Count;
        for (int i = 0; i < coupons.Count; i++)
        {
            if (double.IsNaN(coupons[i].amount()) || double.IsNaN(scheduled[i].amount())
                || double.IsNaN(prepay[i].amount()))
            {
                periods = i;
                break;
            }
        }

        var dates = coupons.Take(periods).Select(c => c.date().serialNumber()).ToList();
        if (paymentConvention != BusinessDayConvention.Unadjusted)
        {
            // QLNet's MBS pays on its unadjusted schedule; its level-payment base bond adjusts the same dates.
            var baseBond = new AmortizingFixedRateBond(0, calendar, face, start, bondTenor, frequency, wac, BondBasis,
                                                       paymentConvention);
            dates = baseBond.cashflows().OfType<FixedRateCoupon>().Take(periods)
                .Select(c => c.date().serialNumber()).ToList();
        }

        var expected = new Obj
        {
            ["periods"] = coupons.Count,
            ["paymentDates"] = dates,
            ["coupons"] = coupons.Take(periods).Select(c => Amount(c.amount(), face)).ToList(),
            ["couponNominals"] = coupons.Take(periods).Select(c => Amount(c.nominal(), face)).ToList(),
            ["scheduledPrincipal"] = scheduled.Take(periods).Select(c => Amount(c.amount(), face)).ToList(),
            ["prepayments"] = prepay.Take(periods).Select(c => Amount(c.amount(), face)).ToList(),
            ["bondFactors"] = bond.BondFactors().ToList(),
        };

        if (frequency == Frequency.Monthly && yields)
        {
            if (periods < coupons.Count || settle >= coupons[^1].date() || ownYields)
            {
                // QLNet's own yields throw: on its NaN flows, or on its stale notional after the pay-off.
                expected["monthlyYield"] = Throws(() => bond.MonthlyYield());
                expected["bondEquivalentYield"] = Throws(() => bond.BondEquivalentYield());
            }
            else if (settle == start)
            {
                expected["monthlyYield"] = Attempt(bond.MonthlyYield);
                expected["bondEquivalentYield"] = Attempt(bond.BondEquivalentYield);
            }
            else
            {
                // The pool's balance at the settlement: the nominal of the coupon whose period starts there.
                FixedRateCoupon current = coupons.First(c => c.accrualStartDate() >= settle);
                double y = MonthlyYieldAt(current.nominal(), flows, settle);
                expected["monthlyYield"] = y;
                expected["bondEquivalentYield"] = 2 * (Math.Pow(1 + y, 6) - 1);
            }

            if (settle == start && periods == coupons.Count)
            {
                expected["pvDifference"] = new[] { 0.3, 0.4583333333333333, 0.6 }
                    .Select(y => Amount(Utils.PVDifference(face, flows, y, settle), face)).ToList();
            }
        }

        doc.Case(
            name,
            new Obj
            {
                ["settings"] = settings,
                ["settlementDays"] = 0,
                ["calendar"] = calendarName ?? "NullCalendar",
                ["faceAmount"] = face,
                ["startDate"] = start.serialNumber(),
                ["bondTenorMonths"] = tenor,
                ["originalLengthMonths"] = original,
                ["frequency"] = (int)frequency,
                ["wacRate"] = wac,
                ["passThroughRate"] = passThrough,
                ["dayCounter"] = "Thirty360BondBasis",
                ["model"] = model?.Spec,
                ["paymentConvention"] = (int)paymentConvention,
                ["pvDifferenceYields"] = new[] { 0.3, 0.4583333333333333, 0.6 },
            },
            expected);
    }

    // QLNet's yield solve, MonthlyYield's, on a given balance.
    private static double MonthlyYieldAt(double balance, List<CashFlow> flows, Date settlement)
    {
        var solver = new Brent();
        solver.setMaxEvaluations(100);
        return solver.solve(new MonthlyYieldFinder(balance, flows, settlement), 1.0e-10, 0.02, 0.0, 1.0) / 100;
    }

    private static Obj Amount(double value, double scale) => new() { ["value"] = value, ["scale"] = scale };

    private static object Attempt(Func<double> f)
    {
        try
        {
            return f();
        }
        catch (Exception)
        {
            return new Obj { ["throws"] = true };
        }
    }

    private static object Throws(Func<double> f)
    {
        object result = Attempt(f);
        if (result is not Obj)
        {
            throw new InvalidOperationException("QLNet returned a yield where the case expects its throw");
        }

        return result;
    }

    /// <summary>A model whose SMM is a period's: one less the product of its months' survival on a monthly
    /// model.</summary>
    private sealed class PeriodModel : IPrepayModel
    {
        private readonly IPrepayModel monthly;
        private readonly int months;

        public PeriodModel(IPrepayModel monthly, int months)
        {
            this.monthly = monthly;
            this.months = months;
        }

        public double getCPR(Date valDate) => throw new NotSupportedException();

        public double getSMM(Date valDate)
        {
            double survival = 1;
            for (int k = 0; k < months; k++)
            {
                survival *= 1 - monthly.getSMM(valDate + new Period(k, TimeUnit.Months));
            }

            return 1 - survival;
        }
    }
}
