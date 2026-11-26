"""Docs/Curriculum_Question_Bank.md (the validated question set) -> Assets/Editor/CurriculumQuestions.json.

NOTE (2026-11-25): after running this script, run Tools > Scam Sense > Curriculum > Arrange Rooms in Unity
(Assets/Editor/CurriculumRoomLayout.cs). It adds the HINT for every non-tutorial question, the M04 English line,
each room's "In this room:" intro and the scoring curve, none of which this script writes.

The JSON is read by Assets/Editor/CurriculumQuestionsImporter.cs, which writes the questions into the five room
assets. Question wording is converted, not retyped: markdown bold/italics become TMP tags, <br> becomes a new line.
The seven Scam-or-Legitimate items are split into sender / subject / body / link so they look like real messages;
their wording is kept, only re-arranged into those fields.
"""
import html, json, os, re, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from activity_content import ACTIVITIES, VERDICT_CLUES, PLAIN   # how questions are played (not what they ask)

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

# ---- Level 1 tutorial: underlined clue details, hints, guided first question and an intro briefing.
# Only room 1 gets these. Match phrases must appear exactly in the question's field text (checked below).
def clue(id, field, match, label, finding, clue_id="", link=""):
    return {"id": id, "field": field, "match": match, "label": label, "finding": finding, "linkAddress": link, "clueId": clue_id}

def ev(id, title, why, key=True):
    return {"id": id, "title": title, "whyItMatters": why, "supportsVerdict": key}

