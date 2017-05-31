using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using R4Mvc.Tools.Extensions;
using R4Mvc.Tools.Services;
using System.Collections.Immutable;
using System.Linq;

namespace R4Mvc.Tools
{
    public class R4MvcGenerator
    {
        private readonly IControllerRewriterService _controllerRewriter;

        private readonly IControllerGeneratorService _controllerGenerator;

        private readonly IStaticFileGeneratorService _staticFileGenerator;

        private static readonly string[] pramaCodes = { "1591", "3008", "3009", "0108" };

        public const string R4MvcFileName = "R4Mvc.generated.cs";

        private const string _headerText = @"
// <auto-generated />
// This file was generated by a R4Mvc.
// Don't change it directly as your change would get overwritten.  Instead, make changes
// to the r4mvc.json file (i.e. the settings file), save it and rebuild.

// Make sure the compiler doesn't complain about missing Xml comments and CLS compliance
// 0108: suppress ""Foo hides inherited member Foo.Use the new keyword if hiding was intended."" when a controller and its abstract parent are both processed";

        public R4MvcGenerator(
            IControllerRewriterService controllerRewriter,
            IControllerGeneratorService controllerGenerator,
            IStaticFileGeneratorService staticFileGenerator)
        {
            _controllerRewriter = controllerRewriter;
            _controllerGenerator = controllerGenerator;
            _staticFileGenerator = staticFileGenerator;
        }


        public SyntaxNode Generate(CSharpCompilation compilation, ISettings settings)
        {
            // Create the root node and add usings, header, pragma
            var r4mvcNode =
                SyntaxFactory.CompilationUnit()
                    .WithUsings(
                        "System.CodeDom.Compiler",
                        "System.Diagnostics",
                        "System.Threading.Tasks",
                        "Microsoft.AspNetCore.Mvc",
                        "Microsoft.AspNetCore.Mvc.Routing",
                        settings.R4MvcNamespace)
                    .WithHeader(_headerText)
                    .WithPragmaCodes(false, pramaCodes);

            var controllers = _controllerRewriter.RewriteControllers(compilation, R4MvcFileName);
            var generatedControllers = _controllerGenerator.GenerateControllers(compilation, controllers).ToImmutableArray();
            var staticFileNode = _staticFileGenerator.GenerateStaticFiles(settings);

            // add the dummy class using in the derived controller partial class
            var r4Namespace = SyntaxNodeHelpers.CreateNamespace(settings.R4MvcNamespace).WithDummyClass();

            // create static MVC class and add controller fields 
            var mvcStaticClass =
                SyntaxNodeHelpers.CreateClass(settings.HelpersPrefix, null, SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword, SyntaxKind.PartialKeyword)
                    .WithAttributes(SyntaxNodeHelpers.CreateGeneratedCodeAttribute(), SyntaxNodeHelpers.CreateDebugNonUserCodeAttribute())
                    .WithControllerFields(controllers);

            r4mvcNode =
                r4mvcNode.AddMembers(generatedControllers.Cast<MemberDeclarationSyntax>().ToArray())
                    .AddMembers(staticFileNode)
                    .AddMembers(r4Namespace)
                    .AddMembers(mvcStaticClass)
                    .NormalizeWhitespace()
                    .WithPragmaCodes(true, pramaCodes);

            // NOTE reenable pragma codes after normalizing whitespace or it doesn't place on newline
            return r4mvcNode;
        }
    }
}
