"""Export the complete official prefix table and localized effect templates.

Colors and final numbers are native runtime calculations, not fixed row fields.
This reference CSV does not drive the in-game editor, which reads g.conf.
"""
import csv
import json
from pathlib import Path
import sys

project = Path(__file__).resolve().parents[2]
folder = Path(sys.argv[1]) if len(sys.argv) > 1 else next(
    (project.parent.parent / 'Mod').rglob('BattleSkillPrefixValue.json')
).parent
rows = json.loads((folder / 'BattleSkillPrefixValue.json').read_text(encoding='utf-8-sig'))
texts = {row['key']: row['ch'] for row in json.loads(
    (folder / 'LocalText.json').read_text(encoding='utf-8-sig'))}
types = {'1': '武技/灵技', '2': '绝技', '3': '身法', '4': '心法', '5': '神通'}
output = project / 'docs/reference/martial-prefix-catalog.csv'
output.parent.mkdir(exist_ok=True)
with output.open('w', encoding='utf-8-sig', newline='') as stream:
    writer = csv.writer(stream)
    writer.writerow(['配置ID', '类型', '招式ID(0为通用)', '词条编号', '中文效果模板',
                     '前置/互斥', '最大重复数量', '生成分组', '数值公式键', '境界1至11随机权重'])
    for row in rows:
        writer.writerow([row['id'], types.get(row['skillType'], row['skillType']),
                         row['skillID'], row['number'], texts.get(row['desc'], row['desc']),
                         row['require'], row['multi'], row['selectType'], row['prefixKey'],
                         '|'.join(row['weight' + str(grade)] for grade in range(1, 12))])
assert len({row['id'] for row in rows}) == len(rows), 'Duplicate configuration IDs'
print(f'Exported {len(rows)} complete configuration rows to {output}')
