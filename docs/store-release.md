# 스토어 출시 준비 — Google Play · App Store

**작성일**: 2026-09-26 (조사 기준일도 같음. 스토어 규정은 자주 바뀌므로 기능 13 시작 때 다시 확인)
**출시 목표**: `1.0.0` 2027-02-04 ([`development-plan.md`](development-plan.md) 기능 13)
**범위**: 오래 걸리는 외부 준비 · 결정할 것 · 비용 · 스토어 요건. 게임 기능 개발은 다루지 않는다.

---

## 0. 결론 먼저

1. **지금 엔진(Unity 2022.3.62f3)으로 2/4 출시는 된다.** 구글의 현재 요건(API 36, 16 KB 페이지)을 이 버전이 지원하고, iOS는 Xcode 26으로 빌드하면 된다.
2. **출시 뒤 첫 iOS 업데이트부터 막힐 수 있다.** iOS 27 SDK로 빌드한 앱은 UIScene 수명주기가 없으면 실행하자마자 죽는다. 2022.3 중 이를 지원하는 버전(72f1 이상)은 엔터프라이즈 전용이다. Apple 관례상 **2027년 4월 전후 iOS 27 SDK가 의무화될 가능성이 크다**(미발표). 그러면 Unity 6 LTS로 올려야 한다 → 2절 결정 7.
3. **구글 개인 계정은 테스터 12명 × 14일 비공개 테스트를 해야 출시할 수 있다.** 팀이 6명이라 **외부 테스터 6명 이상**이 필요하다. 역산하면 **늦어도 1/6에는 비공개 테스트를 시작**해야 한다. `0.5.0`(12/12)으로 시작하는 것을 권장한다.
4. **PUN 무료 20 CCU는 개발 전용이라 출시에 못 쓴다.** 출시 때 100 CCU 요금제($95, 1회 결제로 12개월)를 산다.
5. **Apple 심사자는 혼자 게임을 해본다.** 4인 온라인 전용 게임은 "상대가 없어 진행 불가"로 거절(2.1)될 위험이 있다. 대응 방식은 기획 결정이다 → 결정 4.

---

## 1. 일정 역산 — 오래 걸리는 일

| 언제까지 | 할 일 | 걸리는 시간 · 이유 |
| --- | --- | --- |
| **10월** | 계정 명의 결정(결정 1) → Google Play 개발자 계정 · Apple Developer Program 가입 | 신원 확인 수일. 조직 명의면 D-U-N-S 발급에 최대 30일 |
| 10월 | 게임제작업 · 배급업 등록이 필요한지 구청에 문의 | 게임산업법 25조. 소규모 무료 게임에 실제로 적용되는지 미확인 |
| **11월 중** | iOS 시험 빌드 1회 (빈 씬이라도) — Unity Build Automation → TestFlight | 2022.3.62f3와 Xcode 26 조합이 공식 호환표에 없다. 플랜의 아이폰 빌드(기능 11, 12/13~)보다 앞당겨 확인해 둔다 |
| 11월 중 | 외부 테스터 6명 이상 모집 (Android 폰 보유, Gmail) | 14일 동안 중간에 빠지면 인원에서 빠진다. 여유 있게 15명 이상 |
| **12/12** (`0.5.0`) | 개인정보처리방침 웹페이지 · 지원 연락처 준비 | 비공개 테스트부터 Data safety 신고가 필요하고, 거기에 방침 URL이 들어간다 |
| 12/12 | 구글 비공개 테스트 시작 · TestFlight 외부 테스트 시작 | 14일 연속 |
| 12/26 | 기획: 로그인 방식 확정 (결정 3) | 계정 삭제 · Sign in with Apple 의무가 여기에 달려 있다 |
| 12/26 이후 | 구글 "프로덕션 액세스" 신청 | 심사 최대 약 7일 |
| 1/9 | 스토어 문구 · 스크린샷 · 아이콘 완성 (4-3 · 5-3) | 디자인 트랙 |
| 1/15 ~ | 연령 등급 설문 · Data safety · App Privacy 신고, Photon 100 CCU 구매 | |
| **~1/25** | 양쪽 스토어 심사 제출 | Apple 90%가 24시간 안, 구글 신규 계정은 최대 7일 이상. 게시 시점은 직접 정할 수 있다(구글 관리형 게시, Apple 수동 출시) |

