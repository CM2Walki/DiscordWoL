FROM mcr.microsoft.com/dotnet/sdk:3.1-alpine AS build-env

WORKDIR /app

COPY . ./

RUN set -x \
	&& dotnet restore \
	&& dotnet publish -c Release -r linux-x64 --self-contained=false -o out

FROM mcr.microsoft.com/dotnet/aspnet:3.1-alpine

WORKDIR /app

COPY --from=build-env /app/out .

ENTRYPOINT ["dotnet", "DiscordWoL.dll"]
