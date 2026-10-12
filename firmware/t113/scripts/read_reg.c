#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <sys/sysmacros.h>
#include <errno.h>

#define PAGE_SIZE 4096

static int ensure_dev_mem(void)
{
    struct stat st;
    if (stat("/dev/mem", &st) == 0)
        return 0;
    dev_t dev = makedev(1, 1);
    if (mknod("/dev/mem", S_IFCHR | 0660, dev) < 0) {
        fprintf(stderr, "mknod /dev/mem failed: %s\n", strerror(errno));
        return -1;
    }
    printf("created /dev/mem (c 1 1)\n");
    return 0;
}

int main(int argc, char *argv[])
{
    int width = 8;
    int first = 1;
    int i, fd;

    if (argc < 2) {
        fprintf(stderr, "usage: %s [-w8|-w16|-w32] addr [addr2 ...]\n", argv[0]);
        return 1;
    }
    if (strncmp(argv[1], "-w", 2) == 0) {
        width = atoi(argv[1] + 2);
        first = 2;
    }
    if (argc <= first) {
        fprintf(stderr, "need address\n");
        return 1;
    }

    if (ensure_dev_mem() < 0)
        return 1;

    fd = open("/dev/mem", O_RDWR | O_SYNC);
    if (fd < 0) {
        fprintf(stderr, "open /dev/mem failed: %s\n", strerror(errno));
        return 1;
    }
    printf("width=%d bits\n\n", width);

    for (i = first; i < argc; i++) {
        unsigned long addr = strtoul(argv[i], NULL, 0);
        unsigned long page_addr = addr & ~(unsigned long)(PAGE_SIZE - 1);
        unsigned long off = addr & (PAGE_SIZE - 1);
        size_t map_len = PAGE_SIZE;
        volatile unsigned char *map;
        unsigned long val = 0;
        int b;

        if (off + (width / 8) > PAGE_SIZE)
            map_len = PAGE_SIZE * 2;
        map = mmap(NULL, map_len, PROT_READ | PROT_WRITE, MAP_SHARED, fd, page_addr);
        if (map == MAP_FAILED) {
            fprintf(stderr, "mmap 0x%08lx failed: %s\n", addr, strerror(errno));
            continue;
        }
        for (b = 0; b < width / 8; b++)
            val |= ((unsigned long)map[off + b]) << (8 * b);
        printf("0x%08lx = 0x%0*lx", addr, width / 4, val);
        if (width <= 8) {
            printf(" bin:");
            for (b = 7; b >= 0; b--)
                printf("%d", (int)((val >> b) & 1));
        }
        printf("\n");
        munmap((void *)map, map_len);
    }
    close(fd);
    return 0;
}
