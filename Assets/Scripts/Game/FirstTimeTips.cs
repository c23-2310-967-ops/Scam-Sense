using System;
using UnityEngine;

/// <summary>
/// Short, one-time instructions shown the first time a player meets a new kind of message or mechanic
/// (what the old HOW TO PLAY page explained up front). QuizManager offers one when a message arrives; the HUD shows
/// it as a "TIP" toast for a few seconds. Each tip is shown once per PC (PlayerPrefs).
/// While the Level 1 guide (TutorialCoach) is running it teaches these itself, so the tip is only marked as seen.
/// The controls stay visible on the briefing card and the pause menu.
/// </summary>
public static class FirstTimeTips
{
    public const string Phone = "phone", Computer = "computer", Workstation = "workstation",
                        CaseTerminal = "case_terminal", Choice = "choice";

    const string KeyPrefix = "scamsense_tip_seen_";

    public static bool Seen(string id) => PlayerPrefs.GetInt(KeyPrefix + id, 0) == 1;

    public static void MarkSeen(string id)
    {
        PlayerPrefs.SetInt(KeyPrefix + id, 1);
        PlayerPrefs.Save();
    }

    public static void Forget(string id) => PlayerPrefs.DeleteKey(KeyPrefix + id);

    /// <summary>
    /// The tip to show for a message that just arrived, or null if every tip that applies was already seen.
    /// Where to open it comes first (case terminal, workstation, computer, phone); the question type comes after.
    /// </summary>
    /// <param name="onComputer">The message opens on a computer in this room.</param>
    /// <param name="onCaseTerminal">The message is submitted at the final case terminal.</param>
    public static string NextTip(ScamMessage m, bool onComputer, bool onCaseTerminal, Func<string, bool> seen, out string text)
    {
        text = null;
        if (m == null) return null;
        seen = seen ?? (_ => false);
        string e = UIKit.Key("E");

        if (onCaseTerminal && !seen(CaseTerminal))
        {
            text = $"<b>Final case:</b> check the evidence first (look at documents and boards, press {e}), then submit at the final case terminal.";
            return CaseTerminal;
        }
        if (onComputer && !string.IsNullOrEmpty(m.stationId) && !seen(Workstation))
        {
            text = $"This room has numbered <b>workstations</b>. Go to the one the message names, look at its screen and press {e}.";
            return Workstation;
        }
        if (onComputer && !seen(Computer))
        {
            text = $"Emails and web pages open on a <b>computer</b>. Walk to a screen, look at it and press {e}.";
            return Computer;
        }
        if (!onComputer && !onCaseTerminal && MessageChannels.OnPhone(m.channel) && !seen(Phone))
        {
            text = $"Texts and chats arrive on your <b>phone</b>. Press {e} to read them.";
            return Phone;
        }
        if (m.HasChoices && !seen(Choice))
        {
            text = "This one is a <b>question</b>. Check the details first, then pick the best answer <nobr>(A-D)</nobr>.";
            return Choice;
        }
        return null;
    }
}
