// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The callable-bond vectors, <c>tests/vectors/experimental/callablebonds/callablebond.json</c>: fixed-rate and
/// zero-coupon callable bonds on the tree engine with time steps and on the Black engine, their inspectors, prices,
/// option-adjusted spreads and implied volatilities (<see cref="CallableBondInputs"/>). QuantLib's
/// <c>testObservability</c> is replaced by the file's pair of zero-coupon bonds on a 3% curve and on a rebuilt 4% curve
/// with its own model, whose prices differ. A refused construction is the case's one output, "construct".
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CallableBondVectorTests
{
    [Fact]
    public void CallableBondVector()
    {
        var file = VectorFile.Load("experimental/callablebonds/callablebond");
        var check = new VectorAssert(file);
        var observability = new List<double?>();
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => CallableBondInputs.BondOf(c.Input("bond"), context));
                continue;
            }

            CallableBond bond = CallableBondInputs.BondOf(c.Input("bond"), context);
            Func<CallableBond, EvaluationContext, double, CallableBondResults> engine =
                CallableBondInputs.EngineOf(c.Input("engine"));
            CallableBondInputs.CheckBond(check, c, bond, context);
            CallableBondInputs.CheckEngine(check, c, bond, context, engine);
            if (c.Inputs.TryGetProperty("oas", out _))
            {
                CallableBondInputs.CheckOAS(check, c, bond, context, engine);
            }

            if (c.Inputs.TryGetProperty("implied", out _))
            {
                CallableBondInputs.CheckImplied(check, c, bond, context);
            }

            if (c.Name.StartsWith("observability /", StringComparison.Ordinal))
            {
                observability.Add(engine(bond, context, 0.0).NPV);
            }
        }

        check.Complete();

        // testObservability's replacement: a rebuilt curve and model reprice the bond
        Assert.Equal(2, observability.Count);
        Assert.NotEqual(observability[0], observability[1]);
    }
}