---

## 2. 결정할 것

| # | 결정 | 선택지 · 영향 | 마감 |
| --- | --- | --- | --- |
| 1 | **계정 명의** — 개인 / 조직 | 개인: 본명이 판매자로 표시된다. 구글은 12명 × 14일 테스트가 의무다. 조직: Apple은 **법인만** 가능(상호로는 불가)하고 D-U-N-S가 필요하다. 구글 조직 계정은 테스트 의무가 없지만 D-U-N-S가 필요하다 | 10월 |
| 2 | **수익화** — 없음 / 인앱결제 / 광고 / 확률형 | 없음: 사업자등록 없이 무료 앱으로 출시할 수 있다. 인앱결제: Apple 유료 앱 계약 · 은행 · 미국 세금 양식(W-8BEN), 사업자등록 · 통신판매업 신고(구글 "업체")가 필요하고, 한국 개발자는 사업자 정보가 스토어에 공개된다. 확률형: 게임 안 확률 표시(법 2024-03-22 시행, 두 스토어 정책은 매출과 무관하게 의무). 광고: 광고 ID 신고, iOS 추적 동의(ATT) | 기능 12 전 |
| 3 | **로그인 방식** (플랜: 12/26 마감) | 게스트만 · 계정 없음: 계정 삭제 의무 없음. 계정을 만들면 **앱 안 삭제 기능**이 필수(Apple은 자동 생성된 게스트 계정도 해당)이고, 구글은 **웹 삭제 요청 링크**도 요구한다. 소셜 로그인을 넣으면 iOS에 **Sign in with Apple 같은 동등한 수단**을 같이 넣어야 하고(4.8), 한국 개발자는 서버 알림 엔드포인트가 필수다(2026-01-01~) | 12/26 |
| 4 | **심사자 혼자 플레이** | ① 봇 상대 / 연습 모드 — 가장 안전하지만 기능이 늘어난다. ② 심사 메모에 "기기 2대로 방 만들기 → 입장" 절차 + 시연 영상, 심사 기간 서버 유지 — 추가 개발 없음, 거절 위험이 남는다 | 12/26 |
| 5 | **iPad 지원** | 지금 설정은 iPhone + iPad다. 지원하면 **iPad 13" 스크린샷이 필수**이고 4:3 화면 배치도 봐야 한다. iPhone 전용이면 둘 다 없다 | 12/12 |
| 6 | **EU 배포** | Apple은 EU에서 팔려면 DSA 트레이더 신고가 필수이고, **주소 · 전화번호가 공개**된다(개인이면 집 주소). EU를 빼면 신고할 필요가 없다 | 1/15 |
| 7 | **Unity 6 LTS 업그레이드 시점** | ① 곧(10/21 점검 직후) — 코드를 새로 짓는 중이라 가장 싸다. 대신 기능 1~2 중간에 끼어든다. ② 출시 후 ~2027-03 — 출시 일정은 안전하지만 콘텐츠가 다 찬 뒤라 비싸다. 후보: 6000.0.68f1+ 또는 6000.3.8f1+. PUN2 · DOTween · Splines 호환 확인 필요. **추천 ①** — 구글도 해마다 target API를 올리는데(다음은 2027-08 예상), 2022 LTS는 공개 패치가 끝났다 | 10/21 |
| 8 | **Photon — PUN 유지 / Fusion 전환** | PUN 유지: 출시 때 $95. Fusion 2: 무료 100 CCU를 출시에 쓸 수 있다(계정당 게임 1개). 대신 네트워크 코드를 다시 짜야 한다. 추천은 PUN 유지 | 기능 1 전 |

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
| Target API | **2026-08-31부터 신규 앱 · 업데이트는 API 36** | 2022.3.62f1 이상이 지원 → OK. SDK Platform 36 · build-tools 36 설치 필요 |
| 16 KB 메모리 페이지 | API 35 이상 타깃 앱은 필수 | 2022.3.56f1 이상이 지원 → OK. Photon은 관리 코드(DLL)뿐이라 네이티브 `.so` 없음 |
| 64비트 | arm64-v8a 필수 | 출시 빌드에서 IL2CPP + ARM64 (7절) |
| 형식 · 서명 | AAB + Play 앱 서명. 개발자는 업로드 키로 서명 | 업로드 키를 잃어버리면 재설정을 요청할 수 있지만 번거롭다. 저장소 밖에 백업 |
| 용량 | 베이스 모듈 500 MB. 200 MB를 넘으면 모바일 데이터 사용자에게 경고가 뜬다 | 카드 게임이라 에셋 분할 배포는 필요 없을 것 |
| Android 16 대화면 | 태블릿(sw600dp 이상)에서 방향 고정이 무시된다. 단 manifest에 `android:appCategory="game"`이 있으면 예외 | 2022 LTS는 메뉴가 없어 **커스텀 manifest**로 넣어야 한다 |

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
- **콘텐츠 등급(IARC 설문)**: 한국 등급(전체 · 12 · 15)까지 자동으로 붙는다. 청소년이용불가만 게임물관리위원회 직접 심의.
- **타겟층**: 13세 미만을 포함하면 가족 정책이 적용된다. 귀여운 아트라도 **13세 이상만 선택**하고, 등록 자료가 아동용으로 보이지 않게 주의.
- 광고 포함 여부 · 앱 접근 권한(로그인이 필요하면 테스트 계정) · 건강 앱 신고("없음"으로 제출) · 광고 ID(광고 SDK를 넣을 때).

