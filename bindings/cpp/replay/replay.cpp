// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

// The C++ replayer (docs/bindings.md, "The vector suite through each binding"): replays the recorder's trace through
// nanoql.hpp, one line a call, each C function through its C++ member, and checks every result against C#'s. A fact
// replays up to its first call that needs something the trace couldn't write; the run reports facts replayed whole,
// calls replayed of calls recorded, refusals matched and mismatches, and passes at no mismatch and no live handle.
// Usage: replay <trace.jsonl> [<facts report.tsv>]
#include <algorithm>
#include <cctype>
#include <cinttypes>
#include <cstdio>
#include <cstdlib>
#include <memory>

#include "replay.hpp"

namespace replay {

// ---- the JSON of one line, parsed into an arena ----
namespace {
std::vector<jv> arena;
std::size_t arena_used = 0;
std::vector<jv*> slots;
std::size_t slots_used = 0;
long long shown = 20;  // how many mismatches to print (NANOQL_REPLAY_SHOW)
long long stops = 0;   // how many facts' stops to print (NANOQL_REPLAY_STOPS)

jv* node()
{
    if (arena_used == arena.size()) {
        std::fprintf(stderr, "a trace line holds more than %zu values\n", arena.size());
        std::exit(3);
    }
    jv* v = &arena[arena_used++];
    *v = jv{jt::null, nullptr, 0, 0, nullptr};
    return v;
}

const char* ws(const char* p)
{
    while (*p == ' ' || *p == '\t' || *p == '\r' || *p == '\n') p++;
    return p;
}

const char* parse(const char* p, jv** out);

const char* parse_items(const char* p, jv* v, char close, bool object)
{
    std::vector<jv*> local;
    p = ws(p);
    if (*p == close) return p + 1;
    for (;;) {
        for (int part = object ? 2 : 1; part > 0; part--) {
            jv* item = nullptr;
            p = parse(ws(p), &item);
            local.push_back(item);
            p = ws(p);
            if (part == 2 && *p == ':') p++;
        }
        if (*p == ',') {
            p++;
            continue;
        }
        if (*p == close) p++;
        break;
    }
    if (slots_used + local.size() > slots.size()) {
        std::fprintf(stderr, "a trace line holds too many values\n");
        std::exit(3);
    }
    v->items = &slots[slots_used];
    std::copy(local.begin(), local.end(), v->items);
    slots_used += local.size();
    v->count = static_cast<int>(object ? local.size() / 2 : local.size());
    return p;
}

const char* parse(const char* p, jv** out)
{
    jv* v = node();
    *out = v;
    v->text = p;
    switch (*p) {
    case '{': v->type = jt::object; return parse_items(p + 1, v, '}', true);
    case '[': v->type = jt::array; return parse_items(p + 1, v, ']', false);
    case '"': {
        v->type = jt::string;
        const char* q = p + 1;
        while (*q && *q != '"') q += *q == '\\' ? 2 : 1;
        v->text = p + 1;
        v->len = static_cast<int>(q - p - 1);
        return *q ? q + 1 : q;
    }
    case 'n': v->type = jt::null; return p + 4;
    case 't':
    case 'f':
        v->type = jt::boolean;
        v->len = *p == 't';
        return p + (*p == 't' ? 4 : 5);
    default: {
        v->type = jt::number;
        const char* q = p;
        while (*q == '-' || *q == '+' || *q == '.' || *q == 'e' || *q == 'E' || (*q >= '0' && *q <= '9')) q++;
        v->len = static_cast<int>(q - p);
        return q;
    }
    }
}

void utf8(std::string& s, unsigned cp)
{
    if (cp < 0x80) {
        s += static_cast<char>(cp);
    } else if (cp < 0x800) {
        s += static_cast<char>(0xC0 | (cp >> 6));
        s += static_cast<char>(0x80 | (cp & 0x3F));
    } else if (cp < 0x10000) {
        s += static_cast<char>(0xE0 | (cp >> 12));
        s += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
        s += static_cast<char>(0x80 | (cp & 0x3F));
    } else {
        s += static_cast<char>(0xF0 | (cp >> 18));
        s += static_cast<char>(0x80 | ((cp >> 12) & 0x3F));
        s += static_cast<char>(0x80 | ((cp >> 6) & 0x3F));
        s += static_cast<char>(0x80 | (cp & 0x3F));
    }
}

unsigned hex4(const char* p) { return static_cast<unsigned>(std::strtoul(std::string(p, 4).c_str(), nullptr, 16)); }
}  // namespace

const jv* at(const jv* v, int i) { return v && v->type == jt::array && i < v->count ? v->items[i] : nullptr; }

const jv* member(const jv* o, const char* key)
{
    if (!o || o->type != jt::object) return nullptr;
    std::size_t k = std::strlen(key);
    for (int i = 0; i < o->count; i++) {
        const jv* name = o->items[2 * i];
        if (name->len == static_cast<int>(k) && std::memcmp(name->text, key, k) == 0) return o->items[2 * i + 1];
    }
    return nullptr;
}

std::string unescape(const jv* v)
{
    std::string s;
    for (int i = 0; i < v->len; i++) {
        char c = v->text[i];
        if (c != '\\') {
            s += c;
            continue;
        }
        c = v->text[++i];
        switch (c) {
        case 'n': s += '\n'; break;
        case 't': s += '\t'; break;
        case 'r': s += '\r'; break;
        case 'b': s += '\b'; break;
        case 'f': s += '\f'; break;
        case 'u': {
            unsigned cp = hex4(v->text + i + 1);
            i += 4;
            if (cp >= 0xD800 && cp < 0xDC00 && v->text[i + 1] == '\\' && v->text[i + 2] == 'u') {
                cp = 0x10000 + ((cp - 0xD800) << 10) + (hex4(v->text + i + 3) - 0xDC00);
                i += 6;
            }
            utf8(s, cp);
            break;
        }
        default: s += c; break;
        }
    }
    return s;
}

double hexdouble(const jv* v)
{
    if (!v || v->type != jt::string) return 0.0;
    uint64_t u = std::strtoull(std::string(v->text, static_cast<std::size_t>(std::min(v->len, 16))).c_str(), nullptr, 16);
    double d;
    std::memcpy(&d, &u, sizeof d);
    return d;
}

int64_t num(const jv* v) { return v && v->type == jt::number ? std::strtoll(v->text, nullptr, 10) : 0; }

bool same_double(double a, double b)
{
    if (a != a && b != b) return true;
    uint64_t x, y;
    std::memcpy(&x, &a, 8);
    std::memcpy(&y, &b, 8);
    return x == y;
}

std::string hex(double d)
{
    uint64_t u;
    std::memcpy(&u, &d, 8);
    char buf[64];
    std::snprintf(buf, sizeof buf, "%016" PRIx64 " (%.17g)", u, d);
    return buf;
}

std::string type_key(const std::string& name)
{
    std::string out;
    for (std::size_t i = 0; i < name.size(); i++) {
        if (name[i] == '<') {
            std::size_t q = name.find('>', i);
            if (q != std::string::npos && q + 1 < name.size() && name[q + 1] == 'F') {
                std::size_t e = q + 2;
                while (e < name.size() && std::isxdigit(static_cast<unsigned char>(name[e]))) e++;
                if (e > q + 2 && name.compare(e, 2, "__") == 0) {
                    i = e + 1;
                    continue;
                }
            }
        }
        out += name[i];
    }
    return out;
}

// ---- the replay ----
void rp::mismatch(const std::string& what, const std::string& want, const std::string& got)
{
    if (mismatches++ < shown) std::printf("MISMATCH line %lld %s: %s: recorded %s, replayed %s\n", line, fn, what.c_str(), want.c_str(), got.c_str());
}

const jv* rp::arg(const jv* a, int i)
{
    const jv* v = at(a, i);
    if (v && v->type == jt::object) stop = true;  // {"x": ...}: an argument the trace couldn't record
    return v;
}

std::optional<nanoql::Matrix> rp::opt_matrix(const jv* a, int i)
{
    const jv* v = at(a, i);
    if (!v || v->type != jt::object) return std::nullopt;
    const jv* values = member(v, "v");
    std::vector<double> x;
    for (int k = 0; values && k < values->count; k++) x.push_back(hexdouble(values->items[k]));
    return nanoql::Matrix(static_cast<int32_t>(num(member(v, "r"))), static_cast<int32_t>(num(member(v, "c"))), std::move(x));
}

bool rp::st(int got, int recorded)
{
    if (got == recorded) {
        if (got != NQL_OK) refusals++;
        return got == NQL_OK;
    }
    mismatch("status", "status " + std::to_string(recorded), "status " + std::to_string(got) + (got ? std::string(" (") + (nql_last_error_message() ? nql_last_error_message() : "") + ")" : std::string()));
    return false;
}

void rp::after(int k, const std::vector<double>& v)
{
    const jv* w = at(after_values, k);
    if (!w || w->count != static_cast<int>(v.size())) {
        mismatch("span after the call", "its values", "another count");
        return;
    }
    for (int j = 0; j < w->count; j++) check(*this, w->items[j], v[static_cast<std::size_t>(j)]);
}

void rp::after(int k, const std::vector<int32_t>& v)
{
    const jv* w = at(after_values, k);
    if (!w || w->count != static_cast<int>(v.size())) {
        mismatch("span after the call", "its values", "another count");
        return;
    }
    for (int j = 0; j < w->count; j++) check(*this, w->items[j], v[static_cast<std::size_t>(j)]);
}

void rp::check_object(const jv* res, const nanoql::Object& o)
{
    const jv* n = member(res, "n");
    const jv* s = member(res, "s");
    if (!res || res->type == jt::null) {
        if (o) mismatch("object", "null", "an object");
        return;
    }
    if (!o) {
        mismatch("object", "an object", "null");
        return;
    }
    std::size_t id = static_cast<std::size_t>(num(n ? n : s));
    if (id >= objs.size()) objs.resize(std::max(id + 1, objs.size() * 2));
    if (n || !objs[id]) {
        // A new object's class: the .NET type's full name C#'s had.
        const jv* y = member(res, "y");
        if (n && y && y->type == jt::string) {
            std::string want = unescape(y), got = o.typeName();
            if (type_key(want) != type_key(got)) mismatch("object type", want, got);
        }
        objs[id] = o;
        return;
    }
    if (!(objs[id] == o)) mismatch("object", "the same object", "another");
}

}  // namespace replay

