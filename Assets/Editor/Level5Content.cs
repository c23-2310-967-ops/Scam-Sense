using System.Collections.Generic;

/// <summary>
/// Content of Level 5 - The Compromised Campus Network (MacLab): briefing, the six reported messages with their
/// clickable details and Evidence Notebook clues, and the instructor computer's records (the level's Case Files).
/// Used by Room5Builder to fill Assets/Levels/Level5_MacLab.asset; after that, edit the asset in the Inspector.
/// The setting is UPHSL Biñan's MacLab; every person, message, office address, number and domain is fictional (training
/// content, not official university communication). Domains use the reserved
/// .example suffix and are never opened. Each email is flagged on one MacLab workstation (stationId).
/// </summary>
public static class Level5Content
{
    // Flagged workstations (Room5Builder gives these ids to the matching iMacs).
    public const string StationAccount = "MAC-31", StationScholarship = "MAC-15", StationAnnouncement = "MAC-20",
                        StationAttachment = "MAC-11", StationImpersonation = "MAC-06";
    public static readonly string[] Stations = { StationAccount, StationScholarship, StationAnnouncement, StationAttachment, StationImpersonation };

    public static void Fill(LevelDefinition l)
    {
        l.levelId = "level_5";
        l.levelNumber = 5;
        l.displayName = "The Compromised Campus Network";
        l.description = "Suspicious messages have been reported in the university MacLab. Investigate the flagged workstations and decide what's real.";

        l.briefingTitle = "LEVEL 5: THE COMPROMISED CAMPUS NETWORK";
        l.briefingText = "A series of suspicious emails and messages has been reported in MacLab 3 at UPHSL Biñan. Five workstations have been flagged. Go to each one, investigate the message on screen, and decide whether it is a scam or legitimate. The instructor's computer holds the university's official contacts and policies - compare before you decide.";
        l.caseNotes = new[]
        {
            "You are a UPHSL Biñan student helping the campus security team in MacLab 3.",
            "Your campus account is active - you passed the account check last month.",
            "You did not apply for any scholarship or grant this term.",
            "Your tuition for this term is fully paid (receipt on file).",
        };
        l.objectives = new[]
        {
            "Investigate the five flagged workstations in the MacLab.",
            "Classify every reported message as SCAM or LEGITIMATE.",
            "Check the official records on the instructor's computer at the front.",
        };

        l.messages = new List<ScamMessage>
        {
            AccountVerification(),
            LabReminder(),
            Scholarship(),
            CampusAnnouncement(),
            MaliciousAttachment(),
            Impersonation(),
        };

        l.inboxExtras = new List<ScamMessage>
        {
            new ScamMessage
            {
                id = "l5_old_welcome", channel = MessageChannel.Email,
                sender = "MacLab Coordinator", senderDetail = "labs@uphsl-binan.example",
                subject = "Welcome to MacLab 3",
                body = "Welcome to MacLab 3. Please log out when you finish and report anything suspicious to security@uphsl-binan.example. Official notices are always posted on the student portal.",
            },
        };

        l.caseFiles = new List<CaseFile>
        {
            new CaseFile
            {
                title = "Campus IT Security Notice",
                body = "<b>UPHSL Biñan IT Services</b>\n\n•  We will <b>never</b> ask for your password, verification codes or personal details by email, text or chat.\n•  Account checks happen only when <b>you</b> sign in to the student portal (bookmark: portal.uphsl-binan.example).\n•  Never open unexpected attachments, and never click <b>Enable Content</b> on a document you didn't expect.\n•  Report suspicious messages to security@uphsl-binan.example.",
            },
            new CaseFile
            {
                title = "Official Contacts & Domains",
                body = "The university's only email domain is <b>uphsl-binan.example</b>.\n\n•  IT Services - it@uphsl-binan.example\n•  Student Affairs - studentaffairs@uphsl-binan.example\n•  Registrar - registrar@uphsl-binan.example\n•  Scholarships - scholarships@uphsl-binan.example\n•  Accounting - accounting@uphsl-binan.example · +1 (555) 010-2200\n•  Campus Alerts - texts from the saved contact \"UPHSL Campus Alerts\"\n\nNot official: uphsl-binan-accounts, uphsl-scholarship-awards, upshl-binan (note the spelling), and any free email service.",
            },
            new CaseFile
            {
                title = "Scholarships & Payments",
                body = "<b>Scholarships:</b> students apply through the student portal. The office never charges a fee and never asks for bank details or ID photos by email.\n\n<b>Tuition:</b> paid only through the student portal or at the campus cashier. Payment details never change by email - if in doubt, call Accounting on +1 (555) 010-2200.",
            },
            new CaseFile
            {
                title = "This Week on Campus",
                body = "•  <b>Today 1:00-4:00 PM</b> - MacLab 3 reserved for the CCS cybersecurity workshop.\n•  <b>Saturday 6:00-9:00 AM</b> - scheduled campus Wi-Fi maintenance (no action needed).\n•  Enrolment requirements for next term are posted on the student portal - not sent as attachments.",
            },
        };

        l.firstMessageDelay = 8f;
        l.delayBetweenMessages = new UnityEngine.Vector2(8f, 14f);
        l.minimumActivityDelay = 4f;

        // The hardest case: more points per answer, a larger wrong-answer penalty and a little more time pressure
        // than Level 4 - but reading time still grows with each message, so careful investigation is never punished.
        l.scoring = new LevelScoring
        {
            basePoints = 300, speedBonus = 30, fastWindow = 0.35f, wrongPenalty = 60, recoveryPoints = 70,
            baseReadingSeconds = 20f, secondsPerWord = 0.5f, secondsPerDetail = 3f, minReadingSeconds = 40f, maxReadingSeconds = 120f,
            caseExtraSeconds = 75f, overtimeStepSeconds = 10f, overtimePenaltyPerStep = 10, maxTimePenalty = 70, minCorrectPoints = 230,
        };
        // 6 messages x 300 = 1800 base (1980 with every speed bonus). Passing 1380 (~77%): each case asks a little more than the last.
        l.oneStarScore = 1;
        l.twoStarScore = 1380;
        l.threeStarScore = 1560;
        l.minScoreToUnlockNext = 1380;
        l.nextLevelId = "";        // final case
        l.nextSceneName = "";

        l.lessons = new[]
        {
            "Universities send mail from one official domain - look-alike domains and free email accounts are red flags.",
            "No real IT team asks for your password or a verification code by email or text.",
            "Grants you never applied for, fees to receive money and requests for ID photos or bank details are scams.",
            "A file ending in .exe isn't a document, and 'Enable Content' on an unexpected file is a trap.",
            "Changed bank details, threats and 'don't call us' are signs of impersonation - verify with the office's known number.",
            "Genuine notices match official records and ask for nothing risky.",
        };
        l.learningSummary = "You investigated a campus-wide phishing wave: look-alike and free-mail senders, a credential trap, a fake grant, a disguised executable and a payment-redirection scam - and you recognised the genuine notices by checking them against official records.";
        l.performanceHigh = "Outstanding! The campus network is secure - every flagged message was correctly identified.";
        l.performanceModerate = "Good investigation. Review the clues you missed - the official records separate the real notices from the fakes.";
        l.performanceLow = "Keep practising. Compare every sender, attachment and request with the university's official records.";
    }

