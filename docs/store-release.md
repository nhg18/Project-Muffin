# 스토어 출시 준비 — Google Play · App Store

**작성일**: 2026-09-26 (조사 기준일도 같음. 스토어 규정은 자주 바뀌므로 기능 13 시작 때 다시 확인)
**최근 갱신**: 2026-11-07 — 결정 1 · 3 · 5 · 6 · 8 확정, 패키지 ID 보류, **결정 9 연령 등급** 신설(조사 기준일 2026-11-07, 2-1절)
**출시 목표**: `1.0.0` 2027-02-04 ([`development-plan.md`](development-plan.md) 기능 13)
**범위**: 오래 걸리는 외부 준비 · 결정할 것 · 비용 · 스토어 요건. 게임 기능 개발은 다루지 않는다.

---

## 0. 결론 먼저

1. **엔진은 Unity 6000.3.27f1 (6.3 LTS) 로 올렸다 (2026-10-31, 결정 7).** 구글의 현재 요건(API 36, 16 KB 페이지)과 Xcode 26, iOS 27 SDK(UIScene) 를 지원하는 버전이다.
2. **Unity 6.3 과 Xcode 26 조합은 11월 시험 빌드로 확인한다.** PUN2 · DOTween · Splines 도 업그레이드 뒤 동작을 확인한다.
3. **계정 명의는 개인으로 확정 (2026-11-07, 결정 1).** 그래서 구글의 **테스터 12명 × 14일 비공개 테스트가 의무**다. 팀 밖 테스터가 필요하고, **12월 초에 모집을 끝내고 12/12(`0.5.0`)에 테스트를 시작**한다. 역산한 마지노선은 1/6.
4. **PUN 무료 20 CCU는 개발 전용이라 출시에 못 쓴다.** 출시 때 100 CCU 요금제($95, 1회 결제로 12개월)를 산다.
5. **출시 `1.0` 은 무료 · 광고 없음 (2026-11-06, 결정 2).** 사업자등록 · 통신판매업 · Apple 유료 앱 계약은 출시에 필요 없다. 사업 단계(출시 → 서비스(무료) → 사업화)는 노션 「창업」 8절.
6. **Apple 심사자는 혼자 게임을 해본다.** 4인 온라인 전용 게임은 "상대가 없어 진행 불가"로 거절(2.1)될 위험이 있다. 대응 방식은 기획 결정이다 → 결정 4.
7. **연령 등급 (결정 9, 검토 완료 · PM 결정 대기)**: 한국에는 「7세」 등급이 없다(전체 · 12 · 15 · 청불). 이 게임은 **전체이용가 · Apple 4+ 또는 9+** 가 가능하다. 그러나 **만 14세 미만**은 개인정보보호법상 법정대리인 동의가 필요해, `1.0` 은 **스토어 등급 전체이용가 + 앱 안 「만 14세 이상」 자기 확인**을 추천한다(7 ~ 13세를 받으려면 보호자 동의 흐름을 따로 만들어야 한다). 무료 재화 상자는 확률형 아이템 법정 표시 대상이 **아니다**(직·간접 유상만). 상세 2-1절.
8. **iPhone 전용 · EU 제외 · PUN 유지**로 확정 (2026-11-07, 결정 5 · 6 · 8). iPad 스크린샷 · DSA 트레이더 신고 · Fusion 전환은 하지 않는다.

---

## 1. 일정 역산 — 오래 걸리는 일

| 언제까지 | 할 일 | 걸리는 시간 · 이유 |
| --- | --- | --- |
| **10월** | ~~계정 명의 결정(결정 1)~~ **개인 명의 확정 (2026-11-07)** → Google Play 개발자 계정 · Apple Developer Program 가입 | 신원 확인 수일. 개인 계정은 Play Console 앱으로 Android 기기 보유 인증도 한다 |
| 10월 | 게임제작업 · 배급업 등록이 필요한지 구청에 문의 | 게임산업법 25조. 소규모 무료 게임에 실제로 적용되는지 미확인 |
| **11월 중** | iOS 시험 빌드 1회 (빈 씬이라도) — Unity Build Automation → TestFlight | Unity 6000.3 · Xcode 조합을 아이폰 빌드(기능 11)보다 앞당겨 확인해 둔다 |
| 11월 중 | 외부 테스터 모집 **시작** (Android 폰 보유, Gmail) | 14일 동안 중간에 빠지면 인원에서 빠진다. 여유 있게 15명 이상 |
| **12월 초 (~12/5)** | **테스터 모집 마감 · 비공개 테스트 트랙 준비** (개인 계정 12명 × 14일 의무, 결정 1) | 12/12 시작에 맞추려면 명단 · Gmail 확보가 1주 전 |
| **12/5** | 기획: **연령 등급 결정 (결정 9)** → 23-login 연령 확인 UI 확정 | 비공개 테스트 빌드에 연령 확인 · 등급 설문 답이 들어간다 |
| **12/12** (`0.5.0`) | 개인정보처리방침 웹페이지 · 지원 연락처 · **웹 계정 삭제 요청 페이지** 준비 | 비공개 테스트부터 Data safety 신고가 필요하고, 거기에 방침 URL · 삭제 요청 URL 이 들어간다 (결정 3) |
| 12/12 | 구글 비공개 테스트 시작 · TestFlight 외부 테스트 시작 | 14일 연속 |
| ~~12/26~~ | ~~기획: 로그인 방식 확정 (결정 3)~~ **완료 — [`systems/23-login.md`](systems/23-login.md) (2026-11-07)** | 앱 안 계정 삭제 · 웹 삭제 요청 · Sign in with Apple 을 23 이 포함한다 |
| 12/26 이후 | 구글 "프로덕션 액세스" 신청 | 심사 최대 약 7일 |
| 1/9 | 스토어 문구 · 스크린샷 · 아이콘 완성 (4-3 · 5-3) | 디자인 트랙 |
| 1/15 ~ | 연령 등급 설문(답안은 2-1 표) · Data safety · App Privacy 신고, Photon 100 CCU 구매 | |
| **~1/25** | 양쪽 스토어 심사 제출 | Apple 90%가 24시간 안, 구글 신규 계정은 최대 7일 이상. 게시 시점은 직접 정할 수 있다(구글 관리형 게시, Apple 수동 출시) |

---

## 2. 결정할 것

