// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/alphaformconcrete.hpp ql/models/marketmodels/models/alphaformconcrete.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Models;

/// <summary>
/// The alpha form √(1 + α t_i (atan(α t_i) − π/2)).
/// </summary>
/// <remarks>
/// A working type (the S-mm plan's MR2 (b)). A rate past the times, which QuantLib reads unchecked, is refused (MR3
/// (xx)).
/// </remarks>
[WorkingType]
public sealed class AlphaFormLinearHyperbolic : AlphaForm
{
    private readonly double[] times;
    private double alpha;

    /// <summary>Initializes a new instance of the <see cref="AlphaFormLinearHyperbolic"/> class.</summary>
    /// <param name="times">Each rate's time.</param>
    /// <param name="alpha">The initial α.</param>
    public AlphaFormLinearHyperbolic(ReadOnlySpan<double> times, double alpha = 0.0)
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

        double at = alpha * times[i];
        double res = Math.Atan(at) - (0.5 * Math.PI);
        res *= at;
        res += 1.0;
        res = Math.Sqrt(res);
        return res;
    }

    /// <inheritdoc/>
    public override void SetAlpha(double alpha) => this.alpha = alpha;

    /// <inheritdoc/>
    public override AlphaForm Clone() => new AlphaFormLinearHyperbolic(times, alpha);
}
