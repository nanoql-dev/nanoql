// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Simulated annealing's generator, copied per minimization, and its NaN guard: QuantLib's optimizer holds its
/// generator and continues its stream in a second minimization, where nanoql's draws from a copy each time; and
/// QuantLib's guard on the initial vertices tests <c>ytry_</c>, the previous trial's value, left from an earlier
/// minimization or unset (<c>simulatedannealing.hpp:166</c>), where its trial path tests the value just computed
/// (<c>:121-123</c>), so a vertex whose value is NaN enters the simplex. nanoql's guards each vertex's value, and the
/// vertices a contraction moves alike. QuantLib's values are its own class's.
/// </summary>
public class SimulatedAnnealingTests
{
    [Fact]
    public void ASecondMinimizationRepeatsTheFirst()
    {
        // the extended Rosenbrock from (-1.2, 1), the constant-factor scheme (λ 0.5, T0 10, ε 0.05, m 20), seed 1:
        // QuantLib's first minimization ends at f 2.2923314107319694e-07 after 4,357 evaluations, its second, on the
        // stream continued, at 4.2851581163638525e-07 after 4,414
        var method = new SimulatedAnnealing<MersenneTwisterUniformRng>(0.5, 10.0, 0.05, 20, new MersenneTwisterUniformRng(1));
        var criteria = new EndCriteria(5000, 100, 1e-8, 1e-8, null);
        ProblemResults first = method.Minimize(new Problem(new Rosenbrock(), new NoConstraint(), [-1.2, 1.0]), criteria);
        ProblemResults second = method.Minimize(new Problem(new Rosenbrock(), new NoConstraint(), [-1.2, 1.0]), criteria);
        Assert.Equal(2.2923314107319694e-07, first.FunctionValue);
        Assert.Equal(4357, first.FunctionEvaluation);
        Assert.Equal(first, second);
    }

    [Fact]
    public void AVertexWhoseValueIsNaNEntersAtTheLargestDouble()
    {
        // the Rosenbrock with a NaN where the second variable exceeds 1.2, so the initial simplex's third vertex,
        // (-1.2, 1.5), is NaN: nanoql's run is the one where that value is the largest double, QuantLib's QL_MAX_REAL, as
        // QuantLib's trial-path guard would set it; QuantLib's initial guard reads ytry_ instead, unset on a new object
        var criteria = new EndCriteria(5000, 100, 1e-8, 1e-8, null);
        ProblemResults Run(double above) => new SimulatedAnnealing<MersenneTwisterUniformRng>(
                0.5, 10.0, 0.05, 20, new MersenneTwisterUniformRng(1))
            .Minimize(new Problem(new Rosenbrock(above), new NoConstraint(), [-1.2, 1.0]), criteria);
        ProblemResults withNaN = Run(double.NaN);
        Assert.Equal(Run(double.MaxValue), withNaN);
        Assert.True(withNaN.FunctionValue < 1e-3, $"{withNaN.FunctionValue}");
    }

    [Fact]
    public void AContractedVertexWhoseValueIsNaNEntersAtTheLargestDouble()
    {
        // the Rosenbrock from (-1.2, 1), seed 1, NaN only within 1e-9 of the point its run's one contraction moves a
        // vertex to, (0.9835741504472622, 0.9789993643764852): nanoql's run is the one where that value is the largest
        // double, QuantLib's QL_MAX_REAL, as its trial-path guard would set it; the cost counts its calls there, so the
        // fact reds if the run stops reaching the point
        var criteria = new EndCriteria(5000, 100, 1e-8, 1e-8, null);
        (ProblemResults Results, int Reached) Run(double near)
        {
            var cost = new Rosenbrock(near: near);
            ProblemResults r = new SimulatedAnnealing<MersenneTwisterUniformRng>(
                    0.5, 10.0, 0.05, 20, new MersenneTwisterUniformRng(1))
                .Minimize(new Problem(cost, new NoConstraint(), [-1.2, 1.0]), criteria);
            return (r, cost.NearCalls);
        }

        (ProblemResults results, int reached) = Run(double.NaN);
        Assert.True(reached >= 1, $"the run reaches the NaN region {reached} times");
        Assert.Equal(Run(double.MaxValue).Results, results);
        Assert.True(results.FunctionValue < 1e-3, $"{results.FunctionValue}");
    }

    private sealed class Rosenbrock(double above = 0.0, double near = 0.0) : CostFunction
    {
        public int NearCalls { get; private set; }

        public override double Value(Vector x)
        {
            if (above != 0.0 && x[1] > 1.2)
            {
                return above;
            }

            if (near != 0.0 && Math.Abs(x[0] - 0.9835741504472622) < 1e-9 && Math.Abs(x[1] - 0.9789993643764852) < 1e-9)
            {
                ++NearCalls;
                return near;
            }

            return (100.0 * Math.Pow(x[1] - (x[0] * x[0]), 2)) + Math.Pow(1.0 - x[0], 2);
        }

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
