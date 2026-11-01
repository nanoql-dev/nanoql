// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.Bind.TestTypes;

/// <summary>
/// A user's generator: it implements nanoql's generator interface, unmarked itself, so the generator reads the mark
/// through the interface and keeps it off handles: it crosses as a snapshot drawn in batches. Its draws count up from a
/// start, in steps, each weighted one.
/// </summary>
public sealed class CountingRng : IRng
{
    private readonly double step;
    private double next;

    /// <summary>Initializes a new instance of the <see cref="CountingRng"/> class.</summary>
    /// <param name="start">The first draw.</param>
    /// <param name="step">The step between draws.</param>
    public CountingRng(double start, double step)
    {
        next = start;
        this.step = step;
    }

    /// <inheritdoc/>
    public Sample<double> Next()
    {
        double v = next;
        next += step;
        return new Sample<double>(v, 1.0);
    }

    /// <inheritdoc/>
    public IRng Clone() => new CountingRng(next, step);
}
