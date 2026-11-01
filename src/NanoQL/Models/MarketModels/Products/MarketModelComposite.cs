// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/products/compositeproduct.hpp ql/models/marketmodels/products/compositeproduct.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Models;

/// <summary>
/// A market-model product composed of one or more products on the same rate times, each with a multiplier: its
/// evolution steps at every part's evolution times, and each part steps at its own.
/// </summary>
/// <remarks>
/// <para>
/// A working type (the S-mmp plan's MMPR1 (i)): <see cref="Add"/> and <see cref="Subtract"/> store a clone of the
/// product given, QuantLib's <c>Clone&lt;&gt;</c>, so the caller's product is never stepped by the composite;
/// <see cref="FinalizeComposite"/> fixes the parts, after which the composite steps; <see cref="Item"/> is the
/// composite's own part.
/// </para>
/// <para>
/// <b>A part's end is recorded</b> (MMPR4 (vii), option (1), a documented divergence): a part that reports its path
/// finished isn't stepped again, and the composite's path is finished when every part's is, or at its last step.
/// QuantLib records no part's end (<c>compositeproduct.cpp:43</c> resets the flag its <c>nextTimeStep</c> tests and
/// never sets), so a finished part steps again at each of its later times, and it finishes the composite's path when
/// the parts it stepped at a step are finished, whatever the others' later times: a swaption beside forwards was worth
/// 4.2 to 7.7 times its value alone, and forwards beside a rebate lost their later flows
/// (<c>Divergences/MarketModelCompositeEndTests.cs</c>). A derived composite steps its parts through
/// <see cref="StepPart"/> and ends its step with <see cref="EndStep"/>, which keep these rules.
/// </para>
/// <para>An extension point (U13).</para>
/// </remarks>
[WorkingType]
public abstract class MarketModelComposite : MarketModelMultiProduct
{
    private readonly List<Part> parts;
    private readonly List<ImmutableArray<double>> allEvolutionTimes;
    private ImmutableArray<double> rateTimes = [];
    private ImmutableArray<double> evolutionTimes = [];
    private EvolutionDescription? evolution;
    private ImmutableArray<double> cashflowTimes = [];
    private ImmutableArray<ImmutableArray<bool>> isInSubset = [];
    private bool finalized;
    private int currentIndex;

    /// <summary>Initializes a new instance of the <see cref="MarketModelComposite"/> class, with no part.</summary>
    protected MarketModelComposite()
    {
        parts = [];
        allEvolutionTimes = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketModelComposite"/> class as a deep copy of another, its parts
    /// cloned and its state copied, for a derived class's <see cref="MarketModelMultiProduct.Clone"/>.
    /// </summary>
    /// <param name="other">The composite copied.</param>
    protected MarketModelComposite(MarketModelComposite other)
    {
        ArgumentNullException.ThrowIfNull(other);
        parts = [.. other.parts.Select(p => new Part(p))];
        allEvolutionTimes = [.. other.allEvolutionTimes];
        rateTimes = other.rateTimes;
        evolutionTimes = other.evolutionTimes;
        evolution = other.evolution;
        cashflowTimes = other.cashflowTimes;
        isInSubset = other.isInSubset;
        finalized = other.finalized;
        currentIndex = other.currentIndex;
    }

    /// <summary>Gets the composite's evolution: the parts' rate times and every part's evolution times.</summary>
    /// <exception cref="NanoQLException">The composite isn't finalized.</exception>
    public override EvolutionDescription Evolution
    {
        get
        {
            ThrowIfNotFinalized();
            return evolution!;
        }
    }

    /// <summary>Gets the number of parts.</summary>
    public int Size => parts.Count;

    /// <summary>Gets the index of the composite's next step on its path.</summary>
    protected int CurrentIndex => currentIndex;

    /// <summary>The terminal measure of the composite's evolution.</summary>
    /// <returns>The numeraires' indices.</returns>
    /// <exception cref="NanoQLException">The composite isn't finalized.</exception>
    public override ImmutableArray<int> SuggestedNumeraires()
    {
        ThrowIfNotFinalized();
        return EvolutionDescriptionFunctions.TerminalMeasure(evolution!);
    }

    /// <summary>Every part's possible cash-flow times, sorted, each once.</summary>
    /// <returns>The times.</returns>
    /// <exception cref="NanoQLException">The composite isn't finalized.</exception>
    public override ImmutableArray<double> PossibleCashFlowTimes()
    {
        ThrowIfNotFinalized();
        return cashflowTimes;
    }

    /// <summary>Puts every part, and the composite, at the start of a path.</summary>
    public override void Reset()
    {
        foreach (Part p in parts)
        {
            p.Product.Reset();
            p.Done = false;
        }

        currentIndex = 0;
    }

    /// <summary>Adds a clone of a product, with a multiplier.</summary>
    /// <param name="product">The product, on the rate times of the parts already added.</param>
    /// <param name="multiplier">The multiplier of its cash flows.</param>
    /// <exception cref="NanoQLException">The composite is finalized, or the product's rate times aren't the
    /// parts'.</exception>
    public void Add(MarketModelMultiProduct product, double multiplier = 1.0)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (finalized)
        {
            throw new NanoQLException("product already finalized");
        }

        EvolutionDescription d = product.Evolution;
        if (parts.Count > 0)
        {
            // enforce preconditions
            ImmutableArray<double> rateTimes1 = parts[0].Product.Evolution.RateTimes;
            ImmutableArray<double> rateTimes2 = d.RateTimes;
            if (!rateTimes1.AsSpan().SequenceEqual(rateTimes2.AsSpan()))
            {
                throw new NanoQLException("incompatible rate times");
            }
        }

        parts.Add(new Part(product.Clone(), multiplier));
        allEvolutionTimes.Add(d.EvolutionTimes);
    }