    // ---------- Messages ----------

    static ScamMessage AccountVerification() => new ScamMessage
    {
        id = "l5_account_verification", channel = MessageChannel.Email, stationId = StationAccount,
        sender = "UPHSL IT Services", senderDetail = "it-services@uphsl-binan-accounts.example",
        subject = "Final notice: your campus account will be suspended",
        body = "Our system shows your UPHSL Campus Account failed the annual security verification. Your email, LMS and campus Wi-Fi access will be suspended in 12 hours. Verify your username and password now to keep your access.",
        link = "uphsl-binan-accounts.example/verify",
        correctVerdict = MessageVerdict.Scam,
        explanation = "The address uses uphsl-binan-accounts.example, not the university's own uphsl-binan.example. There was no earlier notice, a 12-hour suspension threat adds pressure, and it asks for your password through its own link. UPHSL IT never asks for passwords by email - check your account by opening the student portal yourself.",
        trigger = MessageTrigger.NearComputer,
        requestedAction = "Enter your username and password on a linked page within 12 hours.",
        linkNote = "uphsl-binan-accounts.example/verify (training placeholder, never opened).",
        clues = new[] { "Claims to be UPHSL IT Services.", "Deadline: 12 hours.", "Compare the domain with Official Contacts on the instructor's computer." },
        inspectables = new List<MessageInspectable>
        {
            I("name", Sender, "", "Display name", "It says 'UPHSL IT Services' - but anyone can type any display name.", "displayname"),
            I("address", Detail, "", "Sender address", "uphsl-binan-accounts.example is not the university's domain. Official mail comes only from uphsl-binan.example (Official Contacts on the instructor's computer).", "domain"),
            I("subject", Subject, "Final notice", "Subject", "'Final notice' - but there was no first notice. Pressure in the subject line is a common trick.", "pressure"),
            I("deadline", Body, "suspended in 12 hours", "Deadline", "A 12-hour suspension threat is there to make you act before you check.", "deadline"),
            I("creds", Body, "Verify your username and password", "What it asks", "It wants your username and password. The Campus IT Security Notice says IT never asks for passwords by email.", "credentials"),
            I("link", Link, "", "Link", "The link really goes to uphsl-binan-accounts.example/verify - a look-alike sign-in page, not the student portal.", "link", "https://uphsl-binan-accounts.example/verify"),
        },
        evidence = new List<MessageEvidence>
        {
            C("domain", "Look-alike sender domain", "uphsl-binan-accounts.example is not uphsl-binan.example."),
            C("pressure", "'Final notice' with no first notice", "Invented urgency stops people from checking."),
            C("deadline", "12-hour suspension threat", "Threats and short deadlines are pressure tactics."),
            C("credentials", "Asks for your password", "Real IT teams never ask for passwords by email."),
            C("link", "Link to a look-alike page", "Sign in only through the portal you already know."),
            C("displayname", "Official-sounding name", "A display name is just text - on its own it proves nothing.", false),
        },
    };

