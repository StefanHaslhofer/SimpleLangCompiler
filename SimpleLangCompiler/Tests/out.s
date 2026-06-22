.bss
.align 3
i: .space 8
.text
.globl _start
_start:
	j skip
put:
	addi sp, sp, -32
	sd ra, 24(sp)
	sd fp, 16(sp)
	sb a0, 8(sp)
	addi fp, sp, 32
	li a7, 64
	li a0, 1
	addi a1, sp, 8
	li a2, 1
	ecall
put_ret:
	ld fp, 16(sp)
	ld ra, 24(sp)
	addi sp, sp, 32
	ret
putLn:
	addi sp, sp, -32
	sd ra, 24(sp)
	sd fp, 16(sp)
	addi fp, sp, 32
	li t0, 10
	sb t0, 0(sp)
	li a7, 64
	li a0, 1
	addi a1, sp, 0
	li a2, 1
	ecall
putLn_ret:
	ld fp, 16(sp)
	ld ra, 24(sp)
	addi sp, sp, 32
	ret
putInt:
	addi sp, sp, -64
	sd ra, 56(sp)
	sd fp, 48(sp)
	sd a0, 40(sp)
	addi fp, sp, 64
	ld t0, -24(fp)
	li t1, 10
	rem t0, t0, t1
	li t1, 48
	add t1, t1, t0
	mv a0, t1
	andi a0, t1, 0xff
	mv t1, a0
	sb t1, -56(fp)
	ld t1, -24(fp)
	li t0, 10
	div t1, t1, t0
	sd t1, -24(fp)
	ld t1, -24(fp)
	li t0, 10
	rem t1, t1, t0
	li t0, 48
	add t0, t0, t1
	mv a0, t0
	andi a0, t0, 0xff
	mv t0, a0
	sb t0, -48(fp)
	ld t0, -24(fp)
	li t1, 10
	div t0, t0, t1
	sd t0, -24(fp)
	ld t0, -24(fp)
	li t1, 10
	rem t0, t0, t1
	li t1, 48
	add t1, t1, t0
	mv a0, t1
	andi a0, t1, 0xff
	mv t1, a0
	sb t1, -40(fp)
	ld t1, -24(fp)
	li t0, 10
	div t1, t1, t0
	sd t1, -24(fp)
	ld t1, -24(fp)
	li t0, 10
	rem t1, t1, t0
	li t0, 48
	add t0, t0, t1
	mv a0, t0
	andi a0, t0, 0xff
	mv t0, a0
	sb t0, -32(fp)
	lb t0, -32(fp)
	li t1, 48
	ble t0, t1, L1
	lb a0, -32(fp)
	call put
	lb a0, -40(fp)
	call put
	lb a0, -48(fp)
	call put
	j L0
L1:
	lb t1, -40(fp)
	li t0, 48
	ble t1, t0, L2
	lb a0, -40(fp)
	call put
	lb a0, -48(fp)
	call put
	j L0
L2:
	lb t0, -48(fp)
	li t1, 48
	ble t0, t1, L3
	lb a0, -48(fp)
	call put
L3:
L0:
	lb a0, -56(fp)
	call put
putInt_ret:
	ld fp, 48(sp)
	ld ra, 56(sp)
	addi sp, sp, 64
	ret
main:
	addi sp, sp, -16
	sd ra, 8(sp)
	sd fp, 0(sp)
	addi fp, sp, 16
	li t1, 1
	la t0, i
	sd t1, 0(t0)
L4:
	la t1, i
	ld t1, 0(t1)
	li t0, 1000
	bge t1, t0, L5
	la a0, i
	ld a0, 0(a0)
	call putInt
	call putLn
	la t0, i
	ld t0, 0(t0)
	li t1, 2
	add t0, t0, t1
	la t1, i
	sd t0, 0(t1)
	j L4
L5:
main_ret:
	ld fp, 0(sp)
	ld ra, 8(sp)
	addi sp, sp, 16
	ret
skip:
	call main
	li a7, 93
	li a0, 0
	ecall