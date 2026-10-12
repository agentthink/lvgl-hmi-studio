#include <stdio.h>
#include <stdlib.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <linux/serial.h>
#include <errno.h>
#include <string.h>

int main(int argc, char *argv[])
{
    const char *dev = argc > 1 ? argv[1] : "/dev/ttyS3";
    int fd = open(dev, O_RDONLY);
    struct serial_rs485 rs;

    if (fd < 0) {
        printf("open %s failed: %s\n", dev, strerror(errno));
        return 1;
    }
    if (ioctl(fd, TIOCGRS485, &rs) < 0) {
        printf("TIOCGRS485 on %s failed: %s (errno=%d)\n",
               dev, strerror(errno), errno);
        if (errno == ENOTTY)
            printf("  -> ENOTTY: port->rs485_config not set by driver\n");
        close(fd);
        return 1;
    }
    printf("%s RS485: flags=0x%x\n", dev, rs.flags);
    printf("  SER_RS485_ENABLED:      %s\n",
           (rs.flags & SER_RS485_ENABLED) ? "YES" : "no");
    printf("  SER_RS485_RTS_ON_SEND: %s\n",
           (rs.flags & SER_RS485_RTS_ON_SEND) ? "YES" : "no");
    printf("  SER_RS485_RTS_AFTER_SEND: %s\n",
           (rs.flags & SER_RS485_RTS_AFTER_SEND) ? "YES" : "no");
    printf("  SER_RS485_RX_DURING_TX: %s\n",
           (rs.flags & SER_RS485_RX_DURING_TX) ? "YES" : "no");
    printf("  delay_rts_before_send: %d\n", rs.delay_rts_before_send);
    printf("  delay_rts_after_send:  %d\n", rs.delay_rts_after_send);
    close(fd);
    return 0;
}
