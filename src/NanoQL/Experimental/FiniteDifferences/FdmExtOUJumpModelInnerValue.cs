// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmextoujumpmodelinnervalue.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>
/// The inner value of a payoff on Kluge's spot e^(f(t) + x + y), x and y a mesher's locations in directions 0 and 1 and
/// f a seasonal shape read at the first of its times at or after t − √ε (0 without a shape).
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
public sealed class FdmExtOUJumpModelInnerValue : FdmInnerValueCalculator
{
    private readonly Payoff payoff;
    private readonly FdmMesher mesher;
    private readonly ImmutableArray<(double Time, double Value)> shape;

    /// <summary>Initializes a new instance of the <see cref="FdmExtOUJumpModelInnerValue"/> class.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="shape">The seasonal shape, (time, value) pairs by increasing time, or <see langword="null"/> for
    /// none.</param>
    /// <exception cref="ArgumentNullException">The payoff or the mesher is <see langword="null"/>.</exception>
    public FdmExtOUJumpModelInnerValue(Payoff payoff, FdmMesher mesher, IReadOnlyList<(double Time, double Value)>? shape = null)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(mesher);
        this.payoff = payoff;
        this.mesher = mesher;
        this.shape = shape is null ? default : [.. shape];
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The time is past the shape's last ((xxxi)).</exception>
    public override double InnerValue(FdmLinearOpIterator iter, double t)
    {
        double x = mesher.Location(iter, 0);
        double y = mesher.Location(iter, 1);
        double f = shape.IsDefault ? 0.0 : FdmExpExtOUInnerValueCalculator.ShapeAt(shape, t);
        return payoff.Evaluate(Math.Exp(f + x + y));
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The time is past the shape's last ((xxxi)).</exception>
    public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
}
