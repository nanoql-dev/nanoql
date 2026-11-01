// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/finitedifferencemodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Methods;

/// <summary>
/// The generic finite-difference model: rolls values back from one time to an earlier one in equal steps, stopping
/// exactly at each stopping time between, and applying a step condition after every step.
/// </summary>
/// <typeparam name="TScheme">The scheme that steps the values (QuantLib's template parameter, D12).</typeparam>
/// <remarks>
/// A working type (the S-eq plan's SEQR1): the rollback steps the caller's values in place through the scheme, so the
/// model's inner loop allocates nothing.
/// </remarks>
[WorkingType]
public sealed class FiniteDifferenceModel<TScheme>
    where TScheme : IFdmScheme
{
    private readonly double[] stoppingTimes;

    /// <summary>Initializes a new instance of the <see cref="FiniteDifferenceModel{TScheme}"/> class.</summary>
    /// <param name="evolver">The scheme.</param>
    /// <param name="stoppingTimes">The stopping times, sorted and made unique here; none by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="evolver"/> is <see langword="null"/>.</exception>
    public FiniteDifferenceModel(TScheme evolver, IEnumerable<double>? stoppingTimes = null)
    {
        ArgumentNullException.ThrowIfNull(evolver);
        Evolver = evolver;

        // std::sort, then std::unique
        double[] times = [.. stoppingTimes ?? []];
        Array.Sort(times);
        var unique = new List<double>(times.Length);
        foreach (double t in times)
        {
            if (unique.Count == 0 || unique[^1] != t)
            {
                unique.Add(t);
            }
        }

        this.stoppingTimes = [.. unique];
    }

    /// <summary>Gets the scheme.</summary>
    public TScheme Evolver { get; }

    /// <summary>Gets the stopping times, sorted and unique.</summary>
    public ImmutableArray<double> StoppingTimes => [.. stoppingTimes];

    /// <summary>Rolls the values back from <paramref name="from"/> to <paramref name="to"/>, in place.</summary>
    /// <param name="a">The values.</param>
    /// <param name="from">The later time.</param>
    /// <param name="to">The earlier time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="from"/> is before <paramref name="to"/>.</exception>
    public void Rollback(Span<double> a, double from, double to, int steps) =>
        RollbackImpl(a, from, to, steps, null);

    /// <summary>
    /// Rolls the values back from <paramref name="from"/> to <paramref name="to"/>, in place, applying a condition at
    /// every step.
    /// </summary>
    /// <param name="a">The values.</param>
    /// <param name="from">The later time.</param>
    /// <param name="to">The earlier time.</param>
    /// <param name="steps">The number of steps.</param>
    /// <param name="condition">The condition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="condition"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="steps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="from"/> is before <paramref name="to"/>.</exception>
    public void Rollback(Span<double> a, double from, double to, int steps, StepCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        RollbackImpl(a, from, to, steps, condition);
    }

    private void RollbackImpl(Span<double> a, double from, double to, int steps, StepCondition? condition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        if (!(from >= to))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"trying to roll back from {from} to {to}"), nameof(from));
        }

        double dt = (from - to) / steps, t = from;
        Evolver.SetStep(dt);

        if (stoppingTimes.Length != 0 && stoppingTimes[^1] == from)
        {
            condition?.ApplyTo(a, from);
        }

        for (int i = 0; i < steps; ++i, t -= dt)
        {
            double now = t;

            // make sure last step ends exactly on "to" in order to not
            // miss a stopping time at "to" due to numerical issues
            double next = (i < steps - 1) ? t - dt : to;

            if (Math.Abs(to - next) < Math.Sqrt(NumericLimits.QL_EPSILON))
            {
                next = to;
            }

            bool hit = false;
            for (int j = stoppingTimes.Length - 1; j >= 0; --j)
            {
                if (next <= stoppingTimes[j] && stoppingTimes[j] < now)
                {
                    // a stopping time was hit
                    hit = true;

                    // perform a small step to stoppingTimes[j]...
                    Evolver.SetStep(now - stoppingTimes[j]);
                    Evolver.Step(a, now);
                    condition?.ApplyTo(a, stoppingTimes[j]);

                    // ...and continue the cycle
                    now = stoppingTimes[j];
                }
            }

            // if we did hit...
            if (hit)
            {
                // ...we might have to make a small step to
                // complete the big one...
                if (now > next)
                {
                    Evolver.SetStep(now - next);
                    Evolver.Step(a, now);
                    condition?.ApplyTo(a, next);
                }

                // ...and in any case, we have to reset the
                // evolver to the default step.
                Evolver.SetStep(dt);
            }
            else
            {
                // if we didn't, the evolver is already set to the
                // default step, which is ok for us.
                Evolver.Step(a, now);
                condition?.ApplyTo(a, next);
            }
        }
    }
}
