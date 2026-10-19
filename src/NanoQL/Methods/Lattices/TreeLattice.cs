// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/lattices/lattice.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The tree-based lattice-method base class: a lattice that rolls a discretized asset back, with discount, on one or
/// more trees.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's curiously recurring template: a derived class implements <see cref="Size"/>, <see cref="Discount"/>,
/// <see cref="Descendant"/> and <see cref="Probability"/>, and may override <see cref="Stepback"/>, which are called
/// per node and branch. The rollback's inner step is one <see cref="Stepback"/> into the asset's spare buffer
/// (<see cref="DiscretizedAsset.StepValues"/>, <see cref="DiscretizedAsset.CommitStep"/>), then the asset's adjustment;
/// it allocates nothing.
/// </para>
/// <para>
/// <b>State prices.</b> A lattice is immutable once built. The rows of Arrow-Debreu state prices its construction
/// computes, through <see cref="ComputeStatePrices"/>, are stored; <see cref="StatePrices"/> computes any later row on
/// each call from the last stored one, with the same operations in the same order, and keeps it nowhere. A derived
/// class whose constructor calls <see cref="ComputeStatePrices"/> reads its own <see cref="Size"/>,
/// <see cref="Discount"/>, <see cref="Descendant"/> and <see cref="Probability"/> there, so it seals them: C# runs a
/// further override from a constructor before that override's class has been constructed.
/// </para>
/// <para>
/// An extension point, as in QuantLib: the one-dimensional lattices derive from it, as later ports' two-dimensional
/// lattice will; a binomial lattice overrides <see cref="Stepback"/>, a convertible's lattice <see cref="Rollback"/> and
/// <see cref="PartialRollback"/>.
/// </para>
/// </remarks>
public abstract class TreeLattice : Lattice
{
    private readonly int n;

    // The stored rows: 0 through statePricesLimit, written only by ComputeStatePrices while the lattice is built; a
    // copy of the lattice holds the same rows in an array of its own.
    private ImmutableArray<double>[] statePrices;
    private int statePricesLimit;

    /// <summary>Initializes a new instance of the <see cref="TreeLattice"/> class.</summary>
    /// <param name="timeGrid">The lattice's time grid.</param>
    /// <param name="n">The number of branches from each node.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeGrid"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="n"/> is 0.</exception>
    protected TreeLattice(TimeGrid timeGrid, int n)
        : base(timeGrid)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        if (!(n > 0))
        {
            throw new ArgumentException("there is no zeronomial lattice!", nameof(n));
        }

