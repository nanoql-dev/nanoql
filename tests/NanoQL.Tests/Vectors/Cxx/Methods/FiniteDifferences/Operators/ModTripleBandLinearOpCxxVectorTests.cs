// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's ModTripleBandLinearOp vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/operators/modtriplebandlinearop.json</c>:
/// <see cref="ModTripleBandLinearOp"/> copied from first and second derivatives and built empty, its bands written
/// through its accessors, read back, applied and solved by splitting.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ModTripleBandLinearOpCxxVectorTests
{
    [Fact]
    public void ModTripleBandLinearOpCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/operators/modtriplebandlinearop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            var mesher = new FdmMesherComposite(
                c.Input("locations").EnumerateArray()
                    .Select(x => (Fdm1dMesher)new Predefined1dMesher(x.EnumerateArray().Select(e => e.GetDouble()))));
            int direction = c.Input("direction").GetInt32();
            ModTripleBandLinearOp op = c.Input("derivative").GetString() switch
            {
                "first" => new ModTripleBandLinearOp(new FirstDerivativeOp(direction, mesher)),
                "second" => new ModTripleBandLinearOp(new SecondDerivativeOp(direction, mesher)),
                _ => new ModTripleBandLinearOp(direction, mesher),
            };
            foreach (JsonElement w in c.Input("writes").EnumerateArray())
            {
                int i = w[1].GetInt32();
                double value = w[2].GetDouble();
                switch (w[0].GetString())
                {
                    case "lower":
                        op.Lower(i) = value;
                        break;
                    case "diag":
                        op.Diag(i) = value;
                        break;
                    default:
                        op.Upper(i) = value;
                        break;
                }
            }

            int n = mesher.Layout.Size;
            check.Output(c, "lower", Enumerable.Range(0, n).Select(i => op.Lower(i)).ToArray());
            check.Output(c, "diag", Enumerable.Range(0, n).Select(i => op.Diag(i)).ToArray());
            check.Output(c, "upper", Enumerable.Range(0, n).Select(i => op.Upper(i)).ToArray());
            var r = new Vector(c.Input("r").EnumerateArray().Select(e => e.GetDouble()).ToArray());
            check.Output(c, "apply", op.Apply(r).ToArray());
            check.Output(c, "solveSplitting", op.SolveSplitting(r, c.Double("s")).ToArray());
        }

        check.Complete();
    }
}
