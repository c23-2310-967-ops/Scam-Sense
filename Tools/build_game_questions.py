"""Docs/Curriculum_Question_Bank.md (the validated question set) -> Assets/Editor/CurriculumQuestions.json.

The JSON is read by Assets/Editor/CurriculumQuestionsImporter.cs, which writes the questions into the five room
assets. Question wording is converted, not retyped: markdown bold/italics become TMP tags, <br> becomes a new line.
The seven Scam-or-Legitimate items are split into sender / subject / body / link so they look like real messages;
their wording is kept, only re-arranged into those fields.
"""
import html, json, re, sys

BANK, OUT = sys.argv[1], sys.argv[2]
md = open(BANK, encoding="utf-8").read()

def rows_of(block):
    rows = {}
    for line in block.splitlines():
        if line.startswith("| ") and not line.startswith("|---") and not line.startswith("| Category"):
            k, v = line[2:-2].split(" | ", 1)
            rows[k.strip()] = v.strip()
    return rows

qs = []
for m in re.finditer(r"^#### ([EMH]\d\d)\n(.*?)(?=^#### |^### |^## |^---)", md, re.S | re.M):
    r = rows_of(m.group(2)); r["_id"] = m.group(1); qs.append(r)
assert [q["_id"] for q in qs] == [f"{p}{i:02d}" for p in "EMH" for i in range(1, 11)], "bank must hold E01-H10 in order"

def tmp(s):
    """Markdown cell text -> TextMeshPro rich text."""
    s = html.unescape(s)
    s = s.replace("✨", "")                      # sparkles: not in the game font (shows as a box)
    s = re.sub(r"<([^<>\s]+@[^<>\s]+)>", r"<noparse><</noparse>\1<noparse>></noparse>", s)   # <name@x.example>
    s = s.replace("<br>", "\n")
    s = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", s)
    s = re.sub(r"(?<![*\w])\*([^*\s][^*]*?)\*(?![*\w])", r"<i>\1</i>", s)
    s = s.replace("`", "")
    s = re.sub(r"[ \t]{2,}", " ", s).replace("\" ", "\" ").strip()
    s = s.replace("\"  ", "\" ")
    return s

def plain(s):
    return re.sub(r"<[^>]+>", "", tmp(s))

def unquote(s):
    s = s.strip()
    return s[1:-1] if len(s) >= 2 and s[0] == '"' and s[-1] == '"' else s

# ---- the seven Scam-or-Legitimate items as messages (wording from the bank, arranged into message fields)
VERDICT_ITEMS = {
    "E02": dict(channel="SMS", sender="+63 917 555 0142", senderDetail="", subject="",
                body="SwiftParcel: Your package is ON HOLD due to an incomplete address. Pay the ₱29 redelivery fee within 12 hours or it will be returned:",
                link="http://swiftparcel-redeliver.example/pay",
                context="You have not ordered anything this month."),
    "E06": dict(channel="Website", sender="Viral post", senderDetail="", subject="FIND YOUR STAR NAME",
                body="Comment your first pet's name + the street you grew up on + your birth month!",
                link="", context="Is this harmless fun or a scam? LEGITIMATE = harmless fun, SCAM = data harvesting."),
    "E09": dict(channel="Website", sender="Unknown web page", senderDetail="", subject="SYSTEM SECURITY ALERT",
                body="Your computer is INFECTED and LOCKED. Call Certified Support at 1-800-555-0199 now. DO NOT restart your computer.",
                link="", context="While browsing, a full-screen red page appeared and started beeping."),
    "M01": dict(channel="Email", sender="NovaCloud", senderDetail="no-reply@novacloud.example", subject="Password reset request",
                body="We received a request to reset your password. This link expires in 15 minutes. If you didn't request this, you can ignore this email.",
                link="https://novacloud.example/reset?t=8f3k…",
                context="One minute before this email arrived, you clicked \"Forgot password\" on NovaCloud yourself. Trusted domain (case files): novacloud.example"),
    "M08": dict(channel="SMS", sender="ExampleBank", senderDetail="In the same thread as your real ExampleBank messages", subject="",
                body="ExampleBank: Your account will be suspended TODAY. Update your details at",
                link="examplebank-ph.example/update",
                context="Trusted domain (case files): examplebank.example"),
    "H07": dict(channel="Website", sender="Scholarship Grant 2027 form", senderDetail="",
                subject="Scholarship Grant 2027",
                body="Please provide: full name, student number, birthdate, mother's maiden name, front and back of your ID, a selfie holding your ID, and your PayLite MPIN \"for fast release of funds\".\n\nThere is no privacy notice. The only contact is a mobile number.",
                link="", context="A \"Scholarship Grant 2027\" form is shared in a student group. It uses the school logo."),
    "H09": dict(channel="Website", sender="Page opened by the canteen QR code", senderDetail="", subject="Scan to pay with PayLite",
                body="Enter your mobile number and MPIN to pay.",
                link="paylite-merchant-pay.example/canteen",
                context="The canteen's \"Scan to pay with PayLite\" poster looks normal, but up close the QR code is a sticker placed on top of the printed one."),
}

