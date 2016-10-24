﻿using System.Collections.Generic;
using Ink.Parsed;

namespace Ink
{
	internal partial class InkParser
	{
        protected class FlowDecl
        {
            public string name;
            public List<FlowBase.Argument> arguments;
            public bool isFunction;
        }

		protected Knot KnotDefinition()
		{
            var knotDecl = Parse(KnotDeclaration);
            if (knotDecl == null)
                return null;

			Expect(EndOfLine, "end of line after knot name definition", recoveryRule: SkipToNextLine);

			SpecificParseRule<List<Parsed.Object>> innerKnotStatements = () => StatementsAtLevel (StatementLevel.Knot);

            var content = Expect<List<Parsed.Object>> (innerKnotStatements, "at least one line within the knot", recoveryRule: KnotStitchNoContentRecoveryRule);
			 
            return new Knot (knotDecl.name, content, knotDecl.arguments, knotDecl.isFunction);
		}

        protected FlowDecl KnotDeclaration()
        {
            IgnoredWhitespace();

            if (KnotTitleEquals () == null)
                return null;

            IgnoredWhitespace();


            string identifier = Parse(Identifier);
            string knotName;

            bool isFunc = identifier == "function";
            if (isFunc) {
                Expect(Whitespace<object>, "whitespace after the 'function' keyword");
                knotName = Expect (Identifier, "the name of the function") as string;
            } else {
                knotName = identifier;
            }

            IgnoredWhitespace();

            List<FlowBase.Argument> parameterNames = Parse (BracketedKnotDeclArguments);

            IgnoredWhitespace();

            // Optional equals after name
            Parse(KnotTitleEquals);

            return new FlowDecl () { name = knotName, arguments = parameterNames, isFunction = isFunc };
        }

        protected string KnotTitleEquals()
        {
            // 2+ "=" starts a knot
            var multiEquals = ParseCharactersFromString ("=");
            if (multiEquals == null || multiEquals.Length <= 1) {
                return null;
            } else {
                return multiEquals;
            }
        }

		protected Stitch StitchDefinition()
		{
            var decl = Parse(StitchDeclaration);
            if (decl == null)
                return null;

			Expect(EndOfLine, "end of line after stitch name", recoveryRule: SkipToNextLine);

			SpecificParseRule<List<Object>> innerStitchStatements = () => StatementsAtLevel (StatementLevel.Stitch);

            var content = Expect(innerStitchStatements, "at least one line within the stitch", recoveryRule: KnotStitchNoContentRecoveryRule) as List<Parsed.Object>;

            return new Stitch (decl.name, content, decl.arguments, decl.isFunction );
		}

        protected FlowDecl StitchDeclaration()
        {
            IgnoredWhitespace();

            // Single "=" to define a stitch
            if (ParseString ("=") == null)
                return null;

            // If there's more than one "=", that's actually a knot definition (or divert), so this rule should fail
            if (ParseString ("=") != null)
                return null;

            IgnoredWhitespace();

            // Stitches aren't allowed to be functions, but we parse it anyway and report the error later
            bool isFunc = ParseString ("function") != null;
            if ( isFunc ) {
                IgnoredWhitespace();
            }

            string stitchName = Parse(Identifier);
            if (stitchName == null)
                return null;

            IgnoredWhitespace();

            List<FlowBase.Argument> flowArgs = Parse(BracketedKnotDeclArguments);

            IgnoredWhitespace();

            return new FlowDecl () { name = stitchName, arguments = flowArgs, isFunction = isFunc };
        }


		protected List<Parsed.Object> KnotStitchNoContentRecoveryRule()
		{
            // Jump ahead to the next knot or the end of the file
            ParseUntil (KnotDeclaration, new CharacterSet ("="), null);

            var recoveredFlowContent = new List<Parsed.Object>();
			recoveredFlowContent.Add( new Parsed.Text("<ERROR IN FLOW>" ) );
			return recoveredFlowContent;
		}

        protected List<FlowBase.Argument> BracketedKnotDeclArguments()
        {
            if (ParseString ("(") == null)
                return null;

            var flowArguments = Interleave<FlowBase.Argument>(Spaced(FlowDeclArgument), Exclude (String(",")));

            Expect (String (")"), "closing ')' for parameter list");

            // If no parameters, create an empty list so that this method is type safe and 
            // doesn't attempt to return the ParseSuccess object
            if (flowArguments == null) {
                flowArguments = new List<FlowBase.Argument> ();
            }

            return flowArguments;
        }

        protected FlowBase.Argument FlowDeclArgument()
        {
            // Possible forms:
            //  name
            //  -> name      (variable divert target argument
            //  ref name
            //  ref -> name  (variable divert target by reference)
            var firstIden = Parse(Identifier);
            IgnoredWhitespace();
            var divertArrow = ParseDivertArrow ();
            IgnoredWhitespace();
            var secondIden = Parse(Identifier);

            if (firstIden == null && secondIden == null)
                return null;


            var flowArg = new FlowBase.Argument ();
            if (divertArrow != null) {
                flowArg.isDivertTarget = true;
            }

            // Passing by reference
            if (firstIden == "ref") {

                if (secondIden == null) {
                    Error ("Expected an parameter name after 'ref'");
                }

                flowArg.name = secondIden;
                flowArg.isByReference = true;
            } 

            // Simple argument name
            else {

                if (flowArg.isDivertTarget) {
                    flowArg.name = secondIden;
                } else {
                    flowArg.name = firstIden;
                }

                if (flowArg.name == null) {
                    Error ("Expected an parameter name");
                }

                flowArg.isByReference = false;
            }

            return flowArg;
        }

        protected ExternalDeclaration ExternalDeclaration()
        {
            IgnoredWhitespace();

            string external = Parse(Identifier);
            if (external != "EXTERNAL")
                return null;

            IgnoredWhitespace();
            
            string funcName = Expect(Identifier, "name of external function") as string ?? "";

            IgnoredWhitespace();

            var parameterNames = Expect (BracketedKnotDeclArguments, "declaration of arguments for EXTERNAL, even if empty, i.e. 'EXTERNAL "+funcName+"()'") as List<FlowBase.Argument>;
            if (parameterNames == null)
                parameterNames = new List<FlowBase.Argument> ();

            var argNames = parameterNames.ConvertAll<string> (arg => arg.name);

            return new ExternalDeclaration (funcName, argNames);
        }

	}
}

