// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's spot-recovery latent model vectors,
/// <c>tests/vectors/cxx/experimental/credit/spotlosslatentmodel.json</c>: each name's conditional default
/// probabilities, expected recoveries, spot recoveries and recovery variables, and the refused models.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SpotRecoveryLatentModelCxxVectorTests
{
    [Fact]
    public void SpotRecoveryLatentModelCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/spotlosslatentmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Basket basket = PortfolioInputs.BasketOf(c.Input("basket"), context);
            if (c.Name == "refused models")
            {
                check.Output(c, "refused", Each(c.Input("models").EnumerateArray(), m => Refused(m, basket)));
                continue;
            }

            JsonElement spec = c.Input("model");
            var read = new Read(
                basket,
                Rows(c.Input("factors")),
                Reals(c.Input("probabilities")),
                Reals(c.Input("latents")),
                Rows(c.Input("samples")));
            check.Output(c, "members", Each(
                c.Input("dates").EnumerateArray().Select(d => new Date(d.GetInt32())),
                d => spec.GetProperty("copula").GetString() == "Gaussian"
                    ? read.Members(Gaussian(spec), d)
                    : read.Members(Student(spec), d)));
        }

        check.Complete();
    }

    private static object Refused(JsonElement spec, Basket basket)
    {
        if (spec.GetProperty("copula").GetString() == "Gaussian")
        {
            _ = Gaussian(spec).ResetBasket(basket);
        }
        else
        {
            _ = Student(spec).ResetBasket(basket);
        }

        return true;
    }

    private static GaussianSpotLossLM Gaussian(JsonElement spec) => new(
        Rows(spec.GetProperty("factorWeights")),
        Reals(spec.GetProperty("recoveries")),
        spec.GetProperty("modelA").GetDouble(),
        (LatentModelIntegrationType)spec.GetProperty("integration").GetInt32());

    private static TSpotLossLM Student(JsonElement spec) => new(
        Rows(spec.GetProperty("factorWeights")),
        Reals(spec.GetProperty("recoveries")),
        spec.GetProperty("modelA").GetDouble(),
        (LatentModelIntegrationType)spec.GetProperty("integration").GetInt32(),
        PortfolioInputs.TraitsOf(spec.GetProperty("tOrders")));

    private static double[][] Rows(JsonElement e) => [.. e.EnumerateArray().Select(Reals)];

    private static double[] Reals(JsonElement e) => [.. e.EnumerateArray().Select(VectorCase.Real)];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];

    // The harness's members, per name, at a date.
    private sealed record Read(Basket Basket, double[][] Factors, double[] Probs, double[] Latents, double[][] Samples)
    {
        public object[] Members<TCopula, TInit>(SpotRecoveryLatentModel<TCopula, TInit> model, Date d)
            where TCopula : struct, ICopulaPolicy<TCopula, TInit>
            where TInit : struct
        {
            SpotRecoveryLatentModel<TCopula, TInit> m = model.ResetBasket(Basket);
            var out_ = new object[Basket.Size];
            for (int i = 0; i < Basket.Size; i++)
            {
                var byDate = new List<object>();
                var byProb = new List<object>();
                var recovery = new List<object>();
                foreach (double[] f in Factors)
                {
                    byDate.Add(m.ConditionalDefaultProbability(d, i, f));
                    recovery.Add(m.ExpCondRecovery(d, i, f));
                    foreach (double p in Probs)
                    {
                        byProb.Add(m.ConditionalDefaultProbability(p, i, f));
                    }
                }

                object[] conditional = [.. Latents.Select(x => (object)m.ConditionalRecovery(x, i, d))];
                var rr = new List<object>();
                foreach (double[] s in Samples)
                {
                    rr.Add(m.LatentRRVarValue(s, i));
                }

                out_[i] = new object[] { byDate.ToArray(), byProb.ToArray(), recovery.ToArray(), conditional, rr.ToArray() };
            }

            return out_;
        }
    }
}
