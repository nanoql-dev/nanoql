// SPDX-FileCopyrightText: Copyright (C) 2006, 2007, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2006 Cristina Duminuco
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: Copyright (C) 2006, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2005, 2006 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/abcdcalibration.hpp ql/termstructures/volatility/abcdcalibration.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// The abcd instantaneous volatility fitted to Black volatilities by their maturities: the root-mean-square error of
/// the abcd Black volatilities, minimized over a, b, c and d through QuantLib's transformation (c, d and a + d
/// positive), the fixed parameters at their guesses.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: <see cref="Compute"/> returns the fitted calibration, where QuantLib's <c>compute()</c> changes the
/// object; before it the parameters are the guesses, and the weights 1/n.
/// </para>
/// <para>
/// One of QuantLib's refusals is narrowed: QuantLib's transformation leaves b free, so its optimizer can step to a
/// trial point the abcd function's validation refuses, and the fit throws. Here such a trial point's residuals are 1.0
/// each, a guard that keeps every fit QuantLib completes bit for bit, since none of them reaches such a point. A fit on
/// which the guard acted is returned where it ends inside the valid region and refused with QuantLib's text, its first
/// invalid trial point's, where it ends against the zero floor: its lowest instantaneous volatility below 1% of the
/// smallest Black volatility given, as for data whose total variance shrinks, which no valid curve fits.
/// </para>
/// <para>
/// One of QuantLib's defects is fixed: its transformation gives a as a positive a + d less d, so with a fixed and d
/// free it fixes a + d, and the fit moves a by what it moves d. Here a fixed keeps its guess, and d stays above
/// max(0, −a), the same region; every other choice of fixed parameters is QuantLib's.
/// </para>
/// </remarks>
public sealed class AbcdCalibration
{
    private const double GuardResidual = 1.0;
    private const double FloorFraction = 0.01;

    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> blackVols;
    private readonly ImmutableArray<double> weights;
    private readonly EndCriteria endCriteria;
    private readonly OptimizationMethod optMethod;
    private readonly bool vegaWeighted;

    /// <summary>Initializes a new instance of the <see cref="AbcdCalibration"/> class, not yet fitted.</summary>
    /// <param name="t">The maturities.</param>
    /// <param name="blackVols">The Black volatilities, one per maturity.</param>
    /// <param name="aGuess">a's guess.</param>
    /// <param name="bGuess">b's guess.</param>
    /// <param name="cGuess">c's guess, positive.</param>
    /// <param name="dGuess">d's guess, non-negative.</param>
    /// <param name="aIsFixed">Whether a is fixed at its guess.</param>
    /// <param name="bIsFixed">Whether b is fixed at its guess.</param>
    /// <param name="cIsFixed">Whether c is fixed at its guess.</param>
    /// <param name="dIsFixed">Whether d is fixed at its guess.</param>
    /// <param name="vegaWeighted">Whether the errors are weighted by the at-the-money vegas.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for QuantLib's
    /// <c>EndCriteria(10000, 1000, 1e-8, 0.3e-4, 0.3e-4)</c>.</param>
    /// <param name="method">The optimizer, or <see langword="null"/> for QuantLib's
    /// <c>LevenbergMarquardt(1e-8, 1e-8, 1e-8, false)</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="t"/> or <paramref name="blackVols"/> is
    /// null.</exception>
    /// <exception cref="ArgumentException">The guesses fail <see cref="AbcdMathFunction.Validate"/>, or the sizes
    /// differ.</exception>
    public AbcdCalibration(
        IEnumerable<double> t,
        IEnumerable<double> blackVols,
        double aGuess = -0.06,
        double bGuess = 0.17,
        double cGuess = 0.54,
        double dGuess = 0.17,
        bool aIsFixed = false,
        bool bIsFixed = false,
        bool cIsFixed = false,
        bool dIsFixed = false,
        bool vegaWeighted = false,
        EndCriteria? endCriteria = null,
        OptimizationMethod? method = null)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(blackVols);
        times = [.. t];
        this.blackVols = [.. blackVols];
        AIsFixed = aIsFixed;
        BIsFixed = bIsFixed;
        CIsFixed = cIsFixed;
        DIsFixed = dIsFixed;
        A = aGuess;
        B = bGuess;
        C = cGuess;
        D = dGuess;
        EndCriteria = EndCriteriaType.None;
        weights = [.. Enumerable.Repeat(1.0 / this.blackVols.Length, this.blackVols.Length)];
        this.vegaWeighted = vegaWeighted;

