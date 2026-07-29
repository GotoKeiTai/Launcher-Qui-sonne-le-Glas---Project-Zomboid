namespace GlasLauncher.Core.Logic;

public static class UpdateNotesParser
{
    public static IReadOnlyList<string> Parse(string notesMarkdown) =>
        notesMarkdown
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.TrimStart('-', '*', ' '))
            .Where(line => line.Length > 0)
            .ToList();
}
