// SPDX-FileCopyrightText: Copyright (C) 2019 Quaternion Risk Management Ltd
// SPDX-FileCopyrightText: Copyright (C) 2022 Skandinaviska Enskilda Banken AB (publ)
// SPDX-FileCopyrightText: Copyright (C) 2025 Paolo D'Elia
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/equityfx/blackvolsurfacedelta.hpp ql/termstructures/volatility/equityfx/blackvolsurfacedelta.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Quotes;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A Black volatility surface quoted by delta: put deltas, call deltas and optionally an at-the-money quote at each
/// expiry, each delta's volatilities a <see cref="BlackVarianceCurve"/> in time, its smile at a time the deltas
/// converted to strikes on the spot and the domestic and foreign curves under the delta and at-the-money conventions,
/// interpolated in strike.
/// </summary>
/// <remarks>
/// <para>
/// Immutable; its smile is built at each query, as QuantLib builds it.
/// </para>
/// <para>
/// Its curves are read at the instants its times denote, each on its own clock, where QuantLib reads them at the
/// surface's own times (<c>blackvolsurfacedelta.cpp:96-97</c>, <c>:210</c>): curves on Actual/360 under a surface on
/// Actual/365 (Fixed) gave the strikes of curves on Actual/365 (Fixed). Where the curves share the surface's day
/// counter and reference date, QuantLib's bits stand.
/// </para>
/// <para>
/// Its smile section's at-the-money level is the surface's forward, <see cref="AtmLevel"/>, where QuantLib passes the
/// at-the-money volatility, or 1 without an at-the-money quote (<c>blackvolsurfacedelta.cpp:118</c>, <c>:135</c>),
/// which the section's option prices, vega, digital prices and density read as the forward. Its volatilities are
/// QuantLib's. A documented divergence.
/// </para>
/// </remarks>
public sealed class BlackVolatilitySurfaceDelta : BlackVolatilityTermStructure
{
    // QuantLib's Null<Real>(), a strike read as at the money
    private const double NullReal = 3.4028234663852886e38;

    private readonly ImmutableArray<Date> dates;
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> putDeltas;
    private readonly ImmutableArray<double> callDeltas;
    private readonly bool hasAtm;
    private readonly ImmutableArray<BlackVarianceCurve> interpolators;
    private readonly double spot;
    private readonly YieldTermStructure domesticTS;
    private readonly YieldTermStructure foreignTS;
    private readonly DeltaVolQuoteDeltaType deltaType;
    private readonly DeltaVolQuoteAtmType atmType;
    private readonly DeltaVolQuoteDeltaType atmDeltaType;
    private readonly BlackVolatilitySurfaceDeltaSmileInterpolationMethod interpolationMethod;
    private readonly bool flatStrikeExtrapolation;
    private readonly BlackVolTimeExtrapolationType timeExtrapolationType;
    private readonly DeltaVolQuoteDeltaType longTermDeltaType;
    private readonly DeltaVolQuoteAtmType longTermAtmType;
    private readonly DeltaVolQuoteDeltaType longTermAtmDeltaType;
    private readonly double switchTime;

