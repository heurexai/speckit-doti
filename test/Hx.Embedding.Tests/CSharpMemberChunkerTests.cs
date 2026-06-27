using Hx.Embedding;
using Xunit;

namespace Hx.Embedding.Tests;

/// <summary>
/// FR-013 / arch-review M2: the lexer-aware C# member chunker. It splits a <c>.cs</c> source into one chunk per
/// type/member and — crucially — its hand-rolled scanner masks string/char/comment spans BEFORE counting braces, so a
/// <c>{</c>/<c>}</c> hidden inside a string or comment never mis-splits a chunk. Deterministic over the same input.
/// </summary>
public sealed class CSharpMemberChunkerTests
{
    private static string OneLine(string s) => s.Replace("\r\n", "\n");

    [Fact]
    public void Splits_a_class_into_one_chunk_per_member()
    {
        const string src = """
            namespace Demo;

            public sealed class Calculator
            {
                private int _state;

                public int Add(int a, int b)
                {
                    return a + b;
                }

                public int Subtract(int a, int b)
                {
                    return a - b;
                }
            }
            """;

        var chunks = CSharpMemberChunker.Chunk("src/Calculator.cs", src);

        // The field, both methods, and the class-close are distinct chunks.
        Assert.Contains(chunks, c => c.Text.Contains("int Add"));
        Assert.Contains(chunks, c => c.Text.Contains("int Subtract"));
        Assert.Contains(chunks, c => c.Text.Contains("_state"));
        // Add and Subtract land in DIFFERENT chunks (the member boundary held).
        Assert.DoesNotContain(chunks, c => c.Text.Contains("int Add") && c.Text.Contains("int Subtract"));
    }

    [Fact]
    public void Does_not_split_on_a_brace_inside_a_string_literal()
    {
        // The '}' inside the format string must NOT close the method body early.
        const string src = """
            public class S
            {
                public string Format(string name)
                {
                    var brace = "}";
                    var open = "{ not a real block";
                    return $"hello {name} }} done";
                }

                public int After() => 42;
            }
            """;

        var chunks = CSharpMemberChunker.Chunk("S.cs", src);

        // Format's body (including the string braces) is ONE chunk that does not bleed into After().
        var format = Assert.Single(chunks, c => c.Text.Contains("public string Format"));
        Assert.Contains("\"}\"", format.Text);
        Assert.Contains("{ not a real block", format.Text);
        Assert.DoesNotContain("public int After", format.Text); // After is its own chunk
        Assert.Contains(chunks, c => c.Text.Contains("public int After"));
    }

    [Fact]
    public void Does_not_split_on_a_brace_inside_a_line_or_block_comment()
    {
        const string src = """
            public class C
            {
                // a stray brace } in a line comment must not close the body {
                public void M()
                {
                    /* another } brace { inside a block comment */
                    DoWork();
                }

                public void N() { }
            }
            """;

        var chunks = CSharpMemberChunker.Chunk("C.cs", src);

        var m = Assert.Single(chunks, c => c.Text.Contains("public void M"));
        Assert.Contains("DoWork();", m.Text);
        Assert.DoesNotContain("public void N", m.Text);
        Assert.Contains(chunks, c => c.Text.Contains("public void N"));
    }

    [Fact]
    public void Does_not_split_on_a_brace_inside_a_verbatim_or_raw_string()
    {
        const string src = """"
            public class V
            {
                public string Verbatim() => @"a } brace { and a "" quote";

                public string Raw()
                {
                    return """
                        a raw string with } and { braces
                        """;
                }

                public int Tail() => 1;
            }
            """";

        var chunks = CSharpMemberChunker.Chunk("V.cs", src);

        Assert.Contains(chunks, c => c.Text.Contains("public string Verbatim"));
        var raw = Assert.Single(chunks, c => c.Text.Contains("public string Raw"));
        Assert.Contains("a raw string with } and { braces", raw.Text);
        Assert.DoesNotContain("public int Tail", raw.Text);
        Assert.Contains(chunks, c => c.Text.Contains("public int Tail"));
    }

