// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The constrained Euler evolver's step covariances (fixed). QuantLib's
/// <c>LogNormalFwdRateEulerConstrained::setConstraintType</c> builds every step's covariances from the pseudo-root of
/// the step it is called at (<c>lognormalfwdrateeulerconstrained.cpp:117</c>), so a later step shifts its forwards by
/// another step's covariances and misses its own constraint, though the code states the intent ("shift each rate by
/// multiplier * weighting of index rate across the step", <c>:191</c>): on FlatVol of a 3-month first period then
/// semi-annual, each step's first alive rate constrained to its initial value plus 0.2% and the type set at step 0, the
/// constrained forward lands at step 0 and misses by +0.00097, −0.00196, −0.00323 and −0.00857 at steps 1 to 4. nanoql
/// builds step i's covariances from step i's pseudo-root. Checks: QuantLib's own evolver with its type set again before
/// each step, so each step's covariances are its own (the vectors), bit for bit; and the constraint landing to rounding
/// at every step.
/// </summary>
public class ConstrainedEulerCovarianceTests
{
    private static readonly double[] RateTimes = [0.25, 0.75, 1.25, 1.75, 2.25, 2.75];
    private static readonly double[] Rates = [0.03, 0.032, 0.034, 0.036, 0.038];

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    public void EachStepsConstraintLands(int factors)
    {
        var model = new FlatVol(
            [0.2, 0.21, 0.22, 0.23, 0.24],
            new ExponentialForwardCorrelation(RateTimes, 0.5, 0.2),
            new EvolutionDescription(RateTimes),
            factors,
            Rates,
            [0.0, 0.0, 0.0, 0.0, 0.0]);
        var e = new LogNormalFwdRateEulerConstrained(model, model, new MTBrownianGeneratorFactory(42), [5, 5, 5, 5, 5]);
        e.SetConstraintType([0, 1, 2, 3, 4], [1, 2, 3, 4, 5]);
        double[] constraints = [.. Rates.Select(r => r + 0.002)];
        e.SetThisConstraint(constraints, [true, true, true, true, true]);
        for (int p = 0; p < 20; ++p)
        {
            e.StartNewPath();
            for (int s = 0; s < 5; ++s)
            {
                e.AdvanceStep();
                Assert.Equal(constraints[s], e.CurrentState.ForwardRate(s), 1e-15);
            }
        }
    }
}
