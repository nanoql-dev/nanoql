// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Bates model vectors, <c>tests/vectors/cxx/models/equity/batesmodel.json</c>: the det-jump and
/// double-exponential models' parameters and accessors at QuantLib's default arguments and at others, the same after
/// <see cref="CalibratedModelExtensions.SetParams{TModel}"/> with the process the new model holds, and the
/// constructors' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BatesModelCxxVectorTests
{
    [Fact]
    public void BatesModelCxxVector()
    {
        var file = VectorFile.Load("cxx/models/equity/batesmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            double[] a = Doubles(c.Input("arguments"));
            double Arg(int i, double fallback) => i < a.Length ? a[i] : fallback;
            string type = c.Input("type").GetString()!;
            Func<HestonModel> make = type switch
            {
                "BatesDetJumpModel" => () => new BatesDetJumpModel(HestonCxxInputs.BatesProcessOf(c.Input("process")), Arg(0, 1.0), Arg(1, 0.1)),
                "BatesDoubleExpModel" => () => new BatesDoubleExpModel(
                    HestonCxxInputs.ProcessOf(c.Input("process")), Arg(0, 0.1), Arg(1, 0.1), Arg(2, 0.1), Arg(3, 0.5)),
                _ => () => new BatesDoubleExpDetJumpModel(
                    HestonCxxInputs.ProcessOf(c.Input("process")), Arg(0, 0.1), Arg(1, 0.1), Arg(2, 0.1), Arg(3, 0.5), Arg(4, 1.0), Arg(5, 0.1)),
            };
            if (!c.Expected.TryGetProperty("theta", out _))
            {
                check.Output(c, "params", () => make().Params().ToArray());
                continue;
            }

            HestonModel model = make();
            Read(check, c, model, string.Empty);
            var newParams = new Vector(Doubles(c.Input("newParams")));
            HestonModel after = model switch
            {
                BatesDetJumpModel m => m.SetParams(newParams),
                BatesDoubleExpDetJumpModel m => m.SetParams(newParams),
                BatesDoubleExpModel m => m.SetParams(newParams),
                _ => throw new InvalidOperationException(type),
            };
            Assert.Equal(model.GetType(), after.GetType());
            Read(check, c, after, "after");
            HestonProcess p = after.Process;
            check.Output(c, "afterProcessType", p is BatesProcess ? "BatesProcess" : "HestonProcess");
            double[] process = p is BatesProcess b
                ? [b.V0, b.Kappa, b.Theta, b.Sigma, b.Rho, b.Lambda, b.Nu, b.Delta]
                : [p.V0, p.Kappa, p.Theta, p.Sigma, p.Rho];
            check.Output(c, "afterProcess", process);
        }

        check.Complete();
    }

    private static void Read(VectorAssert check, VectorCase c, HestonModel m, string prefix)
    {
        string Name(string n) => prefix.Length == 0 ? n : prefix + char.ToUpperInvariant(n[0]) + n[1..];
        check.Output(c, Name("params"), m.Params().ToArray());
        check.Output(c, Name("theta"), m.Theta);
        check.Output(c, Name("kappa"), m.Kappa);
        check.Output(c, Name("sigma"), m.Sigma);
        check.Output(c, Name("rho"), m.Rho);
        check.Output(c, Name("v0"), m.V0);
        switch (m)
        {
            case BatesDetJumpModel d:
                check.Output(c, Name("nu"), d.Nu);
                check.Output(c, Name("delta"), d.Delta);
                check.Output(c, Name("lambda"), d.Lambda);
                check.Output(c, Name("kappaLambda"), d.KappaLambda);
                check.Output(c, Name("thetaLambda"), d.ThetaLambda);
                break;
            case BatesDoubleExpModel e:
                check.Output(c, Name("p"), e.P);
                check.Output(c, Name("nuDown"), e.NuDown);
                check.Output(c, Name("nuUp"), e.NuUp);
                check.Output(c, Name("lambda"), e.Lambda);
                if (e is BatesDoubleExpDetJumpModel f)
                {
                    check.Output(c, Name("kappaLambda"), f.KappaLambda);
                    check.Output(c, Name("thetaLambda"), f.ThetaLambda);
                }

                break;
        }
    }
}
