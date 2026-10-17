// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Problem vectors, <c>tests/vectors/cxx/math/optimization/problem.json</c>: what
/// <see cref="Simplex"/> leaves in the problem it minimizes, as <see cref="ProblemResults"/> (the current value, the
/// function value, the gradient norm, the counts, the end criterion), on the harness's named cost functions
/// (<see cref="OptimizationSpecs"/>); and <see cref="ProblemState"/>'s counting members, its reset and its setters.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ProblemCxxVectorTests
{
    [Fact]
    public void ProblemCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/problem");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            CostFunction costFunction = OptimizationSpecs.CostFunction(c.Input("costFunction"));
            if (c.Name == "Problem reset after the setters")
            {
                var x = new Vector(Numbers(c.Input("x")));
                ProblemState state = Fresh(costFunction, x);
                state.Value(x);
                state.Gradient(new Vector(x.Size), x);
                state.CurrentValue = new Vector(Numbers(c.Input("currentValue")));
                state.FunctionValue = c.Double("functionValue");
                state.GradientNormValue = c.Double("gradientNormValue");
                State(check, c, "before", state);
                state.Reset();
                State(check, c, "after", state);
                continue;
            }

            if (c.Inputs.TryGetProperty("x", out _))
            {
                var x = new Vector(Numbers(c.Input("x")));
                ProblemState state = Fresh(costFunction, x);
                check.Output(c, "value", state.Value(x));
                Counts(check, c, "valueEvaluations", state);

                state = Fresh(costFunction, x);
                check.Output(c, "values", state.Values(x));
                Counts(check, c, "valuesEvaluations", state);

                state = Fresh(costFunction, x);
                var gradient = new Vector(x.Size);
                state.Gradient(gradient, x);
                check.Output(c, "gradient", gradient);
                Counts(check, c, "gradientEvaluations", state);

                state = Fresh(costFunction, x);
                gradient = new Vector(x.Size);
                check.Output(c, "valueAndGradient", state.ValueAndGradient(gradient, x));
                check.Output(c, "valueAndGradientGradient", gradient);
                Counts(check, c, "valueAndGradientEvaluations", state);
                continue;
            }

            var problem = new Problem(
                costFunction, OptimizationSpecs.Constraint(c.Input("constraint")), Numbers(c.Input("initialValue")));
            var simplex = new Simplex(c.Double("lambda"));
            OptimizationSpecs.Results(check, c, simplex.Minimize(problem, OptimizationSpecs.EndCriteria(c.Input("endCriteria"))));
        }

        check.Complete();
    }

    // The harness's Problem: over NoConstraint, the point its initial value, reset.
    private static ProblemState Fresh(CostFunction costFunction, Vector x)
    {
        var state = new ProblemState(new Problem(costFunction, new NoConstraint(), x.AsSpan()));
        state.Reset();
        return state;
    }

    private static void Counts(VectorAssert check, VectorCase c, string output, ProblemState state) =>
        check.Output(c, output, new object?[] { state.FunctionEvaluation, state.GradientEvaluation });

    private static void State(VectorAssert check, VectorCase c, string prefix, ProblemState state)
    {
        check.Output(c, prefix + "CurrentValue", state.CurrentValue);
        check.Output(c, prefix + "FunctionValue", state.FunctionValue);
        check.Output(c, prefix + "GradientNormValue", state.GradientNormValue);
        check.Output(c, prefix + "FunctionEvaluation", state.FunctionEvaluation);
        check.Output(c, prefix + "GradientEvaluation", state.GradientEvaluation);
    }
}
