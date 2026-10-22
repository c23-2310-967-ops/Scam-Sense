using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Scam Sense > Investigation > Apply Message Investigation Data.
/// Adds clickable details and the clues they reveal to the existing Level 1 and Level 2 messages (their text is
/// not changed), and adds one work-chat message to Level 2. Safe to run again: it replaces only the investigation
/// lists of the messages it knows and adds the chat message once. All content is fictional; links are never opened.
/// </summary>
public static class MessageInvestigationContent
{
    const string Level1 = "Assets/Levels/Level1_TheFirstCase.asset";
    const string Level2 = "Assets/Levels/Level2_TheInvestigation.asset";

    [MenuItem("Tools/Scam Sense/Investigation/Apply Message Investigation Data")]
    public static void Apply()
    {
        int n = 0;
        n += ApplyTo(Level1, Level1Data());
        var l2 = AssetDatabase.LoadAssetAtPath<LevelDefinition>(Level2);
        if (l2 != null && !l2.messages.Exists(m => m.id == "l2_it_chat"))
        {
            l2.messages.Add(ItChat());
            var notes = new List<string>(l2.caseNotes ?? new string[0]) { "This morning the company announced a scheduled password change, due by Friday." };
            l2.caseNotes = notes.ToArray();
            EditorUtility.SetDirty(l2);
            Debug.Log("MessageInvestigationContent: added the work-chat message l2_it_chat to Level 2.");
        }
        n += ApplyTo(Level2, Level2Data());
        AssetDatabase.SaveAssets();
        Debug.Log($"MessageInvestigationContent: investigation data applied to {n} message(s).");
    }

    static int ApplyTo(string path, Dictionary<string, (List<MessageInspectable> parts, List<MessageEvidence> clues)> data)
    {
        var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
        if (level == null) { Debug.LogError("MessageInvestigationContent: missing " + path); return 0; }
        int n = 0;
        foreach (var m in level.messages)
        {
            if (!data.TryGetValue(m.id, out var d)) continue;
            m.inspectables = d.parts;
            m.evidence = d.clues;
            foreach (var p in InvestigationSession.Validate(m)) Debug.LogWarning($"{m.id}: {p}");
            n++;
        }
        EditorUtility.SetDirty(level);
        return n;
    }

    // ---------- Builders ----------

    static MessageInspectable I(string id, InspectField f, string match, string label, string finding, string clue = "", string address = "") =>
        new MessageInspectable { id = id, field = f, match = match, label = label, finding = finding, clueId = clue, linkAddress = address };
    static MessageEvidence C(string id, string title, string why, bool supports = true) =>
        new MessageEvidence { id = id, title = title, whyItMatters = why, supportsVerdict = supports };
    static (List<MessageInspectable>, List<MessageEvidence>) D(List<MessageInspectable> p, List<MessageEvidence> c) => (p, c);

    const InspectField Sender = InspectField.Sender, Detail = InspectField.SenderDetail, Subject = InspectField.Subject, Body = InspectField.Body, Link = InspectField.Link;

