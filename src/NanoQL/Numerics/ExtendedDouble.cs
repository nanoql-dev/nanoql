// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Numerics;

/// <summary>
/// An extended-range double: a double mantissa in [1, 2) times 2 to an integer exponent, normalized after each
/// operation, so a value past double's range (e^{5κt} at κt = 600) keeps double's 53-bit precision (the S-hes plan's
/// HR11), as SLATEC's extended-range arithmetic holds a value and an integer exponent.
/// </summary>
/// <remarks>
/// <see cref="Value"/> rounds to a double, overflowing to infinity or underflowing to zero past double's range. Zero is a
/// zero mantissa with exponent 0; a non-finite input gives a non-finite mantissa, which every operation carries.
/// </remarks>
internal readonly struct ExtendedDouble : IRealArithmetic<ExtendedDouble>, IEquatable<ExtendedDouble>
{
    // ln 2 split for the exponential's reduction: its first 32 bits, exact in any multiple below 2^21, and the rest
    private const double Ln2Hi = 0.693147180369123816490;
    private const double Ln2Lo = 1.90821492927058770002e-10;

    private ExtendedDouble(double mantissa, long exponent)
    {
        if (mantissa == 0.0 || !double.IsFinite(mantissa))
        {
            Mantissa = mantissa;
            Exponent = 0;
            return;
        }

        int e = Math.ILogB(mantissa);
        Mantissa = Math.ScaleB(mantissa, -e);
        Exponent = exponent + e;
    }

    /// <summary>Gets the mantissa, in [1, 2) in magnitude, zero or non-finite.</summary>
    public double Mantissa { get; }

    /// <summary>Gets the binary exponent.</summary>
    public long Exponent { get; }

    /// <inheritdoc/>
    public double Value =>
        !double.IsFinite(Mantissa) || Mantissa == 0.0
            ? Mantissa
            : Exponent > 2048 ? Math.CopySign(double.PositiveInfinity, Mantissa)
            : Exponent < -2048 ? Math.CopySign(0.0, Mantissa)
            : Math.ScaleB(Mantissa, (int)Exponent);

    /// <inheritdoc/>
    public static ExtendedDouble Of(double x) => new(x, 0);

    /// <inheritdoc/>
    public static ExtendedDouble Sqrt(ExtendedDouble x)
    {
        if (x.Mantissa == 0.0 || !double.IsFinite(x.Mantissa) || x.Mantissa < 0.0)
        {
            return Of(Math.Sqrt(x.Mantissa));
        }

        // an even exponent halves exactly; an odd one moves a factor 2 into the mantissa
        long odd = x.Exponent & 1;
        return new ExtendedDouble(Math.Sqrt(Math.ScaleB(x.Mantissa, (int)odd)), (x.Exponent - odd) / 2);
    }

    /// <inheritdoc/>
    public static ExtendedDouble Exp(ExtendedDouble x)
    {
        double v = x.Value;
        if (double.IsNaN(v))
        {
            return Of(v);
        }

        if (double.IsInfinity(v) || Math.Abs(v) > 1e15)
        {
            return v > 0.0 ? Of(double.PositiveInfinity) : Of(0.0);
        }

        // e^v = 2^k e^r, r = v - k ln 2 in [-ln 2 / 2, ln 2 / 2], Cody and Waite's two-part reduction
        double k = Math.Round(v / (Ln2Hi + Ln2Lo));
        double r = (v - (k * Ln2Hi)) - (k * Ln2Lo);
        return new ExtendedDouble(Math.Exp(r), (long)k);
    }

    /// <inheritdoc/>
    public static ExtendedDouble Log(ExtendedDouble x)
    {
        if (x.Mantissa == 0.0 || !double.IsFinite(x.Mantissa) || x.Mantissa < 0.0)
        {
            return Of(Math.Log(x.Mantissa));
        }

        // ln(m 2^e) = ln m + e ln 2, m in [1, 2)
        return Of(Math.Log(x.Mantissa) + (x.Exponent * (Ln2Hi + Ln2Lo)));
    }

    /// <inheritdoc/>
    public static ExtendedDouble Pow(ExtendedDouble x, int n)
    {
        // repeated squaring, each product normalized
        if (n == 0)
        {
            return Of(1.0);
        }

        int count = Math.Abs(n);
        ExtendedDouble r = x;
        ExtendedDouble s = Of(1.0);
        while (count > 0)
        {
            if (count % 2 == 1)
            {
                s *= r;
            }

            count /= 2;
            if (count > 0)
            {
                r *= r;
            }
        }

        return n < 0 ? 1.0 / s : s;
    }

    /// <inheritdoc/>
    public static ExtendedDouble Pow(ExtendedDouble x, double y)
    {
        if (y == 1.5)
        {
            return x * Sqrt(x);
        }

        return Exp(Log(x) * y);
    }

    /// <inheritdoc/>
    public static ExtendedDouble operator +(ExtendedDouble a, ExtendedDouble b)
    {
        if (a.Mantissa == 0.0 || !double.IsFinite(b.Mantissa))
        {
            return !double.IsFinite(a.Mantissa) ? Of(a.Mantissa + b.Mantissa) : b;
        }

        if (b.Mantissa == 0.0 || !double.IsFinite(a.Mantissa))
        {
            return a;
        }

        // the smaller aligned onto the larger's exponent; past 2^-1100 it can't move the sum
        (ExtendedDouble big, ExtendedDouble small) = a.Exponent >= b.Exponent ? (a, b) : (b, a);
        long shift = small.Exponent - big.Exponent;
        double aligned = shift < -1100 ? 0.0 : Math.ScaleB(small.Mantissa, (int)shift);
        return new ExtendedDouble(big.Mantissa + aligned, big.Exponent);
    }

    /// <inheritdoc/>
    public static ExtendedDouble operator +(ExtendedDouble a, double b) => a + Of(b);

    /// <inheritdoc/>
    public static ExtendedDouble operator +(double a, ExtendedDouble b) => Of(a) + b;

    /// <inheritdoc/>
    public static ExtendedDouble operator -(ExtendedDouble a, ExtendedDouble b) => a + (-b);

    /// <inheritdoc/>
    public static ExtendedDouble operator -(ExtendedDouble a, double b) => a + Of(-b);

    /// <inheritdoc/>
    public static ExtendedDouble operator -(double a, ExtendedDouble b) => Of(a) + (-b);

    /// <inheritdoc/>
    public static ExtendedDouble operator -(ExtendedDouble a) => new(-a.Mantissa, a.Exponent);

    /// <inheritdoc/>
    public static ExtendedDouble operator *(ExtendedDouble a, ExtendedDouble b) =>
        new(a.Mantissa * b.Mantissa, a.Exponent + b.Exponent);

    /// <inheritdoc/>
    public static ExtendedDouble operator *(ExtendedDouble a, double b) => a * Of(b);

    /// <inheritdoc/>
    public static ExtendedDouble operator *(double a, ExtendedDouble b) => Of(a) * b;

    /// <inheritdoc/>
    public static ExtendedDouble operator /(ExtendedDouble a, ExtendedDouble b) =>
        new(a.Mantissa / b.Mantissa, a.Exponent - b.Exponent);

    /// <inheritdoc/>
    public static ExtendedDouble operator /(ExtendedDouble a, double b) => a / Of(b);

    /// <inheritdoc/>
    public static ExtendedDouble operator /(double a, ExtendedDouble b) => Of(a) / b;

    /// <summary>Whether two values are the same mantissa and exponent.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>Whether they are equal.</returns>
    public static bool operator ==(ExtendedDouble left, ExtendedDouble right) => left.Equals(right);

    /// <summary>Whether two values differ in mantissa or exponent.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>Whether they differ.</returns>
    public static bool operator !=(ExtendedDouble left, ExtendedDouble right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(ExtendedDouble other) => Mantissa.Equals(other.Mantissa) && Exponent == other.Exponent;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ExtendedDouble other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Mantissa, Exponent);
}
