// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CMS-rate bond vectors, <c>tests/vectors/instruments/bonds/cmsratebond.json</c>: 8 bonds on Euribor swap indexes,
/// their coupons priced by Hagan's analytic pricer, with their stored fixings; each cash flow's fixing date and fixing
/// days, and the outputs every bond file shares (<see cref="BondInputs"/>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CmsRateBondVectorTests
{
    [Fact]
    public void CmsRateBondVector()
    {
        var file = VectorFile.Load("instruments/bonds/cmsratebond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var bond = (CmsRateBond)BondInputs.BondOf(c.Input("bond"), context);
            BondInputs.CheckStatic(check, c, bond, context);
            check.Output(c, "fixingDates", bond.Cashflows.Select(cf => (cf as FloatingRateCoupon)?.FixingDate()));
            check.Output(c, "fixingDays", bond.Cashflows.Select(cf => (cf as FloatingRateCoupon)?.FixingDays));
            BondInputs.CheckDated(check, c, bond, context);
            BondInputs.CheckEngine(check, c, bond, context);
        }

        check.Complete();
    }
}
