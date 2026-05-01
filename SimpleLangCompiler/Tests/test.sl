var i: int;

fn putInt(x: int): void { /* largest printable number = 9999 */
    var c0: char;
    var c1: char;
    var c2: char;
    var c3: char;

    c3 = CHR(48 + x % 10); x = x / 10;
    c2 = CHR(48 + x % 10); x = x / 10;
    c1 = CHR(48 + x % 10); x = x / 10;
    c0 = CHR(48 + x % 10);

    if (c0 > '0') { put(c0); put(c1); put(c2); }
    elseif (c1 > '0') { put(c1); put(c2); }
    elseif (c2 > '0') { put(c2); }
    put(c3);
}

fn main(): void { /* print odd numbers */
    i = 1;
    while (i < 100) {
        putInt(i);
        putLn();
        i = i + 2;
    }
}