    static ScamMessage LabReminder() => new ScamMessage
    {
        id = "l5_lab_reminder", channel = MessageChannel.SMS,
        sender = "UPHSL Campus Alerts", senderDetail = "Saved contact",
        body = "UPHSL Alerts: MacLab 3 is reserved today 1:00-4:00 PM for the CCS cybersecurity workshop. Bring your student ID. No reply needed.",
        correctVerdict = MessageVerdict.Legitimate,
        scenarioContext = "Fictional training example. The workshop is on this week's campus schedule.",
        explanation = "It comes from the saved UPHSL Campus Alerts contact, matches the workshop listed in This Week on Campus, has no link and asks for nothing. This is a fictional example - an unexpected request for a code, a password or a payment would still need checking.",
        trigger = MessageTrigger.PlayerWalked,
        requestedAction = "Nothing - it's a reminder.",
        linkNote = "No link.",
        clues = new[] { "From the saved contact UPHSL Campus Alerts.", "No link, no reply needed.", "Compare with This Week on Campus." },
    };

    static ScamMessage Scholarship() => new ScamMessage
    {
        id = "l5_scholarship", channel = MessageChannel.Email, stationId = StationScholarship,
        sender = "UPHSL Scholarship & Grants Office", senderDetail = "grants@uphsl-scholarship-awards.example",
        subject = "Congratulations! You've been pre-selected for a $2,000 grant",
        body = "You have been pre-selected for the 2026 Excellence Grant. To release your funds, complete the claim form with your student number, date of birth, bank account details and a photo of your ID within 48 hours. A $15 processing fee applies.",
        link = "uphsl-scholarship-awards.example/claim",
        correctVerdict = MessageVerdict.Scam,
        explanation = "You never applied for a grant, the address is uphsl-scholarship-awards.example instead of the university's domain, it asks for your bank details and an ID photo on an unverified site, gives a 48-hour deadline and charges a fee to receive money. The real Scholarships office uses the student portal and never charges fees.",
        trigger = MessageTrigger.NearComputer,
        requestedAction = "Send student number, date of birth, bank details and an ID photo, and pay a $15 fee, within 48 hours.",
        linkNote = "uphsl-scholarship-awards.example/claim (training placeholder, never opened).",
        clues = new[] { "A grant you never applied for.", "Fee plus sensitive personal details.", "Compare with Scholarships & Payments." },
        inspectables = new List<MessageInspectable>
        {
            I("name", Sender, "", "Display name", "'UPHSL Scholarship & Grants Office' sounds official - the name alone proves nothing.", "official"),
            I("address", Detail, "", "Sender address", "uphsl-scholarship-awards.example isn't the university. The real office is scholarships@uphsl-binan.example.", "domain"),
            I("offer", Subject, "pre-selected", "The offer", "'Pre-selected' for a grant you never applied for (your case notes).", "notapplied"),
            I("data", Body, "student number, date of birth, bank account details and a photo of your ID", "What it asks", "That's enough to steal your identity and your money. Real scholarship offices never ask for this by email.", "data"),
            I("deadline", Body, "within 48 hours", "Deadline", "A short deadline leaves no time to check.", "deadline"),
            I("fee", Body, "A $15 processing fee applies", "Fee", "A genuine grant never costs money to receive.", "fee"),
            I("link", Link, "", "Link", "uphsl-scholarship-awards.example/claim is an unverified website, not the student portal.", "link", "https://uphsl-scholarship-awards.example/claim"),
        },
        evidence = new List<MessageEvidence>
        {
            C("notapplied", "A grant you never applied for", "Unexpected money is the bait."),
            C("domain", "Unofficial domain", "uphsl-scholarship-awards.example is not uphsl-binan.example."),
            C("data", "Asks for ID photo and bank details", "Identity and banking details are what scammers want."),
            C("fee", "Fee to receive money", "Real scholarships never charge to pay you."),
            C("deadline", "48-hour deadline", "Pressure to act before verifying."),
            C("link", "Unverified claim website", "Apply and claim only through the student portal."),
            C("official", "Official-sounding office name", "Names are easy to copy - check the address.", false),
        },
    };

