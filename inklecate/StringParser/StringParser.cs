using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Text;

namespace Ink
{
    internal class StringParser
    {
        public delegate object ParseRule();

        public delegate T SpecificParseRule<T>() where T : class;

        public delegate void ErrorHandler(string message, int index, int lineIndex, bool isWarning);

        public StringParser(string str)
        {
            str = PreProcessInputString(str);

            state = new StringParserState();

            if (str != null) {
                _chars = str.ToCharArray();
            } else {
                _chars = new char[0];
            }

            inputString = str;
        }


        public static CharacterSet numbersCharacterSet = new CharacterSet("0123456789");

        protected ErrorHandler errorHandler { get; set; }

        public char currentCharacter
        {
            get
            {
                if (index >= 0 && remainingLength > 0) {
                    return _chars[index];
                } else {
                    return (char)0;
                }
            }
        }

        public StringParserState state { get; private set; }

        public bool hadError { get; protected set; }

        // Don't do anything by default, but provide ability for subclasses
        // to manipulate the string before it's used as input (converted to a char array)
        protected virtual string PreProcessInputString(string str)
        {
            return str;
        }

        //--------------------------------
        // Parse state
        //--------------------------------

        protected int BeginRule()
        {
            return state.Push();
        }

        protected object FailRule(int expectedRuleId)
        {
            state.Pop(expectedRuleId);
            return null;
        }

        protected void CancelRule(int expectedRuleId)
        {
            state.Pop(expectedRuleId);
        }

        protected Option<T> SucceedRule<T>(int expectedRuleId, T result = null) where T : class
        {
            // Get state at point where this rule stared evaluating
            var stateAtSucceedRule = state.Peek(expectedRuleId);
            var stateAtBeginRule = state.PeekPenultimate();


            // Allow subclass to receive callback
            RuleDidSucceed(result, stateAtBeginRule, stateAtSucceedRule);

            // Flatten state stack so that we maintain the same values,
            // but remove one level in the stack.
            state.Squash();

            if (result == null) {
                return Option<T>.parseSuccess();
            }

            return new Option<T>(result);
        }

        protected virtual void RuleDidSucceed(object result, StringParserState.Element startState, StringParserState.Element endState)
        {

        }

        protected T Expect<T>(SpecificParseRule<T> rule, string message = null, SpecificParseRule<T> recoveryRule = null) where T : class
        {
            T result = Parse<T>(rule);
            if (result == null) {
                if (message == null) {
                    message = rule.Method.Name;
                }

                string butSaw;
                string lineRemainder = LineRemainder();
                if (lineRemainder == null || lineRemainder.Length == 0) {
                    butSaw = "end of line";
                } else {
                    butSaw = "'" + lineRemainder + "'";
                }

                Error("Expected " + message + " but saw " + butSaw);

                if (recoveryRule != null) {
                    result = recoveryRule();
                }
            }
            return result;
        }

        protected void Error(string message, bool isWarning = false)
        {
            ErrorOnLine(message, lineIndex + 1, isWarning);
        }

        protected void ErrorWithParsedObject(string message, Parsed.Object result, bool isWarning = false)
        {
            ErrorOnLine(message, result.debugMetadata.startLineNumber, isWarning);
        }

        protected void ErrorOnLine(string message, int lineNumber, bool isWarning)
        {
            if (!state.errorReportedAlreadyInScope) {

                var errorType = isWarning ? "Warning" : "Error";

                if (errorHandler == null) {
                    Console.WriteLine(errorType + " on line " + lineNumber + ": " + message);
                } else {
                    errorHandler(message, index, lineNumber - 1, isWarning);
                }

                state.NoteErrorReported();
            }

            if (!isWarning)
                hadError = true;
        }

        protected void Warning(string message)
        {
            Error(message, isWarning: true);
        }

        public bool endOfInput
        {
            get { return index >= _chars.Length; }
        }

        public string remainingString
        {
            get {
                return new string(_chars, index, remainingLength);
            }
        }

        public string LineRemainder()
        {
            return (string)Peek(() => ParseUntilCharactersFromString("\n\r"));
        }

        public int remainingLength
        {
            get {
                return _chars.Length - index;
            }
        }

        public string inputString { get; private set; }


        public int lineIndex
        {
            set {
                state.lineIndex = value;
            }
            get {
                return state.lineIndex;
            }
        }

        public int index
        {
            // If we want subclass parsers to be able to set the index directly,
            // then we would need to know what the lineIndex of the new
            // index would be - would we have to step through manually
            // counting the newlines to do so?
            private set {
                state.characterIndex = value;
            }
            get {
                return state.characterIndex;
            }
        }

