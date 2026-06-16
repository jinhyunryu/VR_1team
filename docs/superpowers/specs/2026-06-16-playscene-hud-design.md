# 인레이스 HUD — PlaySceneUI 디자인 아트 교체 설계 (2026-06-16)

> 팀원(04_CreatureUI)이 추가한 `PlaySceneUI/` 디자인 스프라이트를 인게임 HUD에 연결.
> 작업 브랜치 `multiplay`. 게임 코드 `Assets/_Game/Scripts/` (namespace 없음).

## 목표

현재 `BoatHud.cs`가 흰 박스 + TMP 텍스트로 **코드 생성**하는 인레이스 HUD(콤보/속도/진행도/아이템)를,
팀원이 만든 바다 테마 디자인 스프라이트로 교체한다. **보트 부착 월드공간 HUD는 유지**하고 룩만 업그레이드.

**이번 범위 = 인레이스 HUD 4종** (콤보 / 속도 / 진행도 / 아이템). **Ranking 결과 화면은 다음 세션.**

## 결정 사항 (브레인스토밍)

- **표시 방식**: BoatHud 교체 — 보트에 붙은 월드공간 HUD 유지, 흰 박스를 디자인 스프라이트로.
- **구현 방식**: 프리팹 조립(사용자) + 컨트롤러 스크립트(코드). 코드 레이아웃 생성 폐기.
- **배치**: 한 패널(월드공간 Canvas 1개)에 4그룹을 모서리 앵커로 — 현재 BoatHud 레이아웃 그대로.
- **아이템 표시**: 슬롯에 한글 이름 + 남은시간 텍스트 (아이콘 에셋 없음 — 나중에 확장).

## 에셋 → 요소 매핑

| 그룹 | 사용 스프라이트 | 동적 요소 |
|---|---|---|
| Distance (좌하단) | `Distance_Bar_BG`, `Start_Icon`(좌)·`Finish_Icon`(우) 장식, `Loading_player`(채움), `Player_Loading_Icon`(자기 보트 마커), `AnotherPlayerBar1~3`(다른 레이서 막대) | 채움 fillAmount + 자기 마커 x + 다른 마커 3개 x |
| Combo (우하단) | `ComboUI_BG`/`Title`/`X_Icon` | 숫자 TMP |
| Speed (우하단) | `SpeedUI` | 숫자 TMP |
| Item (우상단) | `ItemUI` 슬롯 | 이름+남은시간 TMP (발동 시 표시) |

## 아키텍처

### 컨트롤러 — `BoatHud.cs` 제자리 재작성

기존 파일을 **제자리에서 재작성**한다 (파일명/GUID/.meta/씬 참조 유지 → 머지 안전, 데이터 필드 연결 보존).
`[ExecuteAlways]` + 코드 생성부(Build/NewImage/NewText/WhiteSprite/캔버스 자동생성/틱 생성)를 전부 제거.
프리팹은 실제 오서링된 아트라 Edit 모드에서 네이티브로 보이므로 ExecuteAlways 불필요.

**SerializeField:**
- 데이터(유지, 이미 연결): `speedController` / `playerBoat` / `raceManager` / `itemSystem`
  - (기존 `noteSpawner`는 미사용 dead field — 제거)
- 시각(신규, 사용자가 프리팹 요소 연결):
  - `comboText`(TMP_Text) / `speedText`(TMP_Text)
  - `distanceFill`(Image, Type=Filled/Horizontal) / `distanceTrack`(RectTransform, 마커 x 폭 기준) / `selfMarker`(RectTransform) / `otherMarkers`(RectTransform[3])
  - `itemRoot`(GameObject) / `itemText`(TMP_Text)
- 포맷/표현: `comboFormat`("{0}") / `speedFormat`("{0:0} m/s") / `flashSeconds`(1.5) / `alwaysOnTop`(bool) / `renderQueue`(5000)

**매 프레임 (`Update`, 표현 only — 로직 비의존):**
- `comboText` ← `speedController.Combo`, `speedText` ← `playerBoat.Speed`(없으면 `CurrentTargetSpeed`)
- 자기 진행률 `p = Clamp01(playerBoat.DistanceTraveled / raceManager.FinishDistance)` → `distanceFill.fillAmount = p`, `selfMarker.x = p * trackWidth`
- `raceManager.BuildStandings()`에서 `isPlayer` 제외한 레이서들 → `otherMarkers[0..2].x = Clamp01(s.distance/finish) * trackWidth`, 남는 마커 비활성
- 아이템: `OnItemActivated(type, duration)` 구독 → `itemActiveTimer`/`flashTimer` → `itemRoot` 표시 + `itemText` = 이름+카운트다운