    /// <summary>Adds a clone of a product, with the opposite of a multiplier.</summary>
    /// <param name="product">The product, on the rate times of the parts already added.</param>
    /// <param name="multiplier">The multiplier of its cash flows, negated.</param>
    /// <exception cref="NanoQLException">The composite is finalized, or the product's rate times aren't the
    /// parts'.</exception>
    public void Subtract(MarketModelMultiProduct product, double multiplier = 1.0) => Add(product, -multiplier);

    /// <summary>
    /// Fixes the parts: merges their evolution times and cash-flow times, maps each part's cash-flow times into the
    /// merged ones and sizes each part's buffers. The composite steps only once finalized. QuantLib's
    /// <c>finalize</c>, renamed: C# reserves <c>Finalize()</c> for a finalizer (CS0465).
    /// </summary>
    /// <exception cref="NanoQLException">The composite is finalized already, or has no part.</exception>
    public void FinalizeComposite()
    {
        if (finalized)
        {
            throw new NanoQLException("product already finalized");
        }

        if (parts.Count == 0)
        {
            throw new NanoQLException("no sub-product provided");
        }

        // fetch the rate times from the first subproduct (we checked they're all the same)
        rateTimes = parts[0].Product.Evolution.RateTimes;

        MergedTimes merged = UtilitiesFunctions.MergeTimes(allEvolutionTimes.ToArray());
        evolutionTimes = merged.Times;
        isInSubset = merged.IsPresent;

        var allCashflowTimes = new List<double>();

        // now, for each subproduct...
        foreach (Part p in parts)
        {
            // ...collect all possible cash-flow times...
            allCashflowTimes.AddRange(p.Product.PossibleCashFlowTimes());

            // ...allocate working vectors...
            p.Allocate();
        }

        // all information having been collected, we can sort and compact the vector of all cash-flow times...
        allCashflowTimes.Sort();
        cashflowTimes = [.. allCashflowTimes.Distinct()];

        // ...and map each product's cash-flow time into the total vector.
        foreach (Part p in parts)
        {
            ImmutableArray<double> productTimes = p.Product.PossibleCashFlowTimes();
            p.TimeIndices = new int[productTimes.Length];
            for (int j = 0; j < productTimes.Length; ++j)
            {
                p.TimeIndices[j] = cashflowTimes.IndexOf(productTimes[j]);
            }
        }

        evolution = new EvolutionDescription(rateTimes.AsSpan(), evolutionTimes.AsSpan());

        // all done.
        finalized = true;
    }

