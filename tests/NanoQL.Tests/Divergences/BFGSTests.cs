// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// BFGS's inverse Hessian, one per minimization (the S-core plan's CR5 (a)): QuantLib's method keeps it as a member,
/// built at its first minimization only (<c>bfgs.cpp:29-32</c>), so a second minimization on the same object starts from
/// the first's final Hessian, and one of another dimension reads it out of range; nanoql's starts each minimization from
/// the identity, so a second is the first. QuantLib's values are its own class's, from this run's
/// <c>probes\c2_facts.cpp</c> (<c>c2_facts.out</c>).
/// </summary>
public class BFGSTests
{
    [Fact]
    public void ASecondMinimizationStartsFromTheIdentity()
    {
        // the extended Rosenbrock from (-1.2, 1): QuantLib's first minimization ends at f 0 after 75 evaluations, its
        // second on the same object at f 4.9303806576313238e-30 after 92, from the first's Hessian
        var method = new BFGS();
        var criteria = new EndCriteria(1000, 100, 1e-8, 1e-8, 1e-8);
        ProblemResults first = method.Minimize(new Problem(new Rosenbrock(), new NoConstraint(), [-1.2, 1.0]), criteria);
        ProblemResults second = method.Minimize(new Problem(new Rosenbrock(), new NoConstraint(), [-1.2, 1.0]), criteria);
        Assert.Equal(0.0, first.FunctionValue);
        Assert.Equal(75, first.FunctionEvaluation);
        Assert.Equal(first, second);

        // and a minimization of another dimension on the same object, where QuantLib's reads its 2 x 2 Hessian
        ProblemResults three = method.Minimize(
            new Problem(new Rosenbrock(), new NoConstraint(), [-1.0, -1.0, -1.0]), criteria);
        Assert.True(three.FunctionValue < 1e-15, $"{three.FunctionValue}");
    }

    internal sealed class Rosenbrock : CostFunction
    {
        public override double Value(Vector x)
        {
            double f = 0.0;
            for (int i = 0; i + 1 < x.Size; ++i)
            {
                f += (100.0 * Math.Pow(x[i + 1] - (x[i] * x[i]), 2)) + Math.Pow(1.0 - x[i], 2);
            }

            return f;
        }

        public override Vector Values(Vector x) => new(1, Value(x));

        public override void Gradient(Vector grad, Vector x)
        {
            grad.AsSpan().Fill(0.0);
            for (int i = 0; i + 1 < x.Size; ++i)
            {
                grad[i] += (-400.0 * x[i] * (x[i + 1] - (x[i] * x[i]))) - (2.0 * (1.0 - x[i]));
                grad[i + 1] += 200.0 * (x[i + 1] - (x[i] * x[i]));
            }
        }

        public override double ValueAndGradient(Vector grad, Vector x)
        {
            Gradient(grad, x);
            return Value(x);
        }
    }
}
