// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A local volatility restated on another structure's clock: dated at the clock's reference date, on its day counter,
/// its value at a time the local volatility's at the instant the time denotes, its time-0 value before its own reference
/// date (<see cref="TermStructure.AccrualTime"/>). For the stochastic-local volatility models, whose times are one clock's
/// (the S-hes plan's HR7 (xxi)): built on it, the local volatility's density calculator reads the curves at the same
/// instants as the models. It extrapolates where the local volatility does, as QuantLib's models read it.
/// </summary>
internal sealed class LocalVolTermStructureOnClock : LocalVolTermStructure
{
    private readonly LocalVolTermStructure localVol;
    private readonly TermStructure clock;

    /// <summary>Initializes a new instance of the <see cref="LocalVolTermStructureOnClock"/> class.</summary>
    /// <param name="localVol">The local volatility.</param>
    /// <param name="clock">The clock it is restated on.</param>
    internal LocalVolTermStructureOnClock(LocalVolTermStructure localVol, TermStructure clock)
        : base(clock.ReferenceDate, localVol.Calendar, localVol.BusinessDayConvention, clock.DayCounter)
    {
        this.localVol = localVol;
        this.clock = clock;
        AllowsExtrapolation = localVol.AllowsExtrapolation;
    }

    /// <inheritdoc/>
    public override Date MaxDate => localVol.MaxDate;

    /// <inheritdoc/>
    public override double MinStrike() => localVol.MinStrike();

    /// <inheritdoc/>
    public override double MaxStrike() => localVol.MaxStrike();

    /// <inheritdoc/>
    protected override double LocalVolImpl(double t, double strike) =>
        localVol.LocalVol(localVol.AccrualTime(clock, t), strike, true);
}
