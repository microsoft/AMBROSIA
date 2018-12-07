# FROM microsoft/dotnet:2.1-sdk
# FROM microsoft/dotnet:2.0.9-sdk-2.1.202
FROM microsoft/dotnet:2.0-sdk

RUN apt-get update -y && \
    apt-get install -y libunwind-dev apache2-utils make gcc

# Add only what we need, and add late to minimize rebuilds during development:
ADD ImmortalCoordinator           /ambrosia/ImmortalCoordinator
ADD Ambrosia                      /ambrosia/Ambrosia
ADD DevTools                      /ambrosia/DevTools
WORKDIR /ambrosia

# This is the command we use to build each of the individual C# projects:
ENV BLDFLAGS " -c Release -f netcoreapp2.0 -r linux-x64 "
ENV BUILDIT "dotnet publish -o /ambrosia/bin $BLDFLAGS"
# NOTE: use the following for a debug build of AMBROSIA:
# ENV BLDFLAGS " -c Debug -f netcoreapp2.0 -r linux-x64 -p:DefineConstants=DEBUG "

# (1) Build the core executables and libraries:
# ---------------------------------------------
RUN $BUILDIT Ambrosia/Ambrosia/Ambrosia.csproj
RUN $BUILDIT ImmortalCoordinator/ImmortalCoordinator.csproj
RUN $BUILDIT DevTools/UnsafeDeregisterInstance/UnsafeDeregisterInstance.csproj

# (2) Language binding: CSharp (depends on AmbrosiaLibCS on nuget)
# ----------------------------------------------------------------
ADD Clients/CSharp                /ambrosia/Clients/CSharp
RUN $BUILDIT Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj

# (3) Low-level Native-code network client:
# -----------------------------------------
ADD Clients/C                     /ambrosia/Clients/C
# This publishes to the build directory: bin/lib*.* and bin/include
RUN cd Clients/C && make publish

# (4) A script used by apps to start the ImmortalCoordinator:
# -----------------------------------------------------------
ADD ./Scripts/runAmbrosiaService.sh bin/
RUN cd bin && ln -s Ambrosia ambrosia

# Make "ambrosia", "AmbrosiaCS", and "ImmortalCoordinator" available on PATH:
ENV AMBROSIA_BINDIR="/ambrosia/bin" \
    PATH="${PATH}:/ambrosia/bin"
