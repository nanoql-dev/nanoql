// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/inversecumulativerng.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The inverse cumulative random-number generator: it uses a uniform deviate in (0, 1), supplied by
/// <typeparamref name="TRng"/>, as the source of cumulative distribution values, then the inverse cumulative
/// distribution <typeparamref name="TIc"/> to calculate the distribution deviate.
/// </summary>
/// <remarks>
/// A mutable working type: each draw changes it, and it is not thread-safe. It copies the generator it is given, so
/// the caller's later draws change nothing in it. Its inverse cumulative is <typeparamref name="TIc"/>'s zero value,
/// QuantLib's default construction, which QuantLib's class never assigns.
/// </remarks>
/// <typeparam name="TRng">The uniform generator, of deviates in (0, 1).</typeparam>
/// <typeparam name="TIc">The inverse cumulative distribution.</typeparam>
[WorkingType]
public class InverseCumulativeRng<TRng, TIc> : IRng
    where TRng : class, IRng
    where TIc : struct, IInverseCumulative
{
    private readonly TRng uniformGenerator;

    // QuantLib's ICND_, default-constructed and never assigned.
    private readonly TIc inverseCumulative = default;

    /// <summary>Initializes a new instance of the <see cref="InverseCumulativeRng{TRng, TIc}"/> class.</summary>
    /// <param name="uniformGenerator">The uniform generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformGenerator"/> is null.</exception>
    public InverseCumulativeRng(TRng uniformGenerator)
    {
        ArgumentNullException.ThrowIfNull(uniformGenerator);
        this.uniformGenerator = (TRng)uniformGenerator.Clone();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InverseCumulativeRng{TRng, TIc}"/> class from a copy of another,
    /// for a subclass's <see cref="Clone"/>.
    /// </summary>
    /// <param name="source">The generator to copy, deeply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    protected InverseCumulativeRng(InverseCumulativeRng<TRng, TIc> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        uniformGenerator = (TRng)source.uniformGenerator.Clone();
    }

    /// <summary>Draws a sample from the distribution: the inverse cumulative of the uniform generator's next draw.</summary>
    /// <returns>The sample, with the uniform draw's weight.</returns>
    public Sample<double> Next()
    {
        Sample<double> sample = uniformGenerator.Next();
        return new(inverseCumulative.Evaluate(sample.Value), sample.Weight);
    }

    /// <summary>
    /// Copies the generator deeply, its uniform generator included: the copy's next draw equals this one's, and the
    /// two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public virtual InverseCumulativeRng<TRng, TIc> Clone() => new(this);

    /// <inheritdoc/>
    IRng IRng.Clone() => Clone();
}
