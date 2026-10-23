// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Kahale smile section vectors, <c>tests/vectors/cxx/termstructures/volatility/kahalesmilesection.json</c>:
/// <see cref="KahaleSmileSection"/> over unshifted harness and SABR sections, plain, interpolated, exponentially
/// extrapolated and with the deletion of arbitrage points, on the default and a given moneyness grid: the core's
/// indices and strikes, the level, and at strikes around it the volatility and the call and put prices.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class KahaleSmileSectionCxxVectorTests
{
    [Fact]
    public void KahaleSmileSectionCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/kahalesmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("kahale");
            double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
            KahaleSmileSection Build() => new(
                SmileSectionInputs.SectionOf(spec.GetProperty("source"), c.Context()),
                SmileSectionInputs.NullableReal(spec.GetProperty("atm")),
                spec.GetProperty("interpolate").GetBoolean(),
                spec.GetProperty("exponentialExtrapolation").GetBoolean(),
                spec.GetProperty("deleteArbitragePoints").GetBoolean(),
                VolatilityInputs.Reals(spec.GetProperty("moneynessGrid")),
                IndexInputs.Real(spec.GetProperty("gap")),
                spec.GetProperty("forcedLeftIndex").GetInt32(),
                spec.GetProperty("forcedRightIndex").GetInt32());

            check.Output(c, "coreIndices", () =>
            {
                (int left, int right) = Build().CoreIndices();
                return new[] { left, right };
            });
            check.Output(c, "leftCoreStrike", () => Build().LeftCoreStrike());
            check.Output(c, "rightCoreStrike", () => Build().RightCoreStrike());
            check.Output(c, "atmLevel", () => Build().AtmLevel());
            check.Output(c, "volatility", AtStrikes(strikes, k => Build().Volatility(k)));
            check.Output(c, "call", AtStrikes(strikes, k => Build().OptionPrice(k, OptionType.Call, 1.0)));
            check.Output(c, "put", AtStrikes(strikes, k => Build().OptionPrice(k, OptionType.Put, 1.0)));
        }

        check.Complete();
    }

    // Each value computed when compared, so that a throw is checked.
    private static Func<object?>[] AtStrikes(double[] strikes, Func<double, double> f) =>
        [.. strikes.Select(k => (Func<object?>)(() => f(k)))];
}