        public void SetFlag(uint flag, bool trueOrFalse) {
            if (trueOrFalse) {
                state.customFlags |= flag;
            } else {
                state.customFlags &= ~flag;
            }
        }

        public bool GetFlag(uint flag) {
            return (state.customFlags & flag) != 0;
        }

        //--------------------------------
        // Structuring
        //--------------------------------

        public object ParseObject(ParseRule rule)
        {
            int ruleId = BeginRule();

            var stackHeightBefore = state.stackHeight;

            var result = rule();

            if (stackHeightBefore != state.stackHeight) {
                throw new System.Exception("Mismatched Begin/Fail/Succeed rules");
            }

            if (result == null)
                return FailRule(ruleId);

            SucceedRule(ruleId, result);
            return result;
        }

        public T Parse<T>(SpecificParseRule<T> rule) where T : class
        {
            int ruleId = BeginRule();

            var result = rule();
            if (result == null) {
                FailRule(ruleId);
                return null;
            }

            SucceedRule(ruleId, result);
            return result;
        }

        public T OneOf<T>(params SpecificParseRule<T>[] array) where T : class
        {
            foreach (SpecificParseRule<T> rule in array) {
                T result = Parse<T>(rule);
                if (result != null)
                    return result;
            }

            return null;
        }

        public List<T> OneOrMore<T>(SpecificParseRule<T> rule) where T : class
        {
            var results = new List<T>();

            T result = null;
            do {
                result = Parse<T>(rule);
                if (result != null) {
                    results.Add(result);
                }
            } while (result != null);

            if (results.Count > 0) {
                return results;
            } else {
                return null;
            }
        }

        public SpecificParseRule<Option<T>> Optional<T>(SpecificParseRule<T> rule) where T : class
        {
            return () => {
                T result = Parse<T>(rule);
                if (result == null) {
                    return Option<T>.parseSuccess();
                }
                return new Option<T>(result);
            };
        }

        // Return ParseSuccess instead the real result so that it gets excluded
        // from result arrays (e.g. Interleave)
        public SpecificParseRule<Empty> Exclude<T>(SpecificParseRule<T> rule) where T : class
        {
            return () => {
                T result = Parse<T>(rule);
                if (result == null) {
                    return null;
                }
                return Empty.empty;
            };
        }

        // Combination of both of the above
        public SpecificParseRule<Option<T>> OptionalExclude<T>(SpecificParseRule<T> rule) where T : class
        {
            return () => {
                Parse<T>(rule);
                return Option<T>.parseSuccess();
            };
        }

        // Convenience method for creating more readable ParseString rules that can be combined
        // in other structuring rules (like OneOf etc)
        // e.g. OneOf(String("one"), String("two"))
        protected SpecificParseRule<String> String(string str)
        {
            return () => ParseString(str);
        }

        public void LegacyTryAddResultToList<T>(object result, List<T> list, bool flatten = true) where T : class
        {
            if (result is Empty)
                return;
            var checkForOption = result as Option<T>;
            if (checkForOption != null)
            {
                if (checkForOption.empty)
                    return;
                result = checkForOption.getValue();
            }
            var checkForOption2 = result as Option<List<T>>;
            if (checkForOption2 != null)
            {
                if (checkForOption2.empty)
                    return;
                result = checkForOption2.getValue();
            }

            if (flatten)
            {
                var resultCollection = result as System.Collections.ICollection;
                if (resultCollection != null)
                {
                    foreach (object obj in resultCollection)
                    {
                        Debug.Assert(obj is T);
                        list.Add((T)obj);
                    }
                    return;
                }
            }
            else
            {
                Debug.Print("Called TryAddResult with flatten enabled but unable to flatten");
            }


            Debug.Assert(result is T);
            list.Add((T)result);
        }

        private void TryAddResultToList<T>(Option<ICollection<T>> result, List<T> list) where T : class {
            if (result.empty) {
                return;
            }

            var resultCollection = result.getValue();
            if (resultCollection != null) {
                foreach (T obj in resultCollection) {
                    list.Add(obj);
                }
            }
        }

        public void TryAddResultToList<T>(Option<T> result, List<T> list) where T : class
        {
            if (result.empty)
            {
                return;
            }
            list.Add(result.getValue());
        }

        public void AddResultToList<T>(T result, List<T> list)
        {
            list.Add(result);
        }

        public bool AlwaysTrue<T, K>(T a, K b) { return true; }

        public bool DefaultOptionalCond<T,K> (Option<T> a, Option<K> b) where T : class where K : class
        { return !(a.empty && b.empty); } 

