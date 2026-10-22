using System.Collections.Generic;

/// <summary>
/// Investigation of one ScamMessage while it is open in the normal gameplay flow (never saved).
/// Inspecting a detail may discover a clue (once, no duplicates). <see cref="Submit"/> evaluates once, when the
/// existing SCAM / LEGITIMATE answer is given: which key clues were found and which were missed.
/// </summary>
public class InvestigationSession
{
    public enum InspectOutcome { NewClue, KnownClue, NoClue, Unknown }

    public class Result
    {
        public MessageVerdict submitted, correct;
        public bool VerdictCorrect => submitted == correct;
        /// <summary>Key clues (supporting the correct verdict) the player found by inspecting the message.</summary>
        public readonly List<MessageEvidence> found = new List<MessageEvidence>();
        /// <summary>Key clues the player never discovered.</summary>
        public readonly List<MessageEvidence> missed = new List<MessageEvidence>();
        public int KeyTotal => found.Count + missed.Count;
    }

    public ScamMessage Message { get; }
    /// <summary>Discovered clues, in the order they were found.</summary>
    public IReadOnlyList<MessageEvidence> Discovered => discovered;
    public IReadOnlyCollection<string> InspectedIds => inspected;
    public Result Evaluation { get; private set; }
    public bool Submitted => Evaluation != null;

    readonly List<MessageEvidence> discovered = new List<MessageEvidence>();
    readonly HashSet<string> inspected = new HashSet<string>();

    public InvestigationSession(ScamMessage message) { Message = message; }

    public MessageInspectable FindInspectable(string id) => Message.inspectables?.Find(i => i.id == id);
    public MessageEvidence FindClue(string id) => Message.evidence?.Find(c => c.id == id);

    /// <summary>Inspects a detail. Only details that reveal a clue add evidence, and only once.</summary>
    public InspectOutcome Inspect(string inspectId, out MessageInspectable part, out MessageEvidence clue, out bool firstLook)
    {
        clue = null;
        part = FindInspectable(inspectId);
        firstLook = false;
        if (part == null) return InspectOutcome.Unknown;
        firstLook = inspected.Add(inspectId);
        if (string.IsNullOrEmpty(part.clueId)) return InspectOutcome.NoClue;
        clue = FindClue(part.clueId);
        if (clue == null) return InspectOutcome.NoClue;
        if (discovered.Contains(clue) || Submitted) return InspectOutcome.KnownClue;
        discovered.Add(clue);
        return InspectOutcome.NewClue;
    }

    public bool WasInspected(string inspectId) => inspected.Contains(inspectId);
    public bool IsDiscovered(string clueId) => discovered.Exists(c => c.id == clueId);
    /// <summary>Evaluates the clues once, with the verdict the player gave. Later calls return the same result.</summary>
    public Result Submit(MessageVerdict verdict)
    {
        if (Submitted) return Evaluation;
        var r = new Result { submitted = verdict, correct = Message.correctVerdict };
        if (Message.evidence != null)
            foreach (var c in Message.evidence)
                if (c.supportsVerdict) (IsDiscovered(c.id) ? r.found : r.missed).Add(c);
        Evaluation = r;
        return r;
    }

    /// <summary>Authoring check for a message's investigation data. Problems are reported, never thrown.</summary>
    public static List<string> Validate(ScamMessage m)
    {
        var problems = new List<string>();
        if (m == null || !m.HasInvestigation) return problems;
        var ids = new HashSet<string>();
        foreach (var i in m.inspectables)
        {
            if (string.IsNullOrEmpty(i.id) || !ids.Add(i.id)) problems.Add($"inspectable id '{i.id}' is empty or repeated");
            if (!string.IsNullOrEmpty(i.clueId) && (m.evidence == null || !m.evidence.Exists(c => c.id == i.clueId)))
                problems.Add($"inspectable '{i.id}' points to missing clue '{i.clueId}'");
            string text = FieldText(m, i.field);
            if (string.IsNullOrEmpty(text)) problems.Add($"inspectable '{i.id}': the {i.field} field is empty");
            else if (!string.IsNullOrEmpty(i.match) && !text.Contains(i.match)) problems.Add($"inspectable '{i.id}': '{i.match}' is not in the {i.field} text");
        }
        if (m.evidence != null)
            foreach (var c in m.evidence)
                if (!m.inspectables.Exists(i => i.clueId == c.id)) problems.Add($"clue '{c.id}' can never be discovered");
        return problems;
    }

    public static string FieldText(ScamMessage m, InspectField f) =>
        f == InspectField.Sender ? m.sender : f == InspectField.SenderDetail ? m.senderDetail : f == InspectField.Subject ? m.subject
        : f == InspectField.Link ? m.link : m.body;
}
