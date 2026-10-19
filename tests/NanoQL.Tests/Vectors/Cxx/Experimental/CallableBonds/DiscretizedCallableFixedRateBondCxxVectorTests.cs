// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's discretized callable bond vectors,
/// <c>tests/vectors/cxx/experimental/callablebonds/discretizedcallablefixedratebond.json</c>: the asset built from
/// callable bonds' arguments and a curve, with and without a spread: its mandatory times, and its time, values and
/// present value after each rollback on Hull-White's tree.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscretizedCallableFixedRateBondCxxVectorTests
{
    [Fact]
    public void DiscretizedCallableFixedRateBondCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/callablebonds/discretizedcallablefixedratebond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CallableBond bond = CallableBondInputs.BondOf(c.Input("bond"), context);
            YieldTermStructure curve = ModelInputs.CurveOf(c.Input("curve"));
            double spread = Number(c.Input("spread"));
            var asset = new DiscretizedCallableFixedRateBond(bond, curve, context, spread);
            check.Output(c, "mandatoryTimes", asset.MandatoryTimes());

            OneFactorModelShortRateTree lattice = ModelInputs.LatticeOf(c.Input("lattice"));
            asset.Initialize(lattice, Number(c.Input("t0")));
            var times = new List<object?>();
            var values = new List<object?>();
            var presentValues = new List<object?>();
            foreach (double t in Numbers(c.Input("times")))
            {
                asset.Rollback(t);
                times.Add(asset.Time);
                values.Add(asset.Values.ToArray());
                presentValues.Add(asset.PresentValue());
            }

            check.Output(c, "rollbackTimes", times);
            check.Output(c, "rollbackValues", values);
            check.Output(c, "presentValue", presentValues);
        }

        check.Complete();
    }
}
