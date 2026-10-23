// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/forwardspreadedtermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>A term structure with a spread added to the instantaneous forward rate of another curve, the original.</summary>
/// <remarks>
/// QuantLib's curve derives from <c>RelativeDerivedYieldTermStructure&lt;ZeroYieldStructure&gt;</c>, a class template
/// over its own base, which C# can't express; it implements the template's members itself, as
/// <see cref="ZeroSpreadedTermStructure"/> does. Its reference date and day counter are the original's, read when it is
/// built, and so is its extrapolation flag; its calendar, settlement days and maximum date are the original's, read when
/// they are asked for, and so are its zero rates. A constant spread on the forward rate is the same spread on the
/// continuously compounded zero rate, which the curve adds. A spread or an original that changes is a new curve:
/// QuantLib's curve follows its quote and its handle.
/// </remarks>
public sealed class ForwardSpreadedTermStructure : ZeroYieldStructure
{
    private readonly YieldTermStructure originalCurve;
    private readonly double spread;

    /// <summary>Initializes a new instance of the <see cref="ForwardSpreadedTermStructure"/> class.</summary>
    /// <param name="originalCurve">The curve the spread is added to.</param>
    /// <param name="spread">The spread on the instantaneous forward rate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="originalCurve"/> is <see langword="null"/>.</exception>
    public ForwardSpreadedTermStructure(YieldTermStructure originalCurve, double spread)
        : base(Original(originalCurve).ReferenceDate, null, originalCurve.DayCounter)
    {
        this.originalCurve = originalCurve;
        this.spread = spread;
        AllowsExtrapolation = originalCurve.AllowsExtrapolation;
    }

    /// <summary>Gets the original curve's calendar, read when asked for.</summary>
    public override Calendar? Calendar => originalCurve.Calendar;

    /// <summary>Gets the original curve's settlement days, read when asked for.</summary>
    public override int? SettlementDays => originalCurve.SettlementDays;

    /// <summary>Gets the original curve's maximum date, read when asked for.</summary>
    public override Date MaxDate => originalCurve.MaxDate;

    /// <summary>The original's continuously compounded zero rate at a time, plus the spread.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The zero yield.</returns>
    protected override double ZeroYieldImpl(double t) =>
        originalCurve.ZeroRate(t, Compounding.Continuous, Frequency.NoFrequency, true).Rate + spread;

    // The original, refused when null before the base constructor reads it.
    private static YieldTermStructure Original(YieldTermStructure originalCurve)
    {
        ArgumentNullException.ThrowIfNull(originalCurve);
        return originalCurve;
    }
}
