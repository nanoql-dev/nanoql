// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: Examples/BermudanSwaption/BermudanSwaption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using CultureInfo = System.Globalization.CultureInfo;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>
/// Short-rate models: QuantLib's BermudanSwaption example, G2 and Hull-White calibrated to five co-terminal swaptions,
/// then a Bermudan swaption priced on trees and by finite differences. Black-Karasinski, the example's fourth model,
/// is left out: no engine but the tree prices its swaptions, so nothing checks its values.
/// </summary>
public static class ShortRateModels
{
    /// <summary>The mark of a value G2's coupons fix moves (the divergences page's G2AnalyticSwaptionCouponTests).</summary>
    public const string G2Mark = "†";

    /// <summary>The mark of a value the tree's dates fix moves (the divergences page's TreeSwaptionSnappingTests).</summary>
    public const string TreeMark = "‡";

    /// <summary>Gets the example's evaluation context: 15 February 2002.</summary>
    public static EvaluationContext Context { get; } = new() { EvaluationDate = new Date(15, Month.February, 2002) };

    /// <summary>The example's outputs.</summary>
    /// <returns>The calibrations' model and market vols, the calibrated parameters and the Bermudan prices.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        Result r = Run();
        var vols = new Table("Model", "Swaption", ">Model vol", ">Market vol");
        foreach (Calibration c in r.Calibrations)
        {
            for (int i = 0; i < c.ModelVols.Count; ++i)
            {
                vols.Add(c.Name + c.Mark, r.Helpers[i], Table.Percent(c.ModelVols[i], 5), Table.Percent(r.MarketVols[i], 5));
            }
        }

        var parameters = new Table("Model", "Calibrated parameters");
        foreach (Calibration c in r.Calibrations)
        {
            parameters.Add(c.Name + c.Mark, string.Join(", ", c.Parameters.Select(p => $"{p.Name} {G5(p.Value)}")));
        }

        var bermudans = new Table("Strike", ">G2, tree " + G2Mark + TreeMark, ">G2, FD " + G2Mark,
            ">Hull-White, tree " + TreeMark, ">Hull-White, FD", ">Hull-White numerical, tree " + TreeMark,
            ">Hull-White numerical, FD " + TreeMark);
        foreach (Bermudan b in r.Bermudans)
        {
            bermudans.Add($"{b.Label}, {Table.Percent(b.Strike, 5)}", G5(b.G2Tree), G5(b.G2Fd), G5(b.HullWhiteTree),
                G5(b.HullWhiteFd), G5(b.NumericalTree), G5(b.NumericalFd));
        }

