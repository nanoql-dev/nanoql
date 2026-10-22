// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using static NanoQL.Numerics.MatrixFunctions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A line-search-based minimization ends at the point its line search last accepted (the S-sr plan's SSR6 (xvii)):
/// QuantLib's <c>LineSearchBasedMethod::minimize</c> returns on the function tolerance or the iteration limit
/// (linesearchbasedmethod.cpp:98-103) before it sets the problem's current value (line 105), so its problem holds the
/// point before with the function value of the point after. The port sets the current value first, the failed search's
/// own line (line 112). QuantLib's check: the point its line search last accepted, read from the cost function's
/// trace on the wheel. The hypersphere and lower-diagonal pseudo square roots, which minimize with
/// <see cref="ConjugateGradient"/> (pseudosqrt.cpp:166-244), move with it.
/// </summary>
public class LineSearchReturnTests
{
    private enum Exit
    {
        FunctionTolerance,
        IterationLimit,
        FailedSearch,
    }

    [Fact]
    public void ConjugateGradientEndsAtItsLastAcceptedPoint()
    {
        // f = (x - 3)² + 10 (y + 1)² from (0, 0), Armijo's line search, a function tolerance of 0.5: QuantLib returns
        // (0.9710071521062345, -1.1890072812441144), f 4.474049500436971, the point before; its line search last
        // accepted (1.400354483624572, -0.8279661301942048), f 2.8548223016635825 (the wheel's trace, 50 calls).
        var f = new Quadratic();
        ProblemResults r = new ConjugateGradient().Minimize(
            new Problem(f, new NoConstraint(), [0.0, 0.0]), new EndCriteria(1000, 100, 1e-8, 0.5, 1e-8));
        Assert.Equal([1.400354483624572, -0.8279661301942048], r.CurrentValue.AsSpan().ToArray());
        Assert.Equal(2.8548223016635825, r.FunctionValue);

        // At every exit the point and the function value are one point's, where QuantLib's first two pair the point
        // before with the value after: the function tolerance's at (1000, 1e-8) and (1000, 0.5); the iteration
        // limit's at (10, 1e-8), after eleven searches, none failing; and a failed search's at (3, 1e-8), (4, 1e-8)
        // and (6, 1e-8), where Armijo's search, which shares the iteration cap, fails at once and the start stands.
        foreach ((int maxIterations, double functionEpsilon, Exit exit) in new[]
                 {
                     (1000, 1e-8, Exit.FunctionTolerance), (1000, 0.5, Exit.FunctionTolerance), (10, 1e-8, Exit.IterationLimit),
                     (3, 1e-8, Exit.FailedSearch), (4, 1e-8, Exit.FailedSearch), (6, 1e-8, Exit.FailedSearch),
                 })
        {
            int stationary = Math.Min(maxIterations - 1, 100) == 100 ? 100 : 2;
            r = new ConjugateGradient().Minimize(
                new Problem(f, new NoConstraint(), [0.0, 0.0]),
                new EndCriteria(maxIterations, stationary, 1e-8, functionEpsilon, 1e-8));
            Assert.Equal(f.Value(new Vector(r.CurrentValue.AsSpan())), r.FunctionValue);
            bool atStart = r.CurrentValue.AsSpan().SequenceEqual([0.0, 0.0]);
            Assert.Equal(exit == Exit.FailedSearch, atStart);
            Assert.Equal(exit != Exit.FunctionTolerance, r.EndCriteriaType == EndCriteriaType.MaxIterations);
        }
    }

    [Fact]
    public void TheHypersphereAndLowerDiagonalRootsMoveWithIt()
    {
        // pseudoSqrt of a correlation matrix with a negative eigenvalue: QuantLib's roots end at the minimization's
        // previous iterate (the wheel, below). The port's end at its last accepted point, so the hypersphere cost
        // function, the distance of S·Sᵀ from the matrix, is lower.
        var m = new Matrix(new[,] { { 1.0, 0.9, 0.7 }, { 0.9, 1.0, -0.4 }, { 0.7, -0.4, 1.0 } });
        var quantLibHypersphere = new Matrix(new[,]
        {
            { 0.9932517970102069, 0.11597787606261183, 1.184299900145371e-08 },
            { 0.7730066570508713, -0.634397909954814, 1.1118021580028878e-08 },
            { 0.4236806741407794, 0.9058116174788302, 6.220809265358418e-09 },
        });
        var quantLibLowerDiagonal = new Matrix(new[,]
        {
            { 1.0, 0.0, 0.0 },
            { 0.6942308307502011, 0.7197524252379326, 0.0 },
            { 0.5258717412569193, -0.850563878698958, 4.306240160872664e-09 },
        });
        foreach ((SalvagingAlgorithmType sa, Matrix quantLib) in new[]
                 {
                     (SalvagingAlgorithmType.Hypersphere, quantLibHypersphere),
                     (SalvagingAlgorithmType.LowerDiagonal, quantLibLowerDiagonal),
                 })
        {
            Matrix root = PseudoSqrtFunctions.PseudoSqrt(m, sa);
            Assert.NotEqual(quantLib, root);
            Assert.True(Cost(root, m) < Cost(quantLib, m), $"{sa}: {Cost(root, m)} against {Cost(quantLib, m)}");
        }
    }

    // The hypersphere cost function on a unit diagonal: the squared Frobenius distance of S·Sᵀ from the target.
    private static double Cost(Matrix root, Matrix target)
    {
        Matrix c = root * Transpose(root);
        double error = 0.0;
        for (int i = 0; i < c.Rows; i++)
        {
            for (int j = 0; j < c.Columns; j++)
            {
                double d = c[i, j] - target[i, j];
                error += d * d;
            }
        }

        return error;
    }

    // The wheel's trace's Python function, (x[0] - 3.0) ** 2 + 10.0 * (x[1] + 1.0) ** 2, in its rounding: each square
    // one product, then 10 times the second.
    private sealed class Quadratic : CostFunction
    {
        public override double Value(Vector x) => ((x[0] - 3.0) * (x[0] - 3.0)) + (10.0 * ((x[1] + 1.0) * (x[1] + 1.0)));

        public override Vector Values(Vector x) => new(1, Value(x));
    }
}
