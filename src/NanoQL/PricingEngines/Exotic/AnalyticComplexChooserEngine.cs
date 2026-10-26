// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/exotic/analyticcomplexchooserengine.hpp ql/pricingengines/exotic/analyticcomplexchooserengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for a complex chooser option: Rubinstein's formula, the critical spot at the choosing date where
/// the call and the put are worth the same, and bivariate terms for each.
/// </summary>
/// <remarks>
/// <para>
/// Stateless. The maturities are measured from the evaluation date, QuantLib's fix after v1.44 (#2891), where 1.44
/// measures them from the choosing date and its critical value from twice it (the S-exo plan's ER6 (ii),
/// <c>Divergences/ComplexChooserEqualTermsTests.cs</c>). QuantLib evaluates the formula on one zero rate, dividend
/// yield and volatility per maturity, its critical value on the times between the dates, so it is exact only on flat
/// markets; here each term reads the variances to each date at the call's strike, the correlations from their ratios
/// and the discounts by date, and the critical spot solves the forward Black values from the choosing date (ER4,
/// <c>Divergences/ComplexChooserTermStructureTests.cs</c>, against the exact integral at the choosing date within the
/// critical value's accuracy). The bivariate normal is West's, QuantLib's default, where its engine reads Drezner's
/// (ER7). QuantLib's Newton iteration and stopping rule are kept, capped at 1,000 iterations (ED24).
/// </para>
/// <para>
/// Every structure is read by date, accruing nothing before its own date (ER5 (xxiv),
/// <c>Divergences/ExoticMismatchedTimesTests.cs</c>); a date before the process's clock keeps QuantLib's "negative
/// time" refusal.
/// </para>
/// </remarks>
public static class AnalyticComplexChooserEngine
{
    // ED24's cap on QuantLib's Newton loops.
    internal const int MaxIterations = 1000;

    // QuantLib's Newton-Raphson loops, this engine's and the holder-extensible engine's: from sv, Newton steps on f's
    // value and derivative while the value exceeds 0.001 in magnitude, refused past MaxIterations steps (ED24).
    internal static double Newton(double sv, Func<double, (double Value, double Derivative)> f)
    {
        (double yi, double di) = f(sv);
        const double epsilon = 0.001;
        int iterations = 0;
        while (Math.Abs(yi) > epsilon)
        {
            if (++iterations > MaxIterations)
            {
                throw new NanoQLException($"critical value not found after {MaxIterations} iterations");
            }

            sv = sv - (yi / di);
            (yi, di) = f(sv);
        }

        return sv;
    }

