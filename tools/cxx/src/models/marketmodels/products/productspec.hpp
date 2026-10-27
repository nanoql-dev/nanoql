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
//
// The other products take their QuantLib constructors' arguments under their parameters' names, in the table
// productspec.cpp's keys() gives: the multi-step swap, swaption ("payoff" one [optionType, strike]), coterminal swaps
// and swaptions, coinitial swaps, inverse floater, ratchet, TARN ("MultiStepTarn") and period caplet swaptions
// ("forwardPayOffs", "swapPayOffs"); the one-step forwards, optionlets, coinitial and coterminal swaps; and
// {"type": "MultiProductPathwiseWrapper", "inner"}, the inner product a pathwise spec. A pathwise product is
// {"type": "MarketModelPathwiseMultiCaplet" or "MarketModelPathwiseMultiDeflatedCaplet", "rateTimes", "accruals",
// "paymentTimes", "strikes"} (the deflated caplet "strike" in place of "strikes" for its one-strike constructor),
// {"type": "MarketModelPathwiseMultiDeflatedCap", "rateTimes", "accruals", "paymentTimes", "strike", "startsAndEnds"}
// (each [start, end]), {"type": "MarketModelPathwiseCashRebate", "rateTimes", "evolutionTimes", "paymentTimes",
// "amounts", "numberOfProducts"}, the inverse floater and swap under their parameters' names, or
// {"type": "MarketModelPathwiseCoterminalSwaptionsDeflated", "rateTimes", "strikes"} and its numerical twin with
// "bumpSize". The callable products are {"type": "CallSpecifiedMultiProduct", "underlying", "strategy", "rebate"}
// (the rebate a product spec or null for QuantLib's default, the strategy callabilitySpec.hpp's), {"type":
// "ExerciseAdapter", "exercise", "numberOfProducts"} (the exercise an exercise value) and, pathwise, {"type":
// "CallSpecifiedPathwiseMultiProduct", "underlying", "strategy", "rebate"} (the underlying and rebate pathwise specs).
// Where QuantLib reads past an input that doesn't fit its rates, the spec is refused (InvalidParams).

#ifndef NANOQL_CXX_MODELS_MARKETMODELS_PRODUCTS_PRODUCTSPEC_HPP
#define NANOQL_CXX_MODELS_MARKETMODELS_PRODUCTS_PRODUCTSPEC_HPP

#include "convert.hpp"
#include "models/marketmodels/callability/callabilityspec.hpp"
#include "models/marketmodels/marketmodelspec.hpp"

#include <ql/models/marketmodels/multiproduct.hpp>
#include <ql/models/marketmodels/pathwisemultiproduct.hpp>

#include <map>
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
        // the other products' arguments, by their parameters' names
        std::map<std::string, Vec> vecs;
        std::map<std::string, QuantLib::Real> reals;
        std::map<std::string, QuantLib::Size> sizes;
        std::map<std::string, bool> flags;
        std::map<std::string, std::vector<std::pair<int, QuantLib::Real>>> payoffLists;
        std::vector<std::pair<QuantLib::Size, QuantLib::Size>> startsAndEnds;
        std::shared_ptr<ProductSpec> inner;
        // the callable products' parts
        std::shared_ptr<ProductSpec> underlying, rebateProduct;
        std::shared_ptr<StrategySpec> strategy;
        std::shared_ptr<ExerciseValueSpec> exercise;
    };
    ProductSpec product(const json::value& v);
    std::unique_ptr<QuantLib::MarketModelMultiProduct> makeProduct(const ProductSpec& p);
    ProductSpec pathwiseProduct(const json::value& v);
    std::unique_ptr<QuantLib::MarketModelPathwiseMultiProduct> makePathwiseProduct(const ProductSpec& p);

}

#endif
