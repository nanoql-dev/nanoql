// SPDX-FileCopyrightText: Copyright (C) 2007 Marco Bianchetti
// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2007 Giorgio Facchinetti
// SPDX-FileCopyrightText: Copyright (C) 2012 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2012 Mateusz Kapturski
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/optimizers.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Experimental;
using NanoQL.Numerics;

namespace NanoQL.Tests.TestSuite;

[Trait("Oracle", "QuantLib")]
public class OptimizersTests
{
    private const double QL_EPSILON = 2.220446049250313E-16;

    // QuantLib's file-level vectors, which setup() fills; xUnit builds the class anew for each case.
    private readonly List<CostFunction> costFunctions_ = [];
    private readonly List<Constraint> constraints_ = [];
    private readonly List<Vector> initialValues_ = [];
    private readonly List<Size> maxIterations_ = [], maxStationaryStateIterations_ = [];
    private readonly List<Real> rootEpsilons_ = [], functionEpsilons_ = [], gradientNormEpsilons_ = [];
    private readonly List<EndCriteria> endCriterias_ = [];
    private readonly List<List<NamedOptimizationMethod>> optimizationMethods_ = [];
    private readonly List<Vector> xMinExpected_ = [], yMinExpected_ = [];

    // BOOST_ERROR records a failure and goes on; the case fails at its end.
    private readonly List<string> errors = [];

    private enum OptimizationMethodType
    {
        simplex,
        levenbergMarquardt,
        levenbergMarquardt2,
        conjugateGradient,
        conjugateGradient_goldstein,
        steepestDescent,
        steepestDescent_goldstein,
        bfgs,
        bfgs_goldstein,
        lbfgsb,
    }

