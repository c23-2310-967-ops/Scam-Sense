# Applies Tools/plain_language_edits.json to the game question data and its sources, so they stay in step:
#   Assets/Editor/CurriculumQuestions.json   (what the game imports)
#   Docs/Curriculum_Question_Bank.md         (the question bank build_game_questions.py reads)
#   Tools/activity_content.py                (activity instructions)
# Every replacement must match exactly the expected number of times, otherwise nothing is written.
# Run from the project folder:  powershell -ExecutionPolicy Bypass -File Tools/apply_plain_language.ps1
# Then in Unity: Tools > Scam Sense > Curriculum > Load Questions into Rooms.

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/..").Path
$utf8 = New-Object System.Text.UTF8Encoding($false)
$jsonPath = Join-Path $root 'Assets/Editor/CurriculumQuestions.json'
$mdPath   = Join-Path $root 'Docs/Curriculum_Question_Bank.md'
$pyPath   = Join-Path $root 'Tools/activity_content.py'

$edits = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'plain_language_edits.json'), $utf8) | ConvertFrom-Json
$json = [IO.File]::ReadAllText($jsonPath, $utf8)
$md   = [IO.File]::ReadAllText($mdPath, $utf8)
$py   = [IO.File]::ReadAllText($pyPath, $utf8)
$problems = New-Object System.Collections.Generic.List[string]

function JsonEsc([string]$s) { $s.Replace('\', '\\').Replace('"', '\"').Replace("`r", '').Replace("`n", '\n') }
function ToMd([string]$s) { $s.Replace('<b>', '**').Replace('</b>', '**').Replace('<i>', '*').Replace('</i>', '*').Replace("`n", '<br>') }
function CountOf([string]$text, [string]$find) {
    if ([string]::IsNullOrEmpty($find)) { return 0 }
    $n = 0; $i = 0
    while (($i = $text.IndexOf($find, $i, [StringComparison]::Ordinal)) -ge 0) { $n++; $i += $find.Length }
    $n
}
function ReplaceExact([string]$text, [string]$old, [string]$new, [int]$expected, [string]$label) {
    $n = CountOf $text $old
    if ($n -ne $expected) { $script:problems.Add("$label : expected $expected match(es), found $n"); return $text }
    $text.Replace($old, $new)
}

# ---------- Per question: explanation, feedback, learning objective ----------
foreach ($q in $edits.questions) {
    $start = $json.IndexOf("`"id`": `"$($q.id)`",", [StringComparison]::Ordinal)
    if ($start -lt 0) { $problems.Add("$($q.id): not found in JSON"); continue }
    foreach ($field in @(@('explanation', $q.explanation), @('feedback', $q.feedback))) {
        $rx = [regex]('"' + $field[0] + '": "(?:[^"\\]|\\.)*"')
        $m = $rx.Match($json, $start)
        if (-not $m.Success) { $problems.Add("$($q.id): no $($field[0])"); continue }
        $json = $json.Substring(0, $m.Index) + "`"$($field[0])`": `"$(JsonEsc $field[1])`"" + $json.Substring($m.Index + $m.Length)
    }
    # Learning objective: appears on the question and in its room's "lessons" list.
    $m = ([regex]'"learningObjective": "((?:[^"\\]|\\.)*)"').Match($json, $start)
    if ($m.Success) {
        $old = $m.Groups[1].Value
        $json = ReplaceExact $json "`"$old`"" "`"$(JsonEsc $q.objective)`"" 2 "$($q.id) objective (JSON)"
    } else { $problems.Add("$($q.id): no learningObjective") }

    # Markdown block for the question
    $bm = ([regex]"(?ms)^#### $($q.id)\r?\n.*?(?=^#### |^### |^## |^---)").Match($md)
    if (-not $bm.Success) { $problems.Add("$($q.id): block not found in the bank"); continue }
    $block = $bm.Value
    $new = $block
    $new = ([regex]'(?m)^\| Explanation \| .*? \|(?=\r?$)').Replace($new, "| Explanation | $(ToMd $q.explanation) |", 1)
    $new = ([regex]'(?m)^\| Game Feedback \| .*? \|(?=\r?$)').Replace($new, "| Game Feedback | `"$(ToMd $q.feedback)`" |", 1)
    $new = ([regex]'(?m)^\| Learning Objective \| .*? \|(?=\r?$)').Replace($new, "| Learning Objective | $(ToMd $q.objective) |", 1)
    if ($new -eq $block) { $problems.Add("$($q.id): bank rows unchanged") }
    $md = $md.Substring(0, $bm.Index) + $new + $md.Substring($bm.Index + $bm.Length)
}

# ---------- Wording replacements (question text, long choices, activity instructions) ----------
foreach ($r in $edits.replace) {
    $json = ReplaceExact $json (JsonEsc $r.json) (JsonEsc $r.new) 1 "JSON: $($r.json)"
    if ($r.py) {
        $py = ReplaceExact $py $r.json $r.new 1 "py: $($r.json)"
    } else {
        $oldMd = if ($r.md) { $r.md } else { $r.json }
        $newMd = if ($r.newMd) { $r.newMd } else { $r.new }
        $md = ReplaceExact $md $oldMd $newMd 1 "bank: $oldMd"
    }
}

if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Host "PROBLEM  $_" -ForegroundColor Red }
    Write-Host "Nothing was written." -ForegroundColor Red
    exit 1
}
[IO.File]::WriteAllText($jsonPath, $json, $utf8)
[IO.File]::WriteAllText($mdPath, $md, $utf8)
[IO.File]::WriteAllText($pyPath, $py, $utf8)
Write-Host "Applied $($edits.questions.Count) question rewrites and $($edits.replace.Count) wording replacements." -ForegroundColor Green
