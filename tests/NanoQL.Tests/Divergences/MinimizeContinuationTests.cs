// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's <c>minimize</c> changes the <c>Problem</c> it is given, so a second <c>minimize</c> on the same problem
/// starts from the first's optimum, its current value then. nanoql's <see cref="Problem"/> is immutable, and
/// <see cref="OptimizationMethod.Minimize(Problem, EndCriteria)"/> starts from the problem's initial value each time
/// (S2R11). <see cref="OptimizationMethod.Minimize(ProblemState, EndCriteria)"/> is QuantLib's: a second minimization of
/// one state continues from the first's optimum, as QuantLib's <c>HybridSimulatedAnnealing</c> runs its local
/// optimizer on the same problem.
/// </summary>
public class MinimizeContinuationTests
{
    [Fact]
    public void MinimizeProblemStartsFromTheInitialValueAndMinimizeStateContinues()
    {
        var problem = new Problem(new Booth(), new NoConstraint(), [3.0, -2.0]);
        var simplex = new Simplex(0.5);
        var criteria = new EndCriteria(1000, 100, 1e-8, 1e-8, 1e-8);

        // Each minimization of the problem starts from [3, -2]: the same results, the problem unchanged.
        ProblemResults first = simplex.Minimize(problem, criteria);
        ProblemResults again = simplex.Minimize(problem, criteria);
        Assert.Equal(first, again);
        Assert.Equal(new[] { 3.0, -2.0 }, problem.InitialValue);

        // A state's first minimization is the problem's; its second continues from the first's optimum, as a second
        // QuantLib minimize does, and so equals a minimization started there.
        var state = new ProblemState(problem);
        Assert.Equal(first.EndCriteriaType, simplex.Minimize(state, criteria));
        Assert.Equal(first.CurrentValue, state.CurrentValue.AsSpan().ToArray());
        Assert.Equal(first.FunctionEvaluation, state.FunctionEvaluation);
        Vector optimum = state.CurrentValue.Clone();

        EndCriteriaType continued = simplex.Minimize(state, criteria);
        ProblemResults fromOptimum =
            simplex.Minimize(new Problem(new Booth(), new NoConstraint(), optimum.AsSpan()), criteria);
        Assert.Equal(fromOptimum.EndCriteriaType, continued);
        Assert.Equal(fromOptimum.CurrentValue, state.CurrentValue.AsSpan().ToArray());
        Assert.Equal(fromOptimum.FunctionEvaluation, state.FunctionEvaluation);
        Assert.NotEqual(first.FunctionEvaluation, state.FunctionEvaluation);
    }

    // Booth's function, with its minimum 0 at (1, 3).
    private sealed class Booth : CostFunction
    {
        public override double Value(Vector x)
        {
            double a = x[0] + (2.0 * x[1]) - 7.0;
            double b = (2.0 * x[0]) + x[1] - 5.0;
            return (a * a) + (b * b);
        }

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
