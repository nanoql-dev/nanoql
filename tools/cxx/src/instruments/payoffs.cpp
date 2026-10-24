// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/payoff.hpp and ql/instruments/payoffs.hpp: the members the wheel doesn't expose (name, description,
// CashOrNothingPayoff::cashPayoff, GapPayoff::secondStrike), and every member of the payoffs it doesn't build as
// QuantLib declares them (NullPayoff and SuperFundPayoff, not wrapped; SuperSharePayoff, whose wrapped constructor
// takes an option type where QuantLib's takes the strike), and ql/instruments/forward.hpp's ForwardTypePayoff, not
// wrapped. Each method builds its payoff from a spec of instruments/payoffspec.hpp inside the call, so that a
// constructor's refusal is QuantLib's.

#include "convert.hpp"
#include "instruments/payoffspec.hpp"

#include <ql/instruments/payoffs.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        // A payoff spec of the given type, for the members only that type declares.
        payoffs::PayoffSpec payoffOf(const json::value& v, const char* type) {
            payoffs::PayoffSpec s = payoffs::payoff(v);
            if (s.type != type)
                throw InvalidParams(std::string("expected a ") + type + " spec, got " + s.type);
            return s;
        }

        // A payoff spec of a type derived from TypePayoff or StrikedTypePayoff.
        payoffs::PayoffSpec typedPayoff(const json::value& v, bool striked) {
            payoffs::PayoffSpec s = payoffs::payoff(v);
            if (s.type == "NullPayoff" || (striked && s.type == "FloatingTypePayoff"))
                throw InvalidParams(s.type + " is no " + (striked ? "StrikedTypePayoff" : "TypePayoff"));
            return s;
        }

        template <class P>
        ext::shared_ptr<P> as(const payoffs::PayoffSpec& s) {
            return ext::dynamic_pointer_cast<P>(payoffs::makePayoff(s));
        }

        const Register unit({
            {"Payoff::name",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 payoffs::PayoffSpec s = payoffs::payoff(a[0]);
                 return [=] { return json::value(payoffs::makePayoff(s)->name()); };
             }},
            {"Payoff::description",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 payoffs::PayoffSpec s = payoffs::payoff(a[0]);
                 return [=] { return json::value(payoffs::makePayoff(s)->description()); };
             }},
            {"Payoff::operator()",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 payoffs::PayoffSpec s = payoffs::payoff(a[0]);
                 Real price = real(a[1]);
                 return [=] { return json::value((*payoffs::makePayoff(s))(price)); };
             }},
            {"TypePayoff::optionType",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 payoffs::PayoffSpec s = typedPayoff(a[0], false);
                 return [=] { return json::value(static_cast<int>(as<TypePayoff>(s)->optionType())); };
             }},
            {"StrikedTypePayoff::strike",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 payoffs::PayoffSpec s = typedPayoff(a[0], true);
                 return [=] { return json::value(as<StrikedTypePayoff>(s)->strike()); };
             }},
            {"FloatingTypePayoff::operator()(Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 payoffs::PayoffSpec s = payoffOf(a[0], "FloatingTypePayoff");
                 Real price = real(a[1]);
                 Real strike = real(a[2]);
                 return [=] { return json::value((*as<FloatingTypePayoff>(s))(price, strike)); };
             }},
            {"CashOrNothingPayoff::cashPayoff",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 payoffs::PayoffSpec s = payoffOf(a[0], "CashOrNothingPayoff");
                 return [=] { return json::value(as<CashOrNothingPayoff>(s)->cashPayoff()); };
             }},
            {"GapPayoff::secondStrike",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 payoffs::PayoffSpec s = payoffOf(a[0], "GapPayoff");
                 return [=] { return json::value(as<GapPayoff>(s)->secondStrike()); };
             }},
            {"SuperFundPayoff::secondStrike",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 payoffs::PayoffSpec s = payoffOf(a[0], "SuperFundPayoff");
                 return [=] { return json::value(as<SuperFundPayoff>(s)->secondStrike()); };
             }},
            {"SuperSharePayoff::secondStrike",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 payoffs::PayoffSpec s = payoffOf(a[0], "SuperSharePayoff");
                 return [=] { return json::value(as<SuperSharePayoff>(s)->secondStrike()); };
             }},
            {"SuperSharePayoff::cashPayoff",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 payoffs::PayoffSpec s = payoffOf(a[0], "SuperSharePayoff");
                 return [=] { return json::value(as<SuperSharePayoff>(s)->cashPayoff()); };
             }},
        });

    }

}
