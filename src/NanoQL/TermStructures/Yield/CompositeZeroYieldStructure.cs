// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2017 Francois Botha
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/compositezeroyieldstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A zero-yield term structure composed of two curves: its zero rate is a function of the two curves' zero rates, in
/// the given compounding and frequency.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's class template over its binary function takes a delegate here. Its reference date and day counter are the
/// first curve's, read when it is built, and so is its extrapolation flag, set when both curves extrapolate; its
/// calendar, settlement days, maximum date and maximum time are the first curve's, read when they are asked for. Curves
/// that change make a new curve: QuantLib's curve follows its handles.
/// </para>
/// <para>
/// QuantLib reads the second curve's zero rate at the composite's time, a time on the first curve's clock, which the
/// second curve counts from its own reference date on its own day counter. Here the second curve is read at the instant
/// that time denotes: its zero rate is the one implied over the composite's time by its discount from the composite's
/// reference date to that instant, none accruing before its own reference date, the second curve restated on the
/// first's clock. Where the two curves share their reference date and day counter, as QuantLib's case builds them, it is
/// QuantLib's read and bits. A documented divergence.
/// </para>
/// </remarks>
public sealed class CompositeZeroYieldStructure : ZeroYieldStructure
{
    private readonly YieldTermStructure curve1;
    private readonly YieldTermStructure curve2;
    private readonly Func<double, double, double> f;
    private readonly Compounding comp;
    private readonly Frequency freq;

    /// <summary>Initializes a new instance of the <see cref="CompositeZeroYieldStructure"/> class.</summary>
    /// <param name="h1">The first curve, which gives the reference date, the day counter and the range.</param>
    /// <param name="h2">The second curve.</param>
    /// <param name="f">The function of the two zero rates, the first curve's first.</param>
    /// <param name="comp">The compounding the zero rates are read and composed in.</param>
    /// <param name="freq">The frequency the zero rates are read and composed in, which a compounded rule needs.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="h1"/>, <paramref name="h2"/> or <paramref name="f"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public CompositeZeroYieldStructure(
        YieldTermStructure h1,
        YieldTermStructure h2,
        Func<double, double, double> f,
        Compounding comp = Compounding.Continuous,
        Frequency freq = Frequency.NoFrequency)
        : base(First(h1).ReferenceDate, null, h1.DayCounter)
    {
        ArgumentNullException.ThrowIfNull(h2);
        ArgumentNullException.ThrowIfNull(f);
        EnumArguments.Check(comp, nameof(comp));
        EnumArguments.Check(freq, nameof(freq));
        curve1 = h1;
        curve2 = h2;
        this.f = f;
        this.comp = comp;
        this.freq = freq;
        AllowsExtrapolation = curve1.AllowsExtrapolation && curve2.AllowsExtrapolation;
    }

    /// <summary>Gets the first curve's calendar, read when asked for.</summary>
    public override Calendar? Calendar => curve1.Calendar;

    /// <summary>Gets the first curve's settlement days, read when asked for.</summary>
    public override int? SettlementDays => curve1.SettlementDays;

    /// <summary>Gets the first curve's maximum date, read when asked for.</summary>
    public override Date MaxDate => curve1.MaxDate;

    /// <summary>Gets the first curve's maximum time, read when asked for.</summary>
    public override double MaxTime => curve1.MaxTime;

    /// <summary>The composite zero yield at a time, continuously compounded.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The zero yield.</returns>
    /// <exception cref="ArgumentException">The compounding is compounded with no frequency or <see cref="Frequency.Once"/>.</exception>
    protected override double ZeroYieldImpl(double t)
    {
        double zeroRate1 = curve1.ZeroRate(t, comp, freq, true).Rate;
        double zeroRate2 = curve2.SharesTimes(this)
            ? curve2.ZeroRate(t, comp, freq, true).Rate
            : InterestRate.ImpliedRate(
                curve2.DiscountAt(this, 0.0, true) / curve2.DiscountAt(this, t, true), DayCounter, comp, freq, t).Rate;

        var compositeRate = new InterestRate(f(zeroRate1, zeroRate2), DayCounter, comp, freq);
        return compositeRate.EquivalentRate(Compounding.Continuous, Frequency.NoFrequency, t).Rate;
    }

    // The first curve, refused when null before the base constructor reads it.
    private static YieldTermStructure First(YieldTermStructure h1)
    {
        ArgumentNullException.ThrowIfNull(h1);
        return h1;
    }
}
