// SPDX-FileCopyrightText: Copyright (C) 2009 Andrea Odetti
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/mcbasket/longstaffschwartzmultipathpricer.hpp ql/experimental/mcbasket/longstaffschwartzmultipathpricer.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The Longstaff-Schwartz path pricer for path payoffs (Longstaff and Schwartz, 2001, "Valuing American Options by
/// Simulation: A Simple Least-Squares Approach", The Review of Financial Studies 14(1), 113-147): it stores the
/// calibration paths, regresses the continuation value on each fixing's state, then prices on the fitted decisions.
/// </summary>
/// <remarks>
/// A working type (the S-xeq plan's §2.0): QuantLib's mutable calibration phase, stored paths and coefficients, owned by
/// one engine call. At each fixing the regression's decision gives way to always or never exercising where either beats
/// it on the calibration paths, as QuantLib's. A calibration on no path is refused, where QuantLib reads the first of
/// none (XER5 (xxiii)).
/// </remarks>
[Experimental("NQL0001")]
public sealed class LongstaffSchwartzMultiPathPricer : PathPricer<MultiPath>
{
    private readonly PathPayoff payoff;
    private readonly Vector[] coeff;
    private readonly double[] lowerBounds;
    private readonly ImmutableArray<int> timePositions;
    private readonly ImmutableArray<YieldTermStructure> forwardTermStructures;
    private readonly Vector dF;
    private readonly List<PathInfo> paths = [];
    private readonly ImmutableArray<Func<Vector, double>> v;
    private bool calibrationPhase = true;

    /// <summary>Initializes a new instance of the <see cref="LongstaffSchwartzMultiPathPricer"/> class.</summary>
    /// <param name="payoff">The path payoff.</param>
    /// <param name="timePositions">The grid index of each fixing time.</param>
    /// <param name="forwardTermStructures">The yield curve at each fixing date.</param>
    /// <param name="discounts">The discount factor to each fixing time.</param>
    /// <param name="polynomialOrder">The basis polynomials' order.</param>
    /// <param name="polynomialType">The basis polynomials' family.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The family isn't monomial, Laguerre, Hermite, hyperbolic or second-kind Chebyshev ("insufficient polynomial
    /// type"), or the payoff's basis dimension isn't positive.
    /// </exception>
    public LongstaffSchwartzMultiPathPricer(
        PathPayoff payoff,
        IEnumerable<int> timePositions,
        ImmutableArray<YieldTermStructure> forwardTermStructures,
        Vector discounts,
        int polynomialOrder,
        LsmBasisSystemPolynomialType polynomialType)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(timePositions);
        ArgumentNullException.ThrowIfNull(discounts);
        this.payoff = payoff;
        this.timePositions = [.. timePositions];
        coeff = new Vector[this.timePositions.Length - 1];
        lowerBounds = new double[this.timePositions.Length];
        this.forwardTermStructures = forwardTermStructures;
        dF = discounts.Clone();
        v = LsmBasisSystem.MultiPathBasisSystem(payoff.BasisSystemDimension, polynomialOrder, polynomialType);
        if (!(polynomialType == LsmBasisSystemPolynomialType.Monomial
              || polynomialType == LsmBasisSystemPolynomialType.Laguerre
              || polynomialType == LsmBasisSystemPolynomialType.Hermite
              || polynomialType == LsmBasisSystemPolynomialType.Hyperbolic
              || polynomialType == LsmBasisSystemPolynomialType.Chebyshev2nd))
        {
            throw new NanoQLException("insufficient polynomial type");
        }
    }

    /// <summary>
    /// In the calibration phase, stores the path and returns 0; afterwards, the path's value on the fitted decisions.
    /// </summary>
    /// <param name="multiPath">The assets' paths.</param>
    /// <returns>The path's value, or 0 while calibrating.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="multiPath"/> is <see langword="null"/>.</exception>
    public override double Evaluate(MultiPath multiPath)
    {
        ArgumentNullException.ThrowIfNull(multiPath);
        PathInfo path = TransformPath(multiPath);
        if (calibrationPhase)
        {
            paths.Add(path);
            return 0.0;
        }

        int len = path.PathLength;
        double price = 0.0;
        {
            double finalPayoff = path.Payments[len - 1];
            double finalExercise = path.Exercises[len - 1];
            Vector finalStates = path.States[len - 1];
            bool canExercise = !finalStates.Empty;
            if (canExercise && finalExercise > 0.0)
            {
                price += finalExercise;
            }

            price += finalPayoff;
        }

        for (int i = len - 2; i >= 0; --i)
        {
            price *= dF[i + 1] / dF[i];

            double exercise = path.Exercises[i];

            // coeff[i].Size: 0 never exercise; v.Length use the estimated continuation value (if above lowerBounds[i]);
            // v.Length + 1 always exercise. In any case if states is empty, no exercise is allowed.
            Vector states = path.States[i];
            bool canExercise = !states.Empty;

            if (canExercise)
            {
                if (coeff[i].Size == v.Length + 1)
                {
                    price = exercise;
                }
                else
                {
                    if (!coeff[i].Empty && exercise > lowerBounds[i])
                    {
                        double continuationValue = 0.0;
                        for (int l = 0; l < v.Length; ++l)
                        {
                            continuationValue += coeff[i][l] * v[l](states);
                        }

                        if (continuationValue < exercise)
                        {
                            price = exercise;
                        }
                    }
                }
            }

            double payment = path.Payments[i];
            price += payment;
        }

        return price * dF[0];
    }

    /// <summary>Fits the exercise decisions on the stored paths, then leaves the calibration phase.</summary>
    /// <exception cref="NanoQLException">
    /// No path was stored (XER5 (xxiii)), or a state's size isn't the basis dimension ("Invalid size of basis system").
    /// </exception>
    public void Calibrate()
    {
        int n = paths.Count; // number of paths
        if (!(n > 0))
        {
            throw new NanoQLException("no calibration paths: the number of calibration samples must be positive");
        }

        var prices = new Vector(n, 0.0);
        var exercise = new Vector(n, 0.0);

        int basisDimension = payoff.BasisSystemDimension;
        int len = paths[0].PathLength;

        // We try to estimate the lower bound of the continuation value, so that only itm paths contribute to the
        // regression.
        for (int j = 0; j < n; ++j)
        {
            double finalPayoff = paths[j].Payments[len - 1];
            double finalExercise = paths[j].Exercises[len - 1];
            Vector finalStates = paths[j].States[len - 1];
            bool canExercise = !finalStates.Empty;
            if (canExercise && finalExercise > 0.0)
            {
                prices[j] += finalExercise;
            }

            prices[j] += finalPayoff;
        }

        lowerBounds[len - 1] = Min(prices);

        var lsExercise = new bool[n];

        for (int i = len - 2; i >= 0; --i)
        {
            var y = new List<double>();
            var x = new List<Vector>();

            double discountRatio = dF[i + 1] / dF[i];
            for (int j = 0; j < n; ++j)
            {
                prices[j] *= discountRatio;
            }

            lowerBounds[i + 1] *= discountRatio;

            for (int j = 0; j < n; ++j)
            {
                exercise[j] = paths[j].Exercises[i];

                Vector states = paths[j].States[i];
                if (!(states.Empty || states.Size == basisDimension))
                {
                    throw new NanoQLException("Invalid size of basis system");
                }

                if (!states.Empty && exercise[j] > lowerBounds[i + 1])
                {
                    x.Add(states);
                    y.Add(prices[j]);
                }
            }

            if (v.Length <= x.Count)
            {
                coeff[i] = GeneralLinearLeastSquares.Create(x, y, v).Coefficients();
            }
            else
            {
                // Not enough itm paths: default decision is NEVER
                coeff[i] = new Vector(0);
            }

            // attempt to avoid static arbitrage given by always or never exercising. always is absolute: regardless of
            // the lowerBoundContinuationValue_ (this could be changed) but it still honours "canExercise"
            double sumOptimized = 0.0;
            double sumNoExercise = 0.0;
            double sumAlwaysExercise = 0.0; // always, if allowed

            for (int j = 0, k = 0; j < n; ++j)
            {
                sumNoExercise += prices[j];
                lsExercise[j] = false;

                bool canExercise = !paths[j].States[i].Empty;
                if (canExercise)
                {
                    sumAlwaysExercise += exercise[j];
                    if (!coeff[i].Empty && exercise[j] > lowerBounds[i + 1])
                    {
                        double continuationValue = 0.0;
                        for (int l = 0; l < v.Length; ++l)
                        {
                            continuationValue += coeff[i][l] * v[l](x[k]);
                        }

                        if (continuationValue < exercise[j])
                        {
                            lsExercise[j] = true;
                        }

                        ++k;
                    }
                }
                else
                {
                    sumAlwaysExercise += prices[j];
                }

                sumOptimized += lsExercise[j] ? exercise[j] : prices[j];
            }

            sumOptimized /= n;
            sumNoExercise /= n;
            sumAlwaysExercise /= n;

            if (sumOptimized >= sumNoExercise && sumOptimized >= sumAlwaysExercise)
            {
                // Accepted LS decision
                for (int j = 0; j < n; ++j)
                {
                    prices[j] = lsExercise[j] ? exercise[j] : prices[j];
                }
            }
            else if (sumAlwaysExercise > sumNoExercise)
            {
                // Overridden bad LS decision: ALWAYS
                for (int j = 0; j < n; ++j)
                {
                    bool canExercise = !paths[j].States[i].Empty;
                    prices[j] = canExercise ? exercise[j] : prices[j];
                }

                coeff[i] = new Vector(v.Length + 1);
            }
            else
            {
                // Overridden bad LS decision: NEVER
                coeff[i] = new Vector(0);
            }

            for (int j = 0; j < n; ++j)
            {
                double payment = paths[j].Payments[i];
                prices[j] += payment;
            }

            lowerBounds[i] = Min(prices);
        }

        paths.Clear();
        calibrationPhase = false;
    }

    // std::min_element's value
    private static double Min(Vector values)
    {
        double min = values[0];
        for (int j = 1; j < values.Size; ++j)
        {
            if (values[j] < min)
            {
                min = values[j];
            }
        }

        return min;
    }

    // Extract the relevant information from the whole path
    private PathInfo TransformPath(MultiPath multiPath)
    {
        int numberOfAssets = multiPath.AssetNumber;
        int numberOfTimes = timePositions.Length;

        var path = new Matrix(numberOfAssets, numberOfTimes, double.NaN);
        for (int i = 0; i < numberOfTimes; ++i)
        {
            int pos = timePositions[i];
            for (int j = 0; j < numberOfAssets; ++j)
            {
                path[j, i] = multiPath[j][pos];
            }
        }

        var info = new PathInfo(numberOfTimes);
        payoff.Value(path, forwardTermStructures, info.Payments, info.Exercises, info.States);
        return info;
    }

    // One path's payments, exercise values and states at the fixing times
    private sealed class PathInfo
    {
        public PathInfo(int numberOfTimes)
        {
            Payments = new Vector(numberOfTimes, 0.0);
            Exercises = new Vector(numberOfTimes, 0.0);
            States = new Vector[numberOfTimes];
            for (int i = 0; i < numberOfTimes; ++i)
            {
                States[i] = new Vector(0);
            }
        }

        public Vector Payments { get; }

        public Vector Exercises { get; }

        public Vector[] States { get; }

        public int PathLength => States.Length;
    }
}
