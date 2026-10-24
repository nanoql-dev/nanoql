// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/cashflows/conundrumpricer.hpp and lineartsrpricer.hpp: what the wheel doesn't bind of the CMS pricers.
//
//     MeanRevertingPricer::meanReversion            (pricer): the pricer's mean reversion
//     NumericHaganPricer::integrationLimits         (pricer, coupon, optionType, strike): [lowerLimit(), upperLimit(),
//                                                   stdDeviations()] after initialize(coupon) and capletPrice(strike)
//                                                   (a call) or floorletPrice(strike) (a put), which price the
//                                                   optionlet that sets them
//     MarketQuotedOptionPricer::operator()          (forward, expiry, swapTenor, volatility, strike, optionType,
//                                                   deflator)
//     GFunctionFactory::newGFunctionStandard        (q, delta, swapLength, xs): per x, [G, G', G'']
//     GFunctionFactory::newGFunctionExactYield      (coupon, xs)
//     GFunctionFactory::newGFunctionWithShifts      (coupon, meanReversion, xs)
//
// The pricers, coupons and volatilities are specs of cashflows/cmsspec.hpp.

#include "cashflows/cmsspec.hpp"
#include "convert.hpp"
#include "termstructures/volatility/optionlet/optionletvolspec.hpp"
#include "termstructures/volatility/swaption/swaptionvolmatrixspec.hpp"

#include <ql/cashflows/conundrumpricer.hpp>
#include <ql/cashflows/lineartsrpricer.hpp>
#include <ql/quotes/simplequote.hpp>

#include <string>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        using cms::coupon;
        using cms::CouponSpec;
        using cms::makeCoupon;
        using cms::makePricer;
        using cms::pricer;
        using cms::PricerSpec;

        json::value values(const ext::shared_ptr<GFunction>& g, const std::vector<Real>& xs) {
            json::array out;
            for (Real x : xs) {
                json::array row;
                row.emplace_back((*g)(x));
                row.emplace_back(g->firstDerivative(x));
                row.emplace_back(g->secondDerivative(x));
                out.emplace_back(std::move(row));
            }
            return out;
        }

        const Register unit({
            {"MeanRevertingPricer::meanReversion",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 PricerSpec p = pricer(a[0]);
                 return [=] {
                     auto m = ext::dynamic_pointer_cast<MeanRevertingPricer>(makePricer(p));
                     return json::value(m->meanReversion());
                 };
             }},
            {"NumericHaganPricer::integrationLimits",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 PricerSpec p = pricer(a[0]);
                 if (p.type != "NumericHaganPricer")
                     throw InvalidParams("expected a numeric Hagan pricer");
                 CouponSpec c = coupon(a[1]);
                 auto type = enumeration<Option::Type>(a[2]);
                 Real strike = real(a[3]);
                 return [=] {
                     auto pricer = ext::dynamic_pointer_cast<NumericHaganPricer>(makePricer(p));
                     auto cpn = makeCoupon(c);
                     // initialize is protected on HaganPricer and public on its base
                     static_cast<FloatingRateCouponPricer&>(*pricer).initialize(*cpn);
                     if (type == Option::Call)
                         pricer->capletPrice(strike);
                     else
                         pricer->floorletPrice(strike);
                     json::array out;
                     out.emplace_back(pricer->lowerLimit());
                     out.emplace_back(pricer->upperLimit());
                     out.emplace_back(pricer->stdDeviations());
                     return json::value(std::move(out));
                 };
             }},
            {"MarketQuotedOptionPricer::operator()",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Real forward = real(a[0]);
                 DateSpec expiry = date(a[1]);
                 termstructures::PeriodSpec swapTenor = termstructures::tenor(a[2]);
                 termstructures::SwaptionVolMatrixSpec vol = termstructures::swaptionVolMatrix(a[3]);
                 Real strike = real(a[4]);
                 auto type = enumeration<Option::Type>(a[5]);
                 Real deflator = real(a[6]);
                 return [=] {
                     MarketQuotedOptionPricer p(forward, makeDate(expiry), termstructures::makePeriod(swapTenor),
                                                termstructures::makeSwaptionVolMatrix(vol));
                     return json::value(p(strike, type, deflator));
                 };
             }},
            {"GFunctionFactory::newGFunctionStandard",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Integer q = integer(a[0]);
                 Real delta = real(a[1]);
                 Integer swapLength = integer(a[2]);
                 if (q <= 0 || swapLength <= 0)
                     throw InvalidParams("expected positive frequency and swap length");
                 std::vector<Real> xs = reals(a[3]);
                 return [=] {
                     return values(GFunctionFactory::newGFunctionStandard(q, delta, swapLength), xs);
                 };
             }},
            {"GFunctionFactory::newGFunctionExactYield",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 CouponSpec c = coupon(a[0]);
                 std::vector<Real> xs = reals(a[1]);
                 return [=] { return values(GFunctionFactory::newGFunctionExactYield(*makeCoupon(c)), xs); };
             }},
            {"GFunctionFactory::newGFunctionWithShifts",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 CouponSpec c = coupon(a[0]);
                 Real m = real(a[1]);
                 std::vector<Real> xs = reals(a[2]);
                 return [=] {
                     Handle<Quote> h(ext::make_shared<SimpleQuote>(m));
                     return values(GFunctionFactory::newGFunctionWithShifts(*makeCoupon(c), h), xs);
                 };
             }},
        });

    }

}
