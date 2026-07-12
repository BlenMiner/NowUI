import json, sys

def load(p):
    with open(p) as f:
        return json.load(f)

def fmt_smoke(base, new):
    rows = []
    b = {s['name']: s for s in base['scenarios']}
    n = {s['name']: s for s in new['scenarios']}
    for name in b:
        if name not in n:
            continue
        bm, nm = b[name]['averageMilliseconds'], n[name]['averageMilliseconds']
        delta = (nm - bm) / bm * 100 if bm else 0
        rows.append((name, bm, nm, delta, b[name].get('batchCount'), n[name].get('batchCount')))
    return rows

def fmt_tests(base, new):
    rows = []
    for name in sorted(base):
        if name not in new:
            continue
        bm, nm = base[name]['median'], new[name]['median']
        delta = (nm - bm) / bm * 100 if bm else 0
        rows.append((name, bm, nm, delta))
    return rows

if __name__ == '__main__':
    mode, basep, newp = sys.argv[1], sys.argv[2], sys.argv[3]
    base, new = load(basep), load(newp)
    if mode == 'smoke':
        print(f"{'scenario':<38}{'base ms':>9}{'new ms':>9}{'delta':>9}{'batches':>12}")
        for name, bm, nm, d, bb, nb in fmt_smoke(base, new):
            print(f"{name:<38}{bm:>9.3f}{nm:>9.3f}{d:>+8.1f}%{str(bb)+'->'+str(nb):>12}")
    else:
        print(f"{'test (median ms)':<55}{'base':>8}{'new':>8}{'delta':>9}")
        for name, bm, nm, d in fmt_tests(base, new):
            print(f"{name:<55}{bm:>8.3f}{nm:>8.3f}{d:>+8.1f}%")
