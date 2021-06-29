FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

COPY ./Emulsion.sln ./
COPY ./Emulsion/Emulsion.fsproj ./Emulsion/
COPY ./Emulsion.Tests/Emulsion.Tests.fsproj ./Emulsion.Tests/
RUN dotnet restore

COPY . ./
RUN dotnet publish Emulsion -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/runtime:5.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "Emulsion.dll"]
