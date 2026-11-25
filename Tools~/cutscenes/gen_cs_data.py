"""웹 game.js의 컷신 대본을 C#으로 옮긴다 → Assets/Scripts/Game/CutsceneData.cs (손으로 고치지 않는다, 웹이 바뀌면 다시 만든다).
사용: python gen_cs_data.py <game.js> <CutsceneData.cs>"""
import sys, json, subprocess, os
here = os.path.dirname(os.path.abspath(__file__))
data = json.loads(subprocess.run(['node', os.path.join(here, 'export_cs.js'), sys.argv[1]], capture_output=True, text=True, encoding='utf-8', check=True).stdout)
def num(v): s = repr(float(v)); return s.replace('e', 'E') if 'e' in s else s
def cstr(s): return json.dumps(s, ensure_ascii=False)   # C# 문자열 (따옴표·역슬래시 이스케이프는 JSON과 같다)
def frames(f): return 'new[] { ' + ', '.join(str(int(x)) for x in (f if isinstance(f, list) else [f])) + ' }' + (', true' if isinstance(f, list) else ', false')
out = ['// 자동 생성: Tools~/cutscenes/gen_cs_data.py (웹 game.js의 CUTSCENES와 같다). 고치지 말고 다시 만든다.',
       'namespace Stairs.Game', '{', '    public static class CutsceneData', '    {',
       '        public static readonly System.Collections.Generic.Dictionary<string, CsShot[]> All = new System.Collections.Generic.Dictionary<string, CsShot[]>', '        {']
for cid, cs in data.items():
    out.append(f'            ["{cid}"] = new[]')
    out.append('            {')
    for sh in cs['shots']:
        parts = [f'd = {num(sh["d"])}']
        if 'bg' in sh: parts += [f'bg = {cstr(sh["bg"][0])}', f'bw = {num(sh["bg"][1])}', f'bh = {num(sh["bg"][2])}']
        for k in ('fg', 'card', 'finc', 'foutc'):
            if k in sh: parts.append(f'{k} = {cstr(sh[k])}')
        for k in ('fin', 'fout'):
            if k in sh: parts.append(f'{k} = {num(sh[k])}')
        if 'cam' in sh:
            parts.append('cam = new[] { ' + ', '.join('new CsCam(' + ', '.join(num(v) for v in c[:4]) + (f', {cstr(c[4])}' if len(c) > 4 else '') + ')' for c in sh['cam']) + ' }')
        if 'act' in sh:
            acts = []
            for a in sh['act']:
                keys = ', '.join('new CsKey(' + num(k[0]) + ', ' + frames(k[1]) + ', ' + ', '.join(num(v) for v in k[2:7]) + (f', {cstr(k[7])}' if len(k) > 7 else '') + ')' for k in a['k'])
                acts.append(f'new CsAct {{ s = {cstr(a["s"])}, z = {num(a.get("z", 1))}, fps = {num(a.get("fps", 6))}, once = {str(bool(a.get("once"))).lower()}, flip = {str(bool(a.get("flip"))).lower()}, k = new[] {{ {keys} }} }}')
            parts.append('act = new[] {\n                    ' + ',\n                    '.join(acts) + ' }')
        if 'cap' in sh:
            parts.append('cap = new[] { ' + ', '.join(f'new CsCap({num(c[0])}, {num(c[1])}, {cstr(c[2])}, {cstr(c[3])})' for c in sh['cap']) + ' }')
        if 'fx' in sh:
            fx = []
            for f in sh['fx']:
                t, kind = f[0], f[1]
                if kind == 'sfx': fx.append(f'new CsFx({num(t)}, "sfx", id: {cstr(f[2])})')
                elif kind == 'shake': fx.append(f'new CsFx({num(t)}, "shake", a: {num(f[2])}, d: {num(f[3])})')
                elif kind == 'flash': fx.append(f'new CsFx({num(t)}, "flash", d: {num(f[2])}, color: {cstr(f[3])})')
                else: fx.append(f'new CsFx({num(t)}, {cstr(kind)}, d: {num(f[2])})')
            parts.append('fx = new[] { ' + ', '.join(fx) + ' }')
        out.append('                new CsShot { ' + ', '.join(parts) + ' },')
    out.append('            },')
out += ['        };', '    }', '}', '']
open(sys.argv[2], 'w', encoding='utf-8', newline='\n').write('\n'.join(out)); print('cutscenes ->', sys.argv[2], sum(len(c['shots']) for c in data.values()), 'shots')
