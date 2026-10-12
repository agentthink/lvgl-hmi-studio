# Tina Linux 固件编译容器 - WavePI-S6 (T113-S3) 专用
# 基于 Debian bullseye，包含完整的 Tina-SDK 构建环境

FROM debian:bullseye

# 设置环境变量
ENV DEBIAN_FRONTEND=noninteractive \
    TZ=Asia/Shanghai \
    LANG=C.UTF-8 \
    LC_ALL=C.UTF-8

# 安装系统依赖
RUN apt-get update && apt-get install -y --no-install-recommends \
    # 基础工具链
    build-essential \
    autoconf \
    automake \
    libtool \
    make \
    cmake \
    ninja-build \
    bc \
    flex \
    bison \
    
    # 编译器工具链
    gcc-arm-linux-gnueabihf \
    g++-arm-linux-gnueabihf \
    binutils-arm-linux-gnueabihf \
    
    # Python 3
    python3 \
    python3-pip \
    python3-dev \
    
    # Git 和版本控制
    git \
    git-lfs \
    subversion \
    
    # 设备树编译器
    device-tree-compiler \
    
    # 文件系统工具
    u-boot-tools \
    mtd-utils \
    
    # 压缩工具
    xz-utils \
    zstd \
    zip \
    unzip \
    tar \
    gzip \
    
    # USB 工具
    libusb-1.0-0-dev \
    android-tools-adb \
    android-tools-fastboot \
    
    # 调试工具
    gdb-multiarch \
    strace \
    lsof \
    
    # 其他工具
    wget \
    curl \
    rsync \
    cpio \
    file \
    
    # 清理
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* /tmp/* /var/tmp/*

# 创建工作目录
WORKDIR /workspace

# 创建必要的目录结构
RUN mkdir -p /workspace/Tina \
    /workspace/out \
    /workspace/tools \
    /workspace/patches \
    /workspace/configs

# 复制构建脚本和配置
COPY build.sh /workspace/
COPY docker-entrypoint.sh /workspace/
RUN chmod +x /workspace/build.sh /workspace/docker-entrypoint.sh

# 设置入口点
ENTRYPOINT ["/workspace/docker-entrypoint.sh"]
CMD ["bash"]

# 持久化卷
VOLUME ["/workspace/Tina", "/workspace/out"]

# 暴露 ADB 端口
EXPOSE 5037

# 环境变量
ENV TINA_TOP=/workspace/Tina \
    OUT_DIR=/workspace/out \
    ARCH=arm \
    CROSS_COMPILE=arm-linux-gnueabihf-

# 健康检查
HEALTHCHECK --interval=30s --timeout=3s \
    CMD test -f /workspace/Tina/build/envsetup.sh || exit 1