using namespace replay;

namespace {
struct fact {
    std::string name;
    long long runs = 0, whole = 0, calls = 0, replayed = 0;
};
}  // namespace

int main(int argc, char** argv)
{
    if (argc < 2) {
        std::fprintf(stderr, "usage: replay <trace.jsonl> [<facts report.tsv>]\n");
        return 2;
    }
    FILE* f = std::fopen(argv[1], "rb");
    if (!f) {
        std::fprintf(stderr, "can't open %s\n", argv[1]);
        return 2;
    }
    if (std::getenv("NANOQL_REPLAY_SHOW")) shown = std::atoll(std::getenv("NANOQL_REPLAY_SHOW"));
    if (std::getenv("NANOQL_REPLAY_STOPS")) stops = std::atoll(std::getenv("NANOQL_REPLAY_STOPS"));
    arena.resize(std::size_t{1} << 22);
    slots.resize(std::size_t{1} << 22);
    std::vector<entry> table;
    for (const entry* const* t = tables; *t; t++)
        for (const entry* e = *t; e->name; e++) table.push_back(*e);
    std::sort(table.begin(), table.end(), [](const entry& a, const entry& b) { return std::strcmp(a.name, b.name) < 0; });

    const std::size_t cap = std::size_t{1} << 26;
    std::unique_ptr<char[]> buffer(new char[cap]);
    char* line = buffer.get();
    rp r;
    std::vector<fact> facts;
    long long calls = 0, replayed = 0;
    int current = -1;
    bool whole = false;
    auto stopped = [&](const char* fn, const char* why) {
        if (stops > 0 && whole) {
            stops--;
            std::printf("STOP line %lld %s: %s: %s\n", r.line, current >= 0 ? facts[static_cast<std::size_t>(current)].name.c_str() : "(no fact)", fn, why);
        }
    };
    while (std::fgets(line, static_cast<int>(cap), f)) {
        r.line++;
        if (std::strlen(line) == cap - 1) {
            std::fprintf(stderr, "line %lld is longer than %zu bytes\n", r.line, cap);
            return 3;
        }
        arena_used = slots_used = 0;
        jv* v = nullptr;
        parse(ws(line), &v);
        if (const jv* name = member(v, "fact")) {
            if (current >= 0 && whole) facts[static_cast<std::size_t>(current)].whole++;
            std::string fname = unescape(name);
            current = -1;
            for (int k = static_cast<int>(facts.size()) - 1; k >= 0 && k >= static_cast<int>(facts.size()) - 64; k--) {
                if (facts[static_cast<std::size_t>(k)].name == fname) {
                    current = k;
                    break;
                }
            }
            if (current < 0) {
                facts.push_back(fact{fname});
                current = static_cast<int>(facts.size()) - 1;
            }
            facts[static_cast<std::size_t>(current)].runs++;
            whole = true;
            r.stop = false;
            continue;
        }
        calls++;
        if (current >= 0) facts[static_cast<std::size_t>(current)].calls++;
        if (r.stop) continue;
        const jv* fn = member(v, "f");
        std::string key = fn ? std::string(fn->text, static_cast<std::size_t>(fn->len)) : std::string();
        auto it = std::lower_bound(table.begin(), table.end(), key, [](const entry& e, const std::string& k) { return std::strcmp(e.name, k.c_str()) < 0; });
        if (it == table.end() || key != it->name) {
            stopped(key.c_str(), "a function the layer doesn't have");
            r.stop = true;
            whole = false;
            continue;
        }
        const jv* status = member(v, "t");
        r.after_values = member(v, "w");
        r.fn = it->name;
        int ok = it->run(r, member(v, "a"), member(v, "r"), status ? static_cast<int>(num(status)) : 0);
        if (!ok || r.stop) {
            stopped(it->name, r.stop ? "an input the trace couldn't write or the replay didn't make" : "its call failed where C#'s didn't");
            r.stop = true;
            whole = false;
            continue;
        }
        replayed++;
        if (current >= 0) facts[static_cast<std::size_t>(current)].replayed++;
    }
    if (current >= 0 && whole) facts[static_cast<std::size_t>(current)].whole++;
    std::fclose(f);

    long long whole_facts = 0;
    for (const fact& x : facts) whole_facts += x.whole == x.runs;
    if (argc > 2) {
        FILE* out = std::fopen(argv[2], "wb");
        std::fprintf(out, "fact\truns\twhole\tcalls\treplayed\n");
        for (const fact& x : facts) std::fprintf(out, "%s\t%lld\t%lld\t%lld\t%lld\n", x.name.c_str(), x.runs, x.whole, x.calls, x.replayed);
        std::fclose(out);
    }
    r.objs.clear();
    int64_t live = nanoql::liveHandles();
    std::printf("C++ replay: facts replayed whole %lld of %zu; calls replayed %lld of %lld; refusals matched %lld; mismatches %lld; live handles at the end %" PRId64 "\n",
                whole_facts, facts.size(), replayed, calls, r.refusals, r.mismatches, live);
    return r.mismatches == 0 && live == 0 ? 0 : 1;
}