---

## 5. App Store

### 5-1. 기술 요건

| 항목 | 요건 | 우리 상태 |
| --- | --- | --- |
| Xcode / SDK | **2026-04-28부터 Xcode 26 · iOS 26 SDK 이상** | Unity Build Automation에 Xcode 26 이미지가 있다(Sequoia/Tahoe). 2022.3.62f3와의 공식 호환은 미확인 → 11월 시험 빌드 |
| 최저 iOS | 2026-09-09부터 iOS 13 이상. Xcode 26의 최저 배포 타깃은 iOS 15 | 출시 빌드에서 15.0 (7절) |
| 다음 SDK | iOS 27 SDK는 UIScene 필수. 의무화 날짜 미발표(2027-04 전후 추정) | 2022.3.62f3는 미지원 → 결정 7 |
| Privacy Manifest | Required-Reason API 사유 선언 필수 | 엔진 것은 2022.3.18f1 이상이 자동으로 넣는다. Photon은 관리 코드라 별도 manifest가 필요 없어 보인다(공식 입장 미확인) |
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
| 스크린샷 iPad 13" | iPad 지원 시 필수, 가로 2752 × 2064 (결정 5) |
| 앱 미리보기 영상 | 선택. 15~30초, 최대 3개 |
| URL | 지원 URL(연락처 포함) · 개인정보처리방침 URL 필수, 마케팅 URL 선택 |

### 5-4. 심사 · 신고

- **TestFlight**: 내부 100명(심사 없음) · 외부 최대 10,000명(첫 빌드는 베타 심사). 공개 링크 가능.
- **심사**: 90%가 24시간 안. 멀티플레이 전용 조항은 없지만 "상대가 없어 무한 대기 → 2.1 거절" 사례가 있다 → 결정 4. 심사 메모는 4,000바이트.
- **연령 등급**: 새 체계(4+/9+/13+/16+/18+) 설문 + **소셜 미디어 설문**(2026-09부터 필수. 피드로 UGC를 퍼뜨리는 기능이 대상이라 단순 멀티플레이 · 채팅은 아닐 가능성이 크다). 게임은 한국 등급(All/12/15/19)이 설문 답으로 자동으로 붙는다. 2026-10부터 가끔 나오는 욕설 · 저속한 유머는 All → 12+.
- **App Privacy 라벨**: 4-4 Data safety와 같은 내용으로 신고.
- **채팅을 넣으면(1.2)**: 필터링 · 신고 · 차단 기능과 연락처 공개가 필요하다.
- **한국 표시 정보**: 한국 거주 개발자는 이메일 등이 제품 페이지에 공개된다(사업자면 사업자등록번호도).
- **미국 텍사스**: 2026-06부터 연령 확인 의무(Declared Age Range API). 미국 배포 시 검토.

