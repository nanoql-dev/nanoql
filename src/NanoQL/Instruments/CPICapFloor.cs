// SPDX-FileCopyrightText: Copyright (C) 2010, 2011 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/cpicapfloor.hpp ql/instruments/cpicapfloor.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>
/// A CPI cap or floor: an option on the cumulative inflation from a base date to maturity, quoted as a fixed strike
/// rate K, paying P(0,T) max(y (N [(1 + K)^T − 1] − N [I(T)/I(0) − 1]), 0), T the maturity time, N the notional, I the
/// index and y the option's sign.
/// </summary>
/// <remarks>
/// <para>
/// A CPI cap or floor is an option, not a cap or floor on a coupon: like a zero-coupon inflation swap it has a single
/// flow, cumulative from the base date to the option's maturity. Inflation is published on every day, holidays and
/// weekends included, so whether the fixing date is adjusted is the fixing convention's choice. Its engine,
/// <see cref="Experimental.InterpolatingCPICapFloorEngine"/>, reads QuantLib's arguments through an internal builder
/// (the S-cms plan's CMD16).
/// </para>
/// <para>
/// It expires after its unadjusted maturity, an option's expiry, as QuantLib's.
/// </para>
/// </remarks>
public sealed class CPICapFloor : Instrument
{
    /// <summary>Initializes a new instance of the <see cref="CPICapFloor"/> class.</summary>
    /// <param name="type">A call (a cap) or a put (a floor).</param>
    /// <param name="nominal">The notional.</param>
    /// <param name="startDate">The contract's start date.</param>
    /// <param name="baseCPI">The base index level.</param>
    /// <param name="maturity">The maturity, unadjusted.</param>
    /// <param name="fixCalendar">The fixing calendar.</param>
    /// <param name="fixConvention">The fixing convention.</param>
    /// <param name="payCalendar">The payment calendar.</param>
    /// <param name="payConvention">The payment convention.</param>
    /// <param name="strike">The strike rate.</param>
    /// <param name="inflationIndex">The zero inflation index.</param>
    /// <param name="observationLag">The observation lag.</param>
    /// <param name="observationInterpolation">The observation's interpolation.</param>
    /// <exception cref="ArgumentNullException">The index or a calendar is null, with QuantLib's text.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum argument isn't a member of its enum.</exception>
    /// <exception cref="ArgumentException">The observation lag is shorter than the index's availability lag, or, for a
    /// linear observation, not longer, with QuantLib's text.</exception>
    public CPICapFloor(
        OptionType type,
        double nominal,
        Date startDate,
        double baseCPI,
        Date maturity,
        Calendar fixCalendar,
        BusinessDayConvention fixConvention,
        Calendar payCalendar,
        BusinessDayConvention payConvention,
        double strike,
        ZeroInflationIndex inflationIndex,
        Period observationLag,
        CPIInterpolationType observationInterpolation = CPIInterpolationType.Flat)
    {
        EnumArguments.Check(type, nameof(type));
        EnumArguments.Check(fixConvention, nameof(fixConvention));
        EnumArguments.Check(payConvention, nameof(payConvention));
        EnumArguments.Check(observationInterpolation, nameof(observationInterpolation));
        if (inflationIndex is null)
        {
            throw new ArgumentNullException(nameof(inflationIndex), "no inflation index passed");
        }

        if (fixCalendar is null)
        {
            throw new ArgumentNullException(nameof(fixCalendar), "no fixing calendar passed");
        }

        if (payCalendar is null)
        {
            throw new ArgumentNullException(nameof(payCalendar), "no payment calendar passed");
        }

        if (!CPI.IsInterpolated(observationInterpolation))
        {
            if (!(observationLag >= inflationIndex.AvailabilityLag))
            {
                throw new ArgumentException(
                    "CPIcapfloor's observationLag must be at least availabilityLag of inflation index: "
                    + "when the observation is effectively flat"
                    + $"{observationLag} vs {inflationIndex.AvailabilityLag}",
                    nameof(observationLag));
            }
        }
        else if (!(observationLag > inflationIndex.AvailabilityLag))
        {
            throw new ArgumentException(
                "CPIcapfloor's observationLag must be greater than availabilityLag of inflation index: when the "
                + $"observation is effectively linear{observationLag} vs {inflationIndex.AvailabilityLag}",
                nameof(observationLag));
        }

        Type = type;
        Nominal = nominal;
        StartDate = startDate;
        BaseCPI = baseCPI;
        Maturity = maturity;
        FixCalendar = fixCalendar;
        FixConvention = fixConvention;
        PayCalendar = payCalendar;
        PayConvention = payConvention;
        Strike = strike;
        Index = inflationIndex;
        ObservationLag = observationLag;
        ObservationInterpolation = observationInterpolation;
    }

    /// <summary>Gets whether it is a cap (a call) or a floor (a put).</summary>
    public OptionType Type { get; }

    /// <summary>Gets the notional.</summary>
    public double Nominal { get; }

    /// <summary>Gets the contract's start date.</summary>
    public Date StartDate { get; }

    /// <summary>Gets the base index level.</summary>
    public double BaseCPI { get; }

    /// <summary>Gets the maturity, unadjusted.</summary>
    public Date Maturity { get; }

    /// <summary>Gets the fixing calendar.</summary>
    public Calendar FixCalendar { get; }

    /// <summary>Gets the fixing convention.</summary>
    public BusinessDayConvention FixConvention { get; }

    /// <summary>Gets the payment calendar.</summary>
    public Calendar PayCalendar { get; }

    /// <summary>Gets the payment convention.</summary>
    public BusinessDayConvention PayConvention { get; }

    /// <summary>Gets the strike rate.</summary>
    public double Strike { get; }

    /// <summary>Gets the zero inflation index.</summary>
    public ZeroInflationIndex Index { get; }

    /// <summary>Gets the observation lag.</summary>
    public Period ObservationLag { get; }

    /// <summary>Gets the observation's interpolation.</summary>
    public CPIInterpolationType ObservationInterpolation { get; }

    /// <summary>The fixing date: the maturity less the observation lag, adjusted on the fixing calendar.</summary>
    /// <returns>The fixing date.</returns>
    public Date FixingDate() => FixCalendar.Adjust(Maturity - ObservationLag, FixConvention);

    /// <summary>The payment date: the maturity adjusted on the payment calendar.</summary>
    /// <returns>The payment date.</returns>
    public Date PayDate() => PayCalendar.Adjust(Maturity, PayConvention);

    /// <inheritdoc/>
    /// <remarks>Expired after its unadjusted maturity, as QuantLib's.</remarks>
    public override bool IsExpired(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.EvaluationDate > Maturity;
    }
}
