using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslynade
{
    public static class RoslynAnalyzer
    {
        public static string Analyze(SyntaxNode? root)
        {
            if (root is null)
            {
                return "No syntax tree provided.";
            }

            int classCount = 0;
            int methodCount = 0;
            int recordCount = 0;

            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case ClassDeclarationSyntax:
                        classCount++;
                        break;
                    case MethodDeclarationSyntax:
                        methodCount++;
                        break;
                    case RecordDeclarationSyntax:
                        recordCount++;
                        break;
                }
            }

            return $"File contains {classCount} class(es), {recordCount} record(s), and {methodCount} method(s).";
        }
    }
}