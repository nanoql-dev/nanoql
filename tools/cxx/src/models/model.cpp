// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/model.hpp, .cpp: the members the wheel doesn't reach. CalibratedModel's constraint(), whose test,
// upperBound and lowerBound the wheel's Constraint proxy doesn't bind; and AffineModel's five-argument
// discountBondOption, called through AffineModel, so that a model that doesn't override it (Vasicek) runs its default
// and Hull-White its override. A model argument is a spec of models/modelspec.hpp, built by the call; the parameters
// are a list of reals, which the harness passes only at the model's own size, since the constraint reads its slices
// unchecked.

#include "models/modelspec.hpp"

#include <ql/option.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        // A function of a model's constraint at the given parameters.
        Call ofConstraint(const Args& a, json::value (*f)(const Constraint&, const Array&)) {
            arity(a, 2);
            models::ModelSpec s = models::model(a[0]);
            std::vector<Real> p = reals(a[1]);
            return [=] {
                ext::shared_ptr<Vasicek> m = models::makeModel(s);
                if (p.size() != m->params().size())
                    throw InvalidParams("the parameters aren't the model's " + std::to_string(m->params().size()));
                return f(*m->constraint(), models::makeArray(p));
            };
        }

        const Register unit({
            {"CalibratedModel::constraint()->test",
             [](const Args& a) {
                 return ofConstraint(a, [](const Constraint& c, const Array& p) { return json::value(c.test(p)); });
             }},
            {"CalibratedModel::constraint()->upperBound",
             [](const Args& a) {
                 return ofConstraint(
                     a, [](const Constraint& c, const Array& p) { return models::arrayResult(c.upperBound(p)); });
             }},
            {"CalibratedModel::constraint()->lowerBound",
             [](const Args& a) {
                 return ofConstraint(
                     a, [](const Constraint& c, const Array& p) { return models::arrayResult(c.lowerBound(p)); });
             }},
            {"AffineModel::discountBondOption(Option::Type,Real,Time,Time,Time)",
             [](const Args& a) -> Call {
                 arity(a, 6);
                 models::ModelSpec s = models::model(a[0]);
                 auto type = enumeration<Option::Type>(a[1]);
                 Real strike = real(a[2]);
                 Time maturity = real(a[3]), bondStart = real(a[4]), bondMaturity = real(a[5]);
                 return [=] {
                     ext::shared_ptr<Vasicek> m = models::makeModel(s);
                     const AffineModel& affine = *m;
                     return json::value(affine.discountBondOption(type, strike, maturity, bondStart, bondMaturity));
                 };
             }},
        });

    }

}
