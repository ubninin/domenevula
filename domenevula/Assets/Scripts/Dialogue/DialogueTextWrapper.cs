using System.Collections.Generic;
using System.Text;

/// <summary>
/// 긴 대사를 "한 줄 N자, 페이지당 M줄" 규칙에 맞춰 여러 페이지로 쪼갠다.
/// 공백 단위로 먼저 줄을 채우고, 단어 하나가 한 줄 길이보다 길면 그 단어 안에서 강제로 끊는다.
/// 원문에 이미 줄바꿈(\n)이 있으면 그 지점에서 강제로 줄을 나눈다 (연출용 수동 줄바꿈을 쓰고 싶을 때).
/// </summary>
public static class DialogueTextWrapper
{
    public const int DefaultMaxCharsPerLine = 19;
    public const int DefaultMaxLinesPerPage = 3;

    /// <summary>텍스트를 페이지 목록으로 변환. 각 페이지는 줄바꿈(\n)으로 이어붙인 문자열.</summary>
    public static List<string> SplitIntoPages(
        string text,
        int maxCharsPerLine = DefaultMaxCharsPerLine,
        int maxLinesPerPage = DefaultMaxLinesPerPage)
    {
        List<string> lines = WrapToLines(text, maxCharsPerLine);

        var pages = new List<string>();
        for (int i = 0; i < lines.Count; i += maxLinesPerPage)
        {
            int count = System.Math.Min(maxLinesPerPage, lines.Count - i);
            pages.Add(string.Join("\n", lines.GetRange(i, count)));
        }

        if (pages.Count == 0) pages.Add(string.Empty);
        return pages;
    }

    private static List<string> WrapToLines(string text, int maxCharsPerLine)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            result.Add(string.Empty);
            return result;
        }

        foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            WrapParagraph(paragraph, maxCharsPerLine, result);
        }
        return result;
    }

    private static void WrapParagraph(string paragraph, int maxCharsPerLine, List<string> result)
    {
        string[] words = paragraph.Split(' ');
        var line = new StringBuilder();

        foreach (string rawWord in words)
        {
            string word = rawWord;

            // 단어 자체가 한 줄보다 길면: 현재 줄 먼저 끊고, 단어를 강제로 쪼개서 채운다.
            while (word.Length > maxCharsPerLine)
            {
                int remaining = maxCharsPerLine - line.Length - (line.Length > 0 ? 1 : 0);
                if (remaining <= 0)
                {
                    result.Add(line.ToString());
                    line.Clear();
                    remaining = maxCharsPerLine;
                }
                else if (line.Length > 0)
                {
                    line.Append(' ');
                }

                line.Append(word.Substring(0, remaining));
                word = word.Substring(remaining);
                result.Add(line.ToString());
                line.Clear();
            }

            int lengthIfAdded = line.Length + (line.Length > 0 ? 1 : 0) + word.Length;
            if (lengthIfAdded > maxCharsPerLine)
            {
                result.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0) line.Append(' ');
            line.Append(word);
        }

        result.Add(line.ToString());
    }
}
