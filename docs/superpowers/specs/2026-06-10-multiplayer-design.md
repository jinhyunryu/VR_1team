# 4인 VR 보트 리듬 레이싱 멀티플레이 — 설계 (2026-06-10)

> 브랜치 `multiplay`. 싱글플레이(VRHand/main)는 안전선 — 이 브랜치 작업은 전부 하위호환.
> 승인: 사용자 (2026-06-10).

## 목표 / 비목표

**목표**
- Quest 헤드셋 4대가 같은 레이스에서 서로의 보트·아바타를 보며 경주.
- 시연 환경: 학원 교실 (와이파이 AP 격리 가능성 있음 → 클라우드 중계 기본).
- 빈자리는 AI(GhostRacer)가 채움. 시연이 1~4인 어디서든 성립.

**비목표 (YAGNI)**
- host migration, 호스트 권위 순위 정산, 음성 채팅, 관전 모드, 노트/판정 동기화.
- 리듬 게임플레이는 전부 로컬: ProtoNote / SpeedController / 판정 / 아이템 무변경.

## 스택 결정

- **NGO (Netcode for GameObjects)** + **Unity Multiplayer Services 패키지**(`com.unity.services.multiplayer`)의 Session API (Relay+Lobby 통합, quick-join).
- 선정 이유: ① Relay 중계로 교실 AP 격리 우회 ② quick-join이라 VR에서 코드 입력 0 ③ 같은 NGO 코드로 LAN 직결 폴백 가능 (Photon은 클라우드 전용) ④ 공식·무료(학생 규모).
- 테스트: **Multiplayer Play Mode**(`com.unity.multiplayer.playmode`) 가상 플레이어로 에디터 4인. ParrelSync 안 씀.
- 거절한 대안: Photon Fusion (셋업은 빠르나 LAN 폴백 없음, 외부 의존), NGO LAN 직결 단독 (AP 격리 시 실패).

## 동기화 모델 (핵심 통찰)

레일 직진 레이스라 **보트 위치 = 시작점 + forward × 거리**. 동기화는 두 가지뿐:

1. **보트 거리** — owner가 자기 `BoatMover.DistanceTraveled`를 `NetworkVariable<float>`에 기록. 원격은 수신 거리(+보간)로 위치 계산.
2. **아바타 포즈** — 머리+양손 3개, 보트 기준 로컬 좌표, ~15Hz + 보간.

속도를 만드는 과정(콤보/노트 히트)은 동기화하지 않는다 — 결과(거리)만 흐른다.

## 컴포넌트 (`Assets/_Game/Scripts/Net/`, namespace 없음 — 프로젝트 컨벤션)

| 컴포넌트 | 책임 | 의존 |
|---|---|---|
| `SessionConnector` | UGS init + 익명 로그인 + `CreateOrJoinSession`(quick-join, 최대 4인) + 해제. 상태/에러 이벤트 노출 | UGS SDK |
| `NetRacer` (프리팹 루트) | NetworkBehaviour. owner: 로컬 BoatMover 거리 → NetworkVariable. 원격: 거리 → 레인 위치 적용. 보트 헐 + NetAvatar 포함. 로컬 자신 인스턴스는 비주얼 숨김(데이터 발행만) | BoatMover(읽기) |
| `NetAvatar` (NetRacer 자식) | 머리+양손 로컬 포즈 동기화. 로컬: XR rig에서 읽기 / 원격: 적용. 모델은 플레이스홀더(팀메이트 교체 가능) | XR Origin(읽기) |
| `NetRaceCoordinator` | 호스트 권한. 레인 배정(접속순), START RPC → 3초 카운트다운 → 음악 동시 시작(ProtoBeatmapSpawner 트리거), AI NetRacer 스폰(4−인원) | NGO |
| `MultiplayerHud` | 코드 생성 월드 UI (RuntimeHud 패턴): 멀티 참가 → 접속 중 → 대기실(N/4) → 호스트 START. 에러 표시 | SessionConnector |

