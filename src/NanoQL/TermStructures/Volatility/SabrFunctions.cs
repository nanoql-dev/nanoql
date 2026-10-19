// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/sabr.hpp ql/termstructures/volatility/sabr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Utilities;
using static NanoQL.Numerics.FunctionalFunctions;

namespace NanoQL.TermStructures;

/// <summary>
/// The SABR functions: Hagan's lognormal and normal expansions of the SABR volatility, Le Floc'h and Kennedy's
/// expansion, the parameters' validation and Le Floc'h and Kennedy's initial guess for a calibration.
/// </summary>
/// <remarks>
/// The expansions keep QuantLib's order of operations, so their values are QuantLib's bit for bit wherever the C
/// runtime's functions agree with .NET's. A <see cref="TermStructures.VolatilityType"/> outside its members is refused
/// on entry, where QuantLib takes any value other than <see cref="VolatilityType.Normal"/> as lognormal.
/// </remarks>
public static class SabrFunctions
{
    /// <summary>Hagan's lognormal SABR volatility, with no check of its arguments.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="expiryTime">The time to expiry.</param>
    /// <param name="alpha">SABR's α.</param>
    /// <param name="beta">SABR's β.</param>
    /// <param name="nu">SABR's ν.</param>
    /// <param name="rho">SABR's ρ.</param>
    /// <returns>The volatility.</returns>
    public static double UnsafeSabrLogNormalVolatility(
        double strike, double forward, double expiryTime, double alpha, double beta, double nu, double rho)
    {
        double oneMinusBeta = 1.0 - beta;
        double a = Math.Pow(forward * strike, oneMinusBeta);
        double sqrtA = Math.Sqrt(a);
        double logM;
        if (!ComparisonFunctions.Close(forward, strike))
        {
            logM = Math.Log(forward / strike);
        }
        else
        {
            double epsilon = (forward - strike) / strike;
            logM = epsilon - (.5 * epsilon * epsilon);
        }

        double z = (nu / alpha) * sqrtA * logM;
        double b = 1.0 - (2.0 * rho * z) + (z * z);
        double c = oneMinusBeta * oneMinusBeta * logM * logM;
        double tmp = (Math.Sqrt(b) + z - rho) / (1.0 - rho);
        double xx = Math.Log(tmp);
        double bigD = sqrtA * (1.0 + (c / 24.0) + (c * c / 1920.0));
        double d = 1.0 + (expiryTime *
            ((oneMinusBeta * oneMinusBeta * alpha * alpha / (24.0 * a))
                + (0.25 * rho * beta * nu * alpha / sqrtA)
                + ((2.0 - (3.0 * rho * rho)) * (nu * nu / 24.0))));

        // computations become precise enough if the square of z worth
        // slightly more than the precision machine (hence the m)
        const double m = 10;
        double multiplier;
        if (Math.Abs(z * z) > NumericLimits.QL_EPSILON * m)
        {
            multiplier = z / xx;
        }
        else
        {
            multiplier = 1.0 - (0.5 * rho * z) - (((3.0 * rho * rho) - 2.0) * z * z / 12.0);
        }

        return (alpha / bigD) * multiplier * d;
    }

    /// <summary>The shifted SABR volatility of the given type, with no check of its arguments.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="expiryTime">The time to expiry.</param>
    /// <param name="alpha">SABR's α.</param>
    /// <param name="beta">SABR's β.</param>
    /// <param name="nu">SABR's ν.</param>
    /// <param name="rho">SABR's ρ.</param>
    /// <param name="shift">The shift added to the strike and the forward.</param>
    /// <param name="volatilityType">Normal, or shifted lognormal.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its type.</exception>
    public static double UnsafeShiftedSabrVolatility(
        double strike,
        double forward,
        double expiryTime,
        double alpha,
        double beta,
        double nu,
        double rho,
        double shift,
        VolatilityType volatilityType = VolatilityType.ShiftedLognormal)
    {
        EnumArguments.Check(volatilityType, nameof(volatilityType));
        return ShiftedVolatility(strike, forward, expiryTime, alpha, beta, nu, rho, shift, volatilityType);
    }

