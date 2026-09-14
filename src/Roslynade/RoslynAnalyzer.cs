using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class RoslynAnalyzer
{
    public static string Analyze(SyntaxNode root)
    {
        int classCount = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Count();
        int methodCount = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Count();
        int recordCount = root.DescendantNodes().OfType<RecordDeclarationSyntax>().Count();

        return $"File contains {classCount} class(es), {recordCount} record(s), and {methodCount} method(s).";
    }
}