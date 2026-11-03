// SPDX-FileCopyrightText: Copyright (C) 2006 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/montecarlo/longstaffschwartzpathpricer.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The Longstaff-Schwartz path pricer for early-exercise options (Francis Longstaff, Eduardo Schwartz, 2001, "Valuing
/// American Options by Simulation: A Simple Least-Squares Approach", The Review of Financial Studies, Volume 14, No. 1,
/// 113-147): in its calibration phase it stores the paths it is given and pricing nothing; <see cref="Calibrate"/>
/// regresses the continuation values backwards over them; in its pricing phase it prices each path, exercising where
/// the exercise value beats the regressed continuation.
/// </summary>
/// <typeparam name="TPath">The path type.</typeparam>
/// <typeparam name="TState">The early-exercise pricer's state type.</typeparam>
/// <remarks>
/// <para>
/// A working type and an extension point, as in QuantLib: a Longstaff-Schwartz engine builds one per calculation, its
/// stored paths, regression coefficients and exercise statistics the calculation's own, and
/// <see cref="PostProcessing"/> is QuantLib's protected hook. Each calibration path is copied, since a path generator's
/// path is a view of its buffer.
/// </para>
/// <para>
/// The pricer exercises only at the grid's exercise times, given beside the grid, and at its last time; QuantLib's
/// exercises at every time of the grid, so a Bermudan option on a grid with steps between its dates, or an American
/// option whose exercise window opens later, prices as an American option exercisable throughout: a documented
/// divergence (<c>Divergences/LongstaffSchwartzExerciseTimesTests.cs</c>). Where every time of the grid is an exercise
/// time, the bits are QuantLib's.
/// </para>
/// </remarks>
[WorkingType]
public class LongstaffSchwartzPathPricer<TPath, TState> : PathPricer<TPath>
{
    private readonly Vector[] coeff;
    private readonly double[] dF;
    private readonly bool[] exercisable;
    private readonly List<TPath> paths = [];
    private readonly ImmutableArray<Func<TState, double>> v;
    private readonly int len;
    private readonly IncrementalStatistics exerciseProbability = new();

    /// <summary>Initializes a new instance of the <see cref="LongstaffSchwartzPathPricer{TPath, TState}"/> class.</summary>
    /// <param name="times">The paths' time grid, whose times are the term structure's.</param>
    /// <param name="pathPricer">The early-exercise pricer.</param>
    /// <param name="termStructure">The discount curve.</param>
    /// <param name="exerciseTimes">
    /// The times of the grid, beside its last, at which the option can be exercised; each a time of the grid.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The grid has fewer than two times.</exception>
    /// <exception cref="NanoQLException">An exercise time isn't a time of the grid.</exception>
    public LongstaffSchwartzPathPricer(
        TimeGrid times,
        IEarlyExercisePathPricer<TPath, TState> pathPricer,
        YieldTermStructure termStructure,
        IReadOnlyList<double> exerciseTimes)
        : this(times, pathPricer, termStructure, termStructure, exerciseTimes)
    {
    }

    // The engines' form: the grid's times on a clock other than the curve's own, the curve's discounts read at the
    // instants they denote, nothing accrued before its reference date.
    internal LongstaffSchwartzPathPricer(
        TimeGrid times,
        IEarlyExercisePathPricer<TPath, TState> pathPricer,
        YieldTermStructure termStructure,
        TermStructure clock,
        IReadOnlyList<double> exerciseTimes)
    {
        ArgumentNullException.ThrowIfNull(times);
        ArgumentNullException.ThrowIfNull(pathPricer);
        ArgumentNullException.ThrowIfNull(termStructure);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(exerciseTimes);

        // QuantLib allocates times.size() - 2 coefficient arrays, wrapping below two times
        if (!(times.Size >= 2))
        {
            throw new ArgumentException($"at least two times required, {times.Size} given", nameof(times));
        }

        EarlyExercisePathPricer = pathPricer;
        coeff = new Vector[times.Size - 2];
        dF = new double[times.Size - 1];
        v = pathPricer.BasisSystem();
        len = times.Size;

        for (int i = 0; i < times.Size - 1; ++i)
        {
            dF[i] = termStructure.DiscountAt(clock, times[i + 1]) / termStructure.DiscountAt(clock, times[i]);
        }

        // the grid's exercise times; the last time always is one, as QuantLib's
        exercisable = new bool[times.Size];
        foreach (double t in exerciseTimes)
        {
            exercisable[times.Index(t)] = true;
        }

        exercisable[len - 1] = true;
    }

    /// <summary>Gets a value indicating whether the pricer is in its calibration phase, storing the paths it is given.</summary>
    protected bool CalibrationPhase { get; private set; } = true;

    /// <summary>Gets the early-exercise pricer.</summary>
    protected IEarlyExercisePathPricer<TPath, TState> EarlyExercisePathPricer { get; }

    /// <summary>
    /// Gets the regression's coefficients, one vector per time step from the grid's first after its start (none at
    /// expiry), set by <see cref="Calibrate"/>: QuantLib's protected <c>coeff_</c>.
    /// </summary>
    protected IReadOnlyList<Vector> Coefficients => coeff;

