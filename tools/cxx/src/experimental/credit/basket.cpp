// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/credit/basket.hpp, .cpp, which the wheel doesn't expose, on experimental/credit/portfoliospec.hpp's
// basket and model specs, the model set on the basket and the basket calculated (its first statistic, which recomputes
// its evaluation-date state after the tranche's amounts) before any read:
//
//     Basket::inspect   (basket, model): [size, notional, basketNotional, trancheNotional, attachmentAmount,
//                        detachmentAmount, attachmentRatio, detachmentRatio, refDate, remainingNotional(),
//                        remainingSize(), remainingNames(), remainingNotionals(), liveList(), settledLoss(),
//                        cumulatedLoss(), remainingAttachmentAmount(), remainingDetachmentAmount(),
//                        remainingTrancheNotional(), [key summary of defaultKeys()...]]
//     Basket::dated     (basket, model, date): [settledLoss, cumulatedLoss, remainingNotional, remainingNotionals,
//                        remainingNames, remainingSize, liveList, remainingAttachmentAmount, remainingDetachmentAmount,
//                        remainingTrancheNotional, probabilities, remainingProbabilities] at the date
//     Basket::exposure  (basket, model, name, date)
//     Basket::probAtLeastNEvents (basket, model, n, date), Basket::defaultCorrelation (basket, model, date, i, j),
//     Basket::recoveryRate (basket, model, date, i), Basket::probsBeingNthEvent (basket, model, n, date),
//     Basket::expectedTrancheLoss (basket, model, date), Basket::probOverLoss (basket, model, date, fraction),
//     Basket::percentile, Basket::expectedShortfall (basket, model, date, p), Basket::splitVaRLevel (basket, model,
//     date, loss), Basket::lossDistribution (basket, model, date): [[loss, probability]...]

#include "experimental/credit/portfoliospec.hpp"

using namespace QuantLib;

namespace harness {

    namespace {

        using credit::portfolio::array;
        using credit::portfolio::keySummary;
        using credit::portfolio::makeBoundBasket;

        json::value strings(const std::vector<std::string>& x) {
            json::array out;
            for (const std::string& s : x)
                out.emplace_back(s);
            return json::value(std::move(out));
        }

        json::value sizes(const std::vector<Size>& x) {
            json::array out;
            for (Size s : x)
                out.emplace_back(static_cast<std::int64_t>(s));
            return json::value(std::move(out));
        }

        ext::shared_ptr<Basket> calculated(const json::value& b, const json::value& m) {
            ext::shared_ptr<Basket> basket = makeBoundBasket(b, m);
            basket->remainingTrancheNotional();  // calculate(): the model linked, the state recomputed
            return basket;
        }

        // A statistic f(basket, rest...) on the calculated basket: (basket, model, rest...).
        template <class F>
        Method statistic(std::size_t n, F f) {
            return [=](const Args& a) -> Call {
                arity(a, n);
                json::value b = a[0], m = a[1];
                Args rest(a.begin() + 2, a.end());
                return [=] { return f(*calculated(b, m), rest); };
            };
        }