    static Dictionary<string, (List<MessageInspectable>, List<MessageEvidence>)> Level1Data() => new Dictionary<string, (List<MessageInspectable>, List<MessageEvidence>)>
    {
        ["l1_package_hold"] = D(new List<MessageInspectable>
        {
            I("sender", Sender, "", "Sender name", "Shown only as 'Unknown Number' - it isn't in your contacts and doesn't name any delivery company.", "unknown"),
            I("number", Detail, "", "Phone number", "+1 (555) 014-2290 is an ordinary mobile number. Real couriers normally text from a named sender or short code.", "number"),
            I("heading", Body, "Delivery Alert", "Heading", "'Delivery Alert' sounds official - but anyone can type a heading.", "heading"),
            I("hold", Body, "Your package is on hold", "The claim", "It never says which package, which company or which order.", "vague"),
            I("details", Body, "confirm your details", "What it asks", "It wants you to 'confirm your details' through a text link. Couriers don't need you to re-enter personal details this way.", "details"),
            I("link", Link, "", "Link", "parcel-redelivery.example is not a courier you use, and the link is plain http (not secure).", "link", "http://parcel-redelivery.example/confirm"),
        }, new List<MessageEvidence>
        {
            C("unknown", "Unknown sender", "The text doesn't come from any company you recognise."),
            C("number", "Ordinary mobile number", "Delivery companies don't usually text from random mobile numbers."),
            C("vague", "No order or company named", "A real delivery update names the company and your parcel."),
            C("details", "Asks you to confirm personal details", "Scam texts collect personal and payment details through links."),
            C("link", "Unfamiliar, insecure link", "parcel-redelivery.example isn't a courier you use, and it's plain http."),
            C("heading", "Official-sounding heading", "Anyone can write 'Delivery Alert' - on its own it isn't evidence.", false),
        }),
        ["l1_bank_verify"] = D(new List<MessageInspectable>
        {
            I("sender", Sender, "", "Sender", "The display name claims to be Example Bank's security team, but no address is shown to back it up - names are easy to fake."),
            I("subject", Subject, "Immediate", "Subject line", "'Immediate' action required: pressure to act before you think.", "urgent"),
            I("issue", Body, "We detected an issue with your account", "The problem", "It never says what the issue is or which account.", "vague"),
            I("threat", Body, "Your access may be restricted today", "Threat", "Threatens to restrict your access today unless you act.", "threat"),
            I("verify", Body, "verify your information", "What it asks", "Asks you to verify your information through a link. Banks tell you to sign in through their app or official site yourself.", "request"),
            I("link", Link, "", "Link", "examplebank-verify.example is not Example Bank's own site - it's a separate look-alike domain built to collect logins.", "link", "http://examplebank-verify.example/login"),
        }, new List<MessageEvidence>
        {
            C("urgent", "'Immediate' action required", "Urgency stops you from checking first."),
            C("threat", "Threat to restrict your account today", "Scammers use threats to rush you."),
            C("vague", "Vague 'issue' with no details", "Real security emails say what happened."),
            C("request", "Asks you to verify information via a link", "A bank won't ask you to verify details through an email link."),
            C("link", "Look-alike bank domain", "examplebank-verify.example isn't the bank's real site."),
        }),
        ["l1_order_confirmed"] = D(new List<MessageInspectable>
        {
            I("sender", Sender, "", "Sender", "Example Delivery - the delivery service for the order you placed. (Names can be faked, so check the rest too.)", "known"),
            I("confirmed", Body, "Your order has been confirmed", "What it says", "It confirms the order you placed earlier - it matches something you actually did.", "matches"),
            I("date", Body, "Your estimated delivery date is tomorrow", "Delivery date", "Just information: no deadline, threat or payment.", "noask"),
            I("app", Body, "view your order status in the official delivery app", "Next step", "It sends you to the official app you already use - there's no link to click.", "app"),
        }, new List<MessageEvidence>
        {
            C("matches", "Matches your real order", "You did place an order, so this message is expected."),
            C("noask", "Asks for nothing", "No payment, details, link or deadline."),
            C("app", "Points to the official app", "Using the app yourself is the safe way to check."),
            C("known", "From the delivery service you used", "Consistent with your order - supporting evidence, not proof on its own."),
        }),
        ["l1_prize"] = D(new List<MessageInspectable>
        {
            I("sender", Sender, "", "Sender", "'Rewards Center' - not a company you know, and no address is shown.", "unknown"),
            I("subject", Subject, "You Won a Special Prize!", "Subject line", "You 'won' a prize for a contest you never entered.", "neverentered"),
            I("free", Body, "free premium reward", "The offer", "A free premium reward out of nowhere - too good to be true.", "toogood"),
            I("now", Body, "Claim your prize immediately", "Urgency", "Pushes you to claim immediately.", "urgency"),
            I("nopurchase", Body, "No purchase is required!", "Reassurance", "'No purchase required' is meant to lower your guard.", "nopurchase"),
            I("link", Link, "", "Link", "rewards-claim.example isn't connected to anything you use. Prize links like this lead to pages asking for personal or card details.", "link", "http://rewards-claim.example/prize"),
        }, new List<MessageEvidence>
        {
            C("neverentered", "A prize you never entered for", "You can't win a contest you didn't enter."),
            C("unknown", "Unknown sender", "'Rewards Center' isn't a company you deal with."),
            C("toogood", "Too good to be true", "Free premium rewards out of nowhere are classic bait."),
            C("urgency", "'Claim immediately'", "Urgency stops you from thinking it through."),
            C("link", "Unknown prize-claim link", "The link isn't connected to any company you use."),
            C("nopurchase", "'No purchase required'", "Real promotions say this too - on its own it proves nothing.", false),
        }),
        ["l1_password_done"] = D(new List<MessageInspectable>
        {
            I("sender", Sender, "", "Sender", "Example Cloud Service - the service you changed your password on.", "service"),
            I("subject", Subject, "", "Subject line", "A calm, factual subject - no urgency or threats."),
            I("requested", Body, "The password update you requested", "What it refers to", "It refers to the password change you really made during setup.", "matches"),
            I("noaction", Body, "no further action is needed", "What it asks", "Nothing - no link, code or password is requested.", "noask"),
            I("official", Body, "open the official application", "Advice", "If it wasn't you, it tells you to open the official app yourself - not to click a link.", "official"),
        }, new List<MessageEvidence>
        {
            C("matches", "Matches the change you made", "You did request a password change, so this notice is expected."),
            C("noask", "Asks for nothing", "No link, code or password is requested."),
            C("official", "Tells you to use the official app", "Genuine services send you to their own app, not a link."),
            C("service", "From the service you used", "Consistent with your change - supporting evidence, not proof on its own."),
        }),
    };

