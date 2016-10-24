using System;
using System.Collections.Generic;
using System.Linq;
using Ink.Parsed;


namespace Ink
{
	internal partial class InkParser
	{
		protected enum StatementLevel
		{
            InnerBlock,
			Stitch,
			Knot,
			Top
		}

		protected List<Parsed.Object> StatementsAtLevel(StatementLevel level)
		{
            // Check for error: Should not be allowed gather dashes within an inner block
            if (level == StatementLevel.InnerBlock) {
                object badGatherDashCount = Parse(GatherDashes);
                if (badGatherDashCount != null) {
                    Error ("You can't use a gather (the dashes) within the { curly braces } context. For multi-line sequences and conditions, you should only use one dash.");
                }
            }

            return Interleave<Option<Empty>,object,Parsed.Object>(
                Optional (MultilineWhitespace),
                Helpers.doNothing,
                () => StatementAtLevel(level),
                (x,list) => LegacyTryAddResultToList(x,list),
                AlwaysTrue,
                untilTerminator: () => StatementsBreakForLevel(level));
		}
            
        protected object StatementAtLevel(StatementLevel level)
        {            
            SpecificParseRule<object>[] rulesAtLevel = _statementRulesAtLevel[(int)level];

            var statement = OneOf (rulesAtLevel);

            // For some statements, allow them to parse, but create errors, since
            // writers may think they can use the statement, so it's useful to have 
            // the error message.
            if (level == StatementLevel.Top) {
                if( statement is Return ) 
                    Error ("should not have return statement outside of a knot");
            }

            return statement;
        }

        protected object StatementsBreakForLevel(StatementLevel level)
        {
            IgnoredWhitespace();

            SpecificParseRule<object>[] breakRules = _statementBreakRulesAtLevel[(int)level];

            var breakRuleResult = OneOf (breakRules);
            if (breakRuleResult == null)
                return null;

            return breakRuleResult;
        }

		void GenerateStatementLevelRules()
		{
            var levels = Enum.GetValues (typeof(StatementLevel)).Cast<StatementLevel> ().ToList();

            _statementRulesAtLevel = new SpecificParseRule<object>[levels.Count][];
            _statementBreakRulesAtLevel = new SpecificParseRule<object>[levels.Count][];

            foreach (var level in levels) {
                var rulesAtLevel = new List<SpecificParseRule<object>> ();
                var breakingRules = new List<SpecificParseRule<object>> ();

                // Diverts can go anywhere
                rulesAtLevel.Add(() => Line(MultiDivert)());

                if (level >= StatementLevel.Top) {

                    // Knots can only be parsed at Top/Global scope
                    rulesAtLevel.Add (KnotDefinition);
                    rulesAtLevel.Add (ExternalDeclaration);
                }

                rulesAtLevel.Add(() => Line(Choice)());

                rulesAtLevel.Add(() => Line(AuthorWarning)());

                // Gather lines would be confused with multi-line block separators, like
                // within a multi-line if statement
                if (level > StatementLevel.InnerBlock) {
                    rulesAtLevel.Add (Gather);
                }

                // Stitches (and gathers) can (currently) only go in Knots and top level
                if (level >= StatementLevel.Knot) {
                    rulesAtLevel.Add (() => StitchDefinition());
                }

                // Global variable declarations can go anywhere
                rulesAtLevel.Add(() => Line(VariableDeclaration)());
                rulesAtLevel.Add(() => Line(ConstDeclaration)());

                // Global include can go anywhere
                rulesAtLevel.Add(() => Line(IncludeStatement)());

                // Normal logic / text can go anywhere
                rulesAtLevel.Add(LogicLine);
                rulesAtLevel.Add(() => LineOfMixedTextAndLogic());

                // --------
                // Breaking rules

                // Break current knot with a new knot
                if (level <= StatementLevel.Knot) {
                    breakingRules.Add (() => KnotDeclaration());
                }

                // Break current stitch with a new stitch
                if (level <= StatementLevel.Stitch) {
                    breakingRules.Add (() => StitchDeclaration());
                }

                // Breaking an inner block (like a multi-line condition statement)
                if (level <= StatementLevel.InnerBlock) {
                    breakingRules.Add (() => ParseDashNotArrow());
                    breakingRules.Add (() => String ("}")());
                }

                _statementRulesAtLevel [(int)level] = rulesAtLevel.ToArray ();
                _statementBreakRulesAtLevel [(int)level] = breakingRules.ToArray ();
            }
		}

		protected Empty SkipToNextLine()
		{
			ParseUntilCharactersFromString ("\n\r");
			ParseNewline ();
			return Empty.empty;
		}

		// Modifier to turn a rule into one that expects a newline on the end.
		// e.g. anywhere you can use "MixedTextAndLogic" as a rule, you can use 
		// "Line(MixedTextAndLogic)" to specify that it expects a newline afterwards.
		protected SpecificParseRule<T> Line<T>(SpecificParseRule<T> inlineRule) where T : class
		{
			return () => {
				T result = Parse(inlineRule);
                if (result == null) {
                    return null;
                }

				Expect(EndOfLine, "end of line", recoveryRule: SkipToNextLine);

				return result;
			};
		}


        SpecificParseRule<object>[][] _statementRulesAtLevel;
        SpecificParseRule<object>[][] _statementBreakRulesAtLevel;
	}
}

