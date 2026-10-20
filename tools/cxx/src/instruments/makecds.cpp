// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/instruments/makecds.hpp with the constructor and setters the wheel doesn't bind. "MakeCreditDefaultSwap::build"
// takes a builder spec {"tenor" ([length, units]), "termDate" or "schedule" (termstructures/credit/creditspec.hpp's),
// "runningSpread", "setters": [[name, value], ...]}, each setter called in order on a builder made inside the call:
// side, nominal, upfrontRate, couponTenor, dateGenerationRule, convention, dayCounter, settleAccrual,
// payAtDefaultTime, protectionStart, upfrontDate, claim ("FaceValueClaim"), lastPeriodDayCounter, rebateAccrual,
// tradeDate and cashSettlementDays; the result is the swap's inspectors.

#include "termstructures/credit/creditspec.hpp"

#include <ql/instruments/claim.hpp>
#include <ql/instruments/makecds.hpp>

#include <optional>
#include <string>
#include <utility>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct BuilderSpec {
            std::optional<std::pair<Integer, TimeUnit>> tenor;
            std::optional<DateSpec> termDate;
            std::optional<credit::ScheduleSpec> schedule;
            Real runningSpread = 0.0;
            std::vector<std::pair<std::string, json::value>> setters;
        };

        std::pair<Integer, TimeUnit> period(const json::value& v) {
            if (!v.is_array() || v.get_array().size() != 2)
                throw InvalidParams("expected a period as [length, units], got " + json::serialize(v));
            return {integer(v.get_array()[0]), enumeration<TimeUnit>(v.get_array()[1])};
        }

        BuilderSpec builder(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a MakeCreditDefaultSwap spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            BuilderSpec s;
            int starts = 0;
            for (const auto& kv : o) {
                const std::string k(kv.key());
                if (k == "tenor") {
                    s.tenor = period(kv.value());
                    ++starts;
                } else if (k == "termDate") {
                    s.termDate = date(kv.value());
                    ++starts;
                } else if (k == "schedule") {
                    s.schedule = credit::schedule(kv.value());
                    ++starts;
                } else if (k == "runningSpread") {
                    s.runningSpread = real(kv.value());
                } else if (k == "setters") {
                    if (!kv.value().is_array())
                        throw InvalidParams("expected the setters as an array");
                    for (const json::value& x : kv.value().get_array()) {
                        if (!x.is_array() || x.get_array().size() != 2)
                            throw InvalidParams("expected a setter as [name, value], got " + json::serialize(x));
                        s.setters.emplace_back(text(x.get_array()[0]), x.get_array()[1]);
                    }
                } else {
                    throw InvalidParams("MakeCreditDefaultSwap takes no key " + k);
                }
            }
            if (starts != 1)
                throw InvalidParams("MakeCreditDefaultSwap needs one of tenor, termDate and schedule");
            return s;
        }

        void apply(MakeCreditDefaultSwap& b, const std::string& name, const json::value& v) {
            if (name == "side")
                b.withSide(enumeration<Protection::Side>(v));
            else if (name == "nominal")
                b.withNominal(real(v));
            else if (name == "upfrontRate")
                b.withUpfrontRate(real(v));
            else if (name == "couponTenor") {
                auto p = period(v);
                b.withCouponTenor(Period(p.first, p.second));
            } else if (name == "dateGenerationRule")
                b.withDateGenerationRule(enumeration<DateGeneration::Rule>(v));
            else if (name == "convention")
                b.withConvention(enumeration<BusinessDayConvention>(v));
            else if (name == "dayCounter")
                b.withDayCounter(credit::makeDayCounter(credit::dayCounter(v)));
            else if (name == "settleAccrual")
                b.settleAccrual(boolean(v));
            else if (name == "payAtDefaultTime")
                b.payAtDefaultTime(boolean(v));
            else if (name == "protectionStart")
                b.withProtectionStart(makeDate(date(v)));
            else if (name == "upfrontDate")
                b.withUpfrontDate(makeDate(date(v)));
            else if (name == "claim") {
                if (text(v) != "FaceValueClaim")
                    throw InvalidParams("only a FaceValueClaim is built");
                b.withClaim(ext::make_shared<FaceValueClaim>());
            } else if (name == "lastPeriodDayCounter")
                b.withLastPeriodDayCounter(credit::makeDayCounter(credit::dayCounter(v)));
            else if (name == "rebateAccrual")
                b.rebateAccrual(boolean(v));
            else if (name == "tradeDate")
                b.withTradeDate(makeDate(date(v)));
            else if (name == "cashSettlementDays") {
                Integer n = integer(v);
                if (n == Null<Integer>() || n < 0)
                    throw InvalidParams("expected the cash settlement days as a Natural");
                b.withCashSettlementDays(static_cast<Natural>(n));
            } else
                throw InvalidParams("unknown setter " + name);
        }

        json::value build(const BuilderSpec& s) {
            std::optional<MakeCreditDefaultSwap> b;
            if (s.tenor)
                b.emplace(Period(s.tenor->first, s.tenor->second), s.runningSpread);
            else if (s.termDate)
                b.emplace(makeDate(*s.termDate), s.runningSpread);
            else
                b.emplace(credit::makeSchedule(*s.schedule), s.runningSpread);
            for (const auto& [name, value] : s.setters)
                apply(*b, name, value);
            ext::shared_ptr<CreditDefaultSwap> cds = *b;
            return credit::inspectors(*cds);
        }

        const Register unit({
            // (builder)
            {"MakeCreditDefaultSwap::build",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 BuilderSpec s = builder(a[0]);
                 return [=] { return build(s); };
             }},
        });

    }

}
