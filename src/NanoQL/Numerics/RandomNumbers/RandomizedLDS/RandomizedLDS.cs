// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/randomizedlds.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's seed constructors of <see cref="RandomizedLDS{TLds, TPrng}"/>, which build its generators from seeds:
/// C++ instantiates them only where a caller uses them, so each is a generic method here, with its own constraints. Its
/// pseudo-random generator is QuantLib's default, a <see cref="RandomSequenceGenerator{TRng}"/>.
/// </summary>
public static class RandomizedLDS
{
    /// <summary>
    /// Builds a randomized sequence over a low-discrepancy sequence and a pseudo-random sequence generator built from a
    /// seed (QuantLib's <c>RandomizedLDS(ldsg)</c>, whose pseudo-random generator QuantLib seeds from the clock).
    /// </summary>
    /// <typeparam name="TLds">The low-discrepancy sequence generator.</typeparam>
    /// <typeparam name="TRng">The pseudo-random generator, which can be built from a seed.</typeparam>
    /// <param name="ldsg">The low-discrepancy sequence generator, which the generator copies.</param>
    /// <param name="seed">The pseudo-random generator's seed, in [1, 4294967295].</param>
    /// <returns>The randomized sequence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ldsg"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is outside [1, 4294967295].</exception>
    public static RandomizedLDS<TLds, RandomSequenceGenerator<TRng>> Create<TLds, TRng>(TLds ldsg, long seed)
        where TLds : class, IRsg
        where TRng : class, ISeedableRng<TRng>
    {
        ArgumentNullException.ThrowIfNull(ldsg);
        return new(ldsg, RandomSequenceGenerator.Create<TRng>(ldsg.Dimension, seed));
    }

    /// <summary>
    /// Builds a randomized sequence over generators built from seeds (QuantLib's
    /// <c>RandomizedLDS(dimensionality, ldsSeed, prsSeed)</c>, whose seeds QuantLib defaults to the clock's).
    /// </summary>
    /// <typeparam name="TLds">The low-discrepancy sequence generator, which can be built from a seed.</typeparam>
    /// <typeparam name="TRng">The pseudo-random generator, which can be built from a seed.</typeparam>
    /// <param name="dimensionality">The dimension of each sequence.</param>
    /// <param name="ldsSeed">The low-discrepancy sequence generator's seed, in [1, 4294967295].</param>
    /// <param name="prsSeed">The pseudo-random generator's seed, in [1, 4294967295].</param>
    /// <returns>The randomized sequence.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="dimensionality"/> is out of the generators' range, or a seed is outside [1, 4294967295].
    /// </exception>
    public static RandomizedLDS<TLds, RandomSequenceGenerator<TRng>> Create<TLds, TRng>(
        int dimensionality, long ldsSeed, long prsSeed)
        where TLds : class, ISeedableRsg<TLds>
        where TRng : class, ISeedableRng<TRng>
    {
        return new(TLds.Create(dimensionality, ldsSeed), RandomSequenceGenerator.Create<TRng>(dimensionality, prsSeed));
    }
}
