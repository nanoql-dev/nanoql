// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmquantohelper.hpp ql/methods/finitedifferences/utilities/fdmquantohelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The market data a quanto option's finite-difference operator needs for its drift adjustment: the domestic and
/// foreign curves, the exchange rate's volatility, its at-the-money level and its correlation with the equity.
/// </summary>
/// <remarks>Immutable.</remarks>
public sealed class FdmQuantoHelper
{
    /// <summary>Initializes a new instance of the <see cref="FdmQuantoHelper"/> class.</summary>
    /// <param name="rTS">The domestic risk-free curve.</param>
    /// <param name="fTS">The foreign risk-free curve.</param>
    /// <param name="fxVolTS">The exchange rate's Black volatility.</param>
    /// <param name="equityFxCorrelation">The correlation of the equity with the exchange rate.</param>
    /// <param name="exchRateATMlevel">The exchange rate's at-the-money level, the strike its volatility is read
    /// at.</param>
    /// <exception cref="ArgumentNullException">A curve or the volatility is <see langword="null"/>.</exception>
    public FdmQuantoHelper(
        YieldTermStructure rTS,
        YieldTermStructure fTS,
        BlackVolTermStructure fxVolTS,
        double equityFxCorrelation,
        double exchRateATMlevel)
    {
        ArgumentNullException.ThrowIfNull(rTS);
        ArgumentNullException.ThrowIfNull(fTS);
        ArgumentNullException.ThrowIfNull(fxVolTS);
        RTS = rTS;
        FTS = fTS;
        FxVolTS = fxVolTS;
        EquityFxCorrelation = equityFxCorrelation;
        ExchRateATMlevel = exchRateATMlevel;
    }

    /// <summary>Gets the domestic risk-free curve.</summary>
    public YieldTermStructure RTS { get; }

    /// <summary>Gets the foreign risk-free curve.</summary>
    public YieldTermStructure FTS { get; }

    /// <summary>Gets the exchange rate's Black volatility.</summary>
    public BlackVolTermStructure FxVolTS { get; }

    /// <summary>Gets the correlation of the equity with the exchange rate.</summary>
    public double EquityFxCorrelation { get; }

    /// <summary>Gets the exchange rate's at-the-money level.</summary>
    public double ExchRateATMlevel { get; }

    /// <summary>The quanto adjustment of the drift over [t1, t2] at an equity volatility.</summary>
    /// <param name="equityVol">The equity's volatility.</param>
    /// <param name="t1">The start time.</param>
    /// <param name="t2">The end time.</param>
    /// <returns>The domestic less the foreign forward rate, plus the volatilities' product times the
    /// correlation.</returns>
    public double QuantoAdjustment(double equityVol, double t1, double t2)
    {
        double rDomestic = RTS.ContinuousForwardRate(t1, t2);
        double rForeign = FTS.ContinuousForwardRate(t1, t2);
        double fxVol = FxVolTS.BlackForwardVol(t1, t2, ExchRateATMlevel);

        return rDomestic - rForeign + (equityVol * fxVol * EquityFxCorrelation);
    }

    /// <summary>The quanto adjustment of the drift over [t1, t2] at each of several equity volatilities.</summary>
    /// <param name="equityVol">The equity's volatilities.</param>
    /// <param name="t1">The start time.</param>
    /// <param name="t2">The end time.</param>
    /// <returns>A new vector of the adjustments, one per volatility.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="equityVol"/> is <see langword="null"/>.</exception>
    public Vector QuantoAdjustment(Vector equityVol, double t1, double t2)
    {
        ArgumentNullException.ThrowIfNull(equityVol);
        var retVal = new Vector(equityVol.Size);
        QuantoAdjustment(equityVol.AsSpan(), t1, t2, retVal.AsSpan());
        return retVal;
    }

    /// <summary>
    /// The quanto adjustment of the drift over [t1, t2] at each of several equity volatilities, written into the
    /// caller's span (the in-place form the operator's time step uses).
    /// </summary>
    /// <param name="equityVol">The equity's volatilities.</param>
    /// <param name="t1">The start time.</param>
    /// <param name="t2">The end time.</param>
    /// <param name="result">The adjustments, one per volatility; it may be <paramref name="equityVol"/> itself.</param>
    public void QuantoAdjustment(ReadOnlySpan<double> equityVol, double t1, double t2, Span<double> result)
    {
        double rDomestic = RTS.ContinuousForwardRate(t1, t2);
        double rForeign = FTS.ContinuousForwardRate(t1, t2);
        double fxVol = FxVolTS.BlackForwardVol(t1, t2, ExchRateATMlevel);

        for (int i = 0; i < equityVol.Length; ++i)
        {
            result[i] = rDomestic - rForeign + (equityVol[i] * fxVol * EquityFxCorrelation);
        }
    }

    // The adjustment over the instants two times on another structure's clock denote, the operator's and the mesher's
    // (TermStructure.OwnTime): each structure read on its own clock, the rates and the exchange rate's volatility per
    // unit of the clock's time; QuantLib's where every structure shares the clock's times.
    internal void QuantoAdjustmentAt(
        TermStructure clock, ReadOnlySpan<double> equityVol, double t1, double t2, Span<double> result)
    {
        if (RTS.SharesTimes(clock) && FTS.SharesTimes(clock) && FxVolTS.SharesTimes(clock))
        {
            QuantoAdjustment(equityVol, t1, t2, result);
            return;
        }

        double rDomestic = RTS.ContinuousForwardRateAt(clock, t1, t2);
        double rForeign = FTS.ContinuousForwardRateAt(clock, t1, t2);
        double fxVol = Math.Sqrt(FxVolTS.BlackForwardVarianceAt(clock, t1, t2, ExchRateATMlevel) / (t2 - t1));

        for (int i = 0; i < equityVol.Length; ++i)
        {
            result[i] = rDomestic - rForeign + (equityVol[i] * fxVol * EquityFxCorrelation);
        }
    }

    // The foreign forward rate and the exchange rate's volatility over the instants two times on another structure's
    // clock denote, QuantoAdjustmentAt's reads: for an engine whose domestic rate is its own state, the CIR engine's.
    internal (double RForeign, double FxVol) ForeignAt(TermStructure clock, double t1, double t2)
    {
        if (FTS.SharesTimes(clock) && FxVolTS.SharesTimes(clock))
        {
            return (FTS.ContinuousForwardRate(t1, t2), FxVolTS.BlackForwardVol(t1, t2, ExchRateATMlevel));
        }

        return (
            FTS.ContinuousForwardRateAt(clock, t1, t2),
            Math.Sqrt(FxVolTS.BlackForwardVarianceAt(clock, t1, t2, ExchRateATMlevel) / (t2 - t1)));
    }
}
