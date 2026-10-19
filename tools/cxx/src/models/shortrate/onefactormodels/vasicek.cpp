// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/shortrate/onefactormodels/vasicek.hpp, .cpp: the inspectors the wheel doesn't bind, a(), b(), sigma(),
// lambda() and r0(), on a Vasicek or Hull-White model (which derives from Vasicek) built by the call from a model spec
// (models/modelspec.hpp), so that the constructor's refusals are QuantLib's.

#include "models/modelspec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        // An inspector of a model.
        Call ofModel(const Args& a, Real (*f)(const Vasicek&)) {
            arity(a, 1);
            models::ModelSpec s = models::model(a[0]);
            return [=] { return json::value(f(*models::makeModel(s))); };
        }

        const Register unit({
            {"Vasicek::a", [](const Args& a) { return ofModel(a, [](const Vasicek& m) { return m.a(); }); }},
            {"Vasicek::b", [](const Args& a) { return ofModel(a, [](const Vasicek& m) { return m.b(); }); }},
            {"Vasicek::sigma", [](const Args& a) { return ofModel(a, [](const Vasicek& m) { return m.sigma(); }); }},
            {"Vasicek::lambda", [](const Args& a) { return ofModel(a, [](const Vasicek& m) { return m.lambda(); }); }},
            {"Vasicek::r0", [](const Args& a) { return ofModel(a, [](const Vasicek& m) { return m.r0(); }); }},
        });

    }

}
