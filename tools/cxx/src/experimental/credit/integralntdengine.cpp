// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/nthtodefault.hpp, .cpp and integralntdengine.hpp, .cpp, which the wheel doesn't expose: an
// NthToDefault on a basket with its loss model (experimental/credit/portfoliospec.hpp's specs), priced by an
// IntegralNtdEngine. The swap is {"n", "side", "schedule" (termstructures/credit/creditspec.hpp's), "upfrontRate",
// "premiumRate", "dayCounter", "nominal", "settlePremiumAccrual"}.
//
//     NthToDefault::inspect          (basket, model, ntd): [rank, nominal, premium, side, basketSize, maturity]
//     NthToDefault::isExpired        (basket, model, ntd)
//     IntegralNtdEngine::calculate   (basket, model, ntd, step [length, units], discount curve): [NPV, fairPremium,
//                                     premiumLegNPV, protectionLegNPV, the additional results fairPremium,
//                                     premiumLegNPV and protectionLegNPV, null where
//                                     absent]

#include "experimental/credit/portfoliospec.hpp"
#include "termstructures/credit/creditspec.hpp"

#include <ql/experimental/credit/integralntdengine.hpp>
#include <ql/experimental/credit/nthtodefault.hpp>

#include <any>

using namespace QuantLib;

namespace harness {

    namespace {

        using credit::portfolio::key;
        using credit::portfolio::makeBoundBasket;
        using credit::portfolio::object;

        NthToDefault makeNtd(const ext::shared_ptr<Basket>& b, const json::value& v) {
            const std::string what = "an n-th-to-default swap";
            const json::object& o = object(v, what);
            return NthToDefault(b, static_cast<Size>(integer(key(o, "n", what))),
                                enumeration<Protection::Side>(key(o, "side", what)),
                                credit::makeSchedule(credit::schedule(key(o, "schedule", what))),
                                real(key(o, "upfrontRate", what)), real(key(o, "premiumRate", what)),
                                makeDayCounter(dayCounter(key(o, "dayCounter", what))), real(key(o, "nominal", what)),
                                boolean(key(o, "settlePremiumAccrual", what)));
        }

        const Register unit({
            {"NthToDefault::inspect",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 json::value b = a[0], m = a[1], n = a[2];
                 return [=] {
                     NthToDefault x = makeNtd(makeBoundBasket(b, m), n);
                     json::array out;
                     out.emplace_back(static_cast<std::int64_t>(x.rank()));
                     out.emplace_back(x.nominal());
                     out.emplace_back(x.premium());
                     out.emplace_back(static_cast<std::int64_t>(x.side()));
                     out.emplace_back(static_cast<std::int64_t>(x.basketSize()));
                     out.push_back(result(x.maturity()));
                     return json::value(std::move(out));
                 };
             }},
            {"NthToDefault::isExpired",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 json::value b = a[0], m = a[1], n = a[2];
                 return [=] { return json::value(makeNtd(makeBoundBasket(b, m), n).isExpired()); };
             }},
            {"IntegralNtdEngine::calculate",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 json::value b = a[0], m = a[1], n = a[2], step = a[3], disc = a[4];
                 return [=] {
                     NthToDefault x = makeNtd(makeBoundBasket(b, m), n);
                     x.setPricingEngine(ext::make_shared<IntegralNtdEngine>(
                         credit::portfolio::period(step), credit::makeDiscount(credit::discount(disc))));
                     json::array out;
                     out.emplace_back(x.NPV());
                     out.emplace_back(x.fairPremium());
                     out.emplace_back(x.premiumLegNPV());
                     out.emplace_back(x.protectionLegNPV());
                     const auto& extra = x.additionalResults();
                     for (const char* k : {"fairPremium", "premiumLegNPV", "protectionLegNPV"}) {
                         auto it = extra.find(k);
                         if (it == extra.end())
                             out.emplace_back(nullptr);
                         else
                             out.emplace_back(std::any_cast<Real>(it->second));
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
