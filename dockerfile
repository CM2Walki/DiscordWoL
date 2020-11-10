FROM mcr.microsoft.com/dotnet/core/aspnet:3.1.9
COPY . ./app
WORKDIR /app
ENTRYPOINT ["dotnet", "DiscordWoL.dll"]