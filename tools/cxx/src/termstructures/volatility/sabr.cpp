// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/termstructures/volatility/sabr.hpp, .cpp: the functions the wheel doesn't bind, the four expansions that check
// nothing (unsafeSabrLogNormalVolatility, unsafeSabrNormalVolatility, unsafeShiftedSabrVolatility and
// unsafeSabrVolatility) and validateSabrParameters, whose result is null where it accepts the parameters. Every
// argument is passed, the defaults included; a volatility type is VolatilityType's integer, which QuantLib reads.

#include "convert.hpp"

#include <ql/termstructures/volatility/sabr.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        // A function of (strike, forward, expiryTime, alpha, beta, nu, rho).
        Method expansion(Real (*f)(Rate, Rate, Time, Real, Real, Real, Real)) {
            return [f](const Args& a) -> Call {
                arity(a, 7);
                Real strike = real(a[0]), forward = real(a[1]), t = real(a[2]), alpha = real(a[3]),
                     beta = real(a[4]), nu = real(a[5]), rho = real(a[6]);
                return [=] { return json::value(f(strike, forward, t, alpha, beta, nu, rho)); };
            };
        }

        const Register unit({
            {"unsafeSabrLogNormalVolatility", expansion(unsafeSabrLogNormalVolatility)},
            {"unsafeSabrNormalVolatility", expansion(unsafeSabrNormalVolatility)},
            // (strike, forward, expiryTime, alpha, beta, nu, rho, shift, volatilityType)
            {"unsafeShiftedSabrVolatility",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 Real strike = real(a[0]), forward = real(a[1]), t = real(a[2]), alpha = real(a[3]),
                      beta = real(a[4]), nu = real(a[5]), rho = real(a[6]), shift = real(a[7]);
                 auto type = enumeration<VolatilityType>(a[8]);
                 return [=] {
                     return json::value(
                         unsafeShiftedSabrVolatility(strike, forward, t, alpha, beta, nu, rho, shift, type));
                 };
             }},
            // (strike, forward, expiryTime, alpha, beta, nu, rho, volatilityType)
            {"unsafeSabrVolatility",
             [](const Args& a) -> Call {
                 arity(a, 8);
                 Real strike = real(a[0]), forward = real(a[1]), t = real(a[2]), alpha = real(a[3]),
                      beta = real(a[4]), nu = real(a[5]), rho = real(a[6]);
                 auto type = enumeration<VolatilityType>(a[7]);
                 return [=] {
                     return json::value(unsafeSabrVolatility(strike, forward, t, alpha, beta, nu, rho, type));
                 };
             }},
            // (alpha, beta, nu, rho)
            {"validateSabrParameters",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 Real alpha = real(a[0]), beta = real(a[1]), nu = real(a[2]), rho = real(a[3]);
                 return [=] {
                     validateSabrParameters(alpha, beta, nu, rho);
                     return json::value(nullptr);
                 };
             }},
        });

    }

}
