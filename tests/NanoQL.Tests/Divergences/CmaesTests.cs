// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// CMA-ES's generator, one per minimization, and its ranking of equal costs: QuantLib's optimizer keeps its Mersenne
/// Twister as a member (<c>cmaes.cpp:50</c>), so a second minimization on the same object continues the first's stream,
/// where nanoql's builds one from the seed in each minimization; and QuantLib ranks the samples with <c>std::sort</c>
/// (<c>cmaes.cpp:190-191</c>), which MSVC runs as an insertion sort up to 32 elements and an introsort beyond, where
/// nanoql sorts stably: equal costs keep QuantLib's order in a population of 32 or fewer, and their own beyond.
/// QuantLib's values are its own class's.
/// </summary>
public class CmaesTests
{
    [Fact]
    public void ASecondMinimizationRepeatsTheFirst()
    {
        // a weighted quadratic from 0, σ 0.5, seed 21: QuantLib's first minimization ends at f 4.4541525061843442e-21
        // after 1,036 evaluations, its second, on the stream continued, at 3.0470384909134869e-20 after 959
        var method = new Cmaes(new CmaesConfiguration().WithSigma(0.5), 21);
        var criteria = new EndCriteria(500, 50, 1e-12, 1e-12, null);
        Problem Quadratic() => new(new WeightedQuadratic(), new NoConstraint(), [0.0, 0.0, 0.0]);
        ProblemResults first = method.Minimize(Quadratic(), criteria);
        ProblemResults second = method.Minimize(Quadratic(), criteria);
        Assert.Equal(4.4541525061843442e-21, first.FunctionValue);
        Assert.Equal(1036, first.FunctionEvaluation);
        Assert.Equal(first, second);
    }

    [Fact]
    public void EqualCostsKeepTheirOrderToAPopulationOf32()
    {
        // QuantLib's floored third De Jong function, whose plateaus give equal costs, in 2 variables from (4.5, 4.5),
        // σ 3, seed 3, 30 generations: at a population of 20 QuantLib's insertion sort is stable and nanoql's point is
        // QuantLib's; at 40 QuantLib's introsort reorders equal costs, and QuantLib ends at (0.50280375608938233,
        // 0.11956447998580955) after 1,160 evaluations
        var criteria = new EndCriteria(30, 25, 1e-12, 1e-12, null);
        Problem Floors() => new(new FloorsFunction(), new NoConstraint(), [4.5, 4.5]);

        ProblemResults twenty = new Cmaes(new CmaesConfiguration().WithSigma(3.0).WithPopulationSize(20), 3)
            .Minimize(Floors(), criteria);
        Assert.Equal([0.43573851270280184, 0.32805134725077167], twenty.CurrentValue.AsSpan().ToArray());
        Assert.Equal(600, twenty.FunctionEvaluation);

        ProblemResults forty = new Cmaes(new CmaesConfiguration().WithSigma(3.0).WithPopulationSize(40), 3)
            .Minimize(Floors(), criteria);
        Assert.Equal(0.0, forty.FunctionValue);
        Assert.NotEqual([0.50280375608938233, 0.11956447998580955], forty.CurrentValue.AsSpan().ToArray());
    }

    private sealed class WeightedQuadratic : CostFunction
    {
        private static readonly double[] Center = [3.0, -2.0, 0.5];
        private static readonly double[] Weight = [1.0, 4.0, 0.25];

        public override double Value(Vector x)
        {
            double f = 0.0;
            for (int i = 0; i < x.Size; ++i)
            {
                f += Weight[i] * Math.Pow(x[i] - Center[i], 2);
            }

            return f;
        }

        public override Vector Values(Vector x) => new(1, Value(x));
    }

    private sealed class FloorsFunction : CostFunction
    {
        public override Vector Values(Vector x) => new(x.Size, Value(x));

        public override double Value(Vector x)
        {
            double fx = 0.0;
            for (int i = 0; i < x.Size; ++i)
            {
                fx += Math.Floor(x[i]) * Math.Floor(x[i]);
            }

            return fx;
        }
    }
}
