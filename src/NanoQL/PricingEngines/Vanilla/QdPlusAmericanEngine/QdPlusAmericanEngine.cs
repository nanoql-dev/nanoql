// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdplusamericanengine.hpp ql/pricingengines/vanilla/qdplusamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The American engine based on the QD+ approximation to the exercise boundary (M. Li, "Analytical Approximations for
/// the Critical Stock Prices of American Options: A Performance Comparison", 2009): the boundary at Chebyshev nodes
/// in √τ, the early-exercise premium integrated over it by tanh-sinh quadrature.
/// </summary>
/// <remarks>
/// <para>
/// Its main purpose is to provide a good initial guess to the exercise boundary for the superior fixed-point engine,
/// <see cref="QdFpAmericanEngine"/>. The engine reports the value.
/// </para>
/// <para>
/// Immutable: each τ's boundary is solved by a working evaluator built for that solve, and each price builds its own
/// boundary, as QuantLib's does. The static <c>Calculate</c> builds an engine with the given settings and prices
/// through it. An extension point, as in QuantLib.
/// </para>
/// </remarks>
public class QdPlusAmericanEngine : QdPutCallParityEngine
{
    private readonly int interpolationPoints;
    private readonly QdPlusAmericanEngineSolverType solverType;
    private readonly double eps;
    private readonly int maxIter;

