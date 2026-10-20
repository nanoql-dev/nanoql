// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabr.hpp ql/termstructures/volatility/zabr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: fdPrice (both forms) and fullFdPrice, the local-volatility and full finite-difference prices, which wait for B3.3 with the finite-difference units they price on (S-eq's meshers, layout and backward solver, S-rx's Dirichlet boundary, B3.3's glued mesher and its Dupire and ZABR operators)

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// The ZABR model (J. Andreasen and B. Huge, "ZABR - Expansions for the masses", 2011): SABR with the volatility's own
/// volatility scaled by a power γ of the volatility. Its short-maturity lognormal and normal volatilities, and its
/// local volatility.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Nu"/> is the transformed ν, ν α<sup>1−γ</sup>, as QuantLib's is. The volatilities rest on the
/// expansion's x(K): for γ close to 1 its closed form, SABR's; otherwise the solution of an ordinary differential
/// equation in y, integrated outward from y = 0 by <see cref="AdaptiveRungeKutta{T}"/> to each strike's y in turn, in
/// QuantLib's order.
/// </para>
/// <para>
/// The vector members take strictly ascending strikes, positive where β is 1. An empty vector is refused, where
/// QuantLib reads its first element.
/// </para>
/// <para>Immutable and thread-safe.</para>
/// </remarks>
public sealed class ZabrModel
{
    private readonly double expiryTime;
    private readonly double forward;
    private readonly double alpha;
    private readonly double beta;
    private readonly double nu;
    private readonly double rho;
    private readonly double gamma;

    /// <summary>Initializes a new instance of the <see cref="ZabrModel"/> class.</summary>
    /// <param name="expiryTime">The time to expiry, positive.</param>
    /// <param name="forward">The forward, non-negative.</param>
    /// <param name="alpha">α, positive.</param>
    /// <param name="beta">β, in [0, 1].</param>
    /// <param name="nu">ν, non-negative.</param>
    /// <param name="rho">ρ, with ρ² below 1.</param>
    /// <param name="gamma">γ, non-negative.</param>
    /// <exception cref="ArgumentException">A parameter is out of its range, the forward is negative, or the time isn't
    /// positive.</exception>
    public ZabrModel(double expiryTime, double forward, double alpha, double beta, double nu, double rho, double gamma)
    {
        this.expiryTime = expiryTime;
        this.forward = forward;
        this.alpha = alpha;
        this.beta = beta;
        this.nu = nu * Math.Pow(alpha, 1.0 - gamma);
        this.rho = rho;
        this.gamma = gamma;

        SabrFunctions.ValidateSabrParameters(alpha, beta, nu, rho);
        if (!(gamma >= 0.0))
        {
            throw new ArgumentException($"gamma must be non negative: {Show(gamma)} not allowed", nameof(gamma));
        }

        if (!(forward >= 0.0))
        {
            throw new ArgumentException($"forward must be non negative: {Show(forward)} not allowed", nameof(forward));
        }

        if (!(expiryTime > 0.0))
        {
            throw new ArgumentException($"expiry time must be positive: {Show(expiryTime)} not allowed", nameof(expiryTime));
        }
    }

    /// <summary>Gets the forward.</summary>
    public double Forward => forward;

    /// <summary>Gets the time to expiry.</summary>
    public double ExpiryTime => expiryTime;

    /// <summary>Gets α.</summary>
    public double Alpha => alpha;

    /// <summary>Gets β.</summary>
    public double Beta => beta;

    /// <summary>Gets the transformed ν: the given ν times α<sup>1−γ</sup>.</summary>
    public double Nu => nu;

    /// <summary>Gets ρ.</summary>
    public double Rho => rho;

    /// <summary>Gets γ.</summary>
    public double Gamma => gamma;

    /// <summary>The local volatility at a forward level.</summary>
    /// <param name="f">The forward level, positive where β is 1.</param>
    /// <returns>The local volatility.</returns>
    /// <exception cref="ArgumentException">β is 1 and <paramref name="f"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The integration fails.</exception>
    public double LocalVolatility(double f) => LocalVolatilities([f], nameof(f))[0];