TUTORIAL = {
    "E01": dict(
        hint="Think about what happened to Ana's information: was it changed, locked away, or seen by someone who shouldn't see it?",
        inspectables=[
            clue("page", "Body", 'fake "school portal" page', "The page",
                 "It only looks like the school portal. Whatever Ana typed went straight to the scammer.", "fake"),
            clue("access", "Body", "read her grades, messages and personal details", "What the scammer can do",
                 "The scammer can now look at Ana's private information. Was anything changed or locked - or only seen?", "seen"),
        ],
        evidence=[ev("fake", "Fake login page", "Fake pages copy real ones to collect what you type."),
                  ev("seen", "Private details exposed", "Someone who isn't allowed can now read Ana's information.")]),
    "E02": dict(
        hint="Compare the text with your case notes: were you expecting a package? Then check who sent it and where the link goes.",
        inspectables=[
            clue("sender", "Sender", "", "Sender",
                 "An ordinary mobile number, not a named courier. Real delivery companies text from their own name or a short code.", "number"),
            clue("claim", "Body", "Your package is ON HOLD", "The claim",
                 "Your case notes say you haven't ordered anything this month - so which package?", "noorder"),
            clue("fee", "Body", "Pay the ₱29 redelivery fee", "The request",
                 "A small fee is bait: the real goal is your card or e-wallet details.", "fee"),
            clue("deadline", "Body", "within 12 hours", "Deadline",
                 "A short deadline pushes you to act before you think.", "urgent"),
            clue("link", "Link", "", "Link",
                 "swiftparcel-redeliver.example is not an official courier site, and it starts with http (not secure).", "link",
                 "http://swiftparcel-redeliver.example/pay"),
        ],
        evidence=[ev("number", "Ordinary mobile number", "Couriers don't text from random mobile numbers."),
                  ev("noorder", "No order to deliver", "You haven't ordered anything, so there is no real package."),
                  ev("fee", "Asks for a payment", "Small fees are used to steal card and e-wallet details."),
                  ev("urgent", "Pressure to act fast", "Scams rush you so you don't stop to check."),
                  ev("link", "Unofficial, insecure link", "The link isn't the courier's site and isn't secure.")]),
    "E03": dict(
        hint="Check the sender's address letter by letter, then ask yourself what the email wants you to do.",
        inspectables=[
            clue("address", "Body", "helpdesk@uphs1-binan.example", "Sender address",
                 "Look closely: uphs1 uses the number 1 instead of the letter l. It's a lookalike of the school's real domain.", "lookalike"),
            clue("subject", "Body", "Mailbox 98% full", "Subject",
                 "Full mailboxes really happen. On its own this isn't a warning sign - look at what the email asks you to do."),
            clue("deadline", "Body", "DELETED in 24 hours", "Threat",
                 "A scary deadline pushes you to act fast without checking.", "urgent"),
            clue("request", "Body", "sign in to verify your account", "What it asks",
                 "It wants your password through a link. Real IT staff never ask you to 'verify' your password by email.", "creds"),
        ],
        evidence=[ev("lookalike", "Lookalike sender domain", "uphs1 is not uphsl - a fake address made to look real."),
                  ev("urgent", "Scary deadline", "Threats and short deadlines are pressure tactics."),
                  ev("creds", "Asks you to sign in through a link", "That's how fake pages collect passwords.")]),
    "E04": dict(
        hint="Who should ever see your one-time code? And how could you check that the caller is really your bank?",
        inspectables=[
            clue("claim", "Body", "This is ExampleBank's fraud department", "Who they say they are",
                 "Anyone can claim to be your bank on the phone. You didn't call them - they called you.", "claim"),
            clue("urgent", "Body", "right now", "Pressure",
                 "Creating panic stops you from thinking clearly.", "urgent"),
            clue("otp", "Body", "Read it to me", "The request",
                 "That code is your one-time password (OTP). Whoever has it can approve logins and payments as you.", "otp"),
        ],
        evidence=[ev("claim", "Unverified caller", "A caller's name or claim proves nothing."),
                  ev("urgent", "Panic and urgency", "Urgency is used to stop you from checking."),
                  ev("otp", "Asks for your OTP", "No real bank ever asks you to read out a one-time code.")]),
    "E05": dict(
        hint="Why would a safe program need your antivirus switched off?",
        inspectables=[
            clue("stranger", "Body", "A stranger in a gaming chat", "Sender",
                 "You don't know this person, and the offer came out of nowhere.", "stranger"),
            clue("file", "Body", "FreeSkins_Generator.exe", "The file",
                 ".exe files are programs that run on your computer. Free 'generators' are a common way to spread malware.", "exe"),
            clue("av", "Body", "Turn off your antivirus first", "The request",
                 "A real tool never needs your protection switched off. Malware does.", "disable"),
        ],
        evidence=[ev("stranger", "Unknown sender", "Files from strangers can't be trusted."),
                  ev("exe", "A program file", "Running an .exe gives it control of your computer."),
                  ev("disable", "Asks you to disable antivirus", "That request exists to get malware past your protection.")]),
    "E06": dict(
        hint="Look at the three things the post asks for. Where else are those exact answers used?",
        inspectables=[
            clue("sender", "Sender", "", "Who posted it",
                 "A viral post from someone you don't know. It's made to be shared widely."),
            clue("comment", "Body", "Comment your", "Where you answer",
                 "Comments are public. Anyone - including scammers - can read them.", "public"),
            clue("pet", "Body", "first pet's name", "It asks for",
                 "A very common answer to account security questions.", "secq"),
            clue("street", "Body", "street you grew up on", "It asks for",
                 "Another classic security-question answer.", "secq"),
            clue("month", "Body", "birth month", "It asks for",
                 "Part of your birthday, which is often used to confirm who you are.", "personal"),
        ],
        evidence=[ev("public", "Public answers", "Anything posted in comments is visible to everyone."),
                  ev("secq", "Security-question answers", "These answers are used to reset passwords."),
                  ev("personal", "Personal details", "Birthdays help scammers pretend to be you.")]),
}

LEVEL1 = dict(
    tutorial=True, firstQuestion="E03", briefingTitle="WELCOME, INVESTIGATOR",
    briefing="You just joined the Cybercrime Investigation Unit. Your job: catch fake texts, emails and posts before they "
             "steal money, passwords or personal details.\n\n"
             "This is your training case. A guide will walk you through your first question.",
    objectives=["Follow the guide on your first question.", "Answer all 6 questions.",
                "Score {unlock}+ to unlock the next room."])

ROOMS = [
    ("Assets/Levels/Level1_TheFirstCase.asset", "Welcome to your first case."),
    ("Assets/Levels/Level2_TheInvestigation.asset", "Your investigation continues."),
    ("Assets/Levels/Level3_TheDigitalTrap.asset", "The traps are getting more convincing."),
    ("Assets/Levels/Level4_DigitalForensicsLab.asset", "Welcome to the Digital Forensics Lab."),
    ("Assets/Levels/Level5_MacLab.asset", "The campus network has been compromised."),
]