    /// <summary>Initializes a new instance of the <see cref="BlackVolatilitySurfaceDelta"/> class.</summary>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dates">The expiries, increasing and after the reference date; at least two.</param>
    /// <param name="putDeltas">The put deltas of the matrix's first columns.</param>
    /// <param name="callDeltas">The call deltas of its last columns.</param>
    /// <param name="hasAtm">Whether an at-the-money column sits between them.</param>
    /// <param name="blackVolMatrix">The volatilities, one row per expiry and one column per delta.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <param name="cal">The calendar, or <see langword="null"/> for QuantLib's empty one.</param>
    /// <param name="spot">The spot.</param>
    /// <param name="domesticTS">The domestic curve.</param>
    /// <param name="foreignTS">The foreign curve.</param>
    /// <param name="deltaType">The delta convention.</param>
    /// <param name="atmType">The at-the-money convention.</param>
    /// <param name="atmDeltaType">The at-the-money quote's delta convention; <see langword="null"/> for
    /// <paramref name="deltaType"/>.</param>
    /// <param name="interpolationMethod">The smile's interpolation in strike.</param>
    /// <param name="flatStrikeExtrapolation">Whether the smile is flat outside its strikes.</param>
    /// <param name="timeExtrapolationType">How the volatilities extrapolate past the last expiry.</param>
    /// <param name="switchTenor">The tenor from which the long-term conventions apply; <see langword="null"/>, QuantLib's
    /// 0 days, for none.</param>
    /// <param name="longTermDeltaType">The long-term delta convention.</param>
    /// <param name="longTermAtmType">The long-term at-the-money convention.</param>
    /// <param name="longTermAtmDeltaType">The long-term at-the-money quote's delta convention; <see langword="null"/>
    /// for <paramref name="longTermDeltaType"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">
    /// Fewer than two dates are given, a date isn't after the reference date, the dates aren't sorted and unique, no
    /// deltas are given, or the matrix's size doesn't match them; with QuantLib's messages.
    /// </exception>
    /// <exception cref="NanoQLException">A switch tenor is given and the surface has no calendar.</exception>
    public BlackVolatilitySurfaceDelta(
        Date referenceDate,
        IEnumerable<Date> dates,
        IEnumerable<double> putDeltas,
        IEnumerable<double> callDeltas,
        bool hasAtm,
        Matrix blackVolMatrix,
        DayCounter dayCounter,
        Calendar? cal,
        double spot,
        YieldTermStructure domesticTS,
        YieldTermStructure foreignTS,
        DeltaVolQuoteDeltaType deltaType = DeltaVolQuoteDeltaType.Spot,
        DeltaVolQuoteAtmType atmType = DeltaVolQuoteAtmType.AtmDeltaNeutral,
        DeltaVolQuoteDeltaType? atmDeltaType = null,
        BlackVolatilitySurfaceDeltaSmileInterpolationMethod interpolationMethod =
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.Linear,
        bool flatStrikeExtrapolation = false,
        BlackVolTimeExtrapolationType timeExtrapolationType = BlackVolTimeExtrapolationType.FlatVolatility,
        Period? switchTenor = null,
        DeltaVolQuoteDeltaType longTermDeltaType = DeltaVolQuoteDeltaType.Fwd,
        DeltaVolQuoteAtmType longTermAtmType = DeltaVolQuoteAtmType.AtmDeltaNeutral,
        DeltaVolQuoteDeltaType? longTermAtmDeltaType = null)
        : base(referenceDate, cal, BusinessDayConvention.Following, dayCounter)
    {
        ArgumentNullException.ThrowIfNull(dates);
        ArgumentNullException.ThrowIfNull(putDeltas);
        ArgumentNullException.ThrowIfNull(callDeltas);
        ArgumentNullException.ThrowIfNull(blackVolMatrix);
        ArgumentNullException.ThrowIfNull(domesticTS);
        ArgumentNullException.ThrowIfNull(foreignTS);
        EnumArguments.Check(deltaType, nameof(deltaType));
        EnumArguments.Check(atmType, nameof(atmType));
        if (atmDeltaType is { } adt)
        {
            EnumArguments.Check(adt, nameof(atmDeltaType));
        }

        EnumArguments.Check(interpolationMethod, nameof(interpolationMethod));
        EnumArguments.Check(timeExtrapolationType, nameof(timeExtrapolationType));
        EnumArguments.Check(longTermDeltaType, nameof(longTermDeltaType));
        EnumArguments.Check(longTermAtmType, nameof(longTermAtmType));
        if (longTermAtmDeltaType is { } ltadt)
        {
            EnumArguments.Check(ltadt, nameof(longTermAtmDeltaType));
        }

        Date[] ds = [.. dates];
        this.dates = [.. ds];
        this.putDeltas = [.. putDeltas];
        this.callDeltas = [.. callDeltas];
        this.hasAtm = hasAtm;
        this.spot = spot;
        this.domesticTS = domesticTS;
        this.foreignTS = foreignTS;
        this.deltaType = deltaType;
        this.atmType = atmType;
        this.atmDeltaType = atmDeltaType ?? deltaType;
        this.interpolationMethod = interpolationMethod;
        this.flatStrikeExtrapolation = flatStrikeExtrapolation;
        this.timeExtrapolationType = timeExtrapolationType;
        this.longTermDeltaType = longTermDeltaType;
        this.longTermAtmType = longTermAtmType;
        this.longTermAtmDeltaType = longTermAtmDeltaType ?? longTermDeltaType;

        // set switch time
        Period tenor = switchTenor ?? new Period(0, TimeUnit.Days);
        switchTime = tenor == new Period(0, TimeUnit.Days) ? double.MaxValue : TimeFromReference(OptionDateFromTenor(tenor));

        if (!(ds.Length > 1))
        {
            throw new ArgumentException("at least 1 date required", nameof(dates));
        }

        // this has already been done for dates
        var t = new double[ds.Length];
        for (int i = 0; i < ds.Length; i++)
        {
            if (!(referenceDate < ds[i]))
            {
                throw new ArgumentException("Dates must be greater than reference date", nameof(dates));
            }

            t[i] = TimeFromReference(ds[i]);
            if (i > 0 && !(t[i] > t[i - 1]))
            {
                throw new ArgumentException("dates must be sorted unique!", nameof(dates));
            }
        }

        times = [.. t];

        // check size of matrix
        int n = this.putDeltas.Length + (hasAtm ? 1 : 0) + this.callDeltas.Length;
        if (!(n > 0))
        {
            throw new ArgumentException("Need at least one delta", nameof(blackVolMatrix));
        }

        if (blackVolMatrix.Columns != n)
        {
            throw new ArgumentException(
                Invariant($"Invalid number of columns in blackVolMatrix, got {blackVolMatrix.Columns} but have {n} deltas"),
                nameof(blackVolMatrix));
        }

        if (blackVolMatrix.Rows != ds.Length)
        {
            throw new ArgumentException(
                Invariant($"Invalid number of rows in blackVolMatrix, got {blackVolMatrix.Rows} but have {ds.Length} dates"),
                nameof(blackVolMatrix));
        }

        // build interpolators for each delta
        const bool ForceMonotoneVariance = false;
        var curves = ImmutableArray.CreateBuilder<BlackVarianceCurve>(n);
        for (int i = 0; i < n; i++)
        {
            var vols = new double[ds.Length];
            for (int j = 0; j < ds.Length; j++)
            {
                vols[j] = blackVolMatrix[j, i];
            }

            curves.Add(new BlackVarianceCurve(referenceDate, ds, vols, dayCounter, ForceMonotoneVariance, timeExtrapolationType));
        }

        interpolators = curves.MoveToImmutable();
    }

