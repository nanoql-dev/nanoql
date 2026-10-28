// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's default latent model vectors,
/// <c>tests/vectors/cxx/experimental/credit/defaultprobabilitylatentmodel.json</c>: default and constant-loss latent
/// models reset on a basket, their default probabilities, correlations, event counts, conditional probabilities and
/// recoveries.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DefaultProbabilityLatentModelCxxVectorTests
{
    [Fact]
    public void DefaultProbabilityLatentModelCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/defaultprobabilitylatentmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("model");
            Basket basket = PortfolioInputs.BasketOf(c.Input("basket"), context);
            if (c.Name == "another size")
            {
                check.Output(c, "probOfDefault", (Func<object?>)(() => ModelOf(spec).Reset(basket).ProbOfDefault(0, context.EvaluationDate + 365)));
                continue;
            }

            IModel m = ModelOf(spec).Reset(basket);
            Date[] dates = c.DateInputs("dates");
            int n = basket.Size;
            check.Output(c, "probOfDefault", Enumerable.Range(0, n).Select(i => (object)dates.Select(d => (object)m.ProbOfDefault(i, d)).ToArray()).ToArray());
            check.Output(c, "defaultCorrelation", Each(
                dates[1..].SelectMany(d => Enumerable.Range(0, n).SelectMany(i => Enumerable.Range(0, n).Select(j => (d, i, j)))),
                p => m.DefaultCorrelation(p.d, p.i, p.j)));
            check.Output(c, "probAtLeastNEvents", Each(
                Enumerable.Range(0, 6).SelectMany(k => dates.Select(d => (k, d))),
                p => m.ProbAtLeastNEvents(p.k, p.d)));
            double[] probs = c.Doubles("probabilities");
            double[][] factors = PortfolioInputs.MatrixOf(c.Input("factors"));
            check.Output(c, "conditional", Enumerable.Range(0, n).Select(i => (object)new object[]
            {
                probs.SelectMany(p => factors.Select(f => (object)m.ConditionalDefaultProbability(p, i, f))).ToArray(),
                probs.SelectMany(p => factors.Select(f => (object)m.ConditionalDefaultProbabilityInvP(m.InverseCumulativeY(p, i), i, f))).ToArray(),
            }).ToArray());
            if (m.Recoveries is { } r)
            {
                check.Output(c, "recoveries", Each(dates[..2], d => new object[]
                {
                    r.Select(x => (object)x).ToArray(),
                    Enumerable.Range(0, n).Select(i => (object)m.ConditionalRecovery(d, i)).ToArray(),
                    Enumerable.Range(0, n).Select(i => (object)m.ExpectedRecovery(d, i, basket.DefaultKeys[i])).ToArray(),
                }));
            }
        }

        check.Complete();
    }

    private static IModel ModelOf(JsonElement spec)
    {
        string kind = spec.GetProperty("kind").GetString()!;
        var type = (LatentModelIntegrationType)spec.GetProperty("integration").GetInt32();
        bool quote = spec.TryGetProperty("correlation", out JsonElement c);
        int nv = quote ? spec.GetProperty("nVariables").GetInt32() : 0;
        double[][] w = quote ? [] : PortfolioInputs.MatrixOf(spec.GetProperty("factorWeights"));
        double[] rec = kind == "ConstantLossLatentModel" ? [.. spec.GetProperty("recoveries").EnumerateArray().Select(VectorCase.Real)] : [];
        if (spec.GetProperty("copula").GetString() == "Gaussian")
        {
            return kind == "DefaultLatentModel"
                ? new Model<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(quote
                    ? new GaussianDefProbLM(c.GetDouble(), nv, type)
                    : new GaussianDefProbLM(w, type))
                : new Model<GaussianCopulaPolicy, GaussianCopulaPolicyInitTraits>(quote
                    ? new GaussianConstantLossLM(c.GetDouble(), rec, type, nv)
                    : new GaussianConstantLossLM(w, rec, type));
        }

        TCopulaPolicyInitTraits ini = PortfolioInputs.TraitsOf(spec.GetProperty("tOrders"));
        return kind == "DefaultLatentModel"
            ? new Model<TCopulaPolicy, TCopulaPolicyInitTraits>(quote
                ? new TDefProbLM(c.GetDouble(), nv, type, ini)
                : new TDefProbLM(w, type, ini))
            : new Model<TCopulaPolicy, TCopulaPolicyInitTraits>(quote
                ? new TConstantLossLM(c.GetDouble(), rec, type, nv, ini)
                : new TConstantLossLM(w, rec, type, ini));
    }

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];

    private interface IModel
    {
        double[]? Recoveries { get; }

        IModel Reset(Basket basket);

        double ProbOfDefault(int i, Date d);

        double DefaultCorrelation(Date d, int i, int j);

        double ProbAtLeastNEvents(int n, Date d);

        double ConditionalDefaultProbability(double p, int i, double[] m);

        double ConditionalDefaultProbabilityInvP(double inv, int i, double[] m);

        double InverseCumulativeY(double p, int i);

        double ConditionalRecovery(Date d, int i);

        double ExpectedRecovery(Date d, int i, DefaultProbKey key);
    }

    private sealed class Model<TCopula, TInit>(DefaultLatentModel<TCopula, TInit> m) : IModel
        where TCopula : struct, ICopulaPolicy<TCopula, TInit>
        where TInit : struct
    {
        public double[]? Recoveries => m is ConstantLossLatentModel<TCopula, TInit> c ? [.. c.Recoveries] : null;

        public IModel Reset(Basket basket) => new Model<TCopula, TInit>(m.ResetBasket(basket));

        public double ProbOfDefault(int i, Date d) => m.ProbOfDefault(i, d);

        public double DefaultCorrelation(Date d, int i, int j) => m.DefaultCorrelation(d, i, j);

        public double ProbAtLeastNEvents(int n, Date d) => m.ProbAtLeastNEvents(n, d);

        public double ConditionalDefaultProbability(double p, int i, double[] f) => m.ConditionalDefaultProbability(p, i, f);

        public double ConditionalDefaultProbabilityInvP(double inv, int i, double[] f) => m.ConditionalDefaultProbabilityInvP(inv, i, f);

        public double InverseCumulativeY(double p, int i) => m.InverseCumulativeY(p, i);

        public double ConditionalRecovery(Date d, int i) => ((ConstantLossLatentModel<TCopula, TInit>)m).ConditionalRecovery(d, i, [0.0]);

        public double ExpectedRecovery(Date d, int i, DefaultProbKey key) => ((ConstantLossLatentModel<TCopula, TInit>)m).ExpectedRecovery(d, i, key);
    }
}