        public List<T> Interleave<T>(SpecificParseRule<T> ruleA, SpecificParseRule<Empty> ruleB, SpecificParseRule<object> untilTerminator = null) where T : class
        {
            return Interleave<T, Empty, T>(ruleA, AddResultToList, ruleB, Helpers.doNothing, AlwaysTrue,untilTerminator);
        }

        public List<T> Interleave<T>(SpecificParseRule<T> ruleA, SpecificParseRule<T> ruleB, SpecificParseRule<object> untilTerminator = null ) where T : class
		{
            return Interleave<T,T,T>(ruleA, AddResultToList, ruleB, AddResultToList, AlwaysTrue, untilTerminator);
        }

        public List<T> Interleave<T>(SpecificParseRule<Option<T>> ruleA, SpecificParseRule<Option<T>> ruleB, SpecificParseRule<object> untilTerminator = null) where T : class
        {
            return Interleave<Option<T>, Option<T>,T>(ruleA,TryAddResultToList,ruleB,TryAddResultToList, DefaultOptionalCond, untilTerminator);
        }

        public List<J> Interleave<T, K, J>(SpecificParseRule<T> ruleA, Action<T, List<J>> mergeA, SpecificParseRule<K> ruleB, Action<K, List<J>> mergeB, Func<T, K, bool> cond, SpecificParseRule<object> untilTerminator = null) where T : class where J : class where K : class
        {
            int ruleId = BeginRule();

            var results = new List<J>();

            // First outer padding
            var firstA = Parse<T>(ruleA);
            if (firstA == null)
            {
                FailRule(ruleId);
                return null;
            }
            else
            {
                mergeA(firstA, results);
            }

            K lastMainResult = null;
            T outerResult = null;
            do
            {

                // "until" condition hit?
                if (untilTerminator != null && Peek(untilTerminator) != null)
                {
                    break;
                }

                // Main inner
                lastMainResult = Parse(ruleB);
                if (lastMainResult == null)
                {
                    break;
                }
                else
                {
                    mergeB(lastMainResult, results);
                }

                // Outer result (i.e. last A in ABA)
                outerResult = null;
                if (lastMainResult != null)
                {
                    outerResult = Parse(ruleA);
                    if (outerResult == null)
                    {
                        break;
                    }
                    else
                    {
                        mergeA(outerResult, results);
                    }
                }

                // Stop if there are no results, or if both are the placeholder "ParseSuccess" (i.e. Optional success rather than a true value)
            } while ((lastMainResult != null || outerResult != null)
                    && cond(outerResult, lastMainResult) && remainingLength > 0);
            if (results.Count == 0)
            {
                FailRule(ruleId);
                return null;
            }
            SucceedRule(ruleId, results);
            return results;
        }


        //--------------------------------
        // Basic string parsing
        //--------------------------------

        public string ParseString(string str)
		{
			if (str.Length > remainingLength) {
				return null;
			}

            int ruleId = BeginRule ();

            // Optimisation from profiling:
            // Store in temporary local variables
            // since they're properties that would have to access
            // the rule stack every time otherwise.
            int i = index;
            int li = lineIndex;

			bool success = true;
			foreach (char c in str) {
				if ( _chars[i] != c) {
					success = false;
					break;
				}
                if (c == '\n') {
                    li++;
                }
				i++;
			}

            index = i;
            lineIndex = li;

			if (success) {
                // this was one line before, resulting in a potential cast from ParseSuccessStruct to String
                SucceedRule(ruleId, str);
                return str;
			}
			else {
                FailRule(ruleId);
                return null;
			}
		}

        public char ParseSingleCharacter()
        {
            if (remainingLength > 0) {
                char c = _chars [index];
                if (c == '\n') {
                    lineIndex++;
                }
                index++;
                return c;
            } else {
                return (char)0;
            }
        }

		public string ParseUntilCharactersFromString(string str, int maxCount = -1)
		{
			return ParseCharactersFromString(str, false, maxCount);
		}

		public string ParseUntilCharactersFromCharSet(CharacterSet charSet, int maxCount = -1)
		{
			return ParseCharactersFromCharSet(charSet, false, maxCount);
		}

		public string ParseCharactersFromString(string str, int maxCount = -1)
		{
			return ParseCharactersFromString(str, true, maxCount);
		}

		public string ParseCharactersFromString(string str, bool shouldIncludeStrChars, int maxCount = -1)
		{
			return ParseCharactersFromCharSet (new CharacterSet(str), shouldIncludeStrChars);
		}

