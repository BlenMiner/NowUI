import codecs
from _scan_comments import FILES, find_comments

KEEP = {
    "NowFont.cs": ["Fixed-size sessions never resize"],
    "NowFontCompiler.cs": ["link the plugin statically"],
    "NowLayout.cs": ["A default-stretched cross axis"],
    "NowManagedFontBaker.cs": ["Nonzero winding"],
    "NowManagedFontSession.cs": ["Blit each cell"],
    "NowMesh.cs": ["Same packing as AddRect"],
    "NowTextShaper.cs": ["link the plugin statically"],
    "NowTrueType.cs": [
        "collection's first font",
        "Unicode-meaningful platforms",
        "empty glyph (whitespace) is valid",
        "Point-matching composites",
    ],
    "NowUI.cs": ["Snap to pixels by rounding EDGES"],
    "NowUIDrawList.cs": ["deferred overlays land inside"],
    "NowUIInput.cs": [
        "Windows reports wheel ticks",
        "Only treat the touchscreen as the pointer",
    ],
    "NowUIGraphic.cs": [],
    "NowUITheme.cs": [],
}

def process(path):
    name = path.split('/')[-1]
    keeps = KEEP.get(name, [])

    with open(path, 'rb') as fh:
        raw = fh.read()
    bom = raw.startswith(codecs.BOM_UTF8)
    text = raw.decode('utf-8-sig')

    comments = [c for c in find_comments(text) if c[2] == 'line']
    lines = text.splitlines(keepends=True)
    line_starts = []
    pos = 0
    for ln in lines:
        line_starts.append(pos)
        pos += len(ln)

    # classify each comment: line index, whole-line or trailing
    items = []
    for (s, e, _k) in comments:
        li = text.count('\n', 0, s)
        prefix = text[line_starts[li]:s]
        items.append({'line': li, 'start': s, 'whole': prefix.strip() == ''})

    # group consecutive whole-line comments into blocks
    blocks = []
    for it in items:
        if it['whole'] and blocks and blocks[-1][-1]['whole'] and blocks[-1][-1]['line'] == it['line'] - 1:
            blocks[-1].append(it)
        else:
            blocks.append([it])

    delete_lines = set()
    truncate = {}   # line index -> truncation char offset within line
    kept = []
    removed = 0

    for block in blocks:
        block_text = '\n'.join(
            text[it['start']:line_starts[it['line']] + len(lines[it['line']])].rstrip('\r\n')
            for it in block)
        if any(k in block_text for k in keeps):
            kept.append(block_text)
            continue
        removed += 1
        for it in block:
            if it['whole']:
                delete_lines.add(it['line'])
            else:
                col = it['start'] - line_starts[it['line']]
                truncate[it['line']] = col

    out = []
    skip_next_blank = False
    for i, ln in enumerate(lines):
        if i in delete_lines:
            # double-blank collapse: previous kept line blank and next line blank
            nxt = i + 1
            while nxt in delete_lines:
                nxt += 1
            prev_blank = out and out[-1].strip() == ''
            next_blank = nxt < len(lines) and lines[nxt].strip() == '' and nxt not in delete_lines
            if prev_blank and next_blank:
                skip_next_blank = True
            continue
        if skip_next_blank and ln.strip() == '':
            skip_next_blank = False
            continue
        skip_next_blank = False
        if i in truncate:
            ending = ln[len(ln.rstrip('\r\n')):]
            ln = ln[:truncate[i]].rstrip() + ending
        out.append(ln)

    result = ''.join(out)
    data = result.encode('utf-8')
    if bom:
        data = codecs.BOM_UTF8 + data
    with open(path, 'wb') as fh:
        fh.write(data)

    print(f"{name}: removed {removed} comment(s)/block(s), kept {len(kept)}")
    for k in kept:
        print("  KEPT: " + " / ".join(s.strip() for s in k.splitlines()))

for f in FILES:
    process(f)
