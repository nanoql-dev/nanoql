// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/credit/isdacdsengine.hpp with the settlement-date flag unset, which the wheel can't pass with the
// engine's options. "IsdaCdsEngine::results" takes a swap, a credit curve and a discount curve
// (termstructures/credit/creditspec.hpp's), the recovery rate, includeSettlementDateFlows (null for unset) and the
// numerical fix, accrual bias and forwards in coupon period as QuantLib's integers; the result is the swap's results,
// null where QuantLib's are "not available".

#include "termstructures/credit/creditspec.hpp"

#include <ql/pricingengines/credit/isdacdsengine.hpp>

#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            // (swap, credit, discount, recovery, includeSettlementDateFlows, numericalFix, accrualBias,
            //  forwardsInCouponPeriod)
            {"IsdaCdsEngine::results",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 credit::SwapSpec s = credit::swap(a[0]);
                 credit::CurveSpec c = credit::curve(a[1]);
                 credit::DiscountSpec d = credit::discount(a[2]);
                 Real recovery = real(a[3]);
                 std::optional<bool> include = optionalBoolean(a[4]);
                 auto fix = enumeration<IsdaCdsEngine::NumericalFix>(a[5]);
                 auto bias = enumeration<IsdaCdsEngine::AccrualBias>(a[6]);
                 auto forwards = enumeration<IsdaCdsEngine::ForwardsInCouponPeriod>(a[7]);
                 return [=] {
                     ext::shared_ptr<CreditDefaultSwap> cds = credit::makeSwap(s);
                     cds->setPricingEngine(ext::make_shared<IsdaCdsEngine>(
                         Handle<DefaultProbabilityTermStructure>(credit::makeCurve(c)), recovery,
                         credit::makeDiscount(d), include, fix, bias, forwards));
                     return credit::results(*cds);
                 };
             }},
        });

    }

}
