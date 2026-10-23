// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's affine-model curve vectors,
/// <c>tests/vectors/cxx/methods/finitedifferences/utilities/fdmaffinemodeltermstructure.json</c>:
/// <see cref="FdmAffineModelTermStructure"/>'s discount at times and dates, on Hull-White and G2 models at states of
/// their factors, dated at and after the model curve's reference date.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmAffineModelTermStructureCxxVectorTests
{
    [Fact]
    public void FdmAffineModelTermStructureCxxVector()
    {
        var file = VectorFile.Load("cxx/methods/finitedifferences/utilities/fdmaffinemodeltermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("curve"));
            double[] p = [.. c.Input("parameters").EnumerateArray().Select(x => x.GetDouble())];
            IAffineModel model = c.String("model") == "G2"
                ? new G2(curve, p[0], p[1], p[2], p[3], p[4])
                : new HullWhite(curve, p[0], p[1]);
            var state = new Vector([.. c.Input("state").EnumerateArray().Select(x => x.GetDouble())]);
            var ts = new FdmAffineModelTermStructure(
                state, new NullCalendar(), curve.DayCounter, new Date(c.Int("referenceDate")), curve.ReferenceDate, model);
            check.Output(c, "discount", c.Input("times").EnumerateArray().Select(t => (object?)ts.Discount(t.GetDouble())).ToArray());
            check.Output(
                c,
                "discountAt",
                c.Input("dates").EnumerateArray().Select(d => (object?)ts.Discount(new Date(d.GetInt32()))).ToArray());
        }

        check.Complete();
    }
}
