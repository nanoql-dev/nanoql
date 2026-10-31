// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using QLNet;

namespace NanoQL.QLNetVectors;

/// <summary>
/// <c>tests/vectors/qlnet/Instruments/Bonds/PSACurve.json</c>: QLNet's <c>PSACurve</c> at ages 0 to 360 months from
/// two starts, QLNet's own test's 1 March 2007 and a month end, 31 January 2007, whose later dates fall on every month
/// end Bond Basis counts apart; and from 1 March 2007 on each month's last day, where Bond Basis counts a 31st end from
/// a start before the 30th as the 31st; at eight speeds from 0 to 1,600% PSA: each date's CPR and SMM. Every value is
/// below SIFMA's cap and from the curve's first month, where nanoql's rates are QLNet's (SF-6). A date where QLNet's
/// month, its Bond Basis days over 30 plus one, isn't SF-6's, the whole calendar months plus one (a February
/// anniversary from a start on the 29th to the 31st, a 31st from a start on the 1st), is left out: the reader compares
/// the dates the case lists, and <c>Divergences/QLNet/PsaMonthCountTests</c> records QLNet's month there.
/// </summary>
internal static class PSACurveVectors
{
    private static readonly double[] Multipliers = [0.0, 0.5, 1.0, 1.5, 2.5, 5.0, 10.0, 16.0];

    /// <summary>Builds the file's cases.</summary>
    /// <returns>The document.</returns>
    public static VectorDocument Build()
    {
        var doc = new VectorDocument("qlnet/Instruments/Bonds/PSACurve", "src/QLNet/Instruments/Bonds/PSACurve.cs");
        var starts = new (Date Start, bool MonthEnds)[]
        {
            (new Date(1, 3, 2007), false), (new Date(31, 1, 2007), false), (new Date(1, 3, 2007), true),
        };
        foreach ((Date start, bool monthEnds) in starts)
        {
            var dates = Enumerable.Range(0, 361)
                .Select(age => start + new Period(age, TimeUnit.Months))
                .Select(d => monthEnds ? Date.endOfMonth(d) : d)
                .Where(d => Math.Min(QLNetMonth(start, d), 30) == Math.Min(Sf6Month(start, d), 30))
                .ToList();
            string at = monthEnds ? " at month ends" : string.Empty;
            foreach (double multiplier in Multipliers)
            {
                Obj settings = CaseSettings.Set(start);
                var curve = new PSACurve(start, multiplier);
                doc.Case(
                    $"PSA {multiplier * 100}% from {start.serialNumber()}{at}",
                    new Obj
                    {
                        ["settings"] = settings,
                        ["startDate"] = start.serialNumber(),
                        ["multiplier"] = multiplier,
                        ["dates"] = dates.Select(d => d.serialNumber()).ToList(),
                    },
                    new Obj
                    {
                        ["CPR"] = dates.Select(curve.getCPR).ToList(),
                        ["SMM"] = dates.Select(curve.getSMM).ToList(),
                    });
            }
        }

        return doc;
    }

    // QLNet's month: the Bond Basis days from the start over 30, plus one (PSACurve.cs:37-38).
    private static int QLNetMonth(Date start, Date d) =>
        (new Thirty360(Thirty360.Thirty360Convention.BondBasis).dayCount(start, d) / 30) + 1;

    // SF-6's MONTH: the largest k whose k-month anniversary of the start is on or before the date, plus one.
    private static int Sf6Month(Date start, Date d)
    {
        int k = 0;
        while (start + new Period(k + 1, TimeUnit.Months) <= d)
        {
            k++;
        }

        return k + 1;
    }
}
