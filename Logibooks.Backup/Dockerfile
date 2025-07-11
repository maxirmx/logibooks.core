FROM postgres:17-alpine

# Install cron
RUN apk add --no-cache dcron

# Copy backup script
COPY backup.sh /usr/local/bin/backup.sh
RUN chmod +x /usr/local/bin/backup.sh

# Create crontab file
COPY crontab /etc/crontabs/root

# Create backup directory
RUN mkdir -p /backups

# Start cron daemon
CMD ["crond", "-f", "-d", "8"]
