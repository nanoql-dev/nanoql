// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The CMS specs (cashflows/cmsspec.hpp).

#include "cashflows/cmsspec.hpp"

#include <ql/cashflows/conundrumpricer.hpp>
#include <ql/cashflows/lineartsrpricer.hpp>
#include <ql/indexes/swap/euriborswap.hpp>
#include <ql/quotes/simplequote.hpp>

#include <initializer_list>

using namespace QuantLib;

namespace harness::cms {

    namespace {

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " needs " + name);
            return *v;
        }

        void onlyKeys(const json::object& o, std::initializer_list<const char*> keys, const std::string& what) {
            for (const auto& kv : o) {
                bool known = false;
                for (const char* k : keys)
                    known = known || kv.key() == k;
                if (!known)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            }
        }

        const json::object& object(const json::value& v, const std::string& what) {
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            return v.get_object();
        }

    }

    SwapIndexSpec swapIndex(const json::value& v) {
        const std::string what = "a swap index";
        const json::object& o = object(v, what);
        onlyKeys(o, {"type", "tenor", "forwarding", "discounting"}, what);
        if (key(o, "type", what) != "EuriborSwapIsdaFixA")
            throw InvalidParams("unknown swap index " + json::serialize(o.at("type")));
        SwapIndexSpec s;
        s.tenor = termstructures::tenor(key(o, "tenor", what));
        s.forwarding = indexes::curve(key(o, "forwarding", what));
        if (o.contains("discounting"))
            s.discounting = indexes::curve(o.at("discounting"));
        return s;
    }

    ext::shared_ptr<SwapIndex> makeSwapIndex(const SwapIndexSpec& s) {
        Period tenor = termstructures::makePeriod(s.tenor);
        if (s.discounting)
            return ext::make_shared<EuriborSwapIsdaFixA>(tenor, indexes::makeCurve(s.forwarding),
                                                         indexes::makeCurve(*s.discounting));
        return ext::make_shared<EuriborSwapIsdaFixA>(tenor, indexes::makeCurve(s.forwarding));
    }

    CouponSpec coupon(const json::value& v) {
        const std::string what = "a CMS coupon";
        const json::object& o = object(v, what);
        onlyKeys(o, {"payment", "start", "end", "fixingDays", "index", "gearing", "spread", "dayCounter", "inArrears"},
                 what);
        CouponSpec s;
        s.payment = date(key(o, "payment", what));
        s.start = date(key(o, "start", what));
        s.end = date(key(o, "end", what));
        s.fixingDays = integer(key(o, "fixingDays", what));
        s.index = swapIndex(key(o, "index", what));
        s.gearing = real(key(o, "gearing", what));
        s.spread = real(key(o, "spread", what));
        s.dayCounter = dayCounter(key(o, "dayCounter", what));
        s.inArrears = boolean(key(o, "inArrears", what));
        return s;
    }

    ext::shared_ptr<CmsCoupon> makeCoupon(const CouponSpec& s) {
        Date start = makeDate(s.start), end = makeDate(s.end);
        return ext::make_shared<CmsCoupon>(makeDate(s.payment), 1.0, start, end, s.fixingDays, makeSwapIndex(s.index),
                                           s.gearing, s.spread, start, end, makeDayCounter(s.dayCounter),
                                           s.inArrears);
    }

    PricerSpec pricer(const json::value& v) {
        const std::string what = "a CMS pricer";
        const json::object& o = object(v, what);
        onlyKeys(o, {"type", "model", "meanReversion", "volatility", "lowerLimit", "upperLimit", "precision"}, what);
        PricerSpec s;
        s.type = text(key(o, "type", what));
        if (s.type != "NumericHaganPricer" && s.type != "AnalyticHaganPricer" && s.type != "LinearTsrPricer")
            throw InvalidParams("unknown CMS pricer " + s.type);
        if (s.type != "LinearTsrPricer")
            s.model = integer(key(o, "model", what));
        s.meanReversion = real(key(o, "meanReversion", what));
        s.volatility = termstructures::swaptionVolMatrix(key(o, "volatility", what));
        if (s.type == "NumericHaganPricer") {
            s.lowerLimit = real(key(o, "lowerLimit", what));
            s.upperLimit = real(key(o, "upperLimit", what));
            s.precision = real(key(o, "precision", what));
        }
        return s;
    }

    ext::shared_ptr<CmsCouponPricer> makePricer(const PricerSpec& s) {
        Handle<SwaptionVolatilityStructure> vol(termstructures::makeSwaptionVolMatrix(s.volatility));
        Handle<Quote> m(ext::make_shared<SimpleQuote>(s.meanReversion));
        auto model = static_cast<GFunctionFactory::YieldCurveModel>(s.model);
        if (s.type == "NumericHaganPricer")
            return ext::make_shared<NumericHaganPricer>(vol, model, m, s.lowerLimit, s.upperLimit, s.precision);
        if (s.type == "AnalyticHaganPricer")
            return ext::make_shared<AnalyticHaganPricer>(vol, model, m);
        return ext::make_shared<LinearTsrPricer>(vol, m);
    }

}
