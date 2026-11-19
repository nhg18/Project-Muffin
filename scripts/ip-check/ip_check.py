"""카드 IP 점검의 정답이 있는 부분 — 상표 판정 · 쓰기 전 검증 · 쓴 뒤 확인.

클라우드 루틴(「카드 IP 점검」)의 AI 가 노션을 읽고 쓰고, 그림 · 욕설은 AI 가 판단한다.
상표(6-1 1번) 판정과 「무엇을 바꿔도 되는지」 는 이 스크립트가 정한다. AI 는 결과를 그대로 쓴다.
표준 라이브러리만 쓴다. 하는 일과 파일 모양은 README.md.

    python ip_check.py names  --cards cards.json [--max-calls 30] > names.json
    python ip_check.py validate --cards cards.json --names names.json --plan plan.json [--max-changes 20]
    python ip_check.py verify --plan plan.json --actual actual.json

환경 변수 KIPRIS_KEY 가 필요하다 (names). 키 값은 어디에도 출력하지 않는다.
종료 코드: 0 = 통과, 1 = 검증 실패 (쓰지 않는다), 2 = 카나리아 실패 · KIPRIS 접속 불가 (쓰지 않는다).
"""
import argparse
import json
import os
import re
import sys
import urllib.parse
import urllib.request

API = "https://plus.kipris.or.kr/kipo-api/kipi/trademarkInfoSearchService/getAdvancedSearch"
PAGE = 500
MAX_PAGES = 10           # 한 류에 5,000건 넘게 나오면 기계 판정을 포기한다
CLASSES = ("09", "41")   # 게임 소프트웨어 · 게임 서비스 (store-release 6-1 1번)

# 고급 검색은 아래 값을 하나라도 빼면 INVALID_REQUEST_PARAMETER_ERROR 다 (2026-11-19 확인).
# 상태는 「등록」 만, 상표 종류 · 모양은 전부.
STATUS = {"application": "false", "registration": "true", "refused": "false", "expiration": "false",
          "withdrawal": "false", "publication": "false", "cancel": "false", "abandonment": "false"}
KINDS = {k: "true" for k in (
    "trademark serviceMark trademarkServiceMark businessEmblem collectiveMark geoOrgMark certMark "
    "geoCertMark internationalMark character figure compositionCharacter figureComposition fragrance "
    "sound color colorMixed dimension hologram invisible motion visual").split()}

# 정답을 아는 이름 — 매 실행 첫머리에 조회해서 결과가 다르면 아무것도 쓰지 않는다 (2026-11-18 확인)
CANARY = {
    "메이플스토리": "수정 필요",   # 09 · 41류 등록 상표 있음
    "무야호": "통과",              # 등록 상표는 있지만 16 · 30 · 32 · 35류
}

FIX, PASS, UNCHECKED, SKIPPED = "수정 필요", "통과", "미점검", "넘김"
NO_ART, ART_WAIT = "없음", "그림 대기"
NAME_RESULTS = {FIX, PASS, UNCHECKED}
ART_RESULTS = {FIX, PASS, NO_ART, UNCHECKED}
IP_VALUES = {FIX, PASS, ART_WAIT, UNCHECKED}


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def norm(s):
    """공백 · 문장부호 무시, 영문 소문자로."""
    return re.sub(r"[\W_]", "", s or "").lower()


def merge(name, art):
    """판정 합치기 표 (루틴 프롬프트 · README 와 같다)."""
    if FIX in (name, art):
        return FIX
    if name == PASS and art == PASS:
        return PASS
    if name == PASS and art == NO_ART:
        return ART_WAIT
    return UNCHECKED


# ── names: KIPRIS 상표 판정 ───────────────────────────────────────────

