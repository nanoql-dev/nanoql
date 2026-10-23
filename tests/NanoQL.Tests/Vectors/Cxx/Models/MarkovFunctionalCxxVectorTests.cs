// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Markov-functional model vectors,
/// <c>tests/vectors/cxx/models/shortrate/onefactormodels/markovfunctional.json</c>: on six calibrations, to swaptions
/// and to caplets, with Kahale's, SABR's or no smile pretreatment and the adjustments, the members of
/// <see cref="MarkovFunctional"/> the wheel doesn't bind, its numeraire date and time, its arbitrage indices and its
/// model outputs; and on the first with its arbitrage indices forced, its numeraire date and time, arbitrage indices,
/// numeraire and zero bonds; each where stable (the S7 plan's S7R6).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MarkovFunctionalCxxVectorTests
{
    [Fact]
    public void MarkovFunctionalCxxVector()
    {
        var file = VectorFile.Load("cxx/models/shortrate/onefactormodels/markovfunctional");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            MarkovFunctional model = Gaussian1dInputs.MarkovFunctionalOf(c.Input("model"), context);
            if (c.Inputs.TryGetProperty("numerairePoints", out _))
            {
                Forced(c, model, check);
                continue;
            }

            MarkovFunctionalModelOutputs o = model.ModelOutputs(context);
            var outputs = new Dictionary<string, object?>
            {
                ["numeraireDate"] = model.NumeraireDate,
                ["numeraireTime"] = model.NumeraireTime,
                ["arbitrageIndices"] = model.ArbitrageIndices().Select(p => p is (int l, int r) ? new[] { l, r } : null).ToArray(),
                ["expiries"] = o.Expiries,
                ["tenors"] = o.Tenors.Select(p => new[] { p.Length, (int)p.Units }).ToArray(),
                ["atm"] = o.Atm,
                ["annuity"] = o.Annuity,
                ["adjustmentFactors"] = o.AdjustmentFactors,
                ["digitalsAdjustmentFactors"] = o.DigitalsAdjustmentFactors,
                ["messages"] = o.Messages,
                ["smileStrikes"] = o.SmileStrikes,
                ["marketRawCallPremium"] = o.MarketRawCallPremium,
                ["marketRawPutPremium"] = o.MarketRawPutPremium,
                ["marketCallPremium"] = o.MarketCallPremium,
                ["marketPutPremium"] = o.MarketPutPremium,
                ["modelCallPremium"] = o.ModelCallPremium,
                ["modelPutPremium"] = o.ModelPutPremium,
                ["marketVega"] = o.MarketVega,
                ["marketZerorate"] = o.MarketZerorate,
                ["modelZerorate"] = o.ModelZerorate,
            };
            foreach ((string name, object? value) in outputs)
            {
                if (c.Expected.TryGetProperty(name, out _))
                {
                    check.Output(c, name, value);
                }
            }
        }

        check.Complete();
    }

    // The forced model's numeraire date and time, arbitrage indices, and numeraire and zero bonds at the case's points.
    private static void Forced(VectorCase c, MarkovFunctional model, VectorAssert check)
    {
        double[][] numeraire = ModelInputs.Points(c, "numerairePoints");
        double[][] zerobond = ModelInputs.Points(c, "zerobondPoints");
        var outputs = new Dictionary<string, object?>
        {
            ["numeraireDate"] = model.NumeraireDate,
            ["numeraireTime"] = model.NumeraireTime,
            ["arbitrageIndices"] = model.ArbitrageIndices().Select(p => p is (int l, int r) ? new[] { l, r } : null).ToArray(),
            ["numeraire"] = numeraire.Select(p => model.Numeraire(p[0], p[1])).ToArray(),
            ["zerobond"] = zerobond.Select(p => model.Zerobond(p[0], p[1], p[2])).ToArray(),
        };
        foreach ((string name, object? value) in outputs)
        {
            if (c.Expected.TryGetProperty(name, out _))
            {
                check.Output(c, name, value);
            }
        }
    }
}
