// SPDX-FileCopyrightText: Copyright (C) 2003, 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/inversecumulativersg.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's random sequence generator concept, the <c>USG</c> or <c>RSG</c> its templates take
/// (<c>InverseCumulativeRsg</c>, <c>PathGenerator</c>): a generator of sequences of a fixed dimension, uniform or
/// Gaussian, pseudo-random or low-discrepancy.
/// </summary>
/// <remarks>
/// A generator is a mutable working type: each draw changes it, and it is not thread-safe. A draw's sample is a view
/// of the generator's buffer, valid until its next draw, which changes it; a caller that keeps one copies it.
/// Construction from a seed in generic code is <see cref="ISeedableRsg{TSelf}"/>'s.
/// </remarks>
public interface IRsg
{
    /// <summary>Gets the dimension of each sequence.</summary>
    int Dimension { get; }

    /// <summary>Draws the next sequence, changing the generator (QuantLib's <c>nextSequence()</c>).</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight.</returns>
    Sample<ReadOnlyMemory<double>> NextSequence();

    /// <summary>Gets the sequence the last draw gave (QuantLib's <c>lastSequence()</c>).</summary>
    /// <returns>The sequence, a view of the generator's buffer valid until its next draw, and its weight.</returns>
    Sample<ReadOnlyMemory<double>> LastSequence();

    /// <summary>
    /// Copies the generator deeply, its state and its buffers: the copy's next draw equals this generator's, into its
    /// own buffer.
    /// </summary>
    /// <returns>The copy.</returns>
    IRsg Clone();
}