## 기존 코드 변경 (정확히 4곳, 하위호환)

1. **BoatMover**: `ApplyNetworkDistance(float distance)` 추가 — 원격 보트용. Update 적분 대신 거리/위치 직접 세팅. 기존 경로 무변경.
2. **RaceManager**: `RegisterRacer(string name, BoatMover mover, bool isPlayer)` 런타임 등록 API 추가. 인스펙터 리스트 방식 공존(싱글 호환).
3. **ProtoBeatmapSpawner**: `StopSong()` + `RestartSong(double delaySeconds)` 공개 메서드 추가 (계획 단계에서 정련 — autoStart 플래그 대신 Stop/Restart 방식이 씬 직렬화 변경 없이 싱글 경로를 완전히 보존). 멀티: 로비 진입 시 Stop, 카운트다운 후 Restart.
4. **GhostRacer**: `InitForNetwork(player, finish, endPace)` 추가 — AI 채움 스폰 시 프리팹이 씬 참조(playerBoat 등)를 가질 수 없어 런타임 주입 필요.

## 설계 결정 기록

- **순위는 각 클라이언트 로컬 계산**: 거리가 동기화되므로 결과 거의 일치. 호스트 권위 정산은 데모에 과함. (간선 차이로 1프레임 순위 엇갈림 가능 — 허용)
- **AI 고스트 = host 소유 NetRacer**: 호스트에서 GhostRacer 구동, 거리는 플레이어와 같은 경로로 동기화. 클라이언트별 로컬 구동 시 러버밴딩 때문에 순위가 기기마다 갈라지므로 배제.
- **음악 동기 시작**: START RPC 수신 시점 +3초 카운트다운 후 `PlayScheduled`. 기기 간 RPC 도착 편차(중계 ~수십ms)는 리듬이 로컬이라 게임플레이에 영향 없음 — 출발 공정성만 미세 오차, 허용.
- **레인**: 접속 순서로 X 오프셋 배정. 로컬 플레이어 배는 씬 배치 그대로(자기 레인), 원격만 오프셋 적용.

## 에러 처리

- 접속 실패/타임아웃 → HUD 메시지, 싱글 플레이는 그대로 가능 (멀티는 명시적 진입).
- 플레이어 중간 이탈 → 해당 NetRacer 디스폰, 레이스 계속.
- 호스트 이탈 → "세션 종료" 표시 후 대기실/싱글 복귀.
- 멀티 오브젝트 미배치/패키지 문제 시 싱글 흐름 무영향 (전부 null-safe — 프로젝트 컨벤션).

## 단계 (각 단계 Unity 검증 후 다음 — 프로젝트 마이크로 단계 원칙)

1. **접속**: 패키지 설치, UGS 프로젝트 연결(사용자 1회), SessionConnector — MPPM 2인 같은 세션 확인
2. **서로 보임**: NetRacer 거리 동기화 + 레인 배치
3. **아바타**: 머리+양손 포즈 동기화
4. **레이스 동기화**: START/카운트다운/음악 동시 출발 + 결과 화면(RaceResultScreen) 통합
5. **빈자리 AI** + 이탈 처리
6. **LAN 폴백** (transport 전환 토글) + Quest 빌드 2대 스모크

## 테스트

- 에디터: MPPM 가상 플레이어 4 — 접속/거리/아바타/시작 동기화 확인.
- 실기: Quest 2대 스모크 (교실 와이파이 + 핫스팟 LAN 폴백 각 1회).
- 회귀: 멀티 오브젝트 없는 씬에서 싱글 레이스 기존과 동일 동작.

## 사용자(에디터) 작업

- UGS: Unity 대시보드에서 프로젝트 연결 + Relay/Lobby 서비스 켜기 (1회).
- 씬: NetworkManager/SessionConnector/MultiplayerHud GameObject 배치, NetRacer 프리팹 필드 연결.
- Quest 빌드 세팅 (기존과 동일).
