using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Remembers which evidence records the player has actually opened during this run (by record title).
/// Evidence stations (CaseFilesTerminal) report a page when the player views it on screen; a
/// FinalCaseTerminal reads it to decide when the final case can be submitted.
/// Lives on the level's manager object; one per scene, reset on every replay (nothing is saved).
/// </summary>
public class EvidenceTracker : MonoBehaviour
{
    /// <summary>Raised when a record is inspected for the first time.</summary>
    public event Action Changed;

    readonly HashSet<string> inspected = new HashSet<string>();

    public int Count => inspected.Count;

    /// <summary>Marks a record as inspected. Returns true the first time only.</summary>
    public bool Record(string recordTitle)
    {
        if (string.IsNullOrEmpty(recordTitle) || !inspected.Add(recordTitle)) return false;
        Changed?.Invoke();
        return true;
    }

    public bool IsInspected(string recordTitle) => !string.IsNullOrEmpty(recordTitle) && inspected.Contains(recordTitle);
}
