// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/experimental/callablebonds/discretizedcallablefixedratebond.hpp, .cpp, which the wheel doesn't expose: the asset
// built from a callable bond's arguments (CallableBond::setupArguments under the case's settings, with the spread
// written into them as QuantLib's NPVSpreadHelper writes it) and a curve spec of models/modelspec.hpp, its mandatory
// times, and its values rolled back on a lattice spec's tree, the bond and the lattice built inside the call.
//
//     DiscretizedCallableFixedRateBond::mandatoryTimes
//                                                  (bond, curve, spread): the times, in QuantLib's order
//     DiscretizedCallableFixedRateBond::rollback   (bond, curve, spread, lattice, t0, [t, ...]): the asset initialized
//                                                  at t0, then rolled back to each time in turn: {"times", "values"}
//                                                  after each
//     DiscretizedCallableFixedRateBond::presentValue
//                                                  (bond, curve, spread, lattice, t0, [t, ...]): the present value
//                                                  after each rollback

#include "convert.hpp"
#include "experimental/callablebonds/callablebondspec.hpp"
#include "models/modelspec.hpp"

#include <ql/experimental/callablebonds/discretizedcallablefixedratebond.hpp>
#include <ql/handle.hpp>

using namespace QuantLib;

namespace harness {

    namespace {

        struct AssetArgs {
            callablebonds::CallableBondSpec bond;
            models::CurveSpec curve;
            Real spread;
        };

        AssetArgs assetArgs(const Args& a) {
            return {callablebonds::bond(a[0]), models::curve(a[1]), real(a[2])};
        }

        ext::shared_ptr<DiscretizedCallableFixedRateBond> makeAsset(const AssetArgs& s) {
            auto bond = callablebonds::makeBond(s.bond);
            CallableBond::arguments args;
            bond->setupArguments(&args);
            args.spread = s.spread;
            return ext::make_shared<DiscretizedCallableFixedRateBond>(
                args, Handle<YieldTermStructure>(models::makeCurve(s.curve)));
        }

        struct RollbackArgs {
            AssetArgs asset;
            models::LatticeSpec lattice;
            Time t0;
            std::vector<Time> times;
        };

        RollbackArgs rollbackArgs(const Args& a) {
            arity(a, 6);
            return {assetArgs(a), models::lattice(a[3]), real(a[4]), harness::reals(a[5])};
        }

        const Register unit({
            {"DiscretizedCallableFixedRateBond::mandatoryTimes",
             [](const Args& a) -> Call {
                 arity(a, 3);
                 AssetArgs s = assetArgs(a);
                 return [=] {
                     json::array out;
                     for (Time t : makeAsset(s)->mandatoryTimes())
                         out.emplace_back(t);
                     return json::value(std::move(out));
                 };
             }},
            {"DiscretizedCallableFixedRateBond::rollback",
             [](const Args& a) -> Call {
                 RollbackArgs r = rollbackArgs(a);
                 return [=] {
                     auto asset = makeAsset(r.asset);
                     asset->initialize(models::makeLattice(r.lattice), r.t0);
                     json::array times, values;
                     for (Time t : r.times) {
                         asset->rollback(t);
                         times.emplace_back(asset->time());
                         values.emplace_back(models::arrayResult(asset->values()));
                     }
                     return json::value(json::object{{"times", std::move(times)}, {"values", std::move(values)}});
                 };
             }},
            {"DiscretizedCallableFixedRateBond::presentValue",
             [](const Args& a) -> Call {
                 RollbackArgs r = rollbackArgs(a);
                 return [=] {
                     auto asset = makeAsset(r.asset);
                     asset->initialize(models::makeLattice(r.lattice), r.t0);
                     json::array out;
                     for (Time t : r.times) {
                         asset->rollback(t);
                         out.emplace_back(asset->presentValue());
                     }
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
