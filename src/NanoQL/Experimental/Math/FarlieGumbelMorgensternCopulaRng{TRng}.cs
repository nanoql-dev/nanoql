// SPDX-FileCopyrightText: Copyright (C) 2010 Hachemi Benyahia
// SPDX-FileCopyrightText: Copyright (C) 2010 DeriveXperts SAS
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/farliegumbelmorgensterncopularng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A random-number generator of Farlie-Gumbel-Morgenstern copula draws, by conditional inversion of uniform draws:
/// QuantLib's <c>FarlieGumbelMorgensternCopulaRng</c>.
/// </summary>
/// <remarks>
/// A working type, as the generator it copies at construction: each draw changes it, and it is not thread-safe.
/// </remarks>
/// <typeparam name="TRng">The uniform generator.</typeparam>
[Experimental("NQL0001")]
[WorkingType]
public sealed class FarlieGumbelMorgensternCopulaRng<TRng>
    where TRng : class, IRng
{
    private readonly double theta;
    private readonly IRng uniformGenerator;

    /// <summary>Initializes a new instance of the <see cref="FarlieGumbelMorgensternCopulaRng{TRng}"/> class.</summary>
    /// <param name="uniformGenerator">The uniform generator, which it copies.</param>
    /// <param name="theta">The parameter θ, in [−1, 1].</param>
    /// <exception cref="ArgumentException"><paramref name="theta"/> is outside [−1, 1], with QuantLib's text.</exception>
    public FarlieGumbelMorgensternCopulaRng(TRng uniformGenerator, double theta)
    {
        ArgumentNullException.ThrowIfNull(uniformGenerator);
        if (!(theta >= -1.0 && theta <= 1.00))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"theta ({theta}) must be in [-1,1]"),
                nameof(theta));
        }

        this.theta = theta;
        this.uniformGenerator = uniformGenerator.Clone();
    }

    private FarlieGumbelMorgensternCopulaRng(FarlieGumbelMorgensternCopulaRng<TRng> other)
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
        double a = theta * ((2.0 * u1) - 1.0);
        double b = Math.Pow(1.0 - (theta * ((2.0 * u1) - 1.0)), 2.0) + (4.0 * theta * v2.Value * ((2.0 * u1) - 1.0));
        double u2 = 2.0 * v2.Value / (Math.Sqrt(b) + 1.0 - a);
        return new Sample<ImmutableArray<double>>([u1, u2], v1.Weight * v2.Weight);
    }

    /// <summary>Copies the generator deeply: the copy's next draw equals this generator's.</summary>
    /// <returns>The copy.</returns>
    public FarlieGumbelMorgensternCopulaRng<TRng> Clone() => new(this);
}
