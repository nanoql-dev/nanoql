// SPDX-FileCopyrightText: Copyright (C) 2009 Andrea Odetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/mcbasket/adaptedpathpayoff.hpp ql/experimental/mcbasket/adaptedpathpayoff.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// A path payoff that is an adapted function: each payment and exercise reads only the fixings up to its own time, which
/// its <see cref="AdaptedPathPayoffValuationData"/> enforces.
/// </summary>
/// <remarks>An extension point, as in QuantLib: a product derives it and implements <see cref="Evaluate"/>.</remarks>
[Experimental("NQL0001")]
public abstract class AdaptedPathPayoff : PathPayoff
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public override void Value(
        Matrix path,
        ImmutableArray<YieldTermStructure> forwardTermStructures,
        Vector payments,
        Vector exercises,
        Vector[] states)
    {
        var data = new AdaptedPathPayoffValuationData(path, forwardTermStructures, payments, exercises, states);
        Evaluate(data);
    }

    /// <summary>
    /// The payoff, QuantLib's <c>operator()</c>: its payments through
    /// <see cref="AdaptedPathPayoffValuationData.SetPayoffValue"/> and its exercise data through
    /// <see cref="AdaptedPathPayoffValuationData.SetExerciseData"/>.
    /// </summary>
    /// <param name="data">The path's data.</param>
    protected abstract void Evaluate(AdaptedPathPayoffValuationData data);
}
