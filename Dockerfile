# FROM microsoft/dotnet:2.1-sdk
# FROM microsoft/dotnet:2.0.9-sdk-2.1.202
# FROM microsoft/dotnet:2.0-sdk
# FROM microsoft/dotnet:2.2-sdk-2.2.108  -- want this version
# FROM microsoft/dotnet:2.2-sdk
# FROM microsoft/dotnet:3.1-sdk -- no longer proper string format
FROM mcr.microsoft.com/dotnet/core/sdk:3.1

RUN apt-get update -y && \
    apt-get install -y libunwind-dev apache2-utils make gcc

# Add only what we need, and add late to minimize rebuilds during development:
ADD AzureBlobsLogPicker           /ambrosia/AzureBlobsLogPicker
ADD GenericLogPicker           /ambrosia/GenericLogPicker
ADD SharedAmbrosiaTools           /ambrosia/SharedAmbrosiaTools
ADD ImmortalCoordinator           /ambrosia/ImmortalCoordinator
ADD AmbrosiaLib           /ambrosia/AmbrosiaLib
ADD Ambrosia                      /ambrosia/Ambrosia
ADD DevTools                      /ambrosia/DevTools
WORKDIR /ambrosia

ENV AMBROSIA_DOTNET_FRAMEWORK=netcoreapp3.1 \
    AMBROSIA_DOTNET_FRAMEWORK_20=netstandard2.0 \
    AMBROSIA_DOTNET_CONF=Release \
    AMBROSIA_DOTNET_PLATFORM=linux-x64 

# This is the command we use to build each of the individual C# projects:
ENV BLDFLAGS " -c Release -f $AMBROSIA_DOTNET_FRAMEWORK -r $AMBROSIA_DOTNET_PLATFORM "
ENV BUILDIT "dotnet publish $BLDFLAGS"

ENV BLDFLAGS_20 " -c Release -f $AMBROSIA_DOTNET_FRAMEWORK_20 -r $AMBROSIA_DOTNET_PLATFORM "
ENV BUILDIT_20 "dotnet publish $BLDFLAGS_20"

# NOTE: use the following for a debug build of AMBROSIA:
# ENV BLDFLAGS " -c Debug -f netcoreapp3.1 -r linux-x64 -p:DefineConstants=DEBUG "

RUN echo "****************************************** 1 Starting ********************************"
# (1) Language binding: CSharp (depends on AmbrosiaLibCS on nuget)
# ----------------------------------------------------------------
ADD Clients/CSharp                /ambrosia/Clients/CSharp
RUN $BUILDIT -o /ambrosia/bin/codegen Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj && \
    cd bin && ln -s codegen/AmbrosiaCS 

RUN echo "****************************************** 2 Starting ********************************"

# (2) Build the core executables and libraries:
# ---------------------------------------------
RUN $BUILDIT_20 -o /ambrosia/bin/AzureBlobsLogPicker       AzureBlobsLogPicker/AzureBlobsLogPicker.csproj
RUN $BUILDIT_20 -o /ambrosia/bin/GenericLogPicker       GenericLogPicker/GenericLogPicker.csproj
RUN $BUILDIT_20 -o /ambrosia/bin/SharedAmbrosiaTools       SharedAmbrosiaTools/SharedAmbrosiaTools.csproj
RUN $BUILDIT -o /ambrosia/bin/runtime     Ambrosia/Ambrosia/Ambrosia.csproj
RUN $BUILDIT_20 -o /ambrosia/bin/AmbrosiaLib       AmbrosiaLib/Ambrosia/AmbrosiaLib.csproj
RUN $BUILDIT -o /ambrosia/bin/coord       ImmortalCoordinator/ImmortalCoordinator.csproj
RUN $BUILDIT -o /ambrosia/bin/unsafedereg DevTools/UnsafeDeregisterInstance/UnsafeDeregisterInstance.csproj

RUN cd bin && \
    ln -s runtime/Ambrosia Ambrosia && \
    #ln -s AzureBlobsLogPicker/AzureBlobsLogPicker && \
    #ln -s GenericLogPicker/GenericLogPicker && \
    #ln -s SharedAmbrosiaTools/SharedAmbrosiaTools && \
    ln -s coord/ImmortalCoordinator && \ 
    ln -s unsafedereg/UnsafeDeregisterInstance

RUN echo "****************************************** 2B Starting ********************************"

# (2B) Reduce the size of our dotnet binary distribution:
ADD ./Scripts/dedup_bindist.sh Scripts/
RUN du -sch ./bin && \
    ./Scripts/dedup_bindist.sh && \
    du -sch ./bin


RUN echo "****************************************** 3 Starting ********************************"
# (3) Low-level Native-code network client:
# -----------------------------------------
ADD Clients/C                     /ambrosia/Clients/C
# This publishes to the build directory: bin/lib*.* and bin/include
RUN cd Clients/C && make debug # publish


RUN echo "****************************************** 4 Starting ********************************"
# (4) A script used by apps to start the ImmortalCoordinator:
# -----------------------------------------------------------
ADD ./Scripts/runAmbrosiaService.sh bin/

RUN echo "****************************************** 4a Starting ********************************"
# We currently use this as a baseline source of dependencies for generated code:
ADD ./Clients/CSharp/AmbrosiaCS/AmbrosiaCS.csproj  bin/AmbrosiaCS.csproj

# Remove unnecessary execute permissions:
# RUN cd bin && (chmod -x *.dll *.so *.dylib *.a 2>/dev/null || echo ok)

RUN echo "****************************************** 4b Starting ********************************"
# Make "ambrosia", "AmbrosiaCS", and "ImmortalCoordinator" available on PATH:
ENV AMBROSIA_BINDIR="/ambrosia/bin" \
    PATH="${PATH}:/ambrosia/bin"
RUN echo "****************************************** Docker Done  ********************************"