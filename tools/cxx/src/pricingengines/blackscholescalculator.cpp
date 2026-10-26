// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/blackscholescalculator.hpp, .cpp: the spot-based Greeks of BlackScholesCalculator, which the wheel
// doesn't expose.
//
// "BlackScholesCalculator::greeks" takes (payoff, spot, growth, stdDev, discount, maturity): the payoff [kind,
// optionType, strike, extra], a "PlainVanilla", "CashOrNothing" (extra its cash), "AssetOrNothing" or "Gap" (extra its
// second strike) payoff; spot, growth, stdDev and discount BlackScholesCalculator's arguments; maturity the time the
// thetas take. It returns {"value", "delta", "elasticity", "gamma", "theta", "thetaPerDay"}; the constructor's
// refusals are QuantLib's, their messages the errors'.

#include "convert.hpp"

#include <ql/instruments/payoffs.hpp>
#include <ql/pricingengines/blackscholescalculator.hpp>

#include <string>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"BlackScholesCalculator::greeks",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 if (!a[0].is_array() || a[0].get_array().size() != 4)
                     throw InvalidParams("expected [kind, optionType, strike, extra], got " + json::serialize(a[0]));
                 const json::array& p = a[0].get_array();
                 const std::string kind = text(p[0]);
                 if (kind != "PlainVanilla" && kind != "CashOrNothing" && kind != "AssetOrNothing" && kind != "Gap")
                     throw InvalidParams("unknown payoff " + kind);
                 const auto type = enumeration<Option::Type>(p[1]);
                 const Real strike = real(p[2]), extra = real(p[3]);
                 const Real spot = real(a[1]), growth = real(a[2]), stdDev = real(a[3]), discount = real(a[4]);
                 const Time maturity = real(a[5]);
                 return [=] {
                     ext::shared_ptr<StrikedTypePayoff> payoff;
                     if (kind == "PlainVanilla")
                         payoff = ext::make_shared<PlainVanillaPayoff>(type, strike);
                     else if (kind == "CashOrNothing")
                         payoff = ext::make_shared<CashOrNothingPayoff>(type, strike, extra);
                     else if (kind == "AssetOrNothing")
                         payoff = ext::make_shared<AssetOrNothingPayoff>(type, strike);
                     else
                         payoff = ext::make_shared<GapPayoff>(type, strike, extra);
                     BlackScholesCalculator bs(payoff, spot, growth, stdDev, discount);
                     return json::value(json::object{{"value", bs.value()}, {"delta", bs.delta()},
                                                     {"elasticity", bs.elasticity()}, {"gamma", bs.gamma()},
                                                     {"theta", bs.theta(maturity)},
                                                     {"thetaPerDay", bs.thetaPerDay(maturity)}});
                 };
             }},
        });

    }

}
