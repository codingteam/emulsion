FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-env

# Install Node.js 18
RUN curl -fsSL https://deb.nodesource.com/setup_18.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY ./Emulsion/Emulsion.fsproj ./Emulsion/
COPY ./Emulsion.ContentProxy/Emulsion.ContentProxy.fsproj ./Emulsion.ContentProxy/
COPY ./Emulsion.Database/Emulsion.Database.fsproj ./Emulsion.Database/
COPY ./Emulsion.Messaging/Emulsion.Messaging.fsproj ./Emulsion.Messaging/
COPY ./Emulsion.MessageArchive.Frontend/Emulsion.MessageArchive.Frontend.proj ./Emulsion.MessageArchive.Frontend/
COPY ./Emulsion.Settings/Emulsion.Settings.fsproj ./Emulsion.Settings/
COPY ./Emulsion.Telegram/Emulsion.Telegram.fsproj ./Emulsion.Telegram/
COPY ./Emulsion.Web/Emulsion.Web.fsproj ./Emulsion.Web/

RUN dotnet restore Emulsion

COPY . ./
RUN dotnet build Emulsion.MessageArchive.Frontend # required to publish the frontend resources
RUN dotnet publish Emulsion -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "Emulsion.dll"]
