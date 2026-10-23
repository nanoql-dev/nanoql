// SPDX-FileCopyrightText: Copyright (C) 2013, 2018 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/markovfunctional.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;

namespace NanoQL.Models;

/// <summary>
/// A smile section of one's own for the Markov-functional model's calibration, which inverts digital prices to market
/// rates itself.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib: a custom section implements <see cref="InverseDigitalCall"/> and the smile
/// section's members, and is arbitrage free by assumption. The model's digital gap, market rate accuracy and rate bounds
/// don't apply to it, and its moneyness checkpoints only to the model's outputs. Its base is the smile section's default
/// construction, as QuantLib's (an exercise time of 0, no exercise date, reference date or day counter), whose members a
/// section overrides.
/// </remarks>
public abstract class MarkovFunctionalCustomSmileSection : SmileSection
{
    /// <summary>The strike at which a digital call has the given price.</summary>
    /// <param name="price">The digital call's price.</param>
    /// <param name="discount">The discount factor, the calibration point's annuity.</param>
    /// <returns>The strike.</returns>
    public abstract double InverseDigitalCall(double price, double discount = 1.0);
}
