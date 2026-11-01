// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/boxmullergaussianrng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The Gaussian random-number generator by the well-known Box-Muller transformation: normally distributed deviates
/// with average 0.0 and standard deviation 1.0, from uniform deviates in (0, 1) supplied by
/// <typeparamref name="TRng"/>.
/// </summary>
/// <remarks>
/// A mutable working type: each draw changes it, and it is not thread-safe. It copies the generator it is given, so the
/// caller's later draws change nothing in it. Each pair of uniform draws gives two deviates, returned one after the
/// other with the pair's weight.
/// </remarks>
/// <typeparam name="TRng">The uniform generator, of deviates in (0, 1).</typeparam>
[WorkingType]
public class BoxMullerGaussianRng<TRng> : IRng
    where TRng : class, IRng
{
    private readonly TRng uniformGenerator;
    private bool returnFirst = true;
    private double firstValue;
    private double secondValue;
    private double firstWeight;
    private double secondWeight;
    private double weight;

    /// <summary>Initializes a new instance of the <see cref="BoxMullerGaussianRng{TRng}"/> class.</summary>
    /// <param name="uniformGenerator">The uniform generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformGenerator"/> is null.</exception>
    public BoxMullerGaussianRng(TRng uniformGenerator)
    {
        ArgumentNullException.ThrowIfNull(uniformGenerator);
        this.uniformGenerator = (TRng)uniformGenerator.Clone();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BoxMullerGaussianRng{TRng}"/> class from a copy of another, for a
    /// subclass's <see cref="Clone"/>.
    /// </summary>
    /// <param name="source">The generator to copy, deeply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    protected BoxMullerGaussianRng(BoxMullerGaussianRng<TRng> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        uniformGenerator = (TRng)source.uniformGenerator.Clone();
        returnFirst = source.returnFirst;
        firstValue = source.firstValue;
        secondValue = source.secondValue;
        firstWeight = source.firstWeight;
        secondWeight = source.secondWeight;
        weight = source.weight;
    }

    /// <summary>Draws a sample from a Gaussian distribution.</summary>
    /// <returns>The sample, with the weight of the uniform pair it comes from.</returns>
    public Sample<double> Next()
    {
        if (returnFirst)
        {
            double x1, x2, r, ratio;
            do
            {
                Sample<double> s1 = uniformGenerator.Next();
                x1 = (s1.Value * 2.0) - 1.0;
                firstWeight = s1.Weight;
                Sample<double> s2 = uniformGenerator.Next();
                x2 = (s2.Value * 2.0) - 1.0;
                secondWeight = s2.Weight;
                r = (x1 * x1) + (x2 * x2);
            }
            while (r >= 1.0 || r == 0.0);

            ratio = Math.Sqrt(-2.0 * Math.Log(r) / r);
            firstValue = x1 * ratio;
            secondValue = x2 * ratio;
            weight = firstWeight * secondWeight;
            returnFirst = false;
            return new(firstValue, weight);
        }
        else
        {
            returnFirst = true;
            return new(secondValue, weight);
        }
    }

    /// <summary>
    /// Copies the generator deeply, its uniform generator included: the copy's next draw equals this one's, and the
    /// two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public virtual BoxMullerGaussianRng<TRng> Clone() => new(this);

    /// <inheritdoc/>
    IRng IRng.Clone() => Clone();
}
