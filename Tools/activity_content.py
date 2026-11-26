"""Interactive activities for the curriculum questions (read by build_game_questions.py).

The validated questions are NOT changed here: wording, choices, correct answers, explanations, learning objectives,
curriculum links, difficulty and IDs all still come from Docs/Curriculum_Question_Bank.md. This file only decides
HOW a question is played:

* ACTIVITIES      - choice questions shown with a simulated app (email, browser, chat, call, form, Wi-Fi list, files,
                    evidence folder). Its details are inspected with the game's clue system; key clues go on the
                    evidence board; `minClues` key clues unlock the answers (the player may still answer early).
                    Decision questions also get `outcomes`: what happens next after each choice (original order).
* VERDICT_CLUES   - SCAM / LEGITIMATE messages and pages outside Level 1 that get clickable clue details.

Rules for this content:
* Show only what the question already states (or a neutral, obviously simulated detail). Never add a fact that
  changes the answer.
* Findings and outcomes follow the question's own explanation.
* Everything is fictional: .example domains, masked passwords and codes, no real sites, nothing to type.
"""


def clue(id, field, match, label, finding, clue_id="", link="", part=""):
    return {"id": id, "field": field, "match": match, "label": label, "finding": finding,
            "linkAddress": link, "clueId": clue_id, "part": part}


def act(id, part, match, label, finding, clue_id="", link=""):
    """An inspectable detail inside an activity item."""
    return clue(id, "Activity", match, label, finding, clue_id, link, part)


def ev(id, title, why, key=True):
    return {"id": id, "title": title, "whyItMatters": why, "supportsVerdict": key}


def item(key, role, text, label=""):
    return {"key": key, "role": role, "label": label, "text": text}


def activity(kind, title, instruction, items, min_clues=1, outcomes=None):
    return {"kind": kind, "title": title, "instruction": instruction, "items": items,
            "minClues": min_clues, "outcomes": outcomes or []}


