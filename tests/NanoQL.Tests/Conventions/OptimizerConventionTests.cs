// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The optimizers' conventions (the S-core plan's C2): the line-search base's refusing direction (CR5 (a)); BFGS's
/// update, QuantLib's (CR7's kept designs); the seeds' refusals (CR2 (c)); the configurations' checks, QuantLib's, and
/// their records (CD12); CMA-ES on a NaN cost (CR7 (xxvi)) and differential evolution on an empty initial population
/// (CR7 (xxxvi)), refused; QuantLib's checks of L-BFGS-B, CMA-ES, differential evolution and simulated annealing;
/// differential evolution's catch of a cost's refusal, QuantLib's; and BFGS, L-BFGS-B and the simplex on one problem,
/// one minimum (§2.6).
/// </summary>
public class OptimizerConventionTests
{
    private static readonly EndCriteria Criteria = new(1000, 100, 1e-8, 1e-8, 1e-8);

    [Fact]
    public void ALineSearchMethodOverridingNoDirectionIsRefused()
    {
        // the four-argument member's default refuses, so a method overriding neither member can't run
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => new NoDirection().Minimize(new Problem(new Rosenbrock(), new NoConstraint(), [-1.2, 1.0]), Criteria));
        Assert.Equal("a line-search method overrides one of the GetUpdatedDirection members", e.Message);
    }

    [Fact]
    public void BFGSsUpdateIsQuantLibs()
    {
        // the update on the search direction before its step scaling, which QuantLib's Armijo search compensates:
        // QuantLib's evaluation counts on the extended Rosenbrock with Armijo, 47 in 2 variables from (-1, -1) and 76 in
        // 10 from -1 (probes\c2_facts.out); the textbook update on the step fails to converge in about 10,000 (the
        // reviewer's r8b.cpp, the plan's CR7)
        ProblemResults two = new BFGS().Minimize(new Problem(new Rosenbrock(), new NoConstraint(), [-1.0, -1.0]), Criteria);
        Assert.Equal(47, two.FunctionEvaluation);
        Assert.Equal(0.0, two.FunctionValue);
        ProblemResults ten = new BFGS().Minimize(
            new Problem(new Rosenbrock(), new NoConstraint(), new Vector(10, -1.0).AsSpan()), Criteria);
        Assert.Equal(76, ten.FunctionEvaluation);
        Assert.Equal(9.6981894086482411e-26, ten.FunctionValue);
    }

    [Fact]
    public void TheSeedsAreRequiredAndChecked()
    {
        foreach (long seed in new long[] { 0, -1, 4294967296 })
        {
            Assert.Equal(
                "seed",
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new DifferentialEvolution(new DifferentialEvolutionConfiguration(), seed)).ParamName);
            Assert.Equal(
                "seed", Assert.Throws<ArgumentOutOfRangeException>(() => new Cmaes(new CmaesConfiguration(), seed)).ParamName);
        }

        Assert.Equal(
            "rng",
            Assert.Throws<ArgumentNullException>(
                () => new SimulatedAnnealing<MersenneTwisterUniformRng>(0.5, 10.0, 0.05, 20, null!)).ParamName);

        // the generator a simulated annealing holds is a copy: the caller's draws don't move it
        var rng = new MersenneTwisterUniformRng(1);
        var method = new SimulatedAnnealing<MersenneTwisterUniformRng>(0.5, 10.0, 0.05, 20, rng);
        ProblemResults before = method.Minimize(new Problem(new Rosenbrock(), new NoConstraint(), [-1.2, 1.0]), Criteria);
        rng.NextReal();
        Assert.Equal(before, method.Minimize(new Problem(new Rosenbrock(), new NoConstraint(), [-1.2, 1.0]), Criteria));
    }

    [Fact]
    public void TheDifferentialEvolutionConfigurationIsQuantLibs()
    {
        // QuantLib's defaults, each setter returning a copy
        var defaults = new DifferentialEvolutionConfiguration();
        Assert.Equal(DifferentialEvolutionStrategy.BestMemberWithJitter, defaults.Strategy);
        Assert.Equal(DifferentialEvolutionCrossoverType.Normal, defaults.CrossoverType);
        Assert.Equal((100, 0.2, 0.9, true, false), (defaults.PopulationMembers, defaults.StepsizeWeight,
            defaults.CrossoverProbability, defaults.ApplyBounds, defaults.CrossoverIsAdaptive));
        Assert.True(defaults.InitialPopulation.IsEmpty && defaults.UpperBound.IsEmpty && defaults.LowerBound.IsEmpty);
        DifferentialEvolutionConfiguration changed = defaults.WithPopulationMembers(7);
        Assert.Equal(100, defaults.PopulationMembers);
        Assert.Equal(7, changed.PopulationMembers);

        // the population members follow the initial population, and setting them clears it, QuantLib's coupling
        DifferentialEvolutionConfiguration seeded = defaults.WithInitialPopulation([new Vector([1.0, 2.0]), new Vector([3.0, 4.0])]);
        Assert.Equal(2, seeded.PopulationMembers);
        Assert.True(seeded.WithPopulationMembers(5).InitialPopulation.IsEmpty);

        // QuantLib's checks, its texts
        AssertRefused<ArgumentException>(() => defaults.WithCrossoverProbability(1.5), "p", "Crossover probability (1.5) must be in [0,1] range");
        AssertRefused<ArgumentException>(() => defaults.WithStepsizeWeight(2.5), "w", "Step size weight (2.5) must be in [0,2] range");
        AssertRefused<ArgumentException>(() => defaults.WithPopulationMembers(0), "n", "Positive number of population members required");
        AssertRefused<ArgumentOutOfRangeException>(
            () => defaults.WithStrategy((DifferentialEvolutionStrategy)9), "s", "Unknown strategy (9)");
        AssertRefused<ArgumentOutOfRangeException>(
            () => defaults.WithCrossoverType((DifferentialEvolutionCrossoverType)3), "t", "Unknown crossover type (3)");

        // an empty initial population (CR7 (xxxvi)): QuantLib's sets 0 members and its minimization reads the front of
        // an empty population (differentialevolution.cpp:85-88); refused with QuantLib's own text for 0 members
        AssertRefused<ArgumentException>(() => defaults.WithInitialPopulation([]), "c", "Positive number of population members required");

        // QuantLib's minimization checks, on the stored configuration against the problem
        var sphere = new Problem(new Rosenbrock(), new BoundaryConstraint(-5.0, 5.0), [1.0, 1.0]);
        var criteria = new EndCriteria(5, 2, 1e-8, 1e-8, null);
        Assert.Equal(
            "wrong upper bound size in differential evolution configuration",
            Assert.Throws<NanoQLException>(() => new DifferentialEvolution(defaults.WithUpperBound([1.0]), 1).Minimize(sphere, criteria)).Message);
        Assert.Equal(
            "wrong lower bound size in differential evolution configuration",
            Assert.Throws<NanoQLException>(() => new DifferentialEvolution(defaults.WithLowerBound([1.0]), 1).Minimize(sphere, criteria)).Message);
        Assert.Equal(
            "wrong values size in initial population",
            Assert.Throws<NanoQLException>(
                () => new DifferentialEvolution(defaults.WithInitialPopulation([new Vector([1.0])]), 1).Minimize(sphere, criteria)).Message);
    }

    [Fact]
    public void TheCmaesConfigurationAndChecksAreQuantLibs()
    {
        var defaults = new CmaesConfiguration();
        Assert.Equal((0, 0.3), (defaults.PopulationSize, defaults.Sigma));
        Assert.True(defaults.LowerBound.IsEmpty && defaults.UpperBound.IsEmpty && defaults.InitialMean.IsEmpty);
        AssertRefused<ArgumentException>(() => defaults.WithSigma(0.0), "s", "sigma must be positive");
        Assert.Equal("n", Assert.Throws<ArgumentOutOfRangeException>(() => defaults.WithPopulationSize(-1)).ParamName);

        var criteria = new EndCriteria(5, 2, 1e-8, 1e-8, null);
        Problem Two() => new(new Rosenbrock(), new NoConstraint(), [0.0, 0.0]);
        Assert.Equal(
            "CMA-ES population size must be at least 2",
            Assert.Throws<NanoQLException>(() => new Cmaes(defaults.WithPopulationSize(1), 1).Minimize(Two(), criteria)).Message);
        Assert.Equal(
            "CMA-ES needs at least one dimension",
            Assert.Throws<NanoQLException>(
                () => new Cmaes(defaults, 1).Minimize(new Problem(new Rosenbrock(), new NoConstraint(), []), criteria)).Message);
        Assert.Equal(
            "initial mean size does not match problem dimension",
            Assert.Throws<NanoQLException>(() => new Cmaes(defaults.WithInitialMean([1.0]), 1).Minimize(Two(), criteria)).Message);
        Assert.Equal(
            "bound size does not match problem dimension",
            Assert.Throws<NanoQLException>(() => new Cmaes(defaults.WithBounds([0.0], [1.0]), 1).Minimize(Two(), criteria)).Message);

        // a NaN cost (CR7 (xxvi)): QuantLib's std::sort reads it, breaking the strict weak ordering it needs
        // (cmaes.cpp:190-191), MSVC's insertion sort finishing on an order that depends on where the NaN falls
        Assert.Equal(
            "CMA-ES cost function returned NaN",
            Assert.Throws<NanoQLException>(
                () => new Cmaes(defaults, 1).Minimize(
                    new Problem(new SimpleCostFunction(x => new Vector(1, double.NaN)), new NoConstraint(), [0.0, 0.0]), criteria)).Message);
    }

    [Fact]
    public void LBFGSBAndSimulatedAnnealingCheckTheirSettings()
    {
        AssertRefused<ArgumentOutOfRangeException>(() => new LBFGSB(0), "memory", "memory must be positive");
        AssertRefused<ArgumentException>(
            () => new LBFGSB([0.0, 0.0], [1.0]), "upperBound", "lower and upper bound sizes are inconsistent");
        Assert.Equal(
            "bounds size does not match the number of variables",
            Assert.Throws<NanoQLException>(
                () => new LBFGSB([0.0], [1.0]).Minimize(new Problem(new Rosenbrock(), new NoConstraint(), [0.5, 0.5]), Criteria)).Message);

        var rng = new MersenneTwisterUniformRng(1);
        Assert.Equal(
            "m",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SimulatedAnnealing<MersenneTwisterUniformRng>(0.5, 10.0, 0.05, -1, rng)).ParamName);
        Assert.Equal(
            "K",
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new SimulatedAnnealing<MersenneTwisterUniformRng>(0.5, 10.0, -1, 2.0, rng)).ParamName);
    }

    [Fact]
    public void ACostThatRefusesATrialPointScoresTheLargestDouble()
    {
        // differential evolution's crossover catches a refusal and scores the point QL_MAX_REAL: the cost
        // (x0 - 0.5)^2 + x1^2 refusing x0 < 0 (an ArgumentException here, QL_FAIL in QuantLib), the bounds [0, 1] for
        // the initial population only, 20 members, F 0.8, CR 0.9, seed 1. QuantLib's run (the code review's probe,
        // rerun in this run's probes\r8k.cpp) catches 1,776 refusals and ends on the maximum iterations, as here
        var cost = new Refusing();
        DifferentialEvolutionConfiguration configuration = new DifferentialEvolutionConfiguration()
            .WithBounds(false).WithPopulationMembers(20).WithStepsizeWeight(0.8).WithCrossoverProbability(0.9);
        ProblemResults r = new DifferentialEvolution(configuration, 1).Minimize(
            new Problem(cost, new BoundaryConstraint(0.0, 1.0), [0.9, 0.9]), new EndCriteria(200, 50, 1e-10, 1e-10, null));
        Assert.Equal(EndCriteriaType.MaxIterations, r.EndCriteriaType);
        Assert.Equal(0.00067232065235972398, r.FunctionValue);
        Assert.Equal(0.48812439639414834, r.CurrentValue[0]);
        Assert.Equal(0.023049743845787247, r.CurrentValue[1]);
        Assert.Equal(1776, cost.Refusals);
    }

    [Fact]
    public void BFGSLBFGSBAndTheSimplexFindOneMinimum()
    {
        // §2.6's two routes: QuantLib's weighted quadratic, its minimum (3, -2, 0.5), by three methods
        var problem = new Problem(new WeightedQuadratic(), new NoConstraint(), [0.0, 0.0, 0.0]);
        double[] expected = [3.0, -2.0, 0.5];
        foreach (OptimizationMethod method in new OptimizationMethod[] { new BFGS(), new LBFGSB(), new Simplex(0.1) })
        {
            ProblemResults r = method.Minimize(problem, new EndCriteria(10000, 1000, 1e-12, 1e-12, 1e-12));
            for (int i = 0; i < 3; i++)
            {
                Assert.True(Math.Abs(r.CurrentValue[i] - expected[i]) < 1e-5, $"{method.GetType().Name}: {r.CurrentValue[i]}");
            }
        }
    }

    private static void AssertRefused<TException>(Action action, string paramName, string message)
        where TException : ArgumentException
    {
        TException e = Assert.Throws<TException>(action);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith(message, e.Message);
    }

    private sealed class NoDirection : LineSearchBasedMethod
    {
    }

    private sealed class Refusing : CostFunction
    {
        public int Refusals { get; private set; }

        public override double Value(Vector x)
        {
            if (!(x[0] >= 0.0))
            {
                ++Refusals;
                throw new ArgumentException("x0 must be non-negative", nameof(x));
            }

            return ((x[0] - 0.5) * (x[0] - 0.5)) + (x[1] * x[1]);
        }

        public override Vector Values(Vector x) => new(1, Value(x));
    }

    private sealed class Rosenbrock : CostFunction
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

    private sealed class WeightedQuadratic : CostFunction
    {
        private static readonly double[] Center = [3.0, -2.0, 0.5];
        private static readonly double[] Weight = [1.0, 4.0, 0.25];

        public override double Value(Vector x)
        {
            double f = 0.0;
            for (int i = 0; i < x.Size; ++i)
            {
                f += Weight[i] * Math.Pow(x[i] - Center[i], 2);
            }

            return f;
        }

        public override Vector Values(Vector x) => new(1, Value(x));

        public override void Gradient(Vector grad, Vector x)
        {
            for (int i = 0; i < x.Size; ++i)
            {
                grad[i] = 2.0 * Weight[i] * (x[i] - Center[i]);
            }
        }
    }
}
