// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/smilesection.hpp ql/termstructures/volatility/smilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Time;
using static NanoQL.PricingEngines.BlackFormulaFunctions;

namespace NanoQL.TermStructures;

/// <summary>An interest-rate volatility smile section: the volatilities of options on one exercise date.</summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: a section implements <see cref="MinStrike"/>, <see cref="MaxStrike"/>,
/// <see cref="AtmLevel"/> and <see cref="VolatilityImpl"/>, and may override the inspectors and the option prices,
/// whose defaults price with Black's or Bachelier's formula on the section's variance.
/// </para>
/// <para>
/// Every section has a fixed reference date. QuantLib's floating section, built by date without a reference date, reads
/// the evaluation date and re-reads it on each change; nanoql's concrete sections build it through a
/// <c>Create(…, EvaluationContext context)</c> factory, which reads the date once (<see cref="FlatSmileSection.Create"/>).
/// </para>
/// </remarks>
public abstract class SmileSection
{
    private readonly Date? exerciseDate;
    private readonly Date? referenceDate;
    private readonly DayCounter? dc;
    private readonly double exerciseTime;
    private readonly VolatilityType volatilityType;
    private readonly double shift;

    /// <summary>Initializes a new instance of the <see cref="SmileSection"/> class for an exercise date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="dc">The day counter of the exercise time, from the reference date to the exercise date.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dc"/> is null: QuantLib's empty day counter fails on
    /// the exercise time it always computes.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, or the exercise date is before the
    /// reference date.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    protected SmileSection(
        Date d,
        DayCounter dc,
        Date referenceDate,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0)
    {
        ArgumentNullException.ThrowIfNull(dc);
        Date.ThrowIfDefault(d, nameof(d));
        Date.ThrowIfDefault(referenceDate, nameof(referenceDate));
        EnumArguments.Check(type, nameof(type));
        exerciseDate = d;
        this.dc = dc;
        volatilityType = type;
        this.shift = shift;
        this.referenceDate = referenceDate;

        // QuantLib's initializeExerciseTime, which only a floating section's update() calls again.
        if (!(d >= referenceDate))
        {
            throw new ArgumentException(
                $"expiry date ({d}) must be greater than reference date ({referenceDate})", nameof(d));
        }

        exerciseTime = dc.YearFraction(referenceDate, d);
    }

    /// <summary>Initializes a new instance of the <see cref="SmileSection"/> class for an exercise time.</summary>
    /// <param name="exerciseTime">The exercise time.</param>
    /// <param name="dc">The day counter, or <see langword="null"/>, QuantLib's empty one; the section only returns
    /// it.</param>
    /// <param name="type">The volatility type.</param>
    /// <param name="shift">The shift of a shifted lognormal volatility.</param>
    /// <exception cref="ArgumentException"><paramref name="exerciseTime"/> is negative or NaN.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    protected SmileSection(
        double exerciseTime,
        DayCounter? dc = null,
        VolatilityType type = VolatilityType.ShiftedLognormal,
        double shift = 0.0)
    {
        EnumArguments.Check(type, nameof(type));
        this.dc = dc;
        this.exerciseTime = exerciseTime;
        volatilityType = type;
        this.shift = shift;
        if (!(exerciseTime >= 0.0))
        {
            throw new ArgumentException($"expiry time must be positive: {Show(exerciseTime)} not allowed", nameof(exerciseTime));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SmileSection"/> class with no exercise date, time or day counter:
    /// for a section that overrides every inspector, as one forwarding them to another section does.
    /// </summary>
    /// <remarks>
    /// QuantLib's default constructor leaves the exercise time, the volatility type and the shift uninitialized; here
    /// they are defined: an exercise time of 0, <see cref="VolatilityType.ShiftedLognormal"/> and a shift of 0, with no
    /// exercise date, reference date or day counter.
    /// </remarks>
    protected SmileSection()
    {
    }

    /// <summary>Gets the exercise date; <see langword="null"/> for a section built from an exercise time.</summary>
    public virtual Date? ExerciseDate => exerciseDate;

    /// <summary>Gets the volatility type.</summary>
    public virtual VolatilityType VolatilityType => volatilityType;

    /// <summary>Gets the shift of a shifted lognormal volatility.</summary>
    public virtual double Shift => shift;

    /// <summary>Gets the exercise time.</summary>
    public virtual double ExerciseTime => exerciseTime;

    /// <summary>Gets the day counter; <see langword="null"/>, QuantLib's empty day counter, where none was given.</summary>
    public virtual DayCounter? DayCounter => dc;

    /// <summary>The minimum strike for which the section returns volatilities.</summary>
    /// <returns>The strike.</returns>
    public abstract double MinStrike();

    /// <summary>The maximum strike for which the section returns volatilities.</summary>
    /// <returns>The strike.</returns>
    public abstract double MaxStrike();

    /// <summary>The at-the-money level, the forward the options are priced on.</summary>
    /// <returns>The level; <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, where the section has none.</returns>
    public abstract double? AtmLevel();

    /// <summary>The reference date.</summary>
    /// <returns>The date.</returns>
    /// <exception cref="NanoQLException">The section has no reference date, as one built from an exercise time.</exception>
    public virtual Date ReferenceDate() =>
        referenceDate ?? throw new NanoQLException("referenceDate not available for this instance");

    /// <summary>The variance at a strike.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The variance.</returns>
    public double Variance(double strike) => VarianceImpl(strike);

    /// <summary>The volatility at a strike.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    public double Volatility(double strike) => VolatilityImpl(strike);

    /// <summary>
    /// The volatility at a strike as a volatility of the given type and shift: the section's own where they are its
    /// own, otherwise the one implied by the section's out-of-the-money option price.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">The volatility type wanted.</param>
    /// <param name="shift">The shift wanted, for a shifted lognormal volatility.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="NanoQLException">A conversion is needed and the section has no at-the-money level.</exception>
    /// <remarks>
    /// As QuantLib's, it compares <paramref name="type"/> with the type the section was built with, not with
    /// <see cref="VolatilityType"/>, which a subclass may override, and <paramref name="shift"/> with
    /// <see cref="Shift"/>. A lognormal volatility is implied by Black's formula, or by Chambers' approximation where
    /// the solver fails.
    /// </remarks>
    public double Volatility(double strike, VolatilityType type, double shift = 0.0)
    {
        if (type == volatilityType && ComparisonFunctions.Close(shift, Shift))
        {
            return Volatility(strike);
        }

        double atm = AtmLevel()
            ?? throw new NanoQLException("smile section must provide atm level to compute converted volatilties");
        OptionType optionType = strike >= atm ? OptionType.Call : OptionType.Put;
        double premium = OptionPrice(strike, optionType);
        double premiumAtm = OptionPrice(atm, optionType);
        if (type == VolatilityType.ShiftedLognormal)
        {
            try
            {
                return BlackFormulaImpliedStdDev(optionType, strike, atm, premium, 1.0, shift) / Math.Sqrt(ExerciseTime);
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                return BlackFormulaImpliedStdDevChambers(optionType, strike, atm, premium, premiumAtm, 1.0, shift)
                    / Math.Sqrt(ExerciseTime);
            }
        }
        else
        {
            return BachelierBlackFormulaImpliedVol(optionType, strike, atm, ExerciseTime, premium);
        }
    }

    /// <summary>
    /// The price of an option on the at-the-money level: Black's formula for a shifted lognormal section, Bachelier's
    /// for a normal one, on the section's variance.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">Call or put.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The section has no at-the-money level.</exception>
    /// <remarks>
    /// A shifted lognormal option struck at minus the shift is priced with a standard deviation of 0.2, on which its
    /// price doesn't depend, so that it has a price even outside the strike range.
    /// </remarks>
    public virtual double OptionPrice(double strike, OptionType type = OptionType.Call, double discount = 1.0)
    {
        EnumArguments.Check(type, nameof(type));
        double atm = AtmLevel()
            ?? throw new NanoQLException("smile section must provide atm level to compute option price");

        // if lognormal or shifted lognormal,
        // for strike at -shift, return option price even if outside
        // minstrike, maxstrike interval
        if (VolatilityType == VolatilityType.ShiftedLognormal)
        {
            double stdDev = Math.Abs(strike + Shift) < NumericLimits.QL_EPSILON ? 0.2 : Math.Sqrt(Variance(strike));
            return BlackFormula(type, strike, atm, stdDev, discount, Shift);
        }
        else
        {
            return BachelierBlackFormula(type, strike, atm, Math.Sqrt(Variance(strike)), discount);
        }
    }

    /// <summary>The price of a digital option, as the difference quotient of two option prices around the strike.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">Call or put.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="gap">The strikes' distance, at least the strike's size times 1e-10.</param>
    /// <returns>The price.</returns>
    public virtual double DigitalOptionPrice(
        double strike, OptionType type = OptionType.Call, double discount = 1.0, double gap = 1.0e-5)
    {
        double m = VolatilityType == VolatilityType.ShiftedLognormal ? -Shift : -NumericLimits.QL_MAX_REAL;

        // A fixed gap loses meaning once it drops below the strike's own
        // precision: the interval quantizes, and collapses entirely when kl and
        // kr coincide. The floor sits far below any usual gap.
        double g = Max(gap, Math.Abs(strike) * 1.0e-10);
        double kl = Max(strike - (g / 2.0), m);
        double kr = kl + g;
        return (type == OptionType.Call ? 1.0 : -1.0) * (OptionPrice(kl, type, discount) - OptionPrice(kr, type, discount)) / g;
    }

    /// <summary>The probability density at a strike, as the difference quotient of two digital call prices.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="gap">The strikes' distance, at least the strike's size times 1e-6.</param>
    /// <returns>The density.</returns>
    public virtual double Density(double strike, double discount = 1.0, double gap = 1.0E-4)
    {
        double m = VolatilityType == VolatilityType.ShiftedLognormal ? -Shift : -NumericLimits.QL_MAX_REAL;

        // A second difference, so roundoff scales as |strike|*eps/g^2 rather
        // than the digital's |strike|*eps/g. The floor has to be
        // correspondingly larger. The same g must reach the digitals and the
        // divisor, or their own floor leaves the three inconsistent.
        double g = Max(gap, Math.Abs(strike) * 1.0e-6);
        double kl = Max(strike - (g / 2.0), m);
        double kr = kl + g;
        return (DigitalOptionPrice(kl, OptionType.Call, discount, g) - DigitalOptionPrice(kr, OptionType.Call, discount, g)) / g;
    }

    /// <summary>The vega of an option on the at-the-money level, for a 1% change of the volatility.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The vega.</returns>
    /// <exception cref="NanoQLException">The section has no at-the-money level, or is normal.</exception>
    public virtual double Vega(double strike, double discount = 1.0)
    {
        double atm = AtmLevel()
            ?? throw new NanoQLException("smile section must provide atm level to compute option vega");
        if (VolatilityType == VolatilityType.ShiftedLognormal)
        {
            return BlackFormulaVolDerivative(strike, atm, Math.Sqrt(Variance(strike)), ExerciseTime, discount, Shift) * 0.01;
        }
        else
        {
            throw new NanoQLException("vega for normal smilesection not yet implemented");
        }
    }

    /// <summary>The variance at a strike; by default the volatility squared times the exercise time.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The variance.</returns>
    protected virtual double VarianceImpl(double strike)
    {
        double v = VolatilityImpl(strike);
        return v * v * ExerciseTime;
    }

    /// <summary>The volatility at a strike.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    protected abstract double VolatilityImpl(double strike);

    // std::max(a, b), which keeps a where b is NaN.
    private static double Max(double a, double b) => a < b ? b : a;

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}