    // Adapted: the problem (lines 342-343) is a ProblemState, the working type with QuantLib's Problem API, so the loop
    // calls setCurrentValue, minimize and values on it as QuantLib's does.
    [Fact]
    public void Test()
    {
        // Testing optimizers...

        Setup();

        // Loop over problems (currently there is only 1 problem)
        for (Size i = 0; i < costFunctions_.Count; ++i)
        {
            var problem = new ProblemState(new Problem(costFunctions_[i], constraints_[i], initialValues_[i].AsSpan()));
            Vector initialValues = problem.CurrentValue.Clone();
            // Loop over optimizers
            for (Size j = 0; j < optimizationMethods_[i].Count; ++j)
            {
                Real rootEpsilon = endCriterias_[i].RootEpsilon;
                Size endCriteriaTests = 1;
                // Loop over rootEpsilon
                for (Size k = 0; k < endCriteriaTests; ++k)
                {
                    problem.CurrentValue = initialValues;
                    var endCriteria = new EndCriteria(
                        endCriterias_[i].MaxIterations,
                        endCriterias_[i].MaxStationaryStateIterations,
                        rootEpsilon,
                        endCriterias_[i].FunctionEpsilon,
                        endCriterias_[i].GradientNormEpsilon);
                    rootEpsilon *= .1;
                    EndCriteriaType endCriteriaResult =
                        optimizationMethods_[i][j].OptimizationMethod.Minimize(problem, endCriteria);
                    Vector xMinCalculated = problem.CurrentValue.Clone();
                    Vector yMinCalculated = problem.Values(xMinCalculated);

                    // Check optimization results vs known solution
                    bool completed;
                    switch (endCriteriaResult)
                    {
                        case EndCriteriaType.None:
                        case EndCriteriaType.MaxIterations:
                        case EndCriteriaType.Unknown:
                            completed = false;
                            break;
                        default:
                            completed = true;
                            break;
                    }

                    Real xError = MaxDifference(xMinCalculated, xMinExpected_[i]);
                    Real yError = MaxDifference(yMinCalculated, yMinExpected_[i]);

                    bool correct = xError <= endCriteria.RootEpsilon || yError <= endCriteria.FunctionEpsilon;

                    if ((!completed) || (!correct))
                    {
                        errors.Add(
                            $"costFunction # = {i}"
                            + $"\nOptimizer: {optimizationMethods_[i][j].Name}"
                            + $"\n    function evaluations: {problem.FunctionEvaluation}"
                            + $"\n    gradient evaluations: {problem.GradientEvaluation}"
                            + $"\n    x expected:           {xMinExpected_[i]}"
                            + $"\n    x calculated:         {xMinCalculated}"
                            + $"\n    x difference:         {xMinExpected_[i] - xMinCalculated}"
                            + $"\n    rootEpsilon:          {endCriteria.RootEpsilon}"
                            + $"\n    y expected:           {yMinExpected_[i]}"
                            + $"\n    y calculated:         {yMinCalculated}"
                            + $"\n    y difference:         {yMinExpected_[i] - yMinCalculated}"
                            + $"\n    functionEpsilon:      {endCriteria.FunctionEpsilon}"
                            + $"\n    endCriteriaResult:    {endCriteriaResult}");
                    }
                }
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void NestedOptimizationTest()
    {
        // Testing nested optimizations...
        var optimizationBasedCostFunction = new OptimizationBasedCostFunction();
        var constraint = new NoConstraint();
        var initialValues = new Vector(1, 0.0);
        var problem = new Problem(optimizationBasedCostFunction, constraint, initialValues.AsSpan());
        var optimizationMethod = new LevenbergMarquardt();
        //Simplex optimizationMethod(0.1);
        //ConjugateGradient optimizationMethod;
        //SteepestDescent optimizationMethod;
        var endCriteria = new EndCriteria(1000, 100, 1e-5, 1e-5, 1e-5);
        optimizationMethod.Minimize(problem, endCriteria);
    }

    [Fact]
    public void TestHybridSimulatedAnnealingTemperatureTermination()
    {
        // Testing hybrid simulated annealing temperature termination...
        (EndCriteriaType type, int evaluations) =
            RunHybridAnnealing(new StationarySampler(), new RejectAllMoves(), new StagedCooling(), 100, 50);

        Assert.Equal(EndCriteriaType.None, type);
        Assert.Equal(3, evaluations);
    }

    [Fact]
    public void TestHybridSimulatedAnnealingIterationTermination()
    {
        // Testing hybrid simulated annealing iteration termination...
        (EndCriteriaType type, int evaluations) =
            RunHybridAnnealing(new ImprovingSampler(), new AcceptAllMoves(), new ConstantTemperature(), 3, 2);

        Assert.Equal(EndCriteriaType.MaxIterations, type);
        Assert.Equal(4, evaluations);
    }

    [Fact]
    public void TestHybridSimulatedAnnealingStationaryTermination()
    {
        // Testing hybrid simulated annealing stationary-point termination...
        (EndCriteriaType type, int evaluations) =
            RunHybridAnnealing(new StationarySampler(), new RejectAllMoves(), new ConstantTemperature(), 100, 2);

        Assert.Equal(EndCriteriaType.StationaryPoint, type);
        Assert.Equal(3, evaluations);
    }

    [Fact]
    public void TestLBFGSB()
    {
        // Testing L-BFGS-B optimizer...

        // Reference values below were produced with
        //   scipy.optimize.minimize(method='L-BFGS-B').
        // The unconstrained Rosenbrock and the box-constrained quadratics have
        // analytic minima, used directly as the expected results.

        var endCriteria = new EndCriteria(1000, 100, 1e-12, 1e-12, 1e-10);

        // Unconstrained equivalence: Rosenbrock in 2 and 10 dimensions must
        // reach the analytic minimum \vec{1}, as plain (L-)BFGS does.
        foreach (Size n in new Size[] { 2, 10 })
        {
            var f = new RosenbrockFunction();
            var c = new NoConstraint();
            var x0 = new Vector(n, -1.0);
            var optimizer = new LBFGSB(10, 1e-10, 1e1 * QL_EPSILON);
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            var x = new Vector(problem.CurrentValue.AsSpan());
            var expected = new Vector(n, 1.0);
            Real xError = MaxDifference(x, expected);
            if (xError > 1e-4)
            {
                errors.Add(
                    $"L-BFGS-B on unconstrained Rosenbrock-{n}D\n    calculated: {x}\n    expected:   {expected}"
                    + $"\n    x error:    {xError}");
            }

            if (problem.FunctionValue > 1e-8)
            {
                errors.Add($"L-BFGS-B Rosenbrock-{n}D f = {problem.FunctionValue} (expected ~0)");
            }
        }

        Vector center = new([3.0, -2.0, 0.5]);
        Vector weight = new([1.0, 4.0, 0.25]);

        // Interior minimum: with wide bounds enclosing the unconstrained
        // optimum the result must equal the unconstrained minimizer.
        {
            var f = new WeightedQuadratic(center, weight);
            Vector lo = new(3, -10.0), hi = new(3, 10.0);
            var c = new NonhomogeneousBoundaryConstraint(lo.AsSpan(), hi.AsSpan());
            var x0 = new Vector(3, 0.0);
            var optimizer = new LBFGSB(10, 1e-10, 1e1 * QL_EPSILON);
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            var x = new Vector(problem.CurrentValue.AsSpan());
            Real xError = MaxDifference(x, center);
            if (xError > 1e-6)
            {
                errors.Add(
                    $"L-BFGS-B interior-bound quadratic\n    calculated: {x}\n    expected:   {center}"
                    + $"\n    x error:    {xError}");
            }
        }

        // Active-bound minimum: the box [0,1]^3 clips the unconstrained
        // optimum (3,-2,0.5), so the solution must sit on the boundary at
        // (1, 0, 0.5) with a vanishing projected gradient.
        {
            var f = new WeightedQuadratic(center, weight);
            Vector lo = new(3, 0.0), hi = new(3, 1.0);
            var c = new NonhomogeneousBoundaryConstraint(lo.AsSpan(), hi.AsSpan());
            var x0 = new Vector(3, 0.5);
            var optimizer = new LBFGSB();
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            EndCriteriaType ret = problem.EndCriteriaType;
            // the operative stop must be the projected-gradient (KKT) test, not
            // the function-reduction fallback
            if (ret != EndCriteriaType.ZeroGradientNorm)
            {
                errors.Add($"L-BFGS-B active-bound quadratic ended with {ret} (expected ZeroGradientNorm)");
            }

            var x = new Vector(problem.CurrentValue.AsSpan());
            Vector expected = new([1.0, 0.0, 0.5]);
            Real xError = MaxDifference(x, expected);
            if (xError > 1e-7)
            {
                errors.Add(
                    $"L-BFGS-B active-bound quadratic\n    calculated: {x}\n    expected:   {expected}"
                    + $"\n    x error:    {xError}");
            }

            var g = new Vector(3);
            f.Gradient(g, x);
            Real pg = ProjectedGradientNorm(x, g, lo, hi);
            if (pg > 1e-6)
            {
                errors.Add($"L-BFGS-B active-bound projected gradient = {pg} (expected ~0)");
            }
        }

        // Bound-constrained Rosenbrock: the box [-2,0.5]^2 clips the optimum;
        // SciPy's L-BFGS-B converges to (0.5, 0.25) on the boundary.
        {
            var f = new RosenbrockFunction();
            Vector lo = new(2, -2.0), hi = new(2, 0.5);
            var c = new NonhomogeneousBoundaryConstraint(lo.AsSpan(), hi.AsSpan());
            var x0 = new Vector(2, -1.0);
            var optimizer = new LBFGSB(10, 1e-10, 1e1 * QL_EPSILON);
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            var x = new Vector(problem.CurrentValue.AsSpan());
            Vector expected = new([0.5, 0.25]);
            Real xError = MaxDifference(x, expected);
            if (xError > 1e-5)
            {
                errors.Add(
                    $"L-BFGS-B bound-constrained Rosenbrock\n    calculated: {x}\n    expected:   {expected}"
                    + $"\n    x error:    {xError}");
            }

            var g = new Vector(2);
            f.Gradient(g, x);
            Real pg = ProjectedGradientNorm(x, g, lo, hi);
            if (pg > 1e-6)
            {
                errors.Add($"L-BFGS-B bound-constrained Rosenbrock projected gradient = {pg} (expected ~0)");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestLBFGSBActiveBounds()
    {
        // Testing L-BFGS-B with active bounds, corners and pinned coordinates...

        var endCriteria = new EndCriteria(1000, 100, 1e-12, 1e-12, 1e-10);

        // All bounds active at a corner: the unconstrained optimum (5,5,5)
        // lies outside [0,1]^3, so every coordinate is pinned at its upper
        // bound and the solution is the corner (1,1,1) with a vanishing
        // projected gradient. Exercises the empty free-set subspace path.
        {
            var f = new WeightedQuadratic(new Vector([5.0, 5.0, 5.0]), new Vector([1.0, 1.0, 1.0]));
            Vector lo = new(3, 0.0), hi = new(3, 1.0);
            var c = new NonhomogeneousBoundaryConstraint(lo.AsSpan(), hi.AsSpan());
            var x0 = new Vector(3, 0.5);
            var optimizer = new LBFGSB(10, 1e-10, 1e1 * QL_EPSILON);
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            var x = new Vector(problem.CurrentValue.AsSpan());
            var expected = new Vector(3, 1.0);
            Real xError = MaxDifference(x, expected);
            var g = new Vector(3);
            f.Gradient(g, x);
            Real pg = ProjectedGradientNorm(x, g, lo, hi);
            if (xError > 1e-8 || pg > 1e-8)
            {
                errors.Add(
                    $"L-BFGS-B all-active corner\n    calculated: {x}\n    expected:   {expected}"
                    + $"\n    x error:    {xError}\n    proj. grad: {pg}");
            }
        }

        // Two simultaneously active bounds reached through distinct
        // breakpoints: the disparate weights make the projected-gradient
        // path hit the two upper bounds at different step lengths, so the
        // Cauchy search must traverse more than one breakpoint.
        {
            var f = new WeightedQuadratic(new Vector([10.0, 10.0]), new Vector([1.0, 100.0]));
            Vector lo = new(2, 0.0), hi = new(2, 1.0);
            var c = new NonhomogeneousBoundaryConstraint(lo.AsSpan(), hi.AsSpan());
            Vector x0 = new([0.9, 0.1]);
            var optimizer = new LBFGSB(10, 1e-10, 1e1 * QL_EPSILON);
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            var x = new Vector(problem.CurrentValue.AsSpan());
            Vector expected = new([1.0, 1.0]);
            Real xError = MaxDifference(x, expected);
            var g = new Vector(2);
            f.Gradient(g, x);
            Real pg = ProjectedGradientNorm(x, g, lo, hi);
            if (xError > 1e-8 || pg > 1e-8)
            {
                errors.Add(
                    $"L-BFGS-B two-active-bound quadratic\n    calculated: {x}\n    expected:   {expected}"
                    + $"\n    x error:    {xError}\n    proj. grad: {pg}");
            }
        }

        // Single variable (n = 1) with an active bound: the minimum of
        // (x-5)^2 over [0,1] is the boundary point x = 1.
        {
            var f = new WeightedQuadratic(new Vector([5.0]), new Vector([1.0]));
            Vector lo = new(1, 0.0), hi = new(1, 1.0);
            var c = new NonhomogeneousBoundaryConstraint(lo.AsSpan(), hi.AsSpan());
            var x0 = new Vector(1, 0.5);
            var optimizer = new LBFGSB(10, 1e-10, 1e1 * QL_EPSILON);
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            var x = new Vector(problem.CurrentValue.AsSpan());
            var g = new Vector(1);
            f.Gradient(g, x);
            Real xError = MaxDifference(x, new Vector([1.0]));
            Real pg = ProjectedGradientNorm(x, g, lo, hi);
            if (xError > 1e-8 || pg > 1e-8)
            {
                errors.Add(
                    $"L-BFGS-B n=1 active bound\n    calculated: {x}\n    x error:    {xError}\n    proj. grad: {pg}");
            }
        }

        // Pinned coordinate (lower == upper): the third variable is frozen
        // at 0.25 while the first two reach their unconstrained optima.
        {
            var f = new WeightedQuadratic(new Vector([3.0, -2.0, 0.5]), new Vector([1.0, 4.0, 0.25]));
            Vector lo = new([-10.0, -10.0, 0.25]), hi = new([10.0, 10.0, 0.25]);
            var c = new NonhomogeneousBoundaryConstraint(lo.AsSpan(), hi.AsSpan());
            var x0 = new Vector(3, 0.0);
            var optimizer = new LBFGSB(10, 1e-10, 1e1 * QL_EPSILON);
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            var x = new Vector(problem.CurrentValue.AsSpan());
            Vector expected = new([3.0, -2.0, 0.25]);
            Real xError = MaxDifference(x, expected);
            if (xError > 1e-6)
            {
                errors.Add(
                    $"L-BFGS-B pinned coordinate (lower==upper)\n    calculated: {x}\n    expected:   {expected}"
                    + $"\n    x error:    {xError}");
            }
        }

        Assert.Empty(errors);
    }

    [Fact]
    public void TestLBFGSBCoverage()
    {
        // Testing L-BFGS-B small memory, infeasible start and finite-difference gradient...

        var endCriteria = new EndCriteria(1000, 100, 1e-12, 1e-12, 1e-10);

        // Limited memory smaller than the dimension (m < n): forces eviction
        // of correction pairs and repeated rebuilds of the compact
        // representation while still converging to \vec{1}.
        {
            const Size n = 20;
            var f = new RosenbrockFunction();
            var c = new NoConstraint();
            var x0 = new Vector(n, -1.0);
            var optimizer = new LBFGSB(3, 1e-8, 1e1 * QL_EPSILON); // memory = 3 < n = 20
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            var x = new Vector(problem.CurrentValue.AsSpan());
            var expected = new Vector(n, 1.0);
            Real xError = MaxDifference(x, expected);
            if (xError > 1e-4)
            {
                errors.Add($"L-BFGS-B small-memory Rosenbrock-{n}D (m=3)\n    x error: {xError}");
            }

            if (problem.FunctionValue > 1e-8)
            {
                errors.Add($"L-BFGS-B small-memory Rosenbrock f = {problem.FunctionValue} (expected ~0)");
            }
        }

        // Infeasible start point: x0 lies outside the box and must be clipped
        // into [l,u] before optimization; the solution is unaffected.
        {
            var f = new WeightedQuadratic(new Vector([3.0, -2.0, 0.5]), new Vector([1.0, 4.0, 0.25]));
            Vector lo = new(3, 0.0), hi = new(3, 1.0);
            var c = new NonhomogeneousBoundaryConstraint(lo.AsSpan(), hi.AsSpan());
            var x0 = new Vector(3, 5.0); // outside [0,1]^3
            var optimizer = new LBFGSB(10, 1e-10, 1e1 * QL_EPSILON);
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            var x = new Vector(problem.CurrentValue.AsSpan());
            Vector expected = new([1.0, 0.0, 0.5]);
            Real xError = MaxDifference(x, expected);
            if (xError > 1e-7)
            {
                errors.Add(
                    $"L-BFGS-B infeasible start\n    calculated: {x}\n    expected:   {expected}\n    x error:    {xError}");
            }
        }

        // Finite-difference gradient with active bounds: the cost function
        // exposes no analytic gradient, so the optimizer uses central
        // differences. The interior coordinate (0.7) has to move off its
        // starting value, unlike the two clipped coordinates.
        {
            var f = new WeightedQuadraticValueOnly(new Vector([3.0, -2.0, 0.7]), new Vector([1.0, 4.0, 0.25]));
            Vector lo = new(3, 0.0), hi = new(3, 1.0);
            var c = new NonhomogeneousBoundaryConstraint(lo.AsSpan(), hi.AsSpan());
            var x0 = new Vector(3, 0.5);
            var optimizer = new LBFGSB(10, 1e-6, 1e7 * QL_EPSILON);
            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            var x = new Vector(problem.CurrentValue.AsSpan());
            Vector expected = new([1.0, 0.0, 0.7]);
            Real xError = MaxDifference(x, expected);
            if (xError > 1e-5)
            {
                errors.Add(
                    $"L-BFGS-B finite-difference gradient with bounds\n    calculated: {x}\n    expected:   {expected}"
                    + $"\n    x error:    {xError}");
            }
        }

        Assert.Empty(errors);
    }

    // Adapted: the optimizers' seed is the constructor's argument, where QuantLib's configuration holds it (withSeed);
    // QuantLib's vector holds four copies of one unused optimizer, each starting its generator from the seed, as each
    // nanoql minimization does.
    [Fact]
    public void TestDifferentialEvolution()
    {
        // Testing differential evolution...

        /* Note:
        *
        * The "ModFourthDeJong" doesn't have a well defined optimum because
        * of its noisy part. It just has to be <= 15 in our example.
        * The concrete value might differ for a different input and
        * different random numbers.
        *
        * The "Griewangk" function is an example where the adaptive
        * version of DifferentialEvolution turns out to be more successful.
        */

        DifferentialEvolutionConfiguration conf =
            new DifferentialEvolutionConfiguration()
            .WithStepsizeWeight(0.4)
            .WithBounds()
            .WithCrossoverProbability(0.35)
            .WithPopulationMembers(500)
            .WithStrategy(DifferentialEvolutionStrategy.BestMemberWithJitter)
            .WithCrossoverType(DifferentialEvolutionCrossoverType.Normal)
            .WithAdaptiveCrossover();
        var deOptim = new DifferentialEvolution(conf, 3242);

        DifferentialEvolutionConfiguration conf2 =
            new DifferentialEvolutionConfiguration()
            .WithStepsizeWeight(1.8)
            .WithBounds()
            .WithCrossoverProbability(0.9)
            .WithPopulationMembers(1000)
            .WithStrategy(DifferentialEvolutionStrategy.Rand1SelfadaptiveWithRotation)
            .WithCrossoverType(DifferentialEvolutionCrossoverType.Normal)
            .WithAdaptiveCrossover();
        var deOptim2 = new DifferentialEvolution(conf2, 3242);

        DifferentialEvolution[] diffEvolOptimisers =
        [
            deOptim,
            deOptim,
            deOptim,
            deOptim,
            deOptim2,
        ];

        CostFunction[] costFunctions =
        [
            new FirstDeJong(),
            new SecondDeJong(),
            new ModThirdDeJong(),
            new ModFourthDeJong(),
            new Griewangk(),
        ];

        BoundaryConstraint[] constraints =
        [
            new(-10.0, 10.0),
            new(-10.0, 10.0),
            new(-10.0, 10.0),
            new(-10.0, 10.0),
            new(-600.0, 600.0),
        ];

        Vector[] initialValues =
        [
            new(3, 5.0),
            new(2, 5.0),
            new(5, 5.0),
            new(30, 5.0),
            new(10, 100.0),
        ];

        EndCriteria[] endCriteria =
        [
            new(100, 10, 1e-10, 1e-8, null),
            new(100, 10, 1e-10, 1e-8, null),
            new(100, 10, 1e-10, 1e-8, null),
            new(500, 100, 1e-10, 1e-8, null),
            new(1000, 800, 1e-12, 1e-10, null),
        ];

        Real[] minima =
        [
            0.0,
            0.0,
            0.0,
            10.9639796558,
            0.0,
        ];

        for (Size i = 0; i < costFunctions.Length; ++i)
        {
            ProblemResults problem = diffEvolOptimisers[i].Minimize(
                new Problem(costFunctions[i], constraints[i], initialValues[i].AsSpan()), endCriteria[i]);

            if (i != 3)
            {
                // stable
                if (Math.Abs(problem.FunctionValue!.Value - minima[i]) > 1e-8)
                {
                    errors.Add($"costFunction # {i}\ncalculated: {problem.FunctionValue}\nexpected:   {minima[i]}");
                }
            }
            else
            {
                // this case is unstable due to randomness; we're good as
                // long as the result is below 15
                if (problem.FunctionValue > 15)
                {
                    errors.Add($"costFunction # {i}\ncalculated: {problem.FunctionValue}\nexpected:   less than 15");
                }
            }
        }

        Assert.Empty(errors);
    }

    // Adapted: the optimizers' seed is the constructor's argument, where QuantLib's configuration holds it (withSeed);
    // withPopulationSize returns a copy, which the 10-dimensional case keeps, where QuantLib's changes the
    // configuration in place.
    [Fact]
    public void TestCMAES()
    {
        // Testing CMA-ES optimizer...

        // Convex convergence: shifted, ill-conditioned separable quadratics
        // (2D, 10D) must reach the analytic minimum value 0.
        {
            var endCriteria = new EndCriteria(5000, 100, 1e-14, 1e-14, null);
            foreach (Size n in new Size[] { 2, 10 })
            {
                Vector center = new(n), weight = new(n);

                for (Size i = 0; i < n; ++i)
                {
                    center[i] = 1.0 + (0.5 * i);
                    weight[i] = Math.Pow(10.0, 2.0 * i / (Real)n);
                }

                var f = new WeightedQuadratic(center, weight);
                var c = new NoConstraint();
                var x0 = new Vector(n, 0.0);
                var optimizer = new Cmaes(new CmaesConfiguration().WithSigma(1.0), 42);

                ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);

                if (problem.FunctionValue > 1e-8)
                {
                    errors.Add($"CMA-ES convex quadratic-{n}D f = {problem.FunctionValue} (expected < 1e-8)");
                }
            }
        }

        // Non-convex convergence: Rosenbrock (2D, 10D) must reach the
        // minimizer (1,...,1) with f \approx 0.
        {
            var endCriteria = new EndCriteria(20000, 300, 1e-14, 1e-14, null);
            foreach (Size n in new Size[] { 2, 10 })
            {
                var f = new RosenbrockFunction();
                var c = new NoConstraint();
                var x0 = new Vector(n, 0.0);
                CmaesConfiguration cfg = new CmaesConfiguration().WithSigma(0.5);

                if (n >= 10)
                {
                    cfg = cfg.WithPopulationSize(20); // larger population for 10D
                }

                ProblemResults problem = new Cmaes(cfg, 123).Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
                var expected = new Vector(n, 1.0);

                Real xError = MaxDifference(new Vector(problem.CurrentValue.AsSpan()), expected);
                if (problem.FunctionValue > 1e-8 || xError > 1e-4)
                {
                    errors.Add($"CMA-ES Rosenbrock-{n}D\n    f       = {problem.FunctionValue}\n    x error = {xError}");
                }
            }
        }

        // Multimodal global search: Rastrigin 5D from a start away from the origin
        // must reach the global minimum (not merely a local one).
        {
            const Size n = 5;
            var f = new Rastrigin();
            var c = new BoundaryConstraint(-5.12, 5.12);
            var x0 = new Vector(n, 3.0);
            var endCriteria = new EndCriteria(2000, 200, 1e-12, 1e-12, null);

            // Large population lets CMA-ES cross the local-minimum lattice to the
            // global basin. Small populations reliably stall one lattice step away.
            CmaesConfiguration cfg = new CmaesConfiguration()
                .WithSigma(4.0).WithPopulationSize(150);
            ProblemResults cmaesProblem = new Cmaes(cfg, 1).Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);

            Real cmaesValue = cmaesProblem.FunctionValue!.Value;
            if (cmaesValue > 1e-4)
            {
                errors.Add($"CMA-ES Rastrigin-5D f = {cmaesValue} (expected < 1e-4)");
            }
        }

        // Rotational/affine invariance: an ill-conditioned quadratic x^T A x and
        // its rotation by an orthogonal Q must be solved in a comparable eval budget.
        {
            var A = new Matrix(2, 2, 0.0);
            A[0, 0] = 1.0;
            A[1, 1] = 30.0;

            Real theta = 0.7;
            var Q = new Matrix(2, 2, 0.0);
            Q[0, 0] = Math.Cos(theta);
            Q[0, 1] = -Math.Sin(theta);
            Q[1, 0] = Math.Sin(theta);
            Q[1, 1] = Math.Cos(theta);
            Matrix Arot = MatrixFunctions.Transpose(Q) * A * Q;

            var f = new QuadraticForm(A);
            var fRot = new QuadraticForm(Arot);
            var c = new NoConstraint();

            Vector x0 = new([2.0, 2.0]);
            Vector x0Rot = MatrixFunctions.Transpose(Q) * x0; // s.t. Q * x0Rot = x0

            var endCriteria = new EndCriteria(5000, 100, 1e-14, 1e-14, null);
            CmaesConfiguration cfg = new CmaesConfiguration().WithSigma(1.0);

            ProblemResults pA = new Cmaes(cfg, 99).Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);

            ProblemResults pRot = new Cmaes(cfg, 99).Minimize(new Problem(fRot, c, x0Rot.AsSpan()), endCriteria);

            if (pA.FunctionValue > 1e-8 || pRot.FunctionValue > 1e-8)
            {
                errors.Add($"CMA-ES invariance: values {pA.FunctionValue} and {pRot.FunctionValue} (expected < 1e-8)");
            }

            Real evalsA = pA.FunctionEvaluation;
            Real evalsRot = pRot.FunctionEvaluation;
            Real ratio = Math.Max(evalsA, evalsRot) / Math.Min(evalsA, evalsRot);
            if (ratio > 2.0)
            {
                errors.Add($"CMA-ES rotational invariance: eval budgets differ by {ratio}x ({evalsA} vs {evalsRot})");
            }
        }

        // Determinism check: runs must be bit-identical
        {
            var f = new WeightedQuadratic(new Vector([1.0, -2.0, 0.5]), new Vector([1.0, 3.0, 7.0]));
            var c = new NoConstraint();
            var x0 = new Vector(3, 0.0);
            var endCriteria = new EndCriteria(500, 50, 1e-12, 1e-12, null);
            CmaesConfiguration cfg = new CmaesConfiguration().WithSigma(1.0);

            ProblemResults p1 = new Cmaes(cfg, 2024).Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            ProblemResults p2 = new Cmaes(cfg, 2024).Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);

            if (p1.FunctionValue != p2.FunctionValue)
            {
                errors.Add($"CMA-ES not reproducible: {p1.FunctionValue} != {p2.FunctionValue}");
            }
        }

        // Box bounds: unconstrained minimum (3,-2) lies outside [0, 1] ^ 2.
        // The returned point must be feasible and sit at the constrained min (1,0).
        {
            var f = new WeightedQuadratic(new Vector([3.0, -2.0]), new Vector([1.0, 1.0]));
            var c = new BoundaryConstraint(0.0, 1.0);
            var x0 = new Vector(2, 0.5);
            var endCriteria = new EndCriteria(3000, 100, 1e-14, 1e-14, null);
            CmaesConfiguration cfg = new CmaesConfiguration().WithSigma(0.3);

            ProblemResults problem = new Cmaes(cfg, 5).Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);

            var x = new Vector(problem.CurrentValue.AsSpan());

            Vector expected = new([1.0, 0.0]);
            if (!c.Test(x))
            {
                errors.Add($"CMA-ES box-bounds returned infeasible point {x}");
            }

            Real xError = MaxDifference(x, expected);
            if (xError > 1e-5)
            {
                errors.Add($"CMA-ES box-bounds\n    calculated: {x}\n    expected:   {expected}\n    x error:    {xError}");
            }
        }

        // Ill-conditioned covariance: weights spanning twelve decades drive the
        // factorization of C over roughly six orders of magnitude in scale.
        {
            const Size n = 6;
            Vector center = new(n, 0.0), weight = new(n);
            for (Size i = 0; i < n; ++i)
            {
                weight[i] = Math.Pow(10.0, 12.0 * i / (Real)(n - 1));
            }

            var f = new WeightedQuadratic(center, weight);
            var c = new NoConstraint();
            var x0 = new Vector(n, 1.0);
            var endCriteria = new EndCriteria(5000, 200, 1e-16, 1e-16, null);
            var optimizer = new Cmaes(new CmaesConfiguration().WithSigma(1.0), 11);

            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);

            if (problem.FunctionValue > 1e-8)
            {
                errors.Add($"CMA-ES condition-1e12 f = {problem.FunctionValue} (expected < 1e-8)");
            }
        }

        // Rank-deficient objective: the last coordinate carries zero weight, so C
        // collapses in that direction. The search must stay usable and stop on a
        // termination criterion rather than throwing or going non-finite.
        {
            const Size n = 5;
            Vector center = new(n, 0.0), weight = new(n, 1.0);
            weight[n - 1] = 0.0;

            var f = new WeightedQuadratic(center, weight);
            var c = new NoConstraint();
            var x0 = new Vector(n, 1.0);
            var endCriteria = new EndCriteria(5000, 200, 1e-16, 1e-16, null);
            var optimizer = new Cmaes(new CmaesConfiguration().WithSigma(0.5), 13);

            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);
            EndCriteriaType ecType = problem.EndCriteriaType;

            if (problem.FunctionValue > 1e-8)
            {
                errors.Add($"CMA-ES rank-deficient f = {problem.FunctionValue} (expected < 1e-8)");
            }

            if (ecType == EndCriteriaType.None)
            {
                errors.Add("CMA-ES rank-deficient did not terminate on a criterion");
            }
        }

