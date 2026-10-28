// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's latent model vectors, <c>tests/vectors/cxx/experimental/math/latentmodel.json</c>: models of each
/// constructor, their inspectors and members, their integrations, and their factor samplers' draws, on a test-side
/// subclass that adds an integration, as the harness's own does.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LatentModelCxxVectorTests
{
    [Fact]
    public void LatentModelCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/math/latentmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Name == "refused models")
            {
                check.Output(c, "inspect", Each(c.Input("models").EnumerateArray(), m => OnModel(m, LatentModelIntegrationType.GaussianQuadrature, new Inspect())));
                continue;
            }

            JsonElement spec = c.Input("model");
            check.Output(c, "inspect", OnModel(spec, LatentModelIntegrationType.GaussianQuadrature, new Inspect()));
            check.Output(c, "members", OnModel(spec, LatentModelIntegrationType.GaussianQuadrature, new Members(c)));
            string[] integrands = [.. c.Input("integrands").EnumerateArray().Select(f => f.GetString()!)];
            check.Output(c, "integrated", Each(integrands, f => OnModel(spec, LatentModelIntegrationType.GaussianQuadrature, new Integrated(f))));
            if (c.Expected.TryGetProperty("integratedTrapezoid", out _))
            {
                check.Output(c, "integratedTrapezoid", Each(
                    integrands.Where(f => f is not "indicator" and not "m0m1"),
                    f => OnModel(spec, LatentModelIntegrationType.Trapezoid, new Integrated(f))));
            }

            string[] samplers = [.. c.Input("samplers").EnumerateArray().Select(s => s.GetString()!)];
            long[] seeds = [.. c.Input("seeds").EnumerateArray().Select(s => s.GetInt64())];
            check.Output(c, "draws", Each(
                samplers.SelectMany(g => seeds.Select(s => (g, s))),
                p => OnModel(spec, LatentModelIntegrationType.GaussianQuadrature, new Draws(p.g, p.s, 20))));
            if (c.Expected.TryGetProperty("polarFirst", out _))
            {
                check.Output(c, "polarFirst", Each(
                    c.Input("polarSeeds").EnumerateArray().Select(s => s.GetInt64()),
                    s => OnModel(spec, LatentModelIntegrationType.GaussianQuadrature, new PolarFirst(s))));
            }
        }

        check.Complete();
    }

    // The integrands of the harness's latentmodelspec.hpp.
    internal static MultidimIntegrand Integrand(string name) => name switch
    {
        "one" => _ => 1.0,
        "m0" => m => m[0],
        "m0sq" => m => m[0] * m[0],
        "m0m1" => m => m[0] * m[1],
        "exp" => m => Math.Exp(0.1 * m[0]),
        "indicator" => m => m[0] < 0.3 ? 1.0 : 0.0,
        "cos" => m =>
        {
            double s = 0.0;
            foreach (double x in m)
            {
                s += x;
            }

            return Math.Cos(s);
        },
        _ => throw new ArgumentException($"unknown integrand {name}", nameof(name)),
    };

    private static object? OnModel(JsonElement spec, LatentModelIntegrationType type, IModelRead read)
    {
        string copula = spec.GetProperty("copula").GetString()!;
        return copula == "Gaussian"
            ? read.Read(Build<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(spec, type, default))
            : read.Read(Build<TCopulaPolicy, TCopulaPolicyInitTraits>(spec, type, PortfolioInputs.TraitsOf(spec.GetProperty("tOrders"))));
    }

    private static IntegratedModel<TCopula, TInit> Build<TCopula, TInit>(JsonElement spec, LatentModelIntegrationType type, TInit ini)
        where TCopula : struct, ICopulaPolicy<TCopula, TInit>
        where TInit : struct
    {
        if (spec.TryGetProperty("factorWeights", out JsonElement w))
        {
            return new IntegratedModel<TCopula, TInit>(type, PortfolioInputs.MatrixOf(w), ini);
        }

        if (spec.TryGetProperty("weights", out JsonElement v))
        {
            return new IntegratedModel<TCopula, TInit>(type, v.EnumerateArray().Select(VectorCase.Real).ToArray(), ini);
        }

        int n = spec.GetProperty("nVariables").GetInt32();
        return spec.TryGetProperty("correlSqr", out JsonElement s)
            ? new IntegratedModel<TCopula, TInit>(type, s.GetDouble(), n, ini)
            : new IntegratedModel<TCopula, TInit>(type, n, spec.GetProperty("correlation").GetDouble(), ini);
    }

    private static object[] Draw(Sample<ReadOnlyMemory<double>> s) => [.. s.Value.ToArray().Select(x => (object)x), s.Weight];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];

    private interface IModelRead
    {
        object? Read<TCopula, TInit>(IntegratedModel<TCopula, TInit> lm)
            where TCopula : struct, ICopulaPolicy<TCopula, TInit>
            where TInit : struct;
    }

    // The test-side latent model with an integration, the harness's HarnessLatentModel.
    private sealed class IntegratedModel<TCopula, TInit> : LatentModel<TCopula, TInit>
        where TCopula : struct, ICopulaPolicy<TCopula, TInit>
        where TInit : struct
    {
        private readonly LMIntegration integration;

        public IntegratedModel(LatentModelIntegrationType type, double[][] w, TInit ini)
            : base(w, ini) => integration = Create(type, NumFactors);

        public IntegratedModel(LatentModelIntegrationType type, double[] w, TInit ini)
            : base(w, ini) => integration = Create(type, NumFactors);

        public IntegratedModel(LatentModelIntegrationType type, double correlSqr, int n, TInit ini)
            : base(correlSqr, n, ini) => integration = Create(type, NumFactors);

        public IntegratedModel(LatentModelIntegrationType type, int n, double correlation, TInit ini)
            : base(n, correlation, ini) => integration = Create(type, NumFactors);

        protected override LMIntegration Integration => integration;

        private static LMIntegration Create(LatentModelIntegrationType type, int dimension) =>
            type == LatentModelIntegrationType.GaussianQuadrature
                ? new GaussianQuadLMIntegration(dimension, 25)
                : new MultidimLMIntegration([.. Enumerable.Range(0, dimension).Select(_ => (Integrator)new TrapezoidIntegral<Default>(1.0e-4, 20))], -35.0, 35.0);
    }

    private sealed class Inspect : IModelRead
    {
        public object? Read<TCopula, TInit>(IntegratedModel<TCopula, TInit> lm)
            where TCopula : struct, ICopulaPolicy<TCopula, TInit>
            where TInit : struct =>
            new object[]
            {
                lm.Size, lm.NumFactors, lm.NumTotalFactors, lm.FactorWeights.Select(r => r.ToArray()).ToArray(),
                lm.IdiosyncFctrs.ToArray(),
            };
    }

    private sealed class Members(VectorCase c) : IModelRead
    {
        public object? Read<TCopula, TInit>(IntegratedModel<TCopula, TInit> lm)
            where TCopula : struct, ICopulaPolicy<TCopula, TInit>
            where TInit : struct
        {
            double[] values = [.. c.Input("values").EnumerateArray().Select(VectorCase.Real)];
            double[] probs = [.. c.Input("probabilities").EnumerateArray().Select(VectorCase.Real)];
            double[][] samples = PortfolioInputs.MatrixOf(c.Input("samples"));
            double[][] uniforms = PortfolioInputs.MatrixOf(c.Input("uniforms"));
            int n = lm.Size, k = lm.NumFactors;
            var cy = new List<object>();
            var cz = new List<object>();
            var icy = new List<object>();
            var icz = new List<object>();
            var icd = new List<object>();
            foreach (double v in values)
            {
                for (int i = 0; i < n; i++)
                {
                    cy.Add(lm.CumulativeY(v, i));
                }

                cz.Add(lm.CumulativeZ(v));
            }

            foreach (double p in probs)
            {
                for (int i = 0; i < n; i++)
                {
                    icy.Add(lm.InverseCumulativeY(p, i));
                }

                icz.Add(lm.InverseCumulativeZ(p));
                for (int j = 0; j < k; j++)
                {
                    icd.Add(lm.InverseCumulativeDensity(p, j));
                }
            }

            object[] dens = [.. samples.Select(s => (object)lm.Density(s.AsSpan(0, k)))];
            object[] lv = [.. samples.SelectMany(s => Enumerable.Range(0, n).Select(i => (object)lm.LatentVarValue(s, i)))];
            object[] inv = [.. uniforms.Select(u => (object)lm.AllFactorCumulInverter(u).Select(x => (object)x).ToArray())];
            object[] corr = [.. Enumerable.Range(0, n).SelectMany(i => Enumerable.Range(0, n).Select(j => (object)lm.LatentVariableCorrel(i, j)))];
            return new object[] { cy.ToArray(), cz.ToArray(), icy.ToArray(), icz.ToArray(), icd.ToArray(), dens, inv, lv, corr };
        }
    }

    private sealed class Integrated(string f) : IModelRead
    {
        public object? Read<TCopula, TInit>(IntegratedModel<TCopula, TInit> lm)
            where TCopula : struct, ICopulaPolicy<TCopula, TInit>
            where TInit : struct => lm.IntegratedExpectedValue(Integrand(f));
    }

    // The polar sampler's first draw on a fresh sampler: its first systemic value, its dimension and its weight.
    private sealed class PolarFirst(long seed) : IModelRead
    {
        public object? Read<TCopula, TInit>(IntegratedModel<TCopula, TInit> lm)
            where TCopula : struct, ICopulaPolicy<TCopula, TInit>
            where TInit : struct
        {
            Sample<ReadOnlyMemory<double>> s = PolarStudentTFactorSampler.Create<MersenneTwisterUniformRng>(
                (TCopulaPolicy)(object)lm.Copula, seed).NextSequence();
            return new object[] { s.Value.Span[0], s.Value.Length, s.Weight };
        }
    }

    private sealed class Draws(string generator, long seed, int count) : IModelRead
    {
        public object? Read<TCopula, TInit>(IntegratedModel<TCopula, TInit> lm)
            where TCopula : struct, ICopulaPolicy<TCopula, TInit>
            where TInit : struct
        {
            IRsg sampler = generator switch
            {
                "MT" => new LatentModelFactorSampler<TCopula, TInit, RandomSequenceGenerator<MersenneTwisterUniformRng>>(
                    lm.Copula, RandomSequenceGenerator.Create<MersenneTwisterUniformRng>(lm.Copula.NumFactors, seed)),
                "Sobol" => LatentModelFactorSampler.Create<TCopula, TInit, SobolRsg>(lm.Copula, seed),
                _ => BoxMullerFactorSampler.Create<MersenneTwisterUniformRng>((GaussianCopulaPolicy)(object)lm.Copula, seed),
            };
            return Enumerable.Range(0, count).Select(_ => (object)Draw(sampler.NextSequence())).ToArray();
        }
    }
}
