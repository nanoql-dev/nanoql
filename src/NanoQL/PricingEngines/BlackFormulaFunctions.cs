// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2008 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mark Joshi
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2007 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Chiara Fornarola
// SPDX-FileCopyrightText: Copyright (C) 2013 Gary Kennedy
// SPDX-FileCopyrightText: Copyright (C) 2015, 2024 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2019 Wojciech Ślusarski
// SPDX-FileCopyrightText: Copyright (C) 2020 Marcin Rybacki
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006, 2007, 2008, 2012 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/blackformula.hpp ql/pricingengines/blackformula.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Numerics;
using static NanoQL.Numerics.ComparisonFunctions;
using static NanoQL.Numerics.FunctionalFunctions;

namespace NanoQL.PricingEngines;

/// <summary>Black and Bachelier formulas: prices, derivatives, in-the-money probabilities and implied volatilities.</summary>
/// <remarks>
/// <para>
/// The Black formulas take a standard deviation, the volatility times the square root of the time to maturity, instead
/// of a volatility; the Bachelier formulas take an absolute (normal) volatility, not a percentage one.
/// </para>
/// <para>
/// Each payoff overload refuses a <see langword="null"/> payoff, where QuantLib dereferences it, and passes the
/// payoff's option type and strike on: a refusal of the strike names <c>strike</c>, as the overload it calls raises it.
/// QuantLib's checks run in QuantLib's order, after the checks nanoql adds on entry (an option type the formula computes
/// with, a negative <c>maxIterations</c>), with QuantLib's texts.
/// </para>
/// </remarks>
public static class BlackFormulaFunctions
{
    // ql/mathconstants.hpp's constants, as QuantLib defines them.
    private const double M_SQRT2 = 1.41421356237309504880;
    private const double M_SQRTPI = 1.77245385090551602792981;
    private const double M_2_PI = 0.636619772367581343076;
    private const double M_PI_2 = 1.57079632679489661923;

    // bachelierBlackFormulaImpliedVolChoi's function-local constant.
    private static readonly double SqrtQlEpsilon = Math.Sqrt(NumericLimits.QL_EPSILON);

    /// <summary>
    /// Black's 1976 formula: the price of an option on a forward, with a shifted lognormal volatility when a
    /// displacement is given.
    /// </summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The price comes out negative.</exception>
    public static double BlackFormula(
        OptionType optionType,
        double strike,
        double forward,
        double stdDev,
        double discount = 1.0,
        double displacement = 0.0)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        CheckParameters(strike, forward, displacement);
        CheckStdDev(stdDev);
        CheckDiscount(discount);

        int sign = (int)optionType;

        if (stdDev == 0.0)
        {
            return Max((forward - strike) * sign, 0.0) * discount;
        }

        forward += displacement;
        strike += displacement;

        // since displacement is non-negative strike==0 iff displacement==0
        // so returning forward*discount is OK
        if (strike == 0.0)
        {
            return optionType == OptionType.Call ? forward * discount : 0.0;
        }

        double d1 = (Math.Log(forward / strike) / stdDev) + (0.5 * stdDev);
        double d2 = d1 - stdDev;
        var phi = new CumulativeNormalDistribution();
        double nd1 = phi.Evaluate(sign * d1);
        double nd2 = phi.Evaluate(sign * d2);
        double result = discount * sign * ((forward * nd1) - (strike * nd2));
        if (!(result >= 0.0))
        {
            throw new NanoQLException(
                $"negative value ({Show(result)}) for {Show(stdDev)} stdDev, {optionType} option, {Show(strike)} strike , "
                + $"{Show(forward)} forward");
        }

