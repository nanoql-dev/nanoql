// SPDX-FileCopyrightText: Copyright (C) 2013 Yue Tian
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/vannavolgabarrierengine.hpp ql/experimental/barrieroption/vannavolgabarrierengine.cpp @ v1.44
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
/// The Vanna-Volga engine for FX barrier options (Castagna and Mercurio, 2007): the analytic barrier price at the
/// money, corrected by the vanna and volga of the 25-delta call and put, weighted by the probability of not touching
/// the barrier, and bounded by the vanilla price the Vanna-Volga smile gives.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's method, its quote shifts for the barrier's vega, volga and vanna replayed on local values in QuantLib's
/// order (each shift and set-back made on the value the quote holds, since <c>a + s − s</c> need not be <c>a</c>), its
/// barrier prices the analytic engine's as nanoql fixes it. Its additional results carry QuantLib's tags: "Forward",
/// "StrikeVol", "VanillaPrice", "BarrierInPrice", "BarrierOutPrice" and, inside the barriers, "lambda".
/// </para>
/// <para>
/// Two changes, each a documented divergence that leaves QuantLib's bits where it doesn't apply. The engine reads each
/// curve at the instant the quotes' time denotes on the volatility's clock (dated by the evaluation date on
/// Actual/365F), nothing accrued before the curve's own date, where QuantLib reads each at that time on its own clock:
/// its discount factors and the touch probability's zero rates (the logarithms of those discounts over the time). And
/// a rebate is priced as each option pays it: an out option's price with its rebate is QuantLib's, but where QuantLib's
/// bounds bind its rebate-free price, the bound plus the rebate's value at the hit; an in option is QuantLib's
/// rebate-free in price plus the analytic engine's value of the rebate paid at expiry; at or beyond the barrier an out
/// option is its rebate, due now. QuantLib prices a knock-in by parity with the knock-out's rebate and bounds the
/// out price as though it had none. At a zero rebate every added term is 0.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class VannaVolgaBarrierEngine
{
    /// <summary>Prices a barrier option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context, whose evaluation date dates the at-the-money volatility.</param>
    /// <param name="atmVol">The at-the-money volatility quote.</param>
    /// <param name="vol25Put">The 25-delta put's volatility quote.</param>
    /// <param name="vol25Call">The 25-delta call's volatility quote.</param>
    /// <param name="spotFX">The spot FX rate.</param>
    /// <param name="domesticTS">The domestic currency's discount curve.</param>
    /// <param name="foreignTS">The foreign currency's discount curve.</param>
    /// <param name="adaptVanDelta">Whether the vanilla price is the one given, adapting the vanilla delta.</param>
    /// <param name="bsPriceWithSmile">The vanilla price, read where <paramref name="adaptVanDelta"/> is set.</param>
    /// <returns>The option's results: its value and additional results; the expired results when it has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The put's delta isn't −0.25 or the call's 0.25, or the three quotes' maturities differ.
    /// </exception>
    /// <exception cref="NanoQLException">The delta calculators, the interpolation or the analytic engine
    /// fail.</exception>
    public static OneAssetOptionResults Calculate(
        BarrierOption option,
        EvaluationContext context,
        DeltaVolQuote atmVol,
        DeltaVolQuote vol25Put,
        DeltaVolQuote vol25Call,
        double spotFX,
        YieldTermStructure domesticTS,
        YieldTermStructure foreignTS,
        bool adaptVanDelta = false,
        double bsPriceWithSmile = 0.0)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
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
        var engine = new Pricing(
            option,
            context,
            atmVol,
            vol25Put,
            vol25Call,
            spotFX,
            domesticTS,
            foreignTS,
            adaptVanDelta,
            bsPriceWithSmile);
        return option.FetchResults(engine.Calculate());
    }

    // QuantLib's calculate(), its quotes local values.
    private sealed class Pricing(
        BarrierOption option,
        EvaluationContext context,
        DeltaVolQuote atmVol,
        DeltaVolQuote vol25Put,
        DeltaVolQuote vol25Call,
        double spotFX,
        YieldTermStructure domesticTS,
        YieldTermStructure foreignTS,
        bool adaptVanDelta,
        double bsPriceWithSmile)
    {
        private const double SigmaShiftVega = 0.0001;
        private const double SigmaShiftVolga = 0.0001;
        private const double SigmaShiftVanna = 0.0001;

        private readonly double t = atmVol.Maturity;
        private readonly StrikedTypePayoff payoff = (StrikedTypePayoff)option.Payoff!;

        // the volatility's clock: QuantLib's BlackConstantVol, dated by the evaluation date on Actual/365F
        private readonly BlackConstantVol clock =
            new(context.EvaluationDate, new NullCalendar(), atmVol.Value, new Actual365Fixed());

        internal OneAssetOptionResults Calculate()
        {
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

            // here use vanna volga interpolated smile to price vanilla
            double[] strikes = [put25Strike, atmStrike, call25Strike];
            double[] vols = [put25Vol, atmVol.Value, call25Vol];
            Interpolation interpolation = new VannaVolga(x0, dDiscount, fDiscount, t).Interpolate(strikes, vols);
            double strikeVol = interpolation.Evaluate(payoff.Strike, allowExtrapolation: true);

            // vanilla option price
            double forward = x0 * fDiscount / dDiscount;
            double vanillaOption = BlackFormulaFunctions.BlackFormula(
                payoff.OptionType, payoff.Strike, forward, strikeVol * sqrtT, dDiscount);
            ImmutableSortedDictionary<string, object>.Builder additional =
                ImmutableSortedDictionary.CreateBuilder<string, object>();
            additional["Forward"] = forward;
            additional["StrikeVol"] = strikeVol;

            double vanilla = adaptVanDelta ? bsPriceWithSmile : vanillaOption;
            double barrier = option.Barrier;
            double rebate = option.Rebate;
            BarrierType barrierType = option.BarrierType;
            double value;
            bool beyondUp = x0 >= barrier && barrierType is BarrierType.UpOut or BarrierType.UpIn;
            bool beyondDown = x0 <= barrier && barrierType is BarrierType.DownOut or BarrierType.DownIn;
            if ((x0 >= barrier && barrierType == BarrierType.UpOut) || (
                x0 <= barrier && barrierType == BarrierType.DownOut))
            {
                // at or beyond the barrier an out option is its rebate, due now, where QuantLib's is 0
                value = rebate;
                additional["VanillaPrice"] = vanilla;
                additional["BarrierInPrice"] = vanilla;
                additional["BarrierOutPrice"] = rebate;
            }
            else if (beyondUp || beyondDown)
            {
                // an in option is the vanilla
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
                BarrierType outType = barrierType is BarrierType.UpOut or BarrierType.UpIn
                    ? BarrierType.UpOut
                    : BarrierType.DownOut;

                double price25CallBS = BlackFormulaFunctions.BlackFormula(
                    OptionType.Call, call25Strike, forward, atmVol.Value * sqrtT, dDiscount);
                double price25PutBS = BlackFormulaFunctions.BlackFormula(
                    OptionType.Put, put25Strike, forward, atmVol.Value * sqrtT, dDiscount);

                // market price
                double price25CallMkt = BlackFormulaFunctions.BlackFormula(
                    OptionType.Call, call25Strike, forward, call25Vol * sqrtT, dDiscount);
                double price25PutMkt = BlackFormulaFunctions.BlackFormula(
                    OptionType.Put, put25Strike, forward, put25Vol * sqrtT, dDiscount);

                // Analytical Black Scholes formula for vanilla option
                var norm = new NormalDistribution();
                (double Vega, double Vanna, double Volga) Analytical(double k)
                {
                    double d1 = (Math.Log(forward / k) + (0.5 * Math.Pow(atmVolValue, 2.0) * t))
                        / (atmVolValue * sqrtT);
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

                // the out option's Black-Scholes price and finite-difference vega, volga and vanna at a rebate, its
                // quotes shifted and set back as QuantLib's are, and the touch probability on the quotes' last values
                OutPart Out(double withRebate)
                {
                    var barrierOption = new BarrierOption(outType, barrier, withRebate, payoff, option.Exercise);
                    double Npv(double spot, double vol) => AnalyticBarrierEngine.Body(
                        barrierOption,
                        new BlackScholesMertonProcess(
                            spot, foreignTS, domesticTS, new BlackConstantVol(
                                context.EvaluationDate, new NullCalendar(), vol, new Actual365Fixed()))).NPV!.Value;

                    double s = x0, v = atmVolValue;

                    // BS price with atm vol
                    double priceBS = Npv(s, v);

                    // BS vega
                    v += SigmaShiftVega;
                    double vegaBarBS = (Npv(s, v) - priceBS) / SigmaShiftVega;
                    v -= SigmaShiftVega; // setback

                    // BS volga: base NPV, then shifted NPV
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

                    // shifted delta
                    v += SigmaShiftVanna; // shift sigma
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

                    // touch probability: the zero rates the logarithms of the discounts read at the instant
                    var cnd = new CumulativeNormalDistribution();
                    double mu = ZeroRate(domesticTS) - ZeroRate(foreignTS) - (Math.Pow(atmVol.Value, 2.0) / 2.0);
                    double h2 = (Math.Log(barrier / s) + (mu * t)) / (atmVol.Value * sqrtT);
                    double h2Prime = (Math.Log(s / barrier) + (mu * t)) / (atmVol.Value * sqrtT);
                    double probTouch = barrierType is BarrierType.UpIn or BarrierType.UpOut
                        ? cnd.Evaluate(h2Prime)
                            + (Math.Pow(barrier / s, 2.0 * mu / Math.Pow(atmVol.Value, 2.0)) * cnd.Evaluate(-h2))
                        : cnd.Evaluate(-h2Prime)
                            + (Math.Pow(barrier / s, 2.0 * mu / Math.Pow(atmVol.Value, 2.0)) * cnd.Evaluate(h2));
                    double pSurvival = 1.0 - probTouch;

                    double lambda = pSurvival;
                    double adjust = (q[1] * (price25CallMkt - price25CallBS)) + (q[2] * (price25PutMkt - price25PutBS));
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
                double outPriceFinal = bounded == rebateFree.Price
                    ? withRebate.Price
                    : bounded + (withRebate.Price - rebateFree.Price);

                // the in option: QuantLib's rebate-free in price plus the rebate paid at expiry, the analytic engine's
                double inRebate = 0.0;
                if (rebate != 0.0)
                {
                    BarrierType inType = outType == BarrierType.UpOut ? BarrierType.UpIn : BarrierType.DownIn;
                    var process = new BlackScholesMertonProcess(
                        withRebate.Spot, foreignTS, domesticTS, new BlackConstantVol(
                            context.EvaluationDate, new NullCalendar(), withRebate.Vol, new Actual365Fixed()));
                    double InValue(double r) => AnalyticBarrierEngine.Body(
                        new BarrierOption(inType, barrier, r, payoff, option.Exercise), process).NPV!.Value;
                    inRebate = InValue(rebate) - InValue(0.0);
                }

                double inPrice = vanilla - bounded + inRebate;

                value = barrierType is BarrierType.DownOut or BarrierType.UpOut ? outPriceFinal : inPrice;
                additional["VanillaPrice"] = vanillaOption;
                additional["BarrierInPrice"] = inPrice;
                additional["BarrierOutPrice"] = outPriceFinal;
                additional["lambda"] = withRebate.Lambda;
            }

            return new OneAssetOptionResults { NPV = value, AdditionalResults = additional.ToImmutable() };
        }

        // QuantLib's zeroRate(T_, Continuous) on the instant's discount: the logarithm of its inverse over the time.
        private double ZeroRate(YieldTermStructure curve) => Math.Log(1.0 / curve.DiscountAt(clock, t)) / t;

        // An out option's unbounded Vanna-Volga price, its survival probability, and the quotes' values after
        // QuantLib's shifts and set-backs.
        private readonly record struct OutPart(double Price, double Lambda, double Spot, double Vol);
    }
}
