// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's optionlet volatility structure vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/optionlet/optionletvolatilitystructure.json</c>:
/// <see cref="OptionletVolatilityStructure.Volatility(Period, double, bool)"/> and
/// <see cref="OptionletVolatilityStructure.BlackVariance(Period, double, bool)"/>, which the wheel doesn't expose; and
/// the date, time and strike refusals of the date and time overloads, with and without the extrapolate argument, on
/// structures as built and with extrapolation enabled; the base's default
/// <see cref="OptionletVolatilityStructure.VolatilityType"/> and <see cref="OptionletVolatilityStructure.Displacement"/>,
/// on the harness's own structure, which keeps them; and the smile sections by tenor, date and time, the constant
/// structure's and, through the base's default by date, the harness structure's.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OptionletVolatilityStructureCxxVectorTests
{
    [Fact]
    public void OptionletVolatilityStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/optionlet/optionletvolatilitystructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            OptionletVolatilityStructure surface = VolatilityInputs.OptionletVolatilityOf(c.Input("surface"), c.Context());
            switch (c.String("kind"))
            {
                case "periods":
                    CheckPeriods(check, c, surface);
                    break;
                case "defaults":
                    check.Output(c, "volatilityType", surface.VolatilityType);
                    check.Output(c, "displacement", surface.Displacement);
                    CheckPeriods(check, c, surface);
                    break;
                case "refusals":
                    CheckRefusals(check, c, surface);
                    break;
                case "smileSections":
                    CheckSmileSections(check, c, surface);
                    break;
                default:
                    Assert.Fail($"unexpected case kind in '{c.Name}'");
                    break;
            }
        }

        check.Complete();
    }

    private static void CheckPeriods(VectorAssert check, VectorCase c, OptionletVolatilityStructure surface)
    {
        Period[] tenors = c.Input("tenors").EnumerateArray().Select(VolatilityInputs.TenorOf).ToArray();
        double[] strikes = c.Doubles("strikes");
        check.Output(c, "volatility", VolatilityInputs.Grid(tenors, strikes, (p, k) => surface.Volatility(p, k)));
        check.Output(c, "blackVariance", VolatilityInputs.Grid(tenors, strikes, (p, k) => surface.BlackVariance(p, k)));
    }

    private static void CheckSmileSections(VectorAssert check, VectorCase c, OptionletVolatilityStructure surface)
    {
        Period[] tenors = c.Input("tenors").EnumerateArray().Select(VolatilityInputs.TenorOf).ToArray();
        Date[] dates = c.DateInputs("dates");
        double[] times = VolatilityInputs.Reals(c.Input("times"));
        bool[] tenorExtrapolate = InterpolationInputs.Bools(c.Input("tenorExtrapolate"));
        bool[] dateExtrapolate = InterpolationInputs.Bools(c.Input("dateExtrapolate"));
        bool[] timeExtrapolate = InterpolationInputs.Bools(c.Input("timeExtrapolate"));
        double strike = c.Double("strike");

        check.Output(
            c,
            "atTenors",
            InterpolationInputs.Deferred(
                Enumerable.Range(0, tenors.Length),
                i => SmileSectionInputs.Summary(surface.SmileSection(tenors[i], tenorExtrapolate[i]), strike)));
        check.Output(
            c,
            "atDates",
            InterpolationInputs.Deferred(
                Enumerable.Range(0, dates.Length),
                i => SmileSectionInputs.Summary(surface.SmileSection(dates[i], dateExtrapolate[i]), strike)));
        check.Output(
            c,
            "atTimes",
            InterpolationInputs.Deferred(
                Enumerable.Range(0, times.Length),
                i => SmileSectionInputs.Summary(surface.SmileSection(times[i], timeExtrapolate[i]), strike)));
    }

    private static void CheckRefusals(VectorAssert check, VectorCase c, OptionletVolatilityStructure surface)
    {
        Date[] dates = c.DateInputs("dates");
        double[] times = VolatilityInputs.Reals(c.Input("times"));
        double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
        bool[] extrapolate = c.Input("extrapolate").EnumerateArray().Select(e => e.GetBoolean()).ToArray();
        double strike = c.Double("strike");
        Date date = c.DateInput("date");
        double time = c.Double("time");

        check.Output(c, "volatilityAtDates", VolatilityInputs.Grid(dates, extrapolate, (d, e) => surface.Volatility(d, strike, e)));
        check.Output(c, "volatilityAtTimes", VolatilityInputs.Grid(times, extrapolate, (t, e) => surface.Volatility(t, strike, e)));
        check.Output(
            c, "volatilityAtStrikesOnDate", VolatilityInputs.Grid(strikes, extrapolate, (k, e) => surface.Volatility(date, k, e)));
        check.Output(
            c, "volatilityAtStrikesOnTime", VolatilityInputs.Grid(strikes, extrapolate, (k, e) => surface.Volatility(time, k, e)));
        check.Output(
            c, "blackVarianceAtDates", VolatilityInputs.Grid(dates, extrapolate, (d, e) => surface.BlackVariance(d, strike, e)));
        check.Output(
            c, "blackVarianceAtTimes", VolatilityInputs.Grid(times, extrapolate, (t, e) => surface.BlackVariance(t, strike, e)));
        check.Output(
            c,
            "blackVarianceAtStrikesOnDate",
            VolatilityInputs.Grid(strikes, extrapolate, (k, e) => surface.BlackVariance(date, k, e)));
        check.Output(
            c,
            "blackVarianceAtStrikesOnTime",
            VolatilityInputs.Grid(strikes, extrapolate, (k, e) => surface.BlackVariance(time, k, e)));
    }
}
