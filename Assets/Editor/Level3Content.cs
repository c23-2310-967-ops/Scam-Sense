using System.Collections.Generic;

/// <summary>
/// Content of Level 3 - The Digital Trap: briefing, objectives, the six messages, case files and results text.
/// Used once by Room3Builder to fill Assets/Levels/Level3_TheDigitalTrap.asset. After that, edit the asset in
/// the Inspector (this file is not read at runtime). All organisations are fictional; domains use the
/// reserved .example suffix and are never opened.
/// </summary>
public static class Level3Content
{
    public static void Fill(LevelDefinition l)
    {
        l.levelId = "level_3";   // same save key as the old placeholder card
        l.levelNumber = 3;
        l.displayName = "The Digital Trap";
        l.description = "Convincing messages in a cybersecurity operations room. Compare each one with the evidence before you decide.";

        l.briefingTitle = "LEVEL 3 — THE DIGITAL TRAP";
        l.briefingText = "Welcome to the next stage of your investigation. These messages are more convincing, and some contain only subtle warning signs. Examine the sender, verify the context, and investigate suspicious requests before making your decision.";
        l.caseNotes = new[]
        {
            "This morning you signed in on a new workstation, OPS-WS3.",
            "You ordered a wireless headset (order ED-48213). Delivery is due tomorrow.",
            "You requested a password change for your Example Cloud Service account.",
            "The case-files computer by the west wall lists trusted contacts, orders, account activity and the IT policy.",
        };
        l.objectives = new[]
        {
            "Investigate six messages.",
            "Classify each message as SCAM or LEGITIMATE.",
            "Examine suspicious details before answering.",
            "Complete the investigation to unlock the next room.",
        };

        l.messages = new List<ScamMessage>
        {
            new ScamMessage
            {
                id = "l3_signin_alert", channel = MessageChannel.Email,
                sender = "Example Cloud Security", senderDetail = "security-alerts@examplecloud.example",
                subject = "Unusual Sign-In Activity",
                body = "We noticed a sign-in to your account from a new device. If this was you, no action is needed. If you do not recognize this activity, open the official application and review your recent sign-ins.",
                correctVerdict = MessageVerdict.Legitimate,
                scenarioContext = "The notification comes from the fictional cloud service used by the player. Its sender information matches the trusted service details provided in the investigation environment.",
                explanation = "Security alerts can be legitimate. The message provides a safe way to investigate the activity through the official application and does not pressure you to disclose your password.",
                trigger = MessageTrigger.PlayerWalked,
                requestedAction = "Nothing if the sign-in was you; otherwise review recent sign-ins in the official app.",
                linkNote = "No link. It tells you to open the official application yourself.",
                clues = new[]
                {
                    "Reports a sign-in from a new device.",
                    "Does not ask for a password, code or payment.",
                    "Compare the sender's domain and the sign-in with the case files.",
                },
            },
            new ScamMessage
            {
                id = "l3_lookalike_bank", channel = MessageChannel.Email,
                sender = "Example Bank Security", senderDetail = "security@examplebank-protect.example",
                subject = "Action Required: Account Protection",
                body = "Our monitoring system detected unusual activity. To prevent temporary restrictions, confirm your account information through our verification page immediately.",
                link = "examplebank-protect.example/verify",
                correctVerdict = MessageVerdict.Scam,
                explanation = "The sender's domain does not match the verified bank domain, and the message pressures you to act quickly. Do not use the supplied link. Contact the bank through its official application or a verified website.",
                trigger = MessageTrigger.NearComputer,
                requestedAction = "Confirm your account information on a verification page, immediately.",
                linkNote = "examplebank-protect.example/verify (training placeholder, never opened).",
                clues = new[]
                {
                    "The display name says Example Bank Security.",
                    "Warns of restrictions unless you act immediately.",
                    "Compare the full sender domain with the Trusted Contacts file.",
                },
            },
            new ScamMessage
            {
                id = "l3_it_password", channel = MessageChannel.Email,
                sender = "IT Support Desk", senderDetail = "it.support@yourcompany.example",
                subject = "Required Security Verification",
                body = "We are updating our security records. Please reply with your current password so we can confirm your account and avoid interruption to your access.",
                correctVerdict = MessageVerdict.Scam,
                explanation = "Legitimate support teams should not ask you to disclose your password by email. Verify the request through an established IT support channel and never send credentials in a message.",
                trigger = MessageTrigger.PlayerWalked,
                requestedAction = "Reply to this email with your current password.",
                linkNote = "No link. The reply goes back to the sender.",
                clues = new[]
                {
                    "The address shows the company domain - addresses can be spoofed.",
                    "Warns that your access may be interrupted.",
                    "Check what the IT Security Policy says about passwords.",
                },
            },
            new ScamMessage
            {
                id = "l3_delivery_sms", channel = MessageChannel.SMS,
                sender = "Example Delivery", senderDetail = "Saved contact",
                body = "Your delivery is scheduled for tomorrow. Your order reference is included in your official order history. Open the delivery app you normally use to view the latest status.",
                correctVerdict = MessageVerdict.Legitimate,
                scenarioContext = "The player previously placed an order in the simulated environment. The reference number matches the order shown in the trusted order history.",
                explanation = "The message matches a transaction you initiated, and its information is consistent with the order record. Checking through the official delivery app is safer than relying on an unfamiliar link.",
                trigger = MessageTrigger.PlayerWalked,
                requestedAction = "Check the delivery status in the delivery app you already use.",
                linkNote = "No link.",
                clues = new[]
                {
                    "Notification preview shows order reference ED-48213.",
                    "Sent from a saved contact.",
                    "Compare the reference with the Orders file.",
                },
            },
            new ScamMessage
            {
                id = "l3_exec_giftcards", channel = MessageChannel.Email,
                sender = "Operations Director", senderDetail = "ops.director@yourcompany-exec.example",
                subject = "Confidential — Urgent Request",
                body = "I need you to purchase several digital gift vouchers for a client meeting. Please keep this confidential and send the redemption codes as soon as possible. I am currently unavailable for a call.",
                correctVerdict = MessageVerdict.Scam,
                explanation = "The request combines urgency, secrecy, an unusual payment method, and avoidance of normal verification. Confirm unusual financial requests through a separate, trusted communication channel.",
                trigger = MessageTrigger.NearComputer,
                requestedAction = "Buy digital gift vouchers and email the redemption codes.",
                linkNote = "No link.",
                clues = new[]
                {
                    "Signed with a job title only.",
                    "Asks you to keep it confidential and says a call isn't possible.",
                    "Compare the domain with your company's domain.",
                },
            },
            new ScamMessage
            {
                id = "l3_password_confirm", channel = MessageChannel.Email,
                sender = "Example Cloud Service", senderDetail = "no-reply@examplecloud.example",
                subject = "Password Change Confirmation",
                body = "The password change you requested has been completed. If you made this change, no further action is needed. If you did not request it, open the official application and review your account security.",
                correctVerdict = MessageVerdict.Legitimate,
                scenarioContext = "The player requested a password change in the simulated investigation setup.",
                explanation = "The notification matches an action you initiated and provides a safe method to review your account. If the change were unexpected, you should investigate through the official application.",
                trigger = MessageTrigger.NearComputer,
                requestedAction = "Nothing if you made the change; otherwise review security in the official app.",
                linkNote = "No link.",
                clues = new[]
                {
                    "Confirms a password change.",
                    "Suggests opening the official application yourself.",
                    "Compare with the Account Activity file.",
                },
            },
        };

        l.inboxExtras = new List<ScamMessage>
        {
            new ScamMessage
            {
                id = "l3_old_welcome", channel = MessageChannel.Email,
                sender = "Security Team", senderDetail = "security@yourcompany.example",
                subject = "Welcome to the operations room",
                body = "Your workstation OPS-WS3 is ready. The case-files computer by the west wall holds trusted contacts, order history, account activity and the IT policy. Compare every flagged message with it before you decide.",
            },
            new ScamMessage
            {
                id = "l3_old_statement", channel = MessageChannel.Email,
                sender = "Example Bank", senderDetail = "statements@examplebank.example",
                subject = "Your monthly statement is ready",
                body = "Your statement is available in the Example Bank app. We will never ask for your password or full card number by email.",
            },
            new ScamMessage
            {
                id = "l3_old_order", channel = MessageChannel.Email,
                sender = "Example Delivery", senderDetail = "orders@exampledelivery.example",
                subject = "Order ED-48213 confirmed",
                body = "Thanks for your order: wireless headset. We'll text you from our saved number when it's on its way.",
            },
        };

        l.caseFiles = new List<CaseFile>
        {
            new CaseFile
            {
                title = "Trusted Contacts",
                body = "Verified sender domains (from your onboarding pack):\n" +
                       "•  Example Bank — <b>examplebank.example</b>\n" +
                       "•  Example Cloud Service / Example Cloud Security — <b>examplecloud.example</b>\n" +
                       "•  Example Delivery — <b>exampledelivery.example</b>, texts from the saved contact \"Example Delivery\"\n" +
                       "•  Your company — <b>yourcompany.example</b> (all staff, including IT and directors)\n\n" +
                       "Compare the whole domain after the @, letter by letter. A display name can say anything.",
            },
            new CaseFile
            {
                title = "Orders",
                body = "Your recent orders and transactions:\n" +
                       "•  <b>ED-48213</b> — Wireless headset — Example Delivery — out for delivery, arriving tomorrow\n" +
                       "•  ED-47790 — Phone case — Example Delivery — delivered last week\n" +
                       "•  Example Bank — no pending payments, no open support requests\n" +
                       "•  Company purchasing — no voucher or gift card purchases on record",
            },
            new CaseFile
            {
                title = "Account Activity",
                body = "Example Cloud Service — today:\n" +
                       "•  08:15  Sign-in from a new device: <b>OPS-WS3</b> (this operations workstation)\n" +
                       "•  08:42  Password change requested by you\n" +
                       "•  08:43  Password change completed\n\n" +
                       "Example Bank — today:\n" +
                       "•  No sign-ins or account changes",
            },
            new CaseFile
            {
                title = "IT Policy",
                body = "IT Security Policy (fictional):\n" +
                       "•  IT Support will never ask for your password — not by email, phone or chat.\n" +
                       "•  Password resets happen only in the self-service portal you already use.\n" +
                       "•  Any purchase or payment needs approval in the finance system, never by email alone.\n" +
                       "•  Gift cards and vouchers are not a payment method for company business.\n" +
                       "•  Report suspicious messages to the security team with the Report button.",
            },
            new CaseFile
            {
                title = "Notes",
                body = "Investigator notes — Case 03:\n" +
                       "•  The Operations Director is travelling today but can be reached on the company chat and phone.\n" +
                       "•  Earlier lookalike domains added a word or a hyphen next to a real brand name.\n" +
                       "•  A real alert tells you to use the official app; a fake one sends you to its own page.\n" +
                       "•  Ask yourself: did I start this? Does it match my records?",
            },
        };

        l.firstMessageDelay = 10f;
        l.delayBetweenMessages = new UnityEngine.Vector2(10f, 20f);
        l.minimumActivityDelay = 5f;

        // 6 messages, 100 points max each = 600. Passing 435 (about 73%): each room asks for a higher share than the last.
        l.oneStarScore = 1;
        l.twoStarScore = 435;
        l.threeStarScore = 520;
        l.minScoreToUnlockNext = 435;
        l.nextLevelId = "level_4";
        l.nextSceneName = "";   // Level 4 isn't built yet: the results screen shows NEXT ROOM: COMING SOON

        l.lessons = new[]
        {
            "Security alerts can be real: judge them by what they ask and where they send you.",
            "Compare the full sender address with trusted records - display names are easy to fake.",
            "No genuine support team needs your password, even from a familiar-looking address.",
            "Urgency, secrecy and gift cards together mean: verify through a separate channel.",
            "Messages that match actions you started are more likely legitimate - still check in the official app.",
        };
        l.learningSummary = "You compared messages against trusted records, spotted a lookalike domain, a credential request and an executive impersonation, and recognised genuine alerts that matched your own activity.";
        l.performanceHigh = "Outstanding analysis! You weighed every clue against the evidence and made sharp decisions.";
        l.performanceModerate = "Solid investigation. Review the explanations - the subtle cases reward checking the case files.";
        l.performanceLow = "Keep practising. Compare each sender and request with the case files before you decide.";
    }
}
