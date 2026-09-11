FROM mcr.microsoft.com/dotnet/sdk:8.0 AS development
RUN apt-get update && \
    apt-get install -y --no-install-recommends ffmpeg && \
    rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY Dionysus.Api/Dionysus.Api.csproj Dionysus.Api/
RUN dotnet restore Dionysus.Api/Dionysus.Api.csproj
COPY Dionysus.Api/ Dionysus.Api/
EXPOSE 8000
CMD ["dotnet", "watch", "--project", "Dionysus.Api/Dionysus.Api.csproj", "run", "--urls", "http://0.0.0.0:8000"]

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Dionysus.Api/Dionysus.Api.csproj Dionysus.Api/
RUN dotnet restore Dionysus.Api/Dionysus.Api.csproj
COPY Dionysus.Api/ Dionysus.Api/
RUN dotnet publish Dionysus.Api/Dionysus.Api.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:8.0
ARG INSTALL_FFMPEG=true
RUN if [ "$INSTALL_FFMPEG" = "true" ]; then \
      apt-get update && \
      apt-get install -y --no-install-recommends ffmpeg && \
      rm -rf /var/lib/apt/lists/*; \
    fi
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8000
ENTRYPOINT ["dotnet", "Dionysus.Api.dll", "--urls", "http://0.0.0.0:8000"]
