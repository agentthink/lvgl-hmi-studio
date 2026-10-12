#include <stdio.h>
#include <stdlib.h>
#include <fcntl.h>
#include <unistd.h>
#include <string.h>
#include <errno.h>
#include <stdint.h>
#include <linux/i2c-dev.h>
#include <sys/ioctl.h>

#define I2C_BUS "/dev/i2c-2"
#define GT911_ADDR_1 0x5D // Primary address after reset (INT high)
#define GT911_ADDR_2 0x14 // Alternate address (INT low)

// GT911 Register addresses
#define GT_REG_STATUS 0x814E // Status register (check for touch data)
#define GT_REG_CONFIG 0x8047 // Config register (to check if active)

int i2c_read(int fd, uint16_t reg, uint8_t *buf, uint16_t len) {
    // I2C write-read operation
    // 1. Write the register address
    uint8_t reg_addr[2] = {(reg >> 8) & 0xFF, reg & 0xFF};
    if (write(fd, reg_addr, 2) != 2) {
        return -1;
    }
    // 2. Read the data
    if (read(fd, buf, len) != len) {
        return -1;
    }
    return 0;
}

int main() {
    int fd;
    uint8_t status_reg[2];
    
    printf("=== GT911 Touch Status Test ===\n");
    
    fd = open(I2C_BUS, O_RDWR);
    if (fd < 0) {
        printf("Failed to open %s: %s\n", I2C_BUS, strerror(errno));
        return -1;
    }
    
    // Test address 0x5D
    printf("Trying address 0x%02X...\n", GT911_ADDR_1);
    if (ioctl(fd, I2C_SLAVE, GT911_ADDR_1) < 0) {
        printf("Failed to set slave 0x%02X: %s\n", GT911_ADDR_1, strerror(errno));
        // Try alternate address
        printf("Trying alternate address 0x%02X...\n", GT911_ADDR_2);
        if (ioctl(fd, I2C_SLAVE, GT911_ADDR_2) < 0) {
            printf("Failed to set slave 0x%02X: %s\n", GT911_ADDR_2, strerror(errno));
            close(fd);
            return -1;
        }
    }
    
    printf("Successfully connected to GT911!\n");
    
    // Read status register in a loop
    printf("\nReading GT911 status register (0x%04X) every 1 second...\n", GT_REG_STATUS);
    printf("Please touch the screen to see status changes.\n");
    printf("Press Ctrl+C to exit.\n\n");
    
    int count = 0;
    while (1) {
        if (i2c_read(fd, GT_REG_STATUS, status_reg, 2) == 0) {
            // GT911 status register format:
            // Byte 1: Reserved
            // Byte 2: Status flags
            //   Bit 0: Status of touch (1 = touch data ready)
            //   Bit 1: Validation bit (must be 1 for valid data)
            
            uint8_t status = status_reg[1];
            int touch_detected = (status & 0x01);
            
            printf("[%03d] Status: 0x%02X | Touch: %s\n", 
                   count, 
                   status, 
                   touch_detected ? "YES" : "NO");
                   
            if (touch_detected) {
                // If touch detected, we can also read coordinates
                // (This is just a basic test, you can extend it)
                uint8_t coord_buf[16];
                if (i2c_read(fd, 0x814E, coord_buf, 16) == 0) {
                    // Coordinates start at 0x8150
                    // ... (can parse touch points here)
                }
            }
        } else {
            printf("[%03d] Error reading register: %s\n", count, strerror(errno));
            // Re-initialize bus
            ioctl(fd, I2C_SLAVE, GT911_ADDR_1);
        }
        
        count++;
        usleep(1000000); // 1 second delay
    }
    
    close(fd);
    return 0;
}
