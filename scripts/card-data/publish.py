"""카드 데이터 내보내기 — cards.json 을 서버(UGS CCD)에 올릴 모양으로 만들고 Remote Config 에 넣을 값을 찍는다.

기준: docs/systems/26-card-data.md 7-2 · 7-3 · 7-5. 표준 라이브러리만 쓴다.

    python publish.py --repo <저장소 루트> --check               # 파일 검사 + 빌드 기본본(Deck_Main)이 이 파일로 만든 것인지
    python publish.py --repo <저장소 루트> --out dist            # dist/cards.json (올릴 파일) + 확인값
    python publish.py --repo <저장소 루트> --out dist --url <릴리스 주소> --version 12   # Remote Config cardData 값까지

확인값은 Unity 의 CardSheet.HashOf 와 같다 — 줄바꿈(CRLF)과 BOM 을 빼고 센 SHA-256.
"""
import argparse
import hashlib
import json
import os
import re
import sys

CARDS_JSON = "MuffinProject/Assets/Cards/Main/cards.json"
DECK_ASSET = "MuffinProject/Assets/Cards/Main/Deck_Main.asset"
MAX_BYTES = 1024 * 1024   # 이보다 크면 올리지 않는다 (26 4절 — 앱은 2MB 에서 끊는다)
SLOT = re.compile(r"\{(\d+)\}")
NUMBER = r"\d+(?:\.\d+)?"


def normalize(text):
    return text.lstrip("﻿").replace("\r", "")


def effect_values(card):
    out = []
    for fx in card.get("effects") or []:
        for pair in (fx.get("args") or "").split(","):
            if "=" in pair:
                v = pair.split("=", 1)[1].strip()
                if re.fullmatch(NUMBER, v):
                    out.append(v)
    return out


def check(text):
    """앱이 받자마자 버릴 파일을 올리기 전에 잡는다. 문제 목록을 돌려준다."""
    problems = []
    cards = json.loads(text)["cards"]
    ids = [c["id"] for c in cards]
    for dup in sorted({i for i in ids if ids.count(i) > 1}):
        problems.append(f"카드 {dup} 가 두 번 나온다")
    if sum(c["count"] for c in cards) <= 0:
        problems.append("덱이 0장이다")
    for c in cards:
        if c["count"] < 0:
            problems.append(f"카드 {c['id']}: 장수 {c['count']}")
        if not c.get("name"):
            problems.append(f"카드 {c['id']}: 이름이 비었다")
        values = effect_values(c)
        for slot in SLOT.findall(c.get("text") or ""):
            if not slot.isascii():   # 전각 숫자 등 — 앱은 읽지 못해 파일을 버린다
                problems.append(f"카드 {c['id']}: 문구의 값 자리 {{{slot}}} — ASCII 숫자가 아니다")
            elif int(slot) >= len(values):
                problems.append(f"카드 {c['id']}: 문구의 값 자리 {{{slot}}} — 효과 값은 {len(values)}개")
    if len(text.encode("utf-8")) > MAX_BYTES:
        problems.append(f"파일이 {MAX_BYTES // 1024}KB 를 넘는다")
    return problems


def deck_hash(repo):
    for line in open(os.path.join(repo, DECK_ASSET), encoding="utf-8"):
        if line.strip().startswith("dataHash:"):
            return line.split(":", 1)[1].strip()
    return ""


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    p = argparse.ArgumentParser()
    p.add_argument("--repo", default=".")
    p.add_argument("--check", action="store_true", help="빌드 기본본(Deck_Main)의 확인값과도 견준다")
    p.add_argument("--out", help="올릴 파일을 쓸 폴더")
    p.add_argument("--url", help="올린 뒤의 릴리스 주소 — Remote Config 값을 찍는다")
    p.add_argument("--version", type=int, default=0, help="사람이 부르는 번호 (패치 노트의 「카드 12」) — 앱은 로그에만 쓴다")
    args = p.parse_args()

    text = normalize(open(os.path.join(args.repo, CARDS_JSON), encoding="utf-8", newline="").read())
    digest = hashlib.sha256(text.encode("utf-8")).hexdigest()
    problems = check(text)
    if args.check and deck_hash(args.repo) != digest:
        problems.append(f"Deck_Main 의 확인값이 cards.json 과 다르다 — Unity 에서 「Chapchu/카드 가져오기」 를 돌리고 같이 커밋한다 (Deck_Main {deck_hash(args.repo) or '(비어 있음)'})")
    for line in problems:
        print("✗", line)
    if problems:
        return 1

    print("확인값", digest)
    if args.out:
        os.makedirs(args.out, exist_ok=True)
        path = os.path.join(args.out, "cards.json")
        open(path, "w", encoding="utf-8", newline="").write(text)
        print("올릴 파일", path, f"({len(text.encode('utf-8'))} 바이트)")
    if args.url:
        print("Remote Config cardData =", json.dumps({"version": args.version, "hash": digest, "url": args.url}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
