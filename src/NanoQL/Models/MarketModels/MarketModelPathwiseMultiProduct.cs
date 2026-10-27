// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisemultiproduct.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A market-model product, or several on the same rate times, that also gives each cash flow's derivatives with
/// respect to each forward rate, for the pathwise Greeks: <see cref="MarketModelMultiProduct"/>'s contract with
/// <see cref="MarketModelPathwiseMultiProductCashFlow"/> cash flows.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)): <see cref="Reset"/> and <see cref="NextTimeStep"/> move it along a
/// path, and <see cref="Clone"/> copies it deeply. <see cref="NextTimeStep"/> writes into buffers the caller sizes
/// once: a count per product and, per product, <see cref="MaxNumberOfCashFlowsPerProductPerStep"/> cash flows, each of
/// 1 + the number of rates amounts.
/// </para>
/// <para>An extension point (U13).</para>
/// </remarks>
public abstract class MarketModelPathwiseMultiProduct
{
    /// <summary>Gets the product's evolution: its rate times and the times it steps at.</summary>
    public abstract EvolutionDescription Evolution { get; }

    /// <summary>Gets the number of products.</summary>
    public abstract int NumberOfProducts { get; }

    /// <summary>Gets the most cash flows a product generates at a step.</summary>
    public abstract int MaxNumberOfCashFlowsPerProductPerStep { get; }

    /// <summary>
    /// Gets a value indicating whether the cash flows are already deflated (in units of the money-market numeraire), so
    /// the engine doesn't discount them.
    /// </summary>
    public abstract bool AlreadyDeflated { get; }

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
    /// <see cref="MaxNumberOfCashFlowsPerProductPerStep"/>, each of 1 + the number of rates amounts.
    /// </param>
    /// <returns>Whether the path is finished for the product.</returns>
    public abstract bool NextTimeStep(
        CurveState currentState,
        Span<int> numberCashFlowsThisStep,
        MarketModelPathwiseMultiProductCashFlow[][] cashFlowsGenerated);

    /// <summary>A deep copy of the product, its state included.</summary>
    /// <returns>A new product.</returns>
    public abstract MarketModelPathwiseMultiProduct Clone();
}