    /// <summary>The local volatilities at forward levels.</summary>
    /// <param name="f">The forward levels, strictly ascending, positive where β is 1.</param>
    /// <returns>The local volatilities, one per level.</returns>
    /// <exception cref="ArgumentException"><paramref name="f"/> is empty, isn't strictly ascending, or β is 1 and its
    /// first level isn't positive.</exception>
    /// <exception cref="NanoQLException">The integration fails.</exception>
    public ImmutableArray<double> LocalVolatility(ReadOnlySpan<double> f)
    {
        CheckNotEmpty(f, nameof(f));
        return [.. LocalVolatilities(f, nameof(f))];
    }

    /// <summary>The short-maturity lognormal volatility at a strike.</summary>
    /// <param name="strike">The strike, positive where β is 1.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentException">β is 1 and <paramref name="strike"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The integration fails.</exception>
    public double LognormalVolatility(double strike) => LognormalVolatilities([strike], nameof(strike))[0];

    /// <summary>The short-maturity lognormal volatilities at strikes.</summary>
    /// <param name="strikes">The strikes, strictly ascending, positive where β is 1.</param>
    /// <returns>The volatilities, one per strike.</returns>
    /// <exception cref="ArgumentException"><paramref name="strikes"/> is empty, isn't strictly ascending, or β is 1 and
    /// its first strike isn't positive.</exception>
    /// <exception cref="NanoQLException">The integration fails.</exception>
    public ImmutableArray<double> LognormalVolatility(ReadOnlySpan<double> strikes)
    {
        CheckNotEmpty(strikes, nameof(strikes));
        return [.. LognormalVolatilities(strikes, nameof(strikes))];
    }

    /// <summary>The short-maturity normal volatility at a strike.</summary>
    /// <param name="strike">The strike, positive where β is 1.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentException">β is 1 and <paramref name="strike"/> isn't positive.</exception>
    /// <exception cref="NanoQLException">The integration fails.</exception>
    public double NormalVolatility(double strike) => NormalVolatilities([strike], nameof(strike))[0];

    /// <summary>The short-maturity normal volatilities at strikes.</summary>
    /// <param name="strikes">The strikes, strictly ascending, positive where β is 1.</param>
    /// <returns>The volatilities, one per strike.</returns>
    /// <exception cref="ArgumentException"><paramref name="strikes"/> is empty, isn't strictly ascending, or β is 1 and
    /// its first strike isn't positive.</exception>
    /// <exception cref="NanoQLException">The integration fails.</exception>
    public ImmutableArray<double> NormalVolatility(ReadOnlySpan<double> strikes)
    {
        CheckNotEmpty(strikes, nameof(strikes));
        return [.. NormalVolatilities(strikes, nameof(strikes))];
    }

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    // QuantLib reads the first element of an empty vector.
    private static void CheckNotEmpty(ReadOnlySpan<double> strikes, string paramName)
    {
        if (strikes.IsEmpty)
        {
            throw new ArgumentException("no strikes given", paramName);
        }
    }

    private double LognormalVolatilityHelper(double strike, double x)
    {
        if (ComparisonFunctions.Close(strike, forward))
        {
            return Math.Pow(forward, beta - 1.0) * alpha;
        }
        else
        {
            return Math.Log(forward / strike) / x;
        }
    }

    private double[] LognormalVolatilities(ReadOnlySpan<double> strikes, string paramName)
    {
        double[] x = X(strikes, paramName);
        double[] result = new double[strikes.Length];
        for (int i = 0; i < strikes.Length; ++i)
        {
            result[i] = LognormalVolatilityHelper(strikes[i], x[i]);
        }

        return result;
    }

    private double NormalVolatilityHelper(double strike, double x)
    {
        if (ComparisonFunctions.Close(strike, forward))
        {
            return Math.Pow(forward, beta) * alpha;
        }
        else
        {
            return (forward - strike) / x;
        }
    }

