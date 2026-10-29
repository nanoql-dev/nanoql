// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The digital legs' naked option flag (the S-cms plan's CMR5 (viii)): QuantLib's <c>DigitalCmsLeg</c>,
/// <c>DigitalIborLeg</c> and <c>DigitalCmsSpreadLeg</c> leave <c>nakedOption_</c> uninitialized unless
/// <c>withNakedOption</c> is called (<c>digitalcmscoupon.cpp</c>, <c>digitaliborcoupon.cpp</c>,
/// <c>digitalcmsspreadcoupon.cpp</c>, their constructors), so a leg built without it reads an indeterminate value,
/// undefined behaviour. nanoql's legs default to <see langword="false"/>, the covered option, the coupon's own default.
/// QuantLib's check: its legs built with <c>withNakedOption(false)</c> (the C++ harness's vectors): on the
/// <c>cash calls at 5%</c> legs, the Ibor leg's first coupon pays 0.030579238011458898 and the CMS leg's
/// 0.06130050779731958, where the naked legs pay 0 and 0.010000000000000286.
/// </summary>
public class DigitalLegNakedOptionTests
{
    [Fact]
    public void TheIborAndCmsLegsDefaultToACoveredOption()
    {
        VectorCase ibor = VectorFile.Load("cxx/cashflows/digitaliborcoupon").Cases.Single(x => x.Name == "cash calls at 5%");
        EvaluationContext context = ibor.Context();
        DigitalIborLeg iborLeg = DigitalIborCouponCxxVectorTests.BuilderOf(ibor.Input("leg"), context);
        double[] rates = Rates(DigitalIborCouponCxxVectorTests.Priced(iborLeg, ibor.Input("leg"), context), context);
        Assert.Equal(Rates(DigitalIborCouponCxxVectorTests.Priced(iborLeg.WithNakedOption(false), ibor.Input("leg"), context), context), rates);
        Assert.NotEqual(Rates(DigitalIborCouponCxxVectorTests.Priced(iborLeg.WithNakedOption(), ibor.Input("leg"), context), context), rates);
        Assert.True(Math.Abs(rates[0] - 0.030579238011458898) <= 1e-15, $"{rates[0]}");

        VectorCase cms = VectorFile.Load("cxx/cashflows/digitalcmscoupon").Cases.Single(x => x.Name == "cash calls at 5%");
        context = cms.Context();
        DigitalCmsLeg cmsLeg = DigitalCmsCouponCxxVectorTests.BuilderOf(cms.Input("leg"), context);
        rates = Rates(DigitalCmsCouponCxxVectorTests.Priced(cmsLeg, cms.Input("leg"), context), context);
        Assert.Equal(Rates(DigitalCmsCouponCxxVectorTests.Priced(cmsLeg.WithNakedOption(false), cms.Input("leg"), context), context), rates);
        Assert.NotEqual(Rates(DigitalCmsCouponCxxVectorTests.Priced(cmsLeg.WithNakedOption(), cms.Input("leg"), context), context), rates);
        Assert.True(Math.Abs(rates[0] - 0.06130050779731958) <= 1e-14, $"{rates[0]}");
    }

    [Fact]
    public void TheCmsSpreadLegDefaultsToACoveredOption()
    {
        VectorCase c = VectorFile.Load("cxx/experimental/coupons/digitalcmsspreadcoupon").Cases.Single(x => x.Name == "cash calls at 5%");
        EvaluationContext context = c.Context();
        JsonElement spec = c.Input("leg");
        DigitalCmsSpreadLeg leg = DigitalCmsSpreadCouponCxxVectorTests.BuilderOf(spec, context);
        double[] rates = Rates(DigitalCmsSpreadCouponCxxVectorTests.Priced(leg, spec, context), context);
        Assert.Equal(Rates(DigitalCmsSpreadCouponCxxVectorTests.Priced(leg.WithNakedOption(false), spec, context), context), rates);
        Assert.NotEqual(Rates(DigitalCmsSpreadCouponCxxVectorTests.Priced(leg.WithNakedOption(), spec, context), context), rates);
    }

    private static double[] Rates(ImmutableArray<CashFlow> leg, EvaluationContext context) =>
        leg.OfType<DigitalCoupon>().Select(d => d.Rate(context)).ToArray();
}
