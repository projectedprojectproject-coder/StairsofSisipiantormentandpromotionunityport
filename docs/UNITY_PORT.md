# Unity 이식 기록 (1단계: 핵심 루프 → 2단계: 경쟁 사원·저장 → 웹과 똑같이)

2026-11-25. 웹 PoC(`demo_api/game.js`)를 Unity로 옮긴 기록이다. 처음에는 웹 저장소의 `unity/StairsOfPromotion/`에서 시작했고,
지금은 이 폴더만 따로 [StairsofSisipiantormentandpromotionunityport](https://github.com/projectedprojectproject-coder/StairsofSisipiantormentandpromotionunityport) 저장소로 낸다.
그림과 소리는 새로 만들지 않고 웹 판의 것(`WebReference/assets`, 웹 저장소에서는 `demo_api/assets`)을 가져와 쓴다.

## 여는 방법

1. Unity Hub → Add → 이 폴더. **Unity 6.6 (6000.6.10f1)** 로 확인했다.
2. Active Input Handling은 ProjectSettings에 **Input System Package**로 들어 있다 (기본값 Old면 키가 하나도 먹지 않는다).
3. 메뉴 **Stairs > Import Web Build Assets**: `WebReference/assets`(없으면 웹 저장소의 `output/build/assets` → `demo_api/assets`)의
   PNG 시트를 픽셀 아트 설정(Point, 압축 없음, 크기 늘리지 않음)으로 가져와 프레임별로 자르고, ogg를 가져와 `Assets/Imported/SpriteCatalog.asset`을 만든다.
4. 메뉴 **Stairs > Create Game Scene** → `Assets/Scenes/Game.unity` → Play.
5. 글꼴: 웹 `txt()`의 `"Noto Sans KR"`과 같은 글꼴을 `Assets/Resources/Fonts`에 넣었다 (보통 400, 굵게 700, 가족 이름 "Stairs Noto Sans KR", SIL OFL, `OFL.txt`).
6. Window > General > Test Runner > EditMode 에서 `ParityTests`(14개)를 돌린다.

조작과 명령줄은 웹과 같다: ← → 오르기, Q E 막기, Esc 일시정지, Enter·Space 결정, 방향키·Tab·마우스로 고르기. `-seed N`, `-bot`, `-mute` (웹 `?seed=`, `?bot=1`, `?mute=1`).

## 구조

| 폴더 | 내용 | 웹 PoC에서 |
|---|---|---|
| `Assets/Scripts/Core` (Stairs.Core, UnityEngine 없음) | `StairsConfig` 수치, `Mulberry32` 난수, `StairLayout` 계단·금 간 계단·젖은 계단·감전 주기, `RunSimulation` 한 판의 규칙(오르기, 패링, 콤보·배수, 일수, 하트, 승급, 위협 8종, 경쟁 사원 쫓아오기, 카메라 플래시, 떠오르는 글자·불꽃, 봇). 상태는 double | `CONFIG`, `mulberry32`, `ensureStairs`, `isCracked`, `isWet`, `shockState`, `startRun`, `doStep`, `doParry`, `updatePlay`, `spawnAttack`, `updateRival`, `rivalPos`, `addText`, `botTick` |
| `Assets/Scripts/Game` (Stairs.Game) | `GameController` 화면·버튼·입력·소리·그리기 전부, `WebCanvas` 웹 2D 캔버스 흉내(GL), `AudioDirector`, `SaveData`, `SpriteCatalog`, `PlaytestDriver` | `screen`, `getButtons`, `keydown`/`mousedown`, `syncAudio`, `drawTitle`~`drawResult`, `shareResult`, `save` |
| `Assets/Resources` | `StairsCanvas.shader`(그림·글자·회색 필터), `Fonts/` Noto Sans KR 보통·굵게 | 캔버스, CSS filter, 글꼴 |
| `Assets/Editor` | `StairsAssetImporter`(에셋 가져오기, 씬 만들기, 명령줄 빌드), `MiniJson` | Publisher의 연결 단계 |
| `Assets/Tests/EditMode` | `ParityTests` 14개, `WebRuns.txt`(웹 판 사건 기록) | Publisher 동작 확인 |
| `WebReference/` | 비교 기준인 웹 판 사본(`index.html`, `game.js`, `assets/`). Unity는 가져오지 않는다 | 웹 판 그대로 |
| `Tools~` (Unity가 가져오지 않음) | `web_expect.js`(웹 판 사건 기록 → `WebRuns.txt`), `rival_expect.js`(테스트 기대값), `web_shots.py`(웹 화면을 PlaytestDriver와 같은 순서로 찍기) | |

## 2단계에서 바뀐 것 (2026-11-25)

- **C# 검토**: Unity·.NET이 없어 눈으로 읽어 확인했다. 컴파일 오류는 찾지 못했고, 대신 그리기 버그 하나를 고쳤다:
  1단계는 i번째 칸을 Unity y = `-i * stepH`에 두어 계단이 화면 **아래로** 내려갔다. 이제 Core는 웹 좌표(아래가 +y)를 쓰고
  `GameController.W2U`/`StairUY`가 Unity 쪽(위가 +y)으로 뒤집는다. uGUI를 붙이며 `UnityEngine.UI`를 `Stairs.Game.asmdef`에 더했다.
- **경쟁 사원 쫓아오기**: 웹 `updateRival`/`rivalPos`를 `RunSimulation`에 옮겼다 (Core는 여전히 UnityEngine 없음).
  화면 끝(`camX ± (viewW/2 + 16)`)에서 가장 가까운 층 바닥(`floorStep`)을 `rivalSpeed` 240px/초로 걸어오고, 플레이어가 그 바닥보다
  높으면 계단 아래 끝에서 `rivalClimb` 10칸/초로 바로 뒤 칸까지 쫓아 오른다. 치기 `rivalLeap` 0.3초 전에 막을 쪽이 잠기며,
  계단에서는 **플레이어가 마지막으로 오른 방향의 반대쪽**이다. 아직 따라잡지 못했으면 붙을 때까지 공격이 미뤄진다.
  패링 타이밍 소리와 봇은 잠긴 뒤에만 반응한다(웹 `p.locked !== false`). `StairsConfig`에 `rivalLeap`, `rivalSpeed`, `rivalClimb`,
  `showRivalCameo`, `viewW/viewH`를 웹 값으로 더했다.
- 경쟁 사원이 들어오는 자리가 카메라에 기대므로 웹의 `pvx/pvy/camX/camY` 따라가기, 연출 타이머(`hitTimer`, `stepTimer`, `shake`,
  `hurtFlash`), 패링 불꽃·물러나는 경쟁 사원·장식 경쟁 사원(`rivalIdx`)도 Core로 옮겼다. 덕분에 `decor` 난수를 웹과 같은 순서로 써서
  카메라 플래시 시각도 웹과 맞는다. `Bot = true`면 웹처럼 `Tick` 안에서 봇이 돈다.
- **uGUI HUD·메뉴** (IMGUI 제거): `GameUi`가 320x180 논리 화면을 코드로 만들고 `CanvasScaler`(Expand)로 키운다. 배치는 웹 05-1~05-5 그대로:
  층·직급 명판(사원증 아이콘), D-day 명판(달력 아이콘, 7일 이하 빨강, 일수 게이지), 하트(`heart`/`heart_empty`), 콤보 "N COMBO / 점수 ×M"(×10이면 빨강),
  조작 키 아이콘(`arrow_left/right`, `shield_q/e`, 누르면 올리브), 감전 예고, 승급·층 문구, 맞으면 붉게 번쩍.
  로비는 `bg_lobby` 위에 1F·D-30 명판, 사원증(최고 직급), 화이트보드 기록, 모드 버튼 3개(연습 · 업무 시작 · 무한)와 고른 모드 설명.
  결과는 `bg_result` 위에 큰 플레이어(구조조정은 회색, 상자 들고 나감), 도장(구조조정 빨강·승급 올리브: 0.15초 뒤 0.22초 동안 크게 떨어져 찍히고,
  흔들림·잉크 튐·`sfx_hit`), 인사 평가 표, NEW 표시, 도장 뒤 떠오르는 TIP 띠. 키보드 포커스와 마우스 클릭(EventSystem + InputSystemUIInputModule을 없으면 만든다) 둘 다 된다.
- **저장**: `SaveData`(PlayerPrefs, 웹 localStorage와 같은 키 `stairs_best_floor`, `stairs_best_score`, `stairs_best_endless`)와 소리 켜기·끄기(`stairs_muted`).
  보통 모드는 최고 층·점수, 무한 모드는 최고 층만. 연습 모드는 기록하지 않는다(웹은 연습도 보통 기록에 섞는다: 웹 쪽 후속 과제).
- **일시정지**: Esc 또는 창이 가려지면 멈춘다. 계속하기 · 조작법(웹 drawHelp 내용) · 소리 켜짐/꺼짐 · 메인으로.
- **테스트**: 기존 6개는 그대로 두고(봇 테스트는 `Bot = true`로 웹과 같은 순서), 경쟁 사원 5개를 더했다. 기대값은 웹 `game.js`를 node로 돌려 뽑았고
  (`Tools~/rival_expect.js`), 웹 봇은 seed 1~4 모두 경쟁 사원과 함께 100층 CEO에 닿는 것을 node로 확인했다.
  (플레이테스트 뒤 Core를 double로 바꿔 지금은 프레임 번호까지 정확히 맞춘다.)

## 플레이테스트와 고친 것 (2026-11-25, Unity 없이)

Unity를 설치하지 못해(샌드박스가 Program Files에 쓰지 못함) Unity 없이 두 갈래로 확인했다.

1. **규칙 플레이테스트 (node)**: `Tools~/cs_core.js`는 C# Core(`Mulberry32`, `StairLayout`, `RunSimulation`)를 한 줄씩 옮긴 JS다.
   `node unity/StairsOfPromotion/Tools~/playtest.js demo_api/game.js` 가 웹 `game.js`와 이것을 같은 seed·모드로 1/60초씩 돌려
   사건 기록(공격, 경쟁 사원 잠금, 패링, 하트, 층, 승급, 끝난 까닭)과 점수를 맞대어 본다.
   - 봇: 보통 6판(모두 100층 CEO), 무한 3판(4분, 196층), 연습 2판 / 사람 흉내: 오르기만(하트 소진, 무한은 감전), 가만히(하트 소진), 틀린 쪽(하트 소진), 느리게.
   - **처음 결과: 보통·무한 9판이 웹과 달랐다** (예: seed 1 점수 웹 110,160 / Unity 146,700). 규칙은 같았다:
     `cs_core.js`를 double로 돌리면 11판이 모두 같았다. 원인은 C# `float`였다. 2분 동안 시간·봇 타이머가 쌓이며 오차가 커지고,
     `0.33f`, `0.19f` 같은 수치가 웹의 0.33, 0.19와 조금씩 달라 한두 프레임씩 밀렸다. 그 결과 경쟁 사원 잠금이 0.15초 늦어지고
     오르는 순간의 배수가 달라 점수가 갈렸다.
   - **고침**: Core 상태를 모두 `double`로 바꾸고, 인스펙터의 float 수치는 `StairsConfig.D(v) = (double)(decimal)v`로 읽는다
     (`(double)0.33f`는 0.32999998이지만 `(double)(decimal)0.33f`는 0.33). 이제 위 19판이 **모두 웹과 프레임 단위로 같다**.
     `GameController`도 웹처럼 1/60초 고정 간격(`RunSimulation.Step60`, 한 번에 최대 0.1초)으로 `Tick`한다.
   - `ParityTests`: 경쟁 사원 테스트의 ±1~2 허용을 없애고 웹 값과 정확히 비교한다. 한 판 전체(보통 seed 1·2, 무한 7, 연습 10)의
     점수·패링 수·최고 콤보를 웹과 비교하는 `BotRun_MatchesWebFrameForFrame`, 수치 읽기 `ConfigValues_ReadAsWebDoubles`를 더했다.
2. **코드 다시 읽기**로 찾아 고친 Unity 쪽 버그:
   - 끝난 뒤 결과로 넘어가기 전 1초(endDelay)에 Esc를 누르면 일시정지가 열리고 "계속하기"가 끝난 판을 거부해 **일시정지 화면에 갇혔다**.
     이제 그 1초에는 일시정지하지 않고, 계속하기도 끝난 판을 받아 결과로 넘어간다.
   - 버튼을 마우스로 누른 뒤 Enter를 치면 EventSystem의 Submit과 키보드 처리가 겹쳐 **두 번 실행**됐다. 버튼의 방향키 이동을 끄고 누른 뒤 선택을 푼다.
   - `SpriteCatalog.Get`에 음수 시간이 들어오면 **배열 밖 예외**가 났다 (경쟁 사원은 `minAttackGap` 때문에 등장 시각이 미뤄지면 생길 수 있다).
     웹처럼 0으로 자르고, 한 번만 도는 동작(step, parry, hit, box_exit, proud, attack, leave)은 웹 `ONCE`처럼 마지막 프레임에 멈춘다 (결과 화면에서 상자 들고 나가는 동작이 되풀이되던 것).
   - 연습 모드에 웹에는 없는 계단이 그려졌다 → 그리지 않는다. 층 바닥(콘크리트 슬래브)을 그려 경쟁 사원이 허공을 걷지 않게 했다.
   - "!" 예고가 경쟁 사원에게만 있었다 → 모든 위협 위에 "!", 커피 위에는 "막지 말라"는 빨간 ×.
   - 카메라 플래시가 사라지지 않고 같은 밝기였다 → 웹처럼 줄어든다. 맞았을 때 붉은 번쩍임을 웹 세기(0.35)로, 금 간 계단은 흔들림과 함께 붉어진다.
   - 비활성 패널에서 `GetComponentsInChildren`이 테두리를 못 찾던 것(`includeInactive`).

## Unity 6.6에서 실제로 확인 (2026-11-25)

Unity 6.6을 설치한 뒤 명령줄로 컴파일·테스트·빌드·자동 플레이까지 돌렸다.

```
Unity.exe -batchmode -nographics -projectPath unity/StairsOfPromotion -runTests -testPlatform EditMode -testResults results.xml
Unity.exe -batchmode -projectPath unity/StairsOfPromotion -executeMethod Stairs.EditorTools.StairsAssetImporter.BuildPlaytestPlayer -stairsBuild <폴더> -quit
<폴더>/Stairs.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -stairsPlaytest <스크린샷 폴더>
```

- **컴파일**: Stairs.Core / Stairs.Game / Stairs.Editor / Stairs.Tests.EditMode 모두 오류·경고 없음.
- **EditMode 테스트 16개 모두 통과** (한 판 전체를 웹과 비교하는 4개, 경쟁 사원 5개 포함).
- **자동 플레이** (`PlaytestDriver`, `-stairsPlaytest`가 있을 때만 켜짐): 타이틀 → 로비 → 보통 모드 봇 100층(승급, 110,160점: 웹과 같음)
  → 일시정지 → 결과 → 가만히 있는 판(하트 소진, 구조조정) → 연습 → 무한 → 기록이 남은 로비까지 13장을 찍고, 오류·예외 0개.
- 이렇게 찾아 고친 것:
  - **시트 그림이 하나도 안 나옴**: 가져오기가 시트를 자르기 전에 잰 크기가 Unity 기본 설정 때문에 2의 거듭제곱으로 늘어난 크기
    (hud_icons 216x24 → 256x32)라, 자른 칸이 그림 밖으로 나가 버려졌다. 한 줄짜리 시트(경쟁 사원, 투척물, HUD 아이콘, 소품)는 전부,
    나머지는 한 줄씩 빠져 계단·플레이어가 그려지지 않았다. PNG 머리에서 실제 크기를 읽고 `npotScale = None`, 못 자른 칸은 오류로 알린다.
  - **키 입력이 안 먹을 설정**: ProjectSettings의 Active Input Handling이 Old(0)였다 → Input System(1).
  - 가져오기가 `output/build/assets`만 찾아 이 저장소에서는 폴더 고르기 창이 떴다 → `demo_api/assets`도 찾는다 (명령줄에서는 창 대신 오류).
  - `ScreenCapture` 모듈이 없어 추가 (`com.unity.modules.screencapture`).
  - 로비의 모드 설명 글이 사원증 오른쪽 끝과 겹쳤다.
- 글꼴은 비워 둔 채로 OS 글꼴(맑은 고딕)로 한글이 잘 나왔다. 픽셀 글꼴을 쓰려면 여전히 `uiFont`에 TTF를 넣는다.

## 웹과 똑같이 (2026-11-25)

uGUI로 비슷하게 그리던 화면을 버리고, 웹 `game.js`의 그리기·메뉴·입력·소리를 **한 줄씩** 옮겼다.

- `WebCanvas`: 웹 캔버스의 `rect`(반올림, `globalAlpha`를 1로 되돌리는 것까지), `frameRect`, `txt`(정렬·기준선·그림자),
  `drawSprite`(기준점 bc/c/tc, 에셋 밀도 PX로 1/PX 반올림, 뒤집기, 회전·눌림, 없는 그림이면 색 사각형), `drawImg`,
  `save/restore/translate/rotate/scale`, CSS `grayscale(1) brightness(0.85) contrast(0.85)`를 GL 즉시 그리기로 옮겼다.
  화면 배율은 웹 `resize()`와 같이 PX의 정수배, 가운데 정렬, 바깥은 `#1E1F22`.
- `GameController`: `drawTitle`(출근하는 사원들, 로고, 톱니바퀴), `drawLobby`/`drawIdCard`, `drawPlayWorld`(층 바닥·소품·계단 받침·디딤판·
  층 번호·난간, 젖은 칸 번개, 금 간 칸 흔들림, 경쟁 사원 장식·물러남·덤벼듦, 위협 8종의 비행 경로·회전·서류가방 폭발, "!"·커피 ×,
  불꽃, "+1.5"·"커피!"·층 문구, 모니터 형광등 깜박임, 맞을 때 붉은 번쩍임, 카메라 플래시), `drawHud`, `drawPause`, `drawSettings`(소리·볼륨·전체화면),
  `drawHelp`, `drawResult`(도장·잉크·표·NEW·TIP·토스트), `shareResult`(결과 카드 PNG 저장 → 사진 폴더), `getButtons`, 키보드·마우스(가리키면 포커스),
  `syncAudio`(구간 음악, 일시정지 때 0.4배, 멈췄다 이어 틀기), 기록(`stairs_best_*`).
- Core에 웹의 `texts`(떠오르는 글자)와 화면 좌표 불꽃을 더했다. 웹과 같은 decor 난수 순서를 지킨다.
- 웹처럼 다른 창을 눌러도 계속 돌고(`runInBackground`), 창을 내렸을 때만 일시정지한다 (`visibilitychange`).
- 확인: `PlaytestDriver`(`-stairsPlaytest`)와 `Tools~/web_shots.py`가 같은 seed·같은 계단 수에서 19장씩 찍어 맞대어 봤다.
  가만히 있는 화면(타이틀·로비·조작법·결과·연습)은 픽셀의 0.6~3.4%만 다르다 (글자 가장자리, 찍은 순간이 몇 프레임 다른 걷는 사원).

맞대어 보며 찾아 고친 것:
- **캐릭터가 뚱뚱하고 애니메이션이 흔들림**: Unity가 시트 칸을 Tight 메시로 자르면 `Sprite.textureRect`가 투명한 가장자리를 뺀 사각형
  (플레이어 32px 칸 중 19.85px)이 된다. 그것을 칸 크기로 그려 플레이어·경쟁 사원·출근하는 사원이 1.6배쯤 넓어지고, 프레임마다 잘린 폭이
  달라 흔들렸다. 이제 칸 전체(`sprite.rect`)를 그리고, 가져올 때 `SpriteMeshType.FullRect`로 자른다.
- **글자가 가늘고 작은 글자가 깨짐**: 설치된 Noto Sans KR은 가변 글꼴(기본 굵기 100)이라 Unity가 굵기를 못 고른다. fontTools로 400·700 굵기를
  고정한 파일을 넣었고, 이름이 "Noto Sans KR"이면 Unity가 깔린 시스템 글꼴을 대신 쓰므로 가족 이름을 "Stairs Noto Sans KR"로 바꿨다 (OFL 허용).
- **빠짐없이 가져오기**: 가져오기 끝에 정의의 시트 9·이미지 10·소리 26, 모든 칸, 모든 동작의 프레임이 들어왔는지 확인하고 빠진 것을 오류로 알린다.
  (소리는 웹이 먼저 쓰는 .ogg를 가져온다. 같은 소리의 .mp3는 웹에서 ogg를 못 틀 때만 쓰는 대체 파일이다.)
- **소리**: 빌드에서 실제 출력 크기를 재어 봤다 (`-stairsAudioCheck`): 타이틀에서 bgm_loop·amb_office, 플레이에서 bgm_stage1이 울리고
  출력 RMS 약 0.13 (처음 0.5초는 장치 지연으로 무음). 플레이테스트 기록에 효과음별로 튼 횟수를 남긴다. `-mute`로 켜면 웹 `?mute=1`처럼 소리가 없다.

## 장애물 보기와 실행 파일 (2026-11-25)

- 웹 판에 새로 넣은 **장애물 보기**(game.js `DEMOS`/`openDemo`/`demoUpdate`/`drawDemo`)를 한 줄씩 옮겼다. 12장면(계단 오르기, 결재판, 경쟁 사원,
  커피, 모니터, 전화기, 서류, 포스트잇, 서류가방, 금 간 계단, 젖은 계단, 카메라 플래시)이 따로 만든 판(seed 7)에서 실제 규칙으로 돌고,
  자동 조작이 맞는 키를 맞는 순간에 누른다. 설정 화면의 "장애물 · 보기"(설정은 타이틀·로비·결과의 톱니바퀴와 일시정지 메뉴)와 조작법 화면의 "장애물 보기"에서 연다. 진행 중인 판·기록에는 영향이 없다.
  새 장애물 후보 25가지는 `docs/OBSTACLE_IDEAS.md` (웹 저장소와 같은 문서).
  `PlaytestDriver`와 `web_shots.py`가 12장면을 같은 순간에 찍어 맞대어 봤다 (같은 장면, 몇 프레임 차이). `WebReference/game.js`도 새 웹 판으로 바꿨다.
- **실행 파일**: `StairsAssetImporter.BuildWindowsGame` (메뉴 Stairs > Build Windows Game). 창 1280x720, 크기 바꾸기 가능, 스플래시 없음,
  제품 이름 "승급의 계단". 약 113MB (zip 41MB). 실행 파일에서도 소리 출력(RMS 0.13)과 오류 0개를 확인했다.

## 새 장애물 5가지 (2026-11-25)

웹 판(`claude/obstacle-demo` 브랜치)에 넣은 것을 그대로 옮겼다. 규칙은 Core, 그림·입력·장애물 보기 장면은 GameController.

| 장애물 | 층 | 규칙 |
|---|---|---|
| 상사의 눈 | 61층~ (보통·무한) | 61층에 처음 닿은 때부터 예고 5초(화면이 붉어지고 먼 배경에 불꽃 하나가 나타났다 사라짐, 눈은 안 보임) → 그 불꽃에서 거대한 레이저가 부채꼴로 퍼져 0.6초 만에 화면을 쓸어내림 → 6초 쉼. 레이저가 몸에 걸칠 때 오르면 하트 -1 |
| 내려가는 에스컬레이터 | 20층~ (무한 4층~) | 쇠빛 칸. 0.6초 서 있으면 한 칸 밀려 내려간다 |
| 사라지는 칸 | 41층~ (무한 5층~) | 하늘색 선 칸. 모두 같은 박자로 5초 있고 1초 없어진다. 없을 때 그 칸에 있으면 아래로 떨어지고 하트 -1 |
| 스테이플러 | 15층~ | 막으면 반대쪽에서 0.9초 뒤 돌아온다. 반대쪽 키로 한 번 더 |
| 종이비행기 편대 | 20층~ | 4대가 0.2초 간격. 날아오는 쪽 막기 키를 꾹 누르고 있어야 한다 (`RunSimulation.held`, Input System `isPressed`) |

- **웹과 같은지**: 예전의 JS 옮김(`cs_core.js`, `playtest.js`)은 지웠다. 대신 `Tools~/web_expect.js`가 웹 판 10판(보통 봇 3, 무한 봇, 연습 봇, 오르기만 2, 가만히, 틀린 쪽, 느리게)의
  사건 기록을 `Assets/Tests/EditMode/WebRuns.txt`로 뽑고, `WebRuns_MatchFrameForFrame`이 같은 입력을 진짜 C# Core에 넣어 한 줄씩 맞대어 본다 (새 장애물 사건 포함). 통과.
- 보통 모드 봇은 6판 모두 100층에 닿는다. 무한 모드 봇은 81층(젖은 칸 바로 다음이 사라지는 칸)에서 끝난다.

## 경쟁 사원 × 계단 장애물 (2026-11-25)

웹 판(`claude/obstacle-demo`)과 같다. Core `RunSimulation.RivalBlocked`·`RivalTileHazards`, 그림은 `GameController`(떨어지는 경쟁 사원 `fallers`, 기다리는 경쟁 사원 머리 위 "…").

- 기다리기: 다음 칸이 사라졌거나 0.5초 안에 사라지면(`rivalVanishWait`), 젖은 칸에 전기가 흐르거나 곧 흐르면 그 앞 칸에서 멈춘다. 플레이어를 늦게 따라잡으니 치는 때도 늦어진다.
- 당하기: 경쟁 사원이 한 칸에 서 있을 때(보통 플레이어 바로 뒤 칸에서 덤비기 전) 금 간 칸 0.8초(`rivalCrackTime`) → 떨어짐, 젖은 칸에 전기 → 감전, 사라지는 칸이 없어짐 → 떨어짐.
  당한 경쟁 사원은 치지 못하고 사라지며 막기 점수(50)만큼 덤 점수, 콤보는 그대로.
- 에스컬레이터 칸에 0.6초 서 있으면 경쟁 사원도 한 칸 밀려 내려간다.
- 장애물 보기 장면 2개 더 (19장면): 경쟁 사원과 사라지는 칸(356칸), 경쟁 사원과 금 간 칸(212칸).
- 확인: `WebRuns.txt`에 장면 3개(seed 7, 356·212·326칸에 서서 경쟁 사원 1명)를 더해 `rival wait`·`rival fall` 사건까지 웹과 한 줄씩 맞대어 본다.
  봇 판에서는 경쟁 사원이 판마다 1~3번 기다리고, 봇이 금 간 칸에 오래 서 있지 않아 떨어지는 일은 거의 없다.

## 상사의 눈 레이저, 종이비행기, 스테이플러, 장애물별 소리 (2026-11-25)

웹 판과 같게 고쳤다.

- 상사의 눈: 눈 그림을 없앴다. 예고 5초 동안 화면이 붉어지고 먼 배경(`GazeSrcX`·`GazeSrcY`)에 불꽃 하나가 1.25초마다 천천히 나타났다 사라지며(상사의 눈인지 아닌지 모르게) 자리는 번갈아 왼쪽·오른쪽, 창문은 그리지 않는다.
  훑을 때는 그 불꽃에서 쏜 거대한 레이저가 부채꼴로 퍼지며 불꽃 높이부터 화면 아래 끝까지 쓸어내린다.
  훑기는 `gazeSweep` 0.6초: 멀리서 쏜 레이저 띠(두께 `gazeWidth` 40px)가 순식간에 위에서 아래로 쓸어내리고, 그동안 화면 전체가 붉게 번쩍인다. 띠가 플레이어 몸(`GazeBodyY`±14)에 걸쳐 있을 때 오르면 하트 -1.
- 종이비행기 편대: 간격 `airplaneGap` 0.2초 → 0.1초 (바짝 붙은 편대, 누르고 있는 시간 0.3초).
- 스테이플러: 막으면 막은 자리에서 뒤로 튕겨 나갔다가 머리 위로 휘어(3차 베지어) 반대쪽에서 맞는다. 규칙(0.9초 뒤 반대쪽)은 그대로.
- 소리: 스테이플러·포스트잇·종이비행기의 패링 타이밍 소리와 상사의 눈 예고·레이저, 사라지는 칸, 에스컬레이터 소리를 새로 만들었다 (`Tools~/make_sounds.py`, 박 위치 = 앞당김 - 0.1초).
- 오르는 몸짓: 사람 그림은 원래 크기. 새 프레임을 더했다 (`Tools~/make_frames.py`가 기존 픽셀을 옮겨 만든다, 웹과 같은 PNG).
  플레이어는 칸마다 왼·오른 무릎을 번갈아 들고(`climb_l`·`climb_r`, 지나가기 프레임 포함) 오르는 쪽으로 기울며 발밑에 먼지가 인다.
  에스컬레이터에 밀리거나 사라지는 칸에서 떨어지면 웅크려 내려 딛는다(`down`, `downT`). 경쟁 사원은 계단을 오를 때 `climb` 4프레임, 밀려 내려가거나 떨어질 때 `down`.
  그리기만 바뀌고 규칙은 그대로.
- 컨셉 아트처럼(2026-11-25): 플레이어는 옆모습으로 앞으로 기울어 무릎을 번갈아 들며 계단을 오르고, 경쟁 사원은 크게 내딛으며 팔을 휘둘러 달린다.
  머리·몸통은 기존 그림, 팔다리는 관절 각도로 그린 프레임 (`Tools~/puppet.py`, player.png 17~23열, rival.png 16~25열). 키 아이콘은 이중 테두리에 오른쪽 아래 빨간 키 이름 딱지.
- 10층 위(보통·무한)에서는 위협 위 표시(!, 커피 ×, 스테이플러 ↺, 종이비행기 꾹!, 기다리는 경쟁 사원 …)를 그리지 않는다 (`ShowMarks`). 연습 모드와 장애물 보기에서는 보인다.
- 막기 성공 프레임: 막는 손이 하얗게 번쩍 → 몸이 1px 밀림 → 막는 자세 (`parry_hit_left`·`parry_hit_right`, player.png 13~16열, `parryHitT`). 판정 창 한가운데(±`parryPerfect` 0.035초)에 막으면 쇳소리·울림·쿵이 겹친 더 짜릿한 소리(`sfx_parry_perfect`), 조금 이르거나 늦으면 예전 막기 소리(`sfx_parry`). 종이비행기 편대는 끝까지 누르면 완벽.
- 61층부터 배경 곳곳에 진짜와 같은 모양·박자의 가짜 불꽃 5개가 나타났다 사라진다(`DrawDecoyGlints`, 난수를 쓰지 않음). 예고 숫자 "레이저 N"은 없앴다.
- 확인: `WebRuns.txt`에 61층에서 오르는 장면을 더해 `gaze caught`까지 웹과 한 줄씩 맞대어 본다.

## 의자·편지·거꾸로 칸, 막기 동작 3가지 (2026-11-25)

웹 판과 같다.

| 장애물 | 층 | 규칙 |
|---|---|---|
| 편지 3연발 | 25층~ | 같은 쪽에서 `envelopeGap` 0.4초 박자로 세 장. 한 장씩 그 순간에 막기가 켜져 있어야 한다 (장마다 점수·콤보, 놓치면 하트 -1) |
| 굴러오는 의자 | 30층~ | 위 계단(오를 쪽)에서 굴러 내려온다. `chairOverShare` 35%는 마지막에 크게 튀어 머리 위로 넘어가 아래로 계속 떨어진다 (해 없음, 막지 않아도 됨). 넘어가는지는 rng로 정해 웹과 같다 |
| 거꾸로 칸 | 35층~ (무한 6층~) | 보라색 칸. 그 칸에서는 반대쪽 화살표로 오른다 (`StairLayout.NeedDir`) |

- 막기 동작 3가지: 누를 때마다 곧게 막기 → 두 손 밀기 → 위로 쳐내기 (`parryVar`, 난수 없음). 왼·오른쪽, 막기 성공 프레임도 동작마다 (player.png 24~35열).
- 경쟁 사원 서 있기(idle, rival.png 26·27열): 바닥에 섰을 때, 기다릴 때, 바로 뒤 칸에서 덤비기 전.
- 팔다리 프레임(`Tools~/puppet.py`)은 외곽선을 몸 위에 칠하지 않고 한 번에 그린 뒤 색을 칠해 마디가 끊겨 보이지 않는다.
- 새 소리: `sfx_cue_chair`, `sfx_chair_hop`(넘어가는 의자), `sfx_cue_envelope`. 장애물 보기 23장면.

## 움직이는 로비, 16비트 다듬기, 계단을 굴러 내려오는 의자 (2026-11-25)

- 로비: 의자에 앉아 타자 치는 사원, 서류함 옆에서 전화하는 사원, 화분 옆에서 커피 마시는 사원, 바닥을 오가는 사원 (`DrawLobbyStaff`, staff.png, 난수 없이 시간으로).
- 계단 아래 끝의 장식 경쟁 사원은 서 있기(idle).
- 의자는 실제 계단 칸 윗면을 따라 칸마다 튀며 굴러 내려온다. 넘어가는 의자는 바로 윗칸에서 머리 위를 넘어 뒤쪽 계단 아래로 떨어진다.
- 스프라이트 만들기: `Tools~/sprite_src`(원본) → `puppet.py`(옆모습·막기·로비 사원 프레임) → `upgrade16.py`(16비트: 하이라이트, 두 단계 그림자, 셀아웃, 머리카락 윤기) → WebReference/assets.
  경쟁 사원 머리는 실제 머리 범위로 잘라 붙이고, 옆모습 넥타이는 앞 가장자리 안쪽의 좁은 띠로 다시 그린다.
- QA: `Tools~/anim_qa.py`(빈 프레임·조각남·발 기준선·흔들림·몸을 가르는 외곽선; 16비트 전 프레임에서 0건)와 Designer QA 에이전트 육안 검토(통과).
- 자동 플레이테스트는 창에 눌린 키·마우스를 무시한다 (`ignoreInput`).

## 아직 웹과 다른 점

- 웹은 첫 키·클릭 전에는 소리를 내지 않는다(브라우저 자동 재생 제한). Unity 판은 처음부터 소리가 난다.
- 기록 공유는 웹처럼 내려받기 대신 `사진\stairs_result.png`(없으면 persistentDataPath)에 저장한다.
- 글자는 같은 글꼴·같은 크기지만 브라우저와 Unity의 글자 그리기(안티에일리어싱, 힌팅)가 달라 픽셀 단위로는 조금 다르다.
- 사람이 직접 키보드로 해 본 플레이, 소리를 귀로 확인하는 것은 아직이다.

## 에이전트 파이프라인과의 관계 (맵 v4)

- Designer와 Publisher는 그대로 웹 에셋을 만든다. Unity 판은 `Stairs > Import Web Build Assets`로 그 결과를 받는다.
- Developer가 웹 `game.js` 대신 C#을 쓰게 하려면 `prompts/developer.md`에 Unity 규칙(Core에 규칙, Game에 표현)을 더하고, Developer QA의 정적 검사(JS 문법 → C# 컴파일, CONFIG 수치 → `StairsConfig` 수치)를 바꾼다.
- Publisher의 Playwright 동작 확인은 Unity Test Runner(`-batchmode -runTests -testPlatform EditMode`)와 WebGL 빌드 확인으로 바꾼다.
