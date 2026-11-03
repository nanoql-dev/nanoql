// SPDX-FileCopyrightText: Copyright (C) 2007 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2009 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/yield/fittedbonddiscountcurve.hpp ql/termstructures/yield/fittedbonddiscountcurve.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The base fitting method of a <see cref="FittedBondDiscountCurve"/>: the methodology that constructs the curve, a
/// discount function of parameters fitted to the bonds' prices.
/// </summary>
/// <remarks>
/// <para>
/// A derived method defines <see cref="DiscountFunction"/> and <see cref="Size"/>, the number of parameters fitted.
/// The generic fit is nonlinear, in contrast to the typically faster linear fitting methods. The bonds' price errors
/// are weighted by the given weights, or by their inverse modified durations, normalized; an L2 penalty, if given,
/// applies a Gaussian prior to each parameter around the initial guess. Before the minimum cut-off time and after the
/// maximum one the discount extrapolates with a flat instantaneous forward rate. The parameters of a method may need
/// tuning for a proper, reasonable or fast convergence, depending on the method.
/// </para>
/// <para>
/// QuantLib's public nested class <c>FittedBondDiscountCurve::FittingMethod</c>. A curve fits a copy of its method,
/// made as QuantLib's <c>clone()</c> makes it, and its <see cref="FittedBondDiscountCurve.FitResults"/> is that copy,
/// holding the fit's solution, iterations, cost, end criterion and the weights it used; the method given stays as it
/// was built. So a derived method needs no clone of its own. An extension point, as in QuantLib.
/// </para>
/// </remarks>
public abstract class FittedBondDiscountCurveFittingMethod
{
    private readonly bool calculateWeights;
    private readonly double minCutoffTime;
    private readonly double maxCutoffTime;

    // the fit's results, set on the curve's copy while the curve is built
    private ImmutableArray<double> weights;
    private ImmutableArray<double> solution = [];
    private int numberOfIterations;
    private double? costValue;
    private EndCriteriaType errorCode = EndCriteriaType.None;

    /// <summary>Initializes a new instance of the <see cref="FittedBondDiscountCurveFittingMethod"/> class.</summary>
    /// <param name="constrainAtZero">Whether the discount function is constrained to 1 at t = 0.</param>
    /// <param name="weights">The bonds' weights, one per bond, or <see langword="null"/> or empty for their inverse durations.</param>
    /// <param name="optimizationMethod">The optimization method, or <see langword="null"/> for a <see cref="Simplex"/>.</param>
    /// <param name="l2">The L2 penalties, one per parameter, or <see langword="null"/> or empty for none.</param>
    /// <param name="minCutoffTime">The time before which the discount extrapolates with a flat forward rate.</param>
    /// <param name="maxCutoffTime">The time after which the discount extrapolates with a flat forward rate.</param>
    /// <param name="constraint">The optimization's constraint, or <see langword="null"/> for none.</param>
    protected FittedBondDiscountCurveFittingMethod(
        bool constrainAtZero = true,
        IEnumerable<double>? weights = null,
        OptimizationMethod? optimizationMethod = null,
        IEnumerable<double>? l2 = null,
        double minCutoffTime = 0.0,
        double maxCutoffTime = double.MaxValue,
        Constraint? constraint = null)
    {
        ConstrainAtZero = constrainAtZero;
        this.weights = weights is null ? [] : [.. weights];
        L2 = l2 is null ? [] : [.. l2];
        calculateWeights = this.weights.IsEmpty;
        OptimizationMethod = optimizationMethod;
        Constraint = constraint ?? new NoConstraint();
        this.minCutoffTime = minCutoffTime;
        this.maxCutoffTime = maxCutoffTime;
    }

    /// <summary>Gets the total number of coefficients to fit, or solve for.</summary>
    public abstract int Size { get; }

    /// <summary>Gets the solution of the optimization problem: the fitted parameters, empty before a fit.</summary>
    public ImmutableArray<double> Solution => solution;

    /// <summary>Gets the final number of iterations used in the optimization problem, its cost function's evaluations.</summary>
    public int NumberOfIterations => numberOfIterations;

    /// <summary>
    /// Gets the final value of the cost function after the optimization; <see langword="null"/> (QuantLib's
    /// <c>Null&lt;Real&gt;()</c>) for a curve built from given parameters.
    /// </summary>
    public double? MinimumCostValue => costValue;

