FROM mcr.microsoft.com/dotnet/sdk:3.1-buster-arm32v7 AS build-env

WORKDIR /app

COPY . ./

RUN set -x \
	&& dotnet restore \
	&& dotnet publish -c Release -r linux-arm --self-contained=false -o out

FROM mcr.microsoft.com/dotnet/aspnet:3.1-buster-slim-arm32v7

WORKDIR /app

COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "DiscordWoL.dll"]
