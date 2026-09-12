"""Build offline dates from HKO's annual text tables (no runtime network access)."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import urllib.request, re, hashlib, json, time

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / 'artifacts/calendar-source'
CACHE.mkdir(parents=True, exist_ok=True)
TERMS = '小寒 大寒 立春 雨水 驚蟄 春分 清明 穀雨 立夏 小滿 芒種 夏至 小暑 大暑 立秋 處暑 白露 秋分 寒露 霜降 立冬 小雪 大雪 冬至'.split()
SIMPLIFIED = '小寒 大寒 立春 雨水 惊蛰 春分 清明 谷雨 立夏 小满 芒种 夏至 小暑 大暑 立秋 处暑 白露 秋分 寒露 霜降 立冬 小雪 大雪 冬至'.split()

def fetch(year):
    url = f'https://www.hko.gov.hk/tc/gts/time/calendar/text/files/T{year}c.txt'
    file = CACHE / f'{year}.txt'
    if not file.exists():
        for attempt in range(3):
            try:
                file.write_bytes(urllib.request.urlopen(url, timeout=30).read()); break
            except Exception:
                if attempt == 2: raise
                time.sleep(1)
    raw = file.read_bytes()
    rows = []
    for line in raw.decode('utf-8-sig').splitlines():
        match = re.match(r'(\d+)年(\d+)月(\d+)日\s+\S+\s+星期\S+\s+(\S+)', line)
        if match:
            y, m, d, term = match.groups()
            if term in TERMS:
                rows.append(f'{int(y):04}-{int(m):02}-{int(d):02}\t{SIMPLIFIED[TERMS.index(term)]}')
    assert len(rows) == 24, (year, len(rows))
    return rows, dict(year=year, url=url, sha256=hashlib.sha256(raw).hexdigest())

with ThreadPoolExecutor(max_workers=4) as pool:
    results = list(pool.map(fetch, range(2026, 2100)))
out = ROOT / 'src/LuoTianyiPet.Core/SolarTerms.tsv'
out.write_text('\n'.join(row for rows, _ in results for row in rows) + '\n', encoding='utf-8')
(ROOT / 'docs/validation/solar-terms-source.json').write_text(json.dumps({
    'source': 'Hong Kong Observatory; Hong Kong civil dates (UTC+08:00)',
    'note': 'Remote future dates near midnight may be revised. No official working-day arrangements included.',
    'files': [meta for _, meta in results], 'outputSha256': hashlib.sha256(out.read_bytes()).hexdigest()
}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'{len(results)*24} verified terms; {out.stat().st_size} bytes')
