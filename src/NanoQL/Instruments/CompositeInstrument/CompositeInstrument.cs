// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/compositeinstrument.hpp ql/instruments/compositeinstrument.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;

namespace NanoQL.Instruments;

/// <summary>
/// An aggregate of other instruments: its value is the sum of its components' values, each times its multiplier,
/// QuantLib's <c>CompositeInstrument</c>.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: <see cref="Add"/> and <see cref="Subtract"/> return a new composite with the component added, where
/// QuantLib's change the composite. Each component carries the engine that prices it, a delegate from the evaluation
/// context (the S-core plan's CR5 (c)), as nanoql's instruments carry none; QuantLib's composite reads the engines its
/// components carry.
/// </para>
/// <para>
/// It is priced without an engine: <see cref="Calculate"/> runs QuantLib's steps, its value the components' values
/// summed in the order they were added, each from its engine's results under the context, an expired component's 0
/// without its engine, as QuantLib's instrument gives it. It has expired when every
/// component has, an empty composite included, as QuantLib's. Its error estimate is <see langword="null"/>, as QuantLib's
/// is on a composite never expired. QuantLib's <c>deepUpdate</c> has no counterpart.
/// </para>
/// </remarks>
public sealed class CompositeInstrument : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="CompositeInstrument"/> class with no component.</summary>
    public CompositeInstrument()
        : this(ImmutableArray<CompositeInstrumentComponent>.Empty)
    {
    }

    private CompositeInstrument(ImmutableArray<CompositeInstrumentComponent> components)
    {
        Components = components;
    }

    /// <summary>Gets the components, in the order they were added.</summary>
    public ImmutableArray<CompositeInstrumentComponent> Components { get; }

    /// <summary>A composite with an instrument added.</summary>
    /// <param name="instrument">The instrument.</param>
    /// <param name="engine">The engine pricing it, from the evaluation context.</param>
    /// <param name="multiplier">The multiplier of its value.</param>
    /// <returns>The new composite.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instrument"/> or <paramref name="engine"/> is
    /// <see langword="null"/>.</exception>
    public CompositeInstrument Add(
        Instrument instrument, Func<EvaluationContext, InstrumentResults> engine, double multiplier = 1.0) =>
        new(Components.Add(new CompositeInstrumentComponent(instrument, engine, multiplier)));

    /// <summary>A composite with an instrument subtracted: added with its multiplier negated.</summary>
    /// <param name="instrument">The instrument.</param>
    /// <param name="engine">The engine pricing it, from the evaluation context.</param>
    /// <param name="multiplier">The multiplier of its value, which is subtracted.</param>
    /// <returns>The new composite.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instrument"/> or <paramref name="engine"/> is
    /// <see langword="null"/>.</exception>
    public CompositeInstrument Subtract(
        Instrument instrument, Func<EvaluationContext, InstrumentResults> engine, double multiplier = 1.0) =>
        Add(instrument, engine, -multiplier);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (CompositeInstrumentComponent component in Components)
        {
            if (!component.Instrument.IsExpired(context))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The composite's results: its components' values summed, each times its multiplier.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">A component's engine gives no value ("NPV not provided"), or fails.</exception>
    public InstrumentResults Calculate(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (IsExpired(context))
        {
            return SetupExpired();
        }

        Validate();
        double npv = 0.0;
        foreach (CompositeInstrumentComponent component in Components)
        {
            // QuantLib's NPV() on the component: an expired instrument's is setupExpired's 0, without its engine
            double componentNpv = component.Instrument.IsExpired(context)
                ? 0.0
                : component.Engine(context).NPV ?? throw new NanoQLException("NPV not provided");
            npv += component.Multiplier * componentNpv;
        }

        return FetchResults(new InstrumentResults { NPV = npv });
    }
}
