// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;

namespace NanoQL.Numerics;

/// <summary>
/// <see cref="Complex"/>'s exponential, power, sine and cosine, each .NET's own formula, with the sine and the cosine
/// of the angle computed apart by <see cref="Math.Sin"/> and <see cref="Math.Cos"/> where .NET's take both from
/// <see cref="Math.SinCos"/>.
/// </summary>
/// <remarks>
/// On macOS, and on Linux x64 under a glibc older than 2.36, whose <c>sincos</c> has no FMA variant where its
/// <c>sin</c> and <c>cos</c> have one, .NET's JIT computes <see cref="Math.SinCos"/> by the platform's combined
/// routine, whose sine or cosine differs from <see cref="Math.Sin"/>'s or <see cref="Math.Cos"/>'s by an ulp for some
/// angles, while Native AOT computes the two apart; so the library's complex arithmetic read different last bits in
/// .NET and through the native library. On Windows, on Linux Arm64 and under a newer glibc the two agree, so these
/// return .NET's own values there, bit for bit.
/// </remarks>
internal static class ComplexFunctions
{
    /// <summary>The exponential, as <see cref="Complex.Exp(Complex)"/>.</summary>
    /// <param name="value">The exponent.</param>
    /// <returns>e raised to <paramref name="value"/>.</returns>
    internal static Complex Exp(Complex value) => FromPolarCoordinates(Math.Exp(value.Real), value.Imaginary);

    /// <summary>A power, as <see cref="Complex.Pow(Complex, Complex)"/>.</summary>
    /// <param name="value">The base.</param>
    /// <param name="power">The exponent.</param>
    /// <returns><paramref name="value"/> raised to <paramref name="power"/>.</returns>
    internal static Complex Pow(Complex value, Complex power)
    {
        if (power == Complex.Zero)
        {
            return Complex.One;
        }

        if (value == Complex.Zero)
        {
            return Complex.Zero;
        }

        double valueReal = value.Real;
        double valueImaginary = value.Imaginary;
        double powerReal = power.Real;
        double powerImaginary = power.Imaginary;

        double rho = Complex.Abs(value);
        double theta = Math.Atan2(valueImaginary, valueReal);
        double newRho = (powerReal * theta) + (powerImaginary * Math.Log(rho));

        double t = Math.Pow(rho, powerReal) * Math.Exp(-powerImaginary * theta);

        return FromPolarCoordinates(t, newRho);
    }

    /// <summary>A real power, as <see cref="Complex.Pow(Complex, double)"/>.</summary>
    /// <param name="value">The base.</param>
    /// <param name="power">The exponent.</param>
    /// <returns><paramref name="value"/> raised to <paramref name="power"/>.</returns>
    internal static Complex Pow(Complex value, double power) => Pow(value, new Complex(power, 0));

    /// <summary>The sine, as <see cref="Complex.Sin(Complex)"/>.</summary>
    /// <param name="value">The angle.</param>
    /// <returns>The sine of <paramref name="value"/>.</returns>
    internal static Complex Sin(Complex value) =>
        new(Math.Sin(value.Real) * Math.Cosh(value.Imaginary), Math.Cos(value.Real) * Math.Sinh(value.Imaginary));

    /// <summary>The cosine, as <see cref="Complex.Cos(Complex)"/>.</summary>
    /// <param name="value">The angle.</param>
    /// <returns>The cosine of <paramref name="value"/>.</returns>
    internal static Complex Cos(Complex value) =>
        new(Math.Cos(value.Real) * Math.Cosh(value.Imaginary), -Math.Sin(value.Real) * Math.Sinh(value.Imaginary));

    // Complex.FromPolarCoordinates, its sine and cosine apart
    private static Complex FromPolarCoordinates(double magnitude, double phase) =>
        new(magnitude * Math.Cos(phase), magnitude * Math.Sin(phase));
}
