// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/saddlepointlossmodel.hpp, which the wheel doesn't expose: its public members beyond the
// basket's statistics (experimental/credit/basket.cpp), on experimental/credit/portfoliospec's basket and model specs,
// the model bound at the basket's first calculation:
//
//     SaddlePointLossModel::members (basket, model, dates, losses): [probOverPortfLoss(d, loss) for each date and loss,
//         probDensity(d, loss) for each date and loss]

// MSVC's C4714, a __forceinline function left out of line, which Boost's gamma code raises at /O2: a note on
// inlining, not on the code, so it is off here and /WX still stops on every other warning.
#pragma warning(disable : 4714)

#include "experimental/credit/portfoliospec.hpp"

#include <ql/experimental/credit/saddlepointlossmodel.hpp>
#include <ql/experimental/math/gaussiancopulapolicy.hpp>
#include <ql/experimental/math/tcopulapolicy.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        std::vector<DateSpec> dateList(const json::value& v) {
            if (!v.is_array())
                throw InvalidParams("expected an array of dates");
            std::vector<DateSpec> out;
            for (const json::value& d : v.get_array())
                out.push_back(date(d));
            return out;
        }

        // The model of a spec bound to its basket, cast to the copula's instantiation; the basket is kept alive in
        // the caller's b, as the model reads it through a handle.
        template <template <class> class M>
        std::pair<ext::shared_ptr<M<GaussianCopulaPolicy>>, ext::shared_ptr<M<TCopulaPolicy>>>
        bound(const json::value& basket, const json::value& model, ext::shared_ptr<Basket>& b) {
            ext::shared_ptr<DefaultLossModel> m = credit::portfolio::makeLossModel(model);
            b = credit::portfolio::makeBasket(basket);
            b->setLossModel(m);
            b->remainingTrancheNotional();  // calculates, which binds the model
            auto g = ext::dynamic_pointer_cast<M<GaussianCopulaPolicy>>(m);
            auto t = ext::dynamic_pointer_cast<M<TCopulaPolicy>>(m);
            if (g == nullptr && t == nullptr)
                throw InvalidParams("the model isn't of the unit's kind");
            return {g, t};
        }

        const Register unit({
            {"SaddlePointLossModel::members",
             [](const Args& a) -> Call {
                 arity(a, 4);
                 json::value basket = a[0], model = a[1];
                 std::vector<DateSpec> dates = dateList(a[2]);
                 std::vector<Real> losses = reals(a[3]);
                 return [=] {
                     ext::shared_ptr<Basket> b;
                     auto [g, t] = bound<SaddlePointLossModel>(basket, model, b);
                     json::array over, density;
                     for (const DateSpec& ds : dates) {
                         Date d = makeDate(ds);
                         for (Real x : losses) {
                             over.emplace_back(g != nullptr ? g->probOverPortfLoss(d, x) : t->probOverPortfLoss(d, x));
                             density.emplace_back(g != nullptr ? g->probDensity(d, x) : t->probDensity(d, x));
                         }
                     }
                     json::array out;
                     out.emplace_back(std::move(over));
                     out.emplace_back(std::move(density));
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
