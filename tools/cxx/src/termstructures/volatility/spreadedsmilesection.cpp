// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/spreadedsmilesection.hpp, .cpp: SpreadedSmileSection, which the wheel builds only
// inside a SpreadedSwaptionVolatility, over a section spec (smilesectionspec.hpp) and a spread, both built inside the
// call. A spreaded section is {"underlying": <section spec>, "spread": <real>}, the spread a SimpleQuote's value.
// Every member QuantLib forwards to the underlying section, the volatility it spreads, and SmileSection's members
// over them: the variance, the option and digital prices, the vega and the density. The volatility converted to
// another type or shift isn't one: SpreadedSmileSection default-constructs its base, so the conversion compares the
// type wanted with an uninitialized one.

#include "convert.hpp"
#include "termstructures/volatility/smilesectionspec.hpp"

#include <ql/handle.hpp>
#include <ql/option.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/termstructures/volatility/spreadedsmilesection.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        using termstructures::makeSmileSection;
        using termstructures::nullableReal;
        using termstructures::SmileSectionSpec;

        struct SpreadedSpec {
            SmileSectionSpec underlying;
            Real spread;
        };

        SpreadedSpec spreaded(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected a spreaded smile section spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const json::value* u = o.if_contains("underlying");
            const json::value* s = o.if_contains("spread");
            if (u == nullptr || s == nullptr || o.size() != 2)
                throw InvalidParams("a spreaded smile section spec is {underlying, spread}: " + json::serialize(v));
            return {termstructures::smileSection(*u), real(*s)};
        }

        ext::shared_ptr<SmileSection> makeSpreaded(const SpreadedSpec& s) {
            return ext::make_shared<SpreadedSmileSection>(makeSmileSection(s.underlying),
                                                          Handle<Quote>(ext::make_shared<SimpleQuote>(s.spread)));
        }

        // A member of the section alone: (spec).
        Call whole(const Args& a, json::value (*f)(const SmileSection&)) {
            arity(a, 1);
            SpreadedSpec s = spreaded(a[0]);
            return [=] { return f(*makeSpreaded(s)); };
        }

        // A member at a strike: (spec, strike).
        Call atStrike(const Args& a, Real (*f)(const SmileSection&, Real)) {
            arity(a, 2);
            SpreadedSpec s = spreaded(a[0]);
            Real k = real(a[1]);
            return [=] { return json::value(f(*makeSpreaded(s), k)); };
        }

        const Register unit({
            {"SpreadedSmileSection::minStrike",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return json::value(s.minStrike()); }); }},
            {"SpreadedSmileSection::maxStrike",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return json::value(s.maxStrike()); }); }},
            {"SpreadedSmileSection::atmLevel",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return nullableReal(s.atmLevel()); }); }},
            {"SpreadedSmileSection::exerciseDate",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return result(s.exerciseDate()); }); }},
            {"SpreadedSmileSection::exerciseTime",
             [](const Args& a) {
                 return whole(a, [](const SmileSection& s) { return json::value(s.exerciseTime()); });
             }},
            // The day counter's name, or null for QuantLib's empty one.
            {"SpreadedSmileSection::dayCounter",
             [](const Args& a) {
                 return whole(a, [](const SmileSection& s) {
                     return s.dayCounter().empty() ? json::value(nullptr) : json::value(s.dayCounter().name());
                 });
             }},
            {"SpreadedSmileSection::referenceDate",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return result(s.referenceDate()); }); }},
            {"SpreadedSmileSection::volatilityType",
             [](const Args& a) {
                 return whole(a, [](const SmileSection& s) {
                     return json::value(static_cast<int>(s.volatilityType()));
                 });
             }},
            {"SpreadedSmileSection::shift",
             [](const Args& a) { return whole(a, [](const SmileSection& s) { return json::value(s.shift()); }); }},
            {"SpreadedSmileSection::volatility",
             [](const Args& a) {
                 return atStrike(a, [](const SmileSection& s, Real k) { return s.volatility(k); });
             }},
            {"SpreadedSmileSection::variance",
             [](const Args& a) { return atStrike(a, [](const SmileSection& s, Real k) { return s.variance(k); }); }},
            // (spec, strike, option type, discount)
            {"SpreadedSmileSection::optionPrice",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SpreadedSpec s = spreaded(a[0]);
                 Real k = real(a[1]);
                 auto type = enumeration<Option::Type>(a[2]);
                 Real discount = real(a[3]);
                 return [=] { return json::value(makeSpreaded(s)->optionPrice(k, type, discount)); };
             }},
            // (spec, strike, option type, discount, gap)
            {"SpreadedSmileSection::digitalOptionPrice",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 SpreadedSpec s = spreaded(a[0]);
                 Real k = real(a[1]);
                 auto type = enumeration<Option::Type>(a[2]);
                 Real discount = real(a[3]);
                 Real gap = real(a[4]);
                 return [=] { return json::value(makeSpreaded(s)->digitalOptionPrice(k, type, discount, gap)); };
             }},
            // (spec, strike, discount)
            {"SpreadedSmileSection::vega",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 SpreadedSpec s = spreaded(a[0]);
                 Real k = real(a[1]);
                 Real discount = real(a[2]);
                 return [=] { return json::value(makeSpreaded(s)->vega(k, discount)); };
             }},
            // (spec, strike, discount, gap)
            {"SpreadedSmileSection::density",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 SpreadedSpec s = spreaded(a[0]);
                 Real k = real(a[1]);
                 Real discount = real(a[2]);
                 Real gap = real(a[3]);
                 return [=] { return json::value(makeSpreaded(s)->density(k, discount, gap)); };
             }},
        });

    }

}