    /// <summary>Initializes a new instance of the <see cref="QdPlusAmericanEngine"/> class.</summary>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="interpolationPoints">The number of Chebyshev nodes of the boundary.</param>
    /// <param name="solverType">The boundary's root finder.</param>
    /// <param name="eps">The boundary's and the premium's accuracy.</param>
    /// <param name="maxIter">The most evaluations of a boundary solve, or <see langword="null"/> for QuantLib's
    /// default: 100 for Brent's, Newton's and Ridder's methods, 10 for the Halley iterations.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="interpolationPoints"/> or <paramref name="maxIter"/> is negative, or the solver type isn't a
    /// member of its type.
    /// </exception>
    public QdPlusAmericanEngine(
        GeneralizedBlackScholesProcess process,
        int interpolationPoints = 8,
        QdPlusAmericanEngineSolverType solverType = QdPlusAmericanEngineSolverType.Halley,
        double eps = 1e-6,
        int? maxIter = null)
        : base(process)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(interpolationPoints);
        EnumArguments.Check(solverType, nameof(solverType));
        if (maxIter is int m)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(m, nameof(maxIter));
        }

        this.interpolationPoints = interpolationPoints;
        this.solverType = solverType;
        this.eps = eps;
        this.maxIter = maxIter ?? (solverType is QdPlusAmericanEngineSolverType.Newton
            or QdPlusAmericanEngineSolverType.Brent or QdPlusAmericanEngineSolverType.Ridder ? 100 : 10);
    }

    /// <summary>Prices an American vanilla option by the QD+ approximation.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="interpolationPoints">The number of Chebyshev nodes of the boundary.</param>
    /// <param name="solverType">The boundary's root finder.</param>
    /// <param name="eps">The boundary's and the premium's accuracy.</param>
    /// <param name="maxIter">The most evaluations of a boundary solve, or <see langword="null"/> for QuantLib's
    /// default.</param>
    /// <returns>The option's results: the value.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The engine refuses the option.</exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int interpolationPoints = 8,
        QdPlusAmericanEngineSolverType solverType = QdPlusAmericanEngineSolverType.Halley,
        double eps = 1e-6,
        int? maxIter = null) =>
        new QdPlusAmericanEngine(process, interpolationPoints, solverType, eps, maxIter).Calculate(option, context);

    /// <summary>
    /// The exercise boundary's limit at expiry: Table 2 of L. Andersen and M. Lake (2021), "Fast American Option
    /// Pricing: The Double-Boundary Case".
    /// </summary>
    /// <param name="k">The strike.</param>
    /// <param name="r">The risk-free rate.</param>
    /// <param name="q">The dividend yield.</param>
    /// <returns>The limit; 0 in the European cases.</returns>
    /// <exception cref="NanoQLException">A rate is NaN ("internal error").</exception>
    public static double XMax(double k, double r, double q)
    {
        // Table 2 from Leif Andersen, Mark Lake (2021)
        // "Fast American Option Pricing: The Double-Boundary Case"
        if (r > 0.0 && q > 0.0)
        {
            return k * Min(1.0, r / q);
        }
        else if (r > 0.0 && q <= 0.0)
        {
            return k;
        }
        else if (r == 0.0 && q < 0.0)
        {
            return k;
        }
        else if (r == 0.0 && q >= 0.0)
        {
            return 0.0; // Eurpoean case
        }
        else if (r < 0.0 && q >= 0.0)
        {
            return 0.0; // European case
        }
        else if (r < 0.0 && q < r)
        {
            return k; // double boundary case
        }
        else if (r < 0.0 && r <= q && q < 0.0)
        {
            return 0; // European case
        }
        else
        {
            throw new NanoQLException("internal error");
        }
    }

    /// <summary>The put's exercise boundary at a time to expiry, and the number of evaluations its solve took.</summary>
    /// <param name="s">The spot.</param>
    /// <param name="k">The strike.</param>
    /// <param name="r">The risk-free rate.</param>
    /// <param name="q">The dividend yield.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="t">The option's time to expiry.</param>
    /// <param name="tau">The time to expiry the boundary is at.</param>
    /// <returns>The number of evaluations and the boundary; 0 and <see cref="XMax"/> below a τ of ε.</returns>
    /// <exception cref="NanoQLException">The solver refuses.</exception>
    public (int Evaluations, double Boundary) PutExerciseBoundaryAtTau(
        double s, double k, double r, double q, double vol, double t, double tau)
    {
        if (tau < NumericLimits.QL_EPSILON)
        {
            return (0, XMax(k, r, q));
        }

        var eval = new BoundaryEvaluator(s, k, r, q, vol, tau, t);

        double x;
        switch (solverType)
        {
            case QdPlusAmericanEngineSolverType.Brent:
                x = BuildInSolver(eval, new Brent(), s, k, maxIter);
                break;
            case QdPlusAmericanEngineSolverType.Newton:
                x = BuildInSolver(eval, new Newton(), s, k, maxIter);
                break;
            case QdPlusAmericanEngineSolverType.Ridder:
                x = BuildInSolver(eval, new Ridder(), s, k, maxIter);
                break;
            case QdPlusAmericanEngineSolverType.Halley:
            case QdPlusAmericanEngineSolverType.SuperHalley:
            {
                bool resultCloseEnough;
                x = eval.XMax;
                double xOld, fx;
                double xmin = eval.XMin;

                do
                {
                    xOld = x;
                    fx = eval.Evaluate(x);
                    double fPrime = eval.Derivative(x);
                    double lf = fx * eval.FPrime2(x) / (fPrime * fPrime);
                    double step = solverType == QdPlusAmericanEngineSolverType.Halley
                        ? 1 / (1 - (0.5 * lf)) * fx / fPrime
                        : (1 + (0.5 * lf / (1 - lf))) * fx / fPrime;

                    x = Max(xmin, x - step);
                    resultCloseEnough = Math.Abs(x - xOld) < 0.5 * eps;
                }
                while (!resultCloseEnough && eval.Evaluations < maxIter);

                if (!resultCloseEnough && !ComparisonFunctions.Close(Math.Abs(fx), 0.0))
                {
                    x = BuildInSolver(eval, new Brent(), s, k, 10 * maxIter, x);
                }

                break;
            }

            default:
                throw new NanoQLException("unknown solver type");
        }

        return (eval.Evaluations, x);
    }

    /// <summary>
    /// The put's exercise boundary as a Chebyshev interpolation, on the second kind's nodes in z = 2√(τ/T) − 1, of
    /// ln(b(τ)/x_max)²: each node's boundary solved by <see cref="PutExerciseBoundaryAtTau"/>.
    /// </summary>
    /// <param name="s">The spot.</param>
    /// <param name="k">The strike.</param>
    /// <param name="r">The risk-free rate.</param>
    /// <param name="q">The dividend yield.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="t">The time to expiry.</param>
    /// <returns>The interpolation.</returns>
    /// <exception cref="NanoQLException">A node's solve refuses.</exception>
    public ChebyshevInterpolation GetPutExerciseBoundary(double s, double k, double r, double q, double vol, double t)
    {
        double xmax = XMax(k, r, q);

        return new ChebyshevInterpolation(
            interpolationPoints,
            z =>
            {
                double xSq = 0.25 * t * ((1 + z) * (1 + z));
                double l = Math.Log(PutExerciseBoundaryAtTau(s, k, r, q, vol, t, xSq).Boundary / xmax);
                return l * l;
            },
            ChebyshevInterpolationPointsType.SecondKind);
    }

    /// <inheritdoc/>
    protected override double CalculatePut(double s, double k, double r, double q, double vol, double t)
    {
        if (r < 0.0 && q < r)
        {
            throw new NanoQLException("double-boundary case q<r<0 for a put option is given");
        }

        ChebyshevInterpolation qZ = GetPutExerciseBoundary(s, k, r, q, vol, t);

        double xmax = XMax(k, r, q);
        var aov = new QdPlusAddOnValue(t, s, k, r, q, vol, xmax, qZ);

        double addOn = new TanhSinhIntegral(eps).Integrate(aov.Evaluate, 0.0, Math.Sqrt(t));

        if (!(addOn > -10 * eps))
        {
            throw new NanoQLException(
                $"negative early exercise value {addOn.ToString(CultureInfo.InvariantCulture)}");
        }

        double europeanValue = Max(
            0.0,
            new BlackCalculator(OptionType.Put, k, s * Math.Exp((r - q) * t), vol * Math.Sqrt(t), Math.Exp(-r * t)).Value);

        return europeanValue + Max(0.0, addOn);
    }

    // QuantLib's buildInSolver: the solver bracketed from the evaluator's lower end, its upper end doubled until the
    // function changes sign, the guess clamped inside.
    private double BuildInSolver<TSolver>(BoundaryEvaluator eval, TSolver solver, double s, double strike, int maxIterations, double? guess = null)
        where TSolver : Solver1D<TSolver, ISolver1DFunction>
    {
        _ = strike;
        solver = solver.SetMaxEvaluations(maxIterations).SetLowerBound(eval.XMin);

        double fxmin = eval.Evaluate(eval.XMin);
        double xmax = Max(0.5 * (eval.XMax + s), eval.XMax);
        while (eval.Evaluate(xmax) * fxmin > 0.0 && eval.Evaluations < maxIter)
        {
            xmax *= 2;
        }

        double g = guess ?? (0.5 * (xmax + s));

        if (g >= xmax)
        {
            g = Math.BitDecrement(xmax);
        }
        else if (g <= eval.XMin)
        {
            g = Math.BitIncrement(eval.XMin);
        }

        return solver.Solve(new Function(eval), eps, g, eval.XMin, xmax);
    }

    // Newton's solver, whose function concept is the derivative one.
    private double BuildInSolver(BoundaryEvaluator eval, Newton solver, double s, double strike, int maxIterations)
    {
        _ = strike;
        solver = solver.SetMaxEvaluations(maxIterations).SetLowerBound(eval.XMin);

        double fxmin = eval.Evaluate(eval.XMin);
        double xmax = Max(0.5 * (eval.XMax + s), eval.XMax);
        while (eval.Evaluate(xmax) * fxmin > 0.0 && eval.Evaluations < maxIter)
        {
            xmax *= 2;
        }

        double g = 0.5 * (xmax + s);

        if (g >= xmax)
        {
            g = Math.BitDecrement(xmax);
        }
        else if (g <= eval.XMin)
        {
            g = Math.BitIncrement(eval.XMin);
        }

        return solver.Solve(new Function(eval), eps, g, eval.XMin, xmax);
    }

    // The evaluator as the solvers' function concept, a value type over the working object.
    private readonly struct Function(BoundaryEvaluator eval) : IDerivativeSolver1DFunction
    {
        public double Evaluate(double x) => eval.Evaluate(x);

        public double Derivative(double x) => eval.Derivative(x);
    }

    // QuantLib's QdPlusBoundaryEvaluator: the QD+ boundary equation at a time to expiry, with its first and second
    // derivatives and its count of evaluations; a working object of one solve, caching its last spot's terms.
    private sealed class BoundaryEvaluator
    {
        private readonly CumulativeNormalDistribution bigPhi = new();
        private readonly NormalDistribution phi = new();
        private readonly double tau;
        private readonly double k;
        private readonly double sigma2;
        private readonly double v;
        private readonly double r;
        private readonly double q;
        private readonly double dr;
        private readonly double dq;
        private readonly double lambda;
        private readonly double alpha;
        private readonly double beta;

        private double sc = 3.4028234663852886e38; // QuantLib's Null<Real>()
        private double dp;
        private double dm;
        private double phiDp;
        private double bigPhiDp;
        private double bigPhiDm;
        private double npv;
        private double theta;
        private double charm;

        public BoundaryEvaluator(double s, double strike, double rf, double dy, double vol, double t, double bigT)
        {
            _ = bigT;
            tau = t;
            k = strike;
            double sigma = vol;
            sigma2 = sigma * sigma;
            v = sigma * Math.Sqrt(tau);
            r = rf;
            q = dy;
            dr = Math.Exp(-r * tau);
            dq = Math.Exp(-q * tau);
            double ddr = Math.Abs(r * tau) > 1e-5 ? r / (1 - dr) : 1 / (tau * (1 - (0.5 * r * tau * (1 - (r * tau / 3)))));
            double omega = 2 * (r - q) / sigma2;
            lambda = 0.5 * (-(omega - 1) - Math.Sqrt(((omega - 1) * (omega - 1)) + (8 * ddr / sigma2)));
            double lambdaPrime = 2 * ddr * ddr / (sigma2 * Math.Sqrt(((omega - 1) * (omega - 1)) + (8 * ddr / sigma2)));
            alpha = 2 * dr / (sigma2 * ((2 * lambda) + omega - 1));
            beta = (alpha * (ddr + (lambdaPrime / ((2 * lambda) + omega - 1)))) - lambda;
            XMax = QdPlusAmericanEngine.XMax(strike, r, q);
            XMin = NumericLimits.QL_EPSILON * 1e4 * Min(0.5 * (strike + s), XMax);
        }

        public double XMax { get; }

        public double XMin { get; }

        public int Evaluations { get; private set; }

        public double Evaluate(double x)
        {
            ++Evaluations;

            if (x != sc)
            {
                PreCalculate(x);
            }

            if (ComparisonFunctions.CloseEnough(k - x, npv))
            {
                return ((1 - (dq * bigPhiDp)) * x) + (alpha * theta / dr);
            }
            else
            {
                double c0 = -beta - lambda + (alpha * theta / (dr * (k - x - npv)));
                return ((1 - (dq * bigPhiDp)) * x) + ((lambda + c0) * (k - x - npv));
            }
        }

        public double Derivative(double x)
        {
            if (x != sc)
            {
                PreCalculate(x);
            }

            return 1 - (dq * bigPhiDp) + (dq / v * phiDp) + (beta * (1 - (dq * bigPhiDp))) + (alpha / dr * charm);
        }

        public double FPrime2(double x)
        {
            if (x != sc)
            {
                PreCalculate(x);
            }

            double gamma = phiDp * dq / (v * x);
            double colour = gamma * (q + ((r - q) * dp / v) + ((1 - (dp * dm)) / (2 * tau)));

            return (dq * ((phiDp / (x * v)) - (phiDp * dp / (x * v * v)))) + (beta * gamma) + (alpha / dr * colour);
        }

        private void PreCalculate(double x)
        {
            x = Max(NumericLimits.QL_EPSILON, x);
            sc = x;
            dp = (Math.Log(x * dq / (k * dr)) / v) + (0.5 * v);
            dm = dp - v;
            bigPhiDp = bigPhi.Evaluate(-dp);
            bigPhiDm = bigPhi.Evaluate(-dm);
            phiDp = phi.Evaluate(dp);

            npv = (dr * k * bigPhiDm) - (x * dq * bigPhiDp);
            theta = (r * k * dr * bigPhiDm) - (q * x * dq * bigPhiDp) - (sigma2 * x / (2 * v) * dq * phiDp);
            charm = -dq * ((phiDp * (((r - q) / v) - (dm / (2 * tau)))) + (q * bigPhiDp));
        }
    }

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;

    // std::min's semantics, which keep or drop a NaN by position where Math.Min propagates it.
    private static double Min(double a, double b) => b < a ? b : a;
}
