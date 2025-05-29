FROM swipl:latest
WORKDIR /app
COPY game.pl .
EXPOSE 8080
CMD ["swipl", "-g", "start_server(8080)", "-t", "halt", "game.pl"]