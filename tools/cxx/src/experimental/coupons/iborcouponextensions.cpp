// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/coupons/quantocouponpricer.hpp, proxyibor.hpp and strippedcapflooredcoupon.hpp, with their .cpp
// files: the quanto Black pricer, the proxy index and the stripped capped/floored coupon, which the wheel doesn't bind.
// Each builds Euribor 6M on a flat continuous curve at the rate from the reference date on Actual/365 (Fixed), and a
// coupon of 100 fixing a TARGET year after the reference date for six months.
//
// "BlackIborQuantoCouponPricer::rates" takes (referenceDate, rate, capletVol, normal, shift, fxVol, correlation, cap,
// floor): the optionlet volatility constant, normal or shifted lognormal by the shift, the exchange rate's constant
// Black volatility; it returns {"swaplet", "caplet", "floorlet"}, the pricer's rates on the coupon.
//
// "ProxyIbor::conventions" takes (referenceDate, rate, gearing, spread, pastFixing): a proxy "PX" of Euribor 6M; it
// returns {"name", "fixingDate", "valueDate", "maturityDate", "pastFixing"}, the index's dates at the coupon's fixing
// and its own fixing read back at a past date. Its forecast, gearing × proxied × spread in QuantLib, isn't among them
// (the S-xrt plan's XRR5 (xxi)).
//
// "StrippedCappedFlooredCoupon::rates" takes (referenceDate, rate, capletVol, cap, floor), cap and floor null for
// none: a capped/floored Ibor coupon on QuantLib's Black pricer at a constant optionlet volatility; it returns
// {"underlying", "capped", "stripped", "legAmounts"}: the three rates, and a three-coupon leg's amounts after
// StrippedCappedFlooredCouponLeg; and the stripped coupon's inspectors, {"cap", "floor", "effectiveCap",
// "effectiveFloor"} (null for Null<Rate>()), {"isCap", "isFloor", "isCollar"} and its "convexityAdjustment".

#include "convert.hpp"

