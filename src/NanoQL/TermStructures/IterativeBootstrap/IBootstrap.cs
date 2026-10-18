// SPDX-FileCopyrightText: Copyright (C) 2008, 2011, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/iterativebootstrap.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// A bootstrap: QuantLib's <c>Bootstrap</c> template parameter (<c>IterativeBootstrap</c>, <c>GlobalBootstrap</c>), as
/// a configuration struct whose <see cref="Calculate"/> fills a curve's working state.
/// </summary>
/// <remarks>
/// A bootstrap is a <see langword="readonly"/> struct holding QuantLib's constructor parameters, whose zero value is
/// QuantLib's default construction. QuantLib's bootstrap holds a pointer to its curve and reads and writes the curve's
/// members as its friend; here the curve hands it a <see cref="BootstrapState{TTermStructure}"/>, the working state of
/// the curve under construction, which the curve freezes into its nodes when <see cref="Calculate"/> returns. An
/// extension point: a bootstrap of one's own implements it.
/// </remarks>
public interface IBootstrap
{
    /// <summary>
    /// Fills the working state: the dates, times and data of the curve's nodes, its interpolation and maximum date, so
    /// that each helper reprices its quote.
    /// </summary>
    /// <typeparam name="TTermStructure">The kind of term structure bootstrapped.</typeparam>
    /// <typeparam name="TTraits">The curve's traits.</typeparam>
    /// <typeparam name="TInterpolator">The curve's interpolator.</typeparam>
    /// <param name="state">The working state of the curve under construction.</param>
    /// <param name="interpolator">The curve's interpolator.</param>
    /// <param name="context">The evaluation context the helpers are priced in.</param>
    void Calculate<TTermStructure, TTraits, TInterpolator>(
        BootstrapState<TTermStructure> state, TInterpolator interpolator, EvaluationContext context)
        where TTermStructure : TermStructure
        where TTraits : struct, IBootstrapTraits<TTermStructure>
        where TInterpolator : struct, IInterpolator;
}
