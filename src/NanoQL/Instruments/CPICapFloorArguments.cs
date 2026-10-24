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
/// QuantLib's <c>CPICapFloor::arguments</c>, which its <c>setupArguments</c> fills: the instrument's terms with its
/// fixing and payment dates (the S-g1d plan's G1D6), the shape S-xrt's engines read (the S-cms plan's CMD16).
/// </summary>
/// <remarks>QuantLib's <c>validate</c> checks nothing yet.</remarks>
internal sealed class CPICapFloorArguments
{
    private CPICapFloorArguments()
    {
    }

    public required OptionType Type { get; init; }

    public required double Nominal { get; init; }

    public required Date StartDate { get; init; }

    public required Date FixDate { get; init; }

    public required Date PayDate { get; init; }

    public required double BaseCPI { get; init; }

    public required Date Maturity { get; init; }

    public required Calendar FixCalendar { get; init; }

    public required Calendar PayCalendar { get; init; }

    public required BusinessDayConvention FixConvention { get; init; }

    public required BusinessDayConvention PayConvention { get; init; }

    public required double Strike { get; init; }

    public required ZeroInflationIndex Index { get; init; }

    public required Period ObservationLag { get; init; }

    public required CPIInterpolationType ObservationInterpolation { get; init; }

    // QuantLib's setupArguments (cpicapfloor.cpp:91-115).
    internal static CPICapFloorArguments Of(CPICapFloor capFloor) => new()
    {
        Type = capFloor.Type,
        Nominal = capFloor.Nominal,
        StartDate = capFloor.StartDate,
        BaseCPI = capFloor.BaseCPI,
        Maturity = capFloor.Maturity,
        FixCalendar = capFloor.FixCalendar,
        FixConvention = capFloor.FixConvention,
        PayCalendar = capFloor.PayCalendar,
        PayConvention = capFloor.PayConvention,
        FixDate = capFloor.FixingDate(),
        PayDate = capFloor.PayDate(),
        Strike = capFloor.Strike,
        Index = capFloor.Index,
        ObservationLag = capFloor.ObservationLag,
        ObservationInterpolation = capFloor.ObservationInterpolation,
    };
}
