// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.MarketModelCxxInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swap-forward mapping vectors, <c>tests/vectors/cxx/models/marketmodels/swapforwardmappings.json</c>:
/// <see cref="SwapForwardMappings"/>' annuities, swap derivatives, Jacobians and Z matrices on LIBOR, coterminal and
/// constant-maturity curve states (the coinitial Z matrix's entries from each row's diagonal on), and swaptions' implied
/// volatilities on evolutions holding their expiries.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwapForwardMappingsCxxVectorTests
{
    [Fact]
    public void SwapForwardMappingsCxxVector()
    {
        var file = VectorFile.Load("cxx/models/marketmodels/swapforwardmappings");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.String("call") == "swaptionVolatility")
            {
                MarketModel m = ModelOf(c.Input("model"));
                int[][] pairs = [.. c.Input("pairs").EnumerateArray().Select(Integers)];
                check.Output(c, "volatilities", pairs.Select(p => SwapForwardMappings.SwaptionImpliedVolatility(m, p[0], p[1])).ToArray());
                int last = m.NumberOfRates;
                check.Output(c, "reversed", Lazy(() => SwapForwardMappings.SwaptionImpliedVolatility(m, last, last)));
                continue;
            }

            double[] times = c.Doubles("rateTimes"), values = c.Doubles("values");
            CurveState cs = c.String("kind") switch
            {
                "LMMForwards" => new LMMCurveState(times).Also(s => s.SetOnForwardRates(values)),
                "Coterminal" => new CoterminalSwapCurveState(times).Also(s => s.SetOnCoterminalSwapRates(values)),
                _ => new CMSwapCurveState(times, c.Int("spanning")).Also(s => s.SetOnCMSwapRates(values)),
            };
            double d = c.Double("displacement");
            int[] spans = c.Ints("spans");
            int n = cs.NumberOfRates;
            var annuities = new List<double[]>();
            var derivatives = new List<double[]>();
            for (int s = 0; s < n; ++s)
            {
                for (int e = s + 1; e <= n; ++e)
                {
                    annuities.Add([.. new[] { 0, e, n }.Select(k => SwapForwardMappings.Annuity(cs, s, e, k))]);
                    derivatives.Add([.. Enumerable.Range(0, n).Select(f => SwapForwardMappings.SwapDerivative(cs, s, e, f))]);
                }
            }

            check.Output(c, "annuities", annuities);
            check.Output(c, "swapDerivatives", derivatives);
            check.Output(c, "coterminalJacobian", Rows(SwapForwardMappings.CoterminalSwapForwardJacobian(cs)));
            check.Output(c, "coterminalZed", Rows(SwapForwardMappings.CoterminalSwapZedMatrix(cs, d)));
            check.Output(c, "coinitialJacobian", Rows(SwapForwardMappings.CoinitialSwapForwardJacobian(cs)));
            Matrix coinitialZed = SwapForwardMappings.CoinitialSwapZedMatrix(cs, d);
            check.Output(c, "coinitialZedUpper", Enumerable.Range(0, n).Select(i => coinitialZed.Row(i)[i..].ToArray()).ToArray());
            check.Output(c, "cmJacobians", spans.Select(s => Rows(SwapForwardMappings.CmSwapForwardJacobian(cs, s))).ToArray());
            check.Output(c, "cmZeds", spans.Select(s => Rows(SwapForwardMappings.CmSwapZedMatrix(cs, s, d))).ToArray());
        }

        check.Complete();
    }
}
