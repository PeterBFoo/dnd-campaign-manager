FROM grafana/alloy:v1.19.2

COPY config.alloy /etc/alloy/config.alloy

ENTRYPOINT ["alloy"]
CMD ["run", "--storage.path=/var/lib/alloy/data", "--disable-reporting", "/etc/alloy/config.alloy"]
