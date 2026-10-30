// SPDX-FileCopyrightText: Copyright (C) 2004 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/interestrate.hpp ql/interestrate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL;

/// <summary>
/// An interest rate with its day-counting and compounding conventions: discount and compound factors, and implied and
/// equivalent rates.
/// </summary>
/// <remarks>
/// A value: two rates are equal when their rate, day counter (by name), compounding and frequency are. QuantLib's null
/// interest rate is a <c>InterestRate?</c> null, and its conversion to <c>Rate</c> is <see cref="Rate"/>.
/// </remarks>
public sealed record InterestRate
{
    private readonly bool freqMakesSense;
    private readonly double freq;

    /// <summary>Initializes a new instance of the <see cref="InterestRate"/> class.</summary>
    /// <param name="r">The rate.</param>
    /// <param name="dc">The day counter.</param>
    /// <param name="comp">The compounding rule.</param>
    /// <param name="freq">The compounding frequency, which compounded rules need.</param>
    /// <exception cref="ArgumentException">A compounded rule with no frequency or <see cref="Frequency.Once"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    public InterestRate(double r, DayCounter dc, Compounding comp, Frequency freq)
    {
        ArgumentNullException.ThrowIfNull(dc);
        EnumArguments.Check(comp, nameof(comp));
        EnumArguments.Check(freq, nameof(freq));
        Rate = r;
        DayCounter = dc;
        Compounding = comp;
        if (comp is Compounding.Compounded or Compounding.SimpleThenCompounded or Compounding.CompoundedThenSimple)
        {
            freqMakesSense = true;
            if (!(freq != Frequency.Once && freq != Frequency.NoFrequency))
            {
                throw new ArgumentException("frequency not allowed for this interest rate", nameof(freq));
            }

            this.freq = (double)freq;
        }
    }

    /// <summary>Gets the rate.</summary>
    public double Rate { get; }

    /// <summary>Gets the day counter.</summary>
    public DayCounter DayCounter { get; }

    /// <summary>Gets the compounding rule.</summary>
    public Compounding Compounding { get; }

    /// <summary>Gets the compounding frequency; <see cref="Time.Frequency.NoFrequency"/> where the rule needs none.</summary>
    public Frequency Frequency => freqMakesSense ? (Frequency)(int)freq : Frequency.NoFrequency;

    /// <summary>The implied interest rate for a compound factor at a time.</summary>
    /// <param name="compound">The compound factor.</param>
    /// <param name="resultDC">The result's day counter, by which <paramref name="t"/> must be measured.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="t">The time.</param>
    /// <returns>The implied rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The compounding is <see cref="Compounding.SimpleThenCompounded"/> or
    /// <see cref="Compounding.CompoundedThenSimple"/>, at any compound factor (QuantLib's master's refusal, #2898,
    /// where 1.44 returns a zero rate at a factor of 1).</exception>
    public static InterestRate ImpliedRate(double compound, DayCounter resultDC, Compounding comp, Frequency freq, double t)
    {
        ArgumentNullException.ThrowIfNull(resultDC);
        EnumArguments.Check(comp, nameof(comp));
        EnumArguments.Check(freq, nameof(freq));
        if (!(compound > 0.0))
        {
            throw new ArgumentException("positive compound factor required", nameof(compound));
        }

        // QuantLib's fix after v1.44 (#2898): the mixed compoundings refused before the unit factor's branch
        if (comp is Compounding.SimpleThenCompounded or Compounding.CompoundedThenSimple)
        {
            throw new NanoQLException($"{comp} is not supported for direct calculations: use the CashFlow methods");
        }

        double r;
        if (compound == 1.0)
        {
            if (!(t >= 0.0))
            {
                throw new ArgumentException($"non negative time ({Show(t)}) required", nameof(t));
            }

            r = 0.0;
        }
        else
        {
            if (!(t > 0.0))
            {
                throw new ArgumentException($"positive time ({Show(t)}) required", nameof(t));
            }

            switch (comp)
            {
                case Compounding.Simple:
                    r = (compound - 1.0) / t;
                    break;
                case Compounding.Compounded:
                    r = (Math.Pow(compound, 1.0 / ((double)freq * t)) - 1.0) * (double)freq;
                    break;
                case Compounding.Continuous:
                    r = Math.Log(compound) / t;
                    break;
                case Compounding.SimpleThenCompounded:
                case Compounding.CompoundedThenSimple:
                    throw new NanoQLException($"{comp} is not supported for direct calculations: use the CashFlow methods");
                default:
                    // QuantLib returns a rate whose value is null here; a nanoql rate can't be null, so this throws.
                    throw new NanoQLException("null interest rate");
            }
        }

        return new InterestRate(r, resultDC, comp, freq);
    }

