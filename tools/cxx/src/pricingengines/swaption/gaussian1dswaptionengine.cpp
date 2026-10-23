// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/pricingengines/swaption/gaussian1dswaptionengine.hpp, .cpp: the engine's additional result "probabilities",
// which the wheel doesn't read, with its NPV:
//     Gaussian1dSwaptionEngine::probabilities(swaption, model, integrationPoints, stddevs, extrapolatePayoff,
//                                             discountCurve, probabilities)
// the swaption a spec of instruments/swaptionspec.hpp, the model a GSR spec (models/.../gsrspec.hpp), the discount
// curve gsrspec.hpp's discount spec (null for the model's), the probabilities QuantLib's integer (Naive 1, Digital 2).
// The result is [NPV, probability...].

#include "convert.hpp"
#include "indexes/indexspec.hpp"
#include "instruments/swaptionspec.hpp"
#include "models/shortrate/onefactormodels/gsrspec.hpp"

#include <ql/pricingengines/swaption/gaussian1dswaptionengine.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        const Register unit({
            {"Gaussian1dSwaptionEngine::probabilities",
             [](const Args& a) -> Call {
                 arity(a, 7);
                 swaptions::SwaptionSpec s = swaptions::swaption(a[0]);
                 gaussian1d::GsrSpec m = gaussian1d::gsr(a[1]);
                 Integer points = integer(a[2]);
                 Real stddevs = real(a[3]);
                 bool extrapolate = boolean(a[4]);
                 gaussian1d::DiscountSpec curve = gaussian1d::discount(a[5]);
                 auto probabilities = enumeration<Gaussian1dSwaptionEngine::Probabilities>(a[6]);
                 return [=] {
                     auto swaption = swaptions::makeSwaption(s);
                     swaption->setPricingEngine(ext::make_shared<Gaussian1dSwaptionEngine>(
                         gaussian1d::makeGsr(m), points, stddevs, extrapolate, false, gaussian1d::makeDiscount(curve),
                         probabilities));
                     json::array out;
                     out.emplace_back(swaption->NPV());
                     for (Real p : swaption->result<std::vector<Real>>("probabilities"))
                         out.emplace_back(p);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
