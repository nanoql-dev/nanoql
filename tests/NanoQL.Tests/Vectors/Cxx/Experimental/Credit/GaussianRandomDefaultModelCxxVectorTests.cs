// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Gaussian random default model vectors,
/// <c>tests/vectors/cxx/experimental/credit/randomdefaultmodel.json</c>: the pool's default times after each sequence,
/// with a reset, and the default maximum time QuantLib refuses.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GaussianRandomDefaultModelCxxVectorTests
{
    [Fact]
    public void GaussianRandomDefaultModelCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/randomdefaultmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            check.Output(c, "sequences", () =>
            {
                Pool pool = PortfolioInputs.PoolOf(a[0], c.Context());
                var model = new GaussianRandomDefaultModel(pool, pool.DefaultKeys, PortfolioInputs.OneFactorCopulaOf(a[1]), VectorCase.Real(a[2]), a[3].GetInt64());
                int n = a[5].GetInt32();
                int resetAfter = a[6].ValueKind == JsonValueKind.Null ? -1 : a[6].GetInt32();
                var sequences = new List<double[]>();
                for (int k = 0; k < n; k++)
                {
                    if (a[4].ValueKind == JsonValueKind.Null)
                    {
                        model.NextSequence();
                    }
                    else
                    {
                        model.NextSequence(VectorCase.Real(a[4]));
                    }

                    sequences.Add([.. model.Pool.Names.Select(model.Pool.GetTime)]);
                    if (k == resetAfter)
                    {
                        model.Reset();
                    }
                }

                return sequences.ToArray();
            });
        }

        check.Complete();
    }
}
