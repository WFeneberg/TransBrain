# Keycloak with the production realm baked in (no users: create them in the admin console).
# Build context: deploy/keycloak
FROM quay.io/keycloak/keycloak:26.4
COPY transbrain-realm.json /opt/keycloak/data/import/transbrain-realm.json
ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--import-realm"]