        yield return ("short-rate-vols", vols);
        yield return ("short-rate-parameters", parameters);
        yield return ("short-rate-bermudans", bermudans);
    }

    /// <summary>A model's calibration: its name, its mark, its model vols by helper and its parameters.</summary>
    /// <param name="Name">The model's name.</param>
    /// <param name="Mark">Its mark, empty when no divergence moves its values.</param>
    /// <param name="Model">The calibrated model.</param>
    /// <param name="ModelValues">Each helper's value on the calibrated model.</param>
    /// <param name="ModelVols">Each helper's value as a Black vol.</param>
    /// <param name="Parameters">The calibrated parameters, named.</param>
    public sealed record Calibration(
        string Name,
        string Mark,
        CalibratedModel Model,
        IReadOnlyList<double> ModelValues,
        IReadOnlyList<double> ModelVols,
        IReadOnlyList<(string Name, double Value)> Parameters);

    /// <summary>A Bermudan swaption's prices at one strike.</summary>
    /// <param name="Label">ATM, OTM or ITM.</param>
    /// <param name="Strike">The fixed rate.</param>
    /// <param name="G2Tree">On G2's tree.</param>
    /// <param name="G2Fd">On G2 by finite differences.</param>
    /// <param name="HullWhiteTree">On the analytically calibrated Hull-White's tree.</param>
    /// <param name="HullWhiteFd">On the analytically calibrated Hull-White by finite differences.</param>
    /// <param name="NumericalTree">On the numerically calibrated Hull-White's tree.</param>
    /// <param name="NumericalFd">On the numerically calibrated Hull-White by finite differences.</param>
    public sealed record Bermudan(
        string Label,
        double Strike,
        double G2Tree,
        double G2Fd,
        double HullWhiteTree,
        double HullWhiteFd,
        double NumericalTree,
        double NumericalFd);

    /// <summary>The example's results.</summary>
    /// <param name="Helpers">The helpers' names, expiry by swap length.</param>
    /// <param name="MarketVols">The helpers' market vols.</param>
    /// <param name="Calibrations">G2's, Hull-White's and the numerical Hull-White's calibrations.</param>
    /// <param name="Bermudans">The Bermudan swaption's prices at the money, out of it and in it.</param>
    /// <param name="Swaptions">The helpers with no engine, to price on another engine or grid.</param>
    /// <param name="Times">The helpers' times, which the tree's grid holds.</param>
    public sealed record Result(
        IReadOnlyList<string> Helpers,
        IReadOnlyList<double> MarketVols,
        IReadOnlyList<Calibration> Calibrations,
        IReadOnlyList<Bermudan> Bermudans,
        IReadOnlyList<SwaptionHelper> Swaptions,
        IReadOnlyList<double> Times);

    /// <summary>Runs the example.</summary>
    /// <returns>Its calibrations and prices.</returns>
    public static Result Run()
    {
        EvaluationContext context = Context;
        // <market>
        Calendar calendar = new TARGET();
        var settlement = new Date(19, Month.February, 2002);
        YieldTermStructure curve = new FlatForward(settlement, 0.04875825, new Actual365Fixed());
        var index = new Euribor6M(curve);

        // The co-terminal swaptions: 1Y into 5Y, 2Y into 4Y, ... 5Y into 1Y.
        double[] marketVols = [0.1148, 0.1108, 0.1070, 0.1021, 0.1000];
        var helpers = new List<SwaptionHelper>();
        var times = new List<double>();
        for (int i = 0; i < 5; ++i)
        {
            var helper = new SwaptionHelper(new Period(i + 1, TimeUnit.Years), new Period(5 - i, TimeUnit.Years),
                marketVols[i], index, index.Tenor, index.DayCounter, index.DayCounter, curve, context);
            helpers.Add(helper);
            times.AddRange(helper.Times(context));
        }

        var grid = new TimeGrid([.. times], 30);
        // </market>

        // <calibrate>
        var optimizer = new LevenbergMarquardt();
        var endCriteria = new EndCriteria(400, 100, 1.0e-8, 1.0e-8, 1.0e-8);
        (TModel Model, List<SwaptionHelper> Helpers) Calibrate<TModel>(
            TModel model, Func<Swaption, EvaluationContext, TModel, InstrumentResults> engine)
            where TModel : ICalibratedModel
        {
            List<SwaptionHelper> priced = [.. helpers.Select(h => h.SetPricingEngine(engine))];
            return (model.Calibrate(priced.Cast<CalibrationHelper>(), optimizer, endCriteria, context).Model, priced);
        }

        var g2 = Calibrate(new G2(curve), (s, c, m) => G2SwaptionEngine.Calculate(s, c, m, 6.0, 16));
        var hullWhite = Calibrate(new HullWhite(curve), (s, c, m) => JamshidianSwaptionEngine.Calculate(s, c, m));
        var numerical = Calibrate(new HullWhite(curve), (s, c, m) => TreeSwaptionEngine.Calculate(s, c, m, grid));
        // </calibrate>

        Calibration Read<TModel>(string name, string mark, (TModel Model, List<SwaptionHelper> Helpers) c,
            params string[] names)
            where TModel : CalibratedModel
        {
            double[] values = [.. c.Helpers.Select(h => h.ModelValue(c.Model, context))];
            double[] vols = [.. c.Helpers.Select((h, i) => h.ImpliedVolatility(values[i], 1e-4, 1000, 0.05, 0.50, context))];
            double[] p = [.. c.Model.Params()];
            return new Calibration(name, mark, c.Model, values, vols, [.. names.Select((n, i) => (n, p[i]))]);
        }

        Calibration[] calibrations =
        [
            Read("G2", " " + G2Mark, g2, "a", "σ", "b", "η", "ρ"),
            Read("Hull-White", "", hullWhite, "a", "σ"),
            Read("Hull-White, numerical", " " + TreeMark, numerical, "a", "σ"),
        ];

        // <bermudan>
        Date start = calendar.Advance(settlement, 1, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing);
        Date maturity = calendar.Advance(start, 5, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing);
        var fixedSchedule = new Schedule(start, maturity, new Period(Frequency.Annual), calendar,
            BusinessDayConvention.Unadjusted, BusinessDayConvention.Unadjusted, DateGenerationRule.Forward, false, context);
        var floatSchedule = new Schedule(start, maturity, new Period(Frequency.Semiannual), calendar,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward,
            false, context);
        VanillaSwap Swap(double rate) => new(SwapType.Payer, 1000.0, fixedSchedule, rate,
            new Thirty360(Thirty360Convention.European), floatSchedule, index, 0.0, index.DayCounter, context);

        double atm = DiscountingSwapEngine.Calculate(Swap(0.03), context, curve).FairRate!.Value;
        var exercise = new BermudanExercise([.. Swap(atm).FixedLeg.Select(cf => ((Coupon)cf).AccrualStartDate)]);
        var bermudans = new List<Bermudan>();
        foreach (var (label, strike, g2Steps) in new[] { ("ATM", atm, 50), ("OTM", atm * 1.2, 300), ("ITM", atm * 0.8, 50) })
        {
            var swaption = new Swaption(Swap(strike), exercise);
            bermudans.Add(new Bermudan(label, strike,
                TreeSwaptionEngine.Calculate(swaption, context, g2.Model, g2Steps).NPV!.Value,
                FdG2SwaptionEngine.Calculate(swaption, context, g2.Model).NPV!.Value,
                TreeSwaptionEngine.Calculate(swaption, context, hullWhite.Model, 50).NPV!.Value,
                FdHullWhiteSwaptionEngine.Calculate(swaption, context, hullWhite.Model).NPV!.Value,
                TreeSwaptionEngine.Calculate(swaption, context, numerical.Model, 50).NPV!.Value,
                FdHullWhiteSwaptionEngine.Calculate(swaption, context, numerical.Model).NPV!.Value));
        }

        // </bermudan>
        string[] names = [.. Enumerable.Range(1, 5).Select(i => $"{i}Y into {6 - i}Y")];
        return new Result(names, marketVols, calibrations, bermudans, helpers, times);
    }

    // The example's stream precision, five significant digits.
    private static string G5(double value) => value.ToString("G5", CultureInfo.InvariantCulture);
}
