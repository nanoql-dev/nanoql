// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic lookback engines' small-carry class (the S-bar plan's BR5 (xxiii), the user's ruling (b) of
/// 2026-10-26): as the carry b = r − q vanishes, each formula's terms over x = 2b/σ² cancel, QuantLib's double
/// departing from the 50-digit value by more than 1e-12 of max(|value|, 1) up to u = |x|σ√T of about 5e-4, and the
/// partial engines are NaN at b = 0.
/// </summary>
/// <remarks>
/// <para>
/// The full engines evaluate QuantLib's expressions in double-double, over <see cref="DoubleDoubleNormal"/>, below
/// <see cref="FullCut"/>, down to <see cref="FullFloor"/>, where double-double cancels past 1e-12 in its turn; below
/// that, the cubic through the double-double values at the carries where u is the floor and twice it, either side of
/// 0; and at b = 0 #2856's limit. The partial engines take, below <see cref="PartialCut"/>, the cubic through
/// QuantLib's double values at the carries where u is the cut and twice it, either side of 0. Each cut is measured
/// against 50-digit values of the formula (the run's probes <c>c2_smallcarry_refs.py</c> and
/// <c>c2_smallcarry_cut.py</c>).
/// </para>
/// <para>
/// A carry moves the dividend side alone: r and its discount as read, q = r − b and its discount the read one times
/// e^{(b − b₀)T}, as a flat dividend curve shifted on the volatility's clock.
/// </para>
/// </remarks>
internal static class LookbackSmallCarry
{
    /// <summary>The u below which the full engines evaluate in double-double.</summary>
    internal const double FullCut = 1.0e-3;

    /// <summary>The u below which the full engines' double-double cancels past 1e-12 in its turn.</summary>
    internal const double FullFloor = 1.0e-18;

    private static readonly CumulativeNormalDistribution F = new();

    /// <summary>
    /// The u below which the partial engines take the cubic through their double values: 2e-3 where σ√T is at most 1,
    /// falling as (σ√T)^−0.8 to 6e-4, where the cubic's truncation grows with the carry's reach over a long, volatile
    /// term, while QuantLib's double, off by up to about 1.2e-15/u, stays within 1e-12 at the nodes.
    /// </summary>
    /// <param name="stdDeviation">σ√T.</param>
    /// <returns>The cut.</returns>
    internal static double PartialCut(double stdDeviation) => Math.Clamp(2.0e-3 * Math.Pow(stdDeviation, -0.8), 6.0e-4, 2.0e-3);

    /// <summary>u = |x| σ√T = |2b√T/σ|, the scale of the cancellation.</summary>
    /// <param name="carry">The carry b.</param>
    /// <param name="volatility">The volatility σ.</param>
    /// <param name="t">The time to expiry T.</param>
    /// <returns>u.</returns>
    internal static double U(double carry, double volatility, double t) => Math.Abs(2.0 * carry * Math.Sqrt(t) / volatility);

    /// <summary>The carry at which u takes a value, positive.</summary>
    /// <param name="u">The u.</param>
    /// <param name="volatility">The volatility σ.</param>
    /// <param name="t">The time to expiry T.</param>
    /// <returns>The carry.</returns>
    internal static double CarryAt(double u, double volatility, double t) => u * volatility / (2.0 * Math.Sqrt(t));

    /// <summary>The cubic through a function's values at −2c, −c, c and 2c, at a carry, by Lagrange's form.</summary>
    /// <param name="value">The function of the carry.</param>
    /// <param name="carry">The carry at which the cubic is read.</param>
    /// <param name="c">The inner node, positive.</param>
    /// <returns>The cubic's value.</returns>
    internal static double Cubic(Func<double, double> value, double carry, double c)
    {
        ReadOnlySpan<double> nodes = [-2.0 * c, -c, c, 2.0 * c];
        double result = 0.0;
        for (int i = 0; i < 4; ++i)
        {
            double basis = 1.0;
            for (int j = 0; j < 4; ++j)
            {
                if (j != i)
                {
                    basis *= (carry - nodes[j]) / (nodes[i] - nodes[j]);
                }
            }

            result += value(nodes[i]) * basis;
        }

        return result;
    }

    /// <summary>The cumulative normal in the arithmetic: QuantLib's in double, <see cref="DoubleDoubleNormal"/>'s in
    /// double-double.</summary>
    /// <typeparam name="T">The arithmetic.</typeparam>
    /// <param name="x">The argument.</param>
    /// <returns>N(x).</returns>
    internal static T N<T>(T x)
        where T : struct, IRealArithmetic<T> =>
        typeof(T) == typeof(DoubleDouble)
            ? (T)(object)DoubleDoubleNormal.Cdf((DoubleDouble)(object)x)
            : T.Of(F.Evaluate(x.Value));

    /// <summary>s^a·N(z) in the arithmetic: QuantLib's product, its log taken in double where it isn't finite
    /// (<see cref="LookbackTails.PowerTimesN"/>); double-double runs only near zero carry, where a is small.</summary>
    /// <typeparam name="T">The arithmetic.</typeparam>
    /// <param name="product">QuantLib's product.</param>
    /// <param name="s">The base.</param>
    /// <param name="a">The exponent.</param>
    /// <param name="z">The normal's argument.</param>
    /// <returns>The product.</returns>
    internal static T PowerTimesN<T>(T product, T s, T a, T z)
        where T : struct, IRealArithmetic<T> =>
        typeof(T) == typeof(RealDouble) ? T.Of(LookbackTails.PowerTimesN(product.Value, s.Value, a.Value, z.Value)) : product;

    /// <summary>A real power in the arithmetic: <c>std::pow</c> in double, e^{y ln x} in double-double.</summary>
    /// <typeparam name="T">The arithmetic.</typeparam>
    /// <param name="x">The base, positive.</param>
    /// <param name="y">The exponent.</param>
    /// <returns>x^y.</returns>
    internal static T Power<T>(T x, T y)
        where T : struct, IRealArithmetic<T> =>
        typeof(T) == typeof(DoubleDouble) ? T.Exp(T.Log(x) * y) : T.Of(Math.Pow(x.Value, y.Value));
}
