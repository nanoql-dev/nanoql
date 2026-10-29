// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/volatility/noarbsabr.hpp, .cpp: the no-arbitrage SABR model's members, which the wheel's sections
// don't reach. "NoArbSabrModel::values" takes (expiryTime, forward, alpha, beta, nu, rho, strikes): the model; it returns
// {"forward", "numericalForward", "expiryTime", "alpha", "beta", "nu", "rho", "absorptionProbability", "optionPrice",
// "digitalOptionPrice", "density"}, the last three at every strike. "D0Interpolator::value" takes (forward, expiryTime,
// alpha, beta, nu, rho) and returns detail::D0Interpolator's d0, the interpolated absorption probability.

#include "convert.hpp"

#include <ql/experimental/volatility/noarbsabr.hpp>

#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"NoArbSabrModel::values",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 const Real t = real(a[0]), forward = real(a[1]), alpha = real(a[2]), beta = real(a[3]);
                 const Real nu = real(a[4]), rho = real(a[5]);
                 const std::vector<Real> strikes = reals(a[6]);
                 return [=] {
                     NoArbSabrModel model(t, forward, alpha, beta, nu, rho);
                     json::object out;
                     out["forward"] = json::value(model.forward());
                     out["numericalForward"] = json::value(model.numericalForward());
                     out["expiryTime"] = json::value(model.expiryTime());
                     out["alpha"] = json::value(model.alpha());
                     out["beta"] = json::value(model.beta());
                     out["nu"] = json::value(model.nu());
                     out["rho"] = json::value(model.rho());
                     out["absorptionProbability"] = json::value(model.absorptionProbability());
                     json::array price, digital, density;
                     for (Real k : strikes) {
                         price.push_back(json::value(model.optionPrice(k)));
                         digital.push_back(json::value(model.digitalOptionPrice(k)));
                         density.push_back(json::value(model.density(k)));
                     }
                     out["optionPrice"] = price;
                     out["digitalOptionPrice"] = digital;
                     out["density"] = density;
                     return json::value(out);
                 };
             }},
            {"D0Interpolator::value",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 const Real forward = real(a[0]), t = real(a[1]), alpha = real(a[2]), beta = real(a[3]);
                 const Real nu = real(a[4]), rho = real(a[5]);
                 return [=] { return json::value(detail::D0Interpolator(forward, t, alpha, beta, nu, rho)()); };
             }},
        });

    }

}
