using Ink.Parsed;
using System.Diagnostics;

namespace Ink
{
	internal partial class InkParser
	{
		protected Choice Choice()
		{
            SpecificParseRule<Option<string>> optionalExcludedWhitespace = () => Option<string>.flatten(OptionalExclude(Whitespace<string>)());
            bool onceOnlyChoice = true;
            var bullets = Interleave<Option<string>,string,string>(optionalExcludedWhitespace, TryAddResultToList, String("*"), AddResultToList, (a,b) => true);
            if (bullets == null) {

                bullets = Interleave<Option<string>, string, string>(optionalExcludedWhitespace, TryAddResultToList, String("+"), AddResultToList, (a,b) => true);
                if (bullets == null) {
                    return null;
                }

                onceOnlyChoice = false;
            }
                
            // Optional name for the choice
            string optionalName = Parse(BracketedName);

            IgnoredWhitespace();

            // Optional condition for whether the choice should be shown to the player
            Expression conditionExpr = Parse(ChoiceCondition);

            IgnoredWhitespace();

            // Ordinarily we avoid parser state variables like these, since
            // nesting would require us to store them in a stack. But since you should
            // never be able to nest choices within choice content, it's fine here.
            Debug.Assert(_parsingChoice == false, "Already parsing a choice - shouldn't have nested choices");
            _parsingChoice = true;
                
            ContentList startContent = null;
            var startTextAndLogic = Parse (MixedTextAndLogic);
            if (startTextAndLogic != null)
                startContent = new ContentList (startTextAndLogic);


            ContentList optionOnlyContent = null;
            ContentList innerContent = null;

            // Check for a the weave style format:
            //   * "Hello[."]," he said.
            bool hasWeaveStyleInlineBrackets = ParseString("[") != null;
            if (hasWeaveStyleInlineBrackets) {

                var optionOnlyTextAndLogic = Parse (MixedTextAndLogic);
                if (optionOnlyTextAndLogic != null)
                    optionOnlyContent = new ContentList (optionOnlyTextAndLogic);
                

                Expect (String("]"), "closing ']' for weave-style option");

                var innerTextAndLogic = Parse (MixedTextAndLogic);
                if( innerTextAndLogic != null )
                    innerContent = new ContentList (innerTextAndLogic);
            }

            _parsingChoice = false;
             
            // Trim
            if( innerContent )
                TrimChoiceContent (ref innerContent);
            else if( optionOnlyContent )
                TrimChoiceContent (ref optionOnlyContent);
            else 
                TrimChoiceContent (ref startContent);

            if (innerContent != null) {
                innerContent.AddContent (new Text ("\n"));
            }

            bool isDefaultChoice = startContent == null && optionOnlyContent == null;
                
			IgnoredWhitespace ();

            var divert =  Parse(SingleDivert);

            IgnoredWhitespace ();

            // Completely empty choice?
            if (!startContent && !optionOnlyContent && !innerContent && !divert) {
                Warning ("Choice is completely empty. Interpretting as a default fallback choice. Add a divert arrow to remove this warning: * ->");
            }

            var tags = Parse (Tags);
            if (tags != null) {
                if (hasWeaveStyleInlineBrackets) {
                    innerContent.AddContent (tags);
                } else {
                    startContent.AddContent (tags);
                }
            }

            var choice = new Choice (startContent, optionOnlyContent, innerContent, divert);
            choice.name = optionalName;
            choice.indentationDepth = bullets.Count;
            choice.hasWeaveStyleInlineBrackets = hasWeaveStyleInlineBrackets;
            choice.condition = conditionExpr;
            choice.onceOnly = onceOnlyChoice;
            choice.isInvisibleDefault = isDefaultChoice;

            return choice;

		}

        void TrimChoiceContent(ref ContentList content)
        {
            if (content != null) {
                content.TrimTrailingWhitespace ();
                if (content.content.Count == 0) {
                    content = null;
                }
            }
        }
            
        protected Expression ChoiceCondition()
        {
            var conditions = Interleave<Expression> (ChoiceSingleCondition,() => ChoiceConditionsSpace());
            if (conditions == null)
                return null;
            else if (conditions.Count == 1)
                return conditions [0];
            else {
                return new MultipleConditionExpression (conditions);
            }
        }
    
        protected Empty ChoiceConditionsSpace()
        {
            // Both optional
            // Newline includes initial end of line whitespace
            Newline ();
            IgnoredWhitespace();
            return Empty.empty;
        }

        protected Expression ChoiceSingleCondition()
        {
            if (ParseString ("{") == null)
                return null;

            var condExpr = Expect(Expression, "choice condition inside { }") as Expression;

            Expect (String ("}"), "closing '}' for choice condition");

            return condExpr;
        }

        protected Gather Gather()
        {
            object gatherDashCountObj = Parse(GatherDashes);
            if (gatherDashCountObj == null) {
                return null;
            }

            int gatherDashCount = (int)gatherDashCountObj;

            // Optional name for the gather
            string optionalName = Parse(BracketedName);

            var gather = new Gather (optionalName, gatherDashCount);

            // Optional newline before gather's content begins
            Newline ();

            return gather;
        }

        protected object GatherDashes()
        {
            IgnoredWhitespace ();

            int gatherDashCount = 0;

            while (ParseDashNotArrow () != null) {
                gatherDashCount++;
                IgnoredWhitespace();
            }

            if (gatherDashCount == 0)
                return null;

            return gatherDashCount;
        }

        protected Empty ParseDashNotArrow()
        {
            var ruleId = BeginRule ();

            if (ParseString ("->") == null && ParseSingleCharacter () == '-') {
                SucceedRule<object>(ruleId);
                return Empty.empty;
            } else {
                FailRule(ruleId);
                return null;
            }
        }

        protected string BracketedName()
        {
            if (ParseString ("(") == null)
                return null;

            IgnoredWhitespace();

            string name = Parse(Identifier);
            if (name == null)
                return null;

            IgnoredWhitespace();

            Expect (String (")"), "closing ')' for bracketed name");

            return name;
        }

        bool _parsingChoice;
	}
}

