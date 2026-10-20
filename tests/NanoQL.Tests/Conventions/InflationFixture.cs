// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The inflation facts' shared market, as the wheel's runs that recorded QuantLib's values for the divergences built
/// it: the evaluation date 15 June 2024; UK RPI's fixings on the first of each month from January 2023 to April 2024,
/// from 300 growing by 0.07% times the month number modulo 5 plus 0.11% times the year modulo 3 a month; a
/// zero-inflation curve flat at 3% and a year-on-year curve flat at 3%, each based in April 2024; and a nominal curve
/// flat at 4%, Actual/365 (Fixed).
/// </summary>
internal static class InflationFixture
{
    public static readonly Date Today = new(15, Month.June, 2024);

    /// <summary>A context on <see cref="Today"/> holding UK RPI's fixings.</summary>
    /// <returns>The context.</returns>
    public static EvaluationContext Context()
    {
        var rpi = new UKRPI();
        var context = new EvaluationContext { EvaluationDate = Today };
        double v = 300.0;
        var d = new Date(1, Month.January, 2023);
        while (d <= new Date(1, Month.April, 2024))
        {
            context = context with { Fixings = context.Fixings.AddFixing(rpi, d, v) };
            v *= 1.0 + (0.0007 * ((int)d.Month % 5)) + (0.0011 * (d.Year % 3));
            d += new Period(1, TimeUnit.Months);
        }

        return context;
    }

    public static ZeroInflationCurve ZeroCurve() =>
        new(
            Today,
            [new Date(1, Month.April, 2024), new Date(1, Month.April, 2034)],
            [0.03, 0.03],
            Frequency.Monthly,
            new Actual365Fixed());

    public static YoYInflationCurve YoYCurve(Date[] dates, double[] rates) =>
        new(Today, dates, rates, Frequency.Monthly, new Actual365Fixed());

    public static FlatForward Nominal() => new(Today, 0.04, new Actual365Fixed());
}
