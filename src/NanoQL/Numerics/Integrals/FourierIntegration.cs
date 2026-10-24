// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/fourierintegration.hpp ql/math/integrals/fourierintegration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: discreteSimpson and discreteTrapezoid (S-van's discrete integrals); expSinh (S-core)

namespace NanoQL.Numerics;

/// <summary>
/// The Fourier integration of characteristic-function pricing engines: a quadrature or an adaptive integrator, with the
/// change of variables that maps the semi-infinite integration range onto the domain the algorithm expects. The decay
/// rate c_∞ of the characteristic function along the integration contour drives the mapping.
/// </summary>
/// <remarks>
/// Immutable: <see cref="CalculateWithStatistics"/> returns each call's number of evaluations with the value (the
/// quadrature's order, or the integrator's count), which QuantLib's <c>numberOfEvaluations()</c> reads after the call.
/// </remarks>
public sealed class FourierIntegration
{
    // QuantLib's Null<Real>(): GaussLobatto's absolute tolerance where none is given, and a bound read as none.
    private const double NullReal = 3.4028234663852886e38;

    private readonly Algorithm intAlgo_;
    private readonly Integrator? integrator_;
    private readonly GaussianQuadrature? gaussianQuadrature_;

    private FourierIntegration(Algorithm intAlgo, Integrator integrator)
    {
        intAlgo_ = intAlgo;
        integrator_ = integrator;
    }

    private FourierIntegration(Algorithm intAlgo, GaussianQuadrature gaussianQuadrature)
    {
        intAlgo_ = intAlgo;
        gaussianQuadrature_ = gaussianQuadrature;
    }

    private enum Algorithm
    {
        GaussLobatto,
        GaussKronrod,
        Simpson,
        Trapezoid,
        DiscreteTrapezoid,
        DiscreteSimpson,
        GaussLaguerre,
        GaussLegendre,
        GaussChebyshev,
        GaussChebyshev2nd,
        ExpSinh,
        TanhSinh,
    }

    /// <summary>Gets a value indicating whether the integration is adaptive, its range possibly bounded by the
    /// caller.</summary>
    public bool IsAdaptiveIntegration =>
        intAlgo_ == Algorithm.GaussLobatto
        || intAlgo_ == Algorithm.GaussKronrod
        || intAlgo_ == Algorithm.Simpson
        || intAlgo_ == Algorithm.Trapezoid
        || intAlgo_ == Algorithm.ExpSinh
        || intAlgo_ == Algorithm.TanhSinh;

    /// <summary>The Gauss-Laguerre quadrature, on the semi-infinite range directly.</summary>
    /// <param name="integrationOrder">The order, positive, at most 192.</param>
    /// <returns>The integration.</returns>
    /// <exception cref="ArgumentException">The order is zero or above 192.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    public static FourierIntegration GaussLaguerre(int integrationOrder = 128)
    {
        if (!(integrationOrder <= 192))
        {
            throw new ArgumentException("maximum integraton order (192) exceeded", nameof(integrationOrder));
        }

        return new FourierIntegration(Algorithm.GaussLaguerre, new GaussLaguerreIntegration(integrationOrder));
    }

    /// <summary>The Gauss-Legendre quadrature, on [−1, 1] mapped by −ln((1 − x)/2)/c_∞.</summary>
    /// <param name="integrationOrder">The order, positive.</param>
    /// <returns>The integration.</returns>
    /// <exception cref="ArgumentException">The order is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    public static FourierIntegration GaussLegendre(int integrationOrder = 128) =>
        new(Algorithm.GaussLegendre, new GaussLegendreIntegration(integrationOrder));

    /// <summary>The Gauss-Chebyshev quadrature, on [−1, 1] mapped by −ln((1 − x)/2)/c_∞.</summary>
    /// <param name="integrationOrder">The order, positive.</param>
    /// <returns>The integration.</returns>
    /// <exception cref="ArgumentException">The order is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    public static FourierIntegration GaussChebyshev(int integrationOrder = 128) =>
        new(Algorithm.GaussChebyshev, new GaussChebyshevIntegration(integrationOrder));

    /// <summary>The Gauss-Chebyshev quadrature of the second kind, on [−1, 1] mapped by −ln((1 − x)/2)/c_∞.</summary>
    /// <param name="integrationOrder">The order, positive.</param>
    /// <returns>The integration.</returns>
    /// <exception cref="ArgumentException">The order is zero.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order is negative.</exception>
    public static FourierIntegration GaussChebyshev2nd(int integrationOrder = 128) =>
        new(Algorithm.GaussChebyshev2nd, new GaussChebyshev2ndIntegration(integrationOrder));

