// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmexpextouinnervaluecalculator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// The inner value of a payoff on e^(f(t) + u), u a mesher's location in one direction and f a seasonal shape read at
/// the first of its times at or after t − √ε (0 without a shape).
/// </summary>
/// <remarks>
/// <para>
/// The shape is held as given, its times increasing, as QuantLib's lower bound reads it.
/// </para>
/// <para>
/// A time past the shape's last, where QuantLib's lookup reads one past the shape's end, is refused (the S-xeq plan's
/// XER5 (xxxi); <c>Conventions/ExperimentalEnergyConventionTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public sealed class FdmExpExtOUInnerValueCalculator : FdmInnerValueCalculator
{
    private readonly int direction;
    private readonly Payoff payoff;
    private readonly FdmMesher mesher;
    private readonly ImmutableArray<(double Time, double Value)> shape;

    /// <summary>Initializes a new instance of the <see cref="FdmExpExtOUInnerValueCalculator"/> class.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="shape">The seasonal shape, (time, value) pairs by increasing time, or <see langword="null"/> for
    /// none.</param>
    /// <param name="direction">The direction whose location is u.</param>
    /// <exception cref="ArgumentNullException">The payoff or the mesher is <see langword="null"/>.</exception>
    public FdmExpExtOUInnerValueCalculator(
        Payoff payoff, FdmMesher mesher, IReadOnlyList<(double Time, double Value)>? shape = null, int direction = 0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(mesher);
        this.direction = direction;
        this.payoff = payoff;
        this.mesher = mesher;
        this.shape = shape is null ? default : [.. shape];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The time is past the shape's last ((xxxi)).</exception>
    public override double InnerValue(FdmLinearOpIterator iter, double t)
    {
        double u = mesher.Location(iter, direction);
        double f = shape.IsDefault ? 0.0 : ShapeAt(shape, t);
        return payoff.Evaluate(Math.Exp(f + u));
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The time is past the shape's last ((xxxi)).</exception>
    public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);

    // QuantLib's std::lower_bound on the pairs at (t − √ε, 0): the first pair not less than it, by time then value;
    // past the last pair, where QuantLib reads one past the end, refused ((xxxi))
    internal static double ShapeAt(ImmutableArray<(double Time, double Value)> shape, double t)
    {
        (double Time, double Value) key = (t - Math.Sqrt(NumericLimits.QL_EPSILON), 0.0);
        int lo = 0, hi = shape.Length;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (Less(shape[mid], key))
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        if (lo == shape.Length)
        {
            throw new NanoQLException(
                $"time {DataFormattersFunctions.General(t)} is past the shape's last time "
                + $"{(shape.IsEmpty ? "(an empty shape)" : DataFormattersFunctions.General(shape[^1].Time))}");
        }

        return shape[lo].Value;
    }

    // std::pair's operator<
    private static bool Less((double Time, double Value) a, (double Time, double Value) b) =>
        a.Time < b.Time || (!(b.Time < a.Time) && a.Value < b.Value);
}