    /// <summary>The normal SABR volatility, with no check of its arguments.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="expiryTime">The time to expiry.</param>
    /// <param name="alpha">SABR's α.</param>
    /// <param name="beta">SABR's β.</param>
    /// <param name="nu">SABR's ν.</param>
    /// <param name="rho">SABR's ρ.</param>
    /// <returns>The volatility.</returns>
    /// <remarks>As QuantLib's: Deloitte's "SABR sensitivities" note.</remarks>
    public static double UnsafeSabrNormalVolatility(
        double strike, double forward, double expiryTime, double alpha, double beta, double nu, double rho)
    {
        double oneMinusBeta = 1.0 - beta;
        double minusBeta = -1.0 * beta;
        double a = Math.Pow(forward * strike, oneMinusBeta);
        double sqrtA = Math.Sqrt(a);
        double logM;
        if (!ComparisonFunctions.Close(forward, strike))
        {
            logM = Math.Log(forward / strike);
        }
        else
        {
            double epsilon = (forward - strike) / strike;
            logM = epsilon - (.5 * epsilon * epsilon);
        }

        double z = (nu / alpha) * sqrtA * logM;
        double b = 1.0 - (2.0 * rho * z) + (z * z);
        double c = oneMinusBeta * oneMinusBeta * logM * logM;
        double bigD = logM * logM;
        double tmp = (Math.Sqrt(b) + z - rho) / (1.0 - rho);
        double xx = Math.Log(tmp);
        double e1 = 1.0 + (bigD / 24.0) + (bigD * bigD / 1920.0);
        double e2 = 1.0 + (c / 24.0) + (c * c / 1920.0);
        double e = e1 / e2;
        double d = 1.0 + (expiryTime * ((minusBeta * (2 - beta) * alpha * alpha / (24.0 * a))
            + (0.25 * rho * beta * nu * alpha / sqrtA)
            + ((2.0 - (3.0 * rho * rho)) * (nu * nu / 24.0))));

        // computations become precise enough if the square of z worth
        // slightly more than the precision machine (hence the m)
        const double m = 10;
        double multiplier;
        if (Math.Abs(z * z) > NumericLimits.QL_EPSILON * m)
        {
            multiplier = z / xx;
        }
        else
        {
            multiplier = 1.0 - (0.5 * rho * z) - (((3.0 * rho * rho) - 2.0) * z * z / 12.0);
        }

        double f = alpha * Math.Pow(forward * strike, beta / 2.0);
        return f * e * multiplier * d;
    }

    /// <summary>The SABR volatility of the given type, with no check of its arguments.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="expiryTime">The time to expiry.</param>
    /// <param name="alpha">SABR's α.</param>
    /// <param name="beta">SABR's β.</param>
    /// <param name="nu">SABR's ν.</param>
    /// <param name="rho">SABR's ρ.</param>
    /// <param name="volatilityType">Normal, or shifted lognormal.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its type.</exception>
    public static double UnsafeSabrVolatility(
        double strike,
        double forward,
        double expiryTime,
        double alpha,
        double beta,
        double nu,
        double rho,
        VolatilityType volatilityType = VolatilityType.ShiftedLognormal)
    {
        EnumArguments.Check(volatilityType, nameof(volatilityType));
        if (volatilityType == VolatilityType.Normal)
        {
            return UnsafeSabrNormalVolatility(strike, forward, expiryTime, alpha, beta, nu, rho);
        }
        else
        {
            return UnsafeSabrLogNormalVolatility(strike, forward, expiryTime, alpha, beta, nu, rho);
        }
    }

    /// <summary>Checks SABR's parameters.</summary>
    /// <param name="alpha">SABR's α, positive.</param>
    /// <param name="beta">SABR's β, in [0, 1].</param>
    /// <param name="nu">SABR's ν, non-negative.</param>
    /// <param name="rho">SABR's ρ, in (−1, 1).</param>
    /// <exception cref="ArgumentException">A parameter is out of its range, or NaN.</exception>
    public static void ValidateSabrParameters(double alpha, double beta, double nu, double rho)
    {
        if (!(alpha > 0.0))
        {
            throw new ArgumentException($"alpha must be positive: {Show(alpha)} not allowed", nameof(alpha));
        }

        if (!(beta >= 0.0 && beta <= 1.0))
        {
            throw new ArgumentException($"beta must be in (0.0, 1.0): {Show(beta)} not allowed", nameof(beta));
        }

        if (!(nu >= 0.0))
        {
            throw new ArgumentException($"nu must be non negative: {Show(nu)} not allowed", nameof(nu));
        }

        if (!(rho * rho < 1.0))
        {
            throw new ArgumentException($"rho square must be less than one: {Show(rho)} not allowed", nameof(rho));
        }
    }

