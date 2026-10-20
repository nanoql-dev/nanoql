// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/quantotermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The quanto-adjusted dividend curve of an underlying in a foreign currency, for quanto option pricing: the zero
/// yield q + r − r_f + ρ σ_S σ_X, from the underlying's dividend curve, the domestic and foreign risk-free curves, the
/// underlying's and the exchange rate's Black volatilities and their correlation.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's curve answers its reference date, calendar, settlement days and day counter from the dividend curve,
/// which can move; here they are the dividend curve's, passed at construction, since that curve's are fixed. Its
/// maximum date is the earliest of the five structures'.
/// </para>
/// <para>
/// The five structures must share one day counter, or the curve refuses them, "all term structures must share one day
/// counter". QuantLib reads every structure at the time the dividend curve's day counter measures, with its own
/// comment that this assumes one day count and "should be QL_REQUIREd" (<c>quantotermstructure.hpp:124-131</c>), so
/// curves equal at every date but on different day counters give different quanto rates; a documented divergence.
/// </para>
/// </remarks>
public sealed class QuantoTermStructure : ZeroYieldStructure
{
    private readonly YieldTermStructure underlyingDividendTS;
    private readonly YieldTermStructure riskFreeTS;
    private readonly YieldTermStructure foreignRiskFreeTS;
    private readonly BlackVolTermStructure underlyingBlackVolTS;
    private readonly BlackVolTermStructure exchRateBlackVolTS;
    private readonly double underlyingExchRateCorrelation;
    private readonly double strike;
    private readonly double exchRateATMlevel;

    /// <summary>Initializes a new instance of the <see cref="QuantoTermStructure"/> class.</summary>
    /// <param name="underlyingDividendTS">The underlying's dividend curve.</param>
    /// <param name="riskFreeTS">The domestic risk-free curve.</param>
    /// <param name="foreignRiskFreeTS">The foreign risk-free curve.</param>
    /// <param name="underlyingBlackVolTS">The underlying's Black volatility.</param>
    /// <param name="strike">The strike the underlying's volatility is read at.</param>
    /// <param name="exchRateBlackVolTS">The exchange rate's Black volatility.</param>
    /// <param name="exchRateATMlevel">The level the exchange rate's volatility is read at.</param>
    /// <param name="underlyingExchRateCorrelation">The correlation of the underlying and the exchange rate.</param>
    /// <exception cref="ArgumentNullException">A structure is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The structures don't share one day counter.</exception>
    public QuantoTermStructure(
        YieldTermStructure underlyingDividendTS,
        YieldTermStructure riskFreeTS,
        YieldTermStructure foreignRiskFreeTS,
        BlackVolTermStructure underlyingBlackVolTS,
        double strike,
        BlackVolTermStructure exchRateBlackVolTS,
        double exchRateATMlevel,
        double underlyingExchRateCorrelation)
        : base(
            DividendCurveOf(underlyingDividendTS).ReferenceDate,
            underlyingDividendTS.Calendar,
            underlyingDividendTS.DayCounter,
            underlyingDividendTS.SettlementDays)
    {
        ArgumentNullException.ThrowIfNull(riskFreeTS);
        ArgumentNullException.ThrowIfNull(foreignRiskFreeTS);
        ArgumentNullException.ThrowIfNull(underlyingBlackVolTS);
        ArgumentNullException.ThrowIfNull(exchRateBlackVolTS);
        DayCounter dc = underlyingDividendTS.DayCounter;
        CheckDayCounter(riskFreeTS.DayCounter, dc, nameof(riskFreeTS));
        CheckDayCounter(foreignRiskFreeTS.DayCounter, dc, nameof(foreignRiskFreeTS));
        CheckDayCounter(underlyingBlackVolTS.DayCounter, dc, nameof(underlyingBlackVolTS));
        CheckDayCounter(exchRateBlackVolTS.DayCounter, dc, nameof(exchRateBlackVolTS));
        this.underlyingDividendTS = underlyingDividendTS;
        this.riskFreeTS = riskFreeTS;
        this.foreignRiskFreeTS = foreignRiskFreeTS;
        this.underlyingBlackVolTS = underlyingBlackVolTS;
        this.exchRateBlackVolTS = exchRateBlackVolTS;
        this.underlyingExchRateCorrelation = underlyingExchRateCorrelation;
        this.strike = strike;
        this.exchRateATMlevel = exchRateATMlevel;
    }

    /// <inheritdoc/>
    public override Date MaxDate
    {
        get
        {
            Date maxDate = Min(underlyingDividendTS.MaxDate, riskFreeTS.MaxDate);
            maxDate = Min(maxDate, foreignRiskFreeTS.MaxDate);
            maxDate = Min(maxDate, underlyingBlackVolTS.MaxDate);
            maxDate = Min(maxDate, exchRateBlackVolTS.MaxDate);
            return maxDate;
        }
    }

    /// <inheritdoc/>
    protected override double ZeroYieldImpl(double t) =>
        underlyingDividendTS.ZeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true).Rate
        + riskFreeTS.ZeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true).Rate
        - foreignRiskFreeTS.ZeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true).Rate
        + (underlyingExchRateCorrelation
            * underlyingBlackVolTS.BlackVol(t, strike, true)
            * exchRateBlackVolTS.BlackVol(t, exchRateATMlevel, true));

    private static YieldTermStructure DividendCurveOf(YieldTermStructure underlyingDividendTS)
    {
        ArgumentNullException.ThrowIfNull(underlyingDividendTS);
        return underlyingDividendTS;
    }

    private static void CheckDayCounter(DayCounter dc, DayCounter expected, string paramName)
    {
        if (dc != expected)
        {
            throw new ArgumentException("all term structures must share one day counter", paramName);
        }
    }

    private static Date Min(Date a, Date b) => b < a ? b : a;
}