        this.n = n;
        statePrices = new ImmutableArray<double>[Math.Max(timeGrid.Size, 1)];
        statePrices[0] = [1.0];
        statePricesLimit = 0;
    }

    /// <summary>Initializes a new instance of the <see cref="TreeLattice"/> class that shares another's stored state prices.</summary>
    /// <param name="other">The lattice whose grid, branches and stored state prices the new one takes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    /// <remarks>
    /// For a derived lattice's copy that differs from <paramref name="other"/> only in what its later rows depend on,
    /// as a short-rate tree's copy with a spread: the stored rows are the ones QuantLib's cache holds when it is copied.
    /// </remarks>
    protected TreeLattice(TreeLattice other)
        : base(NotNull(other).TimeGrid)
    {
        n = other.n;

        // its own array of the same rows, so rows a copy stores leave the other's unchanged
        statePrices = (ImmutableArray<double>[])other.statePrices.Clone();
        statePricesLimit = other.statePricesLimit;
    }

    /// <summary>The number of nodes at a step.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <returns>The number of nodes.</returns>
    public abstract int Size(int i);

    /// <summary>The discount factor over the step that starts at a node.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <returns>The discount factor.</returns>
    public abstract double Discount(int i, int index);

    /// <summary>The index, at the next step, of a node's descendant along a branch.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <param name="branch">The branch.</param>
    /// <returns>The descendant's index.</returns>
    public abstract int Descendant(int i, int index, int branch);

    /// <summary>The probability of a node's branch.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="index">The node's index at the step.</param>
    /// <param name="branch">The branch.</param>
    /// <returns>The probability.</returns>
    public abstract double Probability(int i, int index, int branch);

    /// <summary>Initializes an asset at a time: its time, the buffers its rollback needs, and its values.</summary>
    /// <param name="asset">The asset, which changes.</param>
    /// <param name="time">The time, on the grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="asset"/> is null.</exception>
    /// <exception cref="NanoQLException"><paramref name="time"/> isn't on the grid (the grid's own refusal).</exception>
    public override void Initialize(DiscretizedAsset asset, double time)
    {
        ArgumentNullException.ThrowIfNull(asset);
        int i = TimeGrid.Index(time);
        asset.Time = time;

        // A rollback from step i passes through every step before it, and on a non-uniform grid an earlier step can be
        // wider than step i: the asset reserves the widest, so its steps allocate nothing.
        int widest = 0;
        for (int k = 0; k <= i; k++)
        {
            int size = Size(k);
            if (size > widest)
            {
                widest = size;
            }
        }

        asset.ReserveCapacity(widest);
        asset.Reset(Size(i));
    }

    /// <summary>Rolls an asset back until the given time, then adjusts it.</summary>
    /// <param name="asset">The asset, which changes.</param>
    /// <param name="to">The time to roll back to, on the grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="asset"/> is null.</exception>
    /// <exception cref="NanoQLException">The asset is already before <paramref name="to"/>, or a time isn't on the grid.</exception>
    public override void Rollback(DiscretizedAsset asset, double to)
    {
        PartialRollback(asset, to);
        asset.AdjustValues();
    }

    /// <summary>Rolls an asset back until the given time, adjusting it at each step but the last.</summary>
    /// <param name="asset">The asset, which changes.</param>
    /// <param name="to">The time to roll back to, on the grid.</param>
    /// <exception cref="ArgumentNullException"><paramref name="asset"/> is null.</exception>
    /// <exception cref="NanoQLException">The asset is already before <paramref name="to"/>, or a time isn't on the grid.</exception>
    public override void PartialRollback(DiscretizedAsset asset, double to)
    {
        ArgumentNullException.ThrowIfNull(asset);
        double from = asset.Time;

        if (ComparisonFunctions.Close(from, to))
        {
            return;
        }

        if (!(from > to))
        {
            throw new NanoQLException($"cannot roll the asset back to{Show(to)} (it is already at t = {Show(from)})");
        }

        int iFrom = TimeGrid.Index(from);
        int iTo = TimeGrid.Index(to);

        for (int i = iFrom - 1; i >= iTo; --i)
        {
            int size = Size(i);
            Span<double> newValues = asset.StepValues(size);
            Stepback(i, asset.Values, newValues);
            asset.CommitStep(size, TimeGrid[i]);

            // skip the very last adjustment
            if (i != iTo)
            {
                asset.AdjustValues();
            }
        }
    }

    /// <summary>Computes an asset's present value from the Arrow-Debreu state prices at its time.</summary>
    /// <param name="asset">The asset.</param>
    /// <returns>The present value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="asset"/> is null.</exception>
    /// <exception cref="NanoQLException">The asset's time isn't on the grid (the grid's own refusal).</exception>
    /// <exception cref="ArgumentException">The asset's values and the state prices at its time differ in size.</exception>
    public override double PresentValue(DiscretizedAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        int i = TimeGrid.Index(asset.Time);
        ReadOnlySpan<double> values = asset.Values;
        ImmutableArray<double> prices = StatePrices(i);

        // QuantLib's DotProduct, its check and its order
        if (!(values.Length == prices.Length))
        {
            throw new ArgumentException(
                $"arrays with different sizes ({values.Length}, {prices.Length}) cannot be multiplied", nameof(asset));
        }

        double result = 0.0;
        for (int j = 0; j < values.Length; j++)
        {
            result += values[j] * prices[j];
        }

        return result;
    }

    /// <summary>The Arrow-Debreu state prices at a step: a stored row, or one computed now from the last stored row.</summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <returns>One state price per node at the step.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is outside the grid's indices.</exception>
    public ImmutableArray<double> StatePrices(int i)
    {
        CheckStep(i, nameof(i));
        if (i <= statePricesLimit)
        {
            return statePrices[i];
        }

        // beyond the stored rows: computed for this call, kept nowhere
        ImmutableArray<double> row = statePrices[statePricesLimit];
        for (int k = statePricesLimit; k < i; k++)
        {
            row = NextStatePrices(k, row);
        }

        return row;
    }

    /// <summary>
    /// Rolls values one step back: for each node, the probability-weighted sum of its descendants' values, then the
    /// node's discount.
    /// </summary>
    /// <param name="i">The step's index on the grid.</param>
    /// <param name="values">The values at step <paramref name="i"/> + 1.</param>
    /// <param name="newValues">The values at step <paramref name="i"/>, written here: <see cref="Size"/>(i) of them.</param>
    public virtual void Stepback(int i, ReadOnlySpan<double> values, Span<double> newValues)
    {
        int size = Size(i);
        for (int j = 0; j < size; j++)
        {
            double value = 0.0;
            for (int l = 0; l < n; l++)
            {
                value += Probability(i, j, l) * values[Descendant(i, j, l)];
            }

            value *= Discount(i, j);
            newValues[j] = value;
        }
    }

    /// <summary>
    /// Stores the rows of state prices up to the given step: for a derived lattice while it is built, never after (a
    /// derived class whose constructor calls it seals the members it reads). Rows already stored are kept.
    /// </summary>
    /// <param name="until">The last step whose row is stored.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="until"/> is outside the grid's indices.</exception>
    protected internal void ComputeStatePrices(int until)
    {
        CheckStep(until, nameof(until));
        for (int i = statePricesLimit; i < until; i++)
        {
            statePrices[i + 1] = NextStatePrices(i, statePrices[i]);
        }

        if (until > statePricesLimit)
        {
            statePricesLimit = until;
        }
    }

    private static TreeLattice NotNull(TreeLattice other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other;
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    private ImmutableArray<double> NextStatePrices(int i, ImmutableArray<double> current)
    {
        // QuantLib's computeStatePrices: (statePrice*disc)*prob, added in (j, l) order
        double[] next = new double[Size(i + 1)];
        int size = Size(i);
        for (int j = 0; j < size; j++)
        {
            double disc = Discount(i, j);
            double statePrice = current[j];
            for (int l = 0; l < n; l++)
            {
                next[Descendant(i, j, l)] += statePrice * disc * Probability(i, j, l);
            }
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(next);
    }

    private void CheckStep(int i, string paramName)
    {
        // QuantLib extends its cache to i through size(i + 1), reading past its trees beyond the grid's last time.
        if (i < 0 || i >= TimeGrid.Size)
        {
            throw new ArgumentOutOfRangeException(paramName, i, $"step {i} outside the grid's {TimeGrid.Size} times");
        }
    }
}
