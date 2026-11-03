/* SPDX-FileCopyrightText: 2026 The nanoql Authors
   SPDX-License-Identifier: Apache-2.0

   The C replayer (docs/bindings.md, "The vector suite through each binding"): replays the recorder's trace through
   nanoql's C functions, one line a call, and checks every result against C#'s. A fact replays up to its first call
   that needs something the trace couldn't record; the run reports facts replayed whole, calls replayed of calls
   recorded, refusals matched and mismatches, and passes at no mismatch.
   Usage: replay <trace.jsonl> [<facts report.tsv>] */
#include <ctype.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "replay.h"

struct rp {
    nql_handle* objs;
    int64_t nobjs;
    void* temps[64];
    int ntemps;
    int stop;
    long long line;
    long long mismatches, refusals, calls, replayed;
    const jv* after;
    const char* fn;
};

int r_stop(rp* r) { return r->stop; }

/* ---- the JSON of one line, parsed into an arena ---- */
static jv* arena;
static size_t arena_used, arena_cap;
static jv** slots;
static size_t slots_used, slots_cap;

static jv* node(void)
{
    if (arena_used == arena_cap) {
        fprintf(stderr, "a trace line holds more than %zu values\n", arena_cap);
        exit(3);
    }
    return &arena[arena_used++];
}

static const char* ws(const char* p)
{
    while (*p == ' ' || *p == '\t' || *p == '\r' || *p == '\n') p++;
    return p;
}

static const char* parse(const char* p, jv** out);

