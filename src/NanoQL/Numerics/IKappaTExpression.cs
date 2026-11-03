// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Numerics;

/// <summary>
/// One of QuantLib's generated Heston expressions (LPP2's and LPP3's coefficients, COS's cumulants) as a function of κ,
/// its other inputs fixed, written once over <see cref="IRealArithmetic{T}"/>.
/// </summary>
internal interface IKappaTExpression
{
    /// <summary>The expression at κ, its other inputs converted exactly into the arithmetic.</summary>
    /// <typeparam name="T">The arithmetic.</typeparam>
    /// <param name="kappa">The mean-reversion speed.</param>
    /// <returns>The expression's value.</returns>
    T Evaluate<T>(T kappa)
        where T : struct, IRealArithmetic<T>;

    /// <summary>
    /// The expression's value at κ for a term t, in the arithmetic κt calls for: QuantLib's double, and so its bits,
    /// from the cut to the upper cut; double-double below the cut, down to its floor; quad-double below that, down to
    /// its own floor; below the quad-double floor the cubic in κ through the quad-double values at κ = quadFloor/t and
    /// its next three multiples; and extended range past the upper cut, where e^{κt}'s powers overflow.
    /// </summary>
    /// <typeparam name="TExpression">The expression's type.</typeparam>
    /// <param name="expression">The expression.</param>
    /// <param name="kappa">The mean-reversion speed.</param>
    /// <param name="t">The term, positive.</param>
    /// <param name="cut">The κt below which QuantLib's double cancels past 1e-12 relative.</param>
    /// <param name="floor">The κt below which double-double cancels past 1e-12 relative, at most the cut.</param>
    /// <param name="quadFloor">The κt below which quad-double cancels past 1e-12 relative, at most the floor.</param>
    /// <param name="upper">The κt above which QuantLib's double departs past 1e-12 relative as its exponentials
    /// overflow.</param>
    /// <returns>The value, rounded to a double.</returns>
    static double Evaluate<TExpression>(TExpression expression, double kappa, double t, double cut, double floor, double quadFloor, double upper)
        where TExpression : IKappaTExpression
    {
        double kt = kappa * t;
        if (kt > upper)
        {
            return expression.Evaluate(ExtendedDouble.Of(kappa)).Value;
        }

        if (!(kt < cut))
        {
            return expression.Evaluate(RealDouble.Of(kappa)).Value;
        }

        if (!(kt < floor))
        {
            return expression.Evaluate(DoubleDouble.Of(kappa)).Value;
        }

        if (!(kt < quadFloor))
        {
            return expression.Evaluate(QuadDouble.Of(kappa)).Value;
        }

        // the cubic through (k_i, f(k_i)), k_i = (i + 1) quadFloor / t, evaluated by Lagrange's form at kappa
        double k0 = quadFloor / t;
        double result = 0.0;
        for (int i = 0; i < 4; ++i)
        {
            double ki = (i + 1) * k0;
            double basis = 1.0;
            for (int j = 0; j < 4; ++j)
            {
                if (j != i)
                {
                    double kj = (j + 1) * k0;
                    basis *= (kappa - kj) / (ki - kj);
                }
            }

            result += expression.Evaluate(QuadDouble.Of(ki)).Value * basis;
        }

        return result;
    }
}