    static ScamMessage CampusAnnouncement() => new ScamMessage
    {
        id = "l5_campus_announcement", channel = MessageChannel.Email, stationId = StationAnnouncement,
        sender = "UPHSL Office of Student Affairs", senderDetail = "studentaffairs@uphsl-binan.example",
        subject = "Campus Wi-Fi maintenance this Saturday",
        body = "The campus Wi-Fi will be unavailable on Saturday from 6:00 to 9:00 AM for scheduled maintenance. No action is needed from you. The schedule is also posted on the student portal and on the display in the MacLab.",
        correctVerdict = MessageVerdict.Legitimate,
        scenarioContext = "Fictional training example. The sender domain and the schedule match the university's official records.",
        explanation = "It comes from studentaffairs@uphsl-binan.example - the university's official domain - it describes an ordinary maintenance window that matches This Week on Campus and the notice on the MacLab display, and it asks for nothing. A fictional example, not a promise that every email in this style is safe.",
        trigger = MessageTrigger.NearComputer,
        requestedAction = "Nothing - plan around the outage.",
        linkNote = "No link. It points you to the portal and the MacLab display.",
        clues = new[] { "Specific time: Saturday 6:00-9:00 AM.", "Asks for nothing.", "Compare with the MacLab display and This Week on Campus." },
        inspectables = new List<MessageInspectable>
        {
            I("address", Detail, "", "Sender address", "studentaffairs@uphsl-binan.example uses the university's only official domain (Official Contacts).", "domain"),
            I("when", Body, "Saturday from 6:00 to 9:00 AM", "Details", "A specific, ordinary maintenance window - nothing to do but plan around it.", "specific"),
            I("noaction", Body, "No action is needed from you", "What it asks", "It asks for nothing: no link, no password, no payment.", "noask"),
            I("posted", Body, "on the display in the MacLab", "Where to check", "You can confirm it yourself - the same notice is on the display at the front of the lab.", "matches"),
            I("subject", Subject, "", "Subject", "Plain and calm. It doesn't prove anything on its own, but there's no pressure.", ""),
        },
        evidence = new List<MessageEvidence>
        {
            C("domain", "Official university domain", "uphsl-binan.example is listed in Official Contacts."),
            C("specific", "Specific, ordinary details", "A real maintenance notice names the date and time."),
            C("noask", "Asks for nothing risky", "No link, password, code or payment is requested."),
            C("matches", "Matches the lab display and records", "You can verify it independently."),
        },
    };

