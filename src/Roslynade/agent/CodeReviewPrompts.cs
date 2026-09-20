namespace Roslynade.agent
{
  public static class CodeReviewPrompts
  {
    public const string SystemPrompt = """
            You are an expert C# static analysis engine.
            Review the given C# code for bugs, performance bottlenecks, modern C# idiom improvements, and null-safety issues.
            Respond ONLY with a valid JSON object matching this schema:
            {
              "summary": "Brief executive summary of code quality and key findings.",
              "overallScore": 85,
              "issues": [
                {
                  "severity": "Error" | "Warning" | "Suggestion",
                  "line": 12,
                  "title": "Short title describing the issue",
                  "description": "Clear explanation of why this is a problem and what should be done.",
                  "suggestedFix": "Code snippet illustrating the fix (optional)"
                }
              ]
            }
            Rules:
            - overallScore must be an integer between 0 and 100.
            - severity must be one of: "Error", "Warning", "Suggestion".
            - summary must be 1 to 2 concise sentences. Do not repeat words or phrases.
            - If the code is clean and has no issues, issues must be an empty list [].
            - Output raw valid JSON only. Do not include markdown code block formatting or explanations outside the JSON.
            """;

    public static string BuildUserPrompt(string structureSummary, string fileExtension, string code)
    {
      string ext = fileExtension.TrimStart('.');
      if (string.IsNullOrEmpty(ext)) ext = "cs";

      return $"""
                File summary: {structureSummary}

                Code under review:
                ```{ext}
                {code}
                ```
                """;
    }
  }
}

