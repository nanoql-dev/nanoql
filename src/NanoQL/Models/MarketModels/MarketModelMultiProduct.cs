// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/multiproduct.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A market-model product, or several on the same rate times: the information of its term sheet, and, for a callable
/// product, its exercise strategy. At each step of an evolution it generates the cash flows of that step for the
/// state of the curve.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)): a product is a state machine over a path, <see cref="Reset"/> putting
/// it at the path's start and <see cref="NextTimeStep"/> moving it a step, so it isn't thread-safe; <see cref="Clone"/>
/// copies it deeply, its state included, as QuantLib's <c>clone</c>. A holder of products (a composite, an engine)
/// holds its own clones, so the caller's object is never stepped by another.
/// </para>
/// <para>
/// <see cref="NextTimeStep"/> writes into buffers the caller sizes once (MMPR1 (iii)): a count per product and, per
/// product, <see cref="MaxNumberOfCashFlowsPerProductPerStep"/> cash flows, so a step allocates nothing.
/// </para>
/// <para>An extension point (U13).</para>
/// </remarks>
[WorkingType]
public abstract class MarketModelMultiProduct
{
    /// <summary>Gets the product's evolution: its rate times and the times it steps at.</summary>
    /// <exception cref="NanoQLException">A composite isn't finalized.</exception>
    public abstract EvolutionDescription Evolution { get; }

    /// <summary>Gets the number of products.</summary>
    public abstract int NumberOfProducts { get; }

    /// <summary>Gets the most cash flows a product generates at a step.</summary>
    public abstract int MaxNumberOfCashFlowsPerProductPerStep { get; }

    /// <summary>The numeraires the product suggests, one a step.</summary>
    /// <returns>The numeraires' indices.</returns>
    public abstract ImmutableArray<int> SuggestedNumeraires();

    /// <summary>The times the product can pay at, which its cash flows' time indices index.</summary>
    /// <returns>The times.</returns>
    public abstract ImmutableArray<double> PossibleCashFlowTimes();

    /// <summary>Puts the product at the start of a path.</summary>
    public abstract void Reset();

    /// <summary>
    /// Moves the product a step: writes each product's number of cash flows at this step and the cash flows.
    /// </summary>
    /// <param name="currentState">The state of the curve at the step's end.</param>
    /// <param name="numberCashFlowsThisStep">Each product's number of cash flows, written; one entry a product.</param>
    /// <param name="cashFlowsGenerated">
    /// Each product's cash flows, written from the first; <see cref="NumberOfProducts"/> rows of at least
    /// <see cref="MaxNumberOfCashFlowsPerProductPerStep"/>.
    /// </param>
    /// <returns>Whether the path is finished for the product.</returns>
    public abstract bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelMultiProductCashFlow[][] cashFlowsGenerated);

    /// <summary>A deep copy of the product, its state included.</summary>
    /// <returns>A new product.</returns>
    public abstract MarketModelMultiProduct Clone();
}