    static Dictionary<string, (List<MessageInspectable>, List<MessageEvidence>)> Level2Data() => new Dictionary<string, (List<MessageInspectable>, List<MessageEvidence>)>
    {
        ["l2_updated_invoice"] = D(new List<MessageInspectable>
        {
            I("address", Detail, "", "Sender address", "billing@northstar-supplies.example - the supplier you really ordered from.", "domain"),
            I("order", Body, "your recent office supply order", "Reference", "It refers to the office supply order you placed.", "matches"),
            I("cost", Body, "updated invoice reflecting the final shipping cost", "Money", "It mentions an updated cost.", "money"),
            I("account", Body, "review the invoice in your existing customer account", "Where to look", "It points you to your existing account - no attachment or unfamiliar link.", "account"),
            I("contact", Body, "contact our billing team through the official website", "Contact", "Questions go through the official website.", "official"),
        }, new List<MessageEvidence>
        {
            C("domain", "Supplier's own address", "It comes from the supplier you actually use."),
            C("matches", "Matches your real order", "You did place an office supply order."),
            C("account", "Points to your existing account", "No attachment or link - you check it yourself."),
            C("official", "Official website for questions", "Real companies direct you to their own channels."),
            C("money", "Mentions money", "Invoices mention costs - on its own that isn't a warning sign.", false),
        }),
        ["l2_account_suspension"] = D(new List<MessageInspectable>
        {
            I("number", Detail, "", "Phone number", "+1 (555) 019-4471 isn't in your contacts - an ordinary mobile number.", "number"),
            I("which", Body, "Your account", "Which account?", "It never says which account or which provider.", "vague"),
            I("deadline", Body, "suspended in 30 minutes", "Deadline", "Only 30 minutes - pressure to act without checking.", "deadline"),
            I("unusual", Body, "unusual activity", "Reason given", "'Unusual activity' sounds technical.", "unusual"),
            I("identity", Body, "Confirm your identity immediately", "What it asks", "Asks you to confirm your identity on a website - a common way to steal logins.", "identity"),
            I("link", Link, "", "Link", "secure-check.example.invalid doesn't name any provider at all.", "link", "https://secure-check.example.invalid/verify"),
        }, new List<MessageEvidence>
        {
            C("number", "Unknown number", "The text didn't come from any service you recognise."),
            C("vague", "No account or provider named", "Real alerts say which account they mean."),
            C("deadline", "30-minute deadline", "Extreme urgency is a pressure tactic."),
            C("identity", "Asks you to confirm your identity via a link", "This is how login details are stolen."),
            C("link", "Link with no provider name", "A real provider's link names the provider."),
            C("unusual", "'Unusual activity'", "Real alerts mention this too - on its own it isn't proof.", false),
        }),
        ["l2_project_schedule"] = D(new List<MessageInspectable>
        {
            I("address", Detail, "", "Sender address", "daniel.reed@yourcompany.example - a company address from a colleague you know.", "company"),
            I("meeting", Body, "following our meeting", "Context", "It refers to the meeting you attended today.", "meeting"),
            I("workspace", Body, "the team's usual shared workspace", "Where to look", "It uses the workspace your team already uses - no new link.", "workspace"),
            I("dates", Body, "let me know if any of the dates conflict", "What it asks", "Only asks you to reply about dates - nothing sensitive.", "noask"),
        }, new List<MessageEvidence>
        {
            C("company", "Company address of a known colleague", "Sent from your company's own domain."),
            C("meeting", "Matches today's meeting", "The context fits what really happened."),
            C("workspace", "Uses the usual shared workspace", "No unfamiliar link or attachment."),
            C("noask", "Asks for nothing sensitive", "Only a reply about dates."),
        }),
        ["l2_gift_card_request"] = D(new List<MessageInspectable>
        {
            I("title", Sender, "", "Sender name", "Just a job title - no personal name.", "title"),
            I("address", Detail, "", "Sender address", "quickmail.example is an outside email service, not your company's domain.", "external"),
            I("busy", Body, "I'm currently occupied", "Excuse", "Says they're busy.", "busy"),
            I("cards", Body, "purchase six gift cards", "The request", "Gift cards work like cash and can't be traced - a classic boss-impersonation request.", "cards"),
            I("codes", Body, "Send me the redemption codes as soon as possible", "Urgency", "Wants the codes as soon as possible.", "urgency"),
            I("secret", Body, "keep this confidential", "Secrecy", "Asks you to keep it secret, so no one can warn you.", "secret"),
        }, new List<MessageEvidence>
        {
            C("title", "Job title instead of a name", "Impersonators hide behind a title."),
            C("external", "Outside email service, not the company domain", "A real director writes from the company address."),
            C("cards", "Asks for gift cards", "Gift card codes are untraceable cash."),
            C("urgency", "Wants the codes as soon as possible", "Urgency stops you from checking."),
            C("secret", "Asks for secrecy", "Secrecy cuts you off from anyone who could warn you."),
            C("busy", "'I'm currently occupied'", "Managers really are busy - on its own this isn't proof (though it's a common excuse to avoid a call).", false),
        }),
        ["l2_password_changed"] = D(new List<MessageInspectable>
        {
            I("address", Detail, "", "Sender address", "no-reply@examplecloud.example - the cloud service's own domain.", "domain"),
            I("subject", Subject, "Security Notification", "Subject line", "'Security Notification' can sound alarming.", "alarm"),
            I("requested", Body, "The password change you requested", "What it refers to", "It refers to the password change you really made.", "matches"),
            I("noaction", Body, "no further action is needed", "What it asks", "Nothing - no link, code or password.", "noask"),
            I("official", Body, "open the official application directly", "Advice", "If it wasn't you, it tells you to open the official app yourself.", "official"),
        }, new List<MessageEvidence>
        {
            C("domain", "The service's own domain", "Sent from examplecloud.example, the real service."),
            C("matches", "Matches the change you made", "You did request a password change."),
            C("noask", "Asks for nothing", "No link, code or password is requested."),
            C("official", "Tells you to use the official app", "Genuine services send you to their own app."),
            C("alarm", "'Security Notification' subject", "It sounds alarming, but it's a plain notice with no threat or deadline.", false),
        }),
        ["l2_it_support"] = D(new List<MessageInspectable>
        {
            I("name", Sender, "", "Sender name", "Calls itself 'IT Helpdesk' - anyone can set a display name."),
            I("number", Detail, "", "Phone number", "+1 (555) 016-2208 is an ordinary phone number, not your company's IT system.", "number"),
            I("records", Body, "updating employee security records", "Reason given", "Sounds official.", "records"),
            I("password", Body, "Reply with your account password and verification code", "What it asks", "It asks for your password AND verification code by text. Real IT never asks for these.", "password"),
            I("deadline", Body, "before the end of the day", "Deadline", "Pressure to do it today.", "deadline"),
        }, new List<MessageEvidence>
        {
            C("password", "Asks for your password and verification code", "No real IT team ever asks for these."),
            C("number", "Ordinary phone number", "Company IT doesn't text from random numbers."),
            C("deadline", "'Before the end of the day'", "A deadline to rush you."),
            C("records", "Official-sounding reason", "Anyone can claim to be 'updating records' - on its own it isn't evidence.", false),
        }),
        ["l2_it_chat"] = D(new List<MessageInspectable>
        {
            I("account", Detail, "", "Account", "A verified account listed in your company directory.", "verified"),
            I("announce", Body, "the scheduled password change from this morning's company announcement", "Context", "It matches this morning's real company announcement.", "matches"),
            I("friday", Body, "due by Friday", "Deadline", "A deadline several days away.", "friday"),
            I("portal", Body, "change it yourself in the IT portal from your bookmarks", "How to do it", "No link to click - you go to the portal yourself.", "portal"),
            I("never", Body, "Never share your password with anyone, including IT", "Password advice", "It warns you never to share your password - the opposite of a phisher.", "nopassword"),
            I("call", Body, "call the service desk on extension 4100", "Contact", "It offers a phone check.", "phone"),
        }, new List<MessageEvidence>
        {
            C("verified", "Verified internal account", "The chat comes from a verified account in your directory."),
            C("matches", "Matches the company announcement", "It repeats this morning's official notice."),
            C("portal", "No link - go to the portal yourself", "Typing or bookmarking the official portal avoids fake links."),
            C("nopassword", "Never asks for your password", "It tells you not to share it with anyone."),
            C("phone", "Offers a phone check", "Inviting a call makes it easy to verify - scammers avoid this."),
            C("friday", "'Due by Friday'", "A deadline days away that matches the announcement isn't pressure - on its own it isn't a scam sign.", false),
        }),
    };

