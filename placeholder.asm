section .text
global _start

_start:
    mov     rax, 42
    xor     rbx, rbx
    add     rbx, rax
    imul    rbx, 7
    sub     rbx, 294

    cmp     rbx, 0
    jne     .something_went_wrong

    mov     rdi, 0
    mov     rax, 60
    syscall

.something_went_wrong:
    mov     rdi, 1
    mov     rax, 60
    syscall