---

## 6. 공통 준비물

| 준비물 | 쓰는 곳 | 담당 |
| --- | --- | --- |
| 개인정보처리방침 웹페이지 (한국어 · 영어) | 양쪽 스토어 + 앱 안 링크 | 기획 |
| 지원 페이지 · 연락처 이메일 | 양쪽 스토어 | 기획 |
| 패키지 ID(Bundle ID) — 예: `com.<팀>.chapchu` | 양쪽. **출시 후 바꿀 수 없다** | 사용자 결정 |
| 스토어 문구 (4-3 · 5-3 글자 수) | 양쪽 | 기획 |
| 아이콘 512 · 1024, 피처 그래픽, 스크린샷 | 양쪽 | 디자인 |
| 등급 설문 답변 초안 (폭력성 · 사행성 · 채팅 등) | IARC · Apple | 기획 |
| 심사자용 플레이 안내 (결정 4) | Apple 심사 메모 · 구글 앱 접근 권한 | 기획 |

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
| Photon 지역 | `kr` 고정 검토 (현재 개발 지역 `hk`) |

---

## 8. 미확인

- Unity 2022.3.62f3와 Xcode 26의 공식 호환 여부 → 11월 시험 빌드로 확인
- iOS 27 SDK 의무화 날짜 (Apple 미발표)
- Photon의 privacy manifest · 수출 규정 공식 입장, Data safety 작성 예시
- 게임제작업 · 배급업 등록이 무료 인디 게임에 실제로 적용되는지 → 구청 문의
- 인앱결제가 통신판매업 신고의 "거래"에 해당하는지, 세금 처리 → 세무사
- 확률형 아이템 면제 기준(연 매출 1억 원 이하 중소기업)의 기준 기간
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
- 스크린샷 · 미리보기 · 문구: https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/ · https://developer.apple.com/help/app-store-connect/reference/app-information/app-preview-specifications/ · https://developer.apple.com/help/app-store-connect/reference/app-information/platform-version-information/
- TestFlight · 심사: https://developer.apple.com/testflight/ · https://developer.apple.com/distribute/app-review/
- EU DSA: https://developer.apple.com/help/app-store-connect/manage-compliance-information/manage-european-union-digital-services-act-trader-requirements/
- 수출 규정: https://developer.apple.com/documentation/bundleresources/information-property-list/itsappusesnonexemptencryption
- 계약 · 세금 · 한국 표시: https://developer.apple.com/help/app-store-connect/manage-agreements/sign-and-update-agreements/ · https://developer.apple.com/help/app-store-connect/manage-tax-information/provide-tax-information/ · https://developer.apple.com/help/app-store-connect/manage-compliance-information/manage-korea-compliance-information
- 텍사스 연령 확인: https://developer.apple.com/news/?id=sg176nne

**공통 · 한국**
- Photon 요금: https://www.photonengine.com/pun/pricing · https://forum.photonengine.com/discussion/16907/can-i-publish-a-game-that-uses-the-20-ccu-free-license-model · https://blog.photonengine.com/new-free-100-ccu-for-photon-fusion-and-quantum-games/
- Photon 개인정보: https://www.photonengine.com/terms/gdpr
- 확률형 아이템: https://www.korea.kr/news/policyNewsView.do?newsId=148924297 · https://www.etnews.com/20250806000278
- 게임제작업 등록: https://www.gov.kr/mw/AA020InfoCappView.do?HighCtgCD=A09006&CappBizCD=13700000177
- 폰트(학교안심 둥근미소, OFL — 상업 이용 가능): https://copyright.keris.or.kr/wft/fntDwnldView?fntGrpId=GFT202408200000000000003

---

## 변경 이력

| 날짜 | 내용 |
| --- | --- |
| 2026-09-26 | 신규. 두 스토어 요건 조사, 일정 역산, 결정 8개 |
