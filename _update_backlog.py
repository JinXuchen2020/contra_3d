import re

with open('_os_memory/tasks/backlog.yaml', 'r', encoding='utf-8') as f:
    content = f.read()

tasks = [
    'T-BDD-ADOPT-6253be',
    'T-BDD-ADOPT-a24844',
    'T-BDD-ADOPT-e4c384',
    'T-BDD-ADOPT-085420',
    'T-BDD-ADOPT-de7a62',
    'T-BDD-ADOPT-4e320d',
    'T-BDD-ADOPT-93b81f',
    'T-BDD-ADOPT-f9c88f',
]

result_text = 'BDD adoption tests implemented (12 BDD scenarios), all pass (35/35 HUDUpdaterTests, 170 total)'
timestamp = '2026-09-06T01:35:00+08:00'

for task_id in tasks:
    pattern = r'(- task_id: ' + task_id + r'\n  title:.*?)(\n  status: todo)'
    repl = '\n  status: completed\n  completed_at: "' + timestamp + '"\n  result: ' + result_text
    content, count = re.subn(pattern, repl, content, flags=re.DOTALL)
    print(task_id + ': ' + ('updated' if count > 0 else 'NOT FOUND'))

with open('_os_memory/tasks/backlog.yaml', 'w', encoding='utf-8') as f:
    f.write(content)
print('Done')
