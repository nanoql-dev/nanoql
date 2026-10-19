// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/interestrate.hpp. An InterestRate argument is the spec {"rate", "dayCounter", "compounding", "frequency"}, the
// vectors' input names; an InterestRate result is {"rate", "dayCounter" (its name), "compounding", "frequency"}.

#include "convert.hpp"

#include <ql/interestrate.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct InterestRateSpec {
            Real rate;
            DayCounterSpec dayCounter;
            Compounding compounding;
            Frequency frequency;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("an InterestRate spec needs ") + name);
            return *v;
        }

        InterestRateSpec interestRate(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected an InterestRate spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            for (const auto& kv : o) {
                if (kv.key() != "rate" && kv.key() != "dayCounter" && kv.key() != "compounding" &&
                    kv.key() != "frequency")
                    throw InvalidParams("InterestRate takes no key " + std::string(kv.key()));
            }
            return {real(key(o, "rate")), dayCounter(key(o, "dayCounter")),
                    enumeration<Compounding>(key(o, "compounding")), enumeration<Frequency>(key(o, "frequency"))};
        }

        InterestRate make(const InterestRateSpec& s) {
            return {s.rate, makeDayCounter(s.dayCounter), s.compounding, s.frequency};
        }

        json::value rateResult(const InterestRate& r) {
            return json::object{{"rate", r.rate()},
                                {"dayCounter", r.dayCounter().name()},
                                {"compounding", static_cast<int>(r.compounding())},
                                {"frequency", static_cast<int>(r.frequency())}};
        }

        const Register unit({
            {"InterestRate",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 InterestRateSpec s{real(a[0]), dayCounter(a[1]), enumeration<Compounding>(a[2]),
                                    enumeration<Frequency>(a[3])};
                 return [=] { return rateResult(make(s)); };
             }},
            {"InterestRate::frequency",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InterestRateSpec s = interestRate(a[0]);
                 return [=] { return json::value(static_cast<int>(make(s).frequency())); };
             }},
            {"InterestRate::compoundFactor(Time)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 InterestRateSpec s = interestRate(a[0]);
                 Time t = real(a[1]);
                 return [=] { return json::value(make(s).compoundFactor(t)); };
             }},
            {"InterestRate::discountFactor(Time)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 InterestRateSpec s = interestRate(a[0]);
                 Time t = real(a[1]);
                 return [=] { return json::value(make(s).discountFactor(t)); };
             }},
            {"InterestRate::discountFactorFirstDerivative(Time)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 InterestRateSpec s = interestRate(a[0]);
                 Time t = real(a[1]);
                 return [=] { return json::value(make(s).discountFactorFirstDerivative(t)); };
             }},
            {"InterestRate::discountFactorSecondDerivative(Time)",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 InterestRateSpec s = interestRate(a[0]);
                 Time t = real(a[1]);
                 return [=] { return json::value(make(s).discountFactorSecondDerivative(t)); };
             }},
            {"InterestRate::compoundFactor(Date,Date)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 InterestRateSpec s = interestRate(a[0]);
                 DateSpec d1 = date(a[1]), d2 = date(a[2]);
                 return [=] { return json::value(make(s).compoundFactor(makeDate(d1), makeDate(d2))); };
             }},
            {"InterestRate::equivalentRate(Compounding,Frequency,Time)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 InterestRateSpec s = interestRate(a[0]);
                 auto comp = enumeration<Compounding>(a[1]);
                 auto freq = enumeration<Frequency>(a[2]);
                 Time t = real(a[3]);
                 return [=] { return rateResult(make(s).equivalentRate(comp, freq, t)); };
             }},
            {"InterestRate::equivalentRate(DayCounter,Compounding,Frequency,Date,Date)",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 InterestRateSpec s = interestRate(a[0]);
                 DayCounterSpec dc = dayCounter(a[1]);
                 auto comp = enumeration<Compounding>(a[2]);
                 auto freq = enumeration<Frequency>(a[3]);
                 DateSpec d1 = date(a[4]), d2 = date(a[5]);
                 return [=] {
                     return rateResult(
                         make(s).equivalentRate(makeDayCounter(dc), comp, freq, makeDate(d1), makeDate(d2)));
                 };
             }},
            {"InterestRate::impliedRate(Real,DayCounter,Compounding,Frequency,Time)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 Real compound = real(a[0]);
                 DayCounterSpec dc = dayCounter(a[1]);
                 auto comp = enumeration<Compounding>(a[2]);
                 auto freq = enumeration<Frequency>(a[3]);
                 Time t = real(a[4]);
                 return [=] {
                     return rateResult(InterestRate::impliedRate(compound, makeDayCounter(dc), comp, freq, t));
                 };
             }},
            {"operator<<(InterestRate)",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 InterestRateSpec s = interestRate(a[0]);
                 return [=] { return json::value(streamed(make(s))); };
             }},
        });

    }

}
