// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's risky asset swap option vectors,
/// <c>tests/vectors/cxx/experimental/credit/riskyassetswapoption.json</c>: <see cref="RiskyAssetSwapOption"/>'s value
/// on payer and receiver swaps, at expiries, market spreads and volatilities from 1e-9 (its zero volatility is in the
/// Divergences record).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RiskyAssetSwapOptionCxxVectorTests
{
    [Fact]
    public void RiskyAssetSwapOptionCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/riskyassetswapoption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            EvaluationContext context = c.Context();
            var option = new RiskyAssetSwapOption(
                RiskyAssetSwapCxxVectorTests.SwapOf(a, context), new Date(a[11].GetInt32()), VectorCase.Real(a[12]), VectorCase.Real(a[13]));
            check.Output(c, "NPV", () => option.Calculate(context).NPV);
        }

        check.Complete();
    }
}
