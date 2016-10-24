using System.Collections.Generic;

namespace Ink
{
	internal partial class InkParser
	{
		// Handles both newline and endOfFile
		protected object EndOfLine()
		{
            return OneOf(Newline, EndOfFile);
		}

        // Allow whitespace before the actual newline
        protected Empty Newline()
        {
            IgnoredWhitespace();

            bool gotNewline = ParseNewline () != null;

            // Optional \r, definite \n to support Windows (\r\n) and Mac/Unix (\n)

            if( !gotNewline ) {
                return null;
            } else {
                return Empty.empty;
            }
        }

		protected Empty EndOfFile()
		{
            IgnoredWhitespace();

            if (!endOfInput)
                return null;

            return Empty.empty;
		}


		// General purpose space, returns N-count newlines (fails if no newlines)
		protected Empty MultilineWhitespace()
		{
            List<Empty> newlines = OneOrMore(Newline);
            if (newlines == null)
                return null;

			// Use content field of Token to say how many newlines there were
			// (in most circumstances it's unimportant)
			int numNewlines = newlines.Count;
			if (numNewlines >= 1) {
                return Empty.empty;
			} else {
                return null;
			}
		}

		protected Option<T> Whitespace<T>() where T : class
		{
			if( ParseCharactersFromCharSet(_inlineWhitespaceChars) != null ) {
				return Option<T>.parseSuccess();
			}

			return null;
		}

        protected void IgnoredWhitespace()
        {
            ParseCharactersFromCharSet(_inlineWhitespaceChars);
        }

        protected SpecificParseRule<T> Spaced<T>(SpecificParseRule<T> rule) where T : class
        {
            return () => {

                IgnoredWhitespace();

                var result = Parse(rule);
                if (result == null) {
                    return null;
                }

                IgnoredWhitespace();

                return result;
            };
        }

		private CharacterSet _inlineWhitespaceChars = new CharacterSet(" \t");
	}
}

