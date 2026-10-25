// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/mchestonhullwhiteengine.hpp ql/pricingengines/vanilla/mchestonhullwhiteengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>The European path pricer of a hybrid Heston-Hull-White multi-path: the payoff on the underlying's last value
/// over the numeraire at the exercise.</summary>
/// <remarks>Immutable; its evaluation allocates nothing, the numeraire read from the short rate's last value, all of the
/// state it reads (the S-hes plan's HR2 (iii)).</remarks>
public sealed class HestonHullWhitePathPricer : PathPricer<MultiPath>
{
    private readonly double exerciseTime;
    private readonly Payoff payoff;
    private readonly HybridHestonHullWhiteProcess process;

    /// <summary>Initializes a new instance of the <see cref="HestonHullWhitePathPricer"/> class.</summary>
    /// <param name="exerciseTime">The exercise's time.</param>
    /// <param name="payoff">The payoff.</param>
    /// <param name="process">The hybrid process, whose numeraire discounts.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public HestonHullWhitePathPricer(double exerciseTime, Payoff payoff, HybridHestonHullWhiteProcess process)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(process);
        this.exerciseTime = exerciseTime;
        this.payoff = payoff;
        this.process = process;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The path is empty.</exception>
    public override double Evaluate(MultiPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!(path.PathSize() > 0))
        {
            throw new NanoQLException("the path cannot be empty");
        }

        double df = 1.0 / process.NumeraireAt(exerciseTime, path[2].Back());
        return payoff.Evaluate(path[0].Back()) * df;
    }
}
