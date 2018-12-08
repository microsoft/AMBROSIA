# TODO: should migrate to depend only on 'ambrosia' not 'ambrosia-dev':
FROM ambrosia-dev

# For using Tmux inside the container:
# ------------------------------------------
RUN apt-get install -y tmux
# RUN sed -i -e 's/# en_US.UTF-8 UTF-8/en_US.UTF-8 UTF-8/' /etc/locale.gen && locale-gen
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y locales
RUN sed -i -e 's/# en_US.UTF-8 UTF-8/en_US.UTF-8 UTF-8/' /etc/locale.gen && \
    dpkg-reconfigure --frontend=noninteractive locales && \
    update-locale LANG=en_US.UTF-8
# ----------------------------------------

# This Dockerfile is an example of adding the application
# *piece-by-piece* to enable incremental rebuilds.
ADD IJob /ambrosia/InternalImmortals/PerformanceTestInterruptible/IJob
WORKDIR /ambrosia/InternalImmortals/PerformanceTestInterruptible

# Publish to a local directory:
ENV BUILDIT="dotnet publish -o /ambrosia/InternalImmortals/PerformanceTestInterruptible/bin $BLDFLAGS"
ENV PATH="${PATH}:/ambrosia/InternalImmortals/PerformanceTestInterruptible/bin"

RUN $BUILDIT IJob/IJob.csproj

ADD API /ambrosia/InternalImmortals/PerformanceTestInterruptible/API
RUN $BUILDIT API/ServerAPI.csproj

# Run the code-generation tool:
RUN rm -rf ./GeneratedSourceFiles && \
    mkdir -p GeneratedSourceFiles && \
    mkdir -p ./CodeGenDependencies/netcoreapp2.0
RUN AmbrosiaCS CodeGen -a IJob/bin/Release/netcoreapp2.0/linux-x64/IJob.dll -a API/bin/Release/netcoreapp2.0/linux-x64/ServerAPI.dll -o PTIAmbrosiaGeneratedAPINetCore -f netcoreapp2.0 -b ./CodeGenDependencies/netcoreapp2.0

# publish the api dll whose project we just created:
RUN $BUILDIT GeneratedSourceFiles/PTIAmbrosiaGeneratedAPINetCore/latest/PTIAmbrosiaGeneratedAPINetCore.csproj

# publish the client/server executables that we'll actually run:
ADD Client /ambrosia/InternalImmortals/PerformanceTestInterruptible/Client
ADD Server /ambrosia/InternalImmortals/PerformanceTestInterruptible/Server
RUN $BUILDIT Client/Job.csproj
RUN $BUILDIT Server/Server.csproj

# Add the scripts for invoking PTI inside the container:
ADD run_*.sh            /ambrosia/InternalImmortals/PerformanceTestInterruptible/
ADD Scripts             /ambrosia/InternalImmortals/PerformanceTestInterruptible/Scripts

# ImmortalCoordinator default port:
EXPOSE 1500
# Trying this secondary port as well:
EXPOSE 50500
