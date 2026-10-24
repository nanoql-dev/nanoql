// SPDX-FileCopyrightText: Copyright (C) 2015 Johannes Göttker-Schnetmann
// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/riskneutraldensitycalculator.hpp ql/methods/finitedifferences/utilities/riskneutraldensitycalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The interface of a single asset's risk-neutral terminal density: its density, its cumulative distribution and the
/// distribution's inverse at a time.
/// </summary>
/// <remarks>
/// Unsealed and abstract, as QuantLib's base: each density's calculator derives it. QuantLib's protected
/// <c>InvCDFHelper</c>, the inverse by Brent's method on the cumulative distribution, is internal here, for the
/// library's own calculators.
/// </remarks>
public abstract class RiskNeutralDensityCalculator
{
    /// <summary>The density at <paramref name="x"/>.</summary>
    /// <param name="x">The asset's value.</param>
    /// <param name="t">The time.</param>
    /// <returns>The density.</returns>
    public abstract double Pdf(double x, double t);

    /// <summary>The probability of a value below <paramref name="x"/>.</summary>
    /// <param name="x">The asset's value.</param>
    /// <param name="t">The time.</param>
    /// <returns>The cumulative probability.</returns>
    public abstract double Cdf(double x, double t);

    /// <summary>The value below which the probability is <paramref name="p"/>.</summary>
    /// <param name="p">The probability.</param>
    /// <param name="t">The time.</param>
    /// <returns>The value.</returns>
    public abstract double InvCdf(double p, double t);

    /// <summary>
    /// QuantLib's <c>InvCDFHelper</c>: the inverse of a calculator's cumulative distribution by Brent's method from a
    /// guess.
    /// </summary>
    internal sealed class InvCdfHelper
    {
        private readonly RiskNeutralDensityCalculator calculator;
        private readonly double guess;
        private readonly double accuracy;
        private readonly int maxEvaluations;
        private readonly double stepSize;

        /// <summary>Initializes a new instance of the <see cref="InvCdfHelper"/> class.</summary>
        /// <param name="calculator">The calculator whose distribution is inverted.</param>
        /// <param name="guess">Brent's first guess.</param>
        /// <param name="accuracy">Brent's accuracy.</param>
        /// <param name="maxEvaluations">Brent's limit on the evaluations.</param>
        /// <param name="stepSize">Brent's step from the guess when it brackets the root.</param>
        internal InvCdfHelper(
            RiskNeutralDensityCalculator calculator, double guess, double accuracy, int maxEvaluations, double stepSize = 0.01)
        {
            this.calculator = calculator;
            this.guess = guess;
            this.accuracy = accuracy;
            this.maxEvaluations = maxEvaluations;
            this.stepSize = stepSize;
        }

        /// <summary>The value at which the calculator's cumulative distribution is <paramref name="p"/>.</summary>
        /// <param name="p">The probability.</param>
        /// <param name="t">The time.</param>
        /// <returns>The value.</returns>
        /// <exception cref="NanoQLException">Brent's method fails.</exception>
        internal double InverseCdf(double p, double t) =>
            new Brent().SetMaxEvaluations(maxEvaluations)
                .Solve(x => calculator.Cdf(x, t) - p, accuracy, guess, stepSize);
    }
}
