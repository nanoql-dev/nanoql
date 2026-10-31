// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using QLNet;

namespace NanoQL.QLNetVectors;

/// <summary>
/// <c>tests/vectors/qlnet/Instruments/Bonds/AmortizingBond.json</c>: QLNet's <c>AmortizingBond</c> on purchases on a
/// coupon date, at a premium and a discount, at four frequencies, on 30/360 US, 30/360 Bond Basis and Actual/Actual
/// (ISMA), and on purchases on the first coupon period's start with QLNet's evaluation date there. Each output is
/// QLNet's own, or for a fix QLNet checks, QLNet's own route to it:
/// <list type="bullet">
/// <item>the effective rate: QLNet's <c>Yield()</c>;</item>
/// <item>at that rate, each amortisation but the last, signed (negative where a premium amortises), and the carrying
/// amount at each coupon date before maturity: QLNet's entries and <c>AmortizationValue</c>;</item>
/// <item>the carrying amount between coupon dates: QLNet's <c>AmortizationValue</c> at the period's start plus QLNet's
/// own <c>FixedRateCoupon</c> amounts since then, at the effective rate on that carrying amount less at the coupon rate
/// on the face amount, each on the coupon's reference period, where QLNet accrues on the cost with no reference
/// period.</item>
/// </list>
/// The last amortisation, which nanoql closes to the face amount, and purchases between coupon dates, which nanoql
/// solves at the trade date, are divergences with checks of their own.
/// </summary>
internal static class AmortizingBondVectors
{
    /// <summary>Builds the file's cases.</summary>
    /// <returns>The document.</returns>
    public static VectorDocument Build()
    {
        var doc = new VectorDocument(
            "qlnet/Instruments/Bonds/AmortizingBond", "src/QLNet/Instruments/Bonds/AmortizingBond.cs");
        var us = (Name: "Thirty360USA", Counter: (DayCounter)new Thirty360(Thirty360.Thirty360Convention.USA));
        var bb = (Name: "Thirty360BondBasis",
                  Counter: (DayCounter)new Thirty360(Thirty360.Thirty360Convention.BondBasis));
        var isma = (Name: "ActualActualISMA", Counter: (DayCounter)new ActualActual(ActualActual.Convention.ISMA));
        Case("AB1, a premium on a coupon date", 40000, 43412, 0.06, D(1, 1, 2001), D(1, 1, 2005), D(1, 1, 2004),
             Frequency.Semiannual, us, doc);
        Case("a discount, quarterly", 100000, 95000, 0.05, D(15, 3, 2010), D(15, 3, 2020), D(15, 6, 2013),
             Frequency.Quarterly, us, doc);
        Case("a premium, annual, month ends on Bond Basis", 1000000, 1040000, 0.07, D(31, 7, 2005), D(31, 7, 2025),
             D(31, 7, 2010), Frequency.Annual, bb, doc);
        Case("a discount, monthly", 250000, 240000, 0.04, D(15, 1, 2015), D(15, 1, 2020), D(15, 4, 2016),
             Frequency.Monthly, us, doc);
        Case("a discount, semi-annual, Actual/Actual ISMA", 500000, 480000, 0.045, D(15, 5, 2012), D(15, 5, 2032),
             D(15, 11, 2018), Frequency.Semiannual, isma, doc);
        Case("a premium near par, semi-annual, Actual/Actual ISMA", 1000000, 1003000, 0.03, D(30, 4, 2014),
             D(30, 4, 2024), D(30, 10, 2016), Frequency.Semiannual, isma, doc);
        Case("a premium, semi-annual, Bond Basis", 200000, 214000, 0.065, D(28, 2, 2008), D(28, 2, 2018),
             D(28, 8, 2012), Frequency.Semiannual, bb, doc);
        Case("a purchase at the issue date, semi-annual", 100000, 102000, 0.05, D(15, 3, 2010), D(15, 3, 2015),
             D(15, 3, 2010), Frequency.Semiannual, us, doc);
        Case("a purchase at the issue date, quarterly discount", 50000, 47500, 0.035, D(1, 6, 2011), D(1, 6, 2021),
             D(1, 6, 2011), Frequency.Quarterly, us, doc);
        return doc;
    }

    private static Date D(int d, int m, int y) => new(d, m, y);

    private static void Case(
        string name,
        double face,
        double cost,
        double coupon,
        Date issue,
        Date maturity,
        Date trade,
        Frequency frequency,
        (string Name, DayCounter Counter) dc,
        VectorDocument doc)
    {
        // A purchase on the first period's start reads QLNet's evaluation date (no coupon before it): set there.
        Obj settings = CaseSettings.Set(trade);
        var bond = new AmortizingBond(face, cost, coupon, issue, maturity, trade, frequency, dc.Counter,
                                      AmortizingMethod.EffectiveInterestRate, new NullCalendar());
        double yield = bond.Yield();
        double sign = bond.isPremium() ? -1 : 1;
        var entries = bond.cashflows().OfType<AmortizingPayment>().ToList();
        var coupons = bond.cashflows().OfType<FixedRateCoupon>().ToList();

        // Between coupon dates: the 10th and the 20th day after each coupon date (and the trade) before maturity.
        var betweenDates = new List<int>();
        var between = new List<object>();
        Date last = trade;
        for (int k = 0; k < entries.Count; k++)
        {
            Date end = entries[k].date();
            double lastValue = bond.AmortizationValue(last);
            FixedRateCoupon period = coupons[k];
            foreach (int offset in new[] { 10, 20 })
            {
                Date d = last + offset;
                if (d >= end)
                {
                    continue;
                }

                // QLNet's own coupons on the period's reference period, on the carrying amount at its start.
                var atYield = new FixedRateCoupon(d, lastValue, new InterestRate(yield, dc.Counter, Compounding.Simple,
                    frequency), last, d, period.referencePeriodStart, period.referencePeriodEnd);
                var atCoupon = new FixedRateCoupon(d, face, new InterestRate(coupon, dc.Counter, Compounding.Simple,
                    frequency), last, d, period.referencePeriodStart, period.referencePeriodEnd);
                betweenDates.Add(d.serialNumber());
                between.Add(Amount(lastValue + (atYield.amount() - atCoupon.amount()), face));
            }

            last = end;
        }

        doc.Case(
            name,
            new Obj
            {
                ["settings"] = settings,
                ["faceValue"] = face,
                ["marketValue"] = cost,
                ["couponRate"] = coupon,
                ["issueDate"] = issue.serialNumber(),
                ["maturityDate"] = maturity.serialNumber(),
                ["tradeDate"] = trade.serialNumber(),
                ["frequency"] = (int)frequency,
                ["dayCounter"] = dc.Name,
                ["calendar"] = "NullCalendar",
                ["betweenDates"] = betweenDates,
            },
            new Obj
            {
                ["yield"] = yield,
                ["isPremium"] = bond.isPremium(),
                ["amortizationDates"] = entries.Select(e => e.date().serialNumber()).ToList(),
                ["amortizations"] = entries.Take(entries.Count - 1)
                    .Select(e => Amount(sign * e.amount(), face)).ToList(),
                ["carrying"] = entries.Take(entries.Count - 1)
                    .Select(e => Amount(bond.AmortizationValue(e.date()), face)).ToList(),
                ["between"] = between,
            });
    }

    private static Obj Amount(double value, double scale) => new() { ["value"] = value, ["scale"] = scale };
}
