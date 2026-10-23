// SPDX-FileCopyrightText: Copyright (C) 2020, 2025 Marcin Rybacki
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/ultimateforwardtermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The Dutch regulatory term structure for pension funds: another curve, the original, up to a first smoothing point,
/// and past it a forward rate extrapolated from the last liquid forward rate towards an ultimate forward rate, for
/// discounting long-dated liabilities.
/// </summary>
/// <remarks>
/// <para>
/// Past the cut-off point T_c, the first smoothing point, the forward rate from T_c to T is
/// UFR + (LLFR − UFR)·B(T − T_c), with B(x) = (1 − e^(−αx))/(αx) and α the growth factor. The documentation is the
/// Dutch Central Bank's: the FTK term structure documentation (Financieel toetsingskader,
/// https://www.dnb.nl/media/4lmprzrk/vaststelling_methode_rentetermijnstructuur_ftk.pdf), the UFR 2013-2019 term
/// structure documentation (https://www.dnb.nl/media/0vmbxaf4/methodologie-dnb.pdf) and the UFR 2023 term structure
/// documentation (p. 46, https://www.tweedekamer.nl/downloads/document?id=2022D50944). The zero rates may be rounded to
/// a number of decimal places in a given compounding: rounding a rate of 1.5555% to 5 places gives 0.01556, 1.556%.
/// </para>
/// <para>
/// QuantLib's curve derives from <c>RelativeDerivedYieldTermStructure&lt;ZeroYieldStructure&gt;</c>, a class template
/// over its own base, which C# can't express; it implements the template's members itself, as
/// <see cref="ZeroSpreadedTermStructure"/> does. Its reference date and day counter are the original's, read when it
/// is built, and so is its extrapolation flag; its calendar and settlement days are the original's, read when they are
/// asked for. Its maximum date is the latest date. Quotes or an original that change make a new curve: QuantLib's curve
/// follows its quotes and its handle. The rounding is built with the curve, so a precision it refuses refuses the curve.
/// </para>
/// <para>
/// Two documented divergences. QuantLib's B(x) is unguarded: α = 0 gives 0/0 and the curve throws "positive compound
/// factor required", and α = 1e-12 puts the 60-year zero rate 9e-9 below its limit; here it is −expm1(−αx)/(αx), and 1
/// at αx = 0, its limit, as upstream's own <c>oneMinusExpOverX</c> after the pin. QuantLib reads the original without
/// extrapolation (<c>ultimateforwardtermstructure.hpp:168</c>, <c>:173</c>), so past a base that doesn't extrapolate
/// the curve throws although its maximum date is the latest date, where the zero- and forward-spreaded curves
/// extrapolate; here it reads the original with extrapolation on, as theirs do.
/// </para>
/// </remarks>
public sealed class UltimateForwardTermStructure : ZeroYieldStructure
{
    private readonly YieldTermStructure originalCurve;
    private readonly double llfr;
    private readonly double ufr;
    private readonly Period fsp;
    private readonly double alpha;
    private readonly Rounding? rounding;
    private readonly Compounding compounding;
    private readonly Frequency frequency;

    /// <summary>Initializes a new instance of the <see cref="UltimateForwardTermStructure"/> class.</summary>
    /// <param name="originalCurve">The curve read up to the first smoothing point.</param>
    /// <param name="lastLiquidForwardRate">The last liquid forward rate, LLFR.</param>
    /// <param name="ultimateForwardRate">The ultimate forward rate, UFR.</param>
    /// <param name="firstSmoothingPoint">The first smoothing point, the cut-off from the reference date.</param>
    /// <param name="alpha">The growth factor α.</param>
    /// <param name="roundingDigits">The decimal places the zero rates are rounded to, or <see langword="null"/> for none.</param>
    /// <param name="compounding">The compounding the zero rates are rounded in.</param>
    /// <param name="frequency">The frequency the zero rates are rounded in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="originalCurve"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The first smoothing point's length isn't positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The rounding digits are outside [0, 16], or the compounding or the frequency isn't a member of its enum.
    /// </exception>
    public UltimateForwardTermStructure(
        YieldTermStructure originalCurve,
        double lastLiquidForwardRate,
        double ultimateForwardRate,
        Period firstSmoothingPoint,
        double alpha,
        int? roundingDigits = null,
        Compounding compounding = Compounding.Compounded,
        Frequency frequency = Frequency.Annual)
        : base(Original(originalCurve).ReferenceDate, null, originalCurve.DayCounter)
    {
        EnumArguments.Check(compounding, nameof(compounding));
        EnumArguments.Check(frequency, nameof(frequency));
        if (!(firstSmoothingPoint.Length > 0))
        {
            throw new ArgumentException(
                "first smoothing point must be a period with positive length", nameof(firstSmoothingPoint));
        }

        this.originalCurve = originalCurve;
        llfr = lastLiquidForwardRate;
        ufr = ultimateForwardRate;
        fsp = firstSmoothingPoint;
        this.alpha = alpha;
        rounding = roundingDigits is int digits ? new ClosestRounding(digits) : null;
        this.compounding = compounding;
        this.frequency = frequency;
        AllowsExtrapolation = originalCurve.AllowsExtrapolation;
    }