    /// <summary>The implied rate for a compound factor between two dates, by the result's day-counting rule.</summary>
    /// <param name="compound">The compound factor.</param>
    /// <param name="resultDC">The result's day counter.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="d1">The start date.</param>
    /// <param name="d2">The end date.</param>
    /// <param name="refStart">The reference period's start, for day counters that use one.</param>
    /// <param name="refEnd">The reference period's end, for day counters that use one.</param>
    /// <returns>The implied rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The compounding is <see cref="Compounding.SimpleThenCompounded"/> or
    /// <see cref="Compounding.CompoundedThenSimple"/>.</exception>
    public static InterestRate ImpliedRate(
        double compound,
        DayCounter resultDC,
        Compounding comp,
        Frequency freq,
        Date d1,
        Date d2,
        Date? refStart = null,
        Date? refEnd = null)
    {
        ArgumentNullException.ThrowIfNull(resultDC);
        RequireDates(d1, d2, refStart, refEnd);
        double t = resultDC.YearFraction(d1, d2, refStart, refEnd);
        return ImpliedRate(compound, resultDC, comp, freq, t);
    }

    /// <summary>The discount factor implied by the rate compounded at a time, measured by the rate's day counter.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    public double DiscountFactor(double t) => 1.0 / CompoundFactor(t);

    /// <summary>The discount factor implied by the rate compounded between two dates.</summary>
    /// <param name="d1">The start date.</param>
    /// <param name="d2">The end date.</param>
    /// <param name="refStart">The reference period's start, for day counters that use one.</param>
    /// <param name="refEnd">The reference period's end, for day counters that use one.</param>
    /// <returns>The discount factor.</returns>
    public double DiscountFactor(Date d1, Date d2, Date? refStart = null, Date? refEnd = null)
    {
        RequireDates(d1, d2, refStart, refEnd);
        double t = DayCounter.YearFraction(d1, d2, refStart, refEnd);
        return DiscountFactor(t);
    }

    /// <summary>
    /// The compound (capitalization) factor implied by the rate compounded at a time, measured by the rate's day counter.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <returns>The compound factor.</returns>
    public double CompoundFactor(double t)
    {
        if (!(t >= 0.0))
        {
            throw new ArgumentException($"negative time ({Show(t)}) not allowed", nameof(t));
        }

        return Compounding switch
        {
            Compounding.Simple => 1.0 + (Rate * t),
            Compounding.Compounded => Math.Pow(1.0 + (Rate / freq), freq * t),
            Compounding.Continuous => Math.Exp(Rate * t),
            Compounding.SimpleThenCompounded or Compounding.CompoundedThenSimple =>
                throw new NanoQLException($"{Compounding} is not supported for direct calculations: use the CashFlow methods"),
            _ => throw new NanoQLException("unknown compounding convention"),
        };
    }

    /// <summary>The compound (capitalization) factor implied by the rate compounded between two dates.</summary>
    /// <param name="d1">The start date.</param>
    /// <param name="d2">The end date.</param>
    /// <param name="refStart">The reference period's start, for day counters that use one.</param>
    /// <param name="refEnd">The reference period's end, for day counters that use one.</param>
    /// <returns>The compound factor.</returns>
    public double CompoundFactor(Date d1, Date d2, Date? refStart = null, Date? refEnd = null)
    {
        RequireDates(d1, d2, refStart, refEnd);
        double t = DayCounter.YearFraction(d1, d2, refStart, refEnd);
        return CompoundFactor(t);
    }

    /// <summary>The first derivative of the discount factor at a time with respect to the rate.</summary>
    /// <param name="t">The time, measured by the rate's day counter.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentException">The time is negative.</exception>
    /// <exception cref="NanoQLException">The rule is simple then compounded or compounded then simple.</exception>
    public double DiscountFactorFirstDerivative(double t)
    {
        double discount = DiscountFactor(t);
        switch (Compounding)
        {
            case Compounding.Simple:
                return -t * discount * discount;
            case Compounding.Compounded:
                {
                    double freq = (double)Frequency;
                    double compound = 1 + (Rate / freq);
                    return -t * discount / compound;
                }

            case Compounding.Continuous:
                return -t * discount;
            case Compounding.SimpleThenCompounded:
            case Compounding.CompoundedThenSimple:
                throw new NanoQLException($"{Compounding} is not supported for direct calculations: use the CashFlow methods");
            default:
                throw new NanoQLException("unknown compounding convention");
        }
    }

