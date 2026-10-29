// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/variancegamma/fftengine.hpp, .cpp and fftvanillaengine.hpp, .cpp, which the wheel doesn't expose.
// "FFTVanillaEngine" takes (spot, r, q, volatility, options, list, logStrikeSpacing): a Black-Scholes-Merton process on
// flat Act/365 (Fixed) forwards and a constant volatility from the evaluation date; European options, each
// [optionType, strike, expiryDays] on a plain vanilla payoff; the engine at that log-strike spacing, its precalculate
// called on every option first where list is true. It returns each option's NPV, or a throw for an option the engine
// refuses.

#include "convert.hpp"

#include <ql/exercise.hpp>
#include <ql/experimental/variancegamma/fftvanillaengine.hpp>
#include <ql/instruments/vanillaoption.hpp>
#include <ql/processes/blackscholesprocess.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/volatility/equityfx/blackconstantvol.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/calendars/nullcalendar.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct OptionSpec {
            Option::Type type;
            Real strike;
            Integer expiryDays;
        };

        const Register unit({
            {"FFTVanillaEngine",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 Real spot = real(a[0]);
                 Real r = real(a[1]);
                 Real q = real(a[2]);
                 Real vol = real(a[3]);
                 if (!a[4].is_array() || a[4].get_array().empty())
                     throw InvalidParams("expected a list of options, got " + json::serialize(a[4]));
                 std::vector<OptionSpec> specs;
                 for (const json::value& o : a[4].get_array()) {
                     if (!o.is_array() || o.get_array().size() != 3)
                         throw InvalidParams("expected [optionType, strike, expiryDays], got " + json::serialize(o));
                     const json::array& s = o.get_array();
                     specs.push_back({enumeration<Option::Type>(s[0]), real(s[1]), integer(s[2])});
                 }
                 bool list = boolean(a[5]);
                 Real spacing = real(a[6]);
                 return [=] {
                     const Date today = Settings::instance().evaluationDate();
                     const DayCounter dc = Actual365Fixed();
                     auto process = ext::make_shared<BlackScholesMertonProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(spot)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, q, dc)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, r, dc)),
                         Handle<BlackVolTermStructure>(ext::make_shared<BlackConstantVol>(today, NullCalendar(), vol, dc)));
                     auto engine = ext::make_shared<FFTVanillaEngine>(process, spacing);
                     std::vector<ext::shared_ptr<Instrument>> options;
                     for (const OptionSpec& s : specs) {
                         auto option = ext::make_shared<VanillaOption>(
                             ext::make_shared<PlainVanillaPayoff>(s.type, s.strike),
                             ext::make_shared<EuropeanExercise>(today + s.expiryDays));
                         option->setPricingEngine(engine);
                         options.push_back(option);
                     }
                     if (list)
                         engine->precalculate(options);
                     json::array out;
                     for (const ext::shared_ptr<Instrument>& option : options) {
                         try {
                             out.emplace_back(option->NPV());
                         } catch (const std::exception&) {
                             json::object thrown;
                             thrown["throws"] = true;
                             out.emplace_back(thrown);
                         }
                     }
                     return json::value(out);
                 };
             }},
        });

    }

}
