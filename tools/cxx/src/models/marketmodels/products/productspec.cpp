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
#include <ql/models/marketmodels/products/multistep/multistepcoinitialswaps.hpp>
#include <ql/models/marketmodels/products/multistep/multistepcoterminalswaps.hpp>
#include <ql/models/marketmodels/products/multistep/multistepcoterminalswaptions.hpp>
#include <ql/models/marketmodels/products/multistep/multistepinversefloater.hpp>
#include <ql/models/marketmodels/products/multistep/multisteppathwisewrapper.hpp>
#include <ql/models/marketmodels/products/multistep/multistepperiodcapletswaptions.hpp>
#include <ql/models/marketmodels/products/multistep/multistepratchet.hpp>
#include <ql/models/marketmodels/products/multistep/multistepswap.hpp>
#include <ql/models/marketmodels/products/multistep/multistepswaption.hpp>
#include <ql/models/marketmodels/products/multistep/multisteptarn.hpp>
#include <ql/models/marketmodels/products/onestep/onestepcoinitialswaps.hpp>
#include <ql/models/marketmodels/products/onestep/onestepcoterminalswaps.hpp>
#include <ql/models/marketmodels/products/onestep/onestepforwards.hpp>
#include <ql/models/marketmodels/products/onestep/onestepoptionlets.hpp>
#include <ql/models/marketmodels/products/pathwise/pathwiseproductcaplet.hpp>
#include <ql/models/marketmodels/products/pathwise/pathwiseproductcashrebate.hpp>
#include <ql/models/marketmodels/products/pathwise/pathwiseproductinversefloater.hpp>
#include <ql/models/marketmodels/products/pathwise/pathwiseproductswap.hpp>
#include <ql/models/marketmodels/products/pathwise/pathwiseproductswaption.hpp>
#include <ql/models/marketmodels/products/singleproductcomposite.hpp>

#include <set>

using namespace QuantLib;

namespace harness::marketmodels {

    namespace {

        // The other products' arguments by kind: v reals, r a real, s a size, b a flag, p payoffs, q one payoff, e
        // starts and ends, m a matrix, i an inner pathwise product.
        using Keys = std::vector<std::pair<std::string, char>>;

        const Keys swapKeys = {{"rateTimes", 'v'}, {"fixedAccruals", 'v'}, {"floatingAccruals", 'v'},
                               {"paymentTimes", 'v'}, {"fixedRate", 'r'}};
        const Keys floaterKeys = {{"rateTimes", 'v'}, {"fixedAccruals", 'v'}, {"floatingAccruals", 'v'},
                                  {"fixedStrikes", 'v'}, {"fixedMultipliers", 'v'}, {"floatingSpreads", 'v'},
                                  {"paymentTimes", 'v'}, {"payer", 'b'}};
        const Keys capletKeys = {{"rateTimes", 'v'}, {"accruals", 'v'}, {"paymentTimes", 'v'}, {"strikes", 'v'}};

        const std::map<std::string, Keys>& otherKeys() {
            static const std::map<std::string, Keys> k = {
                {"MultiStepSwap", {{"rateTimes", 'v'}, {"fixedAccruals", 'v'}, {"floatingAccruals", 'v'},
                                   {"paymentTimes", 'v'}, {"fixedRate", 'r'}, {"payer", 'b'}}},
                {"MultiStepSwaption", {{"rateTimes", 'v'}, {"startIndex", 's'}, {"endIndex", 's'}, {"payoff", 'q'}}},
                {"MultiStepCoterminalSwaps", swapKeys},
                {"MultiStepCoterminalSwaptions", {{"rateTimes", 'v'}, {"paymentTimes", 'v'}, {"payoffs", 'p'}}},
                {"MultiStepCoinitialSwaps", swapKeys},
                {"MultiStepInverseFloater", floaterKeys},
                {"MultiStepRatchet", {{"rateTimes", 'v'}, {"accruals", 'v'}, {"paymentTimes", 'v'},
                                      {"gearingOfFloor", 'r'}, {"gearingOfFixing", 'r'}, {"spreadOfFloor", 'r'},
                                      {"spreadOfFixing", 'r'}, {"initialFloor", 'r'}, {"payer", 'b'}}},
                {"MultiStepTarn", {{"rateTimes", 'v'}, {"accruals", 'v'}, {"accrualsFloating", 'v'},
                                   {"paymentTimes", 'v'}, {"paymentTimesFloating", 'v'}, {"totalCoupon", 'r'},
                                   {"strikes", 'v'}, {"multipliers", 'v'}, {"floatingSpreads", 'v'}}},
                {"MultiStepPeriodCapletSwaptions", {{"rateTimes", 'v'}, {"forwardOptionPaymentTimes", 'v'},
                                                    {"swaptionPaymentTimes", 'v'}, {"forwardPayOffs", 'p'},
                                                    {"swapPayOffs", 'p'}, {"period", 's'}, {"offset", 's'}}},
                {"OneStepForwards", capletKeys},
                {"OneStepOptionlets", {{"rateTimes", 'v'}, {"accruals", 'v'}, {"paymentTimes", 'v'}, {"payoffs", 'p'}}},
                {"OneStepCoinitialSwaps", swapKeys},
                {"OneStepCoterminalSwaps", swapKeys},
                {"MultiProductPathwiseWrapper", {{"inner", 'i'}}},
            };
            return k;
        }

