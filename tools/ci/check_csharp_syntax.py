#!/usr/bin/env python3
"""Синтаксическая проверка всех .cs мода (tree-sitter-c-sharp) — без игры и .NET. Запуск: python3 tools/ci/check_csharp_syntax.py"""
import sys,glob
from tree_sitter import Language, Parser
import tree_sitter_c_sharp as cs
p=Parser(Language(cs.language()))
bad=0;n=0
for f in sorted(glob.glob('mod/**/*.cs',recursive=True) + glob.glob('tools/balance/Trace/**/*.cs',recursive=True)):
    n+=1
    t=p.parse(open(f,'rb').read())
    errs=[]
    def walk(node):
        if node.type=='ERROR' or node.is_missing:
            errs.append(node)
            return
        if node.has_error:
            for c in node.children: walk(c)
    walk(t.root_node)
    for e in errs[:3]:
        bad+=1
        print(f"{f}:{e.start_point[0]+1}:{e.start_point[1]+1} {'MISSING '+e.type if e.is_missing else 'ERROR'} {open(f,'rb').read()[e.start_byte:e.start_byte+60]!r}")
print(n,'files',bad,'errors')
sys.exit(1 if bad else 0)
