// 승급의 계단 수치. 웹 PoC(demo_api/game.js의 CONFIG)와 같은 값이다.
// UnityEngine에 기대지 않아 EditMode 테스트와 다른 .NET 환경에서도 돈다.
// 인스펙터에서 고치려면 Game/GameConfigAsset(ScriptableObject)이 이 클래스를 감싼다.
using System;

namespace Stairs.Core
{
    [Serializable]
    public class StairsConfig
    {
        // ---- 기획서 04 초안 ----
        public int maxHearts = 3;
        public float maxDays = 30f, startDays = 30f;
        public float dayDuration = 0.33f;     // 1일 = 약 0.33초
        public float stepDays = 1f, parryDays = 1.5f;
        public float parryWindow = 0.20f;     // 패링 판정 창(초)
        public float parryPerfect = 0.035f;   // 창 한가운데에서 이만큼(초) 안이면 '완벽' (새 막기 소리)
        public float warnTime = 1.0f;         // 공격 예고(초) = 비행 시간
        public int[] promoteFloors = { 20, 40, 60, 80 };
        public int finalFloor = 100;
        public int practiceFloor = 11;
        public float practiceDiff = 0.5f;
        public int endlessRampFloors = 60;
        public int promoteHeal = 1, floorHeal = 1;

        // ---- 계단 장애물 ----
        public int crackFromFloor = 21;
        public float crackShare = 0.16f, crackTime = 1.5f;
        public int wetFromFloor = 30;         // 젖은 계단 WET HAZARD (2026-11-25)
        public float wetShare = 0.14f;
        public float shockPeriod = 3.0f, shockTime = 0.5f, shockWarn = 0.6f;
        public int flashFromFloor = 61;
        public float flashGap = 3.5f, flashTime = 0.28f;

        // ---- 위협 등장 층과 비중 (결재판 1 기준) ----
        public int rivalFromFloor = 11, coffeeFromFloor = 11, monitorFromFloor = 21, phoneFromFloor = 31;
        public int paperFromFloor = 41, stickyFromFloor = 61, suitcaseFromFloor = 71;
        public float weightRival = 0.7f, weightCoffee = 0.5f, weightMonitor = 0.6f, weightPhone = 0.6f;
        public float weightPaper = 0.6f, weightSticky = 0.6f, weightSuitcase = 0.6f;
        public int stickyMax = 5;
        // ---- 새 장애물 (2026-11-25, 웹 CONFIG와 같은 값) ----
        public int staplerFromFloor = 15; public float weightStapler = 0.6f, staplerReturn = 0.9f;            // 막으면 반대쪽에서 돌아온다
        public int airplaneFromFloor = 20; public float weightAirplane = 0.5f; public int airplaneCount = 4; public float airplaneGap = 0.1f;   // 꾹 누르기
        public int envelopeFromFloor = 25; public float weightEnvelope = 0.6f; public int envelopeCount = 3; public float envelopeGap = 0.4f;   // 편지 3연발: 박자에 맞춰 세 번
        public int chairFromFloor = 30; public float weightChair = 0.6f, chairHop = 0.34f;   // 튀어 내려오는 의자: 두 칸을 한 번에 튀는 시간(초)
        public int reverseFromFloor = 35; public float reverseShare = 0.10f;   // 거꾸로 칸: 반대쪽 화살표로 오른다
        public int escalatorFromFloor = 20; public float escalatorShare = 0.12f, escalatorPush = 0.6f;       // 서 있으면 밀려 내려간다
        public int vanishFromFloor = 41; public float vanishShare = 0.10f, vanishShow = 5f, vanishHide = 1f, vanishWarn = 1f;   // 5초 있고 1초 없어짐
        public int gazeFromFloor = 61; public float gazeWarn = 5f, gazeSweep = 0.6f, gazeRest = 6f, gazeWidth = 40f;            // 상사의 눈
        public float rivalWalk = 0.6f, flickerTime = 0.6f;
        // 경쟁 사원 쫓아오기 (2026-11-25 Unity 2단계): 층 바닥을 걸어오고, 플레이어가 더 높으면 계단을 따라 오른다
        public float rivalLeap = 0.3f;        // 치기 직전 플레이어 칸으로 뛰어오르는 시간(초). 이때 막을 쪽이 정해진다
        public float rivalSpeed = 240f;       // 층 바닥을 걷는 속도(논리 px/초)
        public float rivalClimb = 10f;        // 계단을 오르는 속도(칸/초). 플레이어보다 빠르다
        public float rivalCrackTime = 0.8f;   // 금 간 칸에 이만큼(초) 서 있으면 무너져 떨어진다 (플레이어보다 무겁다)
        public float rivalVanishWait = 0.5f;  // 사라지기까지 이만큼(초)도 안 남은 칸에는 올라서지 않고 기다린다
        public bool showRivalCameo = true;    // 경쟁 사원이 공격하기 전 층(rivalFromFloor 미만)의 장식

        // ---- 기획서 미정 ----
        public int stairsPerFloor = 6;
        public int comboPerMult = 3, maxMult = 10;
        public int stepPoints = 10, parryPoints = 50, floorPoints = 100;
        public float drainStart = 1.0f, drainEnd = 1.35f;
        public float attackIntervalStart = 2.4f, attackIntervalEnd = 1.1f, attackJitter = 0.2f;
        public float minAttackGap = 0.8f, firstAttackDelay = 3.0f, parryCooldown = 0.15f;
        public int stairRunMax = 4;
        public float stairKeepProb = 0.5f;
        public float endDelay = 1.0f;
        public float botStepInterval = 0.19f, botParryLead = 0.10f;

        // ---- 표현 (논리 픽셀, 320x180 기준) ----
        public float stepW = 22f, stepH = 12f;
        public float viewW = 320f, viewH = 180f;   // 웹 W, H. 경쟁 사원이 화면 끝에서 들어오는 자리에 쓴다
        // ---- 웹 CONFIG의 표현·소리 값 (그리기와 소리에만 쓴다) ----
        public float suitcaseBurst = 0.1f;   // 서류가방이 도착 몇 초 전에 터지는지
        public float monitorAngle = 70f;     // 모니터가 떨어지는 사선의 각도(수평에서, 도)
        public int tenseFloor = 11;          // 단계 음악이 없을 때 bgm_loop → bgm_loop_tense
        public int commuters = 5;            // 타이틀 거리를 걷는 사원 수
        public float bgmVolume = 0.45f, ambVolume = 0.15f, sfxVolume = 0.8f;

        /// <summary>
        /// 인스펙터용 float 값을 웹(JS double)과 같은 double로 읽는다. (double)0.33f 는 0.32999998이지만
        /// float→decimal 변환은 유효 숫자 7자리로 반올림되므로 (double)(decimal)0.33f == 0.33 이다.
        /// Core는 상태를 모두 double로 쌓고 수치는 이것으로 읽어, 같은 seed면 웹과 프레임 단위까지 같다 (2026-11-25 플레이테스트).
        /// </summary>
        public static double D(float v) => (double)(decimal)v;

        public string[] rankNames = { "사원", "대리", "과장", "간부", "CEO" };
        public string[] zoneNames = { "파티션 사무실", "개방형 오피스", "텅 빈 회의실", "임원층", "CEO실" };
    }
}