    /// <summary>The SABR volatility of the given type, its arguments checked.</summary>
    /// <param name="strike">The strike, positive.</param>
    /// <param name="forward">The forward, positive.</param>
    /// <param name="expiryTime">The time to expiry, non-negative.</param>
    /// <param name="alpha">SABR's α.</param>
    /// <param name="beta">SABR's β.</param>
    /// <param name="nu">SABR's ν.</param>
    /// <param name="rho">SABR's ρ.</param>
    /// <param name="volatilityType">Normal, or shifted lognormal.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">An argument is out of its range.</exception>
    public static double SabrVolatility(
        double strike,
        double forward,
        double expiryTime,
        double alpha,
        double beta,
        double nu,
        double rho,
        VolatilityType volatilityType = VolatilityType.ShiftedLognormal)
    {
        EnumArguments.Check(volatilityType, nameof(volatilityType));
        if (!(strike > 0.0))
        {
            throw new ArgumentException(
                $"strike must be positive: {DataFormattersFunctions.Rate(strike)} not allowed", nameof(strike));
        }

        if (!(forward > 0.0))
        {
            throw new ArgumentException(
                $"at the money forward rate must be positive: {DataFormattersFunctions.Rate(forward)} not allowed",
                nameof(forward));
        }

        if (!(expiryTime >= 0.0))
        {
            throw new ArgumentException(
                $"expiry time must be non-negative: {Show(expiryTime)} not allowed", nameof(expiryTime));
        }

        ValidateSabrParameters(alpha, beta, nu, rho);
        return UnsafeSabrVolatility(strike, forward, expiryTime, alpha, beta, nu, rho, volatilityType);
    }

    /// <summary>The shifted SABR volatility of the given type, its arguments checked.</summary>
    /// <param name="strike">The strike; strike + shift positive.</param>
    /// <param name="forward">The forward; forward + shift positive.</param>
    /// <param name="expiryTime">The time to expiry, non-negative.</param>
    /// <param name="alpha">SABR's α.</param>
    /// <param name="beta">SABR's β.</param>
    /// <param name="nu">SABR's ν.</param>
    /// <param name="rho">SABR's ρ.</param>
    /// <param name="shift">The shift added to the strike and the forward.</param>
    /// <param name="volatilityType">Normal, or shifted lognormal.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">An argument is out of its range.</exception>
    public static double ShiftedSabrVolatility(
        double strike,
        double forward,
        double expiryTime,
        double alpha,
        double beta,
        double nu,
        double rho,
        double shift,
        VolatilityType volatilityType = VolatilityType.ShiftedLognormal)
    {
        EnumArguments.Check(volatilityType, nameof(volatilityType));
        return CheckedShiftedVolatility(strike, forward, expiryTime, alpha, beta, nu, rho, shift, volatilityType);
    }

    /// <summary>Le Floc'h and Kennedy's expansion of the lognormal SABR volatility, with no check of its arguments.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="expiryTime">The time to expiry.</param>
    /// <param name="alpha">SABR's α.</param>
    /// <param name="beta">SABR's β.</param>
    /// <param name="nu">SABR's ν.</param>
    /// <param name="rho">SABR's ρ.</param>
    /// <returns>The volatility; a second-order Taylor expansion where the strike is within 0.25% of the forward.</returns>
    public static double SabrFlochKennedyVolatility(
        double strike, double forward, double expiryTime, double alpha, double beta, double nu, double rho)
    {
        var v = new SabrFlochKennedyVolatilityFunction(forward, alpha, beta, nu, rho, expiryTime);
        return v.Evaluate(strike);
    }