class Kipris:
    def __init__(self, key):
        self.key = key
        self.calls = 0

    def page(self, word, cls, page_no):
        self.calls += 1
        qs = urllib.parse.urlencode(dict(STATUS, **KINDS, trademarkName=word, classification=cls,
                                         numOfRows=PAGE, pageNo=page_no, ServiceKey=self.key))
        try:
            with urllib.request.urlopen(API + "?" + qs, timeout=30) as r:
                xml = r.read().decode("utf-8")
        except Exception as e:   # 메시지에 키가 섞이지 않게 지운다
            raise RuntimeError(str(e).replace(self.key, "***")) from None
        code = re.search(r"<resultCode>([^<]*)", xml)
        if not code or code.group(1) != "00":
            msg = re.search(r"<resultMsg>([^<]*)", xml)
            raise RuntimeError("KIPRIS resultCode %s %s" % (code and code.group(1), msg and msg.group(1)))
        total = int((re.search(r"<totalCount>(\d+)", xml) or [0, 0])[1])
        items = []
        for it in re.findall(r"<item>(.*?)</item>", xml, re.S):
            get = lambda tag: (re.findall(r"<%s>([^<]*)" % tag, it) or [""])[0]
            items.append({"title": get("title"), "status": get("applicationStatus"),
                          "classes": get("classificationCode"), "reg": get("registrationNumber")})
        return total, items

    def check(self, name, budget):
        """1번 판정. budget = 이번 실행 전체 호출 상한 (None = 무제한).

        상표명 · 류(09, 41 따로) · 등록 상태로 KIPRIS 가 미리 거른다 — 이름당 최소 2회.
        상표명 검색은 「포함」 이라 결과를 다 보고 이름이 같은 것만 고른다. 문장부호는 검색어에서
        뺀다 (「진짜 할거야?」 는 그대로 보내면 요청 오류). MAX_PAGES 를 넘는 류는 미점검.
        """
        word = re.sub(r"[^\w\s]", " ", name).strip()
        if not word:
            return {"result": UNCHECKED, "hits": [], "why": "검색할 글자가 없음"}
        if budget is not None and self.calls + len(CLASSES) > budget:
            return {"result": SKIPPED, "hits": [], "why": "호출 상한"}
        hits, total = [], 0
        for cls in CLASSES:
            n, items = self.page(word, cls, 1)
            total += n
            pages = -(-n // PAGE)
            if pages > MAX_PAGES:
                return {"result": UNCHECKED, "hits": [], "total": total,
                        "why": "%s류 검색 결과 %d건 — KIPRIS 에서 사람이 확인" % (cls, n)}
            for page_no in range(2, pages + 1):
                if budget is not None and self.calls >= budget:
                    return {"result": SKIPPED, "hits": [], "total": total, "why": "호출 상한"}
                items += self.page(word, cls, page_no)[1]
            for it in items:
                classes = {c.strip().zfill(2) for c in it["classes"].split("|")}
                if norm(it["title"]) == norm(name) and it["status"] == "등록" and cls in classes \
                        and it["reg"] not in {h["reg"] for h in hits}:
                    hits.append(it)
        return {"result": FIX if hits else PASS, "hits": hits, "total": total}


def cmd_names(a):
    key = os.environ.get("KIPRIS_KEY", "")
    if not key:
        print(json.dumps({"ok": False, "why": "KIPRIS_KEY 없음"}, ensure_ascii=False))
        return 2
    k = Kipris(key)
    canary = {}
    try:
        for word, want in CANARY.items():
            got = k.check(word, None)
            canary[word] = {"want": want, "got": got["result"], "hits": len(got["hits"]), "total": got.get("total")}
    except RuntimeError as e:
        print(json.dumps({"ok": False, "why": "KIPRIS 접속 실패: %s" % e, "canary": canary}, ensure_ascii=False))
        return 2
    if any(c["want"] != c["got"] for c in canary.values()):
        print(json.dumps({"ok": False, "why": "카나리아 결과가 다름", "canary": canary}, ensure_ascii=False))
        return 2

    k.calls = 0   # 카나리아는 상한에 넣지 않는다
    results, errors = {}, 0
    for name in dict.fromkeys(c["Name"] for c in load(a.cards)):   # 같은 이름은 한 번만
        try:
            results[name] = k.check(name, a.max_calls)
        except RuntimeError as e:
            errors += 1
            results[name] = {"result": UNCHECKED, "hits": [], "why": str(e)}
    print(json.dumps({"ok": True, "canary": canary, "calls": k.calls, "errors": errors,
                      "results": results}, ensure_ascii=False, indent=1))
    return 0


# ── validate: 쓰기 전 검증 ────────────────────────────────────────────

def cmd_validate(a):
    cards = {c["url"]: c for c in load(a.cards)}
    names = load(a.names)
    plan = load(a.plan)
    errs = []
    if not names.get("ok"):
        errs.append("names.json 이 실패 상태 — 아무것도 쓰지 않는다")
    trademark = names.get("results", {})
    key = os.environ.get("KIPRIS_KEY", "")
    if key and key in json.dumps(plan, ensure_ascii=False):
        errs.append("계획에 KIPRIS 키 값이 들어 있음")

    seen = set()
    for i, p in enumerate(plan):
        where = "%d번째 (%s)" % (i + 1, p.get("url"))
        card = cards.get(p.get("url"))
        if card is None:
            errs.append(where + ": 대상 카드 목록에 없는 카드")
            continue
        if p["url"] in seen:
            errs.append(where + ": 같은 카드가 두 번")
        seen.add(p["url"])
        name, art, after = p.get("name"), p.get("art"), p.get("after")
        if name not in NAME_RESULTS:
            errs.append(where + ": 이름 결과 「%s」 는 허용 값이 아님" % name)
        if art not in ART_RESULTS:
            errs.append(where + ": 그림 결과 「%s」 는 허용 값이 아님" % art)
        if after not in IP_VALUES:
            errs.append(where + ": 「IP 점검」 값 「%s」 는 허용 값이 아님" % after)
        elif name in NAME_RESULTS and art in ART_RESULTS and after != merge(name, art):
            errs.append(where + ": 합치기 표로는 %s + %s = %s 인데 %s" % (name, art, merge(name, art), after))
        if after == card.get("IP 점검"):
            errs.append(where + ": 지금 값과 같음 — 쓰지 않는다")
        tm = trademark.get(card["Name"])
        if tm is None:
            errs.append(where + ": 상표 판정 결과에 없는 이름 「%s」" % card["Name"])
        elif tm["result"] == SKIPPED:
            errs.append(where + ": 호출 상한으로 넘긴 카드는 이번에 쓰지 않는다")
        elif tm["result"] == FIX and name != FIX:
            errs.append(where + ": 상표가 수정 필요인데 이름 결과가 %s" % name)
        elif tm["result"] == UNCHECKED and name == PASS:
            errs.append(where + ": 상표가 미점검인데 이름 결과가 통과")

    downgrades = [p for p in plan if p.get("after") == UNCHECKED
                  and cards.get(p.get("url"), {}).get("IP 점검") in (PASS, ART_WAIT)]
    if len(plan) > a.max_changes:
        errs.append("한 번에 %d장을 바꾸려 함 (상한 %d)" % (len(plan), a.max_changes))
    if len(downgrades) > a.max_downgrades:
        errs.append("통과 · 그림 대기 → 미점검 이 %d장 (상한 %d) — 무언가 고장 났을 수 있음" % (len(downgrades), a.max_downgrades))

    print(json.dumps({"ok": not errs, "changes": len(plan), "errors": errs}, ensure_ascii=False, indent=1))
    return 0 if not errs else 1


# ── verify: 쓴 뒤 확인 ────────────────────────────────────────────────

def cmd_verify(a):
    actual = {c["url"]: c.get("IP 점검") for c in load(a.actual)}
    errs = ["%s: 계획 %s, 실제 %s" % (p["url"], p["after"], actual.get(p["url"]))
            for p in load(a.plan) if actual.get(p["url"]) != p["after"]]
    print(json.dumps({"ok": not errs, "errors": errs}, ensure_ascii=False, indent=1))
    return 0 if not errs else 1


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("names", help="카나리아 + 카드 이름 상표 판정")
    s.add_argument("--cards", required=True)
    s.add_argument("--max-calls", type=int, default=30)
    s = sub.add_parser("validate", help="노션에 쓰기 전 계획 검증")
    s.add_argument("--cards", required=True)
    s.add_argument("--names", required=True)
    s.add_argument("--plan", required=True)
    s.add_argument("--max-changes", type=int, default=20)
    s.add_argument("--max-downgrades", type=int, default=5)
    s = sub.add_parser("verify", help="쓴 뒤 노션 값과 계획 비교")
    s.add_argument("--plan", required=True)
    s.add_argument("--actual", required=True)
    a = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    return {"names": cmd_names, "validate": cmd_validate, "verify": cmd_verify}[a.cmd](a)


if __name__ == "__main__":
    sys.exit(main())
