// SPDX-FileCopyrightText: Copyright (C) 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisegreeks/swaptionpseudojacobian.hpp ql/models/marketmodels/pathwisegreeks/swaptionpseudojacobian.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.PricingEngines;

namespace NanoQL.Models;

/// <summary>
/// A cap's implied volatility in a market model, the flat undisplaced Black volatility that reprices the model's
/// displaced caplets, and the derivatives of its price and its implied volatility with respect to the pseudo-roots'
/// elements, step by step, for market vegas.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: computed at construction, as QuantLib's constructor computes; its matrix reads return copies.
/// </para>
/// <para>
/// <b>The implied volatility's solve</b> (a documented divergence;
/// <c>Divergences/CapPseudoDerivativeBracketTests.cs</c>): QuantLib's Brent bracket, [0.99, 1.01] times the least
/// displaced and the greatest scaled caplet volatility (<c>swaptionpseudojacobian.cpp:331</c>), first; where it doesn't
/// hold the root, as away from the money on a displaced model, Brent's bracketing solve from QuantLib's guess, where
/// QuantLib refuses "root not bracketed". Wherever QuantLib's bracket holds the root, QuantLib's bits. A negative
/// displacement is refused by Black's formula's own check, as in QuantLib.
/// </para>
/// </remarks>
public sealed class CapPseudoDerivative
{
    private readonly Matrix[] volatilityDerivatives;
    private readonly Matrix[] priceDerivatives;

    /// <summary>Initializes a new instance of the <see cref="CapPseudoDerivative"/> class.</summary>
    /// <param name="inputModel">The model.</param>
    /// <param name="strike">The cap's strike.</param>
    /// <param name="startIndex">The cap's first caplet's rate.</param>
    /// <param name="endIndex">The cap's end, after its last caplet's rate, at most the number of rates.</param>
    /// <param name="firstDF">The discount factor of the first rate time, scaling the caplets' annuities.</param>
    /// <exception cref="ArgumentException">
    /// The start index isn't before the end, or the end is past the rates, or a displacement is negative.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The start index is negative.</exception>
    /// <exception cref="NanoQLException">The implied volatility's solve fails.</exception>
    public CapPseudoDerivative(MarketModel inputModel, double strike, int startIndex, int endIndex, double firstDF)
    {
        ArgumentNullException.ThrowIfNull(inputModel);
        if (!(startIndex < endIndex))
        {
            throw new ArgumentException(
                "for a cap pseudo derivative the start of the cap must be before the end", nameof(startIndex));
        }

        if (!(endIndex <= inputModel.NumberOfRates))
        {
            throw new ArgumentException(
                "for a cap pseudo derivative the end of the cap must before the end of the rates", nameof(endIndex));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);

        int numberCaplets = endIndex - startIndex;
        int numberRates = inputModel.NumberOfRates;
        int factors = inputModel.NumberOfFactors;
        EvolutionDescription evolution = inputModel.Evolution;
        int numberSteps = evolution.NumberOfSteps;
        ReadOnlySpan<double> rateTimes = evolution.RateTimes.AsSpan();
        ReadOnlySpan<double> taus = evolution.RateTaus.AsSpan();
        ReadOnlySpan<double> forwards = inputModel.InitialRates.AsSpan();
        ReadOnlySpan<double> displacements = inputModel.Displacements.AsSpan();

        var curve = new LMMCurveState(rateTimes);
        curve.SetOnForwardRates(forwards);

        Matrix totalCovariance = inputModel.TotalCovariance(numberSteps - 1);

        double[] displacedImpliedVols = new double[numberCaplets];
        double[] annuities = new double[numberCaplets];
        double[] initialRates = new double[numberCaplets];
        double[] expiries = new double[numberCaplets];

        double capPrice = 0.0;
        double guess = 0.0;
        double minVol = 1e10;
        double maxVol = 0.0;

        for (int j = startIndex; j < endIndex; ++j)
        {
            int capletIndex = j - startIndex;
            double resetTime = rateTimes[j];
            expiries[capletIndex] = resetTime;

            double sd = Math.Sqrt(totalCovariance[j, j]);
            displacedImpliedVols[capletIndex] = Math.Sqrt(totalCovariance[j, j] / resetTime);

            double forward = forwards[j];
            initialRates[capletIndex] = forward;

            double annuity = curve.DiscountRatio(j + 1, 0) * taus[j] * firstDF;
            annuities[capletIndex] = annuity;

            double displacement = displacements[j];

            guess += displacedImpliedVols[capletIndex] * (forward + displacement) / forward;

            minVol = Math.Min(minVol, displacedImpliedVols[capletIndex]);
            maxVol = Math.Max(maxVol, displacedImpliedVols[capletIndex] * (forward + displacement) / forward);

            double capletPrice = BlackFormulaFunctions.BlackFormula(OptionType.Call, strike, forward, sd, annuity, displacement);

            capPrice += capletPrice;
        }

        guess /= numberCaplets;

        priceDerivatives = new Matrix[numberSteps];
        for (int step = 0; step < numberSteps; ++step)
        {
            var thisDerivative = new Matrix(numberRates, factors, 0.0);
            Matrix pseudoRoot = inputModel.PseudoRoot(step);

            for (int rate = Math.Max(evolution.FirstAliveRate[step], startIndex); rate < endIndex; ++rate)
            {
                for (int f = 0; f < factors; ++f)
                {
                    double expiry = rateTimes[rate];
                    double volDerivative = pseudoRoot[rate, f] / (displacedImpliedVols[rate - startIndex] * expiry);
                    double capletVega = BlackFormulaFunctions.BlackFormulaVolDerivative(
                        strike,
                        forwards[rate],
                        displacedImpliedVols[rate - startIndex] * Math.Sqrt(expiry),
                        expiry,
                        annuities[rate - startIndex],
                        displacements[rate]);

                    // note that the cap derivative  is equal to one of the caplet ones so we lose a loop
                    thisDerivative[rate, f] = volDerivative * capletVega;
                }
            }

            priceDerivatives[step] = thisDerivative;
        }

        var capPricer = new QuickCap(strike, annuities, initialRates, expiries, capPrice);

        const int maxEvaluations = 1000;
        const double accuracy = 1E-6;

        Brent solver = new Brent().SetMaxEvaluations(maxEvaluations);

        // QuantLib's bracket first; where it doesn't hold the root, the bracketing solve from its guess
        double xMin = minVol * 0.99, xMax = maxVol * 1.01;
        double fxMin = capPricer.Value(xMin), fxMax = capPricer.Value(xMax);
        ImpliedVolatility = fxMin == 0.0 || fxMax == 0.0 || fxMin * fxMax < 0.0
            ? solver.Solve(capPricer.Value, accuracy, guess, xMin, xMax)
            : solver.Solve(capPricer.Value, accuracy, guess, 0.01);

        double vega = capPricer.Vega(ImpliedVolatility);

        volatilityDerivatives = new Matrix[numberSteps];
        for (int step = 0; step < numberSteps; ++step)
        {
            var thisDerivative = new Matrix(numberRates, factors, 0.0);

            for (int rate = Math.Max(evolution.FirstAliveRate[step], startIndex); rate < endIndex; ++rate)
            {
                for (int f = 0; f < factors; ++f)
                {
                    thisDerivative[rate, f] = priceDerivatives[step][rate, f] / vega;
                }
            }

            volatilityDerivatives[step] = thisDerivative;
        }
    }

