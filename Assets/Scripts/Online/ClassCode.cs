using System.Text;

/// <summary>
/// Class codes: 6 characters from an alphabet without I, O, 0 and 1, so a code read aloud or copied from the
/// board can't be mistyped as a look-alike. The server creates the codes (public.new_class_code) and keeps them
/// unique; this class only cleans up and checks what a student types. Same alphabet as the database check
/// (classes.code ~ '^[A-HJ-NP-Z2-9]{6}$'); ClassroomTests verifies they match.
/// </summary>
public static class ClassCode
{
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int Length = 6;

    /// <summary>Upper case, spaces / dashes / anything else removed ("ab3-k7p" -> "AB3K7P").</summary>
    public static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
            if (char.IsLetterOrDigit(c) && c < 128) sb.Append(char.ToUpperInvariant(c));
        return sb.ToString();
    }

    public static bool IsValid(string raw)
    {
        string code = Normalize(raw);
        if (code.Length != Length) return false;
        foreach (char c in code) if (Alphabet.IndexOf(c) < 0) return false;
        return true;
    }

    /// <summary>Null when the code can be sent, otherwise what is wrong with it (shown under the field).</summary>
    public static string Problem(string raw)
    {
        string code = Normalize(raw);
        if (code.Length == 0) return "Type the class code your teacher gave you.";
        if (code.Length != Length) return $"Class codes are {Length} characters long.";
        foreach (char c in code)
            if (Alphabet.IndexOf(c) < 0)
                return c == 'O' || c == '0' || c == 'I' || c == '1'
                    ? "Class codes never use O, 0, I or 1 - check the code with your teacher."
                    : "That isn't a valid class code.";
        return null;
    }

    /// <summary>"AB3K7P" -> "AB3-K7P" (easier to read on screen; the dash is ignored when typed back).</summary>
    public static string Display(string code)
    {
        code = Normalize(code);
        return code.Length == Length ? code.Substring(0, 3) + "-" + code.Substring(3) : code;
    }

    /// <summary>A random code using the same rule as the server (tests and previews; the server makes the real ones).</summary>
    public static string Generate(System.Random rng)
    {
        var sb = new StringBuilder(Length);
        for (int i = 0; i < Length; i++) sb.Append(Alphabet[rng.Next(Alphabet.Length)]);
        return sb.ToString();
    }
}
