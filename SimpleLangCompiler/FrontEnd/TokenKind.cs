namespace SimpleLangCompiler.FrontEnd;

public enum TokenKind
{
    // Special
    Eof     = 0,
    NoSym   = 29,

    // Literals/identifiers
    Ident   = 1,
    Number  = 2,
    CharCon = 3,

    // Keywords
    Var = 4,
    Fn = 7,
    If = 14,
    Elseif = 15,
    Else = 16,
    While = 17,
    Return = 18,

    // Punctuation
    Colon     = 5,   // :
    Semicolon = 6,   // ;
    LBrace    = 8,   // {
    RBrace    = 9,   // }
    LParen    = 10,  // (
    Comma     = 11,  // ,
    RParen    = 12,  // )

    // Assignment/Relops
    Assign    = 13,  // =
    Hash      = 19,  // #
    Less      = 20,  // <
    Greater   = 21,  // >
    GreaterEq = 22,  // >=
    LessEq    = 23,  // <=

    // Addops
    Plus      = 24,  // +
    Minus     = 25,  // -

    // Mulops
    Star      = 26,  // *
    Slash     = 27,  // /
    Percent   = 28,  // %
}