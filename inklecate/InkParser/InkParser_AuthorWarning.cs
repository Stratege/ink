using Ink.Parsed;

namespace Ink
{
    internal partial class InkParser
    {
        protected AuthorWarning AuthorWarning()
        {
            IgnoredWhitespace ();

            if (Parse (Identifier) != "TODO")
                return null;

            IgnoredWhitespace();

            ParseString (":");

            IgnoredWhitespace();

            var message = ParseUntilCharactersFromString ("\n\r");

            return new AuthorWarning (message);
        }

    }
}

