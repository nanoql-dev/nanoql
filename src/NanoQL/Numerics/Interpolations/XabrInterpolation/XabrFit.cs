// SPDX-FileCopyrightText: Copyright (C) 2006 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2006 Mario Pucci
// SPDX-FileCopyrightText: Copyright (C) 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/xabrinterpolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.TermStructures;

namespace NanoQL.Numerics;

/// <summary>
/// QuantLib's <c>XABRCoeffHolder</c> and the fit of <c>XABRInterpolationImpl::update</c>: a model fitted to a smile's
/// volatilities, its parameters fitted when it is built.
/// </summary>
/// <typeparam name="TSpecs">The model's specification.</typeparam>
/// <typeparam name="TInstance">The model instance it builds.</typeparam>
/// <remarks>
/// <para>
/// The constructor runs QuantLib's construction followed by one <c>update()</c>, over a working state: the cost
/// function writes the fit's parameter array and rebuilds the model instance at every evaluation, as QuantLib's
/// writes the coefficient holder, so every value the fit computes is QuantLib's. The restarts draw their guesses from
/// <see cref="HaltonRsg"/> with QuantLib's seed, 42, built inside the fit. After the restarts the best parameters are
/// written back and the instance is rebuilt from them; after construction nothing writes the array, so the fit is
/// immutable and thread-safe.
/// </para>
/// <para>
/// Two differences from QuantLib, each a fix of its defect. QuantLib doesn't rebuild the instance after writing the
/// best parameters back, so a model whose instance copies them (ZABR's, SVI's) prices with its last trial's when the
/// restarts run out, while reporting the best's; here every instance prices with the best parameters. And a smile of
/// zero volatilities fitted with vega weights is refused, where QuantLib divides by their zero sum and fits on NaN
/// weights. QuantLib's choice of the best restart is kept: it compares the error the cost function's last evaluation
/// gives, which under <see cref="Simplex"/> is a later trial's than the result's.
/// </para>
/// </remarks>
internal sealed class XabrFit<TSpecs, TInstance>
    where TSpecs : IXabrSpecs<TInstance>
    where TInstance : IXabrModelInstance
{
    private readonly double[] x;
    private readonly double[] y;
    private readonly double[] parameters;
    private readonly bool[] paramIsFixed;
    private readonly double[] addParams;
    private readonly VolatilityType volatilityType;
    private readonly OptimizationMethod optMethod;
    private readonly EndCriteria endCriteria;
    private readonly double errorAccept;
    private readonly bool useMaxError;
    private readonly int maxGuesses;
    private readonly bool vegaWeighted;
    private double[] weights;
    private TInstance modelInstance;

    /// <summary>Initializes a new instance of the <see cref="XabrFit{TSpecs, TInstance}"/> class, and fits it.</summary>
    /// <param name="x">The strikes, as the interpolation stores them.</param>
    /// <param name="y">The volatilities, as the interpolation stores them.</param>
    /// <param name="t">The time to expiry.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="parameters">The parameters' guesses, <see langword="null"/> where not given.</param>
    /// <param name="paramIsFixed">Whether each parameter is fixed; a parameter not given is free.</param>
    /// <param name="vegaWeighted">Whether the errors are vega-weighted.</param>
    /// <param name="endCriteria">The end criteria, or <see langword="null"/> for QuantLib's default.</param>
    /// <param name="optMethod">The optimizer, or <see langword="null"/> for QuantLib's default.</param>
    /// <param name="errorAccept">The error at or below which a restart is accepted.</param>
    /// <param name="useMaxError">Whether a restart is scored by its maximum error, not its root mean square.</param>
    /// <param name="maxGuesses">The most minimizations, at least one.</param>
    /// <param name="addParams">The model's additional parameters.</param>
    /// <param name="volatilityType">The volatility type the values are.</param>
    internal XabrFit(
        ReadOnlySpan<double> x,
        ReadOnlySpan<double> y,
        double t,
        double forward,
        double?[] parameters,
        bool[] paramIsFixed,
        bool vegaWeighted,
        EndCriteria? endCriteria,
        OptimizationMethod? optMethod,
        double errorAccept,
        bool useMaxError,
        int maxGuesses,
        double[] addParams,
        VolatilityType volatilityType)
    {
        this.x = [.. x];
        this.y = [.. y];
        T = t;
        Forward = forward;
        this.addParams = addParams;
        this.volatilityType = volatilityType;

        // XABRCoeffHolder's constructor.
        if (!(t > 0.0))
        {
            throw new ArgumentException($"expiry time must be positive: {Show(t)} not allowed", nameof(t));
        }

        if (!(parameters.Length == TSpecs.Dimension))
        {
            throw new ArgumentException(
                $"wrong number of parameters ({parameters.Length}), should be {TSpecs.Dimension}", nameof(parameters));
        }

        if (!(paramIsFixed.Length == TSpecs.Dimension))
        {
            throw new ArgumentException(
                $"wrong number of fixed parameters flags ({paramIsFixed.Length}), should be {TSpecs.Dimension}",
                nameof(paramIsFixed));
        }

        this.paramIsFixed = new bool[paramIsFixed.Length];
        for (int i = 0; i < parameters.Length; ++i)
        {
            if (parameters[i] is not null)
            {
                this.paramIsFixed[i] = paramIsFixed[i];
            }
        }

        double?[] given = [.. parameters];
        TSpecs.DefaultValues(given, this.paramIsFixed, Forward, T, addParams);
        this.parameters = new double[given.Length];
        for (int i = 0; i < given.Length; ++i)
        {
            this.parameters[i] = given[i] ?? throw new InvalidOperationException("a parameter left without a default");
        }

        modelInstance = TSpecs.Instance(T, Forward, this.parameters, addParams);

        // XABRInterpolationImpl's constructor: if no optimization method or end criteria is provided, we provide one.
        this.optMethod = optMethod ?? new LevenbergMarquardt(1e-8, 1e-8, 1e-8);
        this.endCriteria = endCriteria ?? new EndCriteria(60000, 100, 1e-8, 1e-8, 1e-8);
        this.errorAccept = errorAccept;
        this.useMaxError = useMaxError;
        this.maxGuesses = maxGuesses;
        this.vegaWeighted = vegaWeighted;
        weights = new double[this.x.Length];
        Array.Fill(weights, 1.0 / this.x.Length);

        Update();
    }

    /// <summary>Gets the time to expiry.</summary>
    internal double T { get; }

    /// <summary>Gets the forward.</summary>
    internal double Forward { get; }

    /// <summary>Gets the root-mean-square error of the fit.</summary>
    internal double Error { get; private set; }

    /// <summary>Gets the maximum error of the fit.</summary>
    internal double MaxError { get; private set; }

    /// <summary>Gets the end criteria of the restart kept: <see cref="EndCriteriaType.None"/> where none was.</summary>
    internal EndCriteriaType XabrEndCriteria { get; private set; } = EndCriteriaType.None;

    /// <summary>Gets the strikes' weights, as normalized.</summary>
    internal ImmutableArray<double> Weights => [.. weights];

    /// <summary>A fitted parameter.</summary>
    /// <param name="i">The parameter's index, in the model's order.</param>
    /// <returns>The parameter.</returns>
    internal double Parameter(int i) => parameters[i];

    /// <summary>The model's volatility at a strike: QuantLib's <c>value(x)</c>.</summary>
    /// <param name="strike">The strike.</param>
    /// <returns>The volatility.</returns>
    internal double Value(double strike) => modelInstance.Volatility(strike, volatilityType);

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);

    private void UpdateModelInstance() => modelInstance = TSpecs.Instance(T, Forward, parameters, addParams);

    // calculate total squared weighted difference (L2 norm)
    private double InterpolationSquaredError()
    {
        double error, totalError = 0.0;
        for (int i = 0; i < x.Length; ++i)
        {
            error = Value(x[i]) - y[i];
            totalError += error * error * weights[i];
        }

        return totalError;
    }

    // calculate weighted differences
    private Vector InterpolationErrors()
    {
        var results = new Vector(x.Length);
        for (int i = 0; i < x.Length; ++i)
        {
            results[i] = (Value(x[i]) - y[i]) * Math.Sqrt(weights[i]);
        }

        return results;
    }

    private double InterpolationError()
    {
        int n = x.Length;
        double squaredError = InterpolationSquaredError();
        return Math.Sqrt(n * squaredError / (n == 1 ? 1 : (n - 1)));
    }

    private double InterpolationMaxError()
    {
        double error, maxError = NumericLimits.QL_MIN_REAL;
        for (int i = 0; i < x.Length; ++i)
        {
            error = Math.Abs(Value(x[i]) - y[i]);
            maxError = maxError < error ? error : maxError;
        }

        return maxError;
    }

    // XABRInterpolationImpl::update.
    private void Update()
    {
        UpdateModelInstance();

        // we must update weights if it is vegaWeighted
        if (vegaWeighted)
        {
            double weightsSum = 0.0;
            for (int i = 0; i < x.Length; ++i)
            {
                double stdDev = Math.Sqrt(y[i] * y[i] * T);
                weights[i] = TSpecs.Weight(x[i], Forward, stdDev, addParams);
                weightsSum += weights[i];
            }

            // QuantLib divides by a zero sum and fits on NaN weights.
            if (weightsSum == 0.0)
            {
                throw new ArgumentException("vega weights are all zero", nameof(y));
            }

            // weight normalization
            for (int i = 0; i < weights.Length; ++i)
            {
                weights[i] /= weightsSum;
            }
        }

        // there is nothing to optimize
        if (Array.TrueForAll(paramIsFixed, fixedParameter => fixedParameter))
        {
            Error = InterpolationError();
            MaxError = InterpolationMaxError();
            XabrEndCriteria = EndCriteriaType.None;
            return;
        }

        var costFunction = new XabrError(this);
        var guess = new Vector(TSpecs.Dimension);
        for (int i = 0; i < guess.Size; ++i)
        {
            guess[i] = parameters[i];
        }

        int iterations = 0;
        int freeParameters = 0;
        double bestError = NumericLimits.QL_MAX_REAL;
        Vector bestParameters = new(0);
        for (int i = 0; i < TSpecs.Dimension; ++i)
        {
            if (!paramIsFixed[i])
            {
                ++freeParameters;
            }
        }

        var halton = new HaltonRsg(freeParameters, 42);
        EndCriteriaType tmpEndCriteria;
        double tmpInterpolationError;
        do
        {
            if (iterations > 0)
            {
                ReadOnlySpan<double> s = halton.NextSequence().Value.Span;
                TSpecs.Guess(guess, paramIsFixed, Forward, T, s, addParams);
                for (int i = 0; i < paramIsFixed.Length; ++i)
                {
                    if (paramIsFixed[i])
                    {
                        guess[i] = parameters[i];
                    }
                }
            }

            Vector inversedTransformatedGuess = TSpecs.Inverse(guess, paramIsFixed, parameters, Forward);
            var constrainedXabrError = new ProjectedCostFunction(costFunction, inversedTransformatedGuess.AsSpan(), paramIsFixed);
            Vector projectedGuess = constrainedXabrError.Project(inversedTransformatedGuess);

            var constraint = new NoConstraint();
            var problem = new ProblemState(new Problem(constrainedXabrError, constraint, projectedGuess.AsSpan()));
            tmpEndCriteria = optMethod.Minimize(problem, endCriteria);
            Vector projectedResult = problem.CurrentValue.Clone();
            Vector transfResult = constrainedXabrError.Include(projectedResult);

            Vector result = TSpecs.Direct(transfResult, paramIsFixed, parameters, Forward);

            // The error the cost function's last evaluation left, QuantLib's: under Simplex a later trial's.
            tmpInterpolationError = useMaxError ? InterpolationMaxError() : InterpolationError();

            if (tmpInterpolationError < bestError)
            {
                bestError = tmpInterpolationError;
                bestParameters = result;
                XabrEndCriteria = tmpEndCriteria;
            }
        }
        while (++iterations < maxGuesses && tmpInterpolationError > errorAccept);

        for (int i = 0; i < bestParameters.Size; ++i)
        {
            parameters[i] = bestParameters[i];
        }

        // QuantLib doesn't rebuild the instance here, so an instance that copies its parameters keeps the last trial's.
        UpdateModelInstance();

        Error = InterpolationError();
        MaxError = InterpolationMaxError();
    }

    // XABRError: every evaluation writes the fit's parameters, fixed ones included, and rebuilds its instance.
    private sealed class XabrError(XabrFit<TSpecs, TInstance> xabr) : CostFunction
    {
        public override double Value(Vector x)
        {
            Vector y = TSpecs.Direct(x, xabr.paramIsFixed, xabr.parameters, xabr.Forward);
            for (int i = 0; i < xabr.parameters.Length; ++i)
            {
                xabr.parameters[i] = y[i];
            }

            xabr.UpdateModelInstance();
            return xabr.InterpolationSquaredError();
        }

        public override Vector Values(Vector x)
        {
            Vector y = TSpecs.Direct(x, xabr.paramIsFixed, xabr.parameters, xabr.Forward);
            for (int i = 0; i < xabr.parameters.Length; ++i)
            {
                xabr.parameters[i] = y[i];
            }

            xabr.UpdateModelInstance();
            return xabr.InterpolationErrors();
        }
    }
}
