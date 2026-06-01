fn foo(): int {
    return 65;
}

fn bar(a: int, b: int): int {
    return a + b;        
}

fn main() {
    var x: int;
    x = foo() + bar(1, 2);
    put(CHR(x));
}