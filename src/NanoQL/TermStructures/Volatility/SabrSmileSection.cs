// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/sabrsmilesection.hpp ql/termstructures/volatility/sabrsmilesection.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>A SABR smile section: the shifted SABR volatility at given parameters.</summary>
/// <remarks>
/// The volatility at a strike below 1e-5 minus the shift is the volatility there, a NaN strike's included, as
/// QuantLib's <c>std::max</c> floors it. QuantLib's date constructor without a reference date, whose section floats
/// with the evaluation date, is <see cref="Create"/>, which takes the evaluation context and fixes the date. QuantLib
/// reads the parameters unchecked; fewer than four are refused here.
/// </remarks>
public sealed class SabrSmileSection : SmileSection
{
    private readonly double alpha;
    private readonly double beta;
    private readonly double nu;
    private readonly double rho;
    private readonly double forward;
    private readonly double shift;

    /// <summary>Initializes a new instance of the <see cref="SabrSmileSection"/> class for an exercise time.</summary>
    /// <param name="timeToExpiry">The exercise time.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="sabrParameters">α, β, ν and ρ, in that order; values after the fourth are ignored.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="volatilityType">The volatility type.</param>
    /// <exception cref="ArgumentException">The time is negative, there are fewer than four parameters, the forward
    /// plus the shift isn't positive, or a parameter is out of its range.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its type.</exception>
    public SabrSmileSection(
        double timeToExpiry,
        double forward,
        ReadOnlySpan<double> sabrParameters,
        double shift = 0.0,
        VolatilityType volatilityType = VolatilityType.ShiftedLognormal)
        : base(timeToExpiry, null, Checked(volatilityType), shift)
    {
        this.forward = forward;
        this.shift = shift;
        (alpha, beta, nu, rho) = Initialise(sabrParameters, forward, shift);
    }

    /// <summary>Initializes a new instance of the <see cref="SabrSmileSection"/> class for an exercise date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="sabrParameters">α, β, ν and ρ, in that order; values after the fourth are ignored.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dc">The day counter of the exercise time, or <see langword="null"/> for QuantLib's default,
    /// Actual/365 (Fixed).</param>
    /// <param name="shift">The shift.</param>
    /// <param name="volatilityType">The volatility type.</param>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, the exercise date is before the reference
    /// date, there are fewer than four parameters, the forward plus the shift isn't positive, or a parameter is out of
    /// its range.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its type.</exception>
    public SabrSmileSection(
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
        this.shift = shift;
        (alpha, beta, nu, rho) = Initialise(sabrParameters, forward, shift);
    }

    /// <summary>Gets α.</summary>
    public double Alpha => alpha;

    /// <summary>Gets β.</summary>
    public double Beta => beta;

    /// <summary>Gets ν.</summary>
    public double Nu => nu;

    /// <summary>Gets ρ.</summary>
    public double Rho => rho;

    /// <summary>A SABR smile section for an exercise date, whose reference date is the evaluation date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="sabrParameters">α, β, ν and ρ, in that order.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="dc">The day counter of the exercise time, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <param name="shift">The shift.</param>
    /// <param name="volatilityType">The volatility type.</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its type.</exception>
    public static SabrSmileSection Create(
        Date d,
        double forward,
        ReadOnlySpan<double> sabrParameters,
        EvaluationContext context,
        DayCounter? dc = null,
        double shift = 0.0,
        VolatilityType volatilityType = VolatilityType.ShiftedLognormal)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new SabrSmileSection(d, forward, sabrParameters, context.EvaluationDate, dc, shift, volatilityType);
    }

    /// <summary>The minimum strike: minus the shift.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => -shift;

    /// <summary>The maximum strike: the largest real.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <summary>The at-the-money level: the forward.</summary>
    /// <returns>The forward.</returns>
    public override double? AtmLevel() => forward;

    /// <inheritdoc/>
    protected override double VarianceImpl(double strike)
    {
        double vol = VolatilityImpl(strike);
        return vol * vol * ExerciseTime;
    }

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike)
    {
        double floor = 0.00001 - Shift;
        strike = floor < strike ? strike : floor;
        return SabrFunctions.ShiftedVolatility(
            strike, forward, ExerciseTime, alpha, beta, nu, rho, shift, VolatilityType);
    }

    // The type is this constructor's own argument, refused under its own name before the base's check.
    private static VolatilityType Checked(VolatilityType volatilityType)
    {
        EnumArguments.Check(volatilityType, nameof(volatilityType));
        return volatilityType;
    }

    private static (double Alpha, double Beta, double Nu, double Rho) Initialise(
        ReadOnlySpan<double> sabrParameters, double forward, double shift)
    {
        // QuantLib reads the four parameters unchecked.
        if (sabrParameters.Length < 4)
        {
            throw new ArgumentException(
                $"sabr expects 4 parameters (alpha,beta,nu,rho) but ({sabrParameters.Length}) given",
                nameof(sabrParameters));
        }

        double alpha = sabrParameters[0], beta = sabrParameters[1], nu = sabrParameters[2], rho = sabrParameters[3];
        if (!(forward + shift > 0.0))
        {
            throw new ArgumentException(
                $"at the money forward rate + shift must be positive: {DataFormattersFunctions.Rate(forward)} with shift {DataFormattersFunctions.Rate(shift)} not allowed",
                nameof(forward));
        }

        SabrFunctions.ValidateSabrParameters(alpha, beta, nu, rho);
        return (alpha, beta, nu, rho);
    }
}
