// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exception types and messages of the solvers' and optimizers' refusals (playbook §2): a <c>QL_REQUIRE</c> on a
/// public method's argument throws <see cref="ArgumentException"/> naming the parameter, and QuantLib's other checks
/// throw <see cref="NanoQLException"/>, each with QuantLib's text. A vector's <c>{"throws": true}</c> accepts either
/// type, so these facts assert them, one per group.
/// </summary>
public class OptimizationErrorTests
{
    private static readonly Func<double, double> Square = x => (x * x) - 1.0;
    private static readonly Func<double, double> Cubic = x => (x * x * x) - (2.0 * x) - 5.0;
    private static readonly EndCriteria Criteria = new(1000, 100, 1e-8, 1e-8, 1e-8);

    [Fact]
    public void Solver1DRefusesItsArgumentsAndAnUnbracketedRoot()
    {
        var brent = new Brent();

        ArgumentException e = Assert.Throws<ArgumentException>(() => brent.Solve(Square, 0.0, 0.5, 0.1));
        Assert.Equal("accuracy", e.ParamName);
        Assert.StartsWith("accuracy (0) must be positive", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => brent.Solve(Square, 1e-8, 1.0, 2.0, 0.0));
        Assert.Equal("xMin", e.ParamName);
        Assert.StartsWith("invalid range: xMin_ (2) >= xMax_ (0)", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => brent.SetLowerBound(0.5).Solve(Square, 1e-8, 0.75, 0.0, 2.0));
        Assert.Equal("xMin", e.ParamName);
        Assert.StartsWith("xMin_ (0) < enforced low bound (0.5)", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => brent.SetUpperBound(1.5).Solve(Square, 1e-8, 0.75, 0.0, 2.0));
        Assert.Equal("xMax", e.ParamName);
        Assert.StartsWith("xMax_ (2) > enforced hi bound (1.5)", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => brent.Solve(Square, 1e-8, -0.5, 0.0, 2.0));
        Assert.Equal("guess", e.ParamName);
        Assert.StartsWith("guess (-0.5) < xMin_ (0)", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => brent.Solve(Square, 1e-8, 2.5, 0.0, 2.0));
        Assert.Equal("guess", e.ParamName);
        Assert.StartsWith("guess (2.5) > xMax_ (2)", e.Message, StringComparison.Ordinal);

        NanoQLException n = Assert.Throws<NanoQLException>(() => brent.Solve(Square, 1e-8, 2.5, 2.0, 3.0));
        Assert.Equal("root not bracketed: f[2,3] -> [3,8]", n.Message);

        n = Assert.Throws<NanoQLException>(() => brent.SetMaxEvaluations(0).Solve(Square, 1e-8, 0.5, 0.1));
        Assert.Equal(
            "unable to bracket root in 0 function evaluations (last bracket attempt: f[0.5,0.6] -> [-0.75,-0.64])",
            n.Message);

        ArgumentOutOfRangeException r = Assert.Throws<ArgumentOutOfRangeException>(() => brent.SetMaxEvaluations(-1));
        Assert.Equal("evaluations", r.ParamName);
    }

    [Fact]
    public void SolversThrowWhenTheirEvaluationsRunOut()
    {
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new Brent().SetMaxEvaluations(6).Solve(Cubic, 1e-15, 2.0, 0.1));
        Assert.Equal("maximum number of function evaluations (6) exceeded", e.Message);