        // Rotated ill-conditioned quadratic. Unlike the separable case above, the
        // rotation correlates every coordinate, which reduces the smallest Cholesky
        // pivot below its floor multiple times per run. Convergence must be unaffected.
        {
            const Size n = 4;
            var Q = new Matrix(n, n, 0.0);
            for (Size i = 0; i < n; ++i)
            {
                Q[i, i] = 1.0;
            }

            // fixed Givens rotations; no RNG, so the test is reproducible
            Size[] p = [0, 1, 2, 0];
            Size[] q = [1, 2, 3, 3];
            Real[] angle = [0.7, 1.1, 0.4, 0.9];
            for (Size k = 0; k < 4; ++k)
            {
                var G = new Matrix(n, n, 0.0);
                for (Size i = 0; i < n; ++i)
                {
                    G[i, i] = 1.0;
                }

                G[p[k], p[k]] = Math.Cos(angle[k]);
                G[p[k], q[k]] = -Math.Sin(angle[k]);
                G[q[k], p[k]] = Math.Sin(angle[k]);
                G[q[k], q[k]] = Math.Cos(angle[k]);
                Q = G * Q;
            }

            var D = new Matrix(n, n, 0.0);
            for (Size i = 0; i < n; ++i)
            {
                D[i, i] = Math.Pow(1e12, (Real)i / (Real)(n - 1));
            }

            var f = new QuadraticForm(MatrixFunctions.Transpose(Q) * D * Q);
            var c = new NoConstraint();
            var x0 = new Vector(n, 1.0);
            var endCriteria = new EndCriteria(3000, 500, 1e-18, 1e-18, null);
            var optimizer = new Cmaes(new CmaesConfiguration().WithSigma(0.5), 17);

            ProblemResults problem = optimizer.Minimize(new Problem(f, c, x0.AsSpan()), endCriteria);

            if (problem.FunctionValue > 1e-8)
            {
                errors.Add($"CMA-ES rotated ill-conditioned f = {problem.FunctionValue} (expected < 1e-8)");
            }
        }

