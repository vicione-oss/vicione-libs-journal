FROM mcr.microsoft.com/dotnet/sdk:9.0

LABEL maintainer="ViciOne" \
      description="Development container for ViciOne.Journal with systemd support"

# Install systemd, required packages, and useful dev tools in a single layer
RUN apt-get update && apt-get install -y --no-install-recommends \
    systemd \
    systemd-sysv \
    libsystemd-dev \
    dbus \
    git \
    curl \
    procps \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/* \
    # Remove unnecessary systemd services that won't work in a container
    && rm -f /lib/systemd/system/multi-user.target.wants/* \
        /etc/systemd/system/*.wants/* \
        /lib/systemd/system/local-fs.target.wants/* \
        /lib/systemd/system/sockets.target.wants/*udev* \
        /lib/systemd/system/sockets.target.wants/*initctl* \
        /lib/systemd/system/sysinit.target.wants/systemd-tmpfiles-setup* \
        /lib/systemd/system/systemd-update-utmp* \
    # Set up persistent journal storage with proper permissions
    && mkdir -p /var/log/journal \
    && chmod 2755 /var/log/journal \
    # Enable journald via symlink (systemctl not available during build)
    && ln -sf /lib/systemd/system/systemd-journald.service \
        /etc/systemd/system/multi-user.target.wants/systemd-journald.service

VOLUME ["/sys/fs/cgroup"]

STOPSIGNAL SIGRTMIN+3

CMD ["/lib/systemd/systemd"]