    [Fact]
    public void Does_not_split_on_a_brace_inside_a_char_literal()
    {
        const string src = """
            public class Ch
            {
                public char Open() => '{';
                public char Close() => '}';
                public int Z() => 0;
            }
            """;

        var chunks = CSharpMemberChunker.Chunk("Ch.cs", src);

        // Each expression-bodied member ends at its ';' — the char-literal braces never moved depth.
        Assert.Contains(chunks, c => c.Text.Contains("Open") && c.Text.Contains("'{'"));
        Assert.Contains(chunks, c => c.Text.Contains("Close") && c.Text.Contains("'}'"));
        Assert.Contains(chunks, c => c.Text.Contains("public int Z"));
        Assert.DoesNotContain(chunks, c => c.Text.Contains("Open") && c.Text.Contains("public int Z"));
    }

    [Fact]
    public void Attaches_leading_attributes_and_doc_comments_to_the_member()
    {
        const string src = """
            public class A
            {
                /// <summary>Adds two numbers.</summary>
                [Obsolete]
                public int Add(int a, int b) => a + b;

                /// <summary>The current state.</summary>
                public int State { get; set; }
            }
            """;

        var chunks = CSharpMemberChunker.Chunk("A.cs", src);

        var add = Assert.Single(chunks, c => c.Text.Contains("public int Add"));
        Assert.Contains("Adds two numbers", add.Text);     // doc-comment travels with the member
        Assert.Contains("[Obsolete]", add.Text);            // attribute travels with the member
        Assert.DoesNotContain("public int State", add.Text);
    }

    [Fact]
    public void Records_struct_record_interface_and_enum_as_chunks()
    {
        const string src = """
            public enum Color { Red, Green, Blue }

            public interface IShape
            {
                double Area();
            }

            public readonly record struct Point(int X, int Y);

            public struct Box
            {
                public int Width;
            }
            """;

        var chunks = CSharpMemberChunker.Chunk("Types.cs", src);

        Assert.Contains(chunks, c => c.Text.Contains("enum Color"));
        Assert.Contains(chunks, c => c.Text.Contains("interface IShape"));
        Assert.Contains(chunks, c => c.Text.Contains("record struct Point"));
        Assert.Contains(chunks, c => c.Text.Contains("struct Box"));
    }

    [Fact]
    public void Is_deterministic_over_the_same_input()
    {
        const string src = """
            public class D
            {
                public int A() => 1;
                public int B() => 2;
            }
            """;

        var first = CSharpMemberChunker.Chunk("D.cs", src);
        var second = CSharpMemberChunker.Chunk("D.cs", src);

        Assert.Equal(first.Count, second.Count);
        for (int i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Text, second[i].Text);
            Assert.Equal(first[i].Label, second[i].Label);
        }
    }

    [Fact]
    public void Chunk_text_round_trips_into_the_source_verbatim()
    {
        const string src = """
            public class R
            {
                public int A() => 1;
                public int B() => 2;
            }
            """;

        var chunks = CSharpMemberChunker.Chunk("R.cs", src);

        // Every chunk's text is a contiguous substring of the source (the chunker never re-renders).
        foreach (var chunk in chunks)
        {
            Assert.Contains(OneLine(chunk.Text), OneLine(src));
        }
    }

    [Fact]
    public void A_file_of_only_usings_yields_a_single_whole_document_chunk()
    {
        const string src = """
            using System;
            using System.Text;
            // no declarations here
            """;

        var chunks = CSharpMemberChunker.Chunk("Usings.cs", src);

        var only = Assert.Single(chunks);
        Assert.Contains("using System;", only.Text);
    }

    [Fact]
    public void Empty_source_yields_no_chunks()
    {
        Assert.Empty(CSharpMemberChunker.Chunk("Empty.cs", string.Empty));
    }

    [Fact]
    public void Label_carries_the_file_name_and_a_declaration_hint()
    {
        const string src = """
            public class L
            {
                public int Add(int a, int b) => a + b;
            }
            """;

        var chunks = CSharpMemberChunker.Chunk("src/deep/L.cs", src);

        Assert.All(chunks, c => Assert.StartsWith("L.cs", c.Label)); // file name, not the full path
        Assert.Contains(chunks, c => c.Label.Contains("Add"));        // declaration hint, skipping attributes/docs
    }
}