    /// <summary>Gets the cap's implied volatility.</summary>
    public double ImpliedVolatility { get; }

    /// <summary>The cap price's derivatives with respect to a step's pseudo-root elements.</summary>
    /// <param name="i">The step.</param>
    /// <returns>A copy of the derivatives, a row per rate and a column per factor.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public Matrix PriceDerivative(int i) => Step(priceDerivatives, i).Clone();

    /// <summary>The implied volatility's derivatives with respect to a step's pseudo-root elements.</summary>
    /// <param name="i">The step.</param>
    /// <returns>A copy of the derivatives, a row per rate and a column per factor.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a step.</exception>
    public Matrix VolatilityDerivative(int i) => Step(volatilityDerivatives, i).Clone();

    // QuantLib reads its vector unchecked.
    private static Matrix Step(Matrix[] derivatives, int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, derivatives.Length);
        return derivatives[i];
    }
}

/// <summary>
/// The cap's undisplaced Black price at a flat volatility, less a target price, and its vega: QuantLib's
/// <c>QuickCap</c>, in an anonymous namespace.
/// </summary>
file sealed class QuickCap(double strike, double[] annuities, double[] currentRates, double[] expiries, double price)
{
    // returns difference from input price
    public double Value(double volatility)
    {
        double p = 0.0;
        for (int i = 0; i < annuities.Length; ++i)
        {
            p += BlackFormulaFunctions.BlackFormula(
                OptionType.Call, strike, currentRates[i], volatility * Math.Sqrt(expiries[i]), annuities[i]);
        }

        return p - price;
    }

    // returns vol derivative
    public double Vega(double volatility)
    {
        double vega = 0.0;
        for (int i = 0; i < annuities.Length; ++i)
        {
            vega += BlackFormulaFunctions.BlackFormulaVolDerivative(
                strike, currentRates[i], volatility * Math.Sqrt(expiries[i]), expiries[i], annuities[i], 0.0);
        }

        return vega;
    }
}
