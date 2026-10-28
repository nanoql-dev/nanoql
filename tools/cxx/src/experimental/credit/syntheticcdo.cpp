// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/syntheticcdo.hpp, .cpp, midpointcdoengine.hpp, .cpp and integralcdoengine.hpp, .cpp, which
// the wheel doesn't expose: a SyntheticCDO on a basket with its loss model (experimental/credit/portfoliospec.hpp's
// specs), priced by either engine. The CDO is {"side", "schedule" (termstructures/credit/creditspec.hpp's),
// "upfrontRate", "runningRate", "dayCounter", "paymentConvention", "notional" (null for the tranche's)}.
//
//     SyntheticCDO::inspect          (basket, model, cdo): [leverageFactor, maturity, isExpired]
//     MidPointCDOEngine::calculate   (basket, model, cdo, discount curve)
//     IntegralCDOEngine::calculate   (basket, model, cdo, discount curve, step [length, units])
//     SyntheticCDO::expired          (basket, model, cdo, discount curve): on the mid-point engine, [isExpired, NPV,
//                                     premiumValue, protectionValue, remainingNotional, expectedTrancheLoss]
//
// each engine's result [NPV, premiumValue, protectionValue, fairPremium, premiumLegNPV, protectionLegNPV,
// remainingNotional, error, expectedTrancheLoss, the additional results premiumLegNPV and protectionLegNPV].

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "experimental/credit/portfoliospec.hpp"
#include "termstructures/credit/creditspec.hpp"

#include <ql/experimental/credit/integralcdoengine.hpp>
#include <ql/experimental/credit/midpointcdoengine.hpp>
#include <ql/experimental/credit/syntheticcdo.hpp>

#include <any>
#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        using credit::portfolio::key;
        using credit::portfolio::makeBoundBasket;
        using credit::portfolio::object;

        SyntheticCDO makeCdo(const ext::shared_ptr<Basket>& b, const json::value& v) {
            const std::string what = "a synthetic CDO";
            const json::object& o = object(v, what);
            std::optional<Real> notional;
            if (const json::value* n = o.if_contains("notional"); n != nullptr && !n->is_null())
                notional = real(*n);
            return SyntheticCDO(b, enumeration<Protection::Side>(key(o, "side", what)),
                                credit::makeSchedule(credit::schedule(key(o, "schedule", what))),
                                real(key(o, "upfrontRate", what)), real(key(o, "runningRate", what)),
                                makeDayCounter(dayCounter(key(o, "dayCounter", what))),
                                enumeration<BusinessDayConvention>(key(o, "paymentConvention", what)), notional);
        }

        json::value results(SyntheticCDO& x) {
            json::array out;
            out.emplace_back(x.NPV());
            out.emplace_back(x.premiumValue());
            out.emplace_back(x.protectionValue());
            out.emplace_back(x.fairPremium());
            out.emplace_back(x.premiumLegNPV());
            out.emplace_back(x.protectionLegNPV());
            out.emplace_back(x.remainingNotional());
            out.emplace_back(static_cast<std::int64_t>(x.error()));
            json::array etl;
            for (Real e : x.expectedTrancheLoss())
                etl.emplace_back(e);
            out.emplace_back(std::move(etl));
            const auto& extra = x.additionalResults();
            for (const char* k : {"premiumLegNPV", "protectionLegNPV"})
                out.emplace_back(std::any_cast<Real>(extra.at(k)));
            return json::value(std::move(out));
        }

        const Register unit({
            {"SyntheticCDO::inspect",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 json::value b = a[0], m = a[1], c = a[2];
                 return [=] {
                     SyntheticCDO x = makeCdo(makeBoundBasket(b, m), c);
                     json::array out;
                     out.emplace_back(x.leverageFactor());
                     out.push_back(result(x.maturity()));
                     out.emplace_back(x.isExpired());
                     return json::value(std::move(out));
                 };
             }},
            {"SyntheticCDO::expired",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 json::value b = a[0], m = a[1], c = a[2], disc = a[3];
                 return [=] {
                     SyntheticCDO x = makeCdo(makeBoundBasket(b, m), c);
                     x.setPricingEngine(
                         ext::make_shared<MidPointCDOEngine>(credit::makeDiscount(credit::discount(disc))));
                     json::array out;
                     out.emplace_back(x.isExpired());
                     out.emplace_back(x.NPV());
                     out.emplace_back(x.premiumValue());
                     out.emplace_back(x.protectionValue());
                     out.emplace_back(x.remainingNotional());
                     out.emplace_back(json::array(x.expectedTrancheLoss().begin(), x.expectedTrancheLoss().end()));
                     return json::value(std::move(out));
                 };
             }},
            {"MidPointCDOEngine::calculate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 json::value b = a[0], m = a[1], c = a[2], disc = a[3];
                 return [=] {
                     SyntheticCDO x = makeCdo(makeBoundBasket(b, m), c);
                     x.setPricingEngine(
                         ext::make_shared<MidPointCDOEngine>(credit::makeDiscount(credit::discount(disc))));
                     return results(x);
                 };
             }},
            {"IntegralCDOEngine::calculate",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 json::value b = a[0], m = a[1], c = a[2], disc = a[3], step = a[4];
                 return [=] {
                     SyntheticCDO x = makeCdo(makeBoundBasket(b, m), c);
                     x.setPricingEngine(ext::make_shared<IntegralCDOEngine>(
                         credit::makeDiscount(credit::discount(disc)), credit::portfolio::period(step)));
                     return results(x);
                 };
             }},
        });

    }

}
