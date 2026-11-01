// SPDX-FileCopyrightText: Copyright (C) 2010 Hachemi Benyahia
// SPDX-FileCopyrightText: Copyright (C) 2010 DeriveXperts SAS
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/claytoncopularng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A random-number generator of Clayton copula draws, by conditional inversion of uniform draws: QuantLib's
/// <c>ClaytonCopulaRng</c>.
/// </summary>
/// <remarks>
/// <para>
/// A working type, as the generator it copies at construction: each draw changes it, and it is not thread-safe.
/// </para>
/// <para>
/// Below |θ| 1e-2 the conditional inverse u₂ = (u₁^(−θ) (v₂^(−θ/(θ+1)) − 1) + 1)^(−1/θ) is drawn in its
/// <c>log1p</c>/<c>expm1</c> form, exp(−log1p(u₁^(−θ) expm1(−θ/(θ+1) · ln v₂))/θ), where QuantLib's cancels: 5e-5 off at
/// θ 1e-12 and 1.8e-7 at 1e-10; at and above 1e-2, QuantLib's formula (the S-xrt plan's XRR5 (xxxiv);
/// <c>Divergences/CopulaRngNearIndependenceTests.cs</c>).
/// </para>
/// </remarks>
/// <typeparam name="TRng">The uniform generator.</typeparam>
[Experimental("NQL0001")]
[WorkingType]
public sealed class ClaytonCopulaRng<TRng>
    where TRng : class, IRng
{
    private readonly double theta;
    private readonly IRng uniformGenerator;

    /// <summary>Initializes a new instance of the <see cref="ClaytonCopulaRng{TRng}"/> class.</summary>
    /// <param name="uniformGenerator">The uniform generator, which it copies.</param>
    /// <param name="theta">The parameter θ, at least −1, not 0.</param>
    /// <exception cref="ArgumentException"><paramref name="theta"/> is outside its range, with QuantLib's texts.</exception>
    public ClaytonCopulaRng(TRng uniformGenerator, double theta)
    {
        ArgumentNullException.ThrowIfNull(uniformGenerator);
        if (!(theta >= -1.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be greater or equal to -1"),
                nameof(theta));
        }

        if (theta == 0.0)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be different from 0"),
                nameof(theta));
        }

        this.theta = theta;
        this.uniformGenerator = uniformGenerator.Clone();
    }

    private ClaytonCopulaRng(ClaytonCopulaRng<TRng> other)
    {
        theta = other.theta;
        uniformGenerator = other.uniformGenerator.Clone();
    }

    /// <summary>Draws the next pair (u₁, u₂), changing the generator (QuantLib's <c>next()</c>).</summary>
    /// <returns>The pair, weighted by the product of the two uniform draws' weights.</returns>
    public Sample<ImmutableArray<double>> Next()
    {
        Sample<double> v1 = uniformGenerator.Next();
        Sample<double> v2 = uniformGenerator.Next();
        double u1 = v1.Value;
        double u2;
        if (Math.Abs(theta) < 1e-2)
        {
            double inner = Math.Pow(v1.Value, -theta) * Expm1Functions.StdExpm1(-theta / (theta + 1.0) * Math.Log(v2.Value));
            u2 = Math.Exp(-StdLog1p(inner) / theta);
        }
        else
        {
            u2 = Math.Pow((Math.Pow(v1.Value, -theta) * (Math.Pow(v2.Value, -theta / (theta + 1.0)) - 1.0)) + 1.0, -1.0 / theta);
        }

        return new Sample<ImmutableArray<double>>([u1, u2], v1.Weight * v2.Weight);
    }

    /// <summary>Copies the generator deeply: the copy's next draw equals this generator's.</summary>
    /// <returns>The copy.</returns>
    public ClaytonCopulaRng<TRng> Clone() => new(this);

    // std::log1p (§2's row for a C runtime function .NET lacks): Goldberg's, as Log1pFunctions writes it.
    private static double StdLog1p(double x)
    {
        double u = 1.0 + x;
        if (u == 1.0)
        {
            return x;
        }

        if (double.IsPositiveInfinity(u))
        {
            return Math.Log(u);
        }

        return Math.Log(u) * x / (u - 1.0);
    }
}
