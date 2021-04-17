FROM mono
ADD ./AmaknaProxy.ProtocolBuilder /src
RUN msbuild src/ProtocolBuilder.csproj /p:Configuration=Release
WORKDIR /files
ENTRYPOINT [ "mono", "/src/bin/Release/ProtocolBuilder.exe" ]