    /// <summary>Prices a complex chooser option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process.</param>
    /// <returns>The option's results: the value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, a date is before the clock's date, or the critical value isn't found in 1,000
    /// iterations.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        ComplexChooserOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new OneAssetOptionResults { NPV = new Form(option, process).Value() });
    }

    // QuantLib's engine over one call's option, its time and rate reads made ER4's discounts and variances by date.
    private sealed class Form
    {
        private readonly double spot, strikeCall, strikePut;
        private readonly double dividendDiscountChoosing, riskFreeDiscountChoosing, varianceChoosing;
        private readonly double dividendDiscountCall, riskFreeDiscountCall, varianceCall;
        private readonly double dividendDiscountPut, riskFreeDiscountPut, variancePut;

        public Form(ComplexChooserOption option, GeneralizedBlackScholesProcess process)
        {
            spot = process.X0;
            strikeCall = option.StrikeCall;
            strikePut = option.StrikePut;
            TermStructure clock = FdmBlackScholesOp.Clock(process);
            YieldTermStructure qTS = process.DividendYield, rTS = process.RiskFreeRate;
            BlackVolTermStructure volTS = process.BlackVolatility;
            Date choosing = option.ChoosingDate, call = option.ExerciseCall.LastDate(), put = option.ExercisePut.LastDate();
            dividendDiscountChoosing = qTS.AccruedDiscountOn(clock, choosing);
            riskFreeDiscountChoosing = rTS.AccruedDiscountOn(clock, choosing);
            dividendDiscountCall = qTS.AccruedDiscountOn(clock, call);
            riskFreeDiscountCall = rTS.AccruedDiscountOn(clock, call);
            dividendDiscountPut = qTS.AccruedDiscountOn(clock, put);
            riskFreeDiscountPut = rTS.AccruedDiscountOn(clock, put);

            // QuantLib reads every volatility at the call's strike
            varianceChoosing = volTS.AccruedVariance(choosing, strikeCall);
            varianceCall = volTS.AccruedVariance(call, strikeCall);
            variancePut = volTS.AccruedVariance(put, strikeCall);
        }

        // QuantLib's calculate().
        public double Value()
        {
            double s = spot;
            double xc = strikeCall;
            double xp = strikePut;

            double i = CriticalValue();

            double sdT = Math.Sqrt(varianceChoosing);
            double d1 = (Math.Log(s / i) + Math.Log(dividendDiscountChoosing / riskFreeDiscountChoosing)
                + (varianceChoosing / 2)) / sdT;
            double d2 = d1 - sdT;

            double sdC = Math.Sqrt(varianceCall);
            double y1 = (Math.Log(s / xc) + Math.Log(dividendDiscountCall / riskFreeDiscountCall) + (varianceCall / 2))
                / sdC;

            double sdP = Math.Sqrt(variancePut);
            double y2 = (Math.Log(s / xp) + Math.Log(dividendDiscountPut / riskFreeDiscountPut) + (variancePut / 2))
                / sdP;

            double rho1 = Math.Sqrt(varianceChoosing / varianceCall);
            double rho2 = Math.Sqrt(varianceChoosing / variancePut);
            var m1 = new BivariateCumulativeNormalDistributionWe04DP(rho1);
            var m2 = new BivariateCumulativeNormalDistributionWe04DP(rho2);

            // the call's deviation in its second term, where QuantLib's reads the put's volatility there
            double complexChooser = (s * dividendDiscountCall * m1.Evaluate(d1, y1))
                - (xc * riskFreeDiscountCall * m1.Evaluate(d2, y1 - sdC));
            complexChooser -= s * dividendDiscountPut * m2.Evaluate(-d1, -y2);
            complexChooser += xp * riskFreeDiscountPut * m2.Evaluate(-d2, -y2 + sdP);
            return complexChooser;
        }

        // QuantLib's bsCalculator: the call or the put at the choosing date, on its forward discounts and variance.
        private BlackScholesCalculator BsCalculator(double spot, OptionType optionType)
        {
            if (optionType == OptionType.Call)
            {
                var vanillaPayoff = new PlainVanillaPayoff(OptionType.Call, strikeCall);
                double vol = Math.Sqrt(varianceCall - varianceChoosing);
                double growth = dividendDiscountCall / dividendDiscountChoosing;
                double discount = riskFreeDiscountCall / riskFreeDiscountChoosing;
                return new BlackScholesCalculator(vanillaPayoff, spot, growth, vol, discount);
            }
            else
            {
                var vanillaPayoff = new PlainVanillaPayoff(OptionType.Put, strikePut);
                double vol = Math.Sqrt(variancePut - varianceChoosing);
                double growth = dividendDiscountPut / dividendDiscountChoosing;
                double discount = riskFreeDiscountPut / riskFreeDiscountChoosing;
                return new BlackScholesCalculator(vanillaPayoff, spot, growth, vol, discount);
            }
        }

        // QuantLib's criticalValue(), capped (ED24): the spot where the call less the put is worth nothing.
        private double CriticalValue() =>
            Newton(spot, sv =>
            {
                BlackScholesCalculator call = BsCalculator(sv, OptionType.Call);
                BlackScholesCalculator put = BsCalculator(sv, OptionType.Put);
                return (call.Value - put.Value, call.Delta() - put.Delta());
            });
    }
}
