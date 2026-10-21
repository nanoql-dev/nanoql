// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The HestonModel vectors, <c>tests/vectors/models/equity/hestonmodel.json</c>: four processes' parameters through
/// the model's accessors and its process's spot, the parameters set to other values on the new model
/// <see cref="CalibratedModelExtensions.SetParams{TModel}"/> returns, and the constructor's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HestonModelVectorTests
{
    [Fact]
    public void HestonModelVector()
    {
        var file = VectorFile.Load("models/equity/hestonmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var model = new Lazy<HestonModel>(() => new HestonModel(HestonProcessVectorTests.HestonProcessOf(c.Input("process"))));
            check.Output(c, "params", Lazy(() => model.Value.Params().ToArray()));
            if (!c.Expected.TryGetProperty("theta", out _))
            {
                continue;
            }

            Read(check, c, model.Value, string.Empty);
            check.Output(c, "s0", model.Value.Process.S0);
            HestonModel after = model.Value.SetParams(new Vector(Doubles(c.Input("newParams"))));
            check.Output(c, "afterParams", after.Params().ToArray());
            Read(check, c, after, "after");
        }

        check.Complete();
    }

    private static void Read(VectorAssert check, VectorCase c, HestonModel m, string prefix)
    {
        string Name(string n) => prefix.Length == 0 ? n : prefix + char.ToUpperInvariant(n[0]) + n[1..];
        check.Output(c, Name("theta"), m.Theta);
        check.Output(c, Name("kappa"), m.Kappa);
        check.Output(c, Name("sigma"), m.Sigma);
        check.Output(c, Name("rho"), m.Rho);
        check.Output(c, Name("v0"), m.V0);
    }
}
