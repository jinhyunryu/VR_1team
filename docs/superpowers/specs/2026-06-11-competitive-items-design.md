# 카트라이더식 견제 아이템 5종 — 설계 (2026-06-11)

> 브랜치 `multiplay` (아이템 공격형이 멀티 인프라에 의존). 싱글/멀티 양쪽 동작, 기존 코드 하위호환.
> 승인: 사용자 (2026-06-11).

## 목표 / 비목표

**목표**
- 아이템을 자기강화 3종 → **견제 포함 5종**(카트라이더식)으로 확장.
- 4명 멀티에서 역전·난장 재미. 데모 부스 회전율과 "한 판 더" 욕구 강화.
- 동시에 기존 "아이템 효과 안 보임" 문제(미연결 + 무피드백)를 해결.

**비목표 (YAGNI)**
- 들고-발동 인벤토리 / 사용 버튼 (자동 발동으로 결정).
- 아이템 3종 이상 다양화, 리더보드, 정식 ItemSystem(팀메이트 영역) 통합.
- 네트워크 물리 오브젝트 (RPC→로컬 효과로 회피).

## 사용 모델 (확정)

- **획득**: 아이템 노트(흰 새/Bird) 명중 → **등수 가중 랜덤**으로 1종 → **즉시 자동 발동** (새 입력·인벤토리 없음).
- **등수 가중 분배**:
  - 1등: 부스트/자석 위주 (약한 풀)
  - 중위권: 무지개별/폭탄
  - 꼴찌 쪽: 우주선 확률 ↑
  - **우주선은 내가 1등이면 드롭 안 됨** (앞 1등 없으면 무의미)
- 등수는 `RaceManager.BuildStandings()` 의 내 place 로 판정.

## 아이템 5종 (정확한 정의)

| 아이템 | enum | 효과 | 성격 | 기본 수치(튜닝) |
|---|---|---|---|---|
| 부스트 | Boost | 속도 급상승 | 로컬 | +5 m/s, 4s (기존 AddBoost) |
| 무지개별 | RainbowStar | 무적 — 노트 자동 명중(콤보·속도 자동↑) + 들어오는 폭탄 무시 + 견제 면역 | 로컬 | 4s |
| 자석 | Magnet | 파란(터치) 노트만 스폰 = 쉬운 콤보 | 로컬 | 4s |
| 폭탄 | Bomb | 발동 즉시 상대 레인에 폭탄 함정 생성. 치면 큰 감속, 안 치고 피하면 무사 | 공격·네트워크 | 피격 시 속도 baseSpeed 로 리셋, 3s 회복 |
| 우주선 | Spaceship | 발동 즉시 현재 1등을 강하게 감속 | 공격·네트워크 | 1등 속도 ×0.5, 3s |

- 기존 `SlowMo` 폐기, `Shield` 는 RainbowStar 무적에 흡수.
- RainbowStar "노트 자동 명중": 무적 동안 도달하는 노트를 자동으로 hit 처리(콤보·속도 climb), 폭탄 함정은 무효.

## 공격형 네트워크 구조 (물리 동기화 0)

핵심 원칙: **RPC 로 대상에게 "신호"만 보내고, 실제 효과는 대상 기기가 로컬로 적용.** 네트워크 물리 오브젝트 없음 — 기존 거리 동기화 + NGO RPC 만 사용.

- **폭탄**: 발동(로컬) → `ItemNetworkRelay.RequestBombServerRpc(senderId)` → 서버 → `ApplyBombClientRpc(senderId)` → **각 피해자 기기가 자기 노트 레인 앞에 폭탄 함정(BombHazard)을 로컬 스폰** (노트와 동일하게 로컬 오브젝트). 맞으면 감속, 피하면 통과. 발신자 자신은 제외.
- **우주선**: 발동 → `RequestSpaceshipServerRpc()` → 서버가 동기화된 `NetRacer.NetDistance` 로 **현재 1등 식별** → 그 클라에게 `ApplySlowClientRpc(duration, multiplier)` → **1등 기기가 자기 SpeedController 에 로컬 감속 디버프**.
- RPC 는 신호일 뿐 — 효과 계산은 전부 수신 측 로컬 (지연/권한 분쟁 없음).

## 싱글 폴백 (AI 고스트)

- 혼자(AI 고스트) 플레이 시 "상대" = AI NetRacer/씬 고스트.
- **폭탄**: AI 는 노트가 없으므로 함정 대신 **대상 AI 를 직접 감속**.
- **우주선**: 1등(AI든 사람이든) 직접 감속 — 멀티와 동일 경로.
- 획득은 AI 기준 등수 가중 그대로 → 솔로도 역전 아이템 작동.
- 멀티 미접속 시 모든 RPC 경로는 "직접 로컬 적용"으로 폴백 (NetworkManager.IsListening 체크).

