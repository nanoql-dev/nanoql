// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// ql/discretizedasset.hpp, .cpp, which the wheel doesn't expose: DiscretizedDiscountBond and DiscretizedOption over a
// DiscretizedDiscountBond, initialized and rolled back on a short-rate model's tree that the call builds from a lattice
// spec (models/modelspec.hpp), their present values, and their mandatory times. DiscretizedOption is concrete, so the
// option is QuantLib's own class, its exercise condition max(underlying, value) over the bond.
//
// An asset argument is {"type": "DiscretizedDiscountBond"} or {"type": "DiscretizedOption", "underlyingTime",
// "otherLattice", "exerciseType", "exerciseTimes"}: the option's underlying is a DiscretizedDiscountBond initialized at
// "underlyingTime" (null for never) on the option's lattice, or with "otherLattice" on a second tree built from the
// same spec, before the option is initialized; "exerciseType" is Exercise::Type's integer and "exerciseTimes" a list
// of reals. A call initializes the asset at t0, then rolls it back to each of the given times in turn. A time off the
// grid, or a rollback forward, is QuantLib's refusal.

#include "models/modelspec.hpp"

#include <ql/discretizedasset.hpp>
#include <ql/exercise.hpp>

#include <optional>

using namespace QuantLib;

namespace harness {

    namespace {

        struct AssetSpec {
            bool option;
            std::optional<Time> underlyingTime;
            bool otherLattice;
            Exercise::Type exerciseType;
            std::vector<Time> exerciseTimes;
        };

        const json::value& key(const json::object& o, const char* name) {
            const json::value* v = o.if_contains(name);
            if (v == nullptr)
                throw InvalidParams(std::string("a discretized option needs ") + name);
            return *v;
        }

        AssetSpec assetSpec(const json::value& v) {
            if (!v.is_object())
                throw InvalidParams("expected an asset spec, got " + json::serialize(v));
            const json::object& o = v.get_object();
            const json::value* type = o.if_contains("type");
            if (type == nullptr || !type->is_string())
                throw InvalidParams("an asset spec needs a type: " + json::serialize(v));
            if (type->get_string() == "DiscretizedDiscountBond") {
                if (o.size() != 1)
                    throw InvalidParams("a DiscretizedDiscountBond takes no key but its type: " + json::serialize(v));
                return {false, std::nullopt, false, Exercise::European, {}};
            }
            if (type->get_string() != "DiscretizedOption")
                throw InvalidParams("unknown asset " + json::serialize(*type));
            for (const auto& kv : o) {
                if (kv.key() != "type" && kv.key() != "underlyingTime" && kv.key() != "otherLattice" &&
                    kv.key() != "exerciseType" && kv.key() != "exerciseTimes")
                    throw InvalidParams("DiscretizedOption takes no key " + std::string(kv.key()));
            }
            const json::value& at = key(o, "underlyingTime");
            std::optional<Time> underlyingTime;
            if (!at.is_null())
                underlyingTime = real(at);
            AssetSpec s{true, underlyingTime, boolean(key(o, "otherLattice")),
                        enumeration<Exercise::Type>(key(o, "exerciseType")), reals(key(o, "exerciseTimes"))};
            // QuantLib reads an American exercise's first two times unchecked.
            if (s.exerciseType == Exercise::American && s.exerciseTimes.size() < 2)
                throw InvalidParams("an American exercise needs two exercise times");
            return s;
        }

        struct Built {
            ext::shared_ptr<DiscretizedAsset> asset;
            ext::shared_ptr<DiscretizedDiscountBond> underlying;
        };

        // The asset, unconnected to any lattice.
        Built makeAsset(const AssetSpec& s) {
            if (!s.option)
                return {ext::make_shared<DiscretizedDiscountBond>(), nullptr};
            auto bond = ext::make_shared<DiscretizedDiscountBond>();
            return {ext::make_shared<DiscretizedOption>(bond, s.exerciseType, s.exerciseTimes), bond};
        }

        // The asset initialized at t0, after its underlying.
        Built initialized(const models::LatticeSpec& l, const AssetSpec& s, Time t0) {
            ext::shared_ptr<Lattice> lattice = models::makeLattice(l);
            Built b = makeAsset(s);
            if (b.underlying && s.underlyingTime)
                b.underlying->initialize(s.otherLattice ? models::makeLattice(l) : lattice, *s.underlyingTime);
            b.asset->initialize(lattice, t0);
            return b;
        }

        struct RollbackArgs {
            models::LatticeSpec lattice;
            AssetSpec asset;
            Time t0;
            std::vector<Time> times;
        };

        RollbackArgs rollbackArgs(const Args& a) {
            arity(a, 4);
            return {models::lattice(a[0]), assetSpec(a[1]), real(a[2]), reals(a[3])};
        }

        // After each rollback, the asset's time and values, and its underlying's.
        json::value rolledBack(const RollbackArgs& r, bool partial) {
            Built b = initialized(r.lattice, r.asset, r.t0);
            json::array times, values, underlyingTimes, underlyingValues;
            for (Time t : r.times) {
                if (partial)
                    b.asset->partialRollback(t);
                else
                    b.asset->rollback(t);
                times.emplace_back(b.asset->time());
                values.emplace_back(models::arrayResult(b.asset->values()));
                if (b.underlying) {
                    underlyingTimes.emplace_back(b.underlying->time());
                    underlyingValues.emplace_back(models::arrayResult(b.underlying->values()));
                }
            }
            json::object out{{"times", std::move(times)}, {"values", std::move(values)}};
            if (b.underlying) {
                out["underlyingTimes"] = std::move(underlyingTimes);
                out["underlyingValues"] = std::move(underlyingValues);
            }
            return json::value(std::move(out));
        }

        const Register unit({
            // rollback(lattice, asset, t0, [t, ...]): the asset initialized at t0, then rolled back to each time.
            {"DiscretizedAsset::rollback",
             [](const Args& a) -> Call {
                 RollbackArgs r = rollbackArgs(a);
                 return [=] { return rolledBack(r, false); };
             }},
            // partialRollback(lattice, asset, t0, [t, ...]): the same, without each rollback's last adjustment.
            {"DiscretizedAsset::partialRollback",
             [](const Args& a) -> Call {
                 RollbackArgs r = rollbackArgs(a);
                 return [=] { return rolledBack(r, true); };
             }},
            // presentValue(lattice, asset, t0, [t, ...]): the present value after each rollback.
            {"DiscretizedAsset::presentValue",
             [](const Args& a) -> Call {
                 RollbackArgs r = rollbackArgs(a);
                 return [=] {
                     Built b = initialized(r.lattice, r.asset, r.t0);
                     json::array out;
                     for (Time t : r.times) {
                         b.asset->rollback(t);
                         out.emplace_back(b.asset->presentValue());
                     }
                     return json::value(std::move(out));
                 };
             }},
            {"DiscretizedAsset::mandatoryTimes",
             [](const Args& a) -> Call {
                 arity(a, 1);
                 AssetSpec s = assetSpec(a[0]);
                 return [=] {
                     json::array out;
                     for (Time t : makeAsset(s).asset->mandatoryTimes())
                         out.emplace_back(t);
                     return json::value(std::move(out));
                 };
             }},
        });

    }

}
