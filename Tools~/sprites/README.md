# 스프라이트 만들기 (32비트)

`demo_api/assets`의 시트·배경은 여기서 만든다 (손으로 고치지 않는다).

1. `src16/` — 16비트(2배 밀도) 원본. 이전 단계(`puppet.py` → `upgrade16.py` → `edge_close.py`, Unity 판 `Tools~/`)의 결과물
2. `python tools/sprites/fix_arms.py tools/sprites/src16` — 팔·손 해부학 고치기 (몸통에 박힌 손 조각 지우기, 막기 2 몸통)
3. `python tools/sprites/upgrade32.py tools/sprites/src16 demo_api/assets` — 4배 밀도: Scale2x, 1px 외곽선, 8단계 부피 명암·림 라이트, 머리 윤기·목 그늘. `pixel_scale` 4
4. `python tools/qa/edge_close.py demo_api/assets` — 프레임 끝에서 잘린 곳을 1px 외곽선으로
5. `python tools/qa/run_all.py --fast` — 스프라이트·애니메이션·해부학 QA
