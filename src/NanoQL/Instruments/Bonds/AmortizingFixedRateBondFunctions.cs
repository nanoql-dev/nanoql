// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/instruments/bonds/amortizingfixedratebond.hpp ql/instruments/bonds/amortizingfixedratebond.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Time;

namespace NanoQL.Instruments;

/// <summary>The schedule and the notionals of a French amortization, for <see cref="AmortizingFixedRateBond"/>.</summary>
public static class AmortizingFixedRateBondFunctions
{
    /// <summary>
    /// The schedule of a French amortization: from the start date to the start date plus the bond's length, at the
    /// frequency, unadjusted and generated backward.
    /// </summary>
    /// <param name="startDate">The start date.</param>
    /// <param name="bondLength">The bond's length.</param>
    /// <param name="frequency">The payment frequency.</param>
    /// <param name="paymentCalendar">The schedule's calendar, or <see langword="null"/>, QuantLib's empty calendar.</param>
    /// <returns>The schedule.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The frequency isn't a member of its enum, or is <see cref="Frequency.OtherFrequency"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The start date is <c>default(Date)</c>, or the schedule can't be generated from the dates and the frequency.
    /// </exception>
    public static Schedule SinkingSchedule(Date startDate, Period bondLength, Frequency frequency, Calendar? paymentCalendar)
    {
        Date.ThrowIfDefault(startDate, nameof(startDate));
        Period tenor = Period.FromFrequency(frequency, nameof(frequency));
        Date maturityDate = startDate + bondLength;
        var retVal = new Schedule(
            startDate,
            maturityDate,
            tenor,
            paymentCalendar,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Backward,
            false,
            null,
            null);
        return retVal;
    }

    /// <summary>
    /// The notionals of a French amortization: constant payments of interest and principal at the frequency, linear
    /// amortization when the coupon per period is below 1e-12; the initial notional first and 0 last.
    /// </summary>
    /// <param name="bondLength">The bond's length, a whole number of the frequency's periods.</param>
    /// <param name="frequency">The payment frequency.</param>
    /// <param name="couponRate">The coupon rate, annual.</param>
    /// <param name="initialNotional">The initial notional.</param>
    /// <returns>The notionals, one more than the periods.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The frequency isn't a member of its enum, or is <see cref="Frequency.OtherFrequency"/>; or the bond's length is
    /// in a time unit shorter than a day.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The bond's length isn't positive (QuantLib writes past the notionals' end), or isn't a whole number of the
    /// frequency's periods, a frequency of no period among them (QuantLib divides by zero).
    /// </exception>
    public static ImmutableArray<double> SinkingNotionals(
        Period bondLength,
        Frequency frequency,
        double couponRate,
        double initialNotional)
    {
        Period subPeriod = Period.FromFrequency(frequency, nameof(frequency));

        // QuantLib's loop below runs past a one-element vector for no period, and reads an empty one's first element
        // for a negative length.
        if (!(bondLength.Length > 0))
        {
            throw new ArgumentException("bond length must be positive", nameof(bondLength));
        }

        if (!IsSubPeriod(subPeriod, bondLength, out int nPeriods))
        {
            throw new ArgumentException("Bond frequency is incompatible with the maturity tenor", nameof(frequency));
        }

        var notionals = new double[nPeriods + 1];
        notionals[0] = initialNotional;
        double coupon = couponRate / (int)frequency;
        double compoundedInterest = 1.0;
        double totalValue = Math.Pow(1.0 + coupon, nPeriods);
        for (int i = 0; i < nPeriods - 1; ++i)
        {
            compoundedInterest *= 1.0 + coupon;
            double currentNotional = 0.0;
            if (coupon < 1.0e-12)
            {
                currentNotional = initialNotional * (1.0 - ((i + 1.0) / nPeriods));
            }
            else
            {
                currentNotional =
                    initialNotional * (compoundedInterest - ((compoundedInterest - 1.0) / (1.0 - (1.0 / totalValue))));
            }

            notionals[i + 1] = currentNotional;
        }

        notionals[^1] = 0.0;

        return [.. notionals];
    }

    private static (int Min, int Max) DaysMinMax(Period p, string paramName) => p.Units switch
    {
        TimeUnit.Days => (p.Length, p.Length),
        TimeUnit.Weeks => (7 * p.Length, 7 * p.Length),
        TimeUnit.Months => (28 * p.Length, 31 * p.Length),
        TimeUnit.Years => (365 * p.Length, 366 * p.Length),
        _ => throw new ArgumentOutOfRangeException(paramName, p.Units, $"unknown time unit ({(int)p.Units})"),
    };

    private static bool IsSubPeriod(Period subPeriod, Period superPeriod, out int numSubPeriods)
    {
        numSubPeriods = 0;
        (int Min, int Max) superDays = DaysMinMax(superPeriod, "bondLength");
        (int Min, int Max) subDays = DaysMinMax(subPeriod, "frequency");

        // QuantLib divides by zero for a frequency of no period, Once or NoFrequency, and converts the infinite or NaN
        // ratio to an Integer, which is undefined; MSVC's result, the smallest Integer, matches no length.
        if (subDays.Min == 0)
        {
            return false;
        }

        // obtain the approximate time ratio
        double minPeriodRatio = (double)superDays.Min / subDays.Max;
        double maxPeriodRatio = (double)superDays.Max / subDays.Min;
        int lowRatio = (int)Math.Floor(minPeriodRatio);
        int highRatio = (int)Math.Ceiling(maxPeriodRatio);

        for (int i = lowRatio; i <= highRatio; ++i)
        {
            Period testPeriod = subPeriod * i;
            if (testPeriod == superPeriod)
            {
                numSubPeriods = i;
                return true;
            }
        }

        return false;
    }
}
