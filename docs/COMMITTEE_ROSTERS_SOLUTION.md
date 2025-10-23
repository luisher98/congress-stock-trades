# Committee Roster Parser - Solution

## The Problem

When using `page.Text` in PdfPig, words run together:
```
COMMITTEESANDSELECT COMMITTEESAND THEIRSUBCOMMITTEES
```

## The Solution

**Use `page.GetWords()` instead of `page.Text`!**

### Analysis Results

Using word-level extraction, we get properly separated words:

```
Words with 'AGRICULTURE': 1
  'AGRICULTURE' @ Y:597.3 X:265.9

Context:
  'STANDING' @ Y:621.3
  'COMMITTEES' @ Y:621.3
  'AGRICULTURE' @ Y:597.3  <<<
  'Ratio' @ Y:577.2
  '29/25' @ Y:577.2
  '1.' @ Y:555.9
  'Glenn' @ Y:555.9
  'Thompson,' @ Y:555.9
  'PA,' @ Y:555.9
  'Chair' @ Y:555.9
```

### Member Lines Structure

Numbered lines are extracted correctly:
```
'1.' 'Glenn' 'Thompson,' 'PA,' 'Chair'
'2.' 'Frank' 'D.' 'Lucas,' 'OK'
'3.' 'Austin' 'Scott,' 'GA,' 'Vice' 'Chair'
```

## Implementation Strategy

### 1. Use Word-Based Parsing

```csharp
var words = page.GetWords().ToList();
```

### 2. Identify Committee Headers by Y Position

- Committee names appear at specific Y positions
- "AGRICULTURE" is at Y:597.3
- Member lines start at Y:555.9

### 3. Group Words by Y Position (Lines)

Group words with same/similar Y coordinate (within 2 points tolerance):

```csharp
var lines = words
    .GroupBy(w => Math.Round(w.BoundingBox.Bottom))
    .OrderByDescending(g => g.Key)
    .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
    .ToList();
```

### 4. Parse Each Line

- Committee headers: Single word or short phrase at high Y position
- Ratio lines: "Ratio 29/25"
- Member lines: Start with `\d+\.`  (number with period)
- Majority/Minority: Look for section markers

### 5. Detect Sections

- MAJORITY section: Members before minority marker
- MINORITY section: Members after minority marker

## Updated Parser Logic

```csharp
public Task<CommitteeRosterParseResult> ParseSCSOALAsync(...)
{
    // Get words instead of text
    var words = page.GetWords().ToList();

    // Group into lines by Y position
    var lines = GroupWordsByLine(words);

    // Parse line by line
    string currentCommittee = null;
    bool inMajority = true;

    foreach (var line in lines)
    {
        // Check if committee header (all caps, short)
        if (IsCommitteeHeader(line))
        {
            currentCommittee = line;
            continue;
        }

        // Check if member line (starts with number)
        var memberMatch = Regex.Match(line, @"^(\d+)\.\s+(.+)");
        if (memberMatch.Success)
        {
            ParseMemberLine(line, currentCommittee, inMajority);
        }

        // Check for section markers
        if (line.Contains("MAJORITY")) inMajority = true;
        if (line.Contains("MINORITY")) inMajority = false;
    }
}

private List<string> GroupWordsByLine(List<Word> words)
{
    return words
        .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
        .OrderByDescending(g => g.Key)
        .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))
        .ToList();
}
```

## Expected Results

With this approach, we should get:
- ✅ Proper committee detection: "AGRICULTURE"
- ✅ Member parsing: "1. Glenn Thompson, PA, Chair"
- ✅ Role extraction: "Chair", "Vice Chair", "Ranking Member"
- ✅ State/district: "PA", "GA", etc.
- ✅ Majority/Minority grouping

## Estimated Fix Time

**30-60 minutes** to:
1. Update `CommitteeRosterParser` to use `GetWords()`
2. Implement `GroupWordsByLine()` helper
3. Update line parsing logic
4. Test with real PDF

---

**Status**: Solution identified ✅
**Next**: Implement word-based parser
