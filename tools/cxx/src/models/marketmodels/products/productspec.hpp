// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The market-model product specs the harness's product units share (ql/models/marketmodels/products), with the names of
// tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MarketModelProductCxxInputs.cs.
//
// A product is {"type": "MultiStepForwards", "rateTimes", "accruals", "paymentTimes", "strikes"},
// {"type": "MultiStepOptionlets", "rateTimes", "accruals", "paymentTimes", "payoffs"} (each payoff [optionType,
// strike], a PlainVanillaPayoff, the type 1 for a call and -1 for a put), {"type": "MultiStepNothing", "rateTimes",
// "evolutionTimes", "numberOfProducts", "doneIndex"} (its evolution EvolutionDescription(rateTimes, evolutionTimes)),
// {"type": "MarketModelCashRebate", "rateTimes", "evolutionTimes", "paymentTimes", "amounts", "numberOfProducts"}, or
// {"type": "MultiProductComposite" or "SingleProductComposite", "parts"}, each part {"product", "multiplier"}, added in
// order and finalized.

#ifndef NANOQL_CXX_MODELS_MARKETMODELS_PRODUCTS_PRODUCTSPEC_HPP
#define NANOQL_CXX_MODELS_MARKETMODELS_PRODUCTS_PRODUCTSPEC_HPP

#include "convert.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/multiproduct.hpp>

#include <memory>
#include <string>
#include <utility>
#include <vector>

namespace harness::marketmodels {

    struct ProductSpec {
        std::string type;
        Vec rateTimes, accruals, paymentTimes, strikes, evolutionTimes;
        std::vector<std::pair<int, QuantLib::Real>> payoffs;
        QuantLib::Size numberOfProducts = 0, doneIndex = 0;
        Mat amounts;
        std::vector<std::pair<ProductSpec, QuantLib::Real>> parts;
    };
    ProductSpec product(const json::value& v);
    std::unique_ptr<QuantLib::MarketModelMultiProduct> makeProduct(const ProductSpec& p);

}

#endif