    /// <summary>Gets the end criterion of the optimization.</summary>
    public EndCriteriaType ErrorCode => errorCode;

    /// <summary>Gets a value indicating whether there is a constraint at zero, the discount function 1 at t = 0.</summary>
    public bool ConstrainAtZero { get; }

    /// <summary>Gets the weights being used: the given ones, or after a fit the normalized inverse durations.</summary>
    public ImmutableArray<double> Weights => weights;

    /// <summary>Gets the L2 penalties being used, empty for none.</summary>
    public ImmutableArray<double> L2 { get; }

    /// <summary>Gets the optimization method being used; <see langword="null"/> for a <see cref="Simplex"/> on the curve's lambda.</summary>
    public OptimizationMethod? OptimizationMethod { get; }

    /// <summary>Gets the optimization's constraint.</summary>
    public Constraint Constraint { get; }

    /// <summary>
    /// Gets the curve the method is fitting, on the curve's copy of the method; <see langword="null"/> on a method not
    /// yet given to a curve. QuantLib's <c>curve_</c>.
    /// </summary>
    protected FittedBondDiscountCurve? Curve { get; private set; }

    /// <summary>
    /// The discount at a time for given parameters: <see cref="DiscountFunction"/> within the cut-off times, and a flat
    /// instantaneous forward rate before the minimum and after the maximum.
    /// </summary>
    /// <param name="x">The parameters.</param>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> holds fewer than <see cref="Size"/> parameters, where QuantLib reads past its end.
    /// </exception>
    public double Discount(ReadOnlySpan<double> x, double t)
    {
        if (!(x.Length >= Size))
        {
            throw new ArgumentException($"{x.Length} parameters given for a method of {Size}", nameof(x));
        }

        if (t < minCutoffTime)
        {
            // flat fwd extrapolation before min cutoff time
            return Math.Exp(Math.Log(DiscountFunction(x, minCutoffTime)) / minCutoffTime * t);
        }
        else if (t > maxCutoffTime)
        {
            // flat fwd extrapolation after max cutoff time
            return DiscountFunction(x, maxCutoffTime)
                * Math.Exp((Math.Log(DiscountFunction(x, maxCutoffTime + 1E-4))
                            - Math.Log(DiscountFunction(x, maxCutoffTime))) * 1E4 * (t - maxCutoffTime));
        }
        else
        {
            return DiscountFunction(x, t);
        }
    }

    // The curve's copy of the method, QuantLib's clone() with curve_ set: the fit's results go on it.
    internal FittedBondDiscountCurveFittingMethod CopyFor(FittedBondDiscountCurve curve)
    {
        var copy = (FittedBondDiscountCurveFittingMethod)MemberwiseClone();
        copy.Curve = curve;
        return copy;
    }

    // The parameters the curve's copy reads while the curve is built: each trial point of the fit, then its solution.
    internal void SetSolution(ImmutableArray<double> x) => solution = x;

    /// <summary>The discount function the curve is fitted to, at given parameters.</summary>
    /// <param name="x">The parameters, <see cref="Size"/> of them.</param>
    /// <param name="t">The time.</param>
    /// <returns>The discount factor.</returns>
    protected abstract double DiscountFunction(ReadOnlySpan<double> x, double t);

    /// <summary>
    /// Sets the fit up on the curve's copy before it runs: the yield conventions and the bonds' weights, and the checks
    /// of the weights, the penalties and the guess. QuantLib's <c>init()</c>; a method that overrides it calls it.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The weights don't cover every bond, the penalties don't cover every parameter, or penalties are given without a
    /// guess.
    /// </exception>
    protected virtual void Init()
    {
        FittedBondDiscountCurve curve = Curve!;
        if (curve.MaxEvaluations == 0)
        {
            return; // we can skip the rest
        }

        // yield conventions
        DayCounter yieldDC = curve.DayCounter;
        Compounding yieldComp = Compounding.Compounded;
        Frequency yieldFreq = Frequency.Annual;

        ImmutableArray<BondHelper> helpers = curve.FitHelpers;
        EvaluationContext context = curve.FitContext!;
        int n = helpers.Length;
        if (calculateWeights)
        {
            var w = new double[n];
            double squaredSum = 0.0;
            for (int i = 0; i < n; ++i)
            {
                Bond bond = helpers[i].Bond;
                double amount = helpers[i].Quote;
                var price = new BondPrice(amount, helpers[i].PriceType);
                Date bondSettlement = bond.SettlementDate(context);
                double ytm = BondFunctions.Yield(bond, price, yieldDC, yieldComp, yieldFreq, context, bondSettlement);
                double dur = BondFunctions.Duration(
                    bond, ytm, yieldDC, yieldComp, yieldFreq, context, DurationType.Modified, bondSettlement);
                w[i] = 1.0 / dur;
                squaredSum += w[i] * w[i];
            }

            double norm = Math.Sqrt(squaredSum);
            for (int i = 0; i < n; ++i)
            {
                w[i] /= norm;
            }

            weights = [.. w];
        }

        if (!(weights.Length == n))
        {
            throw new ArgumentException("Given weights do not cover all boostrapping helpers", "fittingMethod");
        }

        if (!L2.IsEmpty)
        {
            if (!(L2.Length == Size))
            {
                throw new ArgumentException("Given penalty factors do not cover all parameters", "fittingMethod");
            }

            if (curve.Guess.IsEmpty)
            {
                throw new ArgumentException("L2 penalty requires a guess", "guess");
            }
        }
    }

