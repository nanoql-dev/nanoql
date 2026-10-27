// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's least-squares vectors, <c>tests/vectors/cxx/math/optimization/leastsquare.json</c>, on the
/// harness's problem, the fit of a·exp(b·x) to points: <see cref="LeastSquareFunction"/>'s value, values and gradient,
/// and <see cref="NonLinearLeastSquare"/>'s fit, its residual norm, last value and exit flag.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LeastSquareCxxVectorTests
{
    [Fact]
    public void LeastSquareCxxVector()
    {
        var file = VectorFile.Load("cxx/math/optimization/leastsquare");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var problem = new ExponentialFit(c.Doubles("xs"), c.Doubles("ys"));
            if (c.String("call") == "function")
            {
                var f = new LeastSquareFunction(problem);
                var x = new Vector(c.Doubles("point"));
                check.Output(c, "value", f.Value(x));
                check.Output(c, "values", f.Values(x).ToArray());
                var grad = new Vector(2);
                f.Gradient(grad, x);
                check.Output(c, "gradient", grad.ToArray());
            }
            else
            {
                var nnls = new NonLinearLeastSquare(new NoConstraint(), c.Double("accuracy"), c.Int("maxiter"));
                NonLinearLeastSquareResults fit = nnls.Perform(problem, c.Doubles("initial"));
                check.Output(c, "residualNorm", fit.ResidualNorm);
                check.Output(c, "lastValue", fit.LastValue);
                check.Output(c, "exitFlag", (int)fit.ExitFlag);
            }
        }

        check.Complete();
    }

    // the harness's problem: the fit of a·exp(b·x) to the points, its gradient in (a, b)
    private sealed class ExponentialFit(double[] xs, double[] ys) : LeastSquareProblem
    {
        public override int Size => xs.Length;

        public override void TargetAndValue(Vector x, Vector target, Vector fct2fit)
        {
            for (int i = 0; i < xs.Length; ++i)
            {
                target[i] = ys[i];
                fct2fit[i] = x[0] * Math.Exp(x[1] * xs[i]);
            }
        }

        public override void TargetValueAndGradient(Vector x, Matrix gradFct2fit, Vector target, Vector fct2fit)
        {
            for (int i = 0; i < xs.Length; ++i)
            {
                double e = Math.Exp(x[1] * xs[i]);
                target[i] = ys[i];
                fct2fit[i] = x[0] * e;
                gradFct2fit[i, 0] = e;
                gradFct2fit[i, 1] = x[0] * xs[i] * e;
            }
        }
    }
}
