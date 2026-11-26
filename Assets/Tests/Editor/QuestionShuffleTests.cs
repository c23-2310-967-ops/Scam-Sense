using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Per-attempt shuffling (QuestionShuffle) and answer checking of shuffled choices (ScoringSystem.TryRecordChoice).
/// Seeded random numbers, throwaway data - never the real level assets.
/// </summary>
public class QuestionShuffleTests
{
    static ScamMessage Choice(string id, int correct = 2) => new ScamMessage
    {
        id = id, channel = MessageChannel.Question, subject = "Multiple Choice", body = "Question " + id,
        choices = new[] { "first", "second", "third", "fourth" }, correctChoice = correct, explanation = "why"
    };

    static List<ScamMessage> Room() => Enumerable.Range(1, 6).Select(i => Choice("q" + i, i % 4)).ToList();

    [SetUp] public void SetUp() => QuestionShuffle.ClearHistory();

    [Test]
    public void Permutation_ContainsEveryIndexOnce()
    {
        var rng = new System.Random(1);
        for (int n = 0; n <= 8; n++)
            for (int run = 0; run < 50; run++)
                CollectionAssert.AreEquivalent(Enumerable.Range(0, n), QuestionShuffle.Permutation(n, rng));
    }

    [Test]
    public void Shuffle_IsUnbiased()
    {
        // 3 items -> 6 orders; Fisher-Yates makes each about 1/6 of the runs.
        var rng = new System.Random(7);
        var counts = new Dictionary<string, int>();
        const int runs = 12000;
        for (int i = 0; i < runs; i++)
        {
            string key = string.Join("", QuestionShuffle.Permutation(3, rng));
            counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
        }
        Assert.AreEqual(6, counts.Count, "Every order should be possible");
        foreach (var kv in counts) Assert.That(kv.Value, Is.InRange(runs / 6 * 0.9, runs / 6 * 1.1), $"order {kv.Key} is over- or under-represented");
    }

    [Test]
    public void Attempt_AsksEveryQuestionExactlyOnce()
    {
        var room = Room();
        for (int seed = 0; seed < 100; seed++)
        {
            var s = new QuestionShuffle(room, "room_test", new System.Random(seed));
            CollectionAssert.AreEquivalent(Enumerable.Range(0, room.Count), s.MessageOrder, "a question was skipped or repeated");
        }
    }

    [Test]
    public void NewAttempt_GetsADifferentOrder()
    {
        var room = Room();
        var rng = new System.Random(3);
        var previous = new QuestionShuffle(room, "room_a", rng).MessageOrder.ToArray();
        for (int attempt = 0; attempt < 200; attempt++)
        {
            var next = new QuestionShuffle(room, "room_a", rng).MessageOrder.ToArray();
            Assert.IsFalse(previous.SequenceEqual(next), "two attempts in a row had the same question order");
            previous = next;
        }
    }

    [Test]
    public void PinnedFirstQuestion_ComesFirst_AndTheRestStayShuffled()
    {
        var room = Room();
        var rng = new System.Random(9);
        var seenSecond = new HashSet<int>();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            var order = new QuestionShuffle(room, "room_pin", rng, pinFirst: 2).MessageOrder;
            Assert.AreEqual(2, order[0], "the guided question must be asked first");
            CollectionAssert.AreEquivalent(Enumerable.Range(0, room.Count), order, "a question was skipped or repeated");
            seenSecond.Add(order[1]);
        }
        Assert.Greater(seenSecond.Count, 2, "the other questions should still be shuffled");
    }

    [Test]
    public void NoPin_IsUnchanged()
    {
        var a = new QuestionShuffle(Room(), null, new System.Random(4)).MessageOrder.ToArray();
        var b = new QuestionShuffle(Room(), null, new System.Random(4), pinFirst: -1).MessageOrder.ToArray();
        CollectionAssert.AreEqual(a, b);
    }

    [Test]
    public void ChoiceOrder_IsAPermutationAndFixedForTheAttempt()
    {
        var room = Room();
        var s = new QuestionShuffle(room, "room_b", new System.Random(11));
        foreach (var m in room)
        {
            var first = s.ChoiceOrder(m).ToArray();
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, first);
            CollectionAssert.AreEqual(first, s.ChoiceOrder(m).ToArray(), "the order changed during the attempt");
        }
    }

    [Test]
    public void ChoicesAreShuffled_AcrossAttempts()
    {
        var m = Choice("solo");
        var rng = new System.Random(5);
        var seen = new HashSet<string>();
        for (int i = 0; i < 200; i++) seen.Add(string.Join("", new QuestionShuffle(new List<ScamMessage> { m }, null, rng).ChoiceOrder(m)));
        Assert.Greater(seen.Count, 12, "choices should appear in many different orders");
    }

    [Test]
    public void ShuffledChoices_AreCheckedByTheChoiceNotItsPosition()
    {
        var go = new GameObject("ShuffleScoring");
        var level = ScriptableObject.CreateInstance<LevelDefinition>();
        try
        {
            var scoring = go.AddComponent<ScoringSystem>();
            scoring.Configure(level);
            for (int seed = 0; seed < 40; seed++)
            {
                scoring.ResetRun();
                var right = Choice("r" + seed, seed % 4);
                var wrong = Choice("w" + seed, seed % 4);
                var s = new QuestionShuffle(new List<ScamMessage> { right, wrong }, null, new System.Random(seed));

                // The button showing the correct text is correct, wherever the shuffle put it.
                var order = s.ChoiceOrder(right);
                int pos = order.ToList().IndexOf(right.correctChoice);
                Assert.IsTrue(scoring.TryRecordChoice(right, order[pos], 1f, out var ok));
                Assert.IsTrue(ok.isCorrect);
                Assert.AreEqual(right.ChoiceText(right.correctChoice), right.ChoiceText(ok.chosenChoice));

                var order2 = s.ChoiceOrder(wrong);
                int other = order2.ToList().FindIndex(i => i != wrong.correctChoice);
                Assert.IsTrue(scoring.TryRecordChoice(wrong, order2[other], 1f, out var bad));
                Assert.IsFalse(bad.isCorrect);
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(level);
        }
    }
}
