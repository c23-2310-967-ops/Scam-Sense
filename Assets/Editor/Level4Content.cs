using System.Collections.Generic;

/// <summary>
/// Content of Level 4 - Digital Forensics Lab: briefing, the eight investigation scenarios, the evidence wall
/// records (the level's Case Files) and the network analysis station's records. Used by Room4Builder to fill
/// Assets/Levels/Level4_DigitalForensicsLab.asset; after that, edit the asset / the station in the Inspector.
/// Every organisation, person, number and domain is fictional; domains use the reserved .example suffix and
/// are never opened.
/// </summary>
public static class Level4Content
{
    // Evidence wall record titles. The FinalCaseTerminal requires these three before the final case opens.
    public const string EvidenceEmail = "Initial Email";
    public const string EvidenceDomain = "Domain Record";
    public const string EvidenceFollowUp = "Follow-up Message";
    public static readonly string[] RequiredEvidence = { EvidenceEmail, EvidenceDomain, EvidenceFollowUp };

    public static void Fill(LevelDefinition l)
    {
        l.levelId = "level_4";
        l.levelNumber = 4;
        l.displayName = "Digital Forensics Lab";
        l.description = "Trace the evidence. Expose the scam. Compare emails, web pages and records before you decide.";

        l.briefingTitle = "LEVEL 4: DIGITAL FORENSICS LAB";
        l.briefingText = "Several suspicious communications have been reported to the cybersecurity team. Examine the available evidence, identify fraudulent messages, and submit your findings. Some communications are legitimate. Investigate before making your decision.";
        l.caseNotes = new[]
        {
            "You store files with NovaCloud. Your plan is paid until next month.",
            "You ordered headphones from Brightway Store (order BW-55120).",
            "You work with Vertexa Systems as a contractor.",
            "You do not have a BrightPay account.",
        };
        l.objectives = new[]
        {
            "Investigate the available case files.",
            "Classify the seven reported communications as SCAM or LEGITIMATE.",
            "Inspect the evidence wall: initial email, domain record, follow-up message.",
            "Submit your findings at the final case review terminal.",
        };

        l.messages = new List<ScamMessage>
        {
            new ScamMessage
            {
                id = "l4_account_verification", channel = MessageChannel.Email,
                sender = "NovaCloud Account Team", senderDetail = "account-team@novacloud-verify.example",
                subject = "Action needed: verify your account within 24 hours",
                body = "We could not confirm your account details. To avoid restrictions on your storage, verify your information within 24 hours using the secure link below.",
                link = "novacloud-verify.example/confirm",
                correctVerdict = MessageVerdict.Scam,
                explanation = "The display name says NovaCloud, but the address uses novacloud-verify.example, which is not NovaCloud's verified domain. It adds a 24-hour deadline and asks you to verify through its own link instead of the service you already use.",
                trigger = MessageTrigger.NearComputer,
                requestedAction = "Verify your account information through the provided link within 24 hours.",
                linkNote = "novacloud-verify.example/confirm (training placeholder, never opened).",
                clues = new[]
                {
                    "Claims to be NovaCloud.",
                    "Deadline: 24 hours, or storage is restricted.",
                    "Compare the sender domain with Verified Domains on the network station.",
                },
            },
            new ScamMessage
            {
                id = "l4_security_notification", channel = MessageChannel.Email,
                sender = "NovaCloud Security", senderDetail = "security@novacloud.example",
                subject = "Sign-in attempt blocked",
                body = "We blocked a sign-in attempt to your NovaCloud account at 09:12 from an unrecognised device. No action is needed if this wasn't you - your account is protected. To review your activity, open the NovaCloud app or go to novacloud.example yourself. We will never ask for your password or payment by email.",
                correctVerdict = MessageVerdict.Legitimate,
                scenarioContext = "Fictional training example. The sender domain and the event match the account records on the network station.",
                explanation = "The sender uses NovaCloud's verified domain and the 09:12 blocked attempt matches your account record. It asks for nothing and tells you to open the service yourself. This is a fictional example, not a promise that any email in this format is safe - still check important alerts in the official app.",
                trigger = MessageTrigger.NearComputer,
                requestedAction = "Nothing. Optionally review activity in the app or at the address you already know.",
                linkNote = "No link. It points you to the app or the known address.",
                clues = new[]
                {
                    "Describes a specific event: blocked sign-in at 09:12.",
                    "Asks for no password, code or payment.",
                    "Compare with Account & Order Records on the network station.",
                },
            },
            new ScamMessage
            {
                id = "l4_payment_recovery", channel = MessageChannel.SMS,
                sender = "BrightPay Support", senderDetail = "+1 (555) 014-3392",
                body = "BrightPay: your account is frozen with $480 waiting. To release the funds, pay a $25 recovery processing fee today at brightpay-release.example. Case ref: unavailable.",
                link = "brightpay-release.example",
                correctVerdict = MessageVerdict.Scam,
                explanation = "You don't have a BrightPay account, the message comes from an ordinary number, it gives no case reference you can check, and it asks for a fee to release money. Unexpected fees to recover funds or access are a classic scam.",
                trigger = MessageTrigger.PlayerWalked,
                requestedAction = "Pay a $25 processing fee to release frozen funds.",
                linkNote = "brightpay-release.example (training placeholder, never opened).",
                clues = new[]
                {
                    "Sent from an ordinary phone number.",
                    "No verifiable case reference.",
                    "Asks for a fee to get money back.",
                },
            },
            new ScamMessage
            {
                id = "l4_impersonated_announcement", channel = MessageChannel.Email,
                sender = "Vertexa Systems", senderDetail = "news@vertexa-systems-update.example",
                subject = "Required: install the new device security tool",
                body = "All Vertexa staff and contractors must install the new device security tool by Friday. Download it from the update portal below and sign in with your work password. Questions: call +1 (555) 019-2274.",
                link = "vertexa-systems-update.example/portal",
                correctVerdict = MessageVerdict.Scam,
                explanation = "The display name is correct, but the real address is vertexa-systems-update.example, not Vertexa's documented domain vertexa.example. The phone number doesn't match the company profile either. Always read the full address and verify through the company's known contacts.",
                trigger = MessageTrigger.NearComputer,
                requestedAction = "Download a tool from a portal and sign in with your work password.",
                linkNote = "vertexa-systems-update.example/portal (training placeholder, never opened).",
                clues = new[]
                {
                    "Display name: Vertexa Systems.",
                    "Asks for your work password on a download portal.",
                    "Compare the domain and phone number with Verified Domains.",
                },
            },
            new ScamMessage
            {
                id = "l4_order_update", channel = MessageChannel.SMS,
                sender = "Brightway Store", senderDetail = "Saved contact",
                body = "Brightway: your order BW-55120 (wireless headphones) has shipped and should arrive Thursday. Track it in the Brightway app or your account at brightway.example.",
                correctVerdict = MessageVerdict.Legitimate,
                scenarioContext = "You ordered headphones from Brightway Store. The reference matches the order record on the network station.",
                explanation = "The order number, product and store all match your purchase record, it comes from the saved contact, and it only points you to the app or the store's known address. Context matters - but an unexpected request for payment or a password would still need checking.",
                trigger = MessageTrigger.PlayerWalked,
                requestedAction = "Nothing; optionally track the order in the app you already use.",
                linkNote = "No link to tap - it names the store's known address.",
                clues = new[]
                {
                    "Order BW-55120, wireless headphones.",
                    "From a saved contact.",
                    "Compare with Account & Order Records.",
                },
            },
            new ScamMessage
            {
                id = "l4_prize_claim", channel = MessageChannel.Email,
                sender = "Lumen Rewards", senderDetail = "winners@lumen-rewards-prize.example",
                subject = "Congratulations - you've won a tablet!",
                body = "You were selected as this month's winner of a new tablet! To release your prize, pay the $4.99 delivery fee and confirm your date of birth and card details on the claim page within 48 hours.",
                link = "lumen-rewards-prize.example/claim",
                correctVerdict = MessageVerdict.Scam,
                explanation = "You never entered a Lumen Rewards draw, the prize needs an upfront fee, and it asks for your date of birth and card details. Unexpected prizes that need payment or sensitive information are warning signs.",
                trigger = MessageTrigger.NearComputer,
                requestedAction = "Pay a delivery fee and give date of birth and card details within 48 hours.",
                linkNote = "lumen-rewards-prize.example/claim (training placeholder, never opened).",
                clues = new[]
                {
                    "Prize from a competition you don't recall entering.",
                    "Upfront fee plus sensitive personal details.",
                    "Deadline: 48 hours.",
                },
            },
            new ScamMessage
            {
                id = "l4_suspicious_website", channel = MessageChannel.Website,
                sender = "NovaCloud - Sign in", senderDetail = "novacl0ud-login.example",
                subject = "Captured page: NovaCloud sign-in",
                body = "NOVACLOUD\nSign in to keep your files safe.\nEmail address  [__________]\nPassword  [__________]\nCard number (identity check)  [__________]\n[ SIGN IN ]\n\nFooter: (c) 2026 Nova Cloud Services Ltd  -  Help: help@novacloud-assist.example  -  (padlock icon shown)",
                link = "https://novacl0ud-login.example/signin",
                correctVerdict = MessageVerdict.Scam,
                explanation = "The page copies NovaCloud's look, but the domain novacl0ud-login.example is not NovaCloud's, the company name in the footer differs, the help address uses yet another domain, and it asks for a card number to sign in. A padlock or https only means the connection is encrypted - not that the site is who it claims to be.",
                trigger = MessageTrigger.NearComputer,
                requestedAction = "Enter your email, password and card number to sign in.",
                linkNote = "novacl0ud-login.example (training placeholder, never opened). The padlock only means the connection is encrypted.",
                clues = new[]
                {
                    "Address: novacl0ud-login.example.",
                    "Footer company: Nova Cloud Services Ltd; help: novacloud-assist.example.",
                    "Asks for a card number to sign in.",
                },
            },
            new ScamMessage
            {
                id = "l4_final_case", channel = MessageChannel.Case,
                sender = "Case Review", senderDetail = "CASE-04 · Billing Storm",
                subject = "CASE-04  ·  Operation Billing Storm",
                body = "A wave of \"payment failed\" messages has reached staff who use NovaCloud. Using the initial email, the domain record and the follow-up message, decide: is this campaign a scam or legitimate billing contact from NovaCloud?",
                correctVerdict = MessageVerdict.Scam,
                explanation = "Several independent indicators agree: the email comes from novacloud-support.example, which is not NovaCloud's verified domain; that domain was registered 3 days ago with a hidden owner and shares a server with the fake sign-in page; the follow-up text from an unknown number repeats the same link and pressure; and your account record shows the plan is paid. No single clue decides it - together they do.",
                trigger = MessageTrigger.TimeOnly,
                clues = new[]
                {
                    "Email sender: billing@novacloud-support.example - NovaCloud's verified domain is novacloud.example.",
                    "Domain registered 3 days ago, owner hidden, same server as novacl0ud-login.example.",
                    "Follow-up text from an unknown number with the same link and a 'final notice'.",
                    "Your NovaCloud plan is paid until next month.",
                },
            },
        };

        l.inboxExtras = new List<ScamMessage>
        {
            new ScamMessage
            {
                id = "l4_old_welcome", channel = MessageChannel.Email,
                sender = "Forensics Team Lead", senderDetail = "lead@cyberlab.example",
                subject = "Your first case in the lab",
                body = "Welcome to the forensics lab. Reported messages arrive here and on your phone. The evidence wall and the network analysis station hold the records you need - compare before you classify. The final case review terminal unlocks once the evidence is in.",
            },
            new ScamMessage
            {
                id = "l4_old_receipt", channel = MessageChannel.Email,
                sender = "Brightway Store", senderDetail = "orders@brightway.example",
                subject = "Receipt for order BW-55120",
                body = "Thanks for your order: wireless headphones, paid by card. Track it in the Brightway app or at brightway.example.",
            },
        };

        // Evidence wall (the level's Case Files) - the final case's three required records plus case notes.
        l.caseFiles = new List<CaseFile>
        {
            new CaseFile
            {
                title = EvidenceEmail,
                body = "<b>From:</b> NovaCloud Billing  <billing@novacloud-support.example>\n<b>Received:</b> today 08:41\n<b>Subject:</b> Payment failed - update your card within 12 hours\n\n\"Your storage plan payment failed. Update your card within 12 hours at novacloud-support.example/billing or your files will be deleted.\"\n\n<b>Asks for:</b> card details on a linked page\n<b>Link:</b> novacloud-support.example/billing (training placeholder)",
            },
            new CaseFile
            {
                title = EvidenceDomain,
                body = "<b>Domain:</b> novacloud-support.example\n<b>Registered:</b> 3 days ago\n<b>Owner:</b> hidden (privacy service)\n<b>Server:</b> 203.0.113.48 - also hosts novacl0ud-login.example (the fake sign-in page)\n<b>Listed by NovaCloud:</b> no - NovaCloud lists only novacloud.example\n\nThis is a fictional training record; it does not describe any real domain.",
            },
            new CaseFile
            {
                title = EvidenceFollowUp,
                body = "<b>Text message from:</b> +1 (555) 017-8820 (not in contacts)\n<b>Received:</b> today 10:05\n\n\"NovaCloud FINAL NOTICE: billing failed. Update at novacloud-support.example/billing or lose all files today.\"\n\n<b>Same link as the email:</b> yes\n<b>Pressure:</b> 'final notice', 'lose all files today'",
            },
            new CaseFile
            {
                title = "Case Notes",
                body = "•  Reports came from 14 staff between 08:40 and 10:10.\n•  All messages point to the same domain.\n•  NovaCloud's billing page is inside its app; it doesn't delete files over one failed payment.\n•  Compare the records on the network analysis station before submitting the final case.",
            },
        };

        l.firstMessageDelay = 10f;
        l.delayBetweenMessages = new UnityEngine.Vector2(10f, 20f);
        l.minimumActivityDelay = 5f;

        // Existing scoring rules: up to 100 points per message, 8 messages = 800. Thresholds keep Level 2/3's proportions.
        l.oneStarScore = 1;
        l.twoStarScore = 400;
        l.threeStarScore = 640;
        l.minScoreToUnlockNext = 400;
        l.nextLevelId = "level_5";
        l.nextSceneName = "";   // Level 5 doesn't exist: the results show NEXT ROOM: COMING SOON

        l.lessons = new[]
        {
            "A familiar display name proves nothing - read the full sender address and compare it with verified records.",
            "Security alerts can be genuine: check whether the details match your records and whether it asks for anything.",
            "Fees to recover money, release funds or claim a prize are major warning signs.",
            "A professional page or a padlock doesn't prove a site is real - check the domain and verify the organisation.",
            "Strong conclusions come from several independent clues pointing the same way.",
        };
        l.learningSummary = "You compared communications against verified domains and account records, exposed impersonation, fee scams and a fake sign-in page, recognised genuine notices, and tied several pieces of evidence together to close a campaign.";
        l.performanceHigh = "Outstanding forensic work! Every conclusion was backed by the evidence.";
        l.performanceModerate = "Good investigation. Review the explanations - the records hold the clues that separate real from fake.";
        l.performanceLow = "Keep practising. Compare each sender, domain and request with the records before you decide.";
    }

