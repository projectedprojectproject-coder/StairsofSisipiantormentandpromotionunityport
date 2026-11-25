// game.js의 컷신 대본(CUTSCENES)을 JSON으로 꺼낸다 (Unity CutsceneData.cs를 만들 때 씀). 사용: node export_cs.js <game.js>
'use strict';
const fs = require('fs');
const src = fs.readFileSync(process.argv[2], 'utf8');
const i = src.indexOf('const CUTSCENES = {'), j = src.indexOf('\n};\n', i);
const body = src.slice(i + 'const CUTSCENES = '.length, j + 2);
const CUTSCENES = (new Function('return ' + body))();
process.stdout.write(JSON.stringify(CUTSCENES));