    /// <summary>The composite's part at an index: its own clone of the product added.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The part.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a part's index.</exception>
    public MarketModelMultiProduct Item(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, parts.Count);
        return parts[i].Product;
    }

    /// <summary>The multiplier of the part at an index.</summary>
    /// <param name="i">The index.</param>
    /// <returns>The multiplier.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a part's index.</exception>
    public double Multiplier(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, parts.Count);
        return parts[i].Multiplier;
    }

    /// <summary>Whether the part at an index has finished on the current path.</summary>
    /// <param name="i">The index.</param>
    /// <returns><see langword="true"/> where the part has finished.</returns>
    internal bool IsPartDone(int i) => parts[i].Done;

    /// <summary>Refuses a composite that isn't finalized, QuantLib's check.</summary>
    /// <exception cref="NanoQLException">The composite isn't finalized.</exception>
    protected void ThrowIfNotFinalized()
    {
        if (!finalized)
        {
            throw new NanoQLException("composite not finalized");
        }
    }

    /// <summary>
    /// Steps the part at an index where the composite's current step is one of its own and its path isn't finished,
    /// recording whether its path finishes; the part's counts and cash flows are then in <see cref="PartCounts"/> and
    /// <see cref="PartCashFlows"/>, their time indices the part's own (<see cref="CashFlowTimeIndex"/> maps them).
    /// </summary>
    /// <param name="n">The part's index.</param>
    /// <param name="currentState">The state of the curve.</param>
    /// <returns>Whether the part was stepped.</returns>
    /// <exception cref="NanoQLException">The composite is stepped past its last step.</exception>
    protected bool StepPart(int n, CurveState currentState)
    {
        if (currentIndex >= evolutionTimes.Length)
        {
            throw new NanoQLException("the composite is stepped past its last step");
        }

        Part p = parts[n];
        if (!isInSubset[n][currentIndex] || p.Done)
        {
            return false;
        }

        p.Done = p.Product.NextTimeStep(currentState, p.NumberOfCashflows, p.Cashflows);
        return true;
    }

    /// <summary>The counts the part at an index wrote at its last step.</summary>
    /// <param name="n">The part's index.</param>
    /// <returns>A view of the part's buffer.</returns>
    protected ReadOnlySpan<int> PartCounts(int n) => parts[n].NumberOfCashflows;

    /// <summary>The cash flows the part at an index wrote at its last step, in its own buffer.</summary>
    /// <param name="n">The part's index.</param>
    /// <returns>The part's buffer.</returns>
    protected MarketModelMultiProductCashFlow[][] PartCashFlows(int n) => parts[n].Cashflows;

    /// <summary>The index in the composite's cash-flow times of a part's own cash-flow time index.</summary>
    /// <param name="n">The part's index.</param>
    /// <param name="timeIndex">The index in the part's own cash-flow times.</param>
    /// <returns>The composite's index.</returns>
    protected int CashFlowTimeIndex(int n, int timeIndex) => parts[n].TimeIndices[timeIndex];

    /// <summary>
    /// Ends the composite's step: its path is finished when every part's is or the step is the composite's last.
    /// </summary>
    /// <returns>Whether the composite's path is finished.</returns>
    protected bool EndStep()
    {
        bool done = currentIndex == evolutionTimes.Length - 1;
        if (!done)
        {
            done = true;
            foreach (Part p in parts)
            {
                done = done && p.Done;
            }
        }

        ++currentIndex;
        return done;
    }

    private sealed class Part
    {
        public Part(MarketModelMultiProduct product, double multiplier)
        {
            Product = product;
            Multiplier = multiplier;
        }

        public Part(Part other)
        {
            Product = other.Product.Clone();
            Multiplier = other.Multiplier;
            NumberOfCashflows = (int[])other.NumberOfCashflows.Clone();
            Cashflows = [.. other.Cashflows.Select(r => (MarketModelMultiProductCashFlow[])r.Clone())];
            TimeIndices = other.TimeIndices;
            Done = other.Done;
        }

        public MarketModelMultiProduct Product { get; }

        public double Multiplier { get; }

        public int[] NumberOfCashflows { get; private set; } = [];

        public MarketModelMultiProductCashFlow[][] Cashflows { get; private set; } = [];

        public int[] TimeIndices { get; set; } = [];

        public bool Done { get; set; }

        public void Allocate()
        {
            int n = Product.NumberOfProducts;
            NumberOfCashflows = new int[n];
            Cashflows = new MarketModelMultiProductCashFlow[n][];
            for (int i = 0; i < n; ++i)
            {
                Cashflows[i] = new MarketModelMultiProductCashFlow[Product.MaxNumberOfCashFlowsPerProductPerStep];
            }
        }
    }
}
