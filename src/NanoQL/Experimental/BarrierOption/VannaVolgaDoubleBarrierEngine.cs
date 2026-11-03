// SPDX-FileCopyrightText: Copyright (C) 2013 Yue Tian
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/vannavolgadoublebarrierengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.Quotes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The Vanna-Volga engine for FX double barrier options (Castagna and Mercurio, 2007): a double-barrier engine's price
/// at the money, corrected by the vanna and volga of the at-the-money, 25-delta call and put options, weighted by the
/// probability of touching neither barrier, and bounded by the vanilla price the Vanna-Volga smile gives.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's template on its double-barrier engine as a delegate of the engines' <c>Calculate</c> form: the engine
/// given, such as <c>AnalyticDoubleBarrierEngine.Calculate</c> or <c>SuoWangDoubleBarrierEngine.Calculate</c>, is
/// called on the option's knock-out at each of QuantLib's quote shifts, built as QuantLib builds it, on a Black-Scholes
/// process at the at-the-money volatility dated by the evaluation date on Actual/365F, with QuantLib's number of series
/// terms. Its shifts for the barrier's vega, volga and vanna are replayed on local values in QuantLib's order (each
/// shift and set-back made on the value the quote holds, since <c>a + s − s</c> need not be <c>a</c>). Its additional
/// results carry QuantLib's tags: "VanillaPrice", "BarrierInPrice", "BarrierOutPrice" and, inside the barriers,
/// "lambda".
/// </para>
/// <para>
/// Documented divergences, each leaving QuantLib's bits where it doesn't apply
/// (<c>Divergences/VannaVolgaDoubleBarrierTests.cs</c>). The Vanna-Volga smile's interpolation takes the domestic
/// discount first, as its constructor's order and QuantLib's single-barrier engine take it, where QuantLib passes the
/// foreign discount twice: the vanilla on the smile moves by up to 1.4e-4 at QuantLib's rates. The engine reads each
/// curve at the instant the quotes' time denotes on the volatility's clock, nothing accrued before the curve's own
/// date, where QuantLib reads each at that time on its own clock: its discount factors and the survival probability's
/// zero rates. The already-out and already-in tests include a spot on a barrier, as the option's own
/// <see cref="DoubleBarrierOption.Triggered"/>, where QuantLib's strict tests send it to the inner engine's "barrier
/// touched". And a rebate is priced as each option pays it: an out option's price with its rebate is QuantLib's, but
/// where QuantLib's bounds bind its rebate-free price, the bound plus the rebate's value at the hit; an in option is
/// QuantLib's rebate-free in price plus the inner engine's value of the rebate paid at expiry; on or beyond a barrier
/// an out option is its rebate, due now. At a zero rebate every added term is 0. The inner engine's own fixes stand:
/// the analytic engine and Suo-Wang price a rebate and converge their series.
/// </para>
/// <para>
/// The method's equivalent-constant reads stand, QuantLib's: one volatility and the zero rates to the quotes' time.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class VannaVolgaDoubleBarrierEngine
{
    /// <summary>Prices a double barrier option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context, whose evaluation date dates the at-the-money volatility.</param>
    /// <param name="engine">The double-barrier engine the method corrects, called with the option, the context, a
    /// Black-Scholes process and <paramref name="series"/>.</param>
    /// <param name="atmVol">The at-the-money volatility quote.</param>
    /// <param name="vol25Put">The 25-delta put's volatility quote.</param>
    /// <param name="vol25Call">The 25-delta call's volatility quote.</param>
    /// <param name="spotFX">The spot FX rate.</param>
    /// <param name="domesticTS">The domestic currency's discount curve.</param>
    /// <param name="foreignTS">The foreign currency's discount curve.</param>
    /// <param name="adaptVanDelta">Whether the vanilla price is the one given, adapting the vanilla delta.</param>
    /// <param name="bsPriceWithSmile">The vanilla price, read where <paramref name="adaptVanDelta"/> is set.</param>
    /// <param name="series">The number of series terms of the survival probability and of the inner engine.</param>
    /// <returns>The option's results: its value and additional results; the expired results when it has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"> The put's delta isn't −0.25 or the call's 0.25, or the three quotes'
    /// maturities differ. </exception>
    /// <exception cref="NanoQLException"> The type is a KIKO or a KOKI ("Only same type barrier supported"), or the
    /// delta calculators, the interpolation or the inner engine fail. </exception>
    public static OneAssetOptionResults Calculate(
        DoubleBarrierOption option,
        EvaluationContext context,
        Func<DoubleBarrierOption, EvaluationContext, GeneralizedBlackScholesProcess, int, OneAssetOptionResults> engine,
        DeltaVolQuote atmVol,
        DeltaVolQuote vol25Put,
        DeltaVolQuote vol25Call,
        double spotFX,
        YieldTermStructure domesticTS,
        YieldTermStructure foreignTS,
        bool adaptVanDelta = false,
        double bsPriceWithSmile = 0.0,
        int series = 5)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(atmVol);
        ArgumentNullException.ThrowIfNull(vol25Put);
        ArgumentNullException.ThrowIfNull(vol25Call);
        if (!(vol25Put.Delta == -0.25))
        {
            throw new ArgumentException("25 delta put is required by vanna volga method", nameof(vol25Put));
        }

        if (!(vol25Call.Delta == 0.25))
        {
            throw new ArgumentException("25 delta call is required by vanna volga method", nameof(vol25Call));
        }

        if (!(vol25Put.Maturity == vol25Call.Maturity && vol25Put.Maturity == atmVol.Maturity))
        {
            throw new ArgumentException("Maturity of 3 vols are not the same", nameof(atmVol));
        }

        if (domesticTS is null)
        {
            throw new ArgumentNullException(nameof(domesticTS), "domestic yield curve is not defined");
        }

        if (foreignTS is null)
        {
            throw new ArgumentNullException(nameof(foreignTS), "foreign yield curve is not defined");
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        var pricing = new Pricing(
            option,
            context,
            engine,
            atmVol,
            vol25Put,
            vol25Call,
            spotFX,
            domesticTS,
            foreignTS,
            adaptVanDelta,
            bsPriceWithSmile,
            series);
        return option.FetchResults(pricing.Calculate());
    }

    // QuantLib's calculate(), its quotes local values.
    private sealed class Pricing(
        DoubleBarrierOption option,
        EvaluationContext context,
        Func<DoubleBarrierOption, EvaluationContext, GeneralizedBlackScholesProcess, int, OneAssetOptionResults> engine,
        DeltaVolQuote atmVol,
        DeltaVolQuote vol25Put,
        DeltaVolQuote vol25Call,
        double spotFX,
        YieldTermStructure domesticTS,
        YieldTermStructure foreignTS,
        bool adaptVanDelta,
        double bsPriceWithSmile,
        int series)
    {
        private const double SigmaShiftVega = 0.001;
        private const double SigmaShiftVolga = 0.0001;
        private const double SigmaShiftVanna = 0.0001;

        // the relative agreement under which QuantLib's survival series stands, and the most pairs added to converge it
        private const double SeriesAgreement = 1e-12;
        private const int MaxExtraPairs = 10000;

        private readonly double t = atmVol.Maturity;

        // the volatility's clock: QuantLib's BlackConstantVol, dated by the evaluation date on Actual/365F
        private readonly BlackConstantVol clock =
            new(context.EvaluationDate, new NullCalendar(), atmVol.Value, new Actual365Fixed());

        internal OneAssetOptionResults Calculate()
        {
            if (!(option.BarrierType is DoubleBarrierType.KnockIn or DoubleBarrierType.KnockOut))
            {
                throw new NanoQLException("Only same type barrier supported");
            }

            double spotShiftDelta = 0.0001 * spotFX;

            double x0 = spotFX; // used for shift
            double atmVolValue = atmVol.Value; // used for shift

            double dDiscount = domesticTS.DiscountAt(clock, t);
            double fDiscount = foreignTS.DiscountAt(clock, t);
            double sqrtT = Math.Sqrt(t);

            var blackDeltaCalculatorAtm = new BlackDeltaCalculator(
                OptionType.Call, atmVol.DeltaType, x0, dDiscount, fDiscount, atmVolValue * sqrtT);
            double atmStrike = blackDeltaCalculatorAtm.AtmStrike(atmVol.AtmType);

            double call25Vol = vol25Call.Value;
            double put25Vol = vol25Put.Value;
            var blackDeltaCalculatorPut25 = new BlackDeltaCalculator(
                OptionType.Put, vol25Put.DeltaType, x0, dDiscount, fDiscount, put25Vol * sqrtT);
            double put25Strike = blackDeltaCalculatorPut25.StrikeFromDelta(-0.25);
            var blackDeltaCalculatorCall25 = new BlackDeltaCalculator(
                OptionType.Call, vol25Call.DeltaType, x0, dDiscount, fDiscount, call25Vol * sqrtT);
            double call25Strike = blackDeltaCalculatorCall25.StrikeFromDelta(0.25);

            // here use vanna volga interpolated smile to price vanilla, the domestic discount first
            double[] strikes = [put25Strike, atmStrike, call25Strike];
            double[] vols = [put25Vol, atmVol.Value, call25Vol];
            Interpolation interpolation = new VannaVolga(x0, dDiscount, fDiscount, t).Interpolate(strikes, vols);
            var payoff = (StrikedTypePayoff)option.Payoff!;
            double strikeVol = interpolation.Evaluate(payoff.Strike, allowExtrapolation: true);

            // vanilla option price
            double forward = x0 * fDiscount / dDiscount;
            double vanillaOption = BlackFormulaFunctions.BlackFormula(
                payoff.OptionType, payoff.Strike, forward, strikeVol * sqrtT, dDiscount);

            double vanilla = adaptVanDelta ? bsPriceWithSmile : vanillaOption;
            double rebate = option.Rebate;
            bool beyond = option.Triggered(x0);
            ImmutableSortedDictionary<string, object>.Builder additional =
                ImmutableSortedDictionary.CreateBuilder<string, object>();
            double value;
            if (beyond && option.BarrierType == DoubleBarrierType.KnockOut)
            {
                // already out: on or beyond a barrier an out option is its rebate, due now, where QuantLib's is 0
                value = rebate;
                additional["VanillaPrice"] = vanilla;
                additional["BarrierInPrice"] = vanilla;
                additional["BarrierOutPrice"] = rebate;
            }
            else if (beyond)
            {
                // already in
                value = vanilla;
                additional["VanillaPrice"] = vanilla;
                additional["BarrierInPrice"] = vanilla;
                additional["BarrierOutPrice"] = 0.0;
            }
            else
            {
                // set up BS barrier option pricing
                // only calculate out barrier option price
                // in barrier price = vanilla - out barrier
                double priceAtmCallBS = BlackFormulaFunctions.BlackFormula(
                    OptionType.Call, atmStrike, forward, atmVol.Value * sqrtT, dDiscount);
                double price25CallBS = BlackFormulaFunctions.BlackFormula(
                    OptionType.Call, call25Strike, forward, atmVol.Value * sqrtT, dDiscount);
                double price25PutBS = BlackFormulaFunctions.BlackFormula(
                    OptionType.Put, put25Strike, forward, atmVol.Value * sqrtT, dDiscount);

                // market price
                double priceAtmCallMkt = BlackFormulaFunctions.BlackFormula(
                    OptionType.Call, atmStrike, forward, atmVol.Value * sqrtT, dDiscount);
                double price25CallMkt = BlackFormulaFunctions.BlackFormula(
                    OptionType.Call, call25Strike, forward, call25Vol * sqrtT, dDiscount);
                double price25PutMkt = BlackFormulaFunctions.BlackFormula(
                    OptionType.Put, put25Strike, forward, put25Vol * sqrtT, dDiscount);

                // Analytical Black Scholes formula
                var norm = new NormalDistribution();
                (double Vega, double Vanna, double Volga) Analytical(double k)
                {
                    double d1 = (Math.Log(forward / k) + (0.5 * Math.Pow(atmVolValue, 2.0) * t)) / (atmVolValue * sqrtT);
                    double vega = x0 * norm.Evaluate(d1) * sqrtT * fDiscount;
                    double vanna = vega / x0 * (1.0 - (d1 / (atmVolValue * sqrtT)));
                    double volga = vega * d1 * (d1 - (atmVolValue * sqrtT)) / atmVolValue;
                    return (vega, vanna, volga);
                }

                (double vegaAtm, double vannaAtm, double volgaAtm) = Analytical(atmStrike);
                (double vega25Call, double vanna25Call, double volga25Call) = Analytical(call25Strike);
                (double vega25Put, double vanna25Put, double volga25Put) = Analytical(put25Strike);

                // Matrix
                var a = new Matrix(3, 3, 0.0);

                // analytical
                a[0, 0] = vegaAtm;
                a[0, 1] = vega25Call;
                a[0, 2] = vega25Put;
                a[1, 0] = vannaAtm;
                a[1, 1] = vanna25Call;
                a[1, 2] = vanna25Put;
                a[2, 0] = volgaAtm;
                a[2, 1] = volga25Call;
                a[2, 2] = volga25Put;
                Matrix inverseA = MatrixFunctions.Inverse(a);

                // the knock-out's price through the engine and its finite-difference vega, volga and vanna at a
                // rebate, its quotes shifted and set back as QuantLib's are, and the survival probability on the
                // quotes' last values
                OutPart Out(double withRebate)
                {
                    var doubleBarrierOption = new DoubleBarrierOption(
                        DoubleBarrierType.KnockOut, option.BarrierLo, option.BarrierHi, withRebate, payoff, option.Exercise);
                    double Npv(double spot, double vol) => engine(
                        doubleBarrierOption, context, Process(spot, vol), series).NPV!.Value;

                    double s = x0, v = atmVolValue;

                    // BS price
                    double priceBS = Npv(s, v);

                    // BS vega
                    v += SigmaShiftVega;
                    double vegaBarBS = (Npv(s, v) - priceBS) / SigmaShiftVega;
                    v -= SigmaShiftVega; // setback

                    // BS volga: vegaBar2, base NPV, then shifted npv
                    v += SigmaShiftVolga;
                    double priceBS2 = Npv(s, v);
                    v += SigmaShiftVega;
                    double vegaBarBS2 = (Npv(s, v) - priceBS2) / SigmaShiftVega;
                    double volgaBarBS = (vegaBarBS2 - vegaBarBS) / SigmaShiftVolga;
                    v = v - SigmaShiftVolga - SigmaShiftVega; // setback

                    // BS Delta: base delta
                    s += spotShiftDelta; // shift forth
                    double priceBSDelta1 = Npv(s, v);
                    s -= 2 * spotShiftDelta; // shift back
                    double priceBSDelta2 = Npv(s, v);
                    s += spotShiftDelta; // set back
                    double deltaBar1 = (priceBSDelta1 - priceBSDelta2) / (2.0 * spotShiftDelta);

                    // shifted vanna
                    v += SigmaShiftVanna; // shift sigma

                    // shifted delta
                    s += spotShiftDelta; // shift forth
                    priceBSDelta1 = Npv(s, v);
                    s -= 2 * spotShiftDelta; // shift back
                    priceBSDelta2 = Npv(s, v);
                    s += spotShiftDelta; // set back
                    double deltaBar2 = (priceBSDelta1 - priceBSDelta2) / (2.0 * spotShiftDelta);

                    double vannaBarBS = (deltaBar2 - deltaBar1) / SigmaShiftVanna;
                    v -= SigmaShiftVanna; // set back

                    var b = new Vector(3, 0.0);
                    b[0] = vegaBarBS;
                    b[1] = vannaBarBS;
                    b[2] = volgaBarBS;
                    Vector q = inverseA * b;

                    // the survival probability: the zero rates the logarithms of the discounts read at the instant
                    double thetaTiltMinus = (((ZeroRate(domesticTS) - ZeroRate(foreignTS)) / atmVol.Value)
                            - (atmVol.Value / 2.0)) * Math.Sqrt(t);
                    double h = 1.0 / atmVol.Value * Math.Log(option.BarrierHi / s) / Math.Sqrt(t);
                    double l = 1.0 / atmVol.Value * Math.Log(option.BarrierLo / s) / Math.Sqrt(t);
                    double lambda = DoubleNoTouch(thetaTiltMinus, h, l, series);

                    double adjust = (q[0] * (priceAtmCallMkt - priceAtmCallBS))
                        + (q[1] * (price25CallMkt - price25CallBS))
                        + (q[2] * (price25PutMkt - price25PutBS));
                    double outPrice = priceBS + (lambda * adjust);

                    // adapt Vanilla delta
                    if (adaptVanDelta)
                    {
                        outPrice += lambda * (bsPriceWithSmile - vanillaOption);
                    }

                    return new OutPart(outPrice, lambda, s, v);
                }

                OutPart withRebate = Out(rebate);
                OutPart rebateFree = rebate == 0.0 ? withRebate : Out(0.0);

                // capfloored by (0, vanilla): QuantLib's bounds, which hold for the rebate-free out option; the
                // rebate's value at the hit added to a bound that binds it
                double bounded = Math.Max(0.0, Math.Min(vanilla, rebateFree.Price));
                double outPrice = bounded == rebateFree.Price
                    ? withRebate.Price
                    : bounded + (withRebate.Price - rebateFree.Price);

                // the in option: QuantLib's rebate-free in price plus the rebate paid at expiry, the inner engine's
                double inRebate = 0.0;
                if (rebate != 0.0)
                {
                    GeneralizedBlackScholesProcess process = Process(withRebate.Spot, withRebate.Vol);
                    double InValue(double r) => engine(
                        new DoubleBarrierOption(
                            DoubleBarrierType.KnockIn, option.BarrierLo, option.BarrierHi, r, payoff, option.Exercise),
                        context,
                        process,
                        series).NPV!.Value;
                    inRebate = InValue(rebate) - InValue(0.0);
                }

                double inPrice = vanilla - bounded + inRebate;

                value = option.BarrierType == DoubleBarrierType.KnockOut ? outPrice : inPrice;
                additional["VanillaPrice"] = vanillaOption;
                additional["BarrierInPrice"] = inPrice;
                additional["BarrierOutPrice"] = outPrice;
                additional["lambda"] = withRebate.Lambda;
            }

            return new OneAssetOptionResults { NPV = value, AdditionalResults = additional.ToImmutable() };
        }

        // QuantLib's double no-touch probability, its series from −series to series − 1, where it agrees within 1e-12
        // on the value's scale with the series converged: from −series to series, then pairs while the next exceeds
        // machine epsilon of the sum
        private static double DoubleNoTouch(double thetaTiltMinus, double h, double l, int series)
        {
            var cnd = new CumulativeNormalDistribution();
            double Term(int j)
            {
                double eMinus = (2 * j * (h - l)) - thetaTiltMinus;
                return (Math.Exp(-2.0 * j * thetaTiltMinus * (h - l))
                        * (cnd.Evaluate(h + eMinus) - cnd.Evaluate(l + eMinus)))
                    - (Math.Exp((-2.0 * j * thetaTiltMinus * (h - l)) + (2.0 * thetaTiltMinus * h))
                        * (cnd.Evaluate(h - (2.0 * h) + eMinus) - cnd.Evaluate(l - (2.0 * h) + eMinus)));
            }

            double doubleNoTouch = 0.0;
            for (int j = -series; j < series; j++)
            {
                doubleNoTouch += Term(j);
            }

            double converged = doubleNoTouch + Term(series);
            for (int j = series + 1; ; ++j)
            {
                if (j > series + MaxExtraPairs)
                {
                    throw new NanoQLException("double no-touch series did not converge");
                }

                double pair = Term(-j) + Term(j);
                if (!double.IsFinite(pair) || !(Math.Abs(pair) > NumericLimits.QL_EPSILON * Math.Abs(converged)))
                {
                    converged += double.IsFinite(pair) ? pair : 0.0;
                    break;
                }

                converged += pair;
            }

            return double.IsFinite(converged)
                && !(Math.Abs(doubleNoTouch - converged) <= SeriesAgreement * Math.Max(Math.Abs(converged), 1.0))
                ? converged
                : doubleNoTouch;
        }

        // QuantLib's process for the inner engine: the spot, the foreign curve as dividends, the domestic curve as the
        // risk-free rate, and a flat volatility dated by the evaluation date on Actual/365F
        private BlackScholesMertonProcess Process(double spot, double vol) =>
            new(spot, foreignTS, domesticTS, new BlackConstantVol(
                context.EvaluationDate, new NullCalendar(), vol, new Actual365Fixed()));

        // QuantLib's zeroRate(T_, Continuous) on the instant's discount: the logarithm of its inverse over the time.
        private double ZeroRate(YieldTermStructure curve) => Math.Log(1.0 / curve.DiscountAt(clock, t)) / t;

        // An out option's unbounded Vanna-Volga price, its survival probability, and the quotes' values after
        // QuantLib's shifts and set-backs.
        private readonly record struct OutPart(double Price, double Lambda, double Spot, double Vol);
    }
}
