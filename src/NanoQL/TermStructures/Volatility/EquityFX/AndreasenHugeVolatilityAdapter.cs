// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/andreasenhugevolatilityadapter.hpp ql/termstructures/volatility/equityfx/andreasenhugevolatilityadapter.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The Black volatility structure of an <see cref="AndreasenHugeVolatilityInterpl"/>: at a time and strike, the implied
/// variance of its out-of-the-money option's price, by Li's rational approximation refined.
/// </summary>
/// <remarks>
/// Immutable. Its reference date, calendar, day counter and settlement days are the interpolation's risk-free curve's,
/// read when it is built, where QuantLib's adapter reads them through the curve's handle.
/// </remarks>
public sealed class AndreasenHugeVolatilityAdapter : BlackVarianceTermStructure
{
    private readonly double eps;
    private readonly AndreasenHugeVolatilityInterpl volInterpl;

    /// <summary>Initializes a new instance of the <see cref="AndreasenHugeVolatilityAdapter"/> class.</summary>
    /// <param name="volInterpl">The interpolation.</param>
    /// <param name="eps">The implied volatility's accuracy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="volInterpl"/> is <see langword="null"/>.</exception>
    public AndreasenHugeVolatilityAdapter(AndreasenHugeVolatilityInterpl volInterpl, double eps = 1e-6)
        : base(
            Interpl(volInterpl).RiskFreeRate.ReferenceDate,
            volInterpl.RiskFreeRate.Calendar,
            BusinessDayConvention.Following,
            volInterpl.RiskFreeRate.DayCounter,
            volInterpl.RiskFreeRate.SettlementDays)
    {
        this.eps = eps;
        this.volInterpl = volInterpl;
    }

    /// <summary>Gets the interpolation's last expiry.</summary>
    public override Date MaxDate => volInterpl.MaxDate;

    /// <summary>The interpolation's lowest strike.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => volInterpl.MinStrike();

    /// <summary>The interpolation's highest strike.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => volInterpl.MaxStrike();

    /// <summary>The forward at a time, the interpolation's.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The forward.</returns>
    public override double? AtmLevel(double t) => volInterpl.Fwd(t);

    /// <summary>The implied variance of the out-of-the-money option's price at a time and strike.</summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The variance.</returns>
    protected override double BlackVarianceImpl(double t, double strike)
    {
        double fwd = volInterpl.Fwd(t);
        OptionType optionType = (fwd > strike) ? OptionType.Put : OptionType.Call;

        double npv = volInterpl.OptionPrice(t, strike, optionType);

        double stdDev = BlackFormulaFunctions.BlackFormulaImpliedStdDevLiRS(
            optionType, strike, fwd, npv, volInterpl.RiskFreeRate.Discount(t), 0.0, null, 1.0, eps, 1000);
        return stdDev * stdDev;
    }

    private static AndreasenHugeVolatilityInterpl Interpl(AndreasenHugeVolatilityInterpl volInterpl)
    {
        ArgumentNullException.ThrowIfNull(volInterpl);
        return volInterpl;
    }
}
