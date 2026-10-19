// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's constant callable-bond volatility vectors,
/// <c>tests/vectors/cxx/experimental/callablebonds/callablebondconstantvol.json</c>: structures by reference date and
/// by settlement days, their inspectors, their volatilities and variances by time, date and tenor with and without
/// extrapolation, their smile sections, and their date conversions, with the range checks' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CallableBondConstantVolCxxVectorTests
{
    [Fact]
    public void CallableBondConstantVolCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/callablebonds/callablebondconstantvol");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CallableBondVolatilityStructure vol = CallableBondInputs.VolatilityOf(c.Input("volatility"), context);
            double strike = Number(c.Input("strike"));
            check.Output(c, "referenceDate", vol.ReferenceDate);
            check.Output(c, "dayCounter", vol.DayCounter.Name);
            check.Output(c, "maxDate", vol.MaxDate);
            check.Output(c, "maxTime", vol.MaxTime);
            check.Output(c, "maxBondTenor", new object[] { vol.MaxBondTenor.Length, vol.MaxBondTenor.Units });
            check.Output(c, "maxBondLength", vol.MaxBondLength());
            check.Output(c, "minStrike", vol.MinStrike());
            check.Output(c, "maxStrike", vol.MaxStrike());
            check.Output(c, "businessDayConvention", vol.BusinessDayConvention);
            check.Output(c, "settlementDays", vol.SettlementDays);

            double[][] times = [.. c.Input("timePoints").EnumerateArray().Select(Numbers)];
            (Date Date, Period Tenor)[] dates =
            [
                .. c.Input("datePoints").EnumerateArray()
                    .Select(p => (new Date(p[0].GetInt32()), SwapInputs.PeriodOf(p[1]))),
            ];
            (Period Option, Period Tenor)[] tenors =
            [
                .. c.Input("tenorPoints").EnumerateArray()
                    .Select(p => (SwapInputs.PeriodOf(p[0]), SwapInputs.PeriodOf(p[1]))),
            ];
            foreach (bool extrapolate in new[] { false, true })
            {
                string suffix = extrapolate ? "Extrapolated" : string.Empty;
                check.Output(
                    c, "volatilityByTime" + suffix, Each(times, p => vol.Volatility(p[0], p[1], p[2], extrapolate)));
                check.Output(
                    c,
                    "blackVarianceByTime" + suffix,
                    Each(times, p => vol.BlackVariance(p[0], p[1], p[2], extrapolate)));
                check.Output(
                    c,
                    "volatilityByDate" + suffix,
                    Each(dates, p => vol.Volatility(p.Date, p.Tenor, strike, extrapolate)));
                check.Output(
                    c,
                    "blackVarianceByDate" + suffix,
                    Each(dates, p => vol.BlackVariance(p.Date, p.Tenor, strike, extrapolate)));
                check.Output(
                    c,
                    "volatilityByTenor" + suffix,
                    Each(tenors, p => vol.Volatility(p.Option, p.Tenor, strike, extrapolate)));
                check.Output(
                    c,
                    "blackVarianceByTenor" + suffix,
                    Each(tenors, p => vol.BlackVariance(p.Option, p.Tenor, strike, extrapolate)));
            }

            check.Output(
                c,
                "smileSectionByDate",
                Each(dates, p => SmileSectionInputs.Summary(vol.SmileSection(p.Date, p.Tenor), strike)));
            check.Output(
                c,
                "smileSectionByTenor",
                Each(tenors, p => SmileSectionInputs.Summary(vol.SmileSection(p.Option, p.Tenor), strike)));
            check.Output(c, "convertDates", Each(dates, p => Pair(vol.ConvertDates(p.Date, p.Tenor))));
            check.Output(c, "optionDateFromTenor", Each(tenors, p => vol.OptionDateFromTenor(p.Option)));
        }

        check.Complete();
    }

    private static object?[] Pair((double OptionTime, double BondLength) p) => [p.OptionTime, p.BondLength];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
