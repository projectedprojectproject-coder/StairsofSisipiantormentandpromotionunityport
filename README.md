# 승급의 계단 — Unity 판

웹 브라우저 PoC [승급의 계단](https://github.com/projectedprojectproject-coder/StairsOfSysipianTormentAndPromotion)을 Unity로 옮긴 프로젝트입니다.
웹 판과 **같은 게임**이 되도록 만들었습니다: 같은 규칙(같은 seed면 프레임 단위로 같은 판), 같은 화면 배치, 같은 그림과 소리.

- ← → 다음 계단 방향으로 한 칸 오르기 · Q E 왼쪽·오른쪽 패링 · Esc 일시정지 · Enter 메뉴 결정 (방향키·마우스로 선택)
- 1층 사원에서 100층 CEO실까지 (20·40·60·80층 승급), 무한 모드, 연습 모드
- 장애물: 결재판, 경쟁 사원, 커피, 모니터, 전화기, 서류, 포스트잇, 서류가방, 스테이플러, 종이비행기 편대, 편지 3연발, 굴러오는 의자, 금 간 계단, 젖은 계단, 내려가는 에스컬레이터, 사라지는 칸, 거꾸로 칸, 상사의 눈, 카메라 플래시
- 경쟁 사원도 계단 장애물을 겪습니다: 사라지는 칸·전기 앞에서 기다리고, 금 간 칸·젖은 칸에서 떨어지거나 감전됩니다
- 상사의 눈 레이저가 지나갈 때 움직인 경쟁 사원은 들켜서 실격됩니다 (플레이어처럼)
- 명령줄: `-seed N` (웹 `?seed=N`), `-bot` (웹 `?bot=1`, 혼자 오름), `-mute` (웹 `?mute=1`)

## 바로 하기 (Unity 없이)

**`PlayNow_Windows/StairsOfPromotion.exe`** 를 두 번 누르면 바로 시작합니다 (저장소를 받거나 clone한 뒤 그대로. 폴더 안의 다른 파일들도 함께 있어야 합니다).

## 실행 파일로 하기

`StairsOfPromotion.exe`를 두 번 누르면 됩니다 (같은 폴더의 `StairsOfPromotion_Data` 등이 함께 있어야 합니다). 창 1280x720, 크기 바꾸기 가능, 설정 → 전체화면.
실행 파일 만들기: Unity에서 메뉴 **Stairs > Build Windows Game** (→ `Builds/Windows/`), 또는 명령줄

```
Unity.exe -batchmode -projectPath . -executeMethod Stairs.EditorTools.StairsAssetImporter.BuildWindowsGame -stairsBuild 출력폴더 -quit
```

## 장애물 보기

**설정**(톱니바퀴: 타이틀·로비·결과 화면, 일시정지 메뉴) → **장애물 · 보기**, 또는 조작법 화면의 **장애물 보기**에서 장애물 24장면(경쟁 사원과 계단 장애물 2장면, 경쟁 사원과 상사의 눈 1장면 포함)의 대처법을 움직이는 장면으로 봅니다.
실제 규칙으로 돌며 자동으로 맞는 키를 맞는 순간에 누르고(키 아이콘이 켜지고 "지금!"), 패링 타이밍 소리도 그대로 납니다. 웹 판과 같습니다.

## 여는 방법

1. Unity Hub → Add → 이 폴더. **Unity 6.6 (6000.6.10f1)** 에서 확인했습니다.
2. 메뉴 **Stairs > Import Web Build Assets** — `WebReference/assets`의 PNG·소리를 픽셀 아트 설정으로 가져와 `Assets/Imported/SpriteCatalog.asset`을 만듭니다.
3. 메뉴 **Stairs > Create Game Scene** → `Assets/Scenes/Game.unity` → Play.
4. 테스트: Window > General > Test Runner > EditMode (`ParityTests`, 웹과 같은지 확인).

글꼴은 웹 판과 같은 Noto Sans KR 보통·굵게를 `Assets/Resources/Fonts`에 넣어 두었습니다 (SIL OFL, `OFL.txt`). 소리를 끄려면 `-mute`.

## 구조

| 폴더 | 내용 |
|---|---|
| `Assets/Scripts/Core` | 규칙 (UnityEngine 없음): `StairsConfig` 수치, `Mulberry32` 난수, `StairLayout` 계단·금 간·젖은 계단, `RunSimulation` 한 판 (경쟁 사원, 위협 8종, 콤보, 봇). 상태를 double로 쌓아 웹(JS)과 같다 |
| `Assets/Scripts/Game` | `GameController` = 웹 `game.js`의 화면·입력·소리·그리기를 한 줄씩 옮긴 것, `WebCanvas` = 웹 2D 캔버스(rect/txt/drawSprite, save/translate/rotate)를 GL로, `AudioDirector`, `SaveData`(웹과 같은 기록 키), `PlaytestDriver` |
| `Assets/Editor` | `StairsAssetImporter` (에셋 가져오기, 씬 만들기, 명령줄 빌드) |
| `Assets/Tests/EditMode` | `ParityTests` 14개: 난수·계단 장애물 칸·경쟁 사원, 그리고 `WebRuns.txt`(웹 판 10판과 장면 4개의 사건 기록)를 C# Core와 한 줄씩 비교 |
| `WebReference/` | 비교 기준인 웹 판 사본 (`index.html`, `game.js`, `assets/`). Unity는 가져오지 않으며 고치지 않습니다 |
| `PlayNow_Windows/` | 바로 할 수 있는 Windows 실행 파일 (Stairs > Build Windows Game 결과) |
| `Tools~/` | 웹과 맞대어 보는 도구: `web_expect.js`(웹 판 사건 기록 → `WebRuns.txt`), `rival_expect.js`(경쟁 사원 테스트 기대값), `web_shots.py`(웹 화면 스크린샷), `make_sounds.py`(새 장애물 소리), `qa/`(움직임·상호작용 `qa_physics.js`, 스프라이트 `sprite_qa.py`, 애니메이션 `anim_qa.py`) |
| `docs/UNITY_PORT.md` | 이식 기록 (무엇을 어떻게 옮겼고 무엇을 확인했는지) |

## 명령줄로 확인하기

```
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml
Unity.exe -batchmode -projectPath . -executeMethod Stairs.EditorTools.StairsAssetImporter.BuildPlaytestPlayer -stairsBuild Builds/Playtest -quit
Builds/Playtest/Stairs.exe -seed 1 -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -stairsPlaytest shots/unity
python Tools~/web_shots.py WebReference shots/web
node Tools~/web_expect.js WebReference/game.js Assets/Tests/EditMode/WebRuns.txt   # 웹 판이 바뀌면 기록을 다시 만든다
python Tools~/qa/run_all.py   # QA 모두: 움직임·상호작용, 스프라이트, 애니메이션, Unity EditMode 테스트
```

`-stairsPlaytest`는 타이틀부터 결과·연습·무한까지 정해진 순서로 돌며 화면마다 스크린샷을 찍고, `web_shots.py`는 웹 판을 같은 순서·같은 seed로 찍습니다.
