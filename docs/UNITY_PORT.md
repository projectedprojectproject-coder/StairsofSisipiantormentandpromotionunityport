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
6. Window > General > Test Runner > EditMode 에서 `ParityTests`(16개)를 돌린다.

조작과 명령줄은 웹과 같다: ← → 오르기, Q E 막기, Esc 일시정지, Enter·Space 결정, 방향키·Tab·마우스로 고르기. `-seed N`, `-bot`, `-mute` (웹 `?seed=`, `?bot=1`, `?mute=1`).

## 구조

| 폴더 | 내용 | 웹 PoC에서 |
|---|---|---|
| `Assets/Scripts/Core` (Stairs.Core, UnityEngine 없음) | `StairsConfig` 수치, `Mulberry32` 난수, `StairLayout` 계단·금 간 계단·젖은 계단·감전 주기, `RunSimulation` 한 판의 규칙(오르기, 패링, 콤보·배수, 일수, 하트, 승급, 위협 8종, 경쟁 사원 쫓아오기, 카메라 플래시, 떠오르는 글자·불꽃, 봇). 상태는 double | `CONFIG`, `mulberry32`, `ensureStairs`, `isCracked`, `isWet`, `shockState`, `startRun`, `doStep`, `doParry`, `updatePlay`, `spawnAttack`, `updateRival`, `rivalPos`, `addText`, `botTick` |
| `Assets/Scripts/Game` (Stairs.Game) | `GameController` 화면·버튼·입력·소리·그리기 전부, `WebCanvas` 웹 2D 캔버스 흉내(GL), `AudioDirector`, `SaveData`, `SpriteCatalog`, `PlaytestDriver` | `screen`, `getButtons`, `keydown`/`mousedown`, `syncAudio`, `drawTitle`~`drawResult`, `shareResult`, `save` |
| `Assets/Resources` | `StairsCanvas.shader`(그림·글자·회색 필터), `Fonts/` Noto Sans KR 보통·굵게 | 캔버스, CSS filter, 글꼴 |
| `Assets/Editor` | `StairsAssetImporter`(에셋 가져오기, 씬 만들기, 명령줄 빌드), `MiniJson` | Publisher의 연결 단계 |
| `Assets/Tests/EditMode` | `ParityTests` 16개 | Publisher 동작 확인 |
| `WebReference/` | 비교 기준인 웹 판 사본(`index.html`, `game.js`, `assets/`). Unity는 가져오지 않는다 | 웹 판 그대로 |
| `Tools~` (Unity가 가져오지 않음) | `cs_core.js`(C# Core를 옮긴 JS)·`playtest.js`(웹과 사건 기록 비교), `rival_expect.js`(테스트 기대값), `web_shots.py`(웹 화면을 PlaytestDriver와 같은 순서로 찍기) | |

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
- 확인: `PlaytestDriver`(`-stairsPlaytest`)와 `Tools~/web_shots.py`가 같은 seed·같은 계단 수에서 17장씩 찍어 맞대어 봤다.
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

## 아직 웹과 다른 점

- 웹은 첫 키·클릭 전에는 소리를 내지 않는다(브라우저 자동 재생 제한). Unity 판은 처음부터 소리가 난다.
- 기록 공유는 웹처럼 내려받기 대신 `사진\stairs_result.png`(없으면 persistentDataPath)에 저장한다.
- 글자는 같은 글꼴·같은 크기지만 브라우저와 Unity의 글자 그리기(안티에일리어싱, 힌팅)가 달라 픽셀 단위로는 조금 다르다.
- 사람이 직접 키보드로 해 본 플레이, 소리를 귀로 확인하는 것은 아직이다.

## 에이전트 파이프라인과의 관계 (맵 v4)

- Designer와 Publisher는 그대로 웹 에셋을 만든다. Unity 판은 `Stairs > Import Web Build Assets`로 그 결과를 받는다.
- Developer가 웹 `game.js` 대신 C#을 쓰게 하려면 `prompts/developer.md`에 Unity 규칙(Core에 규칙, Game에 표현)을 더하고, Developer QA의 정적 검사(JS 문법 → C# 컴파일, CONFIG 수치 → `StairsConfig` 수치)를 바꾼다.
- Publisher의 Playwright 동작 확인은 Unity Test Runner(`-batchmode -runTests -testPlatform EditMode`)와 WebGL 빌드 확인으로 바꾼다.