    static ScamMessage MaliciousAttachment() => new ScamMessage
    {
        id = "l5_malicious_attachment", channel = MessageChannel.Email, stationId = StationAttachment,
        sender = "Office of the Registrar", senderDetail = "registrar@upshl-binan.example",
        subject = "Revised enrolment requirements - open the attachment today",
        body = "Please review the attached revised enrolment requirements before your classes are affected. When the file opens, click Enable Content so the form displays correctly.\nAttachment: Enrolment_Requirements_2026.pdf.exe (48 KB)",
        correctVerdict = MessageVerdict.Scam,
        explanation = "The address is upshl-binan.example - two letters swapped compared with the real uphsl-binan.example. The 'PDF' actually ends in .exe, so it's a program, and 'Enable Content' would let hidden code run. The Registrar posts requirements on the student portal and never sends them as attachments.",
        trigger = MessageTrigger.NearComputer,
        requestedAction = "Open an attached file and click Enable Content.",
        linkNote = "No link - the danger is the attachment (simulated, never opened).",
        clues = new[] { "Unexpected attachment from 'the Registrar'.", "Asks you to Enable Content.", "Compare the domain spelling with Official Contacts." },
        inspectables = new List<MessageInspectable>
        {
            I("name", Sender, "", "Display name", "'Office of the Registrar' is a real office's name - copying it is easy.", "official"),
            I("address", Detail, "", "Sender address", "Look closely: upshl-binan.example - 'upshl' instead of 'uphsl'. The real domain is uphsl-binan.example.", "lookalike"),
            I("subject", Subject, "open the attachment today", "Subject", "An unexpected attachment plus 'today'. Requirements are posted on the student portal (This Week on Campus).", "urgent"),
            I("enable", Body, "click Enable Content", "Instruction", "'Enable Content' switches on macros - a common way documents run hidden code.", "macros"),
            I("file", Body, "Enrolment_Requirements_2026.pdf.exe", "Attachment", "It ends in .exe: a program pretending to be a PDF. Opening it could install malware. (Simulated - nothing is opened.)", "doubleext"),
        },
        evidence = new List<MessageEvidence>
        {
            C("lookalike", "Misspelled university domain", "upshl-binan.example imitates uphsl-binan.example."),
            C("doubleext", "'.pdf.exe' attachment", "A double extension hides a program inside a fake document name."),
            C("macros", "Asks you to Enable Content", "Macros in unexpected files can run malicious code."),
            C("urgent", "Unexpected attachment with urgency", "Real offices post requirements on the portal."),
            C("official", "Real office name", "A copied name proves nothing.", false),
        },
    };

    static ScamMessage Impersonation() => new ScamMessage
    {
        id = "l5_impersonation", channel = MessageChannel.Email, stationId = StationImpersonation,
        sender = "UPHSL Accounting Office", senderDetail = "uphsl.accountingoffice@quickmail.example",
        subject = "URGENT: tuition balance - pay today to avoid being dropped",
        body = "Due to an internal audit, tuition payments must now be sent to our new bank account (Account 0042-7781-9930). Settle your remaining balance today or you will be dropped from your classes. Do not call the office - our lines are busy. Reply with your payment receipt.",
        correctVerdict = MessageVerdict.Scam,
        explanation = "A university office wouldn't use a free quickmail.example address, your tuition is already paid, payment details never change by email, and 'don't call the office' stops you from checking with the real Accounting Office on +1 (555) 010-2200. Threats plus changed bank details are classic impersonation.",
        trigger = MessageTrigger.NearComputer,
        requestedAction = "Send a tuition payment to a new bank account today.",
        linkNote = "No link - it wants a bank transfer.",
        clues = new[] { "Claims to be the Accounting Office.", "New bank account and a same-day threat.", "Compare with Scholarships & Payments and your case notes." },
        inspectables = new List<MessageInspectable>
        {
            I("name", Sender, "", "Display name", "'UPHSL Accounting Office' - a name anyone can type.", "official"),
            I("address", Detail, "", "Sender address", "quickmail.example is a free email service. University offices use uphsl-binan.example.", "freemail"),
            I("bank", Body, "new bank account", "Payment change", "A sudden change of bank details is a classic payment-redirection scam. Payment details never change by email.", "newbank"),
            I("balance", Body, "Settle your remaining balance today", "The claim", "Your case notes say your tuition is already fully paid.", "paid"),
            I("threat", Body, "you will be dropped from your classes", "Threat", "A threat to drop you pushes you to pay before checking.", "threat"),
            I("nocall", Body, "Do not call the office", "No calls", "Stopping you from calling is how impostors avoid being caught. Call the number in Official Contacts.", "nocall"),
        },
        evidence = new List<MessageEvidence>
        {
            C("freemail", "Free email address", "Offices use the university domain, not free mail."),
            C("newbank", "Changed bank details", "Payment details never change by email."),
            C("paid", "Your tuition is already paid", "The claim contradicts your own records."),
            C("threat", "Threat to drop you from classes", "Fear is used to rush payment."),
            C("nocall", "'Do not call the office'", "Impostors block the one check that would expose them."),
            C("official", "Office name in the sender", "Copied names prove nothing.", false),
        },
    };

    // ---------- Builders ----------

    const InspectField Sender = InspectField.Sender, Detail = InspectField.SenderDetail, Subject = InspectField.Subject, Body = InspectField.Body, Link = InspectField.Link;

    static MessageInspectable I(string id, InspectField f, string match, string label, string finding, string clue = "", string address = "") =>
        new MessageInspectable { id = id, field = f, match = match, label = label, finding = finding, clueId = clue, linkAddress = address };
    static MessageEvidence C(string id, string title, string why, bool supports = true) =>
        new MessageEvidence { id = id, title = title, whyItMatters = why, supportsVerdict = supports };
}
