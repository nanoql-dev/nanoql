// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/alphaformconcrete.hpp ql/models/marketmodels/models/alphaformconcrete.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Models;

/// <summary>
/// The alpha form 1/(1 + α t_i).
/// </summary>
/// <remarks>
/// A working type. A rate past the times, which QuantLib reads unchecked, is refused.
/// </remarks>
[WorkingType]
public sealed class AlphaFormInverseLinear : AlphaForm
{
    private readonly double[] times;
    private double alpha;

    /// <summary>Initializes a new instance of the <see cref="AlphaFormInverseLinear"/> class.</summary>
    /// <param name="times">Each rate's time.</param>
    /// <param name="alpha">The initial α.</param>
    public AlphaFormInverseLinear(ReadOnlySpan<double> times, double alpha = 0.0)
    {
        this.times = times.ToArray();
        this.alpha = alpha;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> isn't a rate.</exception>
    public override double Evaluate(int i)
    {
        if (!(i >= 0 && i < times.Length))
        {
            throw new ArgumentOutOfRangeException(
                nameof(i), i, string.Create(CultureInfo.InvariantCulture, $"index ({i}) must be less than the number of times ({times.Length})"));
        }

        return 1.0 / (1.0 + (alpha * times[i]));
    }

    /// <inheritdoc/>
    public override void SetAlpha(double alpha) => this.alpha = alpha;

    /// <inheritdoc/>
    public override AlphaForm Clone() => new AlphaFormInverseLinear(times, alpha);
}