    /// <summary>
    /// The adaptive Gauss-Lobatto integration, on [0, the caller's bound] or on [0, 1] mapped by −ln(x)/c_∞. Too large
    /// a maximum number of evaluations deepens the recursion.
    /// </summary>
    /// <param name="relTolerance">The relative tolerance, or <see langword="null"/> (QuantLib's <c>Null</c>) for
    /// none.</param>
    /// <param name="absTolerance">The absolute tolerance, or <see langword="null"/> (QuantLib's <c>Null</c>, a number
    /// above any) for none.</param>
    /// <param name="maxEvaluations">The maximum number of evaluations.</param>
    /// <param name="useConvergenceEstimate">Whether the tolerance is scaled by the convergence estimate.</param>
    /// <returns>The integration.</returns>
    /// <exception cref="ArgumentException">The absolute tolerance isn't above machine epsilon.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum number of evaluations is negative.</exception>
    public static FourierIntegration GaussLobatto(
        double? relTolerance, double? absTolerance, int maxEvaluations = 1000, bool useConvergenceEstimate = false) =>
        new(
            Algorithm.GaussLobatto,
            new GaussLobattoIntegral(maxEvaluations, absTolerance ?? NullReal, relTolerance, useConvergenceEstimate));

    /// <summary>
    /// The adaptive Gauss-Kronrod integration, on [0, the caller's bound] or on [0, 1] mapped by −ln(x)/c_∞; its
    /// convergence is usually poor, as QuantLib documents.
    /// </summary>
    /// <param name="absTolerance">The absolute tolerance, above machine epsilon.</param>
    /// <param name="maxEvaluations">The maximum number of evaluations, at least 15.</param>
    /// <returns>The integration.</returns>
    /// <exception cref="ArgumentException">The tolerance isn't above machine epsilon, or the maximum is below
    /// 15.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is negative.</exception>
    public static FourierIntegration GaussKronrod(double absTolerance, int maxEvaluations = 1000) =>
        new(Algorithm.GaussKronrod, new GaussKronrodAdaptive(absTolerance, maxEvaluations));

    /// <summary>Simpson's integration, on [0, the caller's bound] or on [0, 1] mapped by −ln(x)/c_∞.</summary>
    /// <param name="absTolerance">The absolute tolerance, above machine epsilon.</param>
    /// <param name="maxEvaluations">The maximum number of refinements.</param>
    /// <returns>The integration.</returns>
    /// <exception cref="ArgumentException">The tolerance isn't above machine epsilon.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is negative.</exception>
    public static FourierIntegration Simpson(double absTolerance, int maxEvaluations = 1000) =>
        new(Algorithm.Simpson, new SimpsonIntegral(absTolerance, maxEvaluations));

    /// <summary>The trapezoid integration, on [0, the caller's bound] or on [0, 1] mapped by −ln(x)/c_∞.</summary>
    /// <param name="absTolerance">The absolute tolerance, above machine epsilon.</param>
    /// <param name="maxEvaluations">The maximum number of refinements.</param>
    /// <returns>The integration.</returns>
    /// <exception cref="ArgumentException">The tolerance isn't above machine epsilon.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is negative.</exception>
    public static FourierIntegration Trapezoid(double absTolerance, int maxEvaluations = 1000) =>
        new(Algorithm.Trapezoid, new TrapezoidIntegral<Default>(absTolerance, maxEvaluations));

    /// <summary>The tanh-sinh integration, on [0, ∞) directly, Boost's map of a half-infinite range.</summary>
    /// <param name="relTolerance">The tolerance relative to the integral's L1 norm.</param>
    /// <returns>The integration.</returns>
    public static FourierIntegration TanhSinh(double relTolerance = 1e-8) =>
        new(Algorithm.TanhSinh, new TanhSinhIntegral(relTolerance));

    /// <summary>
    /// Andersen and Piterbarg's integration limit: the larger of the roots of c_∞·u + ln u + ln ε and of
    /// (v₀t/2)·u² + ln u + ln ε, or the first alone where the second can't be bracketed.
    /// </summary>
    /// <param name="c_inf">The decay rate c_∞.</param>
    /// <param name="epsilon">The tolerance ε.</param>
    /// <param name="v0">The initial variance.</param>
    /// <param name="t">The maturity.</param>
    /// <returns>The limit.</returns>
    /// <exception cref="NanoQLException">The first root can't be bracketed.</exception>
    public static double AndersenPiterbargIntegrationLimit(double c_inf, double epsilon, double v0, double t)
    {
        double uMaxGuess = -Math.Log(epsilon) / c_inf;
        double uMaxStep = 0.1 * uMaxGuess;

        double logEpsilon = Math.Log(epsilon);
        double uMax = new Brent().Solve(
            u => (c_inf * u) + Math.Log(u) + logEpsilon, NumericLimits.QL_EPSILON * uMaxGuess, uMaxGuess, uMaxStep);

        try
        {
            double uHatMaxGuess = Math.Sqrt(-Math.Log(epsilon) / (0.5 * v0 * t));
            double v0T2 = 0.5 * v0 * t;
            double uHatMax = new Brent().Solve(
                u => (v0T2 * u * u) + Math.Log(u) + logEpsilon,
                NumericLimits.QL_EPSILON * uHatMaxGuess,
                uHatMaxGuess,
                0.001 * uHatMaxGuess);

            return uMax < uHatMax ? uHatMax : uMax;
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            // QuantLib's catch (const Error&)
            return uMax;
        }
    }

