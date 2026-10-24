// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/swap/cvaswapengine.hpp: the counterparty-risk adjusted swap engine, which the wheel doesn't bind,
// set on a vanilla swap built inside the call:
//
//     CounterpartyAdjSwapEngine  (swap, discount, exposure, ctpty curve, ctpty recovery, investor curve or null,
//                                investor recovery): [NPV, fairRate]
//
// A swap is {"type", "nominal", "fixedSchedule", "fixedRate", "fixedDayCount", "floatSchedule", "index", "spread",
// "floatingDayCount"}, its schedules cashflows/couponspec.hpp's specs and its index {"type": "Euribor6M", "curve"},
// QuantLib's Euribor 6M on an indexes/indexspec.hpp curve: the engine builds its swaplets with MakeVanillaSwap, whose
// fixed leg's default tenor reads the index's currency; the discount curve an indexes/indexspec.hpp curve spec; the
// exposure {"kind": "blackVol" | "blackQuote" | "bachelier", "volatility"}: the engine's constructors on a volatility
// and on a volatility quote, and the one on a swaption engine with a BachelierSwaptionEngine on a constant normal
// volatility; the credit curves termstructures/credit/creditspec.hpp's specs, a null investor curve QuantLib's empty
// handle.

#include "cashflows/couponspec.hpp"
#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "termstructures/credit/creditspec.hpp"

#include <ql/indexes/ibor/euribor.hpp>
#include <ql/instruments/vanillaswap.hpp>
#include <ql/pricingengines/swap/cvaswapengine.hpp>
#include <ql/pricingengines/swaption/blackswaptionengine.hpp>
#include <ql/quotes/simplequote.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        const json::value& key(const json::object& o, const char* name, const std::string& what) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(what + " needs " + name);
            return *v;
        }

        struct SwapSpec {
            Swap::Type type = Swap::Payer;
            Real nominal = 0.0, fixedRate = 0.0, spread = 0.0;
            cashflows::ScheduleSpec fixedSchedule, floatSchedule;
            DayCounterSpec fixedDayCount, floatingDayCount;
            indexes::CurveSpec indexCurve;
        };

        SwapSpec swap(const json::value& v) {
            const std::string what = "a vanilla swap";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            SwapSpec s;
            s.type = enumeration<Swap::Type>(key(o, "type", what));
            s.nominal = real(key(o, "nominal", what));
            s.fixedSchedule = cashflows::schedule(key(o, "fixedSchedule", what));
            s.fixedRate = real(key(o, "fixedRate", what));
            s.fixedDayCount = dayCounter(key(o, "fixedDayCount", what));
            s.floatSchedule = cashflows::schedule(key(o, "floatSchedule", what));
            const json::value& index = key(o, "index", what);
            if (!index.is_object() || text(key(index.get_object(), "type", "an index")) != "Euribor6M")
                throw InvalidParams(what + "'s index is {\"type\": \"Euribor6M\", \"curve\"}, got " +
                                    json::serialize(index));
            s.indexCurve = indexes::curve(key(index.get_object(), "curve", "an index"));
            s.spread = real(key(o, "spread", what));
            s.floatingDayCount = dayCounter(key(o, "floatingDayCount", what));
            return s;
        }

        struct ExposureSpec {
            std::string kind;
            Volatility volatility = 0.0;
        };

        ExposureSpec exposure(const json::value& v) {
            const std::string what = "an exposure model";
            if (!v.is_object())
                throw InvalidParams("expected " + what + ", got " + json::serialize(v));
            const json::object& o = v.get_object();
            ExposureSpec s;
            s.kind = text(key(o, "kind", what));
            if (s.kind != "blackVol" && s.kind != "blackQuote" && s.kind != "bachelier")
                throw InvalidParams("unknown exposure model " + s.kind);
            s.volatility = real(key(o, "volatility", what));
            return s;
        }

        const Register unit({
            {"CounterpartyAdjSwapEngine",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 SwapSpec s = swap(a[0]);
                 indexes::CurveSpec discount = indexes::curve(a[1]);
                 ExposureSpec e = exposure(a[2]);
                 credit::CurveSpec ctpty = credit::curve(a[3]);
                 Real ctptyRecovery = real(a[4]);
                 bool noInvestor = a[5].is_null();
                 credit::CurveSpec investor = noInvestor ? credit::CurveSpec() : credit::curve(a[5]);
                 Real investorRecovery = real(a[6]);
                 return [=] {
                     VanillaSwap sw(s.type, s.nominal, cashflows::makeSchedule(s.fixedSchedule), s.fixedRate,
                                    makeDayCounter(s.fixedDayCount), cashflows::makeSchedule(s.floatSchedule),
                                    ext::make_shared<Euribor6M>(indexes::makeCurve(s.indexCurve)), s.spread,
                                    makeDayCounter(s.floatingDayCount));
                     Handle<YieldTermStructure> disc = indexes::makeCurve(discount);
                     Handle<DefaultProbabilityTermStructure> c(credit::makeCurve(ctpty));
                     Handle<DefaultProbabilityTermStructure> i =
                         noInvestor ? Handle<DefaultProbabilityTermStructure>()
                                    : Handle<DefaultProbabilityTermStructure>(credit::makeCurve(investor));
                     ext::shared_ptr<PricingEngine> engine;
                     if (e.kind == "blackVol") {
                         engine = ext::make_shared<CounterpartyAdjSwapEngine>(disc, e.volatility, c, ctptyRecovery, i,
                                                                              investorRecovery);
                     } else if (e.kind == "blackQuote") {
                         Handle<Quote> vol(ext::make_shared<SimpleQuote>(e.volatility));
                         engine = ext::make_shared<CounterpartyAdjSwapEngine>(disc, vol, c, ctptyRecovery, i,
                                                                              investorRecovery);
                     } else {
                         Handle<PricingEngine> swaptions(ext::make_shared<BachelierSwaptionEngine>(disc, e.volatility));
                         engine = ext::make_shared<CounterpartyAdjSwapEngine>(disc, swaptions, c, ctptyRecovery, i,
                                                                              investorRecovery);
                     }
                     sw.setPricingEngine(engine);
                     json::array out;
                     out.emplace_back(sw.NPV());
                     out.emplace_back(sw.fairRate());
                     return json::value(out);
                 };
             }},
        });

    }

}
