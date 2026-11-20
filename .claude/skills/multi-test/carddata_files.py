"""run.sh carddata 의 시험 파일 — cards.json 에서 세 벌을 만들어 폴더에 쓰고 확인값을 한 줄씩 찍는다.

    ok.json       1001 피해 20 → 25 · 1006 장수 +2
    unknown.json  빌드에 없는 카드(9999)가 있다
    kind.json     1001 의 효과 종류가 빌드와 다르다

확인값은 CardSheet.HashOf 와 같다 (줄바꿈 LF · BOM 없음으로 쓴 내용의 SHA-256).
"""
import copy
import hashlib
import json
import os
import sys

src, out = sys.argv[1], sys.argv[2]
base = json.loads(open(src, encoding="utf-8-sig").read())


def card(data, card_id):
    return next(c for c in data["cards"] if c["id"] == card_id)


def write(name, data):
    text = json.dumps(data, ensure_ascii=False, indent=2)
    open(os.path.join(out, name), "w", encoding="utf-8", newline="").write(text)
    print(hashlib.sha256(text.encode("utf-8")).hexdigest())


ok = copy.deepcopy(base)
hit = card(ok, 1001)["effects"][0]
assert "amount=20" in hit["args"], "1001 의 피해가 20 이 아니다 — 시험 기대값을 고친다"
hit["args"] = hit["args"].replace("amount=20", "amount=25")
card(ok, 1006)["count"] += 2
write("ok.json", ok)

unknown = copy.deepcopy(base)
extra = copy.deepcopy(card(unknown, 1001))
extra["id"] = 9999
unknown["cards"].append(extra)
write("unknown.json", unknown)

kind = copy.deepcopy(base)
card(kind, 1001)["effects"][0]["type"] = "HealEffect"
write("kind.json", kind)