        const std::map<std::string, Keys>& pathwiseKeys() {
            static const std::map<std::string, Keys> k = {
                {"MarketModelPathwiseMultiCaplet", capletKeys},
                {"MarketModelPathwiseMultiDeflatedCaplet", capletKeys},
                {"MarketModelPathwiseMultiDeflatedCap", {{"rateTimes", 'v'}, {"accruals", 'v'}, {"paymentTimes", 'v'},
                                                         {"strike", 'r'}, {"startsAndEnds", 'e'}}},
                {"MarketModelPathwiseCashRebate", {{"rateTimes", 'v'}, {"evolutionTimes", 'v'}, {"paymentTimes", 'v'},
                                                   {"amounts", 'm'}, {"numberOfProducts", 's'}}},
                {"MarketModelPathwiseInverseFloater", floaterKeys},
                {"MarketModelPathwiseSwap", {{"rateTimes", 'v'}, {"accruals", 'v'}, {"strikes", 'v'},
                                             {"multiplier", 'r'}}},
                {"MarketModelPathwiseCoterminalSwaptionsDeflated", {{"rateTimes", 'v'}, {"strikes", 'v'}}},
                {"MarketModelPathwiseCoterminalSwaptionsNumericalDeflated", {{"rateTimes", 'v'}, {"strikes", 'v'},
                                                                             {"bumpSize", 'r'}}},
            };
            return k;
        }

        std::pair<int, Real> payoff(const json::value& x) {
            if (!x.is_array() || x.get_array().size() != 2)
                throw InvalidParams("a payoff is [optionType, strike]");
            int type = static_cast<int>(integer(x.get_array()[0]));
            if (type != 1 && type != -1)
                throw InvalidParams("an option type is 1 or -1");
            return {type, real(x.get_array()[1])};
        }

        void readKeys(const json::object& o, Keys keys, ProductSpec& p, const std::string& what) {
            // the deflated caplet's one-strike constructor
            if (p.type == "MarketModelPathwiseMultiDeflatedCaplet" && o.contains("strike"))
                keys.back() = {"strike", 'r'};
            std::set<std::string> names{"type"};
            for (const auto& k : keys)
                names.insert(k.first);
            for (const auto& kv : o)
                if (names.count(std::string(kv.key())) == 0)
                    throw InvalidParams(what + " takes no key " + std::string(kv.key()));
            for (const auto& k : keys) {
                const json::value& x = key(o, k.first.c_str(), what);
                switch (k.second) {
                  case 'v':
                    p.vecs[k.first] = reals(x);
                    break;
                  case 'r':
                    p.reals[k.first] = real(x);
                    break;
                  case 's':
                    p.sizes[k.first] = size(x);
                    break;
                  case 'b':
                    p.flags[k.first] = boolean(x);
                    break;
                  case 'q':
                    p.payoffLists[k.first] = {payoff(x)};
                    break;
                  case 'p':
                    if (!x.is_array())
                        throw InvalidParams("expected an array of payoffs");
                    for (const json::value& y : x.get_array())
                        p.payoffLists[k.first].push_back(payoff(y));
                    break;
                  case 'e':
                    if (!x.is_array())
                        throw InvalidParams("expected an array of [start, end]");
                    for (const json::value& y : x.get_array()) {
                        if (!y.is_array() || y.get_array().size() != 2)
                            throw InvalidParams("a cap's starts and ends are [start, end]");
                        p.startsAndEnds.emplace_back(size(y.get_array()[0]), size(y.get_array()[1]));
                    }
                    break;
                  case 'm':
                    p.amounts = matrix(x);
                    break;
                  default:
                    p.inner = std::make_shared<ProductSpec>(pathwiseProduct(x));
                }
            }
            p.rateTimes = p.vecs["rateTimes"];
        }

