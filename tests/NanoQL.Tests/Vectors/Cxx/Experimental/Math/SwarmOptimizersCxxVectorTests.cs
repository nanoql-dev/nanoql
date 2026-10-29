// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swarm optimizer vectors, <c>tests/vectors/cxx/experimental/math/swarmoptimizers.json</c>: the
/// particle swarm on the global topology as QuantLib documents it with three inertias and both forms, and the firefly
/// algorithm where the fixes don't reach, every evaluated point logged.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwarmOptimizersCxxVectorTests
{
    [Fact]
    public void SwarmOptimizersCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/math/swarmoptimizers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            if (c.Name.StartsWith("swarm", StringComparison.Ordinal))
            {
                int M = a[0].GetInt32();
                double[] coefficients = Numbers(a[2]), ip = Numbers(a[4]);
                long seed = a[5].GetInt64();
                ParticleSwarmOptimizationInertia inertia = a[3].GetString() switch
                {
                    "trivial" => new TrivialInertia(),
                    "simpleRandom" => new SimpleRandomInertia(ip[0], (long)ip[1]),
                    _ => new LevyFlightInertia(ip[0], (int)ip[1], (long)ip[2]),
                };
                var topology = new DocumentedGlobalTopology();
                OptimizationMethod pso = a[1].GetString() == "constriction"
                    ? new ParticleSwarmOptimization(M, topology, inertia, coefficients[0], coefficients[1], seed)
                    : new ParticleSwarmOptimization(M, topology, inertia, coefficients[0], coefficients[1], coefficients[2], seed);
                Run(c, check, pso, Numbers(a[6]), Numbers(a[7]), Numbers(a[8]), false, a[9].GetInt32(), a[10].GetInt32());
            }
            else
            {
                double[] wp = Numbers(a[6]), ip = Numbers(a[8]);
                FireflyAlgorithmRandomWalk walk = a[5].GetString() switch
                {
                    "gaussian" => new GaussianWalk(wp[0], wp[1], (long)wp[2]),
                    "levy" => new LevyFlightWalk(wp[0], wp[1], wp[2], (long)wp[3]),
                    _ => new DecreasingGaussianWalk(wp[0], wp[1], (long)wp[2]),
                };
                FireflyAlgorithmIntensity intensity = a[7].GetString() == "exponential"
                    ? new ExponentialIntensity(ip[0], ip[1], ip[2])
                    : new InverseLawSquareIntensity(ip[0], ip[1]);
                var fa = new FireflyAlgorithm(
                    a[0].GetInt32(), intensity, walk, a[1].GetInt32(), a[2].GetDouble(), a[3].GetDouble(), a[4].GetInt64());
                Run(
                    c,
                    check,
                    fa,
                    Numbers(a[10]),
                    Numbers(a[11]),
                    Numbers(a[12]),
                    a[9].GetString() == "constant",
                    a[13].GetInt32(),
                    a[14].GetInt32());
            }
        }

        check.Complete();
    }

    private static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetDouble())];

    private static void Run(
        VectorCase c,
        VectorAssert check,
        OptimizationMethod method,
        double[] lower,
        double[] upper,
        double[] center,
        bool constant,
        int maxIterations,
        int maxStationary)
    {
        var cost = new LoggedCost(center, constant);
        var problem = new Problem(cost, new NonhomogeneousBoundaryConstraint(lower, upper), center);
        ProblemResults r = method.Minimize(problem, new EndCriteria(maxIterations, maxStationary, 1e-8, 1e-8, 1e-8));
        check.Output(c, "x", r.CurrentValue.ToArray());
        check.Output(c, "value", r.FunctionValue!.Value);
        check.Output(c, "evaluations", r.FunctionEvaluation);
        check.Output(c, "endCriteria", (int)r.EndCriteriaType);
        check.Output(c, "log", cost.Log.ToArray());
    }

    // The harness's: the sphere Σ(xᵢ − cᵢ)², or 1 everywhere, logging each point.
    private sealed class LoggedCost(double[] center, bool constant) : CostFunction
    {
        public List<double> Log { get; } = [];

        public override double Value(Vector x)
        {
            Log.AddRange(x);
            if (constant)
            {
                return 1.0;
            }

            double s = 0.0;
            for (int i = 0; i < x.Size; ++i)
            {
                s += (x[i] - center[i]) * (x[i] - center[i]);
            }

            return s;
        }

        public override Vector Values(Vector x) => new(1, Value(x));
    }

    // The harness's: the global topology as QuantLib's comment documents it, the best personal best every particle's
    // social best.
    private sealed class DocumentedGlobalTopology : ParticleSwarmOptimizationTopology
    {
        private int M;

        public override void SetSize(int M) => this.M = M;

        public override void FindSocialBest()
        {
            double bestF = PersonalBestF[0];
            int bestP = 0;
            for (int i = 1; i < M; i++)
            {
                if (PersonalBestF[i] < bestF)
                {
                    bestF = PersonalBestF[i];
                    bestP = i;
                }
            }

            for (int i = 0; i < M; i++)
            {
                SetSocialBest(i, bestP, bestF);
            }
        }

        public override ParticleSwarmOptimizationTopology Clone() => new DocumentedGlobalTopology();
    }
}