| # | 결정 | 선택지 · 영향 | 마감 |
| --- | --- | --- | --- |
| 1 | **계정 명의** — **확정 (2026-11-07): 개인** | 본명이 판매자로 표시된다. 구글은 **12명 × 14일 비공개 테스트 의무** → 1절 「12월 초」 줄. (버린 안: 조직 — Apple 은 법인만, D-U-N-S 필요) | 완료 |
| 2 | **수익화** — **확정 (2026-11-06): 출시 `1.0` 은 없음.** 유료 · 광고는 사업 3단계(노션 「창업」 8절, 2027-06 이후)에서 다시 결정하고, 그때 아래 요건이 생긴다. **무료 재화로만 여는 상자 가챠는 `1.0` 에 있다** ([`systems/24-currency-quest.md`](systems/24-currency-quest.md)) | 없음: 사업자등록 없이 무료 앱으로 출시할 수 있다. 인앱결제: Apple 유료 앱 계약 · 은행 · 미국 세금 양식(W-8BEN), 사업자등록 · 통신판매업 신고(구글 "업체")가 필요하고, 한국 개발자는 사업자 정보가 스토어에 공개된다. **확률형**: 게임산업법 33조 2항(2024-03-22 시행)은 **직·간접 유상** 아이템만 대상 → 무료 재화 전용 상자는 법정 표시 의무 **없음**. 두 스토어의 확률 공개 정책도 **"from a purchase"(유상 구매)** 가 조건이다. 그래도 상자 화면에 확률을 항상 표시한다(24 3-3 #7). 유료화하면 법정 양식 + 2025-08-01 시행 개정법(거짓 · 누락 표시 손해 3배 배상 · 입증책임 전환)이 붙는다. 광고: 광고 ID 신고, iOS 추적 동의(ATT) | 출시: 완료 · 3단계 진입 전 재결정 |
| 3 | **로그인 방식** — **확정 (2026-11-07)**: [`systems/23-login.md`](systems/23-login.md) — **게스트 자동 + 선택 연동(Google · Apple)** · 앱 안 계정 삭제 · 웹 삭제 요청 페이지 | 계정이 생기므로 **앱 안 삭제 기능** 필수(Apple 은 자동 생성 게스트 계정도 해당), 구글은 **웹 삭제 요청 링크**도 요구 → 1절 12/12 준비물. Apple 연동이 있으므로 Sign in with Apple 포함(4.8), 한국 개발자는 서버 알림 엔드포인트 필수(2026-01-01~). 연령 확인은 결정 9 가 정한다 | 완료 (문서) · 구현 기능 12 |
| 4 | **심사자 혼자 플레이** | **① 봇 상대로 확정** (2026-11-06, PM) — 기획은 [`systems/18-bot.md`](systems/18-bot.md). 사람 1 + 봇 1~3 으로 한 판을 끝까지 돌린다. 봇 1단계 완료 날짜는 `development-plan` 8B. (버린 안 ②: 심사 메모 + 시연 영상 + 심사 기간 서버 유지) | 완료 (결정) · 구현 12/26 전 |
| 5 | **iPad 지원** — **확정 (2026-11-07): iPhone 전용** | iPad 13" 스크린샷 · 4:3 배치 검토가 없어진다. 출시 빌드 설정(7절)에서 Target Device = iPhone | 완료 |
| 6 | **EU 배포** — **확정 (2026-11-07): EU 제외** | DSA 트레이더 신고 · 주소 · 전화번호 공개가 없어진다. App Store Connect 배포 국가에서 EU 27개국을 뺀다 | 완료 |
| 7 | **Unity 6 LTS 업그레이드 시점** | **완료** — 2026-10-31 에 6000.3.27f1 로 올렸다 (선택지 ①) | 완료 |
| 8 | **Photon — PUN 유지 / Fusion 전환** — **확정 (2026-11-07): PUN 유지** | 출시 때 100 CCU $95 (3절). Fusion 전환 안 함 | 완료 |
| 9 | **연령 등급** — PM 요구: 「만 7세 이상부터 할 수 있으면 좋겠다. 아니면 전연령.」 | 검토 결과는 **2-1절**. 요약: 한국에 7세 등급 없음 · 전체이용가 가능 · 걸림돌은 등급이 아니라 **만 14세 미만 개인정보 동의**. 추천 = **전체이용가(Apple 4+ 또는 9+) + 앱 안 「만 14세 이상」 자기 확인** | **12/5** (23-login 연령 확인 UI 가 비공개 테스트 빌드에 들어가야 한다) |

### 2-1. 결정 9 — 연령 등급 검토 (2026-11-07 조사)

게임 내용 기준: 만화풍 치와와 카드 대전 — 물어뜯기 · 짖기 같은 카드로 HP 숫자를 깎는다, 피 · 쓰러지는 연출 없음, 채팅 없음, 유료 없음, **무료 재화로만 여는 상자 가챠 있음**, 친구(코드 · 닉네임) 있음, 광고 없음.

**① Apple (새 체계 4+ · 9+ · 13+ · 16+ · 18+, 설문 답 2026-01-31 마감 · 2026-09 부터 제출 때 필수)**

| 설문 항목 | 우리 답 | 전 세계 등급 | 한국 등급 (Apple 페이지의 KR 표) | 근거 · 비고 |
| --- | --- | --- | --- | --- |
| 만화 · 판타지 폭력 | **간헐적** (제안) — 공격은 숫자 감소 + 짧은 효과. 피 · 상해 묘사 없음 | 9+ | **All** | 「빈번」으로 답하면 13+ · **KR-12**. 카드 공격이 매 턴 있어 심사자가 「빈번」으로 볼 **위험**이 있다 → 8절 미확인. 연출을 "맞는 장면"이 아니라 "숫자 · 반응"으로 유지하면 간헐적 주장 근거가 된다 |
| 루트 박스 (정의: "randomized virtual items **for purchase**") | **아니오** — 돈으로 살 수 없다 | (예여도 9+) | (예여도 **All**) | Apple KR 표에서 Loot boxes 는 All 에 속한다. 유료화하면 「예」(24 9절) |
| 시뮬레이션 도박 (정의: 베팅 · 내기) | 아니오 — 상자는 베팅이 아니다 | — | — | 「간헐적」이면 13+ · KR-12. 상자에 **베팅 요소(걸고 따기)** 를 넣지 않는다 |
| 콘테스트 (순위 · 경쟁) | 간헐적 (판 승패) | 4+ | All | 랭킹 · 시즌을 넣어도 「빈번」 = 13+ 이므로 `1.0` 범위 밖 |
| 사용자 생성 콘텐츠 · 메시지 · 채팅 | UGC **예**(닉네임) · 채팅 **아니오** | 4+ | All | 닉네임은 UGC 로 답하는 것이 안전. 채팅을 넣으면 1.2(필터 · 신고 · 차단) |
| 소셜 미디어 (피드 전파) | 아니오 | — | — | 「예」면 13+ · KR-15 |
| 욕설 · 저속한 유머 | **아니오** — 카드 이름 · 문구에 저속한 유머를 넣지 않는다 | — | — | **2026-10 부터 한국은 「간헐적」도 12+** (Apple 2026-08-12 공지). 카드 이름 IP 점검(6-1)에 이 항목을 더한다 |
| 공포 · 성적 · 약물 · 의료 · 무제한 웹 | 아니오 | — | — | |
| 부모 통제 · 연령 확인 (In-App Controls) | 연령 확인 **예** (만 14세 체크, ④) | 4+ | All | 등급을 올리지 않는다 |
| **계산 결과** | | **9+** (폭력 간헐적) 또는 **4+** (폭력 「없음」으로 답할 수 있으면) | **All** | 게임 카테고리라 KR 등급이 제품 페이지에 자동 표시. GRAC 공식 등급으로 덮어쓰기(RCN) 가능 — 필요 없음 |
| Kids 카테고리 (Made for Kids) | **넣지 않는다** | | | 4+/9+ 일 때 **선택** 사항. 넣으면 1.3 · 5.1.4(분석 · 광고 · 외부 링크 보호자 게이트) 의무 + 승인 뒤 되돌릴 수 없음. 넣지 않으면 의무 없음 |

**② Google Play (IARC 설문 → GRAC · PEGI · ESRB 자동)**

| 항목 | 우리 답 | 예상 등급 | 비고 |
| --- | --- | --- | --- |
| 폭력 | 만화 폭력, 경미 · 비현실 | GRAC **전체이용가** · PEGI 7 (만화 폭력) 또는 3 · ESRB E 또는 E10+ | GRAC 전체이용가 기준 = "경미한 폭력 · 사행성 자극 없음". 한국에는 **7세 등급이 없다** — 전체이용가가 아니면 바로 12세이용가 |
| 인앱 구매 · 무작위 아이템 | **없음** (유상 구매 없음) | 상호작용 요소 라벨 「In-Game Purchases (Includes Random Items)」 **붙지 않음** | 라벨은 유상 구매가 있을 때만. 등급 자체에는 영향 없음 (상호작용 요소 ≠ 콘텐츠 등급) |
| 사용자 상호작용 | 예 (닉네임 · 친구 · 멀티플레이) | 라벨 「Users Interact」 | 등급 영향 없음 |
| 사행성(시뮬레이션 도박) | 없음 | — | 상자를 베팅으로 만들지 않는다 |
| **타깃 연령 · 콘텐츠 선언** | **13세 이상 그룹만** (13-15 · 16-17 · 18+) | 콘텐츠 등급은 그와 별개로 **전체이용가** 가능 (콘텐츠 등급 = 내용, 타깃 = 대상) | 13세 미만 그룹을 하나라도 넣으면 **Families 정책**: Families 자가 인증 광고 SDK 만, 아동에게 광고 ID · 기기 식별자 전송 금지, 어린이용 Data safety · 심사 강화. 우리는 광고가 없어 부담은 작지만 Photon · UGS SDK 의 아동용 적합성을 따로 증명해야 한다 → 넣지 않는다. **주의**: 구글은 "youthful animation or young characters" 같은 등록 자료 외형으로 타깃을 **재평가**할 수 있다 → 스토어 문구에 "친구와 함께하는 파티 카드 대전" 처럼 성인 · 청소년 대상임을 쓴다. 「어린이」의 연령은 구글이 "국가마다 다름 · 현지법 확인" 이라고만 한다 — 한국은 개인정보보호법 14세가 기준이지만 Play Console 그룹은 13 으로 끊긴다 (가정: 13-15 그룹 선택이 Families 를 발동시키지 않는다) |

**③ 한국 개인정보보호법 — 만 14세 미만 (이것이 실제 걸림돌)**

| 질문 | 조사 결과 | 우리에게 |
| --- | --- | --- |
| 게스트 계정(UGS 익명 ID · 닉네임 · 기기 식별자) 만으로도 개인정보 처리인가 | 개인정보위 해석: 기기 식별자(IMEI · SSAID 등)는 **특정 개인의 기기에 귀속되면 개인정보**. 닉네임 · 친구 코드 · 전적 · IP(Photon) 가 한 ID 에 묶이면 「다른 정보와 쉽게 결합해 식별 가능」에 해당할 가능성이 높다 | **개인정보 처리로 보고 설계한다** (Data safety · 방침에도 그렇게 적는 것과 일치) |
| 만 14세 미만이면 | 법 22조의2: 동의가 필요한 처리는 **법정대리인 동의**를 받고 **동의 사실을 확인**해야 한다. 확인 방법(표준 지침): 사이트 동의 표시 + 휴대전화 문자 / 법정대리인 신용카드 / 휴대전화 본인인증 / 서면 · 우편 · 팩스 / **이메일 발송 → 동의 회신** | 휴대전화 본인인증은 본인확인기관 계약(사업자 필요 · 건당 비용 · 월 기본료)이라 **개인 명의 무료 앱에는 무리**. 가능한 것은 **이메일 동의** (보호자 이메일 입력 → 링크 클릭 → 서버 기록) — 구현 + 운영(미회신 · 분쟁) 부담 |
| 「만 14세 이상」 자기 확인 체크로 충분한가 | 개인정보위 아동 · 청소년 가이드라인(2022-07-22): 연령 확인은 **생년월일 입력 또는 「만 14세 이상」 자기 체크**를 제시하고, **허위 기재를 막는 합리적 장치**를 권장. 아동이 실질적으로 이용할 것으로 예상되는 서비스에 적용 | 업계 관행(국내 게임 다수가 「만 14세 이상 가입 가능」 + 체크)으로 **통용된다**. 합리적 장치 = 체크 전 안내 문구 · 체크 안 하면 진행 불가 · 약관에 「14세 미만 이용 불가」 명시 · 발견 시 계정 삭제 |
| 2026 법 개정(2026-03-10 공포 · 2026-09-11 시행) | 과징금 상한 · 유출 통지 72시간 · CPO · ISMS-P 등 — **아동 동의 연령(14) 은 그대로** | 영향 없음. 보호 연령을 18 · 19 로 올리는 것은 2022 기본계획의 **구상**이지 법이 아니다 |
| 7세 이상(7 ~ 13세)을 받으려면 | 생년월일 입력 → 14세 미만이면 보호자 이메일 동의 흐름 → 동의 전까지 플레이 불가(또는 저장 없는 오프라인 봇 판만) → 동의 기록 보관 · 보호자 열람 · 철회 처리 · 아동용 쉬운 처리방침 | 로그인 문서에 **상태 3개 · 화면 2개 · 서버 호출 1개 · 운영 절차**가 늘어난다. `1.0` 일정(2027-02-04)에는 **권하지 않음** |

**④ 결론 · 선택지**

| 선택지 | 스토어 등급 | 앱 안 연령 확인 | 7 ~ 13세 | 비용 · 위험 | 추천 |
| --- | --- | --- | --- | --- | --- |
| A | **전체이용가** · Apple 4+/9+ · Google 타깃 13+ · Kids 아님 | **「만 14세 이상」 자기 확인** — 23-login 의 동의 체크 1개에 문구를 더해 체크 수는 그대로 1개 + 약관 「만 14세 미만 이용 불가」 | 공식적으로 불가 (실제로는 체크로만 막힘) | 가장 싸다. 구글이 외형으로 아동 타깃이라 재평가하면 Families 대응 필요(광고 없어 범위 작음) | **✔ `1.0`** |
| B | 전체이용가 · 4+/9+ | 생년월일 입력 → 14세 미만은 **보호자 이메일 동의** 흐름 | 가능 (PM 의 「7세 이상」 실현) | 로그인 문서 · 서버 · 운영 추가. 이메일 동의의 법적 충분성은 표준 지침에 있음 | 사업화 단계(2027-06 이후)에 재검토 |
| C | **12세이용가** · Apple 13+ | A 와 같음 (12 ~ 13세도 14세 미만이라 **동의 문제가 그대로**) | 불가 | 등급만 올라가고 얻는 것이 없다. 「빈번한 만화 폭력」으로 심사되면 **의도치 않게 여기로 떨어진다** | ✘ |

- **「만 7세 이상」은 한국 등급으로 표현할 수 없다.** 전체이용가 아니면 12세이용가다. Apple 9+ · PEGI 7 이 가장 가깝지만 한국 제품 페이지에는 「All」로 보인다.
- **추천 = A.** 이유: 등급은 내용이 정하고 전체이용가가 되지만, 7 ~ 13세를 **합법적으로** 받는 비용(보호자 동의 흐름 + 운영)이 `1.0` 에 맞지 않는다. 등급을 올려서 해결되는 문제가 아니다(C).
- **A 가 23-login 에 요구하는 것**: 23 의 지금 가정은 「동의 체크 1개 · **연령 확인 없음**」(23 3절 #9 · 7-5) 이다. 여기에 연령 확인을 **더한다** — ① 동의 체크의 문구를 「**만 14세 이상이며** 이용약관 · 개인정보 처리방침에 동의합니다」로 바꿔 체크는 1개 유지(제안 — 체크를 2개로 나눠도 된다), 체크 전 「시작」 비활성, ② 약관 · 처리방침에 「만 14세 미만은 이용할 수 없습니다」 문구, ③ 체크 사실 + 시각을 계정에 저장(23 이 이미 「동의 시각」을 저장한다 — 같은 값), ④ 연동 계정(Google · Apple)으로 들어와도 같은 체크(연동 계정의 생년월일은 받지 않는다 — 최소 수집), ⑤ Apple 설문 「연령 확인 있음」으로 답한다. 결정되면 23 3절 #9 · 7-5 · 미정 #1 과 20-title-ui-full 7-6 동의 줄을 고친다.
- **마감 제안 12/5** — 비공개 테스트 빌드(12/12)에 체크 화면이 들어가야 Data safety · 등급 설문과 맞는다.

---

## 3. 비용

| 항목 | 금액 | 주기 |
| --- | --- | --- |
| Google Play 개발자 등록 | US$25 | 1회 |
| Apple Developer Program | US$99 (원화 약 ₩129,000, 공식 원화가 미확인) | **매년** |
| Photon PUN 100 CCU | US$95 (월 트래픽 0.3 TB) | 12개월, 1회 결제 |
| Photon 그 이상 | 500 CCU $95/월 · 1,000 CCU $185/월 | 매월 |
| iOS 빌드 (Mac 없음) | Unity Build Automation 매월 Mac 100분 무료, 초과 분당 $0.07 | 사용량 |
| 테스트용 iPhone | 1대 필수, 멀티플레이 확인에는 2대 권장 | — |
| D-U-N-S | 무료 (조직일 때만) | — |

**Photon 20 CCU 참고**: 앱을 켜자마자 서버에 연결하므로 타이틀 · 로비에 있는 사람도 CCU에 포함된다. 100 CCU 미만 요금제는 한도를 넘으면 접속이 거절된다(`MaxCcuReached`).

---

## 4. Google Play

### 4-1. 기술 요건

| 항목 | 요건 | 우리 상태 |
| --- | --- | --- |
| Target API | **2026-08-31부터 신규 앱 · 업데이트는 API 36** | Unity 6.3 지원 → OK. SDK Platform 36 · build-tools 36 설치 필요 |
| 16 KB 메모리 페이지 | API 35 이상 타깃 앱은 필수 | Unity 6.3 지원 → OK. Photon은 관리 코드(DLL)뿐이라 네이티브 `.so` 없음 |
| 64비트 | arm64-v8a 필수 | 출시 빌드에서 IL2CPP + ARM64 (7절) |
| 형식 · 서명 | AAB + Play 앱 서명. 개발자는 업로드 키로 서명 | 업로드 키를 잃어버리면 재설정을 요청할 수 있지만 번거롭다. 저장소 밖에 백업 |
| 용량 | 베이스 모듈 500 MB. 200 MB를 넘으면 모바일 데이터 사용자에게 경고가 뜬다 | 카드 게임이라 에셋 분할 배포는 필요 없을 것 |
| Android 16 대화면 | 태블릿(sw600dp 이상)에서 방향 고정이 무시된다. 단 manifest에 `android:appCategory="game"`이 있으면 예외 | 커스텀 manifest 로 넣는다 |

### 4-2. 계정 · 테스트

- 등록비 $25. 신분증 · 신용카드로 신원 확인. 신규 개인 계정은 Play Console 앱으로 **Android 기기 보유 인증**도 한다.
- **2023-11-13 이후 만든 개인 계정**: 테스터 **12명 이상**이 **14일 연속** 참여한 비공개 테스트 → 프로덕션 액세스 신청(심사 보통 7일 이내). 중간에 탈퇴한 테스터는 세지 않는다. 조직 계정은 면제.
- 테스트 트랙: 내부(최대 100명, 몇 분 안에 배포, Data safety 면제) · 비공개(12명 요건은 여기서 채운다) · 공개.
- 사전 출시 보고서가 AAB 업로드 때 자동으로 돈다. Unity 게임은 로봇이 UI를 거의 못 누르므로 실행 시 튕김 확인 정도로만 쓸모 있다.
- Android 개발자 인증(2026-09 일부 국가 → 2027 전 세계)은 Play로만 배포하면 자동 처리된다.

### 4-3. 스토어 등록 자료

| 자료 | 규격 |
| --- | --- |
| 앱 이름 | 30자 |
| 간단한 설명 / 자세한 설명 | 80자 / 4,000자 |
| 아이콘 | 512 × 512 PNG(32비트, 알파 포함), 1 MB 이하 |
| 그래픽 이미지(피처 그래픽) | 1024 × 500 JPEG 또는 PNG(알파 없음) |
| 스크린샷 | 2~8장. 게임 추천에 노출되려면 **16:9 가로 1920 × 1080 이상 3장 이상** |
| 동영상 | 선택. YouTube URL(광고 끔, 연령 제한 없음) |
| 연락처 이메일 | 필수 |

### 4-4. 앱 콘텐츠 신고 (Play Console)

- **개인정보처리방침**: Play Console **과 앱 안** 양쪽에 링크.
- **Data safety**: 비공개 테스트부터 필수. Photon이 처리하는 개인정보는 **IP 주소 · 개발자가 넘기는 UserId(닉네임)** 다. "앱 기능" 목적의 사용자 ID 수집으로 신고하는 편이 안전하다. 전송 암호화 · 삭제 요청 방법도 적는다.
- **콘텐츠 등급(IARC 설문)**: 한국 등급(전체 · 12 · 15)까지 자동으로 붙는다. 청소년이용불가만 게임물관리위원회 직접 심의. 답안은 2-1 ② — 예상 **전체이용가**. 유상 구매가 없어 「무작위 아이템 포함」 라벨은 붙지 않는다.
- **타겟층**: 13세 미만을 포함하면 가족 정책이 적용된다. 귀여운 아트라도 **13세 이상만 선택**하고, 등록 자료가 아동용으로 보이지 않게 주의 (2-1 ② 주의 — 구글은 외형으로 재평가할 수 있다). 결정 9.
- 광고 포함 여부 · 앱 접근 권한(로그인이 필요하면 테스트 계정) · 건강 앱 신고("없음"으로 제출) · 광고 ID(광고 SDK를 넣을 때).

---

## 5. App Store

### 5-1. 기술 요건

| 항목 | 요건 | 우리 상태 |
| --- | --- | --- |
| Xcode / SDK | **2026-04-28부터 Xcode 26 · iOS 26 SDK 이상** | Unity Build Automation에 Xcode 26 이미지가 있다(Sequoia/Tahoe). Unity 6.3 과의 조합은 11월 시험 빌드로 확인 |
| 최저 iOS | 2026-09-09부터 iOS 13 이상. Xcode 26의 최저 배포 타깃은 iOS 15 | 출시 빌드에서 15.0 (7절) |
| 다음 SDK | iOS 27 SDK는 UIScene 필수. 의무화 날짜 미발표(2027-04 전후 추정) | Unity 6.3 지원 → OK |
| Privacy Manifest | Required-Reason API 사유 선언 필수 | 엔진 것은 Unity 가 자동으로 넣는다. Photon은 관리 코드라 별도 manifest가 필요 없어 보인다(공식 입장 미확인) |
| 수출 규정(암호화) | 제출 때 암호화 사용 여부 신고 | Photon 자체 암호화는 인증 용도라 보통 면제로 본다(미확인). `ITSAppUsesNonExemptEncryption` |
| 알려진 빌드 오류 | Xcode 26 링커 `dylibToOrdinal` 오류 | IL2CPP Code Generation을 "Optimize for code size and build time"으로 바꾸면 우회된다 |

### 5-2. Mac 없이 빌드 · 서명 · 업로드

1. **빌드**: Unity Build Automation(Personal 플랜도 iOS 가능, 매월 100분 무료). 대안으로 GitHub Actions macOS(분당 약 $0.062) · Codemagic(Unity Plus/Pro 필요) · MacinCloud(시간당 약 $1).
2. **서명**: 개발자 포털에서 배포 인증서 · App ID · App Store용 프로비저닝 프로파일. 인증서 요청 파일(CSR)과 `.p12` 변환은 Windows OpenSSL로 된다.
3. **업로드**: Build Automation은 IPA만 만든다. Windows에서 **iTMSTransporter CLI** 또는 App Store Connect API로 올린다.
4. **실기기**: iPhone 최소 1대. TestFlight로 설치.

### 5-3. 스토어 등록 자료

| 자료 | 규격 |
| --- | --- |
| 이름 / 부제 | 30자 / 30자 |
| 키워드 | 100바이트 — 한글은 1자에 3바이트라 **약 33자** |
| 설명 / 프로모션 텍스트 | 4,000자 / 170자 |
| 아이콘 | 1024 × 1024 PNG |
| 스크린샷 iPhone 6.9" | 1~10장, 가로 2736 × 1260 (없으면 6.5" 2778 × 1284 필수) |
| 스크린샷 iPad 13" | **해당 없음** — iPhone 전용 확정 (결정 5, 2026-11-07) |
| 앱 미리보기 영상 | 선택. 15~30초, 최대 3개 |
| URL | 지원 URL(연락처 포함) · 개인정보처리방침 URL 필수, 마케팅 URL 선택 |

### 5-4. 심사 · 신고

- **TestFlight**: 내부 100명(심사 없음) · 외부 최대 10,000명(첫 빌드는 베타 심사). 공개 링크 가능.
- **심사**: 90%가 24시간 안. 멀티플레이 전용 조항은 없지만 "상대가 없어 무한 대기 → 2.1 거절" 사례가 있다 → 결정 4. 심사 메모는 4,000바이트.
- **연령 등급**: 새 체계(4+/9+/13+/16+/18+) 설문 + **소셜 미디어 설문**(2026-09부터 필수. 피드로 UGC를 퍼뜨리는 기능이 대상이라 단순 멀티플레이 · 채팅은 아닐 가능성이 크다). 게임은 한국 등급(All/12/15/19)이 설문 답으로 자동으로 붙는다 — Apple KR 표: 간헐적 만화 폭력 · 루트 박스 · UGC · 채팅 = **All**, 빈번한 만화 폭력 · 간헐적 시뮬레이션 도박 · 저속한 유머 = **12+**, 소셜 미디어 · 무제한 웹 = 15+. 2026-10부터 간헐적 욕설 · 저속한 유머는 All → 12+. GRAC 공식 등급 번호(RCN)로 덮어쓸 수 있다(2026-08-12~). 답안 · 결론은 2-1 (결정 9).
- **App Privacy 라벨**: 4-4 Data safety와 같은 내용으로 신고.
- **채팅을 넣으면(1.2)**: 필터링 · 신고 · 차단 기능과 연락처 공개가 필요하다.
- **한국 표시 정보**: 한국 거주 개발자는 이메일 등이 제품 페이지에 공개된다(사업자면 사업자등록번호도).
- **미국 텍사스**: 2026-06부터 연령 확인 의무(Declared Age Range API). 미국 배포 시 검토.

---

## 6. 공통 준비물

| 준비물 | 쓰는 곳 | 담당 |
| --- | --- | --- |
| 개인정보처리방침 웹페이지 (한국어 · 영어) — 「만 14세 미만 이용 불가」 문구 포함 (결정 9) | 양쪽 스토어 + 앱 안 링크 | 기획 |
| **웹 계정 삭제 요청 페이지** (결정 3 · 구글 요건) | Google Play Data safety · 앱 안 설정 「계정」 | 기획 |
| 지원 페이지 · 연락처 이메일 | 양쪽 스토어 | 기획 |
| 패키지 ID(Bundle ID) | 양쪽. **출시 후 바꿀 수 없다**. **보류 (팀명 결정 뒤)** — 팀명이 바뀔 수 있어 PM 이 뒤에 정한다 (2026-11-07). 첫 스토어 업로드(12/12 비공개 테스트) 전에는 필요 | PM |
| 스토어 문구 (4-3 · 5-3 글자 수) — 아동 대상으로 읽히지 않게 (2-1 ②) | 양쪽 | 기획 |
| 아이콘 512 · 1024, 피처 그래픽, 스크린샷 (iPhone 6.9" 만) | 양쪽 | 디자인 |
| 등급 설문 답변 — **2-1 ① · ② 표가 초안** | IARC · Apple | 기획 (완료 · 결정 9 뒤 확정) |
| 앱 안 「만 14세 이상」 확인 체크 (결정 9 → 23-login) | 첫 실행 동의 화면 | 기획 · UI |
| 심사자용 플레이 안내 (결정 4) | Apple 심사 메모 · 구글 앱 접근 권한 | 기획 |
| 카드 이름 · 그림 IP 점검 통과 (6-1) | 양쪽 스토어 신고 대비 | 기획 · 디자인 |

### 6-1. 카드 이름 · 그림 점검 (2026-11-06)

스토어의 지식재산권 정책은 **권리자 신고 기반**이라 심사 때는 안 걸려도 출시 뒤에 내려갈 수 있다 (Apple 5.2.1, Google Play "변형해도 위반"). 무료 출시여도 같다. 그래서 카드가 노션 「카드 DB」에 올라올 때마다 아래 기준으로 본다.

| # | 보는 것 | 기준 | 근거 |
| --- | --- | --- | --- |
| 1 | 이름 | 작품 제목 · 대사 · 유행어는 저작물이 아니라 그대로 써도 된다. 단 KIPRIS 상표명칭 검색에서 **9류(게임 소프트웨어) · 41류(게임 서비스)** 에 등록된 상표와 같으면 바꾼다 | 제호 · 짧은 문구 저작물성 부정(대법원 77다90). 2026-11-06 조회: 영역전개 · 데스노트 등록 없음, 무야호는 16 · 30 · 32 · 35 · 43류뿐 |
| 2 | 그림 — 특정 캐릭터 | 다른 작품 캐릭터의 **식별 요소(복장 · 소품 · 머리 · 손 모양 · 표정 연출)가 2개 이상** 겹치면 수정. 1개는 통과 | 캐릭터 그림은 저작물(대법원 2010, 신야구 사건). 한국 법원은 패러디 항변을 좁게 본다 |
| 3 | 그림 — 원본 복제 | 원본 컷 · 사진 · 방송 화면을 붙이거나 구도를 따라 그리면 수정. 로고 글씨체도 쓰지 않는다. 참고용 원본은 노션에만 두고 출시 에셋에 넣지 않는다 | 저작권법 복제 · 2차적저작물. Google: "Modifying copyrighted content may still lead to a violation" |
| 4 | 그림 — 실제 사람 | 실제 인물의 얼굴 · 이름은 쓰지 않는다. 강아지로 바꿔 그린 패러디는 통과 | 부정경쟁방지법 타목(2022-06-08~) 유명인 성명 · 초상 |

**기록** — 노션 「카드 DB」 속성 「IP 점검」(미점검 · 통과 · 수정 필요). 수정 필요면 그 카드에 댓글로 **무엇을 · 왜 · 어떻게** 바꿀지 남긴다. 매일 09:00(KST) 클라우드 루틴이 새로 올라오거나 바뀐 카드를 점검하고, 급하면 PM 이 세션에 "카드 점검"을 시킨다. 카드 템플릿에 같은 기준 3줄이 있어 올리는 사람이 먼저 거른다.

**2026-11-06 전수 점검** (그림 있는 32장 + 데X노트): 수정 필요 3 — 데X노트(만화 원본 페이지), 영역전개(고죠 사토루 요소 3개), 얼음!(오징어 게임 참가자 복장). 나머지 30장 통과. 그림 없는 카드는 이름만 봤고 전부 문제 없음.

---

## 7. 출시 빌드 설정 (기능 13에서 한 번에)

개발 중에는 건드리지 않는다. 출시 빌드를 만들 때만 확인한다.

| 설정 | 값 |
| --- | --- |
| Android 스크립팅 백엔드 · CPU | IL2CPP · ARM64 |
| Android Target API | 36 (Automatic 대신 명시) |
| Android manifest | `android:appCategory="game"` |
| 업로드 키스토어 | 저장소 밖에 보관 · 팀 백업 |
| iOS 최저 버전 | 15.0 |
| iOS Target Device | **iPhone 전용** (결정 5, 2026-11-07) — Requires Full Screen 켬 (iPad 멀티태스킹 요건 회피) |
| Photon 지역 | `kr` 고정 검토 (현재 개발 지역 `hk`) |

---

## 8. 미확인

- Unity 6000.3.27f1 과 Xcode 26 조합, PUN2 · DOTween · Splines 동작 → 11월 시험 빌드로 확인
- iOS 27 SDK 의무화 날짜 (Apple 미발표)
- Photon의 privacy manifest · 수출 규정 공식 입장, Data safety 작성 예시
- 게임제작업 · 배급업 등록이 무료 인디 게임에 실제로 적용되는지 → 구청 문의
- 인앱결제가 통신판매업 신고의 "거래"에 해당하는지, 세금 처리 → 세무사
- 확률형 아이템 면제 기준 — 「게임물을 제작 · 배급 · 제공하는 자 모두가 **최근 3년간 연평균 매출액 1억 원 이하**인 중소기업」으로 확인(2026-11-07). `1.0` 은 무료 재화 전용이라 면제와 무관하게 대상 밖. 유료화 때 "3년" 산정 시점만 다시 확인
- **Apple 설문 「만화 · 판타지 폭력」의 간헐적 / 빈번 판정** — 카드 공격이 매 턴 있어 심사자가 「빈번」(13+ · KR-12)으로 볼 수 있다. 심사 메모에 "피해는 숫자 감소, 피 · 상해 묘사 없음"을 적고, 거절되면 GRAC 자체등급분류(전체이용가) RCN 으로 덮어쓰기 검토
- **Google Play 타깃 재평가** — 치와와 아트가 아동 대상으로 읽혀 13세 미만 포함을 요구받을 수 있음. 요구받으면 Families 정책 범위(광고 없음 · 기기 식별자 전송 여부 · Photon/UGS SDK) 확인
- **「만 14세 이상」 자기 확인 체크의 충분성** — 가이드라인은 「합리적 장치」를 권장하지 기준을 못 박지 않는다. 분쟁 사례 없음. 법률 자문 1회 권장(출시 전)
- Apple 회비 공식 원화가, 계정 가입 소요 시간

---

## 9. 출처

**Google Play**
- Target API: https://developer.android.com/google/play/requirements/target-sdk · https://support.google.com/googleplay/android-developer/answer/11926878
- Unity Android 호환: https://docs.unity3d.com/2022.3/Documentation/Manual/android-requirements-and-compatibility.html
- 16 KB: https://developer.android.com/guide/practices/page-sizes · https://discussions.unity.com/t/info-unity-engine-support-for-16-kb-memory-page-sizes-android-15/1589588
- Android 16 대화면: https://developer.android.com/about/versions/16/behavior-changes-16 · https://discussions.unity.com/t/info-android-16-resizeability-changes/1659976
- 앱 서명: https://support.google.com/googleplay/android-developer/answer/9842756
- 64비트: https://developer.android.com/google/play/requirements/64-bit
- 용량: https://support.google.com/googleplay/android-developer/answer/9859372
- 계정 · 테스트 요건: https://support.google.com/googleplay/android-developer/answer/6112435 · https://support.google.com/googleplay/android-developer/answer/13628312 · https://support.google.com/googleplay/android-developer/answer/14151465
- 개발자 인증: https://developer.android.com/developer-verification
- 등록 자료: https://support.google.com/googleplay/android-developer/answer/9866151 · https://support.google.com/googleplay/android-developer/answer/9859152
- 개인정보 · Data safety: https://support.google.com/googleplay/android-developer/answer/10144311 · https://support.google.com/googleplay/android-developer/answer/10787469
- 등급 · 타겟층: https://support.google.com/googleplay/android-developer/answer/9898843 · https://support.google.com/googleplay/android-developer/answer/9867159
- 콘텐츠 등급 요건(IARC) · Families 정책 (「어린이」 정의 · 광고 SDK · 식별자 · 외형 재평가, 2026-11-07 확인): https://support.google.com/googleplay/android-developer/answer/9859655 · https://support.google.com/googleplay/android-developer/answer/9893335
- IARC 등급 정의 (GRAC · ESRB · PEGI): https://www.globalratings.com/ratings-definitions/
- 계정 삭제: https://support.google.com/googleplay/android-developer/answer/13327111
- 결제 · 확률 공개: https://support.google.com/googleplay/android-developer/answer/9858738
- 한국 판매자 정보: https://support.google.com/googleplay/android-developer/answer/3255733?hl=ko
- 테스트 트랙 · 사전 출시 보고서 · 심사: https://support.google.com/googleplay/android-developer/answer/9845334 · https://support.google.com/googleplay/android-developer/answer/9842757 · https://support.google.com/googleplay/android-developer/answer/9859751

**App Store**
- 요건 일정: https://developer.apple.com/news/upcoming-requirements/
- Xcode 요구사항: https://developer.apple.com/xcode/system-requirements
- UIScene · Unity: https://discussions.unity.com/t/info-apple-update-your-editor-to-receive-uiscene-lifecycle-support/1709065 · https://discussions.unity.com/t/2022-3-lts-is-now-enterprise-and-industry-only/1652230
- Xcode 26 링커 오류: https://issuetracker.unity3d.com/issues/ios-xcode-26-build-for-ios-fails-on-assertion-failed-when-arkit-is-added-as-a-plug-in-provider
- Build Automation: https://docs.unity.com/en-us/build-automation/reference/available-xcode-versions · https://support.unity.com/hc/en-us/articles/34748492914964 · https://docs.unity.com/ugs/en-us/manual/devops/manual/build-automation/sign-build-artifacts/sign-an-ios-application
- 업로드: https://help.apple.com/itc/transporteruserguide/en.lproj/static.html
- 가입: https://developer.apple.com/kr/programs/enroll/ · https://developer.apple.com/support/enrollment/
- Privacy manifest: https://docs.unity3d.com/2022.3/Documentation/Manual/apple-privacy-manifest-policy.html
- 개인정보 · 계정 삭제: https://developer.apple.com/app-store/user-privacy-and-data-use/ · https://developer.apple.com/support/offering-account-deletion-in-your-app/
- Sign in with Apple 한국: https://developer.apple.com/news/?id=j9zukcr6
- 심사 가이드라인: https://developer.apple.com/app-store/review/guidelines/
- 연령 등급 · 한국 등급: https://developer.apple.com/news/upcoming-requirements/?id=07242025a · https://developer.apple.com/news/?id=tlur8uvi · https://developer.apple.com/news/?id=7byvco78 · https://developer.apple.com/news/?id=oj3r9pvw
- 연령 등급 값 · 정의 (전 세계 · KR 표 · 루트 박스 "for purchase" 정의, 2026-11-07 확인): https://developer.apple.com/help/app-store-connect/reference/app-information/age-ratings-values-and-definitions/ · 설문 · Made for Kids: https://developer.apple.com/help/app-store-connect/manage-app-information/set-an-app-age-rating/
- Kids 카테고리 요건 (1.3 · 5.1.4): https://developer.apple.com/app-store/categories/
- 스크린샷 · 미리보기 · 문구: https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/ · https://developer.apple.com/help/app-store-connect/reference/app-information/app-preview-specifications/ · https://developer.apple.com/help/app-store-connect/reference/app-information/platform-version-information/
- TestFlight · 심사: https://developer.apple.com/testflight/ · https://developer.apple.com/distribute/app-review/
- EU DSA: https://developer.apple.com/help/app-store-connect/manage-compliance-information/manage-european-union-digital-services-act-trader-requirements/
- 수출 규정: https://developer.apple.com/documentation/bundleresources/information-property-list/itsappusesnonexemptencryption
- 계약 · 세금 · 한국 표시: https://developer.apple.com/help/app-store-connect/manage-agreements/sign-and-update-agreements/ · https://developer.apple.com/help/app-store-connect/manage-tax-information/provide-tax-information/ · https://developer.apple.com/help/app-store-connect/manage-compliance-information/manage-korea-compliance-information
- 텍사스 연령 확인: https://developer.apple.com/news/?id=sg176nne

**공통 · 한국**
- Photon 요금: https://www.photonengine.com/pun/pricing · https://forum.photonengine.com/discussion/16907/can-i-publish-a-game-that-uses-the-20-ccu-free-license-model · https://blog.photonengine.com/new-free-100-ccu-for-photon-fusion-and-quantum-games/
- Photon 개인정보: https://www.photonengine.com/terms/gdpr
- 확률형 아이템: https://www.korea.kr/news/policyNewsView.do?newsId=148924297 · https://www.etnews.com/20250806000278 · 정의(직·간접 유상) · 1억 원 면제 · 2025-08-01 개정 시행: https://www.korea.kr/multi/visualNewsView.do?newsId=148924522 · https://factrace.kr/issues/loot-box-disclosure-2026 · https://www.newspost.kr/news/articleView.html?idxno=216334
- 만 14세 미만 · 법정대리인 동의: 개인정보 보호법 22조의2 https://casenote.kr/법령/개인정보_보호법/제22조의2 · 동의 확인 방법 https://www.privacy.go.kr/front/contents/cntntsView.do?contsNo=75 · 아동 · 청소년 개인정보 보호 가이드라인(2022-07-22) https://www.korea.kr/news/policyNewsView.do?newsId=148903862 · https://www.kimchang.com/ko/insights/detail.kc?sch_section=4&idx=25475 · 보호 연령 확대 구상(기본계획, 법 아님) https://www.korea.kr/news/policyNewsView.do?newsId=148903393 · 2026-09-11 시행 개정 요지 https://datalaw.kr/guides/pipa-2026-amendment/
- 기기 식별자의 개인정보 해당성(개인정보위 해설): https://talk.privacy.go.kr/talk/know/knowBbsDetailView.do?nttId=3603
- GRAC 등급 기준(사행성 · 폭력): https://www.grac.or.kr/Institution/EtcForm01.aspx · https://www.mcst.go.kr/usr/child/info/policyPackage/grade.jsp
- 게임제작업 등록: https://www.gov.kr/mw/AA020InfoCappView.do?HighCtgCD=A09006&CappBizCD=13700000177
- 폰트 라이선스 전체 목록(상업 이용 가능 여부 · 확인 방법): [`systems/21-font.md`](systems/21-font.md) 3절. 학교안심 둥근미소(OFL, 폐기 예정): https://copyright.keris.or.kr/wft/fntDwnldView?fntGrpId=GFT202408200000000000003

---

## 변경 이력

| 날짜 | 내용 |
| --- | --- |
| 2026-09-26 | 신규. 두 스토어 요건 조사, 일정 역산, 결정 8개 |
| 2026-11-01 | 결정 7 완료(Unity 6000.3.27f1). 엔진 관련 요건 · 미확인 항목을 새 버전 기준으로 |
| 2026-11-06 | 결정 2 확정: 출시 `1.0` 수익화 없음. 사업 3단계(노션 「창업」 8절) 반영 |
| 2026-11-06 | 6-1 카드 이름 · 그림 점검 기준 신설. 노션 「카드 DB」 「IP 점검」 속성 · 카드 템플릿 체크 · 매일 루틴. 전수 점검: 수정 필요 3 |
| 2026-11-07 | 결정 1 개인 · 3 로그인(23-login) · 5 iPhone 전용 · 6 EU 제외 · 8 PUN 유지 확정. 패키지 ID 보류(팀명 뒤). 일정에 12월 초 테스터 모집 마감 · 웹 삭제 요청 페이지 · 12/5 연령 등급 결정. **결정 9 연령 등급 신설 + 2-1 검토**(Apple KR 표 · IARC · Families · 만 14세 동의 · 확률형 아이템 적용 범위). 결정 2 확률형 문구를 「직·간접 유상만 대상」으로 고침. 8절 미확인 4건 추가 |