ACTIVITIES = {
    # ------------------------------------------------------------------ Room 1 (Easy)
    "E03": dict(  # Identify the Threat -> Email Analysis (the guided tutorial question: no answer lock)
        activity=activity("Email", "Campus Mail  ·  Inbox", "Check the email: who sent it, the subject, and what it asks you to do.", [
            item("from", "Field", "Campus IT Helpdesk <helpdesk@uphs1-binan.example>", "From"),
            item("subject", "Field", "Mailbox 98% full: verify now", "Subject"),
            item("body", "Text", "Your school mailbox will be DELETED in 24 hours. Click below and sign in to verify your account."),
            item("button", "Button", "VERIFY MY ACCOUNT"),
        ], min_clues=0),
        inspectables=[
            act("address", "from", "uphs1-binan.example", "Sender address",
                "Look closely: uphs1 uses the number 1 instead of the letter l. It's a lookalike of the school's real domain.", "lookalike"),
            act("subject", "subject", "Mailbox 98% full", "Subject",
                "Full mailboxes really happen. On its own this isn't a warning sign - look at what the email asks you to do."),
            act("deadline", "body", "DELETED in 24 hours", "Threat",
                "A scary deadline pushes you to act fast without checking.", "urgent"),
            act("request", "body", "sign in to verify your account", "What it asks",
                "It wants your password through a link. Real IT staff never ask you to 'verify' your password by email.", "creds"),
            act("button", "button", "VERIFY MY ACCOUNT", "The button",
                "It leads to a sign-in page from an email link - the way fake pages collect passwords. (Simulated: nothing opens.)", "creds"),
        ],
        evidence=[ev("lookalike", "Lookalike sender domain", "uphs1 is not uphsl - a fake address made to look real."),
                  ev("urgent", "Scary deadline", "Threats and short deadlines are pressure tactics."),
                  ev("creds", "Asks you to sign in through a link", "That's how fake pages collect passwords.")]),

    "E04": dict(  # What Should You Do? -> Call transcript + Decision Challenge
        activity=activity("Call", "Incoming call  ·  transcript", "Read the call. Find the warning signs, then choose what to do.", [
            item("c1", "Incoming", "This is ExampleBank's fraud department. Someone is using your card right now.", "Caller"),
            item("c2", "Incoming", "We just sent you a 6-digit code. Read it to me so we can block the transaction.", "Caller"),
            item("sms", "Note", "New text on your phone: \"Your one-time code is ••••••\" (hidden in this simulation)"),
        ], outcomes=[
            "The caller uses your code to approve their own payment from your card. The \"fraud department\" was the fraud.",
            "Part of the code still helps them, and you've shown you'll share it. They call back with a new story for the rest.",
            "An email from the caller would prove nothing - it comes from the same person. The suspicious payment goes on while you wait.",
            "You reach the real bank on a number you already trust. Your code stays private, and the bank can check your card safely.",
        ]),
        inspectables=[
            act("claim", "c1", "This is ExampleBank's fraud department", "Who they say they are",
                "Anyone can claim to be your bank on the phone. You didn't call them - they called you.", "claim"),
            act("urgent", "c1", "right now", "Pressure",
                "Creating panic stops you from thinking clearly.", "urgent"),
            act("otp", "c2", "Read it to me", "The request",
                "That code is your one-time password (OTP). Whoever has it can approve logins and payments as you.", "otp"),
            act("code", "sms", "one-time code", "The code you received",
                "A one-time code proves that YOU are approving something. A real bank never asks you to read it aloud.", "otp"),
        ],
        evidence=[ev("claim", "Unverified caller", "A caller's name or claim proves nothing."),
                  ev("urgent", "Panic and urgency", "Urgency is used to stop you from checking."),
                  ev("otp", "Asks for your OTP", "No real bank ever asks you to read out a one-time code.")]),

    "E05": dict(  # Scenario-Based -> Chat + file attachment + Decision Challenge
        activity=activity("Chat", "Gaming chat  ·  direct message", "Check the chat and the file, then choose.", [
            item("from", "Field", "Unknown player (not on your friends list)", "From"),
            item("file", "File", "FreeSkins_Generator.exe", "sent a file"),
            item("m1", "Incoming", "Turn off your antivirus first. It gives a false alarm on this tool.", "Unknown player"),
        ], outcomes=[
            "With your protection off, the \"generator\" installs an information stealer. Saved passwords and logged-in sessions are taken.",
            "The program runs before the scan, so the damage is already done. A scan afterwards can't undo stolen passwords.",
            "Nothing runs on your PC, and reporting the sender helps protect other players in the chat.",
            "Your friend's PC is put at risk instead - and the file is still malware.",
        ]),
        inspectables=[
            act("stranger", "from", "Unknown player", "Sender",
                "You don't know this person, and the offer came out of nowhere.", "stranger"),
            act("file", "file", "FreeSkins_Generator.exe", "The file",
                ".exe files are programs that run on your computer. Free 'generators' are a common way to spread malware.", "exe"),
            act("av", "m1", "Turn off your antivirus first", "The request",
                "A real tool never needs your protection switched off. Malware does.", "disable"),
            act("alarm", "m1", "false alarm", "The excuse",
                "Explaining away the warning in advance is part of the trick. The alarm is probably right.", "disable"),
        ],
        evidence=[ev("stranger", "Unknown sender", "Files from strangers can't be trusted."),
                  ev("exe", "A program file", "Running an .exe gives it control of your computer."),
                  ev("disable", "Asks you to disable antivirus", "That request exists to get malware past your protection.")]),

    # ------------------------------------------------------------------ Room 2 (Easy -> Medium)
    "E07": dict(  # Multiple Choice -> Form inspection
        activity=activity("Form", "Mall raffle  ·  sign-up form", "Check each box on the form. Which ones ask for too much?", [
            item("name", "Field", "required", "Full name"),
            item("mobile", "Field", "required", "Mobile number"),
            item("birth", "Field", "required", "Birthdate"),
            item("id", "Field", "Upload a photo of your valid ID (required)", "ID photo"),
            item("notice", "Field", "None provided", "Privacy notice"),
            item("info", "Note", "Free to join! Staff in company shirts are at the booth. The draw is next week."),
        ]),
        inspectables=[
            act("name", "name", "required", "Full name",
                "A raffle needs to know who won. Asking for a name is reasonable."),
            act("mobile", "mobile", "required", "Mobile number",
                "A raffle needs a way to contact the winner. This is proportionate."),
            act("birth", "birth", "required", "Birthdate",
                "A raffle doesn't need your birthdate. Together with an ID photo it can be used for identity theft.", "birth"),
            act("id", "id", "photo of your valid ID", "ID photo",
                "Far more than joining a raffle needs. An ID photo is valuable to identity thieves.", "idphoto"),
            act("notice", "notice", "None provided", "Privacy notice",
                "Without a notice you can't know why your data is collected or what happens to it (transparency).", "notice"),
            act("free", "info", "Free to join!", "Free entry",
                "Free raffles are normal. Not a warning sign on its own."),
            act("shirts", "info", "company shirts", "Staff",
                "Uniforms are easy to copy and prove little either way. Not a warning sign on its own."),
        ],
        evidence=[ev("idphoto", "Asks for an ID photo", "Collect only what is needed (proportionality)."),
                  ev("birth", "Asks for your birthdate", "Birthdates help thieves pretend to be you."),
                  ev("notice", "No privacy notice", "People must be told why their data is collected (transparency).")]),

    "E10": dict(  # What Should You Do? -> Evidence collection + Decision Challenge
        activity=activity("Chat", "Marketplace chat  ·  seller", "Look at what you have. What could prove what happened?", [
            item("m1", "Outgoing", "Paid na po, ₱1,500 via PayLite. When will you ship?", "You"),
            item("blocked", "Banner", "You can no longer message this account. The seller blocked you.", "BLOCKED"),
            item("receipt", "File", "PayLite_Receipt.png  ·  ₱1,500  ·  reference number shown", "payment"),
            item("profile", "Row", "Seller's profile link and account name", "seller"),
        ], outcomes=[
            "The chat is gone, and with it the proof of what was agreed. Your report becomes much harder to act on.",
            "The public post starts a fight and can get you into legal trouble. The money still isn't traced.",
            "The \"refund unlock\" is a second scam. You lose another payment.",
            "PayLite and the authorities get the reference number and account details they need to trace the money.",
        ]),
        inspectables=[
            act("chat", "m1", "Paid na po, ₱1,500 via PayLite", "Your chat",
                "The chat shows what was agreed and paid. Keep it - it's evidence.", "chat"),
            act("blocked", "blocked", "The seller blocked you", "Blocked",
                "Being blocked after paying is a classic sign of a fake seller. Save everything before it disappears."),
            act("receipt", "receipt", "reference number", "Payment receipt",
                "The reference number lets PayLite trace where the money went.", "ref"),
            act("profile", "profile", "profile link and account name", "Seller's profile",
                "The seller's account details help the platform and the police find them.", "account"),
        ],
        evidence=[ev("chat", "The chat history", "Proof of what was agreed. Deleting it destroys evidence."),
                  ev("ref", "Receipt with reference number", "Reference numbers can help trace the money."),
                  ev("account", "Seller's account details", "They let the platform and the police act.")]),

    "M02": dict(  # Identify the Threat -> Website (URL) inspection
        activity=activity("Browser", "ExampleBank Online  ·  Log in", "Check the address bar, piece by piece. Where does this page really go?", [
            item("addr", "Field", "https://examplebank.example.account-verify.example/login", "Address"),
            item("page", "Text", "ExampleBank Online. Log in to continue."),
            item("button", "Button", "LOG IN"),
        ]),
        inspectables=[
            act("https", "addr", "https://", "https://",
                "HTTPS only encrypts the connection. It doesn't tell you who owns the site."),
            act("sub", "addr", "examplebank.example.", "The left part",
                "Everything to the left of the real domain is a subdomain - the attacker can write anything there, even a bank's name.", "sub"),
            act("domain", "addr", "account-verify.example", "The real domain",
                "Read the domain from the right, ending just before the first single '/'. The real domain is account-verify.example.", "domain"),
            act("path", "addr", "/login", "The path",
                "The part after the '/' can say anything. It doesn't change who owns the site."),
            act("page", "page", "ExampleBank Online", "The page",
                "Copying a bank's look is easy. Nothing is misspelled here, so this isn't typosquatting either."),
        ],
        evidence=[ev("domain", "Real domain: account-verify.example", "The domain decides who owns the page."),
                  ev("sub", "Bank name used as a subdomain", "Subdomain tricks put a trusted name on the left.")]),

    # ------------------------------------------------------------------ Room 3 (Medium)
    "M03": dict(  # Multiple Choice -> Browser padlock / certificate inspection
        activity=activity("Browser", "Sign in", "Click the padlock. What does it really prove?", [
            item("addr", "Field", "https://login-page.example/signin", "Address"),
            item("lock", "Row", "Padlock: Connection is secure", "click to view"),
            item("cert", "Note", "Certificate: issued to login-page.example. Checks that the requester controls this domain."),
            item("form", "Text", "Sign in with your email and password."),
        ]),
        inspectables=[
            act("https", "addr", "https://", "https://",
                "The 's' means the connection uses TLS encryption. Others on the network can't read or change your data.", "enc"),
            act("lock", "lock", "Connection is secure", "The padlock",
                "The connection is private - data in transit is encrypted. It says nothing about who runs the site.", "enc"),
            act("cert", "cert", "controls this domain", "The certificate",
                "Certificates are easy for anyone to get for their own domain - scam sites have padlocks too.", "anyone"),
            act("form", "form", "email and password", "The form",
                "A padlock doesn't make a form safe to fill in. You still have to check the domain."),
        ],
        evidence=[ev("enc", "Encrypted connection", "That's what the padlock actually means."),
                  ev("anyone", "Anyone can get a certificate", "So a padlock can't prove the site is the real company.")]),

    "M04": dict(  # Identify the Threat -> Chat (SMS thread)
        activity=activity("Chat", "Messages  ·  unknown number", "Read the texts. Click each warning sign.", [
            item("from", "Field", "Unknown number (not in your contacts)", "From"),
            item("m1", "Incoming", "Ma, si Migs 'to. Nasira phone ko, bagong number ko 'to.", "Unknown number"),
            item("m2", "Incoming", "Pa-send naman ₱3,000 sa PayLite 0917 555 0188, emergency lang.", "Unknown number"),
            item("m3", "Incoming", "Wag ka muna tumawag, wala pa akong load.", "Unknown number"),
        ], min_clues=2),
        inspectables=[
            act("from", "from", "not in your contacts", "Sender",
                "Not in your contacts. The first text explains it away with a 'new number'."),
            act("newnum", "m1", "bagong number ko 'to", "\"New number\"",
                "Claiming to be family on a new number is how impersonators skip your contacts list.", "newnum"),
            act("money", "m2", "Pa-send naman ₱3,000", "The request",
                "The whole story leads to one thing: send money now.", "money"),
            act("urgent", "m2", "emergency lang", "Emergency",
                "An invented emergency rushes you past checking.", "urgent"),
            act("nocall", "m3", "Wag ka muna tumawag", "\"Don't call yet\"",
                "It stops you from calling Migs's old number - the easiest way to check.", "nocall"),
        ],
        evidence=[ev("newnum", "Claims to be family on a new number", "Impersonation exploits trust, not technology."),
                  ev("money", "Asks for money", "The goal of the story."),
                  ev("urgent", "Invented emergency", "Urgency stops you from thinking."),
                  ev("nocall", "Blocks you from checking", "Call the old number or ask something only Migs would know.")]),

    "M05": dict(  # What Should You Do? -> Browser (the page you just used) + Decision Challenge
        activity=activity("Browser", "Account verified", "Check what happened, then choose your next step.", [
            item("addr", "Field", "https://company-portal.login-verify.example/signin", "Address"),
            item("done", "Banner", "Thank you! Your account has been verified.", "SIGNED IN"),
            item("typed", "Note", "One minute ago you typed your work password on this page."),
            item("email", "Row", "The email that sent you to this page", "inbox"),
        ], outcomes=[
            "The attacker can sign in as you while nobody knows. The longer it stays hidden, the worse the damage.",
            "Coworkers open the same dangerous link, and more passwords are stolen.",
            "IT loses the evidence it needs to block the site, and your stolen password still works.",
            "The stolen password stops working, IT blocks the site and warns others, and you'll spot any login you didn't start.",
        ]),
        inspectables=[
            act("domain", "addr", "login-verify.example", "The real domain",
                "Read from the right: the real domain is login-verify.example - not your company's. This was a fake page.", "fake"),
            act("done", "done", "Your account has been verified", "The message",
                "Fake pages often say 'verified' so you don't worry. Nothing was verified - your password was collected."),
            act("typed", "typed", "work password", "What you typed",
                "The attacker now has your work password. Every minute counts.", "stolen"),
            act("email", "email", "The email that sent you to this page", "The email",
                "IT needs this email to block the site and warn others. Don't delete it or forward it to coworkers.", "evidence"),
        ],
        evidence=[ev("fake", "It was a fake page", "The domain wasn't your company's."),
                  ev("stolen", "Your password is stolen", "Change it fast so it becomes useless."),
                  ev("evidence", "The email is evidence", "Reporting it lets IT limit the damage.")]),

    "M07": dict(  # Scenario-Based -> Network (Wi-Fi list + sign-in page) + Decision Challenge
        activity=activity("Network", "Wi-Fi  ·  networks nearby", "Check the networks and the sign-in page, then choose.", [
            item("w1", "Row", "Kape_FreeWiFi", "open"),
            item("w2", "Row", "Kape_Free_WiFi", "open  ·  connected"),
            item("portal", "Text", "Log in with your SocialBook email and password to get internet access.", "Network sign-in page"),
            item("button", "Button", "LOG IN WITH SOCIALBOOK"),
        ], outcomes=[
            "Your SocialBook password stays safe. Staff point you to the real network, and you use mobile data for anything sensitive.",
            "The fake hotspot collects your SocialBook login, and the attacker can take over your account.",
            "You keep joining networks you can't trust, and anything sent on a fake one can be watched.",
            "Your computer loses its protection on an untrusted network, and the fake page is still waiting for a password.",
        ]),
        inspectables=[
            act("twin1", "w1", "Kape_FreeWiFi", "Network 1",
                "Almost the same name as the other network. One of them may be a fake 'evil twin' hotspot.", "twin"),
            act("twin2", "w2", "Kape_Free_WiFi", "Network 2",
                "Only an underscore apart. You can't tell which one is real without asking the staff.", "twin"),
            act("portal", "portal", "SocialBook email and password", "The sign-in page",
                "A Wi-Fi page has no reason to ask for a different site's password. That is credential harvesting.", "harvest"),
            act("button", "button", "LOG IN WITH SOCIALBOOK", "The button",
                "Simulated: nothing is sent. A VPN wouldn't help here - it can't protect a password you type into a fake page.", "harvest"),
        ],
        evidence=[ev("twin", "Two lookalike networks", "A sign of an 'evil twin' hotspot."),
                  ev("harvest", "Asks for another site's password", "Credential harvesting.")]),

    # ------------------------------------------------------------------ Room 4 (Medium -> Hard)
    "M09": dict(  # Scenario-Based -> Call transcript + Decision Challenge
        activity=activity("Call", "IT help desk line  ·  transcript", "Read the call. Find the pressure tricks, then choose.", [
            item("c1", "Incoming", "This is Dean Reyes. I'm locked out and presenting in five minutes.", "Caller"),
            item("c2", "Incoming", "Reset my password and send it to my personal email, deanreyes.personal@freemail.example. Hurry!", "Caller"),
        ], min_clues=2, outcomes=[
            "If the caller isn't the dean, an attacker now controls the dean's account - and the procedure that would have stopped it was skipped.",
            "The password still goes to someone you haven't verified. Changing the delivery method doesn't fix the missing identity check.",
            "An unverified caller now has your account too, and anything they do is logged under your name.",
            "A quick call-back confirms who it really is. The real dean gets help through the official account; an impostor gets nothing.",
        ]),
        inspectables=[
            act("authority", "c1", "This is Dean Reyes", "Who they say they are",
                "A name on the phone isn't proof. Authority is used to make you skip procedure.", "authority"),
            act("pressure", "c1", "presenting in five minutes", "Time pressure",
                "A deadline leaves no time to check - that's the point.", "pressure"),
            act("personal", "c2", "send it to my personal email", "Where it should go",
                "A reset sent to a personal address could hand the account to an attacker.", "personal"),
            act("hurry", "c2", "Hurry!", "Hurry",
                "More pressure. A real dean would understand a quick verification.", "pressure"),
        ],
        evidence=[ev("authority", "Claims authority", "Pretexting uses rank to skip the rules."),
                  ev("pressure", "Time pressure", "Urgency is used to stop you from verifying."),
                  ev("personal", "Reset to a personal address", "Resets go only to the official account.")]),

    "M10": dict(  # Identify the Threat -> Email with attachment + Office warning bar
        activity=activity("Email", "Campus Mail  ·  attachment opened", "Check the attachment and the yellow bar. Don't enable anything.", [
            item("subject", "Field", "Revised grading sheet", "Subject"),
            item("file", "File", "Grades_Final.docm", "attachment"),
            item("bar", "Banner", "Content is disabled. Click ENABLE CONTENT to view the grades.", "SECURITY WARNING"),
            item("button", "Button", "ENABLE CONTENT"),
        ], min_clues=2),
        inspectables=[
            act("subject", "subject", "Revised grading sheet", "Subject",
                "A believable subject is part of the lure. On its own it proves nothing."),
            act("docm", "file", ".docm", "File type",
                "The 'm' means the document can contain macros - small programs.", "macro"),
            act("disabled", "bar", "Content is disabled.", "The yellow bar",
                "Office blocks macros by default, for your safety.", "blocked"),
            act("lure", "bar", "Click ENABLE CONTENT to view the grades", "What it asks",
                "A grades file doesn't need macros to display. The document is pushing you to run its code.", "lure"),
            act("button", "button", "ENABLE CONTENT", "The button",
                "Clicking it would run the hidden code and could install malware. (Simulated: nothing runs.)", "lure"),
        ],
        evidence=[ev("macro", "Macro-enabled file (.docm)", "Macros are programs hidden in the document."),
                  ev("blocked", "Macros blocked by default", "Office disables them for safety."),
                  ev("lure", "Pushes you to enable content", "Enabling runs the attacker's code.")]),

    "H01": dict(  # Identify the Threat -> Email + case file (Evidence Board)
        activity=activity("Email", "Treasurer's inbox  ·  with case file", "Compare the email with the case file. What did the scammer look up?", [
            item("from", "Field", "Prof. L. Santos <lsantos.adviser@freemail.example>", "From"),
            item("body", "Text", "Hi! As we discussed at Tuesday's meeting, please send the venue deposit for the Sept 14 Acquaintance Party to our new account before 3 PM today. Thanks!"),
            item("case1", "Row", "The meeting, the date and the event were all posted on the org's public page.", "case file"),
            item("case2", "Row", "The adviser's school address is lsantos@uphsl-binan.example.", "case file"),
        ], min_clues=2),
        inspectables=[
            act("freemail", "from", "freemail.example", "Sender address",
                "A free-mail address, not the adviser's school address in the case file.", "freemail"),
            act("details", "body", "Tuesday's meeting", "Real details",
                "Real names, dates and events make the email convincing - and gathering them takes research.", "recon"),
            act("public", "case1", "posted on the org's public page", "Where the details came from",
                "Everything the attacker 'knew' was public. That's reconnaissance before a targeted attack.", "recon"),
            act("school", "case2", "lsantos@uphsl-binan.example", "Adviser's real address",
                "Compare it with the sender: the real adviser writes from the school domain.", "freemail"),
            act("account", "body", "our new account", "New account",
                "A sudden change of bank account is a classic sign of payment fraud.", "newacct"),
            act("deadline", "body", "before 3 PM today", "Deadline",
                "A same-day deadline leaves no time to check with the real adviser.", "deadline"),
        ],
        evidence=[ev("recon", "Built from public information", "Research aimed at one person = spear phishing."),
                  ev("freemail", "Free-mail sender", "Not the adviser's school address."),
                  ev("newacct", "\"New account\"", "Payment details changed by email."),
                  ev("deadline", "Same-day deadline", "Pressure to pay before checking.")]),

    "H02": dict(  # Scenario-Based -> Browser consent screen + Decision Challenge
        activity=activity("Browser", "NovaCloud  ·  app permissions", "Check what the app is asking for, then choose.", [
            item("addr", "Field", "https://novacloud.example/consent", "Address"),
            item("app", "Field", "DocuView Plus (unverified publisher)", "App"),
            item("p1", "Row", "Read, send and delete your mail", "wants to"),
            item("p2", "Row", "Access your files at any time", "wants to"),
            item("p3", "Row", "Keep access when you're not using it", "wants to"),
            item("allow", "Button", "ALLOW"),
        ], min_clues=2, outcomes=[
            "The app gets nothing. You report it and review your connected apps in case anything else slipped through.",
            "DocuView Plus can now read, send and delete your mail. The real sign-in page didn't make the app safe.",
            "The app keeps its access even after your password changes. It can still read your mail until you remove it.",
            "Once is enough: the app keeps access even when you're not using it.",
        ]),
        inspectables=[
            act("real", "addr", "novacloud.example", "The address",
                "This really is NovaCloud. The real sign-in page is part of the trick: the attack is the permission request, not your password.", "trick"),
            act("unverified", "app", "unverified publisher", "Publisher",
                "Nobody has verified who made this app.", "unverified"),
            act("mail", "p1", "send and delete your mail", "Mail access",
                "A document viewer has no reason to send or delete your mail.", "overreach"),
            act("files", "p2", "at any time", "File access",
                "Access at any time, not just to open one document.", "overreach"),
            act("keep", "p3", "Keep access", "Lasting access",
                "This access stays even after you change your password - until you remove the app from your account.", "persist"),
            act("allow", "allow", "ALLOW", "Allow",
                "Clicking this would grant the access. (Simulated: nothing is granted.)"),
        ],
        evidence=[ev("trick", "The real page is part of the trick", "Consent phishing doesn't need your password."),
                  ev("overreach", "Asks for far too much", "A viewer doesn't need to send or delete mail."),
                  ev("persist", "Access that lasts", "A password change doesn't remove it."),
                  ev("unverified", "Unverified publisher", "Nobody vouches for this app.")]),

    "H03": dict(  # What Should You Do? -> Email with authentication results + Decision Challenge
        activity=activity("Email", "Finance  ·  existing thread", "Check the email and its checks. Does 'passed' mean safe?", [
            item("from", "Field", "Finance Director  ·  the director's real address", "From"),
            item("auth", "Field", "SPF pass  ·  DKIM pass  ·  DMARC pass", "Checks"),
            item("thread", "Note", "This message is a reply in an existing email thread."),
            item("body", "Text", "Our supplier changed banks. Please pay today's invoice (₱480,000) to the new account below. I'm in meetings all day, so don't call, just reply here."),
        ], min_clues=2, outcomes=[
            "₱480,000 goes to the attacker's account. Passing the checks didn't prove who was typing.",
            "Your reply goes straight to whoever controls the account - and they 'confirm' it.",
            "Half the money still goes to the attacker before anyone checks.",
            "A call on the directory number and the supplier's number on file shows whether the request is real - before any money or bank details change.",
        ]),
        inspectables=[
            act("auth", "auth", "SPF pass  ·  DKIM pass  ·  DMARC pass", "Authentication",
                "These checks prove the email came from the director's account - not who is using it. If the account was taken over, every check still passes.", "passes"),
            act("thread", "thread", "existing email thread", "Thread",
                "Whoever controls the account can reply in real threads too."),
            act("banks", "body", "Our supplier changed banks", "The request",
                "A change of bank details is exactly what business email compromise looks for. Always verify it.", "bank"),
            act("amount", "body", "₱480,000", "Amount",
                "A large, same-day payment raises the stakes - and the pressure."),
            act("nocall", "body", "don't call, just reply here", "\"Don't call\"",
                "Blocking a call-back is a warning sign in itself. A reply goes to whoever controls the account.", "nocall"),
        ],
        evidence=[ev("passes", "Checks pass, but prove little", "A taken-over account passes every check."),
                  ev("bank", "Bank details changed", "Verify every change through a channel you already trust."),
                  ev("nocall", "\"Don't call\"", "It blocks the one check that works.")]),

    # ------------------------------------------------------------------ Room 5 (Hard)
    "H05": dict(  # Scenario-Based -> Evidence Board + Decision Challenge
        activity=activity("Evidence", "Your friend's evidence folder", "Check each item. Which evidence can investigators trust?", [
            item("chat", "Row", "Full chat with the scammer, untouched on the original phone", "original"),
            item("shots", "Row", "Full screenshots showing the profile and number, with dates and times", "screenshots"),
            item("receipt", "Row", "Payment receipts with reference numbers", "receipts"),
            item("crop", "Row", "A cropped screenshot with the scammer's name blurred", "edited copy"),
            item("log", "Note", "A note of when and how each item was collected"),
        ], min_clues=2, outcomes=[
            "Investigators can't confirm the cropped images - and the blurred name was the lead they needed.",
            "Your friend's personal data spreads through the group, and copies get changed along the way.",
            "Investigators get complete, unchanged evidence with a clear record of how it was collected.",
            "Uninstalling may wipe the chat history, leaving the report to rest on memory alone.",
        ]),
        inspectables=[
            act("chat", "chat", "untouched on the original phone", "Original chat",
                "Complete, unchanged evidence on the original device can be trusted.", "complete"),
            act("shots", "shots", "profile and number, with dates and times", "Full screenshots",
                "Identifiers and timestamps are what investigators need to trace the scammer.", "ids"),
            act("receipt", "receipt", "reference numbers", "Receipts",
                "Reference numbers can help trace the money.", "ids"),
            act("crop", "crop", "cropped screenshot with the scammer's name blurred", "Edited copy",
                "Cropping removes context and blurring hides identifiers. Edited evidence is weaker evidence.", "edited"),
            act("log", "log", "when and how each item was collected", "Collection note",
                "This is a basic chain of custody: it shows the evidence wasn't changed.", "custody"),
        ],
        evidence=[ev("complete", "Complete and unchanged", "Evidence has to be complete to be trusted."),
                  ev("ids", "Identifiers kept", "Names, numbers and references are the leads."),
                  ev("edited", "Edits weaken evidence", "Don't crop or blur; share copies instead."),
                  ev("custody", "Chain of custody", "Record when and how each item was collected.")]),

    "H06": dict(  # Identify the Threat -> Email header analysis
        activity=activity("Email", "Message source  ·  headers", "Check each header line. Which one is strong proof?", [
            item("from", "Field", "\"Registrar's Office\" <registrar@uphsl-binan.example>", "From"),
            item("reply", "Field", "registrar.office.ph@freemail.example", "Reply-To"),
            item("ret", "Field", "bounce@mailer-xyz.example", "Return-Path"),
            item("auth", "Field", "spf=fail; dkim=none; dmarc=fail", "Authentication-Results"),
            item("subject", "Field", "FINAL reminder: unpaid tuition, account on hold", "Subject"),
            item("time", "Field", "2:14 AM", "Sent"),
        ], min_clues=2),
        inspectables=[
            act("display", "from", "\"Registrar's Office\"", "Display name",
                "The From line can say anything. A display name proves nothing."),
            act("spf", "auth", "spf=fail", "SPF",
                "SPF fail: the registrar's domain did not authorize the server that sent this email.", "authfail"),
            act("dmarc", "auth", "dmarc=fail", "DMARC",
                "DMARC fail: the email doesn't line up with the registrar's own domain policy.", "authfail"),
            act("reply", "reply", "freemail.example", "Reply-To",
                "Replies quietly go to a free-mail address - straight to the attacker.", "replyto"),
            act("ret", "ret", "mailer-xyz.example", "Return-Path",
                "A different bounce domain. Worth noting, but the authentication results tell you more."),
            act("subject", "subject", "FINAL reminder", "Subject",
                "Pressure is a weak sign on its own. Real reminders can sound urgent too."),
            act("time", "time", "2:14 AM", "Time sent",
                "Odd hours are a weak sign. Emails are sent at all hours."),
        ],
        evidence=[ev("authfail", "SPF and DMARC fail", "The domain didn't authorize the sender."),
                  ev("replyto", "Reply-To goes to free mail", "Your answer would go to the attacker.")]),

    "H08": dict(  # What Should You Do? -> File explorer + Decision Challenge
        activity=activity("Files", "Lab PC  ·  Documents", "Check what's happening on this PC, then choose your first step.", [
            item("f1", "File", "Thesis_Draft.docx.locked", "changed just now"),
            item("f2", "File", "Budget.xlsx.locked", "changed just now"),
            item("note", "File", "README_RESTORE_FILES.txt", "new"),
            item("ransom", "Banner", "Your files are encrypted. Pay in cryptocurrency to get them back.", "RANSOM NOTE"),
            item("net", "Row", "Connected  ·  lab network and shared drives", "network"),
            item("power", "Row", "PC is on", "power"),
        ], min_clues=2, outcomes=[
            "The money goes to criminals with no guarantee your files come back - and the infection keeps spreading.",
            "The infection is cut off from shared drives and other PCs, and the evidence in memory is kept for the incident team.",
            "Restarting can destroy evidence in memory, and cleaning up doesn't stop what has already spread.",
            "The USB copy holds already-locked files and may carry the infection to the next computer.",
        ]),
        inspectables=[
            act("locked", "f1", ".locked", "File names",
                "Your files are being encrypted right now. This is ransomware.", "ransomware"),
            act("locked2", "f2", ".locked", "More files",
                "It's spreading through your files while you watch.", "ransomware"),
            act("readme", "note", "README_RESTORE_FILES.txt", "New file",
                "The ransom note. Don't delete it - it's evidence."),
            act("pay", "ransom", "Pay in cryptocurrency", "The demand",
                "Paying doesn't guarantee you'll get your files back."),
            act("net", "net", "shared drives", "Network",
                "While connected, ransomware can spread to shared drives and other PCs. Cutting the connection contains it.", "contain"),
            act("power", "power", "PC is on", "Power",
                "Leaving it on keeps evidence in memory for investigators. Restarting can destroy it.", "evidence"),
        ],
        evidence=[ev("ransomware", "Files being encrypted", "Ransomware is active on this PC."),
                  ev("contain", "Still on the network", "Disconnect first to stop it spreading."),
                  ev("evidence", "Evidence in memory", "Leave the PC on and report it.")]),

    "H10": dict(  # Scenario-Based -> Browser (fake login admin page) + Decision Challenge
        activity=activity("Browser", "Admin panel  ·  fake school login", "Look at the page, but don't touch anything. What is the right thing to do?", [
            item("addr", "Field", "http://student-portal-login.example/admin", "Address"),
            item("open", "Note", "No sign-in needed to view this page."),
            item("r1", "Row", "student_0417  ·  ••••••••", "captured"),
            item("r2", "Row", "student_0932  ·  ••••••••", "captured"),
            item("hidden", "Note", "Passwords are hidden in this simulation."),
            item("download", "Button", "DOWNLOAD LIST"),
        ], min_clues=2, outcomes=[
            "You're now holding other people's stolen data without authorization, and contacting victims yourself can cause more harm.",
            "The post sends more students to the fake page, and more passwords are captured.",
            "Your own password joins the captured list.",
            "IT takes action with the authority to protect the victims, and you never touched the stolen data.",
        ]),
        inspectables=[
            act("addr", "addr", "student-portal-login.example", "The address",
                "For your report, write it in a non-clickable form, e.g. hxxp://student-portal-login[.]example, so nobody opens it by accident.", "report"),
            act("open", "open", "No sign-in needed", "Open access",
                "Being able to see it doesn't make it yours to use. Using it further would be unauthorized access.", "unauth"),
            act("r1", "r1", "student_0417", "Captured entry",
                "Real victims' stolen passwords. Handling them breaks their privacy - even to help.", "privacy"),
            act("r2", "r2", "student_0932", "Captured entry",
                "Another victim. Their data isn't yours to download or share.", "privacy"),
            act("download", "download", "DOWNLOAD LIST", "Download",
                "Downloading means handling stolen data without authorization. (Simulated: nothing downloads.)", "unauth"),
        ],
        evidence=[ev("unauth", "No authorization", "Access computing resources only when authorized."),
                  ev("privacy", "Other people's data", "Respect privacy; avoid harm."),
                  ev("report", "Report it safely", "Screenshots and a non-clickable URL for IT.")]),
}


