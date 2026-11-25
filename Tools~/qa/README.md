# QA 검사기

| 파일 | 무엇을 보나 |
|---|---|
| `qa_physics.js` | 플레이어 움직임·상호작용. `game.js`를 node에서 봇·사람 흉내로 돌리며 프레임마다 그리기 자리(`QA_HOOK`)를 world 좌표로 기록하고 본다: 플레이어 순간이동, 맞거나 막힌 물건이 그 순간 플레이어에 닿아 있었는지, 물건 순간이동, 떨어지는 모니터가 위로 가지 않는지, 의자가 몸을 맞지 않고 지나가지 않는지, 의자가 허공에 내려앉지 않는지 |
| `sprite_qa.py` | 스프라이트 픽셀: 프레임 테두리에서 잘림(외곽선 없음), 반투명 픽셀, 떨어진 점, 행마다 색 수, 인물 키 일관성, 쓰는 칸이 비었는지, 배경 크기·투명 |
| `anim_qa.py` | 애니메이션: 빈 프레임, 조각남, 발 기준선, 프레임 사이 흔들림, 되풀이 이음매, 멈춘 동작(같은 그림 연속), 좌우 짝 거울 모양(*_left ↔ *_right), fps 범위, 몸을 가르는 외곽선 |
| `run_all.py` | 모든 QA를 한 번에 (위 표의 셋, 단위 테스트, 파이프라인 QA 에이전트 4개). 하나라도 문제면 종료 코드 1 |
| `anatomy_qa.py` | 사람 스프라이트 해부학: 손이 둘을 넘지 않는지(몸통에 박힌 팔), 손이 어깨에서 팔 길이 안인지, 옷자락 모서리에 붙은 손 조각 |
| `cutscene_qa.js` | 컷신 대본: 쓰는 그림·시트·소리가 있는지, 프레임 번호, 키·카메라·자막 시각, 카메라가 배경 밖을 보지 않는지, 배우가 화면에 보이는지, 자막 폭·읽을 시간, 소리 효과를 자막으로 쓰지 않았는지, 길이 (화면은 `tools/cutscenes/cs_gallery.py`) |
| `edge_close.py` | 고치기: 프레임 테두리에 닿아 잘려 보이는 픽셀을 외곽선으로 닫는다 (`upgrade16.py` 다음에 돌린다) |

**한 번에 모두**: `python tools/qa/run_all.py` (Unity 판: `python Tools~/qa/run_all.py`, WebReference + Unity EditMode 테스트) — 아래 세 검사기 + `pytest tests/` + 파이프라인 QA 에이전트(Developer QA, Designer QA, Publisher Playwright 동작 확인, Screens QA)를 지금 게임(`demo_api`)에 돌린다. `--fast`는 Publisher·Screens QA를 뺀다.

```
python tools/qa/run_all.py
node tools/qa/qa_physics.js demo_api/game.js
python tools/qa/sprite_qa.py demo_api/assets
python tools/qa/anim_qa.py demo_api/assets
```

문제가 없으면 `problems: 0` / `문제 0개`로 끝나고, 있으면 종료 코드 1. Unity 판에는 같은 파일이 `Tools~/qa/`에 있다 (`WebReference/`를 대상으로 돌린다).