    // QuantLib's calculate(): the curve's given parameters, or the fit from the guess.
    internal void Calculate()
    {
        FittedBondDiscountCurve curve = Curve!;
        ImmutableArray<double> guess = curve.Guess;
        if (curve.MaxEvaluations == 0)
        {
            // Don't calculate, simply use the given parameters to provide a fitted curve. This turns the instance into
            // an evaluator of the parametric curve, for example allowing to use the parameters for a credit spread curve
            // calculated with bonds in one currency to be coupled to a discount curve in another currency.
            if (!(guess.Length == Size))
            {
                throw new ArgumentException("wrong number of parameters", "parameters");
            }

            solution = guess;
            numberOfIterations = 0;
            costValue = null;
            errorCode = EndCriteriaType.None;
            return;
        }

        var costFunction = new FittingCost(this, curve);

        // start with the guess solution, if it exists
        var x = new double[Size];
        if (!guess.IsEmpty)
        {
            if (!(guess.Length == Size))
            {
                throw new ArgumentException("wrong size for guess", "guess");
            }

            guess.CopyTo(x);
        }

        // workaround for backwards compatibility
        OptimizationMethod optimization = OptimizationMethod ?? new Simplex(curve.SimplexLambda);
        var problem = new Problem(costFunction, Constraint, x);

        double rootEpsilon = curve.Accuracy;
        double functionEpsilon = curve.Accuracy;
        double gradientNormEpsilon = curve.Accuracy;
        var endCriteria = new EndCriteria(
            curve.MaxEvaluations, curve.MaxStationaryStateIterations, rootEpsilon, functionEpsilon, gradientNormEpsilon);

        ProblemResults results = optimization.Minimize(problem, endCriteria);
        errorCode = results.EndCriteriaType;
        solution = results.CurrentValue;
        numberOfIterations = results.FunctionEvaluation;
        costValue = results.FunctionValue;
    }

    // Init, for the curve.
    internal void InitFit() => Init();

    // The fit's cost: the squared weighted price errors of the bonds, priced on the curve at the trial parameters, and
    // the L2 penalties, summed.
    private sealed class FittingCost(FittedBondDiscountCurveFittingMethod fittingMethod, FittedBondDiscountCurve curve)
        : CostFunction
    {
        public override double Value(Vector x)
        {
            double squaredError = 0.0;
            Vector vals = Values(x);
            foreach (double val in vals)
            {
                squaredError += val;
            }

            return squaredError;
        }

        public override Vector Values(Vector x)
        {
            ImmutableArray<BondHelper> helpers = curve.FitHelpers;
            EvaluationContext context = curve.FitContext!;
            int n = helpers.Length;
            ImmutableArray<double> l2 = fittingMethod.L2;
            int N = l2.Length;

            // set solution so that the curve represents the current trial; the final solution will be set in
            // Calculate later on
            fittingMethod.SetSolution([.. x.AsSpan()]);
            var values = new Vector(n + N);
            for (int i = 0; i < n; ++i)
            {
                double weightedError = fittingMethod.weights[i] * helpers[i].QuoteError(context);
                values[i] = weightedError * weightedError;
            }

            if (N != 0)
            {
                ImmutableArray<double> guess = curve.Guess;
                for (int i = 0; i < N; ++i)
                {
                    double error = x[i] - guess[i];
                    values[i + n] = l2[i] * error * error;
                }
            }

            return values;
        }
    }
}