    /// <summary>Gets the latest date: QuantLib's <c>Date::maxDate()</c>.</summary>
    public override Date MaxDate => Date.MaxDate;

    /// <summary>Gets the expiries.</summary>
    public ImmutableArray<Date> Dates => dates;

    /// <summary>The lowest strike, 0.</summary>
    /// <returns>0.</returns>
    public override double MinStrike() => 0.0;

    /// <summary>The highest strike, unbounded: QuantLib's <c>QL_MAX_REAL</c>.</summary>
    /// <returns>1.7976931348623157e308.</returns>
    public override double MaxStrike() => double.MaxValue;

    /// <summary>The forward at a time: the spot times the foreign discount over the domestic one, each read at the
    /// instant the time denotes on its own clock.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The forward.</returns>
    /// <exception cref="NanoQLException">A curve can't give its discount there.</exception>
    public override double? AtmLevel(double t) => Forward(t);

    /// <summary>The smile at a time: the deltas' strikes and volatilities there, interpolated in strike.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The smile section, its at-the-money level the forward.</returns>
    /// <exception cref="NanoQLException">A strike can't be found from its delta, or a curve can't give its
    /// discount.</exception>
    public SmileSection BlackVolSmile(double t)
    {
        double dDiscount = domesticTS.DiscountAt(this, t);
        double fDiscount = foreignTS.DiscountAt(this, t);
        double sqrtT = Math.Sqrt(t);

        DeltaVolQuoteAtmType at;
        DeltaVolQuoteDeltaType dt;
        DeltaVolQuoteDeltaType atmDt;
        if (t < switchTime && !ComparisonFunctions.CloseEnough(t, switchTime))
        {
            at = atmType;
            dt = deltaType;
            atmDt = atmDeltaType;
        }
        else
        {
            at = longTermAtmType;
            dt = longTermDeltaType;
            atmDt = longTermAtmDeltaType;
        }

        // QuantLib's map from strike to volatility, sorted by strike, a strike close to one already there not
        // overwriting it
        var strikes = new List<double>();
        var smileVols = new List<double>();
        int i = 0;

        foreach (double delta in putDeltas)
        {
            double vol = interpolators[i].BlackVol(t, 1, true);
            double strike = Strike(
                () => new BlackDeltaCalculator(OptionType.Put, dt, spot, dDiscount, fDiscount, vol * sqrtT).StrikeFromDelta(delta),
                Invariant($"put strike at delta {delta}"));
            Insert(strikes, smileVols, strike, vol);
            i++;
        }

        if (hasAtm)
        {
            double vol = interpolators[i].BlackVol(t, 1, true);
            double strike = Strike(
                () => new BlackDeltaCalculator(OptionType.Put, atmDt, spot, dDiscount, fDiscount, vol * sqrtT).AtmStrike(at),
                "atm strike");
            Insert(strikes, smileVols, strike, vol);
            i++;
        }

        foreach (double delta in callDeltas)
        {
            double vol = interpolators[i].BlackVol(t, 1, true);
            double strike = Strike(
                () => new BlackDeltaCalculator(OptionType.Call, dt, spot, dDiscount, fDiscount, vol * sqrtT).StrikeFromDelta(delta),
                Invariant($"call strike at delta {delta}"));
            Insert(strikes, smileVols, strike, vol);
            i++;
        }

        // sort and extract to vectors
        var stdDevs = new double[smileVols.Count];
        for (int k = 0; k < stdDevs.Length; k++)
        {
            stdDevs[k] = smileVols[k] * sqrtT;
        }

        // now build smile from strikes and vols
        if (stdDevs.Length == 0)
        {
            throw new NanoQLException(
                Invariant($"BlackVolatilitySurfaceDelta::blackVolSmile({t}): no strikes given, this is unexpected."));
        }

        if (stdDevs.Length == 1)
        {
            // handle the situation that we only have one strike (might occur for e.g. t=0)
            return new FlatSmileSection(t, stdDevs[0] / sqrtT, DayCounter);
        }

        // we have at least two strikes, the forward the section's at-the-money level
        double atmLevel = Forward(t);
        double[] ks = [.. strikes];
        return interpolationMethod switch
        {
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.Linear => new InterpolatedSmileSection<Linear>(
                t, ks, stdDevs, atmLevel, default, DayCounter, VolatilityType.ShiftedLognormal, 0.0, flatStrikeExtrapolation),
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.NaturalCubic => new InterpolatedSmileSection<Cubic>(
                t,
                ks,
                stdDevs,
                atmLevel,
                new Cubic(CubicInterpolationDerivativeApprox.Kruger),
                DayCounter,
                VolatilityType.ShiftedLognormal,
                0.0,
                flatStrikeExtrapolation),
            BlackVolatilitySurfaceDeltaSmileInterpolationMethod.FinancialCubic => new InterpolatedSmileSection<Cubic>(
                t,
                ks,
                stdDevs,
                atmLevel,
                new Cubic(
                    CubicInterpolationDerivativeApprox.Kruger,
                    true,
                    CubicInterpolationBoundaryCondition.SecondDerivative,
                    0.0,
                    CubicInterpolationBoundaryCondition.FirstDerivative),
                DayCounter,
                VolatilityType.ShiftedLognormal,
                0.0,
                flatStrikeExtrapolation),
            _ => new InterpolatedSmileSection<Cubic>(
                t,
                ks,
                stdDevs,
                atmLevel,
                new Cubic(CubicInterpolationDerivativeApprox.Spline),
                DayCounter,
                VolatilityType.ShiftedLognormal,
                0.0,
                flatStrikeExtrapolation),
        };
    }

