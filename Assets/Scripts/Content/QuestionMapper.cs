using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Converts question-bank records into the ScamMessage objects the game plays, and back (for exporting).
/// The only place that knows both shapes. Default rules for empty values live here:
///   trigger ""            -> SMS / Chat near the phone, Email / Website near a computer, others time only
///   consequence.kind ""   -> choice questions FeedbackOnly, SCAM / LEGITIMATE messages Auto
///   consequence.followUp "" -> a re-check for SCAM / LEGITIMATE messages in the tutorial room, none elsewhere
/// Call <see cref="QuestionBankValidator.ValidateQuestion"/> first: unknown enum names fall back to safe defaults here.
/// </summary>
public static class QuestionMapper
{
    // ---------- Record -> game ----------

    public static ScamMessage ToMessage(QuestionRecord q, bool tutorialRoom)
    {
        if (q == null) throw new ArgumentNullException(nameof(q));
        var channel = Parse(q.messageType, MessageChannel.SMS);
        bool choice = q.HasChoices;
        var c = q.consequence ?? new ConsequenceRecord();
        return new ScamMessage
        {
            id = q.questionId ?? "",
            channel = channel,
            sender = q.sender ?? "",
            senderDetail = q.senderDetail ?? "",
            subject = q.subject ?? "",
            body = q.message ?? "",
            link = q.link ?? "",
            correctVerdict = q.isScam ? MessageVerdict.Scam : MessageVerdict.Legitimate,
            scenarioContext = q.scenarioContext ?? "",
            explanation = q.explanation ?? "",
            trigger = string.IsNullOrEmpty(q.trigger) ? DefaultTrigger(channel) : Parse(q.trigger, DefaultTrigger(channel)),
            readingSeconds = Math.Max(0f, q.readingSeconds),
            stationId = q.stationId ?? "",
            requestedAction = q.requestedAction ?? "",
            linkNote = q.linkNote ?? "",
            clues = Copy(q.detailNotes),
            inspectables = (q.clues ?? new List<ClueRecord>()).Where(x => x != null).Select(x => new MessageInspectable
            {
                id = x.id ?? "", field = Parse(x.field, InspectField.Body), match = x.match ?? "", label = x.label ?? "",
                finding = x.finding ?? "", linkAddress = x.linkAddress ?? "", clueId = x.indicatorId ?? "", part = x.part ?? ""
            }).ToList(),
            evidence = (q.indicators ?? new List<IndicatorRecord>()).Where(x => x != null).Select(x => new MessageEvidence
            {
                id = x.id ?? "", title = x.title ?? "", whyItMatters = x.whyItMatters ?? "", supportsVerdict = x.isKeyClue
            }).ToList(),
            consequence = new WrongAnswerConsequence
            {
                kind = string.IsNullOrEmpty(c.kind) ? DefaultKind(choice) : Parse(c.kind, DefaultKind(choice)),
                alertTitle = c.alertTitle ?? "",
                alertBody = c.alertBody ?? "",
                overlookedClue = c.overlookedClue ?? "",
                followUp = string.IsNullOrEmpty(c.followUp) ? DefaultFollowUp(choice, tutorialRoom) : Parse(c.followUp, DefaultFollowUp(choice, tutorialRoom)),
                recheckDetails = Copy(c.recheckClues),
            },
            choices = choice ? Copy(q.choices) : new string[0],
            correctChoice = choice ? q.correctChoice : 0,
            feedback = q.feedback ?? "",
            curriculumRef = q.curriculumRef ?? "",
            hint = q.hint ?? "",
            activity = ToActivity(q.activity),
        };
    }

    public static MessageActivity ToActivity(ActivityRecord a)
    {
        if (a == null || string.IsNullOrEmpty(a.kind) || a.items == null || a.items.Count == 0) return new MessageActivity();
        var kind = Parse(a.kind, ActivityKind.None);
        if (kind == ActivityKind.None) return new MessageActivity();
        return new MessageActivity
        {
            kind = kind,
            title = a.title ?? "",
            instruction = a.instruction ?? "",
            minClues = Math.Max(0, a.minClues),
            outcomes = Copy(a.outcomes),
            items = a.items.Where(i => i != null).Select(i => new ActivityItem
            {
                key = i.key ?? "", label = i.label ?? "", text = i.text ?? "",
                role = string.IsNullOrEmpty(i.role) ? ActivityRole.Text : Parse(i.role, ActivityRole.Text)
            }).ToList(),
        };
    }

    // ---------- Game -> record (export / migration) ----------