    static ScamMessage ItChat() => new ScamMessage
    {
        id = "l2_it_chat",
        channel = MessageChannel.Chat,
        sender = "Priya Shah — IT Service Desk",
        senderDetail = "@priya.shah · verified · company directory",
        body = "Priya: Hi! Reminder from the IT Service Desk - the scheduled password change from this morning's company announcement is due by Friday.\n" +
               "Priya: Please change it yourself in the IT portal from your bookmarks. Never share your password with anyone, including IT.\n" +
               "You: Thanks, will do.\n" +
               "Priya: If anything looks odd, call the service desk on extension 4100.",
        correctVerdict = MessageVerdict.Legitimate,
        scenarioContext = "This morning the company announced a scheduled password change, due by Friday.",
        explanation = "LEGITIMATE. The chat comes from a verified account in your company directory, matches this morning's announcement, " +
                      "asks you to change your password yourself in the IT portal (no link), warns you never to share it and offers a phone check. " +
                      "A deadline that matches an announced schedule isn't a scam sign by itself.",
        requestedAction = "Change your password yourself in the IT portal from your bookmarks, by Friday.",
        linkNote = "No link. It tells you to use the IT portal from your own bookmarks.",
        clues = new[] { "Sent from a verified account in the company directory.", "Refers to this morning's company announcement." },
    };
}
