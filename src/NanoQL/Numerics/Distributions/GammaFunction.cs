// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/gammadistribution.hpp ql/math/distributions/gammadistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The gamma function, Γ(z) = ∫₀^∞ t^(z−1) e^(−t) dt, and its logarithm.</summary>
/// <remarks>
/// The implementation of the algorithm was inspired by "Numerical Recipes in C", 2nd edition, Press, Teukolsky,
/// Vetterling, Flannery, chapter 6: Lanczos's approximation of ln Γ.
/// </remarks>
public sealed class GammaFunction
{
    private const double c1_ = 76.18009172947146;
    private const double c2_ = -86.50532032941677;
    private const double c3_ = 24.01409824083091;
    private const double c4_ = -1.231739572450155;
    private const double c5_ = 0.1208650973866179e-2;
    private const double c6_ = -0.5395239384953e-5;

    // QuantLib's M_PI.
    private const double M_PI = 3.141592653589793238462643383280;

    /// <summary>Γ(x): exp(ln Γ(x)) from 1 up, Γ(x + 1)/x down to −20, and the reflection formula below.</summary>
    /// <param name="x">The argument; NaN recurses without end, as in QuantLib.</param>
    /// <returns>Γ(x).</returns>
    public double Value(double x)
    {
        if (x >= 1.0)
        {
            return Math.Exp(LogValue(x));
        }
        else
        {
            if (x > -20.0)
            {
                // \Gamma(x) = \frac{\Gamma(x+1)}{x}
                return Value(x + 1.0) / x;
            }
            else
            {
                // \Gamma(-x) = -\frac{\pi}{\Gamma(x)\sin(\pi x) x}
                return -M_PI / (Value(-x) * x * Math.Sin(M_PI * x));
            }
        }
    }

    /// <summary>ln Γ(x).</summary>
    /// <param name="x">The argument, positive.</param>
    /// <returns>ln Γ(x).</returns>
    /// <exception cref="ArgumentException"><paramref name="x"/> isn't positive.</exception>
    public double LogValue(double x)
    {
        if (!(x > 0.0))
        {
            throw new ArgumentException("positive argument required", nameof(x));
        }

        double temp = x + 5.5;
        temp -= (x + 0.5) * Math.Log(temp);
        double ser = 1.000000000190015;
        ser += c1_ / (x + 1.0);
        ser += c2_ / (x + 2.0);
        ser += c3_ / (x + 3.0);
        ser += c4_ / (x + 4.0);
        ser += c5_ / (x + 5.0);
        ser += c6_ / (x + 6.0);

        return -temp + Math.Log(2.5066282746310005 * ser / x);
    }
}
