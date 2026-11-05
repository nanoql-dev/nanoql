// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>
/// Basis swaps and FX: a 3M-against-6M Euribor basis swap on two forecasting curves, an FX forward from two curves and
/// spot, and a curve implied by FX swap points.
/// </summary>
public static class BasisAndFx
{
    /// <summary>Gets the examples' evaluation context: 15 October 2026.</summary>
    public static EvaluationContext Context { get; } = new() { EvaluationDate = new Date(15, Month.October, 2026) };

    /// <summary>Gets the €STR discount curve, flat at 2.80%.</summary>
    public static YieldTermStructure EurDiscount { get; } = new FlatForward(Context.EvaluationDate, 0.0280, new Actual365Fixed());

    /// <summary>Gets the USD discount curve, flat at 4.00%.</summary>
    public static YieldTermStructure UsdDiscount { get; } = new FlatForward(Context.EvaluationDate, 0.0400, new Actual365Fixed());

    /// <summary>Gets the EUR/USD spot, in dollars per euro.</summary>
    public static double Spot => 1.0850;

    /// <summary>The examples' outputs.</summary>
    /// <returns>The basis swap, the FX forward and the curve from FX swap points.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        var (spread, before, after) = BasisSwap();
        var basis = new Table("3M + spread against 6M", ">Spread", ">NPV");
        basis.Add("Flat", "0.00 bp", Table.N(before, 2));
        basis.Add("At the fair spread", Table.N(spread * 1e4, 4) + " bp", Table.N(Math.Round(after, 2) + 0.0, 2));

        var (fair, npvAtFair) = FxForwardAtFair();
        var forward = new Table("One-year EUR/USD forward", ">Value");
        forward.Add("Spot", Table.N(Spot, 4));
        forward.Add("Fair forward rate", Table.N(fair, 6));
        forward.Add("NPV of €10 million sold forward at that rate, in dollars", Table.N(Math.Round(npvAtFair, 2) + 0.0, 2));

        var points = new Table("Tenor", ">Swap points", ">Implied by the curve");
        foreach (var (tenor, quote, implied) in FxSwapCurve())
        {
            points.Add(tenor, Table.N(quote * 1e4, 2), Table.N(implied * 1e4, 2));
        }

        yield return ("basis-swap", basis);
        yield return ("fx-forward", forward);
        yield return ("fx-swap-curve", points);
    }

    /// <summary>A five-year basis swap paying Euribor 3M plus a spread and receiving Euribor 6M.</summary>
    /// <returns>Its fair spread, its NPV with no spread and its NPV at the fair spread.</returns>
    public static (double FairSpread, double NpvFlat, double NpvAtFair) BasisSwap()
    {
        EvaluationContext context = Context;
        // <basis>
        var euribor3M = new Euribor3M(new FlatForward(context.EvaluationDate, 0.0295, new Actual365Fixed()));
        var euribor6M = new Euribor6M(new FlatForward(context.EvaluationDate, 0.0305, new Actual365Fixed()));
        Calendar calendar = new TARGET();
        Date start = calendar.Advance(context.EvaluationDate, 2, TimeUnit.Days);
        Date end = calendar.Advance(start, 5, TimeUnit.Years, BusinessDayConvention.ModifiedFollowing);
        Schedule Quarterly() => new MakeSchedule().From(start).To(end).WithTenor(euribor3M.Tenor).WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.ModifiedFollowing).ToSchedule();
        Schedule Semiannual() => new MakeSchedule().From(start).To(end).WithTenor(euribor6M.Tenor).WithCalendar(calendar)
            .WithConvention(BusinessDayConvention.ModifiedFollowing).ToSchedule();
        FloatFloatSwap Swap(double spread) => new(SwapType.Payer, 10e6, 10e6, Quarterly(), euribor3M, new Actual360(),
            Semiannual(), euribor6M, new Actual360(), context, spread1: spread);

        SwapResults flat = DiscountingSwapEngine.Calculate(Swap(0.0), context, EurDiscount);
        // A spread of one basis point on the 3M leg moves that leg's NPV by its BPS.
        double fairSpread = -flat.NPV!.Value / flat.LegBPS[0]!.Value * 1e-4;
        SwapResults atFair = DiscountingSwapEngine.Calculate(Swap(fairSpread), context, EurDiscount);
        // </basis>
        return (fairSpread, flat.NPV.Value, atFair.NPV!.Value);
    }

    /// <summary>A one-year forward selling euros for dollars, struck at its fair rate.</summary>
    /// <returns>The fair forward rate and the NPV of a forward struck at it.</returns>
    public static (double FairRate, double NpvAtFair) FxForwardAtFair()
    {
        EvaluationContext context = Context;
        // <forward>
        Date maturity = new TARGET().Advance(context.EvaluationDate, 1, TimeUnit.Years);
        var probe = new FxForward(10e6, new EURCurrency(), new USDCurrency(), Spot, maturity, true);
        double fair = DiscountingFxForwardEngine.Calculate(probe, context, EurDiscount, UsdDiscount, Spot).FairForwardRate!.Value;

        var forward = new FxForward(10e6, new EURCurrency(), new USDCurrency(), fair, maturity, true);
        FxForwardResults result = DiscountingFxForwardEngine.Calculate(forward, context, EurDiscount, UsdDiscount, Spot);
        // </forward>
        return (fair, result.NPV!.Value);
    }

    /// <summary>A euro curve implied by EUR/USD swap points, with dollars as the collateral currency.</summary>
    /// <returns>Each tenor's quoted points and the points the bootstrapped curve implies.</returns>
    public static IReadOnlyList<(string Tenor, double Quote, double Implied)> FxSwapCurve()
    {
        EvaluationContext context = Context;
        // <fxswaps>
        (Period Tenor, double Points)[] quotes =
        [
            (new Period(1, TimeUnit.Months), 0.0010), (new Period(3, TimeUnit.Months), 0.0031),
            (new Period(6, TimeUnit.Months), 0.0062), (new Period(1, TimeUnit.Years), 0.0125),
        ];
        var helpers = quotes.Select(q => new FxSwapRateHelper(q.Points, Spot, q.Tenor, 2, new TARGET(),
            BusinessDayConvention.Following, false, false, UsdDiscount, context)).ToList();
        var eurCurve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            context.EvaluationDate, helpers, new Actual365Fixed(), context);
        // </fxswaps>
        return [.. helpers.Select((h, i) => (Name(quotes[i].Tenor), h.Quote,
            h.SetTermStructure(eurCurve).ImpliedQuote(context)))];
    }

    private static string Name(Period tenor) =>
        tenor.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + (tenor.Units == TimeUnit.Months ? "M" : "Y");
}
