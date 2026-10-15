#!/bin/sh
# Writes the runtime config read by the SPAs (see public/config.js); runs on every container start.
echo "window.TRANSBRAIN_CONFIG = { authority: \"${OIDC_AUTHORITY}\" };" > /usr/share/nginx/html/config.js