#include <ql/cashflows/capflooredcoupon.hpp>
#include <ql/cashflows/couponpricer.hpp>
#include <ql/cashflows/iborcoupon.hpp>
#include <ql/currencies/europe.hpp>
#include <ql/experimental/coupons/proxyibor.hpp>
#include <ql/experimental/coupons/quantocouponpricer.hpp>
#include <ql/experimental/coupons/strippedcapflooredcoupon.hpp>
#include <ql/indexes/ibor/euribor.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/volatility/optionlet/constantoptionletvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/target.hpp>
#include <ql/time/daycounters/actual360.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct Market {
            Handle<YieldTermStructure> curve;
            ext::shared_ptr<IborIndex> index;
            Date start, end;
        };

        Market market(const Date& reference, Real rate) {
            Market m;
            m.curve = Handle<YieldTermStructure>(ext::make_shared<FlatForward>(reference, rate, Actual365Fixed()));
            m.index = ext::make_shared<Euribor6M>(m.curve);
            m.start = TARGET().advance(reference, 1, Years);
            m.end = TARGET().advance(m.start, 6, Months);
            return m;
        }

        Handle<OptionletVolatilityStructure> optionletVol(const Date& reference, Real vol, bool normal, Real shift) {
            return Handle<OptionletVolatilityStructure>(ext::make_shared<ConstantOptionletVolatility>(
                reference, TARGET(), Following, vol, Actual365Fixed(), normal ? Normal : ShiftedLognormal, shift));
        }

        const Register unit({
            {"BlackIborQuantoCouponPricer::rates",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 DateSpec reference = date(a[0]);
                 Real rate = real(a[1]), capletVol = real(a[2]);
                 bool normal = boolean(a[3]);
                 Real shift = real(a[4]), fxVol = real(a[5]), correlation = real(a[6]), cap = real(a[7]),
                      floor = real(a[8]);
                 return [=] {
                     Date ref = makeDate(reference);
                     Settings::instance().evaluationDate() = ref;
                     Market m = market(ref, rate);
                     IborCoupon coupon(m.end, 100.0, m.start, m.end, 2, m.index);
                     auto pricer = ext::make_shared<BlackIborQuantoCouponPricer>(
                         Handle<BlackVolTermStructure>(
                             ext::make_shared<BlackConstantVol>(ref, TARGET(), fxVol, Actual365Fixed())),
                         Handle<Quote>(ext::make_shared<SimpleQuote>(correlation)),
                         optionletVol(ref, capletVol, normal, shift));
                     coupon.setPricer(pricer);
                     pricer->initialize(coupon);
                     json::object out;
                     out["swaplet"] = pricer->swapletRate();
                     out["caplet"] = pricer->capletRate(cap);
                     out["floorlet"] = pricer->floorletRate(floor);
                     return json::value(out);
                 };
             }},
            {"ProxyIbor::conventions",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 DateSpec reference = date(a[0]);
                 Real rate = real(a[1]), gearing = real(a[2]), spread = real(a[3]), pastFixing = real(a[4]);
                 return [=] {
                     Date ref = makeDate(reference);
                     Settings::instance().evaluationDate() = ref;
                     Market m = market(ref, rate);
                     ProxyIbor proxy("PX", 6 * Months, 2, EURCurrency(), TARGET(), ModifiedFollowing, false,
                                     Actual360(), Handle<Quote>(ext::make_shared<SimpleQuote>(gearing)), m.index,
                                     Handle<Quote>(ext::make_shared<SimpleQuote>(spread)));
                     Date fixingDate = proxy.fixingDate(m.start);
                     Date past = TARGET().advance(ref, -5, Days);
                     proxy.clearFixings();
                     proxy.addFixing(past, pastFixing);
                     json::object out;
                     out["name"] = proxy.name();
                     out["fixingDate"] = result(fixingDate);
                     out["valueDate"] = result(proxy.valueDate(fixingDate));
                     out["maturityDate"] = result(proxy.maturityDate(proxy.valueDate(fixingDate)));
                     out["pastFixing"] = proxy.fixing(past);
                     proxy.clearFixings();
                     return json::value(out);
                 };
             }},
            {"StrippedCappedFlooredCoupon::rates",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 DateSpec reference = date(a[0]);
                 Real rate = real(a[1]), capletVol = real(a[2]), cap = real(a[3]), floor = real(a[4]);
                 return [=] {
                     Date ref = makeDate(reference);
                     Settings::instance().evaluationDate() = ref;
                     Market m = market(ref, rate);
                     auto pricer = ext::make_shared<BlackIborCouponPricer>(optionletVol(ref, capletVol, false, 0.0));
                     auto underlying = ext::make_shared<IborCoupon>(m.end, 100.0, m.start, m.end, 2, m.index);
                     auto capped = ext::make_shared<CappedFlooredCoupon>(underlying, cap, floor);
                     capped->setPricer(pricer);
                     StrippedCappedFlooredCoupon stripped(capped);
                     json::object out;
                     out["underlying"] = underlying->rate();
                     out["capped"] = capped->rate();
                     out["stripped"] = stripped.rate();
                     auto orNull = [](Real x) { return x == Null<Real>() ? json::value(nullptr) : json::value(x); };
                     out["cap"] = orNull(stripped.cap());
                     out["floor"] = orNull(stripped.floor());
                     out["effectiveCap"] = orNull(stripped.effectiveCap());
                     out["effectiveFloor"] = orNull(stripped.effectiveFloor());
                     out["isCap"] = stripped.isCap();
                     out["isFloor"] = stripped.isFloor();
                     out["isCollar"] = stripped.isCollar();
                     out["convexityAdjustment"] = stripped.convexityAdjustment();
                     Leg leg;
                     for (Integer i = 0; i < 3; ++i) {
                         Date s = TARGET().advance(m.start, 6 * i, Months), e = TARGET().advance(s, 6, Months);
                         auto c = ext::make_shared<CappedFlooredCoupon>(
                             ext::make_shared<IborCoupon>(e, 100.0, s, e, 2, m.index), cap, floor);
                         c->setPricer(pricer);
                         leg.push_back(c);
                     }
                     json::array amounts;
                     for (const auto& cf : Leg(StrippedCappedFlooredCouponLeg(leg)))
                         amounts.emplace_back(cf->amount());
                     out["legAmounts"] = amounts;
                     return json::value(out);
                 };
             }},
        });

    }

}