    /// <summary>
    /// In the calibration phase, stores a copy of the path and returns 0; in the pricing phase, the path's value: its
    /// exercise value at expiry rolled back, replaced at each earlier exercise time by the exercise value where that is
    /// positive and beats the regressed continuation, discounted to the grid's start.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The value; 0 in the calibration phase.</returns>
    public override double Evaluate(TPath path)
    {
        if (CalibrationPhase)
        {
            // store paths for the calibration
            paths.Add(Copy(path));

            // result doesn't matter
            return 0.0;
        }

        double price = EarlyExercisePathPricer.Evaluate(path, len - 1);

        // Initialize with exercise on last date
        bool exercised = price > 0.0;

        for (int i = len - 2; i > 0; --i)
        {
            price *= dF[i];

            if (!exercisable[i])
            {
                continue;
            }

            double exercise = EarlyExercisePathPricer.Evaluate(path, i);
            if (exercise > 0.0)
            {
                TState regValue = EarlyExercisePathPricer.State(path, i);

                double continuationValue = 0.0;
                for (int l = 0; l < v.Length; ++l)
                {
                    continuationValue += coeff[i - 1][l] * v[l](regValue);
                }

                if (continuationValue < exercise)
                {
                    price = exercise;

                    // Exercised
                    exercised = true;
                }
            }
        }

        exerciseProbability.Add(exercised ? 1.0 : 0.0);

        return price * dF[0];
    }

    /// <summary>
    /// Regresses the continuation values over the stored paths, backwards from expiry, and enters the pricing phase,
    /// releasing the paths.
    /// </summary>
    /// <exception cref="NanoQLException">A regression's decomposition fails.</exception>
    public virtual void Calibrate()
    {
        int n = paths.Count;
        double[] prices = new double[n], exercise = new double[n];
        var p_state = new TState[n];
        double[] p_price = new double[n], p_exercise = new double[n];

        for (int i = 0; i < n; ++i)
        {
            p_state[i] = EarlyExercisePathPricer.State(paths[i], len - 1);
            prices[i] = p_price[i] = EarlyExercisePathPricer.Evaluate(paths[i], len - 1);
            p_exercise[i] = prices[i];
        }

        PostProcessing(len - 1, p_state, p_price, p_exercise);

        var y = new List<double>();
        var x = new List<TState>();
        for (int i = len - 2; i > 0; --i)
        {
            y.Clear();
            x.Clear();

            //roll back step
            for (int j = 0; j < n; ++j)
            {
                // no exercise off the exercise times
                exercise[j] = exercisable[i] ? EarlyExercisePathPricer.Evaluate(paths[j], i) : 0.0;
                if (exercise[j] > 0.0)
                {
                    x.Add(EarlyExercisePathPricer.State(paths[j], i));
                    y.Add(dF[i] * prices[j]);
                }
            }

            if (v.Length <= x.Count)
            {
                coeff[i - 1] = GeneralLinearLeastSquares.Create(x, y, v).Coefficients();
            }
            else
            {
                // if number of itm paths is smaller then the number of
                // calibration functions then early exercise if exerciseValue > 0
                coeff[i - 1] = new Vector(v.Length, 0.0);
            }

            for (int j = 0, k = 0; j < n; ++j)
            {
                prices[j] *= dF[i];
                if (exercise[j] > 0.0)
                {
                    double continuationValue = 0.0;
                    for (int l = 0; l < v.Length; ++l)
                    {
                        continuationValue += coeff[i - 1][l] * v[l](x[k]);
                    }

                    if (continuationValue < exercise[j])
                    {
                        prices[j] = exercise[j];
                    }

                    ++k;
                }

                p_state[j] = EarlyExercisePathPricer.State(paths[j], i);
                p_price[j] = prices[j];
                p_exercise[j] = exercise[j];
            }

            PostProcessing(i, p_state, p_price, p_exercise);
        }

        // remove calibration paths and release memory
        paths.Clear();
        paths.TrimExcess();

        // entering the calculation phase
        CalibrationPhase = false;
    }

    /// <summary>The fraction of the priced paths on which the option was exercised.</summary>
    /// <returns>The exercise probability.</returns>
    /// <exception cref="NanoQLException">No path has been priced.</exception>
    public double ExerciseProbability() => exerciseProbability.Mean();

    /// <summary>
    /// A hook called at each time step of the calibration, expiry first, with every path's state, value and exercise
    /// value there; by default it does nothing.
    /// </summary>
    /// <param name="i">The time step's index.</param>
    /// <param name="state">The paths' states.</param>
    /// <param name="price">The paths' values at the step.</param>
    /// <param name="exercise">The paths' exercise values at the step, 0 off the exercise times.</param>
    protected virtual void PostProcessing(
        int i, IReadOnlyList<TState> state, IReadOnlyList<double> price, IReadOnlyList<double> exercise)
    {
    }

    // A copy of a generator's path, which is a view of its buffer: QuantLib's push_back copies it.
    private static TPath Copy(TPath path) => path switch
    {
        MonteCarloPath single => (TPath)(object)new MonteCarloPath(single.TimeGrid, [.. single]),
        MultiPath multi => (TPath)(object)new MultiPath([.. Enumerable.Range(0, multi.AssetNumber).Select(j => multi[j])]),
        null => throw new ArgumentNullException(nameof(path)),
        _ => throw new NanoQLException($"no copy of a path of type {path.GetType().Name}"),
    };
}
