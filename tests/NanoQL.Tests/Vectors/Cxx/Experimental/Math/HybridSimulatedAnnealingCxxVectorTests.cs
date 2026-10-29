// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's annealing vectors, <c>tests/vectors/cxx/experimental/math/hybridsimulatedannealing.json</c>: the
/// hybrid simulated annealing on the samplers, probabilities, temperatures and reannealings the wheel doesn't name,
/// with and without Levenberg-Marquardt, every evaluated point logged.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HybridSimulatedAnnealingCxxVectorTests
{
    [Fact]
    public void HybridSimulatedAnnealingCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/math/hybridsimulatedannealing");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            bool lm = a[1].GetString() == "lm";
            var lower = new Vector(2, -5.0);
            var upper = new Vector(2, 5.0);
            OptimizationMethod method = a[0].GetString() switch
            {
                "cauchy" => Annealing(new SamplerCauchy(42), new ProbabilityAlwaysDownhill(), new TemperatureCauchy(1.0, 2), new ReannealingTrivial(), lm),
                "ring" => Annealing(
                    new SamplerRingGaussian(lower, upper, 42),
                    new ProbabilityBoltzmann(43),
                    new TemperatureBoltzmann(1.0, 2),
                    new ReannealingFiniteDifferences(1.0, 2, lower, upper),
                    lm),
                "veryFast" => Annealing(
                    new SamplerVeryFastAnnealing(lower, upper, 42),
                    new ProbabilityBoltzmannDownhill(43),
                    new TemperatureVeryFastAnnealing(1.0, 0.01, 100.0, 2),
                    new ReannealingTrivial(),
                    lm),
                _ => Annealing(
                    new SamplerGaussian(42),
                    new ProbabilityBoltzmannDownhill(43),
                    new TemperatureCauchy1D(1.0, 2),
                    new ReannealingFiniteDifferences(1.0, 2, lower, upper),
                    lm),
            };
            var cost = new LoggedSphere();
            var problem = new Problem(cost, new NoConstraint(), new Vector(2, 0.0) { [0] = 3.0, [1] = 2.0 }.AsSpan());
            ProblemResults r = method.Minimize(problem, new EndCriteria(a[2].GetInt32(), a[3].GetInt32(), 1e-8, 1e-8, 1e-8));
            check.Output(c, "x", r.CurrentValue.ToArray());
            check.Output(c, "value", r.FunctionValue!.Value);
            check.Output(c, "evaluations", r.FunctionEvaluation);
            check.Output(c, "endCriteria", (int)r.EndCriteriaType);
            check.Output(c, "log", cost.Log.ToArray());
        }

        check.Complete();
    }

    // The harness's settings: from temperature 1 to 0.01, reannealing every 5 steps, resetting to the best every 7.
    private static OptimizationMethod Annealing<TS, TP, TT, TR>(TS sampler, TP probability, TT temperature, TR reannealing, bool lm)
        where TS : IAnnealingSampler<TS>
        where TP : IAnnealingProbability<TP>
        where TT : IAnnealingTemperature<TT>
        where TR : IAnnealingReannealing<TR> =>
        new HybridSimulatedAnnealing<TS, TP, TT, TR>(
            sampler,
            probability,
            temperature,
            reannealing,
            1.0,
            0.01,
            5,
            HybridSimulatedAnnealingResetScheme.ResetToBestPoint,
            7,
            null,
            lm ? HybridSimulatedAnnealingLocalOptimizeScheme.EveryBestPoint : HybridSimulatedAnnealingLocalOptimizeScheme.NoLocalOptimize);

    // The harness's: the sphere centred at (1.2, −0.7), its values the residuals, logging each point the values read.
    private sealed class LoggedSphere : CostFunction
    {
        public List<double> Log { get; } = [];

        public override double Value(Vector x)
        {
            Vector r = Values(x);
            return (r[0] * r[0]) + (r[1] * r[1]);
        }

        public override Vector Values(Vector x)
        {
            Log.AddRange(x);
            return new Vector(2, 0.0) { [0] = x[0] - 1.2, [1] = x[1] + 0.7 };
        }
    }
}