        Assert.Empty(errors);
    }

    // infinity norm of the projected gradient P(x - g, l, u) - x, the
    // quantity that vanishes at a KKT point of a box-constrained problem.
    private static Real ProjectedGradientNorm(Vector x, Vector g, Vector lo, Vector hi)
    {
        Real norm = 0.0;
        for (Size i = 0; i < x.Size; ++i)
        {
            Real proj = Math.Min(Math.Max(x[i] - g[i], lo[i]), hi[i]) - x[i];
            norm = Math.Max(norm, Math.Abs(proj));
        }

        return norm;
    }

    private static string OptimizationMethodTypeToString(OptimizationMethodType type) => type switch
    {
        OptimizationMethodType.simplex => "Simplex",
        OptimizationMethodType.levenbergMarquardt => "Levenberg Marquardt",
        OptimizationMethodType.levenbergMarquardt2 => "Levenberg Marquardt (cost function's jacbobian)",
        OptimizationMethodType.conjugateGradient => "Conjugate Gradient",
        OptimizationMethodType.steepestDescent => "Steepest Descent",
        OptimizationMethodType.bfgs => "BFGS",
        OptimizationMethodType.lbfgsb => "L-BFGS-B",
        OptimizationMethodType.conjugateGradient_goldstein => "Conjugate Gradient (Goldstein line search)",
        OptimizationMethodType.steepestDescent_goldstein => "Steepest Descent (Goldstein line search)",
        OptimizationMethodType.bfgs_goldstein => "BFGS (Goldstein line search)",
        _ => throw new NanoQLException("unknown OptimizationMethod type"),
    };

    private static OptimizationMethod MakeOptimizationMethod(
        OptimizationMethodType optimizationMethodType,
        Real simplexLambda,
        Real levenbergMarquardtEpsfcn,
        Real levenbergMarquardtXtol,
        Real levenbergMarquardtGtol) => optimizationMethodType switch
        {
            OptimizationMethodType.simplex => new Simplex(simplexLambda),
            OptimizationMethodType.levenbergMarquardt =>
                new LevenbergMarquardt(levenbergMarquardtEpsfcn, levenbergMarquardtXtol, levenbergMarquardtGtol),
            OptimizationMethodType.levenbergMarquardt2 =>
                new LevenbergMarquardt(levenbergMarquardtEpsfcn, levenbergMarquardtXtol, levenbergMarquardtGtol, true),
            OptimizationMethodType.conjugateGradient => new ConjugateGradient(),
            OptimizationMethodType.steepestDescent => new SteepestDescent(),
            OptimizationMethodType.bfgs => new BFGS(),
            OptimizationMethodType.lbfgsb => new LBFGSB(),
            OptimizationMethodType.conjugateGradient_goldstein => new ConjugateGradient(new GoldsteinLineSearch()),
            OptimizationMethodType.steepestDescent_goldstein => new SteepestDescent(new GoldsteinLineSearch()),
            OptimizationMethodType.bfgs_goldstein => new BFGS(new GoldsteinLineSearch()),
            _ => throw new NanoQLException("unknown OptimizationMethod type"),
        };

    private static List<NamedOptimizationMethod> MakeOptimizationMethods(
        IEnumerable<OptimizationMethodType> optimizationMethodTypes,
        Real simplexLambda,
        Real levenbergMarquardtEpsfcn,
        Real levenbergMarquardtXtol,
        Real levenbergMarquardtGtol)
    {
        var results = new List<NamedOptimizationMethod>();
        foreach (OptimizationMethodType optimizationMethodType in optimizationMethodTypes)
        {
            results.Add(new NamedOptimizationMethod(
                MakeOptimizationMethod(
                    optimizationMethodType,
                    simplexLambda,
                    levenbergMarquardtEpsfcn,
                    levenbergMarquardtXtol,
                    levenbergMarquardtGtol),
                OptimizationMethodTypeToString(optimizationMethodType)));
        }

        return results;
    }

    private static Real MaxDifference(Vector a, Vector b)
    {
        Vector diff = a - b;
        Real maxDiff = 0.0;
        foreach (Real i in diff)
        {
            maxDiff = Math.Max(maxDiff, Math.Abs(i));
        }

        return maxDiff;
    }

    // Set up, for each cost function, all the ingredients for optimization:
    // constraint, initial guess, end criteria, optimization methods.
    private void Setup()
    {
        // Cost function n. 1: 1D polynomial of degree 2 (parabolic function y=a*x^2+b*x+c)
        const Real a = 1;   // required a > 0
        const Real b = 1;
        const Real c = 1;
        var coefficients = new Vector(3);
        coefficients[0] = c;
        coefficients[1] = b;
        coefficients[2] = a;
        costFunctions_.Add(new OneDimensionalPolynomialDegreeN(coefficients));
        // Set constraint for optimizers: unconstrained problem
        constraints_.Add(new NoConstraint());
        // Set initial guess for optimizer
        var initialValue = new Vector(1);
        initialValue[0] = -100;
        initialValues_.Add(initialValue);
        // Set end criteria for optimizer
        maxIterations_.Add(10000);                // maxIterations
        maxStationaryStateIterations_.Add(100);   // MaxStationaryStateIterations
        rootEpsilons_.Add(1e-8);                  // rootEpsilon
        functionEpsilons_.Add(1e-8);              // functionEpsilon
        gradientNormEpsilons_.Add(1e-8);          // gradientNormEpsilon
        endCriterias_.Add(new EndCriteria(
            maxIterations_[^1],
            maxStationaryStateIterations_[^1],
            rootEpsilons_[^1],
            functionEpsilons_[^1],
            gradientNormEpsilons_[^1]));
        // Set optimization methods for optimizer
        OptimizationMethodType[] optimizationMethodTypes =
        [
            OptimizationMethodType.simplex, OptimizationMethodType.levenbergMarquardt,
            OptimizationMethodType.levenbergMarquardt2, OptimizationMethodType.conjugateGradient,
            OptimizationMethodType.bfgs, OptimizationMethodType.lbfgsb, //, steepestDescent
        ];
        Real simplexLambda = 0.1;                   // characteristic search length for simplex
        Real levenbergMarquardtEpsfcn = 1.0e-8;     // parameters specific for Levenberg-Marquardt
        Real levenbergMarquardtXtol = 1.0e-8;       //
        Real levenbergMarquardtGtol = 1.0e-8;       //
        optimizationMethods_.Add(MakeOptimizationMethods(
            optimizationMethodTypes,
            simplexLambda,
            levenbergMarquardtEpsfcn,
            levenbergMarquardtXtol,
            levenbergMarquardtGtol));
        // Set expected results for optimizer
        Vector xMinExpected = new(1), yMinExpected = new(1);
        xMinExpected[0] = -b / (2.0 * a);
        yMinExpected[0] = -((b * b) - (4.0 * a * c)) / (4.0 * a);
        xMinExpected_.Add(xMinExpected);
        yMinExpected_.Add(yMinExpected);
    }

    private sealed record NamedOptimizationMethod(OptimizationMethod OptimizationMethod, string Name);

    private static (EndCriteriaType Type, int Evaluations) RunHybridAnnealing<TSampler, TProbability, TTemperature>(
        TSampler sampler, TProbability probability, TTemperature temperature, Size maxIterations, Size maxStationaryStateIterations)
        where TSampler : IAnnealingSampler<TSampler>
        where TProbability : IAnnealingProbability<TProbability>
        where TTemperature : IAnnealingTemperature<TTemperature>
    {
        var costFunction = new MultidimensionalQuadratic();
        var constraint = new NoConstraint();
        var problem = new Problem(costFunction, constraint, new Vector(2, 1.0).AsSpan());
        var endCriteria = new EndCriteria(maxIterations, maxStationaryStateIterations, 1e-8, 1e-8, 1e-8);

        var annealing = new HybridSimulatedAnnealing<TSampler, TProbability, TTemperature, ReannealingTrivial>(
            sampler,
            probability,
            temperature,
            new ReannealingTrivial(),
            1.0,
            0.5,
            0,
            HybridSimulatedAnnealingResetScheme.NoResetScheme,
            0,
            null,
            HybridSimulatedAnnealingLocalOptimizeScheme.NoLocalOptimize);

        ProblemResults result = annealing.Minimize(problem, endCriteria);
        return (result.EndCriteriaType, result.FunctionEvaluation);
    }

    private sealed class StationarySampler : IAnnealingSampler<StationarySampler>
    {
        public void Sample(Vector newPoint, Vector currentPoint, Vector temp) => currentPoint.AsSpan().CopyTo(newPoint.AsSpan());

        public StationarySampler Clone() => this;
    }

    private sealed class ImprovingSampler : IAnnealingSampler<ImprovingSampler>
    {
        public void Sample(Vector newPoint, Vector currentPoint, Vector temp) => (currentPoint / 2.0).AsSpan().CopyTo(newPoint.AsSpan());

        public ImprovingSampler Clone() => this;
    }

    private sealed class RejectAllMoves : IAnnealingProbability<RejectAllMoves>
    {
        public bool Accept(Real currentValue, Real newValue, Vector temp) => false;

        public RejectAllMoves Clone() => this;
    }

    private sealed class AcceptAllMoves : IAnnealingProbability<AcceptAllMoves>
    {
        public bool Accept(Real currentValue, Real newValue, Vector temp) => true;

        public AcceptAllMoves Clone() => this;
    }

    private sealed class StagedCooling : IAnnealingTemperature<StagedCooling>
    {
        public void Update(Vector newTemperature, Vector currentTemperature, Vector steps)
        {
            newTemperature.AsSpan().Fill(0.0);
            if (steps[0] <= 2.0)
            {
                newTemperature[1] = 0.5;
            }
        }

        public StagedCooling Clone() => this;
    }

    private sealed class ConstantTemperature : IAnnealingTemperature<ConstantTemperature>
    {
        public void Update(Vector newTemperature, Vector currentTemperature, Vector steps) =>
            currentTemperature.AsSpan().CopyTo(newTemperature.AsSpan());

        public ConstantTemperature Clone() => this;
    }

    private sealed class MultidimensionalQuadratic : CostFunction
    {
        public override Real Value(Vector x) => ArrayFunctions.DotProduct(x, x);

        public override Vector Values(Vector x) => new(1, Value(x));
    }

    private sealed class OneDimensionalPolynomialDegreeN : CostFunction
    {
        private readonly Vector coefficients_;
        private readonly Size polynomialDegree_;

        public OneDimensionalPolynomialDegreeN(Vector coefficients)
        {
            coefficients_ = coefficients.Clone();
            polynomialDegree_ = coefficients.Size - 1;
        }

        public override Real Value(Vector x)
        {
            if (!(x.Size == 1))
            {
                throw new NanoQLException("independent variable must be 1 dimensional");
            }

            Real y = 0;
            for (Size i = 0; i <= polynomialDegree_; ++i)
            {
                y += coefficients_[i] * Math.Pow(x[0], i);
            }

            return y;
        }

        public override Vector Values(Vector x)
        {
            if (!(x.Size == 1))
            {
                throw new NanoQLException("independent variable must be 1 dimensional");
            }

            return new Vector(1, Value(x));
        }
    }

    // The goal of this cost function is simply to call another optimization inside
    // in order to test nested optimizations
    private sealed class OptimizationBasedCostFunction : CostFunction
    {
        public override Real Value(Vector x) => 1.0;

        public override Vector Values(Vector x)
        {
            // dummy nested optimization
            var coefficients = new Vector(3, 1.0);
            var oneDimensionalPolynomialDegreeN = new OneDimensionalPolynomialDegreeN(coefficients);
            var constraint = new NoConstraint();
            var initialValues = new Vector(1, 100.0);
            var problem = new Problem(oneDimensionalPolynomialDegreeN, constraint, initialValues.AsSpan());
            var optimizationMethod = new LevenbergMarquardt();
            //Simplex optimizationMethod(0.1);
            //ConjugateGradient optimizationMethod;
            //SteepestDescent optimizationMethod;
            var endCriteria = new EndCriteria(1000, 100, 1e-5, 1e-5, 1e-5);
            optimizationMethod.Minimize(problem, endCriteria);
            // return dummy result
            return new Vector(1, 0);
        }
    }

    // Extended Rosenbrock function with analytic gradient; global minimum 0 at \vec{1}
    private sealed class RosenbrockFunction : CostFunction
    {
        public override Real Value(Vector x)
        {
            Real f = 0.0;
            for (Size i = 0; i + 1 < x.Size; ++i)
            {
                f += (100.0 * Math.Pow(x[i + 1] - (x[i] * x[i]), 2)) + Math.Pow(1.0 - x[i], 2);
            }

            return f;
        }

        public override Vector Values(Vector x) => new(1, Value(x));

        public override void Gradient(Vector grad, Vector x)
        {
            grad.AsSpan().Fill(0.0);
            for (Size i = 0; i + 1 < x.Size; ++i)
            {
                grad[i] += (-400.0 * x[i] * (x[i + 1] - (x[i] * x[i]))) - (2.0 * (1.0 - x[i]));
                grad[i + 1] += 200.0 * (x[i + 1] - (x[i] * x[i]));
            }
        }

        public override Real ValueAndGradient(Vector grad, Vector x)
        {
            Gradient(grad, x);
            return Value(x);
        }
    }

    // Same separable quadratic but with no analytic gradient, forcing the
    // optimizer onto the base-class central-difference gradient path.
    private class WeightedQuadraticValueOnly(Vector center, Vector weight) : CostFunction
    {
        protected Vector Center { get; } = center.Clone();

        protected Vector Weight { get; } = weight.Clone();

        public override Real Value(Vector x)
        {
            Real f = 0.0;
            for (Size i = 0; i < x.Size; ++i)
            {
                f += Weight[i] * Math.Pow(x[i] - Center[i], 2);
            }

            return f;
        }

        public override Vector Values(Vector x) => new(1, Value(x));
    }

    // Separable quadratic sum_i {w_i (x_i - c_i)^2}; unconstrained minimum c.
    private sealed class WeightedQuadratic(Vector center, Vector weight) : WeightedQuadraticValueOnly(center, weight)
    {
        public override void Gradient(Vector grad, Vector x)
        {
            for (Size i = 0; i < x.Size; ++i)
            {
                grad[i] = 2.0 * Weight[i] * (x[i] - Center[i]);
            }
        }

        public override Real ValueAndGradient(Vector grad, Vector x)
        {
            Gradient(grad, x);
            return Value(x);
        }
    }

    private sealed class FirstDeJong : CostFunction
    {
        public override Vector Values(Vector x) => new(x.Size, Value(x));

        public override Real Value(Vector x) => ArrayFunctions.DotProduct(x, x);
    }

    private sealed class SecondDeJong : CostFunction
    {
        public override Vector Values(Vector x) => new(x.Size, Value(x));

        public override Real Value(Vector x) =>
            (100.0 * ((x[0] * x[0]) - x[1]) * ((x[0] * x[0]) - x[1])) + ((1.0 - x[0]) * (1.0 - x[0]));
    }

    private sealed class ModThirdDeJong : CostFunction
    {
        public override Vector Values(Vector x) => new(x.Size, Value(x));

        public override Real Value(Vector x)
        {
            Real fx = 0.0;
            for (Size i = 0; i < x.Size; ++i)
            {
                fx += Math.Floor(x[i]) * Math.Floor(x[i]);
            }

            return fx;
        }
    }

    // Adapted: its generator, a member QuantLib's const value() draws from, is a working type held by the function.
    private sealed class ModFourthDeJong : CostFunction
    {
        private readonly MersenneTwisterUniformRng uniformRng_ = new(4711);

        public override Vector Values(Vector x) => new(x.Size, Value(x));

        public override Real Value(Vector x)
        {
            Real fx = 0.0;
            for (Size i = 0; i < x.Size; ++i)
            {
                fx += ((i + 1.0) * Math.Pow(x[i], 4.0)) + uniformRng_.NextReal();
            }

            return fx;
        }
    }

    private sealed class Griewangk : CostFunction
    {
        public override Vector Values(Vector x) => new(x.Size, Value(x));

        public override Real Value(Vector x)
        {
            Real fx = 0.0;
            for (Size i = 0; i < x.Size; ++i)
            {
                fx += x[i] * x[i] / 4000.0;
            }

            Real p = 1.0;
            for (Size i = 0; i < x.Size; ++i)
            {
                p *= Math.Cos(x[i] / Math.Sqrt(i + 1.0));
            }

            return fx - p + 1.0;
        }
    }

    private sealed class Rastrigin : CostFunction
    {
        public override Real Value(Vector x)
        {
            Real fx = 10.0 * x.Size;

            for (Size i = 0; i < x.Size; ++i)
            {
                fx += (x[i] * x[i]) - (10.0 * Math.Cos(2.0 * Math.PI * x[i]));
            }

            return fx;
        }

        public override Vector Values(Vector x) => new(x.Size, Value(x));
    }

    private sealed class QuadraticForm(Matrix m) : CostFunction
    {
        private readonly Matrix m_ = m;

        public override Real Value(Vector x) => ArrayFunctions.DotProduct(x, m_ * x);

        public override Vector Values(Vector x) => new(x.Size, Value(x));
    }
}
