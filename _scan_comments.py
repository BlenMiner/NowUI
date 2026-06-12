import sys, json

FILES = [
    "Assets/NowUI/Runtime/NowFont.cs",
    "Assets/NowUI/Runtime/NowFontCompiler.cs",
    "Assets/NowUI/Runtime/NowLayout.cs",
    "Assets/NowUI/Runtime/NowManagedFontBaker.cs",
    "Assets/NowUI/Runtime/NowManagedFontSession.cs",
    "Assets/NowUI/Runtime/NowMesh.cs",
    "Assets/NowUI/Runtime/NowTextShaper.cs",
    "Assets/NowUI/Runtime/NowTrueType.cs",
    "Assets/NowUI/Runtime/NowUI.cs",
    "Assets/NowUI/Runtime/NowUIDrawList.cs",
    "Assets/NowUI/Runtime/NowUIGraphic.cs",
    "Assets/NowUI/Runtime/NowUIInput.cs",
    "Assets/NowUI/Runtime/NowUITheme.cs",
]

def find_comments(text):
    """Return list of (start_index, end_index, kind) for comments outside strings.
    kind: 'line' for // (non-doc), 'doc' for ///, 'block' for /* */"""
    comments = []
    i = 0
    n = len(text)
    while i < n:
        c = text[i]
        if c == '"':
            # check raw string
            if text.startswith('"""', i):
                # raw string: count quotes
                q = 0
                while i + q < n and text[i+q] == '"':
                    q += 1
                closer = '"' * q
                j = text.find(closer, i + q)
                i = (j + q) if j != -1 else n
                continue
            i += 1
            while i < n:
                if text[i] == '\\':
                    i += 2
                    continue
                if text[i] == '"':
                    i += 1
                    break
                i += 1
            continue
        if c == "'":
            i += 1
            while i < n:
                if text[i] == '\\':
                    i += 2
                    continue
                if text[i] == "'":
                    i += 1
                    break
                if text[i] == '\n':
                    break
                i += 1
            continue
        if c == '@' and i + 1 < n and text[i+1] == '"':
            i += 2
            while i < n:
                if text[i] == '"':
                    if i + 1 < n and text[i+1] == '"':
                        i += 2
                        continue
                    i += 1
                    break
                i += 1
            continue
        if c == '$':
            # $" or $@" or @$"
            if text.startswith('$"', i):
                # interpolated: treat like regular string with \ escapes; "" not escape
                i += 2
                depth = 0
                while i < n:
                    ch = text[i]
                    if ch == '\\' and depth == 0:
                        i += 2
                        continue
                    if ch == '{':
                        if i + 1 < n and text[i+1] == '{':
                            i += 2
                            continue
                        depth += 1
                        i += 1
                        continue
                    if ch == '}':
                        if i + 1 < n and text[i+1] == '}' and depth == 0:
                            i += 2
                            continue
                        if depth > 0:
                            depth -= 1
                        i += 1
                        continue
                    if ch == '"' and depth == 0:
                        i += 1
                        break
                    i += 1
                continue
            if text.startswith('$@"', i) or text.startswith('@$"', i):
                i += 3
                while i < n:
                    if text[i] == '"':
                        if i + 1 < n and text[i+1] == '"':
                            i += 2
                            continue
                        i += 1
                        break
                    i += 1
                continue
            i += 1
            continue
        if c == '/' and i + 1 < n:
            if text[i+1] == '/':
                # line comment; doc if exactly ///
                is_doc = text.startswith('///', i) and not text.startswith('////', i)
                j = text.find('\n', i)
                end = j if j != -1 else n
                comments.append((i, end, 'doc' if is_doc else 'line'))
                i = end
                continue
            if text[i+1] == '*':
                j = text.find('*/', i + 2)
                end = (j + 2) if j != -1 else n
                # doc block /** */ — treat as block
                comments.append((i, end, 'block'))
                i = end
                continue
        i += 1
    return comments

if __name__ == '__main__':
    mode = sys.argv[1] if len(sys.argv) > 1 else 'list'
    for f in FILES:
        with open(f, encoding='utf-8-sig') as fh:
            text = fh.read()
        comments = find_comments(text)
        nondoc = [c for c in comments if c[2] != 'doc']
        if mode == 'count':
            print(f, len(nondoc))
            continue
        print(f"===== {f} ({len(nondoc)} non-doc comments) =====")
        for (s, e, k) in nondoc:
            line = text.count('\n', 0, s) + 1
            snippet = text[s:e].replace('\n', '\\n')
            if len(snippet) > 200:
                snippet = snippet[:200] + '...'
            print(f"  L{line} [{k}] {snippet}")
