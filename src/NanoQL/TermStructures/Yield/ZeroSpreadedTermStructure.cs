// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/zerospreadedtermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;
using CultureInfo = System.Globalization.CultureInfo;

namespace NanoQL.TermStructures;

/// <summary>A term structure with a spread added to the zero yield rate of another curve, the original.</summary>
/// <remarks>
/// <para>
/// QuantLib's curve derives from <c>RelativeDerivedYieldTermStructure&lt;ZeroYieldStructure&gt;</c>, a class template
/// over its own base, which C# can't express; it implements the template's members itself. Its reference date and day
/// counter are the original's, read when it is built (a term structure has a fixed reference date), and so is its
/// extrapolation flag; its calendar, settlement days and maximum date are the original's, read when they are asked
/// for, and so are its zero rates. So it can be built on a <see cref="MultiCurveMember"/> before the cycle is built,
/// whose reference date and day counter it reads then, as QuantLib builds it on an empty handle.
/// </para>
/// <para>
/// The spread is added in the given compounding and frequency: the original's zero rate at a time in them, plus the
/// spread, converted to continuous compounding at that time. A spread or an original that changes is a new curve:
/// QuantLib's curve follows its quote and its handle. A compounded rule with no frequency, the default frequency,
/// builds and is refused at every query that reads a zero rate, as QuantLib's is.
/// </para>
/// <para>QuantLib's constructor that takes a day counter, which it ignores, is deprecated and not ported.</para>
/// </remarks>
public sealed class ZeroSpreadedTermStructure : ZeroYieldStructure
{
    private readonly YieldTermStructure originalCurve;
    private readonly double spread;
    private readonly Compounding comp;
    private readonly Frequency freq;

    /// <summary>Initializes a new instance of the <see cref="ZeroSpreadedTermStructure"/> class.</summary>
    /// <param name="originalCurve">The curve the spread is added to.</param>
    /// <param name="spread">The spread.</param>
    /// <param name="comp">The compounding the spread is added in.</param>
    /// <param name="freq">The frequency the spread is added in, which a compounded rule needs.</param>
    /// <exception cref="ArgumentNullException"><paramref name="originalCurve"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public ZeroSpreadedTermStructure(
        YieldTermStructure originalCurve,
        double spread,
        Compounding comp = Compounding.Continuous,
        Frequency freq = Frequency.NoFrequency)
        : base(Original(originalCurve).ReferenceDate, null, originalCurve.DayCounter)
    {
        EnumArguments.Check(comp, nameof(comp));
        EnumArguments.Check(freq, nameof(freq));
        this.originalCurve = originalCurve;
        this.spread = spread;
        this.comp = comp;
        this.freq = freq;
        AllowsExtrapolation = originalCurve.AllowsExtrapolation;
    }

    /// <summary>Gets the original curve's calendar, read when asked for.</summary>
    public override Calendar? Calendar => originalCurve.Calendar;

    /// <summary>Gets the original curve's settlement days, read when asked for.</summary>
    public override int? SettlementDays => originalCurve.SettlementDays;

    /// <summary>Gets the original curve's maximum date, read when asked for.</summary>
    public override Date MaxDate => originalCurve.MaxDate;

    /// <summary>The spreaded zero yield rate at a time, continuously compounded.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The zero yield.</returns>
    /// <exception cref="ArgumentException">The compounding is compounded with no frequency or <see cref="Frequency.Once"/>.</exception>
    protected override double ZeroYieldImpl(double t)
    {
        if (comp == Compounding.Continuous)
        {
            return ContinuousZeroYield(t);
        }

        InterestRate zeroRate = originalCurve.ZeroRate(t, comp, freq, true);
        var spreadedRate = new InterestRate(
            zeroRate.Rate + spread, zeroRate.DayCounter, zeroRate.Compounding, zeroRate.Frequency);
        return spreadedRate.EquivalentRate(Compounding.Continuous, Frequency.NoFrequency, t).Rate;
    }

    // The spreaded zero yield for a continuously compounded spread, by the InterestRate route's arithmetic and checks
    // without building the rate objects, so a finite-difference step reading the curve allocates nothing (FdmBatesOp's
    // spreaded dividend curve): ZeroRate(t, Continuous)'s log(1/D(t))/t, the time 1e-4 at t = 0, plus the spread, then
    // EquivalentRate(Continuous)'s log(exp(r·t))/t.
    private double ContinuousZeroYield(double t)
    {
        const double Dt = 0.0001;
        double tz = t == 0.0 ? Dt : t;
        double zero = ImpliedContinuous(1.0 / originalCurve.Discount(tz, true), tz);
        double rate = zero + spread;
        if (!(t >= 0.0))
        {
            throw new ArgumentException(
                $"negative time ({t.ToString(CultureInfo.InvariantCulture)}) not allowed", nameof(t));
        }

        return ImpliedContinuous(Math.Exp(rate * t), t);
    }

    // InterestRate.ImpliedRate's continuous branch, with its checks.
    private static double ImpliedContinuous(double compound, double t)
    {
        if (!(compound > 0.0))
        {
            throw new ArgumentException("positive compound factor required", nameof(compound));
        }

        if (compound == 1.0)
        {
            if (!(t >= 0.0))
            {
                throw new ArgumentException(
                    $"non negative time ({t.ToString(CultureInfo.InvariantCulture)}) required", nameof(t));
            }

            return 0.0;
        }

        if (!(t > 0.0))
        {
            throw new ArgumentException($"positive time ({t.ToString(CultureInfo.InvariantCulture)}) required", nameof(t));
        }

        return Math.Log(compound) / t;
    }

    // The original, refused when null before the base constructor reads it.
    private static YieldTermStructure Original(YieldTermStructure originalCurve)
    {
        ArgumentNullException.ThrowIfNull(originalCurve);
        return originalCurve;
    }
}
