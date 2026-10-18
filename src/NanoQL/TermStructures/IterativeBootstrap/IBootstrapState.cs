// SPDX-FileCopyrightText: Copyright (C) 2008, 2011, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/iterativebootstrap.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.TermStructures;

/// <summary>
/// The working state of a curve under construction, seen without its term structure's type: what a global bootstrap's
/// penalties and initial-guess function read (<see cref="BootstrapState{TTermStructure}"/> implements it).
/// </summary>
/// <remarks>
/// QuantLib's penalties and guess functions reach the curve's data and the helpers through the user's own pointers,
/// which the bootstrap has linked to the curve. Here they receive the state, whose helpers are the copies bound to the
/// curve under construction, so they price against the curve as QuantLib's do.
/// </remarks>
public interface IBootstrapState
{
    /// <summary>Gets the times of the curve's nodes, from the reference date.</summary>
    ReadOnlySpan<double> Times { get; }

    /// <summary>Gets the curve's data at its nodes, as the bootstrap has set them so far.</summary>
    ReadOnlySpan<double> Data { get; }

    /// <summary>Gets the curve's helpers, those alive at the curve's first date bound to it.</summary>
    IReadOnlyList<IBootstrapHelper> Instruments { get; }

    /// <summary>Gets the evaluation context the curve is built in.</summary>
    EvaluationContext Context { get; }
}