    /// <summary>Gets the original curve's calendar, read when asked for.</summary>
    public override Calendar? Calendar => originalCurve.Calendar;

    /// <summary>Gets the original curve's settlement days, read when asked for.</summary>
    public override int? SettlementDays => originalCurve.SettlementDays;

    /// <summary>Gets the latest date, <see cref="Date.MaxDate"/>.</summary>
    public override Date MaxDate => Date.MaxDate;

    /// <summary>The zero yield at a time, continuously compounded: the original's to the cut-off, extrapolated past it.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The zero yield.</returns>
    /// <exception cref="ArgumentException">
    /// The rounding's compounding is compounded with no frequency or <see cref="Frequency.Once"/>.
    /// </exception>
    protected override double ZeroYieldImpl(double t)
    {
        double cutOffTime = originalCurve.TimeFromReference(ReferenceDate + fsp);
        double deltaT = t - cutOffTime;

        // If time to maturity (T) exceeds the cut-off point (T_c), i.e. the first smoothing point, the forward rate f is
        // extrapolated as follows:
        //   f(t,T_c,T) = UFR(t) + (LLFR(t) - UFR(t)) * B(T-T_c),
        // where UFR(t) is the Ultimate Forward Rate quote, LLFR(t) the Last Liquid Forward Rate quote, and
        //   B(t-T_c) = [1 - exp(-a * (T-T_c))] / [a * (T-T_c)],
        // with a being the growth factor (alpha).
        if (deltaT > 0.0)
        {
            InterestRate baseRate = originalCurve.ZeroRate(cutOffTime, Compounding.Continuous, Frequency.NoFrequency, true);
            double beta = OneMinusExpOverX(alpha * deltaT);
            double extrapolatedForward = ufr + ((llfr - ufr) * beta);
            return ApplyRounding(((cutOffTime * baseRate.Rate) + (deltaT * extrapolatedForward)) / t, t);
        }

        return ApplyRounding(originalCurve.ZeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true).Rate, t);
    }

    // (1 - exp(-x))/x without its cancellation at small x, and 1 at 0, its limit: upstream's oneMinusExpOverX.
    private static double OneMinusExpOverX(double x) => x == 0.0 ? 1.0 : -Expm1Functions.StdExpm1(-x) / x;

    // The original, refused when null before the base constructor reads it.
    private static YieldTermStructure Original(YieldTermStructure originalCurve)
    {
        ArgumentNullException.ThrowIfNull(originalCurve);
        return originalCurve;
    }

    // The rounding of a continuously compounded zero rate in the curve's compounding.
    private double ApplyRounding(double r, double t)
    {
        if (rounding is null)
        {
            return r;
        }

        // Input rate is continuously compounded by definition. Hence, in case this is also the selected compounding
        // method for rounding, it is not required to calculate equivalent rates, and rounding may be applied directly.
        double equivalentRate = compounding == Compounding.Continuous
            ? r
            : new InterestRate(r, DayCounter, Compounding.Continuous, Frequency.NoFrequency)
                .EquivalentRate(compounding, frequency, t).Rate;
        double rounded = rounding.Evaluate(equivalentRate);
        return compounding == Compounding.Continuous
            ? rounded
            : new InterestRate(rounded, DayCounter, compounding, frequency)
                .EquivalentRate(Compounding.Continuous, Frequency.NoFrequency, t).Rate;
    }
}