		public string ParseCharactersFromCharSet(CharacterSet charSet, bool shouldIncludeChars = true, int maxCount = -1)
		{
			if (maxCount == -1) {
				maxCount = int.MaxValue;
			}

			int startIndex = index;

            // Optimisation from profiling:
            // Store in temporary local variables
            // since they're properties that would have to access
            // the rule stack every time otherwise.
            int i = index;
            int li = lineIndex;

			int count = 0;
            while ( i < _chars.Length && charSet.Contains (_chars [i]) == shouldIncludeChars && count < maxCount ) {
                if (_chars [i] == '\n') {
                    li++;
                }
                i++;
				count++;
			}

            index = i;
            lineIndex = li;

			int lastCharIndex = index;
			if (lastCharIndex > startIndex) {
				return new string (_chars, startIndex, index - startIndex);
			} else {
				return null;
			}
		}

        public T Peek<T>(SpecificParseRule<T> rule) where T : class
		{
			int ruleId = BeginRule ();
			T result = rule ();
            CancelRule (ruleId);
			return result;
		}

		public string ParseUntil<T>(SpecificParseRule<T> stopRule, CharacterSet pauseCharacters = null, CharacterSet endCharacters = null) where T : class
		{
			int ruleId = BeginRule ();

			
			CharacterSet pauseAndEnd = new CharacterSet ();
			if (pauseCharacters != null) {
				pauseAndEnd.UnionWith (pauseCharacters);
			}
			if (endCharacters != null) {
				pauseAndEnd.UnionWith (endCharacters);
			}

			StringBuilder parsedString = new StringBuilder ();
			T ruleResultAtPause = null;

			// Keep attempting to parse strings up to the pause (and end) points.
			//  - At each of the pause points, attempt to parse according to the rule
			//  - When the end point is reached (or EOF), we're done
			do {

				// TODO: Perhaps if no pause or end characters are passed, we should check *every* character for stopRule?
				string partialParsedString = ParseUntilCharactersFromCharSet(pauseAndEnd);
				if( partialParsedString != null ) {
					parsedString.Append(partialParsedString);
				}

				// Attempt to run the parse rule at this pause point
				ruleResultAtPause = Peek(stopRule);

				// Rule completed - we're done
				if( ruleResultAtPause != null ) {
					break;
				} else {

					if( endOfInput ) {
						break;
					}

					// Reached a pause point, but rule failed. Step past and continue parsing string
					char pauseCharacter = currentCharacter;
					if( pauseCharacters != null && pauseCharacters.Contains(pauseCharacter) ) {
						parsedString.Append(pauseCharacter);
                        if( pauseCharacter == '\n' ) {
                            lineIndex++;
                        }
						index++;
						continue;
					} else {
						break;
					}
				}

			} while(true);

			if (parsedString.Length > 0) {
                var str = parsedString.ToString();
                SucceedRule(ruleId, str);
                return str;
			} else {
                return (string) FailRule (ruleId);
			}

		}

        // No need to Begin/End rule since we never parse a newline, so keeping oldIndex is good enough
		public int? ParseInt()
		{
			int oldIndex = index;

			bool negative = ParseString ("-") != null;

			// Optional whitespace
			ParseCharactersFromString (" \t");

			var parsedString = ParseCharactersFromCharSet (numbersCharacterSet);
			int parsedInt;
			if (int.TryParse (parsedString, out parsedInt)) {
				return negative ? -parsedInt : parsedInt;
			}

			// Roll back and fail
			index = oldIndex;
			return null;
		}

        // No need to Begin/End rule since we never parse a newline, so keeping oldIndex is good enough
        public float? ParseFloat()
        {
            int oldIndex = index;

            int? leadingInt = ParseInt ();
            if (leadingInt != null) {

                if (ParseString (".") != null) {

                    var afterDecimalPointStr = ParseCharactersFromCharSet (numbersCharacterSet);
                    return float.Parse (leadingInt+"." + afterDecimalPointStr, System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            // Roll back and fail
            index = oldIndex;
            return null;
        }

        // You probably want "endOfLine", since it handles endOfFile too.
        protected string ParseNewline()
        {
            int ruleId = BeginRule();

            // Optional \r, definite \n to support Windows (\r\n) and Mac/Unix (\n)
            // 2nd May 2016: Always collapse \r\n to just \n
            ParseString ("\r");

            if( ParseString ("\n") == null ) {
                FailRule(ruleId);
                return null;
            } else {
                string str = "\n";
                SucceedRule(ruleId, str);
                return str;
            }
        }

		private char[] _chars;
	}
}

