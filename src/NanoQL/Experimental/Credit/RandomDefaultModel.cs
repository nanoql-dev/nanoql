// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/randomdefaultmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>The base class of random default models: sequences of random default times, one for each name in the
/// pool.</summary>
/// <remarks>
/// A working type: each <see cref="NextSequence"/> replaces <see cref="Pool"/> by a copy holding the sequence's default
/// times, where QuantLib's sets them on the pool it shares.
/// </remarks>
[Experimental("NQL0001")]
[WorkingType]
public abstract class RandomDefaultModel
{
    /// <summary>Initializes a new instance of the <see cref="RandomDefaultModel"/> class.</summary>
    /// <param name="pool">The pool.</param>
    /// <param name="defaultKeys">The names' default keys, one per name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pool"/> or <paramref name="defaultKeys"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The keys don't number the pool's names ("Incompatible pool and keys
    /// sizes.").</exception>
    protected RandomDefaultModel(Pool pool, IReadOnlyList<DefaultProbKey> defaultKeys)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(defaultKeys);

        // assuming none defaulted this is true.
        if (defaultKeys.Count != pool.Size)
        {
            throw new ArgumentException("Incompatible pool and keys sizes.", nameof(defaultKeys));
        }

        Pool = pool;
        DefaultKeys = [.. defaultKeys];
    }

    /// <summary>Gets the pool, holding the last sequence's default times.</summary>
    public Pool Pool { get; protected set; }

    /// <summary>Gets the names' default keys.</summary>
    protected ImmutableArray<DefaultProbKey> DefaultKeys { get; }

    /// <summary>
    /// Generates a sequence of random default times, one for each name in the pool, and stores them in
    /// <see cref="Pool"/>. Default times beyond a maximum relevant time aren't computed but set to it plus 1, to save
    /// computation time.
    /// </summary>
    /// <param name="tmax">The maximum relevant time.</param>
    public abstract void NextSequence(double tmax = double.MaxValue);

    /// <summary>Restarts the sequences from the first.</summary>
    public abstract void Reset();
}