        // QuantLib reads each of these once a rate, past the vector where it's shorter (nanoql refuses those).
        void checkPerRate(const ProductSpec& p, std::initializer_list<const char*> names, bool atLeast = false) {
            if (p.rateTimes.size() < 2)
                throw InvalidParams("a product needs two rate times");
            Size n = p.rateTimes.size() - 1;
            for (const char* name : names) {
                Size m = p.vecs.at(name).size();
                if (atLeast ? m < n : m != n)
                    throw InvalidParams(std::string(name) + " must be one a rate");
            }
            for (const auto& kv : p.payoffLists)
                if (p.type != "MultiStepSwaption" && p.type != "MultiStepPeriodCapletSwaptions" && kv.second.size() != n)
                    throw InvalidParams(kv.first + " must be one a rate");
        }

        void checkOther(const ProductSpec& p) {
            const std::string& t = p.type;
            if (t == "MultiStepSwap" || t == "MultiStepCoterminalSwaps" || t == "MultiStepCoinitialSwaps" ||
                t == "OneStepCoinitialSwaps" || t == "OneStepCoterminalSwaps")
                checkPerRate(p, {"fixedAccruals", "floatingAccruals", "paymentTimes"});
            else if (t == "MultiStepSwaption") {
                if (p.rateTimes.size() < 2 || !(p.sizes.at("startIndex") < p.sizes.at("endIndex")) ||
                    !(p.sizes.at("endIndex") < p.rateTimes.size()))
                    throw InvalidParams("a swaption's indices must be increasing, within the rates");
            } else if (t == "MultiStepCoterminalSwaptions")
                checkPerRate(p, {"paymentTimes"});
            else if (t == "MultiStepInverseFloater" || t == "MarketModelPathwiseInverseFloater")
                checkPerRate(p, {"fixedAccruals", "floatingAccruals", "fixedStrikes", "fixedMultipliers",
                                 "floatingSpreads", "paymentTimes"});
            else if (t == "MultiStepRatchet")
                checkPerRate(p, {"accruals", "paymentTimes"});
            else if (t == "MultiStepTarn") {
                checkPerRate(p, {"accruals", "accrualsFloating", "paymentTimes", "paymentTimesFloating", "strikes",
                                 "floatingSpreads"});
                checkPerRate(p, {"multipliers"}, true);
            } else if (t == "MultiStepPeriodCapletSwaptions") {
                if (p.rateTimes.size() < 2)
                    throw InvalidParams("a product needs two rate times");
            } else if (t == "OneStepForwards" || t == "MarketModelPathwiseMultiCaplet" ||
                       (t == "MarketModelPathwiseMultiDeflatedCaplet" && p.vecs.count("strikes") != 0))
                checkPerRate(p, {"accruals", "paymentTimes", "strikes"});
            else if (t == "OneStepOptionlets")
                checkPerRate(p, {"accruals", "paymentTimes"});
            else if (t == "MarketModelPathwiseMultiDeflatedCaplet" || t == "MarketModelPathwiseMultiDeflatedCap") {
                checkPerRate(p, {"accruals", "paymentTimes"});
                for (const auto& se : p.startsAndEnds)
                    if (!(se.first < se.second) || se.second > p.rateTimes.size() - 1)
                        throw InvalidParams("a cap must start before it ends, within the rates");
            } else if (t == "MarketModelPathwiseSwap")
                checkPerRate(p, {"accruals", "strikes"});
            else if (t == "MarketModelPathwiseCoterminalSwaptionsDeflated" ||
                     t == "MarketModelPathwiseCoterminalSwaptionsNumericalDeflated")
                checkPerRate(p, {"strikes"});
        }

