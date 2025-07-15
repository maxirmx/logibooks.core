FROM postgres:17-alpine

# Install cron
RUN apk add --no-cache dcron dos2unix

# Copy backup script
COPY backup.sh /usr/local/bin/backup.sh
RUN chmod +x /usr/local/bin/backup.sh
RUN dos2unix /usr/local/bin/backup.sh

COPY healthcheck.sh /usr/local/bin/healthcheck.sh
RUN chmod +x /usr/local/bin/healthcheck.sh
RUN dos2unix /usr/local/bin/healthcheck.sh


# Create crontab file
COPY crontab /etc/crontabs/root
RUN dos2unix /etc/crontabs/root

# Create backup directory
RUN mkdir -p /backups

# Start cron daemon
CMD ["crond", "-f", "-d", "8"]
