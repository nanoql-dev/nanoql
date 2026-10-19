// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/latticeshortratemodelengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Models;

namespace NanoQL.PricingEngines;

/// <summary>
/// What QuantLib's engine base for a short-rate model on a lattice gives the tree engines: the time-steps check and the
/// lattice built from an asset's mandatory times.
/// </summary>
/// <remarks>
/// QuantLib's engine holds the model and, given a time grid, a lattice built once and rebuilt on notification; the
/// static engines take the model, the time steps or grid, or a built lattice, per call (the lattice overload is the
/// reuse).
/// </remarks>
internal static class LatticeShortRateModelEngine
{
    /// <summary>Refuses a non-positive number of time steps: a negative one as an unsigned count, 0 with QuantLib's text.</summary>
    /// <param name="timeSteps">The number of time steps.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeSteps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> is 0.</exception>
    internal static void CheckTimeSteps(int timeSteps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeSteps);
        if (!(timeSteps > 0))
        {
            throw new ArgumentException($"timeSteps must be positive, {timeSteps} not allowed", nameof(timeSteps));
        }
    }

    /// <summary>The model's lattice on a grid through an asset's mandatory times, with the given number of steps.</summary>
    /// <param name="model">The model.</param>
    /// <param name="mandatoryTimes">The asset's mandatory times.</param>
    /// <param name="timeSteps">The number of time steps.</param>
    /// <returns>The lattice.</returns>
    internal static NanoQL.Lattice Lattice(ShortRateModel model, ReadOnlySpan<double> mandatoryTimes, int timeSteps) =>
        model.Tree(new TimeGrid(mandatoryTimes, timeSteps));
}
