// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Methods;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmLocalVolFwdOp vectors, <c>tests/vectors/methods/finitedifferences/operators/fdmlocalvolfwdop.json</c>:
/// <see cref="FdmLocalVolFwdOp"/> after <see cref="FdmLocalVolFwdOp.SetTime"/> at three time steps, over constant,
/// fixed and Dupire local volatilities on Black-Scholes, predefined and two-dimensional meshers; and the predefined
/// meshers' locations and distances (<see cref="Predefined1dMesher"/>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmLocalVolFwdOpVectorTests
{
    [Fact]
    public void FdmLocalVolFwdOpVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/operators/fdmlocalvolfwdop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            if (c.Expected.TryGetProperty("locations", out _))
            {
                CheckMesher1d(check, c);
                continue;
            }

            JsonElement spec = c.Input("op");
            FdmMesherComposite mesher = MesherOf(spec.GetProperty("mesher"), c.Context());
            YieldTermStructure r = CurveOf(spec.GetProperty("riskFree")), q = CurveOf(spec.GetProperty("dividend"));
            double spot = spec.GetProperty("spot").GetDouble();
            var op = new FdmLocalVolFwdOp(
                mesher, spot, r, q, LocalVolOf(spec.GetProperty("localVol"), r, q, spot, c), spec.GetProperty("direction").GetInt32());
            FdmBlackScholesFwdOpVectorTests.CheckOperator(check, c, op, mesher, spec.GetProperty("direction").GetInt32());
        }

        check.Complete();
    }

    private static LocalVolTermStructure LocalVolOf(JsonElement spec, YieldTermStructure r, YieldTermStructure q, double spot, VectorCase c) =>
        spec.GetProperty("type").GetString() switch
        {
            "Constant" => new LocalConstantVol(c.Context().EvaluationDate, spec.GetProperty("volatility").GetDouble(), new Actual365Fixed()),
            "Fixed" => FixedLocalVolSurfaceVectorTests.SurfaceOf(spec.GetProperty("surface")),
            _ => new LocalVolSurface(BlackVolOf(spec.GetProperty("volatility"), c.Context()), r, q, spot),
        };
}