    /// <summary>
    /// Le Floc'h and Kennedy's initial guess for a SABR calibration (Le Floc'h and Kennedy, "Explicit SABR Calibration
    /// through Simple Expansions"): the parameters matching the at-the-money volatility, skew and curvature that three
    /// volatilities around the forward give.
    /// </summary>
    /// <param name="k_m">The strike before the one closest to the forward.</param>
    /// <param name="vol_m">Its volatility.</param>
    /// <param name="k_0">The strike closest to the forward.</param>
    /// <param name="vol_0">Its volatility.</param>
    /// <param name="k_p">The strike after the one closest to the forward.</param>
    /// <param name="vol_p">Its volatility.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="expiryTime">The time to expiry.</param>
    /// <param name="beta">SABR's β, which the guess returns as given.</param>
    /// <param name="shift">The shift.</param>
    /// <param name="volatilityType">Normal, or shifted lognormal.</param>
    /// <returns>The guesses α, β, ν and ρ, in that order.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="volatilityType"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">An argument is NaN or infinite.</exception>
    /// <remarks>
    /// A non-finite argument is refused, where QuantLib computes on it: a NaN smile gives ρ the sign of a NaN, which
    /// depends on which NaN an operation keeps, so QuantLib's value there isn't a number a caller can use. α is the
    /// smallest positive root of the guess's cubic, or its first estimate where the cubic has none, as QuantLib's.
    /// </remarks>
    public static ImmutableArray<double> SabrGuess(
        double k_m,
        double vol_m,
        double k_0,
        double vol_0,
        double k_p,
        double vol_p,
        double forward,
        double expiryTime,
        double beta,
        double shift,
        VolatilityType volatilityType)
    {
        if (volatilityType is not (VolatilityType.ShiftedLognormal or VolatilityType.Normal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(volatilityType), volatilityType, $"unknown volatility type: {(int)volatilityType}");
        }

        CheckFinite(k_m, nameof(k_m));
        CheckFinite(vol_m, nameof(vol_m));
        CheckFinite(k_0, nameof(k_0));
        CheckFinite(vol_0, nameof(vol_0));
        CheckFinite(k_p, nameof(k_p));
        CheckFinite(vol_p, nameof(vol_p));
        CheckFinite(forward, nameof(forward));
        CheckFinite(expiryTime, nameof(expiryTime));
        CheckFinite(beta, nameof(beta));
        CheckFinite(shift, nameof(shift));

        // same variable names as in the equations for ease of reference:
        double f = forward, b = shift, t = expiryTime;

        // change to log-moneyness
        double z_m = Math.Log((k_m + b) / (f + b));
        double z_0 = Math.Log((k_0 + b) / (f + b));
        double z_p = Math.Log((k_p + b) / (f + b));

        // calculate atm, skew, curvature
        double w_m = 1 / ((z_m - z_0) * (z_m - z_p)); // eq. (42) in the paper
        double w_0 = 1 / ((z_0 - z_m) * (z_0 - z_p)); // eq. (43)
        double w_p = 1 / ((z_p - z_m) * (z_p - z_0)); // eq. (44)

        double sigma_0 = (z_0 * z_p * w_m * vol_m) + (z_m * z_p * w_0 * vol_0) + (z_m * z_0 * w_p * vol_p); // (39)
        double sigma_1 = (-(z_0 + z_p) * w_m * vol_m) - ((z_m + z_p) * w_0 * vol_0) - ((z_m + z_0) * w_p * vol_p); // (40)
        double sigma_2 = (2 * w_m * vol_m) + (2 * w_0 * vol_0) + (2 * w_p * vol_p); // (41)

        if (volatilityType == VolatilityType.ShiftedLognormal)
        {
            // equations (32)
            double alpha = sigma_0 * Math.Pow(f + b, 1.0 - beta);
            double nu2 =
                (3 * sigma_0 * sigma_2)
                - (0.5 * Squared(1 - beta) * sigma_0 * sigma_0)
                + (1.5 * Squared((2 * sigma_1) + ((1 - beta) * sigma_0)));
            double nu, rho;
            if (nu2 > 0.0)
            {
                nu = Math.Sqrt(nu2);
                rho = (1 / nu) * ((2 * sigma_1) + ((1 - beta) * sigma_0));
            }
            else
            {
                rho = SignFunctions.Sign((2 * sigma_1) + ((1 - beta) * sigma_0));
                nu = (1 / rho) * ((2 * sigma_1) + ((1 - beta) * sigma_0));
            }

            // coefficients of the polynomial in equation (33)
            double c1 = Squared(1 - beta) * t / (24 * Math.Pow(f + b, 2 - (2 * beta)));
            double c2 = rho * beta * nu * t / (4 * Math.Pow(f + b, 1 - beta));
            double c3 = 1 + ((2 - (3 * rho * rho)) / 24 * nu * nu * t);
            double c4 = -sigma_0 * Math.Pow(f + b, 1 - beta);

            alpha = SabrGuessRoots.SmallestPositiveRoot(c1, c2, c3, c4) ?? alpha;
            return [alpha, beta, nu, rho];
        }
        else
        {
            // equations (37)
            double alpha = sigma_0 * Math.Pow(f + b, -beta);
            double nu2 = Squared(1 / (f + b)) * (
                (3 * sigma_0 * sigma_2)
                - (0.5 * ((beta * beta) + beta) * (sigma_0 * sigma_0))
                - (3 * sigma_0 * (sigma_1 - (0.5 * beta * sigma_0)))
                + (1.5 * Squared((2 * sigma_1) - (beta * sigma_0))));
            double nu, rho;
            if (nu2 > 0.0)
            {
                nu = Math.Sqrt(nu2);
                rho = (1 / (nu * (f + b))) * ((2 * sigma_1) - (beta * sigma_0));
            }
            else
            {
                rho = SignFunctions.Sign((1 / (f + b)) * ((2 * sigma_1) - (beta * sigma_0)));
                nu = (1 / (rho * (f + b))) * ((2 * sigma_1) - (beta * sigma_0));
            }

            // coefficients of the polynomial in equation (38)
            double c1 = ((beta * beta) - (2 * beta)) * t / (24 * Math.Pow(f + b, 2 - (2 * beta)));
            double c2 = rho * beta * nu * t / (4 * Math.Pow(f + b, 1 - beta));
            double c3 = 1 + ((2 - (3 * rho * rho)) / 24 * nu * nu * t);
            double c4 = -sigma_0 * Math.Pow(f + b, -beta);

            alpha = SabrGuessRoots.SmallestPositiveRoot(c1, c2, c3, c4) ?? alpha;
            return [alpha, beta, nu, rho];
        }
    }

