// SPDX-FileCopyrightText: Copyright (C) 2017, 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/andreasenhugelocalvoladapter.hpp ql/termstructures/volatility/equityfx/andreasenhugelocalvoladapter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The local volatility structure of an <see cref="AndreasenHugeVolatilityInterpl"/>: its local volatility, the strike
/// clamped to its grid.
/// </summary>
/// <remarks>
/// Immutable. Its reference date, calendar, day counter and settlement days are the interpolation's risk-free curve's,
/// read when it is built, where QuantLib's adapter reads them through the curve's handle.
/// </remarks>
public sealed class AndreasenHugeLocalVolAdapter : LocalVolTermStructure
{
    private readonly AndreasenHugeVolatilityInterpl localVol;

    /// <summary>Initializes a new instance of the <see cref="AndreasenHugeLocalVolAdapter"/> class.</summary>
    /// <param name="localVol">The interpolation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="localVol"/> is <see langword="null"/>.</exception>
    public AndreasenHugeLocalVolAdapter(AndreasenHugeVolatilityInterpl localVol)
        : base(
            Interpl(localVol).RiskFreeRate.ReferenceDate,
            localVol.RiskFreeRate.Calendar,
            BusinessDayConvention.Following,
            localVol.RiskFreeRate.DayCounter,
            localVol.RiskFreeRate.SettlementDays)
    {
        this.localVol = localVol;
    }

    /// <summary>Gets the interpolation's last expiry.</summary>
    public override Date MaxDate => localVol.MaxDate;

    /// <summary>The lowest strike, 0.</summary>
    /// <returns>0.</returns>
    public override double MinStrike() => 0.0;

    /// <summary>The highest strike, unbounded: QuantLib's <c>QL_MAX_REAL</c>.</summary>
    /// <returns>1.7976931348623157e308.</returns>
    public override double MaxStrike() => double.MaxValue;

    /// <summary>The interpolation's local volatility, the strike clamped to its grid.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The local volatility.</returns>
    protected override double LocalVolImpl(double t, double strike) =>
        localVol.LocalVol(t, Math.Min(localVol.MaxStrike(), Math.Max(localVol.MinStrike(), strike)));

    private static AndreasenHugeVolatilityInterpl Interpl(AndreasenHugeVolatilityInterpl localVol)
    {
        ArgumentNullException.ThrowIfNull(localVol);
        return localVol;
    }
}
