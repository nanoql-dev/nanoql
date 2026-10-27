// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/models/marketmodels/marketmodeldifferences.hpp: the two models' volatility differences, which the wheel doesn't
// expose.
//
// "rateVolDifferences" takes (model1, model2) and "rateInstVolDifferences" takes (model1, model2, index), each model
// a spec of marketmodelspec.hpp; each returns QuantLib's vector.

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/marketmodeldifferences.hpp>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        const Register unit({
            {"rateVolDifferences",
             [](const Args& a) -> Call {
                 arity(a, 2);
                 ModelSpec m1 = model(a[0]), m2 = model(a[1]);
                 if (m1.evolutionTimes.size() + 1 < m1.rateTimes.size())
                     throw InvalidParams("QuantLib reads an evolution time a rate");
                 return [=] { return result(rateVolDifferences(*makeModel(m1), *makeModel(m2))); };
             }},
            {"rateInstVolDifferences",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 ModelSpec m1 = model(a[0]), m2 = model(a[1]);
                 Size index = size(a[2]);
                 if (index + 1 >= m1.rateTimes.size())
                     throw InvalidParams("QuantLib reads the index's covariance entry");
                 return [=] { return result(rateInstVolDifferences(*makeModel(m1), *makeModel(m2), index)); };
             }},
        });

    }

}