# SCAM / LEGITIMATE messages and pages outside Level 1 (E02 and E06 already have clues from the tutorial).
VERDICT_CLUES = {
    "E09": dict(  # Website inspection
        inspectables=[
            clue("sender", "Sender", "", "The page",
                 "A page you didn't ask for, from a site you don't know."),
            clue("alert", "Subject", "", "The alert",
                 "Big red alerts are designed to cause panic."),
            clue("locked", "Body", "INFECTED and LOCKED", "The claim",
                 "A web page can't scan your computer. It's only a page made to scare you.", "fake"),
            clue("call", "Body", "Call Certified Support at 1-800-555-0199", "What it wants",
                 "Real security warnings never ask you to call a number. This is a tech-support scam.", "call"),
            clue("restart", "Body", "DO NOT restart your computer", "The instruction",
                 "It keeps you on the scary page. Closing the browser usually ends it.", "pressure"),
        ],
        evidence=[ev("fake", "Fake infection claim", "Web pages can't see what's on your computer."),
                  ev("call", "Asks you to call 'support'", "Real warnings don't come with a phone number."),
                  ev("pressure", "Keeps you from closing it", "Pressure is the scam's main tool.")]),
    "M01": dict(  # Email analysis (LEGITIMATE)
        inspectables=[
            clue("domain", "SenderDetail", "", "Sender address",
                 "no-reply@novacloud.example matches the trusted domain in your case files.", "domain"),
            clue("expires", "Body", "This link expires in 15 minutes.", "Expiry",
                 "Short expiry times are normal for reset links - it's a security feature, not pressure."),
            clue("ignore", "Body", "If you didn't request this, you can ignore this email.", "No pressure",
                 "It lets you ignore it if you didn't ask. A scam would push you to act.", "nopressure"),
            clue("link", "Link", "", "Reset link",
                 "It goes to novacloud.example, the trusted domain - and you asked for this reset a minute ago.", "link",
                 "https://novacloud.example/reset?t=8f3k…"),
        ],
        evidence=[ev("domain", "Trusted sender domain", "Matches novacloud.example in your case files."),
                  ev("link", "Link to the real domain", "And you requested the reset yourself."),
                  ev("nopressure", "No pressure", "You can safely ignore it if you didn't ask.")]),
    "M08": dict(  # SMS inspection
        inspectables=[
            clue("thread", "SenderDetail", "", "The thread",
                 "Scammers can fake the sender name so a text lands in your real bank's thread. The thread proves nothing.", "spoof"),
            clue("today", "Body", "suspended TODAY", "Deadline",
                 "A same-day threat pushes you to act before you check.", "urgent"),
            clue("request", "Body", "Update your details at", "The request",
                 "Banks don't ask you to update your details through a text link.", "request"),
            clue("link", "Link", "", "Link",
                 "examplebank-ph.example is not examplebank.example (the trusted domain in your case files).", "domain",
                 "examplebank-ph.example/update"),
        ],
        evidence=[ev("spoof", "Sender name can be faked", "Even in your real bank's thread."),
                  ev("domain", "Wrong domain", "examplebank-ph.example isn't your bank."),
                  ev("urgent", "Same-day threat", "Pressure to act now."),
                  ev("request", "Asks for your details by link", "Banks don't do that.")]),
    "H07": dict(  # Website / form inspection
        inspectables=[
            clue("sender", "Sender", "", "The form",
                 "Shared in a student group and using the school logo. Logos are easy to copy."),
            clue("maiden", "Body", "mother's maiden name", "It asks for",
                 "A classic security-question answer, used to reset accounts.", "secq"),
            clue("selfie", "Body", "a selfie holding your ID", "It asks for",
                 "ID photos plus a selfie let someone open accounts as you.", "idtheft"),
            clue("mpin", "Body", "your PayLite MPIN", "It asks for",
                 "Your MPIN is like your e-wallet password. Nobody legitimate asks for it.", "mpin"),
            clue("notice", "Body", "There is no privacy notice.", "Privacy notice",
                 "No notice means no explanation of why the data is collected or what happens to it.", "notice"),
            clue("contact", "Body", "The only contact is a mobile number.", "Contact",
                 "No office, no official address - nobody accountable.", "notice"),
        ],
        evidence=[ev("mpin", "Asks for your MPIN", "Never share it with anyone."),
                  ev("idtheft", "ID plus selfie", "Everything needed to pretend to be you."),
                  ev("secq", "Security-question answer", "Used to take over accounts."),
                  ev("notice", "No privacy notice, no real contact", "Nobody is accountable for your data.")]),
    "H09": dict(  # Website inspection (QR code)
        inspectables=[
            clue("sticker", "Sender", "", "How you got here",
                 "The QR code was a sticker placed on top of the printed one (case notes). Someone swapped it.", "sticker"),
            clue("mpin", "Body", "MPIN", "It asks for",
                 "A web page asking for your MPIN is a phishing page. Enter it only inside the official app.", "mpin"),
            clue("link", "Link", "", "Address",
                 "paylite-merchant-pay.example is a separate domain, not the official app.", "domain",
                 "paylite-merchant-pay.example/canteen"),
        ],
        evidence=[ev("sticker", "QR sticker over the real one", "Tampered QR codes send you to fake pages."),
                  ev("mpin", "Asks for your MPIN on a web page", "Your MPIN belongs only in the app."),
                  ev("domain", "Unofficial domain", "Not PayLite's own app or site.")]),
}

# Plain case sheets on purpose: concept questions with no scenario to investigate.
PLAIN = {"E01", "E08", "M06", "H04"}
