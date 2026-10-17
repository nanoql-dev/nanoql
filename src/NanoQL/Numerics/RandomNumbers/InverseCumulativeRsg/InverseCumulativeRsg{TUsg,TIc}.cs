// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/inversecumulativersg.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics;
using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// The inverse cumulative random sequence generator: it uses a sequence of uniform deviates in (0, 1), supplied by
/// <typeparamref name="TUsg"/>, as the source of cumulative distribution values, then the inverse cumulative
/// distribution <typeparamref name="TIc"/> to calculate the distribution deviates.
/// </summary>
/// <remarks>
/// A mutable working type: each draw changes it, and it is not thread-safe. A draw returns a view of its buffer, valid
/// until its next draw. It copies the uniform sequence generator it is given, so the caller's later draws change
/// nothing in it.
/// </remarks>
/// <typeparam name="TUsg">The uniform sequence generator, of deviates in (0, 1).</typeparam>
/// <typeparam name="TIc">The inverse cumulative distribution.</typeparam>
public class InverseCumulativeRsg<TUsg, TIc> : IRsg
    where TUsg : class, IRsg
    where TIc : struct, IInverseCumulative
{
    private readonly TUsg uniformSequenceGenerator;
    private readonly TIc inverseCumulative;
    private readonly double[] x;
    private double weight;

    /// <summary>
    /// Initializes a new instance of the <see cref="InverseCumulativeRsg{TUsg, TIc}"/> class with the inverse
    /// cumulative's zero value, QuantLib's default construction.
    /// </summary>
    /// <param name="uniformSequenceGenerator">The uniform sequence generator, which the generator copies.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformSequenceGenerator"/> is null.</exception>
    public InverseCumulativeRsg(TUsg uniformSequenceGenerator)
        : this(Copy(uniformSequenceGenerator), default, adopt: true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InverseCumulativeRsg{TUsg, TIc}"/> class.</summary>
    /// <param name="uniformSequenceGenerator">The uniform sequence generator, which the generator copies.</param>
    /// <param name="inverseCumulative">The inverse cumulative distribution.</param>
    /// <exception cref="ArgumentNullException"><paramref name="uniformSequenceGenerator"/> is null.</exception>
    public InverseCumulativeRsg(TUsg uniformSequenceGenerator, TIc inverseCumulative)
        : this(Copy(uniformSequenceGenerator), inverseCumulative, adopt: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InverseCumulativeRsg{TUsg, TIc}"/> class from a copy of another,
    /// for a subclass's <see cref="Clone"/>.
    /// </summary>
    /// <param name="source">The generator to copy, deeply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    protected InverseCumulativeRsg(InverseCumulativeRsg<TUsg, TIc> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        uniformSequenceGenerator = (TUsg)source.uniformSequenceGenerator.Clone();
        inverseCumulative = source.inverseCumulative;
        x = (double[])source.x.Clone();
        weight = source.weight;
    }

    // The traits' path: QuantLib moves the generator it has just built, and so does this, with no copy. adopt is
    // always true; it only tells this constructor from the copying one.
    internal InverseCumulativeRsg(TUsg uniformSequenceGenerator, TIc inverseCumulative, bool adopt)
    {
        Debug.Assert(adopt, "the adopting constructor takes the generator without a copy");
        this.uniformSequenceGenerator = uniformSequenceGenerator;
        this.inverseCumulative = inverseCumulative;
        x = new double[uniformSequenceGenerator.Dimension];
        weight = 1.0;
    }

    /// <summary>Gets the dimension of each sequence, the uniform sequence generator's.</summary>
    public int Dimension => x.Length;

    /// <summary>Draws the next sample from the inverse cumulative distribution.</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, with the uniform draw's weight.</returns>
    /// <exception cref="NanoQLException">
    /// The uniform sequence generator drew fewer values than its dimension, which QuantLib reads past.
    /// </exception>
    public Sample<ReadOnlyMemory<double>> NextSequence()
    {
        Sample<ReadOnlyMemory<double>> sample = uniformSequenceGenerator.NextSequence();
        ReadOnlySpan<double> u = sample.Value.Span;
        if (u.Length < x.Length)
        {
            // QuantLib reads the uniform sample at every index below its dimension, past the end of a shorter one.
            throw new NanoQLException(
                $"the uniform sequence generator drew {u.Length} values, fewer than the dimension ({x.Length})");
        }

        weight = sample.Weight;
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = inverseCumulative.Evaluate(u[i]);
        }

        return new(x, weight);
    }

    /// <summary>Gets the sequence the last draw gave.</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight.</returns>
    public Sample<ReadOnlyMemory<double>> LastSequence() => new(x, weight);

    /// <summary>
    /// Copies the generator deeply, its uniform sequence generator included: the copy's next draw equals this one's,
    /// into its own buffer, and the two change apart.
    /// </summary>
    /// <returns>The copy.</returns>
    public virtual InverseCumulativeRsg<TUsg, TIc> Clone() => new(this);

    /// <inheritdoc/>
    IRsg IRsg.Clone() => Clone();

    private static TUsg Copy(TUsg uniformSequenceGenerator)
    {
        ArgumentNullException.ThrowIfNull(uniformSequenceGenerator);
        return (TUsg)uniformSequenceGenerator.Clone();
    }
}
