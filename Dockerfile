FROM mcr.microsoft.com/dotnet/sdk:8.0.401
ARG servicename
WORKDIR /app
COPY out/$servicename .