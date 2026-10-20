// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/localvolcurve.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The local volatility curve of a Black variance curve: where ∫₀ᵀ σ_L²(t) dt = σ_B(T)² T, the local volatility is
/// σ_L(t) = √(d(σ_B²(t) t)/dt), taken by a forward difference over one day.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's curve answers its reference date, calendar, day counter and maximum date from its Black curve's, which
/// can move; here they are the Black curve's, passed at construction, since that curve's are fixed.
/// </para>
/// <para>
/// A decreasing Black variance is refused, "negative local vol^2 at time …; the black variance curve is not smooth
/// enough", as <see cref="LocalVolSurface"/> refuses the same condition. QuantLib returns the square root of the
/// negative derivative, NaN (<c>localvolcurve.hpp:88-95</c>); a documented divergence.
/// </para>
/// </remarks>
public sealed class LocalVolCurve : LocalVolTermStructure
{
    private readonly BlackVarianceCurve blackVarianceCurve;

    /// <summary>Initializes a new instance of the <see cref="LocalVolCurve"/> class.</summary>
    /// <param name="curve">The Black variance curve.</param>
    /// <exception cref="ArgumentNullException"><paramref name="curve"/> is <see langword="null"/>.</exception>
    public LocalVolCurve(BlackVarianceCurve curve)
        : base(
            CurveOf(curve).ReferenceDate,
            curve.Calendar,
            curve.BusinessDayConvention,
            curve.DayCounter)
    {
        blackVarianceCurve = curve;
    }

    /// <inheritdoc/>
    public override Date MaxDate => blackVarianceCurve.MaxDate;

    /// <inheritdoc/>
    public override double MinStrike() => NumericLimits.QL_MIN_REAL;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The Black variance decreases over the day after the time.</exception>
    protected override double LocalVolImpl(double t, double strike)
    {
        double dt = 1.0 / 365.0;
        double var1 = blackVarianceCurve.BlackVariance(t, strike, true);
        double var2 = blackVarianceCurve.BlackVariance(t + dt, strike, true);
        double derivative = (var2 - var1) / dt;
        if (!(derivative >= 0.0))
        {
            throw new NanoQLException(
                $"negative local vol^2 at time {t.ToString(System.Globalization.CultureInfo.InvariantCulture)}; the black variance curve is "
                + "not smooth enough");
        }

        return Math.Sqrt(derivative);
    }

    private static BlackVarianceCurve CurveOf(BlackVarianceCurve curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        return curve;
    }
}
