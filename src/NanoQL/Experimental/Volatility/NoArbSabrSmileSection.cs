// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/noarbsabrsmilesection.hpp ql/experimental/volatility/noarbsabrsmilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>
/// A smile section on Doust's no-arbitrage SABR model: its prices, digitals and density are the model's, its
/// volatilities those prices' implied Black volatilities, the SABR expansion's where none is found.
/// </summary>
/// <remarks>
/// Immutable, the model built at construction. QuantLib's section dated by a date floats with the evaluation date; here
/// the date's constructor takes the reference date, and <see cref="Create"/> the context.
/// </remarks>
[Experimental("NQL0001")]
public sealed class NoArbSabrSmileSection : SmileSection
{
    private readonly double forward;
    private readonly double[] parameters;

    /// <summary>Initializes a new instance of the <see cref="NoArbSabrSmileSection"/> class at a time to expiry.</summary>
    /// <param name="timeToExpiry">The time to expiry.</param>
    /// <param name="forward">The forward, positive.</param>
    /// <param name="sabrParameters">α, β, ν and ρ.</param>
    /// <param name="shift">The shift, which must be 0.</param>
    /// <param name="volatilityType">The volatility type.</param>
    /// <exception cref="ArgumentException">There are fewer than four parameters, the forward isn't positive, the shift
    /// isn't 0, or the model refuses the parameters.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="NanoQLException">The model's integration fails.</exception>
    public NoArbSabrSmileSection(
        double timeToExpiry,
        double forward,
        ReadOnlySpan<double> sabrParameters,
        double shift = 0.0,
        VolatilityType volatilityType = VolatilityType.ShiftedLognormal)
        : base(timeToExpiry, null, Checked(volatilityType), shift)
    {
        this.forward = forward;
        parameters = sabrParameters.ToArray();
        Model = Init(forward, parameters, shift, ExerciseTime);
    }

    /// <summary>Initializes a new instance of the <see cref="NoArbSabrSmileSection"/> class at an exercise date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="forward">The forward, positive.</param>
    /// <param name="sabrParameters">α, β, ν and ρ.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for QuantLib's default, Actual/365 (Fixed).</param>
    /// <param name="shift">The shift, which must be 0.</param>
    /// <param name="volatilityType">The volatility type.</param>
    /// <exception cref="ArgumentException">There are fewer than four parameters, the forward isn't positive, the shift
    /// isn't 0, a date is <c>default(Date)</c>, or the model refuses the parameters.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="NanoQLException">The model's integration fails.</exception>
    public NoArbSabrSmileSection(
        Date d,
        double forward,
        ReadOnlySpan<double> sabrParameters,
        Date referenceDate,
        DayCounter? dc = null,
        double shift = 0.0,
        VolatilityType volatilityType = VolatilityType.ShiftedLognormal)
        : base(d, dc ?? new Actual365Fixed(), referenceDate, Checked(volatilityType), shift)
    {
        this.forward = forward;
        parameters = sabrParameters.ToArray();
        Model = Init(forward, parameters, shift, ExerciseTime);
    }

    /// <summary>Gets the no-arbitrage SABR model.</summary>
    public NoArbSabrModel Model { get; }

    /// <summary>Creates the section at an exercise date, its reference date the context's evaluation date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="forward">The forward, positive.</param>
    /// <param name="sabrParameters">α, β, ν and ρ.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="dc">The day counter, or <see langword="null"/> for QuantLib's default, Actual/365 (Fixed).</param>
    /// <param name="shift">The shift, which must be 0.</param>
    /// <param name="volatilityType">The volatility type.</param>
    /// <returns>The section.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are fewer than four parameters, the forward isn't positive, the shift
    /// isn't 0, a date is <c>default(Date)</c>, or the model refuses the parameters.</exception>
    /// <exception cref="NanoQLException">The model's integration fails.</exception>
    public static NoArbSabrSmileSection Create(
        Date d,
        double forward,
        ReadOnlySpan<double> sabrParameters,
        EvaluationContext context,
        DayCounter? dc = null,
        double shift = 0.0,
        VolatilityType volatilityType = VolatilityType.ShiftedLognormal)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new NoArbSabrSmileSection(d, forward, sabrParameters, context.EvaluationDate, dc, shift, volatilityType);
    }

    /// <inheritdoc/>
    public override double MinStrike() => 0.0;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    public override double? AtmLevel() => forward;

    /// <summary>The model's price at a strike, a put by parity.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">The option type.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The price.</returns>
    public override double OptionPrice(double strike, OptionType type = OptionType.Call, double discount = 1.0)
    {
        double call = Model.OptionPrice(strike);
        return discount * (type == OptionType.Call ? call : call - (forward - strike));
    }

    /// <summary>The model's digital price at a strike, a put as its complement; the gap isn't read.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">The option type.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="gap">The gap, which the model's exact digital doesn't read.</param>
    /// <returns>The price.</returns>
    public override double DigitalOptionPrice(
        double strike, OptionType type = OptionType.Call, double discount = 1.0, double gap = 1.0e-5)
    {
        double call = Model.DigitalOptionPrice(strike);
        return discount * (type == OptionType.Call ? call : 1.0 - call);
    }

    /// <summary>The model's density at a strike; the gap isn't read.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="gap">The gap, which the model's exact density doesn't read.</param>
    /// <returns>The density.</returns>
    public override double Density(double strike, double discount = 1.0, double gap = 1.0E-4) =>
        discount * Model.Density(strike);

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike)
    {
        double impliedVol = 0.0;
        try
        {
            OptionType type = strike >= forward ? OptionType.Call : OptionType.Put;
            impliedVol = BlackFormulaFunctions.BlackFormulaImpliedStdDev(
                type, strike, forward, OptionPrice(strike, type, 1.0), 1.0) / Math.Sqrt(ExerciseTime);
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            // QuantLib's catch of everything: the expansion's volatility below
        }

        if (impliedVol == 0.0)
        {
            impliedVol = SabrFunctions.UnsafeSabrVolatility(
                strike, forward, ExerciseTime, parameters[0], parameters[1], parameters[2], parameters[3], VolatilityType);
        }

        return impliedVol;
    }

    private static VolatilityType Checked(VolatilityType volatilityType)
    {
        EnumArguments.Check(volatilityType, nameof(volatilityType));
        return volatilityType;
    }

    // QuantLib's init
    private static NoArbSabrModel Init(double forward, double[] parameters, double shift, double exerciseTime)
    {
        if (!(parameters.Length >= 4))
        {
            throw new ArgumentException(
                $"sabr expects 4 parameters (alpha,beta,nu,rho) but ({parameters.Length}) given", nameof(parameters));
        }

        if (!(forward > 0.0))
        {
            throw new ArgumentException($"forward ({DataFormattersFunctions.General(forward)}) must be positive", nameof(forward));
        }

        if (!(shift == 0.0))
        {
            throw new ArgumentException(
                $"shift ({DataFormattersFunctions.General(shift)}) must be zero, other shifts are not implemented yet",
                nameof(shift));
        }

        return new NoArbSabrModel(exerciseTime, forward, parameters[0], parameters[1], parameters[2], parameters[3]);
    }
}
