namespace HmiEditor.Device.Packaging;

/// <summary>
/// T113-S3 / zhishu-HMI 设备的标准 sw-description 模板。
/// 模板中的 <c>{version}</c> 由 <see cref="SwuPackageBuilder"/> 替换；
/// <c>sha256 = "@name"</c> 由打包器按实际文件计算后替换。
/// </summary>
public static class SwuTemplates
{
    /// <summary>
    /// HMI 程序（libhmi_ui.so）更新模板（带 sha256 校验）：
    /// 单步 raw 写入 /mnt/UDISK/hmi/libhmi_ui.so。
    /// 注意：当前 T113 设备固件的 raw handler 使用 <c>device</c> 字段作为目标路径；
    /// bootenv 不可用时（/dev/by-name/env 缺失）请使用无 bootenv 的简化模板。
    /// </summary>
    public const string HmiUi = """
        software =
        {
            version = "{version}";
            description = "zhishu-HMI UI shared library update (libhmi_ui.so)";

            stable = {

                upgrade_hmi_ui = {
                    images: (
                        {
                            filename = "libhmi_ui.so";
                            type = "raw";
                            device = "/mnt/UDISK/hmi/libhmi_ui.so";
                            sha256 = "@libhmi_ui.so";
                            installed-directly = true;
                        },
                        {
                            filename = "version";
                            type = "raw";
                            device = "/mnt/UDISK/hmi/version";
                            sha256 = "@version";
                            installed-directly = true;
                        }
                    );
                };

            };

        }
        """;

    /// <summary>
    /// HMI 程序（libhmi_ui.so）更新模板（无 sha256 变体）：
    /// 适用于未启用 CONFIG_HASH_VERIFY 的 swupdate（如 T113 当前固件 v2019.11）。
    /// 校验由 CPIO 层 checksum（swupdate 强制）+ 传输后文件大小校验承担。
    /// </summary>
    public const string HmiUiNoHash = """
        software =
        {
            version = "{version}";
            description = "zhishu-HMI UI shared library update (libhmi_ui.so)";

            stable = {

                upgrade_hmi_ui = {
                    images: (
                        {
                            filename = "libhmi_ui.so";
                            type = "raw";
                            device = "/mnt/UDISK/hmi/libhmi_ui.so";
                            installed-directly = true;
                        },
                        {
                            filename = "version";
                            type = "raw";
                            device = "/mnt/UDISK/hmi/version";
                            installed-directly = true;
                        }
                    );
                };

            };

        }
        """;

    /// <summary>
    /// 整固件更新模板：recovery→kernel→usr 三阶段 UBI 卷升级 + 系统 reboot。
    /// 与固件 sw-description-ubi 一致；payload 需包含 recovery/uboot/boot0/kernel/rootfs。
    /// </summary>
    public const string FirmwareUbi = """
        software =
        {
            version = "{version}";
            description = "zhishu-HMI T113-S3 firmware update (SPI NAND, UBI volumes)";

            stable = {

                upgrade_recovery = {
                    images: (
                        {
                            filename = "recovery";
                            volume = "recovery";
                            sha256 = "@recovery";
                            installed-directly = true;
                        },
                        {
                            filename = "uboot";
                            type = "awuboot";
                            sha256 = "@uboot";
                        },
                        {
                            filename = "boot0";
                            type = "awboot0";
                            sha256 = "@boot0";
                        }
                    );
                    bootenv: (
                        {
                            name = "swu_mode";
                            value = "upgrade_kernel";
                        },
                        {
                            name = "boot_partition";
                            value = "recovery";
                        },
                        {
                            name = "swu_next";
                            value = "reboot";
                        }
                    );
                };

                upgrade_kernel = {
                    images: (
                        {
                            filename = "kernel";
                            volume = "boot";
                            sha256 = "@kernel";
                            installed-directly = true;
                        },
                        {
                            filename = "rootfs";
                            volume = "rootfs";
                            sha256 = "@rootfs";
                            installed-directly = true;
                        }
                    );
                    bootenv: (
                        {
                            name = "swu_mode";
                            value = "upgrade_usr";
                        },
                        {
                            name = "boot_partition";
                            value = "boot";
                        }
                    );
                };

                upgrade_usr = {
                    bootenv: (
                        {
                            name = "swu_param";
                            value = "";
                        },
                        {
                            name = "swu_software";
                            value = "";
                        },
                        {
                            name = "swu_mode";
                            value = "";
                        },
                        {
                            name = "swu_next";
                            value = "reboot";
                        }
                    );
                };

            };

            bootenv: (
                {
                    name = "swu_param";
                    value = "";
                },
                {
                    name = "swu_software";
                    value = "";
                },
                {
                    name = "swu_mode";
                    value = "";
                },
                {
                    name = "swu_version";
                    value = "";
                }
            );

        }
        """;

    /// <summary>
    /// 整固件更新模板（无 sha256 变体）：
    /// 适用于未启用 CONFIG_HASH_VERIFY 的 swupdate（如 T113 当前固件 v2019.11）。
    /// 校验由 CPIO 层 checksum（swupdate 强制）+ 传输后文件大小校验承担。
    /// </summary>
    public const string FirmwareUbiNoHash = """
        software =
        {
            version = "{version}";
            description = "zhishu-HMI T113-S3 firmware update (SPI NAND, UBI volumes)";

            stable = {

                upgrade_recovery = {
                    images: (
                        {
                            filename = "recovery";
                            volume = "recovery";
                            installed-directly = true;
                        },
                        {
                            filename = "uboot";
                            type = "awuboot";
                        },
                        {
                            filename = "boot0";
                            type = "awboot0";
                        }
                    );
                    bootenv: (
                        {
                            name = "swu_mode";
                            value = "upgrade_kernel";
                        },
                        {
                            name = "boot_partition";
                            value = "recovery";
                        },
                        {
                            name = "swu_next";
                            value = "reboot";
                        }
                    );
                };

                upgrade_kernel = {
                    images: (
                        {
                            filename = "kernel";
                            volume = "boot";
                            installed-directly = true;
                        },
                        {
                            filename = "rootfs";
                            volume = "rootfs";
                            installed-directly = true;
                        }
                    );
                    bootenv: (
                        {
                            name = "swu_mode";
                            value = "upgrade_usr";
                        },
                        {
                            name = "boot_partition";
                            value = "boot";
                        }
                    );
                };

                upgrade_usr = {
                    bootenv: (
                        {
                            name = "swu_param";
                            value = "";
                        },
                        {
                            name = "swu_software";
                            value = "";
                        },
                        {
                            name = "swu_mode";
                            value = "";
                        },
                        {
                            name = "swu_next";
                            value = "reboot";
                        }
                    );
                };

            };

            bootenv: (
                {
                    name = "swu_param";
                    value = "";
                },
                {
                    name = "swu_software";
                    value = "";
                },
                {
                    name = "swu_mode";
                    value = "";
                },
                {
                    name = "swu_version";
                    value = "";
                }
            );

        }
        """;

    /// <summary>固件包内镜像的 CPIO 名（与模板 filename 一致）。</summary>
    public static readonly IReadOnlyList<string> FirmwareImageNames =
        new[] { "recovery", "uboot", "boot0", "kernel", "rootfs" };
}
