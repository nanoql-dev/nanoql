// SPDX-FileCopyrightText: Copyright (C) 2015 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/gridmodellocalvolsurface.hpp ql/termstructures/volatility/equityfx/gridmodellocalvolsurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A local volatility surface parameterised for calibration: one positive parameter per grid point of dates and
/// strikes, all starting at 1, the surface a <see cref="FixedLocalVolSurface"/> on them.
/// </summary>
/// <remarks>
/// Immutable, a <see cref="LocalVolTermStructure"/> implementing <see cref="ICalibratedModel"/> over its arguments (the
/// S-van plan's VD22, the S-bond plan's SBR2): <see cref="WithParams"/> returns a copy on its own fixed surface, which a
/// calibration reads, where QuantLib's model regenerates its surface in place. QuantLib's <c>update()</c>, which
/// forwards a notification to both its bases, has no counterpart: nothing here changes.
/// </remarks>
public sealed class GridModelLocalVolSurface : LocalVolTermStructure, ICalibratedModel
{
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<ImmutableArray<double>> strikes;
    private readonly FixedLocalVolSurfaceExtrapolation lowerExtrapolation;
    private readonly FixedLocalVolSurfaceExtrapolation upperExtrapolation;

    // Written at construction and on a fresh copy only (WithParams).
    private ImmutableArray<Parameter> arguments;
    private FixedLocalVolSurface localVol;

    /// <summary>Initializes a new instance of the <see cref="GridModelLocalVolSurface"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dates">The grid's dates, sorted, not before the reference date.</param>
    /// <param name="strikes">Each date's strikes, sorted, all of one size.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="lowerExtrapolation">How the surface extrapolates below a date's strikes.</param>
    /// <param name="upperExtrapolation">How the surface extrapolates above them.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An extrapolation isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// No strikes are given, where QuantLib reads <c>strikes.front()</c> of an empty list; the strike vectors differ in
    /// size; or the fixed surface refuses the grid; with QuantLib's messages.
    /// </exception>
    public GridModelLocalVolSurface(
        Date referenceDate,
        IEnumerable<Date> dates,
        IEnumerable<IEnumerable<double>> strikes,
        DayCounter dayCounter,
        FixedLocalVolSurfaceExtrapolation lowerExtrapolation = FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation,
        FixedLocalVolSurfaceExtrapolation upperExtrapolation = FixedLocalVolSurfaceExtrapolation.ConstantExtrapolation)
        : base(referenceDate, new NullCalendar(), BusinessDayConvention.Following, dayCounter)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(strikes);
        EnumArguments.Check(lowerExtrapolation, nameof(lowerExtrapolation));
        EnumArguments.Check(upperExtrapolation, nameof(upperExtrapolation));
        Date[] ds = [.. dates];
        this.strikes = [.. strikes.Select(k => (ImmutableArray<double>)[.. k ?? throw new ArgumentNullException(nameof(strikes))])];
        if (this.strikes.Length == 0)
        {
            throw new ArgumentException("at least one strike vector is required", nameof(strikes));
        }

        this.lowerExtrapolation = lowerExtrapolation;
        this.upperExtrapolation = upperExtrapolation;

        for (int i = 1; i < this.strikes.Length; ++i)
        {
            if (!(this.strikes[i].Length == this.strikes[0].Length))
            {
                throw new ArgumentException("strike vectors must have the same dimension", nameof(strikes));
            }
        }

        var unit = ImmutableArray.CreateBuilder<Parameter>(ds.Length * this.strikes[0].Length);
        for (int i = 0; i < unit.Capacity; ++i)
        {
            unit.Add(new ConstantParameter(1.0, new PositiveConstraint()));
        }

        arguments = unit.MoveToImmutable();

        times = [.. ds.Select(d => dayCounter.YearFraction(referenceDate, d))];

        localVol = Generated(arguments);
    }

    /// <summary>Gets the fixed surface's maximum date.</summary>
    public override Date MaxDate => localVol.MaxDate;

    /// <summary>Gets the fixed surface's maximum time.</summary>
    public override double MaxTime => localVol.MaxTime;

    /// <inheritdoc/>
    public ImmutableArray<Parameter> Arguments => arguments;

    /// <inheritdoc/>
    public Constraint Constraint => new CalibratedModel.PrivateConstraint(arguments);

    /// <summary>The fixed surface's lowest strike.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => localVol.MinStrike();

    /// <summary>The fixed surface's highest strike.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => localVol.MaxStrike();

    /// <inheritdoc/>
    public Vector Params() => CalibratedModel.FlattenParams(arguments);

    /// <summary>A copy of the surface on other parameters, its fixed surface rebuilt on them: QuantLib's
    /// <c>setParams</c> with its <c>generateArguments</c>.</summary>
    /// <param name="parameters">The parameters, one per grid point, row by row of strikes.</param>
    /// <returns>The copy; this surface is unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The parameters are too few or too many, with QuantLib's messages.</exception>
    public ICalibratedModel WithParams(Vector parameters)
    {
        ImmutableArray<Parameter> newArguments = CalibratedModel.ArgumentsWithParams(arguments, parameters);
        FixedLocalVolSurface surface = Generated(newArguments);
        var copy = (GridModelLocalVolSurface)MemberwiseClone();
        copy.arguments = newArguments;
        copy.localVol = surface;
        return copy;
    }

    /// <summary>The fixed surface's local volatility, extrapolating.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The local volatility.</returns>
    protected override double LocalVolImpl(double t, double strike) => localVol.LocalVol(t, strike, true);

    // QuantLib's generateArguments: the parameters, row by row, a strikes-by-times matrix
    private FixedLocalVolSurface Generated(ImmutableArray<Parameter> args)
    {
        var localVolMatrix = new Matrix(strikes[0].Length, times.Length);
        for (int k = 0; k < args.Length; ++k)
        {
            localVolMatrix[k / times.Length, k % times.Length] = args[k].Evaluate(0.0);
        }

        return new FixedLocalVolSurface(
            ReferenceDate,
            times,
            strikes.Select(k => (IEnumerable<double>)k),
            localVolMatrix,
            DayCounter,
            lowerExtrapolation,
            upperExtrapolation);
    }
}
