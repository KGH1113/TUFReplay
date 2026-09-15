FROM nginx:1.29-alpine

COPY deploy/gateway/nginx.conf /etc/nginx/conf.d/default.conf
# Standard environments have no submission API or update routes.
# The auto-submission Compose file mounts its routes over this empty include.
RUN touch /etc/nginx/auto-submission-locations.conf

EXPOSE 8080
