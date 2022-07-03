FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY ./Emulsion/Emulsion.fsproj ./Emulsion/
COPY ./Emulsion.ContentProxy/Emulsion.ContentProxy.fsproj ./Emulsion.ContentProxy/
COPY ./Emulsion.Database/Emulsion.Database.fsproj ./Emulsion.Database/
COPY ./Emulsion.Settings/Emulsion.Settings.fsproj ./Emulsion.Settings/
COPY ./Emulsion.Web/Emulsion.Web.fsproj ./Emulsion.Web/

RUN dotnet restore Emulsion

COPY . ./
RUN dotnet publish Emulsion -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "Emulsion.dll"]