static const char* parse_items(const char* p, jv* v, char close, int object)
{
    jv* small[64];
    jv** local = small;
    int n = 0, cap = 64;
    p = ws(p);
    if (*p == close) return p + 1;
    for (;;) {
        for (int part = object ? 2 : 1; part > 0; part--) {
            jv* item;
            p = parse(ws(p), &item);
            if (n == cap) {
                cap *= 2;
                jv** grown = malloc(cap * sizeof(jv*));
                memcpy(grown, local, n * sizeof(jv*));
                if (local != small) free(local);
                local = grown;
            }
            local[n++] = item;
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
    if (slots_used + n > slots_cap) {
        fprintf(stderr, "a trace line holds too many values\n");
        exit(3);
    }
    v->items = &slots[slots_used];
    memcpy(v->items, local, n * sizeof(jv*));
    slots_used += n;
    v->count = object ? n / 2 : n;
    if (local != small) free(local);
    return p;
}

static const char* parse(const char* p, jv** out)
{
    jv* v = node();
    memset(v, 0, sizeof *v);
    *out = v;
    v->text = p;
    if (*p == '{') {
        v->type = JV_OBJ;
        return parse_items(p + 1, v, '}', 1);
    }
    if (*p == '[') {
        v->type = JV_ARR;
        return parse_items(p + 1, v, ']', 0);
    }
    if (*p == '"') {
        v->type = JV_STR;
        const char* q = p + 1;
        while (*q && *q != '"') q += *q == '\\' ? 2 : 1;
        v->text = p + 1;
        v->len = (int)(q - p - 1);
        return *q ? q + 1 : q;
    }
    if (*p == 'n') {
        v->type = JV_NULL;
        return p + 4;
    }
    if (*p == 't' || *p == 'f') {
        v->type = JV_BOOL;
        v->len = *p == 't';
        return p + (*p == 't' ? 4 : 5);
    }
    v->type = JV_NUM;
    const char* q = p;
    while (*q == '-' || *q == '+' || *q == '.' || *q == 'e' || *q == 'E' || (*q >= '0' && *q <= '9')) q++;
    v->len = (int)(q - p);
    return q;
}

static const jv* member(const jv* o, const char* key)
{
    if (!o || o->type != JV_OBJ) return NULL;
    size_t k = strlen(key);
    for (int i = 0; i < o->count; i++) {
        const jv* name = o->items[2 * i];
        if (name->len == (int)k && memcmp(name->text, key, k) == 0) return o->items[2 * i + 1];
    }
    return NULL;
}

const jv* jv_at(const jv* v, int i) { return v && v->type == JV_ARR && i < v->count ? v->items[i] : NULL; }

/* A JSON string's value, unescaped, in a buffer the call's temporaries free. */
static char* unescape(rp* r, const jv* v)
{
    char* s = malloc(v->len + 1);
    int o = 0;
    for (int i = 0; i < v->len; i++) {
        char c = v->text[i];
        if (c != '\\') {
            s[o++] = c;
            continue;
        }
        c = v->text[++i];
        switch (c) {
            case 'n': s[o++] = '\n'; break;
            case 't': s[o++] = '\t'; break;
            case 'r': s[o++] = '\r'; break;
            case 'b': s[o++] = '\b'; break;
            case 'f': s[o++] = '\f'; break;
            case 'u': {
                unsigned cp = (unsigned)strtoul((char[]){v->text[i + 1], v->text[i + 2], v->text[i + 3], v->text[i + 4], 0}, NULL, 16);
                i += 4;
                if (cp >= 0xD800 && cp < 0xDC00 && v->text[i + 1] == '\\' && v->text[i + 2] == 'u') {
                    unsigned lo = (unsigned)strtoul((char[]){v->text[i + 3], v->text[i + 4], v->text[i + 5], v->text[i + 6], 0}, NULL, 16);
                    cp = 0x10000 + ((cp - 0xD800) << 10) + (lo - 0xDC00);
                    i += 6;
                }
                if (cp < 0x80) s[o++] = (char)cp;
                else if (cp < 0x800) { s[o++] = (char)(0xC0 | (cp >> 6)); s[o++] = (char)(0x80 | (cp & 0x3F)); }
                else if (cp < 0x10000) { s[o++] = (char)(0xE0 | (cp >> 12)); s[o++] = (char)(0x80 | ((cp >> 6) & 0x3F)); s[o++] = (char)(0x80 | (cp & 0x3F)); }
                else { s[o++] = (char)(0xF0 | (cp >> 18)); s[o++] = (char)(0x80 | ((cp >> 12) & 0x3F)); s[o++] = (char)(0x80 | ((cp >> 6) & 0x3F)); s[o++] = (char)(0x80 | (cp & 0x3F)); }
                break;
            }
            default: s[o++] = c; break;
        }
    }
    s[o] = 0;
    if (r && r->ntemps < 64) r->temps[r->ntemps++] = s;
    return s;
}

/* ---- values ---- */
static double hexdouble(const jv* v)
{
    char buf[17];
    int n = v->len < 16 ? v->len : 16;
    memcpy(buf, v->text, n);
    buf[n] = 0;
    uint64_t u = strtoull(buf, NULL, 16);
    double d;
    memcpy(&d, &u, sizeof d);
    return d;
}

static int64_t num(const jv* v) { return v && v->type == JV_NUM ? strtoll(v->text, NULL, 10) : 0; }

static void* temp(rp* r, size_t bytes)
{
    void* p = calloc(bytes ? bytes : 1, 1);
    if (r->ntemps < 64) r->temps[r->ntemps++] = p;
    return p;
}

static long long shown = 20;
static long long stops = 0; /* how many facts' stops to print (NANOQL_REPLAY_STOPS) */
/* NANOQL_REPLAY_TOLERANCE and NANOQL_REPLAY_ABS_TOLERANCE: off the reference platform, a double within this relative
   difference, or else within this absolute one, is a match, each kind tallied with its widest. */
static double tolerance = 0, abs_tolerance = 0;
static long long near_doubles = 0, small_doubles = 0;
static double widest = 0, widest_abs = 0;

static void stopped(rp* r, const char* fact_name, const char* fn, const char* why)
{
    if (stops > 0) {
        stops--;
        printf("STOP line %lld %s: %s: %s\n", r->line, fact_name ? fact_name : "(no fact)", fn, why);
    }
}

static void mismatch(rp* r, const char* what, const char* want, const char* got)
{
    if (r->mismatches++ < shown) printf("MISMATCH line %lld %s: %s: recorded %s, replayed %s\n", r->line, r->fn, what, want, got);
}

static const jv* arg(rp* r, const jv* a, int i)
{
    const jv* v = jv_at(a, i);
    if (v && v->type == JV_OBJ) r->stop = 1; /* {"x": ...}: an argument the trace couldn't record */
    return v;
}

double rd_double(rp* r, const jv* a, int i) { const jv* v = arg(r, a, i); return v && v->type == JV_STR ? hexdouble(v) : 0; }
int32_t rd_int(rp* r, const jv* a, int i) { return (int32_t)num(arg(r, a, i)); }
int64_t rd_long(rp* r, const jv* a, int i) { return num(arg(r, a, i)); }
bool rd_bool(rp* r, const jv* a, int i) { const jv* v = arg(r, a, i); return v && v->type == JV_BOOL && v->len; }
const char* rd_str(rp* r, const jv* a, int i) { const jv* v = arg(r, a, i); return v && v->type == JV_STR ? unescape(r, v) : NULL; }
nql_date rd_date(rp* r, const jv* a, int i) { return (nql_date)num(arg(r, a, i)); }

nql_period rd_period(rp* r, const jv* a, int i)
{
    const jv* v = arg(r, a, i);
    nql_period p = {(int32_t)num(jv_at(v, 0)), (int32_t)num(jv_at(v, 1))};
    return p;
}

nql_complex rd_complex(rp* r, const jv* a, int i)
{
    const jv* v = arg(r, a, i);
    nql_complex c = {0, 0};
    if (v && v->type == JV_ARR) {
        c.re = hexdouble(jv_at(v, 0));
        c.im = hexdouble(jv_at(v, 1));
    }
    return c;
}

static nql_handle object_of(rp* r, const jv* v)
{
    if (!v || v->type == JV_NULL) return NULL;
    if (v->type == JV_OBJ) {
        r->stop = 1;
        return NULL;
    }
    int64_t id = num(v);
    if (id <= 0 || id >= r->nobjs || !r->objs[id]) {
        r->stop = 1; /* an object made by a call the replay didn't make */
        return NULL;
    }
    return r->objs[id];
}

nql_handle rd_obj(rp* r, const jv* a, int i) { return object_of(r, arg(r, a, i)); }

const double* rd_optdouble(rp* r, const jv* a, int i, double* s) { const jv* v = arg(r, a, i); if (!v || v->type == JV_NULL) return NULL; *s = hexdouble(v); return s; }
const int32_t* rd_optint(rp* r, const jv* a, int i, int32_t* s) { const jv* v = arg(r, a, i); if (!v || v->type == JV_NULL) return NULL; *s = (int32_t)num(v); return s; }
const int64_t* rd_optlong(rp* r, const jv* a, int i, int64_t* s) { const jv* v = arg(r, a, i); if (!v || v->type == JV_NULL) return NULL; *s = num(v); return s; }
const bool* rd_optbool(rp* r, const jv* a, int i, bool* s) { const jv* v = arg(r, a, i); if (!v || v->type == JV_NULL) return NULL; *s = v->len != 0; return s; }
const nql_period* rd_optperiod(rp* r, const jv* a, int i, nql_period* s) { const jv* v = arg(r, a, i); if (!v || v->type == JV_NULL) return NULL; *s = rd_period(r, a, i); return s; }
const nql_complex* rd_optcomplex(rp* r, const jv* a, int i, nql_complex* s) { const jv* v = arg(r, a, i); if (!v || v->type == JV_NULL) return NULL; *s = rd_complex(r, a, i); return s; }

static size_t size_of(int kind)
{
    switch (kind) {
        case K_DOUBLE: case K_OPTDOUBLE: return sizeof(double);
        case K_LONG: return sizeof(int64_t);
        case K_BOOL: return sizeof(bool);
        case K_PERIOD: return sizeof(nql_period);
        case K_STR: return sizeof(char*);
        case K_OBJ: return sizeof(nql_handle);
        case K_COMPLEX: return sizeof(nql_complex);
        default: return sizeof(int32_t);
    }
}

/* One element of a kind into p[k], its has-value flag into has[k]. */
static void element(rp* r, const jv* e, int kind, void* p, int k, bool* has)
{
    int null = !e || e->type == JV_NULL;
    if (has) has[k] = !null;
    switch (kind) {
        case K_DOUBLE: case K_OPTDOUBLE: ((double*)p)[k] = null ? 0 : hexdouble(e); break;
        case K_LONG: ((int64_t*)p)[k] = num(e); break;
        case K_BOOL: ((bool*)p)[k] = e && e->type == JV_BOOL && e->len; break;
        case K_PERIOD: ((nql_period*)p)[k] = (nql_period){(int32_t)num(jv_at(e, 0)), (int32_t)num(jv_at(e, 1))}; break;
        case K_STR: ((const char**)p)[k] = null ? NULL : unescape(r, e); break;
        case K_OBJ: ((nql_handle*)p)[k] = object_of(r, e); break;
        case K_COMPLEX: ((nql_complex*)p)[k] = (nql_complex){hexdouble(jv_at(e, 0)), hexdouble(jv_at(e, 1))}; break;
        default: ((int32_t*)p)[k] = null ? 0 : (int32_t)num(e); break;
    }
}

const void* rd_array(rp* r, const jv* a, int i, int kind, const bool** has, int32_t* count)
{
    const jv* v = arg(r, a, i);
    if (has) *has = NULL;
    if (!v || v->type != JV_ARR) {
        *count = 0;
        return NULL;
    }
    *count = v->count;
    void* p = temp(r, size_of(kind) * v->count);
    bool* h = (kind == K_OPTDOUBLE || kind == K_OPTINT) ? temp(r, v->count) : NULL;
    for (int k = 0; k < v->count; k++) element(r, v->items[k], kind, p, k, h);
    if (has) *has = h;
    return p;
}

double* rd_span(rp* r, const jv* a, int i, int kind, int32_t* count) { return (double*)rd_array(r, a, i, kind, NULL, count); }

const double* rd_matrix(rp* r, const jv* a, int i, int32_t* rows, int32_t* columns)
{
    const jv* v = jv_at(a, i);
    if (!v || v->type != JV_OBJ) {
        *rows = *columns = 0;
        return NULL;
    }
    *rows = (int32_t)num(member(v, "r"));
    *columns = (int32_t)num(member(v, "c"));
    const jv* values = member(v, "v");
    double* p = temp(r, sizeof(double) * (values ? values->count : 0));
    for (int k = 0; values && k < values->count; k++) p[k] = hexdouble(values->items[k]);
    return p;
}

const double* rd_jagged(rp* r, const jv* a, int i, int32_t* count, const int32_t** lengths, int32_t* rows)
{
    const jv* v = arg(r, a, i);
    if (!v || v->type != JV_ARR) {
        *count = *rows = 0;
        *lengths = NULL;
        return NULL;
    }
    int total = 0;
    for (int k = 0; k < v->count; k++) total += v->items[k]->count;
    double* p = temp(r, sizeof(double) * total);
    int32_t* l = temp(r, sizeof(int32_t) * v->count);
    int at = 0;
    for (int k = 0; k < v->count; k++) {
        l[k] = v->items[k]->count;
        for (int j = 0; j < v->items[k]->count; j++) p[at++] = hexdouble(v->items[k]->items[j]);
    }
    *count = total;
    *lengths = l;
    *rows = v->count;
    return p;
}

void rd_map(rp* r, const jv* a, int i, int kk, int vk, const void** keys, const void** values, const bool** has, int32_t* count)
{
    const jv* v = arg(r, a, i);
    if (!v || v->type != JV_ARR) {
        *keys = *values = NULL;
        *has = NULL;
        *count = 0;
        return;
    }
    void* k = temp(r, size_of(kk) * v->count);
    void* w = temp(r, size_of(vk) * v->count);
    bool* h = (vk == K_OPTDOUBLE || vk == K_OPTINT) ? temp(r, v->count) : NULL;
    for (int j = 0; j < v->count; j++) {
        element(r, jv_at(v->items[j], 0), kk, k, j, NULL);
        element(r, jv_at(v->items[j], 1), vk, w, j, h);
    }
    *keys = k;
    *values = w;
    *has = h;
    *count = v->count;
}

/* A tuple argument's JSON array, its elements read by the readers above; and a collection of tuples, one array per
   element (NULL for a null collection), with has-value flags for a nullable element. */
const jv* rd_tuple(rp* r, const jv* a, int i) { return arg(r, a, i); }

void rd_tuples(rp* r, const jv* a, int i, int n, const int* kinds, const void** items, const bool** has, int32_t* count)
{
    const jv* v = arg(r, a, i);
    for (int j = 0; j < n; j++) {
        items[j] = NULL;
        has[j] = NULL;
    }
    if (!v || v->type != JV_ARR) {
        *count = 0;
        return;
    }
    for (int j = 0; j < n; j++) {
        void* p = temp(r, size_of(kinds[j]) * v->count);
        bool* h = (kinds[j] == K_OPTDOUBLE || kinds[j] == K_OPTINT) ? temp(r, v->count) : NULL;
        for (int k = 0; k < v->count; k++) element(r, jv_at(v->items[k], j), kinds[j], p, k, h);
        items[j] = p;
        has[j] = h;
    }
    *count = v->count;
}

/* ---- checks ---- */
int st(rp* r, nql_status s, int recorded)
{
    if (s == recorded) {
        if (s != NQL_OK) r->refusals++;
        return s == NQL_OK;
    }
    char want[32], got[300];
    snprintf(want, sizeof want, "status %d", recorded);
    snprintf(got, sizeof got, "status %d (%.250s)", s, s ? nql_last_error_message() : "");
    mismatch(r, "status", want, got);
    return 0;
}

static int same_double(double a, double b)
{
    if (isnan(a) && isnan(b)) return 1;
    uint64_t x, y;
    memcpy(&x, &a, 8);
    memcpy(&y, &b, 8);
    if (x == y) return 1;
    double d = fabs(a - b), m = fmax(fabs(a), fabs(b));
    if (!isfinite(a) || !isfinite(b)) return 0;
    if (tolerance > 0 && d <= tolerance * m) {
        near_doubles++;
        if (d / m > widest) widest = d / m;
        return 1;
    }
    if (abs_tolerance > 0 && d <= abs_tolerance) {
        small_doubles++;
        if (d > widest_abs) widest_abs = d;
        return 1;
    }
    return 0;
}

static void hex(char* buf, double d)
{
    uint64_t u;
    memcpy(&u, &d, 8);
    snprintf(buf, 40, "%016llx (%.17g)", (unsigned long long)u, d);
}

void ck_double(rp* r, const jv* res, double v)
{
    double want = res && res->type == JV_STR ? hexdouble(res) : 0;
    if (!same_double(want, v)) {
        char a[64], b[64];
        hex(a, want);
        hex(b, v);
        mismatch(r, "double", a, b);
    }
}

void ck_int(rp* r, const jv* res, int64_t v)
{
    if (num(res) != v) {
        char a[32], b[32];
        snprintf(a, sizeof a, "%lld", (long long)num(res));
        snprintf(b, sizeof b, "%lld", (long long)v);
        mismatch(r, "integer", a, b);
    }
}

void ck_bool(rp* r, const jv* res, bool v)
{
    bool want = res && res->type == JV_BOOL && res->len;
    if (want != v) mismatch(r, "bool", want ? "true" : "false", v ? "true" : "false");
}

void ck_str(rp* r, const jv* res, char* v)
{
    const char* want = res && res->type == JV_STR ? unescape(r, res) : NULL;
    if ((want == NULL) != (v == NULL) || (want && strcmp(want, v) != 0)) mismatch(r, "string", want ? want : "null", v ? v : "null");
    nql_free(v);
}

void ck_period(rp* r, const jv* res, nql_period v)
{
    if (num(jv_at(res, 0)) != v.length || num(jv_at(res, 1)) != v.units) mismatch(r, "period", "", "");
}

void ck_complex(rp* r, const jv* res, nql_complex v)
{
    if (!same_double(hexdouble(jv_at(res, 0)), v.re) || !same_double(hexdouble(jv_at(res, 1)), v.im)) mismatch(r, "complex", "", "");
}

static void ck_element(rp* r, const jv* e, int kind, void* p, int k, bool* has)
{
    int null = !e || e->type == JV_NULL;
    if (has && null != !has[k]) {
        mismatch(r, "has-value", null ? "none" : "a value", has[k] ? "a value" : "none");
        return;
    }
    if (null && kind != K_OBJ && kind != K_STR) return;
    switch (kind) {
        case K_DOUBLE: case K_OPTDOUBLE: ck_double(r, e, ((double*)p)[k]); break;
        case K_LONG: ck_int(r, e, ((int64_t*)p)[k]); break;
        case K_BOOL: ck_bool(r, e, ((bool*)p)[k]); break;
        case K_PERIOD: ck_period(r, e, ((nql_period*)p)[k]); break;
        case K_STR: ck_str(r, e, ((char**)p)[k]); break;
        case K_OBJ: ck_obj(r, e, ((nql_handle*)p)[k]); break;
        case K_COMPLEX: ck_complex(r, e, ((nql_complex*)p)[k]); break;
        default: ck_int(r, e, ((int32_t*)p)[k]); break;
    }
}

void ck_opt(rp* r, const jv* res, bool has, int kind, const void* v)
{
    int null = !res || res->type == JV_NULL;
    if (null != !has) {
        mismatch(r, "has-value", null ? "none" : "a value", has ? "a value" : "none");
        return;
    }
    if (has) ck_element(r, res, kind, (void*)v, 0, NULL);
}

/* A type's name without each file-local class's compiler prefix (<File>F<hash>__, the hash of its source file's path),
   so a trace replays against a library built from another checkout. */
static void type_key(const char* s, char* out, size_t cap)
{
    size_t o = 0;
    while (*s && o + 1 < cap) {
        if (*s == '<') {
            const char* q = strchr(s, '>');
            if (q && q[1] == 'F') {
                const char* e = q + 2;
                while (isxdigit((unsigned char)*e)) e++;
                if (e > q + 2 && e[0] == '_' && e[1] == '_') {
                    s = e + 2;
                    continue;
                }
            }
        }
        out[o++] = *s++;
    }
    out[o] = 0;
}

static int same_type(const char* want, const char* got)
{
    char a[1024], b[1024];
    type_key(want, a, sizeof a);
    type_key(got, b, sizeof b);
    return strcmp(a, b) == 0;
}

void ck_obj(rp* r, const jv* res, nql_handle h)
{
    const jv* n = member(res, "n");
    const jv* s = member(res, "s");
    if (!res || res->type == JV_NULL) {
        if (h) {
            mismatch(r, "object", "null", "an object");
            nql_release(h);
        }
        return;
    }
    if (!h) {
        mismatch(r, "object", "an object", "null");
        return;
    }
    int64_t id = num(n ? n : s);
    if (id >= r->nobjs) {
        int64_t cap = r->nobjs ? r->nobjs : 1024;
        while (cap <= id) cap *= 2;
        r->objs = realloc(r->objs, cap * sizeof(nql_handle));
        memset(r->objs + r->nobjs, 0, (cap - r->nobjs) * sizeof(nql_handle));
        r->nobjs = cap;
    }
    if (n || !r->objs[id]) {
        /* A new object's class: the .NET type's full name C#'s had. */
        const jv* y = member(res, "y");
        if (n && y && y->type == JV_STR) {
            char* tn = NULL;
            const char* want = unescape(r, y);
            if (nql_type_name(h, &tn) != NQL_OK || !tn || !same_type(want, tn)) mismatch(r, "object type", want, tn ? tn : "(none)");
            nql_free(tn);
        }
        if (r->objs[id]) nql_release(r->objs[id]);
        r->objs[id] = h;
        return;
    }
    bool eq = false;
    nql_equals(r->objs[id], h, &eq);
    if (!eq) mismatch(r, "object", "the same object", "another");
    nql_release(h);
}

void ck_array(rp* r, const jv* res, int kind, void* p, bool* has, int32_t n)
{
    if (!res || res->type != JV_ARR) {
        if (p) mismatch(r, "array", "null", "an array");
    } else if (res->count != n) {
        char a[32], b[32];
        snprintf(a, sizeof a, "%d elements", res->count);
        snprintf(b, sizeof b, "%d elements", n);
        mismatch(r, "array", a, b);
    } else {
        for (int k = 0; k < n; k++) ck_element(r, res->items[k], kind, p, k, has);
    }
    nql_free(p);
    nql_free(has);
}

void ck_matrix(rp* r, const jv* res, double* p, int32_t rows, int32_t columns)
{
    if (num(member(res, "r")) != rows || num(member(res, "c")) != columns) mismatch(r, "matrix", "its shape", "another shape");
    else ck_array(r, member(res, "v"), K_DOUBLE, p, NULL, rows * columns), p = NULL;
    nql_free(p);
}

void ck_jagged(rp* r, const jv* res, double* p, int32_t* lengths, int32_t rows)
{
    if (!res || res->type != JV_ARR || res->count != rows) mismatch(r, "jagged array", "its rows", "other rows");
    else {
        int at = 0;
        for (int k = 0; k < rows; k++) {
            const jv* row = res->items[k];
            if (row->count != lengths[k]) {
                mismatch(r, "jagged array", "its row length", "another");
                break;
            }
            for (int j = 0; j < lengths[k]; j++) ck_double(r, row->items[j], p[at++]);
        }
    }
    nql_free(p);
    nql_free(lengths);
}

void ck_map(rp* r, const jv* res, int kk, int vk, void* keys, void* values, bool* has, int32_t n)
{
    if (!res || res->type != JV_ARR || res->count != n) mismatch(r, "map", "its pairs", "other pairs");
    else {
        for (int k = 0; k < n; k++) {
            ck_element(r, jv_at(res->items[k], 0), kk, keys, k, NULL);
            ck_element(r, jv_at(res->items[k], 1), vk, values, k, has);
        }
    }
    nql_free(keys);
    nql_free(values);
    nql_free(has);
}

/* A returned collection of tuples against the recorded list, a null tuple against null by its has-value flag, each
   element by its kind; frees the arrays. */
void ck_tuples(rp* r, const jv* res, int n, const int* kinds, void** items, bool* has, int32_t count)
{
    if (!res || res->type != JV_ARR) {
        if (items[0]) mismatch(r, "tuples", "null", "a collection");
    } else if (res->count != count) {
        char a[32], b[32];
        snprintf(a, sizeof a, "%d tuples", res->count);
        snprintf(b, sizeof b, "%d tuples", count);
        mismatch(r, "tuples", a, b);
    } else {
        for (int k = 0; k < count; k++) {
            const jv* e = res->items[k];
            int null = !e || e->type == JV_NULL;
            if (has && null != !has[k]) {
                mismatch(r, "has-value", null ? "none" : "a tuple", has[k] ? "a tuple" : "none");
                continue;
            }
            if (null) continue;
            for (int j = 0; j < n; j++) ck_element(r, jv_at(e, j), kinds[j], items[j], k, NULL);
        }
    }
    for (int j = 0; j < n; j++) nql_free(items[j]);
    nql_free(has);
}

void ck_distribution(rp* r, const jv* res, double* x, double* dx, double* density, double* cumulative, double* excess, double* average, int32_t n)
{
    double* parts[6] = {x, dx, density, cumulative, excess, average};
    for (int k = 0; k < 6; k++) ck_array(r, jv_at(res, k), K_DOUBLE, parts[k], NULL, n);
}

void ck_span(rp* r, int k, double* p, int32_t n)
{
    const jv* after = jv_at(r->after, k);
    if (!after || after->count != n) {
        mismatch(r, "span after the call", "its values", "another count");
        return;
    }
    for (int j = 0; j < n; j++) ck_double(r, after->items[j], p[j]);
}

void ck_span_ints(rp* r, int k, int32_t* p, int32_t n)
{
    const jv* after = jv_at(r->after, k);
    if (!after || after->count != n) {
        mismatch(r, "span after the call", "its values", "another count");
        return;
    }
    for (int j = 0; j < n; j++) ck_int(r, after->items[j], p[j]);
}

/* ---- the run ---- */
typedef struct {
    char* name;
    long long runs, whole, calls, replayed;
} fact;

static int by_name(const void* a, const void* b) { return strcmp(((const replay_entry*)a)->name, ((const replay_entry*)b)->name); }

static fact* find_fact(fact* facts, int nfacts, const char* name)
{
    for (int k = 0; k < nfacts; k++)
        if (strcmp(facts[k].name, name) == 0) return &facts[k];
    return NULL;
}

/* B4's floor (bindings/replay-floor.json): facts replayed whole, each with calls, at least as many as the floor's
   facts; calls replayed at least its calls; refusals matched; and each fact it names whole. 1 when the run is below. */
static int below_floor(const char* path, fact* facts, int nfacts, long long replayed, long long refusals)
{
    FILE* f = fopen(path, "rb");
    if (!f) {
        printf("floor: can't open %s\n", path);
        return 1;
    }
    fseek(f, 0, SEEK_END);
    long size = ftell(f);
    fseek(f, 0, SEEK_SET);
    char* text = malloc((size_t)size + 1);
    size_t got = fread(text, 1, (size_t)size, f);
    text[got] = 0;
    fclose(f);
    arena_used = slots_used = 0;
    jv* doc;
    parse(ws(text), &doc);
    const jv* floor_facts = member(doc, "facts");
    const jv* whole_facts = member(doc, "whole");
    long long floor_calls = num(member(doc, "calls"));
    int nfloor = floor_facts ? floor_facts->count : 0;
    int whole = 0, named = 1;
    for (int k = 0; k < nfacts; k++) whole += facts[k].whole == facts[k].runs && facts[k].calls > 0;
    for (int k = 0; whole_facts && k < whole_facts->count; k++) {
        char* name = unescape(NULL, whole_facts->items[k]);
        fact* x = find_fact(facts, nfacts, name);
        if (!x || x->whole != x->runs || x->calls == 0) {
            printf("floor: %s isn't replayed whole\n", name);
            named = 0;
        }
        free(name);
    }
    int ok = whole >= nfloor && replayed >= floor_calls && refusals > 0 && named;
    printf("floor: facts replayed whole with calls %d (floor %d); calls replayed %lld (floor %lld); refusals matched %lld; %s\n",
           whole, nfloor, replayed, floor_calls, refusals, ok ? "at or above it" : "BELOW IT");
    free(text);
    return !ok;
}

int main(int argc, char** argv)
{
    if (argc < 2) {
        fprintf(stderr, "usage: replay <trace.jsonl> [<facts report.tsv> [<floor.json>]]\n");
        return 2;
    }
    FILE* f = fopen(argv[1], "rb");
    if (!f) {
        fprintf(stderr, "can't open %s\n", argv[1]);
        return 2;
    }
    arena_cap = 1 << 22;
    arena = malloc(arena_cap * sizeof(jv));
    slots_cap = 1 << 22;
    slots = malloc(slots_cap * sizeof(jv*));
    size_t cap = 1 << 26;
    char* line = malloc(cap);
    rp r;
    memset(&r, 0, sizeof r);
    if (getenv("NANOQL_REPLAY_SHOW")) shown = atoll(getenv("NANOQL_REPLAY_SHOW")); /* how many mismatches to print */
    if (getenv("NANOQL_REPLAY_STOPS")) stops = atoll(getenv("NANOQL_REPLAY_STOPS"));
    if (getenv("NANOQL_REPLAY_TOLERANCE")) tolerance = atof(getenv("NANOQL_REPLAY_TOLERANCE"));
    if (getenv("NANOQL_REPLAY_ABS_TOLERANCE")) abs_tolerance = atof(getenv("NANOQL_REPLAY_ABS_TOLERANCE"));
    fact* facts = NULL;
    int nfacts = 0, capfacts = 0, current = -1, current_whole = 0;
    while (fgets(line, (int)cap, f)) {
        r.line++;
        size_t len = strlen(line);
        if (len == cap - 1) {
            fprintf(stderr, "line %lld is longer than %zu bytes\n", r.line, cap);
            return 3;
        }
        arena_used = slots_used = 0;
        jv* v;
        parse(ws(line), &v);
        const jv* name = member(v, "fact");
        if (name) {
            if (current >= 0 && current_whole) facts[current].whole++;
            char* fname = unescape(NULL, name);
            current = -1;
            for (int k = nfacts - 1; k >= 0 && k >= nfacts - 64; k--) {
                if (strcmp(facts[k].name, fname) == 0) {
                    current = k;
                    break;
                }
            }
            if (current < 0) {
                if (nfacts == capfacts) {
                    capfacts = capfacts ? capfacts * 2 : 1024;
                    facts = realloc(facts, capfacts * sizeof(fact));
                }
                facts[nfacts] = (fact){fname, 0, 0, 0, 0};
                current = nfacts++;
            } else {
                free(fname);
            }
            facts[current].runs++;
            current_whole = 1;
            r.stop = 0;
            continue;
        }
        r.calls++;
        if (current >= 0) facts[current].calls++;
        if (r.stop) continue;
        const jv* fn = member(v, "f");
        char key[512];
        int kl = fn && fn->len < 511 ? fn->len : 0;
        memcpy(key, fn ? fn->text : "", kl);
        key[kl] = 0;
        replay_entry probe = {key, NULL};
        const replay_entry* e = bsearch(&probe, replay_table, replay_table_count, sizeof(replay_entry), by_name);
        if (!e) {
            if (current_whole) stopped(&r, current >= 0 ? facts[current].name : NULL, key, "a function the library doesn't export");
            r.stop = 1;
            current_whole = 0;
            continue;
        }
        const jv* status = member(v, "t");
        r.after = member(v, "w");
        r.fn = e->name;
        int ok = e->handler(&r, member(v, "a"), member(v, "r"), status ? (int)num(status) : 0);
        for (int k = 0; k < r.ntemps; k++) free(r.temps[k]);
        r.ntemps = 0;
        if (!ok || r.stop) {
            if (current_whole) stopped(&r, current >= 0 ? facts[current].name : NULL, e->name, r.stop ? "an input the trace couldn't write or the replay didn't make" : "its call failed where C#'s didn't");
            r.stop = 1;
            current_whole = 0;
            continue;
        }
        r.replayed++;
        if (current >= 0) facts[current].replayed++;
    }
    if (current >= 0 && current_whole) facts[current].whole++;
    fclose(f);

    int whole = 0;
    for (int k = 0; k < nfacts; k++) whole += facts[k].whole == facts[k].runs;
    if (argc > 2) {
        FILE* out = fopen(argv[2], "wb");
        fprintf(out, "fact\truns\twhole\tcalls\treplayed\n");
        for (int k = 0; k < nfacts; k++) fprintf(out, "%s\t%lld\t%lld\t%lld\t%lld\n", facts[k].name, facts[k].runs, facts[k].whole, facts[k].calls, facts[k].replayed);
        fclose(out);
    }
    for (int64_t k = 0; k < r.nobjs; k++)
        if (r.objs[k]) nql_release(r.objs[k]);
    int64_t live = -1;
    nql_live_handles(&live);
    printf("C replay: facts replayed whole %d of %d; calls replayed %lld of %lld; refusals matched %lld; mismatches %lld; live handles at the end %lld\n",
           whole, nfacts, r.replayed, r.calls, r.refusals, r.mismatches, (long long)live);
    if (tolerance > 0 || abs_tolerance > 0)
        printf("tolerance %g relative, %g absolute: %lld doubles within the relative, not bit for bit, the widest %.3g; %lld more within the absolute, the widest %.3g\n",
               tolerance, abs_tolerance, near_doubles, widest, small_doubles, widest_abs);
    int below = argc > 3 && below_floor(argv[3], facts, nfacts, r.replayed, r.refusals);
    return r.mismatches == 0 && live == 0 && !below ? 0 : 1;
}
