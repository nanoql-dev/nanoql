// SPDX-FileCopyrightText: Copyright (C) 2014 Master IMAFA - Polytech'Nice Sophia - Université de Nice Sophia Antipolis
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/exotic/analyticholderextensibleoptionengine.hpp ql/pricingengines/exotic/analyticholderextensibleoptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for a holder-extensible option: Longstaff's formula, the vanilla to the first maturity and
/// bivariate terms over the region where the holder extends, between two critical prices.
/// </summary>
/// <remarks>
/// <para>
/// Stateless. QuantLib's fixes after v1.44 (#2892) are taken: the put's correlation sign, the Newton seeds at the
/// strike, the put's derivative, the empty extension region and the call's with dividends
/// (<c>Divergences/HolderExtensibleCriticalPricesTests.cs</c>). QuantLib evaluates the formula on one zero rate,
/// dividend yield and volatility, read at the first maturity, for both periods, so it is exact only on flat markets;
/// here each term reads the variances to each maturity at the first strike, the correlation from their ratio and the
/// discounts by date, and the critical prices solve the forward Black values from the first maturity
/// (<c>Divergences/HolderExtensibleTermStructureTests.cs</c>, against the exact integral at the first maturity within
/// the critical prices' accuracy). The bivariate normal is West's, QuantLib's default, where its engine reads
/// Drezner's; an infinite critical price meets its limit there. QuantLib's Newton iterations and stopping rule are
/// kept, capped at 1,000 iterations.
/// </para>
/// <para>
/// Every structure is read by date, accruing nothing before its own date
/// (<c>Divergences/ExoticMismatchedTimesTests.cs</c>); a date before the process's clock keeps QuantLib's "negative
/// time" refusal.
/// </para>
/// </remarks>
public static class AnalyticHolderExtensibleOptionEngine
{
    /// <summary>Prices a holder-extensible option: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The underlying's process.</param>
    /// <returns>The option's results: the value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The option's terms are invalid, its payoff isn't plain vanilla, a date is before the clock's date, or a critical
    /// price isn't found in 1,000 iterations.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        HolderExtensibleOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        return option.FetchResults(new OneAssetOptionResults { NPV = new Form(option, payoff, process).Value() });
    }

    // QuantLib's engine over one call's option, its time and rate reads made discounts and variances by date.
    private sealed class Form
    {
        private readonly PlainVanillaPayoff payoff;
        private readonly double spot, x1, x2, premium;
        private readonly double dividendDiscount1, riskFreeDiscount1, variance1;
        private readonly double dividendDiscount2, riskFreeDiscount2, variance2;

        public Form(HolderExtensibleOption option, PlainVanillaPayoff payoff, GeneralizedBlackScholesProcess process)
        {
            this.payoff = payoff;
            spot = process.X0;
            x1 = payoff.Strike;
            x2 = option.SecondStrike;
            premium = option.Premium;
            TermStructure clock = FdmBlackScholesOp.Clock(process);
            YieldTermStructure qTS = process.DividendYield, rTS = process.RiskFreeRate;
            Date first = option.Exercise.LastDate(), second = option.SecondExpiryDate;
            dividendDiscount1 = qTS.AccruedDiscountOn(clock, first);
            riskFreeDiscount1 = rTS.AccruedDiscountOn(clock, first);
            dividendDiscount2 = qTS.AccruedDiscountOn(clock, second);
            riskFreeDiscount2 = rTS.AccruedDiscountOn(clock, second);

            // QuantLib reads one volatility, at the first strike
            variance1 = process.BlackVolatility.AccruedVariance(first, x1);
            variance2 = process.BlackVolatility.AccruedVariance(second, x1);
        }

        // the forward discounts and deviation over the extension, from the first maturity to the second
        private double Growth => dividendDiscount2 / dividendDiscount1;

        private double Discount => riskFreeDiscount2 / riskFreeDiscount1;

        private double ExtensionStdDev => Math.Sqrt(variance2 - variance1);

        // QuantLib's calculate().
        public double Value()
        {
            double s = spot;
            double a = premium;
            double stdDev1 = Math.Sqrt(variance1);
            double stdDev2 = Math.Sqrt(variance2);
            double z1 = Z1(stdDev2);
            double z2 = Z2(stdDev1);
            double rho = Math.Sqrt(variance1 / variance2);

            double growth = dividendDiscount1;
            double discount = riskFreeDiscount1;
            double minusInf = double.NegativeInfinity;

            // extension never pays if it is not worth the premium at X1
            if (BsCalculator(x1, payoff.OptionType).Value <= a)
            {
                return new BlackScholesCalculator(payoff, s, growth, stdDev1, discount).Value;
            }

            double y1 = Y(payoff.OptionType == OptionType.Call ? I2Call() : I2Put(), stdDev1);
            double y2 = Y(payoff.OptionType == OptionType.Call ? I1Call() : I1Put(), stdDev1);
            if (payoff.OptionType == OptionType.Call)
            {
                // instantiate payoff function for a call
                var vanillaCallPayoff = new PlainVanillaPayoff(OptionType.Call, x1);
                double bsm = new BlackScholesCalculator(vanillaCallPayoff, s, growth, stdDev1, discount).Value;
                return bsm
                    + (s * dividendDiscount2 * M2(y1, y2, minusInf, z1, rho))
                    - (x2 * riskFreeDiscount2 * M2(y1 - stdDev1, y2 - stdDev1, minusInf, z1 - stdDev2, rho))
                    - (s * dividendDiscount1 * N2(y1, z2)) + (x1 * riskFreeDiscount1 * N2(y1 - stdDev1, z2 - stdDev1))
                    - (a * riskFreeDiscount1 * N2(y1 - stdDev1, y2 - stdDev1));
            }

            var vanillaPutPayoff = new PlainVanillaPayoff(OptionType.Put, x1);
            return new BlackScholesCalculator(vanillaPutPayoff, s, growth, stdDev1, discount).Value
                - (s * dividendDiscount2 * M2(y1, y2, minusInf, -z1, -rho))
                + (x2 * riskFreeDiscount2 * M2(y1 - stdDev1, y2 - stdDev1, minusInf, -z1 + stdDev2, -rho))
                + (s * dividendDiscount1 * N2(z2, y2)) - (x1 * riskFreeDiscount1 * N2(z2 - stdDev1, y2 - stdDev1))
                - (a * riskFreeDiscount1 * N2(y1 - stdDev1, y2 - stdDev1));
        }

        private static double M2(double a, double b, double c, double d, double rho)
        {
            var cmlNormDist = new BivariateCumulativeNormalDistributionWe04DP(rho);
            return cmlNormDist.Evaluate(b, d) - cmlNormDist.Evaluate(a, d) - cmlNormDist.Evaluate(b, c)
                + cmlNormDist.Evaluate(a, c);
        }

        private static double N2(double a, double b)
        {
            var normDist = new CumulativeNormalDistribution();
            return normDist.Evaluate(b) - normDist.Evaluate(a);
        }

        // QuantLib's I1Call: the spot at the first maturity where the extended call is worth the premium.
        private double I1Call()
        {
            double sv = x1;
            if (premium == 0)
            {
                return 0;
            }

            return Newton(sv, OptionType.Call, (c, s) => c - premium, d => d - 0);
        }

        // QuantLib's I2Call: where the extended call less the premium is worth the intrinsic value.
        private double I2Call()
        {
            double sv = x1;
            double val = x1 - (x2 * Discount);

            // with dividends, exercise beats extension at a high enough spot
            if (premium < val && Growth >= 1.0)
            {
                return double.PositiveInfinity;
            }

            return Newton(sv, OptionType.Call, (c, s) => c - premium - s + x1, d => d - 1);
        }

        // QuantLib's I1Put: where the extended put less the premium is worth the intrinsic value.
        private double I1Put()
        {
            double sv = x1;
            if (premium <= (x2 * Discount) - x1)
            {
                return 0.0;
            }

            return Newton(sv, OptionType.Put, (p, s) => p - premium + s - x1, d => d + 1);
        }

        // QuantLib's I2Put: the spot at the first maturity where the extended put is worth the premium.
        private double I2Put()
        {
            double sv = x1;
            if (premium == 0)
            {
                return double.PositiveInfinity;
            }

            return Newton(sv, OptionType.Put, (p, s) => p - premium, d => d - 0);
        }

        // QuantLib's Newton-Raphson loops, capped.
        private double Newton(double sv, OptionType type, Func<double, double, double> y, Func<double, double> dy) =>
            AnalyticComplexChooserEngine.Newton(sv, s =>
            {
                BlackScholesCalculator bs = BsCalculator(s, type);
                return (y(bs.Value, s), dy(bs.Delta()));
            });

        // QuantLib's bsCalculator: the option extended to the second maturity at the second strike, valued at the
        // first, on its forward discounts and variance.
        private BlackScholesCalculator BsCalculator(double spot, OptionType optionType)
        {
            var vanillaPayoff = new PlainVanillaPayoff(optionType, x2);
            return new BlackScholesCalculator(vanillaPayoff, spot, Growth, ExtensionStdDev, Discount);
        }

        private double Y(double criticalPrice, double stdDev1) =>
            (Math.Log(spot / criticalPrice) + Math.Log(dividendDiscount1 / riskFreeDiscount1) + (variance1 / 2)) / stdDev1;

        private double Z1(double stdDev2) =>
            (Math.Log(spot / x2) + Math.Log(dividendDiscount2 / riskFreeDiscount2) + (variance2 / 2)) / stdDev2;

        private double Z2(double stdDev1) =>
            (Math.Log(spot / x1) + Math.Log(dividendDiscount1 / riskFreeDiscount1) + (variance1 / 2)) / stdDev1;
    }
}
