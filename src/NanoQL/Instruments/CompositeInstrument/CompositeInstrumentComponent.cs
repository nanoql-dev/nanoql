// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/compositeinstrument.hpp ql/instruments/compositeinstrument.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Instruments;

/// <summary>
/// A component of a <see cref="CompositeInstrument"/>: an instrument, the engine that prices it and its multiplier,
/// QuantLib's <c>std::pair&lt;ext::shared_ptr&lt;Instrument&gt;, Real&gt;</c> with the engine the instrument carries.
/// </summary>
/// <remarks>Immutable. The engine is a delegate (S-exo's ER2 form), as nanoql's instruments carry none.</remarks>
public sealed class CompositeInstrumentComponent
{
    /// <summary>Initializes a new instance of the <see cref="CompositeInstrumentComponent"/> class.</summary>
    /// <param name="instrument">The instrument.</param>
    /// <param name="engine">The engine pricing it, from the evaluation context.</param>
    /// <param name="multiplier">The multiplier of its value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="instrument"/> or <paramref name="engine"/> is
    /// <see langword="null"/>.</exception>
    public CompositeInstrumentComponent(
        Instrument instrument, Func<EvaluationContext, InstrumentResults> engine, double multiplier)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(engine);
        Instrument = instrument;
        Engine = engine;
        Multiplier = multiplier;
    }

    /// <summary>Gets the instrument.</summary>
    public Instrument Instrument { get; }

    /// <summary>Gets the engine pricing the instrument.</summary>
    public Func<EvaluationContext, InstrumentResults> Engine { get; }

    /// <summary>Gets the multiplier of the instrument's value.</summary>
    public double Multiplier { get; }
}
