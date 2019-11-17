FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build-env
WORKDIR /app

COPY ./Emulsion.sln ./
COPY ./Emulsion/Emulsion.fsproj ./Emulsion/
COPY ./Emulsion.Tests/Emulsion.Tests.fsproj ./Emulsion.Tests/
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.0
WORKDIR /app
COPY --from=build-env /app/Emulsion/out .
ENTRYPOINT ["dotnet", "Emulsion.dll"]