    /// <summary>The network analysis station's records (shown on its own screen, editable on the station in the Inspector).</summary>
    public static List<CaseFile> NetworkStationPages() => new List<CaseFile>
    {
        new CaseFile
        {
            title = "Verified Domains",
            body = "Organisations' documented domains (fictional):\n•  NovaCloud - <b>novacloud.example</b> (security@, billing inside the app)\n•  Vertexa Systems - <b>vertexa.example</b> · phone +1 (555) 010-4400\n•  Brightway Store - <b>brightway.example</b> · texts from the saved contact\n•  BrightPay - <b>brightpay.example</b> (you have no account)\n\nNot verified: novacloud-verify, novacloud-support, novacl0ud-login, novacloud-assist, vertexa-systems-update, lumen-rewards-prize, brightpay-release.",
        },
        new CaseFile
        {
            title = "Account & Order Records",
            body = "<b>NovaCloud:</b> plan paid until next month · today 09:12 sign-in attempt from an unrecognised device - <b>blocked</b>\n<b>Brightway Store:</b> order <b>BW-55120</b>, wireless headphones, paid · shipped today, due Thursday\n<b>BrightPay:</b> no account on record\n<b>Lumen Rewards:</b> no competition entries on record",
        },
        new CaseFile
        {
            title = "Message Timeline",
            body = "08:41  Email  billing@novacloud-support.example - 'payment failed'\n09:12  Account  NovaCloud blocks a sign-in attempt\n09:13  Email  security@novacloud.example - 'sign-in attempt blocked'\n09:40  Web  novacl0ud-login.example reported by staff\n10:05  Text  +1 (555) 017-8820 - 'final notice' with the same link",
        },
        new CaseFile
        {
            title = "Relationship Map",
            body = "<b>Linked:</b>\nbilling email  ->  <b>novacloud-support.example</b>  <-  follow-up text\nnovacloud-support.example  ->  server 203.0.113.48  <-  novacl0ud-login.example (fake sign-in)\n\n<b>Not linked:</b>\nsecurity@novacloud.example  ->  matches the 09:12 account record (separate, genuine)\n\nServer address is from a reserved documentation range; all records are fictional.",
        },
    };
}
