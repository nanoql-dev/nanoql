// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/randomsequencegenerator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's random-number generator concept, the <c>RNG</c> its templates take (<c>RandomSequenceGenerator</c>,
/// <c>InverseCumulativeRng</c>): a generator of scalar samples, uniform or Gaussian, as QuantLib's are.
/// </summary>
/// <remarks>
/// A generator is a mutable working type: each draw changes it, and it is not thread-safe. A template whose input must
/// be uniform says so in its documentation, as QuantLib's do. Construction from a seed in generic code is
/// <see cref="ISeedableRng{TSelf}"/>'s.
/// </remarks>
[WorkingType]
public interface IRng
{
    /// <summary>Draws the next sample, changing the generator (QuantLib's <c>next()</c>).</summary>
    /// <returns>The sample and its weight.</returns>
    Sample<double> Next();

    /// <summary>
    /// Copies the generator deeply, its state and its buffers: the copy's next draw equals this generator's, into its
    /// own buffer.
    /// </summary>
    /// <returns>The copy.</returns>
    IRng Clone();
}
