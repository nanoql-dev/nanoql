// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Instruments;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Reference;

/// <summary>
/// <see cref="PSACurve"/> and <see cref="ConstantCPR"/> against SIFMA's standard prepayment formulas (The Bond Market
/// Association, Uniform Practices, Chapter SF, "Standard Formulas for the Analysis of Mortgage-Backed Securities and
/// Other Related Securities", 02/01/99): SF-6's PSA model,
/// <c>CPR = min{PSA/100 · 0.2 · max{1, min{MONTH, 30}}, 100}</c>, MONTH 1 for every month before the first; SF-5's
/// and SF-6's SMM, <c>1 − (1 − CPR/100)^(1/12)</c>; and cells of SF-10's PSA-to-SMM table, cited by page.
/// </summary>
[Trait("Oracle", "Reference")]
public class SifmaPrepaymentTests
{
    private static readonly Date Start = new(1, March, 2007);

    [Fact]
    public void ThePsaCurveIsSf6sFormulaAtEveryMonthAndSpeed()
    {
        // The vectors' speeds, and three past SF-6's cap at 1,666⅔% PSA; months 1 to 360 from the curve's start, and
        // dates before it, whose MONTH is 1 (SF-6).
        double[] speeds = [0, 50, 100, 150, 250, 500, 1000, 1600, 1667, 1700, 2000];
        foreach (double psa in speeds)
        {
            var curve = new PSACurve(Start, psa / 100);
            for (int month = 1; month <= 360; month++)
            {
                Date d = Start + new Period(month - 1, TimeUnit.Months);
                Within(Sf6Cpr(psa, month) / 100, curve.GetCPR(d), $"{psa}% PSA, MONTH {month}");
                Within(Sf6Smm(Sf6Cpr(psa, month)), curve.GetSMM(d), $"{psa}% PSA, MONTH {month}'s SMM");
            }

            foreach (int daysBefore in new[] { 1, 15, 29, 30, 45, 60, 400 })
            {
                Within(Sf6Cpr(psa, 1) / 100, curve.GetCPR(Start - daysBefore), $"{psa}% PSA, {daysBefore} days before");
            }
        }
    }

    [Theory]
    // SF-6's MONTH rises by one each month, whatever the start's day: from a start on the 29th to the 31st, at each
    // anniversary (a February one on its last day); from a start on the 1st, at each month's last day, still in the
    // month; from a start on the 31st, at a 31-day month's 30th, the day before its anniversary.
    [InlineData(2007, 1, 29)]
    [InlineData(2007, 1, 30)]
    [InlineData(2007, 1, 31)]
    [InlineData(2008, 1, 31)]
    [InlineData(2007, 8, 31)]
    public void TheMonthRisesByOneEachMonthFromAnyStartDay(int year, int month, int day)
    {
        var start = new Date(day, (Month)month, year);
        var curve = new PSACurve(start);
        for (int k = 0; k < 40; k++)
        {
            Date anniversary = start + new Period(k, TimeUnit.Months);
            Within(
                Sf6Cpr(100, k + 1) / 100, curve.GetCPR(anniversary), $"from {start}, MONTH {k + 1} at {anniversary}");
            Date before = anniversary - 1;
            if (k > 0)
            {
                Within(Sf6Cpr(100, k) / 100, curve.GetCPR(before), $"from {start}, MONTH {k} at {before}");
            }
        }

        var first = new PSACurve(new Date(1, March, 2007));
        for (int k = 0; k < 40; k++)
        {
            Date monthEnd = Date.EndOfMonth(new Date(1, March, 2007) + new Period(k, TimeUnit.Months));
            Within(Sf6Cpr(100, k + 1) / 100, first.GetCPR(monthEnd), $"from 2007-03-01, MONTH {k + 1} at {monthEnd}");
        }
    }

    [Fact]
    public void AConstantCprsSmmIsSf5sMonthlyRate()
    {
        foreach (double cpr in new[] { 0.0, 0.1, 6.0, 8.0, 50.0, 100.0 })
        {
            var model = new ConstantCPR(cpr / 100);
            Within(cpr / 100, model.GetCPR(Start), $"CPR {cpr}%");
            Within(Sf6Smm(cpr), model.GetSMM(Start), $"CPR {cpr}%'s SMM");
        }
    }

    [Theory]
    // SF-10, the PSA-to-SMM table: SMM in percent, to two decimals, at MONTH 1, 15 and 30 and 50%, 500% and 1000% PSA.
    [InlineData(1, 50, 0.01)]
    [InlineData(1, 500, 0.08)]
    [InlineData(1, 1000, 0.17)]
    [InlineData(15, 50, 0.13)]
    [InlineData(15, 500, 1.35)]
    [InlineData(15, 1000, 2.93)]
    [InlineData(30, 50, 0.25)]
    [InlineData(30, 500, 2.93)]
    [InlineData(30, 1000, 7.35)]
    public void TheSmmIsSf10sTableCell(int month, double psa, double smmPercent)
    {
        var curve = new PSACurve(Start, psa / 100);
        double smm = curve.GetSMM(Start + new Period(month - 1, TimeUnit.Months)) * 100;

        Assert.Equal(smmPercent, Math.Round(smm, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>SF-6's CPR in percent at a PSA speed in percent and a MONTH.</summary>
    private static double Sf6Cpr(double psa, int month) =>
        Math.Min(psa / 100 * 0.2 * Math.Max(1, Math.Min(month, 30)), 100);

    /// <summary>SF-6's SMM, a fraction, from a CPR in percent.</summary>
    private static double Sf6Smm(double cprPercent) => 1 - Math.Pow(1 - (cprPercent / 100), 1.0 / 12);

    /// <summary>The vectors' measure (docs/porting-playbook.md §7.3) at scale 1: the formulas round apart.</summary>
    private static void Within(double expected, double actual, string what) =>
        Assert.True(
            Math.Abs(expected - actual) <= 1e-10 * Math.Max(Math.Max(Math.Abs(expected), Math.Abs(actual)), 1.0),
            $"{what}: SIFMA {expected:R}, nanoql {actual:R}");
}
