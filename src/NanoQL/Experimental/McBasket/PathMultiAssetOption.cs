// SPDX-FileCopyrightText: Copyright (C) 2008 Andrea Odetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/mcbasket/pathmultiassetoption.hpp ql/experimental/mcbasket/pathmultiassetoption.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>An option on several assets' paths, its payoff a <see cref="Experimental.PathPayoff"/> at fixing dates.</summary>
/// <remarks>
/// An extension point, as in QuantLib: a product derives it and gives its payoff and fixing dates. Its engines return an
/// <see cref="InstrumentResults"/>, QuantLib's results class adding nothing to the instrument's.
/// </remarks>
[Experimental("NQL0001")]
public abstract class PathMultiAssetOption : Instrument
{
    /// <summary>Gets the payoff on the paths.</summary>
    public abstract PathPayoff PathPayoff { get; }

    /// <summary>Gets the fixing dates.</summary>
    public abstract ImmutableArray<Date> FixingDates { get; }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">There is no fixing date.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ImmutableArray<Date> fixingDates = FixingDates;
        if (fixingDates.IsDefaultOrEmpty)
        {
            throw new NanoQLException("no dates given");
        }

        return new SimpleEvent(fixingDates[^1]).HasOccurred(context);
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">There is no payoff ("no payoff given") or no fixing date ("no dates
    /// given").</exception>
    public override void Validate()
    {
        if (PathPayoff is null)
        {
            throw new NanoQLException("no payoff given");
        }

        if (FixingDates.IsDefaultOrEmpty)
        {
            throw new NanoQLException("no dates given");
        }
    }
}
