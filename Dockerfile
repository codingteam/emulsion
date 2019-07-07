FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env
WORKDIR /app

COPY ./Emulsion.sln ./
COPY ./Emulsion/Emulsion.fsproj ./Emulsion/
COPY ./Emulsion.Tests/Emulsion.Tests.fsproj ./Emulsion.Tests/
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:2.2
WORKDIR /app
COPY --from=build-env /app/Emulsion/out .
COPY ./emulsion.json ./
ENTRYPOINT ["dotnet", "Emulsion.dll"]
