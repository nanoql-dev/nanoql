// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

#include "models/marketmodels/products/productspec.hpp"

#include <ql/instruments/payoffs.hpp>
#include <ql/models/marketmodels/evolutiondescription.hpp>
#include <ql/models/marketmodels/products/multiproductcomposite.hpp>
#include <ql/models/marketmodels/products/multistep/cashrebate.hpp>
#include <ql/models/marketmodels/products/multistep/multistepforwards.hpp>
#include <ql/models/marketmodels/products/multistep/multistepnothing.hpp>
#include <ql/models/marketmodels/products/multistep/multistepoptionlets.hpp>
#include <ql/models/marketmodels/products/singleproductcomposite.hpp>

using namespace QuantLib;

namespace harness::marketmodels {

    ProductSpec product(const json::value& v) {
        const std::string what = "a market-model product";
        const json::object& o = object(v, what);
        ProductSpec p;
        p.type = text(key(o, "type", what));
        if (p.type == "MultiStepForwards") {
            onlyKeys(o, {"type", "rateTimes", "accruals", "paymentTimes", "strikes"}, what);
            p.rateTimes = reals(key(o, "rateTimes", what));
            p.accruals = reals(key(o, "accruals", what));
            p.paymentTimes = reals(key(o, "paymentTimes", what));
            p.strikes = reals(key(o, "strikes", what));
            if (p.strikes.empty() || p.strikes.size() + 1 > p.rateTimes.size() ||
                p.accruals.size() < p.strikes.size() || p.paymentTimes.size() < p.strikes.size())
                throw InvalidParams("the forwards' inputs must fit their strikes, one to the number of rates");
        } else if (p.type == "MultiStepOptionlets") {
            onlyKeys(o, {"type", "rateTimes", "accruals", "paymentTimes", "payoffs"}, what);
            p.rateTimes = reals(key(o, "rateTimes", what));
            p.accruals = reals(key(o, "accruals", what));
            p.paymentTimes = reals(key(o, "paymentTimes", what));
            const json::value& payoffs = key(o, "payoffs", what);
            if (!payoffs.is_array())
                throw InvalidParams("expected an array of payoffs");
            for (const json::value& x : payoffs.get_array()) {
                if (!x.is_array() || x.get_array().size() != 2)
                    throw InvalidParams("a payoff is [optionType, strike]");
                int type = static_cast<int>(integer(x.get_array()[0]));
                if (type != 1 && type != -1)
                    throw InvalidParams("an option type is 1 or -1");
                p.payoffs.emplace_back(type, real(x.get_array()[1]));
            }
            if (p.payoffs.empty() || p.payoffs.size() + 1 > p.rateTimes.size() ||
                p.accruals.size() < p.payoffs.size() || p.paymentTimes.size() < p.payoffs.size())
                throw InvalidParams("the optionlets' inputs must fit their payoffs, one to the number of rates");
        } else if (p.type == "MultiStepNothing") {
            onlyKeys(o, {"type", "rateTimes", "evolutionTimes", "numberOfProducts", "doneIndex"}, what);
            p.rateTimes = reals(key(o, "rateTimes", what));
            p.evolutionTimes = reals(key(o, "evolutionTimes", what));
            p.numberOfProducts = size(key(o, "numberOfProducts", what));
            p.doneIndex = size(key(o, "doneIndex", what));
        } else if (p.type == "MarketModelCashRebate") {
            onlyKeys(o, {"type", "rateTimes", "evolutionTimes", "paymentTimes", "amounts", "numberOfProducts"}, what);
            p.rateTimes = reals(key(o, "rateTimes", what));
            p.evolutionTimes = reals(key(o, "evolutionTimes", what));
            p.paymentTimes = reals(key(o, "paymentTimes", what));
            p.amounts = matrix(key(o, "amounts", what));
            p.numberOfProducts = size(key(o, "numberOfProducts", what));
        } else if (p.type == "MultiProductComposite" || p.type == "SingleProductComposite") {
            onlyKeys(o, {"type", "parts"}, what);
            const json::value& parts = key(o, "parts", what);
            if (!parts.is_array())
                throw InvalidParams("expected an array of parts");
            for (const json::value& x : parts.get_array()) {
                const json::object& part = object(x, "a composite's part");
                onlyKeys(part, {"product", "multiplier"}, "a composite's part");
                p.parts.emplace_back(product(key(part, "product", "a composite's part")),
                                     real(key(part, "multiplier", "a composite's part")));
            }
        } else {
            throw InvalidParams("unknown product type " + p.type);
        }
        return p;
    }

    std::unique_ptr<MarketModelMultiProduct> makeProduct(const ProductSpec& p) {
        if (p.type == "MultiStepForwards")
            return std::make_unique<MultiStepForwards>(p.rateTimes, p.accruals, p.paymentTimes, p.strikes);
        if (p.type == "MultiStepOptionlets") {
            std::vector<ext::shared_ptr<Payoff>> payoffs;
            for (const auto& x : p.payoffs)
                payoffs.push_back(ext::make_shared<PlainVanillaPayoff>(Option::Type(x.first), x.second));
            return std::make_unique<MultiStepOptionlets>(p.rateTimes, p.accruals, p.paymentTimes, payoffs);
        }
        if (p.type == "MultiStepNothing")
            return std::make_unique<MultiStepNothing>(EvolutionDescription(p.rateTimes, p.evolutionTimes),
                                                      p.numberOfProducts, p.doneIndex);
        if (p.type == "MarketModelCashRebate")
            return std::make_unique<MarketModelCashRebate>(EvolutionDescription(p.rateTimes, p.evolutionTimes),
                                                           p.paymentTimes, makeMatrix(p.amounts), p.numberOfProducts);
        std::unique_ptr<MarketModelComposite> c;
        if (p.type == "MultiProductComposite")
            c = std::make_unique<MultiProductComposite>();
        else
            c = std::make_unique<SingleProductComposite>();
        for (const auto& part : p.parts) {
            std::unique_ptr<MarketModelMultiProduct> made = makeProduct(part.first);
            c->add(*made, part.second);
        }
        c->finalize();
        return c;
    }

}
