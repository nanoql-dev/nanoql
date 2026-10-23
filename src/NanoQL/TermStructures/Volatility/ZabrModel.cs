// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabr.hpp ql/termstructures/volatility/zabr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// The ZABR model (J. Andreasen and B. Huge, "ZABR - Expansions for the masses", 2011): SABR with the volatility's own
/// volatility scaled by a power γ of the volatility. Its short-maturity lognormal and normal volatilities, and its
/// local volatility, and call prices by finite differences: on the Dupire equation in the strike under that local
/// volatility, and on the model's full two-dimensional equation in the forward and the volatility.
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

    /// <summary>A call's price at a strike, undiscounted, by finite differences on the Dupire equation in the strike
    /// under the model's local volatility.</summary>
    /// <param name="strike">The strike, inside the grid.</param>
    /// <returns>The price.</returns>
    /// <exception cref="NanoQLException">The strike is outside the grid, or the integration fails.</exception>
    public double FdPrice(double strike) => FdPrices([strike])[0];

    /// <summary>Calls' prices at strikes, undiscounted, by finite differences on the Dupire equation in the strike
    /// under the model's local volatility, on one grid from the lowest strike's half to 1.5 times the
    /// highest.</summary>
    /// <param name="strikes">The strikes, the first the lowest and the last the highest.</param>
    /// <returns>The prices, one per strike.</returns>
    /// <exception cref="ArgumentException"><paramref name="strikes"/> is empty.</exception>
    /// <exception cref="NanoQLException">A strike is outside the grid, or the integration fails.</exception>
    public ImmutableArray<double> FdPrice(ReadOnlySpan<double> strikes)
    {
        CheckNotEmpty(strikes, nameof(strikes));
        return [.. FdPrices(strikes)];
    }

    /// <summary>A call's price at a strike, undiscounted, by finite differences on the model's two-dimensional
    /// equation in the forward and the volatility, on a grid glued from two meshers concentrated at the forward and the
    /// strike.</summary>
    /// <param name="strike">The strike, inside the grid.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentException">The strike is outside the grid.</exception>
    public double FullFdPrice(double strike)
    {
        // TODO what are good values here, still experimenting with them
        double eps = 0.01;
        double scaleFactor = 1.5;
        double normInvEps = new InverseCumulativeNormal().Evaluate(1.0 - eps);
        double alphaI = alpha * Math.Pow(forward, beta - 1.0);

        // nu is already standardized within this class ...
        double v0 = alpha * Math.Exp(-scaleFactor * normInvEps * Math.Sqrt(expiryTime) * nu);
        double v1 = alpha * Math.Exp(scaleFactor * normInvEps * Math.Sqrt(expiryTime) * nu);
        double f0 = forward * Math.Exp(-scaleFactor * normInvEps * Math.Sqrt(expiryTime) * alphaI);
        double f1 = forward * Math.Exp(scaleFactor * normInvEps * Math.Sqrt(expiryTime) * alphaI);
        v1 = Math.Min(v1, 2.0);
        f0 = Math.Min(strike / 2.0, f0);
        f1 = Math.Max(strike * 1.5, Math.Min(f1, Math.Max(2.0, strike * 1.5)));

        const int sizef = 100;
        const int sizev = 100;
        int steps = (int)((24 * expiryTime) + 1);
        const int dampingSteps = 5;
        const double densityf = 0.1;
        const double densityv = 0.1;

        if (!(strike >= f0 && strike <= f1))
        {
            throw new ArgumentException(
                $"strike ({Utilities.DataFormattersFunctions.General(strike)}) must be inside pde grid "
                    + $"[{Utilities.DataFormattersFunctions.General(f0)};{Utilities.DataFormattersFunctions.General(f1)}]",
                nameof(strike));
        }

        // Layout
        var layout = new FdmLinearOpLayout([sizef, sizev]);

        // Mesher
        // two concentrating mesher around f and k to get the mesher for f
        double x0 = Math.Min(forward, strike);
        double x1 = Math.Max(forward, strike);
        int sizefa = Math.Max(4, (int)Math.Ceiling((((x0 + x1) / 2.0) - f0) / (f1 - f0) * sizef));
        int sizefb = sizef - sizefa + 1; // common point, so we can spend one more here
        var mfa = new Concentrating1dMesher(f0, (x0 + x1) / 2.0, sizefa, (x0, densityf), true);
        var mfb = new Concentrating1dMesher((x0 + x1) / 2.0, f1, sizefb, (x1, densityf), true);
        var mf = new Glued1dMesher(mfa, mfb);

        // Volatility mesher
        var mv = new Concentrating1dMesher(v0, v1, sizev, (alpha, densityv), true);
        var mesher = new FdmMesherComposite(layout, [mf, mv]);

        // initial values
        double[] rhs = new double[layout.Size];
        var fs = new List<double>();
        var vs = new List<double>();
        FdmLinearOpIterator iter = layout.Begin();
        for (int i = 0; i < layout.Size; ++i, iter.Increment())
        {
            double f = mesher.Location(iter, 0);
            rhs[iter.Index] = Math.Max(f - strike, 0.0);
            if (iter.Coordinates[1] == 0)
            {
                fs.Add(mesher.Location(iter, 0));
            }

            if (iter.Coordinates[0] == 0)
            {
                vs.Add(mesher.Location(iter, 1));
            }
        }

        // Boundary conditions: none
        var map = new FdmZabrOp(mesher, beta, nu, rho, gamma);
        new FdmBackwardSolver(map, [], null, FdmSchemeDesc.Hundsdorfer()).Rollback(rhs, expiryTime, 0.0, steps, dampingSteps);

        // the solution's rows by volatility, its columns by forward, as QuantLib's Matrix(f_.size(), v_.size()) holds
        // them, its two sizes equal
        var interpolation = new BicubicSpline([.. fs], [.. vs], new Matrix(vs.Count, fs.Count, rhs));
        return interpolation.Evaluate(forward, alpha);
    }

    // fdPrice(strikes), zabr.cpp:120-200.
    private double[] FdPrices(ReadOnlySpan<double> strikes)
    {
        // TODO check strikes to be increasing
        // TODO put these parameters somewhere
        double start = Math.Min(0.00001, strikes[0] * 0.5); // lowest strike for grid
        double end = Math.Max(0.10, strikes[^1] * 1.5); // highest strike for grid
        const int size = 500; // grid points
        const double density = 0.1; // density for concentrating mesher
        int steps = (int)Math.Ceiling(expiryTime * 24); // number of steps in dimension t
        const int dampingSteps = 5; // thereof damping steps

        // Layout
        var layout = new FdmLinearOpLayout([size]);

        // Mesher
        var m1 = new Concentrating1dMesher(start, end, size, (forward, density), true);
        var mesher = new FdmMesherComposite(layout, [m1]);

        // Boundary conditions: none

        // initial values
        double[] rhs = new double[layout.Size];
        FdmLinearOpIterator iter = layout.Begin();
        for (int i = 0; i < layout.Size; ++i, iter.Increment())
        {
            double k = mesher.Location(iter, 0);
            rhs[iter.Index] = Math.Max(forward - k, 0.0);
        }

        // local vols
        Vector ks = mesher.Locations(0);
        double[] locVol = LocalVolatilities(ks.AsSpan(), "strikes");

        // solver
        var map = new FdmDupire1dOp(mesher, new Vector(locVol));
        new FdmBackwardSolver(map, [], null, FdmSchemeDesc.Douglas()).Rollback(rhs, expiryTime, 0.0, steps, dampingSteps);

        // interpolate solution
        var solution = new CubicInterpolation(
            ks.AsSpan(),
            rhs,
            CubicInterpolationDerivativeApprox.Spline,
            true,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0);
        double[] result = new double[strikes.Length];
        for (int i = 0; i < strikes.Length; ++i)
        {
            result[i] = solution.Evaluate(strikes[i]);
        }

        return result;
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
