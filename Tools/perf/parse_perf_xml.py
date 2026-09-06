import xml.etree.ElementTree as ET, json, re, sys

def parse(path):
    tree = ET.parse(path)
    out = {}
    for tc in tree.iter('test-case'):
        if tc.get('result') != 'Passed':
            continue
        o = tc.find('output')
        if o is None or not o.text:
            continue
        # The human-readable summary rounds sub-millisecond timings to two
        # decimals. Prefer the full-precision structured performance samples.
        structured = False
        for line in o.text.splitlines():
            if not line.startswith('##performancetestresult2:'):
                continue
            result = json.loads(line.split(':', 1)[1])
            for group in result.get('SampleGroups', []):
                if group.get('Name') != 'Time' or group.get('Unit') != 2:
                    continue
                out[tc.get('name')] = {
                    'min': group['Min'], 'median': group['Median'],
                    'max': group['Max'], 'avg': group['Average'],
                    'std': group['StandardDeviation'],
                }
                structured = True
                break
            if structured:
                break
        if structured:
            continue
        m = re.search(
            r'Time in Milliseconds\nMin:\t\t([\d.]+) ms\nMedian:\t\t([\d.]+) ms\nMax:\t\t([\d.]+) ms\nAvg:\t\t([\d.]+) ms\nStdDev:\t\t([\d.]+) ms',
            o.text)
        if m:
            out[tc.get('name')] = {
                'min': float(m.group(1)), 'median': float(m.group(2)),
                'max': float(m.group(3)), 'avg': float(m.group(4)), 'std': float(m.group(5)),
            }
    return out

if __name__ == '__main__':
    result = parse(sys.argv[1])
    if len(sys.argv) > 2:
        with open(sys.argv[2], 'w') as f:
            json.dump(result, f, indent=1, sort_keys=True)
    print(json.dumps(result, indent=1, sort_keys=True))
