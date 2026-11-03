// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// L-BFGS-B's gradient norm on its relative function-reduction stop: QuantLib's minimization breaks there after moving
/// to the new point, so the problem keeps the projected gradient's norm of the iterate before (<c>lbfgsb.cpp:528</c>,
/// <c>:638-646</c>); nanoql's reports the norm at the point it returns. QuantLib's values are its own class's.
/// </summary>
public class LBFGSBTests
{
    [Fact]
    public void TheReportedGradientNormIsThePointReturnedOnes()
    {
        // the extended Rosenbrock from (-1.2, 1), the default settings: QuantLib ends on the stationary function value
        // after 64 evaluations at (1.0000000026447173, 1.0000000051365288), reporting 5.4870954296920406e-09, the
        // previous iterate's squared norm, where the returned point's is 4.4158415759940019e-15
        var f = new Rosenbrock();
        ProblemResults r = new LBFGSB().Minimize(
            new Problem(f, new NoConstraint(), [-1.2, 1.0]), new EndCriteria(1000, 100, 1e-12, 1e-12, 1e-10));
        Assert.Equal(EndCriteriaType.StationaryFunctionValue, r.EndCriteriaType);
        Assert.Equal([1.0000000026447173, 1.0000000051365288], r.CurrentValue.AsSpan().ToArray());
        Assert.Equal(64, r.FunctionEvaluation);

        // the projected gradient at the point returned, with no bounds the gradient itself
        var x = new Vector(r.CurrentValue.AsSpan());
        var g = new Vector(2);
        f.Gradient(g, x);
        double norm = Math.Max(Math.Abs((x[0] - g[0]) - x[0]), Math.Abs((x[1] - g[1]) - x[1]));
        Assert.Equal(norm * norm, r.GradientNormValue);
        Assert.True(Math.Abs(r.GradientNormValue!.Value - 4.4158415759940019e-15) < 1e-20);
    }

    private sealed class Rosenbrock : CostFunction
    {
        public override double Value(Vector x) =>
            (100.0 * Math.Pow(x[1] - (x[0] * x[0]), 2)) + Math.Pow(1.0 - x[0], 2);

        public override Vector Values(Vector x) => new(1, Value(x));

        public override void Gradient(Vector grad, Vector x)
        {
            grad[0] = (-400.0 * x[0] * (x[1] - (x[0] * x[0]))) - (2.0 * (1.0 - x[0]));
            grad[1] = 200.0 * (x[1] - (x[0] * x[0]));
        }

        public override double ValueAndGradient(Vector grad, Vector x)
        {
            Gradient(grad, x);
            return Value(x);
        }
    }
}
