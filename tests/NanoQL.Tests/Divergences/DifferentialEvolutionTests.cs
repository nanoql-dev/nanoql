// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Differential evolution's generator, one per minimization: QuantLib's optimizer keeps its Mersenne Twister as a
/// member (<c>differentialevolution.hpp:164</c>), so a second minimization on the same object continues the first's
/// stream; nanoql's builds one from the seed in each minimization, so a second is the first. QuantLib's values are its
/// own class's.
/// </summary>
public class DifferentialEvolutionTests
{
    [Fact]
    public void ASecondMinimizationRepeatsTheFirst()
    {
        // the sphere in 3 variables in [-10, 10] from (5, 5, 5), 30 members, seed 1, 40 generations: QuantLib's first
        // minimization ends at f 6.6875611825651025e-11 after 1,200 evaluations, its second, on the stream continued,
        // at 5.2217248194606262e-11
        var method = new DifferentialEvolution(
            new DifferentialEvolutionConfiguration()
                .WithStrategy(DifferentialEvolutionStrategy.BestMemberWithJitter)
                .WithPopulationMembers(30)
                .WithStepsizeWeight(0.5),
            1);
        var criteria = new EndCriteria(40, 10, 1e-10, 1e-8, null);
        Problem Sphere() => new(new SimpleCostFunction(x => new Vector(x.Size, ArrayFunctions.DotProduct(x, x))),
            new BoundaryConstraint(-10.0, 10.0), [5.0, 5.0, 5.0]);
        ProblemResults first = method.Minimize(Sphere(), criteria);
        ProblemResults second = method.Minimize(Sphere(), criteria);
        Assert.Equal(1200, first.FunctionEvaluation);
        Assert.Equal(EndCriteriaType.MaxIterations, first.EndCriteriaType);
        Assert.Equal(first, second);
    }
}
