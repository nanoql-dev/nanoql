// SPDX-FileCopyrightText: Copyright (C) 2009 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/inflation/piecewiseyoyoptionletvolatility.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The bootstrap traits of a year-on-year optionlet volatility curve: QuantLib's <c>YoYInflationVolatilityTraits</c>.
/// </summary>
/// <remarks>
/// The curve starts at its base date at its base level, the assumption on the unquoted early optionlets; each pillar's
/// volatility is searched within 2% of the one before, never below 0, from 0.5% at the first pillar and 0.2% after;
/// the bootstrap loops at most 25 times.
/// </remarks>
[Experimental("NQL0001")]
public readonly struct YoYInflationVolatilityTraits : IBootstrapTraits<YoYOptionletVolatilitySurface>
{
    /// <inheritdoc/>
    public static int MaxIterations => 25;

    /// <inheritdoc/>
    /// <remarks>The curve's base date.</remarks>
    public static Date InitialDate(BootstrapState<YoYOptionletVolatilitySurface> c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Curve.BaseDate();
    }

    /// <inheritdoc/>
    /// <remarks>The curve's base level.</remarks>
    public static double InitialValue(BootstrapState<YoYOptionletVolatilitySurface> c)
    {
        ArgumentNullException.ThrowIfNull(c);

        // REALLLYYYY important because
        // generally don't have a clue
        // what this should be - embodies
        // assumptions on early options
        // that are _not_ quoted
        return c.Curve.BaseLevel();
    }

    /// <inheritdoc/>
    public static double Guess(
        int i, BootstrapState<YoYOptionletVolatilitySurface> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (validData)
        {
            // previous iteration value
            return c.Data[i];
        }

        if (i == 1)
        {
            // first pillar
            return 0.005;
        }

        // could/should extrapolate
        return 0.002;
    }

    /// <inheritdoc/>
    public static double MinValueAfter(
        int i, BootstrapState<YoYOptionletVolatilitySurface> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);

        // vol cannot be negative
        return Math.Max(0.0, c.Data[i - 1] - 0.02);
    }

    /// <inheritdoc/>
    public static double MaxValueAfter(
        int i, BootstrapState<YoYOptionletVolatilitySurface> c, bool validData, int firstAliveHelper)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Data[i - 1] + 0.02;
    }

    /// <inheritdoc/>
    public static void UpdateGuess(double[] data, double value, int i)
    {
        ArgumentNullException.ThrowIfNull(data);
        data[i] = value;
    }
}