ROOMS = [
    ("Assets/Levels/Level1_TheFirstCase.asset", "Welcome to your first case."),
    ("Assets/Levels/Level2_TheInvestigation.asset", "Your investigation continues."),
    ("Assets/Levels/Level3_TheDigitalTrap.asset", "The traps are getting more convincing."),
    ("Assets/Levels/Level4_DigitalForensicsLab.asset", "Welcome to the Digital Forensics Lab."),
    ("Assets/Levels/Level5_MacLab.asset", "The campus network has been compromised."),
]

out = {"source": "Docs/Curriculum_Question_Bank.md", "rooms": []}
for r_i, (asset, intro) in enumerate(ROOMS):
    room = {"room": r_i + 1, "levelAsset": asset, "intro": intro, "caseNotes": [], "lessons": [], "questions": []}
    for q in qs[r_i * 6:(r_i + 1) * 6]:
        qid = q["_id"]
        item = {
            "id": qid, "number": qs.index(q) + 1, "questionType": q["Question Type"], "difficulty": q["Difficulty"],
            "curriculumRef": plain(q["Course Subject"]) + " — " + plain(q["Related Lesson/Topic"]),
            "explanation": tmp(q["Explanation"]), "feedback": tmp(unquote(q["Game Feedback"])),
            "learningObjective": plain(q["Learning Objective"]),
            "choices": [], "correctChoice": 0, "verdict": "Scam",
            "channel": "Question", "sender": "Case Review", "senderDetail": "", "subject": q["Question Type"],
            "body": tmp(q["Question/Scenario"]), "link": "", "scenarioContext": "",
        }
        correct = plain(q["Correct Answer"]).strip()
        if qid in VERDICT_ITEMS:
            v = VERDICT_ITEMS[qid]
            for f in ("sender", "senderDetail", "subject", "body", "link"):
                assert "<" not in v[f], (qid, f, "messages are shown as plain text - no tags")
            item.update(channel=v["channel"], sender=v["sender"], senderDetail=v["senderDetail"], subject=v["subject"],
                        body=v["body"], link=v["link"], scenarioContext=v["context"])
            item["verdict"] = "Legitimate" if correct.lower().startswith("legitimate") else "Scam"
            assert correct.lower().startswith(("scam", "legitimate")), (qid, correct)
            if not v["context"].startswith("Is this"):
                room["caseNotes"].append(v["context"])
        else:
            parts = [c.strip() for c in q["Choices"].split("<br>")]
            assert len(parts) == 4 and [p[:2] for p in parts] == ["A.", "B.", "C.", "D."], (qid, parts)
            item["choices"] = [tmp(p[2:].strip()) for p in parts]
            letter = correct[0]
            assert letter in "ABCD" and (len(correct) == 1 or correct[1] == "."), (qid, correct)
            item["correctChoice"] = "ABCD".index(letter)
            # when the bank also gives the answer text ("C. Confidentiality"), it must match that choice
            if len(correct) > 2:
                assert plain(parts[item["correctChoice"]][2:]).strip().startswith(correct[3:].strip()[:20]), (qid, correct)
        room["lessons"].append(item["learningObjective"])
        room["questions"].append(item)
    assert len(room["questions"]) == 6
    out["rooms"].append(room)

ids = [q["id"] for r in out["rooms"] for q in r["questions"]]
assert len(ids) == 30 and len(set(ids)) == 30
json.dump(out, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("rooms", len(out["rooms"]), "questions", len(ids),
      "choice questions", sum(1 for r in out["rooms"] for q in r["questions"] if q["choices"]))
