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
fact:
	addi sp, sp, -32
	sd ra, 24(sp)
	sd fp, 16(sp)
	sd a0, 8(sp)
	addi fp, sp, 32
	ld t0, -24(fp)
	li t1, 1
	bgt t0, t1, L1
	li a0, 1
	j fact_ret
L1:
L0:
	ld t1, -24(fp)
	li t0, 1
	sub t1, t1, t0
	call fact
	mv t0, a0
	ld t2, -24(fp)
	mul t2, t2, t0
	mv a0, t2
	j fact_ret
fact_ret:
	ld fp, 16(sp)
	ld ra, 24(sp)
	addi sp, sp, 32
	ret
main:
	addi sp, sp, -32
	sd ra, 24(sp)
	sd fp, 16(sp)
	addi fp, sp, 32
	li a0, 3
	call fact
	mv t2, a0
	sd t2, -24(fp)
main_ret:
	ld fp, 16(sp)
	ld ra, 24(sp)
	addi sp, sp, 32
	ret
skip:
	call main
	li a7, 93
	li a0, 0
	ecall