def check_clues(qid, inspectables, evidence, fields=None, activity_texts=None):
    """Every clue phrase must be in its text, every clue findable, ids unique."""
    ids = [c["id"] for c in inspectables]
    assert len(ids) == len(set(ids)), (qid, "inspectable ids must be unique")
    texts = {i["key"]: i["text"] for i in (activity_texts or [])}
    names = {"Sender": "sender", "SenderDetail": "senderDetail", "Subject": "subject", "Body": "body", "Link": "link"}
    for c in inspectables:
        text = texts.get(c["part"], "") if c["field"] == "Activity" else (fields or {}).get(names[c["field"]], "")
        assert text, (qid, c["id"], "empty field / unknown activity item")
        assert c["match"] == "" or c["match"] in text, (qid, c["id"], "match phrase not in the text")
        assert c["clueId"] == "" or any(e["id"] == c["clueId"] for e in evidence), (qid, c["id"], "unknown clue")
    for e in evidence:
        assert any(c["clueId"] == e["id"] for c in inspectables), (qid, e["id"], "clue can never be found")

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
        item.update(hint="", inspectables=[], evidence=[])
        if r_i == 0 and qid in TUTORIAL:
            t = TUTORIAL[qid]
            fields = {"Sender": item["sender"], "SenderDetail": item["senderDetail"], "Subject": item["subject"],
                      "Body": item["body"], "Link": item["link"]}
            for c in t["inspectables"]:
                assert c["match"] == "" or c["match"] in fields[c["field"]], (qid, c["id"], "match phrase not in the text")
                assert c["clueId"] == "" or any(e["id"] == c["clueId"] for e in t["evidence"]), (qid, c["id"])
            for e in t["evidence"]:
                assert any(c["clueId"] == e["id"] for c in t["inspectables"]), (qid, e["id"], "clue can never be found")
            item.update(hint=t["hint"], inspectables=t["inspectables"], evidence=t["evidence"])
        if qid in ACTIVITIES:
            # Interactive activity: its clue details replace the plain-text ones (the question text is unchanged).
            a = ACTIVITIES[qid]
            assert item["choices"], (qid, "activities are for choice questions")
            assert qid not in PLAIN
            check_clues(qid, a["inspectables"], a["evidence"], activity_texts=a["activity"]["items"])
            keys = [i["key"] for i in a["activity"]["items"]]
            assert len(keys) == len(set(keys)), (qid, "item keys must be unique")
            n_key = sum(1 for e in a["evidence"] if e["supportsVerdict"])
            assert 0 <= a["activity"]["minClues"] <= n_key, (qid, "minClues > key clues")
            assert not a["activity"]["outcomes"] or len(a["activity"]["outcomes"]) == len(item["choices"]), (qid, "one outcome per choice")
            item.update(inspectables=a["inspectables"], evidence=a["evidence"], activity=a["activity"])
        elif qid in VERDICT_CLUES:
            v = VERDICT_CLUES[qid]
            assert not item["choices"] and r_i > 0, (qid, "verdict clues are for SCAM / LEGITIMATE items outside Level 1")
            check_clues(qid, v["inspectables"], v["evidence"], fields=item)
            item.update(inspectables=v["inspectables"], evidence=v["evidence"])
        room["lessons"].append(item["learningObjective"])
        room["questions"].append(item)
    assert len(room["questions"]) == 6
    if r_i == 0:
        assert all(q["hint"] and q["inspectables"] for q in room["questions"]), "every Level 1 question needs a hint and clues"
        assert any(q["id"] == LEVEL1["firstQuestion"] for q in room["questions"])
        room.update(LEVEL1)
    else:
        room.update(tutorial=False, firstQuestion="", briefingTitle="", briefing="", objectives=[])
    out["rooms"].append(room)

ids = [q["id"] for r in out["rooms"] for q in r["questions"]]
assert len(ids) == 30 and len(set(ids)) == 30
json.dump(out, open(OUT, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
acts = [q["id"] for r in out["rooms"] for q in r["questions"] if q.get("activity")]
assert set(acts) == set(ACTIVITIES), "every activity must be used"
print("activities", len(acts), "verdict clue sets", len(VERDICT_CLUES))
print("rooms", len(out["rooms"]), "questions", len(ids),
      "choice questions", sum(1 for r in out["rooms"] for q in r["questions"] if q["choices"]))
