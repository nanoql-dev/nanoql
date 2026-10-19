// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/blackformula.hpp: the 20 overloads the wheel doesn't wrap. On an option type, the forward
// derivatives (Black's and Bachelier's) and the three approximations of the implied standard deviation (Brenner and
// Subrahmanyan's with Corrado and Miller's, Chambers and Nawalkha's, Radoicic and Stefanica's); on a strike, the
// derivatives by the standard deviation (Black's first and second, Bachelier's) and Black's vega; and every payoff
// overload the wheel doesn't wrap: blackFormula, the forward derivatives, the three approximations, the implied
// standard deviation, the derivatives by the standard deviation and bachelierBlackFormula. A payoff is a
// PlainVanillaPayoff spec of instruments/payoffspec.hpp, built inside the call; the option type is Option::Type's
// integer, and every argument is passed, the defaults included. A null guess is Null<Real>().

#include "convert.hpp"
#include "instruments/payoffspec.hpp"

#include <ql/pricingengines/blackformula.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        Natural natural(const json::value& v) {
            if (!v.is_int64() || v.get_int64() < 0 || v.get_int64() > INT_MAX)
                throw InvalidParams("expected a Natural, got " + json::serialize(v));
            return static_cast<Natural>(v.get_int64());
        }

        // A function of (type, strike, forward, x, discount, displacement).
        template <class F>
        Method onType6(F f) {
            return [f](const Args& a) -> Call {
                arity(a, 6);
                auto type = enumeration<Option::Type>(a[0]);
                Real strike = real(a[1]), forward = real(a[2]), x = real(a[3]), discount = real(a[4]),
                     displacement = real(a[5]);
                return [=] { return json::value(f(type, strike, forward, x, discount, displacement)); };
            };
        }

        // A function of (payoff, forward, x, discount, displacement).
        template <class F>
        Method onPayoff5(F f) {
            return [f](const Args& a) -> Call {
                arity(a, 5);
                payoffs::PayoffSpec p = payoffs::plainVanillaPayoff(a[0]);
                Real forward = real(a[1]), x = real(a[2]), discount = real(a[3]), displacement = real(a[4]);
                return [=] {
                    return json::value(f(payoffs::makePlainVanillaPayoff(p), forward, x, discount, displacement));
                };
            };
        }

        // A function of (strike, forward, stdDev, discount, displacement).
        template <class F>
        Method onStrike5(F f) {
            return [f](const Args& a) -> Call {
                arity(a, 5);
                Real strike = real(a[0]), forward = real(a[1]), stdDev = real(a[2]), discount = real(a[3]),
                     displacement = real(a[4]);
                return [=] { return json::value(f(strike, forward, stdDev, discount, displacement)); };
            };
        }

        using PayoffPtr = ext::shared_ptr<PlainVanillaPayoff>;

        const Register unit({
            {"blackFormula(PlainVanillaPayoff,Real,Real,Real,Real)",
             onPayoff5([](const PayoffPtr& p, Real f, Real s, Real d, Real disp) {
                 return blackFormula(p, f, s, d, disp);
             })},
            {"blackFormulaForwardDerivative(Option::Type,Real,Real,Real,Real,Real)",
             onType6([](Option::Type t, Real k, Real f, Real s, Real d, Real disp) {
                 return blackFormulaForwardDerivative(t, k, f, s, d, disp);
             })},
            {"blackFormulaForwardDerivative(PlainVanillaPayoff,Real,Real,Real,Real)",
             onPayoff5([](const PayoffPtr& p, Real f, Real s, Real d, Real disp) {
                 return blackFormulaForwardDerivative(p, f, s, d, disp);
             })},
            {"blackFormulaImpliedStdDevApproximation(Option::Type,Real,Real,Real,Real,Real)",
             onType6([](Option::Type t, Real k, Real f, Real price, Real d, Real disp) {
                 return blackFormulaImpliedStdDevApproximation(t, k, f, price, d, disp);
             })},
            {"blackFormulaImpliedStdDevApproximation(PlainVanillaPayoff,Real,Real,Real,Real)",
             onPayoff5([](const PayoffPtr& p, Real f, Real price, Real d, Real disp) {
                 return blackFormulaImpliedStdDevApproximation(p, f, price, d, disp);
             })},
            {"blackFormulaImpliedStdDevChambers(Option::Type,Real,Real,Real,Real,Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 auto type = enumeration<Option::Type>(a[0]);
                 Real strike = real(a[1]), forward = real(a[2]), price = real(a[3]), atmPrice = real(a[4]),
                      discount = real(a[5]), displacement = real(a[6]);
                 return [=] {
                     return json::value(blackFormulaImpliedStdDevChambers(type, strike, forward, price, atmPrice,
                                                                          discount, displacement));
                 };
             }},
            {"blackFormulaImpliedStdDevChambers(PlainVanillaPayoff,Real,Real,Real,Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 payoffs::PayoffSpec p = payoffs::plainVanillaPayoff(a[0]);
                 Real forward = real(a[1]), price = real(a[2]), atmPrice = real(a[3]), discount = real(a[4]),
                      displacement = real(a[5]);
                 return [=] {
                     return json::value(blackFormulaImpliedStdDevChambers(
                         payoffs::makePlainVanillaPayoff(p), forward, price, atmPrice, discount, displacement));
                 };
             }},
            {"blackFormulaImpliedStdDevApproximationRS(Option::Type,Real,Real,Real,Real,Real)",
             onType6([](Option::Type t, Real k, Real f, Real price, Real d, Real disp) {
                 return blackFormulaImpliedStdDevApproximationRS(t, k, f, price, d, disp);
             })},
            {"blackFormulaImpliedStdDevApproximationRS(PlainVanillaPayoff,Real,Real,Real,Real)",
             onPayoff5([](const PayoffPtr& p, Real f, Real price, Real d, Real disp) {
                 return blackFormulaImpliedStdDevApproximationRS(p, f, price, d, disp);
             })},
            {"blackFormulaImpliedStdDev(PlainVanillaPayoff,Real,Real,Real,Real,Real,Real,Natural)",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 payoffs::PayoffSpec p = payoffs::plainVanillaPayoff(a[0]);
                 Real forward = real(a[1]), price = real(a[2]), discount = real(a[3]), displacement = real(a[4]),
                      guess = real(a[5]), accuracy = real(a[6]);
                 Natural maxIterations = natural(a[7]);
                 return [=] {
                     return json::value(blackFormulaImpliedStdDev(payoffs::makePlainVanillaPayoff(p), forward, price,
                                                                  discount, displacement, guess, accuracy,
                                                                  maxIterations));
                 };
             }},
            {"blackFormulaStdDevDerivative(Real,Real,Real,Real,Real)",
             onStrike5([](Real k, Real f, Real s, Real d, Real disp) {
                 return blackFormulaStdDevDerivative(k, f, s, d, disp);
             })},
            {"blackFormulaVolDerivative",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Real strike = real(a[0]), forward = real(a[1]), stdDev = real(a[2]), expiry = real(a[3]),
                      discount = real(a[4]), displacement = real(a[5]);
                 return [=] {
                     return json::value(
                         blackFormulaVolDerivative(strike, forward, stdDev, expiry, discount, displacement));
                 };
             }},
            {"blackFormulaStdDevDerivative(PlainVanillaPayoff,Real,Real,Real,Real)",
             onPayoff5([](const PayoffPtr& p, Real f, Real s, Real d, Real disp) {
                 return blackFormulaStdDevDerivative(p, f, s, d, disp);
             })},
            {"blackFormulaStdDevSecondDerivative(Rate,Rate,Real,Real,Real)",
             onStrike5([](Real k, Real f, Real s, Real d, Real disp) {
                 return blackFormulaStdDevSecondDerivative(k, f, s, d, disp);
             })},
            {"blackFormulaStdDevSecondDerivative(PlainVanillaPayoff,Real,Real,Real,Real)",
             onPayoff5([](const PayoffPtr& p, Real f, Real s, Real d, Real disp) {
                 return blackFormulaStdDevSecondDerivative(p, f, s, d, disp);
             })},
            {"bachelierBlackFormula(PlainVanillaPayoff,Real,Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 payoffs::PayoffSpec p = payoffs::plainVanillaPayoff(a[0]);
                 Real forward = real(a[1]), stdDev = real(a[2]), discount = real(a[3]);
                 return [=] {
                     return json::value(
                         bachelierBlackFormula(payoffs::makePlainVanillaPayoff(p), forward, stdDev, discount));
                 };
             }},
            {"bachelierBlackFormulaForwardDerivative(Option::Type,Real,Real,Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 5);
                 auto type = enumeration<Option::Type>(a[0]);
                 Real strike = real(a[1]), forward = real(a[2]), stdDev = real(a[3]), discount = real(a[4]);
                 return [=] {
                     return json::value(bachelierBlackFormulaForwardDerivative(type, strike, forward, stdDev, discount));
                 };
             }},
            {"bachelierBlackFormulaForwardDerivative(PlainVanillaPayoff,Real,Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 payoffs::PayoffSpec p = payoffs::plainVanillaPayoff(a[0]);
                 Real forward = real(a[1]), stdDev = real(a[2]), discount = real(a[3]);
                 return [=] {
                     return json::value(bachelierBlackFormulaForwardDerivative(payoffs::makePlainVanillaPayoff(p),
                                                                               forward, stdDev, discount));
                 };
             }},
            {"bachelierBlackFormulaStdDevDerivative(Real,Real,Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real strike = real(a[0]), forward = real(a[1]), stdDev = real(a[2]), discount = real(a[3]);
                 return [=] {
                     return json::value(bachelierBlackFormulaStdDevDerivative(strike, forward, stdDev, discount));
                 };
             }},
            {"bachelierBlackFormulaStdDevDerivative(PlainVanillaPayoff,Real,Real,Real)",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 payoffs::PayoffSpec p = payoffs::plainVanillaPayoff(a[0]);
                 Real forward = real(a[1]), stdDev = real(a[2]), discount = real(a[3]);
                 return [=] {
                     return json::value(bachelierBlackFormulaStdDevDerivative(payoffs::makePlainVanillaPayoff(p),
                                                                              forward, stdDev, discount));
                 };
             }},
        });

    }

}
