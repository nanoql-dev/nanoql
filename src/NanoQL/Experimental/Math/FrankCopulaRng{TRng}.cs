// SPDX-FileCopyrightText: Copyright (C) 2010 Hachemi Benyahia
// SPDX-FileCopyrightText: Copyright (C) 2010 DeriveXperts SAS
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/frankcopularng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A random-number generator of Frank copula draws, by conditional inversion of uniform draws: QuantLib's
/// <c>FrankCopulaRng</c>.
/// </summary>
/// <remarks>
/// <para>
/// A working type, as the generator it copies at construction: each draw changes it, and it is not thread-safe.
/// </para>
/// <para>
/// Below |θ| 1e-2 the conditional inverse u₂ = −ln(1 + v₂(1 − e^(−θ))/(v₂(e^(−θu₁) − 1) − e^(−θu₁)))/θ is drawn in its
/// <c>log1p</c>/<c>expm1</c> form, −log1p(−v₂ expm1(−θ)/((v₂ − 1) expm1(−θu₁) − 1))/θ, where QuantLib's cancels:
/// 5.8e-8 off at θ 1e-10; at and above 1e-2, QuantLib's formula (the S-xrt plan's XRR5 (xxxiv);
/// <c>Divergences/CopulaRngNearIndependenceTests.cs</c>).
/// </para>
/// </remarks>
/// <typeparam name="TRng">The uniform generator.</typeparam>
[Experimental("NQL0001")]
[WorkingType]
public sealed class FrankCopulaRng<TRng>
    where TRng : class, IRng
{
    private readonly double theta;
    private readonly IRng uniformGenerator;

    /// <summary>Initializes a new instance of the <see cref="FrankCopulaRng{TRng}"/> class.</summary>
    /// <param name="uniformGenerator">The uniform generator, which it copies.</param>
    /// <param name="theta">The parameter θ, not 0.</param>
    /// <exception cref="ArgumentException"><paramref name="theta"/> is 0, with QuantLib's text.</exception>
    public FrankCopulaRng(TRng uniformGenerator, double theta)
    {
        ArgumentNullException.ThrowIfNull(uniformGenerator);
        if (theta == 0.0)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be different from 0"),
                nameof(theta));
        }

        this.theta = theta;
        this.uniformGenerator = uniformGenerator.Clone();
    }

    private FrankCopulaRng(FrankCopulaRng<TRng> other)
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
            double ratio = v2.Value * -Expm1Functions.StdExpm1(-theta)
                / (((v2.Value - 1.0) * Expm1Functions.StdExpm1(-theta * v1.Value)) - 1.0);
            u2 = -StdLog1p(ratio) / theta;
        }
        else
        {
            u2 = (-1.0 / theta) * Math.Log(1.0 + (v2.Value * (1.0 - Math.Exp(-theta))
                / ((v2.Value * (Math.Exp(-theta * v1.Value) - 1.0)) - Math.Exp(-theta * v1.Value))));
        }

        return new Sample<ImmutableArray<double>>([u1, u2], v1.Weight * v2.Weight);
    }

    /// <summary>Copies the generator deeply: the copy's next draw equals this generator's.</summary>
    /// <returns>The copy.</returns>
    public FrankCopulaRng<TRng> Clone() => new(this);

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
