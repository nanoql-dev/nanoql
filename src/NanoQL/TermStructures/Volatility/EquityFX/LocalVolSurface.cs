// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/localvolsurface.hpp ql/termstructures/volatility/equityfx/localvolsurface.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The local volatility surface implied by a Black volatility surface and the risk-free and dividend curves, by
/// Dupire's formula in the total variance, as in J. Gatheral, "Stochastic Volatility and Local Volatility", Case
/// Studies and Financial Modelling Course Notes, Fall 2003.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: B4.1's local-volatility surface that doesn't refuse derives from it.
/// QuantLib marks the class as untested and probably unreliable.
/// </para>
/// <para>
/// QuantLib's surface answers its reference date, day counter and maximum date from its Black surface's, which can
/// move; here the reference date and day counter are the Black surface's, passed at construction, since that
/// surface's are fixed. Its calendar is QuantLib's empty one, as QuantLib's surface leaves it. The underlying's level
/// is a number, so QuantLib's constructors taking a quote and a number coincide.
/// </para>
/// </remarks>
public class LocalVolSurface : LocalVolTermStructure
{
    private readonly BlackVolTermStructure blackTS;
    private readonly YieldTermStructure riskFreeTS;
    private readonly YieldTermStructure dividendTS;
    private readonly double underlying;

    /// <summary>Initializes a new instance of the <see cref="LocalVolSurface"/> class.</summary>
    /// <param name="blackTS">The Black volatility surface.</param>
    /// <param name="riskFreeTS">The risk-free curve.</param>
    /// <param name="dividendTS">The dividend curve.</param>
    /// <param name="underlying">The underlying's level.</param>
    /// <exception cref="ArgumentNullException">A structure is <see langword="null"/>.</exception>
    public LocalVolSurface(
        BlackVolTermStructure blackTS, YieldTermStructure riskFreeTS, YieldTermStructure dividendTS, double underlying)
        : base(SurfaceOf(blackTS).ReferenceDate, null, blackTS.BusinessDayConvention, blackTS.DayCounter)
    {
        ArgumentNullException.ThrowIfNull(riskFreeTS);
        ArgumentNullException.ThrowIfNull(dividendTS);
        this.blackTS = blackTS;
        this.riskFreeTS = riskFreeTS;
        this.dividendTS = dividendTS;
        this.underlying = underlying;
    }

    /// <inheritdoc/>
    public override Date MaxDate => blackTS.MaxDate;

    /// <inheritdoc/>
    public override double MinStrike() => blackTS.MinStrike();

    /// <inheritdoc/>
    public override double MaxStrike() => blackTS.MaxStrike();

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The total variance decreases in time, or the local variance is
    /// negative.</exception>
    protected override double LocalVolImpl(double t, double strike)
    {
        double dr = riskFreeTS.Discount(t, true);
        double dq = dividendTS.Discount(t, true);
        double forwardValue = underlying * dq / dr;

        // strike derivatives
        double y, dy, strikep, strikem;
        double w, wp, wm, dwdy, d2wdy2;
        double underlyingLevel = strike;
        y = Math.Log(underlyingLevel / forwardValue);
        dy = Math.Abs(y) > 0.001 ? y * 0.0001 : 0.000001;
        strikep = underlyingLevel * Math.Exp(dy);
        strikem = underlyingLevel / Math.Exp(dy);
        w = blackTS.BlackVariance(t, underlyingLevel, true);
        wp = blackTS.BlackVariance(t, strikep, true);
        wm = blackTS.BlackVariance(t, strikem, true);
        dwdy = (wp - wm) / (2.0 * dy);
        d2wdy2 = (wp - (2.0 * w) + wm) / (dy * dy);

        // time derivative
        double dt, wpt, wmt, dwdt;
        if (t == 0.0)
        {
            dt = 0.0001;
            double drpt = riskFreeTS.Discount(t + dt, true);
            double dqpt = dividendTS.Discount(t + dt, true);
            double strikept = underlyingLevel * dr * dqpt / (drpt * dq);

            wpt = blackTS.BlackVariance(t + dt, strikept, true);
            if (!(wpt >= w))
            {
                throw new NanoQLException(
                    $"decreasing variance at strike {Show(underlyingLevel)} between time {Show(t)} and time {Show(t + dt)}");
            }

            dwdt = (wpt - w) / dt;
        }
        else
        {
            dt = Min(0.0001, t / 2.0);
            double drpt = riskFreeTS.Discount(t + dt, true);
            double drmt = riskFreeTS.Discount(t - dt, true);
            double dqpt = dividendTS.Discount(t + dt, true);
            double dqmt = dividendTS.Discount(t - dt, true);

            double strikept = underlyingLevel * dr * dqpt / (drpt * dq);
            double strikemt = underlyingLevel * dr * dqmt / (drmt * dq);

            wpt = blackTS.BlackVariance(t + dt, strikept, true);
            wmt = blackTS.BlackVariance(t - dt, strikemt, true);

            if (!(wpt >= w))
            {
                throw new NanoQLException(
                    $"decreasing variance at strike {Show(underlyingLevel)} between time {Show(t)} and time {Show(t + dt)}");
            }

            if (!(w >= wmt))
            {
                throw new NanoQLException(
                    $"decreasing variance at strike {Show(underlyingLevel)} between time {Show(t - dt)} and time {Show(t)}");
            }

            dwdt = (wpt - wmt) / (2.0 * dt);
        }

        if (dwdy == 0.0 && d2wdy2 == 0.0)
        {
            // avoid /w where w might be 0.0
            return Math.Sqrt(dwdt);
        }
        else
        {
            double den1 = 1.0 - (y / w * dwdy);
            double den2 = 0.25 * (-0.25 - (1.0 / w) + (y * y / w / w)) * dwdy * dwdy;
            double den3 = 0.5 * d2wdy2;
            double den = den1 + den2 + den3;
            double result = dwdt / den;

            if (!(result >= 0.0))
            {
                throw new NanoQLException(
                    $"negative local vol^2 at strike {Show(underlyingLevel)} and time {Show(t)}; the black vol surface "
                    + "is not smooth enough");
            }

            return Math.Sqrt(result);
        }
    }

    private static BlackVolTermStructure SurfaceOf(BlackVolTermStructure blackTS)
    {
        ArgumentNullException.ThrowIfNull(blackTS);
        return blackTS;
    }

    private static double Min(double a, double b) => b < a ? b : a;

    private static string Show(double x) => x.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