**항상-위 (VR 가독성, Play 모드 한정):**
- 검증된 셰이더 교체 로직 유지: Image/RawImage → `UI/AlwaysOnTop` 머티리얼(Start 1회), TMP → `TextMeshPro/Distance Field AlwaysOnTop`(동적 텍스트라 매 프레임 재적용, 한글 폴백 서브메시 포함).
- Edit 모드 공유 폰트 머티리얼 오염 방지 위해 `Application.isPlaying`에서만. (메모리 교훈 [[netracer-debugging-lessons]] 의 항상-위 HUD 항목 준수)

## 데이터 소스 (재사용 — 새 배선 없음)

전부 현재 BoatHud가 이미 쓰는 API:
- `SpeedController.Combo`(int), `.CurrentTargetSpeed`(float)
- `BoatMover.Speed`(float), `.DistanceTraveled`(float)
- `RaceManager.FinishDistance`(float), `.BuildStandings()` → `{ isPlayer, distance, racerNumber }`
- `ProtoItemSystem.OnItemActivated`(event Action<ItemType,float>), `ItemType` enum

## 작업 분담

- **코드(이 작업)**: `BoatHud.cs` 재작성.
- **사용자(Unity)**:
  1. PlaySceneUI 스프라이트로 HUD 프리팹 조립 — 월드공간 Canvas 1개, 자식 4그룹(Distance/Combo/Speed/Item), 보트(PlayerBoat) 자식. 위치는 현재 BoatHud 레이아웃대로.
  2. 루트에 `BoatHud` 컴포넌트 + 데이터 4종(기존 연결 유지) + 신규 시각 필드 연결.
  3. 기존 코드생성 BoatHud 오브젝트 정리(있으면).
  4. 솔로 플레이 검증 — 콤보/속도 갱신, 진행바 채움+마커 이동, 다른 레이서 막대, 아이템 발동 표시.

## 검증

자동 테스트 없음(프로젝트 관례). 코드 작성 → Unity 컴파일 0 에러(사용자) → 솔로 플레이 확인 → 커밋.
멀티 다른 레이서 막대는 2인 이상 빌드 또는 자동 루프로 추가 확인.

## Ranking 결과 화면 (추가 — 2026-06-16 같은 세션에서 진행)

`Ranking` 디자인 스프라이트로 결과 화면 교체. **BoatHud과 동일 패턴** — 기존 `RaceResultScreen.cs`(흰 박스 코드생성 + 순위 텍스트 + VR 손터치/레이 버튼)를 **제자리 재작성**해 디자인 프리팹 구동으로.

- **유지**: `raceManager`/`hands`(Striker) 데이터, `RaceManager.RaceEnded` 감지 → 표시, `Proceed`(SessionConnector.Disconnect + NM.Shutdown + 씬 재시작/returnScene), **손터치+레이 버튼 인터랙션**(VR 핵심 자산).
- **제거**: 코드생성(Build/NewImage/NewText/WhiteSprite).
- **데이터 소스**: `RaceResult.Standings`(List<RaceStanding>{ name, isPlayer, place, racerNumber, distance }) + `PlayerPlace`. 완주 시간 없음 → 점수 = 거리(m).
- **프리팹**(사용자 조립): World Space Canvas + `Ranking_BG` + `Ranking_Title` + **4행**(각 행: `Ranking_1st`~`4th` 메달[행마다 고정] + 아바타[placeholder] + 이름 TMP + 점수 TMP) + **기능용 재시작 버튼**(Ranking 아트엔 없음 → 따로 배치, 손/레이로 누름).
- **컨트롤러**: `RaceEnded` 시 패널 표시 + `Standings`를 등수순으로 4행에 채움(이름 `P{racerNumber}` + 내 행 "(YOU)" 강조, 점수 `{distance}m`), 레이서<4면 남는 행 숨김. 버튼 터치/레이 → `Proceed`.
- **텍스트 전부 영어**(P1/YOU/m/RESTART) → 한글 폰트 불필요. 항상-위 = BoatHud과 동일(Play 한정 셰이더 교체).
- **결과창 진행 방식**: 기능용 버튼(손터치/레이) — 사용자 결정 2026-06-16.
- **SerializeField**(신규): `panelRoot`(GameObject) / `rows`(ResultRow[4]: root/nameText/scoreText/youMarker) / `restartButton`(RectTransform) / `restartButtonImage`(Image, hover) / `buttonColor`·`buttonHoverColor`·`buttonTouchRadius`·`returnSceneName` / `alwaysOnTop`·`renderQueue`.

## 비목표 (이번 제외)

- 아이템 아이콘 이미지 (현재 에셋 없음 — 이름 텍스트로).
- 콤보 punch/연출 등 모션 폴리시.
- 완주 시간 표시 (데이터 없음 — 거리로 대체).
- 아바타 이미지 per-racer (placeholder 유지).
