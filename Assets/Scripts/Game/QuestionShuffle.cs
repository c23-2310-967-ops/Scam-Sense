using System;
using System.Collections.Generic;

/// <summary>
/// Order of a room's questions and of each question's answer choices for one attempt.
/// Built once when an attempt starts (QuizManager.Begin) and never changed while the player answers.
/// Uses the Fisher-Yates shuffle, so every order is equally likely and every item appears exactly once.
/// </summary>
public class QuestionShuffle
{
    /// <summary>Message indices (into LevelDefinition.messages) in the order they are asked this attempt.</summary>
    public IReadOnlyList<int> MessageOrder => messageOrder;
    private readonly int[] messageOrder;
    private readonly Dictionary<string, int[]> choiceOrders = new Dictionary<string, int[]>();

    /// <summary>The last order used per room, so a new attempt never repeats it (when more than one order exists).</summary>
    static readonly Dictionary<string, int[]> lastOrderByRoom = new Dictionary<string, int[]>();

    /// <param name="pinFirst">Index of a message to always ask first (the tutorial room's guided question), or -1.
    /// The other messages are still shuffled.</param>
    public QuestionShuffle(IList<ScamMessage> messages, string roomKey, Random rng, int pinFirst = -1)
    {
        int n = messages != null ? messages.Count : 0;
        messageOrder = Order(n, rng, pinFirst);
        // A new attempt gets a new order: reshuffle (a few times at most) if it matches the room's previous attempt.
        if (!string.IsNullOrEmpty(roomKey) && n > 1)
        {
            for (int tries = 0; tries < 8 && lastOrderByRoom.TryGetValue(roomKey, out var last) && SameOrder(last, messageOrder); tries++)
                messageOrder = Order(n, rng, pinFirst);
            lastOrderByRoom[roomKey] = (int[])messageOrder.Clone();
        }

        for (int i = 0; i < n; i++)
        {
            var m = messages[i];
            if (m != null && m.HasChoices && !string.IsNullOrEmpty(m.id))
                choiceOrders[m.id] = Permutation(m.choices.Length, rng);
        }
    }

    /// <summary>
    /// Original choice indices in the order they are shown (position 0 = first button, "A").
    /// Identity order for a message without a shuffle entry.
    /// </summary>
    public IReadOnlyList<int> ChoiceOrder(ScamMessage m)
    {
        if (m == null || !m.HasChoices) return Array.Empty<int>();
        if (!string.IsNullOrEmpty(m.id) && choiceOrders.TryGetValue(m.id, out var order) && order.Length == m.choices.Length)
            return order;
        return Permutation(m.choices.Length, null);
    }

    /// <summary>A shuffled order; with a valid <paramref name="pinFirst"/>, that index comes first and the rest are shuffled.</summary>
    static int[] Order(int n, Random rng, int pinFirst)
    {
        var order = Permutation(n, rng);
        if (pinFirst < 0 || pinFirst >= n) return order;
        int at = Array.IndexOf(order, pinFirst);
        for (int i = at; i > 0; i--) order[i] = order[i - 1];   // keep the others' shuffled order
        order[0] = pinFirst;
        return order;
    }

    /// <summary>0..n-1 shuffled with Fisher-Yates. A null rng returns the identity order.</summary>
    public static int[] Permutation(int n, Random rng)
    {
        var a = new int[Math.Max(0, n)];
        for (int i = 0; i < a.Length; i++) a[i] = i;
        if (rng != null) Shuffle(a, rng);
        return a;
    }

    /// <summary>In-place Fisher-Yates (Durstenfeld) shuffle.</summary>
    public static void Shuffle<T>(IList<T> list, Random rng)
    {
        if (list == null || rng == null) return;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);   // 0..i inclusive
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>Forgets the previous orders (tests and a full progress reset).</summary>
    public static void ClearHistory() => lastOrderByRoom.Clear();

    static bool SameOrder(int[] a, int[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