    /// <summary>
    /// The record for a message. Values equal to the default rule are written as "" so a question moved to
    /// another level follows that level's rules; <see cref="ToMessage"/> of the result gives the same message back.
    /// </summary>
    public static QuestionRecord ToRecord(ScamMessage m, string levelId, int order, bool tutorialRoom)
    {
        if (m == null) throw new ArgumentNullException(nameof(m));
        bool choice = m.HasChoices;
        var con = m.consequence ?? new WrongAnswerConsequence();
        return new QuestionRecord
        {
            questionId = m.id ?? "",
            levelId = levelId ?? "",
            order = order,
            isActive = true,
            messageType = m.channel.ToString(),
            sender = m.sender ?? "",
            senderDetail = m.senderDetail ?? "",
            subject = m.subject ?? "",
            message = m.body ?? "",
            link = m.link ?? "",
            scenarioContext = m.scenarioContext ?? "",
            isScam = m.correctVerdict == MessageVerdict.Scam,
            choices = choice ? Copy(m.choices) : new string[0],
            correctChoice = choice ? m.correctChoice : 0,
            explanation = m.explanation ?? "",
            feedback = m.feedback ?? "",
            hint = m.hint ?? "",
            curriculumRef = m.curriculumRef ?? "",
            trigger = m.trigger == DefaultTrigger(m.channel) ? "" : m.trigger.ToString(),
            readingSeconds = m.readingSeconds,
            stationId = m.stationId ?? "",
            requestedAction = m.requestedAction ?? "",
            linkNote = m.linkNote ?? "",
            detailNotes = Copy(m.clues),
            clues = (m.inspectables ?? new List<MessageInspectable>()).Where(i => i != null).Select(i => new ClueRecord
            {
                id = i.id ?? "", field = i.field.ToString(), match = i.match ?? "", label = i.label ?? "", finding = i.finding ?? "",
                linkAddress = i.linkAddress ?? "", indicatorId = i.clueId ?? "", part = i.part ?? ""
            }).ToList(),
            indicators = (m.evidence ?? new List<MessageEvidence>()).Where(e => e != null).Select(e => new IndicatorRecord
            {
                id = e.id ?? "", title = e.title ?? "", whyItMatters = e.whyItMatters ?? "", isKeyClue = e.supportsVerdict
            }).ToList(),
            activity = ToRecord(m.activity),
            consequence = new ConsequenceRecord
            {
                kind = con.kind == DefaultKind(choice) ? "" : con.kind.ToString(),
                alertTitle = con.alertTitle ?? "",
                alertBody = con.alertBody ?? "",
                overlookedClue = con.overlookedClue ?? "",
                followUp = con.followUp == DefaultFollowUp(choice, tutorialRoom) ? "" : con.followUp.ToString(),
                recheckClues = Copy(con.recheckDetails),
            },
        };
    }

    public static ActivityRecord ToRecord(MessageActivity a)
    {
        if (a == null || a.kind == ActivityKind.None || a.items == null || a.items.Count == 0) return new ActivityRecord();
        return new ActivityRecord
        {
            kind = a.kind.ToString(),
            title = a.title ?? "",
            instruction = a.instruction ?? "",
            minClues = a.minClues,
            outcomes = Copy(a.outcomes),
            items = a.items.Where(i => i != null).Select(i => new ActivityItemRecord
            {
                key = i.key ?? "", role = i.role.ToString(), label = i.label ?? "", text = i.text ?? ""
            }).ToList(),
        };
    }

    // ---------- Default rules ----------

    public static MessageTrigger DefaultTrigger(MessageChannel c) =>
        MessageChannels.OnPhone(c) ? MessageTrigger.NearPhone : MessageChannels.OnComputer(c) ? MessageTrigger.NearComputer : MessageTrigger.TimeOnly;

    public static ConsequenceKind DefaultKind(bool choice) => choice ? ConsequenceKind.FeedbackOnly : ConsequenceKind.Auto;

    public static FollowUpMode DefaultFollowUp(bool choice, bool tutorialRoom) => tutorialRoom && !choice ? FollowUpMode.Auto : FollowUpMode.None;

    // ---------- Helpers ----------

    /// <summary>Case-insensitive enum name, or <paramref name="fallback"/>. Numbers are refused (they hide typos).</summary>
    public static T Parse<T>(string value, T fallback) where T : struct =>
        TryParse(value, out T v) ? v : fallback;

    public static bool TryParse<T>(string value, out T result) where T : struct
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        string s = value.Trim();
        if (char.IsDigit(s[0]) || s[0] == '-') return false;
        return Enum.TryParse(s, true, out result) && Enum.IsDefined(typeof(T), result);
    }

    static string[] Copy(string[] a) => a == null ? new string[0] : (string[])a.Clone();
}
