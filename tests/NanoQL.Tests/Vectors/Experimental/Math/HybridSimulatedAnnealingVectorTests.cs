// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The hybrid simulated annealing's vectors, <c>tests/vectors/experimental/math/hybridsimulatedannealing.json</c>: the
/// three typedefs the wheel names on their functors at fixed seeds, every evaluated point and the result.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HybridSimulatedAnnealingVectorTests
{
    [Fact]
    public void HybridSimulatedAnnealingVector()
    {
        var file = VectorFile.Load("experimental/math/hybridsimulatedannealing");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var cost = new LoggedSphere();
            var problem = new Problem(cost, new NoConstraint(), new Vector(2, 0.0) { [0] = 3.0, [1] = 2.0 }.AsSpan());
            var endCriteria = new EndCriteria(60, 40, 1e-8, 1e-8, 1e-8);
            double t0 = c.Double("startTemperature");
            HybridSimulatedAnnealingResetScheme reset = c.String("reset") == "best"
                ? HybridSimulatedAnnealingResetScheme.ResetToBestPoint
                : HybridSimulatedAnnealingResetScheme.ResetToOrigin;
            int resetSteps = c.Int("resetSteps"), reAnnealSteps = c.Int("reAnnealSteps");
            var probability = new ProbabilityBoltzmannDownhill(43);
            var temperature = new TemperatureExponential(1.0, 2);
            OptimizationMethod method = c.String("kind") switch
            {
                "gaussian" => new GaussianSimulatedAnnealing(
                    new SamplerGaussian(42), probability, temperature, null, t0, 0.01, reAnnealSteps, reset, resetSteps),
                "lognormal" => new LogNormalSimulatedAnnealing(
                    new SamplerLogNormal(42), probability, temperature, null, t0, 0.01, reAnnealSteps, reset, resetSteps),
                _ => new MirrorGaussianSimulatedAnnealing(
                    new SamplerMirrorGaussian(new Vector(2, -5.0), new Vector(2, 5.0), 42),
                    probability,
                    temperature,
                    null,
                    t0,
                    0.01,
                    reAnnealSteps,
                    reset,
                    resetSteps),
            };
            ProblemResults results = method.Minimize(problem, endCriteria);
            check.Output(c, "x", results.CurrentValue.ToArray());
            check.Output(c, "log", cost.Log.ToArray());
        }

        check.Complete();
    }

    // The wheel's cost function: its value the sphere's, logging each point; no values, so Levenberg-Marquardt throws.
    private sealed class LoggedSphere : CostFunction
    {
        public List<double> Log { get; } = [];

        public override double Value(Vector x)
        {
            Log.AddRange(x);
            return ((x[0] - 1.2) * (x[0] - 1.2)) + ((x[1] + 0.7) * (x[1] + 0.7));
        }

        public override Vector Values(Vector x) => throw new NanoQLException("the wheel's cost function gives no values");
    }
}
