// SPDX-FileCopyrightText: Copyright (C) 2007 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/optionlet/strippedoptionletbase.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The base class of a vector, indexed by time, of optionlet (caplet and floorlet) volatilities indexed by strike.
/// </summary>
/// <remarks>
/// A stripped base is immutable: QuantLib's lazy calculation is done when the object is built. Its at-the-money rates
/// take the evaluation context, since <see cref="StrippedOptionlet"/>'s read the index's fixings at each call.
/// </remarks>
public abstract class StrippedOptionletBase
{
    /// <summary>Gets the number of optionlet fixing dates.</summary>
    public abstract int OptionletMaturities { get; }

    /// <summary>Gets the optionlets' fixing dates.</summary>
    public abstract ImmutableArray<Date> OptionletFixingDates { get; }

    /// <summary>Gets the optionlets' fixing times.</summary>
    public abstract ImmutableArray<double> OptionletFixingTimes { get; }

    /// <summary>Gets the day counter of the fixing times.</summary>
    public abstract DayCounter DayCounter { get; }

    /// <summary>Gets the calendar, or <see langword="null"/>, QuantLib's empty calendar.</summary>
    public abstract Calendar? Calendar { get; }

    /// <summary>
    /// Gets the settlement days; <see langword="null"/> where they weren't provided, where QuantLib throws "settlement
    /// days not provided for this instance".
    /// </summary>
    public abstract int? SettlementDays { get; }

    /// <summary>Gets the business-day convention.</summary>
    public abstract BusinessDayConvention BusinessDayConvention { get; }

    /// <summary>Gets the volatilities' type.</summary>
    public abstract VolatilityType VolatilityType { get; }

    /// <summary>Gets the displacement of shifted lognormal volatilities.</summary>
    public abstract double Displacement { get; }

    /// <summary>The strikes at the <paramref name="i"/>-th fixing date.</summary>
    /// <param name="i">The fixing date's index.</param>
    /// <returns>The strikes.</returns>
    public abstract ImmutableArray<double> OptionletStrikes(int i);

    /// <summary>The volatilities at the <paramref name="i"/>-th fixing date, one per strike.</summary>
    /// <param name="i">The fixing date's index.</param>
    /// <returns>The volatilities.</returns>
    public abstract ImmutableArray<double> OptionletVolatilities(int i);

    /// <summary>The at-the-money rates, one per fixing date.</summary>
    /// <param name="context">The evaluation context, whose fixings and evaluation date the rates may read.</param>
    /// <returns>The rates.</returns>
    public abstract ImmutableArray<double> AtmOptionletRates(EvaluationContext context);
}
