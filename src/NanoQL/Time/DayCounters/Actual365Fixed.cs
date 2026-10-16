// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2013 BGC Partners L.P.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/time/daycounters/actual365fixed.hpp ql/time/daycounters/actual365fixed.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Time;

/// <summary>
/// "Actual/365 (Fixed)" day count convention, also known as "Act/365 (Fixed)", "A/365 (Fixed)", or "A/365F".
/// </summary>
/// <remarks>
/// According to ISDA, "Actual/365" (without "Fixed") is an alias for "Actual/Actual (ISDA)" (see
/// <see cref="ActualActual"/>). If Actual/365 is not explicitly specified as fixed in an instrument specification, you
/// might want to double-check its meaning.
/// </remarks>
public sealed class Actual365Fixed : DayCounter
{
    private readonly Impl impl;

    /// <summary>Initializes a new instance of the <see cref="Actual365Fixed"/> class.</summary>
    /// <param name="c">The convention.</param>
    /// <exception cref="NanoQLException">The convention is unknown.</exception>
    public Actual365Fixed(Actual365FixedConvention c = Actual365FixedConvention.Standard)
    {
        impl = Implementation(c);
    }

    /// <inheritdoc/>
    public override string Name => impl.Name;

    /// <inheritdoc/>
    public override int DayCount(Date d1, Date d2) => impl.DayCount(d1, d2);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// Under the Canadian convention, for distinct dates: a reference date is missing, or the reference period is
    /// shorter than a month or longer than a year.
    /// </exception>
    public override double YearFraction(Date d1, Date d2, Date? refPeriodStart = null, Date? refPeriodEnd = null)
    {
        RequireDates(d1, d2, refPeriodStart, refPeriodEnd);
        return impl.YearFraction(d1, d2, refPeriodStart, refPeriodEnd);
    }

    private static Impl Implementation(Actual365FixedConvention c) => c switch
    {
        Actual365FixedConvention.Standard => new StandardImpl(),
        Actual365FixedConvention.Canadian => new CAImpl(),
        Actual365FixedConvention.NoLeap => new NLImpl(),
        _ => throw new NanoQLException("unknown Actual/365 (Fixed) convention"),
    };

    // QuantLib's DayCounter::Impl.
    private abstract class Impl
    {
        public abstract string Name { get; }

        public virtual int DayCount(Date d1, Date d2) => d2 - d1;

        public abstract double YearFraction(Date d1, Date d2, Date? refPeriodStart, Date? refPeriodEnd);
    }

    // QuantLib's Actual365Fixed::Impl.
    private sealed class StandardImpl : Impl
    {
        public override string Name => "Actual/365 (Fixed)";

        public override double YearFraction(Date d1, Date d2, Date? refPeriodStart, Date? refPeriodEnd) =>
            DateFunctions.DaysBetween(d1, d2) / 365.0;
    }

    private sealed class CAImpl : Impl
    {
        public override string Name => "Actual/365 (Fixed) Canadian Bond";

        public override double YearFraction(Date d1, Date d2, Date? refPeriodStart, Date? refPeriodEnd)
        {
            if (d1 == d2)
            {
                return 0.0;
            }

            // We need the period to calculate the frequency
            if (refPeriodStart is not Date refStart)
            {
                throw new ArgumentException("invalid refPeriodStart", nameof(refPeriodStart));
            }

            if (refPeriodEnd is not Date refEnd)
            {
                throw new ArgumentException("invalid refPeriodEnd", nameof(refPeriodEnd));
            }

            double dcs = DateFunctions.DaysBetween(d1, d2);
            double dcc = DateFunctions.DaysBetween(refStart, refEnd);
            int months = (int)Math.Round(12 * dcc / 365, MidpointRounding.AwayFromZero);
            if (!(months != 0))
            {
                throw new ArgumentException(
                    "invalid reference period for Act/365 Canadian; must be longer than a month",
                    nameof(refPeriodEnd));
            }

            int frequency = 12 / months;
            if (!(frequency != 0))
            {
                throw new ArgumentException(
                    "invalid reference period for Act/365 Canadian; must not be longer than a year",
                    nameof(refPeriodEnd));
            }

            if (dcs < 365 / frequency)
            {
                return dcs / 365.0;
            }

            return (1.0 / frequency) - ((dcc - dcs) / 365.0);
        }
    }

    private sealed class NLImpl : Impl
    {
        private static readonly int[] MonthOffset =
        [
            0, 31, 59, 90, 120, 151, // Jan - Jun
            181, 212, 243, 273, 304, 334, // Jun - Dec
        ];

        public override string Name => "Actual/365 (No Leap)";

        public override int DayCount(Date d1, Date d2)
        {
            int s1 = d1.DayOfMonth + MonthOffset[(int)d1.Month - 1] + (d1.Year * 365);
            int s2 = d2.DayOfMonth + MonthOffset[(int)d2.Month - 1] + (d2.Year * 365);

            if (d1.Month == Month.February && d1.DayOfMonth == 29)
            {
                --s1;
            }

            if (d2.Month == Month.February && d2.DayOfMonth == 29)
            {
                --s2;
            }

            return s2 - s1;
        }

        public override double YearFraction(Date d1, Date d2, Date? refPeriodStart, Date? refPeriodEnd) =>
            DayCount(d1, d2) / 365.0;
    }
}
