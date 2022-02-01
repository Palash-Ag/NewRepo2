FROM mcr.microsoft.com/dotnet/core/aspnet:3.1

COPY bin/Release/netcoreapp3.1/publish/ App/
WORKDIR /App
Expose 80
ENTRYPOINT ["dotnet", "TodoApi.dll"]