    private double[] NormalVolatilities(ReadOnlySpan<double> strikes, string paramName)
    {
        double[] x = X(strikes, paramName);
        double[] result = new double[strikes.Length];
        for (int i = 0; i < strikes.Length; ++i)
        {
            result[i] = NormalVolatilityHelper(strikes[i], x[i]);
        }

        return result;
    }

    private double LocalVolatilityHelper(double f, double x) =>
        alpha * Math.Pow(Math.Abs(f), beta) / F(Y(f), Math.Pow(alpha, gamma - 1.0) * x);

    private double[] LocalVolatilities(ReadOnlySpan<double> f, string paramName)
    {
        double[] x = X(f, paramName);
        double[] result = new double[f.Length];
        for (int i = 0; i < f.Length; ++i)
        {
            result[i] = LocalVolatilityHelper(f[i], x[i]);
        }

        return result;
    }

    // x(K) at each strike, the strikes refused as QuantLib refuses them, under the caller's parameter name.
    private double[] X(ReadOnlySpan<double> strikes, string paramName)
    {
        if (!(strikes[0] > 0.0 || beta < 1.0))
        {
            throw new ArgumentException($"strikes must be positive ({Show(strikes[0])}) if beta = 1", paramName);
        }

        for (int i = 1; i < strikes.Length; ++i)
        {
            if (!(strikes[i] > strikes[i - 1]))
            {
                throw new ArgumentException(
                    $"strikes must be strictly ascending ({Show(strikes[i - 1])},{Show(strikes[i])})", paramName);
            }
        }

        var rk = new AdaptiveRungeKutta<double>(1.0E-8, 1.0E-5, 0.0);
        int n = strikes.Length;
        double[] y = new double[n], result = new double[n];
        for (int m = 0; m < n; ++m)
        {
            y[m] = Y(strikes[n - 1 - m]);
        }

        if (ComparisonFunctions.Close(gamma, 1.0))
        {
            for (int m = 0; m < n; m++)
            {
                double j = Math.Sqrt(1.0 + (nu * nu * y[m] * y[m]) - (2.0 * rho * nu * y[m]));
                result[n - 1 - m] = Math.Log((j + (nu * y[m]) - rho) / (1.0 - rho)) / nu;
            }
        }
        else
        {
            int ynz = Interpolation.UpperBound(y, 0.0);
            if (ynz > 0)
            {
                if (ComparisonFunctions.Close(y[ynz - 1], 0.0))
                {
                    ynz--;
                }
            }

            if (ynz == n)
            {
                ynz--;
            }

            for (int dir = 1; dir >= -1; dir -= 2)
            {
                double y0 = 0.0, u0 = 0.0;
                for (int m = ynz + (dir == -1 ? -1 : 0); dir == -1 ? m >= 0 : m < n; m += dir)
                {
                    double u = rk.Evaluate(F, u0, y0, y[m]);
                    result[n - 1 - m] = u * Math.Pow(alpha, 1.0 - gamma);
                    u0 = u;
                    y0 = y[m];
                }
            }
        }

        return result;
    }

    private double Y(double strike)
    {
        if (ComparisonFunctions.Close(beta, 1.0))
        {
            return Math.Log(forward / strike) * Math.Pow(alpha, gamma - 2.0);
        }
        else
        {
            return (strike < 0.0
                    ? Math.Pow(forward, 1.0 - beta) + Math.Pow(-strike, 1.0 - beta)
                    : Math.Pow(forward, 1.0 - beta) - Math.Pow(strike, 1.0 - beta))
                * Math.Pow(alpha, gamma - 2.0) / (1.0 - beta);
        }
    }

    private double F(double y, double u)
    {
        double a = 1.0 + ((gamma - 2.0) * (gamma - 2.0) * nu * nu * y * y) + (2.0 * rho * (gamma - 2.0) * nu * y);
        double b = (2.0 * rho * (1.0 - gamma) * nu) + (2.0 * (1.0 - gamma) * (gamma - 2.0) * nu * nu * y);
        double c = (1.0 - gamma) * (1.0 - gamma) * nu * nu;
        return ((-b * u) + Math.Sqrt((b * b * u * u) - (4.0 * a * ((c * u * u) - 1.0)))) / (2.0 * a);
    }
}
