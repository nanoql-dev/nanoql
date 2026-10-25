// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/vanilla/analyticroughhestonengine.hpp, .cpp: the engine's members, which the wheel doesn't expose.
// "AnalyticRoughHestonEngine::members" takes (process, hurst, order, timeSteps, approximation, nFactors, zs, ts,
// payoffs): RoughHestonModel(HestonProcess, hurst) on models/equity/hestonspec.cpp's process, and
// AnalyticRoughHestonEngine(model, order, timeSteps, approximation, nFactors). It returns {"chF", "lnChF",
// "riccatiSolution"} for each complex z ([real, imaginary]) at each t, and "priceVanillaPayoff", the time form's price
// of each payoff [optionType, strike, t]. The evaluation date is the case's Settings.

#include "complexvalue.hpp"
#include "convert.hpp"
#include "models/equity/hestonspec.hpp"

#include <ql/instruments/payoffs.hpp>
#include <ql/models/equity/roughhestonmodel.hpp>
#include <ql/pricingengines/vanilla/analyticroughhestonengine.hpp>

#include <complex>
#include <vector>

using namespace QuantLib;

namespace harness {

    namespace {

        struct PayoffSpec {
            Option::Type type;
            Real strike;
            Time t;
        };

        std::vector<PayoffSpec> payoffs(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of [optionType, strike, t], got " + json::serialize(v));
            std::vector<PayoffSpec> out;
            for (const json::value& e : v.get_array()) {
                if (!e.is_array() || e.get_array().size() != 3)
                    throw InvalidParams("expected [optionType, strike, t], got " + json::serialize(e));
                out.push_back({enumeration<Option::Type>(e.get_array()[0]), real(e.get_array()[1]),
                               real(e.get_array()[2])});
            }
            return out;
        }

        Size size(const json::value& v, const char* what) {
            Integer n = integer(v);
            if (n < 0)
                throw InvalidParams(std::string(what) + " must be non-negative");
            return static_cast<Size>(n);
        }

        const Register unit({
            {"AnalyticRoughHestonEngine::members",
             [](const Args& a) -> Call {
                 arity(a, 9);
                 equity::ProcessSpec p = equity::process(a[0]);
                 Real hurst = real(a[1]);
                 Size order = size(a[2], "an order"), timeSteps = size(a[3], "the time steps");
                 auto approximation = enumeration<AnalyticRoughHestonEngine::Approximation>(a[4]);
                 Size nFactors = size(a[5], "the factors");
                 std::vector<std::complex<Real>> zs = complexValues(a[6]);
                 std::vector<Real> ts = reals(a[7]);
                 std::vector<PayoffSpec> ps = payoffs(a[8]);
                 return [=] {
                     auto model = ext::make_shared<RoughHestonModel>(equity::makeProcess(p), hurst);
                     AnalyticRoughHestonEngine engine(model, order, timeSteps, approximation, nFactors);
                     json::array chF, lnChF, riccati, prices;
                     for (const std::complex<Real>& z : zs)
                         for (Real t : ts) {
                             chF.push_back(complexResult(engine.chF(z, t)));
                             lnChF.push_back(complexResult(engine.lnChF(z, t)));
                             riccati.push_back(complexResult(engine.riccatiSolution(z, t)));
                         }
                     for (const PayoffSpec& s : ps)
                         prices.push_back(
                             engine.priceVanillaPayoff(ext::make_shared<PlainVanillaPayoff>(s.type, s.strike), s.t));
                     json::object out;
                     out["chF"] = chF;
                     out["lnChF"] = lnChF;
                     out["riccatiSolution"] = riccati;
                     out["priceVanillaPayoff"] = prices;
                     return json::value(out);
                 };
             }},
        });

    }

}
