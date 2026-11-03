// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Kyrylo Protsenko
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/gaussian1dmodel.hpp ql/models/shortrate/onefactormodels/gaussian1dmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// What a Gaussian1d model computes during one call and reads again: the underlying swaps QuantLib's model keeps in
/// <c>swapCache_</c>, and the integrals of GSR's process core.
/// </summary>
/// <remarks>
/// A working type (the playbook's Gaussian1d row): each engine call builds one and passes it through the model's
/// internal members, and each public member builds its own, so a price computes each term once, as QuantLib's first
/// price does, and nothing outlives the call. QuantLib keeps both in the model for its life, flushing the integrals at
/// every parameter change.
/// </remarks>
[WorkingType]
internal sealed class Gaussian1dMemo
{
    private readonly Dictionary<SwapKey, VanillaSwap> swaps = [];
    private GsrProcessCore? gsrCore;
    private GsrProcessCoreMemo? gsrMemo;

    /// <summary>
    /// The underlying swap of a swap index cloned to a tenor at a fixing date, built once per call: QuantLib's
    /// <c>Gaussian1dModel::underlyingSwap</c>, keyed as QuantLib's cache is, by the index's name, the fixing and the
    /// tenor.
    /// </summary>
    /// <param name="index">The swap index.</param>
    /// <param name="expiry">The fixing date.</param>
    /// <param name="tenor">The tenor.</param>
    /// <param name="context">The evaluation context the swap is built under.</param>
    /// <returns>The swap.</returns>
    public VanillaSwap UnderlyingSwap(SwapIndex index, Date expiry, Period tenor, EvaluationContext context)
    {
        var key = new SwapKey(index.Name, expiry, tenor);
        if (!swaps.TryGetValue(key, out VanillaSwap? underlying))
        {
            underlying = index.Clone(tenor).UnderlyingSwap(expiry, context);
            swaps.Add(key, underlying);
        }

        return underlying;
    }

    /// <summary>The integrals' memo for a GSR process core, one per core the call reads.</summary>
    /// <param name="core">The core.</param>
    /// <returns>The core's memo for this call.</returns>
    public GsrProcessCoreMemo For(GsrProcessCore core)
    {
        if (!ReferenceEquals(core, gsrCore))
        {
            // A call reads one model; another core, as a copy at another numeraire time, starts a memo of its own.
            gsrCore = core;
            gsrMemo = new GsrProcessCoreMemo();
        }

        return gsrMemo!;
    }

    private readonly record struct SwapKey(string IndexName, Date Fixing, Period Tenor);
}
