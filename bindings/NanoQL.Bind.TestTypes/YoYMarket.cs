// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Bind.TestTypes;

/// <summary>
/// The YoY optionlet case's market, from nanoql's InflationVolatilityTests setup: the EUR nominal curve, the EU HICP
/// year-on-year index on its curve, and a volatility surface for the helper's trial.
/// </summary>
public sealed class YoYMarket
{
    private static readonly Date Eval = new(23, Month.November, 2007);

    private YoYMarket()
    {
        Context = new EvaluationContext { EvaluationDate = Eval };
        double[] times =
        [
            0.0109589, 0.0684932, 0.263014, 0.317808, 0.567123, 0.816438, 1.06575, 1.31507, 1.56438, 2.0137, 3.01918, 4.01644,
            5.01644, 6.01644, 7.01644, 8.01644, 9.02192, 10.0192, 12.0192, 15.0247, 20.0301, 25.0356, 30.0329, 40.0384, 50.0466,
        ];
        double[] rates =
        [
            0.0415600, 0.0426840, 0.0470980, 0.0458506, 0.0449550, 0.0439784, 0.0431887, 0.0426604, 0.0422925, 0.0424591, 0.0421477,
            0.0421853, 0.0424016, 0.0426969, 0.0430804, 0.0435011, 0.0439368, 0.0443825, 0.0452589, 0.0463389, 0.0472636, 0.0473401,
            0.0470629, 0.0461092, 0.0450794,
        ];
        var d = new List<Date>();
        foreach (double t in times)
        {
            int ys = (int)Math.Floor(t);
            int ds = (int)((t - ys) * 365);
            d.Add(Eval + new Period(ys, TimeUnit.Years) + new Period(ds, TimeUnit.Days));
        }

        Nominal = new InterpolatedZeroCurve<Cubic>(d, rates, new Actual365Fixed());
        double[] yoyRates =
        [
            0.0237951, 0.0238749, 0.0240334, 0.0241934, 0.0243567, 0.0245323, 0.0247213, 0.0249348, 0.0251768, 0.0254337, 0.0257258,
            0.0260217, 0.0263006, 0.0265538, 0.0267803, 0.0269378, 0.0270608, 0.0271363, 0.0272, 0.0272512, 0.0272927, 0.027317,
            0.0273615, 0.0273811, 0.0274063, 0.0274307, 0.0274625, 0.027527, 0.0275952, 0.0276734, 0.027794,
        ];
        var hicp = new EUHICP();
        Date baseDate = InflationTermStructureFunctions.InflationPeriod(Eval - new Period(1, TimeUnit.Months), hicp.Frequency).Start;
        var yd = new List<Date> { baseDate };
        var target = new TARGET();
        Date capStart = target.Advance(Eval, -2, TimeUnit.Months, BusinessDayConvention.ModifiedFollowing);
        for (int i = 1; i < yoyRates.Length; i++)
        {
            yd.Add(target.Advance(capStart, i, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing));
        }

        Index = new YoYInflationIndex(hicp, new InterpolatedYoYInflationCurve<Linear>(Eval, yd, yoyRates, Frequency.Monthly, new Actual365Fixed()));
        Date[] dates = [new(1, Month.August, 2007), new(23, Month.November, 2008), new(23, Month.November, 2009), new(23, Month.November, 2012)];
        Surface = InterpolatedYoYOptionletVolatilityCurve<Linear>.Create(
            0, target, BusinessDayConvention.ModifiedFollowing, new Actual365Fixed(), new Period(3, TimeUnit.Months),
            Frequency.Monthly, false, dates, [0.01, 0.009, 0.008, 0.0075], -1.0, 3.0, Context);
    }

    /// <summary>Gets the market.</summary>
    public static YoYMarket Instance { get; } = new();

    /// <summary>Gets the market's evaluation context.</summary>
    public EvaluationContext Context { get; }

    /// <summary>Gets the EUR nominal curve.</summary>
    public YieldTermStructure Nominal { get; }

    /// <summary>Gets the EU HICP year-on-year index on its curve.</summary>
    public YoYInflationIndex Index { get; }

    /// <summary>Gets a volatility surface for the helper's trial.</summary>
    public YoYOptionletVolatilitySurface Surface { get; }
}