## HUD / 체감 (기존 "효과 안 보임" 동시 해결)

- **활성 효과 인디케이터**: 부스트/무적/자석 아이콘 + 남은 시간 바.
- **폭탄 경고**: 들어올 때 화면 경고 + 레인의 검은 폭탄 비주얼 (치면 안 되는 것 직관).
- **발동 배너 + SFX**: "우주선 발동!"/"폭탄!" — 1등에게 "감속!" 피드백.
- **획득 순간 플래시** + 아이템 아이콘 팝.
- 표현 only — 효과 계산은 로직에서 확정, HUD 는 읽어 표시만 (Phase 15 피드백 원칙).

## 컴포넌트

신규 (`Assets/_Game/Scripts/`, namespace 없음, 기존 패턴):
| 컴포넌트 | 역할 |
|---|---|
| `ItemDistributor` | 등수 가중 풀에서 아이템 선택 (RaceManager 순위 읽음). 우주선 1등 제외 규칙 |
| `ItemHud` | 활성 효과/획득/폭탄 경고 표시 (코드 생성 월드 UI, RuntimeHud 패턴) |
| `BombHazard` | 레인에 날아오는 폭탄 함정. 손 접촉 시 감속, 미접촉 시 통과 (ProtoNote 식 로컬) |
| `ItemNetworkRelay` | NetworkBehaviour. 폭탄/우주선 ServerRpc/ClientRpc. 멀티 미접속 시 로컬 폴백 |

기존 확장:
- `ProtoItemSystem`: `ItemType` 5종으로 확장 + RainbowStar/Magnet/Bomb/Spaceship 효과 추가. 씬 연결(미배치 수정).
- `ProtoNoteSpawner`: 자석 모드(파란 노트만), 획득 시 `ItemDistributor` 경유.
- `SpeedController`: 우주선 감속 디버프 적용 API (`ApplyExternalSlow`), 무적 상태 플래그.
- `NetRacer`: 폭탄/우주선 RPC 대상 식별에 기존 NetDistance 활용 (추가 NV 없음).

## 빌드 순서 (한 번에 가되 안전한 순서)

1. `ItemType` 5종 확장 + `ProtoItemSystem` 씬 연결 (미배치 수정) — 아이템이 일단 발동되게
2. **로컬 3종** (부스트/무지개별/자석) — SpeedController·스포너 확장, 저비용. 솔로에서 검증
3. `ItemHud` — 효과 표시 (체감 문제 해결)
4. **공격 2종 네트워크** (`ItemNetworkRelay` + `BombHazard` + 1등 감속) — MPPM/빌드 2인 검증
5. `ItemDistributor` 등수 가중 분배 (RaceManager 순위)
6. 싱글 폴백 + 밸런스 튜닝

각 단계 Unity 검증 후 다음 (프로젝트 마이크로 단계 원칙). 자동 빌드/테스트 루프(`DevBuildScript` + `-lanhost`/`-lanjoin`) 로 공격형 네트워크 검증.

## 테스트

- 솔로(에디터): 5종 발동 + 효과 + HUD. 폭탄/우주선이 AI 대상으로 작동.
- 멀티(빌드 2~4인): 폭탄이 상대 레인에 뜨고 회피/피격 분기, 우주선이 1등만 감속, 등수 가중 분배.
- 회귀: 아이템 미사용 레이스가 기존과 동일. 멀티 미접속 시 공격형 로컬 폴백.

## 사용자(에디터) 작업

- `ProtoItemSystem` 씬 배치 + speedController/itemSystem 연결 (미연결 수정).
- 폭탄 비주얼(검은 폭탄 프리팹) + 아이템 아이콘 5종 (플레이스홀더 가능).
- `ItemHud`/`ItemNetworkRelay`(+NetworkObject) 씬 배치 + 연결.
- 사운드: 발동/폭탄/감속 SFX (SoundManager 또는 NoteFeedback).

## 팀메이트 주의

- **폭탄은 리듬 노트가 아닌 별도 함정(BombHazard)** — 02_RhythmScore 팀메이트 노트 시스템 무관.
- 아이템 분배(`ItemDistributor`)는 정식 ItemSystem 으로 교체 가능하게 분리.
- 정식 채보 도입 시 자석(파란 노트만)은 스포너 필터로 유지/조정.