    // unsafeShiftedSabrVolatility after its type's check.
    internal static double ShiftedVolatility(
        double strike,
        double forward,
        double expiryTime,
        double alpha,
        double beta,
        double nu,
        double rho,
        double shift,
        VolatilityType volatilityType)
    {
        if (volatilityType == VolatilityType.Normal)
        {
            return UnsafeSabrNormalVolatility(strike + shift, forward + shift, expiryTime, alpha, beta, nu, rho);
        }
        else
        {
            return UnsafeSabrLogNormalVolatility(strike + shift, forward + shift, expiryTime, alpha, beta, nu, rho);
        }
    }

    // shiftedSabrVolatility after its type's check: the fit's wrapper calls it at every value.
    internal static double CheckedShiftedVolatility(
        double strike,
        double forward,
        double expiryTime,
        double alpha,
        double beta,
        double nu,
        double rho,
        double shift,
        VolatilityType volatilityType)
    {
        if (!(strike + shift > 0.0))
        {
            throw new ArgumentException(
                $"strike+shift must be positive: {DataFormattersFunctions.Rate(strike)}+{DataFormattersFunctions.Rate(shift)} not allowed",
                nameof(strike));
        }

        if (!(forward + shift > 0.0))
        {
            throw new ArgumentException(
                $"at the money forward rate + shift must be positive: {DataFormattersFunctions.Rate(forward)} {DataFormattersFunctions.Rate(shift)} not allowed",
                nameof(forward));
        }

        if (!(expiryTime >= 0.0))
        {
            throw new ArgumentException(
                $"expiry time must be non-negative: {Show(expiryTime)} not allowed", nameof(expiryTime));
        }

        ValidateSabrParameters(alpha, beta, nu, rho);
        return ShiftedVolatility(strike, forward, expiryTime, alpha, beta, nu, rho, shift, volatilityType);
    }

    private static void CheckFinite(double value, string paramName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentException($"{paramName} must be finite: {Show(value)} not allowed", paramName);
        }
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);
}

