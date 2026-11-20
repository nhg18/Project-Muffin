# 카드 데이터 올리기

`cards.json` 을 서버(UGS CCD)에 올려 앱 업데이트 없이 카드의 이름 · 문구 · 수치 · 장수를 바꾼다. 규칙은 [`docs/systems/26-card-data.md`](../../docs/systems/26-card-data.md).
`publish.py` 는 올릴 파일 · 확인값 · Remote Config 값을 만든다. **CCD · Remote Config 는 대시보드에서 손으로 한다** (자동 올리기는 아직 없다 — `26` 9절 #3).

## 올리기 전

1. 노션 카드 DB 를 고친다 → 카드 정리 봇의 PR (`bot/card-sync`).
2. PR 브랜치에서 Unity 메뉴 `Chapchu/카드 가져오기` → 콘솔의 「바뀜 전 / 후」 를 읽고, 바뀐 `Card_*.asset` · `Deck_Main.asset` 을 같이 커밋한다.
3. `python scripts/card-data/publish.py --check` — 파일 검사 + `Deck_Main` 의 확인값이 파일과 같은지. 통과하면 머지.

## 올리기

```bash
python scripts/card-data/publish.py --out dist          # dist/cards.json + 확인값
```

1. Unity 대시보드 → Cloud Content Delivery → 버킷 「카드 데이터 · 개발」 → `dist/cards.json` 을 올린다 (엔트리 경로 `cards.json`) → **릴리스를 만든다**.
2. 그 릴리스의 주소를 만든다 — 배지 주소가 아니라 **릴리스 주소**다 (`26` 7-4):
   `https://<프로젝트 ID>.client-api.unity3dusercontent.com/client_api/v1/buckets/<버킷 ID>/releases/<릴리스 ID>/entry_by_path/content/?path=cards.json`
3. Remote Config 값을 찍는다:

   ```bash
   python scripts/card-data/publish.py --out dist --url "<릴리스 주소>" --version 12
   # Remote Config cardData = {"version": 12, "hash": "…", "url": "…"}
   ```

4. 대시보드 → Remote Config → **개발 환경** → 키 `cardData` (형 JSON) 에 그 값을 붙인다 → 개발 빌드를 켜서 콘솔 「카드 데이터 받음 — 12번 …」 과 카드 문구를 확인한다.
5. 운영 버킷 · 운영 환경에 같은 순서로 한다. 유저는 다음 앱 실행 · 다음 「방에 가기」 때 받는다.

## 되돌리기 · 끄기

* **되돌리기** — Remote Config `cardData` 를 옛 값으로 되돌린다. 그것으로 끝난다 (옛 릴리스는 CCD 에 그대로 있다).
* **끄기** — `cardData` 를 `{}` 로 비운다. 모든 앱이 빌드 기본본으로 논다.

## 앱이 버리는 파일

아래에 걸리면 앱은 그 파일을 **통째로** 쓰지 않고 쓰던 데이터로 논다 (`26` 8절). `--check` 가 올리기 전에 잡는 것은 ✓.

| 무엇 | `--check` |
| --- | --- |
| JSON 이 아니다 · 같은 번호가 두 번 · 장수가 음수 · 덱이 0장 · 이름이 비었다 | ✓ |
| 문구의 값 자리(`{1}`)가 효과 값 개수보다 많다 | ✓ |
| 1MB 를 넘는다 (앱은 2MB 에서 끊는다) | ✓ |
| `Deck_Main` 이 이 파일로 만든 것이 아니다 (「카드 가져오기」 를 안 돌렸다) | ✓ |
| 빌드에 없는 카드 번호 · 카드 종류가 다르다 · 효과의 종류 · 개수 · 순서가 다르다 · 효과에 없는 값 이름 | ✗ — Unity 「카드 가져오기」 가 「실패」 로 알린다 |

## 로컬에서 시험

실제 서버 없이 받기 · 저장본 · 거절 · 방 가르기를 본다: `bash .claude/skills/multi-test/run.sh carddata` (skill `multi-test`).
