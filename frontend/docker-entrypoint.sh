#!/bin/sh
# Overwrite env.js with runtime values so the image isn't URL-coupled at build time
cat > /usr/share/nginx/html/env.js <<EOF
window.__ENV__ = { "API_URL": "${API_URL:-}" };
EOF

exec nginx -g 'daemon off;'
