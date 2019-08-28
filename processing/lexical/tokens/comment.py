from __future__ import annotations

import specification as spec
from errors.blame import BlameType
from processing.codegen.code_builder import CodeBuilder
from processing.lexical.tokens.token import Token
from processing.lexical.tokens.token_type import TokenType
from processing.location import Location, span_marker
from source import SourceUnit


class CommentToken(Token):
    def __init__(
            self,
            source: SourceUnit,
            value = '',
            unclosed = False,
            multiline = False,
            start = Location(0, 0),
            end = Location(0, 0)
    ):
        super().__init__(source, TokenType.comment, value, start = start, end = end)
        self.unclosed = unclosed
        self.multiline = multiline

    @span_marker
    def read_oneline(self) -> CommentToken:
        self.multiline = False
        self.append_next(spec.oneline_comment_mark, error_source = self.source)
        while not self.stream.at_eol:
            self.append_next(content = True)
        return self

    @span_marker
    def read_multiline(self) -> CommentToken:
        self.multiline = True
        self.append_next(spec.multiline_comment_mark, error_source = self.source)
        while not self.stream.peek_is(spec.multiline_comment_mark):
            if self.stream.peek_is(spec.eoc):
                self.source.blame(BlameType.unclosed_multiline_comment, self)
                self.unclosed = True
                return self
            self.append_next(content = True)
        self.append_next(spec.multiline_comment_mark, error_source = self.source)
        return self

    def to_axion(self, c: CodeBuilder):
        if self.multiline:
            c += spec.multiline_comment_mark, self.content
            if not self.unclosed:
                c += spec.multiline_comment_mark
        else:
            c += spec.oneline_comment_mark, self.content

    def to_csharp(self, c: CodeBuilder):
        if self.multiline:
            c += "/*", self.content
            if not self.unclosed:
                c += "*/"
        else:
            c += "//", self.content

    def to_python(self, c: CodeBuilder):
        if self.multiline:
            lines = self.content.split()
            if len(lines) == 1:
                raise NotImplementedError
            else:
                for l in lines:
                    c += '#', l
        else:
            c += '#', self.content
