fn add(a: int, b: int): int {
    return a + b;
}

fn loop(x: int, y: int, z: int): int {
    while (x <= y) {
        z = z + add(x, y);
        x = x + 1;
    }
  
    return z;
}
   
fn main() {
    var x: int;
    var y: int;
    var z: int;
    var ret: int;
    x = 1;
    y = 4;
    z = loop(x, y, z);
}