        e = Assert.Throws<NanoQLException>(
            () => new NewtonSafe().SetMaxEvaluations(6).Solve(Cubic, x => (3.0 * x * x) - 2.0, 1e-15, 2.0, 0.1));
        Assert.Equal("maximum number of function evaluations (6) exceeded", e.Message);
    }

    [Fact]
    public void EndCriteriaRefusesItsStationaryIterations()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new EndCriteria(10, 1, 1e-8, 1e-8, 1e-8));
        Assert.Equal("maxStationaryStateIterations", e.ParamName);
        Assert.StartsWith("maxStationaryStateIterations_ (1) must be greater than one", e.Message, StringComparison.Ordinal);

        // The default for a null maximum, half the iterations, is checked the same way.
        e = Assert.Throws<ArgumentException>(() => new EndCriteria(3, null, 1e-8, 1e-8, 1e-8));
        Assert.Equal("maxStationaryStateIterations", e.ParamName);
        Assert.StartsWith("maxStationaryStateIterations_ (1) must be greater than one", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => new EndCriteria(10, 10, 1e-8, 1e-8, 1e-8));
        Assert.Equal("maxStationaryStateIterations", e.ParamName);
        Assert.StartsWith(
            "maxStationaryStateIterations_ (10) must be less than maxIterations_ (10)", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstraintsThrowOnBoundSizesAndUpdates()
    {
        var two = new NonhomogeneousBoundaryConstraint([0.0, 0.0], [1.0, 1.0]);
        var composite = new CompositeConstraint(two, new NoConstraint());
        Vector three = [0.5, 0.5, 0.5];

        NanoQLException e = Assert.Throws<NanoQLException>(() => composite.UpperBound(three));
        Assert.Equal("upper bound size (2) not equal to params size (3)", e.Message);
        e = Assert.Throws<NanoQLException>(() => composite.LowerBound(three));
        Assert.Equal("lower bound size (2) not equal to params size (3)", e.Message);

        e = Assert.Throws<NanoQLException>(() => new PositiveConstraint().Update([-0.5, 1.5], [1.0, 0.0], 0.1));
        Assert.Equal("can't update parameter vector", e.Message);

        e = Assert.Throws<NanoQLException>(() => new NonhomogeneousBoundaryConstraint([0.0, 0.0], [1.0]));
        Assert.Equal("Upper and lower boundaries sizes are inconsistent.", e.Message);
        e = Assert.Throws<NanoQLException>(() => two.Test(three));
        Assert.Equal("Number of parameters and boundaries sizes are inconsistent.", e.Message);
    }

    [Fact]
    public void LevenbergMarquardtRefusesItsInputsAndTooSmallTolerances()
    {
        var method = new LevenbergMarquardt();
        CostFunction linear = OptimizationSpecs.Named("linear");

        Assert.Equal("no variables given", Refusal(method, linear, [], Criteria));
        Assert.Equal("less functions (1) than available variables (2)", Refusal(method, linear, [1.0, 2.0], Criteria));
        Assert.Equal("negative f tolerance", Refusal(method, linear, [3.0], new EndCriteria(1000, 100, 1e-8, -1e-8, 1e-8)));
        Assert.Equal("negative x tolerance", Refusal(new LevenbergMarquardt(xtol: -1e-8), linear, [3.0], Criteria));
        Assert.Equal("negative g tolerance", Refusal(new LevenbergMarquardt(gtol: -1e-8), linear, [3.0], Criteria));

        // maxIterations * (n + 1) wraps, in nanoql's int as in QuantLib's int maxfev.
        Assert.Equal(
            "null number of evaluations",
            Refusal(method, linear, [3.0], new EndCriteria(int.MaxValue, 100, 1e-8, 1e-8, 1e-8)));

        // MINPACK's info 7, with zero tolerances on a fit with a residual.
        Assert.Equal(
            "MINPACK: xtol is too small. no further improvement in the approximate solution x is possible.",
            Refusal(
                new LevenbergMarquardt(xtol: 0.0, gtol: 0.0),
                OptimizationSpecs.Named("polynomialFit"),
                [0.0, 0.0, 0.0],
                new EndCriteria(1000, 100, 1e-8, 0.0, 1e-8)));
    }

    [Fact]
    public void SimplexRefusesAnInfeasibleStart()
    {
        var problem = new Problem(OptimizationSpecs.Named("quadratic2"), new BoundaryConstraint(-1.0, 0.75), [0.5, 1.5]);

        NanoQLException e = Assert.Throws<NanoQLException>(() => new Simplex(0.1).Minimize(problem, Criteria));
        Assert.Equal("Initial guess [ 0.5; 1.5 ] is not in the feasible region.", e.Message);
    }

    private static string Refusal(OptimizationMethod method, CostFunction f, double[] start, EndCriteria criteria) =>
        Assert.Throws<NanoQLException>(() => method.Minimize(new Problem(f, new NoConstraint(), start), criteria)).Message;

    [Fact]
    public void ALineSearchRefusesAConstraintItCantReach()
    {
        // linesearch.cpp:35-42: the step halved until the new point satisfies the constraint, at most 200 times.
        var search = new ArmijoLineSearch();
        var point = new Vector(1, 1.0);
        NanoQLException n = Assert.Throws<NanoQLException>(
            () => search.Update(point, new Vector(1, 1.0), 1.0, new NeverConstraint()));
        Assert.Equal("can't update linesearch", n.Message);
        Assert.Equal(1.0, point[0]);
    }

    private sealed class NeverConstraint : Constraint
    {
        public override bool Test(Vector p) => false;
    }
}
