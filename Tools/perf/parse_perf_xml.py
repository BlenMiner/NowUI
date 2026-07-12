import xml.etree.ElementTree as ET, json, re, sys

def parse(path):
    tree = ET.parse(path)
    out = {}
    for tc in tree.iter('test-case'):
        o = tc.find('output')
        if o is None or not o.text:
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
