// SPDX-FileCopyrightText: Copyright (C) 2008, 2011, 2015 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2007 Chris Kenyon
// SPDX-FileCopyrightText: Copyright (C) 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Paolo Mazzocchi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/iterativebootstrap.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Time;
using NanoQL.Utilities;

namespace NanoQL.TermStructures;

/// <summary>
/// The universal piecewise-term-structure bootstrapper: each node is solved in turn so that the helper whose pillar it
/// is reprices its quote, and the passes repeat until the nodes stop moving when the interpolation is global or a
/// pillar comes before its helper's latest relevant date.
/// </summary>
/// <remarks>
/// <para>
/// A configuration struct (<see cref="IBootstrap"/>): its zero value is QuantLib's default construction, every field
/// stored so that zero reads as QuantLib's default. It reproduces QuantLib's sequence: the helpers sorted by pillar,
/// the expired ones skipped, Brent's solver on the first pass and <see cref="FiniteDifferenceNewtonSafe"/> once the
/// data are valid, the retries with widened brackets, the fallback without throwing, the convergence loop, and the
/// node keeping the last value the solver evaluated, since QuantLib ignores the solver's return value.
/// </para>
/// <para>
/// On its first pass QuantLib builds the interpolation over the nodes solved so far before each node's values are
/// known, and takes linear interpolation instead where a global interpolation refuses to be built over that many
/// points. The interpolation here is built with its values in one call, so the fallback is decided by the
/// interpolator's own construction checks, <see cref="IInterpolator.CheckPoints"/>; every other refusal of the
/// interpolation is met when the solver builds it, as QuantLib meets it when the interpolation is updated. The
/// interpolator chosen on the first pass's last node serves the later passes and the built curve.
/// </para>
/// <para>
/// A curve is always built afresh, so its bootstrap starts from the traits' first guess: QuantLib re-bootstraps a curve
/// whose quotes changed from its previous state, which the two reach within the bootstrap's accuracy (a documented
/// divergence).
/// </para>
/// </remarks>
public readonly struct IterativeBootstrap : IBootstrap
{
    // Stored so that zero reads as QuantLib's defaults: null bounds and accuracy are QuantLib's Null<Real>(), and the
    // counts and factors are offsets from their defaults.
    private readonly double? accuracy;
    private readonly double? minValue;
    private readonly double? maxValue;
    private readonly int extraAttempts;
    private readonly double maxFactorExcess;
    private readonly double minFactorExcess;
    private readonly bool dontThrow;
    private readonly int dontThrowStepsOffset;
    private readonly int maxEvaluationsOffset;

    /// <summary>Initializes a new instance of the <see cref="IterativeBootstrap"/> struct.</summary>
    /// <param name="accuracy">
    /// The accuracy of the bootstrap's stopping criterion; <see langword="null"/> takes the curve's.
    /// </param>
    /// <param name="minValue">The initial lower bound of each node's search; <see langword="null"/> takes the traits'.</param>
    /// <param name="maxValue">The initial upper bound of each node's search; <see langword="null"/> takes the traits'.</param>
    /// <param name="maxAttempts">The attempts on each node in each pass: more than 1 implies retries.</param>
    /// <param name="maxFactor">The factor widening the upper bound on each retry, at least 1.</param>
    /// <param name="minFactor">The factor widening the lower bound on each retry, at least 1.</param>
    /// <param name="dontThrow">
    /// Whether a node the solver fails on takes the value of least error on a grid between its bounds instead of
    /// throwing, and a pass limit reached keeps the curve as it is.
    /// </param>
    /// <param name="dontThrowSteps">The grid's steps when <paramref name="dontThrow"/> is set.</param>
    /// <param name="maxEvaluations">The solvers' maximum number of function evaluations.</param>
    /// <exception cref="ArgumentException"><paramref name="maxFactor"/> or <paramref name="minFactor"/> is below 1.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxAttempts"/>, <paramref name="dontThrowSteps"/> or <paramref name="maxEvaluations"/> is negative.
    /// </exception>
    public IterativeBootstrap(
        double? accuracy = null,
        double? minValue = null,
        double? maxValue = null,
        int maxAttempts = 1,
        double maxFactor = 2.0,
        double minFactor = 2.0,
        bool dontThrow = false,
        int dontThrowSteps = 10,
        int maxEvaluations = Solver1D<Brent, ISolver1DFunction>.MaxFunctionEvaluations)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxAttempts);
        ArgumentOutOfRangeException.ThrowIfNegative(dontThrowSteps);
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvaluations);
        if (!(maxFactor >= 1.0))
        {
            throw new ArgumentException(
                $"Expected that maxFactor would be at least 1.0 but got {Show(maxFactor)}", nameof(maxFactor));
        }

        if (!(minFactor >= 1.0))
        {
            throw new ArgumentException(
                $"Expected that minFactor would be at least 1.0 but got {Show(minFactor)}", nameof(minFactor));
        }

        this.accuracy = accuracy;
        this.minValue = minValue;
        this.maxValue = maxValue;
        extraAttempts = maxAttempts - 1;
        maxFactorExcess = maxFactor - 2.0;
        minFactorExcess = minFactor - 2.0;
        this.dontThrow = dontThrow;
        dontThrowStepsOffset = dontThrowSteps - 10;
        maxEvaluationsOffset = maxEvaluations - Solver1D<Brent, ISolver1DFunction>.MaxFunctionEvaluations;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The curve has no helpers, all of them have expired, too few are alive for the interpolation, two have the same
    /// pillar or a helper's latest relevant date doesn't extend the curve, the solver fails on a node, or the passes
    /// don't converge.
    /// </exception>
    /// <exception cref="ArgumentException">A local interpolation can't be built over the curve's first nodes.</exception>
    public void Calculate<TTermStructure, TTraits, TInterpolator>(
        BootstrapState<TTermStructure> state, TInterpolator interpolator, EvaluationContext context)
        where TTermStructure : TermStructure
        where TTraits : struct, IBootstrapTraits<TTermStructure>
        where TInterpolator : struct, IInterpolator
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        BootstrapState<TTermStructure> s = state;
        int maxAttempts = extraAttempts + 1;
        double maxFactor = maxFactorExcess + 2.0;
        double minFactor = minFactorExcess + 2.0;
        int dontThrowSteps = dontThrowStepsOffset + 10;
        int maxEvaluations = maxEvaluationsOffset + Solver1D<Brent, ISolver1DFunction>.MaxFunctionEvaluations;

        // setup
        BootstrapHelper<TTermStructure>[] instruments = s.Instruments;
        int n = instruments.Length;
        if (!(n > 0))
        {
            throw new NanoQLException("no bootstrap helpers given");
        }

        // initialize: ensure helpers are sorted (a stable sort: equal pillars are refused below, so the order of equals
        // is never seen)
        BootstrapHelper<TTermStructure>[] sorted = [.. instruments.OrderBy(h => h.PillarDate)];
        sorted.CopyTo(instruments, 0);

        // skip expired helpers
        Date firstDate = TTraits.InitialDate(s);
        if (!Later(instruments[n - 1].PillarDate, firstDate))
        {
            throw new NanoQLException("all instruments expired");
        }

        int firstAliveHelper = 0;
        while (!Later(instruments[firstAliveHelper].PillarDate, firstDate))
        {
            ++firstAliveHelper;
        }

        int alive = n - firstAliveHelper;
        int nodes = alive + 1;
        if (!(nodes >= TInterpolator.RequiredPoints))
        {
            throw new NanoQLException(
                $"not enough alive instruments: {alive} provided, {TInterpolator.RequiredPoints - 1} required");
        }

        // calculate dates and times
        var dates = new Date[alive + 1];
        var times = new double[alive + 1];
        dates[0] = firstDate;
        times[0] = s.Curve.TimeFromReference(dates[0]);
        bool loopRequired = TInterpolator.Global;

        Date maxDate = firstDate;

        // pillar counter: i
        // helper counter: j
        for (int i = 1, j = firstAliveHelper; j < n; ++i, ++j)
        {
            BootstrapHelper<TTermStructure> helper = instruments[j];
            dates[i] = helper.PillarDate!.Value;
            times[i] = s.Curve.TimeFromReference(dates[i]);

            // check for duplicated pillars
            if (!(dates[i - 1] != dates[i]))
            {
                throw new NanoQLException($"more than one instrument with pillar {dates[i]}");
            }

            // check that the helper is really extending the curve, i.e. that
            // pillar-sorted helpers are also sorted by latestRelevantDate
            Date? latestRelevantDate = helper.LatestRelevantDate;
            if (!Later(latestRelevantDate, maxDate))
            {
                throw new NanoQLException(
                    $"{DataFormattersFunctions.Ordinal(j + 1)} instrument (pillar: {dates[i]}) has latestRelevantDate "
                    + $"({Show(latestRelevantDate)}) before or equal to previous instrument's latestRelevantDate "
                    + $"({maxDate})");
            }

            maxDate = dates[i] < latestRelevantDate!.Value ? latestRelevantDate.Value : dates[i];

            // when a pillar date is before the last relevant date the
            // convergence loop is required even if the Interpolator is local
            if (dates[i] < latestRelevantDate.Value)
            {
                loopRequired = true;
            }
        }

        s.Dates = dates;
        s.Times = times;
        s.MaxDate = maxDate;

        // set the initial guess: a curve is built afresh, so QuantLib's previous state has no counterpart. data[0] is
        // the only relevant item, but reasonable numbers might be needed for the whole data vector
        s.Data = new double[alive + 1];
        Array.Fill(s.Data, TTraits.InitialValue(s));

        // setup helpers
        for (int j = firstAliveHelper; j < n; ++j)
        {
            instruments[j] = s.Bind(instruments[j]);
        }

        double accuracyValue = accuracy ?? s.Accuracy;
        int maxIterations = TTraits.MaxIterations - 1;
        Brent firstSolver = new Brent().SetMaxEvaluations(maxEvaluations);
        FiniteDifferenceNewtonSafe solver = new FiniteDifferenceNewtonSafe().SetMaxEvaluations(maxEvaluations);

        bool validData = false;
        double[] previousData = [];

        for (int iteration = 0; ; ++iteration)
        {
            if (loopRequired && validData)
            {
                previousData = (double[])s.Data.Clone();
            }

            // Store min value and max value at each pillar so that we can expand search if necessary.
            var minValues = new double?[alive + 1];
            var maxValues = new double?[alive + 1];
            var attempts = new int[alive + 1];
            Array.Fill(attempts, 1);

            // pillar loop
            for (int i = 1, j = firstAliveHelper; j < n; ++i, ++j)
            {
                // bracket root and calculate guess
                if (minValues[i] is not double previousMin || maxValues[i] is not double previousMax)
                {
                    // First attempt; we take min and max either from
                    // explicit constructor parameter or from traits
                    minValues[i] = minValue ?? TTraits.MinValueAfter(i, s, validData, firstAliveHelper);
                    maxValues[i] = maxValue ?? TTraits.MaxValueAfter(i, s, validData, firstAliveHelper);
                }
                else
                {
                    // Extending a previous attempt.  A negative min
                    // is enlarged; a positive one is shrunk towards 0.
                    minValues[i] = previousMin < 0.0 ? previousMin * minFactor : previousMin / minFactor;

                    // The opposite holds for the max.
                    maxValues[i] = previousMax > 0.0 ? previousMax * maxFactor : previousMax / maxFactor;
                }

                double min = minValues[i]!.Value, max = maxValues[i]!.Value;
                double guess = TTraits.Guess(i, s, validData, firstAliveHelper);

                // adjust guess if needed
                if (guess >= max)
                {
                    guess = max - ((max - min) / 5.0);
                }
                else if (guess <= min)
                {
                    guess = min + ((max - min) / 5.0);
                }

                // extend interpolation if needed: a point at a time, including the pillar to be bootstrapped
                if (!validData)
                {
                    ExtendInterpolation(s, interpolator, i + 1);
                }

                BootstrapHelper<TTermStructure> instrument = instruments[j];
                var error = new PillarError<TTermStructure, TTraits, TInterpolator>(s, instrument, i, interpolator, context);
                try
                {
                    if (validData)
                    {
                        solver.Solve(error, accuracyValue, guess, min, max);
                    }
                    else
                    {
                        firstSolver.Solve(error, accuracyValue, guess, min, max);
                    }
                }
                catch (Exception e) when (e is NanoQLException or ArgumentException)
                {
                    // If we have more attempts left on this iteration, try again. Note that the max and min
                    // bounds will be widened on the retry.
                    if (attempts[i] < maxAttempts)
                    {
                        attempts[i]++;
                        i--;
                        j--;
                        continue;
                    }

                    if (dontThrow)
                    {
                        // Use the fallback value
                        s.Data[i] = DontThrowFallback(error, min, max, dontThrowSteps);

                        // Remember to update the interpolation. If we don't and we are on the last "i", we will still
                        // have the last attempted value in the solver being used in the interpolation.
                        s.Rebuild(interpolator);
                    }
                    else
                    {
                        throw new NanoQLException(
                            $"{DataFormattersFunctions.Ordinal(iteration + 1)} iteration: failed at "
                            + $"{DataFormattersFunctions.Ordinal(i)} alive instrument, pillar {Show(instrument.PillarDate)}, "
                            + $"maturity {Show(instrument.MaturityDate)}, reference date {dates[0]}: "
                            + DiscountingSwapEngine.QuantLibText(e),
                            e);
                    }
                }
            }

            if (!loopRequired)
            {
                break;
            }

            // exit condition
            double change = 0;
            if (validData)
            {
                for (int i = 1; i <= alive; ++i)
                {
                    // std::max(change, ...): the first argument unless the second compares larger
                    double d = Math.Abs(s.Data[i] - previousData[i]);
                    change = change < d ? d : change;
                }

                if (change <= accuracyValue)
                {
                    // convergence reached
                    break;
                }
            }

            // If we hit the max number of iterations and dontThrow is true, just use what we have
            if (iteration == maxIterations)
            {
                if (dontThrow)
                {
                    break;
                }

                throw new NanoQLException(
                    $"convergence not reached after {iteration} iterations; last improvement {Show(change)}, "
                    + $"required accuracy {Show(accuracyValue)}");
            }

            validData = true;
        }
    }

    // QuantLib's detail::dontThrowFallback: the value that gives the least absolute helper error on a grid of the
    // bracket, its ends included.
    internal static double DontThrowFallback<TF>(TF error, double xMin, double xMax, int steps)
        where TF : struct, ISolver1DFunction
    {
        if (!(xMin < xMax))
        {
            throw new NanoQLException("Expected xMin to be less than xMax");
        }

        // Set the initial value of the result to xMin and store the absolute bootstrap error at xMin
        double result = xMin;
        double absError = Math.Abs(error.Evaluate(xMin));
        double minError = absError;

        // Step out to xMax
        double stepSize = (xMax - xMin) / steps;
        for (int i = 0; i < steps; i++)
        {
            // Get absolute bootstrap error at updated x value
            xMin += stepSize;
            absError = Math.Abs(error.Evaluate(xMin));

            // If this absolute bootstrap error is less than the minimum, update result and minError
            if (absError < minError)
            {
                result = xMin;
                minError = absError;
            }
        }

        return result;
    }

    // QuantLib's interpolateWithoutUpdate over the first points, in its try block: the target's construction checks
    // decide; a global interpolator that refuses them is replaced by linear interpolation until it can be built, and a
    // local one's refusal propagates, as QuantLib rethrows it.
    internal static void ExtendInterpolation<TTermStructure, TInterpolator>(
        BootstrapState<TTermStructure> s, TInterpolator interpolator, int points)
        where TTermStructure : TermStructure
        where TInterpolator : struct, IInterpolator
    {
        s.Points = points;
        s.LinearFallback = false;
        try
        {
            interpolator.CheckPoints(points);
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            if (!TInterpolator.Global)
            {
                // no chance to fix it in a later iteration: the target's build refuses as its checks did
                s.Rebuild(interpolator);
                throw;
            }

            // otherwise use Linear while the target interpolation is not usable yet
            s.LinearFallback = true;
        }
    }

    // A date QuantLib compares as later than another: a null date is QuantLib's Date(), earlier than every date.
    private static bool Later(Date? d, Date than) => d is Date date && date > than;

    private static string Show(Date? d) => d is Date date ? date.ToString() : "null date";

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    // QuantLib's error lambda: the solver writes the node through it, and its return value is ignored, so the curve
    // keeps the last value the solver evaluated.
    private readonly struct PillarError<TTermStructure, TTraits, TInterpolator>(
        BootstrapState<TTermStructure> s,
        BootstrapHelper<TTermStructure> helper,
        int i,
        TInterpolator interpolator,
        EvaluationContext context) : ISolver1DFunction
        where TTermStructure : TermStructure
        where TTraits : struct, IBootstrapTraits<TTermStructure>
        where TInterpolator : struct, IInterpolator
    {
        public double Evaluate(double x)
        {
            TTraits.UpdateGuess(s.Data, x, i);
            s.Rebuild(interpolator);
            return helper.QuoteError(context);
        }
    }
}
