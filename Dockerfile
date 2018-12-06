# FROM microsoft/dotnet:2.1-sdk
# FROM microsoft/dotnet:2.0.9-sdk-2.1.202
FROM microsoft/dotnet:2.0-sdk

RUN apt-get update -y && \
    apt-get install -y libunwind-dev apache2-utils
# netcat telnet net-tools lsof

ENV BLDFLAGS " -c Release -f netcoreapp2.0 -r linux-x64 "

# NOTE: use the following for a debug build of AMBROSIA:
# ENV BLDFLAGS " -c Debug -f netcoreapp2.0 -r linux-x64 -p:DefineConstants=DEBUG "

# Fine-grained version:
ADD Clients/CSharp/AmbrosiaCS     /ambrosia/Clients/CSharp/AmbrosiaCS
ADD Clients/CSharp/AmbrosiaLibCS  /ambrosia/Clients/CSharp/AmbrosiaLibCS
ADD ImmortalCoordinator           /ambrosia/ImmortalCoordinator
ADD Ambrosia                      /ambrosia/Ambrosia
ADD DevTools                      /ambrosia/DevTools
WORKDIR /ambrosia

# This is the command we use to build each of the individual C# projects:
ENV BUILDIT "dotnet publish -o /ambrosia/bin $BLDFLAGS"

RUN $BUILDIT Ambrosia/Ambrosia/Ambrosia.csproj
RUN $BUILDIT ImmortalCoordinator/ImmortalCoordinator.csproj
RUN $BUILDIT Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj
RUN $BUILDIT DevTools/UnsafeDeregisterInstance/UnsafeDeregisterInstance.csproj

ADD ./AKS-scripts/ScriptBits/runAmbrosiaService.sh bin/
RUN cd bin && ln -s Ambrosia ambrosia

ENV AMBROSIA_BINDIR="/ambrosia/bin"
ENV PATH="${PATH}:/ambrosia/bin"