        std::vector<ext::shared_ptr<StrikedTypePayoff>> striked(const std::vector<std::pair<int, Real>>& x) {
            std::vector<ext::shared_ptr<StrikedTypePayoff>> out;
            for (const auto& y : x)
                out.push_back(ext::make_shared<PlainVanillaPayoff>(Option::Type(y.first), y.second));
            return out;
        }

    }

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
        } else if (otherKeys().count(p.type) != 0) {
            readKeys(o, otherKeys().at(p.type), p, what);
            if (p.type != "MultiProductPathwiseWrapper")
                checkOther(p);
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
        const auto& v = p.vecs;
        const auto& r = p.reals;
        if (p.type == "MultiStepSwap")
            return std::make_unique<MultiStepSwap>(p.rateTimes, v.at("fixedAccruals"), v.at("floatingAccruals"),
                                                   v.at("paymentTimes"), r.at("fixedRate"), p.flags.at("payer"));
        if (p.type == "MultiStepSwaption") {
            ext::shared_ptr<StrikedTypePayoff> payoff = striked(p.payoffLists.at("payoff")).front();
            return std::make_unique<MultiStepSwaption>(p.rateTimes, p.sizes.at("startIndex"), p.sizes.at("endIndex"),
                                                       payoff);
        }
        if (p.type == "MultiStepCoterminalSwaps")
            return std::make_unique<MultiStepCoterminalSwaps>(p.rateTimes, v.at("fixedAccruals"),
                                                              v.at("floatingAccruals"), v.at("paymentTimes"),
                                                              r.at("fixedRate"));
        if (p.type == "MultiStepCoterminalSwaptions")
            return std::make_unique<MultiStepCoterminalSwaptions>(p.rateTimes, v.at("paymentTimes"),
                                                                  striked(p.payoffLists.at("payoffs")));
        if (p.type == "MultiStepCoinitialSwaps")
            return std::make_unique<MultiStepCoinitialSwaps>(p.rateTimes, v.at("fixedAccruals"),
                                                             v.at("floatingAccruals"), v.at("paymentTimes"),
                                                             r.at("fixedRate"));
        if (p.type == "MultiStepInverseFloater")
            return std::make_unique<MultiStepInverseFloater>(
                p.rateTimes, v.at("fixedAccruals"), v.at("floatingAccruals"), v.at("fixedStrikes"),
                v.at("fixedMultipliers"), v.at("floatingSpreads"), v.at("paymentTimes"), p.flags.at("payer"));
        if (p.type == "MultiStepRatchet")
            return std::make_unique<MultiStepRatchet>(p.rateTimes, v.at("accruals"), v.at("paymentTimes"),
                                                      r.at("gearingOfFloor"), r.at("gearingOfFixing"),
                                                      r.at("spreadOfFloor"), r.at("spreadOfFixing"),
                                                      r.at("initialFloor"), p.flags.at("payer"));
        if (p.type == "MultiStepTarn")
            return std::make_unique<MultiStepTarn>(p.rateTimes, v.at("accruals"), v.at("accrualsFloating"),
                                                   v.at("paymentTimes"), v.at("paymentTimesFloating"),
                                                   r.at("totalCoupon"), v.at("strikes"), v.at("multipliers"),
                                                   v.at("floatingSpreads"));
        if (p.type == "MultiStepPeriodCapletSwaptions")
            return std::make_unique<MultiStepPeriodCapletSwaptions>(
                p.rateTimes, v.at("forwardOptionPaymentTimes"), v.at("swaptionPaymentTimes"),
                striked(p.payoffLists.at("forwardPayOffs")), striked(p.payoffLists.at("swapPayOffs")),
                p.sizes.at("period"), p.sizes.at("offset"));
        if (p.type == "OneStepForwards")
            return std::make_unique<OneStepForwards>(p.rateTimes, v.at("accruals"), v.at("paymentTimes"),
                                                     v.at("strikes"));
        if (p.type == "OneStepOptionlets") {
            std::vector<ext::shared_ptr<Payoff>> payoffs;
            for (const auto& x : p.payoffLists.at("payoffs"))
                payoffs.push_back(ext::make_shared<PlainVanillaPayoff>(Option::Type(x.first), x.second));
            return std::make_unique<OneStepOptionlets>(p.rateTimes, v.at("accruals"), v.at("paymentTimes"), payoffs);
        }
        if (p.type == "OneStepCoinitialSwaps")
            return std::make_unique<OneStepCoinitialSwaps>(p.rateTimes, v.at("fixedAccruals"), v.at("floatingAccruals"),
                                                           v.at("paymentTimes"), r.at("fixedRate"));
        if (p.type == "OneStepCoterminalSwaps")
            return std::make_unique<OneStepCoterminalSwaps>(p.rateTimes, v.at("fixedAccruals"),
                                                            v.at("floatingAccruals"), v.at("paymentTimes"),
                                                            r.at("fixedRate"));
        if (p.type == "MultiProductPathwiseWrapper") {
            std::unique_ptr<MarketModelPathwiseMultiProduct> inner = makePathwiseProduct(*p.inner);
            return std::make_unique<MultiProductPathwiseWrapper>(*inner);
        }
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


    ProductSpec pathwiseProduct(const json::value& v) {
        const std::string what = "a pathwise market-model product";
        const json::object& o = object(v, what);
        ProductSpec p;
        p.type = text(key(o, "type", what));
        if (pathwiseKeys().count(p.type) == 0)
            throw InvalidParams("unknown pathwise product type " + p.type);
        readKeys(o, pathwiseKeys().at(p.type), p, what);
        checkOther(p);
        return p;
    }

    std::unique_ptr<MarketModelPathwiseMultiProduct> makePathwiseProduct(const ProductSpec& p) {
        const auto& v = p.vecs;
        const auto& r = p.reals;
        if (p.type == "MarketModelPathwiseMultiCaplet")
            return std::make_unique<MarketModelPathwiseMultiCaplet>(p.rateTimes, v.at("accruals"), v.at("paymentTimes"),
                                                                    v.at("strikes"));
        if (p.type == "MarketModelPathwiseMultiDeflatedCaplet") {
            if (v.count("strikes") != 0)
                return std::make_unique<MarketModelPathwiseMultiDeflatedCaplet>(
                    p.rateTimes, v.at("accruals"), v.at("paymentTimes"), v.at("strikes"));
            return std::make_unique<MarketModelPathwiseMultiDeflatedCaplet>(p.rateTimes, v.at("accruals"),
                                                                            v.at("paymentTimes"), r.at("strike"));
        }
        if (p.type == "MarketModelPathwiseMultiDeflatedCap")
            return std::make_unique<MarketModelPathwiseMultiDeflatedCap>(
                p.rateTimes, v.at("accruals"), v.at("paymentTimes"), r.at("strike"), p.startsAndEnds);
        if (p.type == "MarketModelPathwiseCashRebate")
            return std::make_unique<MarketModelPathwiseCashRebate>(
                EvolutionDescription(p.rateTimes, v.at("evolutionTimes")), v.at("paymentTimes"), makeMatrix(p.amounts),
                p.sizes.at("numberOfProducts"));
        if (p.type == "MarketModelPathwiseInverseFloater")
            return std::make_unique<MarketModelPathwiseInverseFloater>(
                p.rateTimes, v.at("fixedAccruals"), v.at("floatingAccruals"), v.at("fixedStrikes"),
                v.at("fixedMultipliers"), v.at("floatingSpreads"), v.at("paymentTimes"), p.flags.at("payer"));
        if (p.type == "MarketModelPathwiseSwap")
            return std::make_unique<MarketModelPathwiseSwap>(p.rateTimes, v.at("accruals"), v.at("strikes"),
                                                             r.at("multiplier"));
        if (p.type == "MarketModelPathwiseCoterminalSwaptionsDeflated")
            return std::make_unique<MarketModelPathwiseCoterminalSwaptionsDeflated>(p.rateTimes, v.at("strikes"));
        return std::make_unique<MarketModelPathwiseCoterminalSwaptionsNumericalDeflated>(p.rateTimes, v.at("strikes"),
                                                                                         r.at("bumpSize"));
    }

}
