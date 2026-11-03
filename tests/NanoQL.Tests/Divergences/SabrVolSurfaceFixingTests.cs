// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The SABR volatility surface's fixing read. QuantLib's smile at time t reads the index's fixing on the date ⌊365t⌋
/// days after the reference date, which the index refuses ("Fixing date … is not valid") on two days in seven (QuantLib
/// 1.44); nanoql reads it on that date adjusted on the fixing calendar by Following, the adjustment QuantLib's
/// interest-rate surface makes to its reference date, the identity on a fixing date. Checks: on a weekend the section's
/// forward is Monday's fixing and its date the weekend's; on business days the forward is the day's own fixing.
/// </summary>
public class SabrVolSurfaceFixingTests
{
    private static readonly Date Today = new(15, Month.May, 2026); // a Friday
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void OnAWeekendTheSectionReadsMondaysFixing()
    {
        (SabrVolSurface surface, Euribor index) = Surface();
        foreach (int days in new[] { 50, 51 }) // Saturday 4 July and Sunday 5 July 2026
        {
            Date d = Today + days;
            Assert.False(index.IsValidFixingDate(d));
            SmileSection section = surface.SmileSection((days + 0.5) / 365.0, true);
            Date monday = index.FixingCalendar.Adjust(d, BusinessDayConvention.Following);
            Assert.Equal(index.Fixing(monday, Context, true), section.AtmLevel());
            Assert.Equal(d, section.ExerciseDate);
        }
    }

    [Fact]
    public void OnABusinessDayTheSectionReadsItsOwnFixing()
    {
        (SabrVolSurface surface, Euribor index) = Surface();
        foreach (int days in new[] { 46, 60, 90, 180 })
        {
            Date d = Today + days;
            Assert.True(index.IsValidFixingDate(d), $"{d}");
            SmileSection section = surface.SmileSection((days + 0.5) / 365.0, true);
            Assert.Equal(index.Fixing(d, Context, true), section.AtmLevel());
        }
    }

    // a SABR surface on Euribor 6M over an abcd at-the-money curve, the volatility spreads flat in the tenors; the
    // curve, as QuantLib's, refuses times before its first tenor
    internal static (SabrVolSurface Surface, Euribor Index) Surface()
    {
        var index = new Euribor(new Period(6, TimeUnit.Months), new FlatForward(Today, 0.03, A365));
        Period[] tenors = [new(1, TimeUnit.Months), new(3, TimeUnit.Months), new(6, TimeUnit.Months), new(1, TimeUnit.Years), new(2, TimeUnit.Years)];
        AbcdAtmVolCurve atm = AbcdAtmVolCurve.Create(0, new TARGET(), tenors, [0.30, 0.28, 0.25, 0.22, 0.20], Context);
        double[] spreads = [-0.01, -0.005, 0.0, 0.005, 0.01];
        double[][] volSpreads = [.. tenors.Select(_ => new[] { 0.03, 0.012, 0.0, -0.005, -0.006 })];
        return (new SabrVolSurface(index, atm, tenors, spreads, volSpreads, Context), index);
    }
}
