// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A line search's state lives for one minimization (the S-sr plan's SSD5): QuantLib's
/// <c>LineSearchBasedMethod</c> keeps one line search, whose Armijo step reads the gradient its last call left
/// (<c>armijo.cpp:42</c>), across minimizations; <see cref="LineSearchBasedMethod"/> runs each on a new one, so a
/// <see cref="ConjugateGradient"/> gives each problem the same result in either order, and its prototype line search is
/// never changed. On the wheel, QuantLib's conjugate gradient minimizing x² + y² from (5, −3) in three iterations
/// returns (0.0516, −0.0309) on a new method and (5, −3) on one that has minimized (x − 10)² + 50 (y + 10)² from 0.
/// </summary>
public class LineSearchStateTests
{
    [Fact]
    public void TwoMinimizationsFromOneMethodGiveTheSameResultsInEitherOrder()
    {
        var prototype = new ArmijoLineSearch();
        foreach ((EndCriteria criteria, Problem a, Problem b) in new[]
                 {
                     (new EndCriteria(1000, 100, 1e-8, 1e-8, 1e-8),
                      new Problem(new Quadratic(3.0, -1.0, 10.0), new NoConstraint(), [0.0, 0.0]),
                      new Problem(new Quadratic(-2.0, 4.0, 3.0), new NoConstraint(), [1.0, 1.0])),
                     (new EndCriteria(3, 2, 1e-8, 1e-8, 1e-8),
                      new Problem(new Quadratic(10.0, -10.0, 50.0), new NoConstraint(), [0.0, 0.0]),
                      new Problem(new Quadratic(0.0, 0.0, 1.0), new NoConstraint(), [5.0, -3.0])),
                 })
        {
            var method = new ConjugateGradient(prototype);
            ProblemResults aFirst = method.Minimize(a, criteria);
            ProblemResults bSecond = method.Minimize(b, criteria);
            ProblemResults bFirst = new ConjugateGradient(prototype).Minimize(b, criteria);
            ProblemResults aSecond = method.Minimize(a, criteria);

            Assert.Equal(aFirst, aSecond);
            Assert.Equal(bFirst, bSecond);
        }

        Assert.True(prototype.LastX.Empty);
        Assert.True(prototype.LastGradient.Empty);
    }

    // (x - x0)² + k (y - y0)²
    private sealed class Quadratic(double x0, double y0, double k) : CostFunction
    {
        public override double Value(Vector x) => ((x[0] - x0) * (x[0] - x0)) + (k * (x[1] - y0) * (x[1] - y0));

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