    /// <summary>The second derivative of the discount factor at a time with respect to the rate.</summary>
    /// <param name="t">The time, measured by the rate's day counter.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentException">The time is negative.</exception>
    /// <exception cref="NanoQLException">The rule is simple then compounded or compounded then simple.</exception>
    public double DiscountFactorSecondDerivative(double t)
    {
        double discount = DiscountFactor(t);
        switch (Compounding)
        {
            case Compounding.Simple:
                return 2.0 * t * t * discount * discount * discount;
            case Compounding.Compounded:
                {
                    double freq = (double)Frequency;
                    double compound = 1 + (Rate / freq);
                    return discount * t * ((freq * t) + 1.0) / (freq * compound * compound);
                }

            case Compounding.Continuous:
                return t * t * discount;
            case Compounding.SimpleThenCompounded:
            case Compounding.CompoundedThenSimple:
                throw new NanoQLException($"{Compounding} is not supported for direct calculations: use the CashFlow methods");
            default:
                throw new NanoQLException("unknown compounding convention");
        }
    }

    /// <summary>The equivalent rate for a compounding period of a time, with the rate's own day counter.</summary>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="t">The time, measured by the rate's day counter.</param>
    /// <returns>The equivalent rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The compounding is <see cref="Compounding.SimpleThenCompounded"/> or
    /// <see cref="Compounding.CompoundedThenSimple"/>.</exception>
    public InterestRate EquivalentRate(Compounding comp, Frequency freq, double t) =>
        ImpliedRate(CompoundFactor(t), DayCounter, comp, freq, t);

    /// <summary>The equivalent rate for a compounding period between two dates, by the result's day-counting rule.</summary>
    /// <param name="resultDC">The result's day counter.</param>
    /// <param name="comp">The result's compounding rule.</param>
    /// <param name="freq">The result's frequency.</param>
    /// <param name="d1">The start date.</param>
    /// <param name="d2">The end date.</param>
    /// <param name="refStart">The reference period's start, for day counters that use one.</param>
    /// <param name="refEnd">The reference period's end, for day counters that use one.</param>
    /// <returns>The equivalent rate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The compounding or the frequency isn't a member of its enum.</exception>
    /// <exception cref="NanoQLException">The compounding is <see cref="Compounding.SimpleThenCompounded"/> or
    /// <see cref="Compounding.CompoundedThenSimple"/>.</exception>
    public InterestRate EquivalentRate(
        DayCounter resultDC,
        Compounding comp,
        Frequency freq,
        Date d1,
        Date d2,
        Date? refStart = null,
        Date? refEnd = null)
    {
        ArgumentNullException.ThrowIfNull(resultDC);
        RequireDates(d1, d2, refStart, refEnd);
        double t1 = DayCounter.YearFraction(d1, d2, refStart, refEnd);
        double t2 = resultDC.YearFraction(d1, d2, refStart, refEnd);
        return ImpliedRate(CompoundFactor(t1), resultDC, comp, freq, t2);
    }

    /// <summary>The rate as QuantLib writes it, e.g. "3.250000 % Actual/365 (Fixed) continuous compounding".</summary>
    /// <returns>The text.</returns>
    /// <exception cref="NanoQLException">A compounded rate without a usable frequency.</exception>
    public override string ToString()
    {
        string rate = DataFormattersFunctions.Rate(Rate) + " " + DayCounter.Name + " ";
        switch (Compounding)
        {
            case Compounding.Simple:
                return rate + "simple compounding";
            case Compounding.Compounded:
                RequireFrequency();
                return rate + FrequencyFunctions.ToString(Frequency) + " compounding";
            case Compounding.Continuous:
                return rate + "continuous compounding";
            case Compounding.SimpleThenCompounded:
                RequireFrequency();
                return rate + "simple compounding up to " + Months() + " months, then "
                    + FrequencyFunctions.ToString(Frequency) + " compounding";
            case Compounding.CompoundedThenSimple:
                RequireFrequency();
                return rate + "compounding up to " + Months() + " months, then "
                    + FrequencyFunctions.ToString(Frequency) + " simple compounding";
            default:
                throw new NanoQLException($"unknown compounding convention ({(int)Compounding})");
        }

        string Months() => (12 / (int)Frequency).ToString(CultureInfo.InvariantCulture);
    }

    private static string Show(double t) => t.ToString(CultureInfo.InvariantCulture);

    private static void RequireDates(Date d1, Date d2, Date? refStart, Date? refEnd)
    {
        Date.ThrowIfDefault(d1, nameof(d1));
        Date.ThrowIfDefault(d2, nameof(d2));
        Date.ThrowIfDefault(refStart, nameof(refStart));
        Date.ThrowIfDefault(refEnd, nameof(refEnd));
        if (!(d2 >= d1))
        {
            throw new ArgumentException($"d1 ({d1}) later than d2 ({d2})", nameof(d1));
        }
    }

    private void RequireFrequency()
    {
        if (Frequency is Frequency.NoFrequency or Frequency.Once)
        {
            throw new NanoQLException(FrequencyFunctions.ToString(Frequency) + " frequency not allowed for this interest rate");
        }
    }
}
