using System.Collections.Generic;
using System.Linq;
using Ink.Parsed;

namespace Ink
{
    internal partial class InkParser
    {
        protected Sequence InnerSequence()
        {
            IgnoredWhitespace();

            // Default sequence type
            SequenceType seqType = SequenceType.Stopping;

            // Optional explicit sequence type
            SequenceType? parsedSeqType = (SequenceType?) Parse(SequenceTypeAnnotation);
            if (parsedSeqType != null)
                seqType = (SequenceType) parsedSeqType;

            var contentLists = Parse(InnerSequenceObjects);
            if (contentLists == null || contentLists.Count <= 1) {
                return null;
            }

            return new Sequence (contentLists, seqType);
        }

        protected object SequenceTypeAnnotation()
        {
            var symbolAnnotation = Parse(SequenceTypeSymbolAnnotation);
            if (symbolAnnotation != null)
                return symbolAnnotation;

            var wordAnnotation = Parse(SequenceTypeWordAnnotation);
            if (wordAnnotation != null)
                return wordAnnotation;

            return null;
        }

        protected object SequenceTypeSymbolAnnotation()
        {
            var symbol = ParseSingleCharacter ();

            switch (symbol) {
            case '!':
                return SequenceType.Once;
            case '&':
                return SequenceType.Cycle;
            case '~':
                return SequenceType.Shuffle;
            case '$':
                return SequenceType.Stopping;
            }

            return null;
        }

        protected object SequenceTypeWordAnnotation()
        {
            SequenceType? seqType = null;

            var word = Parse(Identifier);
            switch (word) {
            case "once":
                seqType = SequenceType.Once;
                break;
            case "cycle":
                seqType = SequenceType.Cycle;
                break;
            case "shuffle":
                seqType = SequenceType.Shuffle;
                break;
            case "stopping":
                seqType = SequenceType.Stopping;
                break;
            }

            if (seqType == null)
                return null;

            IgnoredWhitespace();

            if (ParseString (":") == null)
                return null;

            return seqType;
        }

        protected List<ContentList> InnerSequenceObjects()
        {
            var multiline = Parse(Newline) != null;

            List<ContentList> result = null;
            if (multiline) {
                result = Parse(InnerMultilineSequenceObjects);
            } else {
                result = Parse(InnerInlineSequenceObjects);
            }

            return result;
        }

        protected List<ContentList> InnerInlineSequenceObjects()
        {
            var interleavedContentAndPipes = Interleave<Option<List<Object>>,string,Either<List<Object>,string>> (Optional (MixedTextAndLogic), (x,xs) => TryAddResultToList(x.lift(Either<List<Object>,string>.Left),xs), String ("|"), (x,xs) => xs.Add(Either<List<Object>, string>.Right(x)),AlwaysTrue);
            if (interleavedContentAndPipes == null)
                return null;

            var result = new List<ContentList> ();

            // The content and pipes won't necessarily be perfectly interleaved in the sense that
            // the content can be missing, but in that case it's intended that there's blank content.
            bool justHadContent = false;
            foreach (var contentOrPipe in interleavedContentAndPipes) {

                // Pipe/separator
                if (!contentOrPipe.IsLeft() && contentOrPipe.GetRight() == "|") {

                    // Expected content, saw pipe - need blank content now
                    if (!justHadContent) {

                        // Add blank content
                        result.Add (new ContentList ());
                    }

                    justHadContent = false;
                } 

                // Real content
                else {
                    if (contentOrPipe == null)
                        Error("Expected content, but got " + "null" + " (this is an ink compiler bug!)");
                    else if (!contentOrPipe.IsLeft()) {
                        Error ("Expected content, but got " + "string" + " (this is an ink compiler bug!)");
                    } else {
                        result.Add (new ContentList (contentOrPipe.GetLeft()));
                    }

                    justHadContent = true;
                }
            }

            // Ended in a pipe? Need to insert final blank content
            if (!justHadContent)
                result.Add (new ContentList ());
                
            return result;
        }

        protected List<ContentList> InnerMultilineSequenceObjects()
        {
            MultilineWhitespace ();

            var contentLists = OneOrMore (SingleMultilineSequenceElement);
            if (contentLists == null)
                return null;

            return contentLists.Cast<ContentList> ().ToList();
        }

        protected ContentList SingleMultilineSequenceElement()
        {
            IgnoredWhitespace();

            // Make sure we're not accidentally parsing a divert
            if (ParseString ("->") != null)
                return null;

            if (ParseString ("-") == null)
                return null;

            IgnoredWhitespace();


            List<Parsed.Object> content = StatementsAtLevel (StatementLevel.InnerBlock);

            if( content == null ) 
                MultilineWhitespace ();

            return new ContentList (content);
        }
    }
}