        return result;
    }

    /// <summary>Black's 1976 formula on a plain-vanilla payoff.</summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter, or the payoff's strike, is out of its domain.</exception>
    /// <exception cref="NanoQLException">The price comes out negative.</exception>
    public static double BlackFormula(
        PlainVanillaPayoff payoff,
        double forward,
        double stdDev,
        double discount = 1.0,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormula(payoff.OptionType, payoff.Strike, forward, stdDev, discount, displacement);
    }

    /// <summary>The derivative of Black's 1976 formula with respect to the forward.</summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    public static double BlackFormulaForwardDerivative(
        OptionType optionType,
        double strike,
        double forward,
        double stdDev,
        double discount = 1.0,
        double displacement = 0.0)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        CheckParameters(strike, forward, displacement);
        CheckStdDev(stdDev);
        CheckDiscount(discount);

        int sign = (int)optionType;

        if (stdDev == 0.0)
        {
            return sign * Max(1.0 * SignFunctions.Sign((forward - strike) * sign), 0.0) * discount;
        }

        forward += displacement;
        strike += displacement;

        if (strike == 0.0)
        {
            return optionType == OptionType.Call ? discount : 0.0;
        }

        double d1 = (Math.Log(forward / strike) / stdDev) + (0.5 * stdDev);
        var phi = new CumulativeNormalDistribution();
        return sign * phi.Evaluate(sign * d1) * discount;
    }

    /// <summary>The derivative of Black's 1976 formula with respect to the forward, on a plain-vanilla payoff.</summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter, or the payoff's strike, is out of its domain.</exception>
    public static double BlackFormulaForwardDerivative(
        PlainVanillaPayoff payoff,
        double forward,
        double stdDev,
        double discount = 1.0,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormulaForwardDerivative(payoff.OptionType, payoff.Strike, forward, stdDev, discount, displacement);
    }

    /// <summary>
    /// An approximation of Black's 1976 implied standard deviation: Brenner and Subrahmanyan's (1988) and Feinstein's
    /// (1988) approximation for an at-the-money forward option, with Corrado and Miller's (1996) extended moneyness
    /// approximation.
    /// </summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="blackPrice">The option's price.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The implied standard deviation.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The standard deviation comes out negative or NaN.</exception>
    public static double BlackFormulaImpliedStdDevApproximation(
        OptionType optionType,
        double strike,
        double forward,
        double blackPrice,
        double discount = 1.0,
        double displacement = 0.0)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        CheckParameters(strike, forward, displacement);
        if (!(blackPrice >= 0.0))
        {
            throw new ArgumentException($"blackPrice ({Show(blackPrice)}) must be non-negative", nameof(blackPrice));
        }

        CheckDiscount(discount);

        double stdDev;
        forward += displacement;
        strike += displacement;
        if (strike == forward)
        {
            // Brenner-Subrahmanyan (1988) and Feinstein (1988) ATM approx.
            stdDev = blackPrice / discount * Math.Sqrt(2.0 * Math.PI) / forward;
        }
        else
        {
            // Corrado and Miller extended moneyness approximation
            double moneynessDelta = (int)optionType * (forward - strike);
            double moneynessDelta_2 = moneynessDelta / 2.0;
            double temp = (blackPrice / discount) - moneynessDelta_2;
            double moneynessDelta_PI = moneynessDelta * moneynessDelta / Math.PI;
            double temp2 = (temp * temp) - moneynessDelta_PI;
            if (temp2 < 0.0)
            {
                // approximation breaks down, 2 alternatives:
                // 1. zero it
                temp2 = 0.0;

                // 2. Manaster-Koehler (1982) efficient Newton-Raphson seed
                // return std::fabs(std::log(forward/strike))*std::sqrt(2.0);
            }

            temp2 = Math.Sqrt(temp2);
            temp += temp2;
            temp *= Math.Sqrt(2.0 * Math.PI);
            stdDev = temp / (forward + strike);
        }

        if (!(stdDev >= 0.0))
        {
            throw new NanoQLException($"stdDev ({Show(stdDev)}) must be non-negative");
        }

        return stdDev;
    }

    /// <summary>
    /// The approximation of Black's 1976 implied standard deviation of
    /// <see cref="BlackFormulaImpliedStdDevApproximation(OptionType, double, double, double, double, double)"/>, on a
    /// plain-vanilla payoff.
    /// </summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="blackPrice">The option's price.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The implied standard deviation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter, or the payoff's strike, is out of its domain.</exception>
    /// <exception cref="NanoQLException">The standard deviation comes out negative or NaN.</exception>
    public static double BlackFormulaImpliedStdDevApproximation(
        PlainVanillaPayoff payoff,
        double forward,
        double blackPrice,
        double discount = 1.0,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormulaImpliedStdDevApproximation(
            payoff.OptionType, payoff.Strike, forward, blackPrice, discount, displacement);
    }

    /// <summary>
    /// An approximation of Black's 1976 implied standard deviation following Chambers and Nawalkha, "An improved
    /// approach to computing implied volatility", The Financial Review, 2001, 89-100. The at-the-money option's price
    /// must be known.
    /// </summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="blackPrice">The option's price.</param>
    /// <param name="blackAtmPrice">The at-the-money option's price.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The implied standard deviation.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="optionType"/> isn't a member of its type, which <see cref="BlackFormula(OptionType, double, double, double, double, double)"/>
    /// refuses.
    /// </exception>
    /// <exception cref="NanoQLException">The standard deviation comes out negative or NaN.</exception>
    public static double BlackFormulaImpliedStdDevChambers(
        OptionType optionType,
        double strike,
        double forward,
        double blackPrice,
        double blackAtmPrice,
        double discount = 1.0,
        double displacement = 0.0)
    {
        CheckParameters(strike, forward, displacement);
        if (!(blackPrice >= 0.0))
        {
            throw new ArgumentException($"blackPrice ({Show(blackPrice)}) must be non-negative", nameof(blackPrice));
        }

        if (!(blackAtmPrice >= 0.0))
        {
            throw new ArgumentException($"blackAtmPrice ({Show(blackAtmPrice)}) must be non-negative", nameof(blackAtmPrice));
        }

        CheckDiscount(discount);

        double stdDev;

        forward += displacement;
        strike += displacement;
        blackPrice /= discount;
        blackAtmPrice /= discount;

        double s0 = M_SQRT2 * M_SQRTPI * blackAtmPrice / forward; // Brenner-Subrahmanyam formula
        double priceAtmVol = BlackFormula(optionType, strike, forward, s0, 1.0, 0.0);
        double dc = blackPrice - priceAtmVol;

        if (Close(dc, 0.0))
        {
            stdDev = s0;
        }
        else
        {
            double d1 = BlackFormulaStdDevDerivative(strike, forward, s0, 1.0, 0.0);
            double d2 = BlackFormulaStdDevSecondDerivative(strike, forward, s0, 1.0, 0.0);
            double ds = 0.0;
            double tmp = (d1 * d1) + (2.0 * d2 * dc);
            if (Math.Abs(d2) > 1E-10 && tmp >= 0.0)
            {
                ds = (-d1 + Math.Sqrt(tmp)) / d2; // second order approximation
            }
            else if (Math.Abs(d1) > 1E-10)
            {
                ds = dc / d1; // first order approximation
            }

            stdDev = s0 + ds;
        }

        if (!(stdDev >= 0.0))
        {
            throw new NanoQLException($"stdDev ({Show(stdDev)}) must be non-negative");
        }

        return stdDev;
    }

    /// <summary>
    /// Chambers and Nawalkha's approximation of Black's 1976 implied standard deviation of
    /// <see cref="BlackFormulaImpliedStdDevChambers(OptionType, double, double, double, double, double, double)"/>, on a
    /// plain-vanilla payoff.
    /// </summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="blackPrice">The option's price.</param>
    /// <param name="blackAtmPrice">The at-the-money option's price.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The implied standard deviation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter, or the payoff's strike, is out of its domain.</exception>
    /// <exception cref="NanoQLException">The standard deviation comes out negative or NaN.</exception>
    public static double BlackFormulaImpliedStdDevChambers(
        PlainVanillaPayoff payoff,
        double forward,
        double blackPrice,
        double blackAtmPrice,
        double discount = 1.0,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormulaImpliedStdDevChambers(
            payoff.OptionType, payoff.Strike, forward, blackPrice, blackAtmPrice, discount, displacement);
    }

    /// <summary>
    /// An approximation of Black's 1976 implied standard deviation following R. Radoicic, D. Stefanica, "An Explicit
    /// Implicit Volatility Formula", and J. Gatheral, I. Matic, R. Radoicic, D. Stefanica, "Tighter Bounds for Implied
    /// Volatility".
    /// </summary>
    /// <param name="optionType">Call or put; any value but <see cref="OptionType.Call"/> is read as a put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="blackPrice">The option's price.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The implied standard deviation.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    public static double BlackFormulaImpliedStdDevApproximationRS(
        OptionType optionType,
        double strike,
        double forward,
        double blackPrice,
        double discount = 1.0,
        double displacement = 0.0)
    {
        CheckParameters(strike, forward, displacement);
        if (!(blackPrice >= 0.0))
        {
            throw new ArgumentException($"blackPrice ({Show(blackPrice)}) must be non-negative", nameof(blackPrice));
        }

        CheckDiscount(discount);

        forward += displacement;
        strike += displacement;

        double ey = forward / strike;
        double ey2 = ey * ey;
        double y = Math.Log(ey);
        double alpha = blackPrice / (strike * discount);
        double r = (2 * alpha) + ((optionType == OptionType.Call) ? (-ey + 1.0) : (ey - 1.0));
        double r2 = r * r;

        double a = Math.Exp((1.0 - M_2_PI) * y);
        double bigA = Squared(a - (1.0 / a));
        double b = Math.Exp(M_2_PI * y);
        double bigB = (4.0 * (b + (1 / b)))
            - (2 * strike / forward * (a + (1.0 / a)) * (ey2 + 1 - r2));
        double c = Max(0.0, (r2 - Squared(ey - 1)) * (Squared(ey + 1) - r2) / ey2);

        double beta = 2 * c / (bigB + Math.Sqrt((bigB * bigB) + (4 * bigA * c)));
        double gamma = -M_PI_2 * Math.Log(beta);

        if (y >= 0.0)
        {
            double m0 = strike * discount * ((optionType == OptionType.Call)
                ? ((ey * Af(Math.Sqrt(2 * y))) - 0.5)
                : (0.5 - (ey * Af(-Math.Sqrt(2 * y)))));

            if (blackPrice <= m0)
            {
                return SqrtDiff(gamma, y);
            }
            else
            {
                return Math.Sqrt(gamma + y) + Math.Sqrt(gamma - y);
            }
        }
        else
        {
            double m0 = strike * discount * ((optionType == OptionType.Call)
                ? ((0.5 * ey) - Af(-Math.Sqrt(-2 * y)))
                : (Af(Math.Sqrt(-2 * y)) - (0.5 * ey)));

            if (blackPrice <= m0)
            {
                return SqrtDiff(gamma, -y);
            }
            else
            {
                return Math.Sqrt(gamma + y) + Math.Sqrt(gamma - y);
            }
        }
    }

    /// <summary>
    /// Radoicic and Stefanica's approximation of Black's 1976 implied standard deviation of
    /// <see cref="BlackFormulaImpliedStdDevApproximationRS(OptionType, double, double, double, double, double)"/>, on a
    /// plain-vanilla payoff.
    /// </summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="blackPrice">The option's price.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The implied standard deviation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter, or the payoff's strike, is out of its domain.</exception>
    public static double BlackFormulaImpliedStdDevApproximationRS(
        PlainVanillaPayoff payoff,
        double forward,
        double blackPrice,
        double discount = 1.0,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormulaImpliedStdDevApproximationRS(
            payoff.OptionType, payoff.Strike, forward, blackPrice, discount, displacement);
    }

    /// <summary>
    /// Black's 1976 implied standard deviation, solved for with a safe Newton solver on the out-of-the-money option,
    /// whose greater vega/price ratio makes it the more robust one.
    /// </summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="blackPrice">The option's price.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <param name="guess">
    /// The solver's initial guess; <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for
    /// <see cref="BlackFormulaImpliedStdDevApproximation(OptionType, double, double, double, double, double)"/>'s value,
    /// which QuantLib computes on the displaced strike and forward and with the displacement again.
    /// </param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxIterations">The solver's maximum number of function evaluations.</param>
    /// <returns>The implied standard deviation, in [0, 24].</returns>
    /// <exception cref="ArgumentException">
    /// A parameter is out of its domain, the price is negative or implies a negative price of the other option by
    /// put-call parity, or the solver refuses the guess or the accuracy.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="optionType"/> isn't a member of its type, or <paramref name="maxIterations"/> is negative.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The solver finds no root in [0, 24] or exceeds its evaluations, or the guess's approximation fails.
    /// </exception>
    public static double BlackFormulaImpliedStdDev(
        OptionType optionType,
        double strike,
        double forward,
        double blackPrice,
        double discount = 1.0,
        double displacement = 0.0,
        double? guess = null,
        double accuracy = 1.0e-6,
        int maxIterations = 100)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);
        CheckParameters(strike, forward, displacement);

        CheckDiscount(discount);

        if (!(blackPrice >= 0.0))
        {
            throw new ArgumentException($"option price ({Show(blackPrice)}) must be non-negative", nameof(blackPrice));
        }

        // check the price of the "other" option implied by put-call paity
        double otherOptionPrice = blackPrice - ((int)optionType * (forward - strike) * discount);
        if (!(otherOptionPrice >= 0.0))
        {
            throw new ArgumentException(
                $"negative {(OptionType)(-1 * (int)optionType)} price ({Show(otherOptionPrice)}) implied by put-call "
                + $"parity. No solution exists for {optionType} strike {Show(strike)}, forward {Show(forward)}, price "
                + $"{Show(blackPrice)}, deflator {Show(discount)}",
                nameof(blackPrice));
        }

        // solve for the out-of-the-money option which has
        // greater vega/price ratio, i.e.
        // it is numerically more robust for implied vol calculations
        if (optionType == OptionType.Put && strike > forward)
        {
            optionType = OptionType.Call;
            blackPrice = otherOptionPrice;
        }

        if (optionType == OptionType.Call && strike < forward)
        {
            optionType = OptionType.Put;
            blackPrice = otherOptionPrice;
        }

        strike += displacement;
        forward += displacement;

        double initialGuess;
        if (guess is not double g)
        {
            initialGuess = BlackFormulaImpliedStdDevApproximation(
                optionType, strike, forward, blackPrice, discount, displacement);
        }
        else
        {
            if (!(g >= 0.0))
            {
                throw new ArgumentException($"stdDev guess ({Show(g)}) must be non-negative", nameof(guess));
            }

            initialGuess = g;
        }

        var f = new BlackImpliedStdDevHelper(optionType, strike, forward, blackPrice / discount);
        NewtonSafe solver = new NewtonSafe().SetMaxEvaluations(maxIterations);
        double minSdtDev = 0.0, maxStdDev = 24.0; // 24 = 300% * sqrt(60)
        double stdDev = solver.Solve(f, accuracy, initialGuess, minSdtDev, maxStdDev);
        if (!(stdDev >= 0.0))
        {
            throw new NanoQLException($"stdDev ({Show(stdDev)}) must be non-negative");
        }

        return stdDev;
    }

    /// <summary>Black's 1976 implied standard deviation, on a plain-vanilla payoff.</summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="blackPrice">The option's price.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <param name="guess">The solver's initial guess; <see langword="null"/> for the approximation's value.</param>
    /// <param name="accuracy">The solver's accuracy.</param>
    /// <param name="maxIterations">The solver's maximum number of function evaluations.</param>
    /// <returns>The implied standard deviation, in [0, 24].</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A parameter, or the payoff's strike, is out of its domain, the price is refused, or the solver refuses the guess
    /// or the accuracy.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> is negative.</exception>
    /// <exception cref="NanoQLException">The solver fails, or the guess's approximation fails.</exception>
    public static double BlackFormulaImpliedStdDev(
        PlainVanillaPayoff payoff,
        double forward,
        double blackPrice,
        double discount = 1.0,
        double displacement = 0.0,
        double? guess = null,
        double accuracy = 1.0e-6,
        int maxIterations = 100)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormulaImpliedStdDev(
            payoff.OptionType, payoff.Strike, forward, blackPrice, discount, displacement, guess, accuracy, maxIterations);
    }

    /// <summary>
    /// Black's 1976 implied standard deviation by M. Li's "An Adaptive Successive Over-relaxation Method for Computing
    /// the Black-Scholes Implied Volatility", starting from R. Radoicic and D. Stefanica's "An Explicit Implicit
    /// Volatility Formula".
    /// </summary>
    /// <param name="optionType">Call or put; any value but <see cref="OptionType.Call"/> is read as a put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="blackPrice">The option's price.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <param name="guess">
    /// The iteration's starting point; <see langword="null"/>, QuantLib's <c>Null&lt;Real&gt;()</c>, for
    /// <see cref="BlackFormulaImpliedStdDevApproximationRS(OptionType, double, double, double, double, double)"/>'s
    /// value, which QuantLib computes on the displaced strike and forward and with the displacement again.
    /// </param>
    /// <param name="omega">The relaxation parameter ω.</param>
    /// <param name="accuracy">The accuracy, the bound on the last step.</param>
    /// <param name="maxIterations">The maximum number of iterations.</param>
    /// <returns>
    /// The implied standard deviation: QuantLib returns the iterate before the last step, which is within
    /// <paramref name="accuracy"/> of the last one.
    /// </returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The normalized price is negative, the iteration doesn't converge within its iterations, or the result is
    /// negative.
    /// </exception>
    /// <remarks>
    /// QuantLib doesn't check the strike, the forward and the displacement here, as the other formulas do; only the
    /// guess's approximation does, when it runs.
    /// </remarks>
    public static double BlackFormulaImpliedStdDevLiRS(
        OptionType optionType,
        double strike,
        double forward,
        double blackPrice,
        double discount = 1.0,
        double displacement = 0.0,
        double? guess = null,
        double omega = 1.0,
        double accuracy = 1.0e-6,
        int maxIterations = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxIterations);
        CheckDiscount(discount);

        if (!(blackPrice >= 0.0))
        {
            throw new ArgumentException($"option price ({Show(blackPrice)}) must be non-negative", nameof(blackPrice));
        }

        strike += displacement;
        forward += displacement;

        double initialGuess;
        if (guess is not double g)
        {
            initialGuess = BlackFormulaImpliedStdDevApproximationRS(
                optionType, strike, forward, blackPrice, discount, displacement);
        }
        else
        {
            if (!(g >= 0.0))
            {
                throw new ArgumentException($"stdDev guess ({Show(g)}) must be non-negative", nameof(guess));
            }

            initialGuess = g;
        }

        double x = Math.Log(forward / strike);
        double cs = (optionType == OptionType.Call)
            ? blackPrice / (forward * discount)
            : (blackPrice / (forward * discount)) + 1.0 - (strike / forward);

        if (!(cs >= 0.0))
        {
            throw new NanoQLException($"normalized call price ({Show(cs)}) must be positive");
        }

        if (x > 0)
        {
            // use in-out duality
            cs = (forward / strike * cs) + 1.0 - (forward / strike);
            if (!(cs >= 0.0))
            {
                throw new NanoQLException("negative option price from in-out duality");
            }

            x = -x;
        }

        if (cs == 0.0)
        {
            return 0.0;
        }

        int nIter = 0;
        double dv, vk, vkp1 = initialGuess;

        do
        {
            vk = vkp1;
            double alphaK = (1 + omega) / (1 + PhiLi(x, vk));
            vkp1 = (alphaK * G(vk, x, cs, omega)) + ((1 - alphaK) * vk);
            dv = Math.Abs(vkp1 - vk);
        }
        while (dv > accuracy && ++nIter < maxIterations);

        if (!(dv <= accuracy))
        {
            throw new NanoQLException("max iterations exceeded");
        }

        if (!(vk >= 0.0))
        {
            throw new NanoQLException($"stdDev ({Show(vk)}) must be non-negative");
        }

        return vk;
    }

    /// <summary>Li's implied standard deviation of Black's 1976 formula, on a plain-vanilla payoff.</summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="blackPrice">The option's price.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <param name="guess">The iteration's starting point; <see langword="null"/> for the RS approximation's value.</param>
    /// <param name="omega">The relaxation parameter ω.</param>
    /// <param name="accuracy">The accuracy, the bound on the last step.</param>
    /// <param name="maxIterations">The maximum number of iterations.</param>
    /// <returns>The implied standard deviation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> is negative.</exception>
    /// <exception cref="NanoQLException">The iteration fails.</exception>
    public static double BlackFormulaImpliedStdDevLiRS(
        PlainVanillaPayoff payoff,
        double forward,
        double blackPrice,
        double discount = 1.0,
        double displacement = 0.0,
        double? guess = null,
        double omega = 1.0,
        double accuracy = 1.0e-6,
        int maxIterations = 100)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormulaImpliedStdDevLiRS(
            payoff.OptionType,
            payoff.Strike,
            forward,
            blackPrice,
            discount,
            displacement,
            guess,
            omega,
            accuracy,
            maxIterations);
    }

    /// <summary>
    /// Black's 1976 probability of being in the money in the bond martingale measure, N(d2): a risk-neutral
    /// probability, not the real-world one.
    /// </summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time; unchecked.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    public static double BlackFormulaCashItmProbability(
        OptionType optionType,
        double strike,
        double forward,
        double stdDev,
        double displacement = 0.0)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        CheckParameters(strike, forward, displacement);

        int sign = (int)optionType;

        if (stdDev == 0.0)
        {
            return forward * sign > strike * sign ? 1.0 : 0.0;
        }

        forward += displacement;
        strike += displacement;
        if (strike == 0.0)
        {
            return optionType == OptionType.Call ? 1.0 : 0.0;
        }

        double d2 = (Math.Log(forward / strike) / stdDev) - (0.5 * stdDev);
        var phi = new CumulativeNormalDistribution();
        return phi.Evaluate(sign * d2);
    }

    /// <summary>Black's 1976 probability of being in the money in the bond martingale measure, on a plain-vanilla payoff.</summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time; unchecked.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter, or the payoff's strike, is out of its domain.</exception>
    public static double BlackFormulaCashItmProbability(
        PlainVanillaPayoff payoff,
        double forward,
        double stdDev,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormulaCashItmProbability(payoff.OptionType, payoff.Strike, forward, stdDev, displacement);
    }

    /// <summary>
    /// Black's 1976 probability of being in the money in the asset martingale measure, N(d1): a risk-neutral
    /// probability, not the real-world one.
    /// </summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time; unchecked.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    public static double BlackFormulaAssetItmProbability(
        OptionType optionType,
        double strike,
        double forward,
        double stdDev,
        double displacement = 0.0)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        CheckParameters(strike, forward, displacement);

        int sign = (int)optionType;

        if (stdDev == 0.0)
        {
            return forward * sign > strike * sign ? 1.0 : 0.0;
        }

        forward += displacement;
        strike += displacement;
        if (strike == 0.0)
        {
            return optionType == OptionType.Call ? 1.0 : 0.0;
        }

        double d1 = (Math.Log(forward / strike) / stdDev) + (0.5 * stdDev);
        var phi = new CumulativeNormalDistribution();
        return phi.Evaluate(sign * d1);
    }

    /// <summary>Black's 1976 probability of being in the money in the asset martingale measure, on a plain-vanilla payoff.</summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time; unchecked.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter, or the payoff's strike, is out of its domain.</exception>
    public static double BlackFormulaAssetItmProbability(
        PlainVanillaPayoff payoff,
        double forward,
        double stdDev,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormulaAssetItmProbability(payoff.OptionType, payoff.Strike, forward, stdDev, displacement);
    }

    /// <summary>
    /// The derivative of Black's 1976 formula with respect to the standard deviation: if T is the time to maturity, the
    /// Black vega is this derivative times √T.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    public static double BlackFormulaStdDevDerivative(
        double strike,
        double forward,
        double stdDev,
        double discount = 1.0,
        double displacement = 0.0)
    {
        CheckParameters(strike, forward, displacement);
        CheckStdDev(stdDev);
        CheckDiscount(discount);

        forward += displacement;
        strike += displacement;

        if (stdDev == 0.0 || strike == 0.0)
        {
            return 0.0;
        }

        double d1 = (Math.Log(forward / strike) / stdDev) + (.5 * stdDev);
        return discount * forward * new CumulativeNormalDistribution().Derivative(d1);
    }

    /// <summary>
    /// The derivative of Black's 1976 formula with respect to the implied volatility: the vega, which for a 1% change is
    /// multiplied by 1%.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time.</param>
    /// <param name="expiry">The time to expiry; unchecked.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    public static double BlackFormulaVolDerivative(
        double strike,
        double forward,
        double stdDev,
        double expiry,
        double discount = 1.0,
        double displacement = 0.0)
    {
        return BlackFormulaStdDevDerivative(strike, forward, stdDev, discount, displacement) * Math.Sqrt(expiry);
    }

    /// <summary>The derivative of Black's 1976 formula with respect to the standard deviation, on a plain-vanilla payoff.</summary>
    /// <param name="payoff">The payoff, of which only the strike is read.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter, or the payoff's strike, is out of its domain.</exception>
    public static double BlackFormulaStdDevDerivative(
        PlainVanillaPayoff payoff,
        double forward,
        double stdDev,
        double discount = 1.0,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormulaStdDevDerivative(payoff.Strike, forward, stdDev, discount, displacement);
    }

    /// <summary>The second derivative of Black's 1976 formula with respect to the standard deviation.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The second derivative.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <remarks>This form has no defaults, as QuantLib declares it; the payoff form has.</remarks>
    public static double BlackFormulaStdDevSecondDerivative(
        double strike,
        double forward,
        double stdDev,
        double discount,
        double displacement)
    {
        CheckParameters(strike, forward, displacement);
        CheckStdDev(stdDev);
        CheckDiscount(discount);

        forward += displacement;
        strike += displacement;

        if (stdDev == 0.0 || strike == 0.0)
        {
            return 0.0;
        }

        double d1 = (Math.Log(forward / strike) / stdDev) + (.5 * stdDev);
        double d1p = (-Math.Log(forward / strike) / (stdDev * stdDev)) + .5;
        return discount * forward * new NormalDistribution().Derivative(d1) * d1p;
    }

    /// <summary>
    /// The second derivative of Black's 1976 formula with respect to the standard deviation, on a plain-vanilla payoff.
    /// </summary>
    /// <param name="payoff">The payoff, of which only the strike is read.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <param name="displacement">The displacement of the shifted lognormal model.</param>
    /// <returns>The second derivative.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter, or the payoff's strike, is out of its domain.</exception>
    public static double BlackFormulaStdDevSecondDerivative(
        PlainVanillaPayoff payoff,
        double forward,
        double stdDev,
        double discount = 1.0,
        double displacement = 0.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BlackFormulaStdDevSecondDerivative(payoff.Strike, forward, stdDev, discount, displacement);
    }

    /// <summary>
    /// The Bachelier formula: the price of an option on a forward that is normal rather than lognormal, with a normal
    /// volatility.
    /// </summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the normal volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    /// <exception cref="NanoQLException">The price comes out negative.</exception>
    public static double BachelierBlackFormula(
        OptionType optionType,
        double strike,
        double forward,
        double stdDev,
        double discount = 1.0)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        CheckStdDev(stdDev);
        CheckDiscount(discount);

        double d = (forward - strike) * (int)optionType, h = d / stdDev;
        if (stdDev == 0.0)
        {
            return discount * Max(d, 0.0);
        }

        var phi = new CumulativeNormalDistribution();
        double result = discount * ((stdDev * phi.Derivative(h)) + (d * phi.Evaluate(h)));
        if (!(result >= 0.0))
        {
            throw new NanoQLException(
                $"negative value ({Show(result)}) for {Show(stdDev)} stdDev, {optionType} option, {Show(strike)} strike , "
                + $"{Show(forward)} forward");
        }

        return result;
    }

    /// <summary>The Bachelier formula on a plain-vanilla payoff.</summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the normal volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="NanoQLException">The price comes out negative.</exception>
    public static double BachelierBlackFormula(
        PlainVanillaPayoff payoff,
        double forward,
        double stdDev,
        double discount = 1.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BachelierBlackFormula(payoff.OptionType, payoff.Strike, forward, stdDev, discount);
    }

    /// <summary>The derivative of the Bachelier formula with respect to the forward.</summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the normal volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    /// <remarks>
    /// With a zero standard deviation, the result follows Boost's <c>sign</c> of the signed moneyness, which for a NaN
    /// reads its sign bit.
    /// </remarks>
    public static double BachelierBlackFormulaForwardDerivative(
        OptionType optionType,
        double strike,
        double forward,
        double stdDev,
        double discount = 1.0)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        CheckStdDev(stdDev);
        CheckDiscount(discount);
        int sign = (int)optionType;
        if (stdDev == 0.0)
        {
            return sign * Max(1.0 * SignFunctions.Sign((forward - strike) * sign), 0.0) * discount;
        }

        double d = (forward - strike) * sign, h = d / stdDev;
        var phi = new CumulativeNormalDistribution();
        return sign * phi.Evaluate(h) * discount;
    }

    /// <summary>The derivative of the Bachelier formula with respect to the forward, on a plain-vanilla payoff.</summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the normal volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    public static double BachelierBlackFormulaForwardDerivative(
        PlainVanillaPayoff payoff,
        double forward,
        double stdDev,
        double discount = 1.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BachelierBlackFormulaForwardDerivative(payoff.OptionType, payoff.Strike, forward, stdDev, discount);
    }

    /// <summary>
    /// An approximation of the Bachelier implied volatility: the analytic approximation of J. Choi, K. Kim and M. Kwak
    /// (2009), "Numerical Approximation of the Implied Volatility Under Arithmetic Brownian Motion", Applied Math.
    /// Finance, 16(3), pp. 261-268.
    /// </summary>
    /// <param name="optionType">Call or put; any value but <see cref="OptionType.Call"/> is read as a put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="tte">The time to expiry.</param>
    /// <param name="bachelierPrice">The option's price.</param>
    /// <param name="discount">The discount factor; unchecked.</param>
    /// <returns>The implied normal volatility.</returns>
    /// <exception cref="ArgumentException"><paramref name="tte"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The price implies a moneyness ratio ν outside [−1, 1].</exception>
    public static double BachelierBlackFormulaImpliedVolChoi(
        OptionType optionType,
        double strike,
        double forward,
        double tte,
        double bachelierPrice,
        double discount = 1.0)
    {
        if (!(tte > 0.0))
        {
            throw new ArgumentException($"tte ({Show(tte)}) must be positive", nameof(tte));
        }

        double forwardPremium = bachelierPrice / discount;

        double straddlePremium;
        if (optionType == OptionType.Call)
        {
            straddlePremium = (2.0 * forwardPremium) - (forward - strike);
        }
        else
        {
            straddlePremium = (2.0 * forwardPremium) + (forward - strike);
        }

        double nu = (forward - strike) / straddlePremium;
        if (!(nu < 1.0 || CloseEnough(nu, 1.0)))
        {
            throw new NanoQLException($"nu ({Show(nu)}) must be <= 1.0");
        }

        if (!(nu > -1.0 || CloseEnough(nu, -1.0)))
        {
            throw new NanoQLException($"nu ({Show(nu)}) must be >= -1.0");
        }

        nu = Max(-1.0 + NumericLimits.QL_EPSILON, Min(nu, 1.0 - NumericLimits.QL_EPSILON));

        // nu / arctanh(nu) -> 1 as nu -> 0
        double eta = (Math.Abs(nu) < SqrtQlEpsilon) ? 1.0 : nu / AtanhFunctions.Atanh(nu);

        double heta = H(eta);

        double impliedBpvol = Math.Sqrt(Math.PI / (2 * tte)) * straddlePremium * heta;

        return impliedBpvol;
    }

    /// <summary>
    /// The exact Bachelier implied volatility: the analytic implied volatility formula of Jaeckel (2017), "Implied
    /// Normal Volatility".
    /// </summary>
    /// <param name="optionType">Call or put; any value but <see cref="OptionType.Call"/> is read as a put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="tte">The time to expiry; unchecked.</param>
    /// <param name="bachelierPrice">The option's price.</param>
    /// <param name="discount">The discount factor; unchecked.</param>
    /// <returns>The implied normal volatility; 0 where the time value isn't positive and the price is close to its
    /// intrinsic value.</returns>
    /// <exception cref="NanoQLException">The price implies a negative time value.</exception>
    /// <remarks>
    /// The refusal's text names <c>bachelierBlackFormulaImpliedVolExact</c> and the price divided by the discount, as
    /// QuantLib's does. A price just above its intrinsic value inverts to its volatility, as QuantLib's master does
    /// after 1.44 (#2924), where 1.44 returns 0: a deep in-the-money price one ulp above its intrinsic value after the
    /// division by the discount inverts that rounding (<c>Divergences/BlackFormulaUpstreamFixesTests.cs</c>).
    /// </remarks>
    public static double BachelierBlackFormulaImpliedVol(
        OptionType optionType,
        double strike,
        double forward,
        double tte,
        double bachelierPrice,
        double discount = 1.0)
    {
        double theta = optionType == OptionType.Call ? 1.0 : -1.0;

        // compound bechelierPrice, so that effectively discount = 1

        bachelierPrice /= discount;

        // handle case strike = forward

        if (CloseEnough(strike, forward))
        {
            return bachelierPrice / (Math.Sqrt(tte) * PhiDensity(0.0));
        }

        // handle case strike != forward

        double intrinsicValue = Max(theta * (forward - strike), 0.0);
        double timeValue = bachelierPrice - intrinsicValue;

        // the subtraction above cancels, so compare the two operands directly:
        // close_enough against 0 degrades to an absolute tolerance of
        // (n*epsilon)^2, far below the rounding error of the difference itself;
        // QuantLib's fix after v1.44 (#2924): a positive time value inverts
        if (timeValue <= 0.0 && CloseEnough(bachelierPrice, intrinsicValue))
        {
            return 0.0;
        }

        if (!(timeValue > 0.0))
        {
            throw new NanoQLException(
                $"bachelierBlackFormulaImpliedVolExact(theta={Show(theta)},strike={Show(strike)},forward={Show(forward)},"
                + $"tte={Show(tte)},price={Show(bachelierPrice)}): option price implies negative time value ({Show(timeValue)})");
        }

        double phiTildeStar = -Math.Abs(timeValue / (strike - forward));
        double xstar = InversePhiTilde(phiTildeStar);
        double impliedVol = Math.Abs((strike - forward) / (xstar * Math.Sqrt(tte)));

        return impliedVol;
    }

    /// <summary>
    /// The derivative of the Bachelier formula with respect to the standard deviation: if T is the time to maturity,
    /// the vega is this derivative times √T.
    /// </summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the normal volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    public static double BachelierBlackFormulaStdDevDerivative(
        double strike,
        double forward,
        double stdDev,
        double discount = 1.0)
    {
        CheckStdDev(stdDev);
        CheckDiscount(discount);

        if (stdDev == 0.0)
        {
            return 0.0;
        }

        double d1 = (forward - strike) / stdDev;
        return discount * new CumulativeNormalDistribution().Derivative(d1);
    }

    /// <summary>
    /// The derivative of the Bachelier formula with respect to the standard deviation, on a plain-vanilla payoff.
    /// </summary>
    /// <param name="payoff">The payoff, of which only the strike is read.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the normal volatility times the square root of the time.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The derivative.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A parameter is out of its domain.</exception>
    public static double BachelierBlackFormulaStdDevDerivative(
        PlainVanillaPayoff payoff,
        double forward,
        double stdDev,
        double discount = 1.0)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BachelierBlackFormulaStdDevDerivative(payoff.Strike, forward, stdDev, discount);
    }

    /// <summary>
    /// The Bachelier probability of being in the money in the asset martingale measure, N(d): a risk-neutral
    /// probability, not the real-world one.
    /// </summary>
    /// <param name="optionType">Call or put.</param>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the normal volatility times the square root of the time.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException"><paramref name="stdDev"/> is negative or NaN.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="optionType"/> isn't a member of its type.</exception>
    public static double BachelierBlackFormulaAssetItmProbability(
        OptionType optionType,
        double strike,
        double forward,
        double stdDev)
    {
        EnumArguments.Check(optionType, nameof(optionType));
        CheckStdDev(stdDev);
        double d = (forward - strike) * (int)optionType;
        if (stdDev == 0.0)
        {
            return d > 0.0 ? 1.0 : 0.0;
        }

        var phi = new CumulativeNormalDistribution();
        double result = phi.Evaluate(d / stdDev);
        return result;
    }

    /// <summary>
    /// The Bachelier probability of being in the money in the asset martingale measure, on a plain-vanilla payoff.
    /// </summary>
    /// <param name="payoff">The payoff, which gives the option type and the strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="stdDev">The standard deviation, the normal volatility times the square root of the time.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="payoff"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="stdDev"/> is negative or NaN.</exception>
    public static double BachelierBlackFormulaAssetItmProbability(
        PlainVanillaPayoff payoff,
        double forward,
        double stdDev)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        return BachelierBlackFormulaAssetItmProbability(payoff.OptionType, payoff.Strike, forward, stdDev);
    }

    private static void CheckParameters(double strike, double forward, double displacement)
    {
        if (!(displacement >= 0.0))
        {
            throw new ArgumentException($"displacement ({Show(displacement)}) must be non-negative", nameof(displacement));
        }

        if (!(strike + displacement >= 0.0))
        {
            throw new ArgumentException(
                $"strike + displacement ({Show(strike)} + {Show(displacement)}) must be non-negative", nameof(strike));
        }

        if (!(forward + displacement > 0.0))
        {
            throw new ArgumentException(
                $"forward + displacement ({Show(forward)} + {Show(displacement)}) must be positive", nameof(forward));
        }
    }

    // QuantLib's QL_REQUIRE(stdDev>=0.0, ...), as each formula writes it.
    private static void CheckStdDev(double stdDev)
    {
        if (!(stdDev >= 0.0))
        {
            throw new ArgumentException($"stdDev ({Show(stdDev)}) must be non-negative", nameof(stdDev));
        }
    }

    // QuantLib's QL_REQUIRE(discount>0.0, ...), as each formula writes it.
    private static void CheckDiscount(double discount)
    {
        if (!(discount > 0.0))
        {
            throw new ArgumentException($"discount ({Show(discount)}) must be positive", nameof(discount));
        }
    }

    private static double Af(double x) =>
        0.5 * (1.0 + (SignFunctions.Sign(x) * Math.Sqrt(1.0 - Math.Exp(-M_2_PI * x * x))));

    private static double SqrtDiff(double g, double yy) =>
        double.IsFinite(g) ? Math.Sqrt(g + yy) - Math.Sqrt(g - yy) : 0.0;

    // The LiRS iteration's functions; Li's φ(x, v) is named apart from the normal density and distribution below.
    private static double Np(double x, double v) => new CumulativeNormalDistribution().Evaluate((x / v) + (0.5 * v));

    private static double Nm(double x, double v) =>
        Math.Exp(-x) * new CumulativeNormalDistribution().Evaluate((x / v) - (0.5 * v));

    private static double PhiLi(double x, double v)
    {
        double ax = 2 * Math.Abs(x);
        double v2 = v * v;
        return (v2 - ax) / (v2 + ax);
    }

    private static double F(double v, double x, double cs, double w) => cs + Nm(x, v) + (w * Np(x, v));

    private static double G(double v, double x, double cs, double w)
    {
        double q = F(v, x, cs, w) / (1 + w);

        // Acklam's inverse w/o Halley's refinement step
        // does not provide enough accuracy. But both together are
        // slower than the boost replacement.
        double k = new MaddockInverseCumulativeNormal().Evaluate(q);

        return k + Math.Sqrt((k * k) + (2 * Math.Abs(x)));
    }

    private static double H(double eta)
    {
        const double A0 = 3.994961687345134e-1;
        const double A1 = 2.100960795068497e+1;
        const double A2 = 4.980340217855084e+1;
        const double A3 = 5.988761102690991e+2;
        const double A4 = 1.848489695437094e+3;
        const double A5 = 6.106322407867059e+3;
        const double A6 = 2.493415285349361e+4;
        const double A7 = 1.266458051348246e+4;

        const double B0 = 1.000000000000000e+0;
        const double B1 = 4.990534153589422e+1;
        const double B2 = 3.093573936743112e+1;
        const double B3 = 1.495105008310999e+3;
        const double B4 = 1.323614537899738e+3;
        const double B5 = 1.598919697679745e+4;
        const double B6 = 2.392008891720782e+4;
        const double B7 = 3.608817108375034e+3;
        const double B8 = -2.067719486400926e+2;
        const double B9 = 1.174240599306013e+1;

        if (!(eta >= 0.0))
        {
            throw new NanoQLException($"eta ({Show(eta)}) must be non-negative");
        }

        double num = A0 + (eta * (A1 + (eta * (A2 + (eta * (A3 + (eta * (A4 + (eta
            * (A5 + (eta * (A6 + (eta * A7)))))))))))));

        double den = B0 + (eta * (B1 + (eta * (B2 + (eta * (B3 + (eta * (B4 + (eta
            * (B5 + (eta * (B6 + (eta * (B7 + (eta * (B8 + (eta * B9)))))))))))))))));

        return Math.Sqrt(eta) * (num / den);
    }

    // The standard normal density and distribution, Boost's pdf and cdf, and their combination for Jaeckel's inverse.
    private static double PhiDensity(double x) => NormalFunctions.Pdf(0.0, 1.0, x);

    private static double Phi(double x) => NormalFunctions.Cdf(0.0, 1.0, x);

    private static double PhiTilde(double x) => Phi(x) + (PhiDensity(x) / x);

    private static double InversePhiTilde(double phiTildeStar)
    {
        if (!(phiTildeStar < 0.0))
        {
            throw new NanoQLException($"inversePhiTilde({Show(phiTildeStar)}): negative argument required");
        }

        double xbar;
        if (phiTildeStar < -0.001882039271)
        {
            double g = 1.0 / (phiTildeStar - 0.5);
            double xibar =
                (0.032114372355 -
                 (g * g *
                  (0.016969777977 - (g * g * (2.6207332461E-3 - (9.6066952861E-5 * g * g)))))) /
                (1.0 -
                 (g * g * (0.6635646938 - (g * g * (0.14528712196 - (0.010472855461 * g * g))))));
            xbar = g * (0.3989422804014326 + (xibar * g * g));
        }
        else
        {
            double h = Math.Sqrt(-Math.Log(-phiTildeStar));
            xbar =
                (9.4883409779 - (h * (9.6320903635 - (h * (0.58556997323 + (2.1464093351 * h)))))) /
                (1.0 - (h * (0.65174820867 + (h * (1.5120247828 + (6.6437847132E-5 * h))))));
        }

        double q = (PhiTilde(xbar) - phiTildeStar) / PhiDensity(xbar);
        double xstar =
            xbar +
            (3.0 * q * xbar * xbar * (2.0 - (q * xbar * (2.0 + (xbar * xbar)))) /
             (6.0 + (q * xbar *
                     (-12.0 +
                      (xbar * ((6.0 * q) + (xbar * (-6.0 + (q * xbar * (3.0 + (xbar * xbar)))))))))));
        return xstar;
    }

    // std::max's and std::min's semantics, which keep or drop a NaN by position where Math.Max and Math.Min propagate it.
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    // QuantLib's BlackImpliedStdDevHelper, the solver's function: the undiscounted Black price on the signed strike and
    // forward, less the target price, and its derivative. Its checks run on construction; QuantLib's extra safety checks
    // of a negative standard deviation are off in the default build.
    private readonly struct BlackImpliedStdDevHelper : IDerivativeSolver1DFunction
    {
        private readonly double halfOptionType;
        private readonly double signedStrike, signedForward;
        private readonly double undiscountedBlackPrice, signedMoneyness;
        private readonly CumulativeNormalDistribution n;

        public BlackImpliedStdDevHelper(
            OptionType optionType,
            double strike,
            double forward,
            double undiscountedBlackPrice,
            double displacement = 0.0)
        {
            halfOptionType = 0.5 * (int)optionType;
            signedStrike = (int)optionType * (strike + displacement);
            signedForward = (int)optionType * (forward + displacement);
            this.undiscountedBlackPrice = undiscountedBlackPrice;
            n = new CumulativeNormalDistribution();
            CheckParameters(strike, forward, displacement);
            if (!(undiscountedBlackPrice >= 0.0))
            {
                throw new NanoQLException(
                    $"undiscounted Black price ({Show(undiscountedBlackPrice)}) must be non-negative");
            }

            signedMoneyness = (int)optionType * Math.Log((forward + displacement) / (strike + displacement));
        }

        public double Evaluate(double stdDev)
        {
            if (stdDev == 0.0)
            {
                return Max(signedForward - signedStrike, 0.0) - undiscountedBlackPrice;
            }

            double temp = halfOptionType * stdDev;
            double d = signedMoneyness / stdDev;
            double signedD1 = d + temp;
            double signedD2 = d - temp;
            double result = (signedForward * n.Evaluate(signedD1)) - (signedStrike * n.Evaluate(signedD2));

            // numerical inaccuracies can yield a negative answer
            return Max(0.0, result) - undiscountedBlackPrice;
        }

        public double Derivative(double stdDev)
        {
            double signedD1 = (signedMoneyness / stdDev) + (halfOptionType * stdDev);
            return signedForward * n.Derivative(signedD1);
        }
    }
}