        const Register unit({
            {"Basket::inspect",
             statistic(2, [](const Basket& x, const Args&) {
                 json::array keys;
                 for (const DefaultProbKey& k : x.defaultKeys())
                     keys.push_back(keySummary(k));
                 json::array out;
                 out.emplace_back(static_cast<std::int64_t>(x.size()));
                 out.emplace_back(x.notional());
                 out.emplace_back(x.basketNotional());
                 out.emplace_back(x.trancheNotional());
                 out.emplace_back(x.attachmentAmount());
                 out.emplace_back(x.detachmentAmount());
                 out.emplace_back(x.attachmentRatio());
                 out.emplace_back(x.detachmentRatio());
                 out.push_back(result(x.refDate()));
                 out.emplace_back(x.remainingNotional());
                 out.emplace_back(static_cast<std::int64_t>(x.remainingSize()));
                 out.push_back(strings(x.remainingNames()));
                 out.push_back(array(x.remainingNotionals()));
                 out.push_back(sizes(x.liveList()));
                 out.emplace_back(x.settledLoss());
                 out.emplace_back(x.cumulatedLoss());
                 out.emplace_back(x.remainingAttachmentAmount());
                 out.emplace_back(x.remainingDetachmentAmount());
                 out.emplace_back(x.remainingTrancheNotional());
                 out.emplace_back(std::move(keys));
                 return json::value(std::move(out));
             })},
            {"Basket::dated",
             statistic(3, [](const Basket& x, const Args& r) {
                 Date d = makeDate(date(r[0]));
                 json::array out;
                 out.emplace_back(x.settledLoss(d));
                 out.emplace_back(x.cumulatedLoss(d));
                 out.emplace_back(x.remainingNotional(d));
                 out.push_back(array(x.remainingNotionals(d)));
                 out.push_back(strings(x.remainingNames(d)));
                 out.emplace_back(static_cast<std::int64_t>(x.remainingSize(d)));
                 out.push_back(sizes(x.liveList(d)));
                 out.emplace_back(x.remainingAttachmentAmount(d));
                 out.emplace_back(x.remainingDetachmentAmount(d));
                 out.emplace_back(x.remainingTrancheNotional(d));
                 out.push_back(array(x.probabilities(d)));
                 out.push_back(array(x.remainingProbabilities(d)));
                 return json::value(std::move(out));
             })},
            {"Basket::exposure",
             statistic(4, [](const Basket& x, const Args& r) {
                 return json::value(x.exposure(text(r[0]), makeDate(date(r[1]))));
             })},
            {"Basket::probAtLeastNEvents",
             statistic(4, [](const Basket& x, const Args& r) {
                 return json::value(x.probAtLeastNEvents(static_cast<Size>(integer(r[0])), makeDate(date(r[1]))));
             })},
            {"Basket::defaultCorrelation",
             statistic(5, [](const Basket& x, const Args& r) {
                 return json::value(x.defaultCorrelation(makeDate(date(r[0])), static_cast<Size>(integer(r[1])),
                                                         static_cast<Size>(integer(r[2]))));
             })},
            {"Basket::recoveryRate",
             statistic(4, [](const Basket& x, const Args& r) {
                 return json::value(x.recoveryRate(makeDate(date(r[0])), static_cast<Size>(integer(r[1]))));
             })},
            {"Basket::probsBeingNthEvent",
             statistic(4, [](const Basket& x, const Args& r) {
                 return array(x.probsBeingNthEvent(static_cast<Size>(integer(r[0])), makeDate(date(r[1]))));
             })},
            {"Basket::expectedTrancheLoss",
             statistic(3, [](const Basket& x, const Args& r) {
                 return json::value(x.expectedTrancheLoss(makeDate(date(r[0]))));
             })},
            {"Basket::probOverLoss",
             statistic(4, [](const Basket& x, const Args& r) {
                 return json::value(x.probOverLoss(makeDate(date(r[0])), real(r[1])));
             })},
            {"Basket::percentile",
             statistic(4, [](const Basket& x, const Args& r) {
                 return json::value(x.percentile(makeDate(date(r[0])), real(r[1])));
             })},
            {"Basket::expectedShortfall",
             statistic(4, [](const Basket& x, const Args& r) {
                 return json::value(x.expectedShortfall(makeDate(date(r[0])), real(r[1])));
             })},
            {"Basket::splitVaRLevel",
             statistic(4, [](const Basket& x, const Args& r) {
                 return array(x.splitVaRLevel(makeDate(date(r[0])), real(r[1])));
             })},
            {"Basket::lossDistribution",
             statistic(3, [](const Basket& x, const Args& r) {
                 json::array out;
                 for (const auto& kv : x.lossDistribution(makeDate(date(r[0])))) {
                     json::array p;
                     p.emplace_back(kv.first);
                     p.emplace_back(kv.second);
                     out.emplace_back(std::move(p));
                 }
                 return json::value(std::move(out));
             })},
        });

    }

}
