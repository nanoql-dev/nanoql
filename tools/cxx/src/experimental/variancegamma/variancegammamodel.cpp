// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/variancegamma/variancegammamodel.hpp, .cpp, which the wheel doesn't expose. "VarianceGammaModel"
// takes (s0, dividendYield, riskFreeRate, sigma, nu, theta): a variance gamma process on flat Act/365 (Fixed) forwards
// from the evaluation date and the model on it. It returns {"sigma", "nu", "theta", "params"}: the model's three
// parameters and its params().

#include "convert.hpp"

#include <ql/experimental/variancegamma/variancegammamodel.hpp>
#include <ql/quotes/simplequote.hpp>
#include <ql/settings.hpp>
#include <ql/termstructures/yield/flatforward.hpp>
#include <ql/time/daycounters/actual365fixed.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"VarianceGammaModel",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 Real s0 = real(a[0]);
                 Real q = real(a[1]);
                 Real r = real(a[2]);
                 Real sigma = real(a[3]);
                 Real nu = real(a[4]);
                 Real theta = real(a[5]);
                 return [=] {
                     const Date today = Settings::instance().evaluationDate();
                     const DayCounter dc = Actual365Fixed();
                     auto process = ext::make_shared<VarianceGammaProcess>(
                         Handle<Quote>(ext::make_shared<SimpleQuote>(s0)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, q, dc)),
                         Handle<YieldTermStructure>(ext::make_shared<FlatForward>(today, r, dc)), sigma, nu, theta);
                     VarianceGammaModel model(process);
                     json::object o;
                     o["sigma"] = json::value(model.sigma());
                     o["nu"] = json::value(model.nu());
                     o["theta"] = json::value(model.theta());
                     json::array params;
                     for (Real p : model.params())
                         params.emplace_back(p);
                     o["params"] = json::value(params);
                     return json::value(o);
                 };
             }},
        });

    }

}
