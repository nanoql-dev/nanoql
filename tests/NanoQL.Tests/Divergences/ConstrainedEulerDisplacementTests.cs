// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The constrained Euler evolver's displaced constraint (fixed). QuantLib's <c>setThisConstraint</c> takes each step's
/// constraint in logs displaced by <c>displacements_[i]</c> for step i
/// (<c>lognormalfwdrateeulerconstrained.cpp:146</c>), the displacement of the rate with the step's index, not of the
/// rate constrained, and past the rates when steps outnumber them: on eight steps over four rates displaced 0.1% to
/// 0.4%, rate 0 constrained to 3.5% at step 1 (its type set at that step) lands at 3.6%. nanoql displaces each
/// constraint by its constrained rate's displacement. Checks: on equal displacements QuantLib's arithmetic (the
/// vectors), bit for bit; and the displaced constraint landing to rounding, at steps past the rates too.
/// </summary>
public class ConstrainedEulerDisplacementTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0, 2.5];
    private static readonly double[] EvolutionTimes = [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0];

    [Theory]
    [InlineData(1, 0, 0.035)]
    [InlineData(5, 2, 0.034)]
    [InlineData(7, 3, 0.036)]
    public void ADisplacedConstraintLands(int step, int rate, double constraint)
    {
        var model = new FlatVol(
            [0.2, 0.2, 0.2, 0.2],
            new ExponentialForwardCorrelation(RateTimes, 0.5, 0.2),
            new EvolutionDescription(RateTimes, EvolutionTimes),
            4,
            [0.03, 0.031, 0.032, 0.033],
            [0.001, 0.002, 0.003, 0.004]);
        var e = new LogNormalFwdRateEulerConstrained(model, model, new MTBrownianGeneratorFactory(42), [4, 4, 4, 4, 4, 4, 4, 4]);
        int[] start = [.. Enumerable.Repeat(rate, 8)];
        e.SetConstraintType(start, [.. start.Select(s => s + 1)]);
        bool[] active = new bool[8];
        active[step] = true;
        e.SetThisConstraint([.. Enumerable.Repeat(constraint, 8)], active);
        for (int p = 0; p < 10; ++p)
        {
            e.StartNewPath();
            for (int s = 0; s <= step; ++s)
            {
                e.AdvanceStep();
            }

            Assert.Equal(constraint, e.CurrentState.ForwardRate(rate), 1e-15);
        }

        // QuantLib's landing at step 1, displaced by rate 1's 0.2%
        if (step == 1)
        {
            Assert.NotEqual(0.036000000000000004, e.CurrentState.ForwardRate(rate), 1e-4);
        }
    }
}
