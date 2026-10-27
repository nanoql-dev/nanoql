// SPDX-FileCopyrightText: Copyright (C) 2008 Simon Ibbotson
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/localbootstrap.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// The localised bootstrapper: QuantLib's <c>LocalBootstrap</c>, which solves each section of a curve on a non-local
/// interpolation from the few helpers around it, so that each helper's risk stays local.
/// </summary>
/// <remarks>
/// <para>
/// A configuration struct (<see cref="IBootstrap"/>, D12): its zero value is QuantLib's default construction, a
/// localisation of 2, positivity forced and the curve's accuracy. The curve's nodes are its first date and every
/// helper's pillar, sorted; two helpers on one pillar are refused. From the localisation-th helper on, each step builds
/// the interpolation over the nodes so far through <see cref="ILocalInterpolator.LocalInterpolate"/>, keeping the
/// earlier sections, and solves the step's last nodes by Levenberg-Marquardt (at most 100 iterations, the accuracy as
/// the function's tolerance, positive values where positivity is forced) so that its last localisation helpers reprice
/// their quotes.
/// </para>
/// <para>
/// The interpolator must be an <see cref="ILocalInterpolator"/> (of QuantLib's, <see cref="ConvexMonotone"/>); another is
/// refused when the curve is built, where QuantLib's template fails to compile. As QuantLib's, each calculation is a
/// cold start, and a step's guess for its new node is the traits' guess at the step's index, QuantLib's
/// <c>Traits::guess(iInst, ts_, false, 0)</c> (<c>localbootstrap.hpp:229</c>), kept as written.
/// </para>
/// </remarks>
public readonly struct LocalBootstrap : IBootstrap
{
    // Stored so that zero reads as QuantLib's defaults: the localisation as an offset from 2, positivity forced unless
    // refused, and a null accuracy QuantLib's Null<Real>().
    private readonly int localisationOffset;
    private readonly bool notForcePositive;
    private readonly double? accuracy;

    /// <summary>Initializes a new instance of the <see cref="LocalBootstrap"/> struct.</summary>
    /// <param name="localisation">The number of helpers each step solves on.</param>
    /// <param name="forcePositive">Whether the solved values are kept positive.</param>
    /// <param name="accuracy">The solver's accuracy; <see langword="null"/> takes the curve's.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="localisation"/> is negative.</exception>
    public LocalBootstrap(int localisation = 2, bool forcePositive = true, double? accuracy = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(localisation);
        localisationOffset = localisation - 2;
        notForcePositive = !forcePositive;
        this.accuracy = accuracy;
    }

    /// <summary>Gets the number of helpers each step solves on.</summary>
    public int Localisation => localisationOffset + 2;

    /// <summary>Gets a value indicating whether the solved values are kept positive.</summary>
    public bool ForcePositive => !notForcePositive;

    /// <summary>Gets the solver's accuracy; <see langword="null"/> for the curve's.</summary>
    public double? Accuracy => accuracy;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The interpolator isn't an <see cref="ILocalInterpolator"/> ("LocalBootstrap needs a local interpolation"), or the
    /// localisation is below 1 or below the interpolator's data size adjustment, where QuantLib's steps read out of
    /// their arrays.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// There are fewer helpers than the interpolation requires or than the localisation and one, two helpers have the same
    /// pillar, or a step's solve doesn't reach the accuracy.
    /// </exception>
    public void Calculate<TTermStructure, TTraits, TInterpolator>(
        BootstrapState<TTermStructure> state, TInterpolator interpolator, EvaluationContext context)
        where TTermStructure : TermStructure
        where TTraits : struct, IBootstrapTraits<TTermStructure>
        where TInterpolator : struct, IInterpolator
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(context);
        if (interpolator is not ILocalInterpolator local)
        {
            throw new ArgumentException("LocalBootstrap needs a local interpolation", nameof(interpolator));
        }

        BootstrapState<TTermStructure> s = state;
        int localisation = Localisation;
        int dataAdjust = local.DataSizeAdjustment;
        if (!(localisation >= 1 && localisation >= dataAdjust))
        {
            throw new ArgumentException(
                $"localisation ({localisation}) must be at least 1 and at least the interpolator's data size adjustment "
                + $"({dataAdjust})",
                nameof(interpolator));
        }

        // setup
        BootstrapHelper<TTermStructure>[] instruments = s.Instruments;
        int n = instruments.Length;
        if (!(n >= TInterpolator.RequiredPoints))
        {
            throw new NanoQLException($"not enough instruments: {n} provided, {TInterpolator.RequiredPoints} required");
        }

        if (!(n > localisation))
        {
            throw new NanoQLException($"not enough instruments: {n} provided, {localisation} required.");
        }

        int nInsts = n;

        // ensure rate helpers are sorted (a stable sort: equal pillars are refused below, so the order of equals is never
        // seen)
        BootstrapHelper<TTermStructure>[] sorted = [.. instruments.OrderBy(h => h.PillarDate)];
        sorted.CopyTo(instruments, 0);

        // check that there is no instruments with the same maturity
        for (int i = 1; i < nInsts; ++i)
        {
            Date? m1 = instruments[i - 1].PillarDate, m2 = instruments[i].PillarDate;
            if (!(m1 != m2))
            {
                throw new NanoQLException($"two instruments have the same pillar date ({m1})");
            }
        }

        // QuantLib's check for a valid quote has no counterpart, a quote being a value; setup instruments
        for (int i = 0; i < nInsts; ++i)
        {
            instruments[i] = s.Bind(instruments[i]);
        }

        // a cold start, as QuantLib's every calculation is
        s.Data = new double[nInsts + 1];
        s.Data[0] = TTraits.InitialValue(s);

        // calculate dates and times
        s.Dates = new Date[nInsts + 1];
        s.Times = new double[nInsts + 1];
        s.Dates[0] = TTraits.InitialDate(s);
        s.Times[0] = s.Curve.TimeFromReference(s.Dates[0]);
        for (int i = 0; i < nInsts; ++i)
        {
            s.Dates[i + 1] = instruments[i].PillarDate!.Value;
            s.Times[i + 1] = s.Curve.TimeFromReference(s.Dates[i + 1]);
            s.Data[i + 1] = s.Data[i];
        }

        double accuracyValue = accuracy ?? s.Accuracy;
        var solver = new LevenbergMarquardt(accuracyValue, accuracyValue, accuracyValue);
        var endCriteria = new EndCriteria(100, 10, 0.00, accuracyValue, 0.00);
        Constraint solverConstraint = ForcePositive ? new PositiveConstraint() : new NoConstraint();

        // now start the bootstrapping.
        int iInst = localisation - 1;
        do
        {
            int initialDataPt = iInst + 1 - localisation + dataAdjust;
            var startArray = new Vector(localisation + 1 - dataAdjust);
            for (int j = 0; j < startArray.Size - 1; ++j)
            {
                startArray[j] = s.Data[initialDataPt + j];
            }

            // here we are assuming that the interpolation is a local interpolation: each evaluation rebuilds it over the
            // same nodes from the previous step's, QuantLib's update()
            Interpolation? previous = s.Interpolation;
            int points = iInst + 2;
            s.Interpolation = local.LocalInterpolate(
                s.Times.AsSpan(0, points), s.Data.AsSpan(0, points), localisation, previous, nInsts + 1);

            if (iInst >= localisation)
            {
                startArray[localisation - dataAdjust] = TTraits.Guess(iInst, s, false, 0);
            }
            else
            {
                startArray[localisation - dataAdjust] = s.Data[0];
            }

            int step = iInst;
            var currentCost = new SimpleCostFunction(x =>
            {
                for (int i = 0; i < x.Size; ++i)
                {
                    TTraits.UpdateGuess(s.Data, x[i], initialDataPt + i);
                }

                s.Interpolation = local.LocalInterpolate(
                    s.Times.AsSpan(0, points), s.Data.AsSpan(0, points), localisation, previous, nInsts + 1);

                var penalties = new Vector(localisation);
                int helpersEnd = step + 1;
                for (int k = 0; k < localisation; ++k)
                {
                    penalties[k] = instruments[helpersEnd - localisation + k].QuoteError(context);
                }

                return penalties;
            });

            var toSolve = new Problem(currentCost, solverConstraint, startArray.AsSpan());

            EndCriteriaType endType = solver.Minimize(toSolve, endCriteria).EndCriteriaType;

            // check the end criteria
            if (!EndCriteria.Succeeded(endType))
            {
                throw new NanoQLException($"Unable to strip yieldcurve to required accuracy: {endType}");
            }

            ++iInst;
        }
        while (iInst < nInsts);
    }
}