    /// <summary>The smile at a date: <see cref="BlackVolSmile(double)"/> at its time.</summary>
    /// <param name="d">The date.</param>
    /// <returns>The smile section.</returns>
    /// <exception cref="ArgumentException"><paramref name="d"/> is <c>default(Date)</c>.</exception>
    /// <exception cref="NanoQLException">A strike can't be found from its delta, or a curve can't give its
    /// discount.</exception>
    public SmileSection BlackVolSmile(Date d) => BlackVolSmile(TimeFromReference(d));

    /// <summary>
    /// The volatility at a time and strike: the smile's, past the last expiry at the last expiry's time under flat
    /// extrapolation; at a strike of 0 or QuantLib's null, the at-the-money quote's, or the smile's at the forward.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected override double BlackVolImpl(double t, double strike)
    {
        // If asked for strike == 0, just return the ATM value.
        double tme = t > times[^1] && timeExtrapolationType == BlackVolTimeExtrapolationType.FlatVolatility ? times[^1] : t;

        if (strike == 0 || strike == NullReal)
        {
            if (hasAtm)
            {
                // ask the ATM interpolator directly
                return interpolators[putDeltas.Length].BlackVol(tme, NullReal, true);
            }
            else
            {
                // set strike to be fwd and we will return ATMF
                strike = Forward(tme);
            }
        }

        return BlackVolSmile(tme).Volatility(strike);
    }

    private static string Invariant(FormattableString s) => s.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // the strike from a delta calculator, its refusal raised with QuantLib's prefix
    private static double Strike(Func<double> f, string what)
    {
        try
        {
            return f();
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            throw new NanoQLException($"BlackVolatilitySurfaceDelta: Error during calculating {what}: {e.Message}", e);
        }
    }

    // std::map<Real, Real, comp>'s insertion of a new key, comp(a, b) = !close(a, b) && a < b: at its lower bound, unless
    // the key there is equivalent
    private static void Insert(List<double> strikes, List<double> vols, double strike, double vol)
    {
        int lo = 0, hi = strikes.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (Less(strikes[mid], strike))
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        if (lo < strikes.Count && !Less(strike, strikes[lo]))
        {
            return;
        }

        strikes.Insert(lo, strike);
        vols.Insert(lo, vol);
    }

    private static bool Less(double a, double b) => !ComparisonFunctions.Close(a, b) && a < b;

    // S·P_f/P_d at the instant a time denotes, each curve on its own clock
    private double Forward(double t) => spot * foreignTS.DiscountAt(this, t) / domesticTS.DiscountAt(this, t);
}