        AbcdMathFunction.Validate(aGuess, bGuess, cGuess, dGuess);

        if (!(this.blackVols.Length == times.Length))
        {
            throw new ArgumentException(
                $"mismatch between number of times ({times.Length}) and blackVols ({this.blackVols.Length})", nameof(blackVols));
        }

        // if no optimization method or endCriteria is provided, we provide one
        this.optMethod = method ?? new LevenbergMarquardt(1.0e-8, 1.0e-8, 1.0e-8, false);
        this.endCriteria = endCriteria ?? new EndCriteria(10000, 1000, 1.0e-8, 0.3e-4, 0.3e-4); // Why 0.3e-4 ?
    }

    private AbcdCalibration(AbcdCalibration other, double a, double b, double c, double d, ImmutableArray<double> weights, EndCriteriaType type, ParametersTransformation? transformation)
    {
        times = other.times;
        blackVols = other.blackVols;
        endCriteria = other.endCriteria;
        optMethod = other.optMethod;
        vegaWeighted = other.vegaWeighted;
        AIsFixed = other.AIsFixed;
        BIsFixed = other.BIsFixed;
        CIsFixed = other.CIsFixed;
        DIsFixed = other.DIsFixed;
        A = a;
        B = b;
        C = c;
        D = d;
        this.weights = weights;
        EndCriteria = type;
        Transformation = transformation;
    }

    /// <summary>Gets a: the guess, or the fit.</summary>
    public double A { get; }

    /// <summary>Gets b: the guess, or the fit.</summary>
    public double B { get; }

    /// <summary>Gets c: the guess, or the fit.</summary>
    public double C { get; }

    /// <summary>Gets d: the guess, or the fit.</summary>
    public double D { get; }

    /// <summary>Gets a value indicating whether a is fixed.</summary>
    public bool AIsFixed { get; }

    /// <summary>Gets a value indicating whether b is fixed.</summary>
    public bool BIsFixed { get; }

    /// <summary>Gets a value indicating whether c is fixed.</summary>
    public bool CIsFixed { get; }

    /// <summary>Gets a value indicating whether d is fixed.</summary>
    public bool DIsFixed { get; }

    /// <summary>Gets the end criterion the fit met: <see cref="EndCriteriaType.None"/> before <see cref="Compute"/>
    /// and with every parameter fixed.</summary>
    public EndCriteriaType EndCriteria { get; }

    /// <summary>Gets the parameters' transformation the fit used, or <see langword="null"/> before a fit, QuantLib's
    /// empty pointer.</summary>
    public ParametersTransformation? Transformation { get; }

    /// <summary>The fit, from the guesses: QuantLib's <c>compute()</c>, the receiver unchanged.</summary>
    /// <returns>The fitted calibration.</returns>
    /// <exception cref="NanoQLException">The optimizer fails, or the fit ends against the zero floor after reaching a
    /// trial point QuantLib's validation refuses, with that refusal's text.</exception>
    public AbcdCalibration Compute()
    {
        ImmutableArray<double> fitWeights = weights;
        if (vegaWeighted)
        {
            var w = new double[times.Length];
            double weightsSum = 0.0;
            for (int i = 0; i < times.Length; i++)
            {
                double stdDev = Math.Sqrt(blackVols[i] * blackVols[i] * times[i]);

                // when strike==forward, the blackFormulaStdDevDerivative becomes
                w[i] = new CumulativeNormalDistribution().Derivative(.5 * stdDev);
                weightsSum += w[i];
            }

            // weight normalization
            for (int i = 0; i < times.Length; i++)
            {
                w[i] /= weightsSum;
            }

            fitWeights = [.. w];
        }

        // there is nothing to optimize
        if (AIsFixed && BIsFixed && CIsFixed && DIsFixed)
        {
            return new AbcdCalibration(this, A, B, C, D, fitWeights, EndCriteriaType.None, Transformation);
        }

        var transformation = new AbcdParametersTransformation(AIsFixed && !DIsFixed ? A : null);
        var costFunction = new AbcdError(times, blackVols, fitWeights, transformation);
        Vector guess = new([A, B, C, D]);
        Vector inversedTransformatedGuess = transformation.Inverse(guess);
        var projectedAbcdCostFunction = new ProjectedCostFunction(
            costFunction, inversedTransformatedGuess.AsSpan(), [AIsFixed, BIsFixed, CIsFixed, DIsFixed]);
        Vector projectedGuess = projectedAbcdCostFunction.Project(inversedTransformatedGuess);

        var problem = new ProblemState(new Problem(projectedAbcdCostFunction, new NoConstraint(), projectedGuess.AsSpan()));
        EndCriteriaType type = optMethod.Minimize(problem, endCriteria);
        Vector projectedResult = problem.CurrentValue;
        Vector transfResult = projectedAbcdCostFunction.Include(projectedResult);
        Vector result = transformation.Direct(transfResult);

        if (costFunction.FirstRefusal is string refusal)
        {
            // the guard acted: QuantLib's refusal where the fit ends invalid or against the zero floor
            if (AbcdMathFunction.Check(result[0], result[1], result[2], result[3]) is not null
                || LowestInstantaneousVolatility(result[0], result[1], result[2], result[3]) < FloorFraction * blackVols.Min())
            {
                throw new NanoQLException(refusal);
            }
        }

        // QuantLib validates its members, the last trial point's, which every fit it completes keeps valid
        AbcdMathFunction.Validate(result[0], result[1], result[2], result[3]);
        return new AbcdCalibration(this, result[0], result[1], result[2], result[3], fitWeights, type, transformation);
    }

    /// <summary>The fitted abcd Black volatility at a maturity.</summary>
    /// <param name="x">The maturity.</param>
    /// <returns>The volatility.</returns>
    /// <exception cref="ArgumentException">The parameters fail <see cref="AbcdMathFunction.Validate"/>, or the maturity
    /// is negative.</exception>
    public double Value(double x) => AbcdFunctions.AbcdBlackVolatility(x, A, B, C, D);

    /// <summary>The adjustment factors that match the Black volatilities: each volatility over the fitted
    /// one.</summary>
    /// <param name="t">The maturities.</param>
    /// <param name="blackVols">The Black volatilities, one per maturity.</param>
    /// <returns>The factors.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The sizes differ.</exception>
    public ImmutableArray<double> K(IEnumerable<double> t, IEnumerable<double> blackVols)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(blackVols);
        ImmutableArray<double> ts = [.. t], vols = [.. blackVols];
        if (!(vols.Length == ts.Length))
        {
            throw new ArgumentException($"mismatch between number of times ({ts.Length}) and blackVols ({vols.Length})", nameof(blackVols));
        }

        var k = new double[ts.Length];
        for (int i = 0; i < ts.Length; i++)
        {
            k[i] = vols[i] / Value(ts[i]);
        }

        return ImmutableArray.Create(k);
    }

    /// <summary>The weighted root-mean-square error, √(n·Σwᵢeᵢ²/(n − 1)).</summary>
    /// <returns>The error.</returns>
    public double Error()
    {
        int n = times.Length;
        double squaredError = 0.0;
        for (int i = 0; i < times.Length; i++)
        {
            double error = Value(times[i]) - blackVols[i];
            squaredError += error * error * weights[i];
        }

        return Math.Sqrt(n * squaredError / (n - 1));
    }

    /// <summary>The largest absolute error.</summary>
    /// <returns>The error.</returns>
    public double MaxError()
    {
        double maxError = NumericLimits.QL_MIN_REAL;
        for (int i = 0; i < times.Length; i++)
        {
            double error = Math.Abs(Value(times[i]) - blackVols[i]);
            maxError = Math.Max(maxError, error);
        }

        return maxError;
    }

    /// <summary>The weighted errors, eᵢ·√wᵢ.</summary>
    /// <returns>The errors.</returns>
    public Vector Errors() => Residuals(times, blackVols, weights, A, B, C, D);

    private static Vector Residuals(
        ImmutableArray<double> times, ImmutableArray<double> blackVols, ImmutableArray<double> weights, double a, double b, double c, double d)
    {
        var results = new Vector(times.Length);
        for (int i = 0; i < times.Length; i++)
        {
            results[i] = (AbcdFunctions.AbcdBlackVolatility(times[i], a, b, c, d) - blackVols[i]) * Math.Sqrt(weights[i]);
        }

        return results;
    }

    // The abcd function's lowest value on [0, ∞): at its stationary point for b < 0 when it is positive, else at 0 or ∞.
    private static double LowestInstantaneousVolatility(double a, double b, double c, double d)
    {
        if (b < 0.0)
        {
            double ts = (1.0 / c) - (a / b);
            if (ts > 0.0)
            {
                return ((a + (b * ts)) * Math.Exp(-c * ts)) + d;
            }
        }

        return Math.Min(a + d, d);
    }

    // QuantLib's AbcdError, without the writes to the calibration: the residuals at the trial point, or the guard's.
    private sealed class AbcdError(
        ImmutableArray<double> times, ImmutableArray<double> blackVols, ImmutableArray<double> weights, ParametersTransformation transformation)
        : CostFunction
    {
        // The first trial point the abcd function's validation refuses: QuantLib's refusal, where its fit throws.
        public string? FirstRefusal { get; private set; }

        public override double Value(Vector x)
        {
            Vector r = Values(x);
            int n = times.Length;
            double squaredError = 0.0;
            for (int i = 0; i < r.Size; i++)
            {
                squaredError += r[i] * r[i];
            }

            // QuantLib's error(): its squared errors times the weights are the residuals' squares
            return Math.Sqrt(n * squaredError / (n - 1));
        }

        public override Vector Values(Vector x)
        {
            Vector y = transformation.Direct(x);
            if (AbcdMathFunction.Check(y[0], y[1], y[2], y[3]) is (string message, _))
            {
                FirstRefusal ??= message;
                var guarded = new Vector(times.Length);
                for (int i = 0; i < guarded.Size; i++)
                {
                    guarded[i] = GuardResidual;
                }

                return guarded;
            }

            return Residuals(times, blackVols, weights, y[0], y[1], y[2], y[3]);
        }
    }

    // QuantLib's transformation, which keeps c, d and a + d positive; with a fixed and d free, a at its fixed value and
    // d above max(0, -a), where QuantLib's would fix a + d
    private sealed class AbcdParametersTransformation(double? fixedA) : ParametersTransformation
    {
        // to constrained <- from unconstrained
        public override Vector Direct(Vector x)
        {
            var y = new Vector(4);
            y[1] = x[1];
            y[2] = Math.Exp(x[2]);
            if (fixedA is double a)
            {
                y[3] = Math.Exp(x[3]) + Math.Max(0.0, -a);
                y[0] = a;
                return y;
            }

            y[3] = Math.Exp(x[3]);
            y[0] = Math.Exp(x[0]) - y[3];
            return y;
        }

        // to unconstrained <- from constrained
        public override Vector Inverse(Vector x)
        {
            var y = new Vector(4);
            y[1] = x[1];
            y[2] = Math.Log(x[2]);
            y[3] = fixedA is double a ? Math.Log(x[3] - Math.Max(0.0, -a)) : Math.Log(x[3]);
            y[0] = Math.Log(x[0] + x[3]);
            return y;
        }
    }
}