// sabr.cpp's SabrFlochKennedyVolatility, a function object in an anonymous namespace.
file readonly struct SabrFlochKennedyVolatilityFunction(
    double f, double alpha, double beta, double nu, double rho, double t)
{
    public double Evaluate(double k)
    {
        double m = f / k;
        if (m > 1.0025 || m < 0.9975)
        {
            return (Omega0(k) * (1 + (0.25 * rho * nu * alpha *
                (Math.Pow(k, beta) - Math.Pow(f, beta)) / (k - f) * t)))
                - (Omega0(k) / Squared(Dint(k)) * (Math.Log(
                    Omega0(k)) + (0.5 * Math.Log(f * k / (D(f) * D(k))))) * t);
        }
        else
        {
            return TaylorExpansion(k);
        }
    }

    private double Y(double k) => -1.0 / (1.0 - beta) * (Math.Pow(f, 1 - beta) - Math.Pow(k, 1 - beta));

    private double Dint(double k) =>
        1 / nu * Math.Log((Math.Sqrt(1 + (2 * rho * nu / alpha * Y(k))
            + Squared(nu / alpha * Y(k)))
            - rho - (nu / alpha * Y(k))) / (1 - rho));

    private double D(double k) =>
        Math.Sqrt((alpha * alpha) + (2 * alpha * rho * nu * Y(k))
            + Squared(nu * Y(k))) * Math.Pow(k, beta);

    private double Omega0(double k) => Math.Log(f / k) / Dint(k);

    private double TaylorExpansion(double k)
    {
        double f2 = f * f;
        double alpha2 = alpha * alpha;
        double rho2 = rho * rho;
        return
            (alpha * Math.Pow(f, -3 + beta) * ((alpha2 * Squared(-1 + beta) * Math.Pow(f, 2 * beta) * t) + (6 * alpha * beta * nu * Math.Pow(f, 1 + beta) * rho * t) +
                (f2 * (24 + (nu * nu * (2 - (3 * rho2)) * t)))) / 24.0) +
            (((3 * alpha2 * alpha * Math.Pow(-1 + beta, 3) * Math.Pow(f, 3 * beta) * t) +
                (3 * alpha2 * (-1 + beta) * (-1 + (5 * beta)) * nu * Math.Pow(f, 1 + (2 * beta)) * rho * t) + (nu * f2 * f * rho * (24 + (nu * nu * (-4 + (3 * rho2)) * t))) +
                (alpha * Math.Pow(f, 2 + beta) * ((24 * (-1 + beta)) + (nu * nu * ((2 * (-1 + beta)) + (3 * (1 + beta) * rho2)) * t)))) / (48.0 * f2 * f2) * (k - f)) +
            ((Math.Pow(f, -5 - beta) * ((alpha2 * alpha2 * Math.Pow(-1 + beta, 3) * (-209 + (119 * beta)) * Math.Pow(f, 4 * beta) * t) + (30 * alpha2 * alpha * (-1 + beta) * (9 + (beta * (-37 + (18 * beta)))) * nu * Math.Pow(f, 1 + (3 * beta)) * rho * t) -
                (30 * alpha * nu * Math.Pow(f, 3 + beta) * rho * (24 + (nu * nu * ((-4 * (1 + beta)) + (3 * (1 + (2 * beta)) * rho2)) * t))) +
                (10 * alpha2 * Math.Pow(f, 2 + (2 * beta)) * ((24 * (-4 + beta) * (-1 + beta)) + (nu * nu * ((2 * (-1 + beta) * (-7 + (4 * beta))) + (3 * (-4 + (beta * (-7 + (5 * beta)))) * rho2)) * t))) +
                (nu * nu * f2 * f2 * (480 - (720 * rho2) + (nu * nu * (-64 + (75 * rho2 * (4 - (3 * rho2)))) * t))))) / (2880 * alpha) * (k - f) * (k - f));
    }
}

// sabr.cpp's smallest_positive_root, in an anonymous namespace: null where QuantLib's throws "no positive root",
// which sabrGuess catches.
file static class SabrGuessRoots
{
    public static double? SmallestPositiveRoot(double c1, double c2, double c3, double c4)
    {
        double[] roots = CubicRootsFunctions.CubicRoots(c1, c2, c3, c4);
        double r1 = roots[0], r2 = roots[1], r3 = roots[2];
        if (double.IsNaN(r3))
        {
            // single root (or two equal ones), check that it's positive
            return r1 > 0.0 ? r1 : null;
        }
        else
        {
            // three roots in non-decreasing order, return the first positive one
            if (!(r3 > 0.0))
            {
                return null;
            }

            return r1 > 0.0 ? r1 : (r2 > 0.0 ? r2 : r3);
        }
    }
}
