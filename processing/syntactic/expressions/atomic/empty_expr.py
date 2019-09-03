from __future__ import annotations

from processing.codegen.code_builder import CodeBuilder
from processing.lexical.tokens.token import Token
from processing.lexical.tokens.token_type import TokenType
from processing.location import span_marker
from processing.syntactic.expressions.expr import Expr
from processing.syntactic.expressions.groups import StatementExpression, AtomExpression


class EmptyExpr(StatementExpression, AtomExpression):
    """ expr:
        ';' | 'pass';
    """

    def __init__(
            self,
            parent: Expr = None,
            token: Token = None
    ):
        super().__init__(parent)
        self.token = token

    @span_marker
    def parse(self) -> EmptyExpr:
        self.token = self.stream.eat(
            TokenType.keyword_pass
        )
        return self

    def to_axion(self, c: CodeBuilder):
        c += self.token

    def to_csharp(self, c: CodeBuilder):
        c += ';'

    def to_python(self, c: CodeBuilder):
        c += 'pass'