    /// <summary>The integral of <paramref name="f"/> on [0, ∞).</summary>
    /// <param name="c_inf">The decay rate c_∞ of the mapping.</param>
    /// <param name="f">The integrand.</param>
    /// <param name="maxBound">For an adaptive integrator, the range's bound, or <see langword="null"/> (or a bound
    /// returning <see langword="null"/>, QuantLib's <c>Null</c>) for the mapped range.</param>
    /// <param name="scaling">The scaling of QuantLib's exp-sinh integration, which waits for S-core.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The integrator doesn't reach its accuracy.</exception>
    public double Calculate(double c_inf, Func<double, double> f, Func<double?>? maxBound = null, double scaling = 1.0) =>
        CalculateWithStatistics(c_inf, f, maxBound, scaling).Value;

    /// <summary>The integral of <paramref name="f"/> on [0, ∞), an adaptive integrator's range bounded.</summary>
    /// <param name="c_inf">The decay rate c_∞ of the mapping.</param>
    /// <param name="f">The integrand.</param>
    /// <param name="maxBound">For an adaptive integrator, the range's bound.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The integrator doesn't reach its accuracy.</exception>
    public double Calculate(double c_inf, Func<double, double> f, double maxBound) =>
        CalculateWithStatistics(c_inf, f, () => maxBound).Value;

    /// <summary>The integral of <paramref name="f"/> on [0, ∞), with the call's number of evaluations.</summary>
    /// <param name="c_inf">The decay rate c_∞ of the mapping.</param>
    /// <param name="f">The integrand.</param>
    /// <param name="maxBound">For an adaptive integrator, the range's bound, or <see langword="null"/> (or a bound
    /// returning <see langword="null"/>, QuantLib's <c>Null</c>) for the mapped range.</param>
    /// <param name="scaling">The scaling of QuantLib's exp-sinh integration, which waits for S-core.</param>
    /// <returns>The integral and the number of evaluations: the quadrature's order, or the integrator's
    /// count.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The integrator doesn't reach its accuracy.</exception>
    public IntegrationResult CalculateWithStatistics(
        double c_inf, Func<double, double> f, Func<double?>? maxBound = null, double scaling = 1.0)
    {
        ArgumentNullException.ThrowIfNull(f);
        _ = scaling;

        switch (intAlgo_)
        {
            case Algorithm.GaussLaguerre:
                return new IntegrationResult(gaussianQuadrature_!.Evaluate(f), gaussianQuadrature_.Order);
            case Algorithm.GaussLegendre:
            case Algorithm.GaussChebyshev:
            case Algorithm.GaussChebyshev2nd:
                return new IntegrationResult(
                    gaussianQuadrature_!.Evaluate(x => Integrand1(c_inf, f, x)), gaussianQuadrature_.Order);
            case Algorithm.TanhSinh:
                return integrator_!.IntegrateWithStatistics(f, 0.0, double.MaxValue);
            case Algorithm.Simpson:
            case Algorithm.Trapezoid:
            case Algorithm.GaussLobatto:
            case Algorithm.GaussKronrod:
            {
                double? bound = maxBound?.Invoke();
                if (bound is double b && b != NullReal)
                {
                    return integrator_!.IntegrateWithStatistics(f, 0.0, b);
                }
                else
                {
                    return integrator_!.IntegrateWithStatistics(x => Integrand2(c_inf, f, x), 0.0, 1.0);
                }
            }

            default:
                throw new NanoQLException("unknwon integration algorithm");
        }
    }

    private static double Integrand1(double c_inf, Func<double, double> f, double x)
    {
        if ((1.0 - x) * c_inf > NumericLimits.QL_EPSILON)
        {
            return f(-Math.Log(0.5 - (0.5 * x)) / c_inf) / ((1.0 - x) * c_inf);
        }
        else
        {
            return 0.0;
        }
    }

    private static double Integrand2(double c_inf, Func<double, double> f, double x)
    {
        if (x * c_inf > NumericLimits.QL_EPSILON)
        {
            return f(-Math.Log(x) / c_inf) / (x * c_inf);
        }
        else
        {
            return 0.0;
        }
